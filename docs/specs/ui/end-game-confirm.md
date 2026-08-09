# End the game — confirm

The one dialog in the game that can take something away from somebody who did
nothing wrong. It asks first, and it says who it is asking on behalf of.

## Invariants

**Invariant:** the game does not end without this dialog. Nothing else in the
game ends a game early.
**Invariant:** the confirm **names the cost** — how many frogs are still
swimming — rather than asking an abstract *"Are you sure?"*. A confirm that
gives no new information is a confirm people learn to tap through.
**Invariant:** the safe option is the primary button and is on the right, where
the thumb is. The destructive option is the outlined one, on the left, a full
`ButtonDestructiveGap` away.
**Invariant:** the hardware back button keeps playing. Back never ends a game.
**Invariant:** cancelling returns to the exact board state — same turn, same
positions.

## Regions

| Region | Job |
| --- | --- |
| `question` | `End the game for everyone?` |
| `cost` | What ending it now actually does |
| `controls` | `End the game` and `Keep playing` |

## Anchors

A centred [dialog](shared-components.md#dialog), `ConfirmDialogWidth` by
`ConfirmDialogHeight`. Question at the top of the padding box, cost beneath it
at `ConfirmBodyWidth`, buttons on the bottom row — destructive left, primary
right.

## Named constants

| Element | Constant | Value |
| --- | --- | --- |
| Dialog width | `ConfirmDialogWidth` | 1160 px |
| Dialog height | `ConfirmDialogHeight` | 540 px |
| Question size | `ConfirmQuestionSize` | 56 px |
| Body size | `ConfirmBodySize` | 40 px |
| Body column width | `ConfirmBodyWidth` | 1048 px |

## Elements

- **Question** — `End the game for everyone?` The words *for everyone* are the
  point of the sentence.
- **Cost** — built from the live game state, not a fixed string:

    | Situation | Body |
    | --- | --- |
    | Some frogs still going | `Three frogs are still swimming. Ending it now stops the game for all four players and shows the results.` |
    | Only one frog left going | `One frog is still swimming. Ending it now stops the game for all four players and shows the results.` |
    | Every frog home | `Everybody is home. Ending it now shows the results.` |

- **`End the game`** — destructive [button](shared-components.md#button). Goes
  to [game over](game-over.md) with the standings as they are.
- **`Keep playing`** — primary button. Returns to the board.

## Behaviour

- Reached only from [settings dialog](settings-dialog.md).
- Ending the game is **not** the same as losing it. Every frog keeps the pads it
  has, and [game over](game-over.md) shows the order they finished in — which is
  why the wording is *stops the game and shows the results* rather than
  *quits*.
- Hardware back does what `Keep playing` does.
- There is no second confirm. One confirm that says something useful beats two
  that do not.

## Mockup

[`mockups/end-game-confirm.html`](mockups/end-game-confirm.html) — drawn in the
three-frogs-still-swimming state, which is the one where the confirm has
something to lose.

## Who may end a game

**Anyone, on anyone's turn.** Derek's call in
[issue #186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186).

The alternative was restricting it to the current player, and it was rejected
because the tablet cannot tell who is holding it — the restriction would have
been a guess dressed as a rule. What actually protects a game in progress is
this dialog telling you, before you act, how many frogs you are about to stop.

That is why the cost sentence is built from live state rather than being a fixed
string. It is the whole of the protection, so it has to say something true and
specific every time.

## Open questions

- **Does the game also end on its own when every frog is home?** Still
  [issue #186](https://github.com/derekwinters/connor-multiplying-frogs/issues/186).
  If yes, the everybody-is-home body sentence above is a state almost nobody
  will see, because the game will have ended itself first.
