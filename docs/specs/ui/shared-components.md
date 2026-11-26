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

### What fills a screen that is not 16:10

The target tablet is 1920 × 1200 exactly, but the game runs on whatever screen
it is opened on, and the `CanvasScaler` is set to **expand** — the canvas is
never smaller than the reference in either direction, so nothing drawn at
1920 × 1200 is ever cropped off an edge. The cost of that choice is that on any
other aspect ratio the canvas is *larger* than the reference in one direction,
and something has to fill the difference.

**Invariant:** the screen's background reaches all four edges of the screen, on
every aspect ratio.

**Invariant:** everything laid out is laid out in the 1920 × 1200 reference
canvas, centred on the screen. The extra space a wider or taller device gives
us is margin, not layout — every constant on every screen page keeps meaning
exactly what it says.

**Invariant:** the extra space is background and nothing else. No element
appears there, and no element grows into it. An element that only exists on
some devices is an element nobody can review against a mockup, and every mockup
in this repo is drawn at exactly one size.

**The one exception: a band that is background.** A full-width band whose job is
to be the top or the bottom of the screen — the game board's `header`, `pond`
and `controls`, and nothing else today — reaches the screen's edges by the same
rule the background does, and a control anchored to such a band's left or right
edge sits `SafeMargin` in from the screen's edge with it. Everything the band
*contains* keeps this invariant untouched.

