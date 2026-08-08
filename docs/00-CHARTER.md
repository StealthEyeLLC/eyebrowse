# 00 — Project Charter

Status: **Canonical**  
Project: **eyebrowse**  
Owner: **StealthEyeLLC**  
Target platform: **STEALTHEYELLC**  
Canonical date baseline: **2026-08-08**

## Mission

eyebrowse exists to build the strongest practical browser operating environment for an AI agent that the target machine can support.

The target is not merely good browser automation and not a robot that imitates a human with screenshots and mouse coordinates. The target is an **AI-native browser operating environment** that exposes persistent browser state, structured page semantics, precise browser objects, application/network state, events, files, JavaScript, storage, visual information, and native Windows surfaces through one coherent agent-oriented model.

The system should make statements like these true:

- The agent can deeply understand almost any page.
- The agent can interact precisely without depending on screenshots.
- The agent can use screenshots/vision whenever structured access is insufficient.
- The agent can maintain long-lived authenticated browser state.
- The agent can observe browser events instead of repeatedly polling.
- The agent can operate many tabs, frames, workers, and service workers coherently.
- The agent can refer to previously observed objects without constantly rediscovering them.
- The agent can inspect JavaScript, network, storage, and hidden application state when the rendered interface is insufficient.
- The agent can upload, download, capture, and manipulate browser-associated artifacts.
- The agent can recover from controller interruption and browser/renderer failures.
- The agent can always reach raw browser capabilities when higher-level abstractions are insufficient.
- The browser interface exposes richer information than a human receives through the rendered page.
- No automation framework imposes an artificial capability ceiling.

## Exhaustive architectural constraints

These five constraints are exhaustive. Future contributors must not silently add architectural constraints that contradict them.

### 1. No extra safety

Do not add project-specific safety mechanisms, permission systems, approval layers, confirmation gates, artificial authority restrictions, capability reductions, or protective abstractions merely because the system is powerful.

### 2. No extra guardrails

Do not add policy engines, allowlists, denylists, action classifications, artificial boundaries, capability gates, restricted modes, privilege tiers, or similar internal guardrail systems.

### 3. No theater

Do not add ceremony whose primary purpose is making the system appear controlled, robust, enterprise-ready, auditable, cautious, or sophisticated. Every component must earn its existence through actual capability, reliability, performance, or usability.

### 4. No verification

Do not create a separate verification architecture, verification agents, verification stages, verification pipelines, mandatory double-check systems, proof-generation mechanisms, confirmation loops, or similar machinery.

Normal browser state observation, event-driven completion conditions, error reporting, hit testing, stale-object detection, and lifecycle correctness are not a separate verification subsystem; they are intrinsic browser-operation capabilities.

### 5. No receipts

Do not build receipt systems, action ledgers, evidence trails, provenance systems, audit trails, mandatory execution records, proof-of-action artifacts, or similar bookkeeping unless the underlying browser capability itself intrinsically requires that data to function.

A bounded in-memory event/delta ring, current target mappings, current download state, current network state, and current artifact metadata are allowed because they are operational state required to use the browser. They must not be expanded into a permanent execution-history product unless a browser capability later proves it genuinely necessary.

## Explicit non-assumptions

The project must evaluate technologies on merit. It must not assume that any of the following are undesirable simply because they are powerful or complex:

- daemons and persistent processes;
- Windows services or interactive session processes;
- browser extensions;
- modified browsers or Chromium forks;
- Chrome, Chromium, Edge, Chrome for Testing;
- Playwright, Selenium, Puppeteer, WebDriver BiDi;
- Chrome DevTools Protocol;
- native code;
- Node.js, .NET, Rust, Python, Java, C/C++;
- local databases;
- proxies or packet inspection;
- accessibility APIs;
- OCR and multimodal vision;
- browser instrumentation and injected scripts;
- DevTools internals;
- GPU use;
- multiple cooperating processes;
- persistent browser sessions and authenticated profiles.

Likewise, none of those technologies should be added merely because they exist. Each component must buy meaningful capability, reliability, performance, interoperability, or agent usability.

## First-principles design rules

### Chrome truth versus agent truth

Chrome owns browser truth: processes, targets, frames, documents, DOM, accessibility, execution contexts, network, storage, rendering, downloads, and browser lifecycle.

eyebrowse owns agent truth: stable logical references, compact semantic representations, deltas, queries, conceptual identity, cross-provider fusion, program execution, and the working model presented to the agent.

No convenience framework owns either.

### Structured state first, vision when pixels are the source of truth

DOM, accessibility, browser-native semantic data, JavaScript state, network responses, storage, and application-native tools can expose information that a human cannot see directly. Use those channels when they are richer and more exact.

Vision remains first-class for canvas, charts, maps, image-heavy applications, browser chrome, native UI, and any surface whose meaningful state is genuinely visual.

### Persistent by default where persistence buys capability

Long-lived Chrome processes, profiles, the kernel, document instrumentation, target identity, and event subscriptions are desirable because continuity is a core capability.

Do not restart components between agent calls unless doing so has a concrete benefit.

### Event driven rather than sleep driven

The normal operating loop is:

```text
observe compact state
→ reason
→ act on stable references
→ wait/subscribe to real conditions
→ receive a semantic delta
→ continue
```

Fixed sleeps are only appropriate when actual wall-clock delay is itself required.

### Raw access is permanent

High-level agent-oriented APIs coexist with raw capabilities. The architecture must preserve permanent escape hatches for:

- raw CDP methods/events;
- arbitrary browser-side JavaScript;
- direct DOM queries;
- direct accessibility queries;
- direct network inspection/interception;
- browser command-line switches;
- extension communication;
- native browser process/window control.

A higher-level abstraction must never become a veto layer over Chrome capability.

## Canonical build policy

**Build 001 — Browser Kernel Slice is the first implementation build.**

No later feature should be allowed to distract from proving these four milestones first:

1. Persistent Chrome independent of the controller.
2. Compact semantic state with persistent logical page objects.
3. Controller-death recovery with surviving document/object continuity.
4. Multi-step Agent Program Host execution using the live browser kernel.

The full acceptance criteria live in `02-BUILD-001-SLICE.md`.

## Canonical-document discipline

The numbered files in `docs/` form one specification. If implementation evidence invalidates a decision:

1. update the relevant canonical document;
2. record the replacement decision in `06-DECISIONS.md`;
3. avoid creating competing architecture documents with ambiguous authority.

Experiments may live under `experiments/`, but an experiment does not become canonical merely because it succeeds. Promote its result into the canonical documents explicitly.
