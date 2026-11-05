namespace MacroMaster.WinForms.Theme;

internal static class AppTypography
{
    public static readonly Font FontMono = new("Consolas", AppScale.ScaleFont(9.25f), FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontUiNormal = new("Segoe UI", AppScale.ScaleFont(9f), FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontUiBold = new("Segoe UI", AppScale.ScaleFont(9f), FontStyle.Bold, GraphicsUnit.Point);
    public static readonly Font FontUiSmall = new("Segoe UI", AppScale.ScaleFont(8.25f), FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font FontUiLarge = new("Segoe UI", AppScale.ScaleFont(11f), FontStyle.Bold, GraphicsUnit.Point);

}
