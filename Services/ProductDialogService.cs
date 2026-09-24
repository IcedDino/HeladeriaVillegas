using HeladeriaPOS.Models;
using HeladeriaPOS.Views;

namespace HeladeriaPOS.Services;

public sealed class ProductDialogService : IProductDialogService
{
    private readonly TicketService _tickets;
    private INavigation? _navigation;

    public ProductDialogService(TicketService tickets) => _tickets = tickets;

    public void Attach(INavigation navigation)
    {
        _navigation = navigation;
    }

    public async Task<ProductSelection?> ConfigureAsync(Product product)
    {
        if (_navigation is null)
            throw new InvalidOperationException("La navegación todavía no está disponible.");

        var page = new ProductConfiguratorPage(product, await _tickets.GetFlavorsAsync(true));
        var modal = new NavigationPage(page);
        await _navigation.PushModalAsync(modal, true);

        ProductSelection? result = await page.WaitForResultAsync();

        if (_navigation.ModalStack.Contains(modal))
            await _navigation.PopModalAsync(true);

        return result;
    }
}
