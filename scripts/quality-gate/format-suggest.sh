#!/usr/bin/env bash
#
# format-suggest.sh [dir] [--base-ref <ref>] — apply `dotnet format` to changed
# .cs files and leave the fix as an uncommitted working-tree diff, for a CALLER
# to commit. Used by .github/workflows/format-master.yml: on push to master, it
# runs this scoped to the push's own before..after range and commits whatever
# comes out. Unlike gate.sh's stage_format (--verify-no-changes, checks only,
# never writes), this APPLIES the fix in place. It is not part of the gate: the
# gate stays the read/verdict mechanism (blocking locally and for agents;
# signal-only for PR-time CI via --bypass-format, since a fork contributor
# can't always fix a violation themselves and master gets this auto-fix
# regardless) — this script is what actually produces a fix to commit.
#
# --base-ref overrides which commit "changed" is measured against. Without it,
# scoping falls back to gate.sh's usual formatBaseRef/origin-HEAD logic, which
# assumes comparing a branch against its base — meaningless when run ON master
# itself (every fallback ref would just BE the current commit, a no-op diff).
# format-master.yml passes the push event's `before` SHA so this correctly
# scopes to only the files that specific push actually changed.
#
# Package resolution and changed-file scoping are shared with gate.sh via
# lib/common.sh (discover_scope) and lib/stages.sh (changed_cs_files) — same
# scope, same set of files, so the fix matches exactly what the gate's format
# stage would have failed on.
#
set -euo pipefail

SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SELF_DIR/lib/common.sh"
source "$SELF_DIR/lib/stages.sh"

PACKAGE_DIR="$PWD"; ALL=0; CLI_PROJECT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --all) ALL=1; shift ;;
    --base-ref) export GATE_FORMAT_BASE_REF="$2"; shift 2 ;;
    -h|--help) sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "unknown option: $1  (try --help)" >&2; exit 3 ;;
    *)  PACKAGE_DIR="$1"; shift ;;
  esac
done

[[ -d "$PACKAGE_DIR" ]] || { echo "no such directory: $PACKAGE_DIR" >&2; exit 3; }
# Physical path — see gate.sh for why: `git rev-parse --show-toplevel` returns the
# symlink-resolved path, and changed_cs_files strips that root off project dirs.
PACKAGE_DIR="$(cd "$PACKAGE_DIR" && pwd -P)"
PACKAGE_NAME="$(basename "$PACKAGE_DIR")"

have dotnet || { echo "dotnet not on PATH (DOTNET_ROOT=$DOTNET_ROOT)" >&2; exit 3; }
have jq || { echo "jq not found — required by discover_scope's baseline reads. brew install jq" >&2; exit 3; }

CONFIG=""; _d="$PACKAGE_DIR"
while [[ "$_d" != "/" && -n "$_d" ]]; do
  [[ -f "$_d/.gate-config.json" ]] && { CONFIG="$_d/.gate-config.json"; break; }
  _d="$(dirname "$_d")"
done

discover_scope
SCOPE_DIR="$PACKAGE_DIR"

root="$(git -C "$SCOPE_DIR" rev-parse --show-toplevel)"
files=(); rel=""
while IFS= read -r rel; do [[ -n "$rel" && -f "$root/$rel" ]] && files+=("$rel"); done < <(changed_cs_files)

if [[ ${#files[@]} -eq 0 ]]; then
  echo "no changed .cs files in scope — nothing to format"
  exit 0
fi

for i in "${!PKG_DIR[@]}"; do
  for p in "${PKG_PROJ[$i]}" "${PKG_TEST[$i]}"; do
    pdir="$(dirname "$p")"; pdir="${pdir#$root/}"
    inc=()
    for rf in "${files[@]}"; do [[ "$rf" == "$pdir/"* ]] && inc+=("$rf"); done
    [[ ${#inc[@]} -eq 0 ]] && continue
    echo "== $p (${#inc[@]} file(s)) =="
    ( cd "$root" && dotnet format "$p" --severity warn --include "${inc[@]}" )
  done
done
