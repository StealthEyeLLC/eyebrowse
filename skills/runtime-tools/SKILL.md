---
name: runtime-tools
description: Discover and execute document-scoped developer tools advertised by a web page or framework through Chrome’s current third-party runtime-tool discovery pattern. Use for debugging or application tasks where the page exposes typed JSON-schema tools at runtime, including cases where a tool returns a DOM element that should be correlated back to the existing eyeBROWSE semantic identity instead of creating a parallel selector or framework-object identity system.
---

# Runtime page tools

Call `runtime_tools.list` on the exact target/document. Treat tool groups as current provider capability scoped to that document.

Use `runtime_tools.inspect` when argument shape or group ambiguity matters, then `runtime_tools.execute` with validated JSON input.

If a result is a DOM node, use the eyeBROWSE-correlated `e_*` identity returned by the provider bridge when available. Do not keep a separate framework selector identity.

Rediscover after navigation. If a tool disappears, report provider unavailability and fall back to generic Runtime/DOM/semantic debugging rather than pretending the old tool remains live.
