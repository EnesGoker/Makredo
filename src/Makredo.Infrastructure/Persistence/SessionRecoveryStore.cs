using System.Text.Json;
using Makredo.Application.Abstractions;
using Makredo.Application.Services;
using Makredo.Domain.Models;

namespace Makredo.Infrastructure.Persistence;

public sealed class SessionRecoveryStore : ISessionRecoveryStore
{
    private readonly string _filePath;
    private readonly IJsonMacroStorageService _jsonStorage;
    private readonly IAppLogger _logger;
    private readonly object _commitGate = new();
    private long _nextOperation;
    private long _committedOperation;

    public SessionRecoveryStore(string filePath, IJsonMacroStorageService jsonStorage, IAppLogger? logger = null)
    {
        _filePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
        _jsonStorage = jsonStorage ?? throw new ArgumentNullException(nameof(jsonStorage));
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task SaveAsync(MacroSession session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long operation = Interlocked.Increment(ref _nextOperation);
        string stagedPath = AtomicFileWriteHelper.CreateTemporaryFilePath(_filePath);

        try
        {
            await Task.Run(() => _jsonStorage.SaveAsync(session, stagedPath, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            Commit(operation, () => AtomicFileWriteHelper.CommitTemporaryFile(stagedPath, _filePath), cancellationToken);
        }
        finally
        {
            AtomicFileWriteHelper.TryDeleteTemporaryFile(stagedPath);
        }
    }

    public void SaveForShutdown(MacroSession session)
    {
        long operation = Interlocked.Increment(ref _nextOperation);
        string stagedPath = AtomicFileWriteHelper.CreateTemporaryFilePath(_filePath);

        try
        {
            JsonMacroStorageService.WriteTemporarySessionFile(session, stagedPath);
            Commit(operation, () => AtomicFileWriteHelper.CommitTemporaryFile(stagedPath, _filePath));
        }
        finally
        {
            AtomicFileWriteHelper.TryDeleteTemporaryFile(stagedPath);
        }
    }

    public async Task<MacroSession?> TryLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            MacroSession session = await _jsonStorage.LoadAsync(_filePath, cancellationToken);
            if (session.Events.Sum(static item => (long)item.DelayMs) > int.MaxValue)
            {
                throw new InvalidOperationException("Kurtarma oturumunun toplam süresi desteklenen sınırı aşıyor.");
            }

            return session;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _logger.Log(AppLogLevel.Warning, nameof(SessionRecoveryStore), "Oturum kurtarma dosyası okunamadı.", exception);
            return null;
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long operation = Interlocked.Increment(ref _nextOperation);
        Commit(operation, DeleteFile, cancellationToken);
        return Task.CompletedTask;
    }

    private void Commit(long operation, Action action, CancellationToken cancellationToken = default)
    {
        lock (_commitGate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A staged write cannot replace a newer checkpoint or recreate a discarded file.
            if (operation > _committedOperation)
            {
                action();
                _committedOperation = operation;
            }
        }
    }

    private void DeleteFile()
    {
        try
        {
            File.Delete(_filePath);
        }
        catch (DirectoryNotFoundException)
        {
            // The data directory and recovery file have not been created.
        }
    }
}
