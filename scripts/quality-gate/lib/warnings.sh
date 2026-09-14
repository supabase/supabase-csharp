# lib/warnings.sh — the analyzer warning baseline and its per-package IO.
#
# The per-package baseline holds MEASUREMENTS (warning counts) and the package's
# own project paths. Repo-wide settings (formatBaseRef, e2eHealthUrl) live in
# .gate-config.json; a package baseline may override any of them. The baseline only
# ever moves DOWN automatically — raising a number means editing the file by hand,
# so the increase shows up in code review.

WARN_TOTAL=""

# Split the raw build log into a package's de-duplicated warning list and a
# per-code count file, then read WARN_TOTAL. Project filters keep only warnings
# whose owning [<proj>.csproj] is one of them — that is how a single solution build
# is attributed back to each package. No filter = all warnings.
#
# Multi-targeting emits each warning once per TFM, hence the dedupe before counting.
parse_warnings() {  # parse_warnings <log> <list-out> <codes-out> [proj...]
  local log="$1" list="$2" codes="$3"; shift 3
  local grepf=(cat)
  if [[ $# -gt 0 ]]; then grepf=(grep -F); for p in "$@"; do grepf+=(-e "[$p]"); done; fi
  grep -E ': warning [A-Za-z]+[0-9]+:' "$log" 2>/dev/null | "${grepf[@]}" \
    | sed -E 's/ \[[^]]*\]$//; s/^[[:space:]]+//' | sort -u > "$list" || true
  WARN_TOTAL=$(wc -l < "$list" | tr -d ' ')
  grep -oE ': warning [A-Za-z]+[0-9]+:' "$list" 2>/dev/null \
    | sed -E 's/: warning ([A-Za-z]+[0-9]+):/\1/' | sort | uniq -c | sort -rn \
    | awk '{print $2" "$1}' > "$codes" || true
}

# Compare WARN_TOTAL and the per-code counts against $BASELINE. Echoes
# "STATUS|||detail". A rise in any single code fails even when the total is flat,
# which catches trading one class of warning for another.
warn_verdict() {  # warn_verdict <codes-file>
  local codes="$1" base risen="" was code n
  base="$(bget .warnings.total)"
  if [[ -z "$base" ]]; then
    echo "PASS|||$WARN_TOTAL warning(s) — baseline established by this run"; return
  fi
  if [[ -s "$codes" ]]; then
    while read -r code n; do
      [[ -n "$code" ]] || continue
      was="$(bget ".warnings.byCode.\"$code\"")"; was="${was:-0}"
      (( n > was )) && risen+="$code ${was}→${n}; "
    done < "$codes"
  fi
  if (( WARN_TOTAL > base )) || [[ -n "$risen" ]]; then
    echo "FAIL|||warnings ${base}→${WARN_TOTAL}${risen:+ (${risen%; })}"
  elif (( WARN_TOTAL < base )); then
    echo "PASS|||$WARN_TOTAL < baseline $base — lowering baseline"
  else
    echo "PASS|||$WARN_TOTAL, at baseline"
  fi
}

# Written whenever a measurement improves, or when the file doesn't exist yet.
# Reads $BASELINE/$PACKAGE_NAME/$PROJECT/$TEST_PROJECT/$PACKAGE_DIR/$OUT — all set
# by _use_pkg before this is called.
save_baseline() {
  local base_w; base_w="$(bget .warnings.total)"
  local w="${base_w:-$WARN_TOTAL}" why=""

  [[ -n "$WARN_TOTAL" && ( -z "$base_w" || WARN_TOTAL -lt base_w ) ]] && { w="$WARN_TOTAL"; why="warnings ${base_w:-–}→$WARN_TOTAL"; }
  [[ -f "$BASELINE" && -z "$why" ]] && return

  local codes="{}"
  [[ -s "$OUT/warnings-by-code.txt" ]] && codes=$(awk \
    '{printf "%s\"%s\":%s", (NR>1?",":""), $1, $2} END{print ""}' \
    "$OUT/warnings-by-code.txt" | sed 's/^/{/; s/$/}/')

  cat > "$BASELINE" <<EOF
{
  "schema": 1,
  "package": "$PACKAGE_NAME",
  "project": "${PROJECT#$PACKAGE_DIR/}",
  "testProject": "${TEST_PROJECT#$PACKAGE_DIR/}",
  "warnings": { "total": $w, "byCode": $codes },
  "updated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF
  if [[ -z "$base_w" ]]; then
    echo "${GRN}baseline created${OFF} $BASELINE ${DIM}— commit it${OFF}"
  else
    echo "${GRN}baseline lowered${OFF} ($why) ${DIM}— commit $BASELINE${OFF}"
  fi
}
