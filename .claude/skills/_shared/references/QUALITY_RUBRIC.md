# C# SDK Quality Rubric

The bar a change must clear to be *done*. [CONVENTIONS.md](CONVENTIONS.md) says how code should look; this says when it's finished and how that is enforced.

A change **passes** only if every non-negotiable is met. Preferences are strong defaults, tradeable with a stated reason.

The current release line is **v8** (`.release-please-manifest.json`). **v9** is the next major: it carries the return-based error model, the typed transport and every deferred break. Anything this document defers "to the major" means v9.

---

## 1. Definition of done — parity features

Done when:

1. **Behavior-equivalent** to the capability's contract on every path it supports (happy, documented errors, handled edges). The contract is assembled from the capability-matrix spec (when one exists), the service's API spec, and the supabase-js implementation as a behavioral reference. Signatures may differ; behavior may not. (CONVENTIONS §12)
2. **Idiomatic and discoverable** — usable from IntelliSense without reading source: typed inputs/outputs, XML docs on every public member, an `<example>` on the entry point. DX is pass/fail.
3. **Minimal public surface** — only what a caller needs is public; everything else is `internal` or `private`. New public types are justified by a caller's need, not by testability.
4. **Immutable, typed models** — `record`s with `init`-only members for new request/options/response types (CONVENTIONS §5); no `Dictionary<string, object>` bodies.
5. **Error model of v8** — operation failures throw the package's typed exception (`GotrueException`, `SupabaseStorageException`, `PostgrestException`, `FunctionsException`, `RealtimeException`) carrying the right reason/hint. No `Result` type on new surface until v9: two error contracts side by side would confuse callers (CONVENTIONS §9).
6. **Async idiom** — new public I/O uses the `Async` suffix and takes a `CancellationToken` that is honored through the whole call path.
7. **Declarative, comment-free** — orchestration reads as named intent, mechanics pushed into named private helpers (CONVENTIONS §5.5). No explanatory comments in code: a method that needs one is extracted and renamed.
8. **Annotated** — the entry point carries `[ImplementedCapability("<key>")]` (or `[PartialCapability("<key>", "<what is missing>")]`). `sdk-compliance.yaml` is generated from these after merge and is never hand-edited.
9. **Additive** (§5). If parity truly needs a break, the additive version ships and the break is logged for v9.
10. Intentional deviations from the reference are listed in the PR description.

## 2. Test strategy — the double loop

**Double-loop TDD.** An E2E test against real infrastructure is too broad to pin logic; contract/unit tests against WireMock exercise nothing real. Both, together.

- **Outer loop — E2E / acceptance** against the Supabase CLI stack (`supabase/config.toml` at the repo root). A feature starts with a failing E2E that proves the real behavior. It defines "done" from the outside.
- **Inner loop — contract + unit, hermetic.** Contract tests (WireMock) pin the HTTP interaction; unit tests pin pure logic; wire-shape approval tests (Verify) pin the exact request bytes. No live services.

| Tier | Marker | Against | Runs |
|---|---|---|---|
| **E2E / Acceptance** | `[TestCategory("E2E")]` | Supabase CLI stack | Locally with `supabase start`; on every PR in CI. Excluded from mutation. |
| **Contract** | `[TestCategory("Contract")]` | The package's WireMock server (e.g. `MockGotrueServer`) | Every build. |
| **Unit** | `[TestCategory("Unit")]` | Pure, no I/O | Every build. |

- **E2E are the highest-value tests and must be deterministic.** Flakiness is a design defect, never masked with retries or quarantine. Every test isolates its own state (unique names/emails) and tears it down, so runs are independent and can share one stack concurrently.
- **Every test class carries exactly one tier** — `Unit` included. `TestConventions` enforces this and the naming pattern at build time.
- A package that owns no wire contract (the `Supabase` umbrella) legitimately has no Contract tier.

## 3. Test requirements (non-negotiable)

- **Written first**, driven from a failing outer E2E down into the inner loop.
- **Every new or changed production line is covered by the inner loop, and no mutant survives on it.** This is the contributor's bar, stricter than what the gate mechanizes (the gate ratchets package-level coverage and blocks new mutation survivors on changed lines). Exclusions are per-line `[ExcludeFromCodeCoverage]` with a reason visible in review — never a lowered bar.
- **Test behavior, not implementation.** Assert outcomes and side effects observed **through the public surface** — never internals, private state, or call counts that encode *how* it works.
- **Honor the design feedback.** A test that is hard to write, or needs to reach into internals, signals an awkward API: fix the design, never expose the internal.
- **No test-only code in production.** No setter, constructor parameter, or member that exists only so a test can reach it. The seam is a `protected virtual` overridden by a double in the test project.
- **Tests earn their place.** Each pins a real contract and kills a mutant no other test kills. Don't assert language- or framework-guaranteed behavior. One behavior per test; check for an existing test before adding one.
- **Style & naming (CONVENTIONS §5.6):** `Sut_ShouldConsequence_GivenScenario`, FluentAssertions, fluent and inline, NSubstitute for collaborators, no hand-written fakes, no magic numbers in assertions.
- **The whole suite is green** at the end — not just the new tests.

