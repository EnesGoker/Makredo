using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Makredo.Application.Abstractions;
using Makredo.Application.Hooks;
using Makredo.Platform.Windows.Interop;

namespace Makredo.Platform.Windows.Hooks;

internal enum HookKind { Keyboard = 1, Mouse = 2 }

internal readonly record struct HookSample(HookKind Kind, int Message, uint Code, uint ScanCode,
    uint Flags, uint Time, int X, int Y, uint MouseData, DateTime TimestampUtc);

internal sealed class WindowsHookThread
{
    private const uint CommandMessage = 0x8000 + 67;
    private const uint QuitMessage = 0x0012;
    private readonly IAppLogger _logger;
    private readonly object _lifecycleSync = new();
    private Task _lifecycleTail = Task.CompletedTask;
    private readonly NativeMethods.HookProc _keyboardCallback;
    private readonly NativeMethods.HookProc _mouseCallback;
    private Session? _session;
    private Thread? _producerThread;
    private ChannelWriter<HookPacket>? _callbackWriter;
    private int _runningKinds;
    private int _disposedKinds;

    internal WindowsHookThread(IAppLogger logger)
    {
        _logger = logger;
        _keyboardCallback = KeyboardCallback;
        _mouseCallback = MouseCallback;
    }

    internal event Action<HookSample>? KeyboardReceived;
    internal event Action<HookSample>? MouseReceived;

    internal bool HasLiveThread => Volatile.Read(ref _producerThread)?.IsAlive == true;

    internal uint NativeThreadId => Volatile.Read(ref _session)?.NativeId ?? 0;
    internal Task ConsumerCompletion => Volatile.Read(ref _session)?.Events.Completion ?? Task.CompletedTask;

    internal bool IsRunning(HookKind kind) => (Volatile.Read(ref _runningKinds) & (int)kind) != 0;

