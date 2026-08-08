# 02 — Build 001: Browser Kernel Slice

Status: **COMPLETE — four-milestone acceptance passed 2026-08-08**
Priority: **P0 / implemented before secondary architecture**

Implementation evidence: [`09-BUILD-001-RESULTS.md`](09-BUILD-001-RESULTS.md). Broader workstream items not required by the four formal completion gates are explicitly listed there as post-gate boundaries rather than silently claimed complete.

## 1. Purpose

Build 001 is the first real eyebrowse implementation. It is not a disposable proof of concept and not merely scaffolding.

Its purpose is to establish the permanent architecture spine and prove the four properties that make eyebrowse fundamentally different from ordinary browser automation:

### Milestone A — Persistent Browser

> Chrome runs independently of eyebrowse and survives eyebrowse kernel/controller exit.

### Milestone B — Persistent Agent Objects

> The agent sees compact semantic state containing logical objects such as `e_42`, can act on those objects without screenshot rediscovery, and receives deltas instead of complete page rediscovery after ordinary actions.

### Milestone C — Recovery Continuity

> Kill the eyebrowse kernel while Chrome remains alive, restart it, reattach to the same browser/profile/tabs/documents, reconstruct the world, and recover exact surviving document/object identities wherever the underlying document survived.

### Milestone D — Programmable Browser

> One local Agent Program Host invocation can perform a meaningful workflow involving at least 20 browser operations using eyebrowse queries, persistent references, actions, waits, JavaScript/network information, and tab state without requiring one reasoning-model round trip per browser operation.

These four milestones define **Build 001 complete**.

## 2. Build philosophy

### Vertical slice, not framework skeleton

Every first-build component must contribute to the Milestone D acceptance demonstration.

Do not spend Build 001 implementing broad but unneeded surfaces such as WebRTC, custom Chromium, deep visual grounding, full Windows native integration, or cross-browser parity.

### Permanent architectural foundations

Although scope is deliberately narrow, these interfaces/models are intended to survive into the end-state architecture:

- direct dynamic CDP transport;
- Browser World Graph concepts;
- logical target/document/element IDs;
- extension document-side identity;
- semantic `observe`/`query` API;
- cursor/delta observation;
- event-driven waits;
- raw CDP/JavaScript/network access;
- Program Host calling a persistent kernel.

### Constraints remain active

Build 001 must not introduce:

- project-specific permission systems;
- allow/deny policy engines;
- approval layers;
- verification agents/stages;
- mandatory double-check loops;
- action ledgers;
- receipt/evidence systems;
- permanent execution-history databases.

Normal state checks, browser hit testing, actionability, stale-object handling, event completion conditions, tests, assertions, and debugging are intrinsic engineering/browser functionality and are allowed.

## 3. Initial repository shape

Build 001 begins compactly:

```text
eyebrowse/
├─ README.md
├─ docs/
│
├─ src/
│  ├─ AgentBrowser.Kernel/
│  ├─ AgentBrowser.Cdp/
│  ├─ AgentBrowser.State/
│  ├─ AgentBrowser.Actions/
│  ├─ AgentBrowser.Network/
│  ├─ AgentBrowser.Artifacts/
│  └─ AgentBrowser.Cli/
│
├─ extension/
│  └─ agent-bridge/
│
├─ program-host/
│  ├─ src/
│  └─ sdk/
│
├─ tests/
│  ├─ fixtures/
│  ├─ integration/
│  ├─ identity/
│  └─ reconnect/
│
└─ experiments/
   └─ apc/
```

Do not create additional projects until implementation pressure justifies them.

## 4. Machine bootstrap

### Filesystem

Create:

```text
C:\AgentBrowser\
    Profiles\
        dev\

X:\AgentBrowser\
    Artifacts\
    Temp\
```

The `dev` profile is disposable during implementation but is still launched as a real persistent Chrome profile rather than an automation-framework temporary context.

### Existing required dependencies

Already present on STEALTHEYELLC:

- Windows 11 x64;
- Google Chrome 151.0.7922.109;
- .NET SDK 10.0.302;
- Git 2.55.0.windows.3.

### Dependency to install for Build 001

Install Node 24 LTS for:

- TypeScript extension build;
- Program Host;
- optional JS developer tooling.

Do not install Python, Rust, Java, C++, Docker, Selenium, or Puppeteer unless a Build 001 implementation blocker demonstrates a real need.

### Optional Playwright

Do not make Playwright a Build 001 prerequisite for initial direct actions. Direct CDP Input/DOM operations should prove the spine first.

