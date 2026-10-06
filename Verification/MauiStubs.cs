namespace HeladeriaPOS.Views
{
    public sealed class MainPage { }
}

internal static class MainThread
{
    public static void BeginInvokeOnMainThread(Action action) => action();
}
