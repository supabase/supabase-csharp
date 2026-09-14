---
name: sdk-quality-gate
description: Use before declaring any Supabase C# SDK change done or opening a PR, and as the Verify step of every flow. Runs the committed scripts/quality-gate/gate.sh script — the mechanized gauntlet (build/analyzers with a warning baseline, format on changed files, tests, a line-coverage baseline, vulnerability scan and E2E/acceptance tests, plus a public-API diff signal) — and reports its verdict. This is the deterministic "is it done" check; do not report a change as done without a PASS.
---

# Skill: SDK quality gate

The gauntlet is a program, not a procedure. Run it and report what it says.

The script lives at `scripts/quality-gate/gate.sh` — committed alongside the code
so contributors and CI run the identical command, not just agents. Paths below
assume the working directory is the workspace root (`sdk-csharp`); the script
itself is location-independent, so an absolute path works equally well.

```
scripts/quality-gate/gate.sh <package>          # full gate — before a PR
scripts/quality-gate/gate.sh <package> --fast    # inner loop only
```

There are two modes. `--fast` is the inner loop only (build, format, tests) — the
fast local red/green cycle; the default is the full gate and adds security, the
public-API check, E2E and a line-coverage baseline. `<package>` is a directory —
e.g. `gotrue-csharp`,
`core-csharp` — and defaults to the current directory. Run with `bash <path>` if
the executable bit is unset.

**Mutation testing is not part of the gate.** It is too slow for the inner/PR
loop and runs in its own scheduled GitHub Action.

**CI runs the same command.** The `Build and Test` workflow
(`.github/workflows/build-and-test.yml`) invokes `scripts/quality-gate/gate.sh`
on every push and pull request, so a local PASS and a CI PASS mean the same
thing. The blocking-stage verdict here is the one that gates the merge.

## Reading the verdict

| Verdict | Exit | Meaning |
|---|---|---|
| `PASS` | 0 | Blocking stages green. Report done; surface the signals. |
| `PARTIAL` | 0 | `--fast` only. **Not** sufficient to declare done. |
| `INCOMPLETE` | 2 | A blocking stage could not run. Unverified ≠ verified. |
| `FAIL` | 1 | A blocking stage failed. |

Blocking stages `[B]` decide the verdict: build/analyzers, format, tests,
line coverage, dependency vulnerabilities, public-API declared, **and
E2E/acceptance**. A failing E2E test blocks the merge exactly like a failing unit
test — there is no green build with a red test.

**Test execution differs by mode.** `--fast` runs the inner loop only
(`TestCategory!=E2E`) — no coverage measured, verdict is `PARTIAL`. The full gate,
when the local/CI Supabase stack is reachable, runs every test **in one
unfiltered pass** — unit, contract and E2E together, under stage id `2` — so a
red unit test no longer prevents E2E from executing in the same run (they either
both ran, or neither did). That same run is also the coverage source: `2b`
measures line coverage across the whole suite, not just the inner loop, so
"coverage" always means the full picture. **Stage `7` doesn't appear in the
report at all when this happens** — id `2`'s own per-package rows (labeled
"Tests (Unit + Contract + E2E)") already carry the full outcome, so a separate
E2E row would only restate it, and imprecisely at that (a package with zero E2E
tests has nothing to "fold in"). If the stack is unreachable, the gate falls back
to running the inner loop alone for local feedback, and both E2E (`7`) and
coverage (`2b`) SKIP with a reason — "full" coverage isn't measurable without the
E2E half of the suite, and a check that couldn't run must never read as passed.
Either way, a stack that can't be reached holds the gate at `INCOMPLETE`.

Dropped intentionally: a package with zero `[TestCategory("E2E")]` tests used to
get its own non-blocking signal row under `7` ("no E2E tests in this package").
Now that E2E runs folded into the unfiltered stage `2`, that distinction isn't
separately observable without a second test-discovery pass, so it's gone — not a
regression to chase, just a note so its absence isn't a surprise.

The one signal stage `[s]` is the **public-API diff**: it never fails the build,
because the maintainer is the merge gate on breaking changes — a break may be
intended. The tool informs that call, it does not veto it (QUALITY_RUBRIC §4).

Stages are skipped only when an earlier failure makes them impossible or
meaningless — a failed build blocks format, tests, E2E and coverage; a stack
that can't be reached blocks E2E and coverage specifically (tests still run via
the inner-loop fallback). Nothing else suppresses a stage, and anything not run
is still reported as `SKIP` with its reason.

