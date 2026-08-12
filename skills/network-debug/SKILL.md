---
name: network-debug
description: Diagnose browser network and application-data behavior through eyeBROWSE bounded request correspondence, headers, timing, initiators, redirect/service-worker/cache state, request/response bodies, body search or streaming, WebSocket/SSE messages, and GraphQL hints. Use when a page request fails, data is missing or slow, a virtualized UI is backed by hidden application data, a WebSocket/SSE flow misbehaves, or source/runtime correlation is needed without packet capture.
---

# Network debugging

Resolve the current `t_*` target first. Start with bounded `network.search` and `network.detail`; do not dump all traffic.

Use request/response headers, timing, redirects, cache/service-worker facts, initiator data, and request body before fetching large response bodies. Use `network.search_body` for a precise query and `network.body.save` for large material that should remain an artifact.

Use `network.messages` for bounded WebSocket/SSE evidence. Treat GraphQL operation classification as a lens over browser traffic, not a site ontology.

Correlate network evidence with console/exceptions/runtime scripts and current semantic state when diagnosing cause. Producer correlation is evidence-qualified, not proof of causality.

Do not add packet capture or a MITM path unless Chrome's own data surfaces demonstrate a material gap.
