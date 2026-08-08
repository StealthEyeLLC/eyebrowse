# 08 — Workflow Pressure Tests

Status: **Canonical product pressure-test set**

The architecture is judged by real agent workflows, not by the number of protocol methods wrapped.

Build 001 only needs deterministic fixtures plus at least one real modern site to prove Milestones A–D. The broader workflows below drive later hardening.

## 1. Multi-tab research

### Task

Search the web, open many relevant sources, inspect them without repeatedly activating every tab, extract useful data, and keep the working set alive for an extended reasoning session.

### Required capability

- persistent targets/tabs;
- hot/warm/cold semantic state;
- background target queryability;
- stable target/document identity;
- compact observations/deltas;
- Program Host multi-tab iteration;
- downloads/artifacts as needed.

### Architecture failure signal

If the agent must visually activate and rediscover every tab to remember what is there, the architecture is not sufficiently agent-native.

## 2. Gmail

### Task

Operate a persistent authenticated Gmail session, search mail, open threads, draft/reply using rich editors, handle attachments, and wait for updates while working elsewhere.

### Required capability

- durable Chrome profile;
- DOM/AX/APC semantic fusion;
- rich text/editor input;
- virtualized content;
- application/network fallback;
- attachments/download/upload;
- target/document continuity;
- Attention Engine later.

### Failure signal

If editor/state handling requires per-site coordinate hacks or if every rerender destroys all references, identity/action architecture needs work.

## 3. Google Drive

### Task

Navigate folders, search, upload generated files, wait for completion, open resulting artifacts, and download/export files.

### Required capability

- persistent auth;
- virtualized list handling;
- file inputs/chooser interception;
- network/file lifecycle;
- downloads/artifacts;
- dialogs/popups;
- Program Host for bulk operations.

## 4. GitHub

### Task

Search repositories/issues/PRs, open related items in multiple tabs, inspect code/editor/state, upload/download files where applicable, and leave useful working tabs alive.

### Required capability

- semantic links/buttons/forms;
- target/opener graph;
- code/rich editor interactions;
- network/API visibility;
- Program Host multi-tab iteration;
- persistent logical references.

GitHub is a good early real-site Build 001 pressure test because it exercises modern dynamic DOM behavior without requiring the full native Windows boundary.

## 5. Complex enterprise SaaS

### Task

Operate a large SPA containing nested frames, shadow DOM, virtualized grids, custom controls, modal dialogs, and background API traffic.

### Required capability

- OOPIF/frame graph;
- shadow DOM;
- logical identity/rebinding;
- collection abstraction;
- application/network data provider;
- event-driven waits;
- Playwright/raw CDP/JS action routing;
- visual/native fallback when necessary.

## 6. Multi-page form

### Task

Complete a complex form across navigation steps with validation, conditional fields, date/select/radio controls, and uploads.

### Required capability

- form semantics;
- labels/AX/APC;
- validation state;
- navigation/document lifecycle;
- persistent conceptual identity where same-page rerenders occur;
- file input handling;
- event-driven completion.

## 7. Popup OAuth

### Task

Begin an OAuth flow from one target, operate the popup/new target through redirects, and return to the authenticated originating app.

### Required capability

```text
origin target
→ opener relationship
→ OAuth popup DocumentInstances
→ redirects
→ popup close/authenticated origin state
```

No special automation-framework session abstraction should be required.

## 8. Long-running server operation

### Task

Start a job that takes minutes, work in another tab, and resume when the relevant job completes.

### Required capability

- network/DOM/application events;
- compound waits;
- post-Build-001 Attention Engine;
- persistent target state;
- compact delta notification.

### Failure signal

Repeated “is it done yet?” agent polling is architectural failure once the Watch Engine exists.

## 9. Rich text editor

### Task

Edit formatted text in contenteditable/ProseMirror/CodeMirror/Monaco-like surfaces, select ranges, paste/insert text, submit changes, and detect completion.

### Required capability

- real focus;
- CDP keyboard/text input;
- selection APIs;
- DOM/application state;
- Playwright adapter where useful;
- JS escape hatch.

## 10. Virtualized table/grid

### Task

A UI displays only 30 mounted rows from an 8,000-row dataset. Find all records matching a business predicate.

### Desired strategy

```text
network/application data
→ structured dataset query
→ rendered semantic correlation only where action is required
```

rather than:

```text
scroll 8,000 rows
→ scrape mounted DOM repeatedly
```

This is a flagship “richer than human-visible UI” capability.

## 11. GraphQL-heavy application

### Task

Understand which operations populate the visible UI, query structured responses, and correlate rendered objects with their likely producers.

### Required capability

- request/response metadata;
- GraphQL operation/variables/response indexing;
- lazy response bodies;
- producer correlation;
- semantic object graph.