If difficult controls make Playwright's actionability machinery clearly useful during Build 001, add Playwright as a replaceable action provider without giving it browser ownership.

## 5. Workstream 1 — direct CDP spine

Projects:

```text
AgentBrowser.Cdp
AgentBrowser.Kernel
AgentBrowser.Cli
```

### Chrome launcher

Implement:

- locate installed Chrome;
- choose dedicated profile directory;
- choose available nonzero loopback debug port;
- launch headful Chrome with a minimal command line;
- write small current runtime descriptor beside eyebrowse state (profile, port, process association, browser incarnation);
- detect an already-running eyebrowse-owned Chrome and attach instead of spawning duplicates.

Conceptual launch:

```text
chrome.exe
  --user-data-dir=C:\AgentBrowser\Profiles\dev
  --remote-debugging-address=127.0.0.1
  --remote-debugging-port=<allocated>
  --no-first-run
```

### CDP transport

Implement:

- WebSocket transport;
- monotonically assigned request IDs;
- command response correlation;
- flattened target session routing;
- event dispatch;
- cancellation/timeouts;
- connection/disconnection lifecycle;
- protocol error surfacing;
- bounded message parsing/allocation discipline.

### Dynamic protocol

At attach:

```text
GET /json/version
GET /json/protocol
Browser.getVersion
```

Build a `CapabilityRegistry` from the actual browser.

Provide a typed layer for methods used in Build 001, but preserve:

```text
cdp.send
cdp.subscribe
```

from the beginning.

### Initial CLI

Build early, before AI integration:

```text
eyebrowse browser start dev
eyebrowse browser attach dev
eyebrowse browser status
eyebrowse target list
eyebrowse cdp send <session?> <method> <json>
```

The CLI is the primary engineering/debug interface for Build 001.

## 6. Milestone A acceptance gate

Milestone A is complete only when the following exact sequence works repeatedly:

1. Run `eyebrowse browser start dev`.
2. Chrome launches headful using `C:\AgentBrowser\Profiles\dev`.
3. Kernel attaches through the browser-level CDP WebSocket.
4. `target list` shows the live browser/page targets.
5. Open/navigate a tab through eyebrowse.
6. Evaluate JavaScript through CDP Runtime.
7. Exit/kill the eyebrowse kernel process.
8. Confirm Chrome remains alive with the same open tab/profile state.
9. Start the kernel again.
10. Kernel discovers the existing runtime descriptor/process/port and reattaches without restarting Chrome.

If Chrome ownership is accidentally tied to kernel lifetime, do not proceed to Milestone B until fixed.

## 7. Workstream 2 — Browser World Graph

Project:

```text
AgentBrowser.State
```

Implement a minimal but lifecycle-correct graph.

### Core object IDs

Use compact logical IDs:

```text
browser profile    profile:dev
browser instance   b_1
context            c_1
target             t_1
frame slot          f_1
document            d_1
execution context   x_1
worker              w_1
element concept     e_1
semantic region     r_1
request             req_1
download            dl_1
artifact            a_1
```

Raw Chrome IDs remain internal metadata.

### Target discovery

Use target discovery/flattened recursive auto-attachment and maintain:

- pages/tabs;
- popup pages;
- frames/OOPIFs;
- workers;
- shared workers;
- service workers;
- extension targets when present;
- unknown/future target types as generic target nodes.

### Frame/document distinction

Do not model a frame as if it is the document.

Build 001 minimum:

```text
Target
  → FrameSlot
      → current DocumentInstance
```

Track:

- frame ID;
- parent frame;
- loader/document incarnation;
- URL/origin;
- Runtime execution contexts;
- extension document ID once the extension arrives.

Full BFCache/prerender lifecycle hardening can be post-Build-001, but the data model must leave room for multiple/dormant DocumentInstances rather than making `frame == document` irreversible.

### Target graph fixture

Create a local test fixture containing:

- top page;
- same-origin iframe;
- cross-origin iframe where practical;
- dedicated worker;
- service worker;
- popup/new tab.

The graph must show coherent parent/opener relationships and survive ordinary navigations.

## 8. Workstream 3 — first-party actions

Project:

```text
AgentBrowser.Actions
```

Implement sufficient operations for Build 001:

```text
navigate.go
navigate.back
navigate.forward
navigate.reload

action.click
action.doubleClick
action.hover
action.fill
action.type
action.key
action.scroll
action.focus

js.evaluate
js.call
```

