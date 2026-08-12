---
name: webmcp
description: Discover and operate page-advertised WebMCP tools through eyeBROWSE when the current Chrome/document exposes them. Use when a page offers structured model-context tools or when a task can be completed more deterministically through page-native search, filter, add-item, submit, or similar WebMCP operations. The Skill should inspect live schemas, choose tools from ordinary user intent, execute them, and remain robust when the tool set changes after navigation.
---

# WebMCP

Call `webmcp.list` on the resolved current target. Treat advertised definitions as document-scoped provider data, not browser truth.

Inspect a tool schema before executing when arguments are not obvious. Select the tool that directly represents the user’s intent; do not invoke tools merely because they are available.

Execute with `webmcp.execute` and reason from the structured result. When results refer to DOM/runtime objects, prefer correlation to existing `e_*` concepts where eyeBROWSE supports it.

After navigation/document change, rediscover the tool set. A tool from the previous document does not magically persist.

Fall back to generic browser/application representations when WebMCP is absent or insufficient. Raw browser capability remains available.
