---
name: memory-debug
description: Diagnose browser memory growth and deterministic leaks through eyeBROWSE current heap/DOM counters, on-demand heap-snapshot artifacts, allocation sampling, and local Program Host heap analysis. Use when a page leaks memory, detached DOM or retained objects are suspected, repeated interactions increase heap use, or the user asks for retainers/retaining-path evidence without placing raw heap snapshots in model context.
---

# Memory debugging

Begin with `memory.current` to establish heap usage, document/node counts, and event-listener pressure. Use repeated controlled actions only when the reproduction requires them.

For a leak comparison, prefer the named Program Host routine `developer.analyze-memory-leak`: capture a baseline heap snapshot artifact, exercise the deterministic action repeatedly, collect garbage in the controlled fixture when appropriate, capture the second artifact, and locally compare class/count/self-size/detached-node deltas.

Use the local retaining-path analyzer when a suspicious constructor/class is known. The raw `.heapsnapshot` remains available as an artifact; do not place it into ChatGPT context.

Use allocation sampling for time-bounded allocation pressure when a full snapshot is unnecessary. Stop sampling after the requested interval.

Heap material and parser indexes are transient debugging data, not persistent Browser World Graph state.
