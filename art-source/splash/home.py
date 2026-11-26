"""The title (home) screen's art: the whole splash picture, frogs and all.

Issue #484 drew the title screen on this art ten ways - with and without the frogs, the
log, the drawn title, a scrim, and several kinds of button - and Derek settled it on
2026-11-26: all the frogs, the drawn title, the log, no scrim, and a wooden sign reading
START (docs/specs/ui/title-screen.md). build_splash.py renders the art with these
settings into docs/specs/ui/images/title-screen/, where the agreed mockup loads it, and
copies the title's font and its licence beside it so the mockup can letter START the
way the title is lettered, offline.

It is the launch splash's picture, in the launch splash's window. The comparison pieces
the losing options needed (the scene without frogs, with and without the log, the title
on its own, a lily pad) are no longer built, and nor is the fade that took the title
glow's tail out of them: with the frogs in, the tail is behind the big frog.

Standard library only, so the tests can read the settings without the build's libraries.
"""
from pathlib import Path

import launch

HOME_SIZE = launch.SPLASH_SIZE
HOME_VIEW = launch.SPLASH_VIEW
HOME_OUT = (Path(__file__).resolve().parents[2] / "docs" / "specs" / "ui" / "images"
            / "title-screen" / "full.png")
FONT_FILES = ("Fredoka-Variable.ttf", "OFL.txt")


def home_ids(ids):
    """The objects the title screen's art shows, in the given (back to front) order: all."""
    return list(ids)
