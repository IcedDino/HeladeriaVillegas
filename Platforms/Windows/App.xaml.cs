using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace HeladeriaPOS.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        HeladeriaPOS.Services.StartupLog.Write("WinUI constructor");
        UnhandledException += (_, e) => HeladeriaPOS.Services.StartupLog.Write("Unhandled: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HeladeriaPOS.Services.StartupLog.Write("Domain: " + e.ExceptionObject);
        InitializeComponent();
        HeladeriaPOS.Services.StartupLog.Write("WinUI initialized");
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