The exception is narrow on purpose, and the test for it is what the element
would look like if it did not grow: a band that stops short leaves a strip of
background past each of its ends, which reads as a rendering fault. A button, a
card or a dialog that stops short simply looks placed. Only the first kind grows
— see
[the bands reach the edges too](game-board.md#the-bands-reach-the-edges-too),
which is where it was decided and why.

**Invariant:** nothing behind the canvas is ever visible. Whatever a screen
paints, it paints to all four edges, and the scene camera clears to the app's
background rather than to the engine's default sky — so even a frame drawn
before any screen has painted is the game's own colour.

The app's background is the mockups' `--bg`, `#EDF1EF`. The title screen and
game setup paint it, and it is what the camera clears to.

**The game board and game over are the exceptions.** They paint
[`PondWater`](game-board.md#colours) instead, to the same four edges by the same
rule, because those screens are a pond rather than a page. Game over joined the
board in the [Pond style](style-guide.md): it used to paint `--bg`, and now its
standings sit on the same water the frogs just swam in. A screen may have a
background of its own; what it may not do is leave any of the screen unpainted.

A dialog is the one screen that paints no background at all: what it lays over
the screen underneath is the [scrim](#dialog), and the scrim reaches the edges
for the same reason and by the same rule.

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
**Invariant:** a button is never narrower than the label it is holding plus
`ButtonPaddingX` either side. A label is never clipped, never shrunk to fit,
and never allowed past the button's own edge.
**Invariant:** an outline kind is a ring. Whatever is behind the button — the
panel, the screen — shows through the middle of it, because *no fill* means no
fill.
**Invariant:** every kind is outlined, and a press always reads as the button
going *in*: the primary kind sinks into its lip, and nothing about a press makes
a button bigger, brighter or blurred.

How a button **looks** — its colours, outline, lip, shadows and font — is the
[style guide](style-guide.md)'s. This page keeps its sizes and its behaviour.

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Minimum touch target, any direction | `MinTouchTarget` | 96 px |
| Button height | `ButtonHeight` | 112 px |
| Minimum button width | `ButtonMinWidth` | 320 px |
| Horizontal padding inside a button | `ButtonPaddingX` | 48 px |
| Corner radius | `ButtonRadius` | 30 px |
| Label size | `ButtonLabelSize` | 44 px |
| Gap between adjacent buttons | `ButtonGap` | 32 px |
| Gap between a destructive button and its neighbour | `ButtonDestructiveGap` | 96 px |
| Press travel | `ButtonPressOffset` | 8 px |
| Border width, every kind | `ButtonBorderWidth` | 5 px |
| Depth of the primary kind's solid lip | `ButtonLipDepth` | 10 px |
| Opacity while disabled | `ButtonDisabledOpacity` | 0.40 |

**`ButtonMinWidth` is a floor, not a fit.** A button is `ButtonMinWidth` wide,
or as wide as its label plus `ButtonPaddingX` either side, whichever is more —
which is what every committed mockup draws, `min-width:320px` with
`padding:0 48px` on a box that shrinks to its contents. A short label does not
shrink the button below the floor; a long one raises it above.

The widths a screen page states are floors in the same way. When a page gives a
button its own width — [game setup](game-setup.md)'s controls, the
[settings dialog](settings-dialog.md)'s `SettingsActionWidth` — that is the width the
button has unless its own words need more, in which case they win. A screen 12 px
wider than its table says is a smaller problem than a label hanging over the
edge of the button it belongs to, and the second one is what
[#323](https://github.com/derekwinters/connor-multiplying-frogs/issues/323)
found on a tablet: `Back to the game` at 16 characters, in a 320 px button with
224 px between its paddings.

Two consequences worth stating, because they are the reason this is a rule
rather than a fix. The width of a button whose label is composed at runtime —
[answer result](answer-result.md)'s `{0}'s turn`, holding a name a child typed —
cannot be a constant at all, and nothing on a page can name it. And a mockup
draws this rule already, so the numbers a mockup measures assume it: this page's
own [settings dialog](settings-dialog.md#what-drawing-the-pair-changed) decided
where the About block goes partly on `Back to the game` rendering 531 px wide,
which is 211 px more than `ButtonMinWidth` and only true of a button that widens
to its words.

#### 4. States

| State | Appearance |
| --- | --- |
| Default (primary) | `Leaf` fill, `White` label, `ButtonBorderWidth` `Ink` outline, standing on a `ButtonLipDepth` solid lip of `LeafLip` that is outlined in `Ink` too |
| Default (secondary) | A ring: `ButtonBorderWidth` `Ink` outline, `Ink` label, no fill, a 6 px solid shadow of `Ink` at 25 % |
| Default (destructive) | A ring in `Berry`: `Berry` outline and label, no fill, a 6 px solid shadow of `Berry` at 25 % |
| Pressed | Moves down by `ButtonPressOffset` and its lip or shadow shrinks by the same amount, so it sinks in rather than moving; a primary's fill goes to `LeafPressed` |
| Disabled | `ButtonDisabledOpacity` opacity of the kind's own default look, no shadow, no press response |
| Hidden | Not laid out at all — buttons do not leave gaps behind |

Every colour named in this table is a constant of the
[style guide's palette](style-guide.md#the-palette), and the labels are set in
`FontDisplay` (Fredoka) at `WeightDisplay` (semibold) — which answers
[#422](https://github.com/derekwinters/connor-multiplying-frogs/issues/422).

**The table used to read** *filled accent, light label* for primary, *outlined,
4 px, dark label* for secondary, *outlined in the warning colour* for
destructive, and *moves down 4 px, fill darkens* for pressed. Those were
placeholder looks, written before the game had any art to take a look from.
The [Pond style](style-guide.md) is that look: the outline and the lip come
from the splash's lettering, the press travel doubled so the press reads at
arm's length, and the primary's lip is what it sinks into. Sizes,
`ButtonMinWidth`, the gaps and the label size did not move.

**Outlined means see-through, and it has to be drawn as a ring.** The three
kinds are two images stacked — a border shape, and a fill inset by
`ButtonBorderWidth` — and while the border was drawn as a solid rounded rect the
outlined kinds had nothing over its middle to hide it, so both read as blocks of
their border colour. `Destructive`'s warning-red label on a warning-red block
was invisible, which is
[#323](https://github.com/derekwinters/connor-multiplying-frogs/issues/323)'s
first bug: a button with nothing written on it. This table always said "no
fill"; the drawing is what disagreed, and the border is now a ring with the
middle taken out of it.

`ButtonBorderWidth` and `ButtonDisabledOpacity` were always on this page — both
appeared in the States row above with a value and no name — and are named here
so the code has a name to declare, per
[the named constants are the origin, not an afterthought](../../engineering/ui-design-process.md#the-named-constants-are-the-origin-not-an-afterthought).

**Disabled is 40 % opacity, not a grey fill.** The committed mockups' `.btn.off`
rule draws disabled as a flat grey (`background:#C9D2CE;color:#8C9793`), which
disagrees with this page's own States table. This page is the component's
contract; the mockups are drawings of screens built from it — see
[CLAUDE.md](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
on which document wins being a decision, not an assumption. This page wins: disabled is the kind's own default appearance at
`ButtonDisabledOpacity`, not a distinct grey style. The mockups restyled in the
[Pond style](style-guide.md) draw it that way; the ones not yet restyled still
draw the grey and should be redrawn to match the next time they are touched.

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
[a player has won](player-won.md), [settings dialog](settings-dialog.md),
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
except the four dialogs whose pages make back inert (roll and card,
working-out grid, answer result, [a player has won](player-won.md)), where back
does nothing. All four sit inside a single turn's own chain, where a stray press
should not fast-forward past something the game stopped to show.
**Invariant:** the scrim lets the board read as water. It is `DeepPond`, never
black, and never so dark the pond underneath is lost.
**Invariant:** a dialog opens without moving across the screen. It fades and
grows in place; it never slides, bounces or overshoots.

A dialog panel is `Paper`, outlined in `Ink` at `OutlinePanel` (6 px), dropping a
solid `ShadowPanel` — see the [style guide](style-guide.md). Its title is set in
`FontDisplay` and its sentences in `FontBody`.

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Backdrop colour | `DialogScrimColour` | `DeepPond`, `#092E4A` |
| Backdrop dim | `DialogScrimOpacity` | 0.60 |
| Corner radius | `DialogRadius` | 44 px |
| Padding inside the panel | `DialogPadding` | 56 px |
| Title size | `DialogTitleSize` | 56 px |
| Gap between title and body | `DialogTitleGap` | 40 px |
| Gap between body and the button row | `DialogButtonRowGap` | 48 px |
| Widest a dialog may be | `DialogMaxWidth` | 1824 px |
| Tallest a dialog may be | `DialogMaxHeight` | 1104 px |
| Open and close duration | `DialogFadeDuration` | 0.15 s |
| The panel's scale when it starts to open | `DialogOpenScale` | 0.94 |

`DialogMaxWidth` and `DialogMaxHeight` are the canvas inset by 48 px on every
side. A dialog is allowed to be the whole screen, and the working-out grid is.

**Three values moved with the [Pond style](style-guide.md)**, and none of them
moves anything on screen: the scrim was black at 0.66 and is now `DeepPond` at
0.60, so the board behind still reads as water rather than as a hole; the
corner went from 32 px to 44 px, to match the splash's chunky rounding; and the
open gained the 94 %-to-full grow, on top of the fade it already had. The old
*"cross-fade, no slide, no bounce"* row said what opening is not; it still does,
and now also says what it is.

#### 4. States

| State | Appearance |
| --- | --- |
| Open | Panel at full opacity, scrim at `DialogScrimOpacity` |
| Opening | Scrim and panel fade in over `DialogFadeDuration`, and the panel grows from `DialogOpenScale` to full size over the same time, easing out; no slide, no bounce, no overshoot |
| Closing | Scrim and panel fade out over `DialogFadeDuration`; the panel does not shrink |
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

A frog's identity, wherever the game has to say *which frog* — its frog and its
name, and, on the board, how far up its lane it is.

Since the [Pond style](style-guide.md) it comes in **two forms**, because the two
places it sits have different things around it:

| Form | Where | What it holds |
| --- | --- | --- |
| **Lane chip** | The [game board](game-board.md)'s lanes | The name, lettered in the frog's colour with a thin `Ink` outline, and the pad count under it. **No frog** — the frog is already sitting in the lane beside it. |
| **Dialog chip** | Inside a dialog: [answer result](answer-result.md), [working-out grid](working-out-grid.md) | The frog's render, standing free, and the name beside it. Nothing else. |

Used by: [game board](game-board.md) (lane chip),
[working-out grid](working-out-grid.md) and [answer result](answer-result.md)
(dialog chip), and [roll and card](roll-and-card.md), which keeps its current
drawing until it is [redesigned](roll-and-card.md#pending-redesign).

#### 2. Invariants

**Invariant:** a frog is identified by its colour **and** a word, always
together, and never by colour alone. The word is the frog's name: its colour's
name by default, or whatever was typed on
[game setup](game-setup.md) instead. Colour alone excludes a colour-blind
player from knowing whose turn it is, in a game where four players share one
screen. On a dialog chip the colour is the frog standing beside the name; on a
lane chip it is the name's own lettering, with the frog in the lane beside it.
**Invariant:** a frog is never shown as a coloured dot. Where a chip shows a
frog, it is that player's frog render in their colour, standing free — no
circle, no disc behind it ([PS-10](style-guide.md#ps-10)).
**Invariant:** the chip for the player whose turn it is is visibly different
from every other chip on screen, by something other than colour: a thicker
outline, `OutlineChipActive`. It is never given a second ring.
**Invariant:** nothing is ever appended to a frog's name. The chip shows the
name and only the name — not `Blue frog`, not `Blue (you)`, and on a dialog chip
not `pad 3 → 4` either.
**Invariant:** the chip never refuses or alters a name it is given. A readout is
not where a limit is enforced. The limit is `PlayerNameMaxLength`, and
[game setup](game-setup.md#where-playernamemaxlength-comes-from-and-why-a-count-is-not-enough)
is where it is enforced.
**Invariant:** a **dialog chip** grows to fit the whole name and never cuts it
off. Its text size is fixed; the chip gets wider instead.
**Invariant:** a **lane chip** is `PlayerChipWidth` wide, always, because its
width is part of the board's lane arithmetic. If a name does not fit its name
column it is truncated with an ellipsis — the rule this chip has always had,
kept by Derek on
[#526](https://github.com/derekwinters/connor-multiplying-frogs/issues/526).
**Invariant:** a lane chip's name column is a fixed `LaneChipNameColumn`
(208 px), active or not. The active chip's thicker outline takes its extra width
out of the padding, never out of the name.

The identification invariant was previously worded as colour **and its
colour's name** — literally the colour word. That letter cannot survive a
rename: a chip reading `Connor` beside a blue swatch says "Blue" nowhere. Its
purpose survives intact, because a typed name is also a word, and the reason
the rule exists is that a colour-blind player needs something other than the
colour to read. So the rule now requires a word rather than that specific word.

**Two frogs may end up with the same word**, which is the one case where the
identifying word stops identifying. Nothing prevents it. They are two children
sitting next to each other who chose the same name on purpose, and they can
sort it out; the frogs' colours still tell them apart, and colour-plus-word is
still what the chip shows.

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Chip height, both forms | `PlayerChipHeight` | 96 px |
| Corner radius, both forms | `PlayerChipRadius` | 48 px — half the height, fully round |
| Outline, both forms | `OutlineChip` | 4 px `Ink` |
| Outline of the active chip | `OutlineChipActive` | 7 px `Ink` |
| Fill of the active chip | `ActiveFill` | `#F2FADF` |
| **Lane chip** width | `PlayerChipWidth` | 256 px |
| Lane chip fill | `Paper` | `#FFFDF4` |
| Lane chip padding, each side, inside the outline | `LaneChipPaddingX` | 20 px (17 px on the active chip, so the 3 px of thicker outline comes out of the padding) |
| Lane chip name size | `LaneChipNameSize` | 34 px, `FontDisplay` semibold, 0.03 em letter spacing, in the frog's colour |
| Lane chip name outline | `LaneChipNameOutline` | 5 px `Ink` stroke behind the letters |
| Lane chip pad-count size | `PlayerChipPadCountSize` | 24 px, `FontBody` extra-bold, `Moss` |
| Room a lane chip's name has | `LaneChipNameColumn` | 208 px, fixed |
| **Dialog chip** width | — | grows to fit: `PlayerChipLabelPaddingX` + `PlayerChipFrogSize` + `PlayerChipFrogGap` + the name + `PlayerChipLabelPaddingX`, plus the outline |
| Dialog chip fill | `White` | `#FFFFFF` (`ActiveFill` when active) |
| Dialog chip padding, each side | `PlayerChipLabelPaddingX` | 20 px |
| The frog in a dialog chip | `PlayerChipFrogSize` | 108 × 80 px |
| Gap between the frog and the name | `PlayerChipFrogGap` | 24 px |
| Dialog chip name size | `PlayerChipLabelSize` | 32 px, `FontDisplay` semibold, `Ink` |

**`LaneChipNameColumn` is 208 px, and it is the same 208 px on the active
chip.** `PlayerChipWidth` 256 − 2 × `OutlineChip` 4 − 2 × `LaneChipPaddingX` 20
= 208. On the active chip the outline is 7 px, so the padding is 17 px:
256 − 2 × 7 − 2 × 17 = 208.

The design drawing put the name at 40 px with 24 px padding and said ten
letters fit in 208 px. Counting the outline, that column was really 200 px, and
at 40 px an ordinary ten-letter name came to 205–208 px. Derek settled it on
[#526](https://github.com/derekwinters/connor-multiplying-frogs/issues/526):
34 px, 20 px padding, a fixed 208 px column, and the ellipsis for anything that
still does not fit.

Measured with the committed `Fredoka-Variable.ttf` at 34 px, SemiBold (600),
with 0.03 em between letters:

| Name | Width | In 208 px? |
| --- | --- | --- |
| `Orange`, the longest default | 118 px | fits |
| `Maximilian` | 174 px | fits |
| `Christophe` | 176 px | fits |
| `Alexandria` | 177 px | fits |
| `Gwendolynn` | 198 px | fits |
| `mmmmmmmmmm` | 282 px | truncates |
| `WWWWWWWWWW` | 333 px | truncates |

So every ordinary ten-letter name fits whole, and only a name built from the
widest letters reaches the ellipsis.

**A dialog chip has no column to overflow.** It is as wide as its contents. The
widest it can be is with a ten-character name of wide letters: about 305 px of
name at 32 px, so 20 + 108 + 24 + 305 + 20 + 8 = **485 px** — inside every
dialog that holds one. A typical name comes to about 330 px of chip.

##### The constants this component used to carry

| Was | Is now | Why |
| --- | --- | --- |
| `PlayerSwatchDiameter` 64 px | `PlayerChipFrogSize` 108 × 80 px | The frog replaces the dot ([PS-10](style-guide.md#ps-10)) |
| `PlayerChipSwatchGap` 24 px | `PlayerChipFrogGap` 24 px | Renamed with the thing it is a gap to; same value |
| `PlayerChipRadius` 20 px | `PlayerChipRadius` 48 px | Chips are fully round in the Pond style |
| `PlayerChipActiveRing` 6 px | `OutlineChipActive` 7 px | Active is a thicker outline, not a ring drawn round the chip |
| `PlayerChipLabelColumn` 128 px | `LaneChipNameColumn` 208 px (lane); none (dialog) | The lane chip lost its swatch, so the name has the whole inside; the dialog chip grows instead |
| `PlayerChipLabelSize` 32 px on the board | `LaneChipNameSize` 34 px | The lane chip's name is the only thing in it now, and can be a little bigger — 34 px is the size at which ordinary ten-letter names fit ([#526](https://github.com/derekwinters/connor-multiplying-frogs/issues/526)) |

The old label-column arithmetic is worth keeping as the reason the lane chip
changed shape: with a 64 px swatch in it, the name had 128 px, and `Orange` at
32 px overflowed by 4 px — the game's own longest default name, overflowing
before anybody had typed anything. Dropping the swatch on the board, where the
frog is already in the lane, gives the name the whole chip.

`PlayerChipLabelSize` itself was 40 px on this page once before, and was
corrected to 32 px to match the mockups' shared stylesheet. It stays 32 px — on
the dialog chip, which is where it now applies.

#### 4. States

| State | Lane chip | Dialog chip |
| --- | --- | --- |
| Default | Name in the frog's colour, pad count `3 of 8` under it, `Paper` fill, `OutlineChip` | Frog and name, `White` fill, `OutlineChip` |
| Active (this player's turn) | `OutlineChipActive` and `ActiveFill` | `OutlineChipActive` and `ActiveFill` |
| Home (frog has finished) | Pad count replaced by `Home!` | No change — a dialog chip carries no pad count |

The Default row said "colour name" until names became editable, and "swatch"
until the [Pond style](style-guide.md). It is the same row: the chip has always
drawn the frog's name, and until
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310) a
frog's name was always its colour's.

**Active still has callers, checked rather than assumed.**
[#326](https://github.com/derekwinters/connor-multiplying-frogs/issues/326)
removed the one Active chip in the game board's *header* and asked whether the
state still earns its place on this component. It does:

| Screen | Where the Active chip is |
| --- | --- |
| [Game board](game-board.md) | the active frog's **lane** chip, in the pond |
| [Roll and card](roll-and-card.md) | the `whose` region, beside `rolled` — until it is redesigned |
| [Working-out grid](working-out-grid.md) | the `header`, beside `Work it out` |
| [Answer result](answer-result.md) | **none** — a Default dialog chip for the frog that just answered |

On the board the lane chip makes a header chip redundant; in the dialogs there
are no lanes, so the chip is what carries the frog there.

The Active row used to be *"`PlayerChipActiveRing` ring, label at full
weight"*, and before that it dropped a "filled background" the mockups never
drew. The Pond style brings a fill back on purpose — the pale `ActiveFill` — and
replaces the ring with a heavier outline, so the active chip is told apart by
outline weight first and fill second, and neither is a colour that names a
frog.

The Home row dropped "a small home marker after the name": no committed
mockup has ever drawn one — the only rendered Home chip
(`game-board.html`'s Blue) is just the name and `Home!`.
Building a marker now would mean inventing a visual for it, which
[rule 8](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
(wireframe before UI code) exists to prevent. A marker, if wanted later, needs
its own wireframe first.

There is no longer an Empty-seat state on this page.
[Game setup](game-setup.md) does not use this chip — it specifies its own
Frog seat element, with its own constants and states, and its mockup draws
that element's numbers, not this one's. See that page for the setup screen.

[Game over](game-over.md) does not use this chip either, for the same
reason: it specifies its own **Standings row** element — its own constants
(`StandingsFrogSize` 150 × 120 px, `StandingsNameSize` 52 px, no
`PlayerChip*` constant anywhere on that page). It shows the same frog renders
this chip does, under its own size.

#### 5. Behaviour

The chip is a readout on every screen that uses it. It has no button state in
v0.2 — no tap handler, on any screen, in any state.

#### Why there is now typing

A frog's name can be typed, on [game setup](game-setup.md) and nowhere else.
This reverses a rule this page used to carry, and the old reasoning is kept
below rather than deleted, because it was right about what it predicted.

**What this page used to say:**

> Four players share one tablet, and the setup screen is the first thing they
> touch. Asking four children to each type a name means a soft keyboard, a
> misspelling, an editing flow, and — reliably — somebody typing something rude
> about somebody else. Frogs are identified by colour, exactly as the classroom
> pieces are, and the whole of setup is tapping the colour you want.

Derek asked for editable names anyway, in
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310),
and per [CLAUDE.md](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)
an explicit instruction from Derek beats the docs. Three of the four costs that
paragraph predicts are simply accepted — the keyboard, the misspellings and the
editing flow **are** the feature, and they are specified on the setup page
rather than worked around.

The fourth is worth a sentence, because it is the one that sounds like a safety
question. The app is fully offline, with no accounts and no network, so a rude
name reaches exactly the four children already sitting at the table who could
have said it out loud. That is a different thing from a name other people see,
and it is why this is a taste call rather than a safety one.

**What survives untouched** is the accessibility rule, in a rewritten form: a
frog is still identified by a word and never by colour alone. See the
invariants above. That rule was never about the word being a colour.

This remains a **presentation** decision under
[ADR-0001](../../adr/0001-rules-sacred-presentation-ours.md). The cardboard
pieces have no names; the game they came from does not care what a player is
called, and neither does any rule in it.

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

| Frog | Constant | Value |
| --- | --- | --- |
| Green | `FrogGreen` | `#3E933E` |
| Blue | `FrogBlue` | `#4273B8` |
| Orange | `FrogOrange` | `#D38231` |
| Pink | `FrogPink` | `#D41C78` |

**These four are still placeholders for the real palette**, which is an
`area:art` decision that lands with the frog sprites. What changed on
[#301](https://github.com/derekwinters/connor-multiplying-frogs/issues/301) is
that they are no longer *arbitrary* placeholders: they were **derived** to clear
[the game board's separability bar](game-board.md#keeping-the-frogs-visible)
against the pond's three surfaces, rather than picked as four plausible hues and
checked afterwards.

| Was | Is now | Why |
| --- | --- | --- |
| `#3F8E4F` | `#3E933E` | the pond moved; see below |
| `#2C6DAF` | `#37609A` | |
| `#D2762B` | `#D38231` | |
| `#C24C86` | `#D41C78` | |

**`FrogBlue` moved again on
[#469](https://github.com/derekwinters/connor-multiplying-frogs/issues/469)**,
from `#37609A` to `#4273B8`: the same hue, lightened slightly. The approved 3D
log's bark is lighter than `LogBrown`, and against its lightest bark the old
blue measured 1.49 : 1, below the bar. Derek chose to move the frog rather than
darken the log; the new blue is the smallest step that clears it (1.98 : 1).
[Keeping the frogs visible](game-board.md#keeping-the-frogs-visible) has the
measurements.

Two things follow, and both matter more than the hex codes.

**The frogs are now allowed to move when the pond does.** `game-board.md` used
to say *"the surface moves, not the frog"*; Derek reversed that on #301, because
with the surfaces he had picked **no set of four frog colours existed at all**.
The reversal is recorded on that page. When the real palette arrives it still
has to clear the bar, and the arithmetic that constrains it is
[written out there](game-board.md#how-the-ponds-colours-are-constrained) — it is
narrow, and a palette chosen without reading it will not fit.

**The third invariant above is still not satisfied, and since #469 it is
further from it.** The four stepped **1.28 : 1** apart in lightness at worst
after #301, already short of "distinguishable by lightness alone". Lightening
`FrogBlue` to clear the log put it at almost the same lightness as `FrogPink`:
those two are now **1.03 : 1** apart, so a player who cannot tell blue from pink
by hue has next to no lightness difference to go on. That is the cost of the
log's lighter bark — it raises the floor every frog must clear, and blue and
pink both sit just above it. The invariant and the separability bar are in genuine tension; neither
is wrong, and no set satisfies both today. Said here rather than left for
somebody to find by measuring.

#### 4. States

A frog colour has no states. It is used as a fill by the components that do.

#### 5. Behaviour

None. It is a value.

### Pile card

#### 1. What it is

One of the three piles a turn draws from, drawn as a card: face down it says
**which pile** it is, and turned over it carries **the problem** that was drawn.
The same card is used three times on one panel — Easy, Medium and Hard — which
is why it is specified here and not on a screen page.

Used by: the turn panel
([#402](https://github.com/derekwinters/connor-multiplying-frogs/issues/402),
not yet written), which replaces [roll and card](roll-and-card.md) and the
separate [working-out grid](working-out-grid.md) dialog with one panel. Where the
three cards sit on that panel, and what the two undrawn cards do, is that page's
question; this section only fixes the card.

Mockup: [`pile-card.html`](mockups/pile-card.html) — the three backs in both
orientations, the drawn card picked up, and the front carrying `331 × 41` at its
full covering size.

The design was settled by Derek with interactive prototypes after the issue was
written, and it differs from the issue in two ways, both his decisions:

- **No rings.** The issue asked for one, two or three rings round the back. The
  back is instead a little pond — **two lily pads and a log** — with a die face
  on each pad and the pile's name carved on the log. The die faces already say
  which pile it is; the name says it again in words.
- **The reveal is two steps, not one motion.** The drawn card is first **picked
  up** where it lies, and only then flips, turns and grows. The flip, turn and
  grow are still one motion.

#### 2. Invariants

**Invariant:** the card never stretches. The turned-over card is the face-down
card rotated a quarter turn and scaled by exactly `L / S`, the same in both
directions, so the card's shape is fixed by the space it covers — see
[the proportions](#the-proportions-are-derived-not-chosen).
**Invariant:** `L² = 3S² + 2GS` holds exactly, in integers, for the numbers in
the table. A set of numbers that only nearly satisfies it is a mockup of an
animation that cannot land.
**Invariant:** the turned-over card covers the footprint of all three piles
exactly — the three cards and the two gaps between them — and the two cards that
were not drawn stay where they are, underneath it.
**Invariant:** a die face on a card is drawn as **pips**, never as a numeral, in
the same arrangement as the die on the panel.
**Invariant:** every pile card is marked by its two die faces **and** its name,
together. Easy shows 1 and 2, Medium 3 and 4, Hard 5 and 6, and the words are
exactly `Easy`, `Medium` and `Hard`. A child who cannot yet read the name has the
pips; a child who has not learned the mapping has the name.
**Invariant:** a card's look is the same every time. The lily pads are turned
by a fixed amount per pile so that the three cards do not look stamped, never by
a random amount.
**Invariant:** the front names the drawn pile too, so the pile a problem came
from stays on screen for the whole time the player is solving it.
**Invariant:** nothing on the card implies one pile is worth more. There is no
score, star or bonus on any card — every pile is worth the same one lily pad.

#### Requirements

Each is one behaviour, numbered so a build issue and its test can name it.

- **PC-1 — The back is a pond.** <a id="pc-1"></a> The back is filled with the
  pond-water texture, tiled `PileCardWaterTileSize`, over `PondWater`, inside an
  `Ink` outline of `PileCardBorderWidth` at `PileCardRadius`, and drops
  `ShadowPileCard`. Everything on it is clipped to the inside of the outline.
- **PC-2 — Two pads, notches outward.** <a id="pc-2"></a> Two lily pads of
  `PileCardPadSize` stand side by side, `PileCardPadGap` apart, centred across
  the card, `PileCardPadTop` from its top. The left pad is the lily pad render
  `A` turned 180° plus the pile's `PileCardPadTurn`; the right pad is render
  `B` turned by `PileCardPadTurn` alone, so the two notches face away from each
  other. Each pad has a `PileCardPadOutline` and drops `ShadowPileCardPad`
  straight down on screen, whatever the pad's turn.
- **PC-3 — A die face on each pad.** <a id="pc-3"></a> A white die face of
  `PileCardDieFaceSize`, centred on each pad, shows the lower of the pile's two
  faces on the left pad and the higher on the right. Pips are
  `PileCardDiePipDiameter`, on a three-by-three grid `PileCardDiePipPitch` apart.
- **PC-4 — The name on a log.** <a id="pc-4"></a> A log of `PileCardLogWidth`
  by `PileCardLogHeight`, centred across the card and `PileCardLogBottom` from
  its bottom, carries the pile's name carved in `LogCarve` at
  `PileCardNameSize`. The log is drawn from the bark texture with a cut end at
  its right, outlined in `Ink`.
- **PC-5 — The front.** <a id="pc-5"></a> The front is `White`, in the same
  outline and corner as the back. The problem is written the way the classroom
  cards write it: the two numbers stacked and right-aligned, `×` before the
  second, and a rule under both as wide as the wider line. A small log under
  the problem carries the drawn pile's name.
- **PC-6 — Picked up first.** <a id="pc-6"></a> The drawn card grows to
  `PileCardLiftScale` about its own centre, where it lies, still face down, over
  `PileCardLiftDuration` with `PileCardLiftEasing`, and its shadow deepens to
  `ShadowPileCardLifted`. It rests there for `PileCardLiftHold`.
- **PC-7 — Then turned over.** <a id="pc-7"></a> Over
  `PileCardRevealDuration`, with `PileCardRevealEasing`, as **one** motion, the
  card flips over about its own vertical axis, turns a quarter turn clockwise,
  grows from `PileCardLiftScale` to `L / S`, and moves so its centre lands on the
  centre of the three piles' footprint. It ends there, front up, covering the
  footprint exactly.

#### 3. Named constants

**Every number here is in card units:** pixels on the 1920 × 1200 canvas
while the card is face down at rest. The turned-over card is the same card at
scale `L / S` = 9/5, so on screen each of its numbers is 1.8 times its value.
That is why the front's table gives both. Every position is measured from the
card's **outer** edge, outline included.

##### The card and its proportions

| Element | Constant | Value |
| --- | --- | --- |
| Short side, `S` | `PileCardShortSide` | 225 |
| Long side, `L` | `PileCardLongSide` | 405 |
| Gap between piles, `G` | `PileCardGap` | 27 |
| Scale of the turned-over card | `PileCardRevealScale` | `L / S` = 9/5 = 1.8 |
| Corner radius | `PileCardRadius` | 26 (46.8 px once turned over) |
| Outline | `PileCardBorderWidth` | 6, `Ink` (10.8 px once turned over) |
| Shadow, face down | `ShadowPileCard` | 10 straight down, `Ink` at 35 %, no blur |
| Shadow, picked up | `ShadowPileCardLifted` | 22 straight down, `Ink` at 30 %, no blur |

These are the values for the chosen layout, **three landscape cards in a column
beside the die**. The other layout, which #402 still draws for comparison, has
[its own set](#if-the-piles-sit-above-the-die).

##### The back

| Element | Constant | Value |
| --- | --- | --- |
| Water fill | `PondWater` and `PileCardWaterTileSize` | The pond-water texture, one tile 256 across, from the card's top-left corner, over `#9FD8F2` |
| Lily pad, square | `PileCardPadSize` | 128 |
| Gap between the two pads | `PileCardPadGap` | 41 |
| Left edge to the first pad | `PileCardPadLeft` | 54 — `(L − 2 × 128 − 41) / 2`, so the pair is centred |
| Top edge to both pads | `PileCardPadTop` | 2 — the outline clips the top of each pad, as if it runs under the card's edge |
| The pads' turn | `PileCardPadTurn` | Easy 15° anticlockwise, Medium 20° clockwise, Hard 30° anticlockwise |
| Pad outline | `PileCardPadOutline` | 3, `Ink`, round the pad's own shape |
| Pad shadow | `ShadowPileCardPad` | 6 straight down, `Ink` at 30 % |
| One die picture, square | `PileCardDieFaceSize` | 62, `White`, centred on its pad |
| Gap between the two die pictures | `PileCardDieFaceGap` | 107 — `PileCardPadGap` + `PileCardPadSize` − `PileCardDieFaceSize`, because each is centred on its pad |
| Die picture corner | `PileCardDieFaceRadius` | 12 |
| Die picture outline | `PileCardDieFaceOutline` | 4, `Ink` |
| One pip | `PileCardDiePipDiameter` | 11, `Ink` |
| Pip centre to pip centre | `PileCardDiePipPitch` | 14; the outer pips' centres are 17 from the die picture's edge, so 17 + 14 + 14 + 17 = 62 |
| Log body | `PileCardLogWidth` × `PileCardLogHeight` | 256 × 58 |
| Bottom edge to the log | `PileCardLogBottom` | 18 |
| Log corner | `PileCardLogRadius` | 22 |
| Log outline | `PileCardLogOutline` | 5, `Ink` |
| The log's cut end | `PileCardLogEndWidth` | An ellipse 34 wide and as tall as the log, outlined like it, standing 5 past the log's right end. The log and its end together are 261 wide, so the log starts 72 from the card's left edge and the pair is centred. |
| Log shadow | `ShadowPileCardLog` | 6 straight down, `Ink` at 30 % |
| Pile name | `PileCardNameSize` | 38, `FontDisplay` semibold, 0.03 em letter spacing, `LogCarve` |
| The ink round the name | `PileCardNameOutline` | 6 `Ink` stroke drawn behind the letters, so 3 shows outside them |

The pads are the lily-pad renders committed at
[`images/pile-card/lily-pad-a.png`](images/pile-card/lily-pad-a.png) and
[`lily-pad-b.png`](images/pile-card/lily-pad-b.png) — the same pad with two
widths of notch, notch pointing right at 0°. The water is
[`water.png`](images/pile-card/water.png) and the bark
[`log-bark.jpg`](images/pile-card/log-bark.jpg), tiled along the log at the
log's height. They are documentation images, like the
[frog renders](style-guide.md#the-frog-renders); how the shell gets them is the
build issue's question. The rings of the log's cut end are part of the log's
art, not colours the view draws.

A turn is written clockwise-positive here because that is how it reads on the
screen. Unity's Z rotation is anticlockwise-positive, so the code's values are
the negatives: `+15`, `−20`, `+30`.

| Pile | Left pad | Right pad | Die pictures | Name |
| --- | --- | --- | --- | --- |
| Easy | `A`, 165° clockwise | `B`, 15° anticlockwise | 1, 2 | `Easy` |
| Medium | `A`, 200° clockwise | `B`, 20° clockwise | 3, 4 | `Medium` |
| Hard | `A`, 150° clockwise | `B`, 30° anticlockwise | 5, 6 | `Hard` |

The pips sit on a three-by-three grid, numbered 0 to 8 across from the top
left, and are the same as the panel's die: 1 → centre; 2 → top left, bottom
right; 3 → those and the centre; 4 → the four corners; 5 → the corners and the
centre; 6 → the left and right columns. A pip is
`PileCardDiePipDiameter` / `PileCardDieFaceSize` = 11 / 62 ≈ 0.18 of its die
picture, the same proportion as the panel's die in the agreed prototype
(46 / 260), so the card's dice read as small copies of it.

##### The front

The front is the back's other side: `S` wide and `L` tall once it has turned,
which is `405 × 729` on screen. Positions are from the turned-over card's outer
edge.

| Element | Constant | Card units | On screen |
| --- | --- | --- | --- |
| Fill | — | `White` | — |
| The problem's text size | `RevealedCardProblemSize` | 60 | 108 px, `FontDisplay` semibold, tabular figures, `Ink` |
| Line to line | `RevealedCardProblemLinePitch` | 64 | 115.2 px |
| Top edge to the first line | `RevealedCardProblemTop` | 80 | 144 px |
| Right edge to the problem | `RevealedCardProblemRight` | 50 | 90 px |
| The rule's thickness | `RevealedCardRuleThickness` | 5 | 9 px, `Ink`, round ends |
| Gap above the rule | `RevealedCardRuleGap` | 5 | 9 px |
| Log body | `RevealedCardLogWidth` × `RevealedCardLogHeight` | 134 × 35 | 241.2 × 63 px |
| Bottom edge to the log | `RevealedCardLogBottom` | 20 | 36 px |
| Log corner, outline | — | 12, 3 | 21.6 px, 5.4 px |
| The log's cut end | — | 20 wide, standing 3 past the log | 36 px, 5.4 px |
| Pile name | `RevealedCardNameSize` | 20, with a 3 stroke behind | 36 px, 5.4 px |
| Shadow | — | none: the turned-over card lies flat | — |

The log and its end together are 137 wide, so the log starts 44 from the left
edge and the pair is centred.

**The problem gets bigger when it is turned over, not smaller.** At 108 px,
`331` is 165 px wide and `× 41` 178 px, measured in the committed Fredoka, so
the widest problem in the game sits right-aligned, its left edge 137 px in
from the card's left edge. Every problem fits: no pile deals anything wider than three digits over
`×` and two.

##### The motion

| Element | Constant | Value |
| --- | --- | --- |
| How big the drawn card grows when picked up | `PileCardLiftScale` | 1.3 |
| How long that takes | `PileCardLiftDuration` | 0.3 s |
| Its easing | `PileCardLiftEasing` | `cubic-bezier(0.34, 1.4, 0.64, 1)` — a small overshoot, as if picked up |
| How long it rests, picked up | `PileCardLiftHold` | 0.15 s |
| The flip, the quarter turn and the growth, as one motion | `PileCardRevealDuration` | 0.7 s |
| Its easing | `PileCardRevealEasing` | `cubic-bezier(0.3, 0.7, 0.25, 1)` — quick away, slow to land, no overshoot |
| The flip | `PileCardRevealFlip` | 180° about the card's own vertical axis |
| The quarter turn | `PileCardRevealTurn` | 90° clockwise, in the screen's plane |
| Perspective for the flip | `PileCardFlipPerspective` | 2400 px from the canvas |

#### The proportions are derived, not chosen

Call the card's short side `S`, its long side `L`, and the gap between piles `G`.
Three landscape cards stacked in a column, `G` apart, cover `L` across and
`3S + 2G` down. Turning one card a quarter turn makes it `S` across and `L` down;
growing it by `L / S` makes it `L` across and `L² / S` down. The width matches
on its own. The height matches only if

```
L² / S = 3S + 2G,  that is,  L² = 3S² + 2GS
```

**`S` and `G` were chosen; `L` follows.** The prototype Derek agreed had
`S = 223`, `L = 400`, `G = 24`, which is close but not exact:
`3 × 223² + 2 × 24 × 223 = 159 891`, not `400² = 160 000`, so its revealed card
was 717.49 px long against a 717 px footprint. The nearest set that comes out exactly in
whole pixels is `S = 225`, `G = 27`:

```
3S² + 2GS = 3 × 225² + 2 × 27 × 225
          = 3 × 50 625 + 12 150
          = 151 875 + 12 150
          = 164 025
          = 405²,  so L = 405
```

Check: the column is `3 × 225 + 2 × 27 = 729` tall and `405` wide. The card
turned and grown by `405 / 225 = 9/5` is `225 × 9/5 = 405` wide and
`405 × 9/5 = 729` tall — exactly the column. The drawn card's centre moves by
`S + G = 252` (down for Easy, none for Medium, up for Hard).

Choosing `S` and `G` rather than `L` keeps the scale a simple fraction, which is
what makes every number on the front exact: 9/5 times a whole number is never
more than one decimal place.

#### If the piles sit above the die

#402 still draws the other layout — three **portrait** cards in a row above the
die, turning over into one **landscape** front — so Derek can compare. It was
not chosen: its cards are less than a third of the area, and the die pictures
come out at 38 with 7 px pips. If #402 picks it after all, these replace the
chosen values; otherwise nothing here is built.

| Element | Constant | Value |
| --- | --- | --- |
| `S`, `L`, `G` | `PileCardShortSide`, `PileCardLongSide`, `PileCardGap` | 125, 225, 15 |
| Check | — | `3 × 125² + 2 × 15 × 125 = 46 875 + 3 750 = 50 625 = 225²`; the row is `3 × 125 + 2 × 15 = 405` by `225`, and the card at `225 / 125 = 9/5` is `405 × 225` |
| Pads | `PileCardPadSize`, left, tops | 78, at 23 from the left, 2 and 88 from the top — stacked, not side by side, with the same turns as the landscape back (top pad is `A`) |
| Die pictures | `PileCardDieFaceSize`, corner, outline | 38, 8, 3 |
| Pips | `PileCardDiePipDiameter`, pitch | 7, 9 apart; outer centres 10 from the edge |
| Log | width × height, from the bottom, corner, outline | 106 × 34, 14, 14, 4 |
| Log's cut end | — | 18 wide, standing 3 past the log; the pair starts 8 from the left |
| Name | `PileCardNameSize`, stroke | 20, 4 |

The outline, corner, shadows and motion are the chosen layout's. The landscape
front this layout turns into is drawn on #402 if it is chosen; this section does
not specify it, because nobody has agreed it.

#### 4. States

| State | What it looks like |
| --- | --- |
| Face down | The back, `ShadowPileCard`. All three cards, before the roll. |
| Picked up | The drawn card only: the back at `PileCardLiftScale`, `ShadowPileCardLifted`, drawn above the other two |
| Turning over | Partway through [PC-7](#pc-7). The back is hidden once it faces away; the front shows from then on. |
| Turned over | The front, covering the footprint. Stays for as long as the player is solving. |
| Not drawn | The two other cards: face down, unmoved, under the turned-over card |

The lime glow that marks the matched pile between the die settling and the card
being picked up is the turn panel's, not the card's — its colour, width and hold
are on #402.

#### 5. Behaviour

- The card does not take input. It is never a button: the roll decides which
  card is drawn, and the player never chooses one.
- It is told which pile it is and, once drawn, which problem to show. It never
  draws a problem itself; that is Core's.
- The pick-up and the turn-over run once per turn, in that order, with
  `PileCardLiftHold` between them. Whether a tap can skip them is the turn
  panel's question, on #402.

#### Open questions

None. The issue's two are settled:

- **Is the pile named in words on the card?** Yes — `Easy`, `Medium` or `Hard`,
  carved on the log on the back, and again on a smaller log on the front so the
  drawn pile stays named while the player works. Derek's decision, made with the
  prototype.
- **Are three rings distinguishable from two at arm's length?** No longer a
  question: there are no rings. The pile is told apart by its two die faces and
  its name.
