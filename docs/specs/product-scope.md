# Product scope

The hard edges of the product: what it runs on, how you get hold of it, what it
will never do, and what it keeps on the device. These are constraints on the
**product**. They are not rules of the game — the rules belong to the classroom
game, and they live on [how to play](../intro/how-to-play.md) and in
[reference material](reference/index.md).

This is the page
[`CLAUDE.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md)'s
"what not to do" section points at, which is the whole reason it exists: a rule
that points at nothing has nothing to hold a change against.

Nothing on this page was decided on this page. Every line records a decision
made somewhere else, and [the last section](#where-all-of-this-was-settled) says
where.

## The five invariants

Each of these is written so a pull request can be held against it. If a change
would make one of them false, either the change is wrong, or somebody has
deliberately moved the line — in an ADR, with Derek's name on it, not in a diff.

1. **Nothing in the game costs money.** No in-app purchase, no premium currency,
   no paid unlock, no "watch this to continue", no paid edition alongside a free
   one. There is nothing to buy and nowhere to buy it.

2. **There is no advertising.** No ad SDK, no ad-adjacent dependency, no
   third-party banner, no sponsored content, no cross-promotion of anything —
   including of anything else Derek builds.

3. **There are no accounts and no player identity.** The game never asks who is
   playing, never signs anybody in, and stores nothing that names a person. A
   player is a frog colour for the length of one game and nothing after it.

4. **The app makes no network calls.** No servers, no telemetry, no analytics,
   no crash reporting, no update check, no "anonymous usage stats". A package
   that wants network access is the wrong package.

5. **Everything the game keeps stays on the device.** No cloud save, no sync, no
   account for a game to be attached to. Whatever the app writes, it writes
   locally.

Invariants 4 and 5 both have a subtlety that matters, and each gets a section
below. The first three do not: they mean exactly what they say, in every version,
forever.

### Why these are non-negotiable rather than defaults

This is a game an eight-year-old is helping build, played by him and by whoever
he shows it to. Every one of those five things is an ordinary, uncontroversial
thing to put in a mobile game, and every one of them would change what this one
is for. There is no revenue target here to trade any of them against, and no
version of the game in which the trade becomes worth making.

## Platform

Restated from [the tech stack](../engineering/tech-stack.md#target-platform),
which is the authority. If the two disagree, that page wins and the fix is to
correct this one.

| | |
| --- | --- |
| Primary target | Android tablets, held landscape |
| Also runs on | Android phones — same OS, same APK |
| Minimum Android version | 7.0 (API 24) |
| Orientation | Landscape only, both ways up |
| Reference resolution | 1920 × 1200 (16:10) — the canvas every layout is designed at |
| Not a target | iOS, desktop, console, web |

**A kids' tablet is the device the game is designed around**, because that is
what kids play on and because four players sharing one screen need a screen
worth sharing. Phones are not excluded, they are simply not what the layouts are
drawn for.

## Distribution

**You get the game by installing an APK by hand.** Every
[release](https://github.com/derekwinters/connor-multiplying-frogs/releases) has
one attached to it; you download it onto the tablet and sideload it.

- **No app store, and no distribution service of any kind.** Not Google Play,
  not a beta channel, not a link that expires.
- **An `.aab` gets built only if the game is ever published**, which is not
  planned and would be its own decision.
- **There is no update mechanism, and there cannot be one.** An app that checks
  for a new version makes a network call, and invariant 4 forbids it. A new
  version means somebody installs a new APK.

Being free is not a pricing decision that some later version could revisit. With
no store and no account, there is nothing in the picture that could take money
even if anybody wanted it to.

## The network boundary is two lines, not one

[ADR-0003](../adr/0003-network-boundary.md) draws two different lines, and
flattening them into "no networking, ever" would be wrong in a way that costs
something real. They are stated separately here on purpose.

**The permanent line, true in every version, never up for amendment:** no
traffic ever leaves the local network, no servers, no third-party SDKs, no
telemetry. This is the line ADR-0003 says is *"recorded in product scope"*, and
recording it here is the point of this section. It does not move.

**The v1 property, which is checkable and which may one day be amended:** the
app ships with **zero Android network permissions**. That is stronger than the
permanent line and deliberately so — a permission the app does not hold is a
mechanical fact, where "only local traffic" is a promise that depends on
everyone continuing to mean it.

The reason the v1 property is held separately is
[local-network play](future-ideas.md#local-network-play): two tablets on the
same wifi, so players use their own devices instead of passing one around. It is
wanted for a later version and it cannot honour zero permissions. ADR-0003's
decision is to keep the stronger property now and amend it *if and when that
feature is actually built* — not eighteen months early in exchange for nothing.

!!! warning "The check that makes the v1 property mechanical is not written yet"

    ADR-0003 calls a CI check asserting zero network permissions *"v1 work, not
    a nice-to-have — it is the entire mechanism by which this decision holds"*.
    No workflow asserts it today. Until one does, the v1 property is a promise
    like the permanent line, held up by review rather than by CI.

## The save is local, or it is not a save

A game runs 15–45 minutes, so an in-progress game has to survive the app being
killed — [ADR-0004](../adr/0004-core-owns-the-save-format.md) mandates one and
puts the format in `Core`, with the Unity shell doing nothing but storing bytes.

What that means for the product, rather than for the format:

- **On-device only.** There is nowhere else for it to go, and adding somewhere
  would break invariants 4 and 5 together.
- **No transfer between devices.** A game started on one tablet is finished on
  that tablet.
- **Uninstalling loses the game in progress**, and that is the accepted cost of
  having nothing to back it up to.
- **The save names nobody.** It is lane positions, the player count, whose turn
  it is, the current card, and the random seed. There is no field in it that
  could identify a person, because the game never learns anything that could.

**What this page does not settle** is the shape of the save: how many there are,
when the game writes one, and what a player sees when a saved game is waiting.
Those are mechanics, and mechanics are Connor's. Where a resumed game is
re-entered from *has* been settled — the title screen, with `RESUME` and `NEW`
in place of `Play`, decided on
[#228](https://github.com/derekwinters/connor-multiplying-frogs/issues/228) and
carried into the layout contract by
[#235](https://github.com/derekwinters/connor-multiplying-frogs/issues/235).

The save itself is **not in the v0.2 proof of concept**. That build plays a
whole game in flat shapes and forgets it afterwards; the save round-trip is
later work, and [#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198)
says why it was cut rather than forgotten.

## Who it is for

**Connor first, kids like him second.** He is eight, the game is his, and he
breaks ties on taste. [The vision](../intro/vision.md#who-it-is-for) has the
long version and the reasoning; the part that constrains the product is short:

| | |
| --- | --- |
| Audience | Connor first, kids like him second |
| Session length | 15–45 minutes, from the classroom rules card |
| Players | 2–4 in v1, sharing one device, pass-and-play |
| Reading and typing | Digits to answer with, and a player's own name. Nothing else |

That last row is a real product constraint rather than a UI preference, and it
is narrower than it looks. There are exactly two keyboards in the game, both
drawn by the game rather than by Android: the
[digit keypad](ui/working-out-grid.md) for entering an answer, and the
[letter keyboard on game setup](ui/game-setup.md#the-keyboard) for naming a
frog. Nowhere else does a player type, and nothing a player types goes anywhere
— there is no network, no account, and nothing is kept once the game ends.

**The row used to read "No player ever types anything but digits."** Derek
reversed it in
[#310](https://github.com/derekwinters/connor-multiplying-frogs/issues/310):
frogs are named by their players now, starting from the colour name. The
argument the old rule rested on — that asking four children to each type a name
buys a soft keyboard, a misspelling, an editing flow, and, reliably, somebody
typing something rude about somebody else — is kept in full on
[the player chip](ui/shared-components.md#why-there-is-now-typing), along with
why each of those costs was accepted. The short version: three of the four are
the feature, and the fourth is contained by the game being offline and
forgetting everything.

**The 2–4 cap is a v1 scope fact, not a rule of the classroom game** — the rules
card says 2–8, and the change is Derek's, recorded in
[the vision](../intro/vision.md#two-places-v1-differs-from-the-classroom-game)
and parked in [future ideas](future-ideas.md#five-to-eight-players).

## What is out of v1

Not restated here. [The vision](../intro/vision.md#what-this-game-is-not) is the
one list of what v1 does not include and why each thing is parked rather than
dropped, and a second copy of that list is a second copy to keep true.

The v0.2 proof of concept excludes more than v1 does, because it is a shape-only
build — no art, no audio, no save. Its scope is
[#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198).

## One thing that becomes required the moment art arrives

Art is deferred and free assets are likely, and using one makes an
**attributions screen a requirement** rather than a nicety — a licence
obligation, settled in
[#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7). It is
written here because the requirement attaches to the product the moment the
first asset is committed, which is exactly when nobody is thinking about it.

## Where all of this was settled

| What | Settled in |
| --- | --- |
| Free, no ads, no accounts, no network, no third-party SDKs | [`CLAUDE.md`](https://github.com/derekwinters/connor-multiplying-frogs/blob/main/CLAUDE.md) § What not to do |
| The permanent line and the v1 zero-permissions property | [ADR-0003](../adr/0003-network-boundary.md) |
| The save is local and `Core` owns it | [ADR-0004](../adr/0004-core-owns-the-save-format.md) |
| Where a resumed game is re-entered from | [#228](https://github.com/derekwinters/connor-multiplying-frogs/issues/228), carried by [#235](https://github.com/derekwinters/connor-multiplying-frogs/issues/235) |
| Tablets, landscape, API 24, 1920 × 1200 | [Tech stack](../engineering/tech-stack.md#target-platform) |
| Sideloaded APKs, no store | [CI/CD](../engineering/ci-cd.md) |
| Audience, session length, the four-player cap, attributions | [#7](https://github.com/derekwinters/connor-multiplying-frogs/issues/7) |
| Frogs are identified by colour, and nobody types a name | [Shared components](ui/shared-components.md#player-chip) |
| What the v0.2 proof of concept leaves out | [#198](https://github.com/derekwinters/connor-multiplying-frogs/issues/198) |

If this page and one of those disagree, the source wins, and the fix is to
correct this page in the pull request where you noticed it.
