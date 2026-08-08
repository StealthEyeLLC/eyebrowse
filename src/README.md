# src

Production .NET code lives here.

Build 001 begins with a compact set of projects:

```text
AgentBrowser.Kernel
AgentBrowser.Cdp
AgentBrowser.State
AgentBrowser.Actions
AgentBrowser.Network
AgentBrowser.Artifacts
AgentBrowser.Cli
```

Do not create every end-state project immediately. Split responsibilities only when the implementation boundary is real.

Canonical implementation order and acceptance criteria: `../docs/02-BUILD-001-SLICE.md`.
