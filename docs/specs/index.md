# Specs

**These pages are the implementation contract.** They say what Multiplying
Frogs does — the rules it plays by, the edges of the product, and the layout of
every screen — precisely enough that code is built from them and a pull request
can be held against them.

The [site as a whole](../index.md) is the design contract; this section is the
part of it a build is checked against line by line. The
[introduction](../intro/index.md) explains the game to a person and is allowed
to be friendly about it. These pages are not friendly, on purpose: where the
two describe the same rule, this section is the one the code follows.

!!! warning "The code and a page here cannot quietly disagree"

    When they disagree, one of them is a bug, and **which one moves is a
    decision, not an assumption**. Say so on the issue and let a human pick the
    side that changes. Editing whichever was easier to change is how a contract
    stops being one.

## A gap is a stop, not an invitation

Behaviour these pages do not describe is behaviour nobody has decided yet. The
response is to open a `type:question` issue naming the choice and the options,
and to go and work on something else — never to pick the sensible-looking
answer and leave a note about it in the pull request. A guessed mechanic that
ships is the game drifting away from being Connor's, which is the whole reason
for building it this way.

### Telling a rule gap from a presentation gap

[ADR-0001](../adr/0001-rules-sacred-presentation-ours.md) draws the line this
section is organised around, and it is the fastest way to work out what a
particular gap costs.

| The gap is in | Whose answer it is | What to do |
| --- | --- | --- |
| A **rule** — how far a frog moves, what a wrong answer costs, how many players there are, what winning means | Connor's, via the classroom game | Check [reference material](reference/index.md) first; the board may already settle it. If it does not, open the question and stop. |
| **Presentation** — layout, animation, what is tapped, what the screen shows and when | The project's | Design it. For anything with a layout, that means an agreed wireframe first, not a layout invented in code. |

A presentation gap is ours to fill, which is not the same as ours to skip. UI
structure starts as a wireframe under the
[UI design process](../engineering/ui-design-process.md) and becomes a page
under [UI](ui/index.md) before any of it becomes code.

Why a page here says what it says is often one level down, in
[Decisions](../adr/index.md): a spec page states what is true now, an ADR
records what was chosen once and why.

## What the first complete release covers

**v1 is the classroom game on one tablet.** Two to four players share an
Android tablet held landscape and pass it around: roll the die, draw a card
from the pile the roll names, work the multiplication out on screen, and hop
one lily pad up your own lane — or one back if you got it wrong. The first frog
onto its End log wins. That is the whole of it.

It costs nothing, has no accounts and no advertising, makes no network calls,
and keeps everything it stores on the device. Those are permanent product
invariants rather than v1 defaults. [Product scope](product-scope.md) is the
authority on the product's edges and states them in the form a pull request can
be held against; what is deliberately *out* of v1 — sound, other kinds of
arithmetic, a second board, a tutorial, players five to eight — is listed once
in [the vision](../intro/vision.md#what-this-game-is-not), and each item is
parked rather than dropped.

!!! danger "One page in this section is not the contract"

    [Future ideas](future-ideas.md) is the exact opposite of one. Everything on
    it is deliberately out of scope, and finding an idea there is not
    permission to build it. It sits in this section because the ideas are about
    the game, not because any of them are agreed.

## Pages

Grouped by what they describe. **The list grows as spec pages land** — a system
with no page here is a system whose contract has not been written yet, which is
worth knowing in itself.

### The game

| Page | What it settles |
| --- | --- |
| [Reference material](reference/index.md) | The classroom board, photographed and transcribed: the rules card verbatim, the three card piles and the rolls that reach them, the nine positions in a lane, and the handful of things the board leaves open. |

The precise rules spec — every rule stated edge case by edge case for the code
to be built against — is
[issue #199](https://github.com/derekwinters/connor-multiplying-frogs/issues/199)
and does not exist yet. Until it lands, reference material is where the rules
are recorded, and [how to play](../intro/how-to-play.md) is the same rules in
the friendly form.

### The product

| Page | What it settles |
| --- | --- |
| [Product scope](product-scope.md) | The five invariants, the target device, how you get hold of the game, where the network boundary sits, and what the save is allowed to be. |

### The screens

[UI](ui/index.md) is the layout contract: one page per screen and per dialog,
each carrying its invariants, regions, anchors and named constants, with a 1:1
HTML [mockup](ui/mockups/index.md) alongside it.

| Page | What it settles |
| --- | --- |
| [UI overview](ui/index.md) | The nine screens of v1 in the order a game meets them, and the template every screen page follows. |
| [Shared components](ui/shared-components.md) | The pieces used on more than one screen — buttons, the player chip, the confirm dialog — specified once and referenced everywhere else. |
| [Mockups](ui/mockups/index.md) | The rendered 1920 × 1200 mockups the screen pages link to. |

The individual screen pages are listed on the
[UI overview](ui/index.md#screens) rather than repeated here.

### Not the contract

| Page | What it is |
| --- | --- |
| [Future ideas](future-ideas.md) | The parking lot. Ideas deliberately not being built, each with the reason it was set aside and what it would take to promote it. |
