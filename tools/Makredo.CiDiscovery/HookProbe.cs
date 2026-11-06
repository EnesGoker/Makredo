using System.Globalization;
using System.Runtime.InteropServices;

namespace Makredo.CiDiscovery;

internal static class HookProbe
{
    internal static async Task<HookProbeResult> RunAsync(CancellationToken cancellationToken)
    {
        var result = new HookProbeResult();
        if (!NativeMethods.IsHostedWindowsCi)
        {
            result.RefusalReason = "Hooks and native input are restricted to GitHub Actions Windows runners.";
            return result;
        }

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA || !Application.MessageLoop)
        {
            result.RefusalReason = "The probe requires an STA thread with a running WinForms message loop.";
            return result;
        }

        cancellationToken.ThrowIfCancellationRequested();
        result.Attempted = true;
        result.InputStructureSize = Marshal.SizeOf<NativeMethods.Input>();
        var state = new ProbeState(result);
        try
        {
            state.Install();
            if (result.KeyboardHook.Installed && result.MouseHook.Installed)
            {
                state.SendProbe();
                try
                {
                    await state.Completion.WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken);
                }
                catch (TimeoutException)
                {
                    result.TimedOut = true;
                    result.Errors.Add("The tagged keyboard down/up and two mouse moves were not all observed within five seconds.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            result.Errors.Add("The CI discovery deadline was cancelled.");
        }
        catch (Exception exception)
        {
            result.Errors.Add($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            state.Cleanup();
            GC.KeepAlive(state);
        }

        result.Succeeded = result.KeyboardHook.Installed
            && result.MouseHook.Installed
            && result.Sends.Any(send => send.Purpose == "probe" && send.Accepted == send.Requested)
            && result.Events.Any(item => item.Kind == "keyboard" && item.Message == NativeMethods.KeyDown && item.Injected)
            && result.Events.Any(item => item.Kind == "keyboard" && item.Message == NativeMethods.KeyUp && item.Injected)
            && result.Events.Count(item => item.Kind == "mouse" && item.Message == NativeMethods.MouseMove && item.Injected) >= 2
            && !result.TimedOut
            && !result.Cancelled
            && result.KeyboardHook.UnhookSucceeded
            && result.MouseHook.UnhookSucceeded
            && result.Sends.Any(send => send.Purpose == "cleanup-key-up" && send.Accepted == send.Requested)
            && (!result.CursorRestoreAttempted || result.CursorRestoreSucceeded)
            && result.Errors.Count == 0;
        return result;
    }

    private sealed class ProbeState
    {
        private readonly HookProbeResult _result;
        private readonly NativeMethods.HookCallback _keyboardCallback;
        private readonly NativeMethods.HookCallback _mouseCallback;
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IntPtr _keyboardHook;
        private IntPtr _mouseHook;
        private bool _inputAttempted;
        private bool _hasCursorPosition;
        private NativeMethods.Point _originalCursor;

        internal ProbeState(HookProbeResult result)
        {
            _result = result;
            _keyboardCallback = KeyboardCallback;
            _mouseCallback = MouseCallback;
        }

        internal Task Completion => _completion.Task;

        internal void Install()
        {
            IntPtr module = NativeMethods.GetModuleHandle(null);
            if (module == IntPtr.Zero)
            {
                _result.Errors.Add($"GetModuleHandle failed with Win32 error {Marshal.GetLastPInvokeError()}.");
                return;
            }

            _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.KeyboardHook, _keyboardCallback, module, 0);
            _result.KeyboardHook.Installed = _keyboardHook != IntPtr.Zero;
            _result.KeyboardHook.Handle = _keyboardHook.ToString("X", CultureInfo.InvariantCulture);
            _result.KeyboardHook.InstallError = _keyboardHook == IntPtr.Zero ? Marshal.GetLastPInvokeError() : 0;
            _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.MouseHook, _mouseCallback, module, 0);
            _result.MouseHook.Installed = _mouseHook != IntPtr.Zero;
            _result.MouseHook.Handle = _mouseHook.ToString("X", CultureInfo.InvariantCulture);
            _result.MouseHook.InstallError = _mouseHook == IntPtr.Zero ? Marshal.GetLastPInvokeError() : 0;
        }

        internal void SendProbe()
        {
            _hasCursorPosition = NativeMethods.GetCursorPos(out _originalCursor);
            _result.InitialCursorReadSucceeded = _hasCursorPosition;
            _result.InitialCursorReadError = _hasCursorPosition ? 0 : Marshal.GetLastPInvokeError();
            if (!_hasCursorPosition)
            {
                _result.Errors.Add("The original cursor position could not be read; no input was sent.");
                return;
            }

            _inputAttempted = true;
            Send("probe", [NativeMethods.KeyboardInput(keyUp: false), NativeMethods.KeyboardInput(keyUp: true), NativeMethods.MouseInput(1), NativeMethods.MouseInput(-1)]);
        }

        internal void Cleanup()
        {
            if (_inputAttempted)
            {
                try
                {
                    Send("cleanup-key-up", [NativeMethods.KeyboardInput(keyUp: true)]);
                }
                catch (Exception exception)
                {
                    _result.Errors.Add($"Key-up cleanup failed: {exception.GetType().Name}: {exception.Message}");
                }

                if (_hasCursorPosition)
                {
                    try
                    {
                        _result.CursorRestoreAttempted = true;
                        _result.CursorRestoreSucceeded = NativeMethods.RestoreCursor(_originalCursor);
                        _result.CursorRestoreError = _result.CursorRestoreSucceeded ? 0 : Marshal.GetLastPInvokeError();
                    }
                    catch (Exception exception)
                    {
                        _result.Errors.Add($"Cursor cleanup failed: {exception.GetType().Name}: {exception.Message}");
                    }
                }
            }

            Unhook(ref _keyboardHook, _result.KeyboardHook);
            Unhook(ref _mouseHook, _result.MouseHook);
        }

        private void Send(string purpose, NativeMethods.Input[] inputs)
        {
            uint accepted = NativeMethods.SendTaggedInput(inputs);
            int error = accepted == inputs.Length ? 0 : Marshal.GetLastPInvokeError();
            _result.Sends.Add(new InputSendResult(purpose, inputs.Length, accepted, error));
        }

        private void Unhook(ref IntPtr hook, HookInstallationResult installation)
        {
            if (hook == IntPtr.Zero) { return; }
            installation.UnhookAttempted = true;
            try
            {
                installation.UnhookSucceeded = NativeMethods.UnhookWindowsHookEx(hook);
                installation.UnhookError = installation.UnhookSucceeded ? 0 : Marshal.GetLastPInvokeError();
            }
            catch (Exception exception)
            {
                _result.Errors.Add($"Unhook cleanup failed: {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                hook = IntPtr.Zero;
            }
        }

        private IntPtr KeyboardCallback(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                try
                {
                    NativeMethods.KeyboardHookData keyboard = Marshal.PtrToStructure<NativeMethods.KeyboardHookData>(data);
                    if (keyboard.ExtraInfo == NativeMethods.InputMarker && keyboard.VirtualKey == NativeMethods.VirtualKeyF24)
                    {
                        _result.Events.Add(new HookEvent("keyboard", message.ToInt32(), keyboard.Flags, (keyboard.Flags & NativeMethods.KeyboardInjected) != 0, keyboard.ExtraInfo.ToUInt64().ToString("X", CultureInfo.InvariantCulture), keyboard.VirtualKey, keyboard.ScanCode, null, null));
                        TryComplete();
                    }
                    else
                    {
                        _result.IgnoredKeyboardEvents++;
                    }
                }
                catch (Exception exception)
                {
                    _result.Errors.Add($"Keyboard callback failed: {exception.GetType().Name}: {exception.Message}");
                }
            }

            return NativeMethods.CallNextHookEx(_keyboardHook, code, message, data);
        }

        private IntPtr MouseCallback(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0)
            {
                try
                {
                    NativeMethods.MouseHookData mouse = Marshal.PtrToStructure<NativeMethods.MouseHookData>(data);
                    if (mouse.ExtraInfo == NativeMethods.InputMarker)
                    {
                        _result.Events.Add(new HookEvent("mouse", message.ToInt32(), mouse.Flags, (mouse.Flags & NativeMethods.MouseInjected) != 0, mouse.ExtraInfo.ToUInt64().ToString("X", CultureInfo.InvariantCulture), null, null, mouse.Position.X, mouse.Position.Y));
                        TryComplete();
                    }
                    else
                    {
                        _result.IgnoredMouseEvents++;
                    }
                }
                catch (Exception exception)
                {
                    _result.Errors.Add($"Mouse callback failed: {exception.GetType().Name}: {exception.Message}");
                }
            }

            return NativeMethods.CallNextHookEx(_mouseHook, code, message, data);
        }

