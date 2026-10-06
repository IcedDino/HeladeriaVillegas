using System.Text.Json.Serialization;

namespace HeladeriaPOS.Models;

public enum SalesTutorialStep { Product, Configuration, Quantity, Payment, Cash, Confirm, Completed }

public sealed class SalesTutorialSession
{
    [JsonInclude] public SalesTutorialStep Step { get; private set; }
    [JsonInclude] public OrderItem? Item { get; private set; }
    [JsonInclude] public decimal Received { get; private set; }
    [JsonInclude] public bool QuantityReviewed { get; private set; }
    [JsonInclude] public DateTimeOffset? CompletedAt { get; private set; }
    [JsonIgnore] public decimal Total => Item?.LineTotal ?? 0m;
    [JsonIgnore] public decimal Change => Math.Max(0m, Received - Total);
    [JsonIgnore] public int StepNumber => Math.Min((int)Step + 1, 6);
    [JsonIgnore] public double Progress => Step == SalesTutorialStep.Completed ? 1 : (int)Step / 6d;

    public bool BeginConfiguration()
    {
        if (Step != SalesTutorialStep.Product) return false;
        Step = SalesTutorialStep.Configuration;
        return true;
    }

    public void CancelConfiguration()
    {
        if (Step == SalesTutorialStep.Configuration) Step = SalesTutorialStep.Product;
    }

    public void AddConfiguredProduct(Product product, ProductSelection selection, PricingResult price)
    {
        if (Step != SalesTutorialStep.Configuration) return;
        Item = new OrderItem { ProductId = product.Id, ProductName = product.Name, BaseUnitPrice = price.BasePrice,
            SelectedVariant = price.VariantDescription, Flavors = selection.Flavors, Modifiers = price.Modifiers };
        Step = SalesTutorialStep.Quantity;
    }

    public void ChangeQuantity(int delta)
    {
        if (Step != SalesTutorialStep.Quantity || Item is null || delta is not (1 or -1)) return;
        int quantity = Math.Clamp(Item.Quantity + delta, 1, 99);
        if (quantity == Item.Quantity) return;
        Item.Quantity = quantity;
        QuantityReviewed = true;
    }

    public void OpenPayment()
    {
        if (Step == SalesTutorialStep.Quantity && QuantityReviewed && Total > 0m) Step = SalesTutorialStep.Payment;
    }

    public void SelectCash()
    {
        if (Step == SalesTutorialStep.Payment) Step = SalesTutorialStep.Cash;
    }

    public void AddCash(decimal amount)
    {
        if (Step is not (SalesTutorialStep.Cash or SalesTutorialStep.Confirm) || amount <= 0) return;
        Received += amount;
        Step = Received >= Total ? SalesTutorialStep.Confirm : SalesTutorialStep.Cash;
    }

    public void ClearCash()
    {
        if (Step is not (SalesTutorialStep.Cash or SalesTutorialStep.Confirm)) return;
        Received = 0;
        Step = SalesTutorialStep.Cash;
    }

    public bool Complete()
    {
        if (Step != SalesTutorialStep.Confirm || Total <= 0 || Received < Total) return false;
        CompletedAt = DateTimeOffset.UtcNow;
        Step = SalesTutorialStep.Completed;
        return true;
    }
}
