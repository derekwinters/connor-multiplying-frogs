"""The title screen's wireframe art is the splash picture without the frogs.

Issue #484: the home (title) screen is to be drawn on "the splash picture without the
frogs" (Derek, 2026-11-24), and its wireframe draws "the real exported art rather than
a placeholder block". art-source/splash/build_splash.py renders three pieces of it from
the vector layers (#481), with the window and the pieces named in
art-source/splash/home.py, into docs/specs/ui/images/title-screen/, where the
comparison mockups stack them:

- `scene`         everything but the frogs (and their hop arcs), the title and its glow;
- `scene-no-log`  the same without the log;
- `title`         the drawn title and its glow, on its own, transparent elsewhere.

These tests pin:

- the window is the launch splash's (the whole canvas, cut to 16:10), at 1920 x 1200;
- no frog and no hop arc is in any piece, and the scenes hold no title;
- the log is in `scene` and not in `scene-no-log`, and that is the only difference;
- the committed files are 1920 x 1200 PNGs, the scenes opaque and the title not;
- and the scene really is those layers: it matches the committed layer PNGs stacked.

The pixel tests need Pillow and NumPy and are reported as skipped, by name, where those
are not installed.
"""

import importlib.util
import struct
import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SPLASH = REPO_ROOT / "art-source" / "splash"
OUT_DIR = REPO_ROOT / "docs" / "specs" / "ui" / "images" / "title-screen"
LAYERS = SPLASH / "layers"
TABLET = (1920, 1200)

sys.path.insert(0, str(SPLASH))
import home  # noqa: E402
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
FROGS_AND_ARCS = {"frog-left", "frog-centre-left", "frog-right", "frog-big",
                  "arc-left", "arc-centre-left", "arc-right"}
PIECES = ("scene", "scene-no-log", "title")


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    assert head[:8] == b"\x89PNG\r\n\x1a\n", f"{path} is not a PNG"
    return struct.unpack(">II", head[16:24])


class TheWindow(unittest.TestCase):
    def test_it_is_the_tablets_size(self):
        self.assertEqual(home.HOME_SIZE, TABLET)

    def test_it_is_the_launch_splashs_window(self):
        self.assertEqual(home.HOME_VIEW, launch.SPLASH_VIEW)

    def test_it_is_written_next_to_the_mockups(self):
        self.assertEqual(home.HOME_OUT_DIR, OUT_DIR)


class ThePieces(unittest.TestCase):
    def ids(self, piece):
        return home.piece_ids(piece, COMPOSITE)

    def test_there_are_three(self):
        self.assertEqual(tuple(home.PIECES), PIECES)

    def test_no_frog_and_no_hop_is_in_any(self):
        for piece in PIECES:
            with self.subTest(piece=piece):
                self.assertFalse(FROGS_AND_ARCS & set(self.ids(piece)))

    def test_the_scenes_hold_no_title(self):
        for piece in ("scene", "scene-no-log"):
            with self.subTest(piece=piece):
                self.assertFalse({"title", "title-glow"} & set(self.ids(piece)))

    def test_the_scene_keeps_everything_else_in_order(self):
        keep = [o for o in COMPOSITE if o not in FROGS_AND_ARCS | {"title", "title-glow"}]
        self.assertEqual(self.ids("scene"), keep)

    def test_the_only_difference_between_the_scenes_is_the_log(self):
        self.assertEqual([o for o in self.ids("scene") if o != "log"], self.ids("scene-no-log"))
        self.assertIn("log", self.ids("scene"))

    def test_the_title_is_the_title_and_its_glow(self):
        self.assertEqual(self.ids("title"), ["title-glow", "title"])


class TheCommittedFiles(unittest.TestCase):
    def test_each_is_a_1920_by_1200_png(self):
        for piece in PIECES:
            path = OUT_DIR / f"{piece}.png"
            with self.subTest(piece=piece):
                self.assertTrue(path.is_file(), f"{path} is not committed")
                self.assertEqual(png_size(path), TABLET)


@NEEDS_PIXELS
class WhatTheyShow(unittest.TestCase):
    def test_the_scenes_are_opaque(self):
        from PIL import Image

        for piece in ("scene", "scene-no-log"):
            with self.subTest(piece=piece):
                self.assertEqual(Image.open(OUT_DIR / f"{piece}.png").mode, "RGB")

    def test_the_title_is_transparent_around_it(self):
        import numpy as np
        from PIL import Image

        a = np.asarray(Image.open(OUT_DIR / "title.png").convert("RGBA"))[..., 3]
        self.assertEqual(a[-1].max(), 0, "the bottom row is clear")
        self.assertGreater(a.max(), 0, "something is drawn")

    def test_the_scene_is_its_layers_stacked(self):
        import numpy as np
        from PIL import Image

        stack = None
        for oid in home.piece_ids("scene", COMPOSITE):
            layer = Image.open(LAYERS / f"{oid}.png").convert("RGBA")
            stack = layer if stack is None else Image.alpha_composite(stack, layer)
        want = np.asarray(stack.convert("RGB").resize(TABLET, Image.LANCZOS), dtype=float)
        got = np.asarray(Image.open(OUT_DIR / "scene.png").convert("RGB"), dtype=float)
        # Two renders of the same vectors at different sizes differ only along edges.
        self.assertLess(np.abs(got - want).mean(), 3.0)


if __name__ == "__main__":
    unittest.main()
