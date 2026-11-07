using System.Globalization;
using System.Text;
using Makredo.Application.Abstractions;
using Makredo.Application.Filters;
using Makredo.Application.Reporting;
using Makredo.Application.Services;
using Makredo.Domain;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Platform.Windows.Hotkeys;
using Makredo.WinForms.Controls;
using Makredo.WinForms.Platform;
using Makredo.WinForms.Theme;

namespace Makredo.WinForms.Forms;

public partial class MainForm : Form
{
    private const int PlaybackTelemetryRefreshIntervalMs = 33;
    private const int PlaybackSelectionRefreshIntervalMs = 80;

    private readonly IApplicationStateService _applicationStateService;
    private readonly IMacroRecorderService _macroRecorderService;
    private readonly IMacroPlaybackService _macroPlaybackService;
    private readonly IMacroOptimizationService _macroOptimizationService;
    private readonly IMacroStorageService _macroStorageService;
    private readonly SessionDocument _document;
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

    private Task _recordingRecovery = Task.CompletedTask;
    private TaskCompletionSource? _sessionAction;
    private MacroLibraryUserState _macroLibraryUserState = new();
    private int _playedEventCount;
    private int _playedDurationMs;
    private int? _activePlaybackSourceIndex;
    private int _playbackNavigationInProgress;
    private bool _applyingPlaybackSettings;
    private readonly HotkeyWarningBand _hotkeyWarningBand = new();
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
        SessionDocument sessionDocument,
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
        _document = sessionDocument;
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
        Controls.Add(_hotkeyWarningBand);

        Load += MainForm_Load;
        FormClosing += MainForm_FormClosing;
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        SubscribeToServiceEvents();
        _ = ExecuteSessionActionAsync(InitializeUiAsync, "Uygulama başlatma");
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

        try
        {
            await _document.OfferRecoveryAsync(ConfirmRecovery);
        }
        catch (Exception ex)
        {
            _logger.Log(AppLogLevel.Warning, nameof(MainForm), "Kurtarma dosyası işlenemedi.", ex);
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

        if (e.CloseReason == CloseReason.WindowsShutDown)
        {
            // Windows reads the query result before any asynchronous continuation.
            try
            {
                _document.SaveForShutdown(_macroRecorderService.CaptureRecordingSnapshot());
            }
            catch (Exception ex)
            {
                _logger.Log(AppLogLevel.Error, nameof(MainForm), "Windows kapanırken kurtarma dosyası yazılamadı.", ex);
            }

            e.Cancel = false;
            return;
        }

        e.Cancel = true;
        if (_shutdownInProgress)
        {
            return;
        }

        _shutdownInProgress = true;
        try
        {
            if (_sessionAction is { } action)
            {
                await action.Task;
            }

            await _macroPlaybackService.StopAndWaitAsync();
            await _macroRecorderService.StopAsync();
            await _recordingRecovery;
            if (!await CanLeaveSessionAsync(SessionTransition.Close))
            {
                return;
            }

            await ShutdownAsync();
            _shutdownCompleted = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError("Uygulama kapatılırken bir hata oluştu.", ex);
        }
        finally
        {
            if (!_shutdownCompleted)
            {
                _shutdownInProgress = false;
                RefreshUiState();
            }
        }
    }

    private async Task InitializeHotkeysAsync()
    {
        _hotkeyService.RecordToggleRequested += OnRecordToggleRequested;
        _hotkeyService.PlaybackToggleRequested += OnPlaybackToggleRequested;
        _hotkeyService.StopRequested += OnStopRequested;

        try
        {
            await _hotkeyService.RegisterAsync();
            _hotkeyWarningBand.ShowFailures(_hotkeyService.RegistrationFailures);
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
        _ = ExecuteSessionActionAsync(() => HandleRecordToggleCoreAsync(true), "Kaydı başlat/durdur");
    }

    private Task HandleRecordToggleAsync() => HandleRecordToggleCoreAsync(false);

    private async Task HandleRecordToggleCoreAsync(bool fromHotkey)
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

        if (!await CanLeaveSessionAsync(SessionTransition.NewRecording))
        {
            return;
        }

        await _macroRecorderService.StartAsync(null, fromHotkey ? WindowsHotkeyService.CapturePressedKeys(_hotkeyConfiguration.RecordToggleHotkey) : null);
        RefreshUiState();
    }

