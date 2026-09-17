using HeladeriaPOS.Data;
using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;
using HeladeriaPOS.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HeladeriaPOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Segoe UI se usa de forma nativa en Windows; no se incrustan fuentes adicionales.
            });

        string dataDirectory = FileSystem.AppDataDirectory;
        Directory.CreateDirectory(dataDirectory);
        string databasePath = Path.Combine(dataDirectory, "pos.db");

        builder.Services.AddDbContextFactory<PosDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath};Cache=Shared;Pooling=True;Foreign Keys=True;Default Timeout=5"));

        builder.Services.AddSingleton<DatabaseInitializer>();
        builder.Services.AddSingleton<PricingService>();
        builder.Services.AddSingleton<TicketService>();
        builder.Services.AddSingleton<IProductDialogService, ProductDialogService>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
