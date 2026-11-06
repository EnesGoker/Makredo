using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;

namespace Makredo.Platform.Windows.Interop;

public sealed class WindowsInputPlaybackAdapter : IInputPlaybackAdapter, ICursorPositionProvider
{
    private readonly IAppLogger _logger;

    public WindowsInputPlaybackAdapter(IAppLogger? logger = null)
    {
        _logger = logger ?? NullAppLogger.Instance;
    }

    public Task<CursorPosition> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            PlatformNotSupportedException exception = new(
                "WindowsInputPlaybackAdapter yalnizca Windows ortaminda calisir.");
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsInputPlaybackAdapter),
                "Gecerli imlec konumu Windows disindaki bir ortamda okunamadi.",
                exception);
            throw exception;
        }

        if (!NativeMethods.GetCursorPos(out NativeMethods.POINT point))
        {
            ThrowWin32Exception("Gecerli fare imleci konumu okunamadi.");
        }

        return Task.FromResult(new CursorPosition(point.x, point.y));
    }

    public Task PlayEventAsync(MacroEvent macroEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(macroEvent);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            PlatformNotSupportedException exception = new(
                "WindowsInputPlaybackAdapter yalnizca Windows ortaminda calisir.");
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsInputPlaybackAdapter),
                "Girdi oynatimi Windows disindaki bir ortamda baslatilamadi.",
                exception);
            throw exception;
        }

        return macroEvent.EventType switch
        {
            MacroEventType.Keyboard => PlayKeyboardEventAsync(macroEvent, cancellationToken),
            MacroEventType.Mouse => PlayMouseEventAsync(macroEvent, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    public Task ReleaseEventAsync(MacroEvent releaseEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(releaseEvent);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            PlatformNotSupportedException exception = new(
                "WindowsInputPlaybackAdapter yalnizca Windows ortaminda calisir.");
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsInputPlaybackAdapter),
                "Basılı girdi Windows dışındaki bir ortamda bırakılamadı.",
                exception);
            throw exception;
        }

        if (releaseEvent.EventType == MacroEventType.Keyboard
            && releaseEvent.KeyboardActionType == KeyboardActionType.KeyUp)
        {
            return PlayKeyboardEventAsync(releaseEvent, cancellationToken);
        }

        if (releaseEvent.EventType != MacroEventType.Mouse)
        {
            throw new ArgumentException("Yalnızca tuş veya fare düğmesi bırakma olayı gönderilebilir.", nameof(releaseEvent));
        }

        uint flags = releaseEvent.MouseActionType switch
        {
            MouseActionType.LeftUp => NativeMethods.MouseEventFLeftUp,
            MouseActionType.RightUp => NativeMethods.MouseEventFRightUp,
            MouseActionType.MiddleUp => NativeMethods.MouseEventFMiddleUp,
            _ => throw new ArgumentException("Yalnızca tuş veya fare düğmesi bırakma olayı gönderilebilir.", nameof(releaseEvent))
        };
        SendMouseInput(flags);
        return Task.CompletedTask;
    }

    private void ThrowWin32Exception(string message)
    {
        int errorCode = Marshal.GetLastWin32Error();
        InvalidOperationException exception = new(
            FormattableString.Invariant($"{message} Win32 hata kodu: {errorCode}"));
        _logger.Log(
            AppLogLevel.Error,
            nameof(WindowsInputPlaybackAdapter),
            message,
            exception);
        throw exception;
    }

    private void MoveCursorRequired(int? x, int? y, string actionName)
    {
        if (!x.HasValue || !y.HasValue)
        {
            throw new InvalidOperationException(
                $"Fare {actionName} oynatimi icin hem X hem de Y koordinati gerekir.");
        }

        MoveCursor(x, y);
    }

    private void MoveCursor(int? x, int? y)
    {
        if (!x.HasValue || !y.HasValue) { throw new InvalidOperationException("Fare hareketi için X ve Y gerekir."); }
        int left = NativeMethods.GetSystemMetrics(76);
        int top = NativeMethods.GetSystemMetrics(77);
        int width = NativeMethods.GetSystemMetrics(78);
        int height = NativeMethods.GetSystemMetrics(79);
        if (width <= 0 || height <= 0) { ThrowWin32Exception("Sanal ekran boyutu okunamadı."); }
        SendMouseInput(0x0001 | 0x8000 | 0x4000, 0,
            NormalizeCoordinate(x.Value, left, width), NormalizeCoordinate(y.Value, top, height));
    }

    internal static int NormalizeCoordinate(int coordinate, int origin, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        long offset = Math.Clamp((long)coordinate - origin, 0, size - 1L);
        return (int)Math.Clamp((offset * 65536L + 32768L) / size, 0, 65535);
    }

    private static int GetRequiredWheelDelta(MacroEvent macroEvent)
    {
        if (!macroEvent.WheelDelta.HasValue)
        {
            throw new InvalidOperationException(
                "Fare tekerlegi oynatimi icin wheel delta bilgisi gerekir.");
        }

        return macroEvent.WheelDelta.Value;
    }

    private Task PlayKeyboardEventAsync(
        MacroEvent macroEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!macroEvent.KeyCode.HasValue && !macroEvent.ScanCode.HasValue)
        {
            throw new InvalidOperationException(
                "Klavye oynatimi icin sanal tus kodu veya scan code bilgisi gerekir.");
        }

        bool isKeyUp = macroEvent.KeyboardActionType == KeyboardActionType.KeyUp;
        bool useScanCode = macroEvent.ScanCode is > 0;

        SendKeyboardInput(
            macroEvent.KeyCode.HasValue
                ? (ushort)macroEvent.KeyCode.Value
                : (ushort)0,
            useScanCode
                ? (ushort)macroEvent.ScanCode!.Value
                : (ushort)0,
            isKeyUp,
            useScanCode,
            macroEvent.IsExtendedKey);

        return Task.CompletedTask;
    }

    private Task PlayMouseEventAsync(
        MacroEvent macroEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        switch (macroEvent.MouseActionType)
        {
            case MouseActionType.Move:
                MoveCursor(macroEvent.X, macroEvent.Y);
                break;

            case MouseActionType.LeftDown:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "sol tus basma");
                SendMouseInput(NativeMethods.MouseEventFLeftDown);
                break;

            case MouseActionType.LeftUp:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "sol tus birakma");
                SendMouseInput(NativeMethods.MouseEventFLeftUp);
                break;

            case MouseActionType.RightDown:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "sag tus basma");
                SendMouseInput(NativeMethods.MouseEventFRightDown);
                break;

            case MouseActionType.RightUp:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "sag tus birakma");
                SendMouseInput(NativeMethods.MouseEventFRightUp);
                break;

            case MouseActionType.MiddleDown:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "orta tus basma");
                SendMouseInput(NativeMethods.MouseEventFMiddleDown);
                break;

            case MouseActionType.MiddleUp:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "orta tus birakma");
                SendMouseInput(NativeMethods.MouseEventFMiddleUp);
                break;

            case MouseActionType.Wheel:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "fare tekerlegi");
                SendMouseInput(
                    NativeMethods.MouseEventFWheel,
                    unchecked((uint)GetRequiredWheelDelta(macroEvent)));
                break;

            case MouseActionType.DoubleClick:
                MoveCursorRequired(macroEvent.X, macroEvent.Y, "cift tiklama");
                SendMouseInput(NativeMethods.MouseEventFLeftDown);
                SendMouseInput(NativeMethods.MouseEventFLeftUp);
                SendMouseInput(NativeMethods.MouseEventFLeftDown);
                SendMouseInput(NativeMethods.MouseEventFLeftUp);
                break;

            case MouseActionType.None:
            default:
                break;
        }

        return Task.CompletedTask;
    }

    private void SendMouseInput(uint flags, uint mouseData = 0, int dx = 0, int dy = 0)
    {
        NativeMethods.INPUT[] inputs =
        [
            new NativeMethods.INPUT
            {
                type = NativeMethods.InputMouse,
                U = new NativeMethods.InputUnion
                {
                    mi = new NativeMethods.MOUSEINPUT
                    {
                        dx = dx,
                        dy = dy,
                        mouseData = mouseData,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = new IntPtr(unchecked((long)InputOriginPolicy.PlaybackSignature))
                    }
                }
            }
        ];

        uint sent = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent == 0)
        {
            ThrowWin32Exception("Fare girdisi gonderilemedi.");
        }
    }

    private void SendKeyboardInput(
        ushort virtualKeyCode,
        ushort scanCode,
        bool isKeyUp,
        bool useScanCode,
        bool isExtendedKey)
    {
        uint flags = 0;

        if (isKeyUp)
        {
            flags |= NativeMethods.KeyEventFKeyUp;
        }

        if (useScanCode)
        {
            flags |= NativeMethods.KeyEventFScanCode;
            virtualKeyCode = 0;
        }

        if (isExtendedKey)
        {
            flags |= NativeMethods.KeyEventFExtendedKey;
        }

        NativeMethods.INPUT[] inputs =
        [
            new NativeMethods.INPUT
            {
                type = NativeMethods.InputKeyboard,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT
                    {
                        wVk = virtualKeyCode,
                        wScan = scanCode,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = new IntPtr(unchecked((long)InputOriginPolicy.PlaybackSignature))
                    }
                }
            }
        ];

        uint sent = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());

        if (sent == 0)
        {
            ThrowWin32Exception("Klavye girdisi gonderilemedi.");
        }
    }
}
