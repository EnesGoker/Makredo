# ADR 0016: Separate playback origin from foreign injected input

- Status: Accepted
- Date: 2026-11-06
- Task: T1.4 (F-016)

## Decision

Every product SendInput request carries the fixed opaque dwExtraInfo marker
0xD79A6C53E2184F9B on 64-bit processes (0xD79A6C53 on 32-bit processes).
This includes keyboard, mouse, double-click sub-events and cleanup releases.
Cursor positioning also uses marked absolute SendInput on the virtual desktop:
SetCursorPos cannot carry dwExtraInfo and would otherwise leak movement events.
Normalization targets pixel centers and clamps to the virtual desktop bounds.
The native callbacks only copy dwExtraInfo alongside existing raw fields.
Keyboard LLKHF_INJECTED (0x10) and mouse LLMHF_INJECTED (0x01) are decoded
on the background consumer and carried as typed metadata.

The recorder's consumer handler rejects the product marker unconditionally,
including a marked event without the injected flag. Otherwise physical input
is accepted; foreign injected input follows RecordInjectedInput, default true.
This preserves screen keyboards and other accessibility/automation sources.
Filtering precedes recording buffers, pending modifier events and publication.
Marked keyboard samples do not change the source's modifier snapshot.
The source events still expose origin metadata for diagnostics and future input
policies; publishing a source event does not mean it has been recorded.

The marker is an identification convention, not authentication or a security
boundary. It is visible in source and native events; any program can copy it
and cause its own input to be excluded. It is deliberately stable across runs,
not a secret or a per-process token. No trust is inferred from injected flags.

The existing playback-settings JSON stores RecordInjectedInput. The property
initializer supplies true when old files omit it; explicit false survives load
and save. The settings UI applies it to the recorder on load and on changes.
The legacy mouse event contract remains compatible, while the timestamped
capability carries origin metadata. Legacy fake sources default to physical.

## Validation and limits

Core tests cover physical, foreign injected, marked injected, disabled foreign
input and marked non-injected keyboard/mouse samples, plus old JSON and explicit
true/false round trips. Guarded native CI records real playback including held
input cleanup and requires zero recorded events, then verifies foreign input.
A second native test checks foreign keyboard and mouse with the switch off/on.
Settings UI round-trip and change notification are tested; CI uploads its PNG.
Native inputs never run on developer machines.

Shortcut policy (T1.5) and emergency stop (T1.6) are unchanged. This policy does
not provide protection against a hostile program generating or spoofing input.
