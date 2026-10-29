# PR description

Title: the commit subject — `feat(<product>): <what a caller can now do>`.

Body, in this order. Factual, no process narrative (no "I first tried…", no phase names), no generated-by footer. Omit a section only where marked optional.

```markdown
## Summary

<One or two sentences: the capability a caller gains.>

Capability: `<key>` — <name>. <Link to the capability spec when one exists.>

## Public API

<The approved signature(s), new public types, and the example snippet — as approved.>

## Behavior

<The contract from phase 2: operations, inputs and defaults, outputs, failures and the exception each maps to, state changes.>

## Deviations from supabase-js

<Each deliberate difference and why. "None." when there are none.>

## Tests

| Tier | Class | Pins |
|---|---|---|
| E2E | `<Class>` | <behavior against the live stack> |
| Contract | `<Class>` | <wire interaction / failure mapping> |
| Unit | `<Class>` | <logic> |

Changed-line coverage: 100% (<n> lines). Mutation: <killed>/<total> on changed lines, 0 survivors.

## Notes (optional)

<Stack configuration changes, spec/OpenAPI discrepancies found, follow-ups for v9.>
```
