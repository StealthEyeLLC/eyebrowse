---
name: web-debug
description: Diagnose browser web-application failures through coordinated eyeBROWSE console, exception, network, runtime-debugger, DOM, semantic, and source-context evidence. Use when a page errors, a request fails, JavaScript throws, the UI is stuck, an Actions/browser reproduction needs client-side diagnosis, or a runtime failure must be correlated with CODEeye source without installing a second browser controller or permanent verifier.
---

# Web debugging

Resolve the exact current `t_*` and document lifecycle before JavaScript-dependent diagnostics.

Start cheap with `developer.collect-debug-summary` or bounded `console.list`, `exception.list`, `network.search`, current semantic state, and deltas. Use `developer.investigate-console-error` when stack/source/network association is the core problem.

Enable `runtime_debug` only when script identity, source URL/source map, paused call frames, source retrieval, or source search materially improve diagnosis. Keep breakpoint/step/edit breadth behind raw CDP unless the task truly needs it.

Prefer network/application truth for request failures, Runtime/Debugger truth for JavaScript execution, and semantic/DOM state for the rendered symptom. Use CODEeye for repository/worktree/source modification and tests; running page script remains eyeBROWSE state.

Use bounded `cdp.subscribe` only during the debugging interval and unsubscribe afterward. Do not retain console history forever or turn debugging into a verifier.
