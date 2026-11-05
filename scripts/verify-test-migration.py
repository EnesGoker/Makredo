#!/usr/bin/env python3
"""Verify T0.6's test inventory without loading or executing Windows assemblies."""

import argparse
import dataclasses
import itertools
from pathlib import Path
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
BASELINE = "001acea3ce5050a31ff72499648cadea3a2bdfa0"
OLD_PROJECTS = ("tests/Makredo.Tests", "tests/Makredo.Migration.Tests")
NEW_PROJECTS = ("tests/Makredo.Core.Tests", "tests/Makredo.Windows.Tests")
MAP_PATH = ROOT / "docs/testing/T0.6-test-map.md"
EXPANDED_TEST = "AppShellLayoutProfile_KeepsToolbarMetricsConsistent"
TEST_ATTRIBUTE = re.compile(r"\[(?:Fact|Theory|UnixTheory)\b")
METHOD = re.compile(
    r"public\s+(?:(?:async|static)\s+)*(?:Task(?:<[^>]+>)?|void)\s+(\w+)\s*\("
)


@dataclasses.dataclass(frozen=True)
class Test:
    path: str
    line: int
    name: str
    data: tuple[str, ...]
    attributes: tuple[str, ...]

    @property
    def cases(self):
        return len(self.data) or 1


