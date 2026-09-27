using HeladeriaPOS.Views;
using HeladeriaPOS.Services;
using HeladeriaPOS.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace HeladeriaPOS;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        StartupLog.Write("MAUI constructor");
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        StartupLog.Write("CreateWindow");
        var splashPage = new SplashPage();
        StartupLog.Write("Splash constructed");
        var mainPage = _services.GetRequiredService<MainPage>();
        StartupLog.Write("MainPage constructed");
        var navigationPage = new NavigationPage(mainPage)
        {
            BarBackgroundColor = Colors.White,
            BarTextColor = Color.FromArgb("#242424")
        };

        var window = new Window(splashPage)
        {
            Title = "Heladería POS",
            Width = 1440,
            Height = 900,
            MinimumWidth = 900,
            MinimumHeight = 680
        };

#if WINDOWS
        window.Created += (_, _) =>
        {
            StartupLog.Write("Window Created");
            try
            {
                if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
                {
                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                    Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                    appWindow.Changed += (_, _) => StartupLog.Write($"Window changed visible={appWindow.IsVisible} position={appWindow.Position} size={appWindow.Size}");
                    nativeWindow.Closed += (_, _) => StartupLog.Write("Native closed");
                    if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                        presenter.Maximize();
                    nativeWindow.Activate();
                }
            }
            catch
            {
                // Mantiene el tamaño 1440x900 si el entorno no permite maximizar.
            }
            _ = StartApplicationAsync(window, splashPage, navigationPage, mainPage);
        };
#else
        window.Created += (_, _) => _ = StartApplicationAsync(window, splashPage, navigationPage, mainPage);
#endif

        return window;
    }

    private async Task StartApplicationAsync(Window window, SplashPage splashPage, NavigationPage navigationPage, MainPage mainPage)
    {
        Exception? startupError = null;
        try
        {
            StartupLog.Write("Startup begin");
            var coordinator = _services.GetRequiredService<StartupCoordinator>();
            Task rendererDisplay = coordinator.WaitForRendererAsync(splashPage.PrepareRendererAsync(),
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6));
            startupError = await _services.GetRequiredService<StartupCoordinator>()
                .RunAsync(_services.GetRequiredService<MainViewModel>().LoadAsync, TimeSpan.FromSeconds(4));
            StartupLog.Write("Data loaded: " + startupError);
            await rendererDisplay;

            StartupLog.Write("Fade begin");
            await splashPage.FadeTo(0, 350, Easing.CubicIn);
            StartupLog.Write("Release begin");
            await splashPage.ReleaseRendererAsync();
            StartupLog.Write("Release done");
            window.Page = navigationPage;
            StartupLog.Write("POS assigned");
            ActivateNativeWindow(window);
            if (startupError is not null)
                await mainPage.ShowStartupErrorAsync(startupError);
        }
        catch (Exception exception)
        {
            StartupLog.Write("Startup failed: " + exception);
            await splashPage.ReleaseRendererAsync();
            window.Page = navigationPage;
            ActivateNativeWindow(window);
            await mainPage.ShowStartupErrorAsync(startupError ?? exception);
        }
    }

    private static void ActivateNativeWindow(Window window)
    {
#if WINDOWS
        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId).Show(true);
            nativeWindow.Activate();
            StartupLog.Write($"Activated hwnd={hwnd} visible={Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId).IsVisible}");
            window.Dispatcher.DispatchDelayed(TimeSpan.FromSeconds(2), () =>
            {
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                StartupLog.WindowState(hwnd);
                StartupLog.Write($"Stable window visible={appWindow.IsVisible} x={appWindow.Position.X} y={appWindow.Position.Y} width={appWindow.Size.Width} height={appWindow.Size.Height} page={window.Page?.GetType().Name} pageSize={window.Page?.Width}x{window.Page?.Height} opacity={window.Page?.Opacity} nativeContent={nativeWindow.Content?.GetType().Name}");
            });
        }
        else StartupLog.Write("Activate: no platform window");
#endif
    }
}
