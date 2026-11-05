namespace MacroMaster.Domain.Models;

public static class MacroSessionFormat
{
    public const string CurrentVersion = "1.1";

    public static IReadOnlyCollection<string> SupportedVersions { get; } =
        ["1.0", CurrentVersion];
}
