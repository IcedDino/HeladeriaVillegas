namespace HeladeriaPOS.Models;

public sealed class PricingResult
{
    public decimal BasePrice { get; init; }
    public string VariantDescription { get; init; } = string.Empty;
    public List<Modifier> Modifiers { get; init; } = [];

    public decimal Extras
    {
        get
        {
            decimal value = 0m;
            for (int i = 0; i < Modifiers.Count; i++)
                value += Modifiers[i].UnitPrice * Modifiers[i].Quantity;
            return value;
        }
    }

    public decimal Total => BasePrice + Extras;
}
