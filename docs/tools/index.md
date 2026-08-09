# Tools

The Claude skills, agents, and automation this repo runs on. Everything here is
part of the repo, versioned with it, and reviewable like any other code.

This section fills in as the tooling lands during `v0.0.1`:

- The development agent under `.claude/agents/`, and the skills it can reach
  for — `dw-run-tests`, `ci-watch`, `scaffold-core`, `release-flow`, and the rest.
- The issue-pipeline skills — `pipeline-gatekeeper`, `pipeline-analysis`,
  `triage-issue`, `pipeline-dev`, `pipeline-reconcile`, and
  `pipeline-dashboard`.
- Where each skill came from, and the rule that this repo's copies are
  **isolated** — they are not synced from another repo, so a fix made here
  stays here and a fix made elsewhere does not silently arrive.

For how the pipeline behaves as a system rather than as a set of files, see
[Issue pipeline](../engineering/issue-pipeline.md).
