---
name: gmail-web
description: Operate Gmail Web as a second structurally different authenticated browser application through eyeBROWSE while keeping Gmail-specific concepts procedural rather than kernel state. Use for requests grounded in the current Gmail browser tab such as “this thread”, search/list traversal, opening messages, comparing threads, downloading visible attachments, handling compose/editor workflows, or testing eyeBROWSE generalization across virtualized collections, rich text, multi-step application state, and persistent authentication.
---

# Gmail Web procedural operator

Use neutral eyeBROWSE browser concepts for tab/document/element identity. Keep Gmail thread/message/label interpretation in this Skill.

## Resolve browser context

Start with `context.current`. Require a Gmail Web origin supported by current evidence. Preserve the current `t_*` target rather than silently switching to another Gmail tab.

## Prefer provider truth for mailbox facts

If the connected Gmail provider is available and the user asks about actual mailbox/message state, use it as provider truth. Use eyeBROWSE for the rendered Gmail UI, current selection, virtualized list behavior, editor state, browser downloads, and page-specific interaction.

## Virtualized collections

Do not assume all inbox/search rows are simultaneously rendered. Prefer Gmail application/provider data where available; otherwise use bounded search/pagination/scroll procedures while preserving semantic identities. Do not dump every intermediate row into model context.

## Compose and rich text

Use semantic contenteditable/browser objects and local batching for deterministic field/body updates. Reobserve after actions that change draft/send state. Native OS file pickers remain a DESKTOPeye boundary; direct file inputs may use eyeBROWSE upload semantics.

## Attachments

Compose with `artifact-download` for browser-native downloads. When the user asks about attachment metadata or message state rather than browser transfer state, use the Gmail provider.

## Second-site purpose

Use this Skill during Build 002 acceptance to pressure virtualization, rich text, search, attachments, multi-step state, and persistent auth. Do not add Gmail ontology to the Browser World Graph to make the test pass.
