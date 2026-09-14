#!/usr/bin/env bash
#
# gate.sh — the Supabase C# SDK quality gauntlet (QUALITY_RUBRIC §4), as a program.
#
#   gate.sh [dir]                full gate — build, tests, security, public API, E2E
#   gate.sh [dir] --fast         inner loop only — the agent's red/green cycle
#   gate.sh [dir] --bypass-format  format + naming reports as a signal, not a
#                                blocking stage, for this run. For PR-time CI
#                                only: a human contributor can't always fix a
#                                format violation themselves the way an agent
#                                can, and master gets auto-formatted after merge
#                                regardless (see .github/workflows/format-master.yml).
#                                Local/agent runs should not use this — it stays
#                                blocking by default.
#   gate.sh [dir] --overwrite-baseline  persist the changes to .gate-baseline.json
#                                (warnings down, coverage up). WITHOUT it, the gate
#                                is read-only: it still reads the baseline and
#                                produces a verdict, but never writes. This keeps a
#                                contributor's run from silently editing a committed
#                                file they've no reason to touch — updating the baseline is a
#                                maintainer/CI concern, done post-merge on master
#                                (see .github/workflows/build-and-test.yml), never
#                                on a PR run.
#
# One directory with several packages runs as a solution: one build, one scan, one
# API check, per-package tests, and a single verdict. A single package keeps the
# fast per-package path the inner loop uses. --all forces the solution run.
#
# Mutation testing is not run here — it is too slow for the inner/PR loop and lives
# in its own scheduled GitHub Action.
#
# Config that belongs to the package lives in a committed .gate-baseline.json, not
# on the command line. The file is created on the first run with --overwrite-baseline.
#
# Exit codes:
#   0  all blocking stages passed
#   1  a blocking stage FAILED
#   2  a blocking stage could not be run — "couldn't check" is not "done"
#   3  usage / environment error
#
# Signal stages never affect the exit code; they are surfaced for the human merge
# decision (QUALITY_RUBRIC §4).
#
# Layout: this entry script does argument parsing, scope discovery and
# orchestration. The stages and their machinery live in lib/ beside it:
#   lib/common.sh    environment, jq readers, project discovery, run()
#   lib/report.sh    the row store, streaming renderer and report.json
#   lib/warnings.sh  the analyzer and per-package baseline IO
#   lib/coverage.sh  the line-coverage and per-package baseline IO
#   lib/stages.sh    one definition of each gauntlet stage, for both scopes
#
set -euo pipefail

SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SELF_DIR/lib/common.sh"
source "$SELF_DIR/lib/report.sh"
source "$SELF_DIR/lib/warnings.sh"
source "$SELF_DIR/lib/coverage.sh"
source "$SELF_DIR/lib/stages.sh"

# -------------------------------------------------------------------- options

PACKAGE_DIR="$PWD"; MODE="standard"; ALL=0; CLI_PROJECT=""; BYPASS_FORMAT=0; OVERWRITE_BASELINE=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --fast) MODE="fast"; shift ;;
    --full) shift ;;   # accepted as an alias: the default run is already the full gate
    --all)  ALL=1; shift ;;
    --bypass-format) BYPASS_FORMAT=1; shift ;;
    --overwrite-baseline) OVERWRITE_BASELINE=1; shift ;;
    -h|--help) sed -n '3,23p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "unknown option: $1  (try --help)" >&2; exit 3 ;;
    *)  PACKAGE_DIR="$1"; shift ;;
  esac
done

# Accept a package directory, or a .csproj (use its directory, and pin it).
if [[ -f "$PACKAGE_DIR" ]]; then
  case "$PACKAGE_DIR" in
    *.csproj)
      CLI_PROJECT="$(cd "$(dirname "$PACKAGE_DIR")" && pwd -P)/$(basename "$PACKAGE_DIR")"
      PACKAGE_DIR="$(dirname "$PACKAGE_DIR")" ;;
    *) echo "expected a package directory or a .csproj, got a file: $PACKAGE_DIR" >&2; exit 3 ;;
  esac
elif [[ ! -d "$PACKAGE_DIR" ]]; then
  echo "no such directory: $PACKAGE_DIR" >&2; exit 3
fi
# Physical path (pwd -P): `git rev-parse --show-toplevel` returns the symlink-
# resolved path, and the format stage strips that root off project dirs to match
# changed files. A logical path here (e.g. macOS /var -> /private/var) would fail
# the strip and every file would "match nothing" — a silent format false-pass.
PACKAGE_DIR="$(cd "$PACKAGE_DIR" && pwd -P)"
PACKAGE_NAME="$(basename "$PACKAGE_DIR")"

have dotnet || { echo "dotnet not on PATH (DOTNET_ROOT=$DOTNET_ROOT)" >&2; exit 3; }
# Required, not optional: every baseline read goes through jq. Without it the reads
# return empty, no baseline is ever enforced, and the gate reports "baseline
# established by this run" on every invocation — passing silently.
have jq || { echo "jq not found — required to read baselines. brew install jq" >&2; exit 3; }

# Repo-wide settings live in one .gate-config.json, found by walking up from the
# package. Per-package measurements stay in .gate-baseline.json, which may override
# any config key.
CONFIG=""; _d="$PACKAGE_DIR"
while [[ "$_d" != "/" && -n "$_d" ]]; do
  [[ -f "$_d/.gate-config.json" ]] && { CONFIG="$_d/.gate-config.json"; break; }
  _d="$(dirname "$_d")"
