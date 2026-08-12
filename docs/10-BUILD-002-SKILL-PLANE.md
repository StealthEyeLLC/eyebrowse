# 10 — Build 002 Candidate: Lifecycle-Hardened Skill Plane and Procedural Browser Operating Environment

Status: **CANDIDATE / PROSPECTIVE BUILD 002 — NOT CANONICAL**  
Freeze date: **2026-08-12**  
Base main commit: `2e27f44ebd3522d0d26b036dc57f790535df3533`  
Base main tree: `6af044a71f3e41168abf6fb03bee80e0bd2d2b0c`  
Implementation branch: `build/build002-skill-plane`

This document freezes the prospective Build 002 architecture and evaluation envelope before broad implementation. It does not amend the numbered canonical specification and does not authorize movement of `main`. Canonical promotion requires measured acceptance and an explicit post-measurement promotion decision.

## 1. Mission

Build the strongest candidate browser operating environment justified by the Build 001 spine plus measured Build 002 evidence:

- finish modern browser/document lifecycle correctness;
- make semantic rebinding conservative and explicit;
- scale browser cognition lazily across large target sets;
- expose neutral current browser context;
- package procedural expertise as current-format ChatGPT Skills;
- compile repeated deterministic procedures into source-controlled Program Host routines;
- prove deep GitHub operation without putting GitHub ontology into the browser kernel;
- prove reusable horizontal browser procedures;
- deepen the browser representations needed by those procedures;
- add page-native WebMCP and runtime-tool providers where live capability supports them;
- add debugging, artifact, network, storage, interaction, and visual depth only where real Build 002 workflows earn it;
- prove Skill composition and a structurally different second application;
- compare fresh subjects with and without procedural Skills while holding underlying capability constant;
- preserve every measured Build 001 property.

The central Build 002 claim under test is:

> A fresh ChatGPT instance can enter a real browser context, acquire relevant procedural expertise automatically, reason over compact persistent eyeBROWSE state, execute substantial deterministic work locally, select the correct representation of truth, and compose with sibling Eyes without creating a mega-Eye.

## 2. Frozen authority equation

```text
Chrome
    owns browser material truth

eyeBROWSE
    owns persistent agent/browser correspondence,
    generic browser concepts,
    compact observations and deltas,
    identity and lifecycle,
    queries and waits,
    browser action routing,
    generic browser-local program access

Skills
    own procedural expertise

ChatGPT
    owns cognition and novel reasoning

Program Host
    owns transient local computation

Sibling Eyes
    own their native worlds
```

Invariant:

> **Skills know how. eyeBROWSE knows the browser world. Chrome remains authoritative for browser reality.**

## 3. Build 001 regression obligations

Build 002 inherits, does not rebuild, the measured Build 001 spine:

1. persistent Chrome independent of kernel lifetime;
2. persistent logical `e_*` objects and compact semantic/delta operation;
3. exact surviving-document recovery through document-resident identity plus direct CDP;
4. direct dynamic CDP with runtime capability discovery and raw escape hatches;
5. bounded current Network state with lazy bodies;
6. navigation-aware browser waits;
7. one Program Host invocation executing substantial local browser work (33 kernel operations measured in Build 001).

No Build 002 change may weaken these properties to simplify new work.

## 4. Absolute architecture boundaries

Build 002 must not create any of the following:

- site-specific Browser World Graph node types such as GitHub repository/PR/file nodes;
- Skill-owned browser identity, selector identity, Skill browser state, or Skill execution-history stores;
- a browser-agent daemon, GitHub agent, planner service, verifier model, background reasoning model, or other second autonomous brain;
- a second canonical browser controller;
- Playwright, Puppeteer, Selenium, Chrome DevTools MCP, Browser Use, Stagehand, or any similar framework as browser owner;
- a private Git implementation in eyeBROWSE;
- generic process/file/service semantics that belong to SHELLeye;
- generic native Windows UI semantics that belong to DESKTOPeye;
- engineering/worktree/compiler semantics that belong to CODEeye;
- a second privileged machine-control substrate that duplicates Eye;
- a permanent action cache, action ledger, receipt store, audit trail, or execution-history product;
- project-specific risk tiers, policy engines, approval classes, permission databases, allowlists, or denylists;
- a permanent verification agent/stage/pipeline;
- a Skill registry service, Skill marketplace, Skill database, dependency-manager platform, or autonomous Skill daemon.