### Pointer pipeline

For a structured element:

```text
ElementConcept
→ current browser node
→ geometry
→ optional browser hit test
→ CDP Input dispatch
```

Coordinates come from browser layout, never model estimation when a browser node is available.

### Text pipeline

Prefer real focus and browser input semantics:

```text
resolve editable
→ focus
→ input/keyboard/text insertion
```

Direct value manipulation can exist as an explicit semantic/DOM operation but should not be disguised as keyboard input.

### File input

If easy during Build 001, expose direct file input assignment because it is useful for acceptance workflows. Full native file-dialog handling is post-slice.

## 9. Workstream 4 — semantic observation

Initial providers:

```text
DOM
DOMSnapshot
Accessibility
Runtime
APC probe when available
```

### Initial document build

On a new active document:

1. establish frame/document/runtime context;
2. obtain DOM/DOMSnapshot baseline;
3. obtain accessibility semantics;
4. probe APC if the capability exists;
5. derive semantic regions/interactables;
6. assign logical `e_*` IDs;
7. return a compact surface.

Run independent browser queries in parallel where safe.

### Surface representation

Implement:

```text
observe.surface(target?)
```

Minimum fields:

```json
{
  "cursor": 28,
  "target": "t_3",
  "document": "d_7",
  "url": "...",
  "title": "...",
  "focus": "e_12",
  "navigation": "idle",
  "regions": [
    {"id":"r_2","role":"main","name":"Repository"}
  ],
  "interactables": [
    {
      "id":"e_12@1",
      "role":"textbox",
      "name":"Search",
      "state":["focused"],
      "actions":["fill","type"]
    },
    {
      "id":"e_18@1",
      "role":"button",
      "name":"Issues",
      "actions":["click"]
    }
  ],
  "alerts": [],
  "changes": []
}
```

### First query API

Implement enough of:

```text
query.find(role?, name?, text?, state?, within?)
inspect.element(e_*)
inspect.dom(e_* or document)
inspect.ax(e_* or document)
```

Do not expose full HTML/AX dumps as the normal agent interface.

## 10. Logical identity v1

Before the extension exists, each useful semantic object receives a logical ID backed by:

```text
DocumentInstance
BackendNodeId?
NodeId?
AXNodeId?
APC identity?
role
name
important attributes
text fingerprint
geometry
semantic neighborhood
```

Build 001 first goal is correct identity inside a surviving document, not perfect semantic rebinding.

Identity states:

```text
exact
rebound
stale
ambiguous
```

Internally track an element binding incarnation so diagnostics can show:

```text
e_42@1
e_42@2
```

while the conceptual logical ID remains `e_42`.

## 11. Milestone B acceptance gate

Milestone B is complete when a real modern page can be operated through semantic state without routine screenshots.

Required demonstration:

1. Open a real page such as GitHub plus local hostile fixtures.
2. `observe.surface` returns a compact set of useful regions/interactables.
3. A button/input receives an `e_*` logical reference.
4. `click e_*`, `fill e_*`, `key`, `scroll`, and navigation work using the logical object and browser geometry.
5. `inspect.element e_*` returns deeper structured state.
6. The same logical ID remains usable across ordinary non-destructive mutations.
7. A full HTML/AX snapshot is not required after each action.

Milestone B is not complete if eyebrowse still behaves like `snapshot → assign disposable refs → action → complete snapshot` for every ordinary step.

## 12. Workstream 5 — MV3 agent-bridge

Directory:

```text
extension/agent-bridge/
```

Build in TypeScript.

### Manifest goals

- Manifest V3;
- content script at `document_start`;
- all frames where permitted;
- isolated-world core instrumentation;
- service worker for browser/extension communication;
- targeted main-world injection only when necessary.

### Document identity state

Core isolated-world data structures approximately:

```javascript
const nodeToSerial = new WeakMap<Node, number>();
const serialToNode = new Map<number, WeakRef<Node>>();
```

Expose/maintain:

```text
document identity
NodeSerial
NodeSerial ↔ logical e_* binding
focus
selection
input/change state
MutationObserver batches
scroll state
```

### Kernel communication

For Build 001, a localhost WebSocket channel is acceptable:

```text
content/document
→ extension worker
→ loopback WebSocket
→ kernel
```

The design must tolerate extension worker termination/reconnect.

Document-resident identity is more important than worker immortality.

### Bootstrap/reconnect handshake

When the kernel connects/reconnects, it must be able to request:

