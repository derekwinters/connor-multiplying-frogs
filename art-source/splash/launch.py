"""The launch splash: the whole splash composite, at the tablet's size.

Issue #482: the picture Unity shows while the app starts is "the full layered
composite (frogs and title) exported at 1920 x 1200 from the rebuilt art".
build_splash.py renders it from the vector layers with these settings and writes it to
Assets/Art/Splash/, where the build's AppSplash (Assets/Editor) puts it into
PlayerSettings.

Standard library only, so the tests can read the window without the build's libraries.
"""
from pathlib import Path

# The tablet, landscape: the one canvas every layout is drawn against.
SPLASH_SIZE = (1920, 1200)
# (x, y, width, height) of the window, in the splash canvas's pixels (1586 x 992).
# The canvas is a hair taller than 16:10 (1586 / 1.6 = 991.25), so the window keeps the
# whole width and trims 0.375 canvas px top and bottom - under half a pixel of the
# export - rather than stretching the picture.
SPLASH_VIEW = (0, 0.375, 1586, 991.25)
SPLASH_OUT = Path(__file__).resolve().parents[2] / "Assets" / "Art" / "Splash" / "splash-1920x1200.png"


def splash_ids(ids):
    """The objects the splash shows, in the given (back to front) order: all of them."""
    return list(ids)
