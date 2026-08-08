# program-host

The Agent Program Host is the local multi-action execution plane.

Build 001 target runtime:

```text
Node 24 LTS
TypeScript/JavaScript
```

It connects to the persistent .NET kernel API and lets one reasoning turn execute loops, branches, queries, waits, network inspection, and many browser actions locally.

It does **not** own Chrome or canonical eyebrowse state. Killing the Program Host must leave Chrome and the kernel intact.

Milestone D requires one Program Host invocation to execute at least 20 meaningful browser operations.

See `../docs/02-BUILD-001-SLICE.md`.
