# C# SDK Quality Rubric

The bar a change must clear to be *done* — calibrated to where we're taking the SDK, not where it is. [CONVENTIONS.md](CONVENTIONS.md) says how code should look; this says when it's finished and how the bar is enforced while we climb out of the current debt.

A change **passes** only if every non-negotiable is met. Preferences are strong defaults, tradeable with a stated reason. Tools listed as *judgment tools* are applied where they add value — they are never blanket requirements.

---

## 1. Definition of done — parity features

Done when:

1. **Behavior-equivalent** to the matching supabase-js package (gotrue-js, realtime-js, storage-js, …) on every path it supports (happy, documented errors, handled edges). Signatures may differ; behavior may not. (CONVENTIONS §12)
2. **Idiomatic and discoverable** — usable from IntelliSense without reading source: typed inputs/outputs, familiar Supabase `{ data, error }`-style contract, XML docs with an `<example>` on the entry point. DX is pass/fail here.
3. **Immutable, typed, per-operation contracts** — immutable request/response records (CONVENTIONS §5), typed through the generic transport, no `Dictionary<string,object>` (CONVENTIONS §8), organized as a vertical slice (CONVENTIONS §2).
4. **Error model honored** — expected failures are returned, not thrown; validation returns the result type; the correct typed failure is produced (CONVENTIONS §9).
5. **Declarative** — orchestration reads as named intent, mechanics pushed down (CONVENTIONS §5.5).
6. **Async idiom** — `Async` suffix + mandatory `CancellationToken` on new public I/O.
7. **Additive / opt-in** (§5). If parity truly needs a break, the additive version ships and the break is logged for the major.
8. Intentional deviations from the reference are noted in the PR description.

## 2. Test strategy — the double loop

We develop with **double-loop TDD**. Neither loop alone is sufficient: an E2E test against real infrastructure has too broad a scope to pin down logic; unit/contract tests against WireMock aren't exercising anything real. So we use both, together.

- **Outer loop — E2E / acceptance, against the local CLI infrastructure.** Start a feature with a failing end-to-end test that proves the real behavior against a live Supabase CLI stack. It defines "done" from the outside.
- **Inner loop — contract + unit, hermetic.** Drive the outer test to green through fast red → green → refactor cycles: **contract tests** (WireMock) pin the HTTP interaction, **unit tests** pin pure logic and validation. No live services.

| Tier | Marker | Against | Loop | When |
|---|---|---|---|---|
| **E2E / Acceptance** | `[TestCategory("E2E")]` | Supabase CLI stack (`AgainstCliStack()`) | Outer | Local Docker + CI. Excluded from mutation runs (§4). |
| **Contract** | `[TestCategory("Contract")]` | `MockGotrueServer` (WireMock) | Inner | Every build, every iteration, CI. |
| **Unit** | `[TestCategory("Unit")]` | Pure — no I/O | Inner | Every build, every iteration, CI. |

- **The autonomous agent loop runs the inner loop** (`dotnet test --filter TestCategory=Contract|...`) — fast, no Docker. The outer E2E is authored with the feature and runs in CI / locally with `supabase start`; its need for live infra is **not** a reason for the inner loop to fail.
- **E2E are the highest-value tests and must be deterministic — not flaky.** They exercise the only real system, so they carry the most signal. Flakiness is a **design defect to fix**, never masked with retries or quarantine: isolate state per test (unique users/emails, proper seeding + teardown), and control time where the SDK exposes the seam. E2E are gated out of the PR path for **cost and maintenance reasons — not reliability**; a flaky E2E is a bug, held to the same design-feedback bar as any other test (§3).
- **Every tier is explicitly categorized — `Unit` included; there is no unmarked default.** Not every package carries every tier: a pure composition/umbrella package that owns no wire contract of its own legitimately has **no Contract tier** — its collaborators' contracts are tested in their own packages, so test what *this* package owns (wiring, header propagation, state forwarding). *(Establishing the applicable seam across packages is prerequisite work for the autonomous loop.)*

## 3. Test requirements (non-negotiable)

