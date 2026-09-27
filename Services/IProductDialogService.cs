using HeladeriaPOS.Models;
using HeladeriaPOS.Views;

namespace HeladeriaPOS.Services;

public interface IProductDialogService
{
    void Attach(MainPage page);
    Task<ProductSelection?> ConfigureAsync(Product product);
}
