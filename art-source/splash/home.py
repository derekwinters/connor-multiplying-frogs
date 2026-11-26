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

The same build also writes the game's own copies (#520): the art again under Assets/,
where TitleScreenView loads it, and the START sign's font. Unity's legacy font importer has
no way to choose a variable font's weight - imported as it is, Fredoka draws its default
instance, Light (300) - so the game gets a static Fredoka Bold, instanced at weight 700
from the committed variable font with fontTools, with its licence beside it.

Standard library only at import, so the tests can read the settings without the build's
libraries; bold_font_bytes() imports fontTools when it is called.
"""
import io
from pathlib import Path

import launch

REPO_ROOT = Path(__file__).resolve().parents[2]
FONTS = Path(__file__).resolve().parent / "fonts"

HOME_SIZE = launch.SPLASH_SIZE
HOME_VIEW = launch.SPLASH_VIEW
HOME_OUT = REPO_ROOT / "docs" / "specs" / "ui" / "images" / "title-screen" / "full.png"
FONT_FILES = ("Fredoka-Variable.ttf", "OFL.txt")

# The game's copies. Under Resources/ because the view loads them by path at runtime
# (Resources.Load), the way the water tile and the 3D models are loaded: no serialized
# reference, so nothing to lose on a domain reload or a scene save.
ASSETS_ART_OUT = (REPO_ROOT / "Assets" / "Art" / "TitleScreen" / "Resources" / "TitleScreen"
                  / "title-screen-1920x1200.png")
ASSETS_FONT_DIR = REPO_ROOT / "Assets" / "Fonts" / "Fredoka" / "Resources" / "Fredoka"
ASSETS_FONT_FILES = ("Fredoka-Bold.ttf", "OFL.txt")

# The START label is Fredoka Bold (docs/specs/ui/title-screen.md); title.py letters the
# drawn title at the same instance.
FONT_BOLD_WEIGHT = 700
FONT_BOLD_WIDTH = 100


def home_ids(ids):
    """The objects the title screen's art shows, in the given (back to front) order: all."""
    return list(ids)


def bold_font_bytes():
    """Fredoka Bold as a static TrueType file, instanced from the variable font. The same
    bytes every run: the head table's modified time is kept from the source rather than
    stamped with today's."""
    from fontTools.ttLib import TTFont
    from fontTools.varLib import instancer

    font = instancer.instantiateVariableFont(
        TTFont(str(FONTS / "Fredoka-Variable.ttf")),
        {"wght": FONT_BOLD_WEIGHT, "wdth": FONT_BOLD_WIDTH},
        inplace=False, updateFontNames=True)
    font.recalcTimestamp = False
    out = io.BytesIO()
    font.save(out)
    return out.getvalue()