## 12. Canvas-heavy interface

### Task

Operate an application where important controls/content are drawn into canvas and not represented sufficiently in ordinary DOM/AX.

### Strategy hierarchy

1. application/runtime state if available;
2. backing network data;
3. structured overlays/accessibility if available;
4. screenshot/temporal vision;
5. exact browser pointer coordinates;
6. native input only if necessary.

## 13. WebGL/WebGPU interface

### Task

Interact with a rendered 3D/data interface with minimal conventional DOM.

### Required capability

- application JS/runtime inspection;
- network data;
- vision/temporal perception;
- accurate coordinate transforms;
- CDP pointer input.

Generic GPU-command reversal is not required initially.

## 14. Download and inspect

### Task

Trigger an authenticated or blob download, wait for exact completion, distinguish duplicate filenames, obtain the actual artifact, and parse/use it.

### Required capability

- download lifecycle object `dl_*`;
- artifact handle `a_*`;
- initiator association;
- progress/completion/cancellation;
- deterministic resulting file path;
- direct resource extraction when UI download adds no value.

## 15. Upload generated file

### Task

Generate a file locally, set it on a hidden/native file input or upload zone, and wait for actual application/network completion.

### Required capability

- artifact/data plane;
- `DOM.setFileInputFiles`/equivalent;
- file chooser interception;
- drag/drop fallback;
- network/application completion condition.

## 16. PDF workflow

### Task

Open a PDF-linked resource, extract text/metadata, inspect pages/images, navigate the browser viewer when needed, and create a new PDF from web content.

### Required capability

- source PDF retrieval;
- parser/artifact integration;
- page screenshots;
- OCR only for scanned pages;
- Chrome viewer interaction;
- print-to-PDF.

## 17. Media/streaming application

### Task

Inspect playback state, captions, media metadata, stream/network behavior, and interact with controls.

### Required capability

- HTML media state;
- tracks/captions;
- Network streaming metadata;
- Media/WebAudio providers;
- vision/audio fallback only when structured information is insufficient.

## 18. Browser/native boundary

### Task

A web workflow invokes a browser permission surface, native file dialog, print dialog, extension UI, or external protocol/application.

### Required capability

```text
web semantic object
→ target/browser transition
→ NativeWindow/UIA object
→ native action
→ return to browser world
```

The agent should not experience the web/native transition as an unrelated second automation product.

## 19. Kernel crash after hundreds of actions

### Task

After extended authenticated work across many tabs, kill `AgentBrowser.Kernel` while Chrome/documents remain alive, restart, and continue.

### Required capability

- persistent Chrome process/profile;
- runtime descriptor;
- recursive target rediscovery;
- extension/document exact bindings;
- semantic reconciliation;
- bounded state with no need to replay the whole action history.

This is Milestone C at larger scale.

## 20. Chrome crash

### Task

Chrome itself dies after extended work.

### Required behavior

- increment BrowserIncarnation;
- relaunch same profile when appropriate;
- allow Chrome/profile session restoration;
- rediscover targets/documents;
- recover conceptual workspace where genuinely possible;
- mark impossible exact node identities stale rather than pretending the renderer survived.

## 21. Hundreds/thousands of sequential actions

### Task

Operate for hours without progressively degrading.

### Required properties

- bounded cursor/event state;
- controlled provider activation;
- no unbounded remote-object handle leaks;
- no unbounded DOM/AX snapshot accumulation;
- correct identity lifecycle;
- target command serialization where needed;
- restartable optional adapters/workers;
- compact context/payloads.

### Key measurements

- memory growth per target/action count;
- false identity rebinds;
- stale object rate;
- event/delta backlog;
- attach/recovery latency;
- semantic payload size;
- Program Host operation efficiency.

## 22. Sustained attention

### Task

Watch several independent browser conditions over a long period while the agent performs unrelated work.

Examples:

```text
report completed
new message arrived
job status changed
expected tab appeared
download finished
particular application value changed
```

### Required capability

Post-Build-001 Attention Engine with persistent event predicates and low reasoning-model wakeup rate.

## 23. Multi-agent/multi-profile operation

### Task

Several agent workflows operate different profiles/contexts/tabs concurrently.

### Required capability

- independent profile Chrome processes;
- context/target scheduling;
- concurrent observation/network processing;
- per-target input queue/lease to avoid mechanical races;
- no artificial permission hierarchy.

## 24. Evaluation principle

A workflow is evidence about architecture only when it stresses a real capability boundary.

Do not add per-site special-case code as the default response to failures. First ask whether the failure reveals a missing generic primitive in:

```text
identity
lifecycle
semantic representation
action routing
network/application state
visual grounding
native boundary
```

The goal is a browser operating environment whose generic capabilities compose across sites.
