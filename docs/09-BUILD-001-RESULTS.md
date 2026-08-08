# 09 — Build 001 Results

Status: **COMPLETE — four-milestone Browser Kernel Slice passed on STEALTHEYELLC on 2026-08-08**

Canonical scope: `docs/02-BUILD-001-SLICE.md`
Implementation issues: #1, #2, #3, #4
Parent build issue: #5

## 1. Meaning of complete

Build 001 completion is defined by the four acceptance gates in the canonical slice specification:

- **A — persistent browser:** Chrome survives kernel death and can be reattached without browser restart.
- **B — persistent agent objects:** compact semantic objects with `e_*` IDs can be queried/acted on and ordinary work produces deltas rather than whole-page rediscovery.
- **C — recovery continuity:** exact surviving document/object bindings return after kernel death and can be used again.
- **D — programmable browser:** one Program Host invocation performs 20+ meaningful browser operations against persistent state.

All four gates passed on the physical target laptop. This document records what is actually implemented and distinguishes it from broader post-gate hardening described elsewhere in the architecture.

## 2. Live Build 001 topology

```text
STEALTHEYELLC interactive Windows session
│
├─ Google Chrome Stable 151.0.7922.109
│  ├─ headful / GPU enabled
│  ├─ persistent user-data-dir: C:\AgentBrowser\Profiles\dev
│  ├─ current dynamic CDP port: 62510
│  ├─ current browser WebSocket GUID: 2d77d454-bced-4760-982c-33ebbc033dc3
│  └─ MV3 eyebrowse agent-bridge loaded in profile
│
├─ Windows Scheduled Task: eyebrowse-kernel-dev
│  └─ .NET 10 AgentBrowser.Kernel
│     ├─ direct browser-level CDP WebSocket
│     ├─ named pipe: \\.\pipe\eyebrowse-dev
│     ├─ semantic state / cursors / logical IDs
│     ├─ document identity recovery
│     ├─ first-party actions / JS / waits
│     └─ bounded Network request state
│
└─ disposable Node 24 Program Host
   └─ one persistent named-pipe connection per program invocation
```

Last acceptance-run process state:

```text
Chrome launch/root PID: 25324
Kernel PID:             24616
Chrome CDP port:        62510
Chrome protocol:        1.3
Node:                   v24.18.1
npm:                    11.16.0
```

PIDs and the port are runtime values, not fixed configuration.

## 3. Machine/runtime paths created by Build 001

```text
C:\AgentBrowser\
├─ Profiles\
│  └─ dev\                  persistent Chrome identity
├─ runtime\
│  ├─ dev.json              current browser runtime descriptor
│  ├─ kernel-dev.json       current kernel descriptor
│  └─ logical-ids-dev.json  current logical-ID allocation/target mappings
└─ tools\
   └─ node-v24.18.1-win-x64\ portable Node runtime

X:\AgentBrowser\
├─ repo\                    working clone of StealthEyeLLC/eyebrowse
├─ Artifacts\               large artifact root for later artifact plane
└─ Temp\                    transient build/runtime scratch
```

No action ledger, permanent browser-event history, execution receipt store, or policy/approval database was created.

## 4. Repository implementation map

### Direct CDP

```text
src/AgentBrowser.Cdp/
├─ CdpClient.cs
└─ CdpDiscovery.cs
```

Implemented:

- native .NET `ClientWebSocket` transport;
- command IDs and response correlation;
- flattened target-session `sessionId` routing;
- CDP event dispatch;
- cancellation/error propagation;
- live `/json/version` discovery;
- live `/json/protocol` discovery;
- domain/qualified-command capability registry;
- raw dynamic CDP access.

No Playwright, Puppeteer, or Selenium layer owns Chrome.

### Persistent kernel/state

```text
src/AgentBrowser.Kernel/
├─ BrowserRuntime.cs
├─ BrowserStateEngine.cs
├─ DocumentIdentityBridge.cs
├─ LogicalIdStore.cs
├─ PipeRpc.cs
└─ Program.cs

src/AgentBrowser.State/
├─ SemanticModels.cs
└─ NetworkModels.cs
```

Implemented Build 001 RPC surface includes:

```text
browser.status

target.list
target.open

observe.surface
observe.delta
query.find
inspect.element

action.click
action.fill
action.type
action.key
action.scroll

wait.until

js.evaluate

network.search
network.body

cdp.send
```

The pipe protocol is newline-delimited JSON request/response over a Windows named pipe. This is the local client transport; Chrome control remains direct CDP.

### MV3 document identity

```text
extension/agent-bridge/
├─ manifest.json
├─ bridge.js
└─ service-worker.js
```

The isolated-world bridge maintains:

```text
WeakMap<Node, NodeSerial>
NodeSerial -> WeakRef<Node>
NodeSerial -> logical e_*
logical e_* -> NodeSerial
document token
document logical ID
bounded mutation/focus/input/change/selection/scroll events
```

