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
# The wordmark, drawn stroke by stroke rather than set in a font.
#
# A ROUND MARKER, NOT A CHISEL. The first version of this swept a flat nib, which gives fat
# verticals, thin horizontals and slanted stroke ends -- a completely different hand from the
# one wanted here. This one is a fibre tip held square: the same width whatever direction it
# travels, with round ends and round corners, which is why every stroke here is one weight.
#
# UPRIGHT AND SPIKY. The letters are straight segments meeting at hard angles, standing up
# rather than leaning, and set apart rather than interlocked -- so the shapes read one at a
# time. What stops that being a stencil is that nothing is quite true: every point is nudged,
# and every long run is broken in the middle and nudged again, so the lines bow the way a hand
# bows them.
#
# No font is involved anywhere. There is no graffiti face on a stock Windows box, and a
# wordmark that needs a font nobody has is a wordmark that renders as a fallback.

WEIGHT = 0.105        # stroke width, in glyph heights
WOBBLE = 0.011        # how far off true each point lands
SLANT = 0.0           # upright. This hand does not lean.
CONDENSE = 1.0


# Every glyph as polylines in a box where y=0 is the top of the letter and y=1 the baseline;
# anything past 1 is a descender. 'dots' are drawn as blobs rather than swept.
GLYPHS = {
    'O': (0.62, [[(0.13, 0.10), (0.50, 0.03), (0.60, 0.32), (0.57, 0.80), (0.46, 1.02),
                  (0.15, 1.00), (0.06, 0.70), (0.09, 0.28), (0.13, 0.10)]],
          [(0.32, 0.56)]),

    # Two strokes and nothing else. It had a foot running right off the vertex, which put a
    # horizontal along the baseline between this letter and the next and read as a join.
    'V': (0.80, [[(0.04, 0.05), (0.40, 0.99), (0.76, 0.07)]], []),

    # The lower half is a Z, and it has to be obvious about it: the middle reaches most of
    # the way across before turning back, so the diagonal is long enough to read as a stroke
    # of its own rather than as a kink in the stem.
    'E': (0.72, [[(0.68, 0.07), (0.10, 0.11), (0.13, 0.49), (0.64, 0.44),
                  (0.11, 0.68), (0.16, 1.00), (0.72, 0.94)]], []),

    'R': (0.78, [[(0.11, 1.01), (0.08, 0.07), (0.60, 0.04), (0.69, 0.31),
                  (0.21, 0.49), (0.73, 1.01)]], []),

    # OPEN AT THE TOP, and it has to stay that way. Closing that counter into a box was
    # tried and it makes the letter a nine -- an S is two open hooks facing opposite ways, and
    # sealing either one takes the letter with it.
    'S': (0.70, [[(0.67, 0.11), (0.17, 0.06), (0.10, 0.43), (0.61, 0.53),
                  (0.66, 0.90), (0.12, 0.96)]], []),

    'P': (0.72, [[(0.15, 1.07), (0.10, 0.07), (0.62, 0.10), (0.67, 0.43), (0.17, 0.51)]], []),

    'A': (0.82, [[(0.04, 1.02), (0.23, 0.06), (0.59, 0.06), (0.78, 1.02)],
                 [(0.17, 0.59), (0.41, 0.79), (0.65, 0.57)]], []),

    # The descender drops and hooks BACK LEFT, and it is longer than it was. Turning it
    # right instead was tried: a bowl with a foot going right is a four, and the word ended
    # PRA4.
    'Y': (0.76, [[(0.08, 0.05), (0.11, 0.63), (0.62, 0.67), (0.66, 0.04)],
                 [(0.64, 0.67), (0.60, 1.17), (0.14, 1.20)]], []),

    'G': (0.74, [[(0.67, 0.11), (0.19, 0.06), (0.08, 0.51), (0.21, 0.98),
                  (0.63, 0.95), (0.67, 0.60), (0.39, 0.58)]], []),

    'F': (0.70, [[(0.70, 0.06), (0.12, 0.11), (0.19, 1.04)],
                 [(0.15, 0.53), (0.56, 0.48)]], []),

    'I': (0.30, [[(0.14, 0.05), (0.17, 1.02)]], []),

    'T': (0.72, [[(0.02, 0.09), (0.70, 0.04)],
                 [(0.37, 0.06), (0.33, 1.04)]], []),
}


def handstyle(text, unit=210, gap=0.16):
    """
    The word, laid out and drawn.

    LETTERS SET APART, which is the opposite of what the last version did. This hand writes
    each shape on its own -- they line up rather than run together -- so the gap is positive
    and the letters never touch.
    """
    rng = random.Random(9081)

    def off():
        return (rng.random() - 0.5) * 2.0 * WOBBLE

    placed = []
    pen = 0.0

    for ch in text:
        w, strokes, dots = GLYPHS[ch]

        # Every letter sits a hair off the line and a hair off the size. A row of letters that
        # all sit at exactly the same height is the thing that reads as type however good the
        # shapes are.
        placed.append((pen, (rng.random() - 0.5) * 0.045, 0.96 + rng.random() * 0.08,
                       strokes, dots))
        pen += w + gap

    span = pen - gap

    # The marks at the end. Three of them, which is what the reference has -- a tag is signed
    # off, and a word that simply stops looks unfinished next to one that does not.
    blobs = [(span + 0.20, 0.34), (span + 0.40, 0.52), (span + 0.20, 0.70)]

    pad = 0.20

    lo_x, hi_x = -pad, span + 0.56 + pad
    lo_y, hi_y = -pad * 0.7, 1.22 + pad * 0.7

    W = int((hi_x - lo_x) * unit)
    H = int((hi_y - lo_y) * unit)

    img = Image.new('L', (W, H), 0)
    d = ImageDraw.Draw(img)

    def at(gx, gy, ox, oy, sc):
        x = (gx * sc * CONDENSE + ox - lo_x + off())
        y = (gy * sc + oy - lo_y + off())

        return ((x + (hi_y - lo_y - y) * SLANT) * unit, y * unit)

    def blob(cx, cy, r):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)

    def draw(strokes, dots, ox=0.0, oy=0.0, sc=1.0):
        wide = WEIGHT * sc * unit

        for line in strokes:
            pts = []

            for i, (gx, gy) in enumerate(line):
                pts.append(at(gx, gy, ox, oy, sc))

                # Long runs get a point put in the middle of them, which then wobbles like any
                # other. Without this the letters are made of dead straight lines between two
                # shaky ends, and straight is the one thing a hand cannot do.
                if i + 1 < len(line):
                    nx, ny = line[i + 1]

                    if math.hypot(nx - gx, ny - gy) > 0.28:
                        pts.append(at((gx + nx) / 2, (gy + ny) / 2, ox, oy, sc))

            d.line(pts, fill=255, width=int(round(wide)))

            # Round ends and round corners. PIL's line is drawn with flat caps, so every join
            # and every terminal is a notch until a disc is put on it -- and round ends are
            # most of what says fibre tip rather than vector.
            for px, py in pts:
                blob(px, py, wide / 2.0)

        for gx, gy in dots:
            px, py = at(gx, gy, ox, oy, sc)
            blob(px, py, wide * 0.62)

    for ox, oy, sc, strokes, dots in placed:
        draw(strokes, dots, ox, oy, sc)

    draw([], blobs)

    return img


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
