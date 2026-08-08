# 01 — End-State Architecture

Status: **Canonical**  
Architecture baseline: **Generation 3 / 2026-08-08**

## 1. Executive decision

Build eyebrowse as a **persistent, event-driven, programmable browser operating environment** centered on stock Google Chrome and direct Chrome DevTools Protocol (CDP).

The canonical hierarchy is:

```text
Stock Chrome Stable / Chrome for Testing
                 │
          direct dynamic CDP
                 │
      ┌──────────▼──────────┐
      │ AgentBrowser.Kernel │
      │      .NET 10        │
      │                     │
      │ Capability Registry │
      │ Browser World Graph │
      │ Identity Engine     │
      │ Representation      │
      │ Broker              │
      │ Delta Engine        │
      │ Attention Engine    │
      │ Action Router       │
      │ Durable Network     │
      │ Artifact Plane      │
      └──────┬───────┬──────┘
             │       │
             │       ├──── optional Playwright actuator
             │
             ├──── MV3 agent-bridge
             │       document identity
             │       browser/page instrumentation
             │       extension-only APIs
             │
             ├──── .NET SessionHost
             │       UIA / HWND / capture
             │       clipboard / native input
             │
             ├──── Vision workers
             │
             └──── Agent Program Host
                     Node 24 LTS / TypeScript
                     persistent REPL/session
                     loops / branches / parallel tabs
```

The central rule is:

> **Chrome owns browser truth. eyebrowse owns agent truth.**

Chrome owns processes, targets, frames, documents, DOM, accessibility, JavaScript execution contexts, network, storage, downloads, rendering, and browser lifecycle.

eyebrowse owns stable logical references, conceptual identity, semantic fusion, compact observations, cursor/delta state, queries, watches, agent programs, cross-provider correlations, and the world model presented to an AI agent.

No automation framework becomes a capability ceiling.

## 2. Why this architecture wins

### Direct dynamic CDP is the canonical Chromium control plane

CDP exposes the broadest practical set of Chromium-specific capabilities: browser/target lifecycle, DOM, DOMSnapshot, Accessibility, Runtime, Network, Fetch, Storage, CacheStorage, IndexedDB, ServiceWorker, Input, Browser downloads, Media, WebAudio, WebAuthn, extensions, PWA, WebMCP, Preload, performance/timeline surfaces, tracing, and lower-level escape hatches.

The running browser exposes its actual protocol schema through its remote-debugging endpoint. eyebrowse therefore must not freeze itself to a static third-party CDP wrapper.

Use:

```text
GET /json/version
GET /json/protocol
Browser.getVersion
```

at attach and construct a live `CapabilityRegistry`.

Generate typed bindings for common high-value methods, but permanently support:

```text
cdp.send(session, method, params)
cdp.subscribe(session, event)
```

Unknown methods and event payloads must pass through the transport rather than being discarded.

### Stock Chrome is the correct default browser

The principal workloads are real authenticated web applications: Gmail, Drive, GitHub, OAuth, SaaS, media, browser extensions, downloads, native dialogs, and long-lived profiles. Stock Chrome gives the actual compatibility/product environment those applications expect.

Use Chrome Stable, headful and GPU accelerated, for durable identities.

Use Chrome for Testing for disposable/version-pinned benchmark workers and reproducible experiments.

Edge can become a secondary Chromium/BiDi target later.

A custom Chromium fork is an explicit escalation path, not the initial foundation. Fork only after a high-value capability is demonstrated to be impossible through stock Chrome + CDP + APC + extension + native integration.

### Playwright is an actuator, not an operating system

Playwright's locator and actionability mechanics are valuable. Its ability to check uniqueness, visibility, stability, hit-target status, and enabled state should be reused where it improves interaction reliability.

But canonical target state, browser lifetime, object identity, network state, and browser capabilities remain owned by eyebrowse/CDP. Playwright must be recreatable without affecting Chrome or the Browser World Graph.

### WebDriver BiDi is interoperability, not the Chrome kernel

BiDi is valuable for future Firefox/cross-browser operation and portable provider implementations. For Chrome-specific maximum capability, direct CDP remains canonical.

### Vision is first-class but not primary

Use structured browser state when it is more exact. Use vision when pixels are genuinely the richest representation: canvas, WebGL/WebGPU, charts, diagrams, image-heavy sites, browser chrome, native UI, and visually meaningful dynamic interfaces.

