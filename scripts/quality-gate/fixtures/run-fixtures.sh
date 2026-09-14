#!/usr/bin/env bash
#
# run-fixtures.sh — the test for the gate itself.
#
# Each fixture under this directory is a tiny, self-contained package with one
# planted defect (or none, for `clean`). We run gate.sh against it and assert the
# verdict it SHOULD produce: the exit code, and the status of the specific stage
# that should have produced it. Asserting the stage — not just the exit code —
# is the point: it stops a fixture passing for the wrong reason (a red-test
# fixture that exits 1 because of a format slip we introduced by accident).
#
# The invariant under test (QUALITY_RUBRIC §4): a check that could not run must
# never report as passed. The causal-skip fixtures (build-broken, and later
# no-network / no-jq) are what actually exercise it — build fails, so format and
# tests must come back SKIP, never PASS.
#
#   run-fixtures.sh                 run every fixture
#   run-fixtures.sh clean red-test  run only the named fixtures
#
# Exit 0 if every fixture matched its expectation, 1 otherwise.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="$HERE/../gate.sh"
BASE="$HERE/_base"
RUNROOT="${FIXTURE_RUNROOT:-${TMPDIR:-/tmp}/gate-fixtures.$$}"

command -v jq >/dev/null || { echo "jq required" >&2; exit 3; }
[[ -f "$GATE" ]] || { echo "gate.sh not found at $GATE" >&2; exit 3; }

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  RED=$'\033[31m'; GRN=$'\033[32m'; DIM=$'\033[2m'; BLD=$'\033[1m'; OFF=$'\033[0m'
else RED=""; GRN=""; DIM=""; BLD=""; OFF=""; fi

