# Core owns the save format and hand-rolls it

A game runs 15–45 minutes, so an in-progress game has to survive the app being
killed. That save sits between two non-negotiable rules: `Core` never references
UnityEngine ([rule 2](../engineering/tech-stack.md#the-rule)), and Unity's
serializer is never to be guessed at
([rule 6](../engineering/unity-serialization.md)) — whose canonical failure is
almost literally this feature's failure mode.

**`Core` serializes its own state, and the Unity shell only stores bytes** behind
an interface `Core` owns. Unity's serializer is not involved at all, so rule 6
stops applying rather than having to be carefully navigated. The format is
**hand-rolled**: the state is a couple of dozen numbers — four lane positions,
the player count, whose turn it is, the current card, and the RNG seed — and
`CLAUDE.md` says prefer writing thirty lines over adding a dependency.

## Why not the obvious alternatives

`JsonUtility` is a UnityEngine type, so putting it in `Core` is immediately
illegal. Letting the **shell** serialize a plain `Core` snapshot is legal but
puts the save format in the one assembly that cannot be tested without an editor
— for the feature whose bugs surface days later as *"the game forgot its frogs"*.
A JSON library gets versioning for free, at the price of a dependency to
understand, update and eventually remove, for twenty numbers.

## Consequences

- The whole save round-trip is testable in the two-second NUnit suite with an
  in-memory implementation of the storage interface. No editor, no licence.
- **The dice and the problem generator need a seeded RNG in `Core`, and the seed
  is part of the save.** Otherwise restoring re-randomises everything that hasn't
  happened yet. The seed is worth having regardless — a generator without one
  isn't properly testable.
- A hand-rolled format means a hand-rolled versioning story the first time the
  state changes shape. Small, but it has to be remembered rather than handled by
  a library.
