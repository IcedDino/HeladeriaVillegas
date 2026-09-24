using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public sealed class PricingService
{
    public PricingResult Calculate(Product product, ProductSelection selection)
    {
        var modifiers = new List<Modifier>();
        decimal amount;
        string variant;
        decimal Price(string code) => PriceCatalog.Get(product, code);
        void Extra(string code, ModifierType type, string name, int quantity)
        {
            if (quantity > 0)
                modifiers.Add(new Modifier { Type = type, Name = name, UnitPrice = Price(code), Quantity = quantity });
        }

        switch (product.ProductType)
        {
            case ProductType.Custom:
                amount = product.BasePrice;
                variant = product.Name;
                if (product.AllowsExtras && selection.OtherIngredientCount > 0)
                    modifiers.Add(new Modifier { Type = ModifierType.Other, Name = product.ExtraName ?? "Extra", UnitPrice = product.ExtraPrice, Quantity = selection.OtherIngredientCount });
                break;
            case ProductType.PapasSabritas:
                string snackCode = selection.SnackPreparation switch
                {
                    SnackPreparation.PreparedAll => "prepared",
                    SnackPreparation.MissingIngredient => "missing",
                    _ => "normal"
                };
                amount = Price(snackCode);
                variant = product.Prices.FirstOrDefault(p => p.Code == snackCode)?.Label ?? snackCode;
                Extra("snack_extra", ModifierType.ExtraIngredient, "Ingrediente extra", selection.ExtraIngredientCount);
                break;
            case ProductType.Fritura:
            case ProductType.SopaPalomitas:
                string code = product.ProductType == ProductType.SopaPalomitas && selection.Cooked ? "cooked" : "normal";
                amount = Price(code);
                variant = product.Prices.FirstOrDefault(p => p.Code == code)?.Label ?? code;
                Extra("snack_extra", ModifierType.ExtraIngredient, "Ingrediente extra", selection.ExtraIngredientCount);
                break;
            case ProductType.Barquillo:
            case ProductType.Vaso:
            case ProductType.Canasta:
                if (selection.IceCreamSize is null) throw new InvalidOperationException("Selecciona un tamaño.");
                string size = selection.IceCreamSize.Value.ToString();
                amount = Price(size);
                variant = product.Prices.FirstOrDefault(p => p.Code == size)?.Label ?? size;
                if (selection.IceCreamPreparation == IceCreamPreparation.SingleIngredient)
                    Extra("ice_single", ModifierType.Preparation, "Preparación de un ingrediente", 1);
                if (selection.IceCreamPreparation == IceCreamPreparation.ChocolateOrJamAndCereal)
                    Extra("ice_combined", ModifierType.Preparation, "Chocolate o mermelada + cereal", 1);
                Extra("scoop", ModifierType.ExtraScoop, "Bola extra", selection.ExtraScoops);
                break;
            case ProductType.Envase:
                if (selection.IceCreamSize is null) throw new InvalidOperationException("Selecciona un tamaño.");
                string container = selection.IceCreamSize.Value.ToString();
                amount = Price(container);
                variant = product.Prices.FirstOrDefault(p => p.Code == container)?.Label ?? container;
                if (selection.IceCreamSize is IceCreamSize.MedioLitro or IceCreamSize.UnLitro)
                    Extra("container_extra", ModifierType.ExtraIngredient, "Ingrediente de preparación", selection.PreparationExtraIngredientCount);
                break;
            default:
                amount = Price("normal");
                variant = product.Name;
                if (selection.Chantilly) Extra("chantilly", ModifierType.Chantilly, "Crema Chantilly", 1);
                Extra("special_extra", ModifierType.Other, "Otro ingrediente", selection.OtherIngredientCount);
                break;
        }

        return new PricingResult { BasePrice = amount, VariantDescription = variant, Modifiers = modifiers };
    }
}
