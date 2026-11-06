using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Makredo.Application.Hooks;

/// <summary>
/// Drains one native producer's events on a background consumer. Stop the producer
/// before completing the writer; completion drains every accepted event.
/// </summary>
public sealed class HookEventPump<T> : IAsyncDisposable
{
    private static readonly AsyncLocal<HookEventPump<T>?> Consumer = new();
    private readonly SpscChannel _channel = new();
    private readonly Action<T> _process;
    private readonly Action<Exception>? _onError;

    public HookEventPump(Action<T> process, Action<Exception>? onError = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        _process = process;
        _onError = onError;
        Completion = Task.Run(ConsumeAsync);
    }

    public ChannelWriter<T> Writer => _channel.Writer;
    public Task Completion { get; }
    public bool IsConsumerContext => ReferenceEquals(Consumer.Value, this);

    public Task CompleteAsync()
    {
        if (IsConsumerContext)
        {
            return Task.FromException(new InvalidOperationException("A hook consumer cannot wait for its own completion."));
        }
        Writer.TryComplete();
        return Completion;
    }

    public ValueTask DisposeAsync() => new(CompleteAsync());

    private async Task ConsumeAsync()
    {
        HookEventPump<T>? previous = Consumer.Value;
        Consumer.Value = this;
        try
        {
            await foreach (T item in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    _process(item);
                }
                catch (Exception exception)
                {
                    try { _onError?.Invoke(exception); }
                    catch (Exception) { }
                }
            }
        }
        finally
        {
            try { await _channel.Reader.Completion.ConfigureAwait(false); }
            finally { Consumer.Value = previous; }
        }
    }

    // The platform has exactly one callback thread. The linked-node writer does
    // not enter the monitor used internally by stock unbounded channels.
    private sealed class SpscChannel : Channel<T>
    {
        private const int Writing = 1;
        private const int Closed = 2;
        private Node _head = new(default!);
        private Node _tail;
        private int _producerState;
        private EndOfWriting? _end;
        private TaskCompletionSource<bool>? _waiter;
        private readonly TaskCompletionSource _readCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal SpscChannel()
        {
            _tail = _head;
            Writer = new QueueWriter(this);
            Reader = new QueueReader(this);
        }

        private bool HasItems
        {
            get
            {
                Node head = Volatile.Read(ref _head);
                return Volatile.Read(ref head.Next) is not null;
            }
        }
        private bool IsFinal => Volatile.Read(ref _end) is not null
            && (Volatile.Read(ref _producerState) & Writing) == 0;

        private bool TryWrite(T item)
        {
            int observed = Interlocked.CompareExchange(ref _producerState, Writing, 0);
            if ((observed & Closed) != 0 || Volatile.Read(ref _end) is not null)
            {
                if (observed == 0)
                {
                    Interlocked.Add(ref _producerState, -Writing);
                    SignalReader();
                }
                return false;
            }
            if (observed != 0)
            {
                throw new InvalidOperationException("The hook queue requires a single producer.");
            }

            try
            {
                var next = new Node(item);
                Volatile.Write(ref _tail.Next, next);
                _tail = next;
                SignalReader();
                return true;
            }
            finally
            {
                Interlocked.Add(ref _producerState, -Writing);
                SignalReader();
            }
        }

        private bool TryComplete(Exception? error)
        {
            if (Interlocked.CompareExchange(ref _end, new EndOfWriting(error), null) is not null)
            {
                return false;
            }
            Interlocked.Or(ref _producerState, Closed);
            SignalReader();
            return true;
        }

        private bool TryRead([MaybeNullWhen(false)] out T item)
        {
            Node head = Volatile.Read(ref _head);
            Node? next = Volatile.Read(ref head.Next);
            if (next is null)
            {
                item = default!;
                FinishReadingIfEmpty();
                return false;
            }
            item = next.Item;
            next.Item = default!;
            Volatile.Write(ref _head, next);
            FinishReadingIfEmpty();
            return true;
        }

        private ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) { return ValueTask.FromCanceled<bool>(cancellationToken); }
            if (HasItems) { return new ValueTask<bool>(true); }
            if (IsFinal) { return ReadFinished(); }

            var candidate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> waiter = Interlocked.CompareExchange(ref _waiter, candidate, null) ?? candidate;
            // Recheck after publishing the waiter so a producer notification cannot be lost.
            SignalReader();
            return new ValueTask<bool>(WaitForSignalAsync(waiter.Task, cancellationToken));
        }

        private async Task<bool> WaitForSignalAsync(Task<bool> notification, CancellationToken cancellationToken)
        {
            try
            {
                return await notification.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Only the reader completes its task; the callback only claims and signals a waiter.
                FinishReadingIfEmpty();
            }
        }

        private ValueTask<bool> ReadFinished()
        {
            FinishReadingIfEmpty();
            Exception? error = Volatile.Read(ref _end)?.Error;
            return error is null ? new ValueTask<bool>(false) : ValueTask.FromException<bool>(error);
        }

        private void SignalReader()
        {
            bool available = HasItems;
            if (!available && !IsFinal) { return; }
            TaskCompletionSource<bool>? waiter = Interlocked.Exchange(ref _waiter, null);
            if (waiter is not null)
            {
                Exception? error = available ? null : Volatile.Read(ref _end)?.Error;
                if (error is not null) { waiter.TrySetException(error); }
                else { waiter.TrySetResult(available); }
            }
        }

        private void FinishReadingIfEmpty()
        {
            if (!IsFinal || HasItems) { return; }
            Exception? error = Volatile.Read(ref _end)?.Error;
            if (error is not null) { _readCompletion.TrySetException(error); }
            else { _readCompletion.TrySetResult(); }
        }

        private sealed class Node(T item)
        {
            internal T Item = item;
            internal Node? Next;
        }

        private sealed record EndOfWriting(Exception? Error);

        private sealed class QueueWriter(SpscChannel owner) : ChannelWriter<T>
        {
            public override bool TryWrite(T item) => owner.TryWrite(item);
            public override bool TryComplete(Exception? error = null) => owner.TryComplete(error);
            public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested) { return ValueTask.FromCanceled<bool>(cancellationToken); }
                EndOfWriting? end = Volatile.Read(ref owner._end);
                return end?.Error is { } error
                    ? ValueTask.FromException<bool>(error)
                    : new ValueTask<bool>(end is null);
            }
        }

        private sealed class QueueReader(SpscChannel owner) : ChannelReader<T>
        {
            public override Task Completion => owner._readCompletion.Task;
            public override bool TryRead([MaybeNullWhen(false)] out T item) => owner.TryRead(out item);
            public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default) => owner.WaitToReadAsync(cancellationToken);
        }
    }
}
