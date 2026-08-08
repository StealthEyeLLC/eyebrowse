# Repository and Project Authority

Status: **Canonical operational authority**  
Repository: `StealthEyeLLC/eyebrowse`

## Owner instruction

The owner has provided a GitHub application with full repository administration authority and explicitly instructed ChatGPT to use that GitHub authority aggressively for eyebrowse work.

For this repository, the connected GitHub application has been verified to have:

- `admin: true`
- `maintain: true`
- `push: true`
- `pull: true`
- `triage: true`

The repository currently uses `main` as its default branch.

## Intended use of authority

For owner-directed eyebrowse work, ChatGPT may use the connected GitHub integration directly to perform repository operations required to advance the project, including when useful:

- create and update canonical files;
- create implementation files;
- create branches;
- create and update issues;
- create pull requests;
- inspect commits and diffs;
- manage ordinary repository workflow state;
- inspect and operate GitHub Actions capabilities exposed by the connector;
- organize project documentation and implementation structure.

Repository workflow should be chosen for engineering usefulness rather than ceremony. Direct commits to `main` are acceptable for explicit owner-requested initialization/canonicalization work such as this initial repository setup. Branches and pull requests should be used when they materially improve parallel work, reviewability, experimentation, or recovery—not as a mandatory approval ritual.

## Authority and the project constraints

GitHub workflow must not introduce project-specific permission hierarchies, confirmation gates, approval theater, verification pipelines, receipt systems, or audit machinery that contradict `00-CHARTER.md`.

Git itself naturally records repository history because version control requires commits. That intrinsic history is not an eyebrowse execution-receipt subsystem and must not be expanded into one for browser actions.

## Canonical-document authority

The numbered documents in `docs/` are the canonical technical specification. Experimental notes, issues, branches, and implementation discoveries may propose changes, but canonical changes should be reflected back into those documents so the project does not accumulate multiple contradictory specifications.