A key Build 001 discovery is that exact recovery does **not** require the MV3 service worker to remain alive. The kernel can rediscover the extension isolated execution world through CDP Runtime contexts, probe for `globalThis.__eyebrowseIdentity`, resolve browser nodes directly into that execution context, and recover the document-resident bindings.

The current extension is deliberately plain JavaScript for the slice. TypeScript remains appropriate once extension surface area justifies a build step; this does not change the runtime identity protocol.

### Program Host

```text
program-host/
├─ package.json
├─ sdk/eyebrowse.mjs
├─ src/run.mjs
└─ examples/milestone-d.mjs
```

The Build 001 Program Host:

- uses Node 24 LTS;
- has zero external npm runtime dependencies;
- talks only to the persistent kernel named pipe;
- does not open a second CDP connection as an alternate authority;
- is disposable;
- returns one compact structured result;
- counts calls only inside the transient acceptance program, not in a durable action ledger.

## 5. Milestone A — persistent browser

Acceptance sequence passed:

1. launched Chrome against `C:\AgentBrowser\Profiles\dev` with dynamic `--remote-debugging-port=0`;
2. Chrome chose port 62510;
3. live schema exposed 57 domains, including Target, DOM, DOMSnapshot, Accessibility, Input, Network, Storage, Extensions, FedCM, WebAuthn, and WebMCP;
4. direct `Target.createTarget` opened Example Domain;
5. direct `Runtime.evaluate` read the live page;
6. persistent kernel process was force-killed;
7. Chrome root PID 25324 and the page target remained alive;
8. a fresh direct-CDP client saw the same page target;
9. replacement kernel reattached to the existing browser/profile rather than launching a replacement browser.

Because Eye-launched detached descendants are cleaned up with the connector job, the persistent dev kernel is hosted as a normal Windows scheduled task under the logged-in interactive user:

```text
eyebrowse-kernel-dev
```

This is runtime/process-lifetime mechanics, not an approval or safety layer.

## 6. Milestone B — semantic objects and deltas

Deterministic fixture:

```text
tests/fixtures/milestone-b.html
```

Initial fixture capture:

```text
AX nodes:          25
DOMSnapshot nodes: 49
semantic objects:  4
APC command:        available in live Chrome schema
```

Demonstrated:

- semantic textbox `e_2` filled with `StealthEye`;
- semantic button clicked through browser geometry/CDP Input;
- dynamically created button appeared as a new `e_*` object;
- delta returned only added/changed semantic objects;
- in-place button rename retained the same logical ID;
- `query.find` filtered by role/name content;
- `action.type`, keyboard `Tab`, and wheel scrolling worked through direct CDP.

Real-site pressure test on public `StealthEyeLLC/eyebrowse` GitHub page:

```text
AX nodes:           1,147
DOMSnapshot nodes:  2,431
actionable objects:    89
```

The normal agent surface was the reduced semantic object set, not the raw DOM/AX payload.

## 7. Milestone C — exact identity after kernel death

Fresh post-extension identity fixture:

```text
logical target: t_6
raw target:     AA36B3EC74DEACDBD943944E78FC151A
document:       d_1
textbox:        e_1
25 buttons:     e_2 ... e_26
```

Before kernel death:

- filled `e_1` with `survives-kernel-death`;
- clicked `e_2`, changing its name from `Persistent button 1` to `Clicked button 1`;
- captured the semantic delta.

Then:

1. force-killed kernel PID 29080;
2. confirmed zero state-owning kernel processes remained;
3. confirmed Chrome and raw target `AA36...151A` remained alive;
4. restarted a fresh kernel process;
5. durable target mapping restored the target as `t_6`;
6. surviving extension document restored `d_1`;
7. textbox restored as exactly `e_1` with the pre-kill value;
8. first button restored as exactly `e_2` with its already-mutated name;
9. all 25 buttons restored as exactly `e_2 ... e_26`;
10. old pre-kill `e_3` was clicked after restart;
11. a fresh delta reported `Persistent button 2 -> Clicked button 2` under the same `d_1`.

This proves exact surviving-document identity, not equivalent-element rediscovery.

## 8. Milestone D — programmable browser

Portable runtime:

```text
C:\AgentBrowser\tools\node-v24.18.1-win-x64\node.exe
```

Final strengthened acceptance invocation:

```text
node program-host/src/run.mjs program-host/examples/milestone-d.mjs
```

Result:

```text
ok:                    true
kernel operations:     33
wall time:              1,481 ms
primary target:         t_16
primary document:       d_6
baseline cursor:        1
delta cursor:           17
changed semantic objs: 13
semantic buttons hit:  12/12
real delayed wait:      302 ms
secondary target:       t_17
network request:        req_2
network status:         200
network MIME:           text/html
network body:           559 bytes, lazy-fetched
Chrome:                 151.0.7922.109
```

The single program invocation performed:

