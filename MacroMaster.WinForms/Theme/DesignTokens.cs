namespace MacroMaster.WinForms.Theme;

internal static class DesignTokens
{
    public static float DensityScale => AppScale.DensityScale;
    public static float FontScale => AppScale.FontScale;

    public static Color Background => AppColors.Background;
    public static Color Surface => AppColors.Surface;
    public static Color Surface2 => AppColors.Surface2;
    public static Color Surface3 => AppColors.Surface3;
    public static Color SurfaceInset => AppColors.SurfaceInset;
    public static Color SurfaceHover => AppColors.SurfaceHover;

    public static Color Border => AppColors.Border;
    public static Color BorderSoft => AppColors.BorderSoft;
    public static Color BorderBright => AppColors.BorderBright;

    public static Color Accent => AppColors.Accent;
    public static Color AccentDeep => AppColors.AccentDeep;
    public static Color AccentSoft => AppColors.AccentSoft;
    public static Color AccentPurple => AppColors.AccentPurple;
    public static Color AccentRed => AppColors.AccentRed;
    public static Color AccentRedSoft => AppColors.AccentRedSoft;
    public static Color AccentGreen => AppColors.AccentGreen;
    public static Color AccentOrange => AppColors.AccentOrange;

    public static Color TextPrimary => AppColors.TextPrimary;
    public static Color TextSecondary => AppColors.TextSecondary;
    public static Color TextMuted => AppColors.TextMuted;

    public static int TitleBarHeight => AppSpacing.TitleBarHeight;
    public static int TitleBarButtonWidth => AppSpacing.TitleBarButtonWidth;
    public static int TitleBarButtonHeight => AppSpacing.TitleBarButtonHeight;
    public static int TitleBarIconSize => AppSpacing.TitleBarIconSize;
    public static int TitleBarStatusDotSize => AppSpacing.TitleBarStatusDotSize;
    public static int WindowResizeBorder => AppSpacing.WindowResizeBorder;
    public static int WindowMaximizedPadding => AppSpacing.WindowMaximizedPadding;
    public static int ToolbarHeight => AppSpacing.ToolbarHeight;
    public static int LibraryPanelWidth => AppSpacing.LibraryPanelWidth;
    public static int CardPadding => AppSpacing.CardPadding;
    public static int GapSmall => AppSpacing.GapSmall;
    public static int GapMedium => AppSpacing.GapMedium;
    public static int Radius => AppSpacing.Radius;
    public static int ScrollbarReserveWidth => SystemInformation.VerticalScrollBarWidth + Scale(10);
    public static int GridScrollbarReserveWidth => SystemInformation.VerticalScrollBarWidth + Scale(4);

    public static Font FontMono => AppTypography.FontMono;
    public static Font FontUiNormal => AppTypography.FontUiNormal;
    public static Font FontUiBold => AppTypography.FontUiBold;
    public static Font FontUiSmall => AppTypography.FontUiSmall;
    public static Font FontUiLarge => AppTypography.FontUiLarge;

    public static int Scale(int value)
    {
        return AppScale.Scale(value);
    }

    public static float ScaleFont(float value)
    {
        return AppScale.ScaleFont(value);
    }
}
