namespace MacroMaster.Infrastructure.Persistence;

internal static class AtomicFileWriteHelper
{
    internal static string ResolveFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Dosya yolu bos olamaz.", nameof(filePath));
        }

        return Path.GetFullPath(filePath);
    }

    internal static void EnsureDirectoryExists(string filePath)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                $"Dosya yolu icin hedef klasor cozumlenemedi: {filePath}");

        Directory.CreateDirectory(directoryPath);
    }

    internal static void EnsureFileExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Dosya bulunamadi: {filePath}", filePath);
        }
    }

    internal static async Task WriteAsync(
        string filePath,
        Func<FileStream, CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writeAsync);

        EnsureDirectoryExists(filePath);

        string temporaryFilePath = CreateTemporaryFilePath(filePath);

        try
        {
            await using (var stream = new FileStream(
                             temporaryFilePath,
                             new FileStreamOptions
                             {
                                 Mode = FileMode.CreateNew,
                                 Access = FileAccess.Write,
                                 Share = FileShare.None,
                                 BufferSize = 4096,
                                 Options = FileOptions.Asynchronous | FileOptions.WriteThrough
                             }))
            {
                await writeAsync(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(filePath))
            {
                File.Replace(temporaryFilePath, filePath, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryFilePath, filePath);
            }
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryFilePath);
            throw;
        }
    }

    private static string CreateTemporaryFilePath(string filePath)
    {
        string directoryPath = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException(
                $"Dosya yolu icin hedef klasor cozumlenemedi: {filePath}");

        string fileName = Path.GetFileName(filePath);
        return Path.Combine(directoryPath, $"{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void TryDeleteTemporaryFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best-effort cleanup for temporary files.
        }
    }
}