        private void TryComplete()
        {
            if (_result.Events.Any(item => item.Kind == "keyboard" && item.Message == NativeMethods.KeyDown && item.Injected)
                && _result.Events.Any(item => item.Kind == "keyboard" && item.Message == NativeMethods.KeyUp && item.Injected)
                && _result.Events.Count(item => item.Kind == "mouse" && item.Message == NativeMethods.MouseMove && item.Injected) >= 2)
            {
                _completion.TrySetResult(true);
            }
        }
    }
}

internal sealed class HookProbeResult
{
    public bool Attempted { get; set; }
    public string? RefusalReason { get; set; }
    public bool Succeeded { get; set; }
    public bool TimedOut { get; set; }
    public bool Cancelled { get; set; }
    public string InputMarker { get; } = NativeMethods.InputMarker.ToUInt64().ToString("X", CultureInfo.InvariantCulture);
    public int InputStructureSize { get; set; }
    public int KeyboardInjectedFlagMask { get; } = (int)NativeMethods.KeyboardInjected;
    public int MouseInjectedFlagMask { get; } = (int)NativeMethods.MouseInjected;
    public HookInstallationResult KeyboardHook { get; } = new();
    public HookInstallationResult MouseHook { get; } = new();
    public List<InputSendResult> Sends { get; } = [];
    public List<HookEvent> Events { get; } = [];
    public long IgnoredKeyboardEvents { get; set; }
    public long IgnoredMouseEvents { get; set; }
    public bool InitialCursorReadSucceeded { get; set; }
    public int InitialCursorReadError { get; set; }
    public bool CursorRestoreAttempted { get; set; }
    public bool CursorRestoreSucceeded { get; set; }
    public int CursorRestoreError { get; set; }
    public List<string> Errors { get; } = [];
}

internal sealed class HookInstallationResult
{
    public bool Installed { get; set; }
    public string? Handle { get; set; }
    public int InstallError { get; set; }
    public bool UnhookAttempted { get; set; }
    public bool UnhookSucceeded { get; set; }
    public int UnhookError { get; set; }
}

internal sealed record InputSendResult(string Purpose, int Requested, uint Accepted, int Win32Error);

internal sealed record HookEvent(string Kind, int Message, uint Flags, bool Injected, string ExtraInfo, uint? VirtualKey, uint? ScanCode, int? X, int? Y);
