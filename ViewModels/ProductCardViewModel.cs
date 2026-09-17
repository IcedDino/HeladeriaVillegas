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
    public string ImagePath => Model.ImagePath ?? "placeholder.png";
    public decimal BasePrice => Model.BasePrice;
    public string BasePriceDisplay => BasePrice.ToString("C0", CurrencyCulture);
    public IAsyncRelayCommand SelectCommand { get; }

    public ProductCardViewModel(Product product, Func<ProductCardViewModel, Task> select)
    {
        Model = product;
        SelectCommand = new AsyncRelayCommand(() => select(this));
    }
}