```text
which instrumented documents are alive?
which NodeSerials are alive?
which eyebrowse logical e_* bindings do those documents still know?
```

The kernel merges that exact surviving information into the reconstructed Browser World Graph.

## 13. Workstream 6 — delta engine

Create a bounded monotonic cursor per relevant browser/world scope.

Input signals for Build 001:

- document MutationObserver batches;
- CDP DOM/lifecycle events where useful;
- accessibility updates;
- focus/input/selection;
- navigation/document changes;
- target create/destroy;
- Runtime exceptions/contexts;
- Network request/response completion;
- download events if implemented.

### Coalescing

Do not forward every mutation to the agent.

Example raw input:

```text
342 DOM mutations
14 layout-related changes
6 AX changes
1 POST
```

Desired semantic output:

```text
cursor 421 → 426
changed:
  e_42.text: "Generating..." → "Complete"
added:
  e_77 button "Download"
network:
  POST /generate → 200
```

### API

Implement:

```text
observe.delta(sinceCursor)
```

and return the newest cursor with each ordinary action result where useful.

The cursor ring is bounded operational state, not permanent history.

## 14. Workstream 7 — semantic rebinding

Create hostile deterministic test fixtures before relying on real sites.

Required cases:

- DOM node destroyed and replaced with semantically identical control;
- list reordering;
- duplicate visible labels;
- virtualized/unmounted/re-mounted rows;
- SPA route changes;
- full new document navigation;
- same-name controls in separate semantic regions.

Rebinding evidence order:

1. surviving extension NodeSerial/exact binding;
2. surviving backend/AX/browser identity;
3. strong application identifiers;
4. `id`, `name`, stable `data-*`;
5. role + accessible name + label;
6. form/landmark/region membership;
7. href/action/value semantics;
8. local text/tree fingerprint;
9. neighboring logical objects;
10. geometry/proximity.

### Primary metric

Measure **false rebind rate**, not just successful recovery rate.

If two candidates remain plausible, return:

```text
stale_ambiguous
```

rather than silently choosing one.

## 15. Workstream 8 — event-driven wait engine

Implement:

```text
wait.until(predicate)
```

Build 001 predicates:

```text
element visible
element gone
element changed
navigation/document changed
new target created
target closed
network request finished
text/semantic condition
JS expression
any(...)
all(...)
sequence(...)
quiet_for(...)
```

Wait predicates are reevaluated when relevant underlying events occur.

Avoid ordinary fixed sleeps.

Allow action + completion condition:

```text
click(e_42, wait = element(e_77).visible)
```

No separate mandatory post-action verification stage is introduced.

## 16. Workstream 9 — Network spine

Project:

```text
AgentBrowser.Network
```

Minimum Build 001 network state:

```text
logical request ID
raw CDP request ID
frame/document/loader
URL
method
resource type
initiator
request headers/status where available
response status/MIME/timing
redirect chain
completion/failure
```

Expose:

```text
network.search
network.request
network.response
network.body
network.websocket
```

### Lazy bodies

Do not retain every response body.

Fetch bodies on demand, with bounded optional caching for active workflow data.

### GraphQL

Implement lightweight GraphQL recognition where obvious:

```text
endpoint
operationName
variables
response association
```

This is useful for Build 001 Program Host demonstrations and proves deeper-than-human application visibility.

### WebSocket/SSE

Index basic WebSocket frames and EventSource events if straightforward in the same Network event path.

## 17. Workstream 10 — downloads/artifacts

Project:

```text
AgentBrowser.Artifacts
```

Minimum artifact paths:

```text
X:\AgentBrowser\Artifacts\
```

Logical objects:

```text
dl_*
a_*
```

Build 001 download fields:

```text
logical ID
source URL
initiating target/frame when known
suggested filename
actual path
state
received/total bytes when available
```

Expose:

```text
download.list
download.wait
download.path
download.cancel
```

No native Windows download dialog automation is required for the first slice.

## 18. Workstream 11 — controller-death recovery

This is the defining Milestone C path.

### Runtime descriptor

Persist only current operational information needed to rediscover the browser:

```text
profile logical ID
profile path
browser incarnation
Chrome executable
process association where useful
CDP port/endpoint
time last attached
```

Do not persist browser action history.

### Recovery sequence

On kernel startup:

