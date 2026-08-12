# GitHub context and truth reference

## Route families as hints

Common repository-scoped families include repository root, `tree`, `blob`, `commit`, `issues`, `pull`, `actions`, `settings`, and `compare`.

Always establish owner/repository first. A route may contain a ref, path, PR number, run ID, or commit SHA, but route strings are not provider authority and refs can contain slashes.

Useful page evidence includes canonical URL, repository metadata, default-branch metadata, exact Raw links, semantic labels, and application/network data.

## Deictic examples

- “Copy this repo locally.” → resolve GitHub repository from browser context, then hand true Git acquisition to CODEeye.
- “Save this file.” → resolve current blob and exact Raw resource, then save source content.
- “What files changed?” on a PR → GitHub provider/diff truth.
- “What does this PR page look like?” → eyeBROWSE semantic/visual truth.
- “Why did this run fail?” → current run → failed job → failed step/log → source/config as needed.

## Ambiguity

Do not infer a ref/path solely by splitting `/blob/<remainder>` when multiple valid refs could fit. Prefer provider/page evidence or report ambiguity.
