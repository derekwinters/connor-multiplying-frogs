"""The Android app-icon set is generated from one committed picture.

Connor's artwork is committed once, unmodified, at Assets/Art/app-icon-source.png
and every icon Android wants is derived from it by generate_app_icons.py. These
tests pin the sizes Android asks for, where the picture sits inside the adaptive
icon's mask in each of the two modes, and that the committed files are what the
generator produces — so an icon that drifted from its source is caught here.

The geometry tests need nothing but the standard library. The rendering tests
need Pillow; where it is not installed they are reported as skipped, by name,
rather than passing quietly.

See docs/engineering/tech-stack.md ("The app icon") and issues #169 and #448.
"""

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import generate_app_icons as icons  # noqa: E402

HAVE_PILLOW = importlib.util.find_spec("PIL") is not None
NEEDS_PILLOW = unittest.skipUnless(HAVE_PILLOW, "Pillow is not installed")

REPO_ROOT = Path(__file__).resolve().parents[3]


class SizesAndroidAsksFor(unittest.TestCase):
    def test_adaptive_layers_are_the_108dp_canvas_at_each_density(self):
        self.assertEqual(
            {name: size for name, size in icons.outputs() if name.startswith("adaptive-foreground-")},
            {
                "adaptive-foreground-mdpi.png": 108,
                "adaptive-foreground-hdpi.png": 162,
                "adaptive-foreground-xhdpi.png": 216,
                "adaptive-foreground-xxhdpi.png": 324,
                "adaptive-foreground-xxxhdpi.png": 432,
            },
        )

    def test_every_foreground_has_a_background_of_the_same_size(self):
        sizes = dict(icons.outputs())
        for density in icons.DENSITIES:
            self.assertEqual(
                sizes[f"adaptive-background-{density}.png"],
                sizes[f"adaptive-foreground-{density}.png"],
            )

    def test_legacy_and_round_icons_are_48dp_at_each_density(self):
        expected = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
        sizes = dict(icons.outputs())
        for density, size in expected.items():
            self.assertEqual(sizes[f"legacy-{density}.png"], size)
            self.assertEqual(sizes[f"round-{density}.png"], size)

    def test_the_play_store_image_is_512_square(self):
        self.assertEqual(dict(icons.outputs())["play-store-512.png"], 512)

    def test_nothing_else_is_generated(self):
        # 5 densities x (foreground, background, legacy, round) + the store image.
        self.assertEqual(len(icons.outputs()), 21)

    def test_every_size_is_a_downscale_of_the_608_source(self):
        self.assertTrue(all(size <= 608 for _, size in icons.outputs()))


