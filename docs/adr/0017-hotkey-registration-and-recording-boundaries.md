# ADR 0017: Independent hotkeys and recording boundaries

- Status: Accepted
- Date: 2026-11-06
- Task: T1.5 (F-002, F-015)

## Decision

The default record toggle is Ctrl+Shift+Alt+R. Ctrl+Shift+Alt+P starts playback
when idle and stops an active recording or playback, including paused playback.
Play and Stop may share exactly the same binding; this alias is registered once.
A distinct custom Stop binding remains supported. Record cannot share either.
The toolbar retains its separate play/pause and stop buttons.
No bare function key is globally reserved by the new defaults. The F8/F9/F10
preset remains selectable. The old settings shortcut is retained only as JSON
metadata for compatibility and exact legacy comparison; it is neither registered,
handled locally, displayed as a shortcut, nor excluded from recordings.

Every active binding uses MOD_NOREPEAT and is registered independently. Failure
of one binding never unregisters a successful binding or stops subsequent attempts.
Failures are logged and displayed in a non-modal Turkish warning band, including
the binding, native error code and reason. Successful registrations still work.
A focused-window fallback checks each binding's actual registration status and
consumes key autorepeat without executing the command again.
Microsoft documents MOD_NOREPEAT and registration collisions in
[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).

## Recording

The low-level keyboard hook runs before WM_HOTKEY. A pure Core filter buffers
only a contiguous, unpublished modifier prefix. Matching record/play/stop chords
remove that prefix and the primary key, including repeats and subsequent releases.
An ordinary key, mouse button, wheel event or closed modifier tap commits the
pending modifiers. Mouse movement is recorded immediately while the modifiers
remain pending. This lets the recorder exclude a shortcut chord even if the
cursor moved before the primary key was pressed. When a click or scroll follows,
the pending modifiers are committed before that action.
Left/right virtual key codes, extended-key metadata and capture timestamps are
preserved. A modifier committed after a later move has zero playback delay.
The next delay is measured from the latest capture time already committed.

When a hotkey starts recording, the UI passes only that chord's currently held
keys. Their later releases are excluded. The keyboard consumer initializes its
modifier snapshot from native state so an already held chord and primary repeats
are recognized. Side-specific held modifiers prevent releasing one side from
clearing the other. This native read is on consumer initialization, never in the
hook callback. A fresh non-command key down clears stale startup suppression if
a release occurred before hook installation. Toolbar starts have no startup chord.
Queued events still drain before stop seals the session; lifecycle single-flight
ownership is unchanged. Hooks remain on their dedicated thread with a background
consumer. Emergency-stop policy remains T1.6.

## One-time settings migration

The data migration marker's hotkeyMigrationPending flag gates conversion.
All four legacy values (F8/F9/F10/F12, without modifiers) must match exactly.
Any customization preserves the settings file byte for byte. Both original
settings and marker are backed up locally with the .pre-t15.bak suffix before
mutation; existing backups are preserved. Each changed file is written atomically.
Settings are saved before clearing the flag, so interruption is safe to retry.
Unknown marker fields survive. A migration I/O failure is logged, preserves the
last successfully persisted settings and leaves pending work for the next load.
A missing or false flag never upgrades existing legacy bindings again.

## Validation and limits

Core tests exercise startup releases, stopping chords, repeated primary keys,
intervening user keys/mouse boundaries, modifier taps, side/extended metadata,
capture timing, independent registration and temporary-directory migration/retry.
Guarded hosted Windows tests reserve a conflicting binding in another window,
verify a second binding still fires, hold a shortcut across repeated SendInput
key downs, record a real start/type/stop sequence, and inspect the actual MainForm
warning band. They also save warning/settings PNGs. Native input requires the
hosted Windows CI guard and is never sent by developer-machine test runs.
The layout test now expects three active binding rows and four footer buttons.
Physical keyboard layouts, accessibility combinations and manual visual review
remain release-checkpoint work. No new package dependency is introduced.