Normal tests, prospective measurement artifacts, bounded current operational state, browser reobservation, hit testing, stale detection, and event completion conditions remain intrinsic engineering/browser correctness.

## 5. Campaign 3 and `main` isolation

Until explicit owner authorization changes the scientific dependency:

- do not alter `main`;
- do not alter the Campaign 3 checkout at `X:\AgentBrowser\repo`;
- do not alter `C:\AgentBrowser\Profiles\dev`;
- do not alter the Campaign 3/Build 001 Chrome process;
- do not alter `eyebrowse-kernel-dev`;
- do not alter `\\.\pipe\eyebrowse-dev`;
- do not alter `C:\AgentBrowser\runtime\dev.json`, `kernel-dev.json`, or `logical-ids-dev.json`;
- do not replace the unpacked Build 001 extension used by that runtime.

Build 002 source, fixtures, builds, Skills, Program Host routines, and noninteractive tests live in an isolated worktree. A later Build 002 browser acceptance runtime must use distinct profile, pipe, process/task, descriptor, and artifact identities.

## 6. Runtime capability rule

Direct dynamic CDP remains canonical. At runtime discover the actual browser through:

```text
/json/version
/json/protocol
Browser.getVersion
```

Typed capability is promoted only when a real Build 002 workflow benefits. Rare or unstable surfaces remain reachable through `cdp.send`/`cdp.subscribe` rather than expanding the normal model-facing schema merely for completeness.

## 7. Mandatory Milestone A — lifecycle correctness

Implement a lifecycle model in which these lifetimes remain distinct:

```text
Target
FrameSlot
DocumentInstance
RendererIncarnation
ExecutionRealm
```

Mandatory cases:

- same-document History API and fragment navigation do not fabricate a new `DocumentInstance`;
- BFCache entry/restore preserves a genuinely surviving suspended document rather than inventing a replacement;
- prerender creates a real non-active document and activation promotes that document rather than rediscovering it as nonexistent;
- nested frame and cross-origin OOPIF replacement maintain structural frame reasoning independently from document reasoning;
- renderer/process and execution-realm replacement are tracked separately from conceptual document death;
- frozen documents are represented as frozen and JavaScript-dependent operations do not masquerade timeouts as ordinary page failure;
- discarded tabs retain target/tab continuity where Chrome does, while dead document execution and stale elements are represented honestly;
- reactivation/reload after discard creates a new document incarnation when the old one did not survive;
- popup/opener and redirect transitions remain coherent;
- MV3 document instrumentation can be rediscovered/reconnected after worker restart, renderer change, BFCache/prerender transitions, frame replacement, and kernel restart where provider semantics permit continuity.

## 8. Mandatory Milestone B — conservative semantic identity

The externally meaningful identity outcomes are exactly:

```text
exact
rebound
stale
ambiguous
```

A logical element reference may be rendered as `e_42@N`, where `N` is its binding incarnation.

Resolution preference remains evidence-driven:

1. surviving document-side NodeSerial/exact binding;
2. surviving browser anchor;
3. strong application identifiers;
4. stable id/name/data attributes;
5. role + accessible name + label;
6. semantic region/form/landmark membership;
7. href/action/value semantics;
8. local text/tree fingerprint;
9. neighboring concepts;
10. geometry/proximity only as weak evidence.

A destroyed/recreated node may yield `rebound` only when successor evidence is strong enough. If more than one plausible successor remains, return `ambiguous`. Geometry alone may not force a hard rebind.

Mandatory measurement records:

- exact resolutions;
- correct rebounds;
- stale detections;
- ambiguous abstentions;
- false hard rebounds.

Acceptance target: **zero false hard rebounds** in the frozen hostile set.

