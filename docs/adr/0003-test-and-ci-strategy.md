# ADR 0003: Separate deterministic tests from guarded Windows CI experiments

- Status: Accepted
- Date: 2026-11-06
- Task: T0.8

## Context

Phase 1 needs evidence about hosted Windows desktop capture and native input,
without running real input on a developer's computer. T0.7 established the three
required CI jobs; discovery must not add native experiments to those jobs or to
the default test suite. Hosted runner images and desktop state can change.

## Observed results

The [discovery run 42132475118](https://github.com/EnesGoker/Makredo/actions/runs/42132475118)
tested tool source `c0ebeb3` at PR #8's synthetic merge commit
`3aa3b96896f6e066e4572ec5ce797ad12d79ea1e`.

| Question | Windows x64 (`windows-latest`) | Windows ARM64 (`windows-11-arm`) |
|---|---|---|
| (a) Show a WinForms form and obtain a validated screen PNG? | **Demonstrated.** Actual screen pixels matched both form markers in the first attempt. | **Not demonstrated on this runner desktop.** The form reported visible, handle-created and painted, but markers did not match in ten attempts; the retained PNG showed Windows privacy setup. PNG creation itself succeeded. |
| (b) Install low-level keyboard/mouse hooks and capture SendInput? | **Yes.** Both hooks installed; four tagged inputs accepted and observed with injected flags. | **Yes, natively on ARM64.** Same hook, input and flag evidence. |

Evidence artifacts:

- [ci-discovery-x64](https://github.com/EnesGoker/Makredo/actions/runs/42132475118/artifacts/13464266296)
- [ci-discovery-ARM64](https://github.com/EnesGoker/Makredo/actions/runs/42132475118/artifacts/13464565900)

Each artifact contains `report.json`, `console.log`, and `screenshot-form.png`.
The x64 PNG shows the probe form with its architecture label and two color
markers. The ARM64 PNG shows the Windows device privacy setup screen. This is
evidence that the requested form pixels were not captured, rather than proof of
a generally unsupported ARM64 API. The exact foreground/desktop cause was not
diagnosed; no privacy choices were clicked and no OS policy was changed.

Both reports recorded session ID 2, `UserInteractive=true`, 96 DPI, and a
640 × 360 client capture region. Thus session 0 or a headless runner must not be
inferred from the ARM64 result. These observations describe the sampled runner
desktop, not a guarantee about future images.

### Native input evidence

On both native process architectures, `INPUT` was 40 bytes. `SendInput` accepted
4/4 events: F24 down, F24 up, and two relative mouse MOVE requests
(`dx=+1` and `dx=-1`) without clicks. The captured mouse coordinates remained
unchanged in both reports; the evidence is event delivery, not a measured cursor
displacement. The hooks captured only events carrying the tool's `dwExtraInfo` marker
`0x4D4B5238`:

| Event | Message | Raw flags | Interpretation |
|---|---|---|---|
| F24 down | `WM_KEYDOWN` (`0x0100`) | `0x10` | `LLKHF_INJECTED` set |
| F24 up | `WM_KEYUP` (`0x0101`) | `0x90` | Injected plus key-up flag |
| Both mouse MOVE requests | `WM_MOUSEMOVE` (`0x0200`) | `0x01` | `LLMHF_INJECTED` set |

The additional cleanup F24 key-up was accepted (1/1). Both hooks were unhooked
successfully, the original cursor position was restored, and both reports had
empty error lists and no timeout. No unrelated key values were recorded.

## Decision

1. Keep Core tests deterministic with fake input/hotkey sources, fake playback
   adapters, and `TimeProvider`. They remain the main coverage for cancellation,
   errors, stop paths, state transitions and input ownership.
2. Add guarded real-hook/SendInput CI integration coverage on **both x64 and
   ARM64** in Phase 1. The experiment demonstrates that the required native
   plumbing works. For T1.2, exercise held-input cleanup against the real Windows
   adapter in addition to fake-adapter tests for every exit path. Keep these
   real-input runs explicitly CI-only and separate from default local tests.
3. For T1.6, use fake sources to cover physical versus injected emergency-stop
   branches deterministically, and real hooks in CI to check that actual injected
   SendInput events are identified and ignored as intended. **Injected input does
   not prove the positive physical-key path.** Do not clear an injected flag and
   call that hardware evidence; manual K1 checks must cover physical emergency
   stop with modifiers and focus in another application.
4. Use **x64 as the demonstrated screenshot baseline**. The separate discovery
   workflow uploads actual screen PNGs on both architectures and verifies marker
   pixels before reporting support. Retain ARM64 screenshots as diagnostics;
   establish a repeatable usable desktop before relying on ARM64 pixel baselines.
   Existing layout tests still run on both architectures. This probe form is not
   a screenshot or visual acceptance test of Makredo's product UI.

## Isolation and repeatability

`tools/Makredo.CiDiscovery` is in the solution for compilation and formatting,
but has `IsTestProject=false`, no test SDK, and no product/test project references.
`dotnet test` does not execute it. No product code was changed for this discovery.

Before file creation, UI initialization or probes, the executable requires
`GITHUB_ACTIONS` to equal `true`, a Windows runner with X64/ARM64 architecture,
and the explicit `--ci-discovery --output <directory>` invocation. Native input
wrappers check the CI environment again. The workflow verified that a non-CI
environment and a missing opt-in both exit with code 2 without creating output.
These are accidental-run fences, not an assertion that environment variables
cannot be deliberately spoofed.

`.github/workflows/ci-discovery.yml` runs on `workflow_dispatch` and relevant PR
changes, independently of the three required checks. Its green status means
the experiment completed and produced a valid observation report, including a
truthful negative capability result; it does not mean every capability is
supported. Each matrix job uploads JSON, logs and PNGs for 30 days. Rerun it when
runner images or desktop behavior change, and reassess conditional findings.
The `workflow_dispatch` trigger is configured; evidence above is from the PR
trigger. Default-branch availability of manual dispatch follows merge.

## Validation and limits

[Required CI 42132475038](https://github.com/EnesGoker/Makredo/actions/runs/42132475038)
passed all three jobs: Ubuntu Core 120/120, and Windows x64/ARM64 each with 107
Core passes, five pre-existing UnixTheory exclusions, and 34 Windows passes.
Builds had zero warnings/errors; Windows formatting and the eight-project NuGet
audit passed. The extra project only changes the audit's expected project count,
which is now derived from the solution. Discovery is not a required CI job.

On the developer Mac only compilation, Core tests, and formatting were run;
the discovery executable and real-input tests were never launched there. This
experiment does not establish physical hardware behavior, elevated/UIPI targets,
locked desktops, all DPI configurations, or future hosted image behavior.

## References

- [Graphics.CopyFromScreen](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.copyfromscreen)
  copies actual screen pixels; offscreen rendering alone would not establish a
  usable desktop capture.
- [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
  and [LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)
  require the installing thread to pump messages.
- [KBDLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct)
  and [MSLLHOOKSTRUCT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct)
  define the injected flag bits recorded above.
- [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)
  documents accepted-event counts and the inability to identify UIPI blocking
  solely from its return value or last error.
