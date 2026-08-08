# Engineering

How the game gets built. Each page here expands one of the non-negotiable rules
in `CLAUDE.md` — the root file is the summary you read every session, and these
are the details you come back for.

- **[Tech stack](tech-stack.md)** — Unity, the engine-free Core assembly, and
  the naming conventions that keep the split honest.
- **[Testing](testing.md)** — strict TDD, what gets tested in plain NUnit
  versus Unity EditMode, and why the fast suite has to stay fast.
- **[Versioning](versioning.md)** — Conventional Commits, release-please, and
  `/VERSION` as the single source of the app's version.
- **[CI/CD](ci-cd.md)** — every workflow, what it gates, and what to do when it
  goes red.
- **[Agent workflow](agent-workflow.md)** — how an issue becomes a merged PR,
  including what belongs in a PR body.
- **[Issue pipeline](issue-pipeline.md)** — the label state machine, the
  gatekeeper, and the nightly builder.
- **[UI design process](ui-design-process.md)** — wireframe before UI code, and
  what "agreed" means.
- **[Unity serialization](unity-serialization.md)** — the serializer's rules,
  written down so nobody has to guess at them.
