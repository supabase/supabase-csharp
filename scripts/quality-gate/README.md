# Quality gate

`gate.sh` is the project's "is this change done" check, as a single program. It runs
the same gauntlet locally and in CI — build, tests, coverage, dependency scan, public
API checks, E2E — collects a row per stage, and prints one verdict with a matching exit
code. Nothing about the verdict is subjective: every stage is a command whose output is
parsed into PASS / FAIL / SKIP.

The guiding rule: **a check that could not run is never reported as passed.** A failed
build, a down stack, a missing tool — these produce SKIP, not PASS, and SKIP makes the
run INCOMPLETE rather than green.

## Running it

```sh
./scripts/quality-gate/gate.sh packages          # full gate over the solution
./scripts/quality-gate/gate.sh packages/Gotrue   # one package
./scripts/quality-gate/gate.sh . --fast          # inner loop only (fast red/green cycle)
./scripts/quality-gate/gate.sh --help
```

The argument is a package directory, a `.csproj`, or a directory holding several
packages. A directory with more than one test project runs as a solution: one build,
one scan, one API check, per-package test and coverage rows, one verdict. A single
package keeps the fast per-package path. `--all` forces the solution run.

Requirements: `dotnet`, `jq`, and `git`. For the E2E stage, a running Supabase stack
(`supabase start`); without it, E2E and coverage SKIP and the run is INCOMPLETE.

## Stages

