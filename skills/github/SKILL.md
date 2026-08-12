---
name: github
description: Resolve and operate GitHub from the current authenticated browser context without requiring the user to repeat repository, file, pull request, commit, or Actions-run identifiers already visible in that context. Use for requests about “this repo”, “this file”, “this PR”, “this run”, copying a GitHub repository locally, acquiring current source, inspecting PR changes, diagnosing Actions failures, comparing refs, or combining GitHub browser context with CODEeye, downloads, multi-tab work, export, or debugging.
---

# GitHub procedural operator

Keep GitHub concepts in this Skill and its Program Host procedures. Never turn them into eyeBROWSE kernel node types.

## Resolve context before asking for visible IDs

1. Read neutral `context.current`.
2. Invoke `github.resolve-context` when the current origin is GitHub.
3. Accept repository identity from GitHub page metadata plus browser route evidence when consistent.
4. Treat route parsing as a hint. If exact ref/path is ambiguous, use page-provided raw/canonical links or provider-native truth; otherwise return ambiguity.
5. Starting from blob/tree/issue/PR/commit/Actions pages must still resolve the repository when evidence supports it.

See [references/github-context.md](references/github-context.md) for route families and truth selection.

## Choose authority by intent

- Provider object/diff/CI state: prefer the connected GitHub provider when available; use browser/provider resource data as a fallback.
- Current rendered page/visibility/layout: stay in eyeBROWSE browser state.
- Current local checkout/source/build/test: use CODEeye.
- Browser download/archive: use eyeBROWSE + `artifact-download`.
- Generic export/multi-tab/debugging: compose the independent horizontal Skill instead of absorbing it here.

## Named procedures

Use these when their preconditions match:

- `github.resolve-context`
- `github.acquire-repository`
- `github.acquire-file`
- `github.acquire-directory`
- `github.inspect-pr`
- `github.inspect-failed-actions-run`
- `github.compare-refs`
- `github.collect-repository-summary`

`github.acquire-repository` deliberately returns a CODEeye handoff for a true Git working copy. Do not implement Git clone/fetch inside eyeBROWSE.

## Current-file acquisition

From a blob page, prefer the exact page-provided Raw resource. Save source bytes/content, not GitHub-rendered chrome. If raw/ref/path evidence remains ambiguous, abstain instead of manufacturing a path.

## Actions diagnosis

Resolve current run from context, identify failed job/step/log using provider or browser application state, then inspect relevant source/config through CODEeye if diagnosis requires local engineering semantics.

## Mutation

Use disposable Build 002 fixtures/worktrees for repository mutations during evaluation. Never repurpose Campaign 3 infrastructure.
