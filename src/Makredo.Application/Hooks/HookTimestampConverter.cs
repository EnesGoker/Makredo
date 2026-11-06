namespace Makredo.Application.Hooks;

/// <summary>Maps ordered, wrapping native millisecond ticks to a fixed UTC anchor.</summary>
public sealed class HookTimestampConverter
{
    private readonly long _anchorUptimeMs;
    private readonly DateTime _anchorUtc;
    private long _lastUptimeMs;
    private uint _lastTick;
    private bool _initialized;

    public HookTimestampConverter(long anchorUptimeMs, DateTimeOffset anchorUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(anchorUptimeMs);
        _anchorUptimeMs = anchorUptimeMs;
        _anchorUtc = anchorUtc.UtcDateTime;
    }

    /// <summary>
    /// The first sample uses the nearest era to the anchor. Later samples use
    /// modular forward deltas; consecutive samples must be less than one full
    /// 32-bit tick cycle apart. Equal ticks retain equal timestamps.
    /// </summary>
    public DateTime Convert(uint hookTick)
    {
        _lastUptimeMs = _initialized
            ? checked(_lastUptimeMs + unchecked(hookTick - _lastTick))
            : checked(_anchorUptimeMs + unchecked((int)(hookTick - (uint)_anchorUptimeMs)));
        _lastTick = hookTick;
        _initialized = true;
        return ToUtc(_lastUptimeMs);
    }

    /// <summary>
    /// Uses a current 64-bit uptime observation to resolve whole idle cycles.
    /// Selects the nearest era, tolerating slightly future caller-supplied ticks.
    /// The sample and observation must be less than half a tick cycle apart.
    /// </summary>
    public DateTime Convert(uint hookTick, long observationUptimeMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(observationUptimeMs);
        _lastUptimeMs = checked(observationUptimeMs + unchecked((int)(hookTick - (uint)observationUptimeMs)));
        _lastTick = hookTick;
        _initialized = true;
        return ToUtc(_lastUptimeMs);
    }

    private DateTime ToUtc(long uptimeMs) => _anchorUtc.AddTicks(checked((uptimeMs - _anchorUptimeMs) * TimeSpan.TicksPerMillisecond));
}
