# C# SDK Workflows

> **This is the rationale, not the runbook — and it is a mental model, not wired to skills.** The only mechanized flow that ships as a skill today is `sdk-quality-gate` (the `Verify` step of every flow below; it runs `scripts/quality-gate/gate.sh`). The earlier per-flow skills (`sdk-workflow`, `test-suite-refactor`) and the `_shared/five-step-library.md` file were never carried forward and no longer exist — the five-step model survives here as the *why*, applied by hand. Treat the steps as a way to think about a change, and reach for `sdk-quality-gate` when you get to Verify.

*How we work.* [CONVENTIONS.md](CONVENTIONS.md) says how code should look; [QUALITY_RUBRIC.md](QUALITY_RUBRIC.md) says when it's done and how the bar is enforced. This says **how a change moves from start to done** — and, deliberately, how the same pieces recompose for very different kinds of work.

The core idea: **there is one step library, not many workflows.** Feature parity, refactoring, and bugfixing are not separate machines — they are the same handful of steps run with a different *contract*. The steps are stable; only the contract varies, and it varies in four small, declarable ways.

This is the declarative principle (CONVENTIONS §5.5) applied one level up: a flow reads top-to-bottom as a story of named steps; the *how* of each step lives inside it.

---

## 1. The step library — the reusable pieces

Five steps. They do not change between flows. Each has a stable input/output so it can be recombined.

| Step | What it does | In → Out |
|---|---|---|
| **Establish-contract** | Produce the behavior spec the change must honor | trigger → **contract** (frozen behavior + definition of done + diff rules) |
| **Design** | Idiomatic .NET plan against the contract (vertical slice, types, API) | contract → plan |
| **Drive** | Double-loop TDD: failing outer test → green via inner red/green/refactor | plan + contract → code + tests |
| **Refine** | Declarative pass, honor test design-feedback, apply conventions | code → code |
| **Verify** | Run the ordered gauntlet (QUALITY_RUBRIC §4) | contract → pass / signal report |

`Drive` is always double-loop TDD (QUALITY_RUBRIC §2). `Refine` always enforces declarative composition and the design-feedback rule. `Verify` is always the same pipeline. That invariance is the point — it's what makes the pieces reusable.

**`Establish-contract` has variants** (same output shape, different source of truth):
- *Gap analysis* — read the matching supabase-js package (gotrue-js, realtime-js, storage-js, …), map the behavior the .NET surface is missing.
- *Characterization* — pin the **current** behavior of existing code (wire-shape snapshots, E2E, mutation baseline) before anything is touched.
- *Reproduce* — turn a bug's expected-vs-actual into a failing case.

## 2. The flow contract — what actually varies

The only thing that differs between flows is a four-field contract that `Establish-contract` emits and every later step reads:

1. **Source of truth** — where behavior-truth comes from.
2. **Definition of done** — is behavior *added*, or *frozen*?
3. **Diff semantics** — does an API / wire / behavior delta mean *feature* or *defect*?
4. **Primary gate** — which gauntlet signal decides success.

Set those four and the same five steps become a different flow. The contract artifact is also the **only interface between steps** — it's the glue that lets them recombine.

## 3. Flows — presets of the contract

| Flow | Source of truth | Done | A diff means | Primary gate | Entry variant |
|---|---|---|---|---|---|
| **Feature / parity** | supabase-js behavior | new behavior exists | feature | behavior-equivalence + full gauntlet | Gap analysis |
| **Production refactor** | current behavior | behavior frozen, shape better | **defect** | empty wire-snapshot + API diff | Characterization |
| **Test-suite refactor** *(transitional — §5)* | E2E + snapshots (**not** the unit tests) | net ≥ old, reads better | n/a | **mutation score held** | Characterization |
| **Bugfix** | expected-vs-actual in the report | failing case green, nothing else moves | regression | new test green + no other diff | Reproduce |

Flows are **subsets and orderings of the same DAG**, not separate pipelines:
- **Feature** runs all five, entering through gap analysis. A behavior/API delta is the whole point.
- **Production refactor** freezes behavior. It pins current behavior first, changes shape under green, and treats any behavior/wire/API delta as a **leak to investigate, not an outcome**. `Design` here is "target shape," not "new capability."
- **Test-suite refactor** is the tricky one: you cannot use the tests as the safety net for changing the tests. The oracle moves *outside* the units — E2E and wire snapshots hold behavior, and **mutation score is the measure of success** (the new suite must catch ≥ what the old one did). Work in strangler steps: add the new behavior test, prove it fails when the bug the old test caught is injected, *then* delete the old one.
- **Bugfix** enters at reproduce; done is narrow by construction — the reported case passes and nothing else moves.

**Scope call, made up front (refactor flows):** decide whether it's a *pure* refactor (production or tests frozen on the other side) or one that's *allowed to fix the design smells it surfaces*. They have different safety nets, and mixing them silently is how a scoped change becomes an unreviewable diff. Name which one it is in the PR.

## 4. Composition & granularity rules

- **Don't over-split.** Every piece promoted to its own agent is a cold start that re-derives context, plus a handoff seam that can drop information. Split by **capability with a stable interface reused across ≥2 flows** — which is exactly why these five survive and "rename the variables" doesn't. If two steps only ever appear together and share all context, they are one step.
- **The contract artifact is the interface.** Steps communicate through the frozen-behavior + done + diff-rules object, nothing else. Keep it explicit; it's what makes recomposition safe.
- **A flow reads like a story.** `EstablishContract → Design → Drive → Refine → Verify`. If a flow definition exposes mechanics inline, push them into the step.

## 5. Transitional flows — built to be removed

Not every flow is permanent. **Test-suite refactor is transitional**: it exists to lift the *existing* packages onto the double-loop bar, and it is **retired once that bar is reached** — it is not part of steady-state work.

- **Exit criteria (per package):** the tiers that fit the package's responsibility are present and **explicitly categorized** (`[TestCategory(...)]` on every class, `Unit` included) — a pure composition/umbrella package that owns no wire contract legitimately has **no Contract tier** (QUALITY_RUBRIC §2); tests assert behavior through the public surface, not internals, using NSubstitute rather than hand-written fakes; a mutation-score baseline is established and healthy, **read per QUALITY_RUBRIC §4** (dead-code and E2E-only survivors are triaged, not blindly chased); the suite is green and reads to style (CONVENTIONS §5.6).
- When every package clears it, the flow (and any `/test-refactor` skill wiring it) is **deleted, not kept around.** Steady state is Feature, Production refactor, and Bugfix; test quality is then maintained inside those flows via `Drive` + `Verify`, not as a standalone campaign.
- The same "transitional" treatment applies to any one-time migration campaign: model it as a flow with explicit exit criteria, run it to completion, then remove it. The **Newtonsoft → System.Text.Json** migration at the major is the worked example — it ran to completion and is now retired, exactly as this prescribes.

---

**Companions:** [CONVENTIONS.md](CONVENTIONS.md) (how code looks) · [QUALITY_RUBRIC.md](QUALITY_RUBRIC.md) (when it's done). Each flow's `Verify` step is the QUALITY_RUBRIC gauntlet; each flow's `Refine` step enforces CONVENTIONS.
