# ADR 0019: Single instance per Windows session

- Status: Accepted
- Date: 2026-11-07
- Task: T1.8 (F-014)

## Ownership

The startup thread acquires `Local\Makredo.SingleInstance` before opening
the data directory, attempting migration or creating AppCompositionRoot.
It owns the mutex until application and composition disposal have finished.
An abandoned mutex transfers ownership to the thread that acquires it, so
startup continues after a previous process crashes. The Local namespace
allows an independent instance in another Windows session.

## Activation

The owner creates a message-only window before migration and sets a
session-local readiness event once the endpoint exists. A duplicate
waits at most three seconds for readiness. It resolves the endpoint's PID,
calls AllowSetForegroundWindow for that PID and posts the registered
activation message. The duplicate then exits successfully without opening
the app's files.

The owner's UI thread applies the request once the form is shown. It shows
a hidden form, restores a minimized form and requests activation without
issuing recording or playback commands. Windows decides whether to grant
foreground permission. The restore request is posted even if the grant
is denied.

See Microsoft's [kernel object namespaces](https://learn.microsoft.com/en-us/windows/win32/termserv/kernel-object-namespaces),
[message-only window lookup](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-findwindowexw)
and [foreground permission contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow).
