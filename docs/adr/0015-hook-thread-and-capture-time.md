# ADR 0015: Own hooks on a dedicated thread and preserve capture time

- Status: Accepted
- Date: 2026-11-06
- Task: T1.3 (F-008)

## Context

Low-level hooks run on their installing thread. Installing them from WinForms
couples input delivery to UI work. Running listeners, locks or log writes inside
a hook also consumes LowLevelHooksTimeout; Windows can silently remove an
overdue hook. Moving installation alone does not remove listener-induced stalls.

## Decision

### One native producer, one background consumer

The composition root creates keyboard and mouse facades sharing one owner.
That owner creates a dedicated background thread, forces message-queue creation
with PeekMessage, installs the requested hooks there and pumps GetMessage.
Both devices feed one FIFO channel and one consumer, preserving their combined
capture order. Standalone facade constructors create independent owners for
isolated use. The global hotkey service and its NativeWindow are unchanged.

Callbacks only copy native values, call ChannelWriter.TryWrite and chain to
CallNextHookEx. They contain no application lock, event invocation, logging,
clock conversion or waiting. Readonly delegate fields remain rooted through
unhook and actual producer exit.

Stock .NET unbounded channel writers use an internal monitor even with
SingleWriter selected. HookEventPump therefore uses a small unbounded SPSC
Channel implementation: linked nodes, volatile publication and atomic state /
waiter exchange. Its writer has no locks, capacity waits, spins or inline
consumer continuations. There is one native producer and one reader; terminal
completion occurs after that producer stops writing. Accepted events are never
dropped to make space. The tests also exercise an accepted-write/completion race.

The consumer starts with Task.Run and uses ConfigureAwait(false). It converts
samples and invokes listeners away from the UI and native producer. Subscriber
and logging failures are isolated so later listeners and samples still run.
This leaves a UI-independent delivery path for the later T1.6 emergency-stop
work; that behavior itself is not added here.

### Capture-time timestamps

Native time is an unsigned 32-bit millisecond uptime counter and wraps every
2^32 milliseconds (about 49.7 days). A fixed UTC / 64-bit uptime anchor converts
that native value to UTC. Ordered conversion uses modular differences across
wrap; the native consumer additionally uses current 64-bit uptime to select the
nearest era after whole idle cycles. A one-millisecond future observation does
not incorrectly jump backward an entire cycle. UTC clock changes and ordinary
consumer delays do not replace the event's native timestamp.

Era inference from a uint value assumes a queued sample is less than half a
counter cycle (about 24.8 days) from its 64-bit observation. A uint alone cannot
identify the era of an arbitrarily old backlog. There is no arbitrary queue
capacity or silent-drop fallback: a slow consumer can grow memory usage, and
allocation failure / process failure remains a resource limit.

Keyboard metadata gains an optional captured timestamp. An additive timestamped
mouse-source capability carries the corresponding mouse payload while retaining
the existing mouse event contract. The recorder subscribes to exactly one mouse
path, uses capture time for event timestamps and delays, and uses TimeProvider
for legacy fake sources lacking capture time.

### Stop and disposal

Lifecycle operations are serialized outside callbacks. Owner-thread commands
install or remove a device hook. Stopping one of two active devices unhooks it
on the native owner and appends a consumer barrier, so its accepted tail drains
while the other device continues running.

The last stop posts WM_QUIT with PostThreadMessage. The producer removes hooks
in its finally block and completes the writer, including unexpected exits.
Shutdown joins the actual Thread exit and awaits consumer drain before returning.
A zero-running session that is still publishing its exit is also joined and
drained. Native failures are surfaced rather than reported as successful cleanup.

Async disposal permanently closes the facade. Synchronous IDisposable waits for
the same shutdown, whose continuations never depend on the UI. A consumer cannot
synchronously stop/dispose its own pipeline and wait for its own drain; such
reentry is rejected explicitly. Ordinary listeners must request lifecycle work
from another context rather than synchronously waiting inside delivery.

The recorder keeps subscriptions and accepts the owned session's tail while
Stopping. Only after both sources stop/drain does it unsubscribe, seal the
session and publish Idle. This prevents queued events from disappearing at stop.

### Timeout risk

UI work, subscriber work, logging and application locks no longer execute on the
native callback path or delay its installing thread. The application's causes
of LowLevelHooksTimeout exposure are therefore removed, and the two-second UI
block integration tests verify this separation for both input devices.

This is not an absolute guarantee against OS scheduling pauses, GC, allocation
pressure or delays in another hook reached through CallNextHookEx. Windows can
still remove a hook if those external/runtime conditions exceed its timeout.
No speculative watchdog/reinstallation policy or OS setting change is added.

## Validation

Core tests use fake production and signals to check mixed-device FIFO delivery,
no loss under a blocked consumer, completion races, drain/disposal, error
isolation, wake-up races, tick wrapping, long idle eras and recorder capture-time
delays. A stop regression emits queued keyboard/mouse tails while Stopping.

Guarded Windows tests first verify a real-input warm-up, then use a non-alertable
native SignalObjectAndWait to block the installing STA UI thread for two seconds.
It signals the background sender from inside that native wait. This matters
because a managed STA wait can continue pumping hook messages. The tests require
source processing off the UI, while the UI is still blocked, within one second.
TRX includes measured latency and thread IDs. A separate shared-owner test checks
per-device stop, final active disposal, actual thread exit and rejected restart.
Real input remains restricted to the existing hosted-Windows CI fence.

T1.4 injected-input filtering and T1.6 emergency-stop behavior are unchanged.

## References

- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
  documents installing-thread delivery, timeout removal and the message loop.
- [PostThreadMessage](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postthreadmessagew)
  documents message-queue readiness and non-window thread messages.
- [.NET 10 unbounded channel implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Threading.Channels/src/System/Threading/Channels/SingleConsumerUnboundedChannel.cs)
  shows the monitor taken by the stock writer.