## 4. Enforcement — the quality gate

`scripts/quality-gate/gate.sh` is the single "is it done" program. Contributors, agents and CI run the same command; its README is the authoritative stage list. Stages are **blocking** (fail the PR) or **signal** (surfaced for the maintainer, never failing).

| Stage | Kind | What it enforces |
|---|---|---|
| Build + analyzers | **Block** | `dotnet build --no-incremental`; warning count per package may not exceed its committed baseline. Analyzers: .NET (`AnalysisLevel` latest) + PublicApiAnalyzers, configured by `.editorconfig` / `.globalconfig`. |
| Format + naming | **Block** (signal on PR CI) | `dotnet format --verify-no-changes` on changed files. PR CI bypasses it because master is auto-formatted after merge; an agent's change must be format-clean anyway. |
| Tests | **Block** | Unit + Contract + E2E in one unfiltered run when the stack is reachable; inner loop only with `--fast`. Zero tests executed is a failure. |
| Line-coverage baseline | **Block** | Per-package `coverage.hermeticLine`, up-only, ~1% jitter band, no ceiling. |
| Public API declared | **Block** | `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` match the compiled surface. |
| Mutation (new survivors) | **Block** (own workflow) | `mutation.yml` runs Stryker `--since:origin/master` on changed packages on every PR; `gate.sh --mutation` fails on any survivor on a changed line. Stryker runs hermetic tests only (`TestCategory!=E2E`). |
| Public-API diff | Signal | Added/removed members. A removal is a break needing maintainer sign-off. |
| Vulnerable dependencies | Signal | A vulnerability may have no fixed version; bump when one exists, track when none does. |

- **Baselines are ratchets.** Warnings only go down, coverage only up. They are written only by the post-merge master run (`--overwrite-baseline`); PR and local runs are read-only.
- **A check that could not run is never a pass.** SKIP makes the verdict INCOMPLETE.
- **`TreatWarningsAsErrors`** is on for `Core` (and `CapabilityMatrix`); other packages flip once they reach zero warnings. New code introduces zero new warnings either way.
- **The capability annotations are validated** against the canonical matrix on every PR (`build-and-test.yml` → `capability-matrix` job): a mistyped key fails.

**Reading the mutation score.** Kill what is reachable through the public surface. Don't chase the number by casting to concrete types or asserting internals. A survivor on code that a later step overwrites is dead code: delete it. A survivor that only an E2E test can kill is a design smell: reclaim it with a hermetic seam where the logic can be owned in-process.

**Property-based testing (FsCheck)** where it earns its place: pure logic, invariants, serialization round-trips. Not a substitute for example-based tests.

## 5. Breaking-change gate (non-negotiable)

- The public API diff is **additive** in v8: no changed signatures, defaults, serialized shapes, or error contracts on existing members without maintainer sign-off.
- Retiring surface uses `[Obsolete("… Favor <new> instead.")]` + a `MIGRATION_v9.md` entry.
- When unsure, state the affected consumer population and failure mode in the PR and default to the opt-in version.

## 6. Code quality (non-negotiable)

- Conforms to [CONVENTIONS.md](CONVENTIONS.md).
- New abstractions justify themselves; otherwise reuse the package's existing helper or pattern.
- New I/O participates in the package's OpenTelemetry instrumentation.

**Security (this is an auth SDK):**
- **No secrets in telemetry, exceptions, or logs** — tokens, full auth URLs and credentials go through `UrlSanitizer` / redaction.
- **Token and session-at-rest handling is deliberate** — persistence stores only what's needed, through the explicit, overridable storage seam.

## 7. Preferences (strong defaults, tradeable with a reason)

- One capability per PR; small, reviewable diffs.
- Reach for the pattern a Supabase/.NET developer would predict before inventing one.
- Keep serialization at the model/transport boundary.

## 8. Before opening the PR

- [ ] Full gate PASS locally, or `--fast` PARTIAL locally with the full gate green in CI.
- [ ] Every changed production line covered by the inner loop; zero surviving mutants on changed lines.
- [ ] Wire-shape approval snapshots reviewed — every payload diff is intended.
- [ ] Tests assert behavior through the public surface; testability friction fixed in the design.
- [ ] Format clean; zero new warnings; `PublicAPI.Unshipped.txt` updated.
- [ ] Public surface minimal and documented; the entry point has an `<example>` on the interface, `<inheritdoc/>` on the implementation.
- [ ] `ConfigureAwait(false)` on every new await; `CancellationToken` honored through the call path.
- [ ] Capability annotation present, key exact.
- [ ] Parity deviations and new public surface listed in the PR description.
- [ ] Commits follow `type(scope): description`. The scope names the affected product (`auth`, `storage`, `postgrest`, `realtime`, `functions`, `core`, `supabase`); omit it for cross-cutting changes; `!` before the colon marks a break.