## 9. Mandatory Milestone C — hot/warm/cold target cognition

Target census must be cheap and browser-level first.

### Hot

Active workflow targets may activate expensive providers such as DOM, Accessibility, Network, Runtime helpers, semantic snapshots, or document instrumentation as needed.

### Warm

Retain target identity, frame/document identities, cheap metadata, and bounded important recent deltas without retaining every heavyweight semantic payload.

### Cold

Retain logical/browser metadata and rebuild derived semantics on demand.

The initial many-target census must not navigate, reload, or deliberately wake frozen/discarded pages merely to list them.

Prospective scale acceptance fixture: at least **100 page-like targets**. Before any explicit per-target deep observation, initial listing/current-context discovery may activate heavyweight semantic providers on no more than the active target plus **four additional targets**. The exact count of total targets may be increased during implementation if needed to expose scale pressure, but this upper bound is the prospective eager-activation gate for the 100-target case.

Record attach/list latency, process working set, CPU sample, heavyweight provider activation count, and number of targets whose execution state is deliberately awakened by eyeBROWSE.

## 10. Mandatory Milestone D — wait depth

Implement the compound one-shot wait forms required by Build 002 workflows:

```text
wait.any
wait.all
wait.sequence
wait.quiet_for
```

Support event conditions around target creation/closure, navigation/document change, network completion, download completion, and semantic object state when a real workflow requires them.

`cdp.subscribe` becomes a bounded raw event-stream escape hatch when Program Host/debugging workflows require direct event subscription.

Persistent `watch.create/list/next/cancel` is **conditional**. Add it only if a Build 002 workflow demonstrates that one-shot waits are insufficient.

## 11. Mandatory Milestone E — current-format Skill Plane

Repository layout begins with:

```text
skills/
```

Each implemented ChatGPT Skill follows the current OpenAI Skill tooling/specification at implementation time. The current candidate expects the tool-confirmed structure:

```text
<skill-name>/
  SKILL.md
  agents/openai.yaml
  references/        # only when useful
  scripts/           # only when deterministic code belongs in the Skill package
  assets/            # only when output assets are needed
```

If current official tooling changes before the acceptance freeze, follow the live tooling and record the package-format change prospectively.

Skill rules:

- `SKILL.md` teaches non-obvious procedural knowledge and stays compact;
- supporting references load only when needed;
- browser authority and durable browser state never move into Skill prose or Skill storage;
- machine/browser execution logic that is deterministic and reusable belongs in Program Host or a generic browser provider, not in prose macros;
- Skills may compose automatically when multiple procedures are useful.

Mandatory initial Skill family sufficient for acceptance:

- `eyebrowse-operator`;
- `github`;
- `current-page-export`;
- `artifact-download`;
- `forms`;
- `multi-tab`;
- `web-debug`;
- `accessibility-debug`;
- `performance-debug`;
- `webmcp`;
- `runtime-tools`.

`memory-debug`, `extension-debug`, and `agent-readiness` are candidate additions and become mandatory only if checked into the final pre-acceptance freeze as supported Skills rather than clearly marked experiments.

## 12. `eyebrowse-operator` procedural contract

Teach a fresh ChatGPT to:

- use `t_*`, `d_*`, and `e_*@incarnation` as persistent browser concepts rather than selectors;
- understand `exact`, `rebound`, `stale`, and `ambiguous`;
- prefer compact `context.current`, `observe.surface`, `observe.delta`, `query`, and `wait` before giant page dumps;
- choose among page-native/WebMCP, provider-native state, semantic/application state, network/application data, DOM/AX/JS, raw CDP, visual state, and sibling-Eye truth according to the question;
- keep browser truth distinct from provider truth and local sibling truth;
- use Program Host for loops/branches/batches/multi-target deterministic work rather than one reasoning round trip per primitive;
- retain raw escape hatches for unusual cases.

This is a repertoire, not a hard-coded universal priority ladder.

## 13. Mandatory Milestone F — neutral current context

Implement generic `context.current` as current browser correspondence, not a site ontology.

Prospective response shape:

