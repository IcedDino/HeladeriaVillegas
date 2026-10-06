namespace HeladeriaPOS.Services;

public static class StartupPreparation
{
    public static bool TryPrepare(Func<bool> acquireInstance, Action applyRestore)
    {
        if (!acquireInstance()) return false;
        applyRestore();
        return true;
    }
}
