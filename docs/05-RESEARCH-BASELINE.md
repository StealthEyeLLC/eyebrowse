# 05 — Research Baseline

Status: **Canonical research rationale**  
Research refresh: **2026-08-08**

This document records current external findings that materially influence eyebrowse architecture. It is not intended to become a permanent news archive. Re-research unstable/experimental features when implementation reaches them.

## 1. Chrome DevTools Protocol

Primary reference:

- https://chromedevtools.github.io/devtools-protocol/
- https://chromedevtools.github.io/devtools-protocol/tot/

Current tip-of-tree CDP exposes an unusually broad Chromium control surface, including major domains used by eyebrowse:

```text
Accessibility
Autofill
Browser
CacheStorage
DOM
DOMSnapshot
DOMStorage
Extensions
FedCm
Fetch
IndexedDB
Input
IO
Media
Network
Page
PerformanceTimeline
Preload
Runtime
ServiceWorker
Storage
SystemInfo
Target
Tracing
WebAudio
WebAuthn
WebMCP
```

CDP's current protocol documentation explicitly distinguishes tip-of-tree from stable versions and does not promise backward compatibility for the newest protocol surface.

Architecture consequence:

> runtime protocol/capability discovery is core; frozen third-party wrappers cannot define eyebrowse's capability ceiling.

## 2. Chrome remote debugging/profile rule

Primary reference:

- https://developer.chrome.com/blog/remote-debugging-port

Chrome changed remote debugging in Chrome 136: remote-debugging switches are no longer honored against the ordinary default Chrome data directory and must be paired with a non-standard `--user-data-dir`.

Architecture consequence:

> dedicated persistent eyebrowse profiles are not merely isolation preference; they align with modern Chrome's supported remote-debugging model.

## 3. Annotated Page Content (APC)

Primary references:

- https://chromium.googlesource.com/chromium/src/+/refs/heads/main/third_party/blink/renderer/modules/content_extraction/readme.md
- https://chromium.googlesource.com/chromium/src/+/HEAD/components/optimization_guide/proto/features/common_quality_data.proto
- current CDP Page domain

Chromium describes APC as a structured/actionable representation of page content and layout intended to preserve meaningful page hierarchy while optimizing downstream understanding and efficiency.

The current protobuf contains rich agent-oriented information including semantic content types, DOM associations, geometry/coordinates, interaction/actionability information, labels/roles, form/control state, selection/options, frame/page metadata, and related semantic fields.

Current Chrome/CDP support remains experimental and evolving.

Architecture consequence:

> APC is a privileged semantic provider/accelerator, not canonical browser truth. Always retain independent DOM/DOMSnapshot/AX/Runtime paths.

## 4. Playwright

Primary references:

- https://playwright.dev/docs/actionability
- https://playwright.dev/docs/api/class-browsertype
- https://playwright.dev/dotnet/docs/next/api/class-browsertype

Playwright's actionability engine performs mature pre-action checks such as uniqueness, visibility, stability, event reception/hit-target behavior, and enabled state depending on the action.

Playwright also explicitly documents `connectOverCDP`/`ConnectOverCDPAsync` as significantly lower fidelity than Playwright's own protocol connection.

Architecture consequence:

> reuse Playwright as a replaceable action provider where it measurably improves interactions, but never let Playwright own canonical browser lifecycle/state/identity.

## 5. WebDriver BiDi

Primary reference:

- https://www.w3.org/TR/webdriver-bidi/

The current W3C publication is a Working Draft dated 1 June 2026 and defines a bidirectional protocol for remote browser control.

Architecture consequence:

> BiDi is a serious future interoperability/cross-browser adapter. It is not the maximum-capability control plane for a Chrome-specific environment because CDP exposes Chromium-specific internals beyond the standardized surface.

## 6. WebMCP

Primary references:

- https://developer.chrome.com/blog/ai-webmcp-origin-trial
- https://developer.chrome.com/blog/new-in-devtools-149
- https://developer.chrome.com/docs/devtools/application/webmcp

Chrome 149 introduced/expanded experimental WebMCP support and an origin trial. WebMCP lets websites expose structured operations/tools directly to agents instead of forcing every task through inferred mouse-like UI interaction.

Current Chrome DevTools for agents can inspect/list/execute WebMCP tools experimentally, and current CDP includes a WebMCP domain.

Architecture consequence:

> WebMCP becomes a capability-detected provider/action channel, not a mandatory abstraction and not a replacement for DOM/network/Runtime.

## 7. Third-party developer tools for agents

Primary reference:

- https://developer.chrome.com/blog/devtools-for-agents-3p-tools

Chrome announced third-party developer tools for Chrome DevTools for agents in June 2026. Pages/frameworks can expose runtime-only state such as component hierarchies, JavaScript signals, dependency graphs, and backend/application information through a discovery API.

Chrome's own tooling can map returned DOM elements back into the same UID system used by its page snapshots.

Architecture consequence:

> application/framework-native runtime information deserves a distinct provider in the Representation Broker, and returned DOM/runtime objects should map into existing eyebrowse object identities rather than creating disconnected reference systems.

## 8. Programmatic multi-action browser execution

Primary reference:

- https://www.microsoft.com/en-us/research/articles/webwright-a-terminal-is-all-you-need-for-web-agents/

Microsoft Research's 2026 Webwright work gives web agents a coding environment in which they can compose multiple browser operations inside one model step using normal program structure such as functions and loops. Its published results show a large improvement over its base configuration on long-horizon web benchmarks.

Webwright also discusses mechanisms outside eyebrowse's constraints, including a final reflection/verification component. eyebrowse deliberately does **not** adopt a separate verification stage.

