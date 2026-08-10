# Rules of play

**The rules of Multiplying Frogs, stated once, precisely enough to build
against.** How many frogs play, how far one moves, what a wrong answer costs,
what winning is, and how a game finishes.

Nothing on this page was decided by this page. Every rule below is transcribed
from the classroom game or carried over from a decision already recorded
elsewhere, and each one says where it came from. Where a rule is the project's
rather than the classroom game's, it is marked as such — see
[what is ours and what is theirs](#what-is-ours-and-what-is-theirs).

[Reference material](reference/index.md) is the source this page distils: the
board photograph, the rules card, and the notes taken off them. It is
deliberately not normative — *"nothing on this page is a decision"* — and it
stays that way. This page is the decision.

## How to read this page

**Rules are stated as `**Invariant:** …` lines**, the convention the
[UI specs](ui/index.md#2-invariants) already use, so that a rule can be quoted
in a pull request and asserted in a test. Prose around them is framing, not
contract.

**Where this page restates a rule that is also written in a UI spec, this page
is that rule's normative home and the UI spec is the presentation contract for
its screen** — the screen page describes the rule, it does not own it. Neither
is a rival authority to the other, and a disagreement between them is a bug in
one of them.

The page uses the project's words — lane, lily pad, log, frog, pile, card, roll,
turn — as fixed in `CONTEXT.md`. That file is the glossary and is never the
source of a rule.

**A gap here is a stop, not an invitation.** Behaviour this page does not
describe is behaviour nobody has decided; the response is a `type:question`
issue, never a sensible-looking guess. See
[a gap is a stop](index.md#a-gap-is-a-stop-not-an-invitation), and
[what this page does not settle](#what-this-page-does-not-settle) for the gaps
that are already known and already tracked.

## The players and the board

**Invariant:** a game is played by **two to four frogs**, one per player, each
in its own lane — so a game has between two and four lanes.

This is **ours, not the classroom game's**: the rules card says
[2–8 players](reference/index.md#the-rules-card-verbatim) and the physical board
has eight lanes. Capping v1 at four is a **recorded rule change**, and
[ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) names it as the worked
example of one. Derek made the call in
[#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7) and
restated it while this page was being written:
*"the mobile game will support 4 players for now"*
([#199](https://github.com/derekwinters/connor-multiplying-frogs/issues/199#issuecomment-5239966336)).
Four is **v1's limit rather than a permanent ceiling** — letting five to eight
play is [parked, not dropped](future-ideas.md#five-to-eight-players). The
same bound is stated for the screen that enforces it in
[game setup](ui/game-setup.md#invariants), and the four colours a frog can be
are fixed in [shared components](ui/shared-components.md#frog-colours).

The reference page and this page are describing two different things and do not
contradict each other: **the reference page describes the board Connor's class
uses, and this page describes the game this app implements.**

**Invariant:** a lane has **nine positions** — the Start log is position 0,
seven lily pads are positions 1–7, and the End log is position 8.

| Position | What it is |
| --- | --- |
| 0 | **Start log.** Every frog begins here. The floor of the lane. |
| 1–7 | **Lily pads.** The ordinary spaces. |
| 8 | **End log.** The goal — landing on it wins. |

Confirmed by Derek in
[#185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185) and
recorded under
[the End log is the winning space](reference/index.md#the-end-log-is-the-winning-space);
the same nine positions are the `LanePositionCount` and `LaneWinningPosition`
constants in [game board](ui/game-board.md#named-constants).

**Invariant:** frogs are **independent**. Each player keeps their own lane for
the whole game; frogs never share a lane, never pass one another, and never
interact. A frog's entire state is the one number saying how far up its own lane
it is, and there is no board state beyond those numbers.
([Frogs are independent](reference/index.md#frogs-are-independent), confirmed by
Derek in
[#170](https://github.com/derekwinters/connor-multiplying-frogs/issues/170);
[game board](ui/game-board.md#invariants))

**Invariant:** turn order is fixed when the game starts and does not change
while it is played. It is the order the frogs were chosen in, and it is visible
before the first roll.
([Game setup](ui/game-setup.md#invariants), whose `Start` "begins the game with
the chosen frogs in badge order")

**Invariant:** the first frog in turn order takes the first turn, with every
frog on its Start log.
([Game board](ui/game-board.md#behaviour): *"every frog on its Start log, frog 1
active"*)

## A turn

**Invariant:** a turn is **roll, draw, answer, move**, in that order, and a turn
draws exactly one card and answers it exactly once.
([The rules card](reference/index.md#the-rules-card-verbatim): *"On your turn
role the Dice then draw from the pile that matches what you roled"*;
[roll and card](ui/roll-and-card.md#invariants), which fixes that there is no
re-roll and no discard)

**Invariant:** there is **one die, of six faces**, and **three piles, of two
faces each** — so each pile is drawn a uniform third of the time.
([What is in the photograph](reference/index.md#what-is-in-the-photograph);
[the pile labels](reference/index.md#the-pile-labels);
[ADR-0002](../adr/0002-structured-working-out-grid.md#there-are-exactly-three-problem-shapes))

**Invariant:** the roll's mapping to piles is **fixed**, and the player never
chooses a pile.

| Roll | Pile | Problem shape | Sample card on the board |
| --- | --- | --- | --- |
| **1 or 2** | Easy pile | 2-digit × 1-digit | `68 × 5` |
| **3 or 4** | Medium pile | 2-digit × 2-digit | `22 × 41` |
| **5 or 6** | Hard pile | 3-digit × 2-digit | `331 × 41` |

The board labels each pile with the two die faces that reach it, and the game
uses the same three names ([roll and card](ui/roll-and-card.md#elements)). The
table is read off the pile labels in the board photograph
([the pile labels](reference/index.md#the-pile-labels)) and confirmed by Derek
in [#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7);
the same table is
[ADR-0002](../adr/0002-structured-working-out-grid.md#there-are-exactly-three-problem-shapes)'s.
The three shapes are the only shapes: `331 × 41` is the confirmed widest problem
in the game.

**Invariant:** the roll **selects the pile and does nothing else**. It is not
how far a frog moves, and it never moves a frog.
([Roll and card](ui/roll-and-card.md#invariants))

**Invariant:** the pile decides the problem's shape and nothing else. **Every
pile is worth the same one lily pad** on a correct answer — there is no bonus
for a harder card, and no points of any kind.
([ADR-0002](../adr/0002-structured-working-out-grid.md#there-are-exactly-three-problem-shapes);
[roll and card](ui/roll-and-card.md#invariants);
[game over](ui/game-over.md#invariants), which rules out a score)

**Invariant:** a card is one multiplication problem, and the player answers it
by entering a **positive whole number**. The answer is correct when that number
equals the product on the card; nothing else the player wrote is looked at.
([Working-out grid](ui/working-out-grid.md#invariants): *"nothing in the grid is
marked. Only the answer row decides whether the frog moves"*, and its keypad has
"no decimal point, no minus";
[ADR-0002](../adr/0002-structured-working-out-grid.md#two-constraints-that-keep-it-from-becoming-a-tutor))

**Invariant:** an **empty answer is not a wrong answer** — a turn is not
resolved until a number has been entered.
([Working-out grid](ui/working-out-grid.md#invariants): `Check it` is disabled
until at least one digit is in the answer row)

The working-out grid itself is the one part of the game the classroom game has
no equivalent of — it is the project's, under
[ADR-0002](../adr/0002-structured-working-out-grid.md), decided by Derek in
[#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7). It
changes no rule of play: it is somewhere to work, and none of it is graded. Its
shape, its carry strips and its growable addition section are the
[working-out grid](ui/working-out-grid.md)'s to specify, not this page's.

## Moving

**Invariant:** a frog moves **at most one position per turn**, and only as the
direct result of the answer just given.

| Outcome | Effect |
| --- | --- |
| Correct | Forward one lily pad |
| Wrong, anywhere above the Start log | Back one lily pad |
| Wrong, on the Start log | Stay |

The first two rows are the rules card —
*"If you answer the question correctly move forward. If wrong move one space
back."* The card gives no quantity for "forward"; that it is **one lily pad**
was confirmed by Derek in
[#170](https://github.com/derekwinters/connor-multiplying-frogs/issues/170) and
is the outcome table under
[the Start log is a floor](reference/index.md#the-start-log-is-a-floor-not-a-special-space).

**Invariant:** the **Start log is a floor, not a special space**. A wrong answer
there leaves the frog where it is — a clamp at the bottom of the lane, not a
rule of its own.
([The Start log is a floor](reference/index.md#the-start-log-is-a-floor-not-a-special-space);
[answer result](ui/answer-result.md#the-three-consequence-sentences), which
states the three outcomes as three sentences)

**Invariant:** the board never moves on its own. Nothing but an answer moves a
frog.
([Game board](ui/game-board.md#invariants))

## Winning, and being home

**Invariant:** a frog wins by **landing on the End log** — position 8 — not by
reaching the last lily pad. From the Start log that takes **at least eight
correct answers**, because each correct answer advances exactly one lily pad.
([The End log is the winning space](reference/index.md#the-end-log-is-the-winning-space),
confirmed by Derek in
[#185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185);
[game over](ui/game-over.md#invariants))

**Invariant:** the **first frog onto its End log wins**, which is where the
classroom rules card stops: *"First one to the end wins!"*
([The rules card](reference/index.md#the-rules-card-verbatim))

A frog that has reached its End log is **home**. Being home is a position and
nothing more: a frog that gets there stays there, and no later frog getting home
changes who won.
([Game board](ui/game-board.md#behaviour): *"a frog that reaches the End log
stays there"*)

## Finishing

The classroom game does not say what happens after the first frog wins. The card
stops at *"First one to the end wins!"*, and what Connor's class does next
[is not recorded and stays unknown](reference/index.md#still-unsettled). The
four rules in this section are therefore **ours** — v1 filling a gap the board
leaves open, recorded in
[where v1 fills a gap](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open)
so that nobody later mistakes them for what the classroom game says.

**Invariant:** *(ours)* **play continues after the first frog reaches the End
log.** The first frog home wins; the other frogs keep taking turns. Derek's
provisional call — *"for now"*.
([Where v1 fills a gap](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open))

**Invariant:** *(ours)* **a frog that is home is skipped in turn order.** Turn
order itself does not change; whose turn comes up next is turn order minus the
frogs already home.
([Game board](ui/game-board.md#behaviour))

**Invariant:** *(ours)* **the game ends by itself once every frog is home.** The
last frog to land on its End log finishes the game and the standings appear,
with nobody choosing to stop it. Derek's call in
[#186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186).
([Where v1 fills a gap](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open);
[game board](ui/game-board.md#behaviour);
[game over](ui/game-over.md#behaviour))

**Invariant:** *(ours)* **a game can also be ended deliberately — by anyone, on
any turn, behind a confirm.** The device cannot tell who is holding it, so
restricting the exit to one player was never enforceable; what protects a game
in progress is that the confirm names the cost before it acts. Derek's call in
[#186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186).
([Who may end a game](ui/end-game-confirm.md#who-may-end-a-game); the route to
it is the [settings dialog](ui/settings-dialog.md))

**Invariant:** ending a game deliberately is **not** losing it. Every frog keeps
the position it had, and the standings are produced from those positions.
([End-game confirm](ui/end-game-confirm.md#behaviour): *"stops the game and
shows the results"*)

**Invariant:** those two are the **only** ways a game ends, and there is **no
third state** in which a finished game sits waiting to be dismissed.

| How a game ends | Who caused it |
| --- | --- |
| Every frog reaches its End log | Nobody — the game ends itself |
| Somebody ends it early, with a confirm | Any player, on any turn |

([There is no third way](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open))

## The standings

**Invariant:** the standings list **every frog that played**. Nobody is left
off, however far they got.
([Game over](ui/game-over.md#invariants))

**Invariant:** the winner is the frog that reached its End log **first**, and
finishers are listed in the order they got home. Frogs that did not finish are
ranked below every finisher, by how many lily pads they made.
([Game over](ui/game-over.md#invariants))

**Invariant:** **there is no score.** The classroom game has an order, not a
score, and inventing one would be inventing a mechanic.
([Game over](ui/game-over.md#invariants))

**Invariant:** a game ended before any frog got home produces standings with **no
winner**. There is always a well-defined answer to "did anyone finish".
([Game over](ui/game-over.md#elements): the headline reads `Game over` rather
than announcing a winner who did not win)

How **tied** non-finishers are ranked is
[game over](ui/game-over.md#open-questions)'s open question — two frogs on the
same lily pad currently share a place number, and whether they should instead be
ordered by turn order is not settled. That question stays that page's to
resolve, and this page does not answer it.

## What is ours and what is theirs

Everything above traces to the classroom game except the rules in this table.
They are the project's own, recorded as such so the line stays visible.

| Rule | What kind | Where it was decided |
| --- | --- | --- |
| A game seats **two to four** frogs, not two to eight | A **rule change** — the card says 2–8 | [ADR-0001](../adr/0001-rules-sacred-presentation-ours.md), [#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7), restated on [#199](https://github.com/derekwinters/connor-multiplying-frogs/issues/199#issuecomment-5239966336) |
| **Play continues** past the first frog home | A **gap-fill** — the card says nothing | [Where v1 fills a gap](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open) — Derek's provisional call |
| A frog that is **home is skipped** in turn order | A **gap-fill**, following from the one above | [Game board](ui/game-board.md#behaviour) |
| A game can be **ended deliberately**, behind a confirm | **Purely ours** — a cardboard game ends when you close the box | [#186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186), [end-game confirm](ui/end-game-confirm.md#who-may-end-a-game) |
| A game **ends itself** once the last frog is home | A **gap-fill** | [#186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186), [where v1 fills a gap](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open) |

The [working-out grid](ui/working-out-grid.md) is also ours
([ADR-0002](../adr/0002-structured-working-out-grid.md)), but it is not in this
table because it changes no rule of play: it is where the multiplication is
done, and nothing in it is graded.

## What this page does not settle

Named here so that nothing below is read as settled by omission.

- **What the classroom game does after the first frog finishes.** Genuinely
  unknown, not chosen — the v1 answer above is a gap-fill, and the question
  stays [open on the reference page](reference/index.md#still-unsettled).
- **How the problems on the cards are generated.** This page fixes the three
  shapes and nothing else. What a card may contain within a shape — the ranges,
  whether carrying is always required, which multipliers the deck avoids — is
  waiting on the full deck being photographed,
  [#171](https://github.com/derekwinters/connor-multiplying-frogs/issues/171).
- **The shape of the working-out grid.** Its columns, rows, carry strips and
  growable addition section belong to
  [working-out grid](ui/working-out-grid.md), which carries six open questions
  of its own. One of them changes the grid in `Core`: whether a carry strip is
  shared or belongs to each addition row,
  [#255](https://github.com/derekwinters/connor-multiplying-frogs/issues/255).
  Whether a grown addition row can be taken away again by backspacing is
  [another](ui/working-out-grid.md#open-questions). None of them is a rule of
  play — nothing in the grid is graded either way.
- **How tied non-finishers are ranked** —
  [game over](ui/game-over.md#open-questions)'s question, above.
- **Starting, saving and resuming a session.** How a game is set up, whether a
  half-finished game survives the app closing, and what the title screen's
  `RESUME` and `NEW` buttons do — including which is emphasised and what
  `RESUME` does with no save — are the
  [title screen](ui/title-screen.md#open-questions)'s and
  [product scope](product-scope.md)'s, not rules of the classroom game. A
  cardboard game starts when you open the box.
- **Anything about how the game looks.** Layout, animation and what is tapped
  are presentation and belong to the [UI specs](ui/index.md), per
  [ADR-0001](../adr/0001-rules-sacred-presentation-ours.md).

## Where these rules come from

| What | Where it was settled |
| --- | --- |
| The rules card, the board, the die, the three piles | [Reference material](reference/index.md) |
| Frogs are independent; the Start log is a floor | [Reference material](reference/index.md#frogs-are-independent), from Connor via [#170](https://github.com/derekwinters/connor-multiplying-frogs/issues/170) |
| The End log is the winning position, and a lane is nine | [#185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185) |
| Play after the first frog home; the two ways a game ends | [#186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186), [reference material](reference/index.md#where-v1-fills-a-gap-the-board-leaves-open) |
| The rules belong to the classroom game | [ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) |
| The three problem shapes, and the working-out grid | [ADR-0002](../adr/0002-structured-working-out-grid.md) |
| Four players, and the shape of a turn | [#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7), restated on [#199](https://github.com/derekwinters/connor-multiplying-frogs/issues/199#issuecomment-5239966336) |
| Turn order, whose turn is first, and skipping a home frog | [Game setup](ui/game-setup.md), [game board](ui/game-board.md) |
| The standings, and that there is no score | [Game over](ui/game-over.md) |

[How to play](../intro/how-to-play.md) is the same rules told to a person
holding the tablet. Where the two disagree, this page is the one the code
follows and the friendly page is the one that gets corrected.
