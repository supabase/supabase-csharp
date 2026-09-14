#!/usr/bin/env bash
#
# run.sh — the test for mark-api-shipped.sh.
#
# Each fixture beside this file is a directory with two trees:
#   input/     one or more package dirs (PublicAPI.Shipped.txt + Unshipped.txt)
#   expected/  the exact same tree as it should look AFTER the script runs
# We copy input/ to a scratch dir, run mark-api-shipped.sh against it, and assert
# the result is byte-identical to expected/. Asserting the whole tree — not just a
# grep — is the point: it catches a stray blank line, a lost header, a bad sort, or
# an Unshipped file left un-emptied, any of which would break the gate at release.
#
#   run.sh              run every fixture
#   run.sh additions    run only the named fixture(s)
#
# Exit 0 if every fixture matched, 1 otherwise.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="$HERE/../mark-api-shipped.sh"
RUNROOT="${FIXTURE_RUNROOT:-${TMPDIR:-/tmp}/mark-api-fixtures.$$}"

[[ -f "$SCRIPT" ]] || { echo "mark-api-shipped.sh not found at $SCRIPT" >&2; exit 3; }

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  RED=$'\033[31m'; GRN=$'\033[32m'; DIM=$'\033[2m'; BLD=$'\033[1m'; OFF=$'\033[0m'
else RED=""; GRN=""; DIM=""; BLD=""; OFF=""; fi

fixtures=()
if [[ $# -gt 0 ]]; then
  for n in "$@"; do fixtures+=("$HERE/$n"); done
else
  for d in "$HERE"/*/; do
    [[ -d "$d/input" ]] && fixtures+=("${d%/}")
  done
fi

mkdir -p "$RUNROOT"
trap '[[ -n "${FIXTURE_KEEP:-}" ]] || rm -rf "$RUNROOT"' EXIT

PASS=0; FAIL=0
for fx in "${fixtures[@]}"; do
  name="$(basename "$fx")"
  if [[ ! -d "$fx/input" || ! -d "$fx/expected" ]]; then
    echo "  ${RED}FAIL${OFF}  $name — missing input/ or expected/"; FAIL=$((FAIL+1)); continue
  fi

  # Keep scratch files (log, diff) OUTSIDE work/ so they never pollute the tree
  # we compare against expected/.
  work="$RUNROOT/$name/tree"
  run_log="$RUNROOT/$name.log"
  diff_out="$RUNROOT/$name.diff"
  rm -rf "$work"; mkdir -p "$work"
  cp -R "$fx/input/." "$work/"

  NO_COLOR=1 bash "$SCRIPT" "$work" >"$run_log" 2>&1
  rc=$?

  miss=""
  [[ "$rc" -ne 0 ]] && miss+="      ${RED}✗${OFF} script exited $rc (expected 0)\n"

  # Byte-for-byte compare the whole produced tree against expected/.
  if ! diff -r "$fx/expected" "$work" >"$diff_out" 2>&1; then
    miss+="      ${RED}✗${OFF} output differs from expected/:\n"
    while IFS= read -r line; do miss+="        ${DIM}$line${OFF}\n"; done < "$diff_out"
  fi

  if [[ -z "$miss" ]]; then
    printf "  ${GRN}PASS${OFF}  %-16s\n" "$name"; PASS=$((PASS+1))
  else
    printf "  ${RED}FAIL${OFF}  %-16s\n" "$name"; printf "$miss"
    printf "      ${DIM}script output: %s${OFF}\n" "$run_log"; FAIL=$((FAIL+1))
  fi
done

echo
if [[ $FAIL -eq 0 ]]; then
  echo "${GRN}${BLD}fixtures: $PASS/$((PASS+FAIL)) passed${OFF}"; exit 0
else
  echo "${RED}${BLD}fixtures: $FAIL/$((PASS+FAIL)) failed${OFF}  ${DIM}(re-run with FIXTURE_KEEP=1 to inspect)${OFF}"; exit 1
fi
