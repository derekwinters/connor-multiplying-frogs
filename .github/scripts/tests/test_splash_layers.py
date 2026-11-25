"""The splash picture is rebuilt as separate layers that stack back together.

Connor's splash picture is committed once, unmodified, at
Assets/Art/splash-source.png, and art-source/splash/build_splash.py rebuilds it as one
vector layer per object (#481). These tests read what is committed and pin the
acceptance criteria of #481 that can be checked without running the build:

- every object the issue names is its own group, with an id, on one shared canvas;
- the title is spelled MULTIPLYING (the picture's "MUILTIPLYING" is corrected - Derek's
  call, 2026-11-24);
- the exports are at least the 1920 x 1200 tablet screen;
- with the frogs hidden there is no frog-shaped hole: water, log and pads cover
  everything under the big frog below the water line, and the sky is opaque everywhere,
  so hiding the title leaves no hole either;
- the font the title is built from is open-licensed, with its licence committed.

That a second build is byte-identical is checked by the build itself
(`build_splash.py --check-rebuild`): it needs VTracer and OpenCV, and takes minutes.

The structural tests need nothing but the standard library. The pixel tests need Pillow
and NumPy; where those are not installed they are reported as skipped, by name.

See docs/engineering/tech-stack.md ("The splash art") and issue #481.
"""

import importlib.util
import re
import struct
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SPLASH = REPO_ROOT / "art-source" / "splash"
SOURCE = REPO_ROOT / "Assets" / "Art" / "splash-source.png"
LAYERS = SPLASH / "layers"
COMPOSITE_SVG = SPLASH / "composite.svg"

HAVE_PIXELS = all(importlib.util.find_spec(m) is not None for m in ("PIL", "numpy"))
NEEDS_PIXELS = unittest.skipUnless(HAVE_PIXELS, "Pillow and NumPy are not installed")

# Every object #481 names, by the id of its group.
OBJECTS = [
    "sky", "title-glow", "cloud-left", "cloud-right", "bank-left", "bank-right",
    "reeds-left", "reeds-right", "pond",
    "pad-left-back", "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front",
    "lily-left", "lily-right", "splash-left", "splash-centre-left", "splash-right",
    "arc-left", "arc-centre-left", "arc-right", "grass", "tuft-left", "tuft-right", "log",
    "frog-left", "frog-centre-left", "frog-right", "frog-big", "title",
]
# Groups inside the title: each word, and the action strokes either side of FROGS.
TITLE_PARTS = ["title-multiplying", "title-frogs", "title-strokes-left", "title-strokes-right"]
FROGS = ["frog-left", "frog-centre-left", "frog-right", "frog-big"]
# What lies under the big frog below the water line, in the original's pixels.
WATER_LINE = 600
UNDER_THE_BIG_FROG = ["pond", "log", "grass", "pad-left-back", "pad-centre-left",
                      "pad-right-back", "pad-left-front", "pad-right-front"]
SOURCE_SIZE = (1586, 992)
TABLET = (1920, 1200)


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    assert head[:8] == b"\x89PNG\r\n\x1a\n", f"{path} is not a PNG"
    return struct.unpack(">II", head[16:24])


def alpha(path):
    import numpy as np
    from PIL import Image
    return np.asarray(Image.open(path).convert("RGBA"))[..., 3]


class SourceTests(unittest.TestCase):
    def test_the_source_picture_is_committed_at_its_original_size(self):
        self.assertTrue(SOURCE.is_file(), "Assets/Art/splash-source.png is missing")
        self.assertEqual(png_size(SOURCE), SOURCE_SIZE)


