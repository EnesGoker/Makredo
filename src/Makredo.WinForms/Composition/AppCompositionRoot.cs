using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Infrastructure.Diagnostics;
using Makredo.Infrastructure.Migration;
using Makredo.Infrastructure.Persistence;
using Makredo.Platform.Windows.Hooks;
using Makredo.Platform.Windows.Hotkeys;
using Makredo.Platform.Windows.Interop;
using Makredo.WinForms.Composition.Migration;
using Makredo.WinForms.Forms;

namespace Makredo.WinForms.Composition;

internal sealed class AppCompositionRoot : IDisposable
{
    public IApplicationStateService ApplicationStateService { get; }
    public IMacroRecorderService MacroRecorderService { get; }
    public IMacroPlaybackService MacroPlaybackService { get; }
    public IMacroOptimizationService MacroOptimizationService { get; }
    public IMacroStorageService MacroStorageService { get; }
    public SessionDocument SessionDocument { get; }
    public IMacroLibraryService MacroLibraryService { get; }
    public IPlaybackSettingsStore PlaybackSettingsStore { get; }
    public IHotkeySettingsStore HotkeySettingsStore { get; }
    public IMacroLibraryUserStateStore MacroLibraryUserStateStore { get; }
    public IMutableHotkeyConfiguration HotkeyConfiguration { get; }
    public IHotkeyService HotkeyService { get; }
    public IAppLogger AppLogger { get; }

    private readonly IReadOnlyList<IDisposable> _disposables;
    private bool _disposed;

    private AppCompositionRoot(
        IApplicationStateService applicationStateService,
        IMacroRecorderService macroRecorderService,
        IMacroPlaybackService macroPlaybackService,
        IMacroOptimizationService macroOptimizationService,
        IMacroStorageService macroStorageService,
        SessionDocument sessionDocument,
        IMacroLibraryService macroLibraryService,
        IPlaybackSettingsStore playbackSettingsStore,
        IHotkeySettingsStore hotkeySettingsStore,
        IMacroLibraryUserStateStore macroLibraryUserStateStore,
        IMutableHotkeyConfiguration hotkeyConfiguration,
        IHotkeyService hotkeyService,
        IAppLogger appLogger,
        IReadOnlyList<IDisposable> disposables)
    {
        ApplicationStateService = applicationStateService;
        MacroRecorderService = macroRecorderService;
        MacroPlaybackService = macroPlaybackService;
        MacroOptimizationService = macroOptimizationService;
        MacroStorageService = macroStorageService;
        SessionDocument = sessionDocument;
        MacroLibraryService = macroLibraryService;
        PlaybackSettingsStore = playbackSettingsStore;
        HotkeySettingsStore = hotkeySettingsStore;
        MacroLibraryUserStateStore = macroLibraryUserStateStore;
        HotkeyConfiguration = hotkeyConfiguration;
        HotkeyService = hotkeyService;
        AppLogger = appLogger;
        _disposables = disposables;
    }

    public static AppStoragePaths PrepareDefaultStorage(CancellationToken cancellationToken = default)
    {
        AppStoragePaths storagePaths = AppStoragePaths.CreateDefault();
        string legacyDirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LegacyStoragePaths.DirectoryName);

        // Migration must finish before any logger or store can create the final directory.
        new DataDirectoryMigrator().Migrate(
            legacyDirectoryPath,
            storagePaths.RootDirectoryPath,
            cancellationToken);

        return storagePaths;
    }

    internal static AppCompositionRoot Create(
        AppStoragePaths storagePaths,
        IAppLogger? appLogger = null)
    {
        IApplicationStateService applicationStateService = new ApplicationStateService();
        ArgumentNullException.ThrowIfNull(storagePaths);
        IAppLogger resolvedLogger = appLogger ?? new FileLogger(storagePaths.LogDirectoryPath);

        IMutableHotkeyConfiguration hotkeyConfiguration = new MutableHotkeyConfiguration();

        var (keyboardHookSource, mouseHookSource) = WindowsHookSources.Create(resolvedLogger);
        IHotkeyService hotkeyService = new WindowsHotkeyService(hotkeyConfiguration, resolvedLogger);

        IJsonMacroStorageService jsonMacroStorageService = new JsonMacroStorageService();
        var sessionDocument = new SessionDocument(
            new SessionRecoveryStore(storagePaths.SessionRecoveryFilePath, jsonMacroStorageService, resolvedLogger));
        IXmlMacroStorageService xmlMacroStorageService = new XmlMacroStorageService();
        IMacroStorageService macroStorageService =
            new MacroStorageService(jsonMacroStorageService, xmlMacroStorageService);
        IMacroLibraryService macroLibraryService =
            new MacroLibraryService(
                macroStorageService,
                resolvedLogger,
                storagePaths.MacroLibraryDirectoryPath);
        IPlaybackSettingsStore playbackSettingsStore =
            new JsonPlaybackSettingsStore(storagePaths.PlaybackSettingsFilePath);
        IHotkeySettingsStore hotkeySettingsStore =
            new JsonHotkeySettingsStore(storagePaths.HotkeySettingsFilePath, resolvedLogger);
        IMacroLibraryUserStateStore macroLibraryUserStateStore =
            new JsonMacroLibraryUserStateStore(storagePaths.MacroLibraryStateFilePath);

        WindowsInputPlaybackAdapter inputPlaybackAdapter = new(resolvedLogger);

        IMacroRecorderService macroRecorderService = new MacroRecorderService(
            keyboardHookSource,
            mouseHookSource,
            applicationStateService,
            hotkeyConfiguration,
            resolvedLogger);

        IMacroPlaybackService macroPlaybackService = new MacroPlaybackService(
            inputPlaybackAdapter,
            inputPlaybackAdapter,
            applicationStateService,
            resolvedLogger,
            keyboardHookSource: keyboardHookSource);
        IMacroOptimizationService macroOptimizationService = new MacroOptimizationService();

        List<IDisposable> disposables = [];

        if (resolvedLogger is IDisposable loggerDisposable)
        {
            disposables.Add(loggerDisposable);
        }

        if (hotkeyService is IDisposable hotkeyDisposable)
        {
            disposables.Add(hotkeyDisposable);
        }

        if (mouseHookSource is IDisposable mouseHookDisposable)
        {
            disposables.Add(mouseHookDisposable);
        }

        if (keyboardHookSource is IDisposable keyboardHookDisposable)
        {
            disposables.Add(keyboardHookDisposable);
        }

        return new AppCompositionRoot(
            applicationStateService,
            macroRecorderService,
            macroPlaybackService,
            macroOptimizationService,
            macroStorageService,
            sessionDocument,
            macroLibraryService,
            playbackSettingsStore,
            hotkeySettingsStore,
            macroLibraryUserStateStore,
            hotkeyConfiguration,
            hotkeyService,
            resolvedLogger,
            disposables);
    }

    public MainForm CreateMainForm()
    {
        return new MainForm(
            ApplicationStateService,
            MacroRecorderService,
            MacroPlaybackService,
            MacroOptimizationService,
            MacroStorageService,
            SessionDocument,
            MacroLibraryService,
            PlaybackSettingsStore,
            HotkeySettingsStore,
            MacroLibraryUserStateStore,
            HotkeyConfiguration,
            HotkeyService,
            AppLogger);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        for (int index = _disposables.Count - 1; index >= 0; index--)
        {
            _disposables[index].Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
