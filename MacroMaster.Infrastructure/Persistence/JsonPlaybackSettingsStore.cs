using System.Text.Json;
using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Models;

namespace MacroMaster.Infrastructure.Persistence;

public sealed class JsonPlaybackSettingsStore : IPlaybackSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public JsonPlaybackSettingsStore(string filePath)
    {
        _filePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
    }

    public async Task<PlaybackSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new PlaybackSettings();
        }

        await using FileStream stream = new(
            _filePath,
            new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });

        PlaybackSettings? settings = await JsonSerializer.DeserializeAsync<PlaybackSettings>(
            stream,
            JsonOptions,
            cancellationToken);

        return PlaybackSettingsPersistenceContract.ValidateAfterRead(settings, _filePath);
    }

    public async Task SaveAsync(
        PlaybackSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        PlaybackSettingsPersistenceContract.PrepareForWrite(settings);

        await AtomicFileWriteHelper.WriteAsync(
            _filePath,
            async (stream, token) =>
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    JsonOptions,
                    token);
            },
            cancellationToken);
    }
}
