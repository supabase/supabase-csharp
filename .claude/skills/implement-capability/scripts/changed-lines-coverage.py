#!/usr/bin/env python3
"""Fail when a new or changed production line is not covered by the hermetic test suite.

Usage: changed-lines-coverage.py <package-dir> [--base origin/master]

Runs the package's Unit + Contract tests with coverlet, diffs the working tree against the
merge-base with --base, and reports every changed, instrumentable production line with zero
hits. Untracked files count as fully changed. Exit 0 when every such line is covered, 1 when
one is not, 3 on a usage or environment error.
"""

import argparse
import glob
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

HUNK = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")


def fail(message):
    print(f"error: {message}", file=sys.stderr)
    sys.exit(3)


def git(*args):
    return subprocess.run(["git", *args], check=True, capture_output=True, text=True).stdout


def is_production_source(path):
    return path.endswith(".cs") and ".Tests/" not in path and "/obj/" not in path and "/bin/" not in path


def changed_lines(package, base):
    merge_base = git("merge-base", base, "HEAD").strip()
    changes = {}
    current = None
    for line in git("diff", "-U0", merge_base, "--", package).splitlines():
        if line.startswith("+++ "):
            path = line[6:] if line.startswith("+++ b/") else None
            current = path if path and is_production_source(path) else None
        elif current and (match := HUNK.match(line)):
            start, count = int(match.group(1)), int(match.group(2) or "1")
            changes.setdefault(current, set()).update(range(start, start + count))
    for path in git("ls-files", "--others", "--exclude-standard", "--", package).splitlines():
        if is_production_source(path):
            with open(path, encoding="utf-8") as source:
                changes[path] = set(range(1, sum(1 for _ in source) + 1))
    return changes


def test_project(package):
    projects = glob.glob(os.path.join(package, "*.Tests", "*.Tests.csproj"))
    if len(projects) != 1:
        fail(f"expected one test project under {package}/*.Tests, found {len(projects)}")
    return projects[0]


def collect_coverage(project, results):
    run = subprocess.run(
        ["dotnet", "test", project, "--filter", "TestCategory!=E2E",
         "--collect:XPlat Code Coverage", "--results-directory", results, "-v", "minimal"],
        capture_output=True, text=True)
    if run.returncode != 0:
        print(run.stdout[-4000:], run.stderr[-4000:], sep="\n")
        fail("hermetic test run failed; fix the tests before measuring coverage")
    reports = glob.glob(os.path.join(results, "**", "coverage.cobertura.xml"), recursive=True)
    if not reports:
        fail("coverlet produced no coverage.cobertura.xml")
    return reports


def line_hits(reports, root):
    hits = {}
    for report in reports:
        tree = ET.parse(report)
        sources = [source.text or "" for source in tree.iter("source")] or [""]
        for cls in tree.iter("class"):
            path = resolve(cls.get("filename"), sources, root)
            if path is None:
                continue
            lines = hits.setdefault(path, {})
            measured = cls.find("lines")
            for line in measured if measured is not None else []:
                number = int(line.get("number"))
                lines[number] = max(lines.get(number, 0), int(line.get("hits")))
    return hits


def resolve(filename, sources, root):
    candidates = [filename] if os.path.isabs(filename) else [os.path.join(s, filename) for s in sources]
    for candidate in candidates:
        if os.path.exists(candidate):
            return os.path.relpath(os.path.realpath(candidate), root)
    return None


def report(changes, hits):
    uncovered, instrumentable = {}, 0
    for path, lines in sorted(changes.items()):
        measured = hits.get(path, {})
        changed_measured = sorted(n for n in lines if n in measured)
        instrumentable += len(changed_measured)
        missed = [n for n in changed_measured if measured[n] == 0]
        if missed:
            uncovered[path] = missed
    covered = instrumentable - sum(len(v) for v in uncovered.values())
    print(f"changed instrumentable lines: {instrumentable}, covered: {covered}")
    for path, missed in uncovered.items():
        with open(path, encoding="utf-8") as source:
            text = source.read().splitlines()
        for number in missed:
            print(f"UNCOVERED {path}:{number}: {text[number - 1].strip()}")
    return not uncovered


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("package")
    parser.add_argument("--base", default="origin/master")
    args = parser.parse_args()
    root = git("rev-parse", "--show-toplevel").strip()
    os.chdir(root)
    package = os.path.relpath(os.path.realpath(args.package), root)
    changes = changed_lines(package, args.base)
    if not changes:
        print("no changed production lines")
        return 0
    with tempfile.TemporaryDirectory() as results:
        hits = line_hits(collect_coverage(test_project(package), results), root)
    return 0 if report(changes, hits) else 1


if __name__ == "__main__":
    sys.exit(main())
