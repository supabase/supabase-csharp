# lib/stages.sh — the gauntlet, one definition per stage (QUALITY_RUBRIC §4).
#
# Every stage is written ONCE and runs for both scopes. The scope is carried in a
# handful of globals set by discovery in gate.sh:
#
#   PKG_DIR/PKG_NAME/PKG_PROJ/PKG_TEST[]  the package(s) — one entry, or many
#   BUILD_TARGET   what `dotnet build` compiles: a package's test project
#                  (fast, single scope) or the .sln (solution scope)
#   SCAN_TARGET    what security + api-declared inspect: the production project or
#                  the .sln
#   SCOPE_DIR      the git / PublicAPI / stack root (package dir, or repo root)
#
# Per-package stages (build attribution, tests, e2e) loop over the arrays; the
# genuinely scope-wide stages (security, api-declared, api-diff) run once.

# Bind the per-package context ($PROJECT, $TEST_PROJECT, $BASELINE, $OUT, …) that
# warn_verdict / save_baseline / the loggers read.
_use_pkg() {  # _use_pkg <index>
  local i="$1"
  PACKAGE_DIR="${PKG_DIR[$i]}"; PACKAGE_NAME="${PKG_NAME[$i]}"
  PROJECT="${PKG_PROJ[$i]}"; TEST_PROJECT="${PKG_TEST[$i]}"
  BASELINE="$PACKAGE_DIR/.gate-baseline.json"
  OUT="$PACKAGE_DIR/.gate"; LOGS="$OUT/logs"; mkdir -p "$LOGS"
}

no_tests_matched() { grep -qi 'no test matches the given testcase filter' "$1" 2>/dev/null; }

first_failure() {  # first failed test name, for an actionable summary line
  grep -oE '^\s*(Failed|X) [A-Za-z0-9_.]+' "$1" 2>/dev/null \
    | head -n1 | sed -E 's/^\s*(Failed|X) //' || true
}

# ============================================================ 1a  build + baseline
# --no-incremental is load-bearing: on an incremental build, up-to-date projects
# emit no warnings, so the count reads zero and the baseline check passes on a
# change that added ten.
BUILD_OK=0
stage_build() {
  local log="$SCOPE_LOGS/1a-build.log"
  run "$log" dotnet build "$BUILD_TARGET" -c Release --no-incremental -v n -nologo
  if [[ $RC -ne 0 ]]; then
    add 1a "Build (analyzers)" block "" FAIL "build failed (exit $RC)" "$log"; BUILD_OK=0; return
  fi
  BUILD_OK=1
  # Attribute the build's warnings to each package by its OWN two csprojs: building
  # pulls in dependency packages whose warnings are emitted under their own csproj;
  # counting those would make a package carry its dependencies' debt.
  local i v
  for i in "${!PKG_DIR[@]}"; do
    _use_pkg "$i"
    parse_warnings "$log" "$OUT/warnings.txt" "$OUT/warnings-by-code.txt" "$PROJECT" "$TEST_PROJECT"
    v="$(warn_verdict "$OUT/warnings-by-code.txt")"
    add 1a "Build (analyzers)" block "${PKG_NAME[$i]}" "${v%%|||*}" "${v#*|||}" "$log"
    # Read-only unless --overwrite-baseline: a PR/local run reads the baseline for
    # its verdict but never writes it (updating it is a post-merge maintainer step).
    if [[ "${OVERWRITE_BASELINE:-0}" -eq 1 ]]; then save_baseline; fi
  done
  # Restore scope for the scope-wide helpers below; a per-package baseline must not
  # leak into cget for the solution-wide format/api stages.
  BASELINE=""
}

# ============================================================ 1b  format + naming
# dotnet build does not enforce naming (IDE1006) even with EnforceCodeStyleInBuild;
# dotnet format is the naming gate, which is why it is a separate required stage.
# Scoped to CHANGED files (QUALITY_RUBRIC §4): running it project-wide would fail a
# debt-carrying package on legacy code the change never touched.

default_base_ref() {
  # Override for callers that already know the exact commit to diff against —
  # e.g. format-master.yml, which runs ON master post-merge, where every one of
  # origin/HEAD/main/master below IS the current commit (a no-op diff). Not read
  # from .gate-config.json: this is a per-invocation override, not repo config.
  [[ -n "${GATE_FORMAT_BASE_REF:-}" ]] && { echo "$GATE_FORMAT_BASE_REF"; return; }
  local r; r="$(cget .formatBaseRef)"
  [[ -n "$r" ]] && { echo "$r"; return; }
  for c in origin/HEAD origin/main origin/master main master; do
    git -C "$SCOPE_DIR" rev-parse --verify -q "$c" >/dev/null 2>&1 && { echo "$c"; return; }
  done
  echo ""
}

changed_cs_files() {  # repo-RELATIVE paths, committed + staged + unstaged + untracked
  local root base scope_rel
  root="$(git -C "$SCOPE_DIR" rev-parse --show-toplevel 2>/dev/null)" || return 1
  base="$(default_base_ref)"
  # Only files inside the gated scope are this run's concern. The diff is taken from
  # the repo root (so paths are repo-relative and route to their project), but a run
  # over `packages` must not be failed by a changed .cs that lives in the gate's own
  # fixtures, in scripts/, or in any tree the invocation does not gate. scope_rel is
  # the scope as a repo-relative prefix ("" when the scope IS the repo root, i.e. no
  # filtering — every discovered project is under root anyway).
  #
  # The fixtures exclusion below is unconditional, not folded into that scope_rel
  # filter: a bare `gate.sh` run (scope_rel="") covers the whole repo, and a changed
  # .cs planted under scripts/quality-gate/fixtures/*/overlay never belongs to any
  # discovered package (those overlay fragments aren't complete, buildable projects)
  # — left unfiltered, such a file can never be attributed to a package's format
  # check, and if it's the only changed .cs file, that reads as "matched 0 changed
  # files — nothing was inspected", a false FAIL on code that isn't gated at all.
  scope_rel="${SCOPE_DIR#"$root"}"; scope_rel="${scope_rel#/}"
  {
    [[ -n "$base" ]] && git -C "$root" diff --name-only --diff-filter=ACMR "$base"...HEAD -- '*.cs' 2>/dev/null
    git -C "$root" diff --name-only --diff-filter=ACMR HEAD -- '*.cs' 2>/dev/null
    git -C "$root" ls-files --others --exclude-standard -- '*.cs' 2>/dev/null
  } | sed '/^$/d' \
    | grep -v '^scripts/quality-gate/fixtures/' \
    | awk -v s="$scope_rel" '{ if (s == "" || index($0, s "/") == 1) print }' \
    | sort -u
}

# `dotnet format --include` resolves paths against the *process* cwd and silently
# matches nothing when given an absolute path — which exits 0 and reads as clean.
# So: run from the git root and pass repo-relative paths. Includes are routed to
# the project that owns them, and the stage fails if nothing was inspected, because
# "inspected nothing" must never report as "clean".
stage_format() {
  local log="$SCOPE_LOGS/1b-format.log" root
  # --bypass-format (PR-time CI only, see gate.sh): report as a signal, not a
  # blocking stage. A human contributor can't always fix a violation themselves
  # the way an agent can; master gets auto-formatted post-merge regardless (see
  # .github/workflows/format-master.yml), so this run's verdict shouldn't block
  # on it. Local/agent runs never set this — format stays blocking for them.
  local kind="block"; [[ "${BYPASS_FORMAT:-0}" -eq 1 ]] && kind="signal"
  if ! root="$(git -C "$SCOPE_DIR" rev-parse --show-toplevel 2>/dev/null)"; then
    add 1b "Format + naming" "$kind" "" SKIP "not a git repository — cannot scope to changed files" ""
    return
  fi
  local files=() rel
  while IFS= read -r rel; do [[ -n "$rel" && -f "$root/$rel" ]] && files+=("$rel"); done < <(changed_cs_files)
  if [[ ${#files[@]} -eq 0 ]]; then
    add 1b "Format + naming" "$kind" "" PASS "no changed .cs files in the gated scope" ""; return
  fi

  : > "$log"
  local i p pdir inc rf rc pkg_matched pkg_fail any=0
  for i in "${!PKG_DIR[@]}"; do
    pkg_matched=0; pkg_fail=0
    for p in "${PKG_PROJ[$i]}" "${PKG_TEST[$i]}"; do
      pdir="$(dirname "$p")"; pdir="${pdir#$root/}"
      inc=()
      for rf in "${files[@]}"; do [[ "$rf" == "$pdir/"* ]] && inc+=("$rf"); done
      [[ ${#inc[@]} -eq 0 ]] && continue
      pkg_matched=$(( pkg_matched + ${#inc[@]} )); any=1
      echo "== $p (${#inc[@]} file(s)) ==" >> "$log"
      set +e
      ( cd "$root" && dotnet format "$p" --verify-no-changes --severity warn --include "${inc[@]}" ) >>"$log" 2>&1
      rc=$?
      set -e
      [[ $rc -ne 0 ]] && pkg_fail=1
    done
    [[ $pkg_matched -eq 0 ]] && continue     # this package holds none of the changed files
    if [[ $pkg_fail -eq 0 ]]; then
      add 1b "Format + naming" "$kind" "${PKG_NAME[$i]}" PASS "$pkg_matched changed file(s) clean" "$log"
    else
      add 1b "Format + naming" "$kind" "${PKG_NAME[$i]}" FAIL \
        "$pkg_matched changed file(s) with violations — fix: (cd $root && dotnet format <proj> --include <files>)" "$log"
    fi
  done

  # Includes that matched no gated project mean nothing was inspected — never clean.
  # An explicit `if`, not `[[ ]] && …`: as a function's last command that idiom
  # returns non-zero when the test is false, and under `set -e` a stage returning
  # non-zero would abort the run before the report prints.
  if [[ $any -eq 0 ]]; then
    add 1b "Format + naming" "$kind" "" FAIL \
      "matched 0 of ${#files[@]} changed .cs file(s) — none fall under a gated project; nothing was inspected" "$log"
  fi
  return 0
}

# ============================================================= 2  inner loop
# TestCategory!=E2E also admits uncategorised tests deliberately: the
# TestConventions guardrails fail on anything uncategorised, so it surfaces as a
# red test rather than being silently filtered away.
stage_inner_loop() {
  local i log sum ff
  for i in "${!PKG_DIR[@]}"; do
    _use_pkg "$i"
    log="$LOGS/2-inner-loop.log"
    run "$log" dotnet test "$TEST_PROJECT" -c Release --no-build --filter "TestCategory!=E2E" -v minimal
    sum="$(grep -E '^(Passed!|Failed!)' "$log" 2>/dev/null | tail -n1 || true)"
    # An empty run is checked BEFORE the exit code: `dotnet test` exits 0 when the
    # filter matches no tests on current SDKs, so a zero exit with zero tests would
    # otherwise record PASS "green" — a stage that executed nothing reading as passed.
    if no_tests_matched "$log"; then
      add 2 "Inner loop (Unit + Contract)" block "${PKG_NAME[$i]}" FAIL \
        "no tests matched TestCategory!=E2E — the package has no inner loop" "$log"
    elif [[ $RC -eq 0 ]]; then
      add 2 "Inner loop (Unit + Contract)" block "${PKG_NAME[$i]}" PASS "${sum:-green}" "$log"
    else
      ff="$(first_failure "$log")"
      add 2 "Inner loop (Unit + Contract)" block "${PKG_NAME[$i]}" FAIL \
        "${sum:-tests failed}${ff:+ — first: $ff}" "$log"
    fi
  done
}

# ================================================================= 4  security
# Trap: `dotnet list package --vulnerable` exits 0 even when it finds
# vulnerabilities — the exit code is worthless, the text has to be parsed. Clean
# requires POSITIVE confirmation ("no vulnerable packages"), never merely the
# absence of the findings line: if the tool's output drifts, absence would read as
# clean. It needs network — offline is SKIP, never PASS.
#
# This is a SIGNAL, not a blocking stage: a vulnerable dependency may have no
# fixed version available, and blocking the merge on something we cannot act on
# would wedge every PR for a defect that is not the PR's to fix. A finding is
# surfaced for the maintainer to weigh — bump the dependency when a fix exists,
# accept and track it when one doesn't — not enforced by the gate's exit code.
stage_security() {
  local log="$SCOPE_LOGS/4-security.log"
  run "$log" dotnet list "$SCAN_TARGET" package --vulnerable --include-transitive
  if grep -qiE 'unable to load the service index|could not resolve|failed to retrieve' "$log"; then
    add 4 "Dependency vulnerabilities" signal "" SKIP "no network — scan not performed" "$log"
  elif [[ $RC -ne 0 ]]; then
    add 4 "Dependency vulnerabilities" signal "" SKIP "command failed (exit $RC)" "$log"
  elif grep -qi 'has the following vulnerable packages' "$log"; then
    add 4 "Dependency vulnerabilities" signal "" FAIL \
      "$(awk '/^[[:space:]]+> /{n++} END{print n+0}' "$log") vulnerable package(s)" "$log"
  elif grep -qi 'no vulnerable packages' "$log"; then
    add 4 "Dependency vulnerabilities" signal "" PASS "none known" "$log"
  else
    add 4 "Dependency vulnerabilities" signal "" SKIP \
      "scan produced no recognizable result — neither confirmation nor findings (see log)" "$log"
  fi
}

# ========================================================= 5a  public API declared
# Blocking. The compile pins PublicApiAnalyzers diagnostics to "suggestion" so
# adding a public member never breaks the inner loop; enforcement lives here
# instead: `dotnet format` re-derives the surface and its exit code says whether
# the committed PublicAPI.*.txt still match.
#
# TRAP: `--diagnostics` takes SPACE-separated ids. Comma-separated ("RS0016,RS0017")
# is read as one unknown id — matches nothing, fixes nothing, exits 0 (false "in
# sync"). Do not "tidy" the spaces into commas. Exit 0 (in sync), 2 (out of sync),
# anything else (could not run) -> SKIP, because "couldn't check" is never "declared".
stage_api_sync() {
  local log="$SCOPE_LOGS/5a-api-sync.log"
  if [[ "$SCAN_TARGET" == *.csproj ]] && ! grep -qi 'PublicApiAnalyzers' "$SCAN_TARGET" 2>/dev/null; then
    add 5a "Public API declared" block "" SKIP \
      "PublicApiAnalyzers not referenced by $(basename "$SCAN_TARGET") — surface is untracked" ""; return
  fi
  if ! find "$SCOPE_DIR" -name 'PublicAPI.*.txt' -not -path '*/obj/*' 2>/dev/null | grep -q .; then
    add 5a "Public API declared" block "" SKIP \
      "no PublicAPI.*.txt under scope — nothing to verify against" ""; return
  fi
  run "$log" dotnet format analyzers "$SCAN_TARGET" --verify-no-changes --severity info --diagnostics RS0016 RS0017
  case $RC in
    0) add 5a "Public API declared" block "" PASS "PublicAPI.*.txt matches the compiled surface" "$log" ;;
    2) add 5a "Public API declared" block "" FAIL \
         "undeclared or stale public surface — sync + commit: dotnet format analyzers <proj> --severity info --diagnostics RS0016 RS0017" "$log" ;;
    *) add 5a "Public API declared" block "" SKIP "dotnet format could not run (exit $RC) — see log" "$log" ;;
  esac
}

# ============================================================== 5  public API diff
# Signal, not blocking: the maintainer is the merge gate on breaks and the tool
# informs that call rather than vetoing it (QUALITY_RUBRIC §4).
stage_api_diff() {
  local log="$SCOPE_LOGS/5-api-diff.log" root
  root="$(git -C "$SCOPE_DIR" rev-parse --show-toplevel 2>/dev/null)" || {
    add 5 "Public API diff" signal "" SKIP "not a git repository" ""; return; }
  find "$SCOPE_DIR" -name 'PublicAPI.*.txt' -not -path '*/obj/*' 2>/dev/null | grep -q . || {
    add 5 "Public API diff" signal "" SKIP "PublicApiAnalyzers not wired — surface changes unreviewed" ""; return; }

  local base; base="$(default_base_ref)"
  set +e
  { [[ -n "$base" ]] && git -C "$root" diff --unified=0 "$base"...HEAD -- '*PublicAPI.*.txt'
    git -C "$root" diff --unified=0 HEAD -- '*PublicAPI.*.txt'; } > "$log" 2>&1
  set -e

  # API entries only: drop diff headers and the #nullable marker line.
  local added removed na=0 nr=0
  added=$(grep -E '^\+[^+]' "$log" 2>/dev/null | grep -v '#nullable' | sed 's/^+//' || true)
  removed=$(grep -E '^-[^-]' "$log" 2>/dev/null | grep -v '#nullable' | sed 's/^-//' || true)
  [[ -n "$added" ]]   && na=$(printf '%s\n' "$added"   | wc -l | tr -d ' ')
  [[ -n "$removed" ]] && nr=$(printf '%s\n' "$removed" | wc -l | tr -d ' ')

  if [[ "$nr" -gt 0 ]]; then
    { echo; echo "REMOVED (breaking):"; printf '%s\n' "$removed" | sed 's/^/  /'; } >> "$log"
    add 5 "Public API diff" signal "" FAIL \
      "$na added, $nr REMOVED — breaking: needs sign-off + [Obsolete] + MIGRATION_vN.md; first: $(printf '%s' "$removed" | head -n1)" "$log"
  elif [[ "$na" -gt 0 ]]; then
    add 5 "Public API diff" signal "" PASS "$na member(s) added, additive" "$log"
  else
    add 5 "Public API diff" signal "" PASS "no public surface change" "$log"
  fi
}

# ================================================================ stack probe
# Two ways to answer "is the stack up", in order of authority: `supabase status`
# from the dir holding supabase/config.toml (searched upward from the scope), then
# a probe of the endpoint the tests use — any HTTP status proves something is
# listening; only a failed connection means down. Called once from gate.sh
# (cached in $STACK_OK) to decide both which test path to run and which SKIP
# markers to emit, rather than probing twice.
supabase_root() {
  local d="$SCOPE_DIR"
  while [[ "$d" != "/" && -n "$d" ]]; do
    [[ -f "$d/supabase/config.toml" ]] && { echo "$d"; return 0; }
    d="$(dirname "$d")"
  done
  return 1
}

e2e_url() { local u; u="$(cget .e2eHealthUrl)"; echo "${u:-http://127.0.0.1:54321/auth/v1/health}"; }

STACK_CHECK=""   # how the call was made, so a false negative is diagnosable
stack_up() {
  local root
  if have supabase && root="$(supabase_root)"; then
    STACK_CHECK="supabase status in $root"
    ( cd "$root" && supabase status ) >"$SCOPE_LOGS/7-stack.log" 2>&1 && return 0 || return 1
  fi
  local url; url="$(e2e_url)"
  STACK_CHECK="probe of $url"
  if have curl; then
    local code
    code=$(curl -sS -m 3 -o /dev/null -w '%{http_code}' "$url" 2>/dev/null) || code=""
    [[ "$code" =~ ^[1-5][0-9][0-9]$ ]] && return 0 || return 1
  fi
  local hp="${url#*://}"; hp="${hp%%/*}"
  local host="${hp%%:*}" port="${hp##*:}"
  [[ "$port" == "$host" ]] && port=80
  (exec 3<>"/dev/tcp/$host/$port") >/dev/null 2>&1 && return 0 || return 1
}

# Coverlet's VSTest collector instruments every assembly it can find in the test
# output dir by default — every ProjectReference along with the package under
# test, not just the package itself. Left unscoped, a package's "own" coverage
# report silently includes its dependencies' code too: a shared dependency (e.g.
# Core) gaining new, legitimately-uncovered-from-here lines then drags down every
# consumer's number, for a reason that has nothing to do with that consumer's own
# code or tests. That dependency's coverage is already tracked on its own row —
# counting it a second time, inside an unrelated package's number, is simply
# wrong, not just noisy. `;Include=[<AssemblyName>]*` scopes the collector to
# exactly the module under test. Confirmed necessary and sufficient directly: on
# a branch that only changes Core, every *downstream* package's row FAILed with
# multi-point regressions before this filter, and the newly-added Core classes
# (0% covered, as expected — they're exercised by Core's own tests) were exactly
# the extra lines showing up inside e.g. Functions' report. With the filter, that
# report contains only Functions' own classes.
pkg_assembly_name() {  # pkg_assembly_name <production-csproj>
  local n; n="$(grep -oE '<AssemblyName>[^<]+</AssemblyName>' "$1" 2>/dev/null | head -n1 | sed -E 's/<[^>]+>//g')"
  [[ -n "$n" ]] && echo "$n" || basename "$1" .csproj
}

# ============================================================ 2  tests (full)
# Full mode, stack reachable: one UNFILTERED dotnet test run per package — every
# TestCategory, unit/contract/E2E together — instead of the two disjoint filtered
# runs (inner loop, then E2E-if-green) that --fast still uses. This is what makes
# "full coverage" (unit+contract+E2E, not just the inner loop) measurable, and it
# is the single source of test-correctness truth: this row's PASS/FAIL is what
# blocks on a red test, E2E included.
#
# TRADEOFF, deliberate: a red unit test no longer blocks E2E from running in the
# same pass (previously E2E was "worth minutes only when the inner loop is
# green"). Both categories execute together now; the row FAILs if either does.
stage_tests_full() {
  local i log sum ff resdir hermetic_log hermetic_dir asm
  for i in "${!PKG_DIR[@]}"; do
    _use_pkg "$i"
    asm="$(pkg_assembly_name "$PROJECT")"
    log="$LOGS/2-tests.log"; resdir="$LOGS/coverage"; rm -rf "$resdir"
    run "$log" dotnet test "$TEST_PROJECT" -c Release --no-build \
      --collect:"XPlat Code Coverage;Include=[$asm]*" --results-directory "$resdir" -v minimal
    sum="$(grep -E '^(Passed!|Failed!)' "$log" 2>/dev/null | tail -n1 || true)"
    if no_tests_matched "$log"; then
      add 2 "Tests (Unit + Contract + E2E)" block "${PKG_NAME[$i]}" FAIL \
        "no tests matched — the package has no tests at all" "$log"
    elif [[ $RC -eq 0 ]]; then
      add 2 "Tests (Unit + Contract + E2E)" block "${PKG_NAME[$i]}" PASS "${sum:-green}" "$log"
    else
      ff="$(first_failure "$log")"
      add 2 "Tests (Unit + Contract + E2E)" block "${PKG_NAME[$i]}" FAIL \
        "${sum:-tests failed}${ff:+ — first: $ff}" "$log"
    fi

    # A second, filtered pass sources the coverage baseline below from a hermetic
    # (unit+contract-only) report — see lib/coverage.sh for why the coverage baseline
    # must not read the unfiltered run above. Not a correctness check of its own: the
    # same tests already ran, and any failure already surfaced, in the pass just
    # above — this run exists only to produce a reproducible Cobertura report.
    hermetic_log="$LOGS/2c-hermetic-coverage.log"; hermetic_dir="$LOGS/coverage-hermetic"; rm -rf "$hermetic_dir"
    run "$hermetic_log" dotnet test "$TEST_PROJECT" -c Release --no-build --filter "TestCategory!=E2E" \
      --collect:"XPlat Code Coverage;Include=[$asm]*" --results-directory "$hermetic_dir" -v minimal
  done
}

# =========================================================== 2b  coverage
# Blocking, but scoped to the HERMETIC (unit+contract) report the filtered pass
# above just produced — never dotnet test itself. That report only exists when
# stage_tests_full ran, so this must only ever be called when it did; gate.sh
# emits SKIP markers directly (never calling this function) for the build-failed
# and stack-down cases, same causal-skip pattern as every other stage: a check
# that could not run must never report as passed.
#
# The full (unit+contract+E2E) report is also read here, but only to append an
# informational figure to the detail string — never to gate the verdict or move
# the baseline. See lib/coverage.sh's header for why: E2E is non-hermetic by
# design, so its coverage contribution is real and worth showing, just not a
# reproducible enough signal to block a merge on.
stage_coverage() {
  local i cov cov_full log detail v
  for i in "${!PKG_DIR[@]}"; do
    _use_pkg "$i"
    log="$LOGS/2c-hermetic-coverage.log"
    cov="$(find "$LOGS/coverage-hermetic" -name 'coverage.cobertura.xml' 2>/dev/null | head -n1)"
    if [[ -z "$cov" || ! -s "$cov" ]]; then
      add 2b "Coverage (line, unit+contract)" block "${PKG_NAME[$i]}" SKIP "no coverage report produced — see log" "$log"
      continue
    fi
    if ! parse_coverage "$cov"; then
      add 2b "Coverage (line, unit+contract)" block "${PKG_NAME[$i]}" SKIP "0 instrumentable lines — nothing to measure" "$log"
      continue
    fi
    v="$(coverage_verdict)"
    # Read-only unless --overwrite-baseline (see stage_build). Guarded here too so a
    # PR/local run's verdict never has the side effect of raising the coverage baseline.
    # while COV_* still reflects the hermetic report parsed above:
    if [[ "${OVERWRITE_BASELINE:-0}" -eq 1 ]]; then save_coverage_baseline; fi

    detail="${v#*|||}"
    cov_full="$(find "$LOGS/coverage" -name 'coverage.cobertura.xml' 2>/dev/null | head -n1)"
    if [[ -n "$cov_full" && -s "$cov_full" ]] && parse_coverage "$cov_full"; then
      detail="$detail · full incl. E2E: ${COV_PCT}% (${COV_COVERED}/${COV_VALID})"
    fi
    add 2b "Coverage (line, unit+contract)" block "${PKG_NAME[$i]}" "${v%%|||*}" "$detail" "$LOGS/2-tests.log"
  done
}