| id | Stage                      | Kind   | What it checks                                                                                                                                        |
|----|----------------------------|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| 1a | Build (analyzers)          | block  | `dotnet build -c Release --no-incremental`. Warning count per package must not exceed its baseline.                                                   |
| 1b | Format + naming            | block¹ | `dotnet format --verify-no-changes` on **changed** files only (whitespace + IDE1006 naming).                                                          |
| 2  | Inner loop / Tests         | block  | `--fast`: unit + contract (`TestCategory!=E2E`). Full: one unfiltered run — unit + contract + E2E together. A run that executed **zero** tests FAILs. |
| 2b | Coverage (line)            | block  | Line coverage of the hermetic (unit+contract) run must not drop below baseline. Full-suite coverage is shown for info, not gated.                     |
| 3  | Mutation (new survivors)   | block² | Reads a Stryker report and FAILs on a surviving mutant **on a changed line**. See [Mutation](#mutation-testing).                                      |
| 4  | Dependency vulnerabilities | signal | `dotnet list package --vulnerable`. Reported, never blocks — a vuln may have no fix available.                                                        |
| 5a | Public API declared        | block  | Committed `PublicAPI.*.txt` must match the compiled public surface.                                                                                   |
| 5  | Public API diff            | signal | Flags added/removed public members; a **removal** is a breaking change needing sign-off.                                                              |
| 7  | E2E / acceptance           | block  | Runs as part of stage 2 in full mode (no separate row).                                                                                               |

¹ Blocking by default; `--bypass-format` demotes it to a signal (PR-time CI — see below).
² Only runs in `--mutation` mode, which is off the default path.

## Modes and flags

| Flag                   | Effect                                                                                                                                                    |
|------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------|
| *(none)*               | Full gate.                                                                                                                                                |
| `--fast`               | Inner loop only (build + format + unit/contract). No coverage, E2E, scan, or API checks. Verdict is PARTIAL on success — it's a subset, not the full bar. |
| `--mutation`           | Evaluate a Stryker report only. Does not build or run Stryker.                                                                                            |
| `--bypass-format`      | Format reports as a signal, not a blocker, for this run.                                                                                                  |
| `--overwrite-baseline` | Persist improvements (warnings down, coverage up) to `.gate-baseline.json`. Without it the gate is **read-only** and never edits the file.                |
| `--all`                | Force the solution run for a directory that would resolve to one package.                                                                                 |

## Verdicts and exit codes

| Exit | Verdict    | Meaning                                                        |
|------|------------|----------------------------------------------------------------|
| 0    | PASS       | All blocking stages passed.                                    |
| 0    | PARTIAL    | `--fast` succeeded (a subset of the gate).                     |
| 1    | FAIL       | A blocking stage failed.                                       |
| 2    | INCOMPLETE | A blocking stage could not run (SKIP) — couldn't check ≠ done. |
| 3    | —          | Usage or environment error (missing `dotnet`/`jq`, bad path).  |

Signal stages (4, 5) never change the exit code; they surface information for the human
merge decision. The full report is written to `<scope>/.gate/report.json`, with per-stage
logs under `<scope>/.gate/logs/`.

## Config

Two files, both committed:

- **`.gate-config.json`** (repo root) — repo-wide settings, found by walking up from the
  package. Keys: `formatBaseRef` (what to diff against for changed-file scoping),
  `e2eHealthUrl` (the stack probe target).
- **`.gate-baseline.json`** (per package) — the package's measured baselines:
  `warnings.byCode`, `coverage.hermeticLine`, and `testProject`. A baseline key
  overrides the repo config. Created / updated only with `--overwrite-baseline`, which is
  a post-merge maintainer step — PR and local runs read it but never write it.

Baselines are ratchets, not targets: a change may not make a package worse (more
warnings, less coverage) than `master` already is. They are allowed to be imperfect;
they are not allowed to regress.

## How CI uses it

| Workflow                              | Invocation                              | Why                                                                                                                                                                          |
|---------------------------------------|-----------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `build-and-test.yml` (PR)             | `gate.sh packages --bypass-format`      | A fork contributor can't always fix formatting from their branch; master is auto-formatted after merge anyway. No `--overwrite-baseline`, so a PR never rewrites a baseline. |
| `build-and-test.yml` (push to master) | `gate.sh packages --overwrite-baseline` | Format is already clean post-merge; persist any baseline improvement so master's baselines stay current.                                                                     |
| `mutation.yml` (PR)                   | `gate.sh packages --mutation`           | Separate job — see below.                                                                                                                                                    |

## Mutation testing

Mutation runs in its own workflow (`mutation.yml`), kept off `build-and-test` on purpose:
`release.yml` waits only on `build-and-test`, so mutation never delays a release. Make it
block merges via branch protection (mark the `mutation` check required) rather than by
coupling it to the build.

The split in responsibility:

- The **workflow** runs `dotnet stryker --since:origin/master` on changed packages.
  Stryker's granularity is the **file** — a file with any change gets *all* its mutants,
  not just the ones on changed lines.
- `gate.sh --mutation` then **narrows** that to survivors whose line falls inside a
  changed hunk, and FAILs on any new one. A pre-existing survivor on an untouched line of
  a file you edited is that file's existing debt, not your change's regression, so it does
  not block.

A missing report is SKIP (couldn't check ≠ no survivors), which makes `--mutation` exit 2.

## Testing the gate itself

`fixtures/` is the test suite for the gate. Each fixture is a tiny package with one
planted defect (or none) plus the exact verdict it must produce — asserting the *stage*,
not just the exit code, so a fixture can't pass for the wrong reason.

```sh
./scripts/quality-gate/fixtures/run-fixtures.sh              # all fixtures
./scripts/quality-gate/fixtures/run-fixtures.sh clean red-test   # named ones
FIXTURE_KEEP=1 ./scripts/quality-gate/fixtures/run-fixtures.sh   # keep work dirs to inspect
```

Change the gate's behavior → add or update the fixture that pins it. A behavior with no
fixture is untested.

## Layout

```
gate.sh                 entry: argument parsing, scope discovery, orchestration
lib/common.sh           environment, jq/config readers, project discovery, run()
lib/report.sh           the row store, renderer, report.json
lib/warnings.sh         analyzer warnings + per-package baseline I/O
lib/coverage.sh         line coverage + per-package baseline I/O
lib/stages.sh           one definition of each stage, used for both scopes
format-suggest.sh       prints the format diff for a changed file (dev helper)
fixtures/               the gate's own test suite
```
