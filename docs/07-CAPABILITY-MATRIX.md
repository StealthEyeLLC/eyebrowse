# 07 — Intended Capability Matrix

Status: **Canonical end-state capability classification**

Classifications:

- **Core** — fundamental product capability.
- **Advanced** — important higher-order capability built after the core spine.
- **Fallback** — used when richer/cleaner structured mechanisms are insufficient.
- **Experimental** — high-value but browser/spec/implementation maturity requires capability detection and prototyping.
- **Unnecessary** — deliberately not part of the normal architecture unless evidence changes.

| Capability | Classification | Primary path |
|---|---|---|
| Browser start/stop/restart | Core | .NET kernel + process control + CDP |
| Persistent Chrome process | Core | stock Chrome Stable |
| Persistent browser profiles | Core | dedicated user-data dirs |
| Ephemeral sessions | Core | BrowserContexts / Chrome for Testing later |
| Multiple durable identities | Core | one profile/process per durable identity |
| Multiple browser contexts | Core | CDP Target |
| Headful operation | Core/default | stock Chrome |
| Headless operation | Core | unified Chrome/CfT |
| Browser version/protocol discovery | Core | `/json/version`, `/json/protocol`, Browser.getVersion |
| Browser upgrade adaptation | Core | Capability Registry + dynamic CDP |
| Browser windows | Core | CDP Browser/window + native later |
| Tabs/pages | Core | Target graph |
| Popups/new tabs | Core | Target/opener relationships |
| Targets | Core | Target discovery/auto-attach |
| Unknown/future targets | Core | generic target nodes/raw CDP |
| Frames | Core | Page/Target |
| Nested iframes | Core | frame graph |
| Cross-origin/OOPIF | Core | recursive flattened target attachment |
| Workers | Core | Target |
| Shared workers | Core | Target |
| Service workers | Core | Target + ServiceWorker |
| Extension pages/workers | Core | Target/extension |
| DevTools/browser UI targets | Experimental | raw Target/CDP research |
| FrameSlot identity | Core | Browser World Graph |
| DocumentInstance identity | Core | frame/loader/runtime/extension identity |
| Same-document navigation | Core | lifecycle instrumentation |
| BFCache/cached documents | Advanced/Core after Build 001 | document lifecycle |
| Prerender | Advanced | Preload + document lifecycle |
| Renderer incarnation tracking | Core model | target/runtime lifecycle |
| DOM | Core | CDP DOM |
| DOMSnapshot | Core | CDP DOMSnapshot |
| Accessibility | Core | CDP Accessibility |
| Annotated Page Content | Experimental/high value | Page.getAnnotatedPageContent |
| Computed styles | Advanced/lazy | DOMSnapshot/CSS |
| Layout geometry | Core | DOM/APC/layout |
| Hit testing | Core | DOM.getNodeForLocation / layout |
| Focus | Core | DOM/AX/JS/extension |
| Selection | Core | JS/extension/DOM |
| Form state | Core | DOM/AX/APC/JS |
| Validation state | Core | DOM/JS |
| Semantic regions | Core | state fusion |
| Interactable index | Core | semantic state graph |
| Tables/lists | Core | semantic graph |
| Semantic collections | Advanced | DOM/app/network/scroll fusion |
| Virtualized lists/grids | Core target | collection + app/network state |
| Infinite scrolling | Core target | semantic collection operations |
| Shadow DOM | Core | CDP DOM/DOMSnapshot |
| Closed-shadow enhancement | Advanced | CDP + document-start instrumentation |
| Persistent logical element IDs | Core | Identity Engine |
| Document-side NodeSerial identity | Core | MV3 agent-bridge |
| BackendNodeId anchoring | Core | CDP DOM |
| AX ID anchoring | Core | Accessibility when enabled |
| APC identity anchor | Experimental | APC provider |
| Logical binding incarnation | Core | Identity Engine |
| Semantic node replacement recovery | Core | reconciliation engine |
| Explicit stale/ambiguous objects | Core | Identity Engine |
| Cursor observation | Core | Delta Engine |
| Semantic delta stream | Core | event fusion/coalescing |
| Mutation observation | Core | MutationObserver + CDP |
| AX updates | Core | Accessibility events |
| Target lifecycle events | Core | Target |
| Document lifecycle events | Core | Page/extension/runtime |
| Console output | Core | Runtime/Log |
| JavaScript exceptions | Core | Runtime |
| Primitive waits | Core | event predicate engine |
| Compound waits | Core | any/all/sequence/quiet_for |
| Persistent watches/attention | Advanced/Core post-slice | Attention Engine |
| Browser-side semantic queries | Core | query engine + Runtime |
| Representation Broker | Core | provider selection/fusion |
| Hot/warm/cold target cognition | Advanced | interest scheduler |
| Click | Core | semantic router/CDP/Playwright |
| Double click | Core | CDP/Playwright |
| Context click | Core | CDP/Playwright |
| Hover/pointer movement | Core | CDP Input |
| Wheel/scroll | Core | CDP Input/DOM |
| Element scrolling | Core | DOM/JS/action router |
| Keyboard | Core | CDP Input |
| Text insertion | Core | CDP Input/insertText |
| Key combinations | Core | CDP Input |
| Form filling | Core | semantic action router |
| Select/radio/checkbox | Core | semantic/DOM/Playwright |
| Sliders/date controls | Advanced/Core interaction | action router |
| Rich text/contenteditable | Core target | focus/input/selection/DOM |
| Drag/drop | Core target | pointer + DataTransfer |
| Clipboard | Core eventually | page + SessionHost |
| File input | Core | DOM.setFileInputFiles |
| File chooser interception | Core | Page/Playwright |
| Directory upload | Advanced | file-input semantics |
| Native upload dialog | Fallback | SessionHost/UIA |
| Playwright action provider | Advanced/Core convenience | optional adapter |
| Puppeteer production layer | Unnecessary initially | direct CDP already owns unique value |
| Selenium/WebDriver Classic core | Unnecessary | — |
| JavaScript Runtime evaluate | Core | CDP Runtime |
| Runtime callFunctionOn | Core | CDP Runtime |
| Main-world execution | Core | Runtime/extension |
| Isolated worlds | Core | Page/Runtime/extension |
| Preload/document-start helpers | Core | extension/Page |
| Browser↔controller bindings | Core | Runtime bindings/extension |
| Remote object handles | Core | Runtime |
| Browser-side reductions | Core | Runtime/query engine |
| Agent Program Host | Core | Node 24 + kernel SDK |
| Program loops/branches | Core | Program Host |
| Persistent program sessions | Advanced/Core | Program Host |
| Program multi-tab concurrency | Advanced | Program Host + kernel scheduler |
| URL navigation | Core | Page |
| Redirect handling | Core | Page/Network |
| history back/forward | Core | Page |
| Reload | Core | Page |
| SPA route transitions | Core | lifecycle + instrumentation |
| Fragment navigation | Core | lifecycle |
| External protocol launch | Advanced | Chrome + SessionHost |
| Request observation | Core | Network |
| Response observation | Core | Network |
| Headers/status/timing | Core | Network |
| Request bodies | Core/lazy | Network |
| Response bodies | Core/lazy | Network/Fetch |
| Durable response bodies | Advanced/Core hot targets | Network.configureDurableMessages |
| Body search | Advanced | Network search/body processing |
| Large body streaming | Advanced | Network/Fetch/IO |
| Network search | Core | network index |
| Redirect chains | Core | Network |
| XHR/fetch activity | Core | Network |
| GraphQL understanding | Advanced | network semantic lens |
| WebSockets | Core | Network |
| SSE/EventSource | Core | Network |
| WebTransport | Advanced | Network |
| Request interception | Advanced | Fetch |
| Request modification | Advanced | Fetch |
| Response modification/fulfillment | Advanced | Fetch |
| Offline/throttling/cache emulation | Advanced | Network/Emulation |
| HAR-compatible export | Advanced/on demand | Network |
| Producer correlation UI↔network | Advanced | Browser World Graph |
| Request JS-stack association | Advanced | Network/Runtime |
| Default MITM proxy | Unnecessary | add only for proven CDP gap |
| Packet inspection | Experimental | pktmon/external |
| Cookies | Core | Storage/Network |
| localStorage | Core | DOMStorage/Runtime |
| sessionStorage | Core | DOMStorage/Runtime |
| IndexedDB | Core | IndexedDB/Storage |
| Cache Storage | Core | CacheStorage/Storage |
| Origin/storage-key usage | Core | Storage |
| Service-worker state | Core | ServiceWorker/Target |
| Browser permissions | Core capability | Browser/native |
| Complete-profile authentication | Core | Chrome profile |
| Multiple authenticated identities | Core | separate profiles |
| OAuth popup flows | Core | target graph |
| Device-bound session awareness | Advanced/Core auth reality | Network/auth graph |
| WebAuthn | Advanced | WebAuthn/browser UI |
| FedCM | Advanced | FedCm/browser UI |
| Selective auth export | Advanced/best effort | Storage APIs |
| Downloads detection | Core | Browser download events |
| Download filename/path | Core | Browser + extension |
| Download progress | Core | Browser events |
| Download cancellation | Core | Browser/extension |
| Blob downloads | Core target | browser download lifecycle |
| Authenticated downloads | Core target | profile/browser lifecycle |
| Direct resource extraction | Advanced | Network/IO |
| Artifact handles/data plane | Core | AgentBrowser.Artifacts |
| Viewport screenshot | Core | Page.captureScreenshot |
| Full-page screenshot | Core | Page/layout |
| Element/region screenshot | Core | geometry + Page |
| High-DPI alignment | Core visual correctness | geometry transforms |
| Full browser/window capture | Advanced | Windows Graphics Capture |
| Screencast | Advanced | Page screencast |
| Temporal recording | Experimental/Advanced | Page recording/capture |
| Local frame differencing | Advanced | GPU/vision worker |
| OCR | Fallback | packaged Windows OCR/alternative |
| Visual grounding | Advanced | multimodal provider |
| Visual region identity | Advanced | `v_*` graph objects |
| Visual↔structured binding | Advanced | `v_* ↔ e_*` |
| Canvas | Advanced | Runtime/app state + vision |
| WebGL | Advanced | Runtime/app state + vision |
| WebGPU | Advanced | Runtime/app state + vision |
| Generic GPU-command reversal | Unnecessary initially | — |
| JavaScript alerts/confirms/prompts | Core | Page |
| beforeunload | Core | Page |
| Browser permission bubbles | Advanced/fallback | Browser APIs + UIA |
| Browser chrome | Advanced | UIA + browser target research |
| Native dialogs | Fallback/core boundary | SessionHost/UIA |
| Native auth windows | Fallback/core boundary | SessionHost/UIA |
| Print dialog | Fallback | SessionHost/UIA |
| External apps | Advanced | SessionHost |
| HWND/window management | Advanced | SessionHost |
| Native capture | Fallback/core boundary | Graphics Capture |
| Native mouse/keyboard | Fallback | SendInput |
| Cross-process drag/drop | Advanced | SessionHost |
| PDF source retrieval | Core artifact capability | Network/download |
| PDF parser/text/metadata | Advanced | dedicated parser |
| Chrome PDF viewer interaction | Advanced | target/DOM/visual |
| PDF page screenshots | Advanced | browser/parser |
| PDF OCR | Fallback | OCR for scanned pages |
| HTML print-to-PDF | Advanced | Page.printToPDF |
| Audio/video DOM state | Core | DOM/Runtime |
| Media metadata | Core/Advanced | DOM/Media |
| Captions/subtitles | Advanced | DOM/network |
| MediaSource/stream metadata | Advanced | Network/Runtime |
| WebAudio | Advanced | CDP WebAudio |
| WebRTC | Advanced | Runtime/getStats/network/permissions |
| Direct audio perception | Fallback | audio capture/model |
| MV3 agent-bridge | Core | extension |
| Document-start instrumentation | Core | extension |
| webNavigation document identity | Core | extension |
| Extension download/browser APIs | Advanced/Core where useful | extension |
| Main-world app instrumentation | Advanced | extension |
| userScripts helper world | Experimental | extension API |
| WebMCP | Experimental/high value | WebMCP provider |
| Third-party DevTools tools | Experimental/high value | runtime provider |
| Framework lenses | Experimental | app/runtime provider |
| Browser recovery | Core | profile/process model |
| Renderer recovery | Core | target lifecycle |
| Kernel/controller recovery | Core | CDP reconnect + identity recovery |
| Extension worker recovery | Core | reconnect design |
| Program Host recovery | Core | restart disposable worker |
| Multi-tab concurrency | Core | target scheduler |
| Multi-context concurrency | Core | BrowserContexts |
| Multi-profile concurrency | Core | multiple Chrome processes |
| Multi-agent routing | Advanced | target command queues/leases |
| Chrome for Testing worker pool | Advanced/Core later | CfT |
| Edge | Advanced | CDP/BiDi |
| WebDriver BiDi | Advanced/interoperability | future adapter |
| Firefox | Advanced future | BiDi |
| WebView2/CEF primary runtime | Deferred | wrong default product environment |
| Raw CDP methods/events | Core | permanent escape hatch |
| Raw JavaScript | Core | Runtime |
| Raw DOM | Core | DOM |
| Raw accessibility | Core | Accessibility |
| Raw network | Core | Network/Fetch |
| Browser command-line control | Core | launcher |
| Native browser process control | Core | .NET |
| Deterministic full-browser replay | Experimental/low priority | future research |
| Custom Chromium fork | Experimental escalation | blocker-driven only |
| Permanent action ledger | Unnecessary | prohibited by charter |
| Runtime verification pipeline | Unnecessary | prohibited by charter |
| Receipt/evidence subsystem | Unnecessary | prohibited by charter |
| Project authority/policy engine | Unnecessary | prohibited by charter |

## Build 001 subset

The first build intentionally implements only the subset required for Milestones A–D. The end-state classification above does not authorize secondary work to displace Build 001.

Build 001 core subset:

```text
persistent stock Chrome
profiles
dynamic CDP
Target/FrameSlot/DocumentInstance minimum
DOM/DOMSnapshot/AX/APC probe
logical e_* IDs
MV3 NodeSerial identity
actions/navigation/JS
cursor/deltas
wait engine
network baseline
artifacts/download baseline
kernel reconnect/recovery
Agent Program Host
raw escape hatches
```
