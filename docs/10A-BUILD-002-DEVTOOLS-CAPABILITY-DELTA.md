# 10A — Build 002 Chrome DevTools / Agentic Browser Capability Delta

Status: **CANDIDATE AMENDMENT / PROSPECTIVE — NOT CANONICAL**
Amendment date: **2026-08-12**
Parent candidate: `docs/10-BUILD-002-SKILL-PLANE.md`
Research target snapshot: `ChromeDevTools/chrome-devtools-mcp` `main` at `da8fc21ab430512ff4db1e36bfb0e7701d51d500`; package version `1.7.0`.
Acceptance status at amendment: **NOT STARTED**.

## 1. Purpose

This document reconciles the owner-authorized DVA-1..DVA-67 Chrome DevTools / agentic-browser amendment into the existing Build 002 candidate before measured acceptance. It is additive. It does not restart Build 002, invalidate valid implementation, move `main`, alter Campaign 3, or replace direct dynamic CDP / Browser World Graph / persistent `t_*` / `d_*` / `e_*` identity / Program Host / Skill Plane.

The amendment is governed by the same authority equation:

> Chrome owns browser material truth. eyeBROWSE owns persistent browser correspondence and generic browser capability. Skills own procedural expertise. Program Host owns transient local computation. ChatGPT owns cognition. Sibling Eyes own their native worlds.

Chrome DevTools for agents is aggressive external capability pressure and implementation research, never an authority layer in front of Chrome.

## 2. Current external reference system

Current official Chrome DevTools for agents consists of:

- Chrome DevTools MCP server;
- Chrome DevTools CLI;
- source-controlled Agentic Skills;
- live browser attachment, including existing authenticated sessions;
- browser automation and page snapshots;
- console/network/runtime inspection;
- performance traces and insight reduction;
- Lighthouse audits;
- memory/heap-snapshot analysis;
- accessibility workflows;
- emulation;
- screenshots and experimental screencast;
- Chrome extension management/debugging;
- experimental WebMCP;
- experimental third-party developer tools;
- experimental DevTools-target automation;
- PWA management.

Current MCP 1.7.0 exposes 56 documented tools in these families: Input 10, Navigation 6, Emulation 2, Performance 3, Network 2, Debugging 8, Memory 12, Extensions 5, Third-party 2, WebMCP 2, PWA 4.

Google's current skills directory contains at least:

- `chrome-devtools`;
- `chrome-devtools-cli`;
- `a11y-debugging`;
- `debug-optimize-lcp`;
- `memory-leak-debugging`;
- `troubleshooting`.

The useful pattern is procedural specialization plus compact tool selection; the ownership model and snapshot UID identity are not adopted.

## 3. Capability-delta classification

Classification values:

- **STRONGER** — eyeBROWSE already has a stronger architectural primitive.
- **EQUIVALENT/PRESENT** — already implemented or naturally covered at equivalent generic capability.
- **ADD CORE** — high-value generic Build 002 capability to implement natively.
- **ADD DEBUG** — on-demand developer/debug capability to implement.
- **SKILL** — primarily procedural knowledge, not kernel authority.
- **PROVIDER** — capability-detected provider/runtime facet.
- **RAW CDP** — retain via generic raw CDP unless a real workflow earns typed promotion.
- **DEFER** — valid but not worth Build 002 core complexity under current gates.
- **REJECT** — incompatible/redundant with Eye architecture.

### Input automation

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `click` | STRONGER | persistent `e_*` semantic action + browser geometry/hit testing |
| `drag` | ADD CORE | `action.drag/drop` against persistent concepts; no snapshot UID authority |
| `fill` | STRONGER | `action.fill/select/check` on `e_*` |
| `fill_form` | EQUIVALENT/PRESENT | `common.batch-form-fill` Program Host routine |
| `handle_dialog` | ADD CORE | generic current JS dialog state + accept/dismiss; no approval layer |
| `hover` | EQUIVALENT/PRESENT | `action.hover` |
| `press_key` | EQUIVALENT/PRESENT | `action.key` |
| `type_text` | EQUIVALENT/PRESENT | `action.type` |
| `upload_file` | EQUIVALENT/PRESENT | `file.upload`, direct file-input assignment |
| `click_at` | RAW CDP / VISUAL FALLBACK | only for pixel-native surfaces; structured `e_*` remains default |