```json
{
  "target": "t_14",
  "document": "d_22",
  "lifecycle": "active",
  "url": "https://example.test/path",
  "origin": "https://example.test",
  "title": "Example",
  "canonicalUrl": "https://example.test/path",
  "focus": "e_17",
  "selectedConcept": null,
  "regions": [],
  "availableProviders": []
}
```

Field rules:

- `target` is the logical identity of the currently selected/active browser page target at request time;
- `document` is its current document logical identity when a live document exists, otherwise null;
- `lifecycle` is a generic lifecycle value such as active, prerender, cached, frozen, discarded, or unavailable;
- `url`, `origin`, `title`, and `canonicalUrl` are browser/page-neutral facts;
- `focus` is an existing logical element when known;
- `selectedConcept` remains a generic browser concept or null, never `github.*`;
- `regions` are generic browser semantic regions;
- `availableProviders` advertises capability/current provider availability without granting authority to those providers.

GitHub-specific fields are prohibited from this RPC. If the human changes tabs, the changed `t_*` identity must be visible rather than silently redirecting old references into the newly active tab.

## 14. Mandatory Milestone G — named Program Host procedures

Named procedures remain source code outside the persistent browser kernel.

Preferred repository shape:

```text
program-host/
  skills/
    common/
    github/
    developer/
```

A lightweight launcher accepts a procedure name plus validated JSON arguments and returns compact structured JSON. The kernel does not acquire a site-aware `program.run_named("github...")` ontology. Named routines use the existing eyeBROWSE SDK and explicit sibling-Eye/provider interfaces where appropriate; they do not create a secret second CDP connection.

Mandatory common routines needed by frozen acceptance:

- `common.export-page`;
- `common.download-resource`;
- `common.collect-links`;
- `common.collect-table`;
- `common.multi-tab-compare`;
- `common.batch-form-fill`;
- `common.search-pagination` when the horizontal acceptance fixture requires pagination/traversal.

Mandatory GitHub routines needed by G1–G7:

- `github.resolve-context`;
- `github.acquire-repository`;
- `github.acquire-file`;
- `github.acquire-directory` when directory acquisition is used by acceptance;
- `github.inspect-pr`;
- `github.inspect-failed-actions-run`;
- `github.compare-refs`;
- `github.collect-repository-summary`.

A true Git clone/fetch is CODEeye/authorized engineering-provider work. A repository archive download remains browser/provider artifact work. Do not confuse the two.

Programs are transient: no generic persistent program state or self-growing macro database is introduced.

## 15. Mandatory Milestone H — GitHub Skill depth

GitHub is the first deep procedural proof. GitHub concepts live in the `github` Skill and Program Host/provider interpretation, never as Browser World Graph node types.

The Skill may reason about users, organizations, repositories, refs, commits, files, issues, PRs, reviews, workflows/runs/jobs, artifacts, releases, settings/rulesets, and security findings as procedural/provider concepts.

Route knowledge is a hint. The resolver must fall back to canonical/meta links, browser semantics, application/network state, or provider-native state when route conventions change.

Frozen gates:

- **G1 implicit repository resolution:** from a repository page, `Copy this repository to X:\SkillPlaneTest\eyebrowse.` resolves current repository/default ref without asking for already-visible identifiers;
- **G2 nested resolution:** README/blob, architecture/blob, tree, issue, PR, commit, and Actions-run starting pages resolve the same repository correctly when they belong to it;
- **G3 current-file acquisition:** from a blob page, `Save this file to X:\SkillPlaneTest\current.md.` resolves repository/ref/path and saves source bytes/content rather than rendered GitHub chrome;
- **G4 authority selection:** `What files changed?` uses provider/diff truth while `What does this PR page currently look like?` remains browser/visual truth;
- **G5 failed Actions diagnosis:** current context resolves run → failed job → failed step → relevant logs → source/config where needed, without requiring the human to repeat visible IDs;
- **G6 variation:** an ordinary route/layout variation does not reduce the procedure to a selector macro;
- **G7 cross-substrate:** copy locally, diagnose current Actions failure, fix it through the engineering substrate, and show the resulting diff without semantic takeover by any one substrate.

