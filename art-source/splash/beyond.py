"""The background carried on past the picture's frame (#533).

The splash picture is 1586 x 992 (the original's pixels), cut to 16:10. The title screen
fits that picture whole inside the screen, so a phone wider than 16:10 has room beside it
and a 4:3 tablet has room above and below it. Derek chose, on PR #535, to fill that room
with more of the scene - "Can't we just extend the scene instead of adding blurry edges" -
and for the extension to live in the layers themselves, not in a one-off bake of the
flattened picture: "That's part of all the assets right?"

So each background layer that the frame cuts carries a second group, `<id>-beyond`,
drawn only **outside** the frame (every copy in it is clipped to the region past an edge).
Inside the frame nothing changes: every render of the 1586 x 992 window - the launch
splash, the title screen's art, the app icon, the review images - is the same as before.
Only a window wider or taller than the frame shows it (home.SCENE_VIEW).

What each layer does past the frame, using the layer's own drawing:

| Layer | Past the frame |
| --- | --- |
| `sky` | its gradient, over everything past every edge (above, the top colour carries on) |
| `title-glow` | mirrored upwards about the top edge, fading out over GLOW_FADE |
| `bank-left`, `reeds-left` | mirrored about the left edge |
| `bank-right`, `reeds-right` | mirrored about the right edge |
| `pond` | wraps round: left of the frame is the pond's right-hand side, dropped POND_SHORE_DROP so its shoreline meets the left bank, and the other way about; for the first SEAM units it fades in from a mirror of its own edge |
| `grass` | wraps round the same way, with the same fade; below the frame, its ground gradient carries on, drawn under the grass from UNDERLAP above its bottom edge |

**Why mirror the reeds and wrap the water.** The frame cuts a reed clump through its
leaves, and a mirror is the one join where a cut leaf meets itself. Each side gets a
fuller clump with no hard edge. Water mirrored about an edge shows a symmetric pattern
along the join, so the pond takes the other side's water instead; only a thin fade at the
join is mirrored, to hide where the two sides' ripples meet.

Known flaws, accepted by Derek: the mirrored clumps are reflections of the originals;
where a mirror meets the shoreline there is a small notch; and above the title on a 4:3
screen, the mirrored glow shows as a faint lighter column.

Standard library only.
"""
from __future__ import annotations

FRAME_W, FRAME_H = 1586, 992
REACH = (600, 200)        # how far past the left and right, and the top and bottom, it is drawn
SEAM = 40                 # original px over which the wrapped water fades in from a mirror
GLOW_FADE = 30            # original px over which the mirrored glow fades out upwards
UNDERLAP = 4              # original px the ground below the frame starts above its bottom edge, behind the grass
POND_SHORE_DROP = 12      # the pond's top at the left edge (628) less at the right (616)

EXTENDED = ("sky", "title-glow", "bank-left", "bank-right", "reeds-left", "reeds-right",
            "pond", "grass")

MIRROR_LEFT = "matrix(-1 0 0 1 0 0)"
MIRROR_RIGHT = f"matrix(-1 0 0 1 {2 * FRAME_W} 0)"
MIRROR_TOP = "matrix(1 0 0 -1 0 0)"


def _num(v):
    return f"{v:g}"


def _rect(x, y, w, h, extra=""):
    return f'<rect x="{_num(x)}" y="{_num(y)}" width="{_num(w)}" height="{_num(h)}"{extra}/>'


def _regions():
    """{name: (x, y, w, h)} of the four regions past the frame. Left and right run the
    full height of the drawn area, so the corners belong to them."""
    rx, ry = REACH
    return {
        "left": (-rx, -ry, rx, FRAME_H + 2 * ry),
        "right": (FRAME_W, -ry, rx, FRAME_H + 2 * ry),
        "top": (0, -ry, FRAME_W, ry),
        "bottom": (0, FRAME_H, FRAME_W, ry),
        # the bottom, corners and all; it starts UNDERLAP inside the frame because it is
        # drawn behind the grass, which covers it there - a rect that only met the grass's
        # edge would leave a hairline of sky where both half-cover a row of pixels
        "below": (-rx, FRAME_H - UNDERLAP, FRAME_W + 2 * rx, ry + UNDERLAP),
    }


def _clip(oid, name, rects):
    return f'<clipPath id="{oid}-past-{name}" clipPathUnits="userSpaceOnUse">' + "".join(_rect(*r) for r in rects) + "</clipPath>\n"


