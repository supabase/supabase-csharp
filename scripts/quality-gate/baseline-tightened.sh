#!/usr/bin/env bash
#
# baseline-tightened.sh <before.json> <after.json> — exit 0 when the per-package
# .gate-baseline.json only got stricter, 1 when any measurement got looser.
#
# The gate reads the baseline it is given, so a PR that loosens one passes the
# gate it just weakened. This check closes that: the gate itself only ever moves
# a baseline toward stricter (warnings down, coverage up), so a looser value can
# only come from a hand edit.
#
#   before missing          a new package's first baseline: allowed.
#   after missing           a deleted baseline: the next run would re-establish it
#                           from whatever it measures, so this is a loosening.
#   package/project/...     identity fields must not change.
#   warnings.total          must not rise.
#   warnings.byCode.<code>  must not rise; a code absent before counted as 0.
#   coverage.hermeticLine   must not fall, and must not disappear.
#   updated                 free.
#
set -euo pipefail

[[ $# -eq 2 ]] || { sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 3; }
BEFORE="$1"; AFTER="$2"

command -v jq >/dev/null 2>&1 || { echo "jq not found" >&2; exit 3; }

[[ -f "$BEFORE" ]] || { echo "new baseline: nothing to compare"; exit 0; }
[[ -f "$AFTER" ]] || { echo "LOOSENED: baseline deleted"; exit 1; }

loosenings="$(jq -rn --slurpfile before "$BEFORE" --slurpfile after "$AFTER" '
  ($before[0]) as $b | ($after[0]) as $a
  | [
      ( ["schema", "package", "project", "testProject"][]
        | select($b[.] != null and $a[.] != $b[.])
        | "\(.) changed: \($b[.]) -> \($a[.])" ),
      ( select($b.warnings.total != null
               and (($a.warnings.total // infinite) > $b.warnings.total))
        | "warnings.total rose: \($b.warnings.total) -> \($a.warnings.total)" ),
      ( ($a.warnings.byCode // {}) | to_entries[]
        | select(.value > ($b.warnings.byCode[.key] // 0))
        | "warnings.byCode.\(.key) rose: \($b.warnings.byCode[.key] // 0) -> \(.value)" ),
      ( select($b.coverage.hermeticLine != null
               and (($a.coverage.hermeticLine // -1) < $b.coverage.hermeticLine))
        | "coverage.hermeticLine fell: \($b.coverage.hermeticLine) -> \($a.coverage.hermeticLine)" )
    ]
  | .[]
')"

if [[ -n "$loosenings" ]]; then
  while IFS= read -r line; do echo "LOOSENED: $line"; done <<<"$loosenings"
  exit 1
fi
echo "tightened or unchanged"
