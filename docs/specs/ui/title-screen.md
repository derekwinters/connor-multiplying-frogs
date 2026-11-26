# Title screen

The first thing you see. It says what the game is and gives you the way into a
game: carry on with the one you left, or start a new one. It is Connor's splash
picture, frogs and all, with a wooden sign in the grass that says START.

## Invariants

**Invariant:** the only interactive elements on this screen are `RESUME` and
`START`. There is never a third. How many of the two are on screen when there
is no saved game is settled — [open question 2](#open-questions), `Hidden` —
and nothing is ever *added* beyond these two. The art, the drawn title and the
frogs are a picture: tapping them does nothing.
**Invariant:** `START` is the control that used to read `NEW`, and before that
`Play`. Its visible label is `START` (Derek's call, 2026-11-26), and it does
exactly what they did: it goes to [game setup](game-setup.md). Changing the
word and the look changed nothing about where it goes.
**Invariant:** nothing on this screen can start a game with the wrong number of
players. Choosing who is playing happens on [game setup](game-setup.md), always,
including when you have played five games in a row. **`RESUME` is not an
exception to this.** It does not choose a roster — it restores the one a saved
game already carries, and that roster was chosen on game setup when that game
was started. The one path that leaves this screen without passing through game
setup does so because the choice has already been made, not because it can be
made somewhere else.
**Invariant:** the version string is present and is read from `/VERSION`, never
typed into a layout.

### The invariants this page used to carry

Until [#228](https://github.com/derekwinters/connor-multiplying-frogs/issues/228),
the first invariant on this page read:

> **Invariant:** there is exactly one interactive element on this screen —
> `Play`.

That stopped being true when #228 settled that a saved game is re-entered from
the title screen. `Play` became `NEW`, with `RESUME` as its neighbour.

Until [#484](https://github.com/derekwinters/connor-multiplying-frogs/issues/484),
it read *"the only interactive elements on this screen are `RESUME` and
`NEW`."* `NEW` is now `START`, drawn as a sign — the same control under the
word Derek chose for it. The old wordings are quoted here so the changes are
visible, and they are not true anywhere else on this page.

## Regions

| Region | Job |
| --- | --- |
| `art` | The whole splash picture — every frog with its hop line, the log, and the drawn title — over the whole canvas, behind everything |
| `start` | The `START` sign: a plank on a post, standing in the grass bottom-right |
| `footprint` | Version string, bottom-left; small and quiet |

There is no separate `title` region any more. The game's name is the drawn
MULTIPLYING FROGS that is part of the art ([open question
3](#open-questions)), so it is wherever the picture puts it: across the top,
roughly 50–520 px down. `RESUME` has no region either until a save system needs
one — [open question 8](#open-questions).

## Anchors

- `art` fills the canvas, cropped from the centre outwards. It is the one
  element allowed to run to the screen edge, because it is a picture and a
  margin around a picture is a border nobody asked for.
- `art` is Connor's splash picture, rebuilt as layers under
  [`art-source/splash/`](https://github.com/derekwinters/connor-multiplying-frogs/tree/main/art-source/splash),
  with every layer it uses shown: the same picture as the launch splash. The
  unused splashes on the lily pads are not in it. The game draws it from
  `Assets/Art/TitleScreen/Resources/TitleScreen/title-screen-1920x1200.png`, the
  same render the mockup loads.
- `start`: the plank's **right edge is on the right safe area**, and its
  **bottom edge is `TitleSignBottomOffset` up from the bottom safe area**. At
  1920 × 1200 that puts the plank at 1552–1872 px across and 930–1058 px down,
  on the grass and water right of the log's cut end, clear of every frog. The
  plank is tilted `TitleSignTilt` about its own centre. The post is centred
  under the plank, runs from behind it, and ends `TitleSignPostDrop` below the
  plank's bottom edge, in the grass at 1172 px.
- The tap target is **the plank**, 320 × 128 px — over `MinTouchTarget` (96
  px) both ways, as the [shared Button](shared-components.md#button) requires.
  The post is part of the drawing, not of the target.
- `footprint` is pinned to the bottom-left safe area corner.
- On a screen that is not 16:10, the art crops and everything else keeps its
  distance from the **safe area**, not the screen edge. The sign is anchored
  to the safe area like everything else, so on a much wider or taller screen
  it can drift a little off the grass it stands on in the art; that is the
  same trade every anchored element on every screen makes.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Safe margin, every screen edge | `SafeMargin` | 48 px |
| Sign plank width | `TitleSignWidth` | 320 px |
| Sign plank height | `TitleSignHeight` | 128 px |
| Sign plank's bottom edge up from the bottom safe area | `TitleSignBottomOffset` | 94 px |
| Sign tilt, anticlockwise, about the plank's centre | `TitleSignTilt` | 3° |
| Sign plank corner radius | `TitleSignCornerRadius` | 18 px |
| Sign plank outline | `TitleSignOutlineWidth` | 7 px |
| Sign plank grain line width | `TitleSignGrainWidth` | 4 px |
| Sign plank grain line spacing | `TitleSignGrainSpacing` | 32 px |
| Sign post width | `TitleSignPostWidth` | 32 px |
| Sign post below the plank's bottom edge | `TitleSignPostDrop` | 114 px |
| Sign post outline | `TitleSignPostOutlineWidth` | 6 px |
| Sign post corner radius | `TitleSignPostCornerRadius` | 8 px |
| `START` label size | `TitleSignLabelSize` | 72 px |
| `START` label outline, outside the glyph | `TitleSignLabelOutlineWidth` | 7 px |
| `START` label letter spacing | `TitleSignLabelTracking` | 2 px |
| Sign plank's size while held, of its own | `TitleSignPressScale` | 0.95 |
| Version string size | `VersionLabelSize` | 28 px |
| Entering fade, art (the drawn title is part of it) | `TitleFadeDuration` | 0.3 s |

The `START` label is set in **Fredoka Bold** (weight 700), the font the title is
rebuilt in (`art-source/splash/fonts/`, SIL Open Font License), so the one word
on the sign is lettered the way the two words above it are: a light fill
inside a dark outline. It is centred on the plank.

In the game, weight 700 is a **static Fredoka Bold** instanced from that variable
font, committed with its licence at `Assets/Fonts/Fredoka/Resources/Fredoka/`:
Unity's legacy font importer cannot choose a variable font's weight, and would draw
its default, Light. How it is made is in
[the tech stack](../../engineering/tech-stack.md#the-title-screens-art-and-font).

`TitleSignPressScale` is the answer to [open question 9](#open-questions), added
to this table by [#520](https://github.com/derekwinters/connor-multiplying-frogs/issues/520),
the PR that declared it in code.

### Sign colours

The sign is painted from the art, not from the UI palette: its wood from the
log it stands beside, its outline from the title's outline, its lettering from
the title glow's white.

| Element | Constant | Value |
| --- | --- | --- |
| Plank wood, top | `TitleSignBark` | `#9C540C` |
| Plank wood, bottom, and the post | `TitleSignBarkShade` | `#843C0C` |
| Plank grain lines | `TitleSignGrain` | `#54240C` at 55% |
| Plank, post and label outline | `TitleSignOutline` | `#060B04` |
| `START` label fill | `TitleSignLabelFill` | `#F8FAF6` |

The plank's wood runs from `TitleSignBark` at the top to `TitleSignBarkShade`
at the bottom. The colours were sampled from the log layer and from
`art-source/splash/title.py` and `sky.py`.

`TitleFadeDuration` is stated in [Behaviour](#behaviour) and was added to this
table in the same PR that declared it in code
([#216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216)),
per [named constants are the origin, not an
afterthought](../../engineering/ui-design-process.md#the-named-constants-are-the-origin-not-an-afterthought).

### What became of the title, button-row and scrim constants

[#484](https://github.com/derekwinters/connor-multiplying-frogs/issues/484)
replaced the text title with the drawn one, the button row with the sign, and
dropped the scrim. These constants no longer place anything, and are
**removed**:

| Was | Value | Why it went |
| --- | --- | --- |
| `TitleBaselineY` | 300 px | The text title is gone; the drawn title is where the art puts it |
| `TitleSize` | 160 px | The same |
| `TitleButtonWidth` | 560 px | The button row is gone; the sign is `TitleSignWidth` |
| `TitleButtonHeight` | 160 px | The same; the sign is `TitleSignHeight` |
| `TitleButtonGap` | 48 px | It separated `RESUME` from `NEW` in the row; where `RESUME` goes now is [open question 8](#open-questions) |
| `TitleButtonBottomOffset` | 120 px | The sign is anchored by `TitleSignBottomOffset` and the right safe area |
| `TitleButtonLabelSize` | 64 px | The sign's label is `TitleSignLabelSize` |
| `TitleScrimOpacity` | 0.35 | No scrim ([open question 5](#open-questions)); it never had a shape, and the built screen never applied it |

[#520](https://github.com/derekwinters/connor-multiplying-frogs/issues/520),
the PR that built the sign, took all eight out of `TitleScreenView`.

### What became of the `Play*` constants

[#216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216)
builds this screen from named constants and names three of them that no longer
exist. They are **renamed, not removed, and their values do not change**:

| Was | Is now | Value | Note |
| --- | --- | --- | --- |
| `PlayButtonWidth` | `TitleButtonWidth` | 560 px | Unchanged; now applies to both buttons |
| `PlayButtonHeight` | `TitleButtonHeight` | 160 px | Unchanged; now applies to both buttons |
| `PlayButtonBottomOffset` | `TitleButtonBottomOffset` | 120 px | Unchanged; now measured to the bottom of the row |

The names lose their `Play` prefix because `Play` is not a thing on this screen
any more, and a constant named after a button that does not exist is a constant
somebody will eventually apply to the wrong one. `TitleButtonGap` is the one
genuinely new number.

The three `TitleButton*` names were themselves removed by #484 — see
[above](#what-became-of-the-title-button-row-and-scrim-constants). The table is
kept as history, because the settings dialog still points here.

## Elements

- **`START`** — this screen's primary action, drawn as a wooden sign
  (settled, [open questions 1 and 7](#open-questions)). Renamed from `NEW`,
  which was renamed from `Play`, and it goes exactly where they went: [game
  setup](game-setup.md). Never disabled; there is no state of this screen that
  would justify greying it out. It is the one primary action, so the shared
  [Button](shared-components.md#button) rule — *"exactly one primary button
  is visible at a time"* — still holds, though it is drawn as part of the
  picture rather than in the shared Button's primary style.
- **`RESUME`** — secondary (settled, [open question 1](#open-questions)).
  Goes back into the saved game, on the
  [game board](game-board.md). It restores a roster; it never chooses one. What
  state within a turn a resumed game re-enters is the save format's business
  ([ADR-0004](../../adr/0004-core-owns-the-save-format.md)) and is not settled
  here.
  - **Hidden when there is no game to resume** (settled, [open question
    2](#open-questions)) — not laid out at all. That is the only state that
    exists today, so `RESUME` is hidden on every build of this screen.
  - **Where it goes and what it looks like when it can be shown is not
    drawn** — [open question 8](#open-questions), for the issue that builds
    the save system.
  - Nothing on this screen writes, reads, or deletes a save. The save
    round-trip does not exist yet — it is out of the shape-only proof of
    concept ([#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198)).
- **Title** — the drawn MULTIPLYING FROGS in the art (settled, [open question
  3](#open-questions)). Not text, not a separate element, not a control.
- **Version** — `v0.1.0`, from `/VERSION`. It exists so that a screenshot from a
  tablet says which build it came from, which is the difference between a
  reproducible bug and a story about one. Drawn `#6C7873` at
  `VersionLabelSize`, a grey chosen for the old flat pale placeholder; on the
  art it sits on bright green grass and is hard to read. It is still meant to
  be small and quiet; keeping it legible is
  [open question 10](#open-questions), not redesigned here.

## Behaviour

- Entering: the art — the drawn title with it — fades in over
  `TitleFadeDuration` (0.3 s). The sign does not animate — a control that moves
  is a control that gets missed.
- **Pressed, the plank shrinks.** While a finger is held on it, the plank — its
  wood, outline and word — draws at `TitleSignPressScale` of its size, about its
  own centre, and springs back when the finger lifts or slides off. Nothing else
  changes: no colour, no movement, and the post stays as it is. The tap target
  does not shrink with it. It acts on release, over the plank, like the
  [shared Button](shared-components.md#button)
  ([open question 9](#open-questions), settled).
- The hardware back button on this screen exits the app. It is the only screen
  where back exits.
- Nothing auto-advances. The title screen waits indefinitely.

## Mockup

- **The agreed picture:** [`mockups/title-screen.html`](mockups/title-screen.html) —
  the whole splash picture with the `START` sign bottom-right, in the no-save
  state that exists today (`RESUME` hidden).

It loads its art from [`images/title-screen/full.png`](images/title-screen/full.png),
the whole composite rendered from the splash's vector layers at 1920 × 1200 by
`python3 art-source/splash/build_splash.py --home-only`, and sets `START` in
the title's font from a committed copy beside it
(`images/title-screen/fonts/`, with its licence). Neither needs the network.

[#484](https://github.com/derekwinters/connor-multiplying-frogs/issues/484) got
here through ten comparison drawings: four without the frogs (drawn or text
title, log kept or hidden, with or without a scrim), two with all the frogs and
the old 560 × 160 `NEW` button where it was agreed and moved down, and four
other kinds of button — a lily pad, this wooden sign, `NEW` lettered on the
log, and a small button in the pond corner. Derek picked the sign. The other
nine files, and the art pieces only they used, are deleted: the questions they
existed to ask are answered, and a mockup nobody can build to only confuses the
next reader. The same happened before to `title-screen-resume-primary.html`,
when [open question 1](#open-questions) was settled.

## Open questions

- **1. Which of `RESUME` and `NEW` is the primary button? Settled: `NEW`** —
  now `START`.
  Derek's call, recorded on
  [issue #216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216#issuecomment-5241023983):
  `NEW` is primary, `RESUME` is secondary. `NEW` inherits `Play`'s emphasis and
  is the button that is always there — the shared
  [Button](shared-components.md#button) invariant, *"exactly one primary button
  is visible at a time,"* is satisfied by it alone. It is `START` now
  ([open question 7](#open-questions)); that changes its word and look, not its
  rank.
- **2. What does `RESUME` do when there is no game to resume? Settled: Hidden.**
  Derek's call, same comment as above: not laid out at all, the component's
  existing `Hidden` state. In practice this is the *only* state `RESUME` is ever
  in: the save/resume system does not exist anywhere yet
  ([#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198)
  excludes it from scope), so there is never a saved game for `RESUME` to
  report. [#216](https://github.com/derekwinters/connor-multiplying-frogs/issues/216)
  builds the hidden/shown check as a real query a future save-system issue can
  answer honestly, not as `RESUME` being removed outright; `RESUME` stays a
  real button that is simply always hidden today.
- **3. The title: the drawn lettering, or the text? Settled: drawn.**
  Derek's call, 2026-11-26, on
  [#484](https://github.com/derekwinters/connor-multiplying-frogs/issues/484).
  The art's own MULTIPLYING FROGS (rebuilt as vectors, spelled correctly, in
  Fredoka) is the title, and the text title is gone with `TitleBaselineY` and
  `TitleSize`. It matches the launch splash.
- **4. The log: keep it, or hide it? Settled: kept.**
  Derek's call, 2026-11-26. With all the frogs in
  ([open question 6](#open-questions)) the big frog sits on it, so it was never
  going to be empty.
- **5. The scrim: still wanted? Settled: no.**
  Derek's call, 2026-11-26: no scrim and no dark band. `TitleScrimOpacity`
  is removed. It never had an extent or a colour, and the one drawing of it
  darkened the whole picture.
- **6. Frogs on the home screen: all, or none? Settled: all.**
  Derek's call, 2026-11-26. His first ask was none — *"without the frogs, to
  not crowd out the buttons"* — and four drawings showed that. Seeing them, he
  said *"Let's see it with all the frogs. It's too empty right now."* All four
  frogs, with their hop lines.
- **7. With all the frogs, what is the button and where does it go?
  Settled: a wooden sign in the grass, bottom-right, reading `START`.**
  Derek's call, 2026-11-26: *"I like the sign but I want it to say start."*
  The old 560 × 160 button sat in front of the big frog wherever it went, and
  of the four other kinds drawn — a lily pad, a sign, the log itself, a small
  corner button — he picked the sign, and chose the word on it. Its numbers are
  the `TitleSign*` constants above; they are the numbers the comparison drawing
  had, with only the word changed.
- **8. Where does `RESUME` go when it can be shown? Open — for the save-system
  issue.** The row it shared with `NEW` is gone, and nothing on this page says
  where `RESUME` sits beside the sign, or what it looks like on the picture.
  It does not need an answer until there is a saved game to resume, so it is
  left to the issue that builds the save system, which will need its own
  wireframe for it. Until then `RESUME` is hidden, and both invariants above
  read true with it hidden.
- **9. What does the sign do when it is pressed? Settled: press down — the plank
  shrinks slightly while held.** Derek's call, 2026-11-26, on
  [#521](https://github.com/derekwinters/connor-multiplying-frogs/issues/521).
  The options were *press down* (the plank shrinks to about 95% while held),
  *wobble* (it tips a few degrees and springs back) and *darken* (the wood goes a
  shade darker, like the shared Button's fill). He chose press down, and the
  number is the one that option named: `TitleSignPressScale`, 0.95. It is
  applied at once, with no animation, as the shared Button's pressed state is.

  The option was worded *"like the other buttons do"*. The other buttons do not
  shrink: the [shared Button](shared-components.md#button) moves down by
  `ButtonPressOffset` and darkens. The sign follows the option's own
  description — it shrinks, and does not darken or move — and only that.
- **10. The version string on the art: how does it stay legible? Open.** It is
  drawn as it was on the placeholder — `#6C7873` grey at `VersionLabelSize` —
  and on the bright green grass it is hard to read. It is meant to stay small
  and quiet. Not redesigned here.
- **Is there a `How to play` button here?** Proposed: still no — but for a
  different reason than before, because half of the old reason has gone.

  This question used to end *"it needs its own wireframe for the screen it
  opens"*. That screen now exists:
  [#324](https://github.com/derekwinters/connor-multiplying-frogs/issues/324)
  specified [how to play](how-to-play.md), reachable from the
  [settings dialog](settings-dialog.md). So there is somewhere for a button
  here to go, and the argument for putting one here is a real one — somebody
  who has never played reaches for the title screen, not for a gear inside a
  game they have not started.

  What is left in the way is this page's own first invariant: *"the only
  interactive elements on this screen are `RESUME` and `START`. There is never
  a third."* Adding the button is a change to that invariant, and changing it
  is a decision about what this screen is, not a consequence of the other
  screen existing. It should be its own issue, with its own wireframe for
  where the button sits on the picture.
- **Does the title screen come back after a game, or does `Play again` restart
  directly?** Both exist on [game over](game-over.md); this screen does not care
  which is used.