### Navigation and page selection

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `close_page` | EQUIVALENT/PRESENT | `target.close(t_*)` |
| `list_pages` | STRONGER | cheap `target.list`, persistent `t_*`, hot/warm/cold state |
| `navigate_page` | ADD CORE | typed go/back/forward/reload over target/document lifecycle |
| `new_page` | EQUIVALENT/PRESENT + DEFER FACET | `target.open`; named isolated browser contexts deferred unless acceptance earns them |
| `select_page` | STRONGER | `target.activate(t_*)` plus neutral `context.current` |
| `wait_for` | STRONGER | event-aware `wait.until/any/all/sequence/quiet_for` |

### Emulation

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `emulate` | ADD DEBUG | typed viewport, CPU, network, geolocation, locale/timezone/media/color/reduced-motion/user-agent/headers where supported; explicit reset |
| `resize_page` | ADD DEBUG | viewport/window sizing provider; preserve browser ownership |

### Performance

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `performance_start_trace` | ADD DEBUG | `performance.trace.start`, artifact-backed raw trace |
| `performance_stop_trace` | ADD DEBUG | `performance.trace.stop`, bounded lifecycle |
| `performance_analyze_insight` | SKILL + ADD DEBUG | local trace reduction/insights in Program Host, compact findings |

Additional eyeBROWSE-native capability beyond current MCP tool surface:

- `performance.metrics` — already present;
- `performance.timeline` / bounded timeline subscription — add using `PerformanceTimeline` when supported;
- local trace parsing/reduction — mandatory; raw traces remain artifacts;
- no continuous trace collection.

### Network

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `list_network_requests` | STRONGER/PARTIAL | bounded persistent per-hot-target network correspondence; deepen metadata/search |
| `get_network_request` | ADD CORE | request/response headers, timing, bodies, initiator, redirects, request body, lazy/streaming response body |

Mandatory depth pulled forward by DVA:

- timings and headers;
- redirect chains;
- request bodies;
- bounded durable response bodies when live CDP supports them;
- large-body streaming/artifacts;
- WebSocket frames and SSE events;
- GraphQL recognition/index;
- service-worker/cache relationships;
- initiator/runtime stack correlation;
- semantic-object correlation as evidence-qualified association, never magical causality.

No packet capture/MITM default is added.

### Debugging / page inspection

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `evaluate_script` | EQUIVALENT/PRESENT | raw `js.evaluate` / runtime access |
| `get_console_message` | EQUIVALENT/PRESENT | `console.get` |
| `list_console_messages` | EQUIVALENT/PRESENT | bounded `console.list` |
| `lighthouse_audit` | ADD DEBUG + SKILL | on-demand Lighthouse runner/report artifact + compact audit summary |
| `take_screenshot` | EQUIVALENT/PRESENT/PARTIAL | element/full-page already present; add region and format/quality options only if useful |
| `take_snapshot` | STRONGER | persistent semantic surface/deltas; raw AX/DOM remain available |
| `screencast_start` | ADD DEBUG | on-demand browser screencast artifact/session |
| `screencast_stop` | ADD DEBUG | stop/finalize artifact; never always-on |

### Memory

All current MCP heap tools are high-value evidence. eyeBROWSE will not copy their server-owned loaded-snapshot identity; it will expose a smaller browser-native capture surface plus local Program Host analysis over artifact files.

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `take_heapsnapshot` | ADD DEBUG | `memory.heap_snapshot` → artifact |
| `close_heapsnapshot` | REJECT AS KERNEL STATE | no permanent loaded-snapshot registry; Program Host releases transient parser state |
| `compare_heapsnapshots` | SKILL + PROGRAM | local transient analyzer |
| `get_heapsnapshot_class_nodes` | SKILL + PROGRAM | local index/query |
| `get_heapsnapshot_details` | SKILL + PROGRAM | local index/query |
| `get_heapsnapshot_dominators` | SKILL + PROGRAM | local dominator analysis |
| `get_heapsnapshot_duplicate_strings` | SKILL + PROGRAM | local string aggregation |
| `get_heapsnapshot_edges` | SKILL + PROGRAM | local graph query |
| `get_heapsnapshot_object_details` | SKILL + PROGRAM | local node detail |
| `get_heapsnapshot_retainers` | SKILL + PROGRAM | local retainer query |
| `get_heapsnapshot_retaining_paths` | SKILL + PROGRAM | local retaining-path query |
| `get_heapsnapshot_summary` | SKILL + PROGRAM | local summary/reduction |

