# Settings dialog

The gear on the [game board](game-board.md). It holds the rules, the way out of
a game, and which version you are running.

## Invariants

**Invariant:** opening this changes nothing about the game. It is a menu, not a
turn.
**Invariant:** the only destructive thing on it confirms — see
[end-game confirm](end-game-confirm.md).
**Invariant:** it is reachable on anyone's turn, not only the current player's.
It is the way out of a game, and a way out that is only available to whoever
happens to be holding the tablet is not a way out.
**Invariant:** nothing here can change a rule of the game mid-play. There is no
difficulty setting, no player count, no undo.
**Invariant:** the version string comes from `/VERSION`.

## Regions

| Region | Job |
| --- | --- |
| `title` | `Settings` |
| `actions` | `How to play`, then `End the game` |
| `footprint` | Version string |
| `controls` | `Back to the game` |

## Anchors

A centred [dialog](shared-components.md#dialog), `SettingsDialogWidth` by
`SettingsDialogHeight`. Actions are a single left-aligned column at full inner
width; `footprint` sits bottom-left; the primary button bottom-right.

`End the game` is separated from everything below it by
`ButtonDestructiveGap` — 96 px, which is nearly a whole button's height of
empty space. That gap is the layout, not decoration: `Back to the game` is the
button people reach for without looking, and it must not be adjacent to the one
that ends everybody's game.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Dialog width | `SettingsDialogWidth` | 900 px |
| Dialog height | `SettingsDialogHeight` | 760 px |
| Action button width | `SettingsActionWidth` | 788 px |
| Gap between `How to play` and `End the game` | `SettingsActionGap` | 96 px |
| Gap below `End the game` | `ButtonDestructiveGap` | 96 px |

## Elements

- **`How to play`** — secondary [button](shared-components.md#button). Opens the
  rules. See the open question below: the screen it opens does not exist yet.
- **`End the game`** — destructive button. Opens
  [end-game confirm](end-game-confirm.md). Never ends anything by itself.
- **`Back to the game`** — primary button. Closes and returns to exactly the
  board state that was there.
- **Version** — `v0.1.0`, quiet, bottom-left.

## Behaviour

- Opened by the gear on the board, or by the hardware back button on the board.
- Hardware back inside this dialog does what `Back to the game` does — the
  least destructive button, per the
  [dialog rule](shared-components.md#dialog).
- Closing returns to the board with nothing changed: same turn, same positions,
  same enabled `Roll`.

## Why it is this thin

Because v1 genuinely has nothing to configure. There is no audio to mute, no
account to sign out of, no difficulty to set, no data to clear. Every one of
those is either
[parked](../future-ideas.md) or ruled out by `CLAUDE.md`.

A settings screen padded out with toggles that do nothing is worse than a short
one. What this dialog is *for* is the two things a player actually needs
mid-game — the rules, and the exit.

If audio lands, the mute control belongs here, and it is the reason this dialog
is a list rather than two buttons in a row.

## Mockup

[`mockups/settings-dialog.html`](mockups/settings-dialog.html)

## Open questions

- **What does `How to play` open?** There is no wireframe for it, which means
  under [rule 8](../../engineering/ui-design-process.md) it cannot be built yet.
  Options: a plain scrolling text panel of
  [how to play](../../intro/how-to-play.md), or a picture of the rules card from
  the classroom game — which is charming, and is the actual source of the rules.
  Needs its own `type:wireframe` issue before the button does anything.
- **Should the button be hidden until that exists, or present and disabled?**
  Proposed: present. A disabled button that appears later is less confusing than
  a button that appears from nowhere.
