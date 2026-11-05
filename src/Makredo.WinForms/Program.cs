using System.Diagnostics.CodeAnalysis;
using Makredo.WinForms.Composition;
using Makredo.WinForms.Composition.Migration;

namespace Makredo.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

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
        global::System.Windows.Forms.Application.Run(compositionRoot.CreateMainForm());
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
