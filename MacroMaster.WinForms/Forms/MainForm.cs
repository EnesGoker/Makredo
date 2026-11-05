using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Controls;
using MacroMaster.WinForms.Platform;
using MacroMaster.WinForms.Reporting;
using MacroMaster.WinForms.Theme;
using System.Globalization;
using System.Text;

namespace MacroMaster.WinForms.Forms;

public partial class MainForm : Form
{
    private const int PlaybackTelemetryRefreshIntervalMs = 33;
    private const int PlaybackSelectionRefreshIntervalMs = 80;

    private readonly IApplicationStateService _applicationStateService;
    private readonly IMacroRecorderService _macroRecorderService;
    private readonly IMacroPlaybackService _macroPlaybackService;
    private readonly IMacroOptimizationService _macroOptimizationService;
    private readonly IMacroStorageService _macroStorageService;
    private readonly IMacroLibraryService _macroLibraryService;
    private readonly IPlaybackSettingsStore _playbackSettingsStore;
    private readonly IHotkeySettingsStore _hotkeySettingsStore;
    private readonly IMacroLibraryUserStateStore _macroLibraryUserStateStore;
    private readonly IMutableHotkeyConfiguration _hotkeyConfiguration;
    private readonly IHotkeyService _hotkeyService;
    private readonly IAppLogger _logger;
    private readonly ToolbarControl _toolbarControl = new();
    private readonly TitleBarControl _titleBarControl = new();
    private readonly PlaybackSettingsControl _playbackSettingsControl = new();
    private readonly EventListControl _eventListControl = new();
    private readonly PlaybackControl _playbackControl = new();
    private readonly MacroLibraryControl _macroLibraryControl = new();
    private readonly SessionSummaryControl _sessionSummaryControl = new();
    private MacroPreviewMapDialog? _macroPreviewMapDialog;
    private AppShellLayoutProfile _layoutProfile;

    private MacroSession? _activeSession;
    private string? _lastSessionPath;
    private MacroLibraryUserState _macroLibraryUserState = new();
    private int _playedEventCount;
    private int _playedDurationMs;
    private int? _activePlaybackSourceIndex;
    private int _playbackNavigationInProgress;
    private bool _applyingPlaybackSettings;
    private bool _shutdownInProgress;
    private bool _shutdownCompleted;
    private UiRefreshThrottle? _recordingEventRefreshThrottle;
    private UiRefreshThrottle? _playbackTelemetryRefreshThrottle;
    private UiRefreshThrottle? _playbackSelectionRefreshThrottle;

    public MainForm(
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
        IAppLogger logger)
    {
        _applicationStateService = applicationStateService;
        _macroRecorderService = macroRecorderService;
        _macroPlaybackService = macroPlaybackService;
        _macroOptimizationService = macroOptimizationService;
        _macroStorageService = macroStorageService;
        _macroLibraryService = macroLibraryService;
        _playbackSettingsStore = playbackSettingsStore;
        _hotkeySettingsStore = hotkeySettingsStore;
        _macroLibraryUserStateStore = macroLibraryUserStateStore;
        _hotkeyConfiguration = hotkeyConfiguration;
        _hotkeyService = hotkeyService;
        _logger = logger;

        InitializeComponent();

        var appIcon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
        if (appIcon is not null)
        {
            Icon = appIcon;
        }

        ShowIcon = true;
        ShowInTaskbar = true;

        InitializeDynamicControls();

        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        SubscribeToServiceEvents();
        _ = ExecuteUiActionAsync(InitializeUiAsync, "Uygulama başlatma");
    }

    private async Task InitializeUiAsync()
    {
        try
        {
            await LoadPlaybackSettingsAsync();
        }
        catch (Exception ex)
        {
            ApplyPlaybackSettings(new PlaybackSettings());
            ShowError(
                "Oynatma ayarları yüklenemedi. Varsayılan oynatma ayarları kullanılacak.",
                ex);
        }

        try
        {
            await LoadHotkeySettingsAsync();
        }
        catch (Exception ex)
        {
            _hotkeyConfiguration.Apply(HotkeySettings.CreateDefault());
            ShowError(
                "Kısayol ayarları yüklenemedi. Varsayılan kısayollar kullanılacak.",
                ex);
        }

        try
        {
            await LoadMacroLibraryUserStateAsync();
        }
        catch (Exception ex)
        {
            _macroLibraryUserState = new MacroLibraryUserState();
            ShowError(
                "Makro kütüphanesi kullanıcı tercihleri yüklenemedi. Favori ve son kullanılan bilgileri bu oturumda boş başlatılacak.",
                ex);
        }

        UpdateHotkeySummary();
        try
        {
            await RefreshMacroLibraryAsync();
        }
        catch (Exception ex)
        {
            ShowError(
                "Makro kütüphanesi yüklenemedi. Uygulama boş kütüphane ile devam edecek.",
                ex);
            _macroLibraryControl.SetItems([], null);
        }

        RefreshUiState();
        await InitializeHotkeysAsync();
    }

