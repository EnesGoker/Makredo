# ADR 0004: Emergency stop from the keyboard hook

- Status: Accepted
- Date: 2026-11-07
- Task: T1.6 (F-002)

## Input policy

While a playback owns the application state, the existing keyboard hook
consumer requests stop on key-down for Escape (0x1B), Pause (0x13) or
VK_CANCEL (0x03, the Ctrl+Pause path). Window focus and modifier state are
irrelevant to the match. The consumer ignores the playback signature from
[ADR 0016](0016-injected-input-recording-policy.md) regardless of the injected
flag. Every matching unmarked press can stop playback, including input from an
on-screen keyboard. The emergency subscription belongs to playback, so Escape
remains an ordinary recordable key during recording.

The hook passes each key down the chain. The target application also receives
Escape and may, for example, close a dialog. We accept that effect to preserve
the target's keyboard behavior and keep the hook callback limited to publishing
samples. RegisterHotKey would reserve the key and bind it to one modifier
combination. See Microsoft's [virtual-key codes](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes)
and [low-level keyboard hook contract](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

## Playback ownership

The playback owner starts the shared keyboard hook before admitting playback
input. This includes input from paused manual navigation. A hook startup
failure aborts playback before any input is sent. The consumer calls the
existing request-only StopAsync path directly. Cancellation interrupts the
initial delay, event delays and paused waits without waiting for the UI.
Playback cleanup runs independently of the initiating synchronization context.
The form posts playback notifications to its UI thread.

Stop and admission of each adapter send share the playback ownership lock.
The Windows adapter submits its native input synchronously inside that
admission. A send already admitted can finish. Subsequent macro sends,
including the remaining parts of a double-click, are blocked. The existing
held-input cleanup releases inputs accepted by playback before Idle is
published. The owner removes its subscription and drains the hook after
cleanup, outside the consumer callback.

## Verification

Core tests use a fake keyboard source for the key policy, recording, paused
playback and cancellation of long waits. Hosted Windows tests use actual
hooks and SendInput, check both marked and unmarked Escape, and verify
playback-owned release with the initiating STA blocked. The physical path
requires the K1 test on hardware under [ADR 0003](0003-test-and-ci-strategy.md).
