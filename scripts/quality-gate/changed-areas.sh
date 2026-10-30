#!/usr/bin/env bash
#
# changed-areas.sh <base-sha> — classify the diff <base-sha>...HEAD into the areas
# CI jobs care about, as GitHub Actions outputs (append to $GITHUB_OUTPUT):
#
#   code=true|false    any file other than sdk-compliance.yaml, CHANGELOG.md and
#                      packages/*/.gate-baseline.json. No build, test or analyzer
#                      reads those, so a diff made only of them skips the gate.
#   csharp=true|false  any .cs file. Mutation, annotation validation, formatting
#                      and the capability sync only ever look at .cs files.
#
# The decision is made from the files alone, never from the PR's author, title,
# branch or commit message: those are chosen by whoever opens the PR, so skipping
# on them would let any change skip the gate.
#
# A missing, all-zero or unknown base (a manual dispatch, a first push, a history
# rewrite) and an empty diff count as everything changed.
#
set -euo pipefail

INERT='^(sdk-compliance\.yaml|CHANGELOG\.md|packages/[^/]+/\.gate-baseline\.json)$'
CSHARP='\.cs$'

base="${1:-}"
if [[ -z "$base" || "$base" =~ ^0+$ ]] || ! git cat-file -e "$base^{commit}" 2>/dev/null; then
  files=""
else
  files="$(git diff --name-only "$base"...HEAD)"
fi

if [[ -z "$files" ]]; then
  echo "code=true"
  echo "csharp=true"
  exit 0
fi

grep -qvE "$INERT" <<<"$files" && echo "code=true" || echo "code=false"
grep -qE "$CSHARP" <<<"$files" && echo "csharp=true" || echo "csharp=false"
