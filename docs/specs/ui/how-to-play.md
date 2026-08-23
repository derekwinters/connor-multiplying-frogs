# How to play

What the `How to play` button on the [settings dialog](settings-dialog.md)
opens. Five pages that say how a turn goes, for somebody who has just picked
the tablet up and has never played.

## Invariants

**Invariant:** this screen changes nothing about the game. Opening it, paging
through it, and leaving it return the board exactly as it was — same turn, same
positions, same enabled `Roll`.
**Invariant:** it is a **screen, not a dialog**. It replaces what is on screen
rather than covering it. This is not a preference: it is opened from inside the
settings dialog, and the shared
[dialog](shared-components.md#dialog) component's `Stacked` state says *"does
not happen. A dialog never opens over another dialog."*
**Invariant:** nothing on this screen scrolls. It is `HowToPlayPageCount` pages
reached with `Back` and `Next`, and a page that does not fit is a page whose
copy is too long, not a reason to add a scroll view.
**Invariant:** nothing here teaches an algorithm. Page 3 says where the answer
goes and that nothing else is marked; it does not show how to do a long
multiplication, or lay one out in any particular way. That is
[ADR-0002](../../adr/0002-structured-working-out-grid.md)'s constraint and this
screen does not get to relax it because it is the page about the grid.
**Invariant:** the pictures are drawn in the game's own vocabulary and its real
colours — [`PondWater`, `LilyPadGreen`, `LogBrown`](game-board.md#colours) and
the four [frog colours](shared-components.md#frog-colours) — at a smaller
scale. A diagram that does not look like the board is a diagram of a different
game.
**Invariant:** every page has both buttons, always, in the same two places.
Neither is ever hidden or disabled — on the first page `Back` leaves the
screen, and on the last `Next` reads `Done` and does the same. A control that
disappears on one page of five is a control a child stops trusting.

## Regions

| Region | Job |
| --- | --- |
| `heading` | Which page this is, in two or three words |
| `picture` | The drawing — the point of the page |
| `words` | What the picture means, in a few short paragraphs |
| `progress` | Which of the five pages you are on |
| `controls` | `Back` and `Next` |

`picture` comes first and `words` second, left to right, because the picture is
what an eight-year-old reads first and the words are the caption.

## Anchors

- `heading` is pinned to the top-left safe area corner, its line box
  `HowToPlayHeadingLineBox` tall.
- `picture` and `words` are a **single row** filling everything between the
  heading and the controls: `picture` pinned to the left safe margin,
  `HowToPlayPictureWidth` wide; `words` `HowToPlayColumnGap` to its right,
  running to the right safe margin.
- `words` is top-aligned with `picture`, not centred against it. Pages have
  different amounts to say, and text that floats to a different height on every
  page is text that has to be re-found five times.
- `controls` is a single row along the bottom: `Back` at the left safe margin,
  `Next` at the right safe margin, both at the shared `ButtonHeight`, their
  bottom edges `SafeMargin` up from the bottom safe area. This is the shared
  [dialog](shared-components.md#dialog)'s button placement — primary on the
  right — applied to a screen.
- `progress` is centred horizontally across the full canvas and vertically on
  the `controls` row, so the dots sit between the two buttons rather than under
  them.
- On a screen that is not 16:10, everything keeps its distance from the **safe
  area** and `picture` keeps its aspect by losing height, not width. The lane
  drawings inside it are anchored to its top-left inset.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin, every screen edge | [`SafeMargin`](title-screen.md#named-constants) | 48 px |
| Page heading size | `HowToPlayHeadingSize` | 72 px |
| Page heading line box | `HowToPlayHeadingLineBox` | 88 px |
| Gap below the heading | `HowToPlayHeadingGap` | 48 px |
| Picture width | `HowToPlayPictureWidth` | 1104 px |
| Picture height | `HowToPlayPictureHeight` | 808 px |
| Picture corner radius | [`DialogRadius`](shared-components.md#dialog) | 32 px |
| Inset inside the picture | `HowToPlayPictureInset` | 56 px |
| Gap between `picture` and `words` | `HowToPlayColumnGap` | 64 px |
| Words column width | `HowToPlayWordsWidth` | 656 px |
| Body text size | `HowToPlayBodySize` | 40 px |
| Body line height, as a ratio | `HowToPlayBodyLineHeight` | 1.35 |
| Gap between paragraphs | `HowToPlayParagraphGap` | 32 px |
| Gap above the controls row | `HowToPlayControlsGap` | 48 px |
| Page dot diameter | `HowToPlayDotSize` | 20 px |
| Gap between page dots | `HowToPlayDotGap` | 24 px |
| How many pages there are | `HowToPlayPageCount` | 5 |

### The two numbers that are derived, and the sums that derive them

`HowToPlayPictureWidth` and `HowToPlayPictureHeight` are not free. Both are what
is left over once everything anchored around them has taken its share, and both
sums land exactly — which is the check that the layout has no slack hiding in
it.

```text
width   1920 − 48 − 48 − 64 − 656                  = 1104
        canvas   two SafeMargins   ColumnGap   WordsWidth

height  1200 − 48 − 88 − 48 − 48 − 112 − 48        =  808
        canvas   SafeMargin  HeadingLineBox  HeadingGap
                 ControlsGap  ButtonHeight  SafeMargin
```

`HowToPlayHeadingLineBox` is 88 px — `HowToPlayHeadingSize` 72 at 1.2, rounded
up to a whole pixel — for the same reason
[settings-dialog.md](settings-dialog.md#named-constants) writes its title's line
box down: a gap measured from a font's default line height is 48 px in one
renderer and 51 px in the next, and here it is load-bearing for the picture's
height.

### The pond, drawn smaller

The pictures draw real lanes, so they need the pond's furniture at a size that
fits `HowToPlayPictureWidth` rather than the board's own. These are a **second
set of numbers for the same shapes**, not a rescaling rule, because a rule that
says "70 % of the board's" is a rule that produces fractional pixels.

| Element | Constant | Value | On the board |
| --- | --- | --- | --- |
| Lily pad diameter | `HowToPlayPadDiameter` | 64 px | 112 px |
| Log width | `HowToPlayLogWidth` | 100 px | 176 px |
| Log height | `HowToPlayLogHeight` | 120 px | 896 px, shared by every lane |
| Frog diameter | `HowToPlayFrogDiameter` | 50 px | 88 px |
| Gap between positions in a lane | `HowToPlayLanePositionGap` | 43 px | 48 px and elastic |
| Gap between stacked lanes | `HowToPlayLaneGap` | 48 px | — |
| Log label size | `HowToPlayLogLabelSize` | 20 px | 26 px |

`HowToPlayLanePositionGap` is derived from the picture's width the same way the
board's `LanePositionGap` is derived from the screen's, and the sum is exact:

```text
(1104 − 2 × 56 − 2 × 100 − 7 × 64) ÷ 8  =  (1104 − 112 − 200 − 448) ÷ 8  =  43
 picture   two insets    two logs  seven pads    eight gaps
```

**The logs are per-lane here, not shared.** On the board a single Start log and
a single End log run the full height of the pond
([game-board.md](game-board.md)); in these pictures each lane carries its own
pair. That is a deliberate difference and it is the one place the drawing is
not the board: a picture of a single lane needs the log at the end of *that*
lane, and page 4 draws three lanes that are three separate examples rather than
three lanes of one game. Page 1 and page 5 are the only ones drawing a real
four-lane board, and there the per-lane logs read as one column anyway because
they are the same colour and vertically aligned.

**Every drawn lane repeats the same seven pad shapes.** The lily pad is the
board's own — notched and veined, from
[game-board.md](game-board.md)'s twelve-row variation table, at
`HowToPlayPadDiameter` instead of the board's diameter — but which row a pad
draws depends only on its *position*, so lane 2 draws the same seven as lane 1.
That is what all five mockups draw, and it is deliberate: the board's `× 5`
stagger exists so four lanes of one game do not line up into visible columns,
and page 4's three lanes are three separate examples rather than one game.

## What the pictures are drawn from

The table above stops at the frame. The numbers *inside* the five pictures —
the die, the piles, the grid, the boxes a log's word and a frog sit in — were
agreed in the mockups and lived only in their CSS, which meant the layout was
agreed but not distilled. They are written down here so the code receives them
rather than re-guessing them, which is
[the direction of travel](../../engineering/ui-design-process.md#the-named-constants-are-the-origin-not-an-afterthought)
the rest of this page already follows. **No value here is new**: every one is a
rule in a committed mockup, transcribed by
[#414](https://github.com/derekwinters/connor-multiplying-frogs/issues/414).

| Element | Constant | Value |
| --- | --- | --- |
| A drawn log's corner radius | `HowToPlayLogRadius` | 20 px |
| `Start` / `End` down from the log's top | `HowToPlayLogLabelTopPadding` | 16 px |
| Gap between that word and a frog on the same log | `HowToPlayLogLabelGap` | 12 px |
| A drawn frog's outline | `HowToPlayFrogOutline` | 3 px |
| The paper picture's hairline | `HowToPlayPaperOutline` | 3 px |
| Small print inside a picture | `HowToPlayNoteSize` | 34 px |
| Its line box, as a ratio | `HowToPlayNoteLineHeight` | 1.4 |
| Page 4's caption line box | `HowToPlayCaptionLineBox` | 52 px |
| Gap between page 4's examples | `HowToPlayExampleGap` | 56 px |

The two paper pages are drawn from their own top-left inside corner, which is
where their offsets below are measured from.

| Page 2 — roll the die | Constant | Value |
| --- | --- | --- |
| The die, from the picture's left | `HowToPlayDieLeft` | 96 px |
| The die, from its top | `HowToPlayDieTop` | 164 px |
| The die's side | `HowToPlayDieSize` | 200 px |
| Its corner radius | `HowToPlayDieRadius` | 32 px |
| Its outline | `HowToPlayDieOutline` | 4 px |
| Its inside padding | `HowToPlayDiePadding` | 28 px |
| A pip | `HowToPlayDiePipSize` | 34 px |
| The arrow, from the picture's left | `HowToPlayArrowLeft` | 336 px |
| The arrow's size | `HowToPlayArrowSize` | 56 px |
| The pile stack, from the picture's left | `HowToPlayPileLeft` | 452 px |
| A pile | `HowToPlayPileWidth` × `HowToPlayPileHeight` | 200 × 120 px |
| Its corner radius | `HowToPlayPileRadius` | 16 px |
| Its outline | `HowToPlayPileOutline` | 4 px |
| Gap between piles | `HowToPlayPileGap` | 24 px |
| A pile's label | `HowToPlayPileLabelSize` | 36 px |
| How far the two unpicked piles are dimmed | `HowToPlayPileDimOpacity` | 0.4 |
| The table, from the picture's left | `HowToPlayRollTableLeft` | 96 px |
| The table, from its top | `HowToPlayRollTableTop` | 520 px |
| Its first column | `HowToPlayRollTableColumnWidth` | 220 px |
| Gap between its columns | `HowToPlayRollTableColumnGap` | 32 px |
| Gap under its header row | `HowToPlayRollTableHeaderGap` | 20 px |

The arrow and the pile stack are **centred on the die's own centre line** rather
than carrying a top of their own, which is what the mockup's numbers already
work out to and what keeps the row a row if the die ever changes size.

| Page 3 — work it out | Constant | Value |
| --- | --- | --- |
| The grid, from the picture's left | `HowToPlayGridLeft` | 120 px |
| The grid, from its top | `HowToPlayGridTop` | 96 px |
| A cell | `HowToPlayCellSize` | 88 px |
| Gap between cells | `HowToPlayCellGap` | 8 px |
| A cell's corner radius | `HowToPlayCellRadius` | 8 px |
| A cell's outline | `HowToPlayCellOutline` | 3 px |
| A digit in a cell | `HowToPlayCellDigitSize` | 48 px |
| The carry row's height | `HowToPlayCarryRowHeight` | 56 px |
| A carry box | `HowToPlayCarryBoxWidth` × `HowToPlayCarryBoxHeight` | 48 × 44 px |
| Its corner radius | `HowToPlayCarryBoxRadius` | 6 px |
| The answer row's outline | `HowToPlayAnswerOutline` | 4 px |
| The call-outs, from the picture's left | `HowToPlayCalloutLeft` | 560 px |
| The call-outs, from its top | `HowToPlayCalloutTop` | 208 px |
| Their column | `HowToPlayCalloutWidth` | 440 px |
| Gap between them | `HowToPlayCalloutGap` | 40 px |
| Their line box, as a ratio | `HowToPlayCalloutLineHeight` | 1.5 |

The lane stacks on pages 1, 4 and 5 carry no offsets of their own: each is
`HowToPlayPictureInset` from the picture's left, and as far down from its top as
half of what `HowToPlayPictureHeight` has left once the stack has taken its
share — 92 px on a four-lane page, which is exactly where the mockups draw it.
Written as that sum rather than as 92, and as a distance from the top rather
than as a centring, because the lane drawings are
[anchored to the picture's top-left inset](#anchors): a picture that is taller
than 808 px on a screen that is not 16:10 grows below its lanes instead of
moving them.

## Elements

- **`Back`** — secondary [button](shared-components.md#button), bottom-left.
  On pages 2–`HowToPlayPageCount` it goes back one page. **On page 1 it leaves
  the screen**, which is what makes it a real button on every page rather than
  a disabled one on the first.
- **`Next`** — primary button, bottom-right. On pages
  1–(`HowToPlayPageCount` − 1) it advances one page. On the last page its label
  is **`Done`** and it leaves the screen. It is the same button in the same
  place doing the same thing — finishing with this page — so it is not a second
  element and the shared Button invariant *"exactly one primary button is
  visible at a time"* holds throughout.
- **`progress`** — `HowToPlayPageCount` dots, `HowToPlayDotSize` across,
  `HowToPlayDotGap` apart. The current page's dot is the ink colour; the rest
  are `faint`. Not tappable — they say where you are, they are not a way to
  jump. A row of five 20 px dots is well under `MinTouchTarget` and making them
  targets would mean making them bigger than they should look.
- **`heading`** — per page, at `HowToPlayHeadingSize`. This screen has **no
  title of its own**: the heading is the page's, not the screen's, because
  "How to play" is already the words on the button that opened it and repeating
  it above every page costs a line that a picture wants.

### The five pages

| # | Heading | The picture | What the words say |
| --- | --- | --- | --- |
| 1 | `Your lane` | Four lanes, every frog on its Start log — a game about to begin | Every frog has a lane of its own. Start log, seven lily pads, End log. First frog to its End log wins. |
| 2 | `Roll the die` | A die showing 3, an arrow, three piles with the middle one lit | You roll one die. It picks your pile and does nothing else. You cannot choose or swap. |
| 3 | `Work it out` | The grid on `12 × 34`, with the carry boxes, the working rows and the answer row called out | The grid is your paper. The answer goes in the bottom row. Nothing else is marked. |
| 4 | `Your frog hops` | Three separate lanes: forward one, back one, and a frog staying put on the Start log | Right hops forward one, wrong hops back one, and on the Start log a wrong answer costs the turn and no more. |
| 5 | `Things people ask` | A four-lane board mid-game, one frog home on its End log | Three questions and their answers: landing on somebody, whether a hard card is worth more, and what happens after somebody wins. |

Pages 1–4 are **onboarding** — what to do, in the order you do it. Page 5 is
**reference** — the questions somebody mid-game opens this to answer. Both are
here, in that order, because
[#324](https://github.com/derekwinters/connor-multiplying-frogs/issues/324) is
right that the button is reachable from two very different situations: a
first-time player who needs the sequence, and somebody stuck on their turn who
needs one fact. Putting reference last costs the second reader four presses;
putting it first costs the first reader the thread of the turn. Four presses is
the cheaper mistake.

Every word on these pages is a restatement of
[how to play](../../intro/how-to-play.md) and
[rules of play](../rules.md) for an eight-year-old on a tablet. **No page here
decides a rule.** Where this screen and those pages disagree, those pages are
right and this one is corrected — the same relationship
[how to play](../../intro/how-to-play.md) has with
[rules of play](../rules.md).

## Behaviour

- **Entering:** from `How to play` on the settings dialog. It always opens on
  page 1. It does not remember where you got to, because remembering is a mode
  that arrives at the next player in whatever state the last one left it —
  which is exactly the objection
  [ADR-0002](../../adr/0002-structured-working-out-grid.md) raises about modes
  on a shared device.
- **Leaving** — by `Done`, or by `Back` from page 1 — returns to the
  **settings dialog, open, exactly as it was**. Not to the board. One rule for
  both buttons, and the dialog's own `Back to the game` is a primary button in
  the bottom-right corner, which is precisely where `Done` was: the finger does
  not move.
- **Hardware back** does what `Back` does — one page back, and from page 1 it
  leaves the screen. It never jumps out of the middle of the sequence, in the
  spirit of the [dialog rule](shared-components.md#dialog) that back does what
  the least destructive button does.
- **Paging:** `picture` and `words` cross-fade over
  [`DialogFadeDuration`](shared-components.md#dialog) (0.15 s). Nothing slides.
  `heading`, `progress` and `controls` do not animate at all — they are the
  furniture that says where you are, and furniture that moves is furniture a
  child chases.
- The game underneath is untouched throughout. There is no simulation running
  and nothing to pause.

## Mockup

All five pages, at 1920 × 1200:

- [`mockups/how-to-play-1-your-lane.html`](mockups/how-to-play-1-your-lane.html)
- [`mockups/how-to-play-2-roll-the-die.html`](mockups/how-to-play-2-roll-the-die.html)
- [`mockups/how-to-play-3-work-it-out.html`](mockups/how-to-play-3-work-it-out.html)
- [`mockups/how-to-play-4-your-frog-hops.html`](mockups/how-to-play-4-your-frog-hops.html)
- [`mockups/how-to-play-5-things-people-ask.html`](mockups/how-to-play-5-things-people-ask.html)

Every page is drawn, rather than one page drawn and the rest described, because
the thing most likely to be wrong here is whether the copy fits beside the
picture — and that is not knowable from a sum. It very nearly was not: page 2's
first drawing had the three piles running straight through the table underneath
them, and page 4 and page 5 both overflowed the picture's bottom edge until the
log came down from 160 px to `HowToPlayLogHeight`. None of those three were
visible in the arithmetic.

There are no comparison pairs here. This wireframe is not asking "which of
these two", it is asking "does this read to Connor" — and the answer to that
comes from the tablet, not from a second drawing.

## Open questions

- **1. Is `How to play` reachable from the title screen too? Proposed: no, not
  in this issue.** [#324](https://github.com/derekwinters/connor-multiplying-frogs/issues/324)
  makes the real argument for yes: somebody who has never played reaches for
  the title screen, not for a gear inside a game they have not started. What is
  in the way is that
  [title-screen.md's first invariant](title-screen.md#invariants) says *"the
  only interactive elements on this screen are `RESUME` and `NEW`. There is
  never a third"*, and adding a third is a change to that page rather than a
  consequence of this one. It is a one-button change and it should be its own
  issue, decided on its own terms — not folded into the wireframe for the
  screen it would open. That page's own open question on this is updated to say
  the screen now exists, which is what used to block it.
- **2. Is five pages right, and is the copy right? Connor's call, and it is the
  question this wireframe exists to ask.** He reads it on the tablet. If he
  cannot follow it, it is wrong, and he is the only person who can say so —
  including if the answer is "too many pages" or "page 3 is confusing".
- **3. Does the classroom rules card belong here later?** Not now: the
  photograph does not exist yet
  ([#171](https://github.com/derekwinters/connor-multiplying-frogs/issues/171)
  is asking Connor for it), it would be the project's first imported image, and
  it cannot be guaranteed legible at this size. It is the actual source of the
  rules and it is charming, so when the photograph exists a sixth page showing
  it is worth considering — as a picture of where the game came from, not as
  the page anybody reads the rules off.
