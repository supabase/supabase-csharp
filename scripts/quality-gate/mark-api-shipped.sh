#!/usr/bin/env bash
#
# mark-api-shipped.sh — fold each package's PublicAPI.Unshipped.txt into its
# PublicAPI.Shipped.txt. The release counterpart to the gate's stage 5a: the gate
# only VERIFIES the declared surface matches the compiled one, it never moves an
# entry from unshipped to shipped. That move is a release event, and this is it.
#
#   mark-api-shipped.sh [dir]     apply to every package found under dir (default: cwd)
#   mark-api-shipped.sh [dir] --dry-run   report what would move, write nothing
#
# A "package" here is any directory holding BOTH PublicAPI.Shipped.txt and
# PublicAPI.Unshipped.txt (obj/ and bin/ copies are ignored). Every such directory
# under the given root is processed, so one run covers the whole solution.
#
# The transform, per package, mirrors the Roslyn PublicApiAnalyzers "mark shipped"
# code fix so the result still satisfies the gate:
#   - Lines in Unshipped prefixed *REMOVED* delete the named entry from Shipped
#     (the symbol left the surface); the marker itself is discarded.
#   - Every other Unshipped line is added to Shipped.
#   - Shipped is de-duplicated and ordinal-sorted (LC_ALL=C), matching the code
#     fix's output and the committed files' existing order.
#   - Unshipped is reset to just its `#nullable enable` header.
# A package whose Unshipped holds nothing but the header is a no-op.
#
# Exit codes:
#   0  success (including "nothing to ship")
#   1  a package could not be processed (e.g. unreadable file)
#   3  usage / environment error
#
set -euo pipefail

HEADER="#nullable enable"

# -------------------------------------------------------------------- options
ROOT="$PWD"; DRY_RUN=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) DRY_RUN=1; shift ;;
    -h|--help) sed -n '3,22p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    -*) echo "unknown option: $1  (try --help)" >&2; exit 3 ;;
    *)  ROOT="$1"; shift ;;
  esac
done
[[ -d "$ROOT" ]] || { echo "no such directory: $ROOT" >&2; exit 3; }

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  GRN=$'\033[32m'; YEL=$'\033[33m'; DIM=$'\033[2m'; BLD=$'\033[1m'; OFF=$'\033[0m'
else GRN=""; YEL=""; DIM=""; BLD=""; OFF=""; fi

# meaningful <file> — the file's lines minus the nullable header and blank lines.
meaningful() { grep -vx "$HEADER" -- "$1" 2>/dev/null | grep -v '^[[:space:]]*$' || true; }

MOVED=0; NOOP=0

# process_package <dir> — apply the transform to one package. Prints one summary
# line. Honours DRY_RUN (compute + report, write nothing).
process_package() {
  local dir="$1"
  local shipped="$dir/PublicAPI.Shipped.txt"
  local unshipped="$dir/PublicAPI.Unshipped.txt"
  local label="${dir#"$ROOT"/}"; [[ "$label" == "$dir" ]] && label="$(basename "$dir")"

  local un_body; un_body="$(meaningful "$unshipped")"
  if [[ -z "$un_body" ]]; then
    printf "  ${DIM}—    %-40s nothing to ship${OFF}\n" "$label"
    NOOP=$((NOOP+1)); return 0
  fi

  local removals additions
  removals="$(printf '%s\n' "$un_body" | grep '^\*REMOVED\*' | sed 's/^\*REMOVED\*//' || true)"
  additions="$(printf '%s\n' "$un_body" | grep -v '^\*REMOVED\*' || true)"

  # union(existing shipped, additions), drop the *REMOVED* targets, dedupe + sort.
  local combined new_body
  combined="$(printf '%s\n%s\n' "$(meaningful "$shipped")" "$additions" | grep -v '^[[:space:]]*$' || true)"
  if [[ -n "$removals" ]]; then
    new_body="$(printf '%s\n' "$combined" | grep -vxF -f <(printf '%s\n' "$removals") || true)"
  else
    new_body="$combined"
  fi
  new_body="$(printf '%s\n' "$new_body" | LC_ALL=C sort -u | grep -v '^[[:space:]]*$' || true)"

  local n_add n_rm
  n_add="$(printf '%s\n' "$additions" | grep -c . || true)"
  n_rm="$(printf '%s\n' "$removals" | grep -c . || true)"

  if [[ "$DRY_RUN" -eq 1 ]]; then
    printf "  ${YEL}would${OFF} %-40s +%s added, -%s removed\n" "$label" "$n_add" "$n_rm"
    MOVED=$((MOVED+1)); return 0
  fi

  { printf '%s\n' "$HEADER"; printf '%s\n' "$new_body"; } > "$shipped"
  printf '%s\n' "$HEADER" > "$unshipped"
  printf "  ${GRN}ship ${OFF} %-40s +%s added, -%s removed\n" "$label" "$n_add" "$n_rm"
  MOVED=$((MOVED+1))
}

# ----------------------------------------------------------------- discovery
# Process substitution (not a pipe) so the while loop runs in THIS shell and its
# MOVED/NOOP tallies survive. bash 3.2 has no mapfile, so avoid it.
found=0
while IFS= read -r un; do
  found=1
  dir="$(dirname "$un")"
  [[ -f "$dir/PublicAPI.Shipped.txt" ]] || {
    echo "  skip $dir — Unshipped present but no Shipped file" >&2; continue; }
  process_package "$dir"
done < <(find "$ROOT" -name 'PublicAPI.Unshipped.txt' \
  -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | LC_ALL=C sort)

if [[ "$found" -eq 0 ]]; then
  echo "no PublicAPI.Unshipped.txt found under $ROOT" >&2; exit 3
fi

echo
verb=$([[ "$DRY_RUN" -eq 1 ]] && echo "would ship" || echo "shipped")
echo "${BLD}mark-api-shipped: $verb $MOVED package(s), $NOOP with nothing to ship${OFF}"