## 3. Process model

### AgentBrowser.Kernel — persistent .NET 10 process

The kernel is the canonical agent-state process.

Responsibilities:

- Chrome/profile lifecycle;
- CDP transport and runtime capability discovery;
- target/frame/document graph;
- semantic state graph;
- logical identity and reconciliation;
- observation/delta reduction;
- query execution;
- waits and watches;
- action routing;
- JavaScript/runtime access;
- network/application-data index;
- storage and authentication state views;
- downloads/artifacts;
- raw protocol escape hatches;
- client API/IPC.

The kernel is independent from Chrome. Killing it must not intentionally kill persistent Chrome.

### Chrome — persistent per durable profile

Each durable identity owns a dedicated Chrome user-data directory and normally one Chrome browser process.

Examples:

```text
profile:primary
profile:research
profile:github-alt
profile:customer-a
```

Chrome's whole profile is the canonical authenticated state, including cookies, site storage, IndexedDB, Cache Storage, service workers, extension state, browser permissions, and modern browser/device-bound session state where applicable.

### agent-bridge — Manifest V3 extension

Core responsibilities:

- `document_start` instrumentation;
- document-side NodeSerial identity;
- document lifecycle/document IDs;
- focus/selection/input/mutation sensors;
- isolated-world helper state;
- targeted main-world instrumentation when direct app state is useful;
- extension-only browser APIs where they materially add capability.

The extension service worker is never canonical state. It is expected to restart.

### Agent Program Host — Node 24 LTS / TypeScript

The Program Host is a local computational/action plane adjacent to the kernel.

It allows an agent to compose many browser operations in one program invocation:

```javascript
const rows = await browser.query({ within: "e_42", role: "row" });
for (const row of rows) {
  const amount = await row.get("Amount");
  if (amount > 10000) await row.action("select");
}
await browser.action.click("e_save", {
  wait: browser.wait.any(
    browser.wait.text("Saved"),
    browser.wait.graphql("SaveChanges")
  )
});
```

The Program Host does not own browser state. If it dies, Chrome and the .NET kernel survive.

### SessionHost — interactive .NET Windows process

The SessionHost runs in the logged-on interactive Windows session.

Responsibilities:

- HWND/window enumeration;
- Microsoft UI Automation;
- Windows Graphics Capture;
- clipboard;
- browser chrome/native dialogs;
- external applications launched by Chrome;
- native drag/drop;
- native mouse/keyboard fallback;
- packaged OCR/capture capabilities where required.

The existing Eye/StealthEye supervisor may run as `NT AUTHORITY\SYSTEM`, but ordinary GUI interaction belongs in the interactive user session, not session 0.

## 4. Browser/profile strategy

Recommended layout on STEALTHEYELLC:

```text
C:\AgentBrowser\
    Profiles\
        dev\
        primary\
        research\
        ...

X:\AgentBrowser\
    Artifacts\
        downloads\
        screenshots\
        recordings\
        pdf\
        generated\
    Temp\
```

Profiles stay on NTFS. Large artifacts and transient bulk data use the large ReFS volume.

Launch durable Chrome with a minimal explicit command line:

```text
chrome.exe
  --user-data-dir=<dedicated profile>
  --remote-debugging-address=127.0.0.1
  --remote-debugging-port=<assigned nonzero port>
  --no-first-run
```

Do not accumulate automation-framework switches without a concrete capability reason.

Persistent headful/GPU Chrome is the default. Modern unified headless Chrome/Chrome for Testing is appropriate for disposable/background workers.

## 5. Browser World Graph

The canonical state model is not a page object or DOM tree. It is a Browser World Graph:

```text
Machine
└─ BrowserProfile
   └─ BrowserIncarnation
      ├─ BrowserContext
      │  └─ Target
      │     ├─ FrameSlot
      │     │  ├─ DocumentInstance
      │     │  │  ├─ ExecutionRealms
      │     │  │  ├─ SemanticRegions
      │     │  │  ├─ ElementConcepts
      │     │  │  └─ Collections
      │     │  └─ cached/prerender documents
      │     ├─ Workers
      │     └─ ServiceWorkers
      │
      ├─ Network
      ├─ Authentication
      ├─ Storage
      ├─ Downloads
      ├─ Artifacts
      ├─ PageTools
      ├─ VisualRegions
      └─ NativeWindows
```

### BrowserProfile

Stable durable identity such as `profile:primary`.

