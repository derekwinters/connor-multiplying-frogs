# Mockups

The committed 1:1 HTML mockups. Each is a static page sized to the target tablet
viewport — **1920 × 1200, landscape** — showing a screen's real elements at their
real proportions.

Open one on the tablet. That is what it is for — a mockup judged on a laptop is
a mockup judged at the wrong size, and size is most of what a wireframe decides.

## Committed mockups

| Screen | Mockup | Spec page |
| --- | --- | --- |
| Title screen | [`title-screen.html`](title-screen.html) | [Title screen](../title-screen.md) |
| Game setup | [`game-setup.html`](game-setup.html) | [Game setup](../game-setup.md) |
| Game board | [`game-board.html`](game-board.html) | [Game board](../game-board.md) |
| Game board, paler water | [`game-board-paler-water.html`](game-board-paler-water.html) | [Game board](../game-board.md) |
| Roll and card | [`roll-and-card.html`](roll-and-card.html) | [Roll and card](../roll-and-card.md) |
| Working-out grid, `331 × 41` | [`working-out-grid-331x41.html`](working-out-grid-331x41.html) | [Working-out grid](../working-out-grid.md) |
| Working-out grid, `68 × 5` | [`working-out-grid-68x5.html`](working-out-grid-68x5.html) | [Working-out grid](../working-out-grid.md) |
| Working-out grid, `331 × 41` grown to the cap | [`working-out-grid-331x41-grown.html`](working-out-grid-331x41-grown.html) | [Working-out grid](../working-out-grid.md) |
| Answer result — right | [`answer-result-right.html`](answer-result-right.html) | [Answer result](../answer-result.md) |
| Answer result — wrong | [`answer-result-wrong.html`](answer-result-wrong.html) | [Answer result](../answer-result.md) |
| Settings dialog | [`settings-dialog.html`](settings-dialog.html) | [Settings dialog](../settings-dialog.md) |
| End-game confirm | [`end-game-confirm.html`](end-game-confirm.html) | [End-game confirm](../end-game-confirm.md) |
| Game over | [`game-over.html`](game-over.html) | [Game over](../game-over.md) |

Two screens have more than one mockup, both drawing **a state that only exists
in contrast** — a state you have to see next to its opposite. The grid is drawn
at the biggest and the smallest problem the game can deal, and the result
dialog is drawn right and wrong. Both files in each pair are agreed pictures of
the same screen.

The grid's **third** file is the widest card with its addition section grown to
the cap. It was drawn to find out what the cap costs, and at full-size addition
rows the answer was that it does not fit — the answer row ended up below the
bottom of the tablet. That overflowing drawing was the input to
[open question 3 on the spec page](../working-out-grid.md#open-questions), and
it did its job: it is cheaper to find that in a picture than in a built screen.
Derek settled the question — smaller cells for the addition rows only — so the
file now draws the answer instead: the same six rows at
`GridAdditionRowHeight`, fitting with 16 px to spare.

The game board's pair is the live one, and it is the second kind: a real
choice, drawn twice. `game-board.html` and `game-board-paler-water.html` are
the same canvas differing in exactly one value — the water's blue. Connor picks
one on the tablet, the spec page takes the answer, and the losing file is
deleted. Until then, an edit to the board's layout has to be made in **both**
files, which is the cost of the pair and the reason it does not stay open long.

The title screen used to have a pair too — `title-screen.html` and
`title-screen-resume-primary.html`, differing in exactly which of `RESUME` and
`NEW` was primary, per the wireframe loop's own advice that where there is a
real choice you
[propose two mockups](../../../engineering/ui-design-process.md#the-loop).
That question is
[now settled](../title-screen.md#open-questions) — `NEW` — so
`title-screen.html` is the agreed picture and the losing comparison file is
deleted, per [issue #216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216).

Each row arrives with its screen's `Wireframe:` issue. See
[UI design process](../../../engineering/ui-design-process.md).

### Colour in these mockups is mostly a placeholder

The four frog colours exist so the frogs can be told apart in a picture, and the
single green accent marks the primary action. Neither is a palette decision —
that is an `area:art` call and it lands with the frog sprites. See
[frog colours](../shared-components.md#frog-colours).

The game board is the first exception. Its water, logs and lily pads are a
decision Derek made — a pond is blue, its logs brown, its lily pads green — and
they are written down as
[a constants table on the spec page](../game-board.md#colours), the same as
every dimension on that screen. The mockup receives them from there rather than
being the place they live. The exact hues are still Connor's to settle, which
is what the pair of board files is for.

## What a mockup is, and is not

**Is:** static HTML and CSS, one file, no build step, no framework, no
JavaScript beyond nothing at all. A picture that happens to be made of divs.

**Is not:** a prototype. No behaviour, no navigation between screens, no
interactivity. The moment a mockup does something, people start judging what it
does instead of how it looks, and it stops being cheap to change — which was the
entire point.

## Conventions

- **One file per screen**, named for the screen: `pause-screen.html`.
- **Sized to the target viewport** with a fixed-size container, so it renders at
  1:1 rather than filling whatever window it opens in. State the device and
  dimensions in a comment at the top of the file.
- **Real proportions, real units.** The numbers in the mockup are the numbers in
  the spec page's constants table. If they disagree, one of them is a bug.
- **Placeholder content is honest** — the longest plausible score, not `0`; a
  real-length label, not `Text`. A layout that only works with short strings is
  a layout that breaks on the first real one.
- **Self-contained.** Inline the CSS; no CDN links, no web fonts fetched at
  view time. A mockup that needs the network is a mockup that doesn't open on
  the sofa.
- **Committed, not generated.** These are reviewed artifacts and they live in
  git next to the spec they illustrate.
