using Makredo.Application.Services;
using Makredo.Platform.Windows.Hooks;
using Makredo.Platform.Windows.Interop;
using Makredo.Windows.Tests.Playback;
using Xunit;
using Xunit.Abstractions;

namespace Makredo.Windows.Tests.Hooks;

[Collection("Native input")]
public sealed class HookThreadLifecycleTests(ITestOutputHelper output)
{
    [HostedWindowsInputFact]
    [Trait("Category", "NativeInput")]
    public async Task SharedHooksStopIndependentlyAndLastDisposeJoinsTheProducerThread()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        var owner = new WindowsHookThread(NullAppLogger.Instance);
        using var keyboard = new WindowsKeyboardHookSource(owner, NullAppLogger.Instance);
        using var mouse = new WindowsMouseHookSource(owner, NullAppLogger.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15), TimeProvider.System);
        try
        {
            await keyboard.StartAsync(deadline.Token);
            await mouse.StartAsync(deadline.Token);
            Assert.True(keyboard.IsRunning);
            Assert.True(mouse.IsRunning);
            Assert.True(owner.HasLiveThread);

            await keyboard.StopAsync(deadline.Token);
            Assert.False(keyboard.IsRunning);
            Assert.True(mouse.IsRunning);
            Assert.True(owner.HasLiveThread);
            await keyboard.DisposeAsync();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => keyboard.StartAsync(deadline.Token));

            mouse.Dispose();

            Assert.False(keyboard.IsRunning);
            Assert.False(mouse.IsRunning);
            Assert.False(owner.HasLiveThread);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => mouse.StartAsync(deadline.Token));
            output.WriteLine("Keyboard stopped while mouse remained active; synchronous final mouse disposal returned with both hooks removed and the actual producer thread no longer alive.");
        }
        finally
        {
            await keyboard.DisposeAsync();
            await mouse.DisposeAsync();
        }
    }
    [HostedWindowsInputFact]
    [Trait("Category", "NativeInput")]
    public async Task UnexpectedOwnerExitDrainsItsConsumerBeforeAnyDisposalRequest()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        var owner = new WindowsHookThread(NullAppLogger.Instance);
        using var keyboard = new WindowsKeyboardHookSource(owner, NullAppLogger.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15), TimeProvider.System);
        try
        {
            await keyboard.StartAsync(deadline.Token);
            Task consumer = owner.ConsumerCompletion;
            uint threadId = owner.NativeThreadId;
            Assert.NotEqual(0u, threadId);
            Assert.False(consumer.IsCompleted);
            Assert.True(NativeMethods.PostThreadMessage(threadId, 0x0012, UIntPtr.Zero, IntPtr.Zero));

            await consumer.WaitAsync(deadline.Token);
            await keyboard.DisposeAsync();

            Assert.False(keyboard.IsRunning);
            Assert.False(owner.HasLiveThread);
            output.WriteLine("An externally posted WM_QUIT completed the consumer without a stop request; disposal then joined actual producer exit.");
        }
        finally
        {
            await keyboard.DisposeAsync();
        }
    }

}