# Which fixtures to run: names on the command line, else every dir with expect.json.
fixtures=()
if [[ $# -gt 0 ]]; then
  for n in "$@"; do fixtures+=("$HERE/$n"); done
else
  for d in "$HERE"/*/; do
    [[ -f "$d/expect.json" ]] && fixtures+=("${d%/}")
  done
fi

mkdir -p "$RUNROOT"
trap '[[ -n "${FIXTURE_KEEP:-}" ]] || rm -rf "$RUNROOT"' EXIT

PASS=0; FAIL=0

# assert <label> <expected> <actual> — records a mismatch into $miss, returns nonzero on mismatch.
miss=""
assert() {
  local label="$1" exp="$2" act="$3"
  if [[ "$exp" != "$act" ]]; then
    miss+="      ${RED}✗${OFF} $label: expected ${BLD}$exp${OFF}, got ${BLD}$act${OFF}\n"
    return 1
  fi
  return 0
}

for fx in "${fixtures[@]}"; do
  name="$(basename "$fx")"
  exp="$fx/expect.json"
  [[ -f "$exp" ]] || { echo "${RED}no expect.json for $name${OFF}"; FAIL=$((FAIL+1)); continue; }

  work="$RUNROOT/$name"
  rm -rf "$work"
  cp -R "$BASE" "$work"

  # The format stage scopes to git-changed files, so the fixture must be a real
  # git repo used the way the gate is: commit a clean base, THEN plant the defect.
  # The planted files then show up as a diff against HEAD, which is what puts them
  # in scope. (A commitless repo is a different, latent bug — covered separately.)
  git -C "$work" init -q -b main
  git -C "$work" -c user.email=fixture@gate -c user.name=fixture add -A
  git -C "$work" -c user.email=fixture@gate -c user.name=fixture commit -q -m "clean base"
  [[ -d "$fx/overlay" ]] && cp -R "$fx/overlay/." "$work/"

  mode="$(jq -r '.mode // ""' "$exp")"
  run_log="$work/.run.log"

  # A fixture that exercises E2E needs the gate's stack_up probe to succeed, or the
  # stage skips as "stack down" and never runs the failing test. Stand up a throwaway
  # HTTP listener on the health URL the fixture pins (localhost only, no external
  # network), so E2E actually runs — then tear it down after the gate returns.
  stack_pid=""
  if [[ "$(jq -r '.needsStack // false' "$exp")" == "true" ]]; then
    if ! command -v python3 >/dev/null; then
      echo "  ${RED}FAIL${OFF}  $name — needs a stack listener but python3 is not available"
      FAIL=$((FAIL+1)); continue
    fi
    url="$(jq -r '.e2eHealthUrl // empty' "$work/.gate-baseline.json")"
    hp="${url#*://}"; hp="${hp%%/*}"; shost="${hp%%:*}"; sport="${hp##*:}"
    python3 -m http.server "$sport" --bind "$shost" >/dev/null 2>&1 &
    stack_pid=$!
    disown 2>/dev/null || true   # keep job control from printing "Terminated" on kill
    # Wait until it accepts, so the gate's probe never races the listener's startup.
    for _ in $(seq 1 30); do
      (exec 3<>"/dev/tcp/$shost/$sport") 2>/dev/null && { exec 3>&-; break; }
      sleep 0.1
    done
  fi

  # Snapshot the baseline before the run so a fixture can assert whether the gate
  # WROTE it. Writing is opt-in (--overwrite-baseline): a default run must leave the
  # file byte-identical, an --overwrite-baseline run must change it. "__ABSENT__"
  # distinguishes "no file" from "empty file", so a first-run create is detectable.
  bl="$work/.gate-baseline.json"
  bl_before="$(cat "$bl" 2>/dev/null || echo __ABSENT__)"

  NO_COLOR=1 bash "$GATE" "$work" $mode >"$run_log" 2>&1
  rc=$?
  [[ -n "$stack_pid" ]] && kill "$stack_pid" 2>/dev/null

  report="$work/.gate/report.json"
  miss=""

  if [[ ! -f "$report" ]]; then
    miss+="      ${RED}✗${OFF} gate wrote no report.json (crashed?) — see $run_log\n"
  else
    # Process exit and the report must agree, and both must match the expectation.
    assert "process exit" "$(jq -r '.exit' "$exp")" "$rc"
    assert "report.exitCode" "$(jq -r '.exit' "$exp")" "$(jq -r '.exitCode' "$report")"
    local_verdict="$(jq -r '.verdict // empty' "$exp")"
    [[ -n "$local_verdict" ]] && assert "verdict" "$local_verdict" "$(jq -r '.verdict' "$report")"

    # Per-stage status (and optional detail substring).
    n="$(jq '.stages | length' "$exp")"
    for ((i=0; i<n; i++)); do
      id="$(jq -r ".stages[$i].id" "$exp")"
      want="$(jq -r ".stages[$i].status" "$exp")"
      got="$(jq -r --arg id "$id" '(.stages[] | select(.id==$id) | .status) // "MISSING"' "$report")"
      assert "stage $id" "$want" "$got"

      needle="$(jq -r ".stages[$i].detailContains // empty" "$exp")"
      if [[ -n "$needle" ]]; then
        detail="$(jq -r --arg id "$id" '(.stages[] | select(.id==$id) | .detail) // ""' "$report")"
        case "$detail" in
          *"$needle"*) ;;
          *) miss+="      ${RED}✗${OFF} stage $id detail: expected to contain ${BLD}$needle${OFF}, got ${DIM}$detail${OFF}\n" ;;
        esac
      fi
    done
  fi

  # Optional: did the run write .gate-baseline.json? (expect.baselineWrite: true|false)
  want_write="$(jq -r '.baselineWrite // "unset"' "$exp")"
  if [[ "$want_write" != "unset" ]]; then
    bl_after="$(cat "$bl" 2>/dev/null || echo __ABSENT__)"
    if [[ "$want_write" == "true" && "$bl_before" == "$bl_after" ]]; then
      miss+="      ${RED}✗${OFF} baseline: expected --overwrite-baseline to WRITE it, but it is unchanged\n"
    elif [[ "$want_write" == "false" && "$bl_before" != "$bl_after" ]]; then
      miss+="      ${RED}✗${OFF} baseline: expected a read-only run (no write), but the file CHANGED\n"
    fi
  fi

  if [[ -z "$miss" ]]; then
    printf "  ${GRN}PASS${OFF}  %-18s ${DIM}%s${OFF}\n" "$name" "$(jq -r '.description // ""' "$exp")"
    PASS=$((PASS+1))
  else
    printf "  ${RED}FAIL${OFF}  %-18s ${DIM}%s${OFF}\n" "$name" "$(jq -r '.description // ""' "$exp")"
    printf "$miss"
    printf "      ${DIM}gate output: %s${OFF}\n" "$run_log"
    FAIL=$((FAIL+1))
  fi
done

echo
if [[ $FAIL -eq 0 ]]; then
  echo "${GRN}${BLD}fixtures: $PASS/$((PASS+FAIL)) passed${OFF}"
  exit 0
else
  echo "${RED}${BLD}fixtures: $FAIL/$((PASS+FAIL)) failed${OFF}  ${DIM}(re-run with FIXTURE_KEEP=1 to inspect work dirs)${OFF}"
  exit 1
fi
