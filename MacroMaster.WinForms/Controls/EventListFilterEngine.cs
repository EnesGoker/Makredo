using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using System.Globalization;

namespace MacroMaster.WinForms.Controls;

internal static class EventListFilterEngine
{
    internal const int LongDelayThresholdMs = 250;
    internal const int OptimizationCandidateMaxDelayMs = 100;

    public static EventListViewItem[] Apply(
        IReadOnlyList<MacroEvent> events,
        EventListFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(events);

        var items = new List<EventListViewItem>(events.Count);
        int elapsedMs = 0;

        for (int index = 0; index < events.Count; index++)
        {
            MacroEvent macroEvent = events[index];
            elapsedMs += Math.Max(0, macroEvent.DelayMs);

            if (Matches(events, index, elapsedMs, criteria))
            {
                items.Add(new EventListViewItem(index, macroEvent, elapsedMs));
            }
        }

        return items.ToArray();
    }

    public static bool Matches(
        IReadOnlyList<MacroEvent> events,
        int sourceIndex,
        int elapsedMs,
        EventListFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (sourceIndex < 0 || sourceIndex >= events.Count)
        {
            return false;
        }

        MacroEvent macroEvent = events[sourceIndex];

        return MatchesType(macroEvent, criteria.TypeFilter)
            && MatchesSmartFilter(events, sourceIndex, criteria.SmartFilter)
            && MatchesSearch(macroEvent, sourceIndex, elapsedMs, criteria.SearchTerm);
    }

    public static EventListRowInsight GetInsight(
        IReadOnlyList<MacroEvent> events,
        int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (sourceIndex < 0 || sourceIndex >= events.Count)
        {
            return EventListRowInsight.None;
        }

        MacroEvent macroEvent = events[sourceIndex];

        if (IsInvalidOrIncomplete(macroEvent))
        {
            return EventListRowInsight.InvalidOrIncomplete;
        }

        if (IsOptimizationCandidate(events, sourceIndex))
        {
            return EventListRowInsight.OptimizationCandidate;
        }

        return macroEvent.DelayMs >= LongDelayThresholdMs
            ? EventListRowInsight.LongDelay
            : EventListRowInsight.None;
    }

    private static bool MatchesType(
        MacroEvent macroEvent,
        EventListTypeFilterKind typeFilter)
    {
        return typeFilter switch
        {
            EventListTypeFilterKind.All => true,
            EventListTypeFilterKind.Keyboard => macroEvent.EventType == MacroEventType.Keyboard,
            EventListTypeFilterKind.Mouse => macroEvent.EventType == MacroEventType.Mouse,
            EventListTypeFilterKind.System => macroEvent.EventType == MacroEventType.System,
            _ => true
        };
    }

    private static bool MatchesSmartFilter(
        IReadOnlyList<MacroEvent> events,
        int sourceIndex,
        EventListSmartFilterKind smartFilter)
    {
        MacroEvent macroEvent = events[sourceIndex];

        return smartFilter switch
        {
            EventListSmartFilterKind.All => true,
            EventListSmartFilterKind.LongDelays => macroEvent.DelayMs >= LongDelayThresholdMs,
            EventListSmartFilterKind.OptimizationCandidates => IsOptimizationCandidate(events, sourceIndex),
            EventListSmartFilterKind.InvalidOrIncomplete => IsInvalidOrIncomplete(macroEvent),
            _ => true
        };
    }

    private static bool MatchesSearch(
        MacroEvent macroEvent,
        int sourceIndex,
        int elapsedMs,
        string? searchTerm)
    {
        string[] searchTokens = Tokenize(searchTerm);

        if (searchTokens.Length == 0)
        {
            return true;
        }

        string rowNumber = (sourceIndex + 1).ToString("000", CultureInfo.InvariantCulture);
        string[] searchableValues =
        [
            rowNumber,
            (sourceIndex + 1).ToString(CultureInfo.InvariantCulture),
            FormatElapsedTime(elapsedMs),
            FormatEventType(macroEvent.EventType),
            macroEvent.EventType.ToString(),
            FormatAction(macroEvent),
            FormatPosition(macroEvent),
            macroEvent.DelayMs.ToString(CultureInfo.InvariantCulture),
            FormattableString.Invariant($"{macroEvent.DelayMs} ms"),
            macroEvent.Description ?? string.Empty,
            macroEvent.KeyName ?? string.Empty,
            macroEvent.KeyCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            macroEvent.ScanCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            macroEvent.X?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            macroEvent.Y?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            macroEvent.WheelDelta?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        ];

        return searchTokens.All(token =>
        {
            if (TryMatchStructuredSearchToken(
                    token,
                    macroEvent,
                    sourceIndex,
                    elapsedMs,
                    out bool structuredMatch))
            {
                return structuredMatch;
            }

            return searchableValues.Any(value =>
                value.Contains(token, StringComparison.CurrentCultureIgnoreCase));
        });
    }

