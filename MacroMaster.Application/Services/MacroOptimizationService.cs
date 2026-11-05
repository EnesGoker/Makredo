using MacroMaster.Application.Abstractions;
using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;

namespace MacroMaster.Application.Services;

public sealed class MacroOptimizationService : IMacroOptimizationService
{
    public const int LongDelayThresholdMs = 250;
    public const int BalancedMouseMoveSampleIntervalMs = 200;
    public const int BalancedMouseMoveSampleDistancePx = 280;

    public MacroOptimizationPreview Preview(MacroSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        bool[] keepMap = BuildKeepMap(session.Events);
        var optimizedEvents = new List<MacroEvent>(session.Events.Count);
        long absorbedDelayMs = 0;
        int removedEventCount = 0;

        for (int index = 0; index < session.Events.Count; index++)
        {
            MacroEvent macroEvent = session.Events[index];

            if (keepMap[index])
            {
                MacroEvent keptEvent = CloneMacroEvent(macroEvent);
                keptEvent.DelayMs = AddDelaySafely(keptEvent.DelayMs, absorbedDelayMs);
                optimizedEvents.Add(keptEvent);
                absorbedDelayMs = 0;
                continue;
            }

            absorbedDelayMs += Math.Max(0, macroEvent.DelayMs);
            removedEventCount++;
        }

        if (absorbedDelayMs > 0 && optimizedEvents.Count > 0)
        {
            MacroEvent lastEvent = optimizedEvents[^1];
            lastEvent.DelayMs = AddDelaySafely(lastEvent.DelayMs, absorbedDelayMs);
        }

        int originalDurationMs = CalculateTotalDurationMs(session.Events);
        int optimizedDurationMs = CalculateTotalDurationMs(optimizedEvents);

        return new MacroOptimizationPreview(
            session.Name,
            session.Events.Count,
            optimizedEvents.Count,
            removedEventCount,
            originalDurationMs,
            optimizedDurationMs,
            optimizedEvents);
    }

    private static bool[] BuildKeepMap(IReadOnlyList<MacroEvent> events)
    {
        var keepMap = new bool[events.Count];
        int index = 0;

        while (index < events.Count)
        {
            if (!IsOptimizableMouseMove(events[index]))
            {
                keepMap[index] = true;
                index++;
                continue;
            }

            int segmentStart = index;
            while (index < events.Count && IsOptimizableMouseMove(events[index]))
            {
                index++;
            }

            MarkBalancedMouseMoveSegment(events, keepMap, segmentStart, index - 1);
        }

        return keepMap;
    }

    private static void MarkBalancedMouseMoveSegment(
        IReadOnlyList<MacroEvent> events,
        bool[] keepMap,
        int startIndex,
        int endIndex)
    {
        keepMap[startIndex] = true;

        if (startIndex == endIndex)
        {
            return;
        }

        int lastKeptIndex = startIndex;
        int accumulatedDelayMs = 0;

        for (int index = startIndex + 1; index <= endIndex; index++)
        {
            MacroEvent candidate = events[index];
            accumulatedDelayMs = AddDelaySafely(accumulatedDelayMs, Math.Max(0, candidate.DelayMs));

            bool isLastMoveBeforeAction = index == endIndex;
            bool shouldKeepSample = accumulatedDelayMs >= BalancedMouseMoveSampleIntervalMs
                || HasMovedFarEnough(events[lastKeptIndex], candidate);

            if (isLastMoveBeforeAction
                || candidate.DelayMs >= LongDelayThresholdMs
                || shouldKeepSample)
            {
                keepMap[index] = true;
                lastKeptIndex = index;
                accumulatedDelayMs = 0;
            }
        }
    }

    private static bool HasMovedFarEnough(MacroEvent previousKeptMove, MacroEvent candidate)
    {
        if (!previousKeptMove.X.HasValue
            || !previousKeptMove.Y.HasValue
            || !candidate.X.HasValue
            || !candidate.Y.HasValue)
        {
            return false;
        }

        long deltaX = candidate.X.Value - previousKeptMove.X.Value;
        long deltaY = candidate.Y.Value - previousKeptMove.Y.Value;
        long squaredDistance = deltaX * deltaX + deltaY * deltaY;
        long squaredThreshold = (long)BalancedMouseMoveSampleDistancePx * BalancedMouseMoveSampleDistancePx;

        return squaredDistance >= squaredThreshold;
    }

    private static bool IsOptimizableMouseMove(MacroEvent macroEvent)
    {
        return macroEvent.EventType == MacroEventType.Mouse
            && macroEvent.MouseActionType == MouseActionType.Move
            && macroEvent.X.HasValue
            && macroEvent.Y.HasValue;
    }

    private static int CalculateTotalDurationMs(IEnumerable<MacroEvent> events)
    {
        long totalDurationMs = events.Sum(macroEvent => (long)Math.Max(0, macroEvent.DelayMs));
        return totalDurationMs > int.MaxValue
            ? int.MaxValue
            : (int)totalDurationMs;
    }

    private static int AddDelaySafely(int originalDelayMs, long absorbedDelayMs)
    {
        long totalDelayMs = Math.Max(0, originalDelayMs) + Math.Max(0, absorbedDelayMs);
        return totalDelayMs > int.MaxValue
            ? int.MaxValue
            : (int)totalDelayMs;
    }

    private static MacroEvent CloneMacroEvent(MacroEvent source)
    {
        return new MacroEvent
        {
            Id = source.Id,
            EventType = source.EventType,
            KeyboardActionType = source.KeyboardActionType,
            MouseActionType = source.MouseActionType,
            DelayMs = source.DelayMs,
            TimestampUtc = source.TimestampUtc,
            KeyCode = source.KeyCode,
            ScanCode = source.ScanCode,
            IsExtendedKey = source.IsExtendedKey,
            KeyName = source.KeyName,
            X = source.X,
            Y = source.Y,
            WheelDelta = source.WheelDelta,
            Description = source.Description
        };
    }
}
