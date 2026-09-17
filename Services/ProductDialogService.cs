using HeladeriaPOS.Models;
using HeladeriaPOS.Views;

namespace HeladeriaPOS.Services;

public sealed class ProductDialogService : IProductDialogService
{
    private INavigation? _navigation;

    public void Attach(INavigation navigation)
    {
        _navigation = navigation;
    }

    public async Task<ProductSelection?> ConfigureAsync(Product product)
    {
        if (_navigation is null)
            throw new InvalidOperationException("La navegación todavía no está disponible.");

        var page = new ProductConfiguratorPage(product);
        var modal = new NavigationPage(page);
        await _navigation.PushModalAsync(modal, true);

        ProductSelection? result = await page.WaitForResultAsync();

        if (_navigation.ModalStack.Contains(modal))
            await _navigation.PopModalAsync(true);

        return result;
    }
}
