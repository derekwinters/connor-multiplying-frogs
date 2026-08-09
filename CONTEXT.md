# Multiplying Frogs

Multiplying Frogs is a digital port of a multiplication board game from Connor's
math class. This file is the project's **glossary and nothing else** — it fixes
what words mean so two agents don't invent two names for the same thing.

`/docs` remains the design contract. Anything about how the game *behaves*
belongs there, not here.

## Language

### The source

**The classroom game**:
The physical board game from Connor's math class that this project ports. It is
the authority on every rule.
_Avoid_: the original, the board game, the real game, the paper version

**Rule**:
Something the classroom game decides — how far a frog moves, what a wrong answer
costs, how many players there are, what winning means. Rules belong to Connor.
_Avoid_: mechanic, behaviour, game logic

**Presentation**:
Anything the classroom game left open because cardboard could not express it —
layout, animation, sound, how something is tapped. Presentation belongs to the
project.
_Avoid_: UI, skin, look and feel, polish

### The board

**Board**:
The playing surface: four lanes running from a Start log to an End log.
_Avoid_: map, track, level, arena

**Lane**:
One player's private column of lily pads. Frogs never share a lane and never
interact with each other.
_Avoid_: track, path, row, column

**Lily pad**:
A single step within a lane. A frog occupies exactly one at a time.
_Avoid_: space, square, tile, cell

**Log**:
The platform at either end of a lane. A frog begins on the **Start log**, and
reaching the **End log** is what winning means.
_Avoid_: start square, finish line, goal, home

**Frog**:
A player's playing piece. There is exactly one per player, and it does not
multiply.
_Avoid_: pawn, token, piece, character, avatar

### The cards

**Pile**:
One of the three sources a card is drawn from. Which pile a turn draws from is
decided by the roll, never chosen by the player.
_Avoid_: deck, stack, tier, difficulty level

**Card**:
A single multiplication problem together with its answer.
_Avoid_: question, problem, flashcard, prompt

**Roll**:
The dice throw that opens a turn. It selects the pile and does nothing else — in
particular it does not move a frog.
_Avoid_: throw, dice value, move roll

### A turn

**Turn**:
One player's complete go: roll, draw, answer, move.
_Avoid_: round, go, move, play

**Hand-off**:
The moment the device passes to the next player. The board is on screen during a
hand-off and not during the rest of a turn.
_Avoid_: pass screen, handover, next-player screen

**Pass-and-play**:
The game's only mode — two to four players sharing one device and taking turns
on it.
_Avoid_: hotseat, local multiplayer, couch co-op, shared device mode

### Working out

**Working-out grid**:
The on-screen surface a player uses to perform long multiplication. Nothing
entered into it is marked or scored.
_Avoid_: scratchpad, workspace, worksheet, calculator, notepad

**Answer row**:
The bottom row of the working-out grid, holding the final answer. It is the only
row that decides anything.
_Avoid_: answer box, input field, result, submission

**Carry box**:
A cell above a grid column for recording a carried digit.
_Avoid_: carry, regrouping mark, carry-over
