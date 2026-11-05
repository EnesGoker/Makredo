using System.Text;
using Makredo.Domain;

namespace Makredo.WinForms.Composition.Migration;

internal static class MigrationStartupFailureReporter
{
    private const int MaximumReasonLength = 240;

    internal static void Report(
        Exception exception,
        Action<string, string>? showMessage = null,
        Func<Exception, string?>? writeDiagnosticLog = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string? diagnosticLogPath;
        try
        {
            diagnosticLogPath = writeDiagnosticLog is null
                ? TryWriteDiagnosticLog(exception, Path.GetTempPath())
                : writeDiagnosticLog(exception);
        }
        catch (Exception)
        {
            // Failure reporting must remain available even when temporary storage is unavailable.
            diagnosticLogPath = null;
        }

        string message = BuildMessage(exception, diagnosticLogPath);
        (showMessage ?? ShowMessageBox)(message, ProductInfo.Name);
    }

    internal static string? TryWriteDiagnosticLog(Exception exception, string temporaryDirectoryPath)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryDirectoryPath);

        try
        {
            byte[] contents = Encoding.UTF8.GetBytes(exception.ToString());
            string filePath = Path.Combine(
                temporaryDirectoryPath,
                $"{ProductInfo.Name.ToLowerInvariant()}-migration-{Guid.NewGuid():N}.log");

            using var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            stream.Write(contents);
            stream.Flush(flushToDisk: true);
            return filePath;
        }
        catch (Exception)
        {
            // This fallback must not create the final application directory or suppress the message.
            return null;
        }
    }

    private static string BuildMessage(Exception exception, string? diagnosticLogPath)
    {
        string message =
            $"Eski {LegacyStoragePaths.DirectoryName} verileri taşınamadı.{Environment.NewLine}{Environment.NewLine}"
            + $"Verileriniz %LOCALAPPDATA%\\{LegacyStoragePaths.DirectoryName} klasöründe dokunulmadan duruyor.{Environment.NewLine}"
            + $"{LegacyStoragePaths.DirectoryName} açıksa kapatıp tekrar deneyin.{Environment.NewLine}{Environment.NewLine}"
            + $"Hata nedeni: {GetShortReason(exception)}";

        if (!string.IsNullOrWhiteSpace(diagnosticLogPath))
        {
            message += $"{Environment.NewLine}{Environment.NewLine}Hata ayrıntıları: {diagnosticLogPath}";
        }

        return message;
    }

    private static string GetShortReason(Exception exception)
    {
        string reason = string.Join(
            " ",
            exception.GetBaseException().Message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (reason.Length == 0)
        {
            return "Beklenmeyen bir hata oluştu.";
        }

        if (reason.Length <= MaximumReasonLength)
        {
            return reason;
        }

        int length = char.IsHighSurrogate(reason[MaximumReasonLength - 1])
            ? MaximumReasonLength - 1
            : MaximumReasonLength;
        return reason[..length] + "…";
    }

    private static void ShowMessageBox(string message, string caption)
    {
        MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
