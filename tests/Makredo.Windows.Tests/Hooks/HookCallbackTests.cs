using Makredo.Application.Abstractions;
using Makredo.Platform.Windows.Hooks;
using Makredo.Windows.Tests.Support;
using Xunit;

namespace Makredo.Windows.Tests;

public sealed class HookCallbackTests
{
    [Fact(DisplayName = "WindowsKeyboardHookSource isolates subscriber exceptions during consumption")]
    public Task WindowsKeyboardHookSource_SwallowsSubscriberExceptionsInCallback()
    {
        var logger = new RecordingTestLogger();
        using var source = new WindowsKeyboardHookSource(logger);
        bool laterSubscriberReceived = false;
        source.KeyActivityReceived += _ => throw new InvalidOperationException("Keyboard listener failed.");
        source.KeyActivityReceived += _ => laterSubscriberReceived = true;

        source.ProcessSample(new HookSample(HookKind.Keyboard, 0x0100, 0x41, 0x1E, 0, 100, 0, 0, 0,
            new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc)));

        Assert.True(laterSubscriberReceived);
        Assert.Contains(logger.Entries, entry => entry.Source == nameof(WindowsKeyboardHookSource)
            && entry.Message.Contains("dinleyicilere iletilirken", StringComparison.Ordinal));
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "WindowsMouseHookSource isolates subscriber exceptions during consumption")]
    public Task WindowsMouseHookSource_SwallowsSubscriberExceptionsInCallback()
    {
        var logger = new RecordingTestLogger();
        using var source = new WindowsMouseHookSource(logger);
        bool laterSubscriberReceived = false;
        source.MouseActivityReceived += (_, _, _, _) => throw new InvalidOperationException("Mouse listener failed.");
        source.TimestampedMouseActivityReceived += _ => throw new InvalidOperationException("Timestamped mouse listener failed.");
        source.MouseActivityReceived += (_, _, _, _) => laterSubscriberReceived = true;

        source.ProcessSample(new HookSample(HookKind.Mouse, 0x0200, 0, 0, 0, 100, 320, 240, 0,
            new DateTime(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc)));

        Assert.True(laterSubscriberReceived);
        Assert.Contains(logger.Entries, entry => entry.Source == nameof(WindowsMouseHookSource)
            && entry.Message.Contains("dinleyicilere iletilirken", StringComparison.Ordinal));
        return Task.CompletedTask;
    }
}
