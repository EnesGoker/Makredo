using System.Diagnostics.CodeAnalysis;
using Makredo.Application.Services;
using Makredo.Infrastructure.Persistence;
using Makredo.Platform.Windows.Startup;
using Makredo.WinForms.Composition;
using Makredo.WinForms.Composition.Migration;

namespace Makredo.WinForms;

internal static class Program
{
    private const string InstanceMutexName = @"Local\Makredo.SingleInstance";

    [STAThread]
    static void Main()
    {
        using var instanceMutex = new Mutex(initiallyOwned: false, InstanceMutexName);
        if (!SingleInstanceStartup.TryStart(() => instanceMutex.WaitOne(0), WindowsInstanceActivation.SignalExistingInstance))
        {
            return;
        }

        try
        {
            RunPrimaryInstance();
        }
        finally
        {
            instanceMutex.ReleaseMutex();
        }
    }

    private static void RunPrimaryInstance()
    {
        ApplicationConfiguration.Initialize();
        global::System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        using var activation = new WindowsInstanceActivation();

        if (!TryPrepareStorage(
            () => AppCompositionRoot.PrepareDefaultStorage(),
            exception => MigrationStartupFailureReporter.Report(exception),
            out AppStoragePaths? storagePaths))
        {
            Environment.ExitCode = 1;
            return;
        }

        using AppCompositionRoot compositionRoot = AppCompositionRoot.Create(storagePaths);
        using var globalExceptionHandlerRegistration = new GlobalExceptionHandlerRegistration(
            compositionRoot.AppLogger);
        using var form = compositionRoot.CreateMainForm();
        form.Shown += (_, _) => activation.Attach(form);
        global::System.Windows.Forms.Application.Run(form);
    }

    internal static bool TryPrepareStorage(
        Func<AppStoragePaths> prepareStorage,
        Action<Exception> reportFailure,
        [NotNullWhen(true)] out AppStoragePaths? storagePaths)
    {
        ArgumentNullException.ThrowIfNull(prepareStorage);
        ArgumentNullException.ThrowIfNull(reportFailure);

        try
        {
            storagePaths = prepareStorage()
                ?? throw new InvalidOperationException("Uygulama veri yolu hazırlanamadı.");
            return true;
        }
        catch (Exception exception)
        {
            storagePaths = null;
            reportFailure(exception);
            return false;
        }
    }
}