Architecture consequence:

> code is a first-class agent action modality. The Agent Program Host should allow many eyebrowse operations to execute locally in one reasoning turn.

## 9. Node runtime choice for Program Host/extension development

Primary reference:

- https://nodejs.org/en/about/previous-releases

Node 24 (`Krypton`) is an LTS line in the 2026 release table.

Architecture consequence:

> use Node 24 LTS for TypeScript extension tooling and Agent Program Host; do not make Node the canonical browser kernel when .NET 10 already spans the target Windows/browser/native architecture well.

## 10. Device Bound Session Credentials (DBSC)

Primary references:

- https://developer.chrome.com/blog/dbsc-windows-announcement
- https://developer.chrome.com/docs/web-platform/device-bound-session-credentials

Chrome 145 made DBSC available on Windows. DBSC can bind authentication sessions to the device using a private key protected by hardware such as the TPM.

Architecture consequence:

> the real Chrome profile on the real machine is increasingly important. A portable export of cookies/localStorage cannot be assumed to represent all modern authenticated browser state.

Selective auth export remains best-effort; complete persistent Chrome profile state is canonical.

## 11. Durable CDP network state

Primary reference:

- https://chromedevtools.github.io/devtools-protocol/tot/Network/

Current CDP Network includes dedicated durable-message configuration and advanced response-body/search/streaming features. Durable response bodies can be retained outside renderer-local state within configured limits, improving continuity across cross-process navigation.

Architecture consequence:

> active/hot workflows can maintain bounded durable network/application data instead of treating network bodies as ephemeral diagnostics.

## 12. Public browser-agent convergence

### Browser Use

Primary project references:

- https://github.com/browser-use/browser-use
- https://github.com/browser-use/browser-use/releases

Browser Use's 2026 CLI direction includes persistent daemon/session operation and direct CDP rather than requiring Playwright as the command transport.

Architectural signal:

> independent convergence toward persistent direct-CDP browser control validates the basic process model, while eyebrowse aims substantially deeper on lifecycle identity, semantic fusion, document-side identities, application/network graphing, native Windows integration, and programmable/watched operation.

### Vercel `agent-browser`

Primary project reference:

- https://github.com/vercel-labs/agent-browser

The project exposes compact accessibility-tree snapshots with element refs, annotated screenshots, CDP attachment, and a persistent/native daemon implementation path.

Architectural signal:

> compact refs + persistent direct browser control are becoming standard strong-agent primitives. eyebrowse must distinguish itself through persistent conceptual identity, document lifecycle/recovery, multi-provider world state, semantic deltas, Program Host, and Attention Engine rather than merely providing another ref-based snapshot CLI.

### Stagehand

Primary project reference:

- https://github.com/browserbase/stagehand

Stagehand positions itself as an agent-oriented browser SDK and has moved toward a lower-level CDP engine architecture while preserving higher-level `act`/`extract`/`observe` concepts.

Architectural signal:

> high-level agent ergonomics and low-level direct browser control can coexist. eyebrowse should keep raw CDP permanent while presenting an AI-oriented semantic API.

## 13. Browser-agent benchmark ecosystem

Primary reference:

- https://github.com/ServiceNow/BrowserGym

BrowserGym integrates a broad family of web-agent environments including MiniWoB, WebArena, WebArenaVerified, VisualWebArena, WorkArena, AssistantBench, OpenApps, and others.

Architecture consequence:

> integrate BrowserGym-family benchmarks later as pressure tests rather than inventing an entire benchmark ecosystem. Build 001 remains focused on deterministic local identity/reconnect/delta fixtures and at least one real modern site.

## 14. Windows capture/OCR implementation detail

Primary references:

- https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr
- https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscaptureitem.trycreatefromwindowid

Current Microsoft documentation states that Windows.Media.Ocr desktop use requires package identity, and programmatic GraphicsCaptureItem creation requires requesting programmatic capture access plus the appropriate package capability.

Architecture consequence:

> the future interactive SessionHost should likely be packaged appropriately (for example with MSIX/package identity) rather than assuming every WinRT capture/OCR API is frictionless from an arbitrary unpackaged service executable.

## 15. Research conclusions frozen into architecture

The current external evidence supports these decisions:

1. direct dynamic CDP is the Chrome control plane;
2. stock headful Chrome with dedicated profiles is the canonical durable browser;
3. persistent daemon/kernel is preferable to per-command browser/controller reconstruction;
4. APC is worth prototyping immediately but remains experimental/provider-scoped;
5. Playwright is useful as an actuator rather than canonical state owner;
6. BiDi is future interoperability rather than the Chrome ceiling;
7. WebMCP and third-party runtime tools are emerging first-class modalities;
8. browser-side/programmatic multi-action execution can materially reduce reasoning/tool-turn inefficiency;
9. the full Chrome profile matters more as authentication becomes device-bound;
10. Windows-native capture/UIA/OCR are practical on the target machine but belong in an interactive packaged native boundary;
11. semantic/delta identity continuity is still the project-defining novel engineering problem.

## 16. Re-research triggers

Before implementing each experimental/fast-moving area, refresh its primary sources:

- APC schema/CDP command;
- WebMCP;
- third-party DevTools tools;
- CDP experimental domains;
- DBSC/FedCM/WebAuthn browser interfaces;
- Playwright CDP attachment behavior;
- Windows Graphics Capture/OCR packaging requirements;
- Chrome remote-debugging changes;
- Chrome for Testing behavior;
- WebDriver BiDi.

Do not let a dated research document override the actual running Chrome protocol or current primary documentation.
