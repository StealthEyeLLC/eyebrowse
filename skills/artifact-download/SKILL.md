---
name: artifact-download
description: Acquire browser-associated files and attachments through eyeBROWSE download/resource semantics, including authenticated or blob-backed resources that the live Chrome profile can legitimately resolve. Use for requests such as “download this”, “get every attachment”, “download all PDFs in this list”, saving browser-originated artifacts, waiting for completion, or associating a requested resource with Chrome download state without reconstructing authentication outside the browser.
---

# Browser artifact download

Resolve the current target before initiating a download. Use `common.download-resource` for one deterministic browser resource and compose local loops for bounded collections.

Treat Chrome download events as transfer truth: begin, progress, completion/cancellation, suggested filename, and material path. Wait for browser-reported completion before declaring success.

Use the live profile for authenticated/blob-backed resources. Do not copy cookies into a custom auth store.

For a true Git working copy, compose with `github` + CODEeye instead of treating a repository ZIP as Git semantics.

Return compact artifact metadata and material paths. Do not create a permanent action receipt/history system.
