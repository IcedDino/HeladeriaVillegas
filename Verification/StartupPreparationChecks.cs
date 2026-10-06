using HeladeriaPOS.Services;

internal static class StartupPreparationChecks
{
    public static void Run()
    {
        bool restored = false, acquired = false;
        UpdateChecks.Assert(!StartupPreparation.TryPrepare(() => false, () => restored = true) && !restored,
            "Rejected second instance never touches pending restore");
        UpdateChecks.Assert(StartupPreparation.TryPrepare(() => acquired = true, () =>
        {
            UpdateChecks.Assert(acquired, "Restore only after acquiring installation lock");
            restored = true;
        }) && restored, "First instance restores before loading database");
        Console.WriteLine("OK: restauración solo después de excluir otras instancias");
    }
}