1. enumerate configured eyebrowse profiles;
2. locate an already-running Chrome for the profile through runtime descriptor/process command line/endpoint;
3. reconnect to browser CDP;
4. retrieve the browser's current protocol schema;
5. rediscover targets and recursively attach;
6. reconstruct contexts/targets/frames/documents/execution contexts;
7. reconnect extension worker/document instrumentation;
8. request surviving exact `DocumentInstance`/NodeSerial/logical-element bindings;
9. restore those exact bindings;
10. semantically reconcile the remainder;
11. mark unrecoverable old objects stale/ambiguous;
12. continue without restarting Chrome.

## 19. Milestone C acceptance gate — killer demo

Required exact demonstration:

1. Launch eyebrowse-owned persistent Chrome.
2. Navigate to a page/fixture and observe at least 20 useful `e_*` objects.
3. Perform actions and create at least one changed/rebound object.
4. Record the current logical IDs/cursor only for the acceptance test harness.
5. Force-kill the kernel process.
6. Confirm Chrome remains visibly alive and interactive.
7. Do not reload the page manually.
8. Restart kernel.
9. Kernel reattaches to the exact running Chrome.
10. Browser World Graph reconstructs targets/frames/documents.
11. Extension reports surviving exact document-side logical bindings.
12. Previously observed surviving `e_*` IDs are resolvable again.
13. Perform another action using one of those recovered IDs.
14. Observe a new semantic delta.

Success means continuity is materially better than rediscovering the entire browser after controller death.

If exact surviving bindings cannot be recovered, investigate before declaring Milestone C complete. Semantic rediscovery alone is not the intended milestone.

## 20. Workstream 12 — Agent Program Host

Directory:

```text
program-host/
```

Runtime:

```text
Node 24 LTS
TypeScript/JavaScript
```

The Program Host connects to the persistent kernel API; it does not connect directly to Chrome as a second canonical control plane.

### Initial SDK

Expose typed wrappers around:

```text
browser/target listing
observe/query/inspect
actions
navigation
wait
JavaScript
network search/body
artifacts/downloads
raw CDP if needed
```

### API

Minimum:

```text
program.run(code/file)
program.eval(code)
```

Persistent program sessions are desirable but may be deferred until immediately after Milestone D if they complicate the first slice.

### Program behavior

A program can:

- query multiple targets;
- loop over semantic collections/results;
- branch;
- wait on browser conditions;
- operate tabs concurrently where independent;
- filter network/application data;
- execute many local browser actions;
- return a compact structured result.

The Program Host must be disposable. Killing it must not damage the browser/kernel world.

## 21. Milestone D acceptance gate

The final Build 001 demonstration must require at least 20 meaningful browser operations and execute as **one Program Host invocation** from the reasoning client's perspective.

Suggested acceptance workflow:

1. Open a local/real page with multiple relevant links/items.
2. Query a semantic collection of items.
3. Iterate/filter them locally.
4. Open a subset in new tabs.
5. inspect each tab's semantic state and/or network response.
6. wait on at least one actual event condition.
7. perform at least one form/input action.
8. collect structured results.
9. return a concise result and leave the useful tabs alive.

Alternative real workflow once stable:

> Search GitHub for eyebrowse-related material, inspect result/issues/PRs across several tabs, filter the useful ones, collect structured metadata, and leave the relevant tabs open.

Milestone D is complete when a workflow that would otherwise take dozens of model/tool turns can execute mostly inside the Program Host against persistent eyebrowse state.

## 22. CLI surface at Build 001 completion

Target shape:

```text
eyebrowse browser start <profile>
eyebrowse browser attach <profile>
eyebrowse browser status
eyebrowse browser stop <profile>

eyebrowse target list
eyebrowse target open <url>
eyebrowse target activate <t_id>
eyebrowse target close <t_id>

eyebrowse observe [t_id]
eyebrowse observe --since <cursor>

eyebrowse query ...
eyebrowse inspect <e_id>

eyebrowse click <e_id>
eyebrowse fill <e_id> <text>
eyebrowse type <e_id> <text>
eyebrowse key <key>
eyebrowse scroll ...

eyebrowse wait ...

eyebrowse js eval ...

eyebrowse network list
eyebrowse network body <req_id>

eyebrowse download list
eyebrowse download wait <dl_id>

eyebrowse program run <file>

eyebrowse cdp send ...
```

Exact CLI syntax may evolve; semantic API concepts are more important than flags.

## 23. Kernel API shape for Build 001

Internal/client protocol can be duplex JSON-RPC over loopback WebSocket.

Minimum operations:

