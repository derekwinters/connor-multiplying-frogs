"""The launch splash is the whole splash composite, at the tablet's 1920 x 1200.

Issue #482: Unity's launch splash shows Connor's picture instead of Unity's logo, and
the picture is "the full layered composite (frogs and title) exported at 1920 x 1200
from the rebuilt art". art-source/splash/build_splash.py renders it from the vector
layers (#481) with the window named in art-source/splash/launch.py, into
Assets/Art/Splash/, where the build's AppSplash puts it into PlayerSettings.

These tests pin:

- the window is the whole canvas, cut to 16:10 by trimming less than a pixel of it
  rather than stretching the picture;
- every object in the composite is in the splash, and nothing that is not;
- the committed file is a 1920 x 1200 PNG;
- and it really is the composite: it matches the committed composite PNG brought down
  to the same size.

The pixel test needs Pillow and NumPy and is reported as skipped, by name, where those
are not installed. See docs/engineering/tech-stack.md ("The launch splash").
"""

import importlib.util
import struct
import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SPLASH = REPO_ROOT / "art-source" / "splash"
OUT = REPO_ROOT / "Assets" / "Art" / "Splash" / "splash-1920x1200.png"
COMPOSITE_PNG = SPLASH / "composite.png"
CANVAS = (1586, 992)
TABLET = (1920, 1200)

sys.path.insert(0, str(SPLASH))
import launch  # noqa: E402

HAVE_PIXELS = all(importlib.util.find_spec(m) is not None for m in ("PIL", "numpy"))
NEEDS_PIXELS = unittest.skipUnless(HAVE_PIXELS, "Pillow and NumPy are not installed")

# The composite's objects, back to front (build_splash.object_ids()).
COMPOSITE = [
    "sky", "title-glow", "cloud-left", "cloud-right", "bank-left", "bank-right",
    "reeds-left", "reeds-right", "pond",
    "pad-left-back", "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front",
    "lily-left", "lily-right", "grass", "log", "tuft-left", "tuft-right",
    "arc-left", "frog-left", "arc-centre-left", "frog-centre-left", "arc-right", "frog-right",
    "frog-big", "title",
]


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    assert head[:8] == b"\x89PNG\r\n\x1a\n", f"{path} is not a PNG"
    return struct.unpack(">II", head[16:24])


class TheWindow(unittest.TestCase):
    def test_it_is_the_tablets_size(self):
        self.assertEqual(launch.SPLASH_SIZE, TABLET)

    def test_it_has_the_tablets_shape(self):
        _, _, w, h = launch.SPLASH_VIEW
        self.assertAlmostEqual(w / h, TABLET[0] / TABLET[1], places=9)

    def test_it_is_inside_the_canvas(self):
        x, y, w, h = launch.SPLASH_VIEW
        self.assertGreaterEqual(x, 0)
        self.assertGreaterEqual(y, 0)
        self.assertLessEqual(x + w, CANVAS[0])
        self.assertLessEqual(y + h, CANVAS[1])

    def test_it_trims_less_than_a_pixel_rather_than_cropping_the_picture(self):
        x, y, w, h = launch.SPLASH_VIEW
        self.assertEqual((x, w), (0, CANVAS[0]), "the whole width is shown")
        self.assertLess(CANVAS[1] - h, 1)
        self.assertAlmostEqual(y, (CANVAS[1] - h) / 2, msg="trimmed evenly top and bottom")

    def test_it_shows_every_object_in_the_composite(self):
        self.assertEqual(launch.splash_ids(COMPOSITE), COMPOSITE)

    def test_it_is_written_where_the_build_reads_it(self):
        self.assertEqual(launch.SPLASH_OUT, OUT)


class TheCommittedFile(unittest.TestCase):
    def test_it_is_a_1920_by_1200_png(self):
        self.assertTrue(OUT.is_file(), f"{OUT} is not committed")
        self.assertEqual(png_size(OUT), TABLET)


@NEEDS_PIXELS
class WhatItShows(unittest.TestCase):
    def test_it_is_the_composite(self):
        import numpy as np
        from PIL import Image

        splash = np.asarray(Image.open(OUT).convert("RGB"), dtype=float)
        comp = np.asarray(
            Image.open(COMPOSITE_PNG).convert("RGB").resize(TABLET, Image.LANCZOS),
            dtype=float)

        # Two renders of the same vectors at different sizes differ only along
        # edges; a different picture, or a shifted one, differs everywhere.
        self.assertLess(np.abs(splash - comp).mean(), 3.0)

    def test_it_is_opaque(self):
        from PIL import Image

        self.assertEqual(Image.open(OUT).mode, "RGB")


if __name__ == "__main__":
    unittest.main()
