# Testing

Strict TDD. Not "we value testing" — a hard requirement that the failing test
exists, and has been *seen* to fail, before the code that satisfies it.

This page and [Tech stack](tech-stack.md) are the same tier: the split described
there is what makes the discipline described here affordable.

## Red-green-refactor, strictly enforced

Every behaviour change goes through five steps, in this order:

1. **Write the test.** One behaviour, named for the behaviour, asserting the
   thing the issue actually asked for.
2. **Run it and watch it fail.** Not "assume it fails" — run it. Read the
   failure message and check it is failing for the reason you intended, not
   because of a typo, a missing reference, or a fixture that never ran. A test
   that passes here is testing nothing, and you have just learned that for free.
3. **Write the smallest code that makes it pass.** Not the design you have in
   mind for three issues' time. The smallest thing.
4. **Run it and watch it pass** — along with everything else. Green suite, not
   green test.
5. **Refactor**, with the suite green the whole way. This is where the design
   you had in mind is allowed in, and the tests are what make it safe.

### The rule that has teeth

**No implementation is written before its failing test exists and has been shown
to fail.**

Not "tests land in the same PR". Not "tests before review". Before the
implementation, in that order, in the working tree. The distinction matters
because a test written after the code is a test written by someone who already
knows the answer — it documents what the code does, agrees with its bugs, and
will pass no matter what you break.

Bug fixes are the same shape: reproduce the bug as a failing test first, and
only then fix it. A bug fix without a reproducing test is a bug that comes back.

### What "seen to fail" means in a PR

Say so. The PR body should be able to answer "what did the test say before the
fix?" — usually one line:

> `SplitsAtThreshold` failed with `Expected: 32, But was: 16` before the change.

An agent that cannot say this did not do step 2.

## Where tests live

| Layer | Location | Runner | Speed | Needs an editor |
| --- | --- | --- | --- | --- |
| Game logic | `Tests/Core/` | plain NUnit | seconds | no |
| Unity wiring | `Assets/Tests/EditMode/` | Unity Test Framework, EditMode, headless | minutes | yes |

### Core tests — plain NUnit, no engine

`Tests/Core` compiles the Core sources and nothing else. It compiles and runs with
`dotnet test`: no editor, no licence, no display, no container. This is where
essentially all the tests are, because this is where all the game logic is.

Fast enough to run on every save. That is the whole design goal — a red-green
loop you can run mid-thought is a loop you actually use, and one that takes two
minutes is a loop you start skipping "just this once".

### Unity tests — EditMode, headless

`Assets/Tests/EditMode` covers the shell: that a scene wires the right components
together, that a prefab has the fields it needs, that an adapter converts
between Core's vocabulary and the engine's correctly.

There should not be many of these, and if the count starts climbing that is a
signal the shell has grown logic that belongs in Core.

### No PlayMode tests, no on-device testing

**PlayMode tests are not required and generally not wanted.** They are slow,
flaky in a headless container, and they mostly test Unity rather than the game.
If a behaviour seems to need PlayMode to test, that is usually evidence the
behaviour is in the wrong assembly — move it to Core and test it there.

**On-device testing is not part of CI.** The device is where Derek and Connor
find out whether the game is fun. That is a real and important check, and it is
not something a test suite does.

## What this means for a new feature

The order is always the same:

1. **Model it in Core.** Types, rules, state transitions — in the engine-free
   assembly, in the game's vocabulary.
2. **Test it in Core.** Red, green, refactor, on the model. All the interesting
   cases live here: the boundaries, the "what if it's already at the cap", the
   "what if two happen in the same tick".
3. **Then wire a thin adapter** in the Unity layer to display it and feed input
   into it. If that adapter has a decision in it, the decision escaped from
   step 1 — put it back.

If you find yourself starting in a `MonoBehaviour`, stop. The feature will end
up testable only in an editor, and it will end up untested.

## Known limitation: EditMode tests run in CI, not in agent environments

Agent environments have no Unity editor and no licence. **EditMode tests cannot
be run there.** This is a fact about the environment, not a shortcoming of the
agent or the change.

### The sanctioned flow

1. Run the Core suite locally. This is not optional — it is the suite that
   covers the logic, it needs nothing to run, and "I couldn't run the tests" is
   never true of it.
2. Write EditMode tests when the change needs them, and say in the PR that they
   were written but not executed locally, and why.
3. Push. CI runs both suites — see [CI/CD](ci-cd.md).
4. **Watch CI.** Pushing and walking away is not finishing. If the EditMode
   suite goes red, that is yours to fix, exactly as if you had seen it fail
   locally.

### This is not a reportable deviation

Do **not** write "could not run EditMode tests locally" under
`## Deviations and Decisions`. It is the expected, documented, correct flow for
every agent-authored change, and reporting it as a deviation trains everyone to
skim that section — which is where the deviations that *do* matter live.

What does belong in that section: an EditMode test you decided not to write, a
Core test you could not make fail first and why, or a suite you skipped.