```text
browser.start
browser.attach
browser.status

target.list
target.open
target.activate
target.close

observe.surface
observe.delta
query.find
inspect.element

action.click
action.fill
action.type
action.key
action.scroll
navigate.go
navigate.back
navigate.reload

wait.until

js.evaluate
js.call

network.search
network.request
network.response
network.body
network.websocket

download.list
download.wait
download.path

cdp.send
cdp.subscribe
```

The API must support events/streaming rather than forcing clients to poll all state.

## 24. Build 001 data/storage policy

Use SQLite only if/when useful for current durable state.

Permitted durable operational records:

- profile registry;
- browser runtime descriptor/incarnation;
- logical target/document mappings needed for recovery;
- exact identity metadata needed for restart recovery;
- artifact metadata.

In memory:

- full semantic graph;
- event subscriptions;
- short bounded cursor/delta ring;
- transient network state/buffers;
- current action/wait state.

Explicitly absent:

- action ledger;
- permanent browser event history;
- mandatory HAR for each session;
- execution receipts;
- evidence bundles.

## 25. Test strategy

Tests are engineering quality machinery, not a runtime verification architecture.

### Unit

- CDP message routing;
- ID allocation;
- state graph transforms;
- semantic matching/scoring;
- delta coalescing;
- wait predicate evaluation.

### Fixture integration

Local deterministic websites for:

- nested frames;
- workers/service worker;
- popup targets;
- forms;
- React-style node replacement;
- list reorder;
- duplicate labels;
- virtualization;
- mutation storm;
- network/GraphQL;
- long-running state transitions;
- file download.

### Reconnect integration

Separate test process that deliberately kills/restarts the kernel while Chrome remains running.

### Real-site pressure tests

Once deterministic gates pass:

- GitHub;
- Gmail/Drive when authenticated profile is available;
- modern React/Next SaaS pages.

Do not let fragile real-site tests replace deterministic fixtures for core identity/lifecycle behavior.

## 26. Build 001 metrics

Capture measurements in test/benchmark output, not a permanent runtime ledger.

Key metrics:

```text
Chrome attach/reattach latency
CDP event → normalized event latency
initial semantic surface latency
warm query latency
logical element resolution latency
observation payload size
semantic delta payload size
mutation coalescing ratio
false identity rebind rate
identity recovery count after kernel death
working-set memory by target count
Program Host operations per model invocation
```

The most important correctness metric is false semantic rebind rate.

## 27. Explicit Build 001 non-goals

Do not block Build 001 on:

- full Windows SessionHost;
- native dialogs;
- OCR;
- multimodal vision;
- canvas/WebGL grounding;
- temporal screencast understanding;
- WebMCP;
- third-party DevTools tools;
- DBSC/FedCM/WebAuthn depth;
- PDF parser;
- media/WebRTC;
- BrowserGym integration;
- multi-agent scheduler;
- BiDi;
- Edge/Firefox parity;
- Chrome for Testing worker pool;
- custom Chromium;
- full BFCache/prerender correctness beyond leaving the model extensible for it.

These belong to the roadmap after the four-milestone slice.

## 28. Build 001 completion demonstration

Build 001 is complete when one recorded engineering session can demonstrate all four milestones in sequence:

### A — Chrome independent of kernel

```text
start Chrome through eyebrowse
→ open working tabs
→ kill kernel
→ Chrome remains alive
→ reattach
```

### B — agent objects instead of page rediscovery

```text
observe compact semantic surface
→ receive e_42
→ act on e_42
→ receive delta
→ continue using logical IDs
```

### C — object continuity after kernel death

```text
observe e_42
→ kill kernel
→ leave document alive
→ restart kernel
→ recover e_42 exact binding
→ use e_42 again
```

### D — local multi-action program

```text
one Program Host invocation
→ 20+ browser operations
→ queries + actions + waits + multi-tab/network state
→ compact result
```

If all four are real and stable on deterministic fixtures plus at least one real modern site, eyebrowse has successfully established its unique foundation.

## 29. What comes immediately after Build 001

In priority order:

1. harden document lifecycle around BFCache/prerender/renderer swaps;
2. Playwright actuator integration where it measurably improves actions;
3. durable network/application-data graph and producer correlation;
4. persistent Watch/Attention Engine;
5. auth/storage/file depth;
6. packaged Windows SessionHost;
7. visual/temporal perception;
8. WebMCP/application-native developer tools;
9. long-horizon benchmark/soak testing;
10. only then reconsider whether any stock-Chrome capability ceiling justifies Chromium modification.

Build 001 should make all of those additions incremental rather than architectural rewrites.
