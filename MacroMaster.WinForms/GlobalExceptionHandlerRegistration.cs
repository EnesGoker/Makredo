using System.Threading;
using MacroMaster.Application.Abstractions;
using MacroMaster.WinForms.Forms;

namespace MacroMaster.WinForms;

internal sealed class GlobalExceptionHandlerRegistration : IDisposable
{
    private readonly IAppLogger _logger;
    private readonly Action<string, string, MessageBoxIcon> _showErrorMessage;
    private readonly bool _isRegistered;
    private bool _disposed;

    internal GlobalExceptionHandlerRegistration(
        IAppLogger logger,
        Action<string, string, MessageBoxIcon>? showErrorMessage = null,
        bool registerHandlers = true)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _showErrorMessage = showErrorMessage ?? ShowErrorMessage;

        if (!registerHandlers)
        {
            return;
        }

        global::System.Windows.Forms.Application.SetUnhandledExceptionMode(
            global::System.Windows.Forms.UnhandledExceptionMode.CatchException);
        global::System.Windows.Forms.Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _isRegistered = true;
    }

    internal void HandleThreadException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _logger.Log(
            AppLogLevel.Error,
            nameof(GlobalExceptionHandlerRegistration),
            "UI is parcaciginda yakalanmamis bir hata olustu.",
            exception);

        SafeShowErrorMessage(
            "Beklenmeyen bir uygulama hatasi olustu. Ayrintilar gunluk dosyasina yazildi.",
            "MacroMaster",
            MessageBoxIcon.Error);
    }

    internal void HandleUnhandledException(Exception exception, bool isTerminating)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _logger.Log(
            AppLogLevel.Error,
            nameof(GlobalExceptionHandlerRegistration),
            isTerminating
                ? "Uygulama sonlandirici bir yakalanmamis hatayla karsilasti."
                : "Arka plan is parcaciginda yakalanmamis bir hata olustu.",
            exception);
    }

    internal void HandleUnobservedTaskException(AggregateException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        _logger.Log(
            AppLogLevel.Error,
            nameof(GlobalExceptionHandlerRegistration),
            "Gozlemlenmemis gorev hatasi yakalandi.",
            exception);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isRegistered)
        {
            global::System.Windows.Forms.Application.ThreadException -= OnThreadException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void OnThreadException(object? sender, ThreadExceptionEventArgs eventArgs)
    {
        _ = sender;
        HandleThreadException(eventArgs.Exception);
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs eventArgs)
    {
        _ = sender;

        Exception exception = eventArgs.ExceptionObject as Exception
            ?? new InvalidOperationException(
                $"Yakalanmamis hata nesnesi Exception turunde degil: {eventArgs.ExceptionObject?.GetType().FullName ?? "null"}");

        HandleUnhandledException(exception, eventArgs.IsTerminating);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        _ = sender;
        HandleUnobservedTaskException(eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private void SafeShowErrorMessage(string message, string caption, MessageBoxIcon icon)
    {
        try
        {
            _showErrorMessage(message, caption, icon);
        }
        catch (Exception ex)
        {
            _logger.Log(
                AppLogLevel.Error,
                nameof(GlobalExceptionHandlerRegistration),
                "Global hata iletisi gosterilirken ek bir hata olustu.",
                ex);
        }
    }

    private static void ShowErrorMessage(string message, string caption, MessageBoxIcon icon)
    {
        ModalDialogOverlay.ShowMessage(
            ResolveActiveOwner(),
            message,
            caption,
            MessageBoxButtons.OK,
            icon);
    }

    private static Form? ResolveActiveOwner()
    {
        try
        {
            Form? activeForm = Form.ActiveForm;

            if (CanUseAsOwner(activeForm))
            {
                return activeForm;
            }

            for (int index = global::System.Windows.Forms.Application.OpenForms.Count - 1; index >= 0; index--)
            {
                Form? form = global::System.Windows.Forms.Application.OpenForms[index];

                if (CanUseAsOwner(form))
                {
                    return form;
                }
            }
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        return null;
    }

    private static bool CanUseAsOwner(Form? form)
    {
        return form is not null
            && !form.IsDisposed
            && form.IsHandleCreated
            && form.Visible
            && form.WindowState != FormWindowState.Minimized;
    }
}
