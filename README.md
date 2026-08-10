# Multiplying Frogs

Multiplying Frogs is a digital port of a multiplication board game from Connor's
math class. It is pass-and-play: two to four players share one device and take
turns. The board is four lanes of lily pads running from a Start log to an End
log, with one frog per player in their own lane, and a turn is roll, draw,
answer, move. The roll picks which of the three piles the card comes from; the
card is a multiplication problem, worked out on screen in a working-out grid
built for long multiplication. Reaching the End log is what winning means.

Built by Derek and his son Connor — Connor decides the rules, because it is his
game.

## Docs

**[The docs site](https://derekwinters.github.io/connor-multiplying-frogs/)** is
the design contract: what the game is, how it is played, and how it is built.
If the code and the docs disagree, that is a bug in one of them.

- [`CLAUDE.md`](CLAUDE.md) — the durable rules for anyone, human or agent,
  working on this repo.
- [`CONTEXT.md`](CONTEXT.md) — the glossary. It fixes what the words mean.

### Building the docs site locally

```bash
python -m pip install -r docs/requirements.txt
mkdocs serve          # live preview on http://127.0.0.1:8000
mkdocs build --strict # what CI runs
```

## Free, offline, and saved on the device

- **Free.** No purchases, no premium currency, no paid unlocks, no ads.
- **Offline.** No network calls, no telemetry, no analytics, no accounts and no
  sign-in. The game does not know who is playing and does not need to.
- **Saved locally.** A game in progress is saved on the device and nowhere else.

These are not features to be traded away later. They are rules in
[`CLAUDE.md`](CLAUDE.md), and the offline one is meant to be
[enforced in CI](docs/adr/0003-network-boundary.md) rather than remembered.

## How it is built

Unity and C#, targeting an Android tablet, with the game logic in an engine-free
`Core` assembly that is tested in a couple of seconds without an editor. The
[engineering handbook](docs/engineering/index.md) has the details.
