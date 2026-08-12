# 10B — Build 002 Candidate Decision Record

Status: **BRANCH-LOCAL / PROSPECTIVE / NOT CANONICAL**
Date opened: **2026-08-12**
Canonical decision register `docs/06-DECISIONS.md` is intentionally unchanged until measured acceptance earns promotion.

This record captures material candidate deviations/clarifications before acceptance. It is not an action log, receipt store, or runtime history.

## C2-001 — Skill Plane remains procedural, not browser authority

**Evidence:** current OpenAI Skill mechanism and current Chrome DevTools Agentic Skills both demonstrate reusable procedural expertise, while eyeBROWSE already owns browser correspondence.

**Decision:** Skills teach workflows/tool selection/representation choice/named programs. They do not own browser state, identity, selectors, action history, or provider truth.

## C2-002 — Candidate Build 002 pulls lifecycle hardening forward first

**Evidence:** canonical roadmap already names lifecycle hardening as the immediate post-Build-001 phase; Skill continuity depends on correct BFCache/prerender/renderer/frame/document reasoning.

**Decision:** lifecycle correctness remains mandatory and precedes measured Skill claims.

## C2-003 — Runtime isolation is configuration, not a second product

**Evidence:** Build 001 hard-coded `dev` profile/pipe/runtime paths, which would collide with Campaign 3 if a Build 002 binary were launched unchanged.

**Change:** current Build 002 implementation makes profile/runtime/pipe/artifact identity environment-selectable while retaining Build 001 defaults.

**Boundary:** this changes mechanical runtime identity only; it does not add authority/policy architecture.

## C2-004 — Conservative rebinding requires explicit outcomes and incarnation

**Evidence:** Build 001 kept logical IDs but did not implement full destroyed/recreated-node semantic reconciliation.

**Change:** current candidate exposes `exact/rebound/stale/ambiguous`, binding incarnation, deterministic hostile rebinding smoke cases, and abstains on weak/duplicate matches.

**Target:** zero false hard rebounds in the frozen hostile suite.

## C2-005 — Chrome DevTools for agents is mandatory capability pressure, not controller architecture

**Evidence:** official Chrome DevTools for agents 1.0+ combines MCP, CLI, Agentic Skills, live-session attachment, performance, Lighthouse, memory, extension, WebMCP and third-party runtime-tool capability. Current repository package is 1.7.0.

**Decision:** study and match valuable generic capability through direct CDP/eyeBROWSE providers/Program Host/Skills. Reject MCP/CLI as production controller or browser identity owner.

## C2-006 — DevTools capability delta pulls four debugging families into mandatory Build 002

**Evidence:** current DevTools for agents exposes mature performance-trace insight reduction, Lighthouse, heap-snapshot graph analysis, and extension tooling; these materially strengthen the frozen Build 002 debugging claim.

**Change:** before acceptance, Build 002 must implement controlled evidence for:

- performance trace + PerformanceTimeline + local reduction;
- memory/heap capture + local analysis;
- Lighthouse/agent-readiness;
- generic extension debugging in an isolated profile.

These are on-demand debugging capabilities, not always-on verification.

## C2-007 — Large diagnostic artifacts are locally reduced

**Evidence:** Google's current Skills explicitly avoid reading huge raw heap snapshots into agent context and current performance tools return reduced insights while allowing raw trace files.

**Decision:** traces, heap snapshots, large reports and large network material remain artifacts. Program Host performs transient local parse/index/reduction. ChatGPT receives compact findings plus artifact handles when useful.

## C2-008 — WebMCP and third-party runtime tools remain capability-detected provider facets

**Evidence:** both remain experimental/evolving in current Chrome tooling; WebMCP uses current page/browser APIs and third-party tools are scoped to the defining page/document.

**Decision:** `webmcp.*` and `runtime_tools.*` are document-scoped provider data. Unsupported is explicit. Returned DOM objects reconcile into existing `e_*` where evidence permits. No invocation-history database.

## C2-009 — DevTools Source/Debugger depth is browser-runtime state; CODEeye retains source engineering truth

**Evidence:** CDP Debugger exposes scripts, source maps, paused call frames, breakpoints and runtime source, but repository/worktree/compiler semantics remain outside browser authority.

**Decision:** typed runtime-debug observation is added only where developer flagship workflows need it. Invasive breakpoint/step/edit breadth stays raw CDP until evidence earns promotion. Cross-Eye workflow uses CODEeye for local source modification/testing.

## C2-010 — PWA tools are deliberately not promoted in Build 002

**Evidence:** Chrome DevTools for agents 1.7 exposes four PWA tools, but no frozen Build 002 acceptance workflow requires OS app installation/launch/uninstall semantics.

**Decision:** retain PWA capability through raw dynamic CDP and defer typed surface. This is not false-support or a claim of irrelevance.

## C2-011 — Browser dialogs are a legitimate generic capability gap

**Evidence:** current DevTools tooling treats dialog handling as a first-class browser operation and recent failures demonstrate dialogs can stall tool workflows.

**Decision:** add generic browser JS-dialog current state plus accept/dismiss semantics. This is browser correctness, not approval architecture.

## C2-012 — Capability Registry must represent absence and failure honestly

**Evidence:** tip-of-tree CDP, WebMCP and experimental provider APIs change quickly; browser schema presence does not guarantee a provider call will succeed in every runtime state.

**Decision:** tests cover present/absent/experimental/schema evolution/provider failure/fallback. Unsupported is returned explicitly; eyeBROWSE does not badly emulate unsupported experimental methods and claim support.

## C2-013 — Current DevTools memory UX is translated, not copied

**Evidence:** Chrome DevTools MCP maintains loaded heap snapshot worker state and offers twelve heap-query tools.

**Decision:** eyeBROWSE captures heap material as artifacts and exposes compact browser-side counters/sampling where useful; heap graph indexing/query/comparison lives transiently in Program Host. No permanent heap-snapshot object registry is added to Browser World Graph.

## C2-014 — DevTools capability projection validates compact exposure

**Evidence:** current DevTools for agents groups feature categories and offers a slim mode; smaller surfaces reduce irrelevant tool exposure.

**Decision:** Build 002 keeps raw CDP reachable but Skills/capability projection expose only relevant typed surfaces per task. This is serialization/cognition ergonomics, not policy or permission gating.

## C2-015 — External DevTools comparison is post-Campaign-3 isolated research

**Evidence:** official tooling can attach to existing authenticated sessions, but Campaign 3 scientific isolation forbids attaching competing controllers to its browser.

**Decision:** after Campaign 3 and in a disposable Build 002 browser, compare DevTools-for-agents alone vs eyeBROWSE no-Skill vs eyeBROWSE+Skills. No production dependency follows from the comparison.
