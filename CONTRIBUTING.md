# Contributing

Thank you for contributing. A pull request that does not meet every requirement below will not be
merged. The bar is the same for every contributor, and for changes written with an AI agent: you are
accountable for what you submit, and unreviewed agent output will be closed.

Before you write code, read the [Supabase SDK engineering principles](https://github.com/supabase/sdk/blob/main/docs/principles.md)
and [AGENTS.md](AGENTS.md).

## Before you start

Open an issue to discuss anything beyond a small fix, especially new public API. A large pull request
that nobody agreed to may be closed without review.

## Pull requests

- **One concern per pull request**, small enough to review in one sitting. Split anything larger.
- **Branch from `master`.** Do not stack pull requests on each other. To pick up changes, merge
  `origin/master` into your branch and push normally; do not force-push.
- **The pull request title must be a [Conventional Commit](https://www.conventionalcommits.org/)**
  (`feat(realtime): ...`, `fix(gotrue): ...`). Pull requests are squashed, so the title becomes the
  commit on `master`, and [release-please](https://github.com/googleapis/release-please) derives the
  version and changelog from it. Do not edit `CHANGELOG.md` or version numbers.
- **Describe what changed, why, and how you verified it.** Paste the quality gate verdict, not "tests
  pass".

## Quality gate

Run the [quality gate](scripts/quality-gate/README.md) and get a PASS before you open a pull request:

```sh
./scripts/quality-gate/gate.sh packages/<Package>
```

End-to-end tests need Docker and a local stack (`supabase start`). A stage that could not run is
reported as skipped and the run is incomplete, which does not count as a pass.

The gate enforces:

- **No new warnings.** The warning count per package must not exceed its baseline.
- **Incremental coverage.** Line coverage must not drop below the baseline, and every line you change
  must be covered.
- **Mutation testing.** No surviving mutant on a changed line. Your tests must fail when the
  behaviour is wrong, so cover every scenario and not only the happy path.
- **Formatting and naming** on changed files.
- **Public API declarations** that match the compiled surface (see below).

## Public API

The public surface is tracked in `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` next to each
library project.

- Record every new public member in `PublicAPI.Unshipped.txt`.
- Record a removed public member in `PublicAPI.Unshipped.txt` with the `*REMOVED*` prefix.
- Do not edit `PublicAPI.Shipped.txt`. Entries move from unshipped to shipped at release time.
- Do not write these entries by hand. If the gate fails with `Public API declared`, sync the files and
  commit the result:

  ```sh
  dotnet format analyzers <project> --severity info --diagnostics RS0016 RS0017
  ```
- Shipped members do not change until the next major version. Treat a removal or signature change as a
  breaking change: it needs prior agreement in an issue and, if the compiler error alone does not tell
  users how to adapt, an entry in [docs/migrations](docs/migrations).
- New public timestamps are `DateTimeOffset`.

## Capabilities

`sdk-compliance.yaml` is generated from the `[ImplementedCapability]` and `[PartialCapability]`
attributes in the code. Annotate the public member and do not edit the file by hand.

## Code style

Follow [CONVENTIONS.md](.claude/skills/_shared/references/CONVENTIONS.md). A pull request that
ignores it will be sent back.

## Security

Report vulnerabilities through the repository's **Security** tab (**Report a vulnerability**), not in a
public issue. Never put keys, tokens, or real user data in code, tests, fixtures, logs, or pull
request descriptions.
