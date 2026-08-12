# Representation and identity reference

## Authority by question

| Question | Principal truth |
|---|---|
| What does the current page look like? | eyeBROWSE browser/visual state |
| What files changed in this PR? | GitHub provider/diff truth |
| What request produced this UI state? | eyeBROWSE network/application state |
| What source exists locally? | CODEeye |
| What process owns this listener? | SHELLeye |
| What native dialog is blocking Chrome? | DESKTOPeye |

## Compact operating loop

`context.current` → targeted `observe.surface` → `query.find` → action/program → `wait.*` → `observe.delta`.

Use a new surface when document identity changes or compact state is insufficient.

## Current generic Build 002 surfaces

Context/targets: `context.current`, `target.list`, `target.activate`, `target.close`, `target.cognition`, `target.demote`, `lifecycle.status`.

Semantic: `observe.surface`, `observe.delta`, `query.find`, `inspect.element`, `identity.resolve`.

Interaction: click/fill/type/key/scroll plus hover, double/context click, focus, select, check/uncheck, file upload.

Attention: `wait.until`, `wait.any`, `wait.all`, `wait.sequence`, `wait.quiet_for`.

Data/debug: bounded network, console, exceptions, downloads, artifacts, performance metrics.

Page-native: `webmcp.*`, `runtime_tools.*`.

Escape hatch: target-aware `cdp.send`; bounded `cdp.subscribe` + `cdp.next` + `cdp.unsubscribe`.
