---
name: multi-tab
description: Coordinate several persistent browser targets through eyeBROWSE without losing the user’s primary context. Use for requests such as “compare these tabs”, “open every result and compare them”, “keep this page while investigating those links”, or bounded parallel browser work where stable t_ identities and Program Host concurrency should avoid repeated rediscovery and accidental actions in the wrong tab.
---

# Multi-tab operation

Start with explicit `t_*` identities. Preserve the primary target while opening/investigating secondary targets.

Use `common.multi-tab-compare` for bounded deterministic comparison. Use `target.activate` only when a visible-tab change is actually required; target-scoped CDP/semantic operations do not need UI focus merely to inspect data.

Run independent per-target reads concurrently where safe, while preserving command ordering for actions on the same target.

Before acting after a human tab change, re-read `context.current`. Never silently redirect an old `e_*` reference into whichever tab is currently visible.

Demote no-longer-interesting targets to warm/cold rather than keeping every deep provider active.
