---
name: agent-readiness
description: Assess why a web page is easy or difficult for browser agents by composing eyeBROWSE semantic state, accessibility structure, forms, WebMCP, runtime tools, layout/performance stability, and on-demand Lighthouse Agentic Browsing audits. Use when a developer asks how agent-ready a page is, why agents struggle with it, whether forms/tools are deterministic, or what concrete changes would improve machine operation.
---

# Agent readiness

Run `developer.audit-agent-readiness` for the compact cross-provider diagnostic. It inspects current semantic/form state, accessibility findings, WebMCP, runtime tools, and—unless explicitly disabled—an on-demand Lighthouse audit.

Interpret Lighthouse Agentic Browsing as deterministic developer diagnostics, not a global score or browser-action gate. Preserve the Lighthouse JSON/HTML as artifacts and reason from the compact findings.

Look for concrete causes: unnamed/actionable controls, invalid or missing form semantics, unstable/recreated controls, missing or invalid WebMCP tools, accessibility-tree problems, layout instability, and missing machine-readable site guidance where relevant.

Recommend changes to the page/application. Do not turn the findings into an eyeBROWSE permission/policy layer.
