---
name: implement-capability
description: Use when implementing a capability-matrix feature in the Supabase C# SDK — given a feature card (area, group, name, description, key such as auth.oauth_admin.delete_client, status, note), a bare capability key, or a request to close a parity gap with supabase-js ("implement X", "add support for the Y endpoint", "finish the partially implemented Z"). Takes one or several features; several are dispatched to parallel agents in separate worktrees. Researches the contract, gets the public API approved, drives it test-first to 100% coverage and zero surviving mutants on changed lines, passes the quality gate, and opens a draft PR.
---

# Implement a capability

You are closing one row of the capability matrix. The deliverable is a draft PR the maintainer can merge without rework: a minimal, discoverable public API they approved up front, behavior that matches the capability's contract on every path, tests written first that cover and mutation-proof every changed line, and a passing gate.

Standards, in force throughout — read the sections you touch and treat them as a checklist, not background:
- `../_shared/references/CONVENTIONS.md` — how the code looks (v8 column for anything marked v9).
- `../_shared/references/QUALITY_RUBRIC.md` — when it is done.
- `references/sources.md` — where each area's contract, reference and C# home live.

Several features at once → follow `references/parallel.md` instead of running the phases below yourself.

## Input

A feature card: area, group, name, description, **key**, status, optional note. The key is the identity: everything else is looked up from it. A bare key is enough.

## 1. Locate

Resolve the key to its sources (`references/sources.md`):

1. The canonical entry in `capabilities/<area>.yaml` — confirm the key exists and the name/description match the card.
2. The capability spec `specs/<area>/<group>/<method>.md`, **if one exists**. It is normative: it states the SDK-layer obligations (defaults, state changes, checks before the wire) and wins over everything else.
3. The service's API spec (OpenAPI) — the operations, request/response shapes, status codes and error codes. Present for most features even without a capability spec.
4. The supabase-js implementation — read it to learn the behavior, not the code.
5. The current C# surface: `grep -rn '"<key>"' packages` for an existing `[PartialCapability]`, the client interface that owns the group, neighbouring methods, the package's test `Support/` fixtures.
6. Open issues: `gh issue list --repo supabase-community/supabase-csharp --search "<name>" --state all`.

If the capability cannot apply to a .NET SDK (browser-only API, platform storage), stop and report why instead of building it.

## 2. Contract

Write the behavior contract — the single artifact every later phase reads. It ends up verbatim in the PR description.

- **Operations**: method + path for each call, and the auth it needs (user JWT, service role, anon).
- **Inputs**: required vs optional, defaults the SDK fills in, checks the SDK makes before the wire.
- **Outputs**: what the caller gets back, and how an empty or absent body maps to it.
- **Failures**: each server answer the caller can hit (status, `error_code`) and the typed exception/reason it becomes; argument misuse that throws `ArgumentException` before I/O.
- **State**: anything the SDK stores, clears or emits (session, events).
- **Reference deltas**: what supabase-js does that this SDK deliberately does differently, and why.

The capability spec and the API spec are the contract; supabase-js fills gaps and shows the edges. **Never port, transliterate or adapt JS code.** JS idioms (untyped option bags, string unions, `{ data, error }` tuples, swallowed errors) do not carry over, and where the JS is loose the C# does better. Where the sources disagree, the capability spec wins, then the API spec, then supabase-js; note the disagreement in the PR.

For a `partially_implemented` card, the contract is the gap the existing note describes; everything already shipped stays as is.

## 3. Propose the public API — stop for approval

Present what a developer will see, and nothing else:

```
Placement:   IGotrueAdminClient (Supabase.Gotrue) — next to <neighbour>
Signature:   Task DeleteOAuthClientAsync(string clientId, CancellationToken cancellationToken = default);
New types:   <public records/enums with their members, or "none">
Throws:      GotrueException (FailureReason.X) when …; ArgumentException when …
Example:
    var admin = new AdminClient(serviceRoleKey, options);
    await admin.DeleteOAuthClientAsync("my-client-id");
Deviations:  <from supabase-js, with the reason, or "none">
```

