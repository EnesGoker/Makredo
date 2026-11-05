using System.Runtime.InteropServices;

namespace MacroMaster.WinForms.Theme;

/// <summary>
/// Enterprise-grade DPI-aware scaling system.
///
/// Strategy:
///   1. Read the actual Windows DPI for the primary monitor via Win32 API (GetDpiForSystem).
///   2. Normalize against the standard 96 DPI baseline → yields a true scale factor
///      (e.g. 100% = 1.0f, 125% = 1.25f, 150% = 1.5f, 200% = 2.0f).
///   3. All layout values are authored at 96 DPI (1x) and multiplied by this factor.
///   4. Fonts are scaled separately with a tighter clamp so they don't blow up on 4K.
///
/// Why NOT use Screen.WorkingArea?
///   WorkingArea returns *logical* pixels already adjusted by DPI, so dividing by a
///   reference resolution double-counts the OS scaling — exactly the bug this replaces.
///
/// Why NOT use AutoScaleMode.Dpi on controls?
///   Our layout is built entirely in code with Scale() calls. Windows AutoScale would
///   apply a second pass on top, causing the double-scaling seen at 125%/150% DPI.
///   All Designer files must have AutoScaleMode = None.
/// </summary>
internal static class AppScale
{
    private const float BaseDpi = 96f;

    // True Windows DPI scale: 1.0 at 96dpi, 1.25 at 120dpi, 1.5 at 144dpi, 2.0 at 192dpi
    public static readonly float DensityScale = ResolveWindowsDpiScale();

    // Font scale is clamped tighter — large fonts on high-DPI look disproportionate
    public static readonly float FontScale = Math.Clamp(DensityScale, 1f, 1.15f);

    public static int Scale(int value) =>
        (int)Math.Round(value * DensityScale);

    public static float ScaleFont(float value) =>
        MathF.Round(value * FontScale, 2);

    // -------------------------------------------------------------------------
    // Win32 DPI detection
    // -------------------------------------------------------------------------

    [DllImport("user32.dll")]
    private static extern int GetDpiForSystem();

    private static float ResolveWindowsDpiScale()
    {
        try
        {
            int sysDpi = GetDpiForSystem();
            if (sysDpi > 0)
            {
                // No clamping — respect whatever DPI the user has set.
                // Layout will scale linearly: 125% → 1.25, 200% → 2.0, etc.
                return sysDpi / BaseDpi;
            }
        }
        catch
        {
            // GetDpiForSystem unavailable (pre-Win10) → fall back to Graphics
        }

        // Fallback: read DPI from a temporary Graphics context (works on Win7/8)
        try
        {
            using var screen = Graphics.FromHwnd(IntPtr.Zero);
            float dpi = screen.DpiX;
            if (dpi > 0)
            {
                return dpi / BaseDpi;
            }
        }
        catch
        {
            // Ignore
        }

        return 1f; // Safe default: no scaling
    }
}