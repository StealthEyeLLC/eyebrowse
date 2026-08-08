# 06 — Frozen Decisions

Status: **Canonical decision register**  
Baseline date: **2026-08-08**

This file records decisions that are currently frozen for implementation. It is intentionally not an action log or audit trail. Update it only when a canonical architectural decision changes.

## D-001 — Build 001 is the four-milestone Browser Kernel Slice

**Decision:** The first implementation build is `02-BUILD-001-SLICE.md`.

It must demonstrate:

1. persistent Chrome independent of the kernel;
2. semantic logical browser objects and deltas;
3. surviving document/object identity recovery after kernel death;
4. multi-step Agent Program Host execution in one invocation.

Secondary features must not displace these gates.

## D-002 — Primary durable browser is installed stock Google Chrome Stable

**Decision:** Use installed Chrome Stable for long-lived real-world authenticated identities.

Current measured target version: `151.0.7922.109`.

**Reason:** maximum ordinary Chrome compatibility, extensions, auth flows, browser chrome, media, native interactions, and direct access to Chrome's current CDP surface.

## D-003 — Dedicated non-default profiles

**Decision:** eyebrowse owns dedicated Chrome user-data directories under `C:\AgentBrowser\Profiles\...`.

**Reason:** persistent identities are core, modern Chrome remote debugging requires a non-default data directory, and the target machine has no existing normal Chrome profile to migrate.

## D-004 — Headful/GPU is the default durable mode

**Decision:** Persistent identities run headful with normal GPU acceleration.

**Reason:** browser chrome, interactive OAuth, media, permission UI, extension UI, native surfaces, and ordinary site behavior matter more than maximizing headless density on the primary laptop.

Headless/Chrome for Testing remains valid for disposable workers later.

## D-005 — Direct dynamic CDP is canonical browser transport

**Decision:** AgentBrowser.Kernel connects directly to browser-level CDP and discovers the running protocol/capabilities at attach.

**Reason:** maximum current Chromium surface and no frozen third-party wrapper ceiling.

Permanent escape hatches:

```text
cdp.send
cdp.subscribe
```

## D-006 — Kernel implementation language is C#/.NET 10

**Decision:** Canonical browser/control kernel is .NET 10.

**Target machine fact:** .NET SDK 10.0.302 is already installed.

**Reason:** excellent Windows process/native integration, async networking, WebSockets, IPC, SQLite ecosystem, and direct path to SessionHost/UIA/WinRT/Win32 without requiring a second canonical runtime.

## D-007 — Node 24 LTS is the Program Host/TypeScript build runtime

**Decision:** Install/use Node 24 LTS for the MV3 TypeScript extension and Agent Program Host.

**Reason:** natural JS/TS programming surface for browser-local multi-action programs and extension development.

Node does not own canonical browser state.

## D-008 — Browser World Graph is canonical agent state model

**Decision:** Model browsers/contexts/targets/frame slots/document instances/execution realms/workers plus semantic/application/network/native objects in one graph.

**Reason:** page objects or DOM snapshots alone cannot express long-lived modern browser lifecycle.

## D-009 — Frame and document identity are separate concepts

**Decision:** `FrameSlot != DocumentInstance`.

**Reason:** same-document navigation, new document replacement, prerender, BFCache/cached documents, and renderer swaps require document identity independent from structural frame location.

Build 001 implements the extensible minimum; lifecycle hardening follows immediately afterward.

## D-010 — Agent element IDs represent concepts, not raw DOM nodes

**Decision:** expose logical `e_*` references with binding incarnations.

**Backing anchors may include:** NodeSerial, BackendNodeId, AXNodeId, DOM NodeId, APC identity, Runtime object, application key, and semantic fingerprint.

**Identity outcomes:** `exact`, `rebound`, `stale`, `ambiguous`.

False rebinds are worse than explicit ambiguity.

## D-011 — MV3 extension is core and enters early

**Decision:** Build the `agent-bridge` extension during Build 001, not as a late optional feature.

**Reason:** document-start NodeSerial identity and surviving document-side logical bindings are central to Milestone C recovery continuity.

The service worker is not canonical state.

## D-012 — Observation is multi-provider and delta-first

**Decision:** derive compact semantic state from DOM/DOMSnapshot/Accessibility/APC/Runtime/document instrumentation and later application/network/visual/native providers.

After initialization, ordinary operation consumes cursor deltas rather than complete page rediscovery.

