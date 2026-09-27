namespace HeladeriaPOS.Services;

public sealed class StartupCoordinator
{
    public async Task WaitForRendererAsync(Task rendererReady, TimeSpan visibleDuration, TimeSpan loadTimeout)
    {
        try
        {
            await rendererReady.WaitAsync(loadTimeout);
        }
        catch (Exception)
        {
            // A failed or unavailable renderer must not prevent access to the POS.
            return;
        }
        await Task.Delay(visibleDuration);
    }

    public async Task<Exception?> RunAsync(
        Func<Task> initializeAsync,
        TimeSpan minimumDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initializeAsync);

        Task minimumDisplay = Task.Delay(minimumDuration, cancellationToken);
        Task initialization;
        try
        {
            initialization = initializeAsync();
        }
        catch (Exception exception)
        {
            initialization = Task.FromException(exception);
        }

        Exception? startupError = null;
        try
        {
            await initialization.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            startupError = exception;
        }

        await minimumDisplay.ConfigureAwait(false);
        return startupError;
    }
}
