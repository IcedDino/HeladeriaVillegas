namespace HeladeriaPOS.Services;

internal static class StartupLog
{
#if WINDOWS
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    public static void WindowState(IntPtr hwnd)
    {
        DwmGetWindowAttribute(hwnd, 14, out int cloaked, 4);
        Write($"Native state visible={IsWindowVisible(hwnd)} owner={GetWindow(hwnd, 4)} cloaked={cloaked}");
    }
#endif
    private static readonly object Gate = new();
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "HeladeriaPOS-startup.log"),
                    $"{DateTime.Now:O} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch { /* Diagnostics must never prevent startup. */ }
    }
}
