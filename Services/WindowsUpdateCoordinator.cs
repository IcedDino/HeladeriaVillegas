namespace HeladeriaPOS.Services;

/// <summary>Downloads while the POS runs; only the startup path can launch installation.</summary>
public sealed class WindowsUpdateCoordinator : IDisposable
{
    private readonly GitHubUpdateService _releases;
    private readonly UpdateStateStore _store;
    private readonly Version _version;
    private readonly Func<bool> _online, _installed, _otherInstance;
    private readonly Action _backup;
    private readonly Action<PendingUpdate> _launch;
    private readonly Action<Exception> _onError;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private DateTimeOffset? _lastCheck;
    private bool _started, _disposed;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromHours(1);

    public WindowsUpdateCoordinator(GitHubUpdateService releases, UpdateStateStore store, Version version,
        Func<bool> online, Func<bool> installed, Func<bool> otherInstance, Action backup,
        Action<PendingUpdate> launch, Action<Exception> onError, TimeProvider? clock = null)
    {
        _releases = releases; _store = store; _version = version;
        _online = online; _installed = installed; _otherInstance = otherInstance;
        _backup = backup; _launch = launch; _onError = onError;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task CheckAsync()
    {
        if (_disposed || !_installed() || !_online() || !await _gate.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            if (_lastCheck is not null && _clock.GetUtcNow() - _lastCheck < RetryInterval) return;
            _lastCheck = _clock.GetUtcNow();
            var pending = _store.Read();
            if (pending is not null && GitHubUpdateService.TryParseVersion(pending.Package.Version, out var pendingVersion)
                && pendingVersion > _version && await _releases.VerifyAsync(pending.Package, _store.InstallerPath, _stop.Token).ConfigureAwait(false)) return;
            if (pending is not null) _store.Clear();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var package = await _releases.FindUpdateAsync(_version, timeout.Token).ConfigureAwait(false);
            if (package is null) return;
            await _releases.DownloadAsync(package, _store.InstallerPath, timeout.Token).ConfigureAwait(false);
            _store.Write(new PendingUpdate(package, null));
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception exception) { _onError(exception); }
        finally { _gate.Release(); }
    }

    public async Task<bool> TryInstallPendingAsync(CancellationToken cancellationToken)
    {
        if (_disposed || !_installed() || !await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false)) return false;
        try
        {
            var pending = _store.Read();
            if (pending is null) return false;
            if (!GitHubUpdateService.TryParseVersion(pending.Package.Version, out var version) || version <= _version)
            { _store.Clear(); return false; }
            if (pending.LastAttemptUtc is not null && _clock.GetUtcNow() - pending.LastAttemptUtc < RetryInterval) return false;
            if (_otherInstance()) return false;
            if (!await _releases.VerifyAsync(pending.Package, _store.InstallerPath, cancellationToken).ConfigureAwait(false))
            { _store.Clear(); return false; }
            pending = pending with { LastAttemptUtc = _clock.GetUtcNow() };
            _store.Write(pending);
            _backup();
            _launch(pending);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return false; }
        catch (Exception exception) { _onError(exception); return false; }
        finally { _gate.Release(); }
    }

    public void Start()
    {
        if (_started || _disposed || !_installed()) return;
        _started = true;
#if WINDOWS
        Connectivity.ConnectivityChanged += OnConnectivityChanged;
#endif
        _loop = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(RetryInterval, _clock);
            try
            {
                do { await CheckAsync().ConfigureAwait(false); }
                while (await timer.WaitForNextTickAsync(_stop.Token).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }

#if WINDOWS
    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet) _ = Task.Run(CheckAsync);
    }
#endif

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
#if WINDOWS
        if (_started) Connectivity.ConnectivityChanged -= OnConnectivityChanged;
#endif
        _stop.Cancel();
        // Do not dispose synchronization primitives while a download is unwinding.
    }
}