    internal Task StartAsync(HookKind kind, CancellationToken cancellationToken)
    {
        ValidateKind(kind);
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Girdi hook'ları yalnızca Windows ortamında çalışır.");
        }
        return SerializeLifecycleAsync(() => StartCoreAsync(kind, cancellationToken), cancellationToken);
    }

    private async Task StartCoreAsync(HookKind kind, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf((_disposedKinds & (int)kind) != 0, this);
        if (IsRunning(kind)) { return; }
        Session? session = _session;
        if (session is not null && session.Exited.Task.IsCompleted)
        {
            await FinishSessionAsync(session, requestQuit: false).ConfigureAwait(false);
            session = null;
        }
        if (session is null)
        {
            session = new Session(this, kind);
            _session = session;
            _producerThread = session.Thread;
            _callbackWriter = session.Events.Writer;
            try
            {
                session.Thread.Start();
                await session.Ready.Task.ConfigureAwait(false);
            }
            catch
            {
                await FinishSessionAsync(session, requestQuit: session.Ready.Task.IsCompletedSuccessfully).ConfigureAwait(false);
                throw;
            }
        }
        else
        {
            await SendCommandAsync(session, kind, start: true).ConfigureAwait(false);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            await StopCoreAsync(kind).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    internal Task StopAsync(HookKind kind, CancellationToken cancellationToken)
    {
        ValidateKind(kind);
        return SerializeLifecycleAsync(() => StopCoreAsync(kind), cancellationToken);
    }

    internal Task DisposeSourceAsync(HookKind kind)
    {
        ValidateKind(kind);
        return SerializeLifecycleAsync(async () =>
        {
            _disposedKinds |= (int)kind;
            await StopCoreAsync(kind).ConfigureAwait(false);
        }, CancellationToken.None);
    }

    internal void DisposeSource(HookKind kind)
    {
        EnsureExternalLifecycle();
        DisposeSourceAsync(kind).GetAwaiter().GetResult();
    }

    private Task SerializeLifecycleAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        EnsureExternalLifecycle();
        if (cancellationToken.IsCancellationRequested) { return Task.FromCanceled(cancellationToken); }
        lock (_lifecycleSync)
        {
            Task previous = _lifecycleTail;
            _lifecycleTail = Task.Run(async () =>
            {
                try { await previous.ConfigureAwait(false); }
                catch (Exception) { }
                cancellationToken.ThrowIfCancellationRequested();
                await operation().ConfigureAwait(false);
            }, CancellationToken.None);
            return _lifecycleTail;
        }
    }

    private async Task StopCoreAsync(HookKind kind)
    {
        Session? session = _session;
        if (session is null) { return; }
        if (session.Exited.Task.IsCompleted)
        {
            await FinishSessionAsync(session, requestQuit: false).ConfigureAwait(false);
            return;
        }
        int runningKinds = Volatile.Read(ref _runningKinds);
        if (runningKinds == 0)
        {
            // The producer may have cleared its hooks just before publishing Exited.
            await FinishSessionAsync(session, requestQuit: false).ConfigureAwait(false);
            return;
        }
        if ((runningKinds & (int)kind) == 0) { return; }
        if (runningKinds == (int)kind)
        {
            await FinishSessionAsync(session, requestQuit: true).ConfigureAwait(false);
        }
        else
        {
            await SendCommandAsync(session, kind, start: false).ConfigureAwait(false);
        }
    }

    private async Task FinishSessionAsync(Session session, bool requestQuit)
    {
        if (requestQuit && !session.Exited.Task.IsCompleted
            && !NativeMethods.PostThreadMessage(session.NativeId, QuitMessage, UIntPtr.Zero, IntPtr.Zero)
            && !session.Exited.Task.IsCompleted)
        {
            throw NativeFailure("Hook thread'ine durdurma iletisi gönderilemedi.");
        }

        if (session.Started)
        {
            await Task.Run(session.Thread.Join).ConfigureAwait(false);
        }
        try
        {
            await session.Events.CompleteAsync().ConfigureAwait(false);
        }
        finally
        {
            _session = null;
            _callbackWriter = null;
        }
        if (session.Failure is { } failure) { throw failure; }
    }

    private static Task SendCommandAsync(Session session, HookKind kind, bool start)
    {
        var command = new HookCommand(kind, start);
        session.Commands.Enqueue(command);
        if (!NativeMethods.PostThreadMessage(session.NativeId, CommandMessage, UIntPtr.Zero, IntPtr.Zero))
        {
            command.Completion.TrySetException(NativeFailure("Hook thread'ine çalışma iletisi gönderilemedi."));
        }
        return command.Completion.Task;
    }

    private void Run(Session session, HookKind initialKind)
    {
        try
        {
            session.Started = true;
            session.NativeId = NativeMethods.GetCurrentThreadId();
            _ = NativeMethods.PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            Install(session, initialKind);
            session.Ready.TrySetResult();
            while (true)
            {
                int result = NativeMethods.GetMessage(out NativeMethods.MSG message, IntPtr.Zero, 0, 0);
                if (result == 0) { break; }
                if (result == -1) { throw NativeFailure("Hook mesaj döngüsü sonlandırıldı."); }
                if (message.message != CommandMessage) { continue; }
                while (session.Commands.TryDequeue(out HookCommand? command))
                {
                    if (command.Completion.Task.IsCompleted) { continue; }
                    try
                    {
                        if (command.Start)
                        {
                            Install(session, command.Kind);
                            command.Completion.TrySetResult();
                        }
                        else
                        {
                            Uninstall(session, command.Kind);
                            // The producer writes the barrier after unhooking, preserving one writer.
                            session.Events.Writer.TryWrite(new HookPacket(default, command.Completion));
                        }
                    }
                    catch (Exception exception) { command.Completion.TrySetException(exception); }
                }
            }
        }
        catch (Exception exception)
        {
            session.Failure = exception;
            session.Ready.TrySetException(exception);
        }
        finally
        {
            TryUninstall(session, HookKind.Keyboard);
            TryUninstall(session, HookKind.Mouse);
            Volatile.Write(ref _runningKinds, 0);
            while (session.Commands.TryDequeue(out HookCommand? pending))
            {
                pending.Completion.TrySetException(session.Failure ?? new InvalidOperationException("Hook thread'i sonlandı."));
            }
            if (session.Failure is { } failure)
            {
                session.Events.Writer.TryWrite(new HookPacket(default, null, failure));
            }
            // Unhooked producer completion also drains the consumer after an unexpected owner exit.
            session.Events.Writer.TryComplete();
            session.Exited.TrySetResult();
            GC.KeepAlive(_keyboardCallback);
            GC.KeepAlive(_mouseCallback);
        }
    }

    private void Install(Session session, HookKind kind)
    {
        if (session.Handle(kind) != IntPtr.Zero) { return; }
        if (kind == HookKind.Keyboard)
        {
            session.Events.Writer.TryWrite(new HookPacket(new HookSample(HookKind.Keyboard,
                0, 0, 0, 0, 0, 0, 0, 0, default), null));
        }
        IntPtr module = NativeMethods.GetModuleHandle(null);
        if (module == IntPtr.Zero) { throw NativeFailure("Hook modülü alınamadı."); }
        IntPtr handle = NativeMethods.SetWindowsHookEx(
            kind == HookKind.Keyboard ? NativeMethods.WhKeyboardLl : NativeMethods.WhMouseLl,
            kind == HookKind.Keyboard ? _keyboardCallback : _mouseCallback, module, 0);
        if (handle == IntPtr.Zero) { throw NativeFailure("Global girdi hook'u kurulamadı."); }
        session.SetHandle(kind, handle);
        Volatile.Write(ref _runningKinds, Volatile.Read(ref _runningKinds) | (int)kind);
    }

    private static void Uninstall(Session session, HookKind kind)
    {
        IntPtr handle = session.Handle(kind);
        if (handle == IntPtr.Zero) { return; }
        if (!NativeMethods.UnhookWindowsHookEx(handle)) { throw NativeFailure("Global girdi hook'u kaldırılamadı."); }
        session.SetHandle(kind, IntPtr.Zero);
        Volatile.Write(ref session.Owner._runningKinds, Volatile.Read(ref session.Owner._runningKinds) & ~(int)kind);
    }

    private static void TryUninstall(Session session, HookKind kind)
    {
        try { Uninstall(session, kind); }
        catch (Exception exception)
        {
            session.Failure = session.Failure is null ? exception : new AggregateException(session.Failure, exception);
        }
    }

    private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            int kind = unchecked((int)message.ToInt64());
            if (kind is NativeMethods.WmKeyDown or NativeMethods.WmKeyUp or NativeMethods.WmSysKeyDown or NativeMethods.WmSysKeyUp)
            {
                NativeMethods.KBDLLHOOKSTRUCT raw = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(data);
                _callbackWriter!.TryWrite(new HookPacket(new HookSample(HookKind.Keyboard, kind,
                    raw.vkCode, raw.scanCode, raw.flags, raw.time, 0, 0, 0, default), null));
            }
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    private IntPtr MouseCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            NativeMethods.MSLLHOOKSTRUCT raw = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(data);
            _callbackWriter!.TryWrite(new HookPacket(new HookSample(HookKind.Mouse,
                unchecked((int)message.ToInt64()), 0, 0, raw.flags, raw.time,
                raw.pt.x, raw.pt.y, raw.mouseData, default), null));
        }
        return NativeMethods.CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    private void ProcessPacket(Session session, HookPacket packet)
    {
        if (packet.Barrier is { } barrier)
        {
            barrier.TrySetResult();
            return;
        }
        if (packet.Error is { } error)
        {
            LogConsumerFailure(error);
            return;
        }
        HookSample sample = packet.Sample;
        if (sample.Message != 0)
        {
            sample = sample with { TimestampUtc = session.Clock.Convert(sample.Time, Environment.TickCount64) };
        }
        Action<HookSample>? handlers = sample.Kind == HookKind.Keyboard ? KeyboardReceived : MouseReceived;
        if (handlers is null) { return; }
        foreach (Action<HookSample> handler in handlers.GetInvocationList().Cast<Action<HookSample>>())
        {
            try { handler(sample); }
            catch (Exception exception) { LogConsumerFailure(exception); }
        }
    }

    private void LogConsumerFailure(Exception exception)
    {
        try { _logger.Log(AppLogLevel.Error, nameof(WindowsHookThread), "Girdi olayı tüketicide işlenemedi.", exception); }
        catch (Exception) { }
    }

    private void EnsureExternalLifecycle()
    {
        if (Volatile.Read(ref _session)?.Events.IsConsumerContext == true)
        {
            throw new InvalidOperationException("Hook tüketicisi kendi durmasını veya tamamlanmasını bekleyemez.");
        }
    }

    private static void ValidateKind(HookKind kind)
    {
        if (kind is not (HookKind.Keyboard or HookKind.Mouse)) { throw new ArgumentOutOfRangeException(nameof(kind)); }
    }

    private static InvalidOperationException NativeFailure(string message)
    {
        int error = Marshal.GetLastPInvokeError();
        return new InvalidOperationException($"{message} Win32: {error}.", new Win32Exception(error));
    }

    private readonly record struct HookPacket(HookSample Sample, TaskCompletionSource? Barrier, Exception? Error = null);

    private sealed class HookCommand(HookKind kind, bool start)
    {
        internal HookKind Kind { get; } = kind;
        internal bool Start { get; } = start;
        internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Session
    {
        private IntPtr _keyboardHook;
        private IntPtr _mouseHook;

        internal Session(WindowsHookThread owner, HookKind initialKind)
        {
            Owner = owner;
            Clock = new HookTimestampConverter(Environment.TickCount64, TimeProvider.System.GetUtcNow());
            Events = new HookEventPump<HookPacket>(packet => owner.ProcessPacket(this, packet), owner.LogConsumerFailure);
            Thread = new Thread(() => owner.Run(this, initialKind)) { IsBackground = true, Name = "Makredo input hooks" };
        }

        internal WindowsHookThread Owner { get; }
        internal HookTimestampConverter Clock { get; }
        internal HookEventPump<HookPacket> Events { get; }
        internal Thread Thread { get; }
        internal ConcurrentQueue<HookCommand> Commands { get; } = new();
        internal TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal uint NativeId { get; set; }
        internal bool Started { get; set; }
        internal Exception? Failure { get; set; }
        internal IntPtr Handle(HookKind kind) => kind == HookKind.Keyboard ? _keyboardHook : _mouseHook;
        internal void SetHandle(HookKind kind, IntPtr handle)
        {
            if (kind == HookKind.Keyboard) { _keyboardHook = handle; }
            else { _mouseHook = handle; }
        }
    }
}
