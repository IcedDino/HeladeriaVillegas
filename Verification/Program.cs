using HeladeriaPOS.Data;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using Microsoft.EntityFrameworkCore;

string path = Path.Combine(Path.GetTempPath(), "heladeria_verify_" + Guid.NewGuid().ToString("N") + ".db");
string legacyPath = Path.Combine(Path.GetTempPath(), "heladeria_legacy_" + Guid.NewGuid().ToString("N") + ".db");
FileSystem.AppDataDirectory = Path.Combine(Path.GetTempPath(), "heladeria_appdata_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(FileSystem.AppDataDirectory);
var options = new DbContextOptionsBuilder<PosDbContext>().UseSqlite($"Data Source={path}").Options;
var factory = new ContextFactory(options);
var initializer = new DatabaseInitializer(factory);
var tickets = new TicketService(factory);
try
{
    await initializer.InitializeAsync();
    List<Product> products = await tickets.GetProductsAsync();
    Check(products.Count >= 12, "Catálogo inicial");
    Check((await tickets.GetFlavorsAsync(true)).Count >= 3, "Sabores iniciales");
    Product vaso = products.Single(p => p.ProductType == ProductType.Vaso);
    ProductPrice medium = vaso.Prices.Single(p => p.Code == "Mediano");
    await tickets.SavePriceAsync(medium.Id, 47m);
    await tickets.SetProductActiveAsync(vaso.Id, false);
    await initializer.InitializeAsync();
    Product changed = (await tickets.GetAllProductsAsync()).Single(p => p.Id == vaso.Id);
    Check(!changed.IsActive && changed.Prices.Single(p => p.Code == "Mediano").Amount == 47m, "Reinicio conserva precios y disponibilidad");
    PricingResult calculated = new PricingService().Calculate(changed, new ProductSelection { IceCreamSize = IceCreamSize.Mediano, ExtraScoops = 1 });
    Check(calculated.BasePrice == 47m && calculated.Modifiers.Sum(m => m.Total) == 20m, "Cobro usa precio guardado");
    var ticket = new Ticket { OrderNumber = "VERIFY-" + Guid.NewGuid().ToString("N"), Status = TicketStatus.Paid,
        PaidAt = DateTime.Now, Total = 67m, SubtotalBase = 47m, TotalExtras = 20m, Received = 100m, Change = 33m,
        Items = [new OrderItem { ProductId = changed.Id, ProductName = changed.Name, BaseUnitPrice = 47m,
            Flavors = "Chocolate", Instructions = "Sin nuez", Modifiers = calculated.Modifiers }] };
    await tickets.SaveTicketAsync(ticket);
    Ticket saved = (await tickets.GetTicketsAsync(DateTime.Today)).Single(t => t.OrderNumber == ticket.OrderNumber);
    Check(saved.Items.Single().Flavors == "Chocolate" && saved.Items.Single().Modifiers.Count == 1, "Venta conserva preparación");
    await tickets.CancelTicketAsync(saved.Id, "Prueba de caja");
    Check((await tickets.GetTicketsAsync(DateTime.Today)).Single(t => t.Id == saved.Id).Status == TicketStatus.Cancelled, "Cancelación auditada");
    using (var old = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={legacyPath}"))
    {
        old.Open();
        using var command = old.CreateCommand();
        command.CommandText = """
            CREATE TABLE Products (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Category INTEGER NOT NULL, ProductType INTEGER NOT NULL, BasePrice INTEGER NOT NULL, ImagePath TEXT NULL, Tag TEXT NULL, IsActive INTEGER NOT NULL);
            CREATE TABLE Tickets (Id INTEGER PRIMARY KEY AUTOINCREMENT, OrderNumber TEXT NOT NULL, CreatedAt TEXT NOT NULL, PaidAt TEXT NULL, Status INTEGER NOT NULL, SubtotalBase INTEGER NOT NULL, TotalExtras INTEGER NOT NULL, Discount INTEGER NOT NULL, Total INTEGER NOT NULL, Received INTEGER NOT NULL, Change INTEGER NOT NULL);
            CREATE TABLE OrderItems (Id INTEGER PRIMARY KEY AUTOINCREMENT, TicketId INTEGER NOT NULL, ProductId INTEGER NOT NULL, ProductName TEXT NOT NULL, BaseUnitPrice INTEGER NOT NULL, Quantity INTEGER NOT NULL, SelectedVariant TEXT NULL);
            CREATE TABLE Modifiers (Id INTEGER PRIMARY KEY AUTOINCREMENT, OrderItemId INTEGER NOT NULL, Type INTEGER NOT NULL, Name TEXT NOT NULL, UnitPrice INTEGER NOT NULL, Quantity INTEGER NOT NULL);
            INSERT INTO Products (Name,Category,ProductType,BasePrice,ImagePath,Tag,IsActive) VALUES ('Barquillo',1,3,9900,'product_barquillo.jpg','Anterior',0);
            """;
        command.ExecuteNonQuery();
    }
    var legacyOptions = new DbContextOptionsBuilder<PosDbContext>().UseSqlite($"Data Source={legacyPath}").Options;
    var legacyFactory = new ContextFactory(legacyOptions);
    await new DatabaseInitializer(legacyFactory).InitializeAsync();
    Product oldBarquillo = (await new TicketService(legacyFactory).GetAllProductsAsync()).Single(p => p.Name == "Barquillo");
    Check(!oldBarquillo.IsActive && oldBarquillo.BasePrice == 99m && oldBarquillo.Prices.Count > 0, "Migración conserva producto antiguo");
    var drafts = new OrderDraftService();
    drafts.SaveCurrent(new OrderDraftService.Draft { OrderNumber = "VERIFY-DRAFT", Items = [ticket.Items.Single()] });
    Check(drafts.LoadCurrent()?.Items.Single().Flavors == "Chocolate", "Recuperación de borrador");
    drafts.Hold(drafts.LoadCurrent()!);
    string held = drafts.HeldOrders().Single();
    Check(drafts.LoadHeld(held)?.OrderNumber == "VERIFY-DRAFT", "Orden en espera");
    drafts.DeleteHeld(held);
    using (var source = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Mode=ReadOnly"))
    using (var target = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(FileSystem.AppDataDirectory, "pos.db")}"))
    { source.Open(); target.Open(); source.BackupDatabase(target); }
    var backups = new BackupService();
    string backupFile = backups.CreateBackup();
    Check(File.Exists(backupFile), "Respaldo verificable");
    backups.ScheduleRestore(backupFile);
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    BackupService.ApplyPendingRestore(FileSystem.AppDataDirectory);
    Check(File.Exists(Path.Combine(FileSystem.AppDataDirectory, "pos.db")), "Restauración programada");
    Console.WriteLine("OK: catálogo, precios, disponibilidad, cálculo, venta, cancelación, migración, borradores y respaldo");
}
finally
{
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    foreach (string file in new[] { path, path + "-wal", path + "-shm", legacyPath, legacyPath + "-wal", legacyPath + "-shm" }) if (File.Exists(file)) File.Delete(file);
    Directory.Delete(FileSystem.AppDataDirectory, true);
}

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("Fallo: " + name);
}

sealed class ContextFactory(DbContextOptions<PosDbContext> options) : IDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext() => new(options);
}

static class FileSystem
{
    public static string AppDataDirectory { get; set; } = string.Empty;
}