    private async void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_shutdownCompleted)
        {
            return;
        }

        if (_shutdownInProgress)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        _shutdownInProgress = true;

        try
        {
            await ShutdownAsync();
            _shutdownCompleted = true;
            Close();
        }
        catch (Exception ex)
        {
            _shutdownInProgress = false;
            ShowError("Uygulama kapatılırken bir hata oluştu.", ex);
        }
    }

    private async Task InitializeHotkeysAsync()
    {
        _hotkeyService.RecordToggleRequested += OnRecordToggleRequested;
        _hotkeyService.PlaybackToggleRequested += OnPlaybackToggleRequested;
        _hotkeyService.StopRequested += OnStopRequested;
        _hotkeyService.HotkeySettingsRequested += OnHotkeySettingsRequested;

        try
        {
            await _hotkeyService.RegisterAsync();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Warning,
                nameof(MainForm),
                "Global kısayollar kaydedilemedi. Uygulama odaktayken yerel kısayol yedeği kullanılacak.",
                ex);
        }
    }

    private void OnRecordToggleRequested()
    {
        _ = ExecuteUiActionAsync(HandleRecordToggleAsync, "Kaydı başlat/durdur");
    }

    private async Task HandleRecordToggleAsync()
    {
        if (_shutdownInProgress || _macroPlaybackService.IsPlaying || _macroPlaybackService.IsPaused)
        {
            return;
        }

        if (_macroRecorderService.IsRecording)
        {
            await _macroRecorderService.StopAsync();
            return;
        }

        _lastSessionPath = null;
        await _macroRecorderService.StartAsync();
        RefreshUiState();
    }

    private void OnPlaybackToggleRequested()
    {
        _ = ExecuteUiActionAsync(HandlePlaybackToggleAsync, "Oynat/duraklat");
    }

    private async Task HandlePlaybackToggleAsync()
    {
        if (_shutdownInProgress || _macroRecorderService.IsRecording)
        {
            return;
        }

        if (_macroPlaybackService.IsPaused)
        {
            await _macroPlaybackService.ResumeAsync();
            return;
        }

        if (_macroPlaybackService.IsPlaying)
        {
            await _macroPlaybackService.PauseAsync();
            return;
        }

        if (GetSessionForPlayback() is { Events.Count: > 0 } currentSession)
        {
            await _macroPlaybackService.PlayAsync(currentSession, BuildPlaybackSettings());
        }
    }

    private void OnStopRequested()
    {
        _ = ExecuteUiActionAsync(HandleStopAsync, "Durdurma isteği");
    }

    private void OnHotkeySettingsRequested()
    {
        _ = ExecuteUiActionAsync(EditHotkeysAsync, "Kısayolları aç");
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryHandleLocalHotkeyFallback(keyData))
        {
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task HandleStopAsync()
    {
        if (_macroRecorderService.IsRecording)
        {
            await _macroRecorderService.StopAsync();
        }

        if (_macroPlaybackService.IsPlaying || _macroPlaybackService.IsPaused)
        {
            await _macroPlaybackService.StopAsync();
        }
    }

    private bool TryHandleLocalHotkeyFallback(Keys keyData)
    {
        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.RecordToggleHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.RecordToggleHotkey))
        {
            OnRecordToggleRequested();
            return true;
        }

        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.PlaybackToggleHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.PlaybackToggleHotkey))
        {
            OnPlaybackToggleRequested();
            return true;
        }

        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.StopHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.StopHotkey))
        {
            OnStopRequested();
            return true;
        }

        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.HotkeySettingsHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.HotkeySettingsHotkey))
        {
            OnHotkeySettingsRequested();
            return true;
        }

        return false;
    }

    private bool ShouldUseLocalHotkeyFallback(HotkeyBinding hotkeyBinding)
    {
        return !_hotkeyService.IsHotkeyRegistered(hotkeyBinding);
    }

    private static bool MatchesHotkey(Keys keyData, HotkeyBinding hotkeyBinding)
    {
        return (int)(keyData & Keys.KeyCode) == hotkeyBinding.VirtualKeyCode
            && ResolveKeyDataModifiers(keyData) == hotkeyBinding.Modifiers;
    }

    private static HotkeyModifiers ResolveKeyDataModifiers(Keys keyData)
    {
        HotkeyModifiers modifiers = HotkeyModifiers.None;

        if ((keyData & Keys.Control) == Keys.Control)
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if ((keyData & Keys.Shift) == Keys.Shift)
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if ((keyData & Keys.Alt) == Keys.Alt)
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        return modifiers;
    }

    private async Task ResetPlaybackCursorAsync()
    {
        if (Interlocked.Exchange(ref _playbackNavigationInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            MacroSession? session = GetSessionForPlayback();
            PlaybackSettings playbackSettings = BuildPlaybackSettings();

            if (!CanNavigatePlayback(session, playbackSettings))
            {
                return;
            }

            if (_macroPlaybackService.IsPaused)
            {
                await _macroPlaybackService.WaitForPlaybackNavigationReadyAsync();
            }

            _playedEventCount = 0;
            _playedDurationMs = 0;
            _activePlaybackSourceIndex = session is { Events.Count: > 0 }
                ? 0
                : null;

            if (_macroPlaybackService.IsPaused)
            {
                await _macroPlaybackService.SeekPlaybackCursorAsync(_playedEventCount);
            }

            RefreshUiState();
        }
        finally
        {
            Interlocked.Exchange(ref _playbackNavigationInProgress, 0);
        }
    }

    private async Task StepPlaybackAsync(int stepDirection)
    {
        if (Interlocked.Exchange(ref _playbackNavigationInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            MacroSession? session = GetSessionForPlayback();
            PlaybackSettings playbackSettings = BuildPlaybackSettings();

            if (session is null || !CanNavigatePlayback(session, playbackSettings))
            {
                return;
            }

            if (_macroPlaybackService.IsPaused)
            {
                await _macroPlaybackService.WaitForPlaybackNavigationReadyAsync();
            }

            int totalPlaybackEvents = GetTotalPlaybackEventCount(session, playbackSettings);

            if (stepDirection <= 0)
            {
                StepPlaybackCursorBack(session);

                if (_macroPlaybackService.IsPaused)
                {
                    await _macroPlaybackService.SeekPlaybackCursorAsync(_playedEventCount);
                }

                return;
            }

            int targetLogicalIndex = _playedEventCount;

            if (targetLogicalIndex >= totalPlaybackEvents)
            {
                return;
            }

            int sourceEventIndex = targetLogicalIndex % session.Events.Count;
            await _macroPlaybackService.PlayEventAtAsync(
                session,
                playbackSettings,
                sourceEventIndex,
                targetLogicalIndex);

            _playedEventCount = targetLogicalIndex + 1;
            _playedDurationMs = CalculatePlayedDurationMs(session, targetLogicalIndex);
            _activePlaybackSourceIndex = sourceEventIndex;

            if (_macroPlaybackService.IsPaused)
            {
                await _macroPlaybackService.SeekPlaybackCursorAsync(_playedEventCount);
            }

            RefreshUiState();
        }
        finally
        {
            Interlocked.Exchange(ref _playbackNavigationInProgress, 0);
        }
    }

    private void StepPlaybackCursorBack(MacroSession session)
    {
        if (_playedEventCount <= 0)
        {
            return;
        }

        int previousPlayedEventCount = Math.Max(0, _playedEventCount - 1);
        _playedEventCount = previousPlayedEventCount;
        _playedDurationMs = previousPlayedEventCount == 0
            ? 0
            : CalculatePlayedDurationMs(session, previousPlayedEventCount - 1);
        _activePlaybackSourceIndex = previousPlayedEventCount == 0
            ? 0
            : (previousPlayedEventCount - 1) % session.Events.Count;
        RefreshUiState();
    }

    private async Task ShutdownAsync()
    {
        _recordingEventRefreshThrottle?.Cancel();
        _playbackTelemetryRefreshThrottle?.Cancel();
        _playbackSelectionRefreshThrottle?.Cancel();

        _hotkeyService.RecordToggleRequested -= OnRecordToggleRequested;
        _hotkeyService.PlaybackToggleRequested -= OnPlaybackToggleRequested;
        _hotkeyService.StopRequested -= OnStopRequested;
        _hotkeyService.HotkeySettingsRequested -= OnHotkeySettingsRequested;
        UnsubscribeFromServiceEvents();

        if (_hotkeyService.IsRegistered)
        {
            await _hotkeyService.UnregisterAsync();
        }

        if (_macroRecorderService.IsRecording)
        {
            await _macroRecorderService.StopAsync();
        }

        if (_macroPlaybackService.IsPlaying || _macroPlaybackService.IsPaused)
        {
            await _macroPlaybackService.StopAsync();
        }

        await SavePlaybackSettingsAsync();
        await SaveHotkeySettingsAsync();
    }

    private void DisposeDynamicResources()
    {
        _recordingEventRefreshThrottle?.Dispose();
        _recordingEventRefreshThrottle = null;
        _playbackTelemetryRefreshThrottle?.Dispose();
        _playbackTelemetryRefreshThrottle = null;
        _playbackSelectionRefreshThrottle?.Dispose();
        _playbackSelectionRefreshThrottle = null;
    }

    private async Task ExecuteUiActionAsync(
        Func<Task> action,
        string operationName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowError($"{operationName} işlemi başarısız oldu.", ex);
        }
    }

    private void ShowError(string message, Exception exception)
    {
        if (IsDisposed)
        {
            return;
        }

        string detail = string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;

        _logger.Log(
            AppLogLevel.Error,
            nameof(MainForm),
            message,
            exception);

        ModalDialogOverlay.ShowMessage(
            this,
            $"{message}{Environment.NewLine}{Environment.NewLine}{detail}",
            "Polly",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void SubscribeToServiceEvents()
    {
        _applicationStateService.StateChanged += OnApplicationStateChanged;
        _macroRecorderService.RecordingStarted += OnRecordingStarted;
        _macroRecorderService.EventRecorded += OnEventRecorded;
        _macroRecorderService.RecordingStopped += OnRecordingStopped;
        _macroPlaybackService.PlaybackStarted += OnPlaybackStarted;
        _macroPlaybackService.PlaybackPaused += OnPlaybackStateChanged;
        _macroPlaybackService.PlaybackResumed += OnPlaybackStateChanged;
        _macroPlaybackService.PlaybackStopped += OnPlaybackStopped;
        _macroPlaybackService.EventPlayed += OnPlaybackEventPlayed;

        _activeSession = _macroRecorderService.CurrentSession;
    }

    private void UnsubscribeFromServiceEvents()
    {
        _applicationStateService.StateChanged -= OnApplicationStateChanged;
        _macroRecorderService.RecordingStarted -= OnRecordingStarted;
        _macroRecorderService.EventRecorded -= OnEventRecorded;
        _macroRecorderService.RecordingStopped -= OnRecordingStopped;
        _macroPlaybackService.PlaybackStarted -= OnPlaybackStarted;
        _macroPlaybackService.PlaybackPaused -= OnPlaybackStateChanged;
        _macroPlaybackService.PlaybackResumed -= OnPlaybackStateChanged;
        _macroPlaybackService.PlaybackStopped -= OnPlaybackStopped;
        _macroPlaybackService.EventPlayed -= OnPlaybackEventPlayed;
    }

    private void OnApplicationStateChanged(AppState state)
    {
        _ = state;
        RequestUiRefresh();
    }

    private void OnRecordingStarted()
    {
        _activeSession = _macroRecorderService.CurrentSession;
        _lastSessionPath = null;
        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
        RequestUiRefresh();
    }

    private void OnEventRecorded(MacroEvent macroEvent)
    {
        _ = macroEvent;
        RequestRecordingEventRefresh();
    }

    private void OnRecordingStopped(MacroSession session)
    {
        _activeSession = session;
        _recordingEventRefreshThrottle?.Cancel();
        RequestUiRefresh();
    }

    private void OnPlaybackStarted()
    {
        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
        CancelPendingPlaybackTelemetryRefresh();
        CancelPendingPlaybackSelectionRefresh();
        RequestUiRefresh();
    }

    private void OnPlaybackStateChanged()
    {
        CancelPendingPlaybackTelemetryRefresh();
        CancelPendingPlaybackSelectionRefresh();
        RequestUiRefresh();
    }

    private void OnPlaybackEventPlayed(MacroEvent macroEvent)
    {
        _activePlaybackSourceIndex = ResolveCurrentPlaybackSourceIndex();
        _playedEventCount = SaturatingAdd(_playedEventCount, 1);
        _playedDurationMs = SaturatingAdd(_playedDurationMs, macroEvent.DelayMs);
        RequestPlaybackTelemetryRefresh();
        RequestPlaybackSelectionRefresh();
    }

    private int? ResolveCurrentPlaybackSourceIndex()
    {
        MacroSession? session = GetSessionForPlayback();

        if (session is not { Events.Count: > 0 })
        {
            return null;
        }

        return Math.Clamp(
            _playedEventCount % session.Events.Count,
            0,
            session.Events.Count - 1);
    }

    private void OnPlaybackStopped()
    {
        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
        CancelPendingPlaybackTelemetryRefresh();
        CancelPendingPlaybackSelectionRefresh();
        RequestUiRefresh();
    }

    private void recordToggleButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        OnRecordToggleRequested();
    }

    private void playbackToggleButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        OnPlaybackToggleRequested();
    }

    private void stopButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        OnStopRequested();
    }

    private void playbackSkipBackButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(ResetPlaybackCursorAsync, "Oynatma başa alma");
    }

    private void playbackStepBackButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(() => StepPlaybackAsync(stepDirection: -1), "Oynatma geri adım");
    }

    private void playbackStepForwardButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(() => StepPlaybackAsync(stepDirection: 1), "Oynatma ileri adım");
    }

    private void saveJsonButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(SaveJsonAsync, "JSON kaydetme");
    }

    private void saveLibraryButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(SaveToLibraryAsync, "Makro kütüphanesine kaydetme");
    }

    private void loadJsonButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(LoadJsonAsync, "JSON yükleme");
    }

    private void importLibraryMacroButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(ImportLibraryMacroAsync, "Makro kütüphanesine içe aktarma");
    }

    private void saveXmlButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(SaveXmlAsync, "XML kaydetme");
    }

    private void saveHtmlReportButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(SaveHtmlReportAsync, "HTML rapor oluşturma");
    }

    private void saveTextReportButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(SaveTextReportAsync, "TXT rapor oluşturma");
    }

    private void loadXmlButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(LoadXmlAsync, "XML yükleme");
    }

    private void clearSessionButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(ClearSessionAsync, "Oturum temizleme");
    }

    private void editHotkeysButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(EditHotkeysAsync, "Kısayol ayarları");
    }

    private void macroLibraryControl_LoadRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => LoadLibraryMacroAsync(e.Item), "Makro kütüphanesi yükleme");
    }

    private void macroLibraryControl_RenameRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => RenameLibraryMacroAsync(e.Item), "Makro kütüphanesi isim düzenleme");
    }

    private void macroLibraryControl_DeleteRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => DeleteLibraryMacroAsync(e.Item), "Makro kütüphanesi silme");
    }

    private void macroLibraryControl_FavoriteToggled(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => ToggleLibraryFavoriteAsync(e.Item), "Makro favori değiştirme");
    }

    private void macroLibraryControl_OptimizeRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => OptimizeLibraryMacroAsync(e.Item), "Makro kütüphanesi optimizasyonu");
    }

    private void eventListControl_EventEditRequested(object? sender, EventEditRequestedEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => EditEventAsync(e), "Olay düzenleme");
    }

    private void preserveTimingCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (_applyingPlaybackSettings)
        {
            return;
        }

        RefreshUiState();
    }

    private void loopIndefinitelyCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (_applyingPlaybackSettings)
        {
            return;
        }

        RefreshUiState();
    }

    private void playbackSettingControl_ValueChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        if (_applyingPlaybackSettings)
        {
            return;
        }

        RefreshUiState();
    }

    private async Task SaveJsonAsync()
    {
        MacroSession session = GetRequiredSession();
        EnsureSessionMutationAllowed();

        using var dialog = new SaveFileDialog
        {
            Filter = "JSON makro (*.json)|*.json|Tüm dosyalar (*.*)|*.*",
            FileName = BuildDefaultFileName(session.Name, ".json"),
            AddExtension = true,
            DefaultExt = "json",
            RestoreDirectory = true
        };

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        await _macroStorageService.SaveAsJsonAsync(session, dialog.FileName);
        _lastSessionPath = dialog.FileName;
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task SaveToLibraryAsync()
    {
        MacroSession session = GetRequiredSession();
        EnsureSessionMutationAllowed();

        MacroLibraryFileFormat format = ResolveCurrentLibrarySaveFormat();
        string? macroName = await PromptForLibraryMacroNameAsync(session, format);

        if (macroName is null)
        {
            return;
        }

        string previousName = session.Name;
        session.Name = macroName;

        string filePath;

        try
        {
            filePath = await _macroLibraryService.SaveAsync(session, format);
        }
        catch
        {
            session.Name = previousName;
            throw;
        }

        _lastSessionPath = filePath;
        MarkLibraryFileUsed(filePath);
        await TrySaveMacroLibraryUserStateAsync("Makro kütüphanesi son kullanılan kaydetme");
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task<string?> PromptForLibraryMacroNameAsync(
        MacroSession session,
        MacroLibraryFileFormat format)
    {
        string suggestedName = session.Name;

        while (true)
        {
            using var dialog = MacroNameEditDialog.CreateSaveDialog(suggestedName);

            if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
            {
                return null;
            }

            string requestedName = dialog.MacroName;

            if (!await IsLibraryNameInUseByAnotherMacroAsync(requestedName, format))
            {
                return requestedName;
            }

            ModalDialogOverlay.ShowMessage(
                this,
                $"Bu isimde bir makro zaten var: {requestedName}",
                "Polly",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            suggestedName = requestedName;
        }
    }

    private async Task<bool> IsLibraryNameInUseByAnotherMacroAsync(
        string macroName,
        MacroLibraryFileFormat format)
    {
        IReadOnlyList<MacroLibraryEntry> entries = await _macroLibraryService.ListAsync();

        return entries.Any(entry =>
            entry.Format == format
            && string.Equals(entry.Name, macroName, StringComparison.OrdinalIgnoreCase)
            && !IsSamePath(_lastSessionPath, entry.FilePath));
    }

    private async Task LoadJsonAsync()
    {
        EnsureSessionMutationAllowed();

        using var dialog = new OpenFileDialog
        {
            Filter = "JSON makro (*.json)|*.json|Tüm dosyalar (*.*)|*.*",
            RestoreDirectory = true
        };

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        MacroSession session = await _macroStorageService.LoadFromJsonAsync(dialog.FileName);
        AdoptLoadedSession(session, dialog.FileName);
        await RefreshMacroLibraryAsync();
    }

    private async Task ImportLibraryMacroAsync()
    {
        EnsureSessionMutationAllowed();

        using var dialog = new OpenFileDialog
        {
            Filter = "Makro dosyaları (*.json;*.xml)|*.json;*.xml|JSON makro (*.json)|*.json|XML makro (*.xml)|*.xml|Tüm dosyalar (*.*)|*.*",
            RestoreDirectory = true
        };

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        string importedFilePath = await _macroLibraryService.ImportAsync(dialog.FileName);
        MacroSession session = await _macroLibraryService.LoadAsync(importedFilePath);

        AdoptLoadedSession(session, importedFilePath);
        MarkLibraryFileUsed(importedFilePath);
        await TrySaveMacroLibraryUserStateAsync("Makro kütüphanesi son kullanılan kaydetme");
        await RefreshMacroLibraryAsync();
    }

    private async Task SaveXmlAsync()
    {
        MacroSession session = GetRequiredSession();
        EnsureSessionMutationAllowed();

        using var dialog = new SaveFileDialog
        {
            Filter = "XML makro (*.xml)|*.xml|Tüm dosyalar (*.*)|*.*",
            FileName = BuildDefaultFileName(session.Name, ".xml"),
            AddExtension = true,
            DefaultExt = "xml",
            RestoreDirectory = true
        };

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        await _macroStorageService.SaveAsXmlAsync(session, dialog.FileName);
        _lastSessionPath = dialog.FileName;
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task SaveHtmlReportAsync()
    {
        await SaveReportAsync(
            "HTML rapor (*.html)|*.html|Tüm dosyalar (*.*)|*.*",
            ".html",
            "html",
            MacroReportGenerator.GenerateHtml);
    }

    private async Task SaveTextReportAsync()
    {
        await SaveReportAsync(
            "TXT rapor (*.txt)|*.txt|Tüm dosyalar (*.*)|*.*",
            ".txt",
            "txt",
            MacroReportGenerator.GenerateText);
    }

    private async Task SaveReportAsync(
        string filter,
        string extension,
        string defaultExtension,
        Func<MacroSession, string?, string> buildReport)
    {
        MacroSession session = GetRequiredSession();
        EnsureSessionMutationAllowed();

        using var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = BuildDefaultFileName(session.Name + "_Rapor", extension),
            AddExtension = true,
            DefaultExt = defaultExtension,
            RestoreDirectory = true
        };

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        string reportContent = buildReport(session, _lastSessionPath);
        await File.WriteAllTextAsync(dialog.FileName, reportContent, Encoding.UTF8);
    }

    private async Task LoadXmlAsync()
    {
        EnsureSessionMutationAllowed();

        using var dialog = new OpenFileDialog
        {
            Filter = "XML makro (*.xml)|*.xml|Tüm dosyalar (*.*)|*.*",
            RestoreDirectory = true
        };

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        MacroSession session = await _macroStorageService.LoadFromXmlAsync(dialog.FileName);
        AdoptLoadedSession(session, dialog.FileName);
        await RefreshMacroLibraryAsync();
    }

    private async Task ClearSessionAsync()
    {
        EnsureSessionMutationAllowed();

        _macroRecorderService.Clear();
        _activeSession = null;
        _lastSessionPath = null;
        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task LoadLibraryMacroAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureSessionMutationAllowed();

        MacroSession session = await _macroLibraryService.LoadAsync(item.FilePath);
        AdoptLoadedSession(session, item.FilePath);
        MarkLibraryFileUsed(item.FilePath);
        await TrySaveMacroLibraryUserStateAsync("Makro kütüphanesi son kullanılan kaydetme");
        await RefreshMacroLibraryAsync();
    }

    private async Task RenameLibraryMacroAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureSessionMutationAllowed();

        using var dialog = new MacroNameEditDialog(item.Name);

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        string previousFilePath = item.FilePath;
        string renamedFilePath = await _macroLibraryService.RenameAsync(
            previousFilePath,
            dialog.MacroName);

        if (IsSamePath(_lastSessionPath, previousFilePath))
        {
            _lastSessionPath = renamedFilePath;

            if (_activeSession is not null)
            {
                _activeSession.Name = Path.GetFileNameWithoutExtension(renamedFilePath);
            }
        }

        MoveLibraryStatePath(previousFilePath, renamedFilePath);
        await TrySaveMacroLibraryUserStateAsync("Makro kütüphanesi kullanıcı tercihi taşıma");
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task DeleteLibraryMacroAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureSessionMutationAllowed();

        if (!ThemedConfirmationDialog.ConfirmMacroDelete(this, item.Name))
        {
            return;
        }

        await _macroLibraryService.DeleteAsync(item.FilePath);

        if (IsSamePath(_lastSessionPath, item.FilePath))
        {
            _lastSessionPath = null;
        }

        RemoveLibraryStatePath(item.FilePath);
        await TrySaveMacroLibraryUserStateAsync("Makro kütüphanesi kullanıcı tercihi silme");
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task ToggleLibraryFavoriteAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);

        string normalizedFilePath = NormalizeLibraryStatePath(item.FilePath);

        if (!RemoveFavoritePath(normalizedFilePath))
        {
            _macroLibraryUserState.FavoriteFilePaths.Add(normalizedFilePath);
        }

        await SaveMacroLibraryUserStateAsync();
        await RefreshMacroLibraryAsync();
    }

    private Task EditEventAsync(EventEditRequestedEventArgs editRequest)
    {
        ArgumentNullException.ThrowIfNull(editRequest);
        EnsureSessionMutationAllowed();

        MacroSession session = GetRequiredSession();

        if (editRequest.EventIndex < 0 || editRequest.EventIndex >= session.Events.Count)
        {
            throw new InvalidOperationException("Düzenlenecek olay aktif oturumda bulunamadı.");
        }

        MacroEvent macroEvent = session.Events[editRequest.EventIndex];

        using var dialog = new EventEditDialog(editRequest.EventIndex, macroEvent);

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return Task.CompletedTask;
        }

        macroEvent.DelayMs = dialog.EditResult.DelayMs;

        if (dialog.EditResult.X.HasValue && dialog.EditResult.Y.HasValue)
        {
            macroEvent.X = dialog.EditResult.X;
            macroEvent.Y = dialog.EditResult.Y;
        }

        RefreshUiState(forceEventListReload: true);
        return Task.CompletedTask;
    }

    private async Task OptimizeLibraryMacroAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureSessionMutationAllowed();

        MacroSession session = await _macroLibraryService.LoadAsync(item.FilePath);
        MacroOptimizationPreview preview = _macroOptimizationService.Preview(session);

        if (!preview.HasChanges)
        {
            MacroOptimizationDialog.ShowNoChanges(this);
            return;
        }

        using var dialog = new MacroOptimizationDialog(preview);

        if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
        {
            return;
        }

        session.ReplaceEvents(preview.OptimizedEvents);
        await SaveOptimizedLibraryMacroAsync(session, item);

        bool optimizedActiveSession = IsSamePath(_lastSessionPath, item.FilePath);

        if (optimizedActiveSession)
        {
            _activeSession = session;
            _playedEventCount = 0;
            _playedDurationMs = 0;
            _activePlaybackSourceIndex = null;
        }

        MarkLibraryFileUsed(item.FilePath);
        await TrySaveMacroLibraryUserStateAsync("Makro kütüphanesi optimizasyon son kullanılan kaydetme");
        await RefreshMacroLibraryAsync();
        RefreshUiState(forceEventListReload: optimizedActiveSession);
    }

    private Task SaveOptimizedLibraryMacroAsync(MacroSession session, MacroLibraryEntry item)
    {
        return item.Format switch
        {
            MacroLibraryFileFormat.Json => _macroStorageService.SaveAsJsonAsync(session, item.FilePath),
            MacroLibraryFileFormat.Xml => _macroStorageService.SaveAsXmlAsync(session, item.FilePath),
            _ => throw new NotSupportedException($"Desteklenmeyen makro dosya formatı: {item.FilePath}")
        };
    }

    private async Task RefreshMacroLibraryAsync()
    {
        IReadOnlyList<MacroLibraryEntry> entries = await _macroLibraryService.ListAsync();

        if (PruneMacroLibraryUserState(entries))
        {
            await TrySaveMacroLibraryUserStateAsync(
                "Makro kütüphanesi kullanıcı tercihleri temizleme");
        }

        _macroLibraryControl.SetItems(BuildMacroLibraryViewItems(entries), _lastSessionPath);
    }

    private async Task LoadMacroLibraryUserStateAsync()
    {
        _macroLibraryUserState = await _macroLibraryUserStateStore.LoadAsync();
    }

    private async Task SaveMacroLibraryUserStateAsync()
    {
        await _macroLibraryUserStateStore.SaveAsync(_macroLibraryUserState);
    }

    private async Task TrySaveMacroLibraryUserStateAsync(string operationName)
    {
        try
        {
            await SaveMacroLibraryUserStateAsync();
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Warning,
                nameof(MainForm),
                $"{operationName} işlemi tamamlanamadı.",
                ex);
        }
    }

    private MacroLibraryViewItem[] BuildMacroLibraryViewItems(
        IReadOnlyList<MacroLibraryEntry> entries)
    {
        return entries
            .Select(entry => new MacroLibraryViewItem(
                entry,
                IsFavoritePath(entry.FilePath),
                GetLastUsedUtc(entry.FilePath)))
            .ToArray();
    }

    private bool PruneMacroLibraryUserState(IReadOnlyList<MacroLibraryEntry> entries)
    {
        var activeFilePaths = entries
            .Select(entry => NormalizeLibraryStatePath(entry.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int originalFavoriteCount = _macroLibraryUserState.FavoriteFilePaths.Count;
        int originalRecentCount = _macroLibraryUserState.LastUsedUtcByFilePath.Count;
        bool changed = false;

        List<string> normalizedFavoriteFilePaths = _macroLibraryUserState.FavoriteFilePaths
            .Select(NormalizeLibraryStatePath)
            .Where(activeFilePaths.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        changed = changed
            || originalFavoriteCount != normalizedFavoriteFilePaths.Count
            || !_macroLibraryUserState.FavoriteFilePaths.SequenceEqual(
                normalizedFavoriteFilePaths,
                StringComparer.OrdinalIgnoreCase);
        _macroLibraryUserState.FavoriteFilePaths = normalizedFavoriteFilePaths;

        foreach (string filePath in _macroLibraryUserState.LastUsedUtcByFilePath.Keys.ToArray())
        {
            string normalizedFilePath = NormalizeLibraryStatePath(filePath);

            if (!activeFilePaths.Contains(normalizedFilePath))
            {
                _macroLibraryUserState.LastUsedUtcByFilePath.Remove(filePath);
                changed = true;
            }
            else if (!string.Equals(filePath, normalizedFilePath, StringComparison.Ordinal))
            {
                DateTime lastUsedUtc = _macroLibraryUserState.LastUsedUtcByFilePath[filePath];
                _macroLibraryUserState.LastUsedUtcByFilePath.Remove(filePath);
                _macroLibraryUserState.LastUsedUtcByFilePath[normalizedFilePath] = NormalizeUtc(lastUsedUtc);
                changed = true;
            }
        }

        return changed
            || originalRecentCount != _macroLibraryUserState.LastUsedUtcByFilePath.Count;
    }

    private void MarkLibraryFileUsed(string filePath)
    {
        string normalizedFilePath = NormalizeLibraryStatePath(filePath);
        _macroLibraryUserState.LastUsedUtcByFilePath[normalizedFilePath] = DateTime.UtcNow;
    }

    private void MoveLibraryStatePath(
        string previousFilePath,
        string nextFilePath)
    {
        string previousNormalizedFilePath = NormalizeLibraryStatePath(previousFilePath);
        string nextNormalizedFilePath = NormalizeLibraryStatePath(nextFilePath);

        if (string.Equals(previousNormalizedFilePath, nextNormalizedFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool wasFavorite = RemoveFavoritePath(previousNormalizedFilePath);

        if (wasFavorite)
        {
            _macroLibraryUserState.FavoriteFilePaths.Add(nextNormalizedFilePath);
        }

        if (_macroLibraryUserState.LastUsedUtcByFilePath.TryGetValue(
                previousNormalizedFilePath,
                out DateTime lastUsedUtc))
        {
            _macroLibraryUserState.LastUsedUtcByFilePath.Remove(previousNormalizedFilePath);
            _macroLibraryUserState.LastUsedUtcByFilePath[nextNormalizedFilePath] = NormalizeUtc(lastUsedUtc);
        }
    }

    private void RemoveLibraryStatePath(string filePath)
    {
        string normalizedFilePath = NormalizeLibraryStatePath(filePath);
        RemoveFavoritePath(normalizedFilePath);
        _macroLibraryUserState.LastUsedUtcByFilePath.Remove(normalizedFilePath);
    }

    private bool IsFavoritePath(string filePath)
    {
        string normalizedFilePath = NormalizeLibraryStatePath(filePath);
        return _macroLibraryUserState.FavoriteFilePaths.Any(path =>
            string.Equals(
                NormalizeLibraryStatePath(path),
                normalizedFilePath,
                StringComparison.OrdinalIgnoreCase));
    }

    private DateTime? GetLastUsedUtc(string filePath)
    {
        string normalizedFilePath = NormalizeLibraryStatePath(filePath);

        return _macroLibraryUserState.LastUsedUtcByFilePath.TryGetValue(
            normalizedFilePath,
            out DateTime lastUsedUtc)
            ? NormalizeUtc(lastUsedUtc)
            : null;
    }

    private bool RemoveFavoritePath(string normalizedFilePath)
    {
        bool removed = false;

        for (int index = _macroLibraryUserState.FavoriteFilePaths.Count - 1; index >= 0; index--)
        {
            if (string.Equals(
                    NormalizeLibraryStatePath(_macroLibraryUserState.FavoriteFilePaths[index]),
                    normalizedFilePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                _macroLibraryUserState.FavoriteFilePaths.RemoveAt(index);
                removed = true;
            }
        }

        return removed;
    }

    private static string NormalizeLibraryStatePath(string filePath)
    {
        return Path.GetFullPath(filePath);
    }

    private static DateTime NormalizeUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc),
            _ => timestamp
        };
    }

    private void AdoptLoadedSession(MacroSession session, string filePath)
    {
        _activeSession = session;
        _lastSessionPath = filePath;
        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
        RefreshUiState();
    }

    private async Task LoadPlaybackSettingsAsync()
    {
        PlaybackSettings playbackSettings = await _playbackSettingsStore.LoadAsync();
        ApplyPlaybackSettings(playbackSettings);
    }

    private async Task SavePlaybackSettingsAsync()
    {
        await _playbackSettingsStore.SaveAsync(BuildPlaybackSettings());
    }

    private async Task LoadHotkeySettingsAsync()
    {
        HotkeySettings hotkeySettings = await _hotkeySettingsStore.LoadAsync();
        _hotkeyConfiguration.Apply(hotkeySettings);
    }

    private async Task SaveHotkeySettingsAsync()
    {
        await _hotkeySettingsStore.SaveAsync(_hotkeyConfiguration.Snapshot());
    }

    private void ApplyPlaybackSettings(PlaybackSettings playbackSettings)
    {
        ArgumentNullException.ThrowIfNull(playbackSettings);

        _applyingPlaybackSettings = true;

        try
        {
            _playbackSettingsControl.ApplySettings(playbackSettings);
        }
        finally
        {
            _applyingPlaybackSettings = false;
        }
    }

    private MacroSession? GetSessionForPlayback()
    {
        if (_macroRecorderService.IsRecording)
        {
            return _macroRecorderService.CurrentSession;
        }

        return _activeSession ?? _macroRecorderService.CurrentSession;
    }

    private MacroSession GetRequiredSession()
    {
        return GetSessionForPlayback() is { Events.Count: > 0 } session
            ? session
            : throw new InvalidOperationException("Kullanılacak kayıtlı veya yüklenmiş bir oturum yok.");
    }

    private void EnsureSessionMutationAllowed()
    {
        if (_shutdownInProgress
            || _applicationStateService.IsAny(AppState.Recording, AppState.Playing, AppState.Paused, AppState.Stopping))
        {
            throw new InvalidOperationException(
                "Oturum kaydetme ve yükleme işlemleri yalnızca uygulama boştayken kullanılabilir.");
        }
    }

    private void EnsureHotkeyMutationAllowed()
    {
        if (_shutdownInProgress || !_applicationStateService.IsState(AppState.Idle))
        {
            throw new InvalidOperationException(
                "Kısayol değişiklikleri yalnızca uygulama boştayken yapılabilir.");
        }
    }

    private void RefreshUiState(bool forceEventListReload = false)
    {
        MacroSession? displayedSession = GetSessionForPlayback();
        bool hasSession = displayedSession is { Events.Count: > 0 };
        PlaybackSettings playbackSettings = BuildPlaybackSettings();

        _eventListControl.SetSession(displayedSession, forceEventListReload);
        SelectActivePlaybackCursor(displayedSession, playbackSettings);

        bool isIdle = _applicationStateService.IsState(AppState.Idle);
        bool canRecord = !_shutdownInProgress
            && !_macroPlaybackService.IsPlaying
            && !_macroPlaybackService.IsPaused;
        bool canPlayback = !_shutdownInProgress
            && !_macroRecorderService.IsRecording
            && (_macroPlaybackService.IsPlaying || _macroPlaybackService.IsPaused || hasSession);
        bool canStop = !_shutdownInProgress
            && (_macroRecorderService.IsRecording || _macroPlaybackService.IsPlaying || _macroPlaybackService.IsPaused);
        bool canNavigatePlayback = CanNavigatePlayback(displayedSession, playbackSettings);
        bool canSaveSession = !_shutdownInProgress && isIdle && hasSession;
        bool canLoadSession = !_shutdownInProgress && isIdle;
        bool canEditHotkeys = !_shutdownInProgress && isIdle;
        bool playbackSettingsEnabled = !_shutdownInProgress
            && !_macroRecorderService.IsRecording
            && !_macroPlaybackService.IsPlaying
            && !_macroPlaybackService.IsPaused;
        _playbackSettingsControl.SetControlsEnabled(playbackSettingsEnabled);

        _toolbarControl.UpdateRecordButton(_macroRecorderService.IsRecording);
        _toolbarControl.UpdatePlaybackButton(
            _macroPlaybackService.IsPaused
                ? PlaybackButtonState.Resume
                : _macroPlaybackService.IsPlaying
                    ? PlaybackButtonState.Pause
                    : PlaybackButtonState.Play);
        _toolbarControl.SetButtonsEnabled(
            new ToolbarButtonState(
                canRecord,
                canStop,
                canPlayback,
                canSaveSession,
                canSaveSession,
                canSaveSession,
                canLoadSession,
                canLoadSession,
                canEditHotkeys));
        _playbackControl.UpdateState(
            BuildPlaybackControlState(
                displayedSession,
                playbackSettings,
                canPlayback,
                canStop,
                canNavigatePlayback));
        string statusText = ResolveStatusText(_applicationStateService.CurrentState, playbackSettings);
        var summaryState = BuildSessionSummaryState(displayedSession, statusText);
        _sessionSummaryControl.UpdateState(
            summaryState,
            displayedSession?.Events,
            _activePlaybackSourceIndex);
        UpdateMacroPreviewMapDialog(
            summaryState,
            displayedSession?.Events,
            _activePlaybackSourceIndex);
        _titleBarControl.SetStatus(
            statusText,
            ResolveTitleBarStatusColor(_applicationStateService.CurrentState));
        _titleBarControl.SetMaximized(WindowState == FormWindowState.Maximized);
    }

    private SessionSummaryState BuildSessionSummaryState(
        MacroSession? displayedSession,
        string statusText)
    {
        return new SessionSummaryState(
            statusText,
            displayedSession?.Name ?? "Oturum yok",
            displayedSession?.Events.Count ?? 0,
            displayedSession?.TotalDurationMs ?? 0,
            string.IsNullOrWhiteSpace(_lastSessionPath)
                ? "Kaydedilmedi"
                : Path.GetFileName(_lastSessionPath));
    }

    private void UpdateMacroPreviewMapDialog(
        SessionSummaryState summaryState,
        IReadOnlyList<MacroEvent>? events,
        int? activeSourceEventIndex)
    {
        if (_macroPreviewMapDialog is null
            || _macroPreviewMapDialog.IsDisposed
            || !_macroPreviewMapDialog.Visible)
        {
            return;
        }

        _macroPreviewMapDialog.UpdatePreview(
            summaryState,
            events,
            activeSourceEventIndex);
    }

    private void UpdateActivePreviewMapCursor(int? activeSourceEventIndex)
    {
        _sessionSummaryControl.UpdatePreviewActiveSourceEventIndex(activeSourceEventIndex);

        if (_macroPreviewMapDialog is null
            || _macroPreviewMapDialog.IsDisposed
            || !_macroPreviewMapDialog.Visible)
        {
            return;
        }

        _macroPreviewMapDialog.UpdateActiveSourceEventIndex(activeSourceEventIndex);
    }

    private void ShowMacroPreviewMapDialog(
        SessionSummaryState summaryState,
        IReadOnlyList<MacroEvent>? events,
        int? activeSourceEventIndex,
        Rectangle anchorScreenBounds)
    {
        if (_macroPreviewMapDialog is null || _macroPreviewMapDialog.IsDisposed)
        {
            _macroPreviewMapDialog = new MacroPreviewMapDialog();
            _macroPreviewMapDialog.FormClosed += (_, _) => _macroPreviewMapDialog = null;
        }

        _macroPreviewMapDialog.UpdatePreview(
            summaryState,
            events,
            activeSourceEventIndex);

        if (!_macroPreviewMapDialog.Visible)
        {
            _macroPreviewMapDialog.PositionNear(
                anchorScreenBounds,
                RectangleToScreen(ClientRectangle));
            _macroPreviewMapDialog.Show(this);
        }

        _macroPreviewMapDialog.BringToFront();
        _macroPreviewMapDialog.Activate();
    }

    private void SelectActivePlaybackCursor(
        MacroSession? displayedSession,
        PlaybackSettings playbackSettings)
    {
        if (!ShouldFollowActivePlaybackCursor(displayedSession, playbackSettings))
        {
            return;
        }

        _eventListControl.TrySelectSourceEvent(_activePlaybackSourceIndex.GetValueOrDefault());
    }

    private bool ShouldFollowActivePlaybackCursor(
        MacroSession? displayedSession,
        PlaybackSettings playbackSettings)
    {
        if (displayedSession is not { Events.Count: > 0 }
            || !_activePlaybackSourceIndex.HasValue)
        {
            return false;
        }

        bool shouldFollowIdleDebugCursor = _applicationStateService.IsState(AppState.Idle);
        bool shouldFollowPausedDebugCursor = _applicationStateService.IsState(AppState.Paused);
        bool shouldFollowSimulationPlayback = playbackSettings.SimulationMode
            && _applicationStateService.IsState(AppState.Playing);

        return shouldFollowIdleDebugCursor
            || shouldFollowPausedDebugCursor
            || shouldFollowSimulationPlayback;
    }

    private void RequestUiRefresh()
    {
        if (!CanRunDeferredUiRefresh())
        {
            return;
        }

        if (InvokeRequired)
        {
            TryBeginInvokeOnUiThread(() => RefreshUiState());
            return;
        }

        RefreshUiState();
    }

    private bool CanRunDeferredUiRefresh()
    {
        return !_shutdownInProgress && IsHandleCreated && !IsDisposed;
    }

    private void TryBeginInvokeOnUiThread(Action action)
    {
        try
        {
            BeginInvoke(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void RequestPlaybackTelemetryRefresh()
    {
        if (_playbackTelemetryRefreshThrottle is null)
        {
            RequestUiRefresh();
            return;
        }

        _playbackTelemetryRefreshThrottle.Request();
    }

    private void RefreshPlaybackTelemetry()
    {
        MacroSession? displayedSession = GetSessionForPlayback();
        PlaybackSettings playbackSettings = BuildPlaybackSettings();
        _playbackControl.UpdateTelemetry(BuildPlaybackTelemetrySnapshot(displayedSession, playbackSettings));
    }

    private void CancelPendingPlaybackTelemetryRefresh()
    {
        _playbackTelemetryRefreshThrottle?.Cancel();
    }

    private void RequestPlaybackSelectionRefresh()
    {
        if (_playbackSelectionRefreshThrottle is null)
        {
            RequestUiRefresh();
            return;
        }

        _playbackSelectionRefreshThrottle.Request();
    }

    private void RefreshPlaybackSelection()
    {
        MacroSession? displayedSession = GetSessionForPlayback();
        PlaybackSettings playbackSettings = BuildPlaybackSettings();
        SelectActivePlaybackCursor(displayedSession, playbackSettings);

        if (ShouldFollowActivePlaybackCursor(displayedSession, playbackSettings))
        {
            UpdateActivePreviewMapCursor(_activePlaybackSourceIndex);
        }
    }

    private void CancelPendingPlaybackSelectionRefresh()
    {
        _playbackSelectionRefreshThrottle?.Cancel();
    }

    private void RequestRecordingEventRefresh()
    {
        if (_recordingEventRefreshThrottle is null)
        {
            RequestUiRefresh();
            return;
        }

        _recordingEventRefreshThrottle.Request();
    }

    private void InitializeDynamicControls()
    {
        BuildResponsiveHostLayout();

        _recordingEventRefreshThrottle = new UiRefreshThrottle(
            this,
            intervalMs: 50,
            refreshAction: () => RefreshUiState(),
            canRun: CanRunDeferredUiRefresh);
        _playbackTelemetryRefreshThrottle = new UiRefreshThrottle(
            this,
            PlaybackTelemetryRefreshIntervalMs,
            RefreshPlaybackTelemetry,
            CanRunDeferredUiRefresh);
        _playbackSelectionRefreshThrottle = new UiRefreshThrottle(
            this,
            PlaybackSelectionRefreshIntervalMs,
            RefreshPlaybackSelection,
            CanRunDeferredUiRefresh);

        _toolbarControl.Name = "toolbarControl";
        _toolbarControl.Dock = DockStyle.Fill;
        _toolbarControl.RecordToggleClicked += recordToggleButton_Click;
        _toolbarControl.PlaybackToggleClicked += playbackToggleButton_Click;
        _toolbarControl.StopClicked += stopButton_Click;
        _toolbarControl.SaveLibraryClicked += saveLibraryButton_Click;
        _toolbarControl.SaveJsonClicked += saveJsonButton_Click;
        _toolbarControl.SaveXmlClicked += saveXmlButton_Click;
        _toolbarControl.SaveHtmlReportClicked += saveHtmlReportButton_Click;
        _toolbarControl.SaveTextReportClicked += saveTextReportButton_Click;
        _toolbarControl.LoadJsonClicked += loadJsonButton_Click;
        _toolbarControl.LoadXmlClicked += loadXmlButton_Click;
        _toolbarControl.HotkeysClicked += editHotkeysButton_Click;

        _titleBarControl.Name = "titleBarControl";
        _titleBarControl.Dock = DockStyle.Fill;
        _titleBarControl.SetTitle("Polly Kontrol Merkezi");
        _titleBarControl.DragRequested += titleBarControl_DragRequested;
        _titleBarControl.MinimizeRequested += titleBarControl_MinimizeRequested;
        _titleBarControl.MaximizeRestoreRequested += titleBarControl_MaximizeRestoreRequested;
        _titleBarControl.CloseRequested += titleBarControl_CloseRequested;

        _playbackSettingsControl.Name = "playbackSettingsControl";
        _playbackSettingsControl.Dock = DockStyle.Fill;
        _playbackSettingsControl.SettingsChanged += playbackSettingsControl_SettingsChanged;

        _eventListControl.Name = "eventListControl";
        _eventListControl.Dock = DockStyle.Fill;
        _eventListControl.EventEditRequested += eventListControl_EventEditRequested;

        _macroLibraryControl.Name = "macroLibraryControl";
        _macroLibraryControl.Dock = DockStyle.Fill;
        _macroLibraryControl.AddRequested += importLibraryMacroButton_Click;
        _macroLibraryControl.LoadRequested += macroLibraryControl_LoadRequested;
        _macroLibraryControl.RenameRequested += macroLibraryControl_RenameRequested;
        _macroLibraryControl.DeleteRequested += macroLibraryControl_DeleteRequested;
        _macroLibraryControl.FavoriteToggled += macroLibraryControl_FavoriteToggled;
        _macroLibraryControl.OptimizeRequested += macroLibraryControl_OptimizeRequested;

        _sessionSummaryControl.Name = "sessionSummaryControl";
        _sessionSummaryControl.Dock = DockStyle.Fill;
        _sessionSummaryControl.PreviewMapRequested += sessionSummaryControl_PreviewMapRequested;

        _playbackControl.Name = "playbackControl";
        _playbackControl.Dock = DockStyle.Fill;
        _playbackControl.SkipBackClicked += playbackSkipBackButton_Click;
        _playbackControl.StepBackClicked += playbackStepBackButton_Click;
        _playbackControl.PlaybackClicked += playbackToggleButton_Click;
        _playbackControl.StepForwardClicked += playbackStepForwardButton_Click;
        _playbackControl.StopClicked += stopButton_Click;
    }

    private void sessionSummaryControl_PreviewMapRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_shutdownInProgress)
        {
            return;
        }

        MacroSession? displayedSession = GetSessionForPlayback();
        PlaybackSettings playbackSettings = BuildPlaybackSettings();
        string statusText = ResolveStatusText(_applicationStateService.CurrentState, playbackSettings);
        ShowMacroPreviewMapDialog(
            BuildSessionSummaryState(displayedSession, statusText),
            displayedSession?.Events,
            _activePlaybackSourceIndex,
            _sessionSummaryControl.PreviewMapScreenBounds);
    }

    private void titleBarControl_MinimizeRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        WindowState = FormWindowState.Minimized;
    }

    private void titleBarControl_DragRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Normal;
        }

        WindowChromeNative.BeginWindowDrag(Handle);
    }

    private void titleBarControl_MaximizeRestoreRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
        _titleBarControl.SetMaximized(WindowState == FormWindowState.Maximized);
    }

    private void titleBarControl_CloseRequested(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        Close();
    }

    private static Color ResolveTitleBarStatusColor(AppState appState)
    {
        return appState switch
        {
            AppState.Recording => DesignTokens.AccentRed,
            AppState.Playing => DesignTokens.Accent,
            AppState.Paused => DesignTokens.AccentOrange,
            AppState.Stopping => DesignTokens.TextSecondary,
            _ => DesignTokens.AccentGreen
        };
    }

    private void BuildResponsiveHostLayout()
    {
        SuspendLayout();

        Rectangle startupWorkingArea = ResolveStartupWorkingArea();
        _layoutProfile = AppShellLayoutProfileResolver.Resolve(
            startupWorkingArea,
            DesignTokens.DensityScale);

        BackColor = DesignTokens.Background;
        ForeColor = DesignTokens.TextPrimary;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = _layoutProfile.PreferredClientSize;
        MinimumSize = _layoutProfile.MinimumClientSize;
        Padding = Padding.Empty;
        ApplyWindowChromeConfiguration();
        LogLayoutProfile(startupWorkingArea, _layoutProfile);
        _titleBarControl.ApplyShellLayoutProfile(_layoutProfile);
        _toolbarControl.ApplyShellLayoutProfile(_layoutProfile);
        _macroLibraryControl.ApplyShellLayoutProfile(_layoutProfile);
        _eventListControl.ApplyShellLayoutProfile(_layoutProfile);
        _sessionSummaryControl.ApplyShellLayoutProfile(_layoutProfile);

        var rootLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = DesignTokens.Background,
            Padding = _layoutProfile.RootPadding,
            Margin = Padding.Empty
        };
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, _layoutProfile.Chrome.TitleBarRowHeight));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, _layoutProfile.Chrome.ToolbarRowHeight));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 72f));
        rootLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 28f));

        var toolbarHostPanel = CreateCard();
        toolbarHostPanel.Margin = _layoutProfile.Chrome.ToolbarHostMargin;
        toolbarHostPanel.ContentPadding = _layoutProfile.Chrome.ToolbarContentPadding;
        toolbarHostPanel.Body.Controls.Add(_toolbarControl);

        var mainLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = _layoutProfile.Main.MainMargin,
            Padding = Padding.Empty
        };
        mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, _layoutProfile.Main.LibraryColumnPercent));
        mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, _layoutProfile.Main.PreviewColumnPercent));
        mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, _layoutProfile.Main.SummaryColumnPercent));

        int mainGap = Math.Max(0, _layoutProfile.Main.CardGap);
        int leadingMainGap = mainGap / 2;
        int trailingMainGap = mainGap - leadingMainGap;

        var libraryHostPanel = CreateCard();
        libraryHostPanel.Margin = new Padding(0, 0, leadingMainGap, 0);
        libraryHostPanel.ContentPadding = _layoutProfile.Main.PrimaryCardContentPadding;
        libraryHostPanel.Body.Controls.Add(_macroLibraryControl);

        var previewHostPanel = CreateCard();
        previewHostPanel.Margin = new Padding(trailingMainGap, 0, leadingMainGap, 0);
        previewHostPanel.ContentPadding = _layoutProfile.Main.PrimaryCardContentPadding;
        previewHostPanel.Body.Controls.Add(_eventListControl);

        var sessionHostPanel = CreateSectionCard("Oturum Özeti", _layoutProfile);
        sessionHostPanel.Margin = new Padding(trailingMainGap, 0, 0, 0);
        sessionHostPanel.Body.Controls.Add(_sessionSummaryControl);

        mainLayoutPanel.Controls.Add(libraryHostPanel, 0, 0);
        mainLayoutPanel.Controls.Add(previewHostPanel, 1, 0);
        mainLayoutPanel.Controls.Add(sessionHostPanel, 2, 0);

        var bottomLayoutPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        bottomLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
        bottomLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));

        var playbackControlCard = CreateSectionCard("Oynatma Kontrolü", _layoutProfile);
        playbackControlCard.Margin = new Padding(0, 0, DesignTokens.GapMedium / 2, 0);
        playbackControlCard.Body.Controls.Add(_playbackControl);

        var playbackSettingsHostPanel = CreateSectionCard("Oynatma Ayarları", _layoutProfile);
        playbackSettingsHostPanel.Margin = new Padding(DesignTokens.GapMedium / 2, 0, 0, 0);
        playbackSettingsHostPanel.Body.Controls.Add(_playbackSettingsControl);

        bottomLayoutPanel.Controls.Add(playbackControlCard, 0, 0);
        bottomLayoutPanel.Controls.Add(playbackSettingsHostPanel, 1, 0);

        rootLayoutPanel.Controls.Add(_titleBarControl, 0, 0);
        rootLayoutPanel.Controls.Add(toolbarHostPanel, 0, 1);
        rootLayoutPanel.Controls.Add(mainLayoutPanel, 0, 2);
        rootLayoutPanel.Controls.Add(bottomLayoutPanel, 0, 3);

        Controls.Clear();
        Controls.Add(rootLayoutPanel);

        ResumeLayout(performLayout: true);
    }

    private static Rectangle ResolveStartupWorkingArea()
    {
        try
        {
            Screen cursorScreen = Screen.FromPoint(Cursor.Position);
            if (!cursorScreen.WorkingArea.IsEmpty)
            {
                return cursorScreen.WorkingArea;
            }
        }
        catch
        {
      
        }

        return Screen.PrimaryScreen?.WorkingArea
            ?? new Rectangle(0, 0, DesignTokens.Scale(1280), DesignTokens.Scale(760));
    }

    private void LogLayoutProfile(
        Rectangle workingArea,
        AppShellLayoutProfile profile)
    {
        _logger.Log(
            AppLogLevel.Information,
            nameof(MainForm),
            FormattableString.Invariant(
                $"Shell layout profile resolved. Mode={profile.Mode}; Density={DesignTokens.DensityScale:0.##}; FontScale={DesignTokens.FontScale:0.##}; WorkingArea={workingArea.Width}x{workingArea.Height}; Client={profile.PreferredClientSize.Width}x{profile.PreferredClientSize.Height}; Minimum={profile.MinimumClientSize.Width}x{profile.MinimumClientSize.Height}; Chrome={profile.Chrome.TitleBarRowHeight}/{profile.Chrome.ToolbarRowHeight}; Fit={profile.FitRatio:0.###}."));
    }

    private static DashboardCard CreateCard()
    {
        return new DashboardCard
        {
            Dock = DockStyle.Fill,
            ShowHeader = false,
            Margin = Padding.Empty
        };
    }

    private static DashboardCard CreateSectionCard(
        string title,
        AppShellLayoutProfile profile)
    {
        var card = CreateCard();
        card.ShowHeader = true;
        card.Title = title;
        card.HeaderHeight = profile.Main.SectionHeaderHeight;
        card.ContentPadding = profile.Main.SectionCardContentPadding;
        return card;
    }

    private void playbackSettingsControl_SettingsChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;

        if (_applyingPlaybackSettings)
        {
            return;
        }

        RefreshUiState();
    }

    private async Task EditHotkeysAsync()
    {
        EnsureHotkeyMutationAllowed();

        bool restorePreviousGlobalHotkeys = await SuspendHotkeysForModalDialogAsync();

        try
        {
            using var dialog = new HotkeySettingsDialog(_hotkeyConfiguration.Snapshot());

            if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
            {
                return;
            }

            await ApplyHotkeySettingsAsync(
                dialog.SelectedHotkeySettings,
                restorePreviousGlobalHotkeys);
            restorePreviousGlobalHotkeys = false;
        }
        finally
        {
            if (restorePreviousGlobalHotkeys)
            {
                await RestoreHotkeysAfterModalDialogAsync();
            }
        }
    }

    private async Task ApplyHotkeySettingsAsync(
        HotkeySettings hotkeySettings,
        bool restorePreviousRegistrationOnFailure = false)
    {
        ArgumentNullException.ThrowIfNull(hotkeySettings);

        EnsureHotkeyMutationAllowed();
        HotkeySettingsValidator.Validate(hotkeySettings, "Kısayol ayarı uygulama");

        HotkeySettings previousSettings = _hotkeyConfiguration.Snapshot();
        bool wasRegistered = _hotkeyService.IsRegistered || restorePreviousRegistrationOnFailure;
        bool shouldRefreshGlobalRegistration = OperatingSystem.IsWindows();

        try
        {
            if (shouldRefreshGlobalRegistration && _hotkeyService.IsRegistered)
            {
                await _hotkeyService.UnregisterAsync();
            }

            _hotkeyConfiguration.Apply(hotkeySettings);

            if (shouldRefreshGlobalRegistration)
            {
                await _hotkeyService.RegisterAsync();
            }

            await _hotkeySettingsStore.SaveAsync(_hotkeyConfiguration.Snapshot());
        }
        catch (Exception applyException)
        {
            List<Exception> recoveryErrors = [];

            try
            {
                if (_hotkeyService.IsRegistered)
                {
                    await _hotkeyService.UnregisterAsync();
                }
            }
            catch (Exception unregisterException)
            {
                recoveryErrors.Add(
                    new InvalidOperationException(
                        "Kısmen uygulanan kısayol kaydı temiz bir şekilde geri alınamadı.",
                        unregisterException));
            }

            try
            {
                _hotkeyConfiguration.Apply(previousSettings);
            }
            catch (Exception restoreConfigurationException)
            {
                recoveryErrors.Add(
                    new InvalidOperationException(
                        "Önceki kısayol yapılandırması geri yüklenemedi.",
                        restoreConfigurationException));
            }

            if (shouldRefreshGlobalRegistration && wasRegistered)
            {
                try
                {
                    await _hotkeyService.RegisterAsync();
                }
                catch (Exception restoreRegistrationException)
                {
                    recoveryErrors.Add(
                        new InvalidOperationException(
                            "Önceki kısayol kaydı yeniden yüklenemedi.",
                            restoreRegistrationException));
                }
            }

            UpdateHotkeySummary();
            RefreshUiState();

            if (recoveryErrors.Count > 0)
            {
                List<Exception> aggregateErrors = [applyException];
                aggregateErrors.AddRange(recoveryErrors);
                throw new AggregateException(
                    "Kısayol ayarı güncellemesi başarısız oldu ve geri alma işlemi tam olarak tamamlanamadı.",
                    aggregateErrors);
            }

            throw;
        }

        UpdateHotkeySummary();
        RefreshUiState();
    }

    private async Task<bool> SuspendHotkeysForModalDialogAsync()
    {
        if (!OperatingSystem.IsWindows() || !_hotkeyService.IsRegistered)
        {
            return false;
        }

        await _hotkeyService.UnregisterAsync();
        return true;
    }

    private async Task RestoreHotkeysAfterModalDialogAsync()
    {
        if (!OperatingSystem.IsWindows() || _hotkeyService.IsRegistered)
        {
            return;
        }

        await _hotkeyService.RegisterAsync();
    }

    private void UpdateHotkeySummary()
    {
        string recordHotkey = FormatHotkey(_hotkeyConfiguration.RecordToggleHotkey);
        string playbackHotkey = FormatHotkey(_hotkeyConfiguration.PlaybackToggleHotkey);
        string stopHotkey = FormatHotkey(_hotkeyConfiguration.StopHotkey);
        string hotkeySettingsHotkey = FormatHotkey(_hotkeyConfiguration.HotkeySettingsHotkey);

        _toolbarControl.SetHotkeyHints(
            recordHotkey,
            stopHotkey,
            playbackHotkey,
            hotkeySettingsHotkey);
    }

    private static string FormatHotkey(HotkeyBinding hotkeyBinding)
    {
        List<string> parts = [];

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (hotkeyBinding.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(VirtualKeyDisplayNameFormatter.Format(hotkeyBinding.VirtualKeyCode));
        return string.Join("+", parts);
    }

    private static string FormatAppState(AppState appState)
    {
        return appState switch
        {
            AppState.Idle => "Boş",
            AppState.Recording => "Kayıt",
            AppState.Playing => "Oynatma",
            AppState.Paused => "Duraklatıldı",
            AppState.Stopping => "Durduruluyor",
            AppState.Error => "Hata",
            _ => appState.ToString()
        };
    }

    private static string ResolveStatusText(
        AppState appState,
        PlaybackSettings playbackSettings)
    {
        if (playbackSettings.SimulationMode)
        {
            return appState switch
            {
                AppState.Playing => "Simülasyon",
                AppState.Paused => "Simülasyon duraklatıldı",
                _ => FormatAppState(appState)
            };
        }

        return FormatAppState(appState);
    }

    private static string BuildDefaultFileName(string sessionName, string extension)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string sanitizedName = new string(sessionName
            .Where(character => !invalidCharacters.Contains(character))
            .ToArray());

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "MakroOturumu";
        }

        return sanitizedName + extension;
    }

    private MacroLibraryFileFormat ResolveCurrentLibrarySaveFormat()
    {
        string extension = string.IsNullOrWhiteSpace(_lastSessionPath)
            ? string.Empty
            : Path.GetExtension(_lastSessionPath);

        return extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            ? MacroLibraryFileFormat.Xml
            : MacroLibraryFileFormat.Json;
    }

    private static bool IsSamePath(string? left, string right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ClampDecimal(
        decimal value,
        decimal minimum,
        decimal maximum)
    {
        return Math.Min(Math.Max(value, minimum), maximum);
    }

    private PlaybackSettings BuildPlaybackSettings()
    {
        return _playbackSettingsControl.GetCurrentSettings();
    }

    private PlaybackControlState BuildPlaybackControlState(
        MacroSession? session,
        PlaybackSettings playbackSettings,
        bool canPlayback,
        bool canStop,
        bool canNavigatePlayback)
    {
        PlaybackTelemetrySnapshot telemetry = BuildPlaybackTelemetrySnapshot(session, playbackSettings);

        return new PlaybackControlState(
            telemetry,
            canPlayback,
            canStop,
            canNavigatePlayback,
            _macroPlaybackService.IsPaused
                ? PlaybackButtonState.Resume
                : _macroPlaybackService.IsPlaying
                    ? PlaybackButtonState.Pause
                    : PlaybackButtonState.Play);
    }

    private PlaybackTelemetrySnapshot BuildPlaybackTelemetrySnapshot(
        MacroSession? session,
        PlaybackSettings playbackSettings)
    {
        int repeatCount = Math.Max(playbackSettings.RepeatCount, 1);
        int sessionEventCount = session?.Events.Count ?? 0;
        int sessionDurationMs = Math.Max(0, session?.TotalDurationMs ?? 0);
        int initialDelayMs = Math.Max(0, playbackSettings.InitialDelayMs);
        int totalEventCount = playbackSettings.LoopIndefinitely
            ? Math.Max(0, sessionEventCount)
            : SaturatingMultiply(sessionEventCount, repeatCount);
        int totalDurationMs = playbackSettings.LoopIndefinitely
            ? sessionDurationMs
            : SaturatingAdd(SaturatingMultiply(sessionDurationMs, repeatCount), initialDelayMs);
        int playedEventCount = Math.Clamp(_playedEventCount, 0, Math.Max(0, totalEventCount));
        int playedDurationMs = Math.Clamp(_playedDurationMs, 0, Math.Max(0, totalDurationMs));
        double speedMultiplier = playbackSettings.PreserveOriginalTiming || playbackSettings.SpeedMultiplier <= 0
            ? 1.0
            : playbackSettings.SpeedMultiplier;

        return new PlaybackTelemetrySnapshot(
            ResolveStatusText(_applicationStateService.CurrentState, playbackSettings),
            playedEventCount,
            totalEventCount,
            playedDurationMs,
            totalDurationMs,
            speedMultiplier,
            repeatCount,
            playbackSettings.LoopIndefinitely,
            initialDelayMs);
    }

    private sealed class UiRefreshThrottle : IDisposable
    {
        private readonly Control _owner;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly Action _refreshAction;
        private readonly Func<bool> _canRun;
        private volatile bool _pending;
        private bool _disposed;

        public UiRefreshThrottle(
            Control owner,
            int intervalMs,
            Action refreshAction,
            Func<bool> canRun)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(refreshAction);
            ArgumentNullException.ThrowIfNull(canRun);

            if (intervalMs <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(intervalMs),
                    intervalMs,
                    "Refresh throttle interval must be greater than zero.");
            }

            _owner = owner;
            _refreshAction = refreshAction;
            _canRun = canRun;
            _timer = new System.Windows.Forms.Timer
            {
                Interval = intervalMs
            };
            _timer.Tick += OnTick;
        }

        public void Request()
        {
            if (_disposed)
            {
                return;
            }

            if (!CanAccessOwner())
            {
                return;
            }

            if (_owner.InvokeRequired)
            {
                TryBeginInvoke(RequestCore);
                return;
            }

            RequestCore();
        }

        public void Cancel()
        {
            _pending = false;

            if (_disposed)
            {
                return;
            }

            if (!_owner.IsHandleCreated || _owner.IsDisposed)
            {
                return;
            }

            if (_owner.InvokeRequired)
            {
                TryBeginInvoke(CancelCore);
                return;
            }

            CancelCore();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending = false;
            _timer.Tick -= OnTick;
            _timer.Dispose();
        }

        private void RequestCore()
        {
            if (_disposed)
            {
                return;
            }

            if (!CanAccessOwner())
            {
                CancelCore();
                return;
            }

            _pending = true;

            if (!_timer.Enabled)
            {
                _timer.Start();
            }
        }

        private void CancelCore()
        {
            if (_disposed)
            {
                return;
            }

            _pending = false;
            _timer.Stop();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            _ = sender;
            _ = e;

            if (_disposed)
            {
                return;
            }

            _timer.Stop();

            if (!_pending || !CanAccessOwner())
            {
                _pending = false;
                return;
            }

            _pending = false;
            _refreshAction();
        }

        private bool CanAccessOwner()
        {
            return !_disposed && _owner.IsHandleCreated && !_owner.IsDisposed && _canRun();
        }

        private void TryBeginInvoke(Action action)
        {
            try
            {
                _owner.BeginInvoke(action);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private bool CanNavigatePlayback(
        MacroSession? session,
        PlaybackSettings playbackSettings)
    {
        return !_shutdownInProgress
            && session is { Events.Count: > 0 }
            && !_macroRecorderService.IsRecording
            && !_macroPlaybackService.IsPlaying
            && _applicationStateService.IsAny(AppState.Idle, AppState.Paused)
            && GetTotalPlaybackEventCount(session, playbackSettings) > 0;
    }

    private static int GetTotalPlaybackEventCount(
        MacroSession session,
        PlaybackSettings playbackSettings)
    {
        if (session.Events.Count == 0)
        {
            return 0;
        }

        int repeatCount = playbackSettings.LoopIndefinitely
            ? 1
            : Math.Max(playbackSettings.RepeatCount, 1);

        return SaturatingMultiply(session.Events.Count, repeatCount);
    }

    private static int SaturatingMultiply(int value, int multiplier)
    {
        long result = (long)Math.Max(0, value) * Math.Max(0, multiplier);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }

    private static int SaturatingAdd(int left, int right)
    {
        long result = (long)Math.Max(0, left) + Math.Max(0, right);
        return result > int.MaxValue ? int.MaxValue : (int)result;
    }

    private static int CalculatePlayedDurationMs(
        MacroSession session,
        int inclusiveLogicalIndex)
    {
        if (session.Events.Count == 0 || inclusiveLogicalIndex < 0)
        {
            return 0;
        }

        int completedCycles = inclusiveLogicalIndex / session.Events.Count;
        int partialEventIndex = inclusiveLogicalIndex % session.Events.Count;
        long playedDurationMs = (long)completedCycles * Math.Max(0, session.TotalDurationMs);

        for (int eventIndex = 0; eventIndex <= partialEventIndex; eventIndex++)
        {
            playedDurationMs += Math.Max(0, session.Events[eventIndex].DelayMs);
        }

        return playedDurationMs > int.MaxValue
            ? int.MaxValue
            : (int)playedDurationMs;
    }
}
