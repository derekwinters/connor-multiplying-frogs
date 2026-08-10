# Vision

Multiplying Frogs is the multiplication board game from Connor's math class, on
a tablet. Two to four players share one device: roll, draw a card, and answer a
long-multiplication problem to move your frog up its lane. First frog to the End
log wins.

That is the whole game, and it is deliberately the whole game. Everything on
this page is either something that was settled in
[issue #7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7),
the founding design conversation, or something that was deliberately left open
there.

## A port, not a new game

The classroom game already exists. It is cardboard, it has eight frog pawns and
a handwritten rules card, and Connor plays it at school. This project is a port
of that game and nothing more ambitious than that.

So the classroom game is **the authority on every rule** —
[ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) draws the line.
How far a frog moves, what a wrong answer costs, how many players there are,
what winning means: those belong to the board, and changing one needs Connor.
Layout, animation, and how something is tapped are ours to design, because
cardboard never had an opinion about them.

The board itself is photographed and transcribed in
[reference material](../specs/reference/index.md), so any claim about the game
can be checked against the artifact rather than against somebody's memory of it.

## Who it is for

**Connor first, kids like him second.** He is eight, he already knows this game,
and he breaks ties on taste. Designing for a hypothetical median eight-year-old
instead is how the game quietly stops being his — which would remove the reason
for building it.

The practical version of that rule: when something is a matter of taste, the
answer is to ask Connor, not to reason it out. When something is a rule of the
classroom game, the answer is to look at the board.

## What it feels like to play

**A slow, thinky game.** The rules card says 15–45 minutes, and that is right,
because the cards are multi-digit long multiplication — `331 × 41`, not
`7 × 8`. A turn is two to five minutes of actual arithmetic, done properly, on
screen.

This is the single most important thing to know before designing anything for
it. **It is not a snappy game and must not be built like one.** Nothing in the
classroom game rewards speed and nothing here should either; the pace of the
game is the pace of a child working out a three-digit multiplication. A screen
that hurries the player is a screen that has misunderstood the game.

The other half of the feel is that a turn asks you for exactly one thing: the
answer. You do not choose which problem you get and you do not choose where to
move — the roll decides which pile your card comes from, and a right answer
moves you along. Whether the problem in front of you is an easy one is luck, and
that is how the classroom game works.

The rules themselves are on [how to play](how-to-play.md), and the edges of
what v1 includes are in [product scope](../specs/product-scope.md). This page
does not restate either — there should only ever be one copy of a rule to keep
true.

## Two places v1 differs from the classroom game

Both are Derek's calls, both are permitted by
[ADR-0001](../adr/0001-rules-sacred-presentation-ours.md), and both are written
here rather than left to be discovered. They are worth telling Connor about
rather than presenting as already done.

**Four players, where the card says 2–8.** The physical board has eight lanes
and the box has eight frogs. v1 caps at four, because two-to-five-minute turns
and one shared screen means eight players spend half an hour waiting. At a table
that is fine — everyone can see the board and think at the same time. One device
takes that away. Five to eight players is
[parked, not dropped](../specs/future-ideas.md#five-to-eight-players).

**A working-out grid, which the classroom game has no equivalent of.** At school
the working happens on paper next to the board. On a tablet there is no paper,
so the game provides a structured grid with carry boxes to do the long
multiplication in —
[ADR-0002](../adr/0002-structured-working-out-grid.md). It is a workspace and
nothing else: nothing written in it is marked, and only the answer moves a frog.

## What this game is not

**Nothing multiplies on screen.** The frog is a playing piece, there is one per
player, and the title stays a pun. Frogs that visibly multiply on a correct
answer was proposed and declined by Connor; it is
[parked](../specs/future-ideas.md#frogs-that-visibly-multiply).

**No computer frogs.** Every frog belongs to a person in the room. The intended
direction is toward *more* real people playing together, not a synthetic
opponent, and the cost of that is real and accepted: Connor cannot play alone.
[Parked](../specs/future-ideas.md#computer-frogs).

**No accounts, no ads, no purchases, no network.** The game does not know who is
playing, does not cost anything, and does not talk to the internet — v1 ships
with no network permission at all, and
[ADR-0003](../adr/0003-network-boundary.md) says why the internet is out
permanently. These are not features waiting to be traded away in a later
version.

And four things that are simply out of v1, each parked rather than dropped:

| Not in v1 | Where it went |
| --- | --- |
| Sound and music | [Parked](../specs/future-ideas.md#sound-and-music) |
| Any arithmetic but multiplication | [Parked](../specs/future-ideas.md#other-kinds-of-arithmetic) |
| More than one board | [Parked](../specs/future-ideas.md#more-than-one-board) |
| An in-app tutorial | [Parked](../specs/future-ideas.md#an-in-app-tutorial) |

"Parked" means exactly what [future ideas](../specs/future-ideas.md) says it
means: the idea is written down with the reason it is not being built, and it
stays out of scope until somebody deliberately promotes it. It is not a no.

## What it looks like is not decided yet

There is no art direction on this page, and that is deliberate rather than an
omission. The founding conversation ticked six of its seven boxes and left this
one open on purpose — writing a line about the look to satisfy a checklist would
be inventing a decision nobody made.

What was decided is how to keep the question cheap to answer later: placeholder
shapes for now, every placeholder dimension a named constant, so a later
re-skin is a change of values rather than a rebuild. Art direction gets its own
issue, and Connor gets to have opinions in it.

## Where all of this was settled

Nothing on this page was decided here. It is a distillation, and every claim
should be walkable back to the conversation that settled it:

| What | Settled in |
| --- | --- |
| The pitch, the audience, the pace, and what is out of v1 | [#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7) |
| The rules the photograph could not settle, answered by Connor | [#170](https://github.com/derekwinters/connor-multiplying-frogs/issues/170) |
| Rules sacred, presentation ours | [ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) |
| The working-out grid | [ADR-0002](../adr/0002-structured-working-out-grid.md) |
| The network boundary | [ADR-0003](../adr/0003-network-boundary.md) |
| The classroom game itself | [Reference material](../specs/reference/index.md) |

If this page and one of those disagree, the source wins, and the fix is to
correct this page in the pull request where you noticed it.
