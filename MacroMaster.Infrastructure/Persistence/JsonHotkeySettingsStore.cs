using System.Text.Json;
using MacroMaster.Application.Abstractions;

namespace MacroMaster.Infrastructure.Persistence;

public sealed class JsonHotkeySettingsStore : IHotkeySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;

    public JsonHotkeySettingsStore(string filePath)
    {
        _filePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
    }

    public async Task<HotkeySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return HotkeySettings.CreateDefault();
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

        HotkeySettings? hotkeySettings = await JsonSerializer.DeserializeAsync<HotkeySettings>(
            stream,
            JsonOptions,
            cancellationToken);

        if (hotkeySettings is null)
        {
            throw new InvalidOperationException(
                $"Dosyadan gecerli bir kisayol ayari okunamadi: {_filePath}");
        }

        HotkeySettingsValidator.Validate(
            hotkeySettings,
            $"Kisayol ayari yukleme: '{_filePath}'");

        return hotkeySettings;
    }

    public async Task SaveAsync(
        HotkeySettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        HotkeySettings persistedSettings = settings.Clone();
        HotkeySettingsValidator.Validate(
            persistedSettings,
            $"Kisayol ayari kaydetme: '{_filePath}'");

        await AtomicFileWriteHelper.WriteAsync(
            _filePath,
            async (stream, token) =>
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    persistedSettings,
                    JsonOptions,
                    token);
            },
            cancellationToken);
    }
}