class LayerStructureTests(unittest.TestCase):
    def test_every_object_has_its_own_layer_file_on_the_shared_canvas(self):
        for oid in OBJECTS:
            with self.subTest(oid=oid):
                svg = (LAYERS / f"{oid}.svg").read_text(encoding="utf-8")
                self.assertIn('viewBox="0 0 1586 992"', svg)
                self.assertIn(f'<g id="{oid}" ', svg)

    def test_the_composite_holds_every_object_as_a_named_group(self):
        svg = COMPOSITE_SVG.read_text(encoding="utf-8")
        self.assertIn('viewBox="0 0 1586 992"', svg)
        for oid in OBJECTS + TITLE_PARTS:
            with self.subTest(oid=oid):
                self.assertIn(f'<g id="{oid}" ', svg)

    def test_ids_in_the_composite_are_unique(self):
        ids = re.findall(r'\bid="([^"]+)"', COMPOSITE_SVG.read_text(encoding="utf-8"))
        self.assertEqual(sorted({i for i in ids if ids.count(i) > 1}), [])

    def test_the_frogs_are_stacked_above_everything_but_the_title(self):
        svg = COMPOSITE_SVG.read_text(encoding="utf-8")
        at = {oid: svg.index(f'<g id="{oid}" ') for oid in OBJECTS}
        for frog in FROGS:
            for oid in OBJECTS:
                if oid in FROGS or oid == "title":
                    continue
                with self.subTest(frog=frog, under=oid):
                    self.assertLess(at[oid], at[frog])
        self.assertEqual(max(at, key=at.get), "title")

    def test_the_title_is_spelled_multiplying_frogs(self):
        svg = (LAYERS / "title.svg").read_text(encoding="utf-8")
        top = re.findall(r'<path id="title-multiplying-\d+-([A-Z])"', svg)
        bottom = re.findall(r'<path id="title-frogs-\d+-([A-Z])"', svg)
        self.assertEqual("".join(top), "MULTIPLYING")
        self.assertEqual("".join(bottom), "FROGS")

    def test_the_title_font_is_open_licensed_with_its_licence_committed(self):
        fonts = sorted((SPLASH / "fonts").glob("*.ttf"))
        self.assertTrue(fonts, "no font committed beside the build")
        licence = (SPLASH / "fonts" / "OFL.txt").read_text(encoding="utf-8")
        self.assertIn("SIL Open Font License", licence)

    def test_every_export_is_at_least_the_tablet_screen(self):
        pngs = [LAYERS / f"{oid}.png" for oid in OBJECTS] + [SPLASH / "composite.png"]
        for p in pngs:
            with self.subTest(png=p.name):
                w, h = png_size(p)
                self.assertGreaterEqual(w, TABLET[0])
                self.assertGreaterEqual(h, TABLET[1])

    def test_every_layer_png_is_the_same_full_canvas(self):
        sizes = {png_size(LAYERS / f"{oid}.png") for oid in OBJECTS}
        self.assertEqual(len(sizes), 1, sizes)

    def test_the_review_images_are_committed(self):
        for name in ("original-vs-composite.png", "no-frogs.png", "no-frogs-no-title.png",
                     "layer-sheet.png"):
            with self.subTest(review=name):
                self.assertTrue((SPLASH / "review" / name).is_file())


@NEEDS_PIXELS
class NoHoleTests(unittest.TestCase):
    def test_the_sky_is_opaque_everywhere_so_hiding_the_title_leaves_no_hole(self):
        self.assertEqual(int(alpha(LAYERS / "sky.png").min()), 255)

    def test_nothing_under_the_big_frog_is_a_hole_once_it_is_hidden(self):
        import numpy as np
        frog = alpha(LAYERS / "frog-big.png") > 0
        scale = frog.shape[1] / SOURCE_SIZE[0]
        frog[: int(WATER_LINE * scale)] = False
        # stacked, the layers under the frog let no sky through: alpha composes as
        # 1 - product of (1 - alpha)
        clear = np.ones(frog.shape)
        for oid in UNDER_THE_BIG_FROG:
            clear *= 1 - alpha(LAYERS / f"{oid}.png") / 255.0
        cover = clear <= 1 / 255.0
        self.assertTrue(frog.any())
        self.assertEqual(int((frog & ~cover).sum()), 0)

    def test_the_log_carries_on_under_the_big_frog(self):
        # the frog sits on the log: with the frog hidden, the log is whole under it
        frog = alpha(LAYERS / "frog-big.png") > 0
        log = alpha(LAYERS / "log.png") == 255
        scale = frog.shape[1] / SOURCE_SIZE[0]
        # a band across the frog's feet, well inside the log's top edge
        band = frog[int(780 * scale):int(820 * scale)]
        self.assertTrue(band.any())
        self.assertEqual(int((band & ~log[int(780 * scale):int(820 * scale)]).sum()), 0)


if __name__ == "__main__":
    unittest.main()
