using System.Text.Json;
using System.Text.Json.Nodes;
using Makredo.Application.Abstractions;
using Makredo.Application.Hotkeys;
using Makredo.Application.Services;
using Makredo.Infrastructure.Migration;

namespace Makredo.Infrastructure.Persistence;

public sealed class JsonHotkeySettingsStore : IHotkeySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly IAppLogger _logger;

    public JsonHotkeySettingsStore(string filePath, IAppLogger? logger = null)
    {
        _filePath = AtomicFileWriteHelper.ResolveFilePath(filePath);
        _logger = logger ?? NullAppLogger.Instance;
    }

    public async Task<HotkeySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return await ApplyPendingMigrationAsync(HotkeySettings.CreateDefault(), cancellationToken);
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

        await stream.DisposeAsync();
        HotkeySettingsValidator.Validate(
            hotkeySettings,
            $"Kisayol ayari yukleme: '{_filePath}'");

        return await ApplyPendingMigrationAsync(hotkeySettings, cancellationToken);
    }

    private async Task<HotkeySettings> ApplyPendingMigrationAsync(HotkeySettings settings, CancellationToken cancellationToken)
    {
        string markerPath = Path.Combine(Path.GetDirectoryName(_filePath)!, DataDirectoryMigrator.CompletionMarkerFileName);
        if (!File.Exists(markerPath)) { return settings; }
        HotkeySettings effective = settings;
        try
        {
            JsonObject marker = JsonNode.Parse(await File.ReadAllTextAsync(markerPath, cancellationToken))?.AsObject()
                ?? throw new InvalidOperationException("Kısayol göç işareti geçersiz.");
            if (marker["hotkeyMigrationPending"]?.GetValue<bool>() != true) { return settings; }
            if (File.Exists(_filePath) && !File.Exists(_filePath + ".pre-t15.bak")) { File.Copy(_filePath, _filePath + ".pre-t15.bak"); }
            if (!File.Exists(markerPath + ".pre-t15.bak")) { File.Copy(markerPath, markerPath + ".pre-t15.bak"); }
            if (HotkeyDefaultsMigration.ShouldUpgrade(settings, true))
            {
                HotkeySettings upgraded = HotkeySettings.CreateDefault();
                await SaveAsync(upgraded, cancellationToken);
                effective = upgraded;
            }
            marker["hotkeyMigrationPending"] = false;
            await AtomicFileWriteHelper.WriteAsync(markerPath, async (stream, token) =>
            {
                await JsonSerializer.SerializeAsync(stream, marker, cancellationToken: token);
            }, cancellationToken);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            _logger.Log(AppLogLevel.Warning, nameof(JsonHotkeySettingsStore), "Kısayol göçü tamamlanamadı; mevcut ayarlar korunuyor ve sonraki açılışta tekrar denenecek.", exception);
        }
        return effective;
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
