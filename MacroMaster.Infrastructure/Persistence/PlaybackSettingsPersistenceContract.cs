using MacroMaster.Domain.Models;

namespace MacroMaster.Infrastructure.Persistence;

internal static class PlaybackSettingsPersistenceContract
{
    internal static void PrepareForWrite(PlaybackSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        NormalizeSettings(settings);
        ValidateSettings(settings, "kaydetme", null);
    }

    internal static PlaybackSettings ValidateAfterRead(
        PlaybackSettings? settings,
        string filePath)
    {
        if (settings is null)
        {
            throw new InvalidOperationException(
                $"Dosyadan gecerli bir oynatma ayari okunamadi: {filePath}");
        }

        NormalizeSettings(settings);
        ValidateSettings(settings, "yukleme", filePath);
        return settings;
    }

    private static void NormalizeSettings(PlaybackSettings settings)
    {
        if (settings.PreserveOriginalTiming)
        {
            settings.SpeedMultiplier = 1.0;
        }
    }

    private static void ValidateSettings(
        PlaybackSettings settings,
        string operation,
        string? filePath)
    {
        if (!double.IsFinite(settings.SpeedMultiplier) || settings.SpeedMultiplier <= 0)
        {
            throw CreateValidationException(
                $"Oynatma hiz carpani sifirdan buyuk olmalidir. Gecerli deger: {settings.SpeedMultiplier}.",
                operation,
                filePath);
        }

        if (settings.RepeatCount < 1)
        {
            throw CreateValidationException(
                $"Oynatma tekrar sayisi en az 1 olmalidir. Gecerli deger: {settings.RepeatCount}.",
                operation,
                filePath);
        }

        if (settings.InitialDelayMs < 0)
        {
            throw CreateValidationException(
                $"Oynatma baslangic gecikmesi negatif olamaz. Gecerli deger: {settings.InitialDelayMs}.",
                operation,
                filePath);
        }
    }

    private static InvalidOperationException CreateValidationException(
        string message,
        string operation,
        string? filePath)
    {
        string sourceSuffix = string.IsNullOrWhiteSpace(filePath)
            ? string.Empty
            : $" Kaynak: {filePath}";

        return new InvalidOperationException(
            $"Oynatma ayari dogrulamasi {operation} sirasinda basarisiz oldu. {message}{sourceSuffix}");
    }
}
