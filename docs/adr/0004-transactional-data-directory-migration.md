# ADR 0004: Publish migrated data only after a complete copy

- Status: Accepted
- Date: 2026-11-06

## Context

The renamed application must discover existing user settings and macros without
modifying the legacy directory or replacing an existing Makredo directory. A
partially copied destination must not look like a completed migration on the
next launch. Migration tests must execute on macOS before the Core test project
is introduced in T0.6.

## Decision

Implement the migration in the portable Infrastructure project. The WinForms
composition root supplies the legacy path using `LegacyStoragePaths.DirectoryName`
and runs migration before constructing any logger or store. Keep synchronous,
cancellation-aware startup so the existing WinForms STA entry point is preserved.
`AppStoragePaths.CreateDefault()` resolves the Makredo directory without writing it.

Copy only `playback-settings.json`, `hotkey-settings.json`, `library-state.json`,
and the recursive `macros/` tree. Exclude legacy logs and unrelated root files.
Read the source without modifying or deleting it. Reject reparse points in the
selected source tree rather than following links outside that tree.

Build a complete copy in a uniquely named sibling staging directory. After all
files have been copied, write `.legacy-data-migration.json` with schema version
`1` and `hotkeyMigrationPending: true`. Publish the staged directory with
`Directory.Move`; never merge into or overwrite an existing destination. Shortcut
settings remain byte-for-byte unchanged until T1.5.

On failure or cancellation, clean up only this attempt's staging directory on a
best-effort basis and propagate the failure. Startup must not continue with an
empty final directory. Abandoned staging directories are never treated as the
final destination, and other attempts' staging directories are not deleted.

Add a dedicated `net10.0` migration test project using the existing xUnit stack.
Use real temporary directories and an internal copy-file fault seam for reliable
I/O-failure and cancellation tests. T0.6 will consolidate these tests into Core.

## Consequences

The old directory remains available as the original copy. Existing Makredo data
always wins, including when another process creates the destination during a
migration. Retrying starts from a fresh staging directory and current source data.

A hard process termination or a failed cleanup can leave a staging directory.
Later attempts ignore it and retry; automatic removal of another process's
staging data is intentionally avoided. Files are copied individually, so the
legacy application should be closed while migration runs; a read or sharing
failure aborts the attempt without publishing partial data.
