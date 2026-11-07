using System.Runtime.InteropServices;
using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Makredo.Application.Services;
using Makredo.Domain;

namespace Makredo.Platform.Windows.Hotkeys;

public sealed class WindowsHotkeyService : IHotkeyService, IDisposable
{
    private const int RecordToggleHotkeyId = 1001;
    private const int PlaybackToggleHotkeyId = 1002;
    private const int StopHotkeyId = 1003;

    private const uint ModNoRepeat = 0x4000;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly HotkeyMessageWindow _messageWindow;
    private readonly IHotkeyConfiguration _hotkeyConfiguration;
    private readonly IAppLogger _logger;
    private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys = [];
    private bool _disposed;

    public WindowsHotkeyService(
        IHotkeyConfiguration hotkeyConfiguration,
        IAppLogger? logger = null)
    {
        _hotkeyConfiguration = hotkeyConfiguration;
        _logger = logger ?? NullAppLogger.Instance;
        _messageWindow = new HotkeyMessageWindow(this);
    }

    public IReadOnlyList<HotkeyRegistrationFailure> RegistrationFailures { get; private set; } = [];

    public bool IsRegistered => _registeredHotkeys.Count > 0;

    public event Action? RecordToggleRequested;
    public event Action? PlaybackToggleRequested;
    public event Action? StopRequested;

    public bool IsHotkeyRegistered(HotkeyBinding hotkeyBinding)
    {
        ArgumentNullException.ThrowIfNull(hotkeyBinding);
        return _registeredHotkeys.ContainsValue(hotkeyBinding);
    }

    public Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();

        if (!OperatingSystem.IsWindows())
        {
            PlatformNotSupportedException exception = new(
                "WindowsHotkeyService yalnızca Windows ortamında çalısır.");
            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsHotkeyService),
                "Global kısayol servisi Windows dışındaki bir ortamda başlatılamadı.",
                exception);
            throw exception;
        }

        if (IsRegistered)
        {
            return Task.CompletedTask;
        }

        _messageWindow.EnsureHandleCreated();

        List<HotkeyRegistrationRequest> requests =
        [
            new(RecordToggleHotkeyId, _hotkeyConfiguration.RecordToggleHotkey),
            new(PlaybackToggleHotkeyId, _hotkeyConfiguration.PlaybackToggleHotkey)
        ];
        if (_hotkeyConfiguration.StopHotkey != _hotkeyConfiguration.PlaybackToggleHotkey)
        {
            requests.Add(new(StopHotkeyId, _hotkeyConfiguration.StopHotkey));
        }
        RegistrationFailures = HotkeyRegistrationBatch.Register(requests, request => RegisterRequiredHotkey(request.Id, request.Binding));
        foreach (HotkeyRegistrationFailure failure in RegistrationFailures)
        {
            _logger.Log(AppLogLevel.Warning, nameof(WindowsHotkeyService), failure.Reason);
        }
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UnregisterCore();
        return Task.CompletedTask;
    }

    private void UnregisterCore()
    {
        if (_registeredHotkeys.Count == 0)
        {
            return;
        }

        foreach (int hotkeyId in _registeredHotkeys.Keys.ToArray())
        {
            UnregisterSingleHotkey(hotkeyId);
        }

        _registeredHotkeys.Clear();
        _logger.Log(
            AppLogLevel.Information,
            nameof(WindowsHotkeyService),
            "Global kısayollar kaldırıldı.");
    }

    internal void HandleHotkeyPressed(int hotkeyId)
    {
        if (!_registeredHotkeys.ContainsKey(hotkeyId)) { return; }
        switch (hotkeyId)
        {
            case RecordToggleHotkeyId:
                RecordToggleRequested?.Invoke();
                break;

            case PlaybackToggleHotkeyId:
                PlaybackToggleRequested?.Invoke();
                break;

            case StopHotkeyId:
                StopRequested?.Invoke();
                break;

        }
    }

    private void RegisterRequiredHotkey(int id, HotkeyBinding hotkeyBinding)
    {
        if (!TryRegisterHotkey(id, hotkeyBinding, out InvalidOperationException? exception))
        {
            InvalidOperationException registrationException = exception
                ?? new InvalidOperationException($"Kısayol kaydedilemedi. Kimlik: {id}.");

            _logger.Log(
                AppLogLevel.Error,
                nameof(WindowsHotkeyService),
                registrationException.Message,
                registrationException);
            throw registrationException;
        }

        _registeredHotkeys[id] = hotkeyBinding;
    }

    private bool TryRegisterHotkey(
        int id,
        HotkeyBinding hotkeyBinding,
        out InvalidOperationException? exception)
    {
        bool result = HotkeyNativeMethods.RegisterHotKey(
            _messageWindow.Handle,
            id,
            MapModifiers(hotkeyBinding.Modifiers),
            unchecked((uint)hotkeyBinding.VirtualKeyCode));

        if (result)
        {
            exception = null;
            return true;
        }

        int errorCode = Marshal.GetLastWin32Error();
        exception = new InvalidOperationException(
            FormattableString.Invariant(
                $"{FormatHotkey(hotkeyBinding)} kaydedilemedi: {(errorCode == 1409 ? "başka bir uygulama tarafından kullanılıyor" : new System.ComponentModel.Win32Exception(errorCode).Message)} (Win32 {errorCode})."));
        return false;
    }

    public static IReadOnlyCollection<int> CapturePressedKeys(HotkeyBinding binding)
    {
        List<int> keys = [binding.VirtualKeyCode];
        foreach (int key in new[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
        {
            if ((HotkeyRecordingFilter.ModifierForKey(key) & binding.Modifiers) != 0) { keys.Add(key); }
        }
        return keys.Where(key => (HotkeyNativeMethods.GetAsyncKeyState(key) & 0x8000) != 0).ToArray();
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

    private void UnregisterSingleHotkey(int id)
    {
        _ = HotkeyNativeMethods.UnregisterHotKey(_messageWindow.Handle, id);
    }

    private static uint MapModifiers(HotkeyModifiers modifiers)
    {
        uint nativeModifiers = ModNoRepeat;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            nativeModifiers |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            nativeModifiers |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            nativeModifiers |= ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            nativeModifiers |= ModWin;
        }

        return nativeModifiers;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            UnregisterCore();
        }
        catch
        {
            // no-op
        }

        _messageWindow.Release();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private sealed class HotkeyMessageWindow : NativeWindow
    {
        private const int WmHotkey = 0x0312;

        private readonly WindowsHotkeyService _owner;

        public HotkeyMessageWindow(WindowsHotkeyService owner)
        {
            _owner = owner;
        }

        public void EnsureHandleCreated()
        {
            if (Handle != IntPtr.Zero)
            {
                return;
            }

            CreateHandle(new CreateParams
            {
                Caption = $"{ProductInfo.Name}HotkeyMessageWindow"
            });
        }

        public void Release()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                _owner.HandleHotkeyPressed(m.WParam.ToInt32());
            }

            base.WndProc(ref m);
        }
    }

    private static class HotkeyNativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int key);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(
            IntPtr hWnd,
            int id,
            uint fsModifiers,
            uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(
            IntPtr hWnd,
            int id);
    }
}
