# art-source/splash — the splash picture, rebuilt as separate layers

Connor's splash picture (`Assets/Art/splash-source.png`, 1586 × 992, committed unmodified)
is one flattened raster. This folder rebuilds it as **one vector layer per object**, so the
same art can make the launch splash, the app icon and the home screen without redrawing it
(#481). Nothing here is used by the game; using the art is the job of the splash, icon and
home-screen issues.

The approach follows `derekwinters/lucas-doggiehood` `art-source/splash/`, adapted.

## What is here

| Path | What it is |
| --- | --- |
| `layers/<id>.svg` | One file per object, all on the same full-frame canvas (`viewBox="0 0 1586 992"`, the original's pixels), so any set of them stacks straight back together. Each holds one group with the object's `id`. |
| `layers/<id>.png` | The same layer rendered at 3965 × 2480 (2.5× the canvas, more than twice the 1920 × 1200 tablet), transparent outside the object. Only the sky is opaque. |
| `composite.svg` / `composite.png` | Every layer stacked: one Inkscape layer per family below, one group per object inside it. |
| `objects/<id>.png` | Each notable object cropped on its own, 2000 px on its long side, transparent. Its vector is `layers/<id>.svg`. |
| `review/` | `original-vs-composite.png`, `no-frogs.png`, `no-frogs-no-title.png`, `layer-sheet.png`, `title-vs-original.png`. |
| `build_splash.py` | The one script that writes all of the above, then self-checks. |
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
| splashes | `splash-left`, `splash-centre-left`, `splash-right` |
| hop-arcs | `arc-left`, `arc-centre-left`, `arc-right` — the dashed hop lines |
| grass | `grass` — the front grass, with the small shore tufts |
| grass-tufts | `tuft-left`, `tuft-right` |
| log | `log` |
| frogs | `frog-left`, `frog-centre-left`, `frog-right` (the jumping frogs), `frog-big` |
| title | `title`, holding `title-multiplying`, `title-frogs`, `title-strokes-left`, `title-strokes-right` |

Inside a traced object, one sub-group per colour family (`<id>-<colour>`, such as
`frog-big-green-2`), each with one path per colour.

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
   and filled from the object's own visible colours. The log's hidden part is filled row by
   row, so its grain carries on under the big frog, and its outline is drawn across. That is
   why `review/no-frogs.png` has no frog-shaped holes.
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
```

Self-checks: every layer PNG is the full canvas and at least 1920 × 1200; the sky is opaque;
every object is a group with a unique id in the composite; the title spells MULTIPLYING
FROGS; under the big frog, below the water line, the layers beneath let no sky through;
the composite without the frogs is opaque; and with `--check-rebuild` a second build, every
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