    private static bool TryMatchStructuredSearchToken(
        string token,
        MacroEvent macroEvent,
        int sourceIndex,
        int elapsedMs,
        out bool isMatch)
    {
        isMatch = false;

        if (TryMatchComparisonSearchToken(token, macroEvent, sourceIndex, elapsedMs, out isMatch))
        {
            return true;
        }

        int separatorIndex = token.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
        {
            return false;
        }

        string fieldName = token[..separatorIndex].Trim();
        string fieldValue = token[(separatorIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(fieldValue))
        {
            return false;
        }

        if (TryGetNumericFieldValue(fieldName, macroEvent, sourceIndex, elapsedMs, out int numericFieldValue))
        {
            isMatch = int.TryParse(
                    fieldValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int expectedValue)
                && numericFieldValue == expectedValue;
            return true;
        }

        if (IsField(fieldName, "type", "tur", "tür", "event"))
        {
            isMatch = MatchesTypeAlias(macroEvent.EventType, fieldValue);
            return true;
        }

        if (IsField(fieldName, "action", "aksiyon"))
        {
            isMatch = FormatAction(macroEvent).Contains(
                fieldValue,
                StringComparison.CurrentCultureIgnoreCase);
            return true;
        }

        if (IsField(fieldName, "detail", "detay", "desc", "description", "aciklama", "acıklama", "açıklama"))
        {
            string detail = macroEvent.Description ?? string.Empty;
            isMatch = detail.Contains(fieldValue, StringComparison.CurrentCultureIgnoreCase);
            return true;
        }

        if (IsField(fieldName, "key", "tus", "tuş"))
        {
            string keyName = macroEvent.KeyName ?? string.Empty;
            isMatch = keyName.Contains(fieldValue, StringComparison.CurrentCultureIgnoreCase);
            return true;
        }

        return false;
    }

    private static bool TryMatchComparisonSearchToken(
        string token,
        MacroEvent macroEvent,
        int sourceIndex,
        int elapsedMs,
        out bool isMatch)
    {
        isMatch = false;

        foreach (string comparisonOperator in new[] { ">=", "<=", ">", "<", "=" })
        {
            int operatorIndex = token.IndexOf(comparisonOperator, StringComparison.Ordinal);
            if (operatorIndex <= 0 || operatorIndex >= token.Length - comparisonOperator.Length)
            {
                continue;
            }

            string fieldName = token[..operatorIndex].Trim();
            string expectedValueText = token[(operatorIndex + comparisonOperator.Length)..].Trim();

            if (!TryGetNumericFieldValue(fieldName, macroEvent, sourceIndex, elapsedMs, out int actualValue))
            {
                return false;
            }

            if (!int.TryParse(
                    expectedValueText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int expectedValue))
            {
                isMatch = false;
                return true;
            }

            isMatch = comparisonOperator switch
            {
                ">=" => actualValue >= expectedValue,
                "<=" => actualValue <= expectedValue,
                ">" => actualValue > expectedValue,
                "<" => actualValue < expectedValue,
                "=" => actualValue == expectedValue,
                _ => false
            };
            return true;
        }

        return false;
    }

    private static bool TryGetNumericFieldValue(
        string fieldName,
        MacroEvent macroEvent,
        int sourceIndex,
        int elapsedMs,
        out int value)
    {
        value = 0;

        if (IsField(fieldName, "row", "no", "#", "sira", "sıra"))
        {
            value = sourceIndex + 1;
            return true;
        }

        if (IsField(fieldName, "delay", "ms", "gecikme"))
        {
            value = macroEvent.DelayMs;
            return true;
        }

        if (IsField(fieldName, "elapsed", "time", "zaman", "sure", "süre"))
        {
            value = elapsedMs;
            return true;
        }

        if (IsField(fieldName, "x"))
        {
            if (!macroEvent.X.HasValue)
            {
                return false;
            }

            value = macroEvent.X.Value;
            return true;
        }

        if (IsField(fieldName, "y"))
        {
            if (!macroEvent.Y.HasValue)
            {
                return false;
            }

            value = macroEvent.Y.Value;
            return true;
        }

        if (IsField(fieldName, "wheel", "delta", "kaydirma", "kaydırma"))
        {
            if (!macroEvent.WheelDelta.HasValue)
            {
                return false;
            }

            value = macroEvent.WheelDelta.Value;
            return true;
        }

        if (IsField(fieldName, "keycode"))
        {
            if (!macroEvent.KeyCode.HasValue)
            {
                return false;
            }

            value = macroEvent.KeyCode.Value;
            return true;
        }

        if (IsField(fieldName, "scancode"))
        {
            if (!macroEvent.ScanCode.HasValue)
            {
                return false;
            }

            value = macroEvent.ScanCode.Value;
            return true;
        }

        return false;
    }

    private static bool MatchesTypeAlias(MacroEventType eventType, string value)
    {
        return eventType switch
        {
            MacroEventType.Keyboard => IsValue(value, "keyboard", "klavye"),
            MacroEventType.Mouse => IsValue(value, "mouse", "fare"),
            MacroEventType.System => IsValue(value, "system", "sistem"),
            _ => eventType.ToString().Equals(value, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool IsField(string value, params string[] aliases)
    {
        return aliases.Any(alias => value.Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValue(string value, params string[] aliases)
    {
        return aliases.Any(alias => value.Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsOptimizationCandidate(
        IReadOnlyList<MacroEvent> events,
        int sourceIndex)
    {
        if (sourceIndex <= 0 || sourceIndex >= events.Count - 1)
        {
            return false;
        }

        MacroEvent macroEvent = events[sourceIndex];

        return IsMouseMove(macroEvent)
            && macroEvent.DelayMs <= OptimizationCandidateMaxDelayMs
            && IsMouseMove(events[sourceIndex - 1])
            && IsMouseMove(events[sourceIndex + 1]);
    }

    private static bool IsInvalidOrIncomplete(MacroEvent macroEvent)
    {
        return macroEvent.EventType switch
        {
            MacroEventType.Keyboard => macroEvent.KeyboardActionType == KeyboardActionType.None
                || (!macroEvent.KeyCode.HasValue && !macroEvent.ScanCode.HasValue),
            MacroEventType.Mouse => macroEvent.MouseActionType == MouseActionType.None
                || (RequiresCoordinates(macroEvent.MouseActionType)
                    && (!macroEvent.X.HasValue || !macroEvent.Y.HasValue))
                || (macroEvent.MouseActionType == MouseActionType.Wheel
                    && !macroEvent.WheelDelta.HasValue),
            _ => false
        };
    }

    private static bool IsMouseMove(MacroEvent macroEvent)
    {
        return macroEvent.EventType == MacroEventType.Mouse
            && macroEvent.MouseActionType == MouseActionType.Move;
    }

    private static bool RequiresCoordinates(MouseActionType mouseActionType)
    {
        return mouseActionType is MouseActionType.Move
            or MouseActionType.LeftDown
            or MouseActionType.LeftUp
            or MouseActionType.RightDown
            or MouseActionType.RightUp
            or MouseActionType.MiddleDown
            or MouseActionType.MiddleUp
            or MouseActionType.DoubleClick;
    }

    private static string[] Tokenize(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return [];
        }

        return searchTerm
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    private static string FormatElapsedTime(int elapsedMs)
    {
        TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMs);
        return FormattableString.Invariant(
            $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}");
    }

    private static string FormatEventType(MacroEventType eventType)
    {
        return eventType switch
        {
            MacroEventType.Keyboard => "Klavye",
            MacroEventType.Mouse => "Fare",
            MacroEventType.System => "Sistem",
            _ => eventType.ToString()
        };
    }

    private static string FormatAction(MacroEvent macroEvent)
    {
        return macroEvent.EventType switch
        {
            MacroEventType.Keyboard => macroEvent.KeyboardActionType.ToString(),
            MacroEventType.Mouse => macroEvent.MouseActionType.ToString(),
            MacroEventType.System => "Sistem",
            _ => macroEvent.Description ?? string.Empty
        };
    }

    private static string FormatPosition(MacroEvent macroEvent)
    {
        if (macroEvent.X.HasValue && macroEvent.Y.HasValue)
        {
            return FormattableString.Invariant($"X: {macroEvent.X}, Y: {macroEvent.Y}");
        }

        return string.Empty;
    }
}
