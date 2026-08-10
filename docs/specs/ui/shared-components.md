# Shared components

Reusable atomic pieces of UI, specified **once** and referenced by screen pages.

A screen page says *"the confirm dialog ([shared](shared-components.md))
appears"* and moves on. It does not restate the dialog's padding, its button
order, or how it dismisses.

## Why this page exists

Three screens that each describe "the primary button" will, within a month,
describe three subtly different buttons. Nobody will have decided they should
differ — the descriptions were written on different days by people making
reasonable choices. Then a change to the button means finding all three, and one
gets missed.

One page, three references, one edit.

## When something belongs here

A piece belongs here once **either** is true:

- it appears on two or more screens; or
- it is going to, and the second screen is already specified.

Do not pre-emptively generalise a one-screen element. A component extracted
before its second use is a component shaped by exactly one caller, and the
second caller ends up working around it.

Moving an element here later is cheap: cut its section out of the screen page,
paste it here, and leave a reference behind.

## The per-component template

### 1. What it is

One sentence, and where it is used — a list of the screen pages that reference
it. That list is how you find out what a change to this page affects.

### 2. Invariants

`**Invariant:** …` lines. Shared components are where invariants earn the most:
"every destructive action confirms" is one rule stated once, rather than a thing
five screens each have to remember.

### 3. Named constants

The same constants table screen pages use. These are the code's constants.

### 4. States

Every state the component can be in — default, pressed, disabled, loading,
error — and what it looks like in each. A component page that only describes the
default state is a component that gets a different disabled style on every
screen.

### 5. Behaviour

What it does when interacted with, what it emits, and what it never does.

---

## The canvas every component is measured in

