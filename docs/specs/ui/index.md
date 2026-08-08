# UI specs

**These pages are the layout contract.** One page per screen, saying what is on
it, how it is arranged, and the named constants that define its geometry. Code
is built from these pages; it does not decide them.

How a page here comes to exist — the wireframe loop, what counts as structure,
and when an implementation is allowed to start — is in
[UI design process](../../engineering/ui-design-process.md). Read that first if
you are about to open a wireframe issue.

## Screens

*No screens specified yet.* The game concept is still being settled in
[issue #7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7),
and every screen page arrives through its own `Wireframe:` issue.

## Shared components

Reusable atomic pieces — a primary button, the score readout, a confirm dialog —
are specified once in [Shared components](shared-components.md) and
**referenced** by screen pages, never re-specified in them.

The reason is drift. Three screens that each describe "the primary button" will,
within a month, describe three subtly different buttons, and no one edit fixes
all of them. One page, three references, one edit.

## The per-screen page template

Every screen page carries these sections, in this order. Sections that don't
apply are written as "None" rather than omitted — an omitted section reads as an
oversight, and the whole point of a template is that a reader knows where to
look.

### 1. Title and one-line purpose

What the screen is for, in a sentence, in words Connor would use.

### 2. Invariants

Things that must always be true of this screen, stated as
`**Invariant:** …` lines so they can be quoted in a PR and asserted in a test.

```markdown
**Invariant:** the pause screen never covers the score readout.
**Invariant:** every destructive action confirms before it acts.
```

### 3. Regions

The screen broken into its named areas, top to bottom. A region is a box with a
name and a job — `header`, `playfield`, `controls`. Naming them is what lets
everything else say *where* without re-describing the layout each time.

| Region | Job |
| --- | --- |
| `header` | score and pause button |
| `playfield` | the pond; everything interactive |
| `controls` | the two action buttons |

### 4. Anchors

How each region is positioned and what it is pinned to — which edge, which safe
area, what happens when the screen is taller or shorter than the mockup.

Anchors are the section people skip and then regret. A phone is not one size;
a layout that only says "the button is 40 dp from the bottom" has not said
whether that's from the screen edge or the safe area, and the answer is the
difference between a reachable button and one under the gesture bar.

### 5. Named constants

The table that the code's constants come from. Every size, margin, spacing,
radius, and duration on the screen, named and valued.

| Element | Constant | Value |
| --- | --- | --- |
| Panel width | `PausePanelWidth` | 280 dp |
| Gap between buttons | `PauseButtonSpacing` | 12 dp |

Same names in the code. See
[named constants are the origin](../../engineering/ui-design-process.md#the-named-constants-are-the-origin-not-an-afterthought).

### 6. Elements

Each interactive element: what it says, what it does, what state it can be in
(disabled, pressed, hidden), and whether it confirms.

### 7. Behaviour

What happens on entry and exit, what the hardware back button does, what
animates and for how long, and what the screen does to the simulation
underneath it.

### 8. Mockup

A link to the 1:1 HTML mockup for this screen, in
[`mockups/`](mockups/index.md).

### 9. Open questions

Anything deliberately not decided yet, with the issue that will decide it.
"None" when there are none — an empty section is a claim that the spec is
complete, which is worth making explicitly.
