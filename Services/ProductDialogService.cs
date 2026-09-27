using HeladeriaPOS.Models;
using HeladeriaPOS.Views;

namespace HeladeriaPOS.Services;

public sealed class ProductDialogService : IProductDialogService
{
    private readonly TicketService _tickets;
    private MainPage? _hostPage;

    public ProductDialogService(TicketService tickets) => _tickets = tickets;

    public void Attach(MainPage page) => _hostPage = page;

    public async Task<ProductSelection?> ConfigureAsync(Product product)
    {
        if (_hostPage is null)
            throw new InvalidOperationException("La pantalla principal todavía no está disponible.");

        IReadOnlyList<Flavor> flavors = await _tickets.GetFlavorsAsync(true);
        return await _hostPage.ShowProductConfiguratorAsync(product, flavors);
    }
}
