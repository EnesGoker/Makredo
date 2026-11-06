using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Platform.Windows.Interop;

namespace Makredo.Platform.Windows.Hooks;

public sealed class WindowsMouseHookSource : ITimestampedMouseHookSource, IDisposable, IAsyncDisposable
{
    private readonly WindowsHookThread _owner;
    private readonly IAppLogger _logger;
    private int _disposed;

    public WindowsMouseHookSource(IAppLogger? logger = null)
        : this(new WindowsHookThread(logger ?? NullAppLogger.Instance), logger ?? NullAppLogger.Instance) { }

    internal WindowsMouseHookSource(WindowsHookThread owner, IAppLogger logger)
    {
        _owner = owner;
        _logger = logger;
        _owner.MouseReceived += ProcessSample;
    }

    public bool IsRunning => _owner.IsRunning(HookKind.Mouse);
    public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;
    public event Action<MouseActivityInfo>? TimestampedMouseActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _owner.StartAsync(HookKind.Mouse, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => _owner.StopAsync(HookKind.Mouse, cancellationToken);

    internal void ProcessSample(HookSample sample)
    {
        MouseActionType action = sample.Message switch
        {
            NativeMethods.WmMouseMove => MouseActionType.Move,
            NativeMethods.WmLButtonDown => MouseActionType.LeftDown,
            NativeMethods.WmLButtonUp => MouseActionType.LeftUp,
            NativeMethods.WmRButtonDown => MouseActionType.RightDown,
            NativeMethods.WmRButtonUp => MouseActionType.RightUp,
            NativeMethods.WmMButtonDown => MouseActionType.MiddleDown,
            NativeMethods.WmMButtonUp => MouseActionType.MiddleUp,
            NativeMethods.WmMouseWheel => MouseActionType.Wheel,
            NativeMethods.WmLButtonDblClk => MouseActionType.DoubleClick,
            _ => MouseActionType.None
        };
        if (action == MouseActionType.None) { return; }
        int? wheel = action == MouseActionType.Wheel ? unchecked((short)(sample.MouseData >> 16)) : null;
        var activity = new MouseActivityInfo(action, sample.X, sample.Y, wheel, sample.TimestampUtc, (sample.Flags & 0x01) != 0, sample.ExtraInfo);
        foreach (Action<MouseActivityInfo> handler in TimestampedMouseActivityReceived?.GetInvocationList() ?? [])
        {
            try { handler(activity); }
            catch (Exception exception) { LogSubscriberFailure(exception); }
        }
        foreach (Action<MouseActionType, int?, int?, int?> handler in MouseActivityReceived?.GetInvocationList() ?? [])
        {
            try { handler(action, sample.X, sample.Y, wheel); }
            catch (Exception exception) { LogSubscriberFailure(exception); }
        }
    }

    private void LogSubscriberFailure(Exception exception)
    {
        try { _logger.Log(AppLogLevel.Error, nameof(WindowsMouseHookSource), "Fare olayı dinleyicilere iletilirken hata oluştu.", exception); }
        catch (Exception) { }
    }

    public void Dispose()
    {
        _owner.DisposeSource(HookKind.Mouse);
        Interlocked.Exchange(ref _disposed, 1);
        _owner.MouseReceived -= ProcessSample;
    }

    public async ValueTask DisposeAsync()
    {
        await _owner.DisposeSourceAsync(HookKind.Mouse).ConfigureAwait(false);
        Interlocked.Exchange(ref _disposed, 1);
        _owner.MouseReceived -= ProcessSample;
    }
}
