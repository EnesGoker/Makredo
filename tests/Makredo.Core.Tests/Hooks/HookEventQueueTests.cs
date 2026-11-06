using System.Threading.Channels;
using Makredo.Application.Hooks;
using Xunit;

namespace Makredo.Core.Tests.Hooks;

public sealed class HookEventQueueTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task BlockedConsumerPreservesEveryMixedInputInProducerOrder()
    {
        const int sampleCount = 10_000;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEvent(false);
        List<RawSample> observed = [];
        var queue = new HookEventPump<RawSample>(sample =>
        {
            if (sample.Sequence == 0)
            {
                entered.TrySetResult();
                Assert.True(release.WaitOne(Deadline));
            }
            observed.Add(sample);
        });
        var source = new FakeRawSource(queue.Writer);

        try
        {
            Assert.True(source.Emit(new RawSample(0, Keyboard: true, Tick: 0)));
            await WithinDeadline(entered.Task);
            for (int index = 1; index < sampleCount; index++)
            {
                Assert.True(source.Emit(new RawSample(index, index % 2 == 0, (uint)index)));
            }

            Task firstStop = queue.CompleteAsync();
            Task overlappingStop = queue.CompleteAsync();
            Assert.False(firstStop.IsCompleted);
            Assert.False(overlappingStop.IsCompleted);
            Assert.False(source.Emit(new RawSample(sampleCount, Keyboard: true, Tick: sampleCount)));
            release.Set();
            await WithinDeadline(Task.WhenAll(firstStop, overlappingStop));

            Assert.Equal(Enumerable.Range(0, sampleCount).Select(index => new RawSample(index, index % 2 == 0, (uint)index)), observed);
        }
        finally
        {
            release.Set();
            await WithinDeadline(queue.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task ConsumerDoesNotRunInlineOrCaptureTheProducerSynchronizationContext()
    {
        using var processed = new ManualResetEvent(false);
        var producerContext = new RejectingSynchronizationContext();
        SynchronizationContext? previousContext = SynchronizationContext.Current;
        SynchronizationContext? observedContext = producerContext;
        int producerThread = Environment.CurrentManagedThreadId;
        int consumerThread = producerThread;
        bool insideConsumer = false;
        HookEventPump<int>? queue = null;

        try
        {
            SynchronizationContext.SetSynchronizationContext(producerContext);
            queue = new HookEventPump<int>(_ =>
            {
                consumerThread = Environment.CurrentManagedThreadId;
                observedContext = SynchronizationContext.Current;
                insideConsumer = queue!.IsConsumerContext;
                processed.Set();
            });

            Assert.False(queue.IsConsumerContext);
            Assert.True(queue.Writer.TryWrite(1));
            // Keep this thread occupied until processing finishes; this models an unpumped UI context.
            Assert.True(processed.WaitOne(Deadline));
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        Assert.NotNull(queue);
        await WithinDeadline(queue.CompleteAsync());
        await queue.DisposeAsync();
        Assert.NotEqual(producerThread, consumerThread);
        Assert.Null(observedContext);
        Assert.True(insideConsumer);
        Assert.False(queue.IsConsumerContext);
    }

    [Fact]
    public async Task ProcessingAndErrorHandlerFailuresDoNotPreventLaterSamples()
    {
        List<int> observed = [];
        List<Exception> errors = [];
        var expected = new IOException("The first sample processor failed.");
        await using var queue = new HookEventPump<int>(sample =>
        {
            observed.Add(sample);
            if (sample == 1) { throw expected; }
        }, exception =>
        {
            errors.Add(exception);
            throw new InvalidOperationException("The error handler also failed.");
        });

        Assert.True(queue.Writer.TryWrite(1));
        Assert.True(queue.Writer.TryWrite(2));
        await WithinDeadline(queue.CompleteAsync());

        Assert.Equal<int>([1, 2], observed);
        Assert.Same(expected, Assert.Single(errors));
        Assert.True(queue.Completion.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task DisposeWaitsForAcceptedSamplesAndIsSafeToRepeat()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEvent(false);
        List<int> observed = [];
        var queue = new HookEventPump<int>(sample =>
        {
            if (sample == 1)
            {
                entered.TrySetResult();
                Assert.True(release.WaitOne(Deadline));
            }
            observed.Add(sample);
        });

        try
        {
            Assert.True(queue.Writer.TryWrite(1));
            await WithinDeadline(entered.Task);
            Assert.True(queue.Writer.TryWrite(2));
            Task firstDispose = queue.DisposeAsync().AsTask();
            Task overlappingDispose = queue.DisposeAsync().AsTask();
            Assert.False(firstDispose.IsCompleted);
            Assert.False(overlappingDispose.IsCompleted);
            Assert.False(queue.Writer.TryWrite(3));
            release.Set();
            await WithinDeadline(Task.WhenAll(firstDispose, overlappingDispose));
            await queue.DisposeAsync();

            Assert.Equal<int>([1, 2], observed);
            Assert.True(queue.Completion.IsCompletedSuccessfully);
            await Assert.ThrowsAsync<ChannelClosedException>(() => queue.Writer.WriteAsync(4).AsTask());
        }
        finally
        {
            release.Set();
            await WithinDeadline(queue.DisposeAsync().AsTask());
        }
    }

    [Fact]
    public async Task CompletingFromTheConsumerRejectsAWaitOnItsOwnDrain()
    {
        var attempted = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        HookEventPump<int>? queue = null;
        queue = new HookEventPump<int>(_ => attempted.TrySetResult(queue!.CompleteAsync()));
        await using var owner = queue;

        Assert.True(queue.Writer.TryWrite(1));
        Task selfCompletion = await attempted.Task.WaitAsync(Deadline, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WithinDeadline(selfCompletion));
        await WithinDeadline(queue.CompleteAsync());
    }

    [Fact]
    public async Task ProducerFailureIsReportedOnlyAfterAcceptedSamplesDrain()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEvent(false);
        List<int> observed = [];
        var failure = new IOException("The fake producer failed after accepting input.");
        var queue = new HookEventPump<int>(sample =>
        {
            if (sample == 1)
            {
                entered.TrySetResult();
                Assert.True(release.WaitOne(Deadline));
            }
            observed.Add(sample);
        });

        try
        {
            Assert.True(queue.Writer.TryWrite(1));
            await WithinDeadline(entered.Task);
            Assert.True(queue.Writer.TryWrite(2));
            Assert.True(queue.Writer.TryComplete(failure));
            Assert.False(queue.Completion.IsCompleted);
        }
        finally
        {
            release.Set();
        }

        Exception observedFailure = await Assert.ThrowsAsync<IOException>(() => WithinDeadline(queue.Completion));
        Assert.Same(failure, observedFailure);
        Assert.Equal<int>([1, 2], observed);
        await Assert.ThrowsAsync<IOException>(() => queue.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task EmptyQueueCompletesWithoutWaitingForAnotherInput()
    {
        int processed = 0;
        await using var queue = new HookEventPump<int>(_ => Interlocked.Increment(ref processed));

        await WithinDeadline(queue.CompleteAsync());

        Assert.True(queue.Completion.IsCompletedSuccessfully);
        Assert.False(queue.Writer.TryWrite(1));
        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task ConcurrentCompletionPreservesExactlyTheInputsAcceptedByOneProducer()
    {
        const int iterations = 25;
        const int attempts = 2_000;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            List<int> accepted = [];
            List<int> rejected = [];
            List<int> observed = [];
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var race = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var queue = new HookEventPump<int>(observed.Add);
            Task producer = Task.Run(async () =>
            {
                Assert.True(queue.Writer.TryWrite(0));
                accepted.Add(0);
                ready.TrySetResult();
                await race.Task;
                for (int value = 1; value < attempts; value++)
                {
                    if (queue.Writer.TryWrite(value)) { accepted.Add(value); }
                    else { rejected.Add(value); }
                }
            });
            Task completion = Task.CompletedTask;
            try
            {
                await WithinDeadline(ready.Task);
                completion = Task.Run(async () =>
                {
                    await race.Task;
                    await queue.CompleteAsync();
                });
                race.TrySetResult();
                await WithinDeadline(Task.WhenAll(producer, completion));

                Assert.Equal(accepted, observed);
                Assert.Empty(observed.Intersect(rejected));
                Assert.Equal(attempts, accepted.Count + rejected.Count);
                Assert.False(queue.Writer.TryWrite(attempts));
            }
            finally
            {
                race.TrySetResult();
                await WithinDeadline(Task.WhenAll(producer, queue.DisposeAsync().AsTask(), completion));
            }
        }
    }

    [Fact]
    public async Task AlternatingEnqueueAndEmptyConsumerWakeDoesNotLoseNotifications()
    {
        const int sampleCount = 1_000;
        TaskCompletionSource<int>[] acknowledgements = Enumerable.Range(0, sampleCount)
            .Select(_ => new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        List<int> observed = [];
        var queue = new HookEventPump<int>(sample =>
        {
            observed.Add(sample);
            acknowledgements[sample].TrySetResult(sample);
        });

        try
        {
            for (int index = 0; index < sampleCount; index++)
            {
                Assert.True(queue.Writer.TryWrite(index));
                Assert.Equal(index, await acknowledgements[index].Task.WaitAsync(Deadline, TimeProvider.System));
            }
            await WithinDeadline(queue.CompleteAsync());

            Assert.Equal(Enumerable.Range(0, sampleCount), observed);
        }
        finally
        {
            await WithinDeadline(queue.DisposeAsync().AsTask());
        }
    }

    private static Task WithinDeadline(Task task) => task.WaitAsync(Deadline, TimeProvider.System);

    private readonly record struct RawSample(int Sequence, bool Keyboard, uint Tick);

    private sealed class FakeRawSource(ChannelWriter<RawSample> writer)
    {
        internal bool Emit(RawSample sample) => writer.TryWrite(sample);
    }

    private sealed class RejectingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            throw new InvalidOperationException("The hook consumer must not post work to the producer context.");
    }
}
