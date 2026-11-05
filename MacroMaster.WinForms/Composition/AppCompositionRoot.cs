using MacroMaster.Application.Abstractions;
using MacroMaster.Application.Services;
using MacroMaster.Infrastructure.Diagnostics;
using MacroMaster.Infrastructure.Hooks;
using MacroMaster.Infrastructure.Interop;
using MacroMaster.Infrastructure.Persistence;
using MacroMaster.WinForms.Forms;
using MacroMaster.WinForms.Platform;

namespace MacroMaster.WinForms.Composition;

internal sealed class AppCompositionRoot : IDisposable
{
    public IApplicationStateService ApplicationStateService { get; }
    public IMacroRecorderService MacroRecorderService { get; }
    public IMacroPlaybackService MacroPlaybackService { get; }
    public IMacroOptimizationService MacroOptimizationService { get; }
    public IMacroStorageService MacroStorageService { get; }
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
        MacroLibraryService = macroLibraryService;
        PlaybackSettingsStore = playbackSettingsStore;
        HotkeySettingsStore = hotkeySettingsStore;
        MacroLibraryUserStateStore = macroLibraryUserStateStore;
        HotkeyConfiguration = hotkeyConfiguration;
        HotkeyService = hotkeyService;
        AppLogger = appLogger;
        _disposables = disposables;
    }

    public static AppCompositionRoot Create()
    {
        return Create(AppStoragePaths.CreateDefault());
    }

    internal static AppCompositionRoot Create(
        AppStoragePaths storagePaths,
        IAppLogger? appLogger = null)
    {
        IApplicationStateService applicationStateService = new ApplicationStateService();
        ArgumentNullException.ThrowIfNull(storagePaths);
        IAppLogger resolvedLogger = appLogger ?? new FileLogger(storagePaths.LogDirectoryPath);

        IMutableHotkeyConfiguration hotkeyConfiguration = new MutableHotkeyConfiguration();

        IKeyboardHookSource keyboardHookSource = new WindowsKeyboardHookSource(resolvedLogger);
        IMouseHookSource mouseHookSource = new WindowsMouseHookSource(resolvedLogger);
        IHotkeyService hotkeyService = new WindowsHotkeyService(hotkeyConfiguration, resolvedLogger);

        IJsonMacroStorageService jsonMacroStorageService = new JsonMacroStorageService();
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
            new JsonHotkeySettingsStore(storagePaths.HotkeySettingsFilePath);
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
            resolvedLogger);
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
