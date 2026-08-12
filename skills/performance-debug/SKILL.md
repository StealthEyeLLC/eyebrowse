---
name: performance-debug
description: Diagnose browser performance through eyeBROWSE using current metrics and PerformanceTimeline first, then bounded on-demand tracing, local Program Host trace reduction, and temporary emulation when deeper evidence is required. Use for slow-page, LCP/layout-shift/long-task, CPU or network throttling, rendering, navigation-performance, or trace-analysis requests without dumping raw traces into model context.
---

# Performance debugging

Begin with `performance.metrics` and capability-detected `performance.timeline.enable/list` when structured events answer the question.

For deeper evidence, use `developer.capture-performance-profile`. It records a bounded `performance.trace`, preserves the raw trace as an artifact, reduces the trace locally, and returns compact longest-event/category/navigation findings plus current metrics/timeline state.

Use typed `emulate.*` only for controlled viewport, CPU, network, geolocation, media, locale, or timezone experiments. Always call `emulate.reset` when the program finishes, including failure paths.

Do not record traces continuously. Tie findings to the current `t_*`/`d_*`; renderer changes do not automatically mean conceptual document death.
