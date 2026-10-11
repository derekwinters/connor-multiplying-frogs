#!/usr/bin/env python3
"""Generate the Android app-icon set from Connor's artwork.

The one picture is committed unmodified at Assets/Art/app-icon-source.png.
Everything Android wants is derived from it here, so the icon is changed by
changing the picture (or the mode) and rerunning this — never by editing a
generated file.

    python3 .github/scripts/generate_app_icons.py               # Fill, the default
    python3 .github/scripts/generate_app_icons.py --mode shrink

What it writes, into Assets/Art/AppIcon/:

    adaptive-foreground-<density>.png   108dp canvas: 108, 162, 216, 324, 432 px
    adaptive-background-<density>.png   the same sizes
    legacy-<density>.png                48dp: 48, 72, 96, 144, 192 px
    round-<density>.png                 the same, masked to a circle
    play-store-512.png                  the store listing, opaque

An adaptive icon is a 108dp canvas of which the launcher shows the middle 72dp
(the viewport), cut to whatever shape the launcher likes; only the middle 66dp
circle is guaranteed to survive every shape. The mode says how the picture sits
in that:

    fill    The picture spans the 72dp viewport. Round launchers trim the
            scenery in the corners; the frog in the middle stays whole. Nothing
            is added: the background layer is the picture too. Derek's call,
            2026-10-11.
    shrink  The whole picture is scaled into the 66dp safe circle on a plain
            cream background, so no launcher ever trims it.

The legacy, round and store images are what the adaptive icon shows through its
viewport, so all of them agree with each other in either mode.

Needs Pillow. See docs/engineering/tech-stack.md ("The app icon").
"""

from __future__ import annotations

import argparse
import math
import sys
from pathlib import Path
from typing import NamedTuple

REPO_ROOT = Path(__file__).resolve().parents[2]

SOURCE = Path("Assets/Art/app-icon-source.png")
OUTPUT_DIR = Path("Assets/Art/AppIcon")

FILL = "fill"
SHRINK = "shrink"
MODES = (FILL, SHRINK)
DEFAULT_MODE = FILL

# Shrink's background: the cream of the picture's own sky.
SHRINK_BACKGROUND = (245, 242, 234, 255)

# Android's density buckets, as multiples of mdpi (1px per dp).
DENSITIES = {"mdpi": 2, "hdpi": 3, "xhdpi": 4, "xxhdpi": 6, "xxxhdpi": 8}
DENSITY_DIVISOR = 2

ADAPTIVE_CANVAS_DP = 108
ADAPTIVE_VIEWPORT_DP = 72
ADAPTIVE_SAFE_CIRCLE_DP = 66
LEGACY_DP = 48
PLAY_STORE_PX = 512

# Supersampling for the round icon's circle edge, so it is antialiased.
MASK_SUPERSAMPLE = 4


class Box(NamedTuple):
    left: int
    top: int
    size: int


def _px(dp: int, density: str) -> int:
    return dp * DENSITIES[density] // DENSITY_DIVISOR


def outputs() -> list[tuple[str, int]]:
    """Every file the generator writes, with its square size in pixels."""
    files: list[tuple[str, int]] = []
    for density in DENSITIES:
        canvas = _px(ADAPTIVE_CANVAS_DP, density)
        legacy = _px(LEGACY_DP, density)
        files += [
            (f"adaptive-foreground-{density}.png", canvas),
            (f"adaptive-background-{density}.png", canvas),
            (f"legacy-{density}.png", legacy),
            (f"round-{density}.png", legacy),
        ]
    files.append((f"play-store-{PLAY_STORE_PX}.png", PLAY_STORE_PX))
    return files


def picture_box(canvas: int, mode: str) -> Box:
    """Where the picture goes on an adaptive-icon canvas `canvas` px square."""
    if mode == FILL:
        size = canvas * ADAPTIVE_VIEWPORT_DP // ADAPTIVE_CANVAS_DP
        left = (canvas - size) // 2
        return Box(left, left, size)

    if mode == SHRINK:
        radius = canvas * ADAPTIVE_SAFE_CIRCLE_DP / ADAPTIVE_CANVAS_DP / 2
        centre = canvas / 2
        # The largest square inside the circle, shrunk a pixel at a time until
        # its whole-pixel placement still keeps every corner inside.
        size = math.floor(radius * math.sqrt(2))
        while True:
            left = (canvas - size) // 2
            far = max(abs(left - centre), abs(left + size - centre))
            if math.hypot(far, far) <= radius:
                return Box(left, left, size)
            size -= 1

    raise ValueError(f"unknown mode {mode!r}; expected one of {', '.join(MODES)}")


def _layers(source, canvas: int, mode: str):
    """The (foreground, background) adaptive layers at `canvas` px."""
    from PIL import Image

    picture = source.convert("RGBA")
    box = picture_box(canvas, mode)

    foreground = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    foreground.paste(picture.resize((box.size, box.size), Image.LANCZOS), (box.left, box.top))

    if mode == FILL:
        background = picture.resize((canvas, canvas), Image.LANCZOS)
    else:
        background = Image.new("RGBA", (canvas, canvas), SHRINK_BACKGROUND)

    return foreground, background


def _viewport(source, size: int, mode: str):
    """What the adaptive icon shows through its 72dp viewport, at `size` px."""
    from PIL import Image

    canvas = size * ADAPTIVE_CANVAS_DP // ADAPTIVE_VIEWPORT_DP
    foreground, background = _layers(source, canvas, mode)
    composed = Image.alpha_composite(background, foreground)
    inset = (canvas - size) // 2
    return composed.crop((inset, inset, inset + size, inset + size))


def _circle(image):
    from PIL import Image, ImageChops, ImageDraw

    size = image.size[0]
    big = size * MASK_SUPERSAMPLE
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, big - 1, big - 1), fill=255)
    mask = mask.resize((size, size), Image.LANCZOS)

    rounded = image.copy()
    rounded.putalpha(ImageChops.multiply(image.getchannel("A"), mask))
    return rounded


def render(source, mode: str = DEFAULT_MODE) -> dict:
    """Every output, by file name, as a Pillow image."""
    picture_box(ADAPTIVE_CANVAS_DP, mode)  # refuse an unknown mode up front

    rendered = {}
    for density in DENSITIES:
        canvas = _px(ADAPTIVE_CANVAS_DP, density)
        legacy = _px(LEGACY_DP, density)
        foreground, background = _layers(source, canvas, mode)
        rendered[f"adaptive-foreground-{density}.png"] = foreground
        rendered[f"adaptive-background-{density}.png"] = background
        rendered[f"legacy-{density}.png"] = _viewport(source, legacy, mode)
        rendered[f"round-{density}.png"] = _circle(_viewport(source, legacy, mode))
    rendered[f"play-store-{PLAY_STORE_PX}.png"] = _viewport(source, PLAY_STORE_PX, mode)
    return rendered


def write(rendered: dict, directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    for name, image in sorted(rendered.items()):
        image.save(directory / name, format="PNG")


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--mode", choices=MODES, default=DEFAULT_MODE,
                        help=f"how the picture sits in the adaptive mask (default: {DEFAULT_MODE})")
    args = parser.parse_args(argv)

    try:
        from PIL import Image
    except ImportError:
        print("Pillow is needed: python3 -m pip install Pillow", file=sys.stderr)
        return 1

    with Image.open(REPO_ROOT / SOURCE) as source:
        rendered = render(source, args.mode)
    write(rendered, REPO_ROOT / OUTPUT_DIR)
    print(f"Wrote {len(rendered)} files to {OUTPUT_DIR} ({args.mode}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
