#!/usr/bin/env python3
"""The one definition of `Blocked by #N`, and the one way blockers are merged.

Six modules across five skills used to carry their own copy of this pattern,
and five of them their own copy of the union. They are not independent — they
are several readings of the same thing at several points in one pipeline — and
when they disagree nothing raises: the queue selector reads a line as a blocker
and refuses to build the issue, the sweep does not and so never wakes it, the
dashboard shows it as ready. The issue simply stops moving.

`issue-blockers` owns this because it is the skill that documents the format,
on the same "the side that writes the format owns the recognizer" rule that put
`has_analysis_signature` in `triage-issue`. See #65 and #147.

Importing it from another skill is a `sys.path` insertion and a plain import:

    _BLOCKERS = Path(__file__).resolve().parents[1] / "issue-blockers"
    if str(_BLOCKERS) not in sys.path:
        sys.path.insert(0, str(_BLOCKERS))

    from blocker_refs import blockers_of  # noqa: E402

**Stdlib only, and nothing happens at import time.** The test runner's
per-suite teardown only evicts modules living under the suite it just ran, so
this module stays in `sys.modules` for the whole run once any suite has
imported it. That is harmless for a module that only defines things, and only
for one.

See docs/engineering/issue-pipeline.md.
"""

from __future__ import annotations

import re

# "Blocked by #42", "blocked by: #42". Structured only, and deliberately NOT
# "Depends on: #42" — that is soft ordering with no native form, and reading it
# as a blocker would turn a preference into a hard gate the builder refuses to
# pass. "This is similar to #42" is not a dependency either.
TEXT_BLOCKER = re.compile(r"^\s*blocked\s+by\s*:?\s*#(\d+)\s*$", re.IGNORECASE | re.MULTILINE)


def text_blockers(body) -> set[int]:
    """Issue numbers named as blockers in a body — which is the wrong place."""
    return {int(number) for number in TEXT_BLOCKER.findall(body or "")}


def union_blockers(body, native) -> list[int]:
    """Text lines **unioned** with native edges, sorted.

    Union rather than either-or: an issue can have one dependency recorded
    natively and another still written in prose, and reading one source alone
    releases work that is still half-blocked.

    `native` is whatever the caller managed to find out. A caller that could not
    reach the dependency API passes an empty set and gets the text blockers it
    does know about — a smaller list, which is the safe direction.
    """
    return sorted(text_blockers(body) | {int(number) for number in (native or [])})


def blockers_of(issue) -> list[int]:
    """Every issue this one waits on, for an issue dict carrying its natives.

    The shape four of the five readers have: a snapshot where the dependency
    edges were fetched up front and left on the issue as `native_blockers`. The
    fifth fetches them at the moment it needs them and calls `union_blockers`.
    """
    return union_blockers((issue or {}).get("body"), (issue or {}).get("native_blockers"))
