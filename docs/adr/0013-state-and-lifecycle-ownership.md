# ADR 0013: Commit state changes in order and give lifecycle work one owner

- Status: Accepted
- Date: 2026-11-06
- Task: T1.1 / F-013

## Context

Audit scenarios R1, R2, R4 and R9 reproduced duplicate recording startup,
duplicate subscriptions, lost recorded events, and active playback whose state
had incorrectly become Idle. State callbacks could also observe transitions in
the reverse order under concurrency or reentrancy. Calling subscribers under a
reentrant monitor would not solve the two-subscriber ordering problem.

Test-only commit `f3468dc` introduced deterministic regressions before fixes.
[Red CI 42186380050](https://github.com/EnesGoker/Makredo/actions/runs/42186380050)
ran unchanged production code: eight failures among the ten new cases (two
explicitly permitted same-state cases passed). No sleeps establish race timing;
TaskCompletionSource gates and a barrier establish the required interleavings.

## Decision

### State commits and notifications

`TryTransitionTo` accepts a same-state request only when the supplied allowed
current states explicitly contain that state. An empty list does not permit a
same-state request. Same-state no-ops do not emit another notification;
`SetState` retains its existing same-state no-op behavior.

Commit the state and capture its subscriber snapshot into a FIFO queue under the
state lock. One publisher drains snapshots outside that lock. Reentrant or
concurrent transitions enqueue behind the current notification, so every
subscriber finishes the current notification before the next notification starts.

A transition's Boolean result describes its committed state change, not a
notification-completion barrier. When a publisher is already active, a call can
return before its notification is delivered. `CurrentState` is the latest
committed state and may be newer than the historical event argument. The UI
already requests a refresh of current state rather than interpreting the event
argument as a complete state snapshot.

Preserve normal multicast exception behavior within each notification: the first
throwing subscriber prevents later subscribers in that notification from running.
Retain the first exception, drain queued notifications, release publication
ownership, and rethrow that exception to the publishing caller. A subscriber
failure must not permanently strand the queue or replace the original failure
with a later queued subscriber's failure.

### Recording lifecycle

Reserve a shared completion task before publishing Recording or calling hooks.
Overlapping starts share startup completion/failure; an already active recording
is not replaced. Overlapping stops share one complete cleanup, including its
error result. Stop requested during startup waits for startup or its rollback
before checking the session and stopping hooks. Start requested during stop is
ignored rather than queued to begin another recording afterward.

The lifecycle lock protects task reservation/completion only. Hook calls, state
notifications and recording notifications execute outside it. Startup failures
release subscriptions and ownership; a state-publication failure before session
preparation does not erase a previous completed session. A failure while
publishing Stopping is collected while cleanup still finishes, then reported
through the shared stop task.

### Playback lifecycle

Reserve the cancellation source before publishing Playing. A duplicate Play
request returns without starting another run or entering the active owner's
cleanup. Only the owner may reset its resolver, cursor, resume signal and state.
Keep that reservation until final Idle/PlaybackStopped notification has finished,
so callbacks cannot start work that an older cleanup would subsequently overwrite.

Coalesce stop requests separately from playback completion. The stop task still
means that the stop transition/cancellation request has completed; it does not
wait for the entire Play task. This preserves callback usability. Callers that
need completed playback await the original Play task. Finishing playback waits
for an in-flight stop request before releasing/disposal of its cancellation
source, preventing a late stop from affecting a newer owner.

No held-input release, native hook-thread changes, emergency-stop policy or
playback scheduling algorithm is added by this decision. Those remain T1.2,
T1.3 and T1.6 work.

## Validation

Keep the original red regressions unchanged and add focused ownership,
publication-failure, rollback and reentry cases. All are portable Core tests with
fake input sources/adapters; no native input is sent on a developer's computer.

The Ubuntu CI job additionally runs `Category=Concurrency` in 100 independent
test invocations. Each invocation must report exactly 25 passing cases, with the
same test names, no missing/duplicate/skipped case and no test-run failure. TRX,
per-invocation logs and `summary.json` are uploaded as
`concurrency-repetitions-ubuntu-x64`. Update the expected case count intentionally
when adding another test to this category. Green run links and measured repeat
results are recorded in PR #9 after execution; a configured repeat is not proof
that it ran.
