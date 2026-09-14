# lib/coverage.sh — the line-coverage baseline, mirroring lib/warnings.sh's shape
# but inverted: the warning baseline only moves DOWN (fewer is better), coverage
# only UP (more is better). Both directions share the same contract — the baseline
# only ever moves the right way automatically; moving it the wrong way needs a human
# hand-edit, so the regression shows up in code review.
#
# The baseline is scoped to HERMETIC tests only — unit + contract, no real I/O, no
# real clock, no live external process — never to E2E. A coverage delta is only
# evidence that a diff changed something when the run producing it is fully
# reproducible; E2E tests exist specifically to exercise real, live dependencies
# (containers, network, timing), so their execution path can legitimately vary
# run-to-run for reasons that have nothing to do with the code under test. Gating
# merges on a single-run E2E coverage delta gates on infrastructure noise, not on
# test discipline — this was verified directly, not assumed: the same commit run
# twice locally produced byte-identical hermetic coverage both times, while two
# real CI runs of identical, untouched package code showed multi-point swings
# wherever E2E contributed. E2E's correctness contract stays pass/fail
# (stage_tests_full, unchanged, still fully blocking) — its coverage contribution
# is still measured and reported alongside (stage_coverage's "full incl. E2E"
# figure), so E2E-only-covered code is never hidden, it just isn't what fails a PR.
#
# The source is a Cobertura XML written by coverlet.collector (already referenced
# by every test csproj) via `dotnet test --collect:"XPlat Code Coverage"`. Kept in
# its own file rather than folded into warnings.sh: that file is explicitly
# scoped to the analyzer baseline — a text-parsed build-log concern — while this is
# an XML-parsed test-artifact concern.

COVERAGE_EPSILON="1.0"    # percentage points. Absorbs CI run-to-run jitter, not
                          # just float rounding. Even hermetic (unit+contract)
                          # coverage isn't byte-reproducible on a loaded CI runner:
                          # async continuations — retry/backoff Task.Delay, cancellation
                          # races, background timers — get scheduled differently run to
                          # run, flipping a few lines either way (observed: the same
                          # commit alternating 613/615 of 1479 lines on Gotrue across
                          # identical master runs). 1% gives headroom over that noise
                          # while still failing a real regression — an untested method
                          # is far more than 1% of a package. Applied symmetrically
                          # (coverage_verdict fails below −ε; save_coverage_baseline
                          # raises only above +ε), so a lucky-high run never locks the
                          # baseline onto a peak the next run can't reach.

COV_PCT=""; COV_COVERED=""; COV_VALID=""

# parse_coverage <cobertura-xml> — sets COV_PCT/COV_COVERED/COV_VALID from the
# root <coverage> element's lines-covered/lines-valid attributes. Raw counts, not
# the precomputed line-rate: this keeps rounding fully in our control, which the
# epsilon comparison in coverage_verdict needs. `grep -m1` assumes those
# attributes appear only on the root element in coverlet's Cobertura output (not
# also on nested <package>/<class> elements, which carry only line-rate/
# branch-rate) — verified against coverlet.collector's actual output before
# relying on this in production; if a future coverlet version nests them too,
# anchor the grep to the text before the first "<package" tag instead.
parse_coverage() {
  local xml="$1" covered valid
  covered="$(grep -m1 -oE 'lines-covered="[0-9]+"' "$xml" 2>/dev/null | grep -oE '[0-9]+')"
  valid="$(grep -m1 -oE 'lines-valid="[0-9]+"' "$xml" 2>/dev/null | grep -oE '[0-9]+')"
  if [[ -z "$covered" || -z "$valid" || "$valid" -eq 0 ]]; then
    COV_PCT=""; COV_COVERED=""; COV_VALID=""; return 1
  fi
  COV_COVERED="$covered"; COV_VALID="$valid"
  COV_PCT="$(awk -v c="$covered" -v v="$valid" 'BEGIN{printf "%.2f", (c/v)*100}')"
}

# Compare $COV_PCT against $BASELINE's coverage.hermeticLine. Echoes
# "STATUS|||detail". No ceiling: any measured decrease beyond the epsilon fails,
# no matter how high the baseline already is — a capped baseline would let a large
# chunk of untested new code land, once at the cap, without the aggregate dipping
# below it.
coverage_verdict() {
  local base; base="$(bget .coverage.hermeticLine)"
  if [[ -z "$base" ]]; then
    echo "PASS|||${COV_PCT}% line coverage (${COV_COVERED}/${COV_VALID}) — baseline established by this run"
    return
  fi
  local cmp; cmp="$(awk -v c="$COV_PCT" -v b="$base" -v e="$COVERAGE_EPSILON" \
    'BEGIN{d=c-b; if(d<0)d=-d; print (d<=e)?"eq":(c>b?"up":"down")}')"
  case "$cmp" in
    down) echo "FAIL|||line coverage ${base}%→${COV_PCT}% — regression (${COV_COVERED}/${COV_VALID} lines)" ;;
    up)   echo "PASS|||line coverage ${base}%→${COV_PCT}% — raising baseline" ;;
    *)    echo "PASS|||${COV_PCT}%, at baseline" ;;
  esac
}

# Written whenever $COV_PCT improves on the baseline beyond the epsilon, or when
# the baseline has no coverage key yet. Uses a targeted jq merge — touching only
# .coverage.hermeticLine — rather than reconstructing the whole file from a
# heredoc the way save_baseline (warnings) does: that approach only knows about
# schema/package/project/testProject/warnings/updated and silently drops any
# other key (mutationScore, e2eHealthUrl, formatBaseRef) whenever it fires. Since
# save_baseline already ran earlier in the same gate.sh invocation (stage_build),
# a merge here avoids stomping whatever it just wrote.
#
# Must be called while $COV_PCT/$COV_COVERED/$COV_VALID still reflect the
# HERMETIC report — stage_coverage re-parses the full (incl. E2E) report
# afterward, for display only, which overwrites these globals.
save_coverage_baseline() {
  local base; base="$(bget .coverage.hermeticLine)"
  [[ -n "$COV_PCT" ]] || return 0
  if [[ -z "$base" ]] || awk -v c="$COV_PCT" -v b="$base" -v e="$COVERAGE_EPSILON" 'BEGIN{exit !(c>b+e)}'; then
    jq --argjson line "$COV_PCT" '.coverage.hermeticLine = $line' "$BASELINE" > "$BASELINE.tmp" && mv "$BASELINE.tmp" "$BASELINE"
    echo "${GRN}baseline raised${OFF} (coverage ${base:-–}%→$COV_PCT%) ${DIM}— commit $BASELINE${OFF}"
  fi
}