Design rules: v8 error model (throw the package's typed exception, no `Result`); `Async` suffix and a trailing `CancellationToken` that reaches the HTTP call; immutable `init`-only records for new request/response/options types; read-only collections; enums over strings; one optional `XxxOptions` record over a parameter list; names from .NET/Supabase vocabulary, not JS; consistent with the neighbouring members a caller already knows; nothing public that a caller does not need.

**Wait for the maintainer's approval.** Do not write production code before it. Revise and re-present on feedback.

## 4. Branch

From the repository root, on a fresh branch off the latest master — never on the maintainer's current checkout:

```
git fetch origin master
git worktree add ../wt/<key> -b feat/<key> origin/master
```

Work only inside that worktree from here on. If the feature needs a service enabled on the local stack, change `supabase/config.toml` in the branch (CI starts the stack from it) and restart the local stack once.

## 5. Drive — double-loop TDD

1. **Outer loop: a failing E2E** (`[TestCategory("E2E")]`) proving the approved example works against the real stack. Self-isolating: unique names, guaranteed teardown. Reuse the package's E2E fixture.
2. **Inner loop**, red → green → refactor, through the public surface only:
   - **Contract** (`[TestCategory("Contract")]`): the package's WireMock server pins method, path, headers, query and the mapping of every failure in the contract to its exception.
   - **Wire-shape approval** (`Contract`, the package's `RequestApprovalFixture` + Verify): snapshot the exact request body. Review the `.verified` file: the approved bytes must match the API spec.
   - **Unit** (`[TestCategory("Unit")]`): pure logic, checks before the wire.
3. Every failure and edge in the contract has a test. A test that is awkward to write is design feedback: change the API, never expose an internal or add test-only production code.

Test style: `Sut_ShouldConsequence_GivenScenario`; class named for the element under test, in the domain folder (`Admin/`, `Mfa/`); FluentAssertions fluent and inline; NSubstitute for collaborators; reason strings cite the spec; no magic numbers; no comments.

Inner loop: `scripts/quality-gate/gate.sh packages/<Name> --fast`.

## 6. Refine

- Orchestration reads as named intent; mechanics in named private helpers. No comments in code.
- XML `<summary>` on every new public member, `<example>` on the entry point — on the interface; the implementation uses `<inheritdoc/>`.
- `ConfigureAwait(false)` on every await; the token reaches the HTTP call.
- New I/O participates in the package's instrumentation.
- Annotate the entry point: `[ImplementedCapability("<key>")]` — or swap an existing `[PartialCapability]` for it. Supporting types are not annotated. Never edit `sdk-compliance.yaml`; it is generated after merge.
- Add the new public surface to the package's `PublicAPI.Unshipped.txt`.

## 7. Verify

All three, in this order; each must pass before the next:

1. **Changed-line coverage — 100%.** `python3 -I .claude/skills/implement-capability/scripts/changed-lines-coverage.py packages/<Name>` exits 0. Each `UNCOVERED` line is either a missing test or dead code to delete.
2. **Mutation — zero survivors on changed lines.** `(cd packages/<Name>/<Name>.Tests && dotnet stryker --since:origin/master)`, then `scripts/quality-gate/gate.sh packages/<Name> --mutation`. A survivor is a missing assertion, or code that does nothing.
3. **Full gate** — invoke `sdk-quality-gate` on `packages/<Name>`. PASS is required. If the local stack is unavailable or shared with parallel work, the local verdict is INCOMPLETE; then the CI run on the draft PR is the gate (step 8).

Then walk QUALITY_RUBRIC §8 item by item, and re-read the diff as the reviewer: anything that would draw a comment is fixed now.

## 8. Ship

1. Commit with `feat(<product>): <what a caller can now do>` — product = `auth`, `storage`, `postgrest`, `realtime`, `functions`, `supabase`. No co-author trailer, no generated-by footer.
2. `git push -u origin feat/<key>`.
3. `gh pr create --draft --base master` with the body from `references/pr-template.md`. No generated-by footer.
4. `gh pr checks <number> --watch`. Every check green — `Build and Test` (full gate incl. E2E), `Mutation`, `Validate capability annotations`. Fix and push until they are.
5. Report: PR link, gate verdict, coverage and mutation results, deviations, anything left for the maintainer's judgment.

## Never

- Copy, transliterate or adapt supabase-js code.
- Write production code before the API is approved.
- Introduce a `Result` type or any v9 construct on v8 surface.
- Edit `sdk-compliance.yaml`, a `.gate-baseline.json`, or analyzer severities to get green.
- Suppress a warning, skip/ignore/re-categorize a test, or retry a flaky E2E.
- Commit on the maintainer's checkout, push to master, or mark a PR ready for review.
