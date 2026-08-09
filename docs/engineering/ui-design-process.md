# UI design process

**No UI gets built without an approved wireframe. Not even graybox.**

**Every layout and every dialog.** Not just the big screens — a confirm box, a
toast, an end-of-turn panel each get their own wireframe. A dialog is the
easiest thing to slip in without one and, being small, the easiest to get
wrong in a way nobody notices until Connor is holding it.

UI is the part of this game Connor has the strongest opinions about and the part
that is most expensive to redo. A layout argued about as a picture costs an
evening; the same argument had about working code costs the code.

There is also a specific failure this prevents. An agent handed "add a pause
screen" with no wireframe will produce *a* pause screen — a competent, tested,
plausible one — and it will be somebody's idea of a pause screen rather than
Connor's. Nothing in the test suite catches that, and by the time it's visible
it's built.

## What a wireframe is: a dual artifact

Two files per screen, both under `docs/specs/ui/`, both required.

### 1. The screen spec — `docs/specs/ui/<screen>.md`

Structured text. What is on the screen, how it is arranged, what each element
does, and — critically — the **named constants** that define its geometry.

```markdown
# Pause screen

**Invariant:** the pause screen never covers the score readout.

## Layout

Vertical stack, centred, over a dimmed copy of the playfield.

| Element | Constant | Value |
| --- | --- | --- |
| Panel width | `PausePanelWidth` | 280 dp |
| Panel corner radius | `PausePanelCornerRadius` | 16 dp |
| Gap between buttons | `PauseButtonSpacing` | 12 dp |
| Backdrop dim | `PauseBackdropOpacity` | 0.6 |

## Elements

- **Resume** — returns to play. Primary; first in tab order.
- **Restart** — confirms first (`RestartConfirmDelay`, 0 s — no delay, but the
  confirm step is required).
- **Quit to menu** — confirms first.

## Behaviour

- Opening pauses the simulation; nothing in Core advances.
- Hardware back button resumes, it does not quit.
```

### 2. The mockup — `docs/specs/ui/mockups/<screen>.html`

A **1:1 HTML mockup**: a static page sized to the target device, laying out the
real elements at the real proportions.

- **1920 × 1200, landscape.** That is the target device — a kids' Android
  tablet — and it is the viewport every mockup is drawn at. See
  [target platform](tech-stack.md#target-platform).
- 1:1 because "roughly this" is how a button ends up too small for an
  eight-year-old's thumb. Real viewport, real units.
- HTML because it opens in a browser on the tablet itself. Connor can look at
  the actual thing at the actual size, on the device he plays on, and say it's
  wrong.
- Static. No behaviour, no framework, no build step. It is a picture that
  happens to be made of divs.

### Why both

The spec is what the code is built from; the mockup is what the decision is made
from. Neither substitutes:

- A spec alone gets approved by someone who read the words and imagined a
  different screen.
- A mockup alone leaves every number to be re-guessed at implementation time,
  and re-guessed differently.

## The named constants are the origin, not an afterthought

**The constants in the spec table are the same constants the code declares.**
Same names, same values. The spec is where they are born.

```csharp
// Assets/Scripts/Unity/Views/PausePanelView.cs
[SerializeField] float _pausePanelWidth = 280f;          // PausePanelWidth
[SerializeField] float _pauseButtonSpacing = 12f;        // PauseButtonSpacing
```

This is the same rule as
[named values](tech-stack.md#geometry-layout-and-tuning-values-are-named-variables),
arriving one step earlier. The point is the direction of travel: the number is
decided when the layout is agreed, and the code *receives* it. If the code
invents a number the spec doesn't have, the layout was not fully agreed — go
back and agree it, don't paper over it in an implementation PR.

It also means "make the buttons further apart" is answerable in one place, by
someone who doesn't read C#.

Changing a constant after approval is a spec change: update the spec table and
the mockup, and say so in the PR under
[how the spec is changing](agent-workflow.md#how-the-spec-is-changing).

## The loop

**propose → review → approve → distill → implement → verify**

1. **Propose.** Open a `type:wireframe` issue. Write the spec page and build the
   mockup. Where there's a real choice, propose *two* mockups — comparing two
   pictures is a much easier conversation than critiquing one.
2. **Review.** Connor looks at the mockup, on a phone, at 1:1. Derek looks at
   the spec. Feedback goes on the issue.
3. **Approve.** The wireframe issue is **closed** by a human. Closing it is the
   approval — there is no separate approved label to forget to apply.
4. **Distill.** Fold the agreed decisions back into the spec page and mockup so
   they describe what was agreed, not what was proposed. A spec page that
   requires reading the issue thread to interpret has not been distilled.
5. **Implement.** An implementation issue, blocked-by the wireframe issue, built
   against the spec's constants.
6. **Verify.** The PR shows the built screen against the mockup — a screenshot,
   side by side. The check is "does it match the agreed picture", which is a
   question anyone can answer.

## The gate

Before writing UI code, ask: **does an approved wireframe cover this?**

### Stop and flag when it doesn't

If the issue asks for UI structure and there's no approved wireframe:

1. Stop. Do not sketch one in code "to be replaced later" — placeholder layout
   is the code most likely to survive to release, because it works and nobody
   goes back to it.
2. Comment on the issue saying which screen needs a wireframe.
3. Open the `type:wireframe` issue, and add a **blocked-by** relationship from
   the implementation issue to it.
4. Work something else.

### What counts as structure

Gated:

- adding, removing, or moving an element;
- changing sizes, spacing, or proportions;
- changing what an element does, or the order things happen in;
- a new screen, dialog, or overlay of any kind.

### What isn't gated

**Purely visual restyling** goes ahead without a wireframe: colours within the
palette, a font weight, an icon swapped for a clearer one at the same size, a
shadow, a transition's easing.

The test is whether the change would alter the mockup's *layout*. If the mockup
would still be an accurate picture of the screen afterwards, it's a restyle. If
the mockup would now be wrong, it's structure — and the fix is to update the
mockup, which is the wireframe loop.

This carve-out is deliberate. A process that gates every pixel is a process
people route around, and then it gates nothing.

## Conventions

### The `type:wireframe` label and the `Wireframe:` prefix

Wireframe issues are labelled `type:wireframe` and titled with a `Wireframe:`
prefix:

```text
Wireframe: pause screen
Wireframe: level select
```

The label is what tooling filters on; the prefix is what a human scanning the
issue list sees. Both, because they serve different readers.

### How the blocker sweep treats a wireframe blocker

The pipeline's [auto-revisit](issue-pipeline.md#auto-revisit-when-a-blocker-clears)
returns an issue to triage once its blockers have closed. For a wireframe
blocker that is exactly right — **closed is approved**, so an implementation
issue blocked by a wireframe wakes up precisely when the wireframe is agreed.

The carve-out runs the other way: **a `type:wireframe` issue is never itself
auto-revisited.** Unblocking a wireframe doesn't make it agreeable; it still
needs a person looking at a picture. Waking one automatically would produce an
issue claiming to be ready that no agent can act on. They surface on the
dashboard as waiting-on-a-human instead.

So: wireframes are woken by people, and the things they block are woken by the
wireframe closing.
