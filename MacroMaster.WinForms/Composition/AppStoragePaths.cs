namespace MacroMaster.WinForms.Composition;

internal sealed class AppStoragePaths
{
    public string RootDirectoryPath { get; }

    public string LogDirectoryPath { get; }

    public string PlaybackSettingsFilePath { get; }

    public string HotkeySettingsFilePath { get; }

    public string MacroLibraryStateFilePath { get; }

    public string MacroLibraryDirectoryPath { get; }

    private AppStoragePaths(string rootDirectoryPath)
    {
        RootDirectoryPath = rootDirectoryPath;
        LogDirectoryPath = Path.Combine(rootDirectoryPath, "logs");
        PlaybackSettingsFilePath = Path.Combine(rootDirectoryPath, "playback-settings.json");
        HotkeySettingsFilePath = Path.Combine(rootDirectoryPath, "hotkey-settings.json");
        MacroLibraryStateFilePath = Path.Combine(rootDirectoryPath, "library-state.json");
        MacroLibraryDirectoryPath = Path.Combine(rootDirectoryPath, "macros");
    }

    public static AppStoragePaths CreateDefault()
    {
        string rootDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Polly");

        return FromRootDirectory(rootDirectoryPath);
    }

    public static AppStoragePaths FromRootDirectory(string rootDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectoryPath))
        {
            throw new ArgumentException("Uygulama veri klasörü yolu boş olamaz.", nameof(rootDirectoryPath));
        }

        return new AppStoragePaths(Path.GetFullPath(rootDirectoryPath));
    }
}
