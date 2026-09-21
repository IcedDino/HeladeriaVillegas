using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public sealed class PricingService
{
    public PricingResult Calculate(Product product, ProductSelection selection)
    {
        return product.ProductType switch
        {
            ProductType.Custom => CalculateCustom(product, selection),
            ProductType.PapasSabritas => CalculatePapasSabritas(selection),
            ProductType.Fritura => CalculateFritura(selection),
            ProductType.SopaPalomitas => CalculateSopaPalomitas(selection),
            ProductType.Barquillo or ProductType.Vaso => CalculateBarquilloVaso(selection),
            ProductType.Canasta => CalculateCanasta(selection),
            ProductType.Envase => CalculateEnvase(selection),
            ProductType.Malteada or ProductType.Copa or ProductType.BananaSplit or ProductType.TresMarias => CalculateSpecialty(product, selection),
            _ => throw new NotSupportedException($"Tipo no soportado: {product.ProductType}")
        };
    }

    private static PricingResult CalculateCustom(Product product, ProductSelection selection)
    {
        var modifiers = new List<Modifier>();
        if (product.AllowsExtras)
        {
            AddCounterModifier(
                modifiers,
                ModifierType.Other,
                string.IsNullOrWhiteSpace(product.ExtraName) ? "Extra" : product.ExtraName,
                product.ExtraPrice,
                selection.OtherIngredientCount);
        }

        return new PricingResult
        {
            BasePrice = product.BasePrice,
            VariantDescription = product.Name,
            Modifiers = modifiers
        };
    }

    private static PricingResult CalculatePapasSabritas(ProductSelection selection)
    {
        (decimal basePrice, string variant) = selection.SnackPreparation switch
        {
            SnackPreparation.PreparedAll => (65m, "Preparado con todos los ingredientes"),
            SnackPreparation.MissingIngredient => (60m, "Preparado sin algún ingrediente"),
            _ => (35m, "Normal")
        };

        var modifiers = new List<Modifier>();
        AddCounterModifier(modifiers, ModifierType.ExtraIngredient, "Ingrediente extra", 10m, selection.ExtraIngredientCount);

        return new PricingResult { BasePrice = basePrice, VariantDescription = variant, Modifiers = modifiers };
    }

    private static PricingResult CalculateFritura(ProductSelection selection)
    {
        var modifiers = new List<Modifier>();
        AddCounterModifier(modifiers, ModifierType.ExtraIngredient, "Ingrediente extra", 10m, selection.ExtraIngredientCount);
        return new PricingResult { BasePrice = 15m, VariantDescription = "Salsa incluida", Modifiers = modifiers };
    }

    private static PricingResult CalculateSopaPalomitas(ProductSelection selection)
    {
        var modifiers = new List<Modifier>();
        AddCounterModifier(modifiers, ModifierType.ExtraIngredient, "Ingrediente extra", 10m, selection.ExtraIngredientCount);
        return new PricingResult
        {
            BasePrice = selection.Cooked ? 35m : 30m,
            VariantDescription = selection.Cooked ? "Cocinada" : "Normal",
            Modifiers = modifiers
        };
    }

    private static PricingResult CalculateBarquilloVaso(ProductSelection selection)
    {
        decimal basePrice = selection.IceCreamSize switch
        {
            IceCreamSize.Chico => 25m,
            IceCreamSize.Mediano => 35m,
            IceCreamSize.Grande => 45m,
            IceCreamSize.Jumbo => 55m,
            _ => throw new InvalidOperationException("Debe seleccionar un tamaño de helado.")
        };

        return new PricingResult
        {
            BasePrice = basePrice,
            VariantDescription = SizeLabel(selection.IceCreamSize!.Value),
            Modifiers = CreateIceCreamModifiers(selection)
        };
    }

    private static PricingResult CalculateCanasta(ProductSelection selection)
    {
        decimal basePrice = selection.IceCreamSize switch
        {
            IceCreamSize.Doble => 45m,
            IceCreamSize.Triple => 55m,
            _ => throw new InvalidOperationException("Debe seleccionar canasta Doble o Triple.")
        };

        return new PricingResult
        {
            BasePrice = basePrice,
            VariantDescription = SizeLabel(selection.IceCreamSize!.Value),
            Modifiers = CreateIceCreamModifiers(selection)
        };
    }

    private static PricingResult CalculateEnvase(ProductSelection selection)
    {
        decimal basePrice = selection.IceCreamSize switch
        {
            IceCreamSize.MedioLitro => 65m,
            IceCreamSize.UnLitro => 110m,
            IceCreamSize.CincoLitros => 450m,
            IceCreamSize.DoceLitros => 850m,
            _ => throw new InvalidOperationException("Debe seleccionar el tamaño del envase o bote.")
        };

        var modifiers = new List<Modifier>();
        if (selection.IceCreamSize is IceCreamSize.MedioLitro or IceCreamSize.UnLitro)
        {
            AddCounterModifier(modifiers, ModifierType.ExtraIngredient, "Ingrediente de preparación", 10m, selection.PreparationExtraIngredientCount);
        }

        return new PricingResult
        {
            BasePrice = basePrice,
            VariantDescription = SizeLabel(selection.IceCreamSize!.Value),
            Modifiers = modifiers
        };
    }

    private static PricingResult CalculateSpecialty(Product product, ProductSelection selection)
    {
        decimal basePrice = product.ProductType switch
        {
            ProductType.Malteada => 40m,
            ProductType.Copa => 70m,
            ProductType.BananaSplit => 70m,
            ProductType.TresMarias => 70m,
            _ => throw new NotSupportedException()
        };

        var modifiers = new List<Modifier>(2);
        if (selection.Chantilly)
        {
            modifiers.Add(new Modifier { Type = ModifierType.Chantilly, Name = "Crema Chantilly", UnitPrice = 10m, Quantity = 1 });
        }
        AddCounterModifier(modifiers, ModifierType.Other, "Otro ingrediente", 5m, selection.OtherIngredientCount);

        return new PricingResult { BasePrice = basePrice, VariantDescription = product.Name, Modifiers = modifiers };
    }

    private static List<Modifier> CreateIceCreamModifiers(ProductSelection selection)
    {
        var modifiers = new List<Modifier>(2);

        switch (selection.IceCreamPreparation)
        {
            case IceCreamPreparation.SingleIngredient:
                modifiers.Add(new Modifier { Type = ModifierType.Preparation, Name = "Preparación de 1 ingrediente", UnitPrice = 5m, Quantity = 1 });
                break;
            case IceCreamPreparation.ChocolateOrJamAndCereal:
                modifiers.Add(new Modifier { Type = ModifierType.Preparation, Name = "Chocolate/mermelada + cereal", UnitPrice = 10m, Quantity = 1 });
                break;
        }

        AddCounterModifier(modifiers, ModifierType.ExtraScoop, "Bola extra", 20m, selection.ExtraScoops);
        return modifiers;
    }

    private static void AddCounterModifier(ICollection<Modifier> modifiers, ModifierType type, string name, decimal price, int quantity)
    {
        if (quantity <= 0)
            return;

        modifiers.Add(new Modifier { Type = type, Name = name, UnitPrice = price, Quantity = quantity });
    }

    private static string SizeLabel(IceCreamSize size) => size switch
    {
        IceCreamSize.MedioLitro => "½ litro",
        IceCreamSize.UnLitro => "1 litro",
        IceCreamSize.CincoLitros => "Bote 5 litros",
        IceCreamSize.DoceLitros => "Bote 12 litros",
        _ => size.ToString()
    };
}