Mutation cases use disposable Build 002 fixtures/worktrees, never Campaign 3 infrastructure.

## 16. Mandatory Milestone I — horizontal browser Skills

Demonstrate reusable value independent of GitHub:

- **current-page-export:** useful page content to Markdown and a table to CSV, selecting semantic/application/resource representation rather than blindly serializing full HTML;
- **artifact-download:** one resource plus a bounded multi-attachment/PDF case, with browser-native download completion/path association;
- **forms:** semantically fill and submit a multi-field form with one local batch where useful;
- **multi-tab:** compare multiple persistent `t_*` targets and preserve the intended primary tab while investigating others;
- **bounded collection traversal:** pagination/search/virtualized collection procedure when required by the second-site or horizontal fixture.

## 17. Interaction hardening scope

Promote typed interaction semantics only when an acceptance workflow requires them. Candidate operations include hover, double-click, context click, focus, select/check/uncheck, contenteditable fill/selection, drag/drop, file upload/chooser, date/time/slider controls, and nested scrolling.

Playwright is **conditional and replaceable**. Add it only if it materially improves a real workflow. If added, eyeBROWSE still selects `e_*`; Playwright may temporarily map the current browser concept to action machinery, but it never creates canonical identity or owns Chrome/profile/network/lifecycle.

Wrong-target actuation is unacceptable. Use browser geometry, hit testing, or an actionability provider where pointer semantics require it.

## 18. Network/application, storage, and artifact depth

Build 002 must deepen the Build 001 bounded data plane far enough to support the frozen workflows, not to fill an API checklist.

Mandatory where exercised:

- browser download begin/progress/completion/cancellation and deterministic artifact association;
- authenticated/blob resource handling through the live browser when legitimate;
- bounded response-body retention/streaming where large or cross-process resources require it;
- selective GraphQL/WebSocket/SSE indexing when GitHub/second-site workflows benefit;
- request initiator/source correlation when debugging requires it;
- service-worker-aware relationships where present;
- cookies/local/session storage and storage-key/bucket-aware deeper storage views when a frozen workflow requires them;
- artifact handles for material downloads/screenshots/traces/heap data/resources actually produced by Build 002 workflows.

The whole Chrome profile remains authentication authority. No custom auth database is created.

## 19. Mandatory Milestone J — debugging Skills

At minimum, frozen acceptance requires:

- `web-debug`: coordinated console, exceptions, network, Runtime, DOM, and source/stack context;
- `accessibility-debug`: Accessibility tree/semantic relationships and on-demand browser audits where available;
- `performance-debug`: cheap metrics/PerformanceTimeline first, on-demand trace/emulation when needed.

Console/exception state is compact, searchable, and bounded. Tracing and profiling are on demand and stopped after the task.

`memory-debug`, `extension-debug`, and `agent-readiness` remain conditional unless included in the final pre-acceptance freeze as supported candidate capabilities.

## 20. Mandatory Milestone K — WebMCP

Implement a capability-detected provider following the live browser/protocol/API rather than a frozen prototype revision.

Candidate normalized API:

```text
webmcp.list
webmcp.inspect
webmcp.execute
```

WebMCP tool definitions are provider-advertised page capability, not eyeBROWSE browser truth. Provider results referencing DOM/runtime objects may map to existing `e_*` concepts when evidence supports the correlation.

A controlled fixture must expose at least search, filter, add-item, and submit. A fresh Skill-enabled subject receives only an ordinary natural-language task and must discover available tools, interpret schemas, choose the appropriate tool, supply valid arguments, execute it, and reason from the result.

Navigation that changes the page tool set must update provider scope honestly.

## 21. Mandatory Milestone L — runtime/page-native developer tools

Implement generic document-scoped runtime-tool discovery and execution, following the current live provider mechanism.

Candidate normalized API:

```text
runtime_tools.list
runtime_tools.inspect
runtime_tools.execute
```

A tool advertised by one document does not persist into another document/origin unless independently rediscovered. Returned DOM objects map to the existing browser correspondence where supported, never to a parallel selector identity universe.