### BrowserIncarnation

Changes whenever the Chrome process restarts.

### Target

Agent-facing logical target such as `t_41`, mapped to the current CDP TargetId, context, opener/parent relationship, type, and semantic target fingerprint.

### FrameSlot

Represents the structural frame location.

### DocumentInstance

Represents an actual browser document, independently from the frame slot that currently hosts it. The model must support documents moving among active, prerendered, cached/BFCache, and pending-deletion-like lifecycle states without assuming every frame navigation means all conceptual identity is destroyed.

### RendererIncarnation / ExecutionRealm

Renderer/process and execution-context changes are tracked separately from document identity. A renderer/process swap is not automatically conceptual document death.

### ElementConcept

The agent-facing `e_*` object denotes the conceptual browser object previously observed, not merely a current DOM `NodeId`.

## 6. Persistent identity

Persistent conceptual identity is the highest-risk/highest-value subsystem.

### Exact document-side identity

At document start, the extension maintains approximately:

```javascript
WeakMap<Node, NodeSerial>
Map<NodeSerial, WeakRef<Node>>
```

and associates live NodeSerials with eyebrowse logical IDs.

If the kernel dies while Chrome/document execution survives, the kernel can reconnect and ask the document instrumentation for surviving exact bindings instead of rediscovering everything semantically.

### Browser-owned anchors

An `ElementConcept` may hold:

```text
logicalId
DocumentInstance
incarnation
NodeSerial?
BackendNodeId?
NodeId?
AXNodeId?
APC node identity?
Runtime RemoteObject?
application key?
role
accessible name
label
important attributes
text fingerprint
semantic neighborhood
geometry
collection membership
```

`NodeId` is treated as ephemeral. `BackendNodeId`, AX IDs while enabled, document IDs, frame IDs, execution contexts, and extension NodeSerials are stronger anchors within their valid lifetimes.

### Semantic rebinding

When a framework destroys and recreates a node representing the same conceptual object:

```text
e_483@1
   ↓ old node destroyed
semantic/application reconciliation
   ↓
e_483@2
```

Use progressively weaker evidence:

1. surviving exact document-side binding;
2. surviving browser anchor;
3. strong application identifiers;
4. id/name/data attributes;
5. role + accessible name + label;
6. form/landmark/region membership;
7. href/action/semantic value;
8. local text/tree fingerprint;
9. neighboring concepts;
10. geometry/proximity.

Identity outcomes are explicit:

```text
exact
rebound
stale
ambiguous
```

False rebinds are more damaging than returning `ambiguous`.

## 7. Observation and Representation Broker

The agent should receive the smallest high-value state sufficient for the current task, while deeper state remains queryable.

Potential providers:

- Annotated Page Content (APC);
- Accessibility;
- DOM;
- DOMSnapshot;
- Runtime/JavaScript;
- resident document instrumentation;
- application/framework state;
- third-party DevTools tools;
- WebMCP;
- Network/application data;
- browser storage;
- form/autofill semantics where available;
- Performance/Preload/lifecycle information;
- screenshots/visual understanding;
- native UI Automation.

The Representation Broker selects/combines providers based on:

```text
semantic fit
coverage
freshness
latency
serialization/token cost
interaction relevance
```

There is no hard-coded belief that DOM or vision is always best.

Examples:

- “Which 2,000 orders failed?” → network/application data if complete structured data exists.
- “Click the visible Save control.” → semantic element + browser geometry/actionability.
- “What does this unlabeled canvas diagram mean?” → visual provider.
- “Which app component backs this DOM region?” → runtime/developer-tool provider.

### APC

APC is a privileged experimental semantic accelerator when supported. It can provide unusually dense actionable semantic information, but it is not canonical truth and must be hidden behind runtime capability detection/versioning.

### DOMSnapshot

Use for initial/deep document capture: flattened DOM, iframe/shadow information, layout geometry, selected computed styles, paint/scroll information where useful.

Do not capture a full DOM snapshot after every action.

### Accessibility

Use for role, computed accessible name, descriptions, relationships, states, form semantics, and live-region information. Activate with attention to performance and target interest level.

### Resident instrumentation

Observe and reduce:

- MutationObserver batches;
- focus/blur;
- input/change;
- selection changes;
- scroll;
- ResizeObserver;
- IntersectionObserver when useful;
- History API/route/hash transitions;
- shadow-root creation when useful.

Do low-cost reduction in the document before flooding the kernel.

