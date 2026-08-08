#!/usr/bin/env python3
"""Decide whether a Unity EditMode run actually passed.

The exit code alone cannot answer that, in both directions:

  * **Falsely red.** Unity's editor sometimes dies during *teardown*, after a
    fully green run has already written its results. Treating that as a failure
    means a red PR with nothing wrong in it, and the fix everyone reaches for is
    "re-run until it's green" — which trains people to re-run real failures too.

  * **Falsely green.** The runner can exit 0 having run *zero* tests. A licence
    problem, a missing assembly, or a filter that matched nothing all look like
    success from the outside, and "0 tests passed" is the most dangerous green
    there is.

So the verdict comes from the results XML, and the exit code is forgiven in
exactly one narrow case: a code known to come from teardown, on a run whose
results say everything passed. Forgiveness is never about results.

Usage:
    python3 .github/scripts/verify_editmode_results.py \\
        --results artifacts/editmode-results.xml --exit-code "$UNITY_EXIT_CODE"

Stdlib only, so it runs in any job without a setup step.
"""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ElementTree
from pathlib import Path

# Unity on Linux segfaults during editor shutdown often enough to be a known
# quantity; 139 is the shell's way of reporting SIGSEGV. It is forgiven ONLY
# when the results are green.
#
# Adding a code here needs evidence — a run where the results XML is complete
# and green and the editor still exited nonzero. Anything else is a real
# failure being hidden.
DEFAULT_FORGIVEN_EXIT_CODE = 139


class Verdict:
    def __init__(self, ok: bool, reason: str) -> None:
        self.ok = ok
        self.reason = reason

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"Verdict(ok={self.ok!r}, reason={self.reason!r})"


def _count(element: ElementTree.Element, name: str) -> int | None:
    """An integer attribute, or None when it is absent or not a number.

    None rather than 0 on purpose: a `failed` count defaulting to zero would
    turn an unreadable results file into a pass.
    """
    raw = element.get(name)
    if raw is None:
        return None
    try:
        return int(raw)
    except ValueError:
        return None


def verify(
    results: str | None,
    exit_code: int,
    forgiven: tuple[int, ...] = (DEFAULT_FORGIVEN_EXIT_CODE,),
) -> Verdict:
    """The verdict for one run, from its results XML and its exit code."""
    if results is None:
        return Verdict(
            False,
            f"The run produced no results (exit code {exit_code}). Unity died before "
            "writing the XML, so there is nothing to trust — this is a failure, not a "
            "teardown crash.",
        )

    try:
        root = ElementTree.fromstring(results)
    except ElementTree.ParseError as error:
        return Verdict(False, f"The results XML could not be parsed: {error}.")

    if root.tag != "test-run":
        return Verdict(
            False,
            f"Expected a <test-run> document, found <{root.tag}>. This is not an NUnit "
            "results file.",
        )

    total = _count(root, "total")
    failed = _count(root, "failed")
    inconclusive = _count(root, "inconclusive")

    for name, value in (("total", total), ("failed", failed), ("inconclusive", inconclusive)):
        if value is None:
            return Verdict(
                False,
                f"The results XML has no readable '{name}' count, so the run cannot be "
                "verified. Treating that as a pass is how an unreadable file becomes a "
                "green build.",
            )

    if total == 0:
        return Verdict(
            False,
            "The run reported 0 tests. That is not a pass — it usually means the licence "
            "failed, an assembly did not compile, or a filter matched nothing.",
        )

    if failed:
        return Verdict(False, f"{failed} failed of {total} tests.")

    if inconclusive:
        return Verdict(
            False,
            f"{inconclusive} of {total} tests were inconclusive. An inconclusive test is "
            "one that did not answer the question it was asked.",
        )

    # Results are green from here on. Only now does the exit code matter.
    if exit_code == 0:
        return Verdict(True, f"All {total} tests passed.")

    if exit_code in forgiven:
        return Verdict(
            True,
            f"All {total} tests passed. Unity exited {exit_code} during teardown, which is "
            "forgiven — the results were already written.",
        )

    return Verdict(
        False,
        f"All {total} tests passed, but Unity exited with exit code {exit_code}, which is "
        f"not a known teardown code (forgiven: {', '.join(str(code) for code in forgiven)}). "
        "Something went wrong after the tests ran; look at the log before forgiving it.",
    )


def verify_file(
    path: Path,
    exit_code: int,
    forgiven: tuple[int, ...] = (DEFAULT_FORGIVEN_EXIT_CODE,),
) -> Verdict:
    if not path.is_file():
        return Verdict(
            False,
            f"No results file at {path}. A missing results file is a failure: the gate "
            "cannot tell a green run from one that never started.",
        )

    return verify(path.read_text(encoding="utf-8", errors="replace"), exit_code, forgiven)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results", required=True, help="path to the NUnit results XML")
    parser.add_argument("--exit-code", type=int, required=True, help="Unity's exit code")
    parser.add_argument(
        "--forgive",
        type=int,
        action="append",
        default=[],
        metavar="CODE",
        help="an additional teardown exit code to forgive on a green run",
    )
    arguments = parser.parse_args(argv)

    forgiven = tuple([DEFAULT_FORGIVEN_EXIT_CODE] + arguments.forgive)
    verdict = verify_file(Path(arguments.results), arguments.exit_code, forgiven)

    print(verdict.reason, file=sys.stdout if verdict.ok else sys.stderr)
    return 0 if verdict.ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
