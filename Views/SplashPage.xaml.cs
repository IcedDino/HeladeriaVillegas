using Microsoft.Web.WebView2.Core;
using System.Security.Cryptography;

namespace HeladeriaPOS.Views;

public partial class SplashPage : ContentPage
{
    private readonly TaskCompletionSource _rendererReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? _assetDirectory;
    private bool _released;
    private bool _initializing;
    private bool _revealing;

    public SplashPage()
    {
        InitializeComponent();
        SplashWebView.HandlerChanged += OnWebViewHandlerChanged;
    }

    public Task PrepareRendererAsync() => _rendererReady.Task;

    private async void OnWebViewHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (_released || _initializing || SplashWebView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.WebView2 platformWebView)
            return;

        _initializing = true;
        HeladeriaPOS.Services.StartupLog.Write("WebView initializing");
        try
        {
            platformWebView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 255, 247, 249);
            _assetDirectory = Path.Combine(FileSystem.AppDataDirectory, "Splash");
            HeladeriaPOS.Services.StartupLog.Write("Splash assets: " + _assetDirectory);
            Directory.CreateDirectory(_assetDirectory);
            foreach (string asset in new[] { "index.html", "splash.js", "three.module.js", "GLTFLoader.js", "BufferGeometryUtils.js", "ice_cream.glb" })
            {
                string assetPath = Path.Combine(_assetDirectory, asset);
                string hashPath = assetPath + ".sha256";
                string sourceHash;
                await using (Stream source = await FileSystem.OpenAppPackageFileAsync($"Splash/{asset}"))
                    sourceHash = Convert.ToHexString(await SHA256.HashDataAsync(source));
                if (File.Exists(assetPath) && File.Exists(hashPath) && await File.ReadAllTextAsync(hashPath) == sourceHash)
                    continue;
                await using Stream updatedSource = await FileSystem.OpenAppPackageFileAsync($"Splash/{asset}");
                await using (var destination = File.Create(assetPath + ".tmp"))
                    await updatedSource.CopyToAsync(destination);
                File.Move(assetPath + ".tmp", assetPath, overwrite: true);
                await File.WriteAllTextAsync(hashPath, sourceHash);
            }

            await platformWebView.EnsureCoreWebView2Async();
            HeladeriaPOS.Services.StartupLog.Write("WebView ready");
            platformWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "heladeria.local", _assetDirectory, CoreWebView2HostResourceAccessKind.Allow);
            platformWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            SplashWebView.Source = new UrlWebViewSource { Url = "https://heladeria.local/index.html" };
        }
        catch (Exception exception)
        {
            HeladeriaPOS.Services.StartupLog.Write("WebView failed: " + exception);
            _rendererReady.TrySetException(exception);
        }
#else
        _rendererReady.TrySetResult();
#endif
    }

#if WINDOWS
    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        HeladeriaPOS.Services.StartupLog.Write("Web message: " + e.WebMessageAsJson);
        if (e.Source.StartsWith("https://heladeria.local/", StringComparison.OrdinalIgnoreCase) &&
            e.TryGetWebMessageAsString() == "model-ready" && !_released && !_revealing)
        {
            _revealing = true;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (_released) return;
                try
                {
                    await Task.WhenAll(RendererCover.FadeTo(0, 450, Easing.CubicOut),
                        SplashTitle.FadeTo(1, 450, Easing.CubicOut));
                    RendererCover.IsVisible = false;
                    _rendererReady.TrySetResult();
                }
                catch (Exception exception)
                {
                    _rendererReady.TrySetException(exception);
                }
            });
        }
    }
#endif

    public async Task ReleaseRendererAsync()
    {
        if (_released)
            return;
        _released = true;
        try
        {
            if (SplashWebView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 platformWebView &&
                platformWebView.CoreWebView2 is { } core)
            {
                core.WebMessageReceived -= OnWebMessageReceived;
                await core.ExecuteScriptAsync("window.stopSplash?.();");
                core.NavigateToString("<!doctype html><html><body></body></html>");
            }
        }
        catch (Exception)
        {
            // El cierre del WebView no debe impedir que aparezca el POS.
        }
        SplashWebView.HandlerChanged -= OnWebViewHandlerChanged;
        SplashWebView.Source = new HtmlWebViewSource { Html = "" };
        Content = null;
    }
}