Every number on this page and on every screen page is in **pixels at the
1920 × 1200 reference resolution**, which is the one canvas this game is
designed against — see
[target platform](../../engineering/tech-stack.md#target-platform). Unity's
`CanvasScaler` is set to that same reference resolution, so a constant written
here is the constant the code sets, with no conversion in between.

### Why the numbers look big

`MinTouchTarget` is 96 px, which is roughly double what a UI guideline for
adults would say. On a 10-inch 1920 × 1200 tablet that is about 64 dp, or 10 mm
of glass.

The guideline figure of 48 dp is a *minimum for an adult who is paying
attention*. This game is tapped by an eight-year-old who is thinking about
`331 × 41` and not about the button, and a mis-tap here does not cost a
scrolled list — it can cost a turn. Every interactive element on every screen is
at least this big, and the ones that decide something are considerably bigger.

---

## Components

### Button

#### 1. What it is

The one button in the game. It comes in three **kinds**, which differ in colour
and weight but never in size or shape.

Used by: [title screen](title-screen.md), [game setup](game-setup.md),
[game board](game-board.md), [roll and card](roll-and-card.md),
[working-out grid](working-out-grid.md), [answer result](answer-result.md),
[settings dialog](settings-dialog.md),
[end-game confirm](end-game-confirm.md), [game over](game-over.md).

#### 2. Invariants

**Invariant:** a button is never smaller than `MinTouchTarget` in either
direction.
**Invariant:** exactly one primary button is visible at a time. Two primaries
is a screen that has not decided what it wants the player to do.
**Invariant:** a destructive button never sits within `ButtonGap` of the button
a player is most likely to be reaching for.

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Minimum touch target, any direction | `MinTouchTarget` | 96 px |
| Button height | `ButtonHeight` | 112 px |
| Minimum button width | `ButtonMinWidth` | 320 px |
| Horizontal padding inside a button | `ButtonPaddingX` | 48 px |
| Corner radius | `ButtonRadius` | 24 px |
| Label size | `ButtonLabelSize` | 44 px |
| Gap between adjacent buttons | `ButtonGap` | 32 px |
| Gap between a destructive button and its neighbour | `ButtonDestructiveGap` | 96 px |
| Press travel | `ButtonPressOffset` | 4 px |

#### 4. States

| State | Appearance |
| --- | --- |
| Default (primary) | Filled accent, light label |
| Default (secondary) | Outlined, `ButtonBorderWidth` 4 px, dark label, no fill |
| Default (destructive) | Outlined in the warning colour, warning-coloured label |
| Pressed | Moves down by `ButtonPressOffset`; fill darkens |
| Disabled | 40 % opacity, no press response |
| Hidden | Not laid out at all — buttons do not leave gaps behind |

#### 5. Behaviour

Acts on **release**, not on press, so a finger that lands wrong can slide off
and cancel. A disabled button does nothing at all — it does not explain itself,
because an eight-year-old reading an error about why a button is off is a worse
outcome than a button that is obviously not ready yet.

---

### Dialog

#### 1. What it is

A panel over a dimmed copy of the screen underneath, used for everything that
interrupts play: the card, the working-out grid, the result, settings, and
confirms.

Used by: [roll and card](roll-and-card.md),
[working-out grid](working-out-grid.md), [answer result](answer-result.md),
[settings dialog](settings-dialog.md),
[end-game confirm](end-game-confirm.md).

#### 2. Invariants

**Invariant:** a dialog always dims what is behind it — there is never a panel
floating on live board.
**Invariant:** the board underneath never moves while a dialog is open. The
frog is where the player left it.
**Invariant:** a dialog that decides something has no tap-outside-to-dismiss and
no close cross. It is left by pressing one of its buttons, so a game is never
half-answered because a sleeve brushed the glass.
**Invariant:** the hardware back button does what the dialog's *least
destructive* button does, and never what its most destructive one does —
except the three dialogs whose pages make back inert (roll and card,
working-out grid, answer result), where back does nothing.

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Backdrop dim | `DialogScrimOpacity` | 0.66 |
| Corner radius | `DialogRadius` | 32 px |
| Padding inside the panel | `DialogPadding` | 56 px |
| Title size | `DialogTitleSize` | 56 px |
| Gap between title and body | `DialogTitleGap` | 40 px |
| Gap between body and the button row | `DialogButtonRowGap` | 48 px |
| Widest a dialog may be | `DialogMaxWidth` | 1824 px |
| Tallest a dialog may be | `DialogMaxHeight` | 1104 px |
| Open and close duration | `DialogFadeDuration` | 0.15 s |

`DialogMaxWidth` and `DialogMaxHeight` are the canvas inset by 48 px on every
side. A dialog is allowed to be the whole screen, and the working-out grid is.

#### 4. States

| State | Appearance |
| --- | --- |
| Open | Panel at full opacity, scrim at `DialogScrimOpacity` |
| Opening / closing | Scrim and panel cross-fade over `DialogFadeDuration`; no slide, no bounce |
| Stacked | Does not happen. A dialog never opens over another dialog. |

#### 5. Behaviour

Opening a dialog stops the turn timer if one ever exists, and stops nothing else
— there is no simulation running underneath to pause. Buttons sit in a single
row along the bottom of the panel, the primary one on the **right**, because
that is the side a right-handed child holding a tablet reaches first and the
left side is where a thumb rests.

The no-dismiss rule is the one that matters. Every dialog in this game either
belongs to a turn in progress or is asking a question with a cost, and both are
worse when they can be dismissed by accident than when they need a deliberate
tap.

---

### Player chip

#### 1. What it is

A frog's identity, wherever the game has to say *which frog* — its colour, its
name, and how far up its lane it is.

Used by: [game setup](game-setup.md), [game board](game-board.md),
[roll and card](roll-and-card.md), [answer result](answer-result.md),
[game over](game-over.md).

#### 2. Invariants

**Invariant:** a frog is identified by its colour **and** its colour's name, in
words, always together. Colour alone excludes a colour-blind player from knowing
whose turn it is, in a game where four players share one screen.
**Invariant:** the chip for the player whose turn it is is visibly different
from every other chip on screen, by something other than colour.
**Invariant:** no chip anywhere contains a name a player typed. See
[why there is no typing](#why-there-is-no-typing).

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Chip height | `PlayerChipHeight` | 96 px |
| Chip width on the board | `PlayerChipWidth` | 256 px |
| Frog swatch diameter | `PlayerSwatchDiameter` | 64 px |
| Gap between swatch and label | `PlayerChipSwatchGap` | 24 px |
| Label size | `PlayerChipLabelSize` | 40 px |
| Corner radius | `PlayerChipRadius` | 20 px |
| Ring drawn around the active chip | `PlayerChipActiveRing` | 6 px |

#### 4. States

| State | Appearance |
| --- | --- |
| Default | Swatch, colour name, pad count |
| Active (this player's turn) | `PlayerChipActiveRing` ring, filled background, label at full weight |
| Home (frog has finished) | A small home marker after the name; pad count replaced by `Home!` |
| Empty seat (setup only) | Dashed outline, `Tap to add` in place of the name |

#### 5. Behaviour

The chip is not a button anywhere except on
[game setup](game-setup.md), where tapping one adds or removes that frog from
the game. On every other screen it is a readout.

#### Why there is no typing

Four players share one tablet, and the setup screen is the first thing they
touch. Asking four children to each type a name means a soft keyboard, a
misspelling, an editing flow, and — reliably — somebody typing something rude
about somebody else. Frogs are identified by colour, exactly as the classroom
pieces are, and the whole of setup is tapping the colour you want.

This is a **presentation** decision under
[ADR-0001](../../adr/0001-rules-sacred-presentation-ours.md): the cardboard
pieces have no names either.

---

### Frog colours

#### 1. What it is

The four colours a player can be in v1, and their names.

Used by: [game setup](game-setup.md), [game board](game-board.md),
[game over](game-over.md), and every chip above.

#### 2. Invariants

**Invariant:** exactly four are offered. The cap of four players is a recorded
rule change from the classroom game's 2–8; see
[future ideas](../future-ideas.md).
**Invariant:** two frogs in the same game are never the same colour.
**Invariant:** the four are distinguishable to a colour-blind player by
lightness alone, which is why the set is not four mid-tone colours.

#### 3. Named constants

| Frog | Constant | Wireframe placeholder |
| --- | --- | --- |
| Green | `FrogGreen` | `#3F8E4F` |
| Blue | `FrogBlue` | `#2C6DAF` |
| Orange | `FrogOrange` | `#D2762B` |
| Pink | `FrogPink` | `#C24C86` |

**These four values are placeholders, not the palette.** The mockups need
*something* to draw, and four separable hues taken from the classroom pieces is
the least-committing thing to draw. The real palette is an `area:art` decision
and it lands with the frog sprites; when it does, these four constants take the
real values and nothing else on any screen changes.

#### 4. States

A frog colour has no states. It is used as a fill by the components that do.

#### 5. Behaviour

None. It is a value.
