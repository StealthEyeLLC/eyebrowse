---
name: accessibility-debug
description: Inspect and diagnose browser accessibility through eyeBROWSE semantic e_* correspondence, Accessibility-tree provider data, focus/labels/relationships, and optional on-demand Lighthouse findings. Use for accessible-name, role, focus-order, label, tree-integrity, keyboard-structure, or inaccessible-element questions where browser accessibility truth should be preferred over screenshot or OCR guesses.
---

# Accessibility debugging

Use `accessibility.audit` for a compact current-page pass and `accessibility.inspect(e_*)` for a specific persistent semantic concept. Accessibility nodes are provider representations of the same browser concepts, not a second identity universe.

Use raw `Accessibility.*` only when a deeper provider property or full-tree relationship is genuinely needed. Correlate backend DOM nodes to existing `e_*` whenever evidence supports it.

Prefer structured browser accessibility state over pixels. Use vision only when the question is explicitly visual or structural providers cannot answer it.

For broader developer health or agent readiness, compose with `agent-readiness` and on-demand Lighthouse. Lighthouse remains a diagnostic, never a mandatory action gate.