- browser status;
- target creation;
- navigation-resilient browser wait;
- JavaScript fixture creation;
- semantic observation;
- semantic textbox query/fill;
- semantic collection query;
- 12 logical-object clicks in a local loop;
- same-origin Fetch API request;
- structured kernel network search;
- lazy network body retrieval;
- delayed DOM scheduling;
- a real browser-resident wait;
- semantic delta collection;
- JS application-state inspection;
- second-tab creation and committed-navigation wait;
- second-tab JS inspection;
- live target listing;
- raw `Browser.getVersion`;
- browser storage/cookie inspection.

The Program Host exited afterward while Chrome and the persistent kernel remained alive.

## 9. Network spine implemented during D hardening

Every page target that becomes hot in the kernel enables CDP Network and receives a bounded in-memory request index.

Current normalized fields include:

```text
req_* logical ID
raw CDP request ID
target/document association where known
URL
method
resource type
initiator type
status
MIME type
completed/failed state
error text
encoded data length
start/finish timestamps
```

Current API:

```text
network.search(target, contains?, method?, status?, limit?)
network.body(req_*)
```

Response bodies are fetched on demand with `Network.getResponseBody`; the kernel does not archive every body.

The first request index is bounded to 1,000 current requests per hot target and evicts oldest entries when over that bound.

## 10. Real defects found and fixed during Build 001

### Optional CDP key fields

`Input.dispatchKeyEvent` rejected a serialized optional `text: null` property for `Tab`.

Fix: omit optional CDP fields when absent rather than serializing null.

### Initial-document race

`Target.createTarget(url)` can expose an initial complete document before the requested navigation commits.

Fix: acceptance/navigation waits require both the intended URL/document condition and readiness rather than testing `readyState` alone.

### Wait context destroyed by navigation

An awaited Runtime promise can be invalidated when navigation destroys its execution context.

Fix: `wait.until` keeps one original deadline, catches navigation-context destruction, waits briefly for the replacement context, and resumes the condition against the new document.

### Eye descendant lifetime

A process detached from an Eye `run` call is still subject to the connector job lifetime.

Fix: host the interactive dev kernel under a Windows scheduled task instead of pretending connector-child detachment is durable process ownership.

## 11. Architectural discoveries promoted by the slice

1. **Direct dynamic CDP is sufficient for the permanent kernel spine.** No automation framework is needed for browser ownership.
2. **Document-resident extension identity survives kernel death exactly.** This is now demonstrated, not hypothetical.
3. **CDP can directly reach the extension isolated world.** Exact recovery need not depend on an immortal MV3 worker or a mandatory extension-to-kernel WebSocket.
4. **Target logical IDs should be current durable operating state.** They survive kernel reconstruction without storing action history.
5. **Waits must be navigation-lifecycle aware.** A wait is attached to a target intent, not one ephemeral JavaScript execution context.
6. **Program execution materially compresses interaction.** The acceptance workflow performed 33 kernel operations in one reasoning-client invocation.
7. **Network state belongs next to semantic state.** A Program Host can reason from the structured request/response without moving all response bodies into model context.

## 12. Build 001 boundaries — intentionally not claimed complete

The four-milestone slice is complete. The following broader surfaces in the architecture are **not yet claimed production-complete** and remain next work rather than being silently counted as done:

- full lifecycle Browser World Graph for nested frames/OOPIFs, workers, service workers, BFCache and prerender;
- complete semantic region model;
- APC protobuf decode/fusion (capability is detected; APC is not yet the canonical surface provider);
- semantic rebinding of destroyed/recreated nodes with explicit `exact/rebound/stale/ambiguous` results and false-rebind metrics;
- full wait predicate algebra (`any/all/sequence`, semantic element conditions, network-finished predicates, target create/close predicates);
- full typed navigation/action set (back/forward/reload, hover, double-click, focus, target activate/close);
- GraphQL classification;
- WebSocket/SSE frame indexing;
- downloads/artifact API and file upload depth;
- `cdp.subscribe` client streaming surface;
- persistent Program Host sessions / `program.eval`;
- Playwright actuator;
- Windows SessionHost/UIA/WGC/OCR;
- visual/temporal perception;
- WebMCP/third-party DevTools tools;
- deep auth/DBSC/FedCM/WebAuthn modeling;
- long soak/benchmark hardening.

These are additions to a working spine, not reasons to rewrite Build 001.

## 13. Constraint compliance

Build 001 did not add:

- project-specific approval/permission mechanisms;
- project policy/guardrail authority;
- privilege/action tiers;
- mandatory verification agents or stages;
- action receipts/evidence bundles;
- permanent browser-action ledger.

Durable files store only current operating information needed to find the browser/kernel and prevent logical-ID collisions during continuity recovery.

## 14. Current handoff state

At the end of Build 001 acceptance:

```text
Chrome:             running
persistent profile: running
MV3 bridge:         installed/loaded in dev profile
kernel scheduled task: running
named pipe:         available
Node Program Host:  installed and runnable
Git working tree:   clean before documentation update
A issue:            complete
B issue:            complete
C issue:            complete
D issue:            complete
```

The next implementation should begin from the priorities in `04-ROADMAP.md`, not by rebuilding this spine.