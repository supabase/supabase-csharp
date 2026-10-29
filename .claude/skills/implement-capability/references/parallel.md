# Several features at once

You are the orchestrator: you never implement a feature yourself. Lane agents do the work; you plan lanes, relay proposals for approval, prepare the environment, and report.

## 1. Plan lanes

Resolve every key to its package and client interface (`sources.md`). Then:

- Features that touch the **same client interface or group** go in the **same lane**, implemented one after another, one branch and one PR each. Parallel edits to one interface produce PRs that conflict with each other.
- **Every PR is standalone: it branches off `master` and is reviewable, testable and mergeable on its own.** Never stack PRs (a PR whose base is another feature branch). CI only runs on PRs against `master`, and merging a stacked PR from GitHub lands it in the feature branch below it, not in `master`.
- When a feature needs a type that another unmerged feature introduces, it waits. Ship the feature that introduces the type first, and branch the dependent features off `master` once it has merged. Features with no such dependency on each other can be open at the same time; they conflict only where each adds its member to the shared files (`Api`, the client, the interfaces, `PublicAPI.Unshipped.txt`), and whichever merges second rebases and keeps both sides.
- Different interfaces or packages go in different lanes.
- At most three lanes. More lanes add merge points and machine contention faster than they save time.

Tell the maintainer the lane plan in one table before dispatching.

## 2. Research — lanes in parallel

Spawn one `general-purpose` agent per lane, in the background. Its prompt:

> Read `.claude/skills/implement-capability/SKILL.md` and its references. For each feature card below, in order, run phases 1–3 (Locate, Contract, Propose). Read-only: no branch, no worktree, no file writes in the repository. Return, per feature: the API proposal in the phase 3 format, the contract, any `supabase/config.toml` change the E2E needs, and open questions. Then stop and wait.
> <feature cards>

## 3. Approve

Present every proposal together, grouped by lane, verbatim. Collect the maintainer's approval or changes per feature. Relay changes to the lane (`SendMessage`) and re-present until each proposal is approved.

## 4. Prepare

For each approved feature, from the repository root:

```
git fetch origin master
git worktree add ../wt/<key> -b feat/<key> origin/master
```

The local Supabase stack is shared by all lanes (one project, fixed ports). E2E tests are self-isolating, so concurrent runs against it are safe. Lanes never stop, restart or reset it. If a lane needs a stack configuration change, either restart the stack once now from that lane's worktree after it commits the change (`supabase stop && supabase start --workdir ../wt/<key>`), or — when lanes need conflicting changes — run E2E in CI only for that lane.

## 5. Implement — lanes in parallel

Continue each lane agent (`SendMessage`, so it keeps its research context):

> Approved: <final proposal(s)>. Implement them in order, phases 4–8 of the skill, each in its worktree `<path>` on branch `feat/<key>`. Work only inside your worktree. Never stop, restart or reset the Supabase stack. Locally, run the inner loop (`gate.sh --fast`), the changed-line coverage script and Stryker; the full gate including E2E runs in CI on the draft PR — watch it with `gh pr checks --watch` and fix until green. Return, per feature: PR URL, final check status, coverage and mutation results, deviations, and anything needing the maintainer's judgment.

## 6. Report and merge

Report each lane's results as they arrive: PR link, checks, deviations, open questions. Do not merge anything.

Lanes on the same package conflict only on append-only files (`PublicAPI.Unshipped.txt`). When one PR merges, the others rebase onto `origin/master` (keep both sides' lines), push, and rerun checks.

After a PR merges, `git worktree remove ../wt/<key>` and delete the local branch.
