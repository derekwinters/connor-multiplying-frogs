# The game adds a structured working-out grid the classroom game has no equivalent of

The cards are multi-digit long multiplication, not times-table recall. At the
table that is fine, because paper is ambient and the working-out *is* the
exercise; on a phone there is nowhere to work. Rather than assume paper beside
the device, the game provides a **structured grid with carry boxes** that a
player fills in to perform the multiplication on screen.

## There are exactly three problem shapes

The three card piles are difficulty tiers, and the tier decides how many digits
are being multiplied. There is **one die**, and each pile is labelled with the two
faces that send you to it — six faces split three ways, so every tier comes up a
uniform third of the time. Confirmed by Derek:

| Pile | Roll | Shape | Example |
| --- | --- | --- | --- |
| Easy | 1 or 2 | 2-digit × 1-digit | `68 × 5` |
| Medium | 3 or 4 | 2-digit × 2-digit | `22 × 41` |
| Hard | 5 or 6 | 3-digit × 2-digit | `331 × 41` |

The hard pile's faces are inferred by elimination rather than read off the board;
the other two rows are stated. The uniform distribution matters for the problem
generator, which has to reproduce the classroom game's difficulty curve rather
than invent its own.

This bounds the grid tightly, and the bound is worth stating because it is the
difference between three layouts and a general solver. **The grid never has to
render a shape outside this table**, and `331 × 41` is the confirmed worst case
rather than an assumed one.

The tier also has no effect on movement — every pile is worth the same one lily
pad forward. Drawing the easy pile is pure luck and strictly better, which is the
classroom game's design and stays.

This is a **new mechanic**, and under
[ADR-0001](0001-rules-sacred-presentation-ours.md) that made it Connor's call.
Derek decided it in [issue #7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7),
which the project contract permits, and it is recorded here as a deliberate
deviation rather than allowed to pass as presentation.

## Two constraints that keep it from becoming a tutor

**Nothing in the grid is marked.** Only the answer determines whether the frog
moves. Marking cells would change what the game rewards, and — more importantly
— it would require picking one algorithm as canonical. Long multiplication has
several valid layouts (partial products, lattice, area model), and choosing one
to grade against is a pedagogical stance this project has not taken. Carry boxes
exist for the same reason they exist on paper: so you don't lose the 3. Not to be
checked.

**There is no mode.** The answer box *is* the grid's bottom row. A player who can
do `68 × 5` in their head fills one row; a player facing `331 × 41` works upward
through the partial products. No toggle, no "show working" button, nothing to
discover — which matters on a shared device, where every mode arrives at the next
player in whatever state the last one left it.

## Consequences

- The grid must be **sized to the card**, not to the largest possible problem, or
  `68 × 5` looks like homework. Which cells exist for a given problem is game
  logic and belongs in `Core`; only drawing them belongs in the Unity shell.
- `331 × 41` in portrait — two partial-product rows, a sum row, and carry boxes
  above them — is the tightest layout in the game and the project's biggest
  unknown. It gets a `type:wireframe` issue and is drawn before any UI code
  exists.
- Revealing the *worked solution* on a wrong answer is ruled out by the same
  reasoning: showing one method's partial products would quietly make that method
  canonical. The correct answer is revealed; the working is not.
- Diagnostic marking — highlighting where the working first went astray — is
  genuinely the best teaching tool available here, and is parked in
  [future ideas](../specs/future-ideas.md) rather than dropped.
