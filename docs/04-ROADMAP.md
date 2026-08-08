# 04 — Development Roadmap

Status: **Canonical sequencing**

## Rule zero

**Build 001 — Browser Kernel Slice is the first build.**

Do not begin broad secondary feature work until the four Build 001 milestones are demonstrated:

```text
A — persistent Chrome independent of kernel
B — semantic logical objects + delta operation
C — recovery of surviving object identity after kernel death
D — one Agent Program Host invocation performing a 20+ operation workflow
```

See `02-BUILD-001-SLICE.md` for acceptance criteria.

## Build 001 — Browser Kernel Slice

Deliver the permanent spine:

- dedicated persistent Chrome profile;
- direct browser-level dynamic CDP;
- Capability Registry;
- target/frame/document graph;
- first-party actions;
- DOM/DOMSnapshot/AX/APC semantic surface;
- logical `e_*` objects;
- MV3 document-side NodeSerial identity;
- compact `observe.surface`;
- `observe.delta` cursor stream;
- semantic rebinding baseline;
- event-driven waits;
- JavaScript Runtime access;
- network request/response/body/WebSocket baseline;
- downloads/artifacts baseline;
- controller-death reattach/recovery;
- Node 24 TypeScript Agent Program Host.

Build 001 is intentionally narrow enough to build immediately but architecturally permanent.

---

# After Build 001

## Phase 2 — Lifecycle hardening

Goal: make the world model correct across the full modern document lifecycle.

Add/harden:

- BFCache/document cached lifecycle;
- prerender warm documents and activation;
- renderer/process swaps;
- document lifecycle state machine;
- same-document SPA routing;
- frame replacement/OOPIF transitions;
- extension/document instrumentation reconnection through lifecycle changes;
- long-running target identity reconciliation.

Pressure tests:

- repeated back/forward navigation;
- prerender-enabled test cases;
- cross-origin frame swaps;
- popup/opener changes;
- renderer crashes.

Primary success metric: logical document/object identity remains correct without false continuity.

## Phase 3 — Interaction hardening

Add the replaceable Playwright action provider where it measurably improves ordinary controls.

Harden:

- rich text/contenteditable;
- selection APIs;
- drag/drop;
- sliders/date/time controls;
- nested scroll containers;
- virtualized lists;
- file chooser;
- multiple/directory file inputs;
- browser hit testing;
- action routing among Playwright/CDP/DOM/JS.

Do not let Playwright take browser ownership.

## Phase 4 — Durable application-data plane

Upgrade the Network/runtime side into a richer application-data graph.

Add:

- durable response-body buffering on hot targets where supported;
- body streaming;
- GraphQL semantic index;
- WebSocket/SSE search;
- request/response initiator association;
- JavaScript call-stack/network association where useful;
- application entity objects;
- network ↔ semantic-object producer correlation;
- service-worker-aware request relationships;
- direct structured data extraction for virtualized interfaces.

Pressure test: answer questions about thousands of offscreen records without scrolling through every rendered row.

## Phase 5 — Attention Engine

Extend blocking waits into persistent watches:

```text
watch.create
watch.cancel
watch.list
watch.next
```

Watch:

- semantic object state;
- target/tab lifecycle;
- network operations;
- downloads;
- application JS predicates;
- long-running jobs;
- meaningful region changes.

Goal: allow an agent to begin a long operation, work elsewhere, and receive an event only when the relevant condition changes.

## Phase 6 — Authentication, storage, and files

Deepen:

- complete profile identity handling;
- multiple durable identities;
- cookies;
- local/session storage;
- IndexedDB;
- Cache Storage;
- service-worker state;
- browser permissions;
- device-bound session awareness where exposed;
- WebAuthn/FedCM operational state where useful;
- OAuth popup workflows;
- download association;
- authenticated/blob downloads;
- direct resource extraction;
- generated uploads;
- artifact metadata/data plane.

The complete Chrome profile remains canonical auth state.

## Phase 7 — Interactive Windows SessionHost

Build the packaged .NET interactive-session component.

