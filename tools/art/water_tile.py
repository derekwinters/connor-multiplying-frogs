"""Multiplying Frogs -- the pond's water tile.

Re-runnable, in the same spirit as frog_model.py, lilypad_model.py and
log_model.py: draws the tile from nothing and writes one PNG. Nothing is
touched by hand; change the numbers here and run it again.

    python3 -m venv venv && venv/bin/pip install numpy pillow
    venv/bin/python water_tile.py                  # writes the tile into Assets/Art
    venv/bin/python water_tile.py --out <file.png> # writes it somewhere else

What it follows (docs/specs/ui/game-board.md, "The water's marks"):

* One seamless 512 px tile, `PondWaterTile` (PW-1). Every mark is drawn nine
  times -- once where it is and once a whole tile away in each direction -- so
  a mark that crosses an edge comes back in on the opposite one.
* The tile is `PondWater` (#9FD8F2) with see-through white marks on it (PW-2):
  arcs in groups of one to three, each group 24 to 40 px wide per arc, at
  alpha 140 to 190, with a 4 or 5 px stroke. Between the marks the water is
  exactly `PondWater`.
* The marks are spaced by Poisson-disc sampling on the torus: 30 groups, no
  two closer than 62 px, measured across the wrap -- the calm density Derek
  approved on 2026-11-20.
* Everything is drawn at 2x and downsampled with Lanczos, which is what keeps
  the arcs' edges soft.

It is deterministic. The seed is fixed and NumPy's PCG64 generator draws the
same numbers on every platform, so a run writes the committed PNG byte for
byte. This is the approved generator's `SM()` with its own defaults (n=30,
mind=62, seed=9), lifted out of the exploration script it was written in; the
order random numbers are drawn in is kept exactly, because changing it moves
every mark.

One thing kept for that reason that a fresh script would not do: each mark's
stroke width (4 or 5 px) is drawn anew for each of its nine copies, so the
part of a mark that crosses the tile's left or right edge can be 1 px thicker
or thinner than the rest of it. Changing that changes the approved tile.
"""

import argparse
import os

import numpy as np
from PIL import Image, ImageDraw

# --------------------------------------------------------------------------
# Spec constants (game-board.md)
# --------------------------------------------------------------------------

POND_WATER = (0x9F, 0xD8, 0xF2)  # PondWater
TILE = 512                       # PondWaterTile, px

# --------------------------------------------------------------------------
# The approved look ("soft + mix", Derek, 2026-11-20)
# --------------------------------------------------------------------------

SUPERSAMPLE = 2                  # drawn at 2x, then downsampled
GROUPS = 30                      # how many groups of arcs
MIN_SPACING = 62                 # px between group centres, across the wrap
SEED = 9
ARCS_PER_GROUP = (1, 2, 2, 3)    # drawn from uniformly: two arcs most often
ARC_WIDTH = (24, 40)             # px per arc, uniform
ARC_ALPHA = (140, 190)           # white at this alpha, uniform
ARC_STROKE = (4, 5)              # px
ARC_HALF_HEIGHT = 10             # px: each arc's ellipse is this far above and below its centre
ARC_SWEEP = (200, 340)           # degrees: the top of the ellipse, PIL's clockwise-from-+x
PLACEMENT_TRIES = 20000

WHITE = (255, 255, 255)

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.join(
    HERE, "..", "..", "Assets", "Art", "Water", "Resources", "Water", "pond_water_tile.png")


def group_centres(count, spacing, seed):
    """Poisson-disc points on the TILE x TILE torus, and the generator the
    marks are then drawn from (seed + 100), exactly as the approved script."""
    rng = np.random.default_rng(seed)
    points = []
    tries = 0

    def wrapped_distance_squared(a, b):
        dx = abs(a[0] - b[0])
        dx = min(dx, TILE - dx)
        dy = abs(a[1] - b[1])
        dy = min(dy, TILE - dy)
        return dx * dx + dy * dy

    while len(points) < count and tries < PLACEMENT_TRIES:
        tries += 1
        p = rng.uniform(0, TILE, 2)
        if all(wrapped_distance_squared(p, q) > spacing ** 2 for q in points):
            points.append(p)

    return points, np.random.default_rng(seed + 100)


def draw_arcs(draw, x, y, width, count, colour, stroke, half_height):
    """`count` arcs side by side, centred on (x, y), in supersampled pixels."""
    for i in range(count):
        cx = x + (i - (count - 1) / 2) * width
        draw.arc([cx - width / 2, y - half_height, cx + width / 2, y + half_height],
                 ARC_SWEEP[0], ARC_SWEEP[1], fill=colour, width=stroke)


def draw_wrapped(draw_one, x, y):
    """Draw a mark at (x, y) and a whole tile away in every direction."""
    for dx in (-TILE, 0, TILE):
        for dy in (-TILE, 0, TILE):
            draw_one((x + dx) * SUPERSAMPLE, (y + dy) * SUPERSAMPLE)


def water_tile(count=GROUPS, spacing=MIN_SPACING, seed=SEED):
    """The tile, as a TILE x TILE RGB image."""
    size = TILE * SUPERSAMPLE
    water = Image.new("RGB", (size, size), POND_WATER).convert("RGBA")
    marks = Image.new("RGBA", water.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(marks)

    centres, rng = group_centres(count, spacing, seed)

    for x, y in centres:
        arcs = int(rng.choice(ARCS_PER_GROUP))
        width = rng.uniform(*ARC_WIDTH)
        alpha = int(rng.uniform(*ARC_ALPHA))

        def one(sx, sy, arcs=arcs, width=width, alpha=alpha):
            # The stroke is drawn per copy -- see the docstring.
            stroke = int(rng.choice(ARC_STROKE)) * SUPERSAMPLE
            draw_arcs(draw, sx, sy, width * SUPERSAMPLE, arcs, WHITE + (alpha,), stroke,
                      ARC_HALF_HEIGHT * SUPERSAMPLE)

        draw_wrapped(one, x, y)

    tile = Image.alpha_composite(water, marks).convert("RGB")
    return tile.resize((TILE, TILE), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", default=DEFAULT_OUT, help="where to write the PNG")
    args = parser.parse_args()

    out = os.path.abspath(args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    water_tile().save(out)
    print(out)


if __name__ == "__main__":
    main()
