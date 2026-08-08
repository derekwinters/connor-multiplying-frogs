# Shared components

!!! note "Stub — fills in as components are specified"
    No shared components exist yet. This page is the home they go in, and the
    rule for when something belongs here.

Reusable atomic pieces of UI, specified **once** and referenced by screen pages.

A screen page says *"the confirm dialog ([shared](shared-components.md))
appears"* and moves on. It does not restate the dialog's padding, its button
order, or how it dismisses.

## Why this page exists

Three screens that each describe "the primary button" will, within a month,
describe three subtly different buttons. Nobody will have decided they should
differ — the descriptions were written on different days by people making
reasonable choices. Then a change to the button means finding all three, and one
gets missed.

One page, three references, one edit.

## When something belongs here

A piece belongs here once **either** is true:

- it appears on two or more screens; or
- it is going to, and the second screen is already specified.

Do not pre-emptively generalise a one-screen element. A component extracted
before its second use is a component shaped by exactly one caller, and the
second caller ends up working around it.

Moving an element here later is cheap: cut its section out of the screen page,
paste it here, and leave a reference behind.

## The per-component template

### 1. What it is

One sentence, and where it is used — a list of the screen pages that reference
it. That list is how you find out what a change to this page affects.

### 2. Invariants

`**Invariant:** …` lines. Shared components are where invariants earn the most:
"every destructive action confirms" is one rule stated once, rather than a thing
five screens each have to remember.

### 3. Named constants

The same constants table screen pages use. These are the code's constants.

### 4. States

Every state the component can be in — default, pressed, disabled, loading,
error — and what it looks like in each. A component page that only describes the
default state is a component that gets a different disabled style on every
screen.

### 5. Behaviour

What it does when interacted with, what it emits, and what it never does.

## Components

*None yet.*
