using Makredo.Domain.Enums;
using Makredo.Domain.Models;

namespace Makredo.Application.Services;

internal sealed class HeldInputTracker
{
    private readonly List<(InputIdentity Identity, MacroEvent Release)> _pressed = [];

    internal void RecordSuccessfulSend(MacroEvent input)
    {
        if (!TryDescribe(input, out InputIdentity identity, out bool pressed, out MouseActionType mouseUp))
        {
            return;
        }

        int existing = _pressed.FindIndex(item => item.Identity == identity);
        if (!pressed)
        {
            if (existing >= 0) { _pressed.RemoveAt(existing); }
            return;
        }

        // Autorepeat does not create another held interval.
        if (existing < 0)
        {
            _pressed.Add((identity, new MacroEvent
            {
                EventType = input.EventType,
                KeyboardActionType = input.EventType == MacroEventType.Keyboard ? KeyboardActionType.KeyUp : KeyboardActionType.None,
                MouseActionType = mouseUp,
                KeyCode = input.KeyCode,
                ScanCode = input.ScanCode,
                IsExtendedKey = input.IsExtendedKey,
                X = input.X,
                Y = input.Y,
                TimestampUtc = input.TimestampUtc
            }));
        }
    }

    internal IEnumerable<MacroEvent> TakeReleases()
    {
        while (_pressed.Count > 0)
        {
            int last = _pressed.Count - 1;
            MacroEvent release = _pressed[last].Release;
            _pressed.RemoveAt(last);
            yield return release;
        }
    }

    private static bool TryDescribe(MacroEvent input, out InputIdentity identity, out bool pressed, out MouseActionType mouseUp)
    {
        identity = default;
        pressed = false;
        mouseUp = MouseActionType.None;
        if (input.EventType == MacroEventType.Keyboard
            && input.KeyboardActionType is KeyboardActionType.KeyDown or KeyboardActionType.KeyUp)
        {
            bool usesScanCode = input.ScanCode is > 0;
            identity = new InputIdentity(true, usesScanCode, usesScanCode ? input.ScanCode!.Value : input.KeyCode ?? 0, input.IsExtendedKey);
            pressed = input.KeyboardActionType == KeyboardActionType.KeyDown;
            return true;
        }

        if (input.EventType != MacroEventType.Mouse) { return false; }
        mouseUp = input.MouseActionType switch
        {
            MouseActionType.LeftDown or MouseActionType.LeftUp => MouseActionType.LeftUp,
            MouseActionType.RightDown or MouseActionType.RightUp => MouseActionType.RightUp,
            MouseActionType.MiddleDown or MouseActionType.MiddleUp => MouseActionType.MiddleUp,
            _ => MouseActionType.None
        };
        if (mouseUp == MouseActionType.None) { return false; }
        identity = new InputIdentity(false, false, (int)mouseUp, false);
        pressed = input.MouseActionType is MouseActionType.LeftDown or MouseActionType.RightDown or MouseActionType.MiddleDown;
        return true;
    }

    private readonly record struct InputIdentity(bool Keyboard, bool ScanCode, int Code, bool Extended);
}
