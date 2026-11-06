using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Platform.Windows.Interop;

namespace Makredo.Platform.Windows.Hooks;

public sealed class WindowsKeyboardHookSource : IKeyboardHookSource, IDisposable, IAsyncDisposable
{
    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyMenu = 0x12;
    private const int VirtualKeyLeftShift = 0xA0;
    private const int VirtualKeyRightShift = 0xA1;
    private const int VirtualKeyLeftControl = 0xA2;
    private const int VirtualKeyRightControl = 0xA3;
    private const int VirtualKeyLeftMenu = 0xA4;
    private const int VirtualKeyRightMenu = 0xA5;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;


    private readonly WindowsHookThread _owner;
    private readonly IAppLogger _logger;
    private HotkeyModifiers _activeModifiers;
    private readonly HashSet<int> _heldModifiers = [];
    private int _disposed;

    public WindowsKeyboardHookSource(IAppLogger? logger = null)
        : this(new WindowsHookThread(logger ?? NullAppLogger.Instance), logger ?? NullAppLogger.Instance) { }

    internal WindowsKeyboardHookSource(WindowsHookThread owner, IAppLogger logger)
    {
        _owner = owner;
        _logger = logger;
        _owner.KeyboardReceived += ProcessSample;
    }

    public bool IsRunning => _owner.IsRunning(HookKind.Keyboard);
    public event Action<KeyboardActivityInfo>? KeyActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _owner.StartAsync(HookKind.Keyboard, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _owner.StopAsync(HookKind.Keyboard, cancellationToken);

    internal void ProcessSample(HookSample sample)
    {
        if (sample.Message == 0)
        {
            _heldModifiers.Clear();
            foreach (int key in new[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
            {
                if ((GetAsyncKeyState(key) & 0x8000) != 0) { _heldModifiers.Add(key); }
            }
            RefreshModifiers();
            return;
        }
        if (sample.Message is not (NativeMethods.WmKeyDown or NativeMethods.WmKeyUp
            or NativeMethods.WmSysKeyDown or NativeMethods.WmSysKeyUp)) { return; }
        bool down = sample.Message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        int code = unchecked((int)sample.Code);
        bool modifierKey = TryMapModifier(code, out HotkeyModifiers modifier);
        if (modifier != HotkeyModifiers.None && sample.ExtraInfo != InputOriginPolicy.PlaybackSignature)
        {
            if (down) { _heldModifiers.Add(code); }
            else { _heldModifiers.Remove(code); }
            RefreshModifiers();
        }
        var activity = new KeyboardActivityInfo(code, unchecked((int)sample.ScanCode), down,
            (sample.Flags & NativeMethods.KbdLlHookFlagExtended) != 0,
            _activeModifiers, modifierKey, modifier, sample.TimestampUtc, (sample.Flags & 0x10) != 0, sample.ExtraInfo);
        foreach (Action<KeyboardActivityInfo> handler in KeyActivityReceived?.GetInvocationList() ?? [])
        {
            try { handler(activity); }
            catch (Exception exception)
            {
                try { _logger.Log(AppLogLevel.Error, nameof(WindowsKeyboardHookSource), "Klavye olayı dinleyicilere iletilirken hata oluştu.", exception); }
                catch (Exception) { }
            }
        }
    }

    private void RefreshModifiers()
    {
        _activeModifiers = HotkeyModifiers.None;
        foreach (int key in _heldModifiers)
        {
            TryMapModifier(key, out HotkeyModifiers modifier);
            _activeModifiers |= modifier;
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    private static bool TryMapModifier(int virtualKeyCode, out HotkeyModifiers modifier)
    {
        switch (virtualKeyCode)
        {
            case VirtualKeyShift:
            case VirtualKeyLeftShift:
            case VirtualKeyRightShift:
                modifier = HotkeyModifiers.Shift;
                return true;

            case VirtualKeyControl:
            case VirtualKeyLeftControl:
            case VirtualKeyRightControl:
                modifier = HotkeyModifiers.Control;
                return true;

            case VirtualKeyMenu:
            case VirtualKeyLeftMenu:
            case VirtualKeyRightMenu:
                modifier = HotkeyModifiers.Alt;
                return true;

            case VirtualKeyLeftWindows:
            case VirtualKeyRightWindows:
                modifier = HotkeyModifiers.Windows;
                return true;

            default:
                modifier = HotkeyModifiers.None;
                return false;
        }
    }


    public void Dispose()
    {
        _owner.DisposeSource(HookKind.Keyboard);
        Interlocked.Exchange(ref _disposed, 1);
        _owner.KeyboardReceived -= ProcessSample;
    }

    public async ValueTask DisposeAsync()
    {
        await _owner.DisposeSourceAsync(HookKind.Keyboard).ConfigureAwait(false);
        Interlocked.Exchange(ref _disposed, 1);
        _owner.KeyboardReceived -= ProcessSample;
    }
}
