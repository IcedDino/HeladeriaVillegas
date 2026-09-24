using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeladeriaPOS.Models;
using System.Globalization;

namespace HeladeriaPOS.ViewModels;

public partial class OrderItemViewModel : ObservableObject
{
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("es-MX");
    private readonly Action<OrderItemViewModel> _remove;
    private readonly Action _recalculate;

    public OrderItem Model { get; }
    public string ProductName => Model.ProductName;
    public string Variant => string.Join(" · ", new[] { Model.SelectedVariant, Model.Flavors, Model.Instructions }.Where(s => !string.IsNullOrWhiteSpace(s)));
    public decimal BaseUnitPrice => Model.BaseUnitPrice;
    public decimal ExtrasUnitTotal => Model.ExtrasUnitTotal;
    public decimal UnitPrice => BaseUnitPrice + ExtrasUnitTotal;
    public decimal LineTotal => UnitPrice * Quantity;
    public string UnitPriceDisplay => UnitPrice.ToString("C0", CurrencyCulture);
    public string LineTotalDisplay => LineTotal.ToString("C0", CurrencyCulture);
    public string QuantityDisplay => Quantity.ToString(CultureInfo.InvariantCulture);

    public string ModifierSummary
    {
        get
        {
            if (Model.Modifiers.Count == 0)
                return string.Empty;

            return string.Join(" · ", Model.Modifiers.Select(m =>
                m.Quantity > 1 ? $"+ {m.Name} x{m.Quantity}" : $"+ {m.Name}"));
        }
    }

    [ObservableProperty]
    private double quantityValue = 1d;

    public int Quantity => Math.Max(1, (int)Math.Round(QuantityValue));

    public OrderItemViewModel(OrderItem model, Action<OrderItemViewModel> remove, Action recalculate)
    {
        Model = model;
        _remove = remove;
        _recalculate = recalculate;
        quantityValue = Math.Max(1, model.Quantity);
    }

    partial void OnQuantityValueChanged(double value)
    {
        int quantity = Math.Max(1, (int)Math.Round(value));
        Model.Quantity = quantity;

        if (Math.Abs(value - quantity) > double.Epsilon)
        {
            quantityValue = quantity;
            OnPropertyChanged(nameof(QuantityValue));
        }

        NotifyQuantityChanged();
        _recalculate();
    }

    [RelayCommand]
    private void IncrementQuantity()
    {
        QuantityValue = Quantity + 1;
    }

    [RelayCommand]
    private void DecrementQuantity()
    {
        if (Quantity > 1)
            QuantityValue = Quantity - 1;
    }

    [RelayCommand]
    private void Remove() => _remove(this);

    private void NotifyQuantityChanged()
    {
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(QuantityDisplay));
        OnPropertyChanged(nameof(LineTotal));
        OnPropertyChanged(nameof(LineTotalDisplay));
    }
}
