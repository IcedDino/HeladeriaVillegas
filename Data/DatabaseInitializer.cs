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

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(seedProducts);
            await db.SaveChangesAsync();
            return;
        }

        List<Product> existingProducts = await db.Products.ToListAsync();
        bool changed = false;

        foreach (Product seed in seedProducts)
        {
            Product? existing = existingProducts.FirstOrDefault(p =>
                p.Name == seed.Name &&
                p.Category == seed.Category &&
                p.ProductType == seed.ProductType);

            if (existing is null)
            {
                db.Products.Add(seed);
                changed = true;
                continue;
            }

            if (existing.BasePrice != seed.BasePrice)
            {
                existing.BasePrice = seed.BasePrice;
                changed = true;
            }

            if (existing.Tag != seed.Tag)
            {
                existing.Tag = seed.Tag;
                changed = true;
            }

            if (!string.Equals(existing.ImagePath, seed.ImagePath, StringComparison.OrdinalIgnoreCase))
            {
                existing.ImagePath = seed.ImagePath;
                existing.ImageCreator = seed.ImageCreator;
                existing.ImageLicense = seed.ImageLicense;
                existing.ImageLicenseUrl = seed.ImageLicenseUrl;
                existing.ImageSourceUrl = seed.ImageSourceUrl;
                changed = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
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
