using CommunityToolkit.Mvvm.Input;
using HeladeriaPOS.Models;
using System.Globalization;

namespace HeladeriaPOS.ViewModels;

public sealed class ProductCardViewModel
{
    private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("es-MX");

    public Product Model { get; }
    public string Name => Model.Name;
    public string Tag => Model.Tag ?? string.Empty;
    public string ImagePath => string.IsNullOrWhiteSpace(Model.ImagePath) ? "placeholder.png" : Model.ImagePath;
    public string ImageAttribution => string.IsNullOrWhiteSpace(Model.ImageCreator)
        ? string.Empty
        : $"Foto: {Model.ImageCreator} · {Model.ImageLicense}";
    public bool HasImageAttribution => !string.IsNullOrWhiteSpace(Model.ImageCreator);
    public decimal BasePrice => Model.BasePrice;
    public string BasePriceDisplay => Model.Prices.Count > 0
        ? $"Desde {Model.Prices.Where(p => p.Code is "normal" or "Chico" or "Doble" or "MedioLitro").Select(p => p.Amount).DefaultIfEmpty(Model.BasePrice).Min().ToString("C0", CurrencyCulture)}"
        : BasePrice.ToString("C0", CurrencyCulture);
    public IAsyncRelayCommand SelectCommand { get; }

    public ProductCardViewModel(Product product, Func<ProductCardViewModel, Task> select)
    {
        Model = product;
        SelectCommand = new AsyncRelayCommand(() => select(this));
    }
}
