using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Makredo.Platform.Windows.Startup;

public sealed class WindowsInstanceActivation : IDisposable
{
    private const string WindowName = "Makredo.SingleInstance.ActivationWindow";
    private const string MessageName = "Makredo.SingleInstance.Activate";
    private const string ReadyEventName = @"Local\Makredo.SingleInstance.ActivationReady";
    private const int SwRestore = 9;
    private static readonly nint MessageOnlyParent = new(-3);
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(3);

    private readonly ActivationWindow _window;
    private readonly EventWaitHandle _ready;
    private Form? _form;
    private bool _pendingActivation;
    private bool _disposed;

    public WindowsInstanceActivation()
    {
        uint message = RegisterWindowMessageW(MessageName);
        if (message == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _window = new ActivationWindow(message, ActivateForm);
        _ready = OpenReadyEvent();
        try
        {
            _ready.Reset();
            _window.CreateHandle(new CreateParams { Parent = MessageOnlyParent, Caption = WindowName });
            _ready.Set();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public static void SignalExistingInstance()
    {
        using EventWaitHandle ready = OpenReadyEvent();
        if (!ready.WaitOne(StartupTimeout))
        {
            return;
        }

        nint window = FindWindowExW(MessageOnlyParent, 0, null, WindowName);
        if (window == 0 || GetWindowThreadProcessId(window, out uint processId) == 0)
        {
            return;
        }

        uint message = RegisterWindowMessageW(MessageName);
        if (message != 0)
        {
            // A denied foreground grant still allows the owner to restore its window.
            _ = AllowSetForegroundWindow(processId);
            _ = PostMessageW(window, message, 0, 0);
        }
    }

    // Attach from the primary UI thread after the form has been shown.
    public void Attach(Form form)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(form);
        _form = form;
        if (_pendingActivation)
        {
            ActivateForm();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _form = null;
        try
        {
            if (_window.Handle != 0)
            {
                _window.DestroyHandle();
            }
        }
        finally
        {
            _ready.Dispose();
        }
    }

    private static EventWaitHandle OpenReadyEvent() =>
        new(initialState: false, EventResetMode.ManualReset, ReadyEventName);

    private void ActivateForm()
    {
        if (_form is not { } form)
        {
            _pendingActivation = true;
            return;
        }

        _pendingActivation = false;
        if (form.IsDisposed || form.Disposing)
        {
            return;
        }

        if (!form.Visible)
        {
            form.ShowInTaskbar = true;
            form.Show();
        }

        if (form.WindowState == FormWindowState.Minimized)
        {
            _ = ShowWindow(form.Handle, SwRestore);
        }

        form.Activate();
    }

    private sealed class ActivationWindow(uint activationMessage, Action activate) : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            if ((uint)m.Msg == activationMessage)
            {
                activate();
                return;
            }

            base.WndProc(ref m);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint RegisterWindowMessageW(string name);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint FindWindowExW(nint parent, nint childAfter, string? className, string windowName);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
}
