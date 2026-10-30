#!/usr/bin/env bash
#
# run.sh — the test for changed-areas.sh.
#
# Each case commits a set of files on top of a base commit in a throwaway repo and
# asserts the areas the script reports. A wrong answer here skips a check a change
# needed, so every rule in the script's header has a case.
#
#   run.sh    run every case
#
# Exit 0 if every case passed, 1 otherwise.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AREAS="$HERE/../changed-areas.sh"
RUNROOT="${FIXTURE_RUNROOT:-${TMPDIR:-/tmp}/changed-areas-fixtures.$$}"

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  RED=$'\033[31m'; GRN=$'\033[32m'; DIM=$'\033[2m'; BLD=$'\033[1m'; OFF=$'\033[0m'
else RED=""; GRN=""; DIM=""; BLD=""; OFF=""; fi

mkdir -p "$RUNROOT"
trap '[[ -n "${FIXTURE_KEEP:-}" ]] || rm -rf "$RUNROOT"' EXIT

PASS=0; FAIL=0

report() {
  local name="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    printf "  ${GRN}PASS${OFF}  %s\n" "$name"; PASS=$((PASS+1))
  else
    printf "  ${RED}FAIL${OFF}  %s\n      ${DIM}expected: %s${OFF}\n      ${DIM}actual:   %s${OFF}\n" \
      "$name" "$expected" "$actual"
    FAIL=$((FAIL+1))
  fi
}

commit_all() {
  git -c user.email=fixture@gate -c user.name=fixture add -A
  git -c user.email=fixture@gate -c user.name=fixture commit -q --allow-empty -m "$1"
}

# areas_case <name> <expected> <changed file>... — commits the files on a fresh base
areas_case() {
  local name="$1" expected="$2"; shift 2
  local repo="$RUNROOT/${name// /-}"
  mkdir -p "$repo"
  (
    cd "$repo"
    git init -q -b main
    commit_all base
    for file in "$@"; do mkdir -p "$(dirname "$file")"; echo changed >"$file"; done
    commit_all change
  )
  report "$name" "$expected" "$(cd "$repo" && bash "$AREAS" "$(git rev-parse HEAD~1)" | paste -sd' ' -)"
}

areas_case "capability sync"           "code=false csharp=false" sdk-compliance.yaml
areas_case "baseline refresh"          "code=false csharp=false" packages/Gotrue/.gate-baseline.json
areas_case "bookkeeping together"      "code=false csharp=false" sdk-compliance.yaml CHANGELOG.md packages/Core/.gate-baseline.json
areas_case "release"                   "code=true csharp=false"  CHANGELOG.md .release-please-manifest.json packages/Core/Core/Core.csproj
areas_case "mark API shipped"          "code=true csharp=false"  packages/Gotrue/Gotrue/PublicAPI.Shipped.txt
areas_case "feature"                   "code=true csharp=true"   packages/Gotrue/Gotrue/Client.cs
areas_case "feature with bookkeeping"  "code=true csharp=true"   packages/Gotrue/Gotrue/Client.cs sdk-compliance.yaml
areas_case "workflow"                  "code=true csharp=false"  .github/workflows/build-and-test.yml
areas_case "nested compliance file"    "code=true csharp=false"  docs/sdk-compliance.yaml
areas_case "nested changelog"          "code=true csharp=false"  packages/Core/CHANGELOG.md
areas_case "baseline outside a package" "code=true csharp=false" .gate-baseline.json
areas_case "empty diff"                "code=true csharp=true"

report "no base" "code=true csharp=true" \
  "$(cd "$RUNROOT/feature" && bash "$AREAS" "" | paste -sd' ' -)"
report "all-zero base" "code=true csharp=true" \
  "$(cd "$RUNROOT/capability-sync" && bash "$AREAS" 0000000000000000000000000000000000000000 | paste -sd' ' -)"
report "unknown base" "code=true csharp=true" \
  "$(cd "$RUNROOT/capability-sync" && bash "$AREAS" 1234567890abcdef1234567890abcdef12345678 | paste -sd' ' -)"

echo
if [[ $FAIL -eq 0 ]]; then
  echo "${GRN}${BLD}cases: $PASS/$((PASS+FAIL)) passed${OFF}"; exit 0
else
  echo "${RED}${BLD}cases: $FAIL/$((PASS+FAIL)) failed${OFF}"; exit 1
fi
