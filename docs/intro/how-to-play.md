# How to play

Multiplying Frogs is the multiplication board game from Connor's math class, on
a tablet. Two to four people share one device. On your turn you roll the die,
draw a card from the pile the roll names, work out the multiplication, and your
frog hops one lily pad up its lane if you got it right — or one back if you
didn't. The first frog to reach its End log wins.

That is the whole game. The rest of this page is the same thing explained the
way you would explain it to someone who has just picked the tablet up.

This is the friendly version, not the exact one. The rules spec — every rule
stated precisely, edge case by edge case, for the code to be built against — is
[issue #199](https://github.com/derekwinters/connor-multiplying-frogs/issues/199)
and this page will link to it once it exists. Until then, and whenever the two
disagree, the classroom game wins: it is photographed and transcribed in
[reference material](../specs/reference/index.md), and
[ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) makes it the authority
on every rule.

## Who is playing

Two to four people, sharing one device and taking turns on it. That is
pass-and-play, and it is the only way the game is played — there are no computer
frogs, and nobody plays over the internet.

Everyone gets one frog, and it stays one frog. The title is a pun; nothing
multiplies except the numbers.

**The classroom rules card says 2–8 players, and v1 caps it at four.** That is a
real change to a rule of the classroom game, not a detail of the port, so it is
written down as one — the reasoning is in
[the vision](vision.md#two-places-v1-differs-from-the-classroom-game), and
letting five to eight people play is
[parked, not dropped](../specs/future-ideas.md#five-to-eight-players).

## Your lane

Every frog has a lane of its own, and stays in it for the whole game. A lane is
a **Start log**, then **seven lily pads**, then an **End log** — nine positions
in all. Every frog starts on its Start log, so it takes eight correct answers to
get from one end to the other.

Frogs never share a lane, never pass one another, and never get in each other's
way. There is nothing to plan and nothing you can do to anybody else: how far up
your own lane you are is the whole of your position in the game. What the other
players are doing is only ever something to watch.

## A turn, start to finish

A turn is one player's complete go, and it always goes the same way.

1. **The hand-off.** The device reaches you with the board on screen, saying
   whose turn it is. That is when the board is on screen — for the rest of the
   turn you are looking at your card and your working.

2. **You roll.** One die, six faces. The roll decides which of the three piles
   your card comes from, and it does nothing else. It is not how far you move,
   and it never moves a frog.

3. **You draw a card.** Each pile is labelled with the two die faces that send
   you to it, so each one comes up a third of the time.

    | You rolled | The pile it sends you to | What is on the card |
    | --- | --- | --- |
    | 1 or 2 | The easiest of the three | 2-digit × 1-digit, like `68 × 5` |
    | 3 or 4 | The middle one | 2-digit × 2-digit, like `22 × 41` |
    | 5 or 6 | The hardest | 3-digit × 2-digit, like `331 × 41` |

    You never choose your pile, and there is no way to swap the card once it is
    drawn. Getting an easy one is luck.

4. **You work it out.** The card is proper long multiplication, so the game
   gives you somewhere to do it: a **working-out grid**, with **carry boxes**
   above the columns for holding a digit you have carried, exactly as you would
   on paper. You fill it in with a keypad of digits. There is nothing to switch
   on and no mode to find — the **answer row** is simply the bottom row of the
   grid, so if you can do `68 × 5` in your head you fill in one row and you are
   done, and if you are facing `331 × 41` you work your way up to it.

    Nothing you write in the grid is marked. The carry boxes and the rows above
    the answer are yours to think in, and no one is checking them — the reason
    is in [ADR-0002](../adr/0002-structured-working-out-grid.md).

5. **The game checks your answer.** You type the number yourself; there is
   nothing to pick from and nothing to guess between. The game tells you
   straight away whether it is right, and only the answer row decides that.

    If you got it wrong, it tells you what the answer was. It does not show you
    how to get there — there is more than one right way to lay out a long
    multiplication, and the game does not take a side about which one is yours.

6. **Your frog moves**, and you can see it move. What it does is the next
   section.

7. **You hand the device on**, and the board comes back for the next player.

There is no hurry anywhere in that. The cards are `331 × 41`, not `7 × 8`, so a
turn is a couple of minutes of real arithmetic and the game is built to let you
take them. Nothing in it rewards being fast.

## Getting it right, and getting it wrong

| What happened | What your frog does |
| --- | --- |
| You got it right | Forward one lily pad |
| You got it wrong | Back one lily pad |
| You got it wrong on the Start log | Nothing — it stays where it is |

The Start log is the bottom of the lane rather than anywhere special: there is
simply nowhere further back to go, so a wrong answer there costs you the turn
and no more.

**Every card is worth the same one lily pad.** A card from the hardest pile
moves you exactly as far as one from the easiest, and there is no bonus for the
hard one and no points of any kind. Drawing `68 × 5` instead of `331 × 41` is
pure luck and it is straightforwardly good news. That is how the classroom game
works, and it stays that way.

**The first frog to reach its End log wins**, which is where the classroom rules
card stops: *"First one to the end wins!"*. The rest of the players keep taking
their turns after that, and the game finishes on its own once every frog has
reached its End log. Those last two are v1's calls rather than the classroom
game's — the card never said what happens after the winner, and
[reference material](../specs/reference/index.md#where-v1-fills-a-gap-the-board-leaves-open)
records both as gaps the board left open rather than rules it settled.

## What this page does not cover

**Starting a game and stopping one.** Choosing how many are playing, and how a
session gets ended before everybody is finished, are set out in the layout pages
for [game setup](../specs/ui/game-setup.md),
[the end-game confirm](../specs/ui/end-game-confirm.md) and
[game over](../specs/ui/game-over.md). They are not rules of the classroom game
— a cardboard game starts when you open the box — so they are described where
they were decided rather than restated here.

**What any of it looks like.** Nothing on this page describes the screen,
because how the game is drawn is not what a rule is. That belongs to
[the layout contract](../specs/ui/index.md), and art direction has not been
decided at all yet.

## Where these rules come from

Nothing on this page was decided by this page.

| What | Where it was settled |
| --- | --- |
| The rules card, the board, the piles, the lane | [Reference material](../specs/reference/index.md) |
| Frogs are independent; the Start log is a floor | [Reference material](../specs/reference/index.md#frogs-are-independent), from Connor via [#170](https://github.com/derekwinters/connor-multiplying-frogs/issues/170) |
| The End log is the winning position | [#185](https://github.com/derekwinters/connor-multiplying-frogs/issues/185) |
| Play after the first frog finishes; how a game ends | [#186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186) |
| The rules belong to the classroom game | [ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) |
| The working-out grid and the three card shapes | [ADR-0002](../adr/0002-structured-working-out-grid.md) |
| Four players, keypad entry, and the shape of a turn | [#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7) |

If this page and one of those disagree, the source wins, and the fix is to
correct this page in the pull request where you noticed it.
