# Mockups

The committed 1:1 HTML mockups. Each is a static page sized to the target phone
viewport, showing a screen's real elements at their real proportions.

Open one on a phone. That is what it is for — a mockup judged on a laptop is a
mockup judged at the wrong size, and size is most of what a wireframe decides.

## Committed mockups

| Screen | Mockup | Spec page |
| --- | --- | --- |
| *(none yet)* | | |

Each row arrives with its screen's `Wireframe:` issue. See
[UI design process](../../../engineering/ui-design-process.md).

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
