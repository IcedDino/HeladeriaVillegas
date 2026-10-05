using HeladeriaPOS.Data;
using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

decimal cashDue = CashPaymentCalculator.AmountDue(167m, 100m, 20m, true);
Check(cashDue == 47m, "Pago mixto calcula saldo en efectivo");
Check(CashPaymentCalculator.ApplyQuickCash(0m, cashDue, "Exacto") == 47m, "Exacto cubre solamente saldo de pago mixto");
decimal receivedBills = CashPaymentCalculator.ApplyQuickCash(0m, cashDue, "20");
receivedBills = CashPaymentCalculator.ApplyQuickCash(receivedBills, cashDue, "50");
Check(receivedBills == 70m && receivedBills - cashDue == 23m, "Billetes acumulan efectivo y cambio");
Check(CashPaymentCalculator.ApplyQuickCash(receivedBills, cashDue, "Limpiar") == 0m, "Limpiar reinicia efectivo");
Check(CashPaymentCalculator.ApplyQuickCash(0m, 167m, "1000") == 1000m, "Acepta billete de mil pesos");
Check(CashPaymentCalculator.AmountDue(167m, 100m, 20m, false) == 167m, "Efectivo ignora importes de otros métodos");
Check(CashPaymentCalculator.AmountDue(100m, 150m, 0m, true) == 0m, "Saldo en efectivo no es negativo");

var startupCoordinator = new StartupCoordinator();
var renderer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Task visibleInterval = startupCoordinator.WaitForRendererAsync(renderer.Task,
    TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(2));
await Task.Delay(200);
Check(!visibleInterval.IsCompleted, "La espera visible no termina antes de cargar el modelo");
var visibleClock = Stopwatch.StartNew();
renderer.SetResult();
await visibleInterval;
Check(visibleClock.ElapsedMilliseconds >= 130, "El modelo permanece visible tras terminar de cargar");
await startupCoordinator.WaitForRendererAsync(Task.FromException(new Exception("WebGL unavailable")),
    TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(1));
await startupCoordinator.WaitForRendererAsync(new TaskCompletionSource().Task,
    TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(50)).WaitAsync(TimeSpan.FromSeconds(1));
var initializationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var releaseInitialization = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
Task<Exception?> coordinatedStartup = startupCoordinator.RunAsync(async () =>
{
    initializationStarted.SetResult();
    await releaseInitialization.Task;
}, TimeSpan.FromMilliseconds(120));
await initializationStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
Check(!coordinatedStartup.IsCompleted, "Arranque espera la inicialización y el tiempo mínimo");
releaseInitialization.SetResult();
Check(await coordinatedStartup is null, "Arranque completa las dos tareas");

var failureClock = Stopwatch.StartNew();
Exception? startupFailure = await startupCoordinator.RunAsync(
    () => Task.FromException(new InvalidOperationException("inicio fallido")), TimeSpan.FromMilliseconds(120));
Check(startupFailure is InvalidOperationException && failureClock.ElapsedMilliseconds >= 100,
    "Error de inicio se entrega después del tiempo mínimo");

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
    var noFlavorTicket = new Ticket { OrderNumber = "NO-FLAVOR-" + Guid.NewGuid().ToString("N"), Status = TicketStatus.Paid,
        PaidAt = DateTime.Now, Total = 47m, SubtotalBase = 47m, Received = 50m, Change = 3m,
        Items = [new OrderItem { ProductId = changed.Id, ProductName = changed.Name, BaseUnitPrice = 47m }] };
    await tickets.SaveTicketAsync(noFlavorTicket);
    Ticket savedNoFlavor = (await tickets.GetTicketsAsync(DateTime.Today)).Single(t => t.OrderNumber == noFlavorTicket.OrderNumber);
    Check(savedNoFlavor.Items.Single().Flavors is null && savedNoFlavor.Change == 3m, "Venta sin sabor se guarda y conserva cambio");
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
    drafts.SaveCurrent(new OrderDraftService.Draft { OrderNumber = "VERIFY-DRAFT", Items = [ticket.Items.Single()],
        PaymentMethod = PaymentMethod.Mixed, ReceivedCashInput = "50", CardInput = "17", DiscountInput = "5", DiscountReason = "Prueba" });
    Check(drafts.LoadCurrent()?.Items.Single().Flavors == "Chocolate", "Recuperación de borrador");
    string currentDraftFile = Path.Combine(FileSystem.AppDataDirectory, "Drafts", "current.json");
    if (args.Contains("--performance"))
    {
        OrderDraftService.Draft sample = drafts.LoadCurrent()!;
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var draftClock = Stopwatch.StartNew();
        for (int i = 0; i < 500; i++) drafts.SaveCurrent(sample);
        Console.WriteLine($"PERF: 500 guardados idénticos: {draftClock.Elapsed.TotalMilliseconds:F2} ms; " +
            $"{GC.GetAllocatedBytesForCurrentThread() - allocatedBefore} bytes asignados; JSON {new FileInfo(currentDraftFile).Length} bytes");
    }
    DateTime unchangedTime = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    File.SetLastWriteTimeUtc(currentDraftFile, unchangedTime);
    drafts.SaveCurrent(drafts.LoadCurrent()!);
    Check(File.GetLastWriteTimeUtc(currentDraftFile) == unchangedTime, "Borrador idéntico no vuelve a escribir el disco");
    string storedDraft = File.ReadAllText(currentDraftFile);
    Check(!storedDraft.Contains("LineTotal") && !storedDraft.Contains("\n"), "Borrador almacena solamente datos necesarios sin formato adicional");
    OrderDraftService.Draft updatedDraft = drafts.LoadCurrent()!;
    updatedDraft.Items.Single().Quantity = 3;
    drafts.SaveCurrent(updatedDraft);
    Check(new OrderDraftService().LoadCurrent()?.Items.Single().Quantity == 3, "Cambio de cantidad se guarda inmediatamente");
    string legacyDraft = System.Text.Json.JsonSerializer.Serialize(updatedDraft,
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(currentDraftFile, legacyDraft);
    Check(new OrderDraftService().LoadCurrent()?.Items.Single().LineTotal == updatedDraft.Items.Single().LineTotal,
        "Borradores antiguos con importes calculados siguen siendo compatibles");
    drafts.Hold(drafts.LoadCurrent()!);
    string held = drafts.HeldOrders().Single();
    Check(drafts.LoadHeld(held)?.OrderNumber == "VERIFY-DRAFT", "Orden en espera");
    Check(drafts.LoadCurrent() is null, "Poner en espera libera el ticket actual");
    OrderDraftService.Draft? heldDraft = new OrderDraftService().LoadHeld(held);
    Check(heldDraft?.Items.Single().Flavors == "Chocolate" && heldDraft.PaymentMethod == PaymentMethod.Mixed
        && heldDraft.ReceivedCashInput == "50" && heldDraft.CardInput == "17" && heldDraft.DiscountInput == "5",
        "Orden en espera conserva productos y pago tras reinicio");
    drafts.DeleteHeld(held);
    drafts.SaveCurrent(updatedDraft);
    Check(drafts.LoadCurrent()?.OrderNumber == updatedDraft.OrderNumber, "Guardado idéntico tras poner en espera recrea borrador actual");
    using (var source = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Mode=ReadOnly"))
    using (var target = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={Path.Combine(FileSystem.AppDataDirectory, "pos.db")}"))
    { source.Open(); target.Open(); source.BackupDatabase(target); }
    var backups = new BackupService();
    string backupFile = backups.CreateBackup();
    Check(File.Exists(backupFile), "Respaldo verificable");
    var backupClock = new VerificationTimeProvider(DateTimeOffset.UtcNow);
    using var scheduledBackups = new BackupService(backupClock);
    Check(scheduledBackups.BackupIfDue() is null, "No duplica respaldo reciente al iniciar");
    backupClock.UtcNow = backupClock.UtcNow.AddHours(11);
    Check(scheduledBackups.BackupIfDue() is null, "No adelanta respaldo antes de las 12 horas");
    backupClock.UtcNow = backupClock.UtcNow.AddHours(1).AddSeconds(1);
    string? nextBackup = scheduledBackups.BackupIfDue();
    Check(nextBackup is not null && File.Exists(nextBackup) && nextBackup != backupFile, "Crea respaldo automático al cumplirse 12 horas");
    Check(scheduledBackups.BackupIfDue() is null, "No repite respaldo después de completar el intervalo");
    using var restartedBackups = new BackupService(backupClock);
    Check(restartedBackups.BackupIfDue() is null, "Intervalo de respaldo se conserva después de reiniciar");
    backups.ScheduleRestore(backupFile);
    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    BackupService.ApplyPendingRestore(FileSystem.AppDataDirectory);
    Check(File.Exists(Path.Combine(FileSystem.AppDataDirectory, "pos.db")), "Restauración programada");
    Console.WriteLine("OK: coordinación de inicio, catálogo, precios, disponibilidad, cálculo, venta, cancelación, migración, borradores y respaldo");
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

sealed class VerificationTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}
