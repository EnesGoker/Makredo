using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;

namespace MacroMaster.Infrastructure.Persistence;

internal static class MacroSessionPersistenceContract
{
    internal static void PrepareForWrite(MacroSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        session.FormatVersion = MacroSessionFormat.CurrentVersion;
        ValidateSession(session, "kaydetme", null);
    }

    internal static MacroSession ValidateAfterRead(MacroSession? session, string filePath)
    {
        if (session is null)
        {
            throw new InvalidOperationException(
                $"Dosyadan gecerli bir makro oturumu okunamadi: {filePath}");
        }

        ValidateSession(session, "yukleme", filePath);
        return session;
    }

    private static void ValidateSession(
        MacroSession session,
        string operation,
        string? filePath)
    {
        if (string.IsNullOrWhiteSpace(session.FormatVersion))
        {
            throw CreateValidationException(
                "Makro oturumu bicim surumu eksik.",
                operation,
                filePath);
        }

        if (!MacroSessionFormat.SupportedVersions.Contains(session.FormatVersion))
        {
            throw CreateValidationException(
                $"Desteklenmeyen makro oturumu bicim surumu: {session.FormatVersion}.",
                operation,
                filePath);
        }

        if (session.Events is null)
        {
            throw CreateValidationException(
                "Makro oturumu olay listesi bos olamaz.",
                operation,
                filePath);
        }

        for (int index = 0; index < session.Events.Count; index++)
        {
            ValidateEvent(session.Events[index], index, operation, filePath);
        }
    }

    private static void ValidateEvent(
        MacroEvent macroEvent,
        int index,
        string operation,
        string? filePath)
    {
        if (macroEvent is null)
        {
            throw CreateValidationException(
                $"Makro oturumunda {index}. sirada bos olay var.",
                operation,
                filePath);
        }

        if (macroEvent.DelayMs < 0)
        {
            throw CreateValidationException(
                $"{index}. olay negatif gecikme iceriyor.",
                operation,
                filePath);
        }

        if (!Enum.IsDefined(macroEvent.EventType))
        {
            throw CreateValidationException(
                $"{index}. olay gecersiz olay tipi degeri iceriyor: {(int)macroEvent.EventType}.",
                operation,
                filePath);
        }

        switch (macroEvent.EventType)
        {
            case MacroEventType.Keyboard:
                ValidateKeyboardEvent(macroEvent, index, operation, filePath);
                break;

            case MacroEventType.Mouse:
                ValidateMouseEvent(macroEvent, index, operation, filePath);
                break;

            case MacroEventType.System:
                throw CreateValidationException(
                    $"Kaydedilmis oturumlarda sistem olaylari desteklenmez. Olay sirasi: {index}.",
                    operation,
                    filePath);
        }
    }

    private static void ValidateKeyboardEvent(
        MacroEvent macroEvent,
        int index,
        string operation,
        string? filePath)
    {
        if (!Enum.IsDefined(macroEvent.KeyboardActionType))
        {
            throw CreateValidationException(
                $"Klavye olayi {index} gecersiz klavye eylem degeri iceriyor: {(int)macroEvent.KeyboardActionType}.",
                operation,
                filePath);
        }

        if (macroEvent.KeyboardActionType == KeyboardActionType.None)
        {
            throw CreateValidationException(
                $"Klavye olayi {index} bir tus basma veya tus birakma eylemi belirtmelidir.",
                operation,
                filePath);
        }

        if (macroEvent.MouseActionType != MouseActionType.None)
        {
            throw CreateValidationException(
                $"Klavye olayi {index} beklenmeyen fare eylemi degeri iceriyor: {macroEvent.MouseActionType}.",
                operation,
                filePath);
        }

        if (!macroEvent.KeyCode.HasValue && !macroEvent.ScanCode.HasValue)
        {
            throw CreateValidationException(
                $"Klavye olayi {index} en az bir sanal tus veya scan code bilgisi icermelidir.",
                operation,
                filePath);
        }
    }

    private static void ValidateMouseEvent(
        MacroEvent macroEvent,
        int index,
        string operation,
        string? filePath)
    {
        if (!Enum.IsDefined(macroEvent.MouseActionType))
        {
            throw CreateValidationException(
                $"Fare olayi {index} gecersiz fare eylem degeri iceriyor: {(int)macroEvent.MouseActionType}.",
                operation,
                filePath);
        }

        if (macroEvent.MouseActionType == MouseActionType.None)
        {
            throw CreateValidationException(
                $"Fare olayi {index} somut bir fare eylemi belirtmelidir.",
                operation,
                filePath);
        }

        if (macroEvent.KeyboardActionType != KeyboardActionType.None)
        {
            throw CreateValidationException(
                $"Fare olayi {index} beklenmeyen klavye eylemi degeri iceriyor: {macroEvent.KeyboardActionType}.",
                operation,
                filePath);
        }

        if (RequiresMouseCoordinates(macroEvent.MouseActionType)
            && (!macroEvent.X.HasValue || !macroEvent.Y.HasValue))
        {
            throw CreateValidationException(
                $"Fare olayi {index} ({macroEvent.MouseActionType}) koordinat bilgisi icermiyor.",
                operation,
                filePath);
        }

        if (macroEvent.MouseActionType == MouseActionType.Wheel
            && !macroEvent.WheelDelta.HasValue)
        {
            throw CreateValidationException(
                $"Fare tekerlegi olayi {index} wheel delta bilgisi icermiyor.",
                operation,
                filePath);
        }
    }

    private static bool RequiresMouseCoordinates(MouseActionType mouseActionType)
    {
        return mouseActionType is MouseActionType.Move
            or MouseActionType.LeftDown
            or MouseActionType.LeftUp
            or MouseActionType.RightDown
            or MouseActionType.RightUp
            or MouseActionType.MiddleDown
            or MouseActionType.MiddleUp
            or MouseActionType.Wheel
            or MouseActionType.DoubleClick;
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
            $"Makro oturumu dogrulamasi {operation} sirasinda basarisiz oldu. {message}{sourceSuffix}");
    }
}
