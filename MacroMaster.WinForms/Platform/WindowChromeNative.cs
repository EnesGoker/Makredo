using System.Runtime.InteropServices;

namespace MacroMaster.WinForms.Platform;

internal enum WindowHitTest
{
    Nowhere = 0,
    Client = 1,
    Caption = 2,
    SystemMenu = 3,
    Left = 10,
    Right = 11,
    Top = 12,
    TopLeft = 13,
    TopRight = 14,
    Bottom = 15,
    BottomLeft = 16,
    BottomRight = 17,
    MinButton = 8,
    MaxButton = 9,
    CloseButton = 20
}

internal enum DwmWindowAttribute
{
    UseImmersiveDarkMode = 20,
    WindowCornerPreference = 33,
    BorderColor = 34,
    CaptionColor = 35,
    TextColor = 36
}

internal enum DwmWindowCornerPreference
{
    Default = 0,
    DoNotRound = 1,
    Round = 2,
    RoundSmall = 3
}

internal static class WindowChromeNative
{
    public const int WmNcHitTest = 0x0084;
    public const int WmNcLButtonDown = 0x00A1;
    public const int WmSysCommand = 0x0112;
    public const int ScMove = 0xF010;
    public const int ScSize = 0xF000;
    public const int ScMinimize = 0xF020;
    public const int ScMaximize = 0xF030;
    public const int ScClose = 0xF060;
    public const int ScRestore = 0xF120;
    public const int WsMinimizeBox = 0x00020000;
    public const int WsMaximizeBox = 0x00010000;
    public const int WsThickFrame = 0x00040000;

    private const int Succeeded = 0;

    public static bool TryApplyDwmBoolAttribute(IntPtr handle, DwmWindowAttribute attribute, bool enabled)
    {
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return false;
        }

        int value = enabled ? 1 : 0;
        return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == Succeeded;
    }

    public static bool TryApplyDwmColorAttribute(IntPtr handle, DwmWindowAttribute attribute, Color color)
    {
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            return false;
        }

        int value = ColorTranslator.ToWin32(color);
        return DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)) == Succeeded;
    }

    public static bool TryApplyDwmCornerPreference(IntPtr handle, DwmWindowCornerPreference preference)
    {
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return false;
        }

        int value = (int)preference;
        return DwmSetWindowAttribute(handle, DwmWindowAttribute.WindowCornerPreference, ref value, sizeof(int)) == Succeeded;
    }

    public static Point GetPointFromLParam(IntPtr lParam)
    {
        int value = unchecked((int)lParam.ToInt64());
        return new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
    }

    public static WindowHitTest GetResizeHitTest(Rectangle bounds, Point screenPoint, int borderWidth)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || borderWidth <= 0)
        {
            return WindowHitTest.Client;
        }

        bool left = screenPoint.X >= bounds.Left && screenPoint.X < bounds.Left + borderWidth;
        bool right = screenPoint.X <= bounds.Right && screenPoint.X > bounds.Right - borderWidth;
        bool top = screenPoint.Y >= bounds.Top && screenPoint.Y < bounds.Top + borderWidth;
        bool bottom = screenPoint.Y <= bounds.Bottom && screenPoint.Y > bounds.Bottom - borderWidth;

        return (left, right, top, bottom) switch
        {
            (true, false, true, false) => WindowHitTest.TopLeft,
            (false, true, true, false) => WindowHitTest.TopRight,
            (true, false, false, true) => WindowHitTest.BottomLeft,
            (false, true, false, true) => WindowHitTest.BottomRight,
            (true, false, false, false) => WindowHitTest.Left,
            (false, true, false, false) => WindowHitTest.Right,
            (false, false, true, false) => WindowHitTest.Top,
            (false, false, false, true) => WindowHitTest.Bottom,
            _ => WindowHitTest.Client
        };
    }

    public static void BeginWindowDrag(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(handle, WmNcLButtonDown, (IntPtr)WindowHitTest.Caption, IntPtr.Zero);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        DwmWindowAttribute dwAttribute,
        ref int pvAttribute,
        int cbAttribute);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam);
}
