---
name: forms
description: Fill and submit browser forms through eyeBROWSE semantic objects with local batching, including text fields, selects, checkboxes, contenteditable controls, and file inputs when represented by the browser. Use for requests such as “fill this form”, “update all these fields”, “submit this application”, or any multi-field browser workflow where one Program Host batch should replace one model round trip per field.
---

# Forms

Observe/query semantic fields and keep their `e_*` identities. Do not compile the form into selector macros.

Use `common.batch-form-fill` when the requested fields are resolved. Batch deterministic fills/selects/checks locally; submit only when the user intent includes submission.

Use `action.fill` for ordinary editable text, `action.select` for select controls, `action.check`/`action.uncheck` for checked state, and `file.upload` for direct file inputs.

Reobserve or wait after state-changing interactions. If an object becomes stale/ambiguous, resolve it conservatively rather than redirecting to a nearby field.

Native OS file dialogs remain DESKTOPeye territory unless the browser file input can be set directly.