## 8. Compact observations

A normal agent view should resemble:

```json
{
  "cursor": 18422,
  "target": "t_41",
  "document": "d_41_8",
  "url": "https://...",
  "navigation": "idle",
  "focus": "e_873",
  "regions": [
    {"id":"r_7","role":"main","name":"Inbox"}
  ],
  "interactables": [
    {
      "id":"e_873@2",
      "role":"textbox",
      "name":"Search mail",
      "state":["focused"],
      "actions":["fill","type"]
    },
    {
      "id":"e_892@1",
      "role":"button",
      "name":"Compose",
      "actions":["click"]
    }
  ],
  "alerts": [],
  "changes": []
}
```

Ordinary observations omit wrapper nodes, full CSS, unchanged subtrees, giant script state, and every response body.

## 9. Delta-first operation

After initial state, the default is:

```text
observe(since=18422)
```

not a complete re-snapshot.

Underlying signals can include:

- DOM events;
- document MutationObserver;
- accessibility updates;
- frame/target/document lifecycle;
- focus/input/selection state;
- Runtime contexts/errors;
- Network requests/responses;
- WebSocket/SSE activity;
- downloads;
- service-worker/storage events;
- extension events;
- native UIA/window events.

Many low-level events should be coalesced into a small semantic delta:

```text
cursor 18422 → 18431
changed:
  e_92.text: "Saving…" → "Saved"
  e_117.enabled: false → true
added:
  e_119 alert "Changes saved"
removed:
  e_84 progressbar
network:
  POST SaveDraft → 200
```

Keep a bounded operational event/delta ring. Do not turn it into permanent execution history.

## 10. Hot/warm/cold target cognition

Derived semantic state should be interest-driven.

### Hot

Active agent workflow: full semantic providers/deltas as needed.

### Warm

Retain target/frame/document identities, compact indexes, important events, and cheap providers.

### Cold

Retain logical metadata and persistent browser state; rebuild expensive derived state on demand.

This allows many live tabs without retaining heavyweight derived snapshots for all of them continuously.

## 11. Wait and Attention Engine

Browser waiting is a first-class operation.

Examples:

```text
wait element(e_42).visible
wait element(e_42).gone
wait target.created(opener=t_12)
wait navigation(t_12).documentChanged
wait navigation(t_12).idle
wait download(dl_7).complete
wait network.graphql("SaveDraft").finished
wait dialog.present
wait js("window.application.ready === true")
wait any(...)
wait all(...)
wait sequence(...)
wait quiet_for(...)
```

The Attention Engine extends a blocking wait into persistent background observation:

```text
watch.create(...)
watch.cancel(...)
watch.list(...)
watch.next(...)
```

An agent can start a long-running report, work elsewhere, and let the kernel surface the relevant completion event without repeatedly waking the reasoning model.

This is event subscription, not a verification architecture.

## 12. Interaction architecture

Use a routing engine rather than a universal interaction method.

### Agent Program Host

For multi-step work, code can be a better action space than one tool call per browser operation. The Program Host can run loops, branches, filters, parallel tab queries, waits, artifact handling, and many actions in one local invocation.

### Site/application-native operation

Use WebMCP/application-native/runtime tool surfaces when they provide the most exact operation.

### Semantic browser action

Resolve an `e_*` object and perform the appropriate semantic operation.

### Playwright adapter

Use mature actionability/locator semantics for ordinary DOM controls when useful. Never let Playwright own canonical browser state.

### Raw CDP Input

Support precise mouse, keyboard, text, wheel, touch, drag, and gesture dispatch. Coordinates come from browser geometry rather than model guesses. Use browser hit testing when appropriate.

### Direct DOM/Runtime

Use for exact form/state operations, file inputs, JS escape hatches, application internals, and hard-to-act surfaces.

### Visual grounding

Use screenshot/temporal vision to locate visual objects; bind visual objects back to structured `e_*` identities where possible.

### Native UIA/input

Use after interaction crosses from web content to browser chrome/Windows/external applications.

## 13. JavaScript execution

Raw JavaScript is core.

Expose:

```text
js.evaluate
js.call
js.getProperties
js.resolveElement
js.release
js.addBinding
js.preload
```

Support page main world, isolated worlds, frame-specific contexts, workers, async/promises, object handles, structured serialization, bindings, exception details, and browser-side reductions.

Prefer executing filters/aggregations browser-side over transferring huge application objects into model context.

