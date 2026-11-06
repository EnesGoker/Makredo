# ADR 0014: Release playback-owned input before completion

- Status: Accepted
- Date: 2026-11-06
- Task: T1.2 (F-001)

## Context

A successful key or mouse-button down can outlive playback when the recording
has no matching up, or playback stops, is cancelled, faults, or the window closes.
Releasing every system key would affect input that this playback never pressed.
ADR-0013 also defines StopAsync as a coalesced stop request, rather than a wait
for the Play task to finish. Changing that contract could deadlock callbacks.

## Decision

- Keep an ordered set of unmatched successful downs per playback owner. Update
  it only after the adapter send completes successfully; a failed down adds
  nothing and a failed up retains its down. Repeated downs do not create duplicate
  held intervals. An up followed by another down creates a new, latest interval.
- Match the Windows adapter's injection identity: a positive scan code and its
  extended flag, otherwise the virtual-key code and its extended flag. Preserve
  the original key code, scan code and extended metadata in the release. Mouse
  ownership distinguishes left, right and middle buttons.
- Serialize tracking and release with the existing event execution gate.
  CompletePlaybackAsync waits for an outstanding stop request, then attempts
  releases in reverse press order with CancellationToken.None, before Idle and
  PlaybackStopped. Retain the original owner through the final notifications.
  A release failure is logged independently; remaining releases still run.
  A logging failure also cannot interrupt the remaining attempts. This is a
  best-effort release of owned input; OS rejection cannot guarantee a released
  hardware state. No blanket system-wide release or automatic retry is added.
- Add ReleaseEventAsync to the adapter contract. Its default implementation
  forwards an up event for portable adapters. The Windows override sends mouse
  ups directly, without SetCursorPos or a coordinate requirement, so a cursor
  move failure cannot block button release. Keyboard releases preserve the
  regular scan-code and extended-key path.
- Expand a logical double-click into four individual adapter sends in the
  playback service. Each accepted sub-send updates ownership, so a failure in
  the middle still releases a successfully pressed button. EventPlayed and the
  playback cursor still advance once per recorded logical event.
- Paused single-step input shares the active owner's tracker. Revalidate its
  owner and state after acquiring the gate; a queued step cannot inject after
  stop or completion. A standalone single step has its own tracker and releases
  at the end of that operation. Simulation sends neither downs nor releases.
- Implement IAsyncDisposable on the playback contract for terminal shutdown.
  DisposeAsync closes admission, requests Stop, and waits for owner completion
  and all admitted manual operations before disposing resources. Concurrent
  callers share that disposal task. Synchronous Dispose requires an inactive
  service; it cannot safely wait for asynchronous release.
- Window shutdown requests Stop unconditionally, including an already-Stopping
  owner. It awaits DisposeAsync before closing, after settings persistence has
  succeeded. A failed settings save therefore does not prematurely dispose a
  service in a window that remains open. StopAsync itself remains request-only.

## Validation and limits

The first test-only commit reproduces the defect with portable fake adapters
and a CI-only native R3 test. The native test runs on both Windows architectures,
observes injected LShift down/up through a real low-level hook, checks that the
OS first sees the held key and later reports it released, and asserts one
successful production up before its unconditional test-owned fallback cleanup.
The exact hosted Windows environment is checked at discovery and again before
native input. Developer machines never execute this native test.

Core tests cover all five exits, successful/failed sends, release failures,
reverse order, modifier identity, simulation, partial double-click failure,
queued paused steps and joined shutdown. The original red cases remain unchanged.
Six older event-count assertions now explicitly include their required cleanup
up, while retaining the original recorded-event order and cardinality.

Hook thread ownership (T1.3), physical emergency-stop behavior (T1.6), elevated
targets, locked desktops and hardware input remain outside this change. Injected
input is not evidence for a physical-key emergency stop.

## References

- [KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput)
  defines scan-code injection and the extended-key flag.
- [GetAsyncKeyState](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getasynckeystate)
  defines the high-bit held-state check and left/right modifier constants.
- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
  runs before asynchronous key state is updated; the native test waits for its
  message loop to become idle before inspecting OS state.
