using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroMaster.Application.Abstractions;
using MacroMaster.Application.Services;
using MacroMaster.Domain.Enums;
using MacroMaster.Infrastructure.Interop;

namespace MacroMaster.Infrastructure.Hooks;

public sealed class WindowsMouseHookSource : IMouseHookSource, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly IAppLogger _logger;

    private NativeMethods.HookProc? _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private bool _disposed;

    public WindowsMouseHookSource(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public event Action<MouseActionType, int?, int?, int?>? MouseActivityReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (!OperatingSystem.IsWindows())
        {
            PlatformNotSupportedException exception = new(
                "WindowsMouseHookSource yalnizca Windows ortaminda calisir.");
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsMouseHookSource),
                "Fare kancasi Windows disindaki bir ortamda baslatilamadi.",
                exception);
            throw exception;
        }

        lock (_syncRoot)
        {
            if (IsRunning)
            {
                return Task.CompletedTask;
            }

            _hookProc = HookCallback;
            _hookHandle = InstallHook(_hookProc);
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(WindowsMouseHookSource),
            "Fare kancasi baslatildi.");
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
                ThrowWin32Exception("Fare kancasi kaldirilamadi.");
            }

            _hookHandle = IntPtr.Zero;
            _hookProc = null;
        }

        _logger.Log(
            AppLogLevel.Information,
            nameof(WindowsMouseHookSource),
            "Fare kancasi durduruldu.");
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
            NativeMethods.WhMouseLl,
            hookProc,
            moduleHandle,
            0);

        if (hookHandle == IntPtr.Zero)
        {
            ThrowWin32Exception("Global fare kancasi kurulamadi.");
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

                NativeMethods.MSLLHOOKSTRUCT hookData =
                    Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

                MouseActionType actionType = message switch
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

                if (actionType != MouseActionType.None)
                {
                    int? wheelDelta = actionType == MouseActionType.Wheel
                        ? ExtractWheelDelta(hookData.mouseData)
                        : null;

                    SafeInvokeMouseActivityReceived(
                        actionType,
                        hookData.pt.x,
                        hookData.pt.y,
                        wheelDelta);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsMouseHookSource),
                "Fare kancasi callback'i islenirken hata olustu.",
                ex);
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void SafeInvokeMouseActivityReceived(
        MouseActionType mouseActionType,
        int? x,
        int? y,
        int? wheelDelta)
    {
        try
        {
            MouseActivityReceived?.Invoke(mouseActionType, x, y, wheelDelta);
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsMouseHookSource),
                "Fare olayi dinleyicilere iletilirken hata olustu.",
                ex);
        }
    }

    private static int ExtractWheelDelta(uint mouseData)
    {
        short highWord = unchecked((short)((mouseData >> 16) & 0xffff));
        return highWord;
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
