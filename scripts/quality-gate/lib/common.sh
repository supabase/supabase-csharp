# lib/common.sh — environment, terminal, tool checks, jq readers, process runner.
# Sourced by gate.sh; runs under its `set -euo pipefail`.

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  RED=$'\033[31m'; GRN=$'\033[32m'; YEL=$'\033[33m'
  DIM=$'\033[2m'; BLD=$'\033[1m'; OFF=$'\033[0m'
else RED=""; GRN=""; YEL=""; DIM=""; BLD=""; OFF=""; fi

have() { command -v "$1" >/dev/null 2>&1; }

# Baseline / config readers. $BASELINE (per-package measurements) and $CONFIG
# (repo-wide settings) are globals set by discovery. jq is required — see gate.sh:
# without it every read returns empty, no baseline is ever enforced, and the gate
# reports "baseline established by this run" on every invocation, passing silently.
bget() {  # bget <jq-path> — read a field from the per-package baseline
  [[ -f "$BASELINE" ]] || { echo ""; return; }
  jq -r "$1 // empty" "$BASELINE"
}

cget() {  # cget <jq-path> — package baseline wins, then repo config
  local v; v="$(bget "$1")"
  if [[ -z "$v" && -n "$CONFIG" ]]; then v="$(jq -r "$1 // empty" "$CONFIG")"; fi
  echo "$v"
}

# Run a command with output captured to a log, recording its exit in $RC without
# tripping `set -e`. Callers read $RC immediately after.
RC=0
run() { local log="$1"; shift; set +e; "$@" >"$log" 2>&1; RC=$?; set -e; }

# Project discovery, shared by single- and solution-scope resolution.
# Production project = the one non-test csproj. Exclusion patterns are QUOTED so
# find does the matching: passed unquoted through a variable, the shell would glob
# '*Tests*' against the cwd first and the exclusion would silently stop working.
#
# scripts/quality-gate/fixtures is the gate's OWN test harness — tiny throwaway
# Sample/Sample.Tests projects planted for run-fixtures.sh, never gated content.
# Excluded here so a broad-scope run (e.g. bare `gate.sh` at the repo root) never
# discovers one as a real package to build/test/format-check.
find_production_project() {  # find_production_project <dir>
  find "$1" -name '*.csproj' -not -name '*Tests*' \
    -not -path '*/bin/*' -not -path '*/obj/*' \
    -not -path '*/scripts/quality-gate/fixtures/*' 2>/dev/null | sort | head -n1
}
find_test_project() {  # find_test_project <dir>
  find "$1" \( -name '*Tests*.csproj' -o -name '*.Tests.csproj' \) \
    -not -path '*/bin/*' -not -path '*/obj/*' \
    -not -path '*/scripts/quality-gate/fixtures/*' 2>/dev/null | sort -u | head -n1
}
all_test_projects() {  # all_test_projects <dir>
  find "$1" -name '*Tests*.csproj' -o -name '*.Tests.csproj' 2>/dev/null \
    | grep -v '/bin/\|/obj/\|/scripts/quality-gate/fixtures/' | sort -u
}

# One or many packages? A baseline that pins testProject settles it as single.
# Otherwise, the number of test projects under the directory decides: more than one
# means the directory holds several packages, so the run is a solution. $ALL forces
# the solution run for a directory that would otherwise resolve to one package.
#
# Reads $PACKAGE_DIR/$PACKAGE_NAME/$ALL/$CLI_PROJECT (set by the caller's argument
# parsing); writes $BASELINE, $PKG_DIR/$PKG_NAME/$PKG_PROJ/$PKG_TEST, $MULTI, $SLN,
# $SCOPE_DIR, $BUILD_TARGET, $SCAN_TARGET. Shared by gate.sh and any other entry
# point (e.g. format-suggest.sh) that needs the same package resolution.
discover_scope() {
  BASELINE="$PACKAGE_DIR/.gate-baseline.json"
  PKG_DIR=(); PKG_NAME=(); PKG_PROJ=(); PKG_TEST=()
  MULTI=0; SLN=""

  local _bpkg_test; _bpkg_test="$(bget .testProject)"
  [[ -n "$_bpkg_test" ]] && _bpkg_test="$PACKAGE_DIR/$_bpkg_test"

  if [[ -n "$_bpkg_test" && -f "$_bpkg_test" ]]; then
    MULTI=0                                   # baseline pins a single package
  else
    local _dirs=() _seen="" t p
    while IFS= read -r t; do
      [[ -n "$t" ]] || continue
      p="$(cd "$(dirname "$(dirname "$t")")" && pwd -P)"
      case "$_seen" in *"|$p|"*) ;; *) _seen="$_seen|$p|"; _dirs+=("$p") ;; esac
    done < <(all_test_projects "$PACKAGE_DIR")
    if [[ ${#_dirs[@]} -gt 1 || ( $ALL -eq 1 && ${#_dirs[@]} -ge 1 ) ]]; then
      MULTI=1; PKG_DIR=("${_dirs[@]}")
    fi
  fi

  if [[ $MULTI -eq 1 ]]; then
    # Solution: one .sln at the git root drives the build; each package resolves its
    # own two projects for tests and warning attribution.
    SCOPE_DIR="$PACKAGE_DIR"
    local _sroot; _sroot="$(git -C "$SCOPE_DIR" rev-parse --show-toplevel 2>/dev/null || echo "$SCOPE_DIR")"
    SLN="$(find "$_sroot" -maxdepth 1 -name '*.sln' 2>/dev/null | sort | head -n1)"
    [[ -f "$SLN" ]] || { echo "solution mode needs a .sln at $_sroot, none found" >&2; exit 3; }
    local p
    for p in "${PKG_DIR[@]}"; do
      PKG_NAME+=("$(basename "$p")")
      PKG_PROJ+=("$(find_production_project "$p")")
      PKG_TEST+=("$(find_test_project "$p")")
    done
    BUILD_TARGET="$SLN"; SCAN_TARGET="$SLN"
  else
    # Single package: production project = the one non-test csproj, unless the baseline
    # or a .csproj argument pins it. The test project is required.
    SCOPE_DIR="$PACKAGE_DIR"
    local _proj _test
    _proj="$(bget .project)"; [[ -n "$_proj" ]] && _proj="$PACKAGE_DIR/$_proj"
    [[ -f "${_proj:-}"      ]] || _proj="$(find_production_project "$PACKAGE_DIR")"
    [[ -n "$CLI_PROJECT"    ]] && _proj="$CLI_PROJECT"
    _test="${_bpkg_test:-}"
    [[ -f "${_test:-}"      ]] || _test="$(find_test_project "$PACKAGE_DIR")"
    [[ -f "${_test:-}"      ]] || { echo "no test project found under $PACKAGE_DIR — set testProject in $BASELINE" >&2; exit 3; }
    [[ -f "${_proj:-}"      ]] || { echo "no production project found under $PACKAGE_DIR — set production project in $BASELINE" >&2; exit 3; }
    PKG_DIR=("$PACKAGE_DIR"); PKG_NAME=("$PACKAGE_NAME"); PKG_PROJ=("$_proj"); PKG_TEST=("$_test")
    BUILD_TARGET="$_test"; SCAN_TARGET="$_proj"
  fi
}
