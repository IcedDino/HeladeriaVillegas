namespace HeladeriaPOS.Models;

public sealed class ProductSelection
{
    public SnackPreparation SnackPreparation { get; set; } = SnackPreparation.Normal;
    public bool Cooked { get; set; }
    public int ExtraIngredientCount { get; set; }

    public IceCreamSize? IceCreamSize { get; set; }
    public IceCreamPreparation IceCreamPreparation { get; set; } = IceCreamPreparation.None;
    public int ExtraScoops { get; set; }
    public int PreparationExtraIngredientCount { get; set; }

    public bool Chantilly { get; set; }
    public int OtherIngredientCount { get; set; }
    public string? Flavors { get; set; }
    public string? Instructions { get; set; }
}
