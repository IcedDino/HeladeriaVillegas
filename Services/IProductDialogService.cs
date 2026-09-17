using HeladeriaPOS.Models;

namespace HeladeriaPOS.Services;

public interface IProductDialogService
{
    void Attach(INavigation navigation);
    Task<ProductSelection?> ConfigureAsync(Product product);
}
