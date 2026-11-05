using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroMaster.Application.Abstractions;
using MacroMaster.Application.Services;
using MacroMaster.Infrastructure.Interop;

namespace MacroMaster.Infrastructure.Hooks;

public sealed class WindowsKeyboardHookSource : IKeyboardHookSource, IDisposable
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

    private readonly object _syncRoot = new();
    private readonly IAppLogger _logger;

    private NativeMethods.HookProc? _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private HotkeyModifiers _activeModifiers;
    private bool _disposed;

    public WindowsKeyboardHookSource(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public event Action<KeyboardActivityInfo>? KeyActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (!OperatingSystem.IsWindows())
        {
            PlatformNotSupportedException exception = new(
                "WindowsKeyboardHookSource yalnizca Windows ortaminda calisir.");
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsKeyboardHookSource),
                "Klavye kancasi Windows disindaki bir ortamda baslatilamadi.",
                exception);
            throw exception;
        }

        lock (_syncRoot)
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            _activeModifiers = HotkeyModifiers.None;
            _hookProc = HookCallback;
            _hookHandle = InstallHook(_hookProc);
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(WindowsKeyboardHookSource),
            "Klavye kancasi baslatildi.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopCore();
        return Task.CompletedTask;
    }

    private void StopCore()
    {
        bool wasRunning;

        lock (_syncRoot)
        {
            wasRunning = IsRunning;

            if (!wasRunning)
            {
                return;
            }

            if (!NativeMethods.UnhookWindowsHookEx(_hookHandle))
            {
                ThrowWin32Exception("Klavye kancasi kaldirilamadi.");
            }

            _hookHandle = IntPtr.Zero;
            _hookProc = null;
            _activeModifiers = HotkeyModifiers.None;
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(WindowsKeyboardHookSource),
            "Klavye kancasi durduruldu.");
    }

    private static IntPtr InstallHook(NativeMethods.HookProc hookProc)
    {
        using Process currentProcess = Process.GetCurrentProcess();
        ProcessModule? mainModule = currentProcess.MainModule;

        IntPtr moduleHandle = IntPtr.Zero;

        if (mainModule is not null)
        {
            moduleHandle = NativeMethods.GetModuleHandle(mainModule.ModuleName);
        }

        IntPtr hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            hookProc,
            moduleHandle,
            0);

        if (hookHandle == IntPtr.Zero)
        {
            ThrowWin32Exception("Global klavye kancasi kurulamadi.");
        }

        return hookHandle;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                int message = unchecked((int)wParam.ToInt64());

                if (message is NativeMethods.WmKeyDown
                    or NativeMethods.WmKeyUp
                    or NativeMethods.WmSysKeyDown
                    or NativeMethods.WmSysKeyUp)
                {
                    NativeMethods.KBDLLHOOKSTRUCT hookData =
                        Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);

                    bool isKeyDown = message is NativeMethods.WmKeyDown
                        or NativeMethods.WmSysKeyDown;
                    int virtualKeyCode = unchecked((int)hookData.vkCode);
                    bool isModifierKey = TryMapModifier(virtualKeyCode, out HotkeyModifiers modifierKey);
                    // Capture a point-in-time modifier snapshot under the lock, then notify
                    // listeners outside the lock so callbacks cannot block hook state updates.
                    HotkeyModifiers activeModifiers = UpdateActiveModifiers(modifierKey, isKeyDown);

                    var keyboardActivity = new KeyboardActivityInfo(
                        virtualKeyCode,
                        unchecked((int)hookData.scanCode),
                        isKeyDown,
                        (hookData.flags & NativeMethods.KbdLlHookFlagExtended) != 0,
                        activeModifiers,
                        isModifierKey,
                        modifierKey);

                    SafeInvokeKeyActivityReceived(keyboardActivity);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsKeyboardHookSource),
                "Klavye kancasi callback'i islenirken hata olustu.",
                ex);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void SafeInvokeKeyActivityReceived(KeyboardActivityInfo keyboardActivity)
    {
        try
        {
            KeyActivityReceived?.Invoke(keyboardActivity);
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsKeyboardHookSource),
                "Klavye olayi dinleyicilere iletilirken hata olustu.",
                ex);
        }
    }

    private HotkeyModifiers UpdateActiveModifiers(
        HotkeyModifiers modifier,
        bool isKeyDown)
    {
        lock (_syncRoot)
        {
            if (modifier != HotkeyModifiers.None)
            {
                _activeModifiers = isKeyDown
                    ? _activeModifiers | modifier
                    : _activeModifiers & ~modifier;
            }

            return _activeModifiers;
        }
    }

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

    private static void ThrowWin32Exception(string message)
    {
        int errorCode = Marshal.GetLastWin32Error();
        var nativeException = new Win32Exception(errorCode);
        InvalidOperationException exception = new(
            FormattableString.Invariant(
                $"{message} Win32 hata kodu: {errorCode}. {nativeException.Message}"),
            nativeException);
        throw exception;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            StopCore();
        }
        catch
        {
            // no-op
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
