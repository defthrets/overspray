# -*- coding: utf-8 -*-
#
# The wordmark and the can, as white masks on transparent.
#
# WHITE, ALWAYS. The colour goes on at draw time -- CustomSprite tints whatever it is given --
# so one file is the dim mark on a panel header and the same file is a bright one in whatever
# the player has loaded in the can. Baking a colour in would mean shipping eleven of each.
#
# THE WORDMARK IS NOT SET, IT IS DRAWN. It used to be Segoe Script sheared and dilated, with a
# sprayed halo thrown round it, and it never looked like a tag -- because a script FACE is one
# pen width everywhere and its letters are correct, and a handstyle is neither of those things.
#
# Every letter is a few polylines now, swept with a flat chisel nib. That one change is what
# does it: verticals come out fat, horizontals come out thin, and every stroke ends on a slant,
# none of which a font can be talked into. See handstyle.
#
#   python tools/make_art.py

import math
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(HERE, 'data', 'icons')

FONT = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', 'impact.ttf')

TEXT = 'OVERSPRAY'
SIZE = 260           # per-glyph render height
TRACK = 14           # air between letters
PAD = 90             # room for the halo to fall off into

random.seed(20260827)

S = 512              # working size for the cap icons, downsampled to 64
WHITE = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


def wordmark(text):
    font = ImageFont.truetype(FONT, SIZE)

    # Measured glyph by glyph so the tracking is real rather than a guess at a string width.
    glyphs = []
    total = 0
    for ch in text:
        box = font.getbbox(ch)
        w = box[2] - box[0]
        glyphs.append((ch, box, w))
        total += w + TRACK
    total -= TRACK

    top = min(g[1][1] for g in glyphs)
    bottom = max(g[1][3] for g in glyphs)
    tall = bottom - top

    W = total + PAD * 2
    H = tall + PAD * 2

    letters = Image.new('L', (W, H), 0)
    pen = ImageDraw.Draw(letters)

    x = PAD
    for ch, box, w in glyphs:
        pen.text((x - box[0], PAD - top), ch, font=font, fill=255)
        x += w + TRACK

    # ---- the halo ----
    #
    # Two blurs subtracted: a wide one minus the letters themselves gives a band that is
    # brightest hard against the strokes and fades outward. Speckling INTO that band, with the
    # chance of a dot proportional to its brightness, puts the spatter where a can would put it
    # instead of scattering it evenly over a rectangle.
    near = letters.filter(ImageFilter.GaussianBlur(26))
    field = Image.new('L', (W, H), 0)
    fp = field.load()
    np_ = near.load()
    lp = letters.load()

    spots = ImageDraw.Draw(field)

    for _ in range(42000):
        px = random.randrange(W)
        py = random.randrange(H)

        weight = np_[px, py]
        if weight < 8:
            continue
        if lp[px, py] > 200:          # not on top of the solid letter
            continue
        if random.random() > (weight / 255.0) ** 1.15:
            continue

        r = random.choice((0, 0, 1, 1, 2, 3))
        a = int(min(255, weight * random.uniform(0.5, 1.15)))

        if r == 0:
            fp[px, py] = max(fp[px, py], a)
        else:
            spots.ellipse((px - r, py - r, px + r, py + r), fill=a)

    out = Image.new('RGBA', (W, H), (255, 255, 255, 0))
    out.putalpha(Image.new('L', (W, H), 0))

    alpha = Image.new('L', (W, H), 0)
    alpha.paste(field, (0, 0))
    alpha.paste(letters, (0, 0), letters)      # letters solid over the speckle

    out = Image.merge('RGBA', (Image.new('L', (W, H), 255),) * 3 + (alpha,))

    # Trimmed to what is actually inked, so the draw code can position by its real bounds
    # rather than by however much padding the generator happened to use.
    out = out.crop(out.getbbox())
    return out


