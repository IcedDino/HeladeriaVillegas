using System.Runtime.Versioning;
using System.Security.Principal;
using HeladeriaPOS.Services;

internal static class UpdateStorageChecks
{
    [SupportedOSPlatform("windows")]
    private static void RunWindows()
    {
        string root = Path.Combine(Path.GetTempPath(), "storage-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            bool rejected = false;
            try { WindowsUpdateStorage.EnsureSecure(root); }
            catch (UnauthorizedAccessException) { rejected = true; }
            UpdateChecks.Assert(rejected, "Never adopt an existing user-writable updater directory");
            using var identity = WindowsIdentity.GetCurrent();
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            {
                Console.WriteLine("SKIP: creación de almacenamiento protegido requiere proceso elevado");
                return;
            }
            string secure = Path.Combine(root, "Secure");
            WindowsUpdateStorage.EnsureSecure(secure);
            File.WriteAllText(Path.Combine(secure, "installer.exe"), "protected fixture");
            WindowsUpdateStorage.EnsureSecure(secure);
            Console.WriteLine("OK: almacenamiento protegido rechaza carpetas inseguras y admite archivos heredados seguros");
        }
        finally { Directory.Delete(root, true); }
    }

    public static void Run()
    {
        if (OperatingSystem.IsWindows()) RunWindows();
    }
}