Also add/use on demand where live capability exists:

- `Memory.getDOMCounters` or equivalent current memory counters;
- `Runtime.getHeapUsage`;
- `HeapProfiler.startSampling/stopSampling/getSamplingProfile`;
- garbage collection only in controlled diagnostic fixtures when required;
- detached-DOM investigation through heap metadata and browser semantics.

Heap snapshots remain artifacts/data, never durable Browser World Graph state.

### Extensions

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `install_extension` | ADD DEBUG | capability-detected `Extensions.loadUnpacked`; isolated test profile only |
| `list_extensions` | ADD DEBUG | `Extensions.getExtensions` |
| `reload_extension` | ADD DEBUG | generic controlled reload semantics using provider-supported lifecycle; never fake support |
| `trigger_extension_action` | ADD DEBUG | `Extensions.triggerAction` |
| `uninstall_extension` | ADD DEBUG | `Extensions.uninstall` in disposable Build 002 profile |

Also expose current extension storage views through `Extensions.getStorageItems` where useful. Build an independent `extension-debug` Skill. Chrome owns extension reality; extension state does not become Skill state.

### Third-party runtime developer tools

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `list_3p_developer_tools` | PROVIDER/PRESENT | `runtime_tools.list` |
| `execute_3p_developer_tool` | PROVIDER/PRESENT | `runtime_tools.execute`; page/document scoped |

`runtime_tools.inspect` is an eyeBROWSE ergonomic addition. Returned DOM objects must reconcile to existing `e_*` where evidence permits. Framework data remains provider data; framework operating knowledge remains Skills.

### WebMCP

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `list_webmcp_tools` | PROVIDER/PRESENT | `webmcp.list/inspect` capability-detected from live page/browser |
| `execute_webmcp_tool` | PROVIDER/PRESENT | `webmcp.execute` |

Add transient diagnostic details useful from the current DevTools WebMCP pane (schema/status/error for the current call) only as bounded current debugging state. Reject a permanent invocation-history product.

### Progressive Web Apps

| DevTools capability | Build 002 classification | eyeBROWSE path |
|---|---|---|
| `get_os_app_state` | RAW CDP / DEFER | current PWA domain available through raw CDP; no frozen acceptance workflow needs typed promotion |
| `install_pwa` | DEFER | valid generic browser capability, outside mandatory Build 002 evidence |
| `launch_pwa` | DEFER | same |
| `uninstall_pwa` | DEFER | same |

This is a deliberate non-adoption for Build 002 typed surface, not a claim that PWA capability is unimportant.

## 4. Attachment / browser-lifecycle comparison

Chrome DevTools for agents supports creating its own profile, manual `--browser-url` / WebSocket attachment, and Chrome 144+ `--autoConnect` to an existing session after browser-side remote-debugging enablement.

Classification:

- existing-session attachment: **useful reference evidence**;
- MCP daemon as browser owner: **REJECT**;
- replacing BrowserProfile/BrowserIncarnation/t_*/d_*/e_* identity with MCP page IDs/snapshot UIDs: **REJECT**;
- capability to attach the Build 002 kernel to its own independently identified durable browser: **STRONGER/PRESENT architecturally**, with runtime isolation made environment-configurable by current implementation;
- external MCP comparison against a disposable test browser after Campaign 3: **AUTHORIZED COMPARISON ONLY**, never production dependency.

## 5. Agentic Skills translation

Google's Skills demonstrate several useful patterns:

1. start from a specialized diagnosis/workflow rather than dumping all tools;
2. select structural snapshot versus screenshot versus script by semantic fit;
3. use local/file reduction for large Lighthouse/trace/heap material;
4. use repeated controlled actions to amplify memory leaks;
5. drill from summary → suspicious class/insight → retainers/details rather than loading giant artifacts;
6. combine performance traces with network and runtime evidence;
7. keep troubleshooting knowledge separate from the browser controller.

Translate these into eyeBROWSE Skills that exploit persistent browser correspondence:

- `web-debug`;
- `network-debug`;
- `performance-debug`;
- `accessibility-debug`;
- `memory-debug`;
- `extension-debug`;
- `agent-readiness`;
- `webmcp`;
- `runtime-tools`.

Do not create a giant `chrome-devtools` Skill. Do not encode snapshot UID workflows where `e_*` identity is available.

