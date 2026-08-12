---
name: extension-debug
description: Inspect and diagnose generic Chrome extension behavior through eyeBROWSE capability-detected Extensions, Target, Runtime, console, exception, and storage surfaces. Use when an unpacked extension fails to load, its service worker or content script errors, storage/action behavior is wrong, or the eyeBROWSE MV3 bridge needs controlled debugging in an isolated Build 002 profile.
---

# Extension debugging

Use `extension.list` first and confirm the running Chrome advertises the required `Extensions.*` capability. Unsupported must remain explicit.

In a disposable/isolated profile, use `extension.load_unpacked`, `extension.storage`, `extension.trigger_action`, and `extension.uninstall` only as the debugging workflow requires. Chrome remains extension-state authority.

Correlate the extension ID with extension/service-worker targets from `target.list`, then use bounded console/exception state and runtime debugger scripts for the relevant target. A disappearing MV3 worker is normal lifecycle evidence, not automatic extension death.

Do not persist extension state in the Skill and do not introduce extension-specific Browser World Graph ontology.
