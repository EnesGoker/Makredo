using Makredo.Application.Hooks;
using Xunit;

namespace Makredo.Core.Tests.Hooks;

public sealed class HookTimestampConverterTests
{
    private static readonly DateTimeOffset AnchorUtc = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1_000L, 1_000U, 0L)]
    [InlineData(1_000L, 1_025U, 25L)]
    [InlineData(1_000L, 995U, -5L)]
    [InlineData(4_294_968_296L, 1_005U, 5L)]
    [InlineData(4_294_967_298L, 4_294_967_294U, -4L)]
    public void FirstSampleUsesTheNearestUptimeEra(long anchorUptime, uint sample, long expectedOffset)
    {
        var converter = new HookTimestampConverter(anchorUptime, AnchorUtc);

        DateTime result = converter.Convert(sample);

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(AnchorUtc.AddMilliseconds(expectedOffset).UtcDateTime, result);
    }

    [Fact]
    public void CounterWrapPreservesElapsedMilliseconds()
    {
        var converter = new HookTimestampConverter(uint.MaxValue - 2L, AnchorUtc);

        DateTime beforeWrap = converter.Convert(uint.MaxValue - 1);
        DateTime afterWrap = converter.Convert(3);
        DateTime next = converter.Convert(8);

        Assert.Equal(AnchorUtc.AddMilliseconds(1).UtcDateTime, beforeWrap);
        Assert.Equal(5, (afterWrap - beforeWrap).TotalMilliseconds);
        Assert.Equal(AnchorUtc.AddMilliseconds(6).UtcDateTime, afterWrap);
        Assert.Equal(5, (next - afterWrap).TotalMilliseconds);
    }

    [Fact]
    public void EqualTicksKeepTheSameTimestamp()
    {
        var converter = new HookTimestampConverter(100, AnchorUtc);

        DateTime first = converter.Convert(101);
        DateTime second = converter.Convert(101);
        DateTime later = converter.Convert(102);

        Assert.Equal(first, second);
        Assert.Equal(1, (later - second).TotalMilliseconds);
    }

    [Fact]
    public void UptimeCanCrossMoreThanOneCounterEraDuringAnActiveStream()
    {
        var converter = new HookTimestampConverter(0, AnchorUtc);

        Assert.Equal(AnchorUtc.UtcDateTime, converter.Convert(0));
        _ = converter.Convert(uint.MaxValue);
        DateTime firstWrap = converter.Convert(0);
        _ = converter.Convert(uint.MaxValue);
        DateTime secondWrap = converter.Convert(0);

        Assert.Equal(AnchorUtc.AddMilliseconds(4_294_967_296L).UtcDateTime, firstWrap);
        Assert.Equal(AnchorUtc.AddMilliseconds(8_589_934_592L).UtcDateTime, secondWrap);
    }

    [Theory]
    [InlineData(4_294_968_306L, 4_294_967_301L)]
    [InlineData(8_589_935_602L, 8_589_934_597L)]
    public void ObservationUptimeDisambiguatesWholeCounterCyclesWithoutInput(long observationUptime, long expectedOffset)
    {
        var converter = new HookTimestampConverter(1_000, AnchorUtc);

        DateTime first = converter.Convert(1_005, 1_010);
        DateTime afterIdle = converter.Convert(1_005, observationUptime);

        Assert.Equal(AnchorUtc.AddMilliseconds(5).UtcDateTime, first);
        Assert.Equal(AnchorUtc.AddMilliseconds(expectedOffset).UtcDateTime, afterIdle);
    }

    [Fact]
    public void DelayedConsumerObservationDoesNotReplaceTheHookCaptureTime()
    {
        var converter = new HookTimestampConverter(1_000, AnchorUtc);

        DateTime first = converter.Convert(1_010, 100_000);
        DateTime second = converter.Convert(1_020, 200_000);

        Assert.Equal(AnchorUtc.AddMilliseconds(10).UtcDateTime, first);
        Assert.Equal(AnchorUtc.AddMilliseconds(20).UtcDateTime, second);
        Assert.Equal(10, (second - first).TotalMilliseconds);
    }

    [Fact]
    public void ObservedSampleOneMillisecondAheadDoesNotJumpBackAWholeCounterCycle()
    {
        var converter = new HookTimestampConverter(1_000, AnchorUtc);

        DateTime slightlyAhead = converter.Convert(1_001, 1_000);
        DateTime next = converter.Convert(1_002, 1_002);

        Assert.Equal(AnchorUtc.AddMilliseconds(1).UtcDateTime, slightlyAhead);
        Assert.Equal(AnchorUtc.AddMilliseconds(2).UtcDateTime, next);
    }

    [Fact]
    public void ObservedWrapRetainsAnEventImmediatelyBeforeTheStartupAnchor()
    {
        var converter = new HookTimestampConverter(4_294_967_298L, AnchorUtc);

        DateTime beforeAnchor = converter.Convert(uint.MaxValue - 1, 4_294_967_301L);
        DateTime afterWrap = converter.Convert(5, 4_294_967_302L);

        Assert.Equal(AnchorUtc.AddMilliseconds(-4).UtcDateTime, beforeAnchor);
        Assert.Equal(AnchorUtc.AddMilliseconds(3).UtcDateTime, afterWrap);
    }
}