## D-013 — APC is an experimental high-value provider

**Decision:** probe/decode APC immediately when the live Chrome capability exists, but never make it the only representation.

**Reason:** unusually dense browser-native semantic/actionable data, balanced against experimental evolution and coverage/lifecycle limitations.

## D-014 — Representation Broker chooses the best data modality

**Decision:** do not impose a universal DOM/AX/vision hierarchy.

A question may be best answered through application/network state, Runtime, DOM, AX, APC, WebMCP, third-party tools, vision, or native UIA.

## D-015 — Playwright is optional/replaceable action machinery

**Decision:** Playwright may be added as an actuator for mature locator/actionability behavior.

It does not own Chrome lifecycle, canonical page state, identity, network model, or capability ceiling.

## D-016 — Puppeteer has no initial production role

**Decision:** do not add Puppeteer merely because it is CDP-oriented.

**Reason:** direct CDP already provides the unique low-level capability; Playwright provides more differentiated interaction semantics if a helper framework is useful.

Revisit only after a concrete capability/engineering advantage is demonstrated.

## D-017 — Selenium/WebDriver Classic is not the core

**Decision:** no Selenium/WebDriver Classic foundation.

BiDi may be added later as an interoperability adapter.

## D-018 — Program execution is a first-class action modality

**Decision:** build the Agent Program Host as part of Build 001 Milestone D.

**Reason:** loops/branches/local composition can remove large numbers of reasoning-model/tool round trips and are a better action space for many long workflows.

The Program Host is disposable and talks to the kernel API.

## D-019 — Waits are event-driven; watches come after Build 001

**Decision:** Build 001 includes compound event-driven waits. Persistent Attention/Watch Engine follows immediately post-slice.

Ordinary workflows must not rely on fixed sleeps.

## D-020 — Network is application data, not only diagnostics

**Decision:** expose requests/responses/bodies/WebSockets/SSE/GraphQL and later durable response buffering/producer correlation.

Bodies are lazy/bounded by default.

Fetch interception is enabled only when needed.

## D-021 — Complete Chrome profile is canonical auth state

**Decision:** cookies/localStorage exports are useful views, not the definition of a session.

**Reason:** modern auth includes IndexedDB, service workers, browser permissions, extension state, OAuth state, and increasingly device-bound session material.

## D-022 — Artifacts are a separate data plane

**Decision:** large screenshots/PDFs/downloads/recordings/response bodies travel via files/stream handles rather than huge base64 JSON messages.

Use `X:\AgentBrowser\Artifacts` for bulk data.

## D-023 — SessionHost will be an interactive .NET process

**Decision:** browser/native boundary runs in the actual `StealthEye` desktop session.

**Reason:** UI Automation, browser chrome, Windows capture, native dialogs, clipboard, and input are interactive-session capabilities and should not be forced through the SYSTEM Eye supervisor.

## D-024 — Vision is advanced first-class fallback, not default perception

**Decision:** structured state is primary when richer; visual grounding handles pixel-native surfaces.

Future visual inputs should be enriched with browser geometry/semantics to avoid rediscovering known information.

## D-025 — SQLite only for durable operating state

**Decision:** introduce SQLite when useful for profiles/runtime descriptors/identity recovery/artifact metadata.

Do not create a permanent action/event/receipt database.

## D-026 — No default external proxy

**Decision:** CDP Network/Fetch is the normal browser network path.

Add proxy/packet tooling only if a demonstrated protocol gap provides material value.

## D-027 — No Chromium fork until a blocker exists

**Decision:** stock Chrome + CDP + extension + native + vision is the practical ceiling to exhaust first.

A fork requires a concrete capability gain sufficient to justify ongoing Chromium maintenance.

## D-028 — Constraints are architectural and remain exhaustive

The project does not add extra safety/guardrails/theater/verification/receipts beyond the five owner-specified constraints in `00-CHARTER.md`.

Normal browser mechanics, tests, errors, stale detection, event waits, actionability, and version control are not separate prohibited subsystems.

## D-029 — Canonical documents stay singular

**Decision:** implementation evidence that changes architecture updates the numbered canonical docs rather than spawning competing specifications.

Experiments remain experimental until explicitly promoted.

## D-030 — Target-machine-first optimization

**Decision:** optimize first for STEALTHEYELLC rather than an imaginary portable lowest common denominator.

Portability/interoperability is added when it materially improves the product, not at the cost of the machine's available Windows/Chrome/GPU capabilities.