## 14. Durable Network Spine

Network is a first-class application-data subsystem, not merely diagnostics.

Always-on inexpensive metadata:

```text
request ID
frame/document/navigation
URL
method
resource type
status
timing
MIME
initiator
redirect chain
cache/service-worker relationship
WebSocket/SSE metadata
```

Fetch bodies lazily.

For hot workflows, use bounded durable response-body storage where current CDP supports it so useful response data can survive renderer/process navigation better than ordinary renderer-local buffering.

Support:

- request/response search;
- request bodies;
- response bodies;
- body search;
- streaming large responses;
- WebSockets;
- SSE/EventSource;
- GraphQL classification;
- interception through Fetch when explicitly needed;
- request modification/fulfillment/failure;
- request initiator/JavaScript stack correlation when useful;
- direct downloadable-resource extraction.

### Producer correlation

Build a useful correlation graph:

```text
network request/response
  → document/frame
  → application/runtime activity
  → semantic mutations/objects
```

Expose queries such as:

```text
network.correlate(e_317)
```

as likely producer correlation, not magical proof of causality.

## 15. Authentication Continuity Graph

The whole Chrome profile is canonical authenticated state.

Model operationally:

```text
AuthIdentity
  profile
  cookies
  local/session storage
  IndexedDB
  Cache Storage
  service workers
  browser permissions
  device-bound browser sessions where visible
  WebAuthn/FedCM state where relevant
  active login/OAuth targets
```

Modern browser/device-bound sessions can be tied to the machine and cannot always be faithfully serialized into a portable cookie bundle. Treat selective auth export as best-effort rather than pretending it replaces a Chrome profile.

OAuth popup flows are ordinary target/opener/document transitions in the Browser World Graph.

## 16. Storage

Expose structured access to:

- cookies;
- localStorage;
- sessionStorage;
- IndexedDB;
- Cache Storage;
- origin/storage-key usage;
- service-worker state;
- browser permissions and relevant Chromium storage domains.

The profile remains authoritative for persistence across browser runs.

## 17. Downloads and uploads

### Downloads

Use CDP Browser download lifecycle plus extension download APIs where they provide useful complementary information.

Agent-visible download object:

```text
dl_31
  initiator=t_41
  source_url=...
  suggested_filename=report.xlsx
  actual_path=X:\...\report.xlsx
  mime=...
  received_bytes=...
  total_bytes=...
  state=complete
```

Store artifacts by logical ID/GUID under `X:\AgentBrowser\Artifacts\downloads\...` to avoid duplicate-filename ambiguity.

### Uploads

Prefer:

1. direct file-input assignment (`DOM.setFileInputFiles` or equivalent);
2. Playwright file-input helpers where useful;
3. file-chooser interception;
4. DataTransfer drag/drop;
5. native file-dialog fallback.

Support generated files, multiple files, and directory upload where Chrome/page semantics support it.

## 18. Artifact/data plane

Do not force large binary data through the ordinary JSON agent control plane.

### Control plane

JSON-RPC/WebSocket for commands, events, metadata, references, and compact state.

### Artifact/data plane

Local files/stream handles for:

- downloads;
- screenshots;
- PDF;
- recordings;
- audio;
- large response bodies;
- generated files;
- optional HAR/large state exports.

Return compact handles such as:

```json
{"artifact":"a_938","type":"video/webm","size":84392112}
```

rather than base64 data inside normal agent responses.

## 19. Visual and temporal understanding

### Static capture

- viewport screenshot;
- full-page screenshot;
- element/region crop;
- exact geometry-aligned capture.

### Temporal visual state

Use browser screencast/recording capabilities where available for animation, dynamic charts, video apps, canvas transitions, and streaming dashboards.

Perform local frame differencing/keyframe selection before invoking a multimodal model where possible.

### Vision input should be enriched

Provide a vision worker with:

```text
image/crop
viewport dimensions
known DOM/APC/AX element boxes
semantic regions
OCR boxes
nearby text
current task/query
```

The model should not rediscover what Chrome already knows.

### Visual identity

A visual region can receive `v_*`. If it overlaps/corresponds to a structured element, bind `v_* ↔ e_*` so visual coordinate actions do not form a disconnected identity universe.

## 20. Native Windows boundary

The interactive SessionHost provides structured/native control when browser content APIs end.

Capabilities:

