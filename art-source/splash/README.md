# art-source/splash — the splash picture, rebuilt as separate layers

Connor's splash picture (`Assets/Art/splash-source.png`, 1586 × 992, committed unmodified)
is one flattened raster. This folder rebuilds it as **one vector layer per object**, so the
same art can make the launch splash, the app icon and the home screen without redrawing it
(#481). The app icon source, `Assets/Art/app-icon-source.png`, is cropped from these
layers by the build (#483, below), and so is the launch splash,
`Assets/Art/Splash/splash-1920x1200.png` (#482, below), and so is the title screen's
art under `docs/specs/ui/images/title-screen/` (#484, below); otherwise nothing
here is used by the game.

The approach follows `derekwinters/lucas-doggiehood` `art-source/splash/`, adapted.

## What is here

| Path | What it is |
| --- | --- |
| `layers/<id>.svg` | One file per object, all on the same full-frame canvas (`viewBox="0 0 1586 992"`, the original's pixels), so any set of them stacks straight back together. Each holds one group with the object's `id`. |
| `layers/<id>.png` | The same layer rendered at 3965 × 2480 (2.5× the canvas, more than twice the 1920 × 1200 tablet), transparent outside the object. Only the sky is opaque. |
| `composite.svg` / `composite.png` | Every layer in use stacked: one Inkscape layer per family below, one group per object inside it. The splashes are not in it (see below). |
| `objects/<id>.png` | Each notable object cropped on its own, 2000 px on its long side, transparent. Its vector is `layers/<id>.svg`. |
| `review/` | `original-vs-composite.png`, `no-frogs.png`, `no-frogs-no-title.png`, `no-frogs-no-log.png`, `no-frogs-no-log-no-title.png`, `layer-sheet.png`, `title-vs-original.png`, `app-icon-masks.png` (the app icon as a circle, a squircle and a rounded square). |
| `icon.py` | The app icon's crop: the window (`ICON_CROP`), its size (`ICON_SIZE`) and the layers it hides (`ICON_HIDDEN`). |
| `launch.py` | The launch splash's window (`SPLASH_VIEW`) and size (`SPLASH_SIZE`). |
| `home.py` | The title screen's art: the window, the size and where it is written, and the font files copied beside it. |
| `beyond.py` | Each background layer's drawing past the frame, for the title screen's extended scene (#533). |
| `build_splash.py` | The one script that writes all of the above, the app icon source and the launch splash, then self-checks. |
| `cutout.py`, `tracing.py`, `sky.py`, `title.py` | Its stages: cut-out and completion, VTracer tracing, the fitted sky and glow, the vector title. |
| `fonts/Fredoka-Variable.ttf`, `fonts/OFL.txt` | The title's font and its licence (SIL Open Font License 1.1). |

## The layers, back to front

| Family | Objects (`id`) |
| --- | --- |
| sky | `sky` — a vertical gradient fitted to the picture's own blue |
| title-glow | `title-glow` — the whitish haze behind the title |
| clouds | `cloud-left`, `cloud-right` |
| banks | `bank-left`, `bank-right` — the far grass the reeds stand on |
| reeds | `reeds-left`, `reeds-right` — cattails and reeds |
| pond | `pond` — the water and its ripples |
| lily-pads | `pad-left-back`, `pad-centre-left`, `pad-right-back`, `pad-left-front`, `pad-right-front` |
| water-lilies | `lily-left`, `lily-right` |
| grass | `grass` — the front grass, with the small shore tufts |
| log | `log` |
| grass-tufts | `tuft-left`, `tuft-right` — in front of the log, as drawn |
| frogs | `frog-jump-left`, `frog-jump-centre-left`, `frog-jump-right` — each a group of a jumping frog (`frog-left`, …) and the dashed arc of its hop (`arc-left`, …) — then `frog-big` |
| title | `title`, holding `title-multiplying`, `title-frogs`, `title-strokes-left`, `title-strokes-right` |

**The hop arcs go with the frogs.** A dashed hop line means nothing without its frog, so
each arc sits in its frog's group inside the frogs' layer: hiding the frogs hides the arcs
(Derek's review of #512).

**The splashes are kept but not used.** `splash-left`, `splash-centre-left` and
`splash-right` (the water splashing on the lily pads) are built and exported as layers
like everything else, but left out of the composite and every review image (Derek, review
of #512: "make it a layer that we don't use right now so we don't lose it"). The pads and
the water under them are completed, so leaving them out leaves no hole. To put them back,
add them to `FAMILIES` in `build_splash.py` and remove them from `UNUSED`.

**The log can be taken away.** Behind it the water, the shoreline, the grass and the
tufts are complete, and so is the water under the big frog; `review/no-frogs-no-log.png`.

Inside a traced object, one sub-group per colour family (`<id>-<colour>`, such as
`frog-big-green-2`), each with one path per colour.

## The app icon

The app icon is a close crop of this picture: the big frog on the log, with no title
(Derek's call, 2026-11-24, #483). `build_splash.py` writes it to
`Assets/Art/app-icon-source.png`; `.github/scripts/generate_app_icons.py` makes every
Android icon from that.

- **Hidden:** the title and its glow, and the three jumping frogs with their hop arcs
  (`frog-centre-left` and `arc-right` would otherwise poke into the frame). The splashes
  are not in the composite, so not in the icon.
- **Window:** `ICON_CROP = (473, 345, 640)`, a 640 × 640 square of the canvas, centred
  left-right on the big frog (its box is x 584–1002, y 385–852), from just above its
  eyes to just below the log. All of the big frog fits inside the circle a round
  launcher cuts; the log's ends and the corners of sky and water are what get trimmed.
- **Size:** rendered from the vectors at `ICON_SIZE = 1024` px square, flattened onto
  the opaque sky — a fresh render, not an upscale.

The tests in `.github/scripts/tests/test_app_icon_crop.py` check those, and that the
committed source matches the committed layer PNGs stacked and cropped the same way.

## How each part is made

**Title** (`title.py`) — vector lettering, not a cut-out, so it is sharp at any size and has
no sky fringe. Letters are Fredoka Bold glyphs. Each drawn letter of FROGS is fitted
(position, tilt, size) to the drawing by best overlap with its green fill, so the bounce is
kept. The drawing spells the top word **MUILTIPLYING**; the rebuild spells it
**MULTIPLYING** (Derek's call, 2026-11-24): the drawn letters are fitted the same way, a
smooth arch is fitted through them, and the eleven correct letters are laid along it, evenly
spaced across the same width. Dark outline, green fill and FROGS's darker rim are three
bands, coloured from the drawing. The green action strokes either side of FROGS are traced.

**Sky and glow** (`sky.py`) — fitted, not traced. The sky is a vertical gradient of the
drawing's sky colour row by row, measured clear of the title, and covers the whole canvas,
so it is complete behind the title and the frogs. The glow is how far the drawing pushes
the sky toward white around the title, smoothed and drawn as eight nested white shapes.

**Everything else** (`cutout.py`, `tracing.py`) — traced from the drawing:

1. *Cut-out*, front to back, each object taking only what nothing in front took. Most
   objects are outlined, so the background is flooded in from the object's box, stopping at
   the dark outline and at the object's own colours; the object is what is left. Splashes are
   what differs from the water and pads behind them; the hop arcs are the dark dashes; the
   tufts are the dark leaves and what they enclose; the pond, grass and banks are what is
   left inside their area.
2. *Completion*. Where something in front hides an object, the hidden part is added (the
   object's convex hull for pads, clouds and the log; its area for pond, grass and banks)
   and filled from the object's own visible colours. Objects are completed front to back, so
   the water also runs on under the hidden part of the log. The log's hidden part is filled
   row by row, so its grain carries on under the big frog, and its outline is drawn across.
   The grass's shoreline behind the log is carried straight across from the shoreline either
   side. Next to an object, off-colour water or grass (a frog's toe, the log's shadow on the
   grass) is treated as part of what is hidden, so it does not stay behind when the object
   is taken away. That is why the `review/no-*.png` images have no holes.
3. *Trace*. Per object: upscale, flatten the crayon texture (mean-shift), quantize to a small
   palette (deterministic k-means in Lab), and trace every colour with VTracer as stacked
   shapes, so neighbouring colours overlap rather than leave seams. The pond and grass are
   a fitted gradient with only the ripples and shore tufts traced on top; hidden ground is
   plain gradient.

All tuning is in named constants at the top of each file, and the object list (boxes,
polygons, how each is cut) is `OBJECTS` in `cutout.py`.

## Rebuilding

```sh
pip install vtracer opencv-python-headless scipy cairosvg pillow numpy fonttools skia-pathops
python3 art-source/splash/build_splash.py                  # about 2 minutes
python3 art-source/splash/build_splash.py --check-rebuild  # also builds again and compares
python3 art-source/splash/build_splash.py --icon-only      # only the app icon, from the committed
                                                           # layer SVGs; seconds, no tracing
python3 art-source/splash/build_splash.py --home-only      # only the title screen's art, likewise
```

Self-checks: every layer PNG is the full canvas and at least 1920 × 1200; the sky is opaque;
the app icon source is `ICON_SIZE` square with none of `ICON_HIDDEN` in it;
every object is a group with a unique id in the composite, each jumping frog is grouped
with its arc, and the splashes are not in it; the title spells MULTIPLYING FROGS; below
the water line, the layers behind let no sky through under the big frog, under the log
(and the big frog) once both are taken away, and under the splashes and arcs; and with `--check-rebuild` a second build, every
cut-out and trace recomputed, is byte-identical. The committed files were built with
Python 3.13, VTracer 0.6.15, OpenCV 5.1, SciPy 1.18, CairoSVG 2.9, Pillow, NumPy and
fontTools 4.68; another cairo version may anti-alias slightly differently.

The tests in `.github/scripts/tests/test_splash_layers.py` check the committed files.

## What differs from the original

- Crayon texture becomes flat colour steps; at splash size it reads the same, zoomed in it is
  posterised. The sky is a clean gradient without the crayon strokes.
- The title is spelled correctly and set in Fredoka, so letter shapes differ a little from
  the hand lettering (the M's legs are upright, for instance).
- Hidden parts are our completion, plainer than what shows: under the big frog the log's
  grain is carried straight across, the water under the log and pads is plain water, and
  the pads under the water lilies and splashes are plain green.

## The launch splash

The picture Unity shows while the app starts is the whole composite — every layer it
uses, frogs and title — at the tablet's 1920 × 1200 (#482). `build_splash.py` renders it
from the vectors to `Assets/Art/Splash/splash-1920x1200.png`; `--splash-only` renders
just that, from the committed layers, in seconds.

- **Window:** `SPLASH_VIEW = (0, 0.375, 1586, 991.25)`. The canvas is a hair taller than
  16:10, so the window keeps the whole width and trims 0.375 canvas px top and bottom —
  under half a pixel of the export — rather than stretching the picture.
- **Opaque**, like the sky it is drawn on.

`.github/scripts/tests/test_launch_splash.py` checks those, and that the committed file
matches the committed composite brought down to the same size.

## The title screen's art

The title (home) screen is drawn on this picture, frogs and all, with a wooden sign
reading START (#484; Derek's call, 2026-11-26 - `docs/specs/ui/title-screen.md`). Its
mockup loads the art from `docs/specs/ui/images/title-screen/full.png`, which
`build_splash.py` renders from the vectors, at the launch splash's window and
1920 × 1200 (`home.py`), and it letters START in the title's font, which the same step
copies, with its licence, into `docs/specs/ui/images/title-screen/fonts/`.
`--home-only` does just that, in seconds.

The same step writes the game's own copies, for the built title screen (#520): the art
again, byte for byte, at `Assets/Art/TitleScreen/Resources/TitleScreen/`, and a static
**Fredoka Bold** with its licence at `Assets/Fonts/Fredoka/Resources/Fredoka/`. The font
is the variable font instanced at weight 700 with fontTools (`home.bold_font_bytes()`),
because Unity's legacy font importer cannot choose a variable font's weight and would
draw its default, Light. See `docs/engineering/tech-stack.md` § The title screen's art
and font.

The comparison drawings #484 went through needed more (the scene without the frogs,
with and without the log, the title on its own, a lily pad, and a fade on the title
glow's tail, which shows once the big frog is gone). They were deleted with the losing
mockups; with the frogs in, the tail is behind the big frog.
`.github/scripts/tests/test_title_screen_art.py` checks what is left.

## Past the frame: the title screen's extended scene

The title screen fits this picture whole inside the screen, so a phone wider than 16:10
has room beside it and a 4:3 tablet above and below it (#533). That room shows more of
the same pond. Every layer the frame cuts - `sky`, `title-glow`, the banks, the reeds,
`pond` and `grass` - carries a second group, `<id>-beyond`, drawn **only outside** the
1586 × 992 frame (`beyond.py`): the sky's gradient carries on; the glow is mirrored
upwards and fades; each reed clump and bank is mirrored at its edge; the pond and the
front grass are carried round from the other side, with a short mirrored fade at each
join; and below the frame the grass's ground carries on. The layer's own drawing is
unchanged, in `<id>-framed`.

Because all of it is outside the frame, every render of the frame - the launch splash,
the title screen's art, the app icon, the composite, the layer PNGs - is byte for byte
what it was. `--home-only` also renders the stack in a window 480 px wider each side and
120 px taller top and bottom (`home.SCENE_VIEW`), at the same scale, to
`Assets/Art/TitleScreen/Resources/TitleScreen/title-scene-2880x1440.png`, which the
title screen draws behind its art. Standalone object crops leave the drawing past the
frame out (`beyond.within_frame`). `.github/scripts/tests/test_splash_beyond.py` checks it.

Derek chose this over stretching the picture's edge pixels and over a blur (PR #535).
Accepted flaws: each mirrored reed clump is a reflection of the one inside the frame,
there is a small notch where a mirror meets the shoreline, and on a 4:3 screen a faint
lighter column shows above the title where its glow is mirrored.
