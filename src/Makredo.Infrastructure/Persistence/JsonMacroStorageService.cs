using System.Text.Json;
using Makredo.Application.Abstractions;
using Makredo.Domain.Models;

namespace Makredo.Infrastructure.Persistence;

public sealed class JsonMacroStorageService : IJsonMacroStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task SaveAsync(
        MacroSession session,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        MacroSessionPersistenceContract.PrepareForWrite(session);

        string resolvedFilePath = AtomicFileWriteHelper.ResolveFilePath(filePath);

        await AtomicFileWriteHelper.WriteAsync(
            resolvedFilePath,
            async (stream, token) =>
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    session,
                    JsonOptions,
                    token);
            },
            cancellationToken);
    }

    internal static void WriteTemporarySessionFile(MacroSession session, string temporaryFilePath)
    {
        MacroSessionPersistenceContract.PrepareForWrite(session);
        AtomicFileWriteHelper.WriteTemporaryFile(
            temporaryFilePath,
            stream => JsonSerializer.Serialize(stream, session, JsonOptions));
    }

    public async Task<MacroSession> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string resolvedFilePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
        AtomicFileWriteHelper.EnsureFileExists(resolvedFilePath);

        await using FileStream stream = new(
            resolvedFilePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        MacroSession? session = await JsonSerializer.DeserializeAsync<MacroSession>(
            stream,
            JsonOptions,
            cancellationToken);

        return MacroSessionPersistenceContract.ValidateAfterRead(session, resolvedFilePath);
    }
}
