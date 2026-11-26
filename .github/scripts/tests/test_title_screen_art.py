"""The title screen's art is the whole splash picture, frogs and all.

Issue #484 asked for the home (title) screen to be drawn on the splash art. Ten
comparison mockups later, Derek settled it on 2026-11-26: all the frogs, the drawn title,
the log, no scrim, and a wooden sign reading START. art-source/splash/build_splash.py
renders that art from the vector layers (#481), with the window named in
art-source/splash/home.py, into docs/specs/ui/images/title-screen/full.png, and copies
the title's font beside it so the mockup can letter START the way the title is lettered.

These tests pin:

- the window is the launch splash's (the whole canvas, cut to 16:10), at 1920 x 1200;
- the art shows every object in the composite, and nothing that is not;
- the committed art is a 1920 x 1200 opaque PNG that matches the launch splash;
- the font and its licence are the title's own, unchanged;
- the folder holds only what the agreed mockup needs - the comparison pieces the
  losing options were drawn from are gone;
- and (#520) the game's own copies: the same art under Assets/, where the title screen
  loads it, and a static Fredoka Bold - the variable font instanced at weight 700, because
  Unity's legacy font importer has no way to pick a variable font's weight - with its
  licence beside it.

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
        self.assertEqual(home.HOME_OUT, OUT_DIR / "full.png")


class WhatItShows(unittest.TestCase):
    def test_every_object_in_the_composite(self):
        self.assertEqual(home.home_ids(COMPOSITE), COMPOSITE)


class TheCommittedFiles(unittest.TestCase):
    def test_the_art_is_a_1920_by_1200_png(self):
        self.assertTrue(home.HOME_OUT.is_file(), f"{home.HOME_OUT} is not committed")
        self.assertEqual(png_size(home.HOME_OUT), TABLET)

    def test_the_font_and_its_licence_are_the_titles(self):
        for name in home.FONT_FILES:
            with self.subTest(name=name):
                self.assertEqual((OUT_DIR / "fonts" / name).read_bytes(),
                                 (SPLASH / "fonts" / name).read_bytes())

    def test_nothing_else_is_kept(self):
        kept = sorted(str(p.relative_to(OUT_DIR)) for p in OUT_DIR.rglob("*") if p.is_file())
        self.assertEqual(kept, sorted(["full.png"] + [f"fonts/{n}" for n in home.FONT_FILES]))


class TheGamesCopies(unittest.TestCase):
    """What build_splash.py --home-only writes under Assets/ for the built title screen (#520)."""

    def test_the_art_is_written_where_the_title_screen_loads_it(self):
        self.assertEqual(
            home.ASSETS_ART_OUT,
            REPO_ROOT / "Assets" / "Art" / "TitleScreen" / "Resources" / "TitleScreen"
            / "title-screen-1920x1200.png")

    def test_the_art_is_a_1920_by_1200_png(self):
        self.assertTrue(home.ASSETS_ART_OUT.is_file(), f"{home.ASSETS_ART_OUT} is not committed")
        self.assertEqual(png_size(home.ASSETS_ART_OUT), TABLET)

    def test_the_art_is_the_docs_copy_byte_for_byte(self):
        self.assertEqual(home.ASSETS_ART_OUT.read_bytes(), home.HOME_OUT.read_bytes())

    def test_the_font_is_written_where_the_title_screen_loads_it(self):
        self.assertEqual(
            home.ASSETS_FONT_DIR,
            REPO_ROOT / "Assets" / "Fonts" / "Fredoka" / "Resources" / "Fredoka")
        self.assertEqual(home.ASSETS_FONT_FILES, ("Fredoka-Bold.ttf", "OFL.txt"))

    def test_the_licence_is_beside_it_unchanged(self):
        self.assertEqual((home.ASSETS_FONT_DIR / "OFL.txt").read_bytes(),
                         (SPLASH / "fonts" / "OFL.txt").read_bytes())

    def test_the_font_is_bold_at_weight_700_and_not_variable(self):
        data = (home.ASSETS_FONT_DIR / "Fredoka-Bold.ttf").read_bytes()
        tables = ttf_tables(data)
        self.assertNotIn(b"fvar", tables, "still a variable font: Unity would draw its default, Light")
        os2 = data[tables[b"OS/2"]:]
        self.assertEqual(struct.unpack(">H", os2[4:6])[0], home.FONT_BOLD_WEIGHT)
        self.assertEqual(home.FONT_BOLD_WEIGHT, 700)

    @unittest.skipUnless(importlib.util.find_spec("fontTools") is not None, "fontTools is not installed")
    def test_the_font_is_regenerated_byte_for_byte_from_the_variable_font(self):
        self.assertEqual(home.bold_font_bytes(),
                         (home.ASSETS_FONT_DIR / "Fredoka-Bold.ttf").read_bytes())


def ttf_tables(data):
    """{tag: offset} from a TrueType file's table directory."""
    count = struct.unpack(">H", data[4:6])[0]
    out = {}
    for i in range(count):
        tag, _, offset, _ = struct.unpack(">4sIII", data[12 + 16 * i: 28 + 16 * i])
        out[tag] = offset
    return out


@NEEDS_PIXELS
class ThePixels(unittest.TestCase):
    def test_the_art_is_opaque(self):
        from PIL import Image

        self.assertEqual(Image.open(home.HOME_OUT).mode, "RGB")

    def test_the_art_is_the_launch_splash(self):
        import numpy as np
        from PIL import Image

        got = np.asarray(Image.open(home.HOME_OUT).convert("RGB"), dtype=float)
        want = np.asarray(Image.open(launch.SPLASH_OUT).convert("RGB"), dtype=float)
        self.assertLess(np.abs(got - want).max(), 4.0)


if __name__ == "__main__":
    unittest.main()
