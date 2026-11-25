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
- the hop arcs go with the frogs (Derek's review of #512: "there shouldn't be dotted
  lines for jumps if the frogs aren't there"): each jumping frog is one group with its
  arc, inside the frogs' layer, and what the arcs hide is completed;
- the splashes on the lily pads are kept as their own layer files but left out of the
  composite (Derek, same review: "make it a layer that we don't use right now"), and
  the pads and water under them are complete;
- the log can be taken away too, and leaves no log-shaped hole: water, grass and
  tufts are complete behind it and under the big frog;
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
# Each jumping frog's group: the frog and the dashed arc of its hop.
JUMPS = {
    "frog-jump-left": ["arc-left", "frog-left"],
    "frog-jump-centre-left": ["arc-centre-left", "frog-centre-left"],
    "frog-jump-right": ["arc-right", "frog-right"],
}
# Kept as layers, not used in the composite.
UNUSED = ["splash-left", "splash-centre-left", "splash-right"]
PADS = ["pad-left-back", "pad-centre-left", "pad-right-back", "pad-left-front", "pad-right-front"]
# What lies behind the log (and under the big frog, which sits on it).
BEHIND_THE_LOG = ["pond", "grass", "tuft-left", "tuft-right"] + PADS
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
            if oid in UNUSED:
                continue
            with self.subTest(oid=oid):
                self.assertIn(f'<g id="{oid}" ', svg)

    def test_the_splashes_are_kept_as_layers_but_left_out_of_the_composite(self):
        svg = COMPOSITE_SVG.read_text(encoding="utf-8")
        for oid in UNUSED:
            with self.subTest(oid=oid):
                self.assertTrue((LAYERS / f"{oid}.svg").is_file())
                self.assertTrue((LAYERS / f"{oid}.png").is_file())
                self.assertNotIn(f'id="{oid}"', svg)

    def test_ids_in_the_composite_are_unique(self):
        ids = re.findall(r'\bid="([^"]+)"', COMPOSITE_SVG.read_text(encoding="utf-8"))
        self.assertEqual(sorted({i for i in ids if ids.count(i) > 1}), [])

    def test_each_jumping_frog_is_one_group_with_its_arc(self):
        import xml.etree.ElementTree as ET
        root = ET.parse(COMPOSITE_SVG).getroot()
        groups = {g.get("id"): g for g in root.iter("{http://www.w3.org/2000/svg}g")}
        frogs_layer = groups["layer-frogs"]
        inside_frogs = {g.get("id") for g in frogs_layer.iter("{http://www.w3.org/2000/svg}g")}
        for jump, members in JUMPS.items():
            with self.subTest(jump=jump):
                self.assertIn(jump, inside_frogs)
                inside = {g.get("id") for g in groups[jump].iter("{http://www.w3.org/2000/svg}g")}
                for oid in members:
                    self.assertIn(oid, inside)
        for oid in ("arc-left", "arc-centre-left", "arc-right"):
            with self.subTest(oid=oid):
                self.assertIn(oid, inside_frogs, "hiding the frogs must hide it too")

    def test_the_frogs_are_stacked_above_everything_but_the_title(self):
        svg = COMPOSITE_SVG.read_text(encoding="utf-8")
        at = {oid: svg.index(f'<g id="{oid}" ') for oid in OBJECTS if oid not in UNUSED}
        jumping = [m for ms in JUMPS.values() for m in ms]
        for frog in FROGS:
            for oid in at:
                if oid in FROGS or oid in jumping or oid == "title":
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
                     "no-frogs-no-log.png", "no-frogs-no-log-no-title.png", "layer-sheet.png"):
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

    def covered(self, footprint, layers):
        """Pixels of footprint (below the water line) that the stacked layers leave
        clear: alpha composes as 1 - product of (1 - alpha)."""
        import numpy as np
        scale = footprint.shape[1] / SOURCE_SIZE[0]
        footprint = footprint.copy()
        footprint[: int(WATER_LINE * scale)] = False
        clear = np.ones(footprint.shape)
        for oid in layers:
            clear *= 1 - alpha(LAYERS / f"{oid}.png") / 255.0
        return int((footprint & (clear > 1 / 255.0)).sum())

    def test_nothing_under_a_splash_or_an_arc_is_a_hole(self):
        for oid in ("splash-left", "splash-centre-left", "splash-right",
                    "arc-left", "arc-centre-left", "arc-right"):
            with self.subTest(oid=oid):
                self.assertEqual(self.covered(alpha(LAYERS / f"{oid}.png") > 0, ["pond"] + PADS), 0)

    def test_nothing_behind_the_log_is_a_hole_once_it_is_hidden(self):
        log = alpha(LAYERS / "log.png") > 0
        self.assertTrue(log.any())
        self.assertEqual(self.covered(log, BEHIND_THE_LOG), 0)

    def test_nothing_under_the_big_frog_is_a_hole_once_it_and_the_log_are_hidden(self):
        frog = alpha(LAYERS / "frog-big.png") > 0
        self.assertEqual(self.covered(frog, BEHIND_THE_LOG), 0)


if __name__ == "__main__":
    unittest.main()
