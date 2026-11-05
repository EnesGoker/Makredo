using System.Text;
using System.Collections.Concurrent;
using MacroMaster.Application.Abstractions;

namespace MacroMaster.Infrastructure.Diagnostics;

public sealed class FileLogger : IAppLogger, IDisposable
{
    private const int MaxQueuedEntries = 2048;
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly BlockingCollection<QueuedLogEntry> _queuedEntries = new(MaxQueuedEntries);
    private readonly string _logDirectoryPath;
    private readonly Thread _writerThread;
    private int _droppedEntryCount;
    private bool _disposed;

    public FileLogger(string logDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(logDirectoryPath))
        {
            throw new ArgumentException("Gunluk klasor yolu bos olamaz.", nameof(logDirectoryPath));
        }

        _logDirectoryPath = Path.GetFullPath(logDirectoryPath);
        _writerThread = new Thread(ProcessQueuedEntries)
        {
            IsBackground = true,
            Name = "MacroMaster.FileLogger"
        };
        _writerThread.Start();
    }

    public void Log(
        AppLogLevel logLevel,
        string source,
        string message,
        Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            source = "BilinmeyenKaynak";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "Bos gunluk mesaji";
        }

        try
        {
            if (_disposed || _queuedEntries.IsAddingCompleted)
            {
                return;
            }

            string logEntry = BuildLogEntry(logLevel, source, message, exception);
            string logFilePath = GetDailyLogFilePath(DateTimeOffset.Now);

            if (!_queuedEntries.TryAdd(new QueuedLogEntry(logFilePath, logEntry)))
            {
                Interlocked.Increment(ref _droppedEntryCount);
            }
        }
        catch
        {
            // Loglama hicbir zaman uygulama akisini bozmaz.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _queuedEntries.CompleteAdding();
            if (_writerThread.Join(ShutdownFlushTimeout))
            {
                _queuedEntries.Dispose();
            }
        }
        catch
        {
            // Loglama hicbir zaman uygulama akisini bozmaz.
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    private string GetDailyLogFilePath(DateTimeOffset now)
    {
        return Path.Combine(
            _logDirectoryPath,
            $"macromaster-{now:yyyyMMdd}.log");
    }

    private void ProcessQueuedEntries()
    {
        foreach (QueuedLogEntry queuedLogEntry in _queuedEntries.GetConsumingEnumerable())
        {
            try
            {
                WriteDroppedEntriesNoticeIfNeeded(queuedLogEntry.FilePath);
                Directory.CreateDirectory(_logDirectoryPath);
                File.AppendAllText(queuedLogEntry.FilePath, queuedLogEntry.Content, Utf8WithoutBom);
            }
            catch
            {
                // Loglama hicbir zaman uygulama akisini bozmaz.
            }
        }

        try
        {
            WriteDroppedEntriesNoticeIfNeeded(GetDailyLogFilePath(DateTimeOffset.Now));
        }
        catch
        {
            // Loglama hicbir zaman uygulama akisini bozmaz.
        }
    }

    private static string BuildLogEntry(
        AppLogLevel logLevel,
        string source,
        string message,
        Exception? exception)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        string levelText = logLevel switch
        {
            AppLogLevel.Information => "Bilgi",
            AppLogLevel.Warning => "Uyari",
            AppLogLevel.Error => "Hata",
            _ => "Bilinmeyen"
        };

        StringBuilder builder = new();
        builder.Append('[')
            .Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", System.Globalization.CultureInfo.InvariantCulture))
            .Append("] [")
            .Append(levelText)
            .Append("] [")
            .Append(source)
            .Append("] ")
            .AppendLine(message);

        if (exception is not null)
        {
            builder.AppendLine(exception.ToString());
        }

        return builder.ToString();
    }

    private void WriteDroppedEntriesNoticeIfNeeded(string logFilePath)
    {
        int droppedEntryCount = Interlocked.Exchange(ref _droppedEntryCount, 0);

        if (droppedEntryCount <= 0)
        {
            return;
        }

        Directory.CreateDirectory(_logDirectoryPath);
        string warningEntry = BuildLogEntry(
            AppLogLevel.Warning,
            nameof(FileLogger),
            $"{droppedEntryCount} gunluk kaydi kuyruk kapasitesi nedeniyle atlandi.",
            null);
        File.AppendAllText(logFilePath, warningEntry, Utf8WithoutBom);
    }

    private readonly record struct QueuedLogEntry(
        string FilePath,
        string Content);
}
