# Answer result

Right or wrong, what the number was, and what your frog does about it.

## Invariants

**Invariant:** on a wrong answer the **correct answer is revealed and the
working is not**. Showing worked partial products would quietly make one
method canonical, which is the thing
[ADR-0002](../../adr/0002-structured-working-out-grid.md) rules out.
**Invariant:** right or wrong is never signalled by colour alone — the mark,
the words, and the movement all say it.
**Invariant:** the movement is stated before it happens, in words, and the frog
then visibly makes that move on the board once this dialog closes. The player
is told what will happen and then watches it happen.
**Invariant:** a wrong answer on the Start log moves nothing and says so. The
Start log is a floor, not a special space — see
[the reference material](../reference/index.md#the-start-log-is-a-floor-not-a-special-space).
**Invariant:** this dialog cannot be dismissed, and its only button hands the
turn on.

## Regions

| Region | Job |
| --- | --- |
| `mark` | The tick or the cross |
| `verdict` | The headline — the sum, or that it was wrong |
| `consequence` | One sentence: what the frog does |
| `chip` | The player chip, showing the move as `pad 3 → 4` |
| `controls` | The hand-on button |

## Anchors

A centred [dialog](shared-components.md#dialog), `ResultDialogWidth` by
`ResultDialogHeight`. `mark` is pinned top-left of the panel's padding box;
`verdict` and `consequence` sit to its right in a column; `chip` below the
mark; `controls` bottom-right.

Both states use the same panel size and the same anchors, so the dialog does not
resize or reflow between a right answer and a wrong one. Two dialogs that jump
about are two dialogs a child reads as two different things happening.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Dialog width | `ResultDialogWidth` | 1100 px |
| Dialog height | `ResultDialogHeight` | 620 px |
| Mark diameter | `ResultMarkSize` | 180 px |
| Mark ring width, wrong state | `ResultMarkRingWidth` | 8 px |
| Mark glyph size | `ResultMarkGlyphSize` | 110 px |
| Verdict size | `ResultVerdictSize` | 76 px |
| Verdict top, from the panel's top edge | `ResultVerdictTop` | 70 px |
| Consequence size | `ResultConsequenceSize` | 48 px |
| Consequence top, from the panel's top edge | `ResultConsequenceTop` | 180 px |
| Consequence column width | `ResultTextWidth` | 760 px |
| Text column left, from the panel's left edge | `ResultTextLeft` | 280 px |
| Chip top, from the panel's top edge | `ResultChipTop` | 340 px |
| Hold before the frog hops | `ResultHopDelay` | 0.2 s |

`mark`, `chip` and `controls` are all `DialogPadding` from the panel's edges —
the shared [Dialog](shared-components.md#dialog)'s own padding, not a constant
of this page's. `ResultMarkRingWidth` and the four rows that place `verdict`,
`consequence` and `chip` are numbers the two committed mockups already draw;
they are on this table so the layout is built from names rather than measured
off the picture.

The frog's hop itself runs over `FrogHopDuration`, which is the
[game board](game-board.md#named-constants)'s constant, not this page's — the
hop happens on that screen, after this dialog has gone.

## Elements

| | **Right** | **Wrong** |
| --- | --- | --- |
| Mark | Filled tick | Outlined cross |
| Verdict | `331 × 41 = 13,571` | `Not this time` |
| Consequence | `Right! Green hops forward one lily pad.` | `331 × 41 = 13,571. Green hops back one lily pad.` |
| Chip | `pad 3 → 4` | `pad 3 → 2` |
| Button | `Blue's turn` | `Blue's turn` |

The chip's move reads `pad <before> → <after>` in all three situations,
including the floor case where nothing moved (`pad 0 → 0`). Neither mockup draws
that one; extending the pattern rather than writing a separate sentence for it
is a presentation call under
[ADR-0001](../../adr/0001-rules-sacred-presentation-ours.md), and the numbers are
the lane positions Core reports either way.

The wrong state leads with *Not this time* rather than with the number, because
the first thing a child reads should not be the size of their mistake. The
correct answer is in the next line, where it teaches instead of stings.

The button is named for **the next player**, not `OK`. On a shared tablet the
useful information at the end of a turn is whose turn it is now, and a button
that says it is a button that passes the device to the right person.

### The three consequence sentences

| Situation | Sentence |
| --- | --- |
| Right | `Right! <Name> hops forward one lily pad.` |
| Wrong, above the Start log | `<Name> hops back one lily pad.` |
| Wrong, on the Start log | `<Name> stays on the Start log.` |

Three, not two. The third is the floor rule, and writing it as its own sentence
is how the layout proves it was thought about rather than left as an
off-by-one.

`<Name>` is the frog's name, with nothing appended: `Green` for a frog still on
its default, `Connor` for one renamed on [game setup](game-setup.md). These
sentences already read the bare colour name rather than `Green frog`, so only
the placeholder's name changed here — see
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310).

## Behaviour

- Entering from [working-out grid](working-out-grid.md) when `Check it` is
  pressed.
- Nothing is decided here. Core has already compared the answer and computed
  the new position; this dialog reads it out.
- The button closes the dialog, waits `ResultHopDelay`, and the frog hops on the
  [game board](game-board.md) over `FrogHopDuration`. Then the next player's
  turn begins.
- If that hop puts a frog on the End log, its chip switches to `Home` and play
  continues with the remaining frogs.
- Hardware back does nothing.

## Mockup

- **Right:** [`mockups/answer-result-right.html`](mockups/answer-result-right.html)
- **Wrong:** [`mockups/answer-result-wrong.html`](mockups/answer-result-wrong.html)

## Open questions

- **What does the button say when there is no next player?** On the hop that
  gets the *last* frog home, the game ends itself
  ([game board](game-board.md)) and there is nobody to hand the device to — so
  the rule above, "named for the next player", has no answer for that one turn.
  The button is still pressed: it is what closes the dialog and starts the hop.
  It currently reads `Game over`, borrowed from
  [game over](game-over.md)'s own words for the screen it now leads to, as a
  placeholder rather than a decision. Connor's to settle — asked in
  [#287](https://github.com/derekwinters/connor-multiplying-frogs/issues/287).

    **This got worse, not better, since #287 was asked.** Since
    [a player has won](player-won.md) landed, this button is no longer the last
    thing a winner reads: the hop it starts now ends in a dialog saying
    `Blue wins!`, and only after *that* does the game over screen appear. So
    `Game over` announces an ending that has not happened yet and that the next
    dialog is about to talk over. Answer #287 with that page in view.
- **Does a right answer get any celebration beyond the hop?** Audio is
  [parked](../future-ideas.md), and a "correct" chime is the most likely parked
  item to be promoted. If it is, this is the screen it plays on.
- **Should the wrong state offer to show the working?** Ruled out by ADR-0002 —
  noted here so it is not re-proposed as a kindness.