A deterministic fixture must prove discovery, schema inspection, execution, disappearance/change after navigation, and DOM-object correlation where the provider permits it.

## 22. APC evaluation

APC decoding/fusion is a **mandatory evaluation**, not a mandatory promoted provider.

When supported, measure `Page.getAnnotatedPageContent` against the existing semantic surface on:

- latency;
- serialized/token-relevant size;
- actionable/semantic coverage;
- identity usefulness.

If APC does not materially improve a real workflow, record the result and leave it experimental rather than forcing it into normal operation.

## 23. On-demand visual/temporal scope

Browser-page screenshots remain eyeBROWSE. Native Windows capture remains DESKTOPeye.

Implement `screenshot.element` and `screenshot.full_page` if required by the frozen GitHub/debugging/visual-truth tasks. `screenshot.region` may be added with the same generic geometry semantics.

Screencast is conditional on a real temporal workflow. No always-on screenshots/video/OCR are permitted by this candidate. Pixels do not become `e_*` identity.

## 24. Compact capability projection

The model-facing surface should expose the relevant current capability set rather than every profiler/emulation/media method on every turn. Projection is context serialization, not policy: all raw browser capabilities remain reachable through the permanent escape hatches.

A GitHub workflow may prominently expose current context, semantic observation/query/wait, network/download state, relevant named programs, and raw fallback without dumping every unrelated CDP domain into immediate context.

## 25. Mandatory Milestone M — Skill composition

Do not build a giant GitHub mega-Skill. Generic export, download, forms, multi-tab, and debugging procedures remain independent horizontal Skills.

Frozen composition gates:

1. at least one useful task uses **three independently reusable Skills** in one workflow;
2. at least one later task combines procedural Skill knowledge with **two or more actual Eyes/providers** without moving their native semantics into eyeBROWSE.

Preferred three-Skill fixtures include `github + artifact-download + web-debug` or `github + multi-tab + current-page-export`.

## 26. Mandatory Milestone N — structurally different second application

Final measured acceptance requires one second deep application structurally different from GitHub. Prefer authenticated Gmail Web or Google Drive based on live availability at the interactive acceptance boundary.

The second application should pressure virtualization, rich text/search, attachments/files, multi-step application state, and persistent authentication rather than merely replay GitHub-shaped navigation.

All site interpretation remains procedural/provider-level. If second-site pressure reveals a genuinely generic missing browser primitive, record the evidence and add only that generic primitive.

Deterministic second-site fixtures may be used during noninteractive development, but they do not by themselves satisfy the final live second-application gate.

## 27. Mandatory Milestone O — hostile lifecycle/recovery suite

The frozen hostile families are:

- nested GitHub starting URL;
- human tab change between reasoning and action;
- SPA navigation;
- BFCache back/forward;
- prerender activation;
- frame/OOPIF replacement;
- renderer crash/restart;
- frozen tab;
- discarded tab and reactivation;
- stale `e_*` binding;
- ambiguous semantic rebind;
- kernel death/restart;
- MV3 worker restart;
- browser surviving kernel failure;
- popup/opener change;
- OAuth popup where feasible;
- ordinary download;
- blob/authenticated resource;
- very large repository;
- large PR;
- long Actions logs;
- temporarily unavailable provider-native path;
- temporarily unavailable browser-semantic representation;
- route/layout variation;
- ambiguous deictic `this`;
- WebMCP tool-set change after navigation;
- runtime developer-tool disappearance after navigation;
- many-target scale pressure.

Record wrong target, wrong tab, wrong element, wrong repository, wrong file, and wrong PR/run actions. Acceptance target: **zero wrong-object actions** in the frozen suite.

After kernel death, prove exact surviving continuity only where the underlying browser/document identity genuinely survived; otherwise prove explicit new incarnation/stale/ambiguous state.

## 28. Mandatory Milestone P — controlled Skill Plane experiment

### Control

```text
fresh ChatGPT
+ identical eyeBROWSE Build 002 candidate
+ identical Program Host
+ no relevant site/operation Skill
```

