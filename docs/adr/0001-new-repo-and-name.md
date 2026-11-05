# ADR 0001: Start a new repository under the Makredo name

- Status: Accepted
- Date: 2026-11-05

## Context

The application previously used the names MacroMaster and Polly. Polly conflicts
with the name of a well-known .NET library. The existing repository also has a
fragmented history that is unsuitable as the foundation for the planned release.

## Decision

Use **Makredo** as the product name and
[EnesGoker/Makredo](https://github.com/EnesGoker/Makredo) as its public repository.
Import the source from
[MacroMaster commit 837fe7585f90102d54062898061481d3a3870a2f](https://github.com/EnesGoker/MacroMaster/commit/837fe7585f90102d54062898061481d3a3870a2f)
in a single root commit without carrying over the old Git history. Record the
source repository and full commit URL in that commit's body.

Use the MIT license, with Enes Göker as the publisher. Make the old MacroMaster
repository private after the Makredo beta release.

## Consequences

The new repository has an independent history while retaining an explicit source
reference. Import sanitization must remove the installer’s personal absolute path
before the first commit. The starter kit and architecture decisions are added in
a separate commit. Product, project, namespace, and assembly renaming is planned
for T0.2; importing the source does not complete that work. Making the old
repository private remains a post-beta action.
