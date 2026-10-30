#!/usr/bin/env bash
#
# run.sh — the test for baseline-tightened.sh.
#
# Each case is inline: a before/after baseline pair and the verdict it must
# produce. A wrong answer here lets a PR loosen the gate unnoticed, or blocks a
# legitimate baseline refresh, so every rule in the script's header has a case.
# A "loosened" verdict must come with a LOOSENED line, so a jq error can never
# pass for a detected loosening.
#
#   run.sh    run every case
#
# Exit 0 if every case passed, 1 otherwise.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TIGHTENED="$HERE/../baseline-tightened.sh"
RUNROOT="${FIXTURE_RUNROOT:-${TMPDIR:-/tmp}/baseline-tightened-fixtures.$$}"

command -v jq >/dev/null || { echo "jq required" >&2; exit 3; }

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  RED=$'\033[31m'; GRN=$'\033[32m'; DIM=$'\033[2m'; BLD=$'\033[1m'; OFF=$'\033[0m'
else RED=""; GRN=""; DIM=""; BLD=""; OFF=""; fi

mkdir -p "$RUNROOT"
trap '[[ -n "${FIXTURE_KEEP:-}" ]] || rm -rf "$RUNROOT"' EXIT

PASS=0; FAIL=0

baseline_case() {
  local name="$1" expected="$2" before="$3" after="$4"
  local dir="$RUNROOT/${name// /-}"
  mkdir -p "$dir"
  [[ -n "$before" ]] && printf '%s' "$before" >"$dir/before.json"
  [[ -n "$after" ]] && printf '%s' "$after" >"$dir/after.json"

  bash "$TIGHTENED" "$dir/before.json" "$dir/after.json" >"$dir/out.log" 2>&1
  local rc=$?
  local verdict="error (exit $rc): $(tr '\n' ' ' <"$dir/out.log")"
  [[ $rc -eq 0 ]] && verdict=tightened
  [[ $rc -eq 1 ]] && grep -q '^LOOSENED: ' "$dir/out.log" && verdict=loosened

  if [[ "$expected" == "$verdict" ]]; then
    printf "  ${GRN}PASS${OFF}  %s\n" "$name"; PASS=$((PASS+1))
  else
    printf "  ${RED}FAIL${OFF}  %s\n      ${DIM}expected: %s${OFF}\n      ${DIM}actual:   %s${OFF}\n" \
      "$name" "$expected" "$verdict"
    FAIL=$((FAIL+1))
  fi
}

BASE='{"schema":1,"package":"Gotrue","project":"Gotrue/Gotrue.csproj","testProject":"Gotrue.Tests/Gotrue.Tests.csproj","warnings":{"total":10,"byCode":{"CS1591":6,"CS8618":4}},"updated":"2026-10-01T00:00:00Z","coverage":{"hermeticLine":60.5}}'
with() { jq -c "$1" <<<"$BASE"; }

baseline_case "unchanged"               tightened "$BASE" "$BASE"
baseline_case "only updated moved"      tightened "$BASE" "$(with '.updated = "2026-10-30T00:00:00Z"')"
baseline_case "warnings lowered"        tightened "$BASE" "$(with '.warnings = {"total":8,"byCode":{"CS1591":4,"CS8618":4}}')"
baseline_case "code dropped to zero"    tightened "$BASE" "$(with '.warnings = {"total":6,"byCode":{"CS1591":6}}')"
baseline_case "coverage raised"         tightened "$BASE" "$(with '.coverage.hermeticLine = 62.1')"
baseline_case "coverage added"          tightened "$(with 'del(.coverage)')" "$BASE"
baseline_case "new package"             tightened "" "$BASE"
baseline_case "warnings total rose"     loosened  "$BASE" "$(with '.warnings.total = 11')"
baseline_case "warnings total removed"  loosened  "$BASE" "$(with 'del(.warnings.total)')"
baseline_case "one code rose"           loosened  "$BASE" "$(with '.warnings.byCode.CS8618 = 5')"
baseline_case "new code appeared"       loosened  "$BASE" "$(with '.warnings.byCode.CS0168 = 1')"
baseline_case "coverage fell"           loosened  "$BASE" "$(with '.coverage.hermeticLine = 60.4')"
baseline_case "coverage removed"        loosened  "$BASE" "$(with 'del(.coverage)')"
baseline_case "project changed"         loosened  "$BASE" "$(with '.project = "Other/Other.csproj"')"
baseline_case "baseline deleted"        loosened  "$BASE" ""

echo
if [[ $FAIL -eq 0 ]]; then
  echo "${GRN}${BLD}cases: $PASS/$((PASS+FAIL)) passed${OFF}"; exit 0
else
  echo "${RED}${BLD}cases: $FAIL/$((PASS+FAIL)) failed${OFF}"; exit 1
fi
