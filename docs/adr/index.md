# Decisions

Architectural decision records. Each one captures a choice that was **hard to
reverse**, **surprising without context**, and **the result of a real
trade-off** — the three tests a decision has to pass before it earns a page
here.

Most decisions don't. A choice that is easy to change gets changed rather than
recorded, and a choice nobody would question needs no defence. What lands here
is the small set a future reader would otherwise look at and ask *"why on earth
did they do it that way?"*

These are not the design contract — [`specs/`](../specs/index.md) is. An ADR
says why a decision was taken, once, at a point in time. It is not updated as
the game evolves; it is superseded by a later ADR that says so.

| ADR | Decision |
| --- | --- |
| [0001](0001-rules-sacred-presentation-ours.md) | The classroom game's rules are sacred; presentation is ours |
| [0002](0002-structured-working-out-grid.md) | The game adds a structured working-out grid the classroom game has no equivalent of |
| [0003](0003-network-boundary.md) | The internet is permanently out; the local network is an open question |
| [0004](0004-core-owns-the-save-format.md) | Core owns the save format and hand-rolls it |

All four were settled in
[issue #7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7),
the design conversation that established what Multiplying Frogs is.
