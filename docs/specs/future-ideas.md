# Future ideas

!!! danger "Everything on this page is out of scope"
    Nothing here is being built. Nothing here is planned. An idea is on this
    page precisely because we decided **not** to do it yet, and it stays out of
    scope until it is **deliberately promoted** — which means someone opens an
    issue for it, gives it a milestone, and it goes through triage like any
    other work. Finding an idea here is not permission to build it.

## What this page is for

Good ideas turn up at the worst possible moments — mid-issue, mid-review, in the
car. The temptation is to argue them into the current milestone, because the
alternative feels like throwing them away.

This page is the alternative. It costs one paragraph to park an idea and keeps
the current milestone from growing a new goal every time someone has a good
thought. "That's a future-ideas entry" is a complete answer, and it is not a no.

It also protects the ideas. An idea that lives in a Slack message or a comment
on a closed issue is an idea that is gone. An idea on this page is one Connor
can scroll through later and pick from.

## How to park an idea

1. Add an entry under [The parking lot](#the-parking-lot), newest at the top.
2. Give it a one-line summary anyone could understand, then two or three
   sentences at most.
3. Say **why it is parked** — too big, depends on something that doesn't exist,
   needs a decision from Connor, or just not now. This is the important part; an
   entry without it reads like an oversight six months later.
4. Link its GitHub issue if one exists. Most entries will not have one, and that
   is fine — an idea with no issue is exactly what this page is for.

Keep entries short. If an idea needs a page to explain, it has outgrown the
parking lot and wants a `type:question` issue instead.

## How an idea leaves

An idea is **promoted** by opening a real issue for it, putting it in a version
milestone, and letting triage handle it from there. When that happens, move the
entry to [Promoted](#promoted) with a link, rather than deleting it — the record
of when something stopped being a someday-idea is worth keeping.

An idea is **dropped** by moving it to [Dropped](#dropped) with a one-line
reason. Deleting an entry outright loses the fact that we considered it, which
is how the same idea gets re-argued a year later.

### Entry format

```markdown
### A short, plain name for the idea

What it is, in a sentence or two.

**Why it's parked:** the reason it is not being built now.
**Issue:** #123 — or "none yet".
```

## The parking lot

Everything below was raised and deliberately set aside during the founding
design conversation in
[issue #7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7).

### Local-network play

Two or more phones on the same wifi discovering each other, so players use their
own devices instead of passing one around.

**Why it's parked:** wanted for a later version, but it requires overriding a
non-negotiable `CLAUDE.md` rule — the app is fully offline, and LAN discovery
needs an Android network permission. See
[ADR-0003](../adr/0003-network-boundary.md); the rule gets amended when the
feature is actually built, not before.
**Issue:** none yet.

### Diagnostic marking of the working-out grid

On a wrong answer, highlight the first cell where the working went astray,
rather than just revealing the correct answer.

**Why it's parked:** genuinely the best teaching tool available here, and it
requires picking one long-multiplication algorithm as canonical — a pedagogical
stance nobody has taken. See
[ADR-0002](../adr/0002-structured-working-out-grid.md).
**Issue:** none yet.

### Computer frogs

Fill empty seats with computer-controlled players so the game can be played
alone.

**Why it's parked:** the planned direction is toward *more real people in the
room* (local-network play), so an artificial opponent is a branch off that road
rather than a step along it. The cost is real — Connor cannot play his own game
without someone to play it with.
**Issue:** none yet.

### Five to eight players

The classroom game's rules card says 2–8 players and the board has eight lanes.
v1 caps at four.

**Why it's parked:** with two-to-five-minute turns, eight players sharing one
phone means waiting fifteen to thirty-five minutes between turns. At the table
that works because everyone can see the board and think at once; one screen
takes that away. Note this cap is a **rule change**, made by Derek in #7.
**Issue:** none yet.

### Sound and music

Any audio at all — a chime for a correct answer, music, effects.

**Why it's parked:** deliberately out of v1. Audio means assets, licences, a mute
control, and a settings screen to put the mute control on. A single "correct"
chime does a lot of work and is the most likely of the parked items to be
promoted first.
**Issue:** none yet.

### Other kinds of arithmetic

Division, addition or subtraction modes, which the card and pile system would
support with almost no change.

**Why it's parked:** it is a multiplication game; the name says so.
**Issue:** none yet.

### More than one board

Alternate tracks, longer or shorter lanes, difficulty settings beyond what the
piles already provide.

**Why it's parked:** v1 ships exactly the classroom board. Nothing has been
played yet, so there is no evidence about what a second board would need to be.
**Issue:** none yet.

### An in-app tutorial

An onboarding flow teaching the rules inside the game.

**Why it's parked:** Connor already knows the rules, and
[how to play](../intro/how-to-play.md) covers anyone who doesn't. A tutorial is a
second thing to build and keep correct as the game changes.
**Issue:** none yet.

### Frogs that visibly multiply

A correct answer briefly swarms the screen with that many frogs before they
collapse back into your single playing piece — making the title literal and
showing what multiplication actually means.

**Why it's parked:** Connor's call, and the decision was that the frog stays a
playing piece and the title stays a pun.
**Issue:** none yet.

## Promoted

*Nothing promoted yet.*

## Dropped

*Nothing dropped yet.*
