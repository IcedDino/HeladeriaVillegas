using HeladeriaPOS.Views;
using Microsoft.Extensions.DependencyInjection;

namespace HeladeriaPOS;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Resolve the page after application resources have been initialized.
        var navigationPage = new NavigationPage(_services.GetRequiredService<MainPage>())
        {
            BarBackgroundColor = Colors.White,
            BarTextColor = Color.FromArgb("#242424")
        };

        var window = new Window(navigationPage)
        {
            Title = "Heladería POS",
            Width = 1440,
            Height = 900,
            MinimumWidth = 1000,
            MinimumHeight = 820
        };

#if WINDOWS
        window.Created += (_, _) =>
        {
            try
            {
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                        presenter.Maximize();
                }
            }
            catch
            {
                // Mantiene el tamaño 1440x900 si el entorno no permite maximizar.
            }
        };
#endif

        return window;
    }
}