## 6. Sources / JavaScript debugger decision

The current CDP Debugger domain exposes script discovery, source URLs/source maps, breakpoints, paused call frames/scopes, exception pause policy, source retrieval/search, and stepping.

Build 002 decision:

- add a compact typed **runtime debugger observation** surface for scripts/paused state/call frames/source retrieval/search only where the developer flagship fixture needs it;
- keep invasive breakpoint/edit/step controls behind raw CDP unless the flagship workflow proves typed promotion materially improves capability;
- never treat repository source as browser authority;
- source/worktree/compiler/edit/test meaning remains CODEeye.

## 7. Dynamic capability registry amendment

Build 002 Capability Registry tests must cover:

- domain present/absent;
- command present/absent;
- experimental command present but failing at runtime;
- changed/unknown schema fields preserved rather than discarded;
- provider facet unavailable after navigation/document replacement;
- fallback to another representation or raw CDP;
- explicit `unsupported` rather than false emulation.

The live protocol remains the source of capability truth. No static DevTools/MCP version map is authoritative.

## 8. Mandatory developer Program Host routines added by this amendment

Implement only where the underlying provider is present and the controlled fixture demonstrates value:

- `developer.collect-debug-summary`;
- `developer.investigate-console-error`;
- `developer.capture-performance-profile`;
- `developer.analyze-memory-leak`;
- `developer.audit-agent-readiness`;
- `developer.inspect-webmcp`;
- `developer.inspect-runtime-tools`.

Large traces, heap snapshots, network sets, console sets, and Lighthouse reports are reduced locally and returned as compact structured findings plus raw artifact handles where useful.

## 9. DVA flagship gates added before measured acceptance

Mandatory controlled developer fixtures/gates:

1. **WebMCP:** unfamiliar fixture; discover → inspect schema → choose → execute → reason → fallback if needed.
2. **Runtime tools:** discover page-provided developer tools; use richer internal state; map returned DOM identity to `e_*` when possible.
3. **Performance:** deliberately slow page; choose timeline versus full trace; locally reduce; identify likely bottleneck; raw trace remains artifact.
4. **Memory:** deterministic leak; capture/compare/analyze retainers; identify retained structure without raw snapshot in model context.
5. **Accessibility:** inaccessible page; diagnose labels/relationships/focus order structurally; vision only where appropriate.
6. **Extension:** controlled broken Build 002 MV3 fixture in isolated profile; use generic extension/runtime debugging to diagnose.
7. **Agent readiness:** unfamiliar page; combine semantic state, AX, forms, WebMCP, Lighthouse Agentic Browsing/runtime tools as available.
8. **Developer cross-Eye:** browser runtime failure/performance issue → eyeBROWSE runtime/network/source evidence → CODEeye source fix/test → browser reload/retest → measured runtime difference.

These are additions to, not replacements for, existing GitHub/horizontal/second-site/control-vs-treatment gates.

## 10. Explicit non-adoptions

Build 002 deliberately does not adopt:

- Chrome DevTools MCP/CLI as a production dependency or browser owner;
- MCP page IDs or snapshot UIDs as canonical identity;
- Google tool names as a compatibility surface merely for parity;
- MCP daemon lifecycle/telemetry as eyeBROWSE runtime architecture;
- blocked/allowed URL patterns, redaction, or server safety policy as eyeBROWSE policy machinery;
- permanent WebMCP invocation history;
- permanent loaded heap-snapshot registry;
- PWA typed breadth without a Build 002 workflow;
- DevTools target automation as a second controller;
- always-on screenshots/screencast/traces/heap sampling;
- source editing/workspace semantics that belong to CODEeye;
- a generic DevTools mega-Skill.

## 11. Updated completion condition

Build 002 cannot be `DEMONSTRATED` unless the original frozen mandatory gates plus DVA-66 are answered by measured evidence. The final results must contain a DevTools capability acceptance matrix with:

```text
Capability
Google DevTools-for-agents equivalent
Build 001 state
Build 002 state
implementation path
Skill using it
measured result
```

The objective is not exact product parity. It is to preserve eyeBROWSE advantages—persistent browser/controller separation, conceptual identity, lifecycle correctness, deltas, exact surviving-document recovery, local programs, provider fusion, Skill composition, and sibling-Eye composition—while closing legitimate generic browser/debugging gaps exposed by current Chrome work.
