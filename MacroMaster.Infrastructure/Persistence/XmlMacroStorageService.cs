using System.Xml;
using System.Xml.Serialization;
using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Models;

namespace MacroMaster.Infrastructure.Persistence;

public sealed class XmlMacroStorageService : IXmlMacroStorageService
{
    private static readonly XmlSerializer Serializer = new(typeof(MacroSession));
    private static readonly XmlReaderSettings ReaderSettings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
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
            (stream, _) =>
            {
                Serializer.Serialize(stream, session);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public async Task<MacroSession> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string resolvedFilePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
        AtomicFileWriteHelper.EnsureFileExists(resolvedFilePath);

        await using FileStream stream = new(
            resolvedFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var buffer = new MemoryStream();

        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        using XmlReader reader = XmlReader.Create(buffer, ReaderSettings);

        object? result = Serializer.Deserialize(reader);

        return MacroSessionPersistenceContract.ValidateAfterRead(
            result as MacroSession,
            resolvedFilePath);
    }
}