### Treatment

```text
fresh ChatGPT
+ identical eyeBROWSE Build 002 candidate
+ identical Program Host
+ installed relevant Skills
```

The treatment receives no hidden browser/provider capability unavailable to control. The experimental difference is procedural expertise.

Use genuinely fresh conversations, not the implementation/orchestrator conversation. Verify actual Skill installation/availability before subjects.

Do not name the expected Skill in ordinary treatment prompts. Include unrelated negative-selection tasks and at least one task that genuinely benefits from multiple Skills.

## 29. Prospective control-versus-treatment task set

Freeze **12 paired ordinary-language tasks** before the first measured subject. The set must contain at least:

- 4 GitHub contextual/deictic tasks, including repository and current-file acquisition;
- 2 GitHub provider-versus-browser truth tasks;
- 2 horizontal browser tasks from export/download/forms/multi-tab;
- 1 debugging task;
- 1 WebMCP task;
- 1 second-site task;
- 1 unrelated negative-selection browser task.

The exact task text, start URLs/state, mutation fixture IDs, and expected source-of-truth classification must be committed with the acceptance harness before subjects begin.

## 30. Prospective Skill Plane success thresholds

These thresholds are frozen before measured subjects.

### Task-success improvement

Treatment must satisfy both:

1. succeed on at least **10 of 12** frozen paired tasks; and
2. succeed on at least **2 more tasks than control**.

A task is successful only when its functional outcome and target/object identity are correct. An incorrect-object action cannot be counted as success.

### Reasoning/model-browser round-trip improvement

Across paired tasks that both conditions complete successfully, treatment must reduce the **median model/browser round trips by at least 25%** relative to control. A zero-denominator case is adjudicated explicitly rather than manufactured into a percentage.

### Wrong-object rate

Treatment and control acceptance candidate must each record wrong-object actions. Build 002 canonical promotion requires **zero wrong-object actions by the treatment** on the frozen 12-task set and **zero false hard semantic rebounds** in the frozen hostile identity set.

### Automatic Skill selection

Across the frozen task set:

- relevant-Skill activation recall must be at least **80%**;
- irrelevant/false-positive Skill activation must occur on no more than **10%** of tasks where that Skill is not relevant;
- the frozen multi-Skill task must activate/use a useful combination without being explicitly told Skill names.

If the actual ChatGPT product exposes activation evidence at a coarser granularity, record the observable event faithfully and do not infer invisible activations.

### Local execution compression

At least one Skill-triggered useful operation must execute **10 or more meaningful eyeBROWSE/Program Host operations within one agent reasoning invocation**. The Build 001 33-operation result remains a regression reference, not a requirement to fabricate a higher count.

### Rediscovery

On treatment tasks whose procedure is explicitly encoded in a relevant Skill, unnecessary repeated rediscovery of the same encoded site mechanics must be lower than control in aggregate. This is a reported supporting metric rather than a separate pass/fail threshold because control may independently know some site mechanics.

### Secondary metrics

Record reasoning turns, eyeBROWSE calls, Program Host operations, full-page reobservations, screenshots, raw-CDP fallbacks, stale-object events, wall time, named-program usage, representation choice, and route/layout robustness. These are measurement artifacts, not permanent runtime telemetry.

## 31. Representation-quality classification

For every preregistered measured task record the expected and actual principal truth source:

```text
browser
provider
network/application
sibling/local
visual
```

Correct source selection is a mandatory adjudication item. A task may use multiple sources, but the principal factual authority must match the question.

## 32. Build 001 regression gate

Before final Build 002 classification, rerun the relevant Build 001 acceptance subset and prove:

- A persistent browser survives kernel death and reattachment;
- B semantic objects/deltas still operate without whole-page rediscovery as the normal loop;
- C exact surviving document/object identity still recovers after kernel death;
- D one Program Host invocation still executes substantial local browser work.

No Build 002 feature may be accepted by weakening an inherited gate.

## 33. External pressure tests

