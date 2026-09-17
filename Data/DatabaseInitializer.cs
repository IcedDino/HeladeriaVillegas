using HeladeriaPOS.Models;
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

            if (existing.ImagePath != seed.ImagePath)
            {
                existing.ImagePath = seed.ImagePath;
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

    private static Product[] CreateSeedProducts() =>
    [
        new() { Name = "Papas / Sabritas", Category = ProductCategory.Snacks, ProductType = ProductType.PapasSabritas, BasePrice = 35m, Tag = "Snack", ImagePath = "papas_sabritas.png" },
        new() { Name = "Frituras", Category = ProductCategory.Snacks, ProductType = ProductType.Fritura, BasePrice = 15m, Tag = "Salsa incluida", ImagePath = "frituras.png" },
        new() { Name = "Sopa instantánea", Category = ProductCategory.Snacks, ProductType = ProductType.SopaPalomitas, BasePrice = 30m, Tag = "Caliente", ImagePath = "sopa_instantanea.png" },
        new() { Name = "Palomitas", Category = ProductCategory.Snacks, ProductType = ProductType.SopaPalomitas, BasePrice = 30m, Tag = "Snack", ImagePath = "palomitas.png" },

        new() { Name = "Barquillo", Category = ProductCategory.Helados, ProductType = ProductType.Barquillo, BasePrice = 25m, Tag = "Helado", ImagePath = "barquillo.png" },
        new() { Name = "Vaso", Category = ProductCategory.Helados, ProductType = ProductType.Vaso, BasePrice = 25m, Tag = "Helado", ImagePath = "vaso_helado.png" },
        new() { Name = "Canasta", Category = ProductCategory.Helados, ProductType = ProductType.Canasta, BasePrice = 45m, Tag = "Gourmet", ImagePath = "canasta.png" },
        new() { Name = "Envase / Bote", Category = ProductCategory.Helados, ProductType = ProductType.Envase, BasePrice = 65m, Tag = "Para llevar", ImagePath = "envase_bote.png" },

        new() { Name = "Malteada", Category = ProductCategory.Especialidades, ProductType = ProductType.Malteada, BasePrice = 40m, Tag = "Especialidad", ImagePath = "malteada.png" },
        new() { Name = "Copa", Category = ProductCategory.Especialidades, ProductType = ProductType.Copa, BasePrice = 70m, Tag = "Especialidad", ImagePath = "copa.png" },
        new() { Name = "Banana Split", Category = ProductCategory.Especialidades, ProductType = ProductType.BananaSplit, BasePrice = 70m, Tag = "Especialidad", ImagePath = "banana_split.png" },
        new() { Name = "Tres Marías", Category = ProductCategory.Especialidades, ProductType = ProductType.TresMarias, BasePrice = 70m, Tag = "Especialidad", ImagePath = "tres_marias.png" }
    ];
}