class WhereThePictureSits(unittest.TestCase):
    """The 108dp canvas, its 72dp visible viewport, and the 66dp safe circle."""

    def test_fill_spans_the_72dp_viewport_exactly(self):
        for canvas in (108, 162, 216, 324, 432):
            box = icons.picture_box(canvas, icons.FILL)
            self.assertEqual(box.size, canvas * 72 // 108)
            self.assertEqual(box.left, (canvas - box.size) // 2)
            self.assertEqual(box.top, box.left)

    def test_fill_viewport_lands_on_whole_pixels_at_every_density(self):
        for canvas in (108, 162, 216, 324, 432):
            self.assertEqual(canvas * 72 % 108, 0)
            self.assertEqual((canvas - canvas * 72 // 108) % 2, 0)

    def test_shrink_keeps_every_corner_of_the_picture_inside_the_66dp_circle(self):
        for canvas in (108, 162, 216, 324, 432):
            box = icons.picture_box(canvas, icons.SHRINK)
            radius = canvas * 66 / 108 / 2
            centre = canvas / 2
            for x in (box.left, box.left + box.size):
                for y in (box.top, box.top + box.size):
                    self.assertLessEqual(((x - centre) ** 2 + (y - centre) ** 2) ** 0.5, radius)

    def test_shrink_is_centred(self):
        for canvas in (108, 162, 216, 324, 432):
            box = icons.picture_box(canvas, icons.SHRINK)
            self.assertLessEqual(abs((canvas - box.size) - 2 * box.left), 1)
            self.assertEqual(box.left, box.top)

    def test_shrink_is_smaller_than_fill(self):
        self.assertLess(icons.picture_box(432, icons.SHRINK).size,
                        icons.picture_box(432, icons.FILL).size)

    def test_an_unknown_mode_is_refused(self):
        with self.assertRaises(ValueError):
            icons.picture_box(432, "stretch")

    def test_fill_is_the_default(self):
        self.assertEqual(icons.DEFAULT_MODE, icons.FILL)


@NEEDS_PILLOW
class Rendering(unittest.TestCase):
    def setUp(self):
        from PIL import Image

        self.Image = Image
        # A picture with a different colour in each quadrant, so a crop, a
        # flip, or a misplaced paste all show up as the wrong colour somewhere.
        self.source = Image.new("RGBA", (608, 608))
        for (x, y), colour in {
            (0, 0): (255, 0, 0, 255), (304, 0): (0, 255, 0, 255),
            (0, 304): (0, 0, 255, 255), (304, 304): (255, 255, 0, 255),
        }.items():
            self.source.paste(colour, (x, y, x + 304, y + 304))

    def render(self, mode):
        return icons.render(self.source, mode)

    def test_every_listed_output_is_rendered_at_its_size(self):
        for mode in icons.MODES:
            rendered = self.render(mode)
            self.assertEqual(set(rendered), {name for name, _ in icons.outputs()})
            for name, size in icons.outputs():
                self.assertEqual(rendered[name].size, (size, size), name)

    def test_fill_adds_no_background_colour(self):
        # Every opaque pixel of the background layer comes from the picture.
        background = self.render(icons.FILL)["adaptive-background-xxxhdpi.png"]
        self.assertEqual(background.getpixel((0, 0)), (255, 0, 0, 255))
        self.assertEqual(background.getpixel((431, 431)), (255, 255, 0, 255))

    def test_fill_foreground_is_transparent_outside_the_viewport(self):
        foreground = self.render(icons.FILL)["adaptive-foreground-xxxhdpi.png"]
        self.assertEqual(foreground.getpixel((0, 0))[3], 0)
        self.assertEqual(foreground.getpixel((80, 80)), (255, 0, 0, 255))

    def test_shrink_background_is_the_plain_cream(self):
        background = self.render(icons.SHRINK)["adaptive-background-xxxhdpi.png"]
        self.assertEqual(background.getcolors(), [(432 * 432, icons.SHRINK_BACKGROUND)])

    def test_shrink_shows_cream_round_the_picture_in_the_legacy_icon(self):
        legacy = self.render(icons.SHRINK)["legacy-xxxhdpi.png"]
        self.assertEqual(legacy.getpixel((2, 2)), icons.SHRINK_BACKGROUND)

    def test_fill_legacy_icon_is_the_whole_picture(self):
        legacy = self.render(icons.FILL)["legacy-xxxhdpi.png"]
        self.assertEqual(legacy.getpixel((2, 2)), (255, 0, 0, 255))
        self.assertEqual(legacy.getpixel((189, 189)), (255, 255, 0, 255))

    def test_round_icon_is_transparent_in_the_corners_and_whole_in_the_middle(self):
        for mode in icons.MODES:
            round_icon = self.render(mode)["round-xxxhdpi.png"]
            self.assertEqual(round_icon.getpixel((0, 0))[3], 0)
            self.assertEqual(round_icon.getpixel((96, 96))[3], 255)

    def test_play_store_image_is_fully_opaque(self):
        for mode in icons.MODES:
            store = self.render(mode)["play-store-512.png"]
            self.assertEqual(store.getchannel("A").getextrema(), (255, 255))

    def test_rendering_twice_writes_identical_files(self):
        with tempfile.TemporaryDirectory() as first, tempfile.TemporaryDirectory() as second:
            icons.write(self.render(icons.FILL), Path(first))
            icons.write(self.render(icons.FILL), Path(second))
            for name, _ in icons.outputs():
                self.assertEqual((Path(first) / name).read_bytes(),
                                 (Path(second) / name).read_bytes(), name)


@NEEDS_PILLOW
class CommittedFiles(unittest.TestCase):
    """What is in Assets/Art/AppIcon/ is exactly what the generator makes."""

    def test_the_committed_icons_match_a_fresh_render_of_the_committed_source(self):
        from PIL import Image

        source = REPO_ROOT / icons.SOURCE
        out = REPO_ROOT / icons.OUTPUT_DIR
        with Image.open(source) as picture:
            rendered = icons.render(picture, icons.DEFAULT_MODE)
        with tempfile.TemporaryDirectory() as scratch:
            icons.write(rendered, Path(scratch))
            for name, _ in icons.outputs():
                self.assertEqual((out / name).read_bytes(), (Path(scratch) / name).read_bytes(),
                                 f"{name} is stale — rerun generate_app_icons.py")


if __name__ == "__main__":
    unittest.main()