def _fade(oid, name, x1, y1, x2, y2, region):
    """A mask, opaque at (x1, y1) and clear at (x2, y2), over the region. It fades white's
    opacity rather than going from white to black, so it means the same whether a renderer
    reads a mask by its luminance (SVG) or by its alpha (CairoSVG does)."""
    gid = f"{oid}-fade-{name}"
    return (f'<linearGradient id="{gid}-ramp" x1="{_num(x1)}" y1="{_num(y1)}" x2="{_num(x2)}" y2="{_num(y2)}" '
            f'gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#ffffff" stop-opacity="1"/>'
            f'<stop offset="1" stop-color="#ffffff" stop-opacity="0"/></linearGradient>\n'
            f'<mask id="{gid}" maskUnits="userSpaceOnUse" x="{_num(region[0])}" y="{_num(region[1])}" '
            f'width="{_num(region[2])}" height="{_num(region[3])}">'
            + _rect(*region, f' fill="url(#{gid}-ramp)"') + "</mask>\n")


def _copy(oid, region, transform, mask=None):
    m = f' mask="url(#{mask})"' if mask else ""
    return (f'<g clip-path="url(#{oid}-past-{region})"{m}>'
            f'<use href="#{oid}-framed" transform="{transform}"/></g>\n')


def beyond(oid, gradient=None):
    """(defs, behind, body) of the layer's drawing past the frame - `behind` is drawn
    under the layer's own drawing, `body` over it - or None for a layer the frame
    does not cut. `gradient` names the layer's ground gradient, for the sky and the grass."""
    if oid not in EXTENDED:
        return None
    regions = _regions()
    defs = []
    body = []
    behind = []
    if oid == "sky":
        defs.append(_clip(oid, "all", list(regions.values())))
        rx, ry = REACH
        body.append(f'<g clip-path="url(#{oid}-past-all)">'
                    + _rect(-rx, -ry, FRAME_W + 2 * rx, FRAME_H + 2 * ry, f' fill="url(#{gradient})"') + "</g>\n")
    elif oid == "title-glow":
        defs.append(_clip(oid, "top", [regions["top"]]))
        defs.append(_fade(oid, "top", 0, 0, 0, -GLOW_FADE, regions["top"]))
        body.append(_copy(oid, "top", MIRROR_TOP, f"{oid}-fade-top"))
    elif oid in ("bank-left", "reeds-left"):
        defs.append(_clip(oid, "left", [regions["left"]]))
        body.append(_copy(oid, "left", MIRROR_LEFT))
    elif oid in ("bank-right", "reeds-right"):
        defs.append(_clip(oid, "right", [regions["right"]]))
        body.append(_copy(oid, "right", MIRROR_RIGHT))
    else:   # pond, grass: wrap round, with a mirrored fade at each join
        drop = POND_SHORE_DROP if oid == "pond" else 0
        for side, wrap, mirror, edge, inward in (
                ("left", f"translate({-FRAME_W} {drop})", MIRROR_LEFT, 0, -SEAM),
                ("right", f"translate({FRAME_W} {-drop})", MIRROR_RIGHT, FRAME_W, FRAME_W + SEAM)):
            defs.append(_clip(oid, side, [regions[side]]))
            defs.append(_fade(oid, side, edge, 0, inward, 0, regions[side]))
            body.append(_copy(oid, side, wrap))
            body.append(_copy(oid, side, mirror, f"{oid}-fade-{side}"))
        if oid == "grass":
            defs.append(_clip(oid, "below", [regions["below"]]))
            behind.append(f'<g id="{oid}-below" inkscape:label="past the frame, under it">'
                          + _rect(*regions["below"], f' fill="url(#{gradient})"') + "</g>\n")
    return ("".join(defs), "".join(behind),
            f'<g id="{oid}-beyond" inkscape:label="past the frame">\n' + "".join(body) + "</g>\n")


GRADIENTS = {"sky": "sky-gradient", "grass": "grass-gradient"}


def extend(oid, defs, inner):
    """The layer's (defs, inner) with its drawing past the frame added: the drawing as it
    was, in `<oid>-framed`, then `<oid>-beyond`. Unchanged for a layer the frame does not cut."""
    extra = beyond(oid, GRADIENTS.get(oid))
    if extra is None:
        return defs, inner
    extra_defs, behind, extra_body = extra
    framed = f'<g id="{oid}-framed" inkscape:label="in the frame">\n{inner}</g>\n'
    return defs + extra_defs, behind + framed + extra_body


def within_frame(oid, body):
    """An object's body without its drawing past the frame: what a standalone crop of the
    object shows, where a few pixels of padding round it reach past the frame's edge."""
    start = body.find(f'<g id="{oid}-beyond"')
    if start >= 0:
        body = body[:start] + body[body.rindex("</g>\n"):]
    below = body.find(f'<g id="{oid}-below"')
    if below >= 0:
        body = body[:below] + body[body.index("</g>\n", below) + len("</g>\n"):]
    return body
