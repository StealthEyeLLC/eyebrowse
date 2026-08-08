# tests

Build 001 tests focus on deterministic browser mechanics rather than broad end-to-end coverage.

Initial groups:

```text
fixtures/      deterministic local hostile pages
integration/   Chrome/CDP/world-graph integration
identity/      exact/rebound/stale/ambiguous object behavior
reconnect/     deliberate kernel-death recovery tests
```

Required hostile fixtures include frames/OOPIFs, workers/service workers, popups, forms, DOM replacement, list reorder, duplicate labels, virtualization, mutation storms, GraphQL/network activity, long-running state changes, and downloads.

Tests are engineering quality machinery, not a runtime verification architecture or receipt system.

See `../docs/02-BUILD-001-SLICE.md`.
