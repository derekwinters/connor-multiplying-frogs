# Multiplying Frogs

Multiplying Frogs is a digital port of a multiplication board game from
Connor's math class. It is pass-and-play: two to four players share one tablet
and take turns. The board is four lanes of lily pads running from a Start log
to an End log, with one frog per player in their own lane, and a turn is roll,
draw, answer, move — the roll picks which of the three piles the card comes
from, the card is a multiplication problem, and the problem is worked out on
screen in a grid built for long multiplication. Reaching the End log is what
winning means. Derek is building it with his son Connor, and Connor decides the
rules, because it is his game.

## What this documentation is

This site is the **design contract**. It is the single description of what the
game is and how it is built — not a pile of notes, not a changelog, and not
somewhere to think out loud. Behaviour that is not written down here is
behaviour nobody has decided yet, and the right response to finding a gap is to
ask rather than to fill it in.

!!! warning "Code that disagrees with the docs is a bug in one of them"

    Which one is a **decision, not an assumption**. Whoever finds the code and
    a page here saying different things stops and says so — in a comment on the
    issue — rather than quietly editing the code to match the page, or the page
    to match the code. A human decides which side moves, and the PR that
    resolves it says which one was treated as authoritative and why.

    The same rule, spelled out for agents, is in `CLAUDE.md` at the repo root
    and in [Agent workflow](engineering/agent-workflow.md).

The other half of the contract is that docs land with the change they describe.
A page here is meant to be true of the build you are holding, so a behaviour
change and its doc update are the same PR — never a follow-up issue.

### Who each section is for

The site has two audiences, and every section belongs to one of them. The
**Introduction** and **Specs** are for anyone who wants to know what the game
is; **Decisions**, **Engineering**, and **Tools** are for whoever is building
it.

| Section | It answers | Read it when |
| --- | --- | --- |
| [Introduction](intro/index.md) | What is this game, and what is it trying to be? | You are new here, or explaining the game to someone |
| [Specs](specs/index.md) | Exactly how does it behave? | You are about to build, test, or argue about a rule |
| [Decisions](adr/index.md) | Why is it built that way? | Something looks odd and you want the trade-off behind it |
| [Engineering](engineering/index.md) | How does the work get done? | You are writing code, tests, CI, or a PR |
| [Tools](tools/index.md) | What automation runs this repo? | You are working on the skills or the issue pipeline |

The split that matters most is **Specs versus Decisions**. A spec page says what
is true now and is edited whenever that changes; an ADR says what was chosen
once, at a point in time, and is never rewritten — it is superseded by a later
ADR that says so.

## Where decisions live

**Decisions are made in GitHub.** An issue body, a comment thread, a PR review —
that is where a thing gets argued out and settled, and that record keeps its
dates, its alternatives, and the reasons the losing options lost.

**This site is the distillation.** It is the settled shape of those decisions,
written up so nobody has to reconstruct the current rules from a fifty-comment
thread. Two things follow:

- **When this site and GitHub disagree, GitHub wins.** A comment from Derek
  three days ago beats a paragraph here from three weeks ago, and the fix is to
  update the page in the PR where you noticed it.
- **A decision that only exists on this site was never made.** If it was not
  agreed in an issue, write the issue first.

**`#NN` points at the deciding issue.** When a page here says a rule was
settled, it links the issue that settled it — like
[issue #7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7),
the design conversation the four
[architectural decisions](adr/index.md) came out of. The number is not
decoration: it is how you get from "the rule is X" to "here is the conversation
where X won", which is the only thing that tells you whether X is still right.

What this site deliberately does **not** record is live state — no milestone
lists, no issue counts, no "we are currently working on…". That is queried from
GitHub, because a page that mixes conventions with state is a page that is
always slightly out of date.

## Where to start

- **Curious about the game** — [Vision](intro/vision.md), then
  [How to play](intro/how-to-play.md).
- **About to build something** — `CLAUDE.md` at the repo root first, then
  [Agent workflow](engineering/agent-workflow.md) and
  [Conventions](intro/conventions.md).
- **Looking for a specific rule** — [Specs](specs/index.md), and the
  [glossary](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CONTEXT.md)
  if a word is doing more work than you expected.

The [repo](https://github.com/derekwinters/connor-multiplying-frogs) is public,
and its README covers building the game and the site locally.
