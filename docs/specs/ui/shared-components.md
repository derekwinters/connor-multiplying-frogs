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

The app's background is the mockups' `--bg`, `#EDF1EF`. The title screen, game
setup and game over paint it, and it is what the camera clears to.

**The game board is the exception, and the only one.** It paints
[`PondWater`](game-board.md#colours) instead, to the same four edges by the same
rule, because that screen is a pond rather than a page. A screen may have a
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
| Border width, outlined kinds | `ButtonBorderWidth` | 4 px |
| Opacity while disabled | `ButtonDisabledOpacity` | 0.40 |

#### 4. States

| State | Appearance |
| --- | --- |
| Default (primary) | Filled accent, light label |
| Default (secondary) | Outlined, `ButtonBorderWidth` 4 px, dark label, no fill |
| Default (destructive) | Outlined in the warning colour, warning-coloured label |
| Pressed | Moves down by `ButtonPressOffset`; fill darkens |
| Disabled | `ButtonDisabledOpacity` opacity, no press response |
| Hidden | Not laid out at all — buttons do not leave gaps behind |

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
`ButtonDisabledOpacity`, not a distinct grey style. The mockups should be
redrawn to match the next time they are touched.

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

Used by: [game board](game-board.md), [roll and card](roll-and-card.md),
[answer result](answer-result.md).

#### 2. Invariants

**Invariant:** a frog is identified by its colour **and** a word, always
together, and never by colour alone. The word is the frog's name: its colour's
name by default, or whatever was typed on
[game setup](game-setup.md) instead. Colour alone excludes a colour-blind
player from knowing whose turn it is, in a game where four players share one
screen.
**Invariant:** the chip for the player whose turn it is is visibly different
from every other chip on screen, by something other than colour.
**Invariant:** nothing is ever appended to a frog's name. The chip shows the
name and only the name — not `Blue frog`, not `Blue (you)`.
**Invariant:** the chip never refuses or alters a name it is given; if a name
does not fit, the chip truncates it with an ellipsis. A readout is not where a
limit is enforced. The limit is `PlayerNameMaxLength`, and
[game setup](game-setup.md#where-playernamemaxlength-comes-from-and-why-a-count-is-not-enough)
is where it is enforced.

The identification invariant was previously worded as colour **and its
colour's name** — literally the colour word. That letter cannot survive a
rename: a chip reading `Connor` beside a blue swatch says "Blue" nowhere. Its
purpose survives intact, because a typed name is also a word, and the reason
the rule exists is that a colour-blind player needs something other than the
swatch to read. So the rule now requires a word rather than that specific word.

**Two frogs may end up with the same word**, which is the one case where the
identifying word stops identifying. Nothing prevents it. They are two children
sitting next to each other who chose the same name on purpose, and they can
sort it out; the swatch still tells them apart, and colour-plus-word is still
what the chip shows.

#### 3. Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Chip height | `PlayerChipHeight` | 96 px |
| Chip width on the board | `PlayerChipWidth` | 256 px |
| Frog swatch diameter | `PlayerSwatchDiameter` | 64 px |
| Gap between swatch and label | `PlayerChipSwatchGap` | 24 px |
| Label size | `PlayerChipLabelSize` | 32 px |
| Pad-count text size | `PlayerChipPadCountSize` | 24 px |
| Corner radius | `PlayerChipRadius` | 20 px |
| Ring drawn around the active chip | `PlayerChipActiveRing` | 6 px |
| Horizontal padding inside the chip | `PlayerChipLabelPaddingX` | 20 px |
| Room the name actually has | `PlayerChipLabelColumn` | 128 px |

`PlayerChipLabelSize` was 40 px on this page; the committed mockups' shared
stylesheet — repeated across all eleven files — draws the name at 32 px and the
pad-count line separately at 24 px, and `game-board.html`'s five instantiated
chips (covering all three states below) all render at those two sizes. The
mockups are what Connor approved when each screen's wireframe was signed off,
so they win: `PlayerChipLabelSize` is corrected to 32 px, and
`PlayerChipPadCountSize` — a value this table previously had no name for at
all — is added at 24 px.

`PlayerChipLabelPaddingX` and `PlayerChipLabelColumn` distil the next
paragraph's own arithmetic into the names the code declares them under — the
same move [game setup](game-setup.md#named-constants) made for
`SeatBadgeInset`. The padding was already stated in prose here, and the column
is what truncation is measured against, so it cannot stay a number that only
exists inside a sentence.

**The label column is 128 px, and it is tighter than it looks.**
`PlayerChipWidth` 256 less 20 px padding either side, less
`PlayerSwatchDiameter` 64, less `PlayerChipSwatchGap` 24, leaves 128 px for the
name and the pad count. The pad count fits easily at 24 px. The name often does
not: `Orange` renders at 132 px at `PlayerChipLabelSize` 32 and overflows by
4 px — the game's own longest default name, overflowing before anybody has
typed anything. This is why the chip truncates rather than refuses, per the
invariant above, and why `PlayerNameMaxLength` is derived from the setup seat
instead of from here. Widening the chip is not free: `PlayerChipWidth` is part
of the [game board](game-board.md)'s lane arithmetic, so it is a board layout
change and would need its own wireframe.

#### 4. States

| State | Appearance |
| --- | --- |
| Default | Swatch, name, pad count |
| Active (this player's turn) | `PlayerChipActiveRing` ring, label at full weight |
| Home (frog has finished) | Pad count replaced by `Home!` |

The Default row said "colour name" until names became editable. It is the same
row: the chip has always drawn the frog's name, and until
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310) a
frog's name was always its colour's.

**Active still has callers, checked rather than assumed.**
[#326](https://github.com/derekwinters/connor-multiplying-frogs/issues/326)
removed the one Active chip in the game board's *header* and asked whether the
state still earns its place on this component. It does, in four places, and
three of them are not the board:

| Screen | Where the Active chip is |
| --- | --- |
| [Game board](game-board.md) | the active frog's **lane** chip, in the pond |
| [Roll and card](roll-and-card.md) | the `whose` region, beside `rolled` |
| [Working-out grid](working-out-grid.md) | the `header`, beside `Work it out` |
| [Answer result](answer-result.md) | **none** — it draws a `pad 7 → 8` chip, not an Active one |

So the board's header was never the state's only caller, and dropping it there
changes nothing here. What is worth noticing is the last row: the four screens
of a single turn were already not uniform about this, which is part of why
removing the header chip from the board does not make the turn sequence
inconsistent — it was never consistent, and the chip's job differs by screen. On
the board the lane chip makes a header chip redundant; in the two dialogs there
are no lanes, so nothing else there carries the colour.

The Active row dropped "filled background": the mockups' `.chip.act` rule
gives the ring and the bold weight but leaves the base chip background
unchanged, and the chip invariant only requires the active chip be visibly
different by something other than colour, which the ring and weight already
satisfy without a fill.

The Home row dropped "a small home marker after the name": no committed
mockup has ever drawn one — the only rendered Home chip
(`game-board.html`'s Blue) is just the swatch, the colour name, and `Home!`.
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
(`StandingsSwatchDiameter` 88 px against this chip's `PlayerSwatchDiameter`
64 px, `StandingsNameSize` 52 px, no `PlayerChip*` constant anywhere on that
page) and its mockup draws `.row` markup, not `.chip`. It stays listed under
[Frog colours](#frog-colours) below, since it legitimately reuses that
element's four colour constants for its own swatch.

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
| Blue | `FrogBlue` | `#37609A` |
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

Two things follow, and both matter more than the hex codes.

**The frogs are now allowed to move when the pond does.** `game-board.md` used
to say *"the surface moves, not the frog"*; Derek reversed that on #301, because
with the surfaces he had picked **no set of four frog colours existed at all**.
The reversal is recorded on that page. When the real palette arrives it still
has to clear the bar, and the arithmetic that constrains it is
[written out there](game-board.md#how-the-ponds-colours-are-constrained) — it is
narrow, and a palette chosen without reading it will not fit.

**The third invariant below is still not satisfied.** These four step
**1.28 : 1** apart in lightness at worst. That is better than the values they
replace, which stepped **1.11 : 1** — four mid-tones, exactly what the invariant
says the set is not — but it is not "distinguishable by lightness alone" in any
strong sense, and it is the most the available band allows once four colours
share it. The invariant and the separability bar are in genuine tension; neither
is wrong, and no set satisfies both today. Said here rather than left for
somebody to find by measuring.

#### 4. States

A frog colour has no states. It is used as a fill by the components that do.

#### 5. Behaviour

None. It is a value.
