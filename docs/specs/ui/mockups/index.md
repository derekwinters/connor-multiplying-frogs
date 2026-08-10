# Mockups

The committed 1:1 HTML mockups. Each is a static page sized to the target tablet
viewport — **1920 × 1200, landscape** — showing a screen's real elements at their
real proportions.

Open one on the tablet. That is what it is for — a mockup judged on a laptop is
a mockup judged at the wrong size, and size is most of what a wireframe decides.

## Committed mockups

| Screen | Mockup | Spec page |
| --- | --- | --- |
| Title screen — `NEW` primary | [`title-screen.html`](title-screen.html) | [Title screen](../title-screen.md) |
| Title screen — `RESUME` primary | [`title-screen-resume-primary.html`](title-screen-resume-primary.html) | [Title screen](../title-screen.md) |
| Game setup | [`game-setup.html`](game-setup.html) | [Game setup](../game-setup.md) |
| Game board | [`game-board.html`](game-board.html) | [Game board](../game-board.md) |
| Roll and card | [`roll-and-card.html`](roll-and-card.html) | [Roll and card](../roll-and-card.md) |
| Working-out grid, `331 × 41` | [`working-out-grid-331x41.html`](working-out-grid-331x41.html) | [Working-out grid](../working-out-grid.md) |
| Working-out grid, `68 × 5` | [`working-out-grid-68x5.html`](working-out-grid-68x5.html) | [Working-out grid](../working-out-grid.md) |
| Answer result — right | [`answer-result-right.html`](answer-result-right.html) | [Answer result](../answer-result.md) |
| Answer result — wrong | [`answer-result-wrong.html`](answer-result-wrong.html) | [Answer result](../answer-result.md) |
| Settings dialog | [`settings-dialog.html`](settings-dialog.html) | [Settings dialog](../settings-dialog.md) |
| End-game confirm | [`end-game-confirm.html`](end-game-confirm.html) | [End-game confirm](../end-game-confirm.md) |
| Game over | [`game-over.html`](game-over.html) | [Game over](../game-over.md) |

Three screens have two mockups each, for two different reasons.

Two of them are drawing **a state that only exists in contrast** — a state you
have to see next to its opposite. The grid is drawn at the biggest and the
smallest problem the game can deal, and the result dialog is drawn right and
wrong. Both files in each pair are agreed pictures of the same screen.

The title screen's pair is **a question, not two states**. Which of `RESUME` and
`NEW` is the primary button is
[open on its spec page](../title-screen.md#open-questions), and the wireframe
loop's own advice is that where there is a real choice you
[propose two mockups](../../../engineering/ui-design-process.md#the-loop),
because comparing two pictures is a much easier conversation than critiquing
one. Neither file is agreed yet; when review picks one it becomes
`title-screen.html` and the other is deleted.

Each row arrives with its screen's `Wireframe:` issue. See
[UI design process](../../../engineering/ui-design-process.md).

### Colour in these mockups is a placeholder

The four frog colours exist so the frogs can be told apart in a picture, and the
single green accent marks the primary action. Neither is a palette decision —
that is an `area:art` call and it lands with the frog sprites. See
[frog colours](../shared-components.md#frog-colours).

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