Selected BrowserGym/WebArena/VisualWebArena/WorkArena-family tasks are diagnostic only after internal acceptance is substantially healthy. Include both structured-state-favorable and genuinely visual tasks when practical.

Do not rewrite the frozen Build 002 acceptance criteria after seeing benchmark scores and do not redesign the product solely to climb a leaderboard.

WebDriver BiDi remains a stretch interoperability probe only after primary gates are healthy and only if it yields concrete value without delaying acceptance.

## 34. Pre-interactive implementation boundary

While no safe authenticated interactive Build 002 desktop/runtime exists, source implementation may proceed fully in isolation:

- lifecycle/state code;
- Skills and packages;
- Program Host routines;
- deterministic fixtures;
- WebMCP/runtime-tool fixtures;
- static provider tests;
- unit/integration tests that do not attach to the Campaign 3 browser;
- acceptance harness and preregistration artifacts;
- branch publication.

Do not fake live authenticated browser acceptance. Remaining live gates stay explicitly open.

## 35. Pre-interactive candidate freeze

Before first interactive measured acceptance, bind and record:

- branch;
- commit;
- tree;
- SHA-256 of this candidate specification;
- each Skill package SHA-256;
- Program Host source hashes;
- kernel/extension hashes;
- fixture hashes;
- acceptance harness hash;
- Chrome/CfT version used by the frozen candidate.

The working tree must be clean and the exact commit must be provider-published.

If acceptance reveals a defect, preserve that result, repair prospectively, create a new acceptance-candidate commit, and rerun affected gates.

## 36. Architecture-preservation inspection

Final acceptance explicitly answers YES/NO for whether the candidate created:

- site-specific kernel authority;
- second browser controller;
- Skill-owned identity;
- selector-owned identity;
- permanent action ledger;
- receipt subsystem;
- verification architecture;
- policy/approval architecture;
- second autonomous brain;
- sibling-Eye semantic takeover.

Successful Build 002 requires `NO` for every item.

## 37. Frozen final classifications

The only Build 002 final classifications are:

### DEMONSTRATED

Use only when all mandatory frozen gates pass strongly enough to justify canonical promotion, including the prospective Skill Plane improvement thresholds.

### PARTIAL

Use when meaningful candidate capability is measured but one or more mandatory Build 002 claims remain unearned.

### ARCHITECTURE REDUCTION

Use when the Skill Plane or another major candidate component fails to buy enough measured capability to justify its complexity, but a smaller evidence-supported subset is worth preserving.

### FAILED

Use when the candidate fails its central architecture/capability claim or violates a binding architecture boundary.

Failure is valid evidence.

## 38. Results and canonical promotion

After measurement, create `docs/11-BUILD-002-RESULTS.md` containing actual numbers for the frozen gates, regressions, hostile cases, GitHub G1–G7, horizontal Skills, WebMCP/runtime tools, second site, Skill composition, control/treatment, wrong-object actions, recovery, and any external pressure tests.

Results are development measurement, not product receipts.

Only after measured acceptance may Build 002 update canonical numbered documents and `docs/06-DECISIONS.md`. Do not rewrite Build 001 history to imply Build 002 capability existed earlier. Do not merge/move `main` while Campaign 3 depends on the measured Build 001 head unless the owner explicitly ends or reconstitutes that dependency.

Build 002 success does not authorize Build 003.

## 39. Governing principle

Build the strongest browser operating environment the evidence actually justifies.

Do not make Skills own the browser.  
Do not make eyeBROWSE know GitHub ontology.  
Do not make Playwright own Chrome.  
Do not make runtime action history into memory.  
Do not make debugging into a permanent verifier.  
Do not turn browser capability into a giant immediate tool dump.  
Do not duplicate sibling Eyes.

Instead preserve:

```text
persistent Chrome truth
+ conservative eyeBROWSE correspondence
+ modern lifecycle correctness
+ lazy scalable perception
+ rich provider representations
+ compact model-facing state
+ composable procedural Skills
+ deterministic local programs
+ raw escape hatches
+ fresh frontier-model reasoning
```

Then let measured evidence decide whether this candidate deserves canonical Build 002 status.