## Reporting

Paste the summary table verbatim. Never paraphrase a stage result, and never
report a stage as passing when the script recorded `SKIP`. Detail is in
`<package>/.gate/report.json` and the per-stage logs it references.

Add the triage a number alone doesn't carry:

- **Public-API diff** — additive, or a break needing sign-off + a major plan +
  `[Obsolete]` + a `MIGRATION_vN.md` entry.

## On failure — fix the cause, not the check

Not acceptable repairs:

- `#pragma warning disable`, or lowering a severity in `.editorconfig`
- re-categorising a test so a filter skips it
- deleting or `[Ignore]`-ing a red test
- retrying or quarantining a flaky E2E — flakiness is a design defect
- hand-editing `.gate-baseline.json` the wrong way (warnings up, coverage down)
  to make a stage pass

If a blocking stage fails for a reason outside the change's scope, stop and
report it. Do not work around it and continue.

## Baselines

`<package>/.gate-baseline.json` is committed and holds the discovered project
paths, the warning count per code, and line coverage. The warning baseline only
moves *down* (fewer is better); coverage only *up* (more is better,
`coverage.hermeticLine`, a single percentage). Either direction, moving the number the
wrong way means editing the file by hand, so the regression appears in code review.

**Writing the baseline is opt-in.** A plain `gate.sh` run is read-only — it reads
the baseline to produce a verdict but never touches the file, so a contributor's
run can't silently rewrite a committed baseline. Pass `--overwrite-baseline` to
create it (a package's first run) or to update it. On CI this is a post-merge
maintainer concern: PR runs stay read-only, and only the push-to-master run
passes `--overwrite-baseline` and commits any improvement back to master.

**Coverage is compared with a ~1% tolerance band, not an exact match.** Even
hermetic (unit+contract) coverage isn't byte-reproducible on a loaded CI runner —
async continuations (retry/backoff `Task.Delay`, cancellation races, background
timers) get scheduled differently run to run, flipping a few covered lines. The
band absorbs that jitter symmetrically: a drop within it passes, a rise within it
doesn't move the baseline (so it never locks onto a lucky peak the next run can't
reach), and a real regression — an untested method, far more than 1% — still fails.

**Coverage has no ceiling.** It must always be at or above the best ever
recorded — never capped at, say, 95%, because a cap reopens exactly the
regression window the baseline exists to close: once at a cap, a large chunk of
fully untested new code could land without the aggregate dipping below it,
especially against a large existing denominator. If coverage nears 100% and the
remaining gaps are genuinely low-value to test (a guard clause, an unreachable
`default:` arm), the answer is `[ExcludeFromCodeCoverage]` on those specific
lines — not a gate-level threshold. That's a per-line, reviewed-in-diff opt-out;
a reviewer sees it and can push back, unlike a global cap that quietly loosens
the bar for everyone forever.

Optional keys: `formatBaseRef` (default: `origin/HEAD`, then `main`/`master`)
for the changed-file comparison, and `e2eHealthUrl` if the local stack isn't on
the default port.

When a `--overwrite-baseline` run reports `baseline lowered` or `baseline raised`,
commit the file. A read-only run (the default) never emits those — it won't have
written anything to commit.

## What the script cannot check

These stay human judgment. Walk them; don't recall them:

- [ ] Behavior asserted through the public surface; testability friction fixed by
      changing the design, not by exposing internals.
- [ ] Approved wire-shape snapshots are *right* — the contract tests only prove
      the current output still matches them, not that the approved bytes are correct.
- [ ] `CancellationToken` genuinely honored through the call path, not just
      accepted at the entry point.
- [ ] Orchestration reads as named intent; expected failures returned, not thrown.
- [ ] Public surface minimal and documented; entry points carry an `<example>`.
- [ ] Parity deviations noted in the PR description.
- [ ] Commits follow `type(scope): description`. The scope is required since the
      move to a monorepo and names the affected package — `functions`, `gotrue`,
      `postgrest`, `realtime`, `storage`, `core`, `supabase` (append `!` before
      the colon for breaking changes, e.g. `feat(functions)!:`).

Every item that becomes mechanizable — an analyzer, an architecture test, a
`TestConventions` rule — should move out of this list and into the script. The
list is meant to shrink.