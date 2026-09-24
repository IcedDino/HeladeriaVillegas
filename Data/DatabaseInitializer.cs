using HeladeriaPOS.Models;
using HeladeriaPOS.Services;
using Microsoft.EntityFrameworkCore;

namespace HeladeriaPOS.Data;

public sealed class DatabaseInitializer
{
    private readonly IDbContextFactory<PosDbContext> _factory;

    public DatabaseInitializer(IDbContextFactory<PosDbContext> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();

        await EnsureProductColumnsAsync(db);
        await EnsureSalesColumnsAsync(db);
        await EnsureFlavorTableAsync(db);

        await db.Database.OpenConnectionAsync();
        try
        {
            await ExecutePragmaAsync(db, "PRAGMA journal_mode=WAL;");
            await ExecutePragmaAsync(db, "PRAGMA synchronous=NORMAL;");
            await ExecutePragmaAsync(db, "PRAGMA foreign_keys=ON;");
            await ExecutePragmaAsync(db, "PRAGMA busy_timeout=5000;");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        Product[] seedProducts = CreateSeedProducts();
        if (!await db.Flavors.AnyAsync())
        {
            db.Flavors.AddRange(new[] { "Vainilla", "Chocolate", "Fresa", "Napolitano", "Oreo", "Café" }.Select(name => new Flavor { Name = name }));
            await db.SaveChangesAsync();
        }

        if (!await db.Products.AnyAsync())
        {
            foreach (Product product in seedProducts)
                product.Prices = PriceCatalog.Defaults(product.ProductType);
            db.Products.AddRange(seedProducts);
            await db.SaveChangesAsync();
            return;
        }

        List<Product> existingProducts = await db.Products.Include(p => p.Prices).ToListAsync();
        bool changed = false;

        foreach (Product seed in seedProducts)
        {
            Product? existing = existingProducts.FirstOrDefault(p =>
                p.Name == seed.Name &&
                p.Category == seed.Category &&
                p.ProductType == seed.ProductType);

            if (existing is null)
            {
                seed.Prices = PriceCatalog.Defaults(seed.ProductType);
                db.Products.Add(seed);
                changed = true;
                continue;
            }
            if (existing.Prices.Count == 0 && existing.ProductType != ProductType.Custom)
            {
                existing.Prices.AddRange(PriceCatalog.Defaults(existing.ProductType));
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static async Task ExecutePragmaAsync(PosDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureProductColumnsAsync(PosDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "PRAGMA table_info('Products');";
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    existingColumns.Add(reader.GetString(1));
            }

            string[] migrations =
            [
                "AllowsExtras INTEGER NOT NULL DEFAULT 0",
                "ExtraName TEXT NULL",
                "ExtraPrice INTEGER NOT NULL DEFAULT 0",
                "ImageCreator TEXT NULL",
                "ImageLicense TEXT NULL",
                "ImageLicenseUrl TEXT NULL",
                "ImageSourceUrl TEXT NULL"
            ];

            foreach (string migration in migrations)
            {
                string columnName = migration.Split(' ', 2)[0];
                if (existingColumns.Contains(columnName))
                    continue;

                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = $"ALTER TABLE Products ADD COLUMN {migration};";
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureSalesColumnsAsync(PosDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await AddMissingColumnsAsync(db, "Tickets", [
                "PaymentMethod INTEGER NOT NULL DEFAULT 0",
                "CardPaid INTEGER NOT NULL DEFAULT 0",
                "TransferPaid INTEGER NOT NULL DEFAULT 0",
                "DiscountReason TEXT NULL",
                "CancellationReason TEXT NULL",
                "CancelledAt TEXT NULL"]);
            await AddMissingColumnsAsync(db, "OrderItems", ["Flavors TEXT NULL", "Instructions TEXT NULL"]);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS ProductPrices (Id INTEGER NOT NULL CONSTRAINT PK_ProductPrices PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL, Code TEXT NOT NULL, Label TEXT NOT NULL, Amount INTEGER NOT NULL, CONSTRAINT FK_ProductPrices_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE); CREATE UNIQUE INDEX IF NOT EXISTS IX_ProductPrices_ProductId_Code ON ProductPrices (ProductId, Code);";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task AddMissingColumnsAsync(PosDbContext db, string table, string[] definitions)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info('{table}');";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        }
        foreach (string definition in definitions)
        {
            if (columns.Contains(definition.Split(' ', 2)[0])) continue;
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition};";
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureFlavorTableAsync(PosDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS Flavors (Id INTEGER NOT NULL CONSTRAINT PK_Flavors PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, IsAvailable INTEGER NOT NULL DEFAULT 1); CREATE UNIQUE INDEX IF NOT EXISTS IX_Flavors_Name ON Flavors (Name);";
            await command.ExecuteNonQueryAsync();
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private static Product[] CreateSeedProducts() =>
    [
        Seed("Papas / Sabritas", ProductCategory.Snacks, ProductType.PapasSabritas, 35m, "Snack", "product_papas.jpg"),
        Seed("Frituras", ProductCategory.Snacks, ProductType.Fritura, 15m, "Salsa incluida", "product_frituras.jpg"),
        Seed("Sopa instantánea", ProductCategory.Snacks, ProductType.SopaPalomitas, 30m, "Caliente", "product_sopa.jpg"),
        Seed("Palomitas", ProductCategory.Snacks, ProductType.SopaPalomitas, 30m, "Snack", "product_palomitas.jpg"),

        Seed("Barquillo", ProductCategory.Helados, ProductType.Barquillo, 25m, "Helado", "product_barquillo.jpg"),
        Seed("Vaso", ProductCategory.Helados, ProductType.Vaso, 25m, "Helado", "product_vaso.jpg"),
        Seed("Canasta", ProductCategory.Helados, ProductType.Canasta, 45m, "Gourmet", "product_canasta.jpg"),
        Seed("Envase / Bote", ProductCategory.Helados, ProductType.Envase, 65m, "Para llevar", "product_envase.jpg"),

        Seed("Malteada", ProductCategory.Especialidades, ProductType.Malteada, 40m, "Especialidad", "product_malteada.jpg"),
        Seed("Copa", ProductCategory.Especialidades, ProductType.Copa, 70m, "Especialidad", "product_copa.jpg"),
        Seed("Banana Split", ProductCategory.Especialidades, ProductType.BananaSplit, 70m, "Especialidad", "product_banana_split.jpg"),
        Seed("Tres Marías", ProductCategory.Especialidades, ProductType.TresMarias, 70m, "Especialidad", "product_tres_marias.jpg")
    ];

    private static Product Seed(
        string name,
        ProductCategory category,
        ProductType productType,
        decimal basePrice,
        string tag,
        string imageFile)
    {
        OpenverseImageResult image = ProductImageLibrary.ForFile(imageFile);
        return new Product
        {
            Name = name,
            Category = category,
            ProductType = productType,
            BasePrice = basePrice,
            Tag = tag,
            ImagePath = image.Thumbnail,
            ImageCreator = image.Creator,
            ImageLicense = image.License,
            ImageLicenseUrl = image.LicenseUrl,
            ImageSourceUrl = image.SourceUrl
        };
    }
}
