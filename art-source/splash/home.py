"""The title (home) screen's wireframe art: the splash picture without the frogs.

Issue #484: Derek, 2026-11-24 - "The home screen where a user selects to play a game,
could be this same image, but without the frogs, to not crowd out the buttons." The
wireframe draws the real art, so build_splash.py renders it from the vector layers in
three pieces that the comparison mockups under docs/specs/ui/mockups/ stack:

    scene         everything except the frogs (with their hop arcs), the title and its glow
    scene-no-log  the same, without the log
    title         the drawn title and its glow on their own, transparent elsewhere

Separate pieces rather than one picture per option because the open questions on
docs/specs/ui/title-screen.md (drawn title or text, log or not, scrim or not) cross:
stacking lets each mockup show its option, and lets a scrim sit between the scene and
the title, without a picture per combination.

Standard library only, so the tests can read the pieces without the build's libraries.
"""
from pathlib import Path

import launch

# The tablet, landscape, and the launch splash's window: the whole canvas cut to 16:10.
HOME_SIZE = launch.SPLASH_SIZE
HOME_VIEW = launch.SPLASH_VIEW
HOME_OUT_DIR = Path(__file__).resolve().parents[2] / "docs" / "specs" / "ui" / "images" / "title-screen"

FROGS_AND_ARCS = ("frog-left", "frog-centre-left", "frog-right", "frog-big",
                  "arc-left", "arc-centre-left", "arc-right")
TITLE = ("title-glow", "title")

# piece name -> the objects it hides; "title" instead keeps only TITLE.
PIECES = {
    "scene": FROGS_AND_ARCS + TITLE,
    "scene-no-log": FROGS_AND_ARCS + TITLE + ("log",),
    "title": None,
}


def piece_ids(piece, ids):
    """The objects a piece shows, in the given (back to front) order."""
    hidden = PIECES[piece]
    if hidden is None:
        return [oid for oid in ids if oid in TITLE]
    return [oid for oid in ids if oid not in hidden]