def can():
    """A spray can, drawn rather than found. Readable at forty pixels is the only requirement."""
    S = 8                                   # supersample; the diagonals need it
    W, H = 150 * S, 340 * S

    img = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(img)

    def box(x0, y0, x1, y1, r, fill=255):
        d.rounded_rectangle((x0 * S, y0 * S, x1 * S, y1 * S), radius=r * S, fill=fill)

    # STRAIGHT SIDES. The first attempt tapered a shoulder in at the top and put a big
    # nozzle off one edge, and the result was unmistakably a pump-action soap bottle. A spray
    # can is a plain cylinder; all of its character is in the three things stacked on top.
    box(20, 126, 130, 330, 12)

    # THE WAIST IS WHAT MAKES IT A CAN. Seating the cap flush on the rim removed the floating
    # notch but replaced it with a worse problem: cap and body merged into one tall block with
    # a nub on it. What separates them on a real can is a narrow neck with daylight either
    # side, so that is what this draws -- cap, visible waist, rim, body, four shapes you can
    # count at a glance instead of one column.
    box(12, 112, 138, 130, 4)          # rim, proud of the body on both sides
    box(56, 98, 94, 118, 3)            # the neck itself, narrow on purpose
    box(38, 44, 112, 104, 10)          # cap, well short of the body's width

    # The tip OUT OF THE SIDE of the cap, not off the top of it.
    #
    # On top is where a pump handle or an aerosol button lives, and on a plain square cap it
    # reads as a chimney. A spray can's nozzle points out sideways: you aim the can at the wall
    # and press DOWN with your finger, so the paint has to leave at right angles to the press.
    #
    # It is also the only thing in the silhouette that says which way the can is facing, which
    # matters more than it sounds -- the shape rotates during the shake, and a symmetrical can
    # just wobbles while this one turns.
    box(108, 54, 136, 76, 4)

    # Label band, punched out, so the silhouette still has something to read against when it
    # rotates during the shake.
    box(30, 178, 120, 236, 8, fill=0)
    box(38, 190, 112, 224, 5, fill=255)
    box(46, 198, 104, 216, 3, fill=0)

    img = img.resize((W // S, H // S), Image.LANCZOS)

    return Image.merge('RGBA', (Image.new('L', img.size, 255),) * 3 + (img,))


def tile(_tin, phase=None):
    """
    The can again, as a 64x64 app icon -- drawn fresh, not the masthead one shrunk.

    A SQUARE FILE BECAUSE THE TILE IS A SQUARE. Every icon in the set is 64x64 and the draw
    call forces the aspect, so the tall masthead can would squash into a fat little barrel.

    AND ALMOST NO DETAIL, which took a wrong turn to learn: a real speckle cone off the nozzle
    looked good at 128 pixels and was a grey shard stuck to a white blob at twenty, which is
    the size that matters. An icon is read in about a tenth of a second. Three dots say spray;
    two thousand say nothing.

    phase is 0..1 round a loop, or None for the still frame the tile actually uses.
    """
    S = 512
    img = Image.new('L', (S, S), 0)
    d = ImageDraw.Draw(img)

    def box(x0, y0, x1, y1, r):
        d.rounded_rectangle((x0 * S, y0 * S, x1 * S, y1 * S), radius=r * S, fill=255)

    # Deliberately chunkier than the real can. A faithful 0.44 aspect is a sliver in a square,
    # and the tile's job is to be recognisable, not to be to scale.
    box(0.10, 0.40, 0.44, 0.93, 0.05)      # body
    box(0.07, 0.33, 0.47, 0.42, 0.02)      # rim, proud both sides
    box(0.20, 0.24, 0.34, 0.35, 0.015)     # waist
    box(0.13, 0.09, 0.41, 0.26, 0.04)      # cap
    box(0.40, 0.13, 0.53, 0.21, 0.02)      # nozzle, out of the flank

    # ---- the spray ----
    #
    # SMALLEST AT THE NOZZLE, GROWING AS IT GOES. It ran the other way before -- a fat blob
    # against the tip tapering away to nothing -- and that is the shape of something being
    # SUCKED IN. Paint leaves a nozzle as a fine point and opens out, so an icon that does the
    # opposite reads backwards to anybody who has held a can.
    #
    # Each dot walks the same path a third of a loop apart, so it reads as a continuous stream
    # rather than as three things blinking together.
    for j in range(3):
        if phase is None:
            u = (j + 1) / 4.0                       # still frame: evenly spaced, mid-travel
        else:
            u = (phase + j / 3.0) % 1.0

        # Travel and growth both pulled in so the LAST dot -- the biggest -- still fits the
        # box. It ran to 0.96 with a 0.086 radius, which puts its far edge outside the canvas,
        # and a clipped circle reads as a half-moon rather than as a blob of paint.
        x = (0.50 + u * 0.330) * S
        y = (0.190 - u * 0.078) * S
        r = (0.014 + u * 0.058) * S

        # In quickly at the tip, out gently at the far end, so nothing pops into existence.
        a = 255
        if u < 0.14:
            a = int(255 * (u / 0.14))
        elif u > 0.80:
            a = int(255 * (1.0 - (u - 0.80) / 0.20))

        if a <= 4:
            continue

        d.ellipse((x - r, y - r, x + r, y + r), fill=a)

    img = img.rotate(-10, resample=Image.BICUBIC, center=(S * 0.28, S * 0.62))
    img = img.resize((64, 64), Image.LANCZOS)

    return Image.merge('RGBA', (Image.new('L', img.size, 255),) * 3 + (img,))



# ------------------------------------------------------------------- handstyle
#
# The wordmark as a marker tag, drawn stroke by stroke rather than set in a font.
#
# THIS USED TO BE SEGOE SCRIPT, sheared and dilated, and it was never going to get there. A
# script FACE is one pen width everywhere and its letters are correct; a handstyle is neither.
# What makes a tag look like a tag is a flat nib -- verticals come out fat, horizontals come
# out thin, and every stroke ends on a slant because the tip is a chisel and not a point. No
# amount of thickening a font produces that, because thickening is uniform by definition.
#
# So: every letter is a few polylines, and every polyline is swept with a rectangular nib. The
# widths fall out of the geometry instead of being drawn in.
#
# STROKES ACCUMULATE. Each goes down at part strength and overlaps add, so a crossbar over a
# stem is denser than either -- which is what a marker does when it crosses itself, and it is
# the detail that stops the whole thing reading as a vector shape.

NIB_W = 0.225                # the chisel, across, in glyph units
NIB_T = 0.075                # and its thickness
NIB_A = math.radians(-16)    # held just off horizontal, the way a right hand holds one

SLANT = 0.26                 # how far the whole word leans

# CONDENSED, WHICH IS WHERE THE PROPORTIONS COME FROM. Nine letters set at their natural width
# make a strip four and a half times as wide as it is tall, and the reference is two and a
# half. The first attempt at closing that gap was to overlap the letters harder, and it turned
# the word into a solid block nobody could read -- the reference's letters interlock at the
# edges but every one of them is legible.
#
# So the letters are narrowed instead, which is what a real hand does when it wants a word to
# fit: tall and thin, leaning, touching at the corners.
CONDENSE = 0.80


def _hull(pts):
    """Andrew monotone chain. The region a convex nib sweeps IS the hull of its two ends."""
    pts = sorted(set(pts))
    if len(pts) < 3:
        return pts

    def half(ps):
        out = []
        for p in ps:
            while len(out) > 1:
                (ax, ay), (bx, by) = out[-2], out[-1]
                if (bx - ax) * (p[1] - ay) - (by - ay) * (p[0] - ax) > 0:
                    break
                out.pop()
            out.append(p)
        return out

    return half(pts)[:-1] + half(list(reversed(pts)))[:-1]


def _nib(d, p0, p1, w, t, ang, ink):
    """One segment of a stroke, swept with a rectangular tip."""
    cx, sy = math.cos(ang), math.sin(ang)

    corners = [(cx * w / 2 - sy * t / 2, sy * w / 2 + cx * t / 2),
               (cx * w / 2 + sy * t / 2, sy * w / 2 - cx * t / 2),
               (-cx * w / 2 + sy * t / 2, -sy * w / 2 - cx * t / 2),
               (-cx * w / 2 - sy * t / 2, -sy * w / 2 + cx * t / 2)]

    pts = [(p0[0] + ox, p0[1] + oy) for ox, oy in corners] + \
          [(p1[0] + ox, p1[1] + oy) for ox, oy in corners]

    d.polygon(_hull(pts), fill=ink)


# Every glyph as polylines, in a box where y=0 is the cap line and y=1 the baseline. Angular
# on purpose: a handstyle has no curves in it, it has corners taken at speed.
GLYPHS = {
    'O': (0.92, [[(0.60, 0.02), (0.20, 0.16), (0.04, 0.55), (0.16, 0.90), (0.50, 1.00),
                  (0.82, 0.82), (0.90, 0.40), (0.72, 0.08), (0.46, 0.03)]]),
    'V': (0.86, [[(0.04, 0.02), (0.42, 1.00), (0.84, 0.00)]]),
    'E': (0.78, [[(0.74, 0.06), (0.16, 0.10)],
                 [(0.16, 0.10), (0.22, 1.00)],
                 [(0.19, 0.52), (0.60, 0.46)],
                 [(0.22, 1.00), (0.76, 0.92)]]),
    'R': (0.90, [[(0.08, 1.02), (0.22, 0.02)],
                 [(0.22, 0.02), (0.78, 0.14), (0.62, 0.50), (0.20, 0.54)],
                 [(0.40, 0.50), (0.86, 1.02)]]),
    'S': (0.84, [[(0.82, 0.14), (0.44, 0.00), (0.10, 0.24), (0.52, 0.50),
                  (0.78, 0.72), (0.42, 1.00), (0.04, 0.86)]]),
    'P': (0.86, [[(0.10, 1.16), (0.26, 0.02)],
                 [(0.26, 0.02), (0.84, 0.16), (0.70, 0.54), (0.22, 0.58)]]),
    'A': (0.88, [[(0.02, 1.02), (0.44, 0.00), (0.86, 1.02)],
                 [(0.16, 0.70), (0.74, 0.64)]]),
    'Y': (0.90, [[(0.02, 0.02), (0.44, 0.60)],
                 [(0.88, 0.00), (0.44, 0.60)],
                 [(0.44, 0.60), (0.38, 1.06)]]),
    'G': (0.92, [[(0.86, 0.14), (0.52, 0.00), (0.14, 0.22), (0.10, 0.72),
                  (0.44, 1.00), (0.82, 0.86), (0.86, 0.56)],
                 [(0.86, 0.56), (0.52, 0.58)]]),
    'F': (0.76, [[(0.72, 0.04), (0.18, 0.08)],
                 [(0.18, 0.08), (0.26, 1.04)],
                 [(0.21, 0.50), (0.62, 0.44)]]),
    'I': (0.34, [[(0.20, 0.02), (0.14, 1.02)]]),
    'T': (0.74, [[(0.02, 0.08), (0.72, 0.02)],
                 [(0.40, 0.05), (0.30, 1.04)]]),
}


def handstyle(text, unit=190, gap=-0.04):
    """
    The word, laid out and swept.

    LETTERS OVERLAP, which is why the gap is negative. A tag is written without lifting much,
    so its letters run into each other and share space -- setting them apart at even intervals
    is the other thing that makes a word read as type.
    """
    rng = random.Random(4471)

    placed = []
    pen = 0.0

    for ch in text:
        w, strokes = GLYPHS[ch]

        # A wobble on every letter. A tag written by a hand does not sit on a ruled line, and a
        # baseline that is exactly flat reads as type however good the letters are.
        dy = (rng.random() - 0.5) * 0.10
        sc = 0.94 + rng.random() * 0.12

        placed.append((pen, dy, sc, strokes))
        pen += w * sc * CONDENSE + gap

    span = pen - gap

    # An arrow going in on the left, ticks coming off on the right. This is the grammar of the
    # thing; without them a tag is a word in a funny hand.
    marks = [[(-0.52, 1.06), (-0.14, 0.90)],
             [(-0.48, 0.86), (-0.18, 0.82)],
             [(span + 0.20, 0.30), (span + 0.44, 0.04)],
             [(span + 0.14, 0.62), (span + 0.38, 0.38)]]

    pad = 0.24

    lo_x, hi_x = -0.58 - pad, span + 0.48 + pad
    # Down to the P's descender and no further. The box used to reach 1.60 to hold a long
    # sweep under the word; with that gone, the same number is a third of the picture spent on
    # nothing -- and the panel scales this to a fixed height, so empty space at the bottom is
    # paid for by the letters being smaller.
    lo_y, hi_y = -0.10 - pad, 1.22 + pad

    # The lean carries the top rightward, so the box has to allow for how far it will go.
    lean = SLANT * (hi_y - lo_y)

    W = int((hi_x - lo_x + lean) * unit)
    H = int((hi_y - lo_y) * unit)

    img = Image.new('L', (W, H), 0)

    def put(base, strokes, ox=0.0, oy=0.0, sc=1.0, ink=190, narrow=True):
        """
        One letter's worth, on its own layer, so its own overlaps add rather than replace.

        NARROW IS FOR LETTERS ONLY. The pen advances by condensed widths, so a letter's origin
        is already in final space while its own points are still in glyph space and need
        narrowing on the way through. The flourishes are written directly in final space --
        they are placed relative to the finished word, not to a glyph box -- so narrowing them
        again dragged them back inside it, which is how the ticks ended up sitting on the last
        letter instead of beside it.
        """
        layer = Image.new('L', (W, H), 0)
        ld = ImageDraw.Draw(layer)

        for line in strokes:
            pts = []

            for gx, gy in line:
                x = gx * sc * (CONDENSE if narrow else 1.0) + ox - lo_x
                y = gy * sc + oy - lo_y

                # Lean applied to the POINTS, not to the finished picture. Shearing the image
                # shears the chisel with it, and then every flat end points the wrong way.
                pts.append(((x + (hi_y - lo_y - y) * SLANT) * unit, y * unit))

            for a, b in zip(pts, pts[1:]):
                _nib(ld, a, b, NIB_W * unit * sc, NIB_T * unit * sc, NIB_A, ink)

        return ImageChops.add(base, layer)

    for ox, dy, sc, strokes in placed:
        img = put(img, strokes, ox, dy, sc, ink=168 + rng.randrange(0, 46))

    img = put(img, marks, ink=214, narrow=False)

    # A whisker of blur. The nib polygons are exact, and exact is the one thing a marker on a
    # wall never is.
    return img.filter(ImageFilter.GaussianBlur(unit * 0.006))


def tag(text):
    img = handstyle(text)

    out = Image.merge('RGBA', (Image.new('L', img.size, 255),) * 3 + (img,))
    return out.crop(out.getbbox())


# ------------------------------------------------------------------------- caps
#
# The three nozzles, head on.
#
# THE HOLE IS THE ICON. A cap is a rim and an aperture and the aperture is the entire
# difference between them, so the rim is identical on all three and the hole is drawn at the
# real multiplier -- thin 1, stock 2.2, fat 4.4, the same numbers Caps.cs uses. The picture is
# not an illustration of the setting, it IS the setting.
#
# Same house style as Posted Up's app icons: a white mask on transparent, drawn big and
# downsampled, tinted at draw time. These land at about thirty device pixels, which is smaller
# than those tiles ever get, so there are exactly two shapes in each -- a ring and a dot -- and
# the specks around the outside, which are the only thing saying it sprays rather than being a
# washer.


def cap(width):
    """
    One nozzle, drawn the way a cap actually looks: the pressing pad face on, and its stem.

    OUTLINE FOR THE BODY, SOLID FOR THE HOLE, which is the whole reason this reads at thirty
    pixels. The body is a shape you recognise and then stop looking at -- it is identical on
    all three -- so it can be a thin line. The hole is the only thing that differs, so it is
    the only thing that is solid.

    The hole grows by the SQUARE ROOT of the cap's multiplier rather than by the multiplier.
    Drawn literally, a 4.4x fat cap next to a 1x thin one leaves the thin one as a dot about
    two pixels across on screen, which is not a hole, it is a speck of dust. Root scaling puts
    the three at 1 : 1.5 : 2.1, which is far enough apart to read at a glance and is what the
    caps in a real rack look like next to each other anyway.
    """
    big = Image.new('RGBA', (S, S), CLEAR)
    d = ImageDraw.Draw(big)

    mid = S // 2

    body_w, body_h = 295, 358
    stem_w, stem_h = 83, 112
    stroke = 22

    top = (S - (body_h + stem_h)) // 2

    d.rounded_rectangle([mid - body_w // 2, top, mid + body_w // 2, top + body_h],
                        radius=68, outline=WHITE, width=stroke)

    # Overlapped upward by the stroke so the two shapes weld rather than leaving a seam where
    # they meet, which at this size is a white pixel that reads as a nick in the outline.
    d.rounded_rectangle([mid - stem_w // 2, top + body_h - stroke,
                         mid + stem_w // 2, top + body_h + stem_h],
                        radius=14, outline=WHITE, width=stroke)

    r = 42 * math.sqrt(width)

    hole_y = top + body_h // 2

    d.ellipse([mid - r, hole_y - r, mid + r, hole_y + r], fill=WHITE)

    # CROPPED TO THE CAP, not left in the middle of a square.
    #
    # A cap is two thirds as wide as it is tall, and a square file of it is a third air. The
    # panel draws art into a box, so that air is a third of the box spent on nothing and the
    # cap ends up two thirds the size it could be -- at thirty pixels that is the difference
    # between reading it and squinting at it. Cropped here and drawn at CapAspect there.
    margin = 6

    box = (mid - body_w // 2 - margin, top - margin,
           mid + body_w // 2 + margin, top + body_h + stem_h + margin)

    art = big.crop(box)

    tall = 64
    wide = int(round(art.width * tall / float(art.height)))

    return art.resize((wide, tall), Image.LANCZOS)


def main():
    if not os.path.exists(FONT):
        raise SystemExit('no Impact at ' + FONT)

    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    tin = can()

    # The three nozzles. Names match Caps.cs, which is what builds the filename at draw time.
    nozzles = [('thin', 1.0), ('stock', 2.2), ('fat', 4.4)]

    mark = tag(TEXT)
    mark.save(os.path.join(OUT, 'logo.png'))
    tin.save(os.path.join(OUT, 'can.png'))

    for name, width in nozzles:
        cap(width).save(os.path.join(OUT, 'cap_%s.png' % name))

    nozzle = cap(1.0)

    print('  overspray  cap_thin.png cap_stock.png cap_fat.png  %dx%d' % nozzle.size)
    print('    cap aspect %.4f   <- CapAspect in both pickers'
          % (nozzle.size[0] / float(nozzle.size[1])))
    print('  overspray  logo.png %dx%d   can.png %dx%d' % (mark.size + tin.size))

    # And the same treatment for the app inside Posted Up.
    #
    # A DIFFERENT WORD ON PURPOSE. The tile there is called Graffiti and the mod it lives in
    # is called Posted Up, so a header reading OVERSPRAY would be a third name for a thing
    # that already has two. What carries across is the look, not the wordmark.
    other = os.path.join(os.path.dirname(HERE), 'hoodrich', 'data', 'icons')

    if os.path.isdir(other):
        g = tag('GRAFFITI')
        g.save(os.path.join(other, 'graffiti.png'))
        tin.save(os.path.join(other, 'spraycan.png'))

        # One still file. The animated version -- eight frames swapped by a star in the
        # filename -- is in the history if it is ever wanted again; the tile reads better
        # holding still next to eight other tiles that do.
        tile(tin).save(os.path.join(other, 'sprayapp.png'))

        for name, width in nozzles:
            cap(width).save(os.path.join(other, 'cap_%s.png' % name))

        print('  hoodrich   graffiti.png %dx%d   spraycan.png %dx%d   sprayapp.png'
              % (g.size + tin.size))
        print('    graffiti aspect %.4f' % (g.size[0] / float(g.size[1])))
    else:
        print('  hoodrich   not beside this repo; skipped')

    print('    logo aspect %.4f   can aspect %.4f'
          % (mark.size[0] / float(mark.size[1]), tin.size[0] / float(tin.size[1])))


if __name__ == '__main__':
    main()