- **Written first**, driven from a failing outer E2E down into the inner loop.
- **Test behavior, not implementation.** Assert outcomes and side effects observed **through the public surface** — never internals, private state, or call counts that encode *how* it works.
- **Honor the design feedback from tests — do not skip it.** If something is hard to test, or a test has to reach into internals, that is a **design smell to fix, not to work around**. This is how we build DX: only expose what must be exposed, and make what *is* exposed explicit and discoverable. A test that's awkward to write is telling you the API is awkward to use.
- **One behavior per test** — assert the method's own contract, not downstream consumer behavior; check for an existing test before adding one.
- **Style & naming (CONVENTIONS §5.6):** FluentAssertions, **fluent** and **inline** (no blank lines between arrange/act/assert). Name each test `Sut_ShouldConsequence_GivenScenario` — the method name is the spec, so no redundant `[TestMethod(DisplayName = …)]`. Assertion reason strings explain *why* (cite spec/RFC/issue). Collaborators are **NSubstitute** substitutes, never hand-written stub/fake classes.
- **The full inner-loop suite is green** at the end — not just the new tests.

## 4. Enforcement & quality tooling

**Enforcement (non-negotiable, ratcheted from debt):**
- **New and changed code introduces zero new warnings** (nullable, analyzers, CS1591 on public API). The debt only shrinks.
- **A committed per-package warning-count baseline is what makes "zero new" enforceable** — CI diffs the current count against the baseline and fails if it rises; the baseline is ratcheted down, never up.
- Analyzers (Roslynator/StyleCop) and `.editorconfig` enabled as guidance; **`dotnet format` clean** on changed files.
- **`TreatWarningsAsErrors` turns on per package once it hits zero warnings** — not before.

**Mechanized gates & CI pipeline (ordered — turns this rubric into a gauntlet):**
A change runs an ordered pipeline; stages are either **blocking** (fail the PR) or **signal** (surfaced for the human merge decision, never auto-failing).

| Stage | Kind | Notes |
|---|---|---|
| `dotnet format` + analyzers + nullable | **Block** | Style/correctness floor; warning-count baseline diff. |
| Unit + Contract (inner loop) | **Block** | Fast, hermetic; the green bar the agent loop drives to. |
| **Wire-shape approval tests** | **Block** | Snapshot the serialized request/response payloads (Verify/ApprovalTests). A changed payload is a red diff to accept deliberately — the guard rail against accidental serialization drift. (It was the safety net through the now-complete Newtonsoft → System.Text.Json migration.) |
| **Public-API diff** (PublicApiAnalyzers) | **Signal** | Surfaces exactly what public surface changed for the maintainer's judgment — **a warning, not a blocker.** Guillaume is the human merge gate and is allowed to cut majors / request breaks; the tool informs that call, it doesn't veto it. |
| **Line-coverage baseline** | **Block** | A committed per-package `coverage.hermeticLine` baseline, ratcheted up-only within a ~1% tolerance band. Measured over the full suite when the stack is reachable, hermetic-only otherwise. See §4 note below. |
| Security scan (`dotnet list package --vulnerable`) | **Signal** | See §6. Surfaced for the maintainer's judgment, not auto-failing: a vulnerable dependency may have no fixed version available, and blocking on something we cannot act on would wedge every PR. |
| E2E / acceptance | **Nightly / on-demand** | Deterministic, highest-value; gated off the PR path for cost, not reliability (§2). |
| Mutation (Stryker) | **Periodic / signal** | Test-quality check on inner-loop tests (below). |

