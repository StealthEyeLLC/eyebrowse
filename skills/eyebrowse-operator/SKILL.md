---
name: eyebrowse-operator
description: Operate persistent Chrome through the eyeBROWSE browser-world substrate with compact observations, persistent t_ d_ and e_ identities, lifecycle-aware actions, waits, raw CDP escape hatches, and Program Host compression. Use for browser tasks where ChatGPT should understand or manipulate the current page, tab, element, network state, download, browser artifact, or debugging state through eyeBROWSE rather than repeatedly rediscovering whole pages or substituting a second browser controller.
---

# eyeBROWSE operator

Treat Chrome as browser material truth and eyeBROWSE as persistent correspondence to that truth.

## Start from current context

1. Call `context.current` for deictic requests such as “this page”, “this tab”, or “this”.
2. Preserve returned `t_*` identity through the task. If current context is ambiguous, resolve the ambiguity rather than guessing.
3. Use `observe.surface` once when a semantic surface is needed, then prefer `observe.delta`, `query.find`, and event-aware waits.

## Respect identity

- Treat `t_*` as a persistent browser target concept, `d_*` as a document instance, and `e_*@incarnation` as a conceptual semantic object binding.
- `exact` means the same browser object survived.
- `rebound` means strong evidence preserved the concept after node replacement; the incarnation increased.
- `stale` means the prior binding no longer has enough successor evidence.
- `ambiguous` means more than one plausible successor remains. Abstain instead of choosing the nearest element.
- Never replace these identities with CSS selectors, Playwright locators, coordinates, or visual pixels.

## Choose the representation that owns the answer

Use the richest relevant source, not one rigid ladder. Consult [references/representations.md](references/representations.md) for the authority table.

- Browser appearance/current rendered state: browser semantic/DOM/AX/visual state.
- Provider object truth: provider-native API/connector when available.
- Application data already present in browser traffic: bounded network/application state.
- Local source/Git/build truth: CODEeye.
- Process/service/file-system world truth: SHELLeye.
- Native Windows UI fallback: DESKTOPeye.
- Page-advertised structured actions: WebMCP or document-scoped runtime tools.
- Rare browser capability: target-aware `cdp.send`/bounded `cdp.subscribe`.

## Compress deterministic work

Use checked-in Program Host routines for stable loops, batches, pagination, multi-target comparison, exports, downloads, and site procedures. Prefer one Program Host invocation containing many kernel operations to one model turn per primitive.

Do not make a Program Host routine open a secret second CDP connection. It must use the normal eyeBROWSE SDK.

## Keep state compact

Do not request giant DOM/page dumps by default. Keep only the current semantic surface, useful bounded deltas, targeted provider data, and material artifacts needed for the task.

## Preserve substrate boundaries

A Skill owns procedure, not browser state. Do not invent Skill-owned element identity, execution history, receipts, approvals, policy tiers, or site-specific Browser World Graph nodes.