def parse_tests(path, contents):
    """Read the single-line xUnit attributes/signatures used by this migration."""
    attributes = []
    tests = []
    for line_number, line in enumerate(contents.splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith("["):
            attributes.append(stripped)
            continue
        method = METHOD.search(line)
        if method and any(TEST_ATTRIBUTE.match(item) for item in attributes):
            if any("MemberData" in item or "ClassData" in item for item in attributes):
                raise ValueError(f"Unsupported data source at {path}:{line_number}")
            tests.append(
                Test(
                    path,
                    line_number,
                    method.group(1),
                    tuple(item for item in attributes if item.startswith("[InlineData(")),
                    tuple(attributes),
                )
            )
        if stripped:
            attributes = []
    return tests


def git(*arguments):
    return subprocess.check_output(["git", *arguments], cwd=ROOT, text=True)


def read_baseline():
    paths = git("ls-tree", "-r", "--name-only", BASELINE, "--", *OLD_PROJECTS).splitlines()
    tests = []
    for path in paths:
        if path.endswith(".cs"):
            tests.extend(parse_tests(path, git("show", f"{BASELINE}:{path}")))
    return tests


def read_current():
    tests = []
    for project in NEW_PROJECTS:
        for path in sorted((ROOT / project).rglob("*.cs")):
            if "obj" in path.parts or "bin" in path.parts:
                continue
            relative = path.relative_to(ROOT).as_posix()
            contents = path.read_text()
            if re.search(r"\b(?:Task\.Delay|Thread\.Sleep)\s*\(", contents):
                raise ValueError(f"Fixed-delay test wait remains in {relative}")
            if re.search(r"\b(?:class\s+Expect|Makredo\.Tests\.Expect)\b", contents):
                raise ValueError(f"Custom Expect layer remains in {relative}")
            tests.extend(parse_tests(relative, contents))
    return tests


def indexed(tests):
    result = {}
    for test in tests:
        if test.name in result:
            raise ValueError(f"Duplicate test method name: {test.name}")
        result[test.name] = test
    return result


def verify(baseline, current):
    if len(baseline) != 108 or sum(test.cases for test in baseline) != 143:
        raise ValueError("Baseline must contain 108 methods / 143 discovered cases")
    old = indexed(baseline)
    new = indexed(current)
    missing = sorted(old.keys() - new.keys())
    extra = sorted(new.keys() - old.keys())
    if missing or extra:
        raise ValueError(f"Test identities differ; missing={missing}; extra={extra}")
    for name, previous in old.items():
        replacement = new[name]
        if any(re.search(r"\bSkip\s*=", item) for item in replacement.attributes) and not any(
            re.search(r"\bSkip\s*=", item) for item in previous.attributes
        ):
            raise ValueError(f"Previously active test was skipped: {name}")
        if name == EXPANDED_TEST:
            expected = {
                (width, height, scale)
                for (width, height), scale in itertools.product(
                    ((1280, 760), (1920, 1200), (3360, 2100)),
                    (1.0, 1.25, 1.5, 2.0),
                )
            }
            observed = []
            for row in replacement.data:
                match = re.fullmatch(
                    r"\[InlineData\((\d+),\s*(\d+),\s*([\d.]+)f\)\]", row
                )
                if not match:
                    raise ValueError(f"Unrecognized toolbar data row: {row}")
                observed.append((int(match[1]), int(match[2]), float(match[3])))
            if len(observed) != 12 or set(observed) != expected:
                raise ValueError("Toolbar theory must retain all 3 working areas x 4 scales")
            if not any(item.startswith("[Theory") for item in replacement.attributes):
                raise ValueError("Toolbar matrix must be an xUnit Theory")
        else:
            previous_kind = next(re.match(r"\[(\w+)", item)[1] for item in previous.attributes if TEST_ATTRIBUTE.match(item))
            replacement_kind = next(re.match(r"\[(\w+)", item)[1] for item in replacement.attributes if TEST_ATTRIBUTE.match(item))
            if previous_kind != replacement_kind:
                raise ValueError(f"Test attribute kind changed: {name}")
            if previous.data != replacement.data:
                raise ValueError(f"InlineData changed or a case disappeared: {name}")
            if previous.cases != replacement.cases:
                raise ValueError(f"Case count changed: {name}")
    if sum(test.cases for test in current) != 154:
        raise ValueError("Expected 143 previous cases + 11 separately discovered toolbar rows")
    return old, new


def group(test):
    if test.path.endswith("/Program.cs"):
        return "Original Program.cs"
    if test.path.startswith("tests/Makredo.Migration.Tests/"):
        return "Migration"
    return "T0.2/T0.3 regressions"


def render_map(baseline, current):
    new = indexed(current)
    lines = [
        "# T0.6 test migration inventory",
        "",
        f"Baseline: PR #4 squash merge `{BASELINE}`.",
        "",
        "Generated and checked by `python3 scripts/verify-test-migration.py`; regenerate with `--write-map`.",
        "The script reads baseline sources with `git show` and compares every original test method and InlineData row with the two current projects. It does not run Windows code.",
        "",
        "All **108 original test methods** remain. The original **143 discovered cases** become **154** because one existing toolbar Fact's internal 3 working areas × 4 scales loop is now 12 separately discovered Theory rows (**+11**, no new scenarios and no omitted rows).",
        "",
        "| Baseline source group | Original methods | Original cases | Current methods | Current cases |",
        "|---|---:|---:|---:|---:|",
    ]
    for label in ("Original Program.cs", "Migration", "T0.2/T0.3 regressions"):
        previous = [test for test in baseline if group(test) == label]
        replacements = [new[test.name] for test in previous]
        lines.append(
            f"| {label} | {len(previous)} | {sum(test.cases for test in previous)} | "
            f"{len(replacements)} | {sum(test.cases for test in replacements)} |"
        )
    lines.extend(
        [
            "| **Total** | **108** | **143** | **108** | **154** |",
            "",
            "| Current project | Methods | Discovered cases |",
            "|---|---:|---:|",
        ]
    )
    for project in NEW_PROJECTS:
        tests = [test for test in current if test.path.startswith(project + "/")]
        lines.append(f"| `{project}` | {len(tests)} | {sum(test.cases for test in tests)} |")
    lines.extend(
        [
            "",
            "Counts describe source/discovery inventory. Passing runtime results are reported separately; the script does not claim Windows tests passed. `UnixTheory` rows remain platform-specific and skip on Windows as before.",
            "",
            "## Every original test and its new location",
            "",
            "Paths are repository-relative. Method names are unchanged; a row with multiple cases retains all its original InlineData values.",
            "",
            "| Original source | Original test name | New file | Cases before → after |",
            "|---|---|---|---:|",
        ]
    )
    for previous in baseline:
        replacement = new[previous.name]
        lines.append(
            f"| `{previous.path}` | `{previous.name}` | `{replacement.path}` | "
            f"{previous.cases} → {replacement.cases} |"
        )
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-map", action="store_true", help="Regenerate the Markdown inventory")
    arguments = parser.parse_args()
    baseline = read_baseline()
    current = read_current()
    verify(baseline, current)
    rendered = render_map(baseline, current)
    if arguments.write_map:
        MAP_PATH.parent.mkdir(parents=True, exist_ok=True)
        MAP_PATH.write_text(rendered)
    elif not MAP_PATH.exists() or MAP_PATH.read_text() != rendered:
        raise ValueError("Inventory document is stale; run with --write-map")
    print("PASS: 108/108 original methods retained; 143 original cases -> 154 (toolbar loop -> 12 Theory rows).")
    for project in NEW_PROJECTS:
        tests = [test for test in current if test.path.startswith(project + "/")]
        print(f"{project}: {len(tests)} methods / {sum(test.cases for test in tests)} cases")
    print("PASS: original InlineData retained; toolbar matrix complete; no custom Expect or fixed-delay waits.")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        sys.exit(1)
