---
name: current-page-export
description: Export useful content from the current browser page through eyeBROWSE into Markdown, text, or CSV while choosing a representation that matches the requested material instead of blindly saving full HTML. Use for requests such as “save this page as Markdown”, “save this table as CSV”, “save the useful content from this page”, or other current-page extraction where browser semantic state, application data, DOM, or page resources should be selected deliberately.
---

# Current page export

Resolve the current `t_*` with `context.current`; do not guess among tabs.

Prefer `common.export-page` when the request matches its deterministic preconditions. For CSV, target an actual tabular representation. For Markdown/text, prefer the useful `main`/article/semantic content over browser chrome.

If application/network/provider data represents the requested material more faithfully than rendered text, use that representation instead and explain the source in the structured result.

Keep exports bounded. Large binary or streamed resources belong to the artifact/resource path, not model context.

Do not interpret saving rendered GitHub HTML as saving the current GitHub source file; compose with `github` for that case.
