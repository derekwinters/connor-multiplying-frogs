"""The splash layers carried on past the picture's frame, and the title screen's extended
scene built from them (#533).

Derek, on PR #535: "Can't we just extend the scene instead of adding blurry edges", and
then "That's part of all the assets right?" - so the extension lives in the layers
(art-source/splash/beyond.py), drawn only outside the 1586 x 992 frame, and
build_splash.py renders the extended scene from them in a wider window.

These tests pin:

- every layer the frame cuts is extended, and no other;
- every extended layer file carries its drawing as it was, plus a drawing past the frame;
- the extended scene is written where the title screen loads it, 2880 x 1440, its window
  the title screen art's grown by whole pixels at the same scale;
- its middle is the title screen's art;
- and, where CairoSVG is installed, that rendering the committed layers in the art's own
  window gives the committed art byte for byte - the extension changed nothing inside the
  frame - and in the scene's window gives the committed scene.

The pixel tests need Pillow and NumPy, the rendering tests CairoSVG too, and are reported
as skipped, by name, where those are not installed.
"""

import importlib.util
import struct
import sys
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[3]
SPLASH = REPO_ROOT / "art-source" / "splash"

sys.path.insert(0, str(SPLASH))
import beyond  # noqa: E402
import home  # noqa: E402

HAVE_PIXELS = all(importlib.util.find_spec(m) is not None for m in ("PIL", "numpy"))
NEEDS_PIXELS = unittest.skipUnless(HAVE_PIXELS, "Pillow and NumPy are not installed")
HAVE_RENDER = HAVE_PIXELS and all(importlib.util.find_spec(m) is not None for m in ("cairosvg", "scipy", "cv2"))
NEEDS_RENDER = unittest.skipUnless(HAVE_RENDER, "CairoSVG and the build's libraries are not installed")


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    assert head[:8] == b"\x89PNG\r\n\x1a\n", f"{path} is not a PNG"
    return struct.unpack(">II", head[16:24])


class TheLayers(unittest.TestCase):
    @NEEDS_PIXELS
    def test_every_layer_the_frame_cuts_is_extended_and_no_other(self):
        import numpy as np
        from PIL import Image

        cut = set()
        for png in sorted((SPLASH / "layers").glob("*.png")):
            a = np.asarray(Image.open(png).convert("RGBA"))[..., 3] > 8
            if a[:, 0].any() or a[:, -1].any() or a[0].any() or a[-1].any():
                cut.add(png.stem)
        self.assertEqual(cut, set(beyond.EXTENDED))

    def test_each_extended_layer_keeps_its_drawing_and_adds_one_past_the_frame(self):
        for oid in beyond.EXTENDED:
            with self.subTest(oid=oid):
                text = (SPLASH / "layers" / f"{oid}.svg").read_text(encoding="utf-8")
                self.assertIn(f'<g id="{oid}-framed"', text)
                self.assertIn(f'<g id="{oid}-beyond"', text)
                self.assertLess(text.index(f'<g id="{oid}-framed"'), text.index(f'<g id="{oid}-beyond"'),
                                "drawn after the layer's own drawing")

    def test_a_layer_the_frame_does_not_cut_is_left_as_it_was(self):
        self.assertEqual(beyond.extend("log", "<d/>", "<g/>\n"), ("<d/>", "<g/>\n"))

    def test_a_standalone_crop_shows_only_what_is_inside_the_frame(self):
        defs, inner = beyond.extend("grass", "", "<path/>\n")
        body = f'<g id="grass">\n{inner}</g>\n'
        within = beyond.within_frame("grass", body)
        self.assertNotIn("grass-beyond", within)
        self.assertNotIn("grass-below", within)
        self.assertIn('<g id="grass-framed"', within)
        self.assertTrue(within.endswith("</g>\n</g>\n"))


class TheScene(unittest.TestCase):
    def test_it_is_written_where_the_title_screen_loads_it(self):
        self.assertEqual(
            home.ASSETS_SCENE_OUT,
            REPO_ROOT / "Assets" / "Art" / "TitleScreen" / "Resources" / "TitleScreen"
            / "title-scene-2880x1440.png")

    def test_it_is_2880_by_1440(self):
        self.assertEqual(home.SCENE_SIZE, (2880, 1440))
        self.assertTrue(home.ASSETS_SCENE_OUT.is_file(), f"{home.ASSETS_SCENE_OUT} is not committed")
        self.assertEqual(png_size(home.ASSETS_SCENE_OUT), home.SCENE_SIZE)

    def test_its_window_is_the_arts_grown_by_whole_pixels_at_the_same_scale(self):
        hx, hy, hw, hh = home.HOME_VIEW
        sx, sy, sw, sh = home.SCENE_VIEW
        scale = home.HOME_SIZE[0] / hw
        self.assertAlmostEqual(home.SCENE_SIZE[0] / sw, scale)
        self.assertAlmostEqual(home.SCENE_SIZE[1] / sh, scale)
        self.assertAlmostEqual((hx - sx) * scale, home.SCENE_MARGIN[0])
        self.assertAlmostEqual((hy - sy) * scale, home.SCENE_MARGIN[1])

    def test_it_reaches_past_the_frame_no_further_than_the_layers_are_drawn(self):
        sx, sy, sw, sh = home.SCENE_VIEW
        rx, ry = beyond.REACH
        self.assertGreaterEqual(sx, -rx)
        self.assertLessEqual(sx + sw, beyond.FRAME_W + rx)
        self.assertGreaterEqual(sy, -ry)
        self.assertLessEqual(sy + sh, beyond.FRAME_H + ry)

    @NEEDS_PIXELS
    def test_its_middle_is_the_title_screens_art(self):
        import numpy as np
        from PIL import Image

        scene = np.asarray(Image.open(home.ASSETS_SCENE_OUT).convert("RGB"), dtype=float)
        art = np.asarray(Image.open(home.ASSETS_ART_OUT).convert("RGB"), dtype=float)
        mx, my = home.SCENE_MARGIN
        middle = scene[my:my + art.shape[0], mx:mx + art.shape[1]]
        # The same vectors at the same scale, a whole number of pixels along: only
        # anti-aliasing along outlines may differ.
        self.assertLess(np.abs(middle - art).mean(), 0.25)

    @NEEDS_PIXELS
    def test_it_is_opaque(self):
        from PIL import Image

        self.assertEqual(Image.open(home.ASSETS_SCENE_OUT).mode, "RGB")


@NEEDS_RENDER
class RebuiltFromTheLayers(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        import build_splash

        build_splash.load_committed_parts()
        cls.build = build_splash

    def test_inside_the_frame_nothing_changed(self):
        import io

        out = io.BytesIO()
        self.build.home_png().save(out, format="PNG", optimize=True)
        self.assertEqual(out.getvalue(), home.ASSETS_ART_OUT.read_bytes())

    def test_the_scene_is_the_committed_one(self):
        import numpy as np
        from PIL import Image

        got = np.asarray(self.build.scene_png(), dtype=float)
        want = np.asarray(Image.open(home.ASSETS_SCENE_OUT).convert("RGB"), dtype=float)
        self.assertLess(np.abs(got - want).max(), 4.0)


if __name__ == "__main__":
    unittest.main()
