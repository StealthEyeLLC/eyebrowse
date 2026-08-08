# extension

Browser extension code lives here.

Build 001 creates exactly one Manifest V3 extension:

```text
extension/agent-bridge/
```

Its first-build reason for existence is document-start identity and compact state instrumentation, especially NodeSerial/logical `e_*` bindings required for Milestone C controller-death recovery.

The extension service worker is not canonical browser state and must be allowed to restart.

See `../docs/02-BUILD-001-SLICE.md`.
