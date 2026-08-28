# -*- coding: utf-8 -*-
#
# The wordmark and the can, as white masks on transparent.
#
# WHITE, ALWAYS. The colour goes on at draw time -- CustomSprite tints whatever it is given --
# so one file is the dim mark on a panel header and the same file is a bright one in whatever
# the player has loaded in the can. Baking a colour in would mean shipping eleven of each.
#
# THE LOGO SPRAYS ITSELF, and that is the whole idea rather than a flourish: overspray IS the
# paint that lands outside where you aimed. So the letters are solid and a real speckle halo
# falls off around them, densest at the edges, thinning outward -- which is what a can actually
# leaves on a wall. A drop shadow would have been easier and would have said nothing.
#
# Impact, because it is the only heavy condensed face that ships on a stock Windows box, and a
# wordmark that needs a font nobody has is a wordmark that renders as a fallback and looks like
# a mistake. Tracked out, since Impact sets almost solid and a sprayed mark wants air in it.
#
#   python tools/make_art.py

import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

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



SCRIPT = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', 'segoescb.ttf')


def tagmark(text, size=250, slant=0.20, fatten=13):
    """
    The wordmark as a handstyle tag: fat connected strokes, drips, and flourishes.

    NOT A GRAFFITI FONT, because no graffiti font ships on Windows and a wordmark that needs a
    font nobody has renders as a fallback. Segoe Script Bold is the nearest thing on a stock
    box -- connected, flowing, already close to a marker hand -- and everything that makes it
    read as a TAG rather than as handwriting is done to it afterwards:

      SHEARED, because a tag leans. Upright script is a wedding invitation.
      FATTENED with a max filter, which is what turns a pen line into a marker stroke. A
        stroke-width setting cannot do this: it would outline the glyph, and a tag has no
        outline, it has weight.
      DRIPPED from the bottom of the strokes, tapering, with a bead on the end. This is the
        single detail that says spray paint rather than ink.
      FLOURISHED with arrows and tick marks, which is the grammar of the thing -- the reference
        has them above, below and at both ends, and without them a tag is just a word.
    """
    font = ImageFont.truetype(SCRIPT, size)

    # ASYMMETRIC PADDING. Square margins put the word in the middle of a lot of nothing and
    # pushed the flourishes -- which are placed as fractions of the canvas -- out to the
    # corners, where they read as separate marks instead of as part of the tag. Tight at the
    # sides, room above for the arrow, more below because that is where the drips go.
    padx = int(size * 0.42)
    top = int(size * 0.62)
    bot = int(size * 1.05)

    box = font.getbbox(text)
    W = box[2] - box[0] + padx * 2
    H = box[3] - box[1] + top + bot

    img = Image.new('L', (W, H), 0)
    ImageDraw.Draw(img).text((padx - box[0], top - box[1]), text, font=font, fill=255)

    # Lean. Positive shear pulls the top to the right, which is the direction a right hand
    # naturally slants.
    img = img.transform((W, H), Image.AFFINE, (1, slant, -slant * H * 0.5, 0, 1, 0),
                        resample=Image.BICUBIC)

    # Weight. Dilate, then a whisker of blur so the edges are not stepped.
    img = img.filter(ImageFilter.MaxFilter(fatten if fatten % 2 else fatten + 1))
    img = img.filter(ImageFilter.GaussianBlur(1.2))
    img = img.point(lambda v: 255 if v > 96 else 0)

    d = ImageDraw.Draw(img)
    px = img.load()

    # ---- drips ----
    #
    # Off the LOWEST ink in a column, so they hang from the real bottom of a stroke rather
    # than from wherever a fixed offset happened to land.
    cols = []
    for x in range(0, W, 3):
        low = -1
        for y in range(H - 1, -1, -1):
            if px[x, y]:
                low = y
                break
        cols.append((x, low))

    picked = []
    for x, low in cols:
        if low < 0:
            continue
        # Only from a local bottom, and never two drips on top of each other.
        # FEWER AND FURTHER APART. Every third column with a 30% chance gave a dozen drips at
        # even spacing, which reads as a comb rather than as paint running. A real one has a
        # handful, clustered where the hand lingered.
        if picked and x - picked[-1][0] < size * 0.85:
            continue
        if random.random() < 0.42:
            picked.append((x, low))

    for x, low in picked:
        # Wildly uneven. Two of these should be barely a bead and one should be a long run,
        # which is what stops a row of drips looking measured out.
        run = int(size * random.choice((0.10, 0.14, 0.30, 0.45, 0.72, 0.95))
                  * random.uniform(0.85, 1.15))
        wide = size * random.uniform(0.035, 0.055)

        for i in range(run):
            t = i / float(run)
            r = wide * (1.0 - t * 0.55)
            yy = low + i
            d.ellipse((x - r, yy - r, x + r, yy + r), fill=255)

        bead = wide * random.uniform(0.9, 1.4)
        d.ellipse((x - bead, low + run - bead, x + bead, low + run + bead), fill=255)

    return img, d, W, H, size


def flourish(img, d, W, H, size):
    """The arrows and ticks. The grammar that makes a word a tag."""
    def stroke(pts, wide):
        d.line(pts, fill=255, width=int(wide), joint='curve')

    def arrow(x0, y0, x1, y1, wide, head):
        stroke([(x0, y0), (x1, y1)], wide)
        ang = math.atan2(y1 - y0, x1 - x0)
        for turn in (2.6, -2.6):
            hx = x1 + math.cos(ang + turn) * head
            hy = y1 + math.sin(ang + turn) * head
            stroke([(x1, y1), (hx, hy)], wide)

    w = size * 0.075
    head = size * 0.30

    # Above, pointing right, with the dash-dash the reference has trailing off it.
    arrow(W * 0.36, H * 0.20, W * 0.55, H * 0.13, w, head)
    for i, fx in enumerate((0.60, 0.65)):
        stroke([(W * fx, H * 0.12), (W * (fx + 0.028), H * 0.115)], w * 0.9)

    # Below, pointing right, with the dashes LEADING it instead -- mirrored on purpose so the
    # two do not read as the same stamp used twice.
    for fx in (0.30, 0.35):
        stroke([(W * fx, H * 0.88), (W * (fx + 0.028), H * 0.882)], w * 0.9)
    arrow(W * 0.41, H * 0.885, W * 0.60, H * 0.90, w, head)

    # Double ticks at both ends, leaning with the letters.
    for fx, fy in ((0.055, 0.30), (0.085, 0.28), (0.925, 0.62), (0.955, 0.60)):
        stroke([(W * fx, H * fy), (W * (fx + 0.022), H * (fy - 0.13))], w)

    # And a little spatter, because a real one always has some.
    # Clustered near the letters rather than sprinkled over the whole rectangle -- overspray
    # lands close to what made it, which is the entire idea the mod is named after.
    for _ in range(34):
        x = random.uniform(W * 0.12, W * 0.90)
        y = random.gauss(H * 0.52, H * 0.16)

        if not (H * 0.14 < y < H * 0.94):
            continue

        r = random.uniform(1.5, size * 0.020)
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)

    return img


def tag(text):
    img, d, W, H, size = tagmark(text)
    img = flourish(img, d, W, H, size)

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
