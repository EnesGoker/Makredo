# ADR 0012: Test portable behavior through portable assemblies

- Status: Accepted
- Date: 2026-11-06

## Context

T0.6 separates tests into Core (`net10.0`) and Windows (`net10.0-windows`).
Report generation, event/library filtering, and storage path calculation have no
Windows dependency, but originally lived in the WinForms assembly. Keeping their
tests there would prevent portable behavior from being exercised on macOS.
The playback delay regression also relied on a real-time sleep to reach an
in-flight delay.

## Decision

Move the existing pure filtering types and report generator into Application,
and `AppStoragePaths` into Infrastructure.Persistence. Change only namespaces
and consumer imports; keep their implementation and internal visibility.
Use friend assemblies for the existing UI assembly (`Makredo`) and the relevant
test assemblies. Core tests reference the actual production assemblies; do not
compile duplicate linked copies of production source into tests.

Keep WinForms layout/dialog, hook callback, exception presentation, composition,
and startup adapter tests in Windows.Tests. Merge the temporary migration test
project into Core.Tests, including its existing platform-specific theories.

Allow playback to receive an optional `TimeProvider`, defaulting to
`TimeProvider.System`. Both existing delays use that provider. The test-only
`Microsoft.Extensions.TimeProvider.Testing` dependency supplies `FakeTimeProvider`;
a timer-registration signal lets the existing regression advance the pending
delay deterministically. Pause/resume tests use navigation readiness rather than
sleeping. Preserve default runtime behavior and cancellation.

## Consequences

Portable tests execute on macOS against the same assemblies used by the app.
Windows tests still need a Windows runner; compiling them does not demonstrate
that they pass. Future extractions should retain these dependency boundaries.