Add:

- HWND/window enumeration;
- Microsoft UI Automation;
- browser chrome inspection;
- Windows Graphics Capture;
- clipboard;
- file dialogs;
- print dialogs;
- permission/auth surfaces not available programmatically;
- external applications/protocol handlers;
- cross-process drag/drop;
- native input final fallback;
- coordinate/DPI transformation library.

The existing SYSTEM-level Eye supervisor may manage lifecycle, but GUI work runs in the `StealthEye` interactive desktop session.

## Phase 8 — Visual and temporal perception

Add:

- exact region/element screenshot capture;
- full window/native capture;
- visual region identity `v_*`;
- binding `v_* ↔ e_*` where possible;
- OCR fallback;
- pluggable multimodal provider;
- canvas/chart/diagram grounding;
- screencast/temporal capture;
- GPU frame differencing/keyframe extraction;
- visually significant change detection.

Do not OCR or invoke a large vision model when DOM/APC/AX/network already provide the answer.

## Phase 9 — Page-native agent interfaces

Add capability-detected providers for:

- WebMCP;
- third-party DevTools/runtime tools;
- page-provided component/backend state;
- framework adapters where they materially improve real workflows;
- optional helper script worlds.

Map returned DOM/runtime objects back into existing `e_*`/application graph objects.

## Phase 10 — PDF/media depth

PDF:

- source-byte retrieval;
- parser integration;
- page/text/metadata API;
- Chrome PDF-viewer integration;
- scanned-page OCR;
- print-to-PDF.

Media:

- audio/video element state;
- text tracks/captions;
- manifests/network resources;
- CDP Media/WebAudio;
- WebRTC application state/getStats/permissions;
- direct audio capture only when structured alternatives fail.

## Phase 11 — Long-horizon hardening

Pressure-test on real applications:

- Gmail;
- Google Drive;
- GitHub;
- rich editors;
- OAuth;
- virtualized enterprise grids;
- uploads/downloads;
- multiple profiles;
- multi-tab research;
- canvas/WebGL interfaces;
- deliberate kernel/renderer/Chrome crashes;
- hundreds/thousands of sequential actions.

Integrate relevant benchmark families such as BrowserGym/WebArena/VisualWebArena/WorkArena and long-horizon/sustained-attention style tests where practical.

The goal is not benchmark theater. Use benchmarks to expose capability/reliability gaps.

## Phase 12 — Scale and interoperability

Add as useful:

- Chrome for Testing disposable worker pool;
- multiple simultaneous durable Chrome profiles;
- multi-agent target routing;
- hot/warm/cold state scheduling at scale;
- Edge support;
- WebDriver BiDi adapter;
- future Firefox support;
- MCP adapter and other client ecosystems.

## Phase 13 — Chromium ceiling experiment

Only after the stock-browser system is mature ask:

> What materially valuable agent capability remains impossible?

Candidate fork triggers might eventually include:

- missing renderer/compositor observability that cannot be exposed through CDP/extensions;
- identity/lifecycle primitives impossible to reconstruct externally;
- PDF/browser-UI internals whose absence blocks major workflows;
- an event/state transport whose in-browser implementation would drastically improve performance/correctness.

If there is no concrete high-value blocker, do not fork Chromium.

## Deliberately deferred unless evidence changes

- Chromium fork maintenance;
- CEF/WebView2 as primary browser;
- Selenium/WebDriver Classic core;
- Puppeteer production layer when direct CDP + optional Playwright already cover its distinct value;
- default MITM proxy;
- packet capture as a normal path;
- deterministic replay of the entire modern browser/web;
- always-on screenshots;
- always-on OCR;
- mandatory giant local vision model;
- framework-private reverse engineering before generic providers are strong;
- Python/Rust/C++ rewrites before profiling demonstrates a need;
- permanent action/event history;
- verification agents/pipelines;
- receipt/evidence systems;
- project-specific authority/policy engines.

## Roadmap principle

Each phase must leave eyebrowse more capable in real workflows. Do not add components merely to make the architecture look complete.
