# ADR 0018: Session document and recovery

- Status: Accepted
- Date: 2026-11-07
- Task: T1.7 (F-003)

## Document ownership

SessionDocument holds the active macro, its file path and its dirty state.
A recording, event edit, optimization or rename changes the document.
Saving or loading establishes a clean document. A recovered session is an
unsaved document with no associated path. Playback only reads the document.

Closing, starting a recording, loading and clearing share a single decision
path. Cancel leaves the document and recovery intact. Save writes the current
file or opens a file picker for a new session. A canceled picker or failed
save prevents the pending action. Discard removes recovery while keeping the
document in memory until the action actually replaces or closes it.
Normal closing waits for recording finalization and playback cleanup before
asking. Closing subscriptions are removed after the user has agreed to leave.

Library selection is separate from loading. Double-click or Enter loads the
selected macro. Optimization of the open macro uses its current events and
leaves the result in the dirty document until saved. Optimization of another
library file remains a library operation. A library rename updates the file
and, if open, updates the document and marks it dirty.

## Recovery

A completed recording and subsequent document edits checkpoint the session
as `session-recovery.json` in the Makredo data directory. The existing JSON
macro format and atomic replacement logic are reused. Only the committed file
is offered at startup. Invalid files are logged and skipped. Accepting recovery
keeps the checkpoint until Save or Discard. Declining it deletes the file.
Exporting a report leaves the document dirty because the report cannot be
loaded as a macro.

Windows checks the synchronous result of its shutdown query. MainForm writes
the current dirty session, or a snapshot of already captured recording events,
before returning that result. The snapshot leaves the live recorder buffers
untouched in case Windows cancels shutdown. No modal prompt is shown. Recovery
writes stage in the same directory, and a commit version prevents an older
write from replacing a newer shutdown checkpoint or recreating a deleted file.
See [WM_QUERYENDSESSION](https://learn.microsoft.com/en-us/windows/win32/shutdown/wm-queryendsession).