**Judgment tools (use where they add value — not gates, never for show):**
- **Mutation testing (Stryker)** — the honest measure of whether tests actually catch regressions; reach for it to check test quality on logic that matters. It runs on the **hermetic inner-loop tests only**; E2E/live-infra tests are incompatible and are **excluded** via their category. *(Open, to discuss: whether excluding E2E skews the score. Working view: exclusion surfaces logic that leaked into being E2E-only — which the inner loop should own anyway — so it's a useful signal, not a distortion.)*
- **Read the mutation score honestly.** A composition/umbrella layer's score is bounded by what its collaborators expose. Kill what's reachable through the public surface — option defaults, header/bearer composition, setter side-effects verified with `Received()`, merge/guard behavior via header propagation — but do **not** chase the number by casting to concrete child types or asserting internals; that violates behavior-through-public-surface (§3). Two residual categories are expected, and are *signals, not test gaps*: **survived mutants on code a later step overwrites are dead code to delete** (a production cleanup, tracked separately from the test work); **E2E-only survivors are a design smell** — reclaim them with a seam (inject the collaborator, assert `Received()`) wherever the logic can be owned hermetically, leaving only genuine real-infrastructure integration to the E2E tier.
- **Property-based testing (FsCheck)** — use where it genuinely earns its place: pure logic, invariants, serialization round-trips. Not required, and not a substitute for clear example-based tests.
- **Line coverage is a ratcheted per-package baseline, not a fixed target %.** The gate commits `coverage.hermeticLine` per package and fails only when coverage drops below the best-ever recorded (outside a ~1% jitter band) — it moves up-only and has no ceiling. This guards against untested new code eroding the number; it is *not* a blanket "hit N%" bar. Behavior coverage (§3) and, where used, mutation score remain the meaningful quality signals — the line baseline is a regression floor, not a measure of test quality.

## 5. Breaking-change gate (non-negotiable)

- The **public API diff is additive** — no changed signatures, defaults, serialized shapes, or error contracts on existing members without maintainer sign-off and a major-version plan.
- Retiring surface uses `[Obsolete("… Favor <new> instead.")]` + a `MIGRATION_vN.md` entry (CONVENTIONS §7).
- When unsure, state the affected consumer population and failure mode in the PR and default to the opt-in version.

## 6. Code quality (non-negotiable)

- Conforms to [CONVENTIONS.md](CONVENTIONS.md): immutability, vertical slices, declarative composition, typed transport, typed failure hierarchy, return-based errors on new surface, OTel participation, no sync-over-async, no secrets in telemetry/failures.
- Public surface fully documented; entry points have an example — and is **minimal**: expose only what must be exposed (§3).
- New abstractions justify themselves; otherwise reuse the existing helper/pattern.

**Security (non-negotiable — this is an auth SDK):**
- **No secrets in telemetry, failures, or logs** — tokens, full auth URLs, and credentials are routed through `UrlSanitizer` / redaction, never surfaced raw (CONVENTIONS §9).
- **Token & session-at-rest handling is deliberate** — session persistence stores only what's needed, and the storage seam is explicit and overridable, not silently writing tokens to disk in the clear.
- **Dependency vulnerability scan** (`dotnet list package --vulnerable --include-transitive`) runs in CI and **surfaces** known-vulnerable transitive packages as a signal for the maintainer to weigh — bump the dependency when a fix exists, accept and track it when one doesn't. It does not auto-fail the PR: a vulnerability with no available fix is not the PR's to resolve, and blocking on it would wedge every merge.

## 7. Preferences (strong defaults, tradeable with a reason)

- Thread `CancellationToken` through the entire new call path, not just the entry point.
- Keep serialization at the model/transport boundary so the future STJ migration stays contained.
- Small, reviewable changes; one feature per PR.
- Reach for the pattern a Supabase/.NET developer would predict before inventing a new one.

## 8. Before opening the PR

- [ ] Inner-loop suite green (`dotnet test --filter TestCategory=Contract` + unit); outer E2E green locally against `supabase start` when a live path is touched.
- [ ] Wire-shape approval snapshots reviewed — any payload diff is intended (not an accidental serialization change).
- [ ] Tests assert behavior through the public surface; any testability friction was resolved by fixing the design, not exposing internals.
- [ ] `dotnet format` clean; **zero new warnings** vs. baseline; any known-vulnerable dependency reviewed — bumped where a fix exists, consciously accepted and tracked where none does.
- [ ] Public API diff reviewed — intended (additive, or break logged for the major + signed off; `[Obsolete]` + MIGRATION entry where retiring surface).
- [ ] `ConfigureAwait(false)` on new awaits; `CancellationToken` genuinely honored through the call path.
- [ ] Models immutable & per-operation; transport typed; orchestration declarative; expected failures returned not thrown; `Async`+`CancellationToken` on new I/O.
- [ ] Public surface minimal, documented; entry points have an `<example>`.
- [ ] Parity deviations / new public surface noted in the PR description.
- [ ] Commits follow `type(scope): description` — the scope names the affected package (`functions`, `gotrue`, `postgrest`, `realtime`, `storage`, `core`, `supabase`); append `!` before the colon for breaking changes (`feat(functions)!:`).
