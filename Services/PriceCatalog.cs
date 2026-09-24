using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public static class PriceCatalog
{
    public static List<ProductPrice> Defaults(ProductType type)
    {
        var values = type switch
        {
            ProductType.PapasSabritas => new[] { ("normal", "Normal", 35m), ("prepared", "Preparado completo", 65m), ("missing", "Preparado sin ingrediente", 60m), ("snack_extra", "Ingrediente extra", 10m) },
            ProductType.Fritura => new[] { ("normal", "Normal", 15m), ("snack_extra", "Ingrediente extra", 10m) },
            ProductType.SopaPalomitas => new[] { ("normal", "Normal", 30m), ("cooked", "Cocinada", 35m), ("snack_extra", "Ingrediente extra", 10m) },
            ProductType.Barquillo or ProductType.Vaso => new[] { ("Chico", "Chico", 25m), ("Mediano", "Mediano", 35m), ("Grande", "Grande", 45m), ("Jumbo", "Jumbo", 55m), ("ice_single", "Preparación de un ingrediente", 5m), ("ice_combined", "Chocolate o mermelada + cereal", 10m), ("scoop", "Bola extra", 20m) },
            ProductType.Canasta => new[] { ("Doble", "Doble", 45m), ("Triple", "Triple", 55m), ("ice_single", "Preparación de un ingrediente", 5m), ("ice_combined", "Chocolate o mermelada + cereal", 10m), ("scoop", "Bola extra", 20m) },
            ProductType.Envase => new[] { ("MedioLitro", "Medio litro", 65m), ("UnLitro", "1 litro", 110m), ("CincoLitros", "5 litros", 450m), ("DoceLitros", "12 litros", 850m), ("container_extra", "Ingrediente de preparación", 10m) },
            ProductType.Malteada => new[] { ("normal", "Normal", 40m), ("chantilly", "Crema Chantilly", 10m), ("special_extra", "Otro ingrediente", 5m) },
            ProductType.Copa or ProductType.BananaSplit or ProductType.TresMarias => new[] { ("normal", "Normal", 70m), ("chantilly", "Crema Chantilly", 10m), ("special_extra", "Otro ingrediente", 5m) },
            _ => Array.Empty<(string, string, decimal)>()
        };
        return values.Select(v => new ProductPrice { Code = v.Item1, Label = v.Item2, Amount = v.Item3 }).ToList();
    }

    public static decimal Get(Product product, string code)
    {
        ProductPrice? price = product.Prices.FirstOrDefault(p => p.Code == code);
        if (price is not null) return price.Amount;
        return Defaults(product.ProductType).FirstOrDefault(p => p.Code == code)?.Amount
            ?? throw new InvalidOperationException($"Falta el precio {code} de {product.Name}.");
    }
}