- enumerate/manage windows;
- browser chrome inspection;
- Microsoft UI Automation;
- Windows Graphics Capture;
- clipboard;
- native file/print/auth dialogs;
- external-protocol applications;
- extension/browser UI;
- drag/drop across process boundaries;
- native input as final fallback.

Coordinate systems must be modeled explicitly: CSS pixels, device pixels, window coordinates, screen coordinates, capture pixels, DPI scaling.

## 21. PDFs

Treat PDFs as first-class artifacts rather than pixels whenever possible.

Preferred order:

1. identify/download actual PDF bytes;
2. parse text/metadata outside the viewer;
3. retain Chrome PDF-viewer target for browser interactions/navigation;
4. expose page screenshots;
5. OCR scanned/image pages only;
6. support Chrome HTML→PDF/print-to-PDF.

## 22. Media

Expose:

- audio/video element state;
- source URLs;
- duration/current time;
- buffered ranges;
- playback state;
- captions/subtitles/text tracks;
- associated network resources;
- CDP Media events;
- WebAudio information;
- WebRTC application state through JS/getStats/network/permissions where useful.

Add direct audio capture/perception only when audible content is unavailable through richer structured channels.

## 23. Page-native agent interfaces

### WebMCP

Expose site-published structured tools when the running Chrome supports them:

```text
webmcp.tools
webmcp.call
```

Keep raw browser channels available independently.

### Third-party DevTools/runtime tools

Support emerging browser/application developer-tool interfaces that expose framework/component/backend/runtime state not present in the final DOM.

Returned DOM/runtime objects should map into existing `e_*`/application objects rather than creating isolated reference universes.

### Framework lenses

React/Angular/Vue/Svelte/Next-specific inspection is advanced/experimental and should only be added after the generic DOM/AX/APC/runtime/network model is strong.

## 24. Document lifecycle and recovery

### Kernel dies, Chrome survives

Recovery sequence:

```text
locate profile/runtime descriptor
→ reconnect browser CDP
→ reload runtime protocol schema
→ rediscover/reattach targets
→ reconstruct target/frame/document graph
→ reconnect extension/document instrumentation
→ recover surviving exact logical bindings
→ semantically reconcile the remainder
→ recreate optional Playwright adapter
→ continue
```

### Document enters BFCache/cached lifecycle

Do not automatically declare conceptual document death. Preserve the DocumentInstance and logical objects as dormant when browser lifecycle indicates the underlying document persists.

### Prerender

Represent prerendered documents as warm DocumentInstances. When activation occurs, promote them without unnecessarily rediscovering all state.

### Renderer/process swap

Update renderer incarnation separately from document/concept identity when browser lifecycle supports continuity.

### Chrome dies

Increment `BrowserIncarnation`, relaunch the same profile when appropriate, let Chrome restore profile/session state, rediscover targets/documents, and reconcile whatever conceptual workspace can genuinely be recovered.

Do not pretend exact DOM object identity survived if the renderer/document died.

### Extension/Program Host/SessionHost dies

Restart/reconnect the component. Chrome and kernel canonical state remain independent.

## 25. Concurrency

Default internal operation is concurrent.

Parallelize:

- independent target observations;
- network/event processing;
- semantic queries;
- multiple browser contexts;
- multiple persistent browser-profile processes;
- Program Host operations on independent targets.

Serialize competing input operations on the same target so two physical actions do not race. Use target queues/leases strictly as mechanical concurrency primitives, not an authority model.

## 26. Agent API shape

Representative end-state API:

```text
browser.*
profile.*
context.*
target.*

document.list
document.current
document.lifecycle

observe.surface
observe.delta
observe.around

query.find
query.collection
query.semantic
query.application

inspect.element
inspect.document
inspect.dom
inspect.ax
inspect.apc
inspect.runtime
inspect.application

program.run
program.eval
program.session.*

watch.create
watch.cancel
watch.list
watch.next

action.click
action.doubleClick
action.contextClick
action.hover
action.drag
action.fill
action.type
action.key
action.select
action.check
action.scroll
action.focus
action.blur
action.upload

navigate.go
navigate.back
navigate.forward
navigate.reload

wait.until
events.subscribe

network.search
network.request
network.response
network.body
network.stream
network.graphql
network.websocket
network.intercept
network.correlate
network.replay

auth.state
auth.sessions

storage.*

download.*
artifact.*

js.*

webmcp.*
devtoolsTools.*

screenshot.*
visual.*

native.*

cdp.send
cdp.subscribe
```

