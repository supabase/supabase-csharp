# lib/report.sh — one row store and one renderer for both scopes.
#
# Every stage appends rows here and they stream as they are recorded, so progress
# is visible as the run proceeds. $MULTI selects the layout: a single package gets
# the compact one-line-per-stage form; a solution gets the phase-major form (a
# header per phase, an indented row per package). Same data, two renderings — so
# there is exactly one place a stage result is produced.

R_ID=(); R_NAME=(); R_KIND=(); R_PKG=(); R_STATUS=(); R_DETAIL=(); R_LOG=()
_LAST_PHASE=""

_badge() { case "$1" in
  PASS) printf '%s' "${GRN}PASS${OFF}" ;; FAIL) printf '%s' "${RED}FAIL${OFF}" ;;
  *)    printf '%s' "${YEL}SKIP${OFF}" ;; esac; }
_tag() { [[ "$1" == block ]] && printf '[B]' || printf '[s]'; }

emit_row() {  # emit_row <name> <kind> <pkg> <status> <detail>
  local b; b="$(_badge "$4")"
  if [[ $MULTI -eq 1 ]]; then
    if [[ "$1" != "$_LAST_PHASE" ]]; then
      _LAST_PHASE="$1"; printf '  %s %s\n' "$(_tag "$2")" "$1"
    fi
    if [[ -n "$3" ]]; then printf '        %s %-11s %s\n' "$b" "$3" "${DIM}$5${OFF}"
    else                   printf '        %s %s\n'       "$b"       "${DIM}$5${OFF}"; fi
  else
    printf '  %s %s %-28s %s\n' "$b" "$(_tag "$2")" "$1" "${DIM}$5${OFF}"
  fi
}

# add <id> <name> <kind> <pkg> <status> <detail> [log]
# id is the stable stage id (1a/1b/2/4/5a/5/7); name is the human phase label.
add() {
  R_ID+=("$1"); R_NAME+=("$2"); R_KIND+=("$3"); R_PKG+=("$4")
  R_STATUS+=("$5"); R_DETAIL+=("$6"); R_LOG+=("${7:-}")
  emit_row "$2" "$3" "$4" "$5" "$6"
}

# All recorded statuses for a stage id (there is one row per package, so a stage
# can have several). Used by the causal-skip checks.
statuses_of() { local i; for i in "${!R_ID[@]}"; do
  [[ "${R_ID[$i]}" == "$1" ]] && echo "${R_STATUS[$i]}"; done; }

esc() { sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' <<<"$1" | tr -d '\n'; }

write_report() {  # write_report <verdict> <exitCode>
  { echo '{'
    if [[ $MULTI -eq 1 ]]; then echo "  \"solution\": \"$(basename "$SLN")\", \"mode\": \"$MODE\","
    else                        echo "  \"package\": \"${PKG_NAME[0]}\", \"mode\": \"$MODE\","; fi
    echo "  \"logsRelativeTo\": \"<scope>\","
    echo "  \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\","
    echo "  \"verdict\": \"$1\", \"exitCode\": $2,"
    echo '  "stages": ['
    local i last=$(( ${#R_ID[@]} - 1 ))
    for i in "${!R_ID[@]}"; do
      printf '    { "id": "%s", "name": "%s", "package": "%s", "kind": "%s", "status": "%s", "detail": "%s", "log": "%s" }%s\n' \
        "${R_ID[$i]}" "$(esc "${R_NAME[$i]}")" "$(esc "${R_PKG[$i]}")" "${R_KIND[$i]}" "${R_STATUS[$i]}" \
        "$(esc "${R_DETAIL[$i]}")" "$(esc "${R_LOG[$i]#$SCOPE_DIR/}")" \
        "$([[ $i -lt $last ]] && echo ,)"
    done
    echo '  ]'; echo '}'
  } > "$SCOPE_OUT/report.json"
}

# The closing banner. One definition for both scopes.
print_verdict() {  # print_verdict <verdict> <exitCode>
  echo
  case "$1" in
    PASS)       echo "${GRN}${BLD}GATE: PASS${OFF} (exit 0) — blocking stages green; signal stages are the maintainer's call." ;;
    PARTIAL)    echo "${YEL}${BLD}GATE: PARTIAL${OFF} (exit 0) — fast mode, a subset of the gate. Re-run without --fast before a PR." ;;
    INCOMPLETE) echo "${YEL}${BLD}GATE: INCOMPLETE${OFF} (exit 2) — a blocking stage could not run. Unverified ≠ verified." ;;
    FAIL)       echo "${RED}${BLD}GATE: FAIL${OFF} (exit 1) — fix the cause, not the check: no #pragma disable, no"
                echo "severity downgrade, no re-categorising to dodge the filter, no deleting a red test." ;;
  esac
  echo "${DIM}report: $SCOPE_OUT/report.json${OFF}"
}
