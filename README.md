# eyebrowse

**eyebrowse** is an AI-native browser operating environment for StealthEyeLLC.

The project is not intended to be a conventional browser-test framework or a screenshot-driven computer-use wrapper. Its goal is to expose the modern web to an AI agent as a persistent, programmable, event-driven world with richer information than a human receives from pixels alone.

The canonical target is:

> Persistent stock Chrome + direct dynamic CDP + lifecycle-correct browser world graph + persistent logical object identities + compact semantic observations + cursor/delta events + browser-side waits + programmable multi-action execution + deep JavaScript/network/storage access + MV3 document instrumentation + Windows-native fallback + visual/temporal understanding.

## Build 1 is the four-milestone slice

The **first implementation build is Build 001: Browser Kernel Slice**. It is not a throwaway prototype. It establishes the permanent architectural spine and has four milestone outcomes:

1. **Milestone A — Persistent browser:** Chrome runs independently of the eyebrowse kernel and remains alive when the kernel exits.
2. **Milestone B — Persistent agent objects:** the agent observes compact semantic state and addresses useful page objects with logical IDs such as `e_42` rather than rediscovering the whole page after every action.
3. **Milestone C — Recovery continuity:** kill the eyebrowse kernel while Chrome remains alive, restart it, reattach, reconstruct the browser world, and recover surviving document/object identities where the underlying document survived.
4. **Milestone D — Programmable browser:** one Agent Program Host invocation can execute a substantial multi-step browser workflow locally using eyebrowse queries, actions, waits, network state, and persistent object references.

Build 001 is specified in [`docs/02-BUILD-001-SLICE.md`](docs/02-BUILD-001-SLICE.md).

## Canonical documentation

The numbered documents in `docs/` are the canonical project specification in reading order:

- [`00-CHARTER.md`](docs/00-CHARTER.md) — objective, exhaustive architectural constraints, project principles, authority.
- [`01-ARCHITECTURE.md`](docs/01-ARCHITECTURE.md) — end-state technical architecture.
- [`02-BUILD-001-SLICE.md`](docs/02-BUILD-001-SLICE.md) — first build, four milestones, implementation order, gates, acceptance criteria.
- [`03-PLATFORM-STEALTHEYELLC.md`](docs/03-PLATFORM-STEALTHEYELLC.md) — target-laptop inventory and deployment assumptions.
- [`04-ROADMAP.md`](docs/04-ROADMAP.md) — development sequence after the first slice.
- [`05-RESEARCH-BASELINE.md`](docs/05-RESEARCH-BASELINE.md) — state-of-the-art findings that materially influence the architecture.
- [`06-DECISIONS.md`](docs/06-DECISIONS.md) — frozen architectural decisions and intentionally deferred choices.
- [`AUTHORITY.md`](docs/AUTHORITY.md) — repository/project operating authority.

If implementation reveals a canonical architectural assumption to be wrong, update the relevant canonical document rather than allowing a contradictory second specification to grow alongside it.

## Initial repository shape

```text
eyebrowse/
├─ README.md
├─ docs/
├─ src/
├─ extension/
├─ program-host/
├─ tests/
└─ experiments/
```

The first code should stay compact. Split projects/packages only when a real implementation boundary earns the complexity.

## Immediate implementation target

The first executable vertical slice is:

```text
persistent Chrome
  → direct browser-level CDP
  → target/frame/document graph
  → DOM/DOMSnapshot/AX/APC semantic surface
  → logical element IDs
  → MV3 document-side identity
  → semantic deltas
  → event-driven waits
  → raw actions/JS/network
  → kernel death/reconnect
  → Agent Program Host
```

That is the shortest path to proving whether eyebrowse is fundamentally different from ordinary browser automation.