done

# --------------------------------------------------------------- scope discovery
# One or many packages? A baseline that pins testProject settles it as single.
# Otherwise, the number of test projects under the directory decides: more than one
# means the directory holds several packages, so the run is a solution. --all forces
# the solution run for a directory that would otherwise resolve to one package.
# (discover_scope lives in lib/common.sh — shared with format-suggest.sh.)

discover_scope

SCOPE_OUT="$SCOPE_DIR/.gate"; SCOPE_LOGS="$SCOPE_OUT/logs"; mkdir -p "$SCOPE_LOGS"

# ==================================================================== orchestration
# The short-circuits are CAUSAL, not sequential: only a failed build makes the
# stages below impossible. A format violation says nothing about whether the tests
# pass, so it must not hide them. Anything not run is still recorded as SKIP with a
# reason — a stage that silently vanishes reads as verified when it wasn't.
#
# Three test-execution paths (STACK_OK probed once, right after the build, so
# both "which test path to run" and "which SKIP markers to emit" share one
# network check instead of probing twice):
#   --fast                    stage_inner_loop, filtered TestCategory!=E2E — the
#                              fast local cycle; no coverage, verdict is PARTIAL.
#   full mode, stack up       stage_tests_full — an unfiltered run (every
#                              TestCategory together) that is the test-correctness
#                              result, plus a second filtered run that sources the
#                              coverage baseline from a hermetic report — then
#                              stage_coverage (see lib/coverage.sh: the coverage
#                              baseline is hermetic-only by design, E2E stays pass/fail).
#   full mode, stack down     stage_inner_loop as a fallback, so local dev still
#                              gets build/test feedback without `supabase start`;
#                              E2E and coverage both SKIP — "full" coverage isn't
#                              measurable without the E2E half of the suite.

if [[ $MULTI -eq 1 ]]; then
  echo "${DIM}$(basename "$SLN")  ·  ${#PKG_DIR[@]} packages  ·  mode=$MODE${OFF}"
else
  echo "${DIM}$SCOPE_DIR  ·  $(basename "$BUILD_TARGET")  ·  mode=$MODE${OFF}"
fi
echo

stage_build
STACK_OK=0
if [[ $BUILD_OK -eq 1 && "$MODE" != "fast" ]]; then
  # stage_build just reset $BASELINE to "" (so it doesn't leak into the
  # scope-wide stages below) — restore it to a package's own baseline before
  # probing, so a per-package e2eHealthUrl override is actually read. Without
  # this, the probe silently falls through to $CONFIG / the hardcoded default,
  # which can false-positive against an unrelated stack already running on the
  # default port. First package is representative for a solution run.
  BASELINE="${PKG_DIR[0]}/.gate-baseline.json"
  if stack_up; then STACK_OK=1; fi
  BASELINE=""
fi

if [[ $BUILD_OK -eq 1 ]]; then
  stage_format
  if [[ "$MODE" == "fast" ]]; then
    stage_inner_loop
  elif [[ $STACK_OK -eq 1 ]]; then
    stage_tests_full
    # No separate id-7 row here: stage_tests_full's own per-package rows already
    # carry the full outcome (label says "Unit + Contract + E2E"), so a second row
    # would only restate it — and imprecisely, since "E2E ran" isn't uniformly true
    # across every package (a package with zero E2E tests has nothing to report).
    stage_coverage
  else
    stage_inner_loop
  fi
else
  add 1b "Format + naming"              block "" SKIP "not run — build failed" ""
  add 2  "Inner loop (Unit + Contract)" block "" SKIP "not run — build failed" ""
fi

if [[ "$MODE" != "fast" ]]; then
  stage_security
  # The sync check compiles (dotnet format runs the analyzers), so a failed build
  # makes it impossible, not failing — skip it causally, as with format and tests.
  if [[ $BUILD_OK -eq 1 ]]; then stage_api_sync
  else add 5a "Public API declared" block "" SKIP "not run — build failed" ""; fi
  stage_api_diff

  if [[ $BUILD_OK -eq 0 ]]; then
    add 7  "E2E / acceptance" block "" SKIP "not run — build failed" ""
    add 2b "Coverage (line, unit+contract)"  block "" SKIP "not run — build failed" ""
  elif [[ $STACK_OK -eq 0 ]]; then
    add 7  "E2E / acceptance" block "" SKIP "stack down per $STACK_CHECK — run: supabase start" "$SCOPE_LOGS/7-stack.log"
    add 2b "Coverage (line, unit+contract)"  block "" SKIP "not measured — stack down, full run did not execute" ""
  fi
fi

# -------------------------------------------------------------------- verdict
EXIT=0
for i in "${!R_ID[@]}"; do
  [[ "${R_KIND[$i]}" == "block" ]] || continue
  case "${R_STATUS[$i]}" in
    FAIL) EXIT=1 ;;
    SKIP) [[ $EXIT -eq 0 ]] && EXIT=2 ;;
  esac
done
case $EXIT in 0) V="PASS" ;; 1) V="FAIL" ;; 2) V="INCOMPLETE" ;; esac
[[ "$MODE" == "fast" && $EXIT -eq 0 ]] && V="PARTIAL"

write_report "$V" "$EXIT"
print_verdict "$V" "$EXIT"
exit $EXIT
