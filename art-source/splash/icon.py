"""The app icon's crop of the splash art: which window, at what size, with what hidden.

Derek's call on 2026-11-24 (#483): "The app icon will be a cropped version of this
image, without the title text, zoomed in to the frog on the log in center of the
image." build_splash.py renders the icon from the vector layers with these settings
and writes it to Assets/Art/app-icon-source.png; .github/scripts/generate_app_icons.py
makes every Android icon from that.

Standard library only, so the tests can read the crop without the build's libraries.
"""
from pathlib import Path

# (x, y, side) of the square window, in the splash canvas's pixels (1586 x 992).
# Centred left-right on the big frog (its box is x 584-1002, y 385-852), from just
# above its eyes to just below the log, so the eyes and the log are both inside the
# circle a round launcher cuts.
ICON_CROP = (473, 345, 640)
# The rendered icon source, px square: 1.6x the window, and over twice the largest
# Android icon (the 512 px store image), so every icon is a downscale.
ICON_SIZE = 1024
# Hidden in the icon: the title and its glow, and the small jumping frogs with their
# hop arcs (the left-centre frog and the right arc would poke into the window).
ICON_HIDDEN = ("title", "title-glow",
               "frog-left", "frog-centre-left", "frog-right",
               "arc-left", "arc-centre-left", "arc-right")
ICON_OUT = Path(__file__).resolve().parents[2] / "Assets" / "Art" / "app-icon-source.png"


def icon_ids(ids):
    """The objects the icon shows, in the given (back to front) order."""
    return [oid for oid in ids if oid not in ICON_HIDDEN]
