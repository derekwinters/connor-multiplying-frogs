"""The app icon is a close crop of the splash art's big frog on the log.

Derek's call on 2026-11-24 (#483): "The app icon will be a cropped version of this
image, without the title text, zoomed in to the frog on the log in center of the
image." The crop is made from the splash art's vector layers (#481) by
art-source/splash/build_splash.py, with the window and the hidden layers named in
art-source/splash/icon.py. These tests pin:

- the crop is a square inside the splash canvas, centred on the big frog, with all of
  the big frog and some of the log in it;
- the frog's face and the log survive the round mask: the big frog's top (its eyes)
  and the log are inside the circle a round launcher cuts;
- the title (and its glow) and the small jumping frogs with their hop arcs are hidden,
  and the unused splashes are not used;
- the committed source, Assets/Art/app-icon-source.png, is square and at least
  1024 x 1024, so every Android icon is a downscale of it;
- that source really is those layers, cropped: it matches the committed layer PNGs
  stacked without the hidden ones and cut to the same window.

The pixel tests need Pillow and NumPy and are reported as skipped, by name, where those
are not installed. See docs/engineering/tech-stack.md ("The app icon").
"""

import importlib.util
import struct
import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SPLASH = REPO_ROOT / "art-source" / "splash"
LAYERS = SPLASH / "layers"
SOURCE = REPO_ROOT / "Assets" / "Art" / "app-icon-source.png"
CANVAS = (1586, 992)
LAYER_SCALE = 3965 / 1586  # layer PNGs are rendered at 2.5x the canvas

sys.path.insert(0, str(SPLASH))

import icon  # noqa: E402

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


def layer(oid):
    from PIL import Image
    return Image.open(LAYERS / f"{oid}.png").convert("RGBA")


def bbox(oid):
    """The object's box in canvas pixels, from its layer PNG's alpha."""
    import numpy as np
    a = np.asarray(layer(oid))[..., 3] > 0
    ys, xs = np.nonzero(a)
    s = LAYER_SCALE
    return xs.min() / s, ys.min() / s, (xs.max() + 1) / s, (ys.max() + 1) / s


class TheCrop(unittest.TestCase):
    def test_the_crop_is_a_square_inside_the_canvas(self):
        x, y, side = icon.ICON_CROP
        self.assertGreater(side, 0)
        self.assertGreaterEqual(x, 0)
        self.assertGreaterEqual(y, 0)
        self.assertLessEqual(x + side, CANVAS[0])
        self.assertLessEqual(y + side, CANVAS[1])

    def test_the_icon_is_rendered_at_least_1024_square(self):
        self.assertGreaterEqual(icon.ICON_SIZE, 1024)

    def test_the_title_and_its_glow_are_hidden(self):
        self.assertIn("title", icon.ICON_HIDDEN)
        self.assertIn("title-glow", icon.ICON_HIDDEN)

    def test_the_jumping_frogs_and_their_hops_are_hidden(self):
        for oid in ("frog-left", "frog-centre-left", "frog-right",
                    "arc-left", "arc-centre-left", "arc-right"):
            self.assertIn(oid, icon.ICON_HIDDEN)

    def test_the_big_frog_and_the_log_are_shown(self):
        shown = icon.icon_ids(COMPOSITE)
        self.assertIn("frog-big", shown)
        self.assertIn("log", shown)
        self.assertEqual(shown, [o for o in COMPOSITE if o not in icon.ICON_HIDDEN])

    def test_the_unused_splashes_are_not_used(self):
        shown = icon.icon_ids(COMPOSITE)
        for oid in ("splash-left", "splash-centre-left", "splash-right"):
            self.assertNotIn(oid, shown)


@NEEDS_PIXELS
class WhatTheCropHolds(unittest.TestCase):
    def test_the_crop_is_centred_on_the_big_frog(self):
        x0, _, x1, _ = bbox("frog-big")
        x, _, side = icon.ICON_CROP
        self.assertAlmostEqual(x + side / 2, (x0 + x1) / 2, delta=2)

    def test_all_of_the_big_frog_is_in_the_crop(self):
        x0, y0, x1, y1 = bbox("frog-big")
        x, y, side = icon.ICON_CROP
        self.assertLessEqual(x, x0)
        self.assertLessEqual(y, y0)
        self.assertGreaterEqual(x + side, x1)
        self.assertGreaterEqual(y + side, y1)

    def _inside_round_mask(self, oid):
        """The fraction of the object's crop-window pixels inside the inscribed circle,
        and how many of them there are."""
        import numpy as np
        x, y, side = icon.ICON_CROP
        s = LAYER_SCALE
        a = np.asarray(layer(oid))[..., 3] > 0
        win = a[int(round(y * s)):int(round((y + side) * s)), int(round(x * s)):int(round((x + side) * s))]
        n = win.shape[0]
        yy, xx = np.mgrid[0:n, 0:n]
        r = n / 2
        circle = (xx + 0.5 - r) ** 2 + (yy + 0.5 - r) ** 2 <= r * r
        return (win & circle).sum(), win.sum()

    def test_the_big_frogs_face_survives_the_round_mask(self):
        inside, total = self._inside_round_mask("frog-big")
        self.assertGreater(total, 0)
        self.assertEqual(inside, total, "part of the big frog is cut off by a round launcher")

    def test_the_log_survives_the_round_mask(self):
        # The log is wider than the icon, so its ends go; what matters is that a good
        # band of it shows under the frog.
        import numpy as np
        inside, _ = self._inside_round_mask("log")
        x, y, side = icon.ICON_CROP
        circle_area = np.pi * (side * LAYER_SCALE / 2) ** 2
        self.assertGreater(inside / circle_area, 0.15)


class TheReviewImage(unittest.TestCase):
    def test_the_icon_is_shown_in_the_three_launcher_shapes(self):
        sheet = SPLASH / "review" / "app-icon-masks.png"
        self.assertTrue(sheet.is_file(), "art-source/splash/review/app-icon-masks.png is missing")
        w, h = png_size(sheet)
        self.assertGreater(w, 2 * h, "the three shapes should sit side by side")


class TheCommittedSource(unittest.TestCase):
    def test_the_source_is_square_and_at_least_1024(self):
        w, h = png_size(SOURCE)
        self.assertEqual(w, h)
        self.assertGreaterEqual(w, 1024)
        self.assertEqual(w, icon.ICON_SIZE)

    @NEEDS_PIXELS
    def test_the_source_is_the_layers_cropped_without_the_hidden_ones(self):
        import numpy as np
        from PIL import Image

        x, y, side = icon.ICON_CROP
        s = LAYER_SCALE
        stacked = None
        for oid in icon.icon_ids(COMPOSITE):
            im = layer(oid)
            stacked = im if stacked is None else Image.alpha_composite(stacked, im)
        box = tuple(int(round(v * s)) for v in (x, y, x + side, y + side))
        expected = stacked.crop(box).resize((icon.ICON_SIZE, icon.ICON_SIZE), Image.LANCZOS)
        with Image.open(SOURCE) as got:
            got = got.convert("RGBA")
        diff = np.abs(np.asarray(got, float) - np.asarray(expected, float))
        # Rendered straight from the vectors rather than from these PNGs, so edges differ
        # by anti-aliasing; a different window, or the title left in, differs by far more.
        self.assertLess(diff.mean(), 3.0)
        self.assertTrue((np.asarray(got)[..., 3] == 255).all(), "the icon source is not opaque")


if __name__ == "__main__":
    unittest.main()