The raw escape hatches never disappear.

## 27. Persistence model

Persist only operational state required to resume use:

- profile registry;
- browser runtime descriptors/ports/process association;
- browser/profile configuration;
- durable logical workspace IDs where useful;
- identity mappings required for restart reconciliation;
- artifact metadata;
- limited current operating state.

Use SQLite if/when relational persistence is useful.

Keep large live semantic state, short-lived network buffers, and cursor event rings in memory unless a capability specifically requires disk.

Do not build a permanent action ledger, receipt store, or mandatory event archive.

## 28. Performance principles

World-class performance targets are engineering goals to benchmark, not promises:

```text
reattach already-running Chrome          target <250 ms
CDP event → normalized event             p50 <10 ms added controller latency
warm logical-element resolution          p50 <10 ms, p95 <25 ms
ordinary warm semantic observation       p50 <50 ms
semantic delta reduction                 target <25 ms after batching
controller input dispatch overhead       target <20 ms excluding site response
typical viewport screenshot              target <50–100 ms
ordinary observation payload             roughly 2–16 KB where practical
typical semantic delta                   preferably <4 KB
```

Eliminate model/browser round trips through three operating modes:

```text
Primitive mode — one operation
Program mode   — many local operations in one invocation
Watch mode     — zero model polling until a relevant condition changes
```

## 29. Repository architecture at maturity

The implementation should grow toward:

```text
src/
  AgentBrowser.Kernel/
  AgentBrowser.Cdp/
  AgentBrowser.State/
  AgentBrowser.Actions/
  AgentBrowser.Network/
  AgentBrowser.Storage/
  AgentBrowser.Artifacts/
  AgentBrowser.Vision/
  AgentBrowser.SessionHost/
  AgentBrowser.Cli/
  AgentBrowser.Mcp/

extension/
  agent-bridge/

program-host/
  sdk/
  src/

tests/
  fixtures/
  integration/
  identity/
  reconnect/
  stress/
  workflows/

experiments/
  apc/
  identity/
  lifecycle/
  targets/
  webmcp/
  framework-tools/
  canvas/
  browser-ui/
  chromium/
```

Do not create every project on day one. Build 001 begins with a compact subset and splits only when boundaries become real.

## 30. Major technical risks

1. **Conceptual identity:** false semantic rebinds are the highest-risk novel problem.
2. **Frame/document/renderer lifecycle:** OOPIF, BFCache, prerender, renderer swaps, popup/opener transitions can break naïve identity models.
3. **Semantic reduction:** thousands of mutations must become compact useful deltas without losing important state.
4. **Instrumentation overhead:** provider activation must be interest-driven and bounded.
5. **Long-horizon state leaks/races:** a system that demos for five minutes but degrades after 500 actions is not world-class.
6. **Extension lifecycle:** design reconnection; never rely on an immortal MV3 worker.
7. **Coordinate alignment:** DOM/CSS/device/window/screen/capture coordinate transforms must be exact.
8. **Browser protocol evolution:** runtime capability discovery is mandatory.
9. **Native boundary:** Windows GUI interaction belongs in the interactive session and is mechanically distinct from CDP.
10. **Visual interfaces:** canvas/WebGL may have no sufficiently rich structured representation and require vision/application instrumentation.

## 31. Custom Chromium policy

Do not create a Chromium fork directory until a concrete experiment demonstrates a material blocker.

A fork becomes justified only if all three are true:

1. the missing capability is materially valuable to agents;
2. it cannot be obtained through stock Chrome CDP/APC/extensions/native integration;
3. the benefit justifies ongoing Chromium build/rebase/release maintenance.

Until then, stock Chrome provides a dramatically better capability/maintenance ratio.

## 32. End state

The finished system should feel to an agent like a live programmable world, not a click tool.

The agent should be able to say:

```text
show me what changed
show me the five relevant controls
which response populated this grid?
watch this report until it completes while I work in another tab
run this 80-step browser algorithm locally and return the records
return to the cached document and keep using my previous objects
inspect the application's runtime object rather than the rendered approximation
use the site's structured tool if it is the most exact operation
this interface is canvas-based — switch to visual perception
Chrome has crossed into an OS dialog — continue through native UI Automation
```

The architecture is successful when thousands of browser observations/actions do not require thousands of reasoning-model turns and when the agent can maintain a coherent working model of the web for hours.