    private void OnPlaybackToggleRequested()
    {
        _ = ExecuteUiActionAsync(async () =>
        {
            if (_macroRecorderService.IsRecording || _macroPlaybackService.IsPlaying || _macroPlaybackService.IsPaused) { await HandleStopAsync(); }
            else { await HandlePlaybackToggleAsync(); }
        }, "Oynat/durdur");
    }

    private async Task HandlePlaybackToggleAsync()
    {
        if (_shutdownInProgress || _sessionAction is not null || _macroRecorderService.IsRecording)
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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (TryHandleLocalHotkeyFallback(keyData, (msg.LParam.ToInt64() & (1L << 30)) != 0))
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

    private bool TryHandleLocalHotkeyFallback(Keys keyData, bool repeated)
    {
        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.RecordToggleHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.RecordToggleHotkey))
        {
            if (!repeated) { OnRecordToggleRequested(); }
            return true;
        }

        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.PlaybackToggleHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.PlaybackToggleHotkey))
        {
            if (!repeated) { OnPlaybackToggleRequested(); }
            return true;
        }

        if (ShouldUseLocalHotkeyFallback(_hotkeyConfiguration.StopHotkey)
            && MatchesHotkey(keyData, _hotkeyConfiguration.StopHotkey))
        {
            if (!repeated) { OnStopRequested(); }
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
        if (_sessionAction is not null || _shutdownInProgress)
        {
            return;
        }

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
        if (_sessionAction is not null || _shutdownInProgress)
        {
            return;
        }

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
        await SavePlaybackSettingsAsync();
        await SaveHotkeySettingsAsync();

        if (_hotkeyService.IsRegistered)
        {
            await _hotkeyService.UnregisterAsync();
        }

        await _macroPlaybackService.DisposeAsync();
        _recordingEventRefreshThrottle?.Cancel();
        _playbackTelemetryRefreshThrottle?.Cancel();
        _playbackSelectionRefreshThrottle?.Cancel();
        _hotkeyService.RecordToggleRequested -= OnRecordToggleRequested;
        _hotkeyService.PlaybackToggleRequested -= OnPlaybackToggleRequested;
        _hotkeyService.StopRequested -= OnStopRequested;
        UnsubscribeFromServiceEvents();
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

    private async Task ExecuteSessionActionAsync(Func<Task> action, string operationName)
    {
        if (_sessionAction is not null || _shutdownInProgress || _playbackNavigationInProgress != 0)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sessionAction = completion;
        try
        {
            await ExecuteUiActionAsync(async () =>
            {
                await _recordingRecovery;
                if (!_shutdownInProgress)
                {
                    await action();
                }
            }, operationName);
        }
        finally
        {
            _sessionAction = null;
            completion.TrySetResult();
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
            ProductInfo.Name,
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
        if (InvokeRequired)
        {
            TryBeginInvokeOnUiThread(OnRecordingStarted);
            return;
        }

        if (_macroRecorderService.CurrentSession is { } session)
        {
            _document.StartRecording(session);
        }
        ResetSessionPlaybackPosition();
        RequestUiRefresh();
    }

    private void OnEventRecorded(MacroEvent macroEvent)
    {
        _ = macroEvent;
        RequestRecordingEventRefresh();
    }

    private void OnRecordingStopped(MacroSession session)
    {
        if (InvokeRequired)
        {
            TryBeginInvokeOnUiThread(() => OnRecordingStopped(session));
            return;
        }

        _recordingRecovery = TryWriteRecoveryAsync(_document.CompleteRecordingAsync(session));
        _recordingEventRefreshThrottle?.Cancel();
        RequestUiRefresh();
    }

    private async Task TryWriteRecoveryAsync(Task checkpoint)
    {
        try
        {
            await checkpoint;
        }
        catch (Exception ex)
        {
            _logger.Log(AppLogLevel.Error, nameof(MainForm), "Oturumun kurtarma dosyası yazılamadı.", ex);
        }
    }

    private void OnPlaybackStarted()
    {
        if (DeferPlaybackNotification(OnPlaybackStarted))
        {
            return;
        }

        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
        CancelPendingPlaybackTelemetryRefresh();
        CancelPendingPlaybackSelectionRefresh();
        RequestUiRefresh();
    }

    private void OnPlaybackStateChanged()
    {
        if (DeferPlaybackNotification(OnPlaybackStateChanged))
        {
            return;
        }

        CancelPendingPlaybackTelemetryRefresh();
        CancelPendingPlaybackSelectionRefresh();
        RequestUiRefresh();
    }

    private void OnPlaybackEventPlayed(MacroEvent macroEvent)
    {
        if (DeferPlaybackNotification(() => OnPlaybackEventPlayed(macroEvent)))
        {
            return;
        }

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
        if (DeferPlaybackNotification(OnPlaybackStopped))
        {
            return;
        }

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
        _ = ExecuteSessionActionAsync(HandleRecordToggleAsync, "Kaydı başlat/durdur");
    }

    private void playbackToggleButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteUiActionAsync(HandlePlaybackToggleAsync, "Oynat/duraklat");
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
        _ = ExecuteSessionActionAsync(SaveJsonAsync, "JSON kaydetme");
    }

    private void saveLibraryButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteSessionActionAsync(SaveToLibraryAsync, "Makro kütüphanesine kaydetme");
    }

    private void loadJsonButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteSessionActionAsync(LoadJsonAsync, "JSON yükleme");
    }

    private void importLibraryMacroButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteSessionActionAsync(ImportLibraryMacroAsync, "Makro kütüphanesine içe aktarma");
    }

    private void saveXmlButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteSessionActionAsync(SaveXmlAsync, "XML kaydetme");
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
        _ = ExecuteSessionActionAsync(LoadXmlAsync, "XML yükleme");
    }

    private void clearSessionButton_Click(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _ = ExecuteSessionActionAsync(ClearSessionAsync, "Oturum temizleme");
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
        _ = ExecuteSessionActionAsync(() => LoadLibraryMacroAsync(e.Item), "Makro kütüphanesi yükleme");
    }

    private void macroLibraryControl_RenameRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteSessionActionAsync(() => RenameLibraryMacroAsync(e.Item), "Makro kütüphanesi isim düzenleme");
    }

    private void macroLibraryControl_DeleteRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteSessionActionAsync(() => DeleteLibraryMacroAsync(e.Item), "Makro kütüphanesi silme");
    }

    private void macroLibraryControl_FavoriteToggled(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteUiActionAsync(() => ToggleLibraryFavoriteAsync(e.Item), "Makro favori değiştirme");
    }

    private void macroLibraryControl_OptimizeRequested(object? sender, MacroLibraryItemEventArgs e)
    {
        _ = sender;
        _ = ExecuteSessionActionAsync(() => OptimizeLibraryMacroAsync(e.Item), "Makro kütüphanesi optimizasyonu");
    }

    private void eventListControl_EventEditRequested(object? sender, EventEditRequestedEventArgs e)
    {
        _ = sender;
        _ = ExecuteSessionActionAsync(() => EditEventAsync(e), "Olay düzenleme");
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
        await _document.MarkSavedAsync(dialog.FileName);
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

        await _document.MarkSavedAsync(filePath);
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
                ProductInfo.Name,
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
            && !IsSamePath(_document.FilePath, entry.FilePath));
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

        if (!await CanLeaveSessionAsync(SessionTransition.Load))
        {
            return;
        }

        MacroSession session = await _macroStorageService.LoadFromJsonAsync(dialog.FileName);
        await AdoptLoadedSessionAsync(session, dialog.FileName);
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

        if (!await CanLeaveSessionAsync(SessionTransition.Load))
        {
            return;
        }

        string importedFilePath = await _macroLibraryService.ImportAsync(dialog.FileName);
        MacroSession session = await _macroLibraryService.LoadAsync(importedFilePath);

        await AdoptLoadedSessionAsync(session, importedFilePath);
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
        await _document.MarkSavedAsync(dialog.FileName);
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

        string reportContent = buildReport(session, _document.FilePath);
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

        if (!await CanLeaveSessionAsync(SessionTransition.Load))
        {
            return;
        }

        MacroSession session = await _macroStorageService.LoadFromXmlAsync(dialog.FileName);
        await AdoptLoadedSessionAsync(session, dialog.FileName);
        await RefreshMacroLibraryAsync();
    }

    private async Task ClearSessionAsync()
    {
        EnsureSessionMutationAllowed();

        if (!await CanLeaveSessionAsync(SessionTransition.Clear))
        {
            return;
        }

        _macroRecorderService.Clear();
        _document.Clear();
        ResetSessionPlaybackPosition();
        await RefreshMacroLibraryAsync();
        RefreshUiState();
    }

    private async Task LoadLibraryMacroAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureSessionMutationAllowed();

        if (!await CanLeaveSessionAsync(SessionTransition.Load))
        {
            return;
        }

        MacroSession session = await _macroLibraryService.LoadAsync(item.FilePath);
        await AdoptLoadedSessionAsync(session, item.FilePath);
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

        if (IsSamePath(_document.FilePath, previousFilePath))
        {
            await TryWriteRecoveryAsync(_document.RenameAsync(dialog.MacroName, renamedFilePath));
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

        if (IsSamePath(_document.FilePath, item.FilePath))
        {
            _document.DetachFile();
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

    private async Task EditEventAsync(EventEditRequestedEventArgs editRequest)
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
            return;
        }

        await TryWriteRecoveryAsync(_document.EditEventAsync(
            editRequest.EventIndex, dialog.EditResult.DelayMs, dialog.EditResult.X, dialog.EditResult.Y));
        RefreshUiState(forceEventListReload: true);
    }

    private async Task OptimizeLibraryMacroAsync(MacroLibraryEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureSessionMutationAllowed();

        bool optimizedActiveSession = IsSamePath(_document.FilePath, item.FilePath) && _document.Session is not null;
        MacroSession session = optimizedActiveSession
            ? _document.Session!
            : await _macroLibraryService.LoadAsync(item.FilePath);
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

        if (optimizedActiveSession)
        {
            await TryWriteRecoveryAsync(_document.OptimizeAsync(preview.OptimizedEvents));
            ResetSessionPlaybackPosition();
        }
        else
        {
            session.ReplaceEvents(preview.OptimizedEvents);
            await SaveOptimizedLibraryMacroAsync(session, item);
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

        _macroLibraryControl.SetItems(BuildMacroLibraryViewItems(entries), _document.FilePath);
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

    private async Task AdoptLoadedSessionAsync(MacroSession session, string filePath)
    {
        await _document.LoadAsync(session, filePath);
        ResetSessionPlaybackPosition();
        RefreshUiState();
    }

    private void ResetSessionPlaybackPosition()
    {
        _playedEventCount = 0;
        _playedDurationMs = 0;
        _activePlaybackSourceIndex = null;
    }

    private Task<bool> CanLeaveSessionAsync(SessionTransition transition) =>
        _document.CanLeaveAsync(transition, AskAboutUnsavedSession, SaveBeforeLeavingAsync);

    private UnsavedSessionChoice AskAboutUnsavedSession(SessionTransition transition)
    {
        string message = transition switch
        {
            SessionTransition.Close => "Kapatmadan önce makro oturumunu kaydetmek ister misiniz?",
            SessionTransition.NewRecording => "Yeni kayıt başlatmadan önce açık makro oturumunu kaydetmek ister misiniz?",
            SessionTransition.Load => "Başka bir makro yüklemeden önce açık oturumu kaydetmek ister misiniz?",
            _ => "Oturumu temizlemeden önce makroyu kaydetmek ister misiniz?"
        };
        const string caption = "Kaydedilmemiş değişiklikler";
        using var dialog = ThemedMessageDialog.CreateSaveChanges($"{caption}\n\n{message}", caption);
        return ModalDialogOverlay.ShowDialog(this, dialog) switch
        {
            DialogResult.Yes => UnsavedSessionChoice.Save,
            DialogResult.No => UnsavedSessionChoice.Discard,
            _ => UnsavedSessionChoice.Cancel
        };
    }

    private async Task<string?> SaveBeforeLeavingAsync(MacroSession session, CancellationToken cancellationToken)
    {
        string? filePath = _document.FilePath;
        if (filePath is null)
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "JSON makro (*.json)|*.json|XML makro (*.xml)|*.xml",
                FileName = BuildDefaultFileName(session.Name, ".json"),
                AddExtension = true,
                DefaultExt = "json",
                RestoreDirectory = true
            };
            if (ModalDialogOverlay.ShowDialog(this, dialog) != DialogResult.OK)
            {
                return null;
            }

            filePath = dialog.FileName;
        }

        if (string.Equals(Path.GetExtension(filePath), ".xml", StringComparison.OrdinalIgnoreCase))
        {
            await _macroStorageService.SaveAsXmlAsync(session, filePath, cancellationToken);
        }
        else
        {
            await _macroStorageService.SaveAsJsonAsync(session, filePath, cancellationToken);
        }

        return filePath;
    }

    private bool ConfirmRecovery(MacroSession session) =>
        ModalDialogOverlay.ShowMessage(
            this,
            $"Makro oturumu kurtarıldı\n\n“{session.Name}” adlı kaydedilmemiş oturumu geri yüklemek ister misiniz?",
            "Oturum kurtarma",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;

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
            _macroRecorderService.RecordInjectedInput = playbackSettings.RecordInjectedInput;
        }
        finally
        {
            _applyingPlaybackSettings = false;
        }
    }

    private MacroSession? GetSessionForPlayback() => _document.Session;

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
            string.IsNullOrWhiteSpace(_document.FilePath)
                ? "Kaydedilmedi"
                : Path.GetFileName(_document.FilePath));
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

    private bool DeferPlaybackNotification(Action notification)
    {
        if (!CanRunDeferredUiRefresh())
        {
            return true;
        }

        if (!InvokeRequired)
        {
            return false;
        }

        TryBeginInvokeOnUiThread(notification);
        return true;
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
        _titleBarControl.SetTitle($"{ProductInfo.Name} Kontrol Merkezi");
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
        _macroRecorderService.RecordInjectedInput = _playbackSettingsControl.GetCurrentSettings().RecordInjectedInput;
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
                _hotkeyWarningBand.ShowFailures(_hotkeyService.RegistrationFailures);
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
                    _hotkeyWarningBand.ShowFailures(_hotkeyService.RegistrationFailures);
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
        _hotkeyWarningBand.ShowFailures(_hotkeyService.RegistrationFailures);
    }

    private void UpdateHotkeySummary()
    {
        string recordHotkey = FormatHotkey(_hotkeyConfiguration.RecordToggleHotkey);
        string playbackHotkey = FormatHotkey(_hotkeyConfiguration.PlaybackToggleHotkey);
        string stopHotkey = FormatHotkey(_hotkeyConfiguration.StopHotkey);
        string? hotkeySettingsHotkey = null;

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
        string extension = string.IsNullOrWhiteSpace(_document.FilePath)
            ? string.Empty
            : Path.GetExtension(_document.FilePath);

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
