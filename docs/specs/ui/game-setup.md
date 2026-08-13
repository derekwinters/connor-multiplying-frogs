# Game setup

Pick which frogs are playing and what they are called. Tap a frog to put it in
the game, tap its name to change it, then start.

## Invariants

**Invariant:** a game cannot start with fewer than two frogs or more than four.
The cap of four is a recorded rule change from the classroom game's 2–8 — see
[future ideas](../future-ideas.md).
**Invariant:** the keyboard on this screen is one the game draws, never the
Android system keyboard. A keyboard whose height is not knowable at design time
cannot be drawn in a mockup, and a screen nobody can draw is a screen nobody can
review — see [the keyboard](#the-keyboard) below.
**Invariant:** every seat always shows a word. A seat that is playing shows its
name — the colour name until somebody changes it — and never its colour alone.
This is the [player chip's identification
invariant](shared-components.md#player-chip) applied to the seat.
**Invariant:** a seat that is not playing has no name and cannot be given one.
Naming a frog that is not in the game is a state with no meaning.
**Invariant:** removing a player is a deliberate tap on a target that does
nothing else. There is still no confirm, and there does not need to be one,
because there is no longer a large target that removes a player by accident.
**Invariant:** turn order is the order the frogs are listed in, left to right,
and that order is visible before the game starts. A player should not have to
discover whose turn is next by watching.
**Invariant:** every frog seat is a `MinTouchTarget`-safe target with a very
large margin — these are the biggest tap targets in the game, because they are
tapped by four children at once, reaching across each other.

## Regions

| Region | Job |
| --- | --- |
| `header` | The question: *Who is playing?* — or, while a name is being typed, which frog is being named |
| `seats` | The four frog seats, in a row |
| `hint` | One line saying what to do, and what is stopping you if `Start` is off |
| `controls` | `Back` and `Start` |
| `keyboard` | The letter keys, present only while a name is being typed |

## Anchors

- `header` pinned to the top safe area, centred.
- `seats` centred both ways in the space between `header` and `controls`. Four
  seats, always four, always in the same left-to-right order — an unchosen frog
  is an empty seat, not a missing one, so the row never reflows while you are
  tapping it.
- `hint` sits `HintGap` beneath `seats`, centred.
- `controls` pinned to the bottom safe area: `Back` at the left, `Start` at the
  right, both `SafeMargin` from their edge.
- `keyboard` pinned to the bottom safe area, centred, `NameKeyboardHeight` tall.
  It is laid out over `hint` and `controls` rather than beside them, and both
  are hidden while it is up.

### The screen has two layouts, not one

Everything above describes the screen at rest. While a name is being typed the
keyboard takes the bottom `NameKeyboardHeight` of the canvas, and the seat row
moves up from `SeatRowTop` to `SeatRowEditingTop` to clear it. Nothing else
moves and nothing resizes — the seats are the same seats at the same size, 150
px higher up.

That the seats move at all is the reason question 3 of
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310) had
to be answered before anything was drawn. With the system keyboard the distance
they move is whatever that device's keyboard happens to be, which is not a
number a spec can carry.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin | `SafeMargin` | 48 px |
| Header size | `SetupHeaderSize` | 72 px |
| Seat width | `SeatWidth` | 360 px |
| Seat height | `SeatHeight` | 480 px |
| Gap between seats | `SeatGap` | 48 px |
| Seat corner radius | `SeatRadius` | 32 px |
| Frog swatch in a seat | `SeatSwatchDiameter` | 200 px |
| Seat name size | `SeatLabelSize` | 48 px |
| Ring on a chosen seat | `SeatChosenRing` | 8 px |
| Gap between the seat row and the hint | `HintGap` | 56 px |
| Hint size | `SetupHintSize` | 36 px |
| Turn-order badge diameter | `SeatOrderBadge` | 72 px |
| Space above a seat's swatch, holding the two corner targets | `SeatTopBand` | 136 px |
| Gap between a seat's swatch and the name row below it | `SeatContentGap` | 16 px |
| Turn-order badge inset from the seat's corner | `SeatBadgeInset` | 24 px |
| Remove target diameter | `SeatCornerTarget` | 96 px |
| Remove target inset from the seat's corner | `SeatCornerInset` | 16 px |
| Name row width | `SeatNameRowWidth` | 312 px |
| Name row height | `SeatNameRowHeight` | 96 px |
| Name row corner radius | `SeatNameRowRadius` | 20 px |
| Horizontal padding inside the name row | `SeatNameRowPaddingX` | 16 px |
| Seat row top, at rest | `SeatRowTop` | 300 px |
| Seat row top, while a name is being typed | `SeatRowEditingTop` | 150 px |
| Longest name a player can type | `PlayerNameMaxLength` | 10 |
| Keyboard block height | `NameKeyboardHeight` | 480 px |
| Keyboard block width | `NameKeyboardWidth` | 1664 px |
| Letter key width | `NameKeyWidth` | 152 px |
| Letter key height | `NameKeyHeight` | 108 px |
| Gap between keys | `NameKeyGap` | 16 px |
| Key corner radius | `NameKeyRadius` | 20 px |
| Key label size | `NameKeyLabelSize` | 52 px |
| Space bar width | `NameSpaceKeyWidth` | 1160 px |
| `Done` key width | `NameDoneKeyWidth` | 488 px |
| Fewest frogs a game can start with | `GameSetupMinFrogs` | 2 |
| Most frogs a game can start with | `GameSetupMaxFrogs` | 4 |

Four seats at 360 px with three 48 px gaps is 1584 px, centred in 1920 with
168 px either side. The row is deliberately not full-bleed: the empty space is
what makes it obvious there are four seats and no more.

`SeatBadgeInset` distils what
[`mockups/game-setup.html`](mockups/game-setup.html) already drew: the
turn-order badge sits at a fixed `left:24px; top:24px` inset from the seat's
own corner. `GameSetupMinFrogs` and `GameSetupMaxFrogs` distil the Invariants
section's own prose above ("a game cannot start with fewer than two frogs or
more than four") into the names the code declares them under.

### Why the seat grew from 440 px to 480 px

A seat used to hold a swatch and a line of text. It now holds a swatch, a
96 px name row, and a 96 px remove target — and `MinTouchTarget` sets both of
those 96s, so neither can shrink. At `SeatHeight` 440 the remove target in the
top-right corner overlapped the swatch: a 96 px target inset 16 px reaches
136 px down the seat, and a 200 px swatch centred in what was left started
above that.

Two ways out, and the seat growing is the cheaper one. Shrinking
`SeatSwatchDiameter` would have kept the seat at 440 px by making the frog
smaller, and the frog is the thing a child actually looks at when picking a
colour. 40 px of empty canvas is worth more to the layout than 24 px of frog.

`SeatContentGap` fell from 32 px to 16 px in the same pass, for the same
reason: the name row is a bordered box rather than bare text, so it already
reads as separate from the swatch without a large gap doing that work.

### Where `PlayerNameMaxLength` comes from, and why a count is not enough

Ten characters of ordinary mixed-case text is about 270 px at `SeatLabelSize`
48, and the name row holds 274 px inside its padding. So ten is what the seat
can draw, and [`mockups/game-setup-names-set.html`](mockups/game-setup-names-set.html)
is where that was read off.

**A character count cannot promise a width.** `Mohammed` is eight characters
and 314 px; `Alexander` is nine and 274 px. So the count is the cap the
keyboard enforces, and any surface that still cannot fit the string it is given
truncates it with an ellipsis. On the seat that is rare. On the board's
[player chip](shared-components.md#player-chip) it is the normal case, and it
already was before this issue: the chip's label column is 128 px, and `Orange`
— the game's own longest default name — renders at 132 px in it.

**The cap is not derived from the chip**, which is what
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310)
question 5 proposed. 128 px at `PlayerChipLabelSize` 32 holds about five
characters, so a chip-derived cap would refuse `Connor` at the sixth keystroke.
The chip is a readout that cannot refuse anything anyway; the seat is where the
typing happens and where a refusal is visible. So the seat sets the cap and the
chip truncates.

## Elements

- **Frog seat ×4** — Green, Blue, Orange, Pink, in that order, using
  [frog colours](shared-components.md#frog-colours). Three states:

    | State | What it looks like | What tapping the seat's body does |
    | --- | --- | --- |
    | Empty | Dashed outline, grey swatch, `Tap to play` | Adds this frog; it takes the next free turn-order number and the colour name as its name |
    | Chosen | Filled, `SeatChosenRing` ring, name row, turn-order badge, remove target | **Nothing.** The two things a chosen seat can do each have their own target |
    | Editing | As Chosen, ring in the accent colour, name row replaced by the name field, no remove target | Nothing |

    **A chosen seat's body is inert, and that is the change.** It used to be
    the remove target — "tapping a chosen seat removes it", no confirm. Adding
    an edit target inside a 360 × 480 destructive target means a child aiming
    at the name and missing loses the player, and there is no confirm to catch
    it. Moving removal onto its own target costs a smaller target for a rare
    action, and buys a screen where a mis-tap costs nothing at all.

- **Name row** — on a chosen seat, `SeatNameRowWidth` × `SeatNameRowHeight`,
  below the swatch, showing the frog's name at `SeatLabelSize`. It is the edit
  target: tapping it opens the keyboard on this seat. It is drawn as a field
  rather than as a caption, because it is the one part of a seat that a player
  can change and it has to look like it.
- **Remove target** — on a chosen seat, `SeatCornerTarget` at
  `SeatCornerInset` from the top-right corner, in the warning colour. Removes
  this frog; the badges after it renumber. It is `MinTouchTarget` exactly, and
  it is the only way a player leaves the game.
- **Turn-order badge** — `1`–`4` on a chosen seat, top-left corner. This is the
  only thing on the screen that says what turn order is, and it is why the
  numbers renumber immediately when a frog is removed rather than at start. It
  is a readout, not a target, which is why it is `SeatOrderBadge` 72 px and the
  remove target opposite it is 96 px.
- **Name field** — on the seat being edited, replacing the name row: the text
  typed so far and a caret, in the accent colour.
- **`Start`** — primary [button](shared-components.md#button). Disabled below
  two frogs. Not drawn while the keyboard is up.
- **`Back`** — secondary button. Returns to the [title screen](title-screen.md).
  Not drawn while the keyboard is up.
- **Hint** — `Pick two to four frogs` while `Start` is disabled;
  `<name> goes first` once it is enabled, using the first frog's name — so
  `Connor goes first`, not `Green goes first`, once Green has been renamed. One
  line, always present, so the layout does not jump when it changes. Not drawn
  while the keyboard is up.

### The keyboard

A keyboard **this game draws**, not Android's. Four rows, `NameKeyWidth` ×
`NameKeyHeight` per key, laid out in the block described by the constants
table — the same approach the
[working-out grid](working-out-grid.md) already takes for its digit keypad, and
sized to the same tap-target family as everything else in the game.

| Row | Keys |
| --- | --- |
| 1 | `Q W E R T Y U I O P` |
| 2 | `A S D F G H J K L` |
| 3 | `⇧ Z X C V B N M ⌫` |
| 4 | space, `Done` |

`Done` is the primary [button](shared-components.md#button) kind and the only
way out of the keyboard. There is no cancel: a name is edited in place and
every keystroke has already happened, so there is nothing a cancel would undo
that backspace does not.

**Why not the system keyboard.** It is free and familiar, and it would have
been the smaller change. It also has a height nobody knows at design time,
covering an unknown part of the screen — which means no mockup can honestly
draw this screen, and a screen that cannot be drawn cannot go through the
[wireframe loop](../../engineering/ui-design-process.md#the-loop) that every
other screen in this game went through. The cost is an alphabet to lay out and
build, which is 26 keys of the same key.

## Behaviour

- Entering: seats all empty, every time. The game does not remember the last
  line-up, because it does not remember anything between sessions in v1 and
  because the players at the table are usually different ones. No name survives
  either, for the same reason.
- Seating a frog gives it the bare colour name — `Blue`, not `Blue Frog`. A
  default name is a real name, not a placeholder: it is stored, drawn and
  spoken about exactly like a typed one, and nothing anywhere appends a word to
  it.
- Tapping the remove target removes that frog. There is still no confirm —
  nothing has started yet, and a confirm on an action with no cost teaches
  children to dismiss confirms.
- Tapping a chosen seat's name row opens the keyboard on that seat and puts the
  caret at the end of the name. Only one seat is being edited at a time.
- Typing appends a character. **At `PlayerNameMaxLength` the next keystroke is
  refused** — the key does nothing, the name is unchanged, and nothing explains
  itself, which is how a
  [disabled button](shared-components.md#button) already behaves in this game.
- Backspace deletes the last character. Clearing the name to empty and pressing
  `Done` restores the frog's colour name — a nameless frog is not a state this
  screen can reach.
- `Done` closes the keyboard, puts the seat row back at `SeatRowTop`, and
  brings `hint` and the controls back.
- Hardware back does what `Done` does while the keyboard is up, and what `Back`
  does otherwise. This follows the
  [dialog rule](shared-components.md#dialog) that back does what the least
  destructive button does, `Done` being the only button there is.
- **Two seats may hold the same name.** Nothing prevents it, nothing numbers
  them, nothing warns. The two players are sitting next to each other and can
  sort it out, and a collision flow is machinery for a case nobody has hit yet.
- `Start` begins the game with the chosen frogs in badge order and goes to
  [game board](game-board.md), with frog 1's turn active and nothing rolled.
  Their names go with them, and are what every later screen shows.
- Names last as long as the game does, which includes
  [`Play again`](game-over.md) — that button starts a new game with the same
  frogs in the same turn order without passing through this screen, so it keeps
  their names too. Changing a name means going back to the title screen and
  through setup again, exactly as changing who is playing already does.

## Mockup

Three, all at 1920 × 1200:

| Mockup | What it draws |
| --- | --- |
| [`game-setup-names-set.html`](mockups/game-setup-names-set.html) | **The screen at rest**, with two frogs renamed and one still on its colour name. This is the authoritative at-rest picture. |
| [`game-setup-name-edit-inline.html`](mockups/game-setup-name-edit-inline.html) | **A name being typed**, keyboard up and seats raised. The agreed answer to where the edit target lives. |
| [`game-setup-name-edit-pencil.html`](mockups/game-setup-name-edit-pencil.html) | The **alternative that was considered** — a pencil target for edit, the seat's body still removing. Kept as the record of the comparison, not a live option. |

The two editing files share every number and differ on one thing: which part of
a chosen seat means *edit* and which means *remove*. Derek picked the inline
one. See
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310).

[`mockups/game-setup.html`](mockups/game-setup.html) draws this screen **before
names existed** — no name row, no remove target, and `SeatHeight` at its old
440 px. It is superseded by `game-setup-names-set.html` and kept as the record
of what the screen was, the way
[`working-out-grid.md`](working-out-grid.md) keeps the invariants it used to
carry. Do not build from it.

## Open questions

- **Does a saved game change this screen?** **Answered: no.**
  [#228](https://github.com/derekwinters/connor-multiplying-frogs/issues/228)
  settled that a game in progress is re-entered from the
  [title screen](title-screen.md), which gained a `RESUME` button beside `NEW`.
  Nothing on this screen changes: a resumed game does not pass through setup,
  because its roster was chosen here when that game was started.
  [ADR-0004](../../adr/0004-core-owns-the-save-format.md)'s save format is still
  the thing that has to exist behind that button, and it does not exist yet.
- **Should the four colours be reorderable?** Proposed: no. Turn order is
  tap order, which is one gesture rather than two, and re-ordering is a drag
  interaction on a screen four children are all reaching at.
- **What does `⇧` do?** **Open, and it blocks nothing else.** The keyboard
  table above lists a shift key and the Behaviour section never says what
  pressing it does — so the built keyboard draws it, disabled, and types the
  glyph on each key cap, which is uppercase. That is the literal reading of
  "typing appends a character" and it is the only part of this screen a guess
  could have filled in. The mockups draw names as `Connor` and `Isabella`, so
  mixed case is clearly wanted; what is not settled is *how* — a one-shot
  shift that capitalises the next letter and then releases, a caps-lock style
  toggle, or an automatic capital on the first letter of a name. They differ in
  how many taps a child spends to get `Connor`, which makes it a taste call
  rather than a mechanical one. Until it is answered the built screen can only
  produce `CONNOR`.
- **Are the letter keys in QWERTY order or alphabetical order?** The mockups
  draw QWERTY, because that is the arrangement on every keyboard a child has
  seen, including the one on the tablet this game runs on. The case for
  `A B C D E …` is that an eight-year-old who does not touch-type finds a
  letter by hunting for it, and hunting is faster in the order they already
  know by heart. This is a taste call and it is Connor's — it changes only
  which glyph is on which key, so it can be settled after the layout is agreed
  and costs nothing to change later.
