# Build 002 Skill Plane controlled experiment preregistration

Status: **PROSPECTIVE / FROZEN BEFORE FIRST MEASURED SUBJECT / NOT RESULTS**

`task-set.json` is the machine-readable authority for the 12 paired tasks required by `docs/10-BUILD-002-SKILL-PLANE.md` Milestone P. Task wording, starting URL/state, mutation fixture identifier, source-of-truth classification, relevant Skill expectations, and success criteria are frozen before any measured control or treatment subject is launched.

## Conditions

- **Control:** fresh ChatGPT + the exact same frozen eyeBROWSE candidate + the exact same Program Host + no Build 002 site/operation Skills.
- **Treatment:** fresh ChatGPT + the exact same frozen eyeBROWSE candidate + the exact same Program Host + the frozen Build 002 Skill set.
- No browser/provider capability may differ between conditions.
- No subject receives hidden expected answers or Skill names in ordinary task prompts.
- A subject conversation is used for one frozen task only, then discarded.

## Recorded per subject

Each subject result must record task ID, condition, success, functional outcome evidence, start/end target and document identity where applicable, wrong-object actions, model/browser round trips, observable Skill activations, maximum meaningful eyeBROWSE/Program Host operations inside one reasoning invocation, and any abstention/unsupported result.

`tests/build002/score-skill-plane-experiment.mjs` validates and scores a completed result set. It does not launch subjects and cannot substitute synthetic records for genuine fresh ChatGPT runs.

## No post-hoc mutation

If a start URL or external object becomes unavailable before the first subject, amend the task set prospectively, create a new candidate commit, and restart the experiment from zero. After the first measured subject, do not change tasks, scoring, or thresholds in response to observed outcomes.