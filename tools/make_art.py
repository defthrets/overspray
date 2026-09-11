# -*- coding: utf-8 -*-
#
# The wordmark and the can, as white masks on transparent.
#
# WHITE, ALWAYS. The colour goes on at draw time -- CustomSprite tints whatever it is given --
# so one file is the dim mark on a panel header and the same file is a bright one in whatever
# the player has loaded in the can. Baking a colour in would mean shipping eleven of each.
#
# THE WORDMARK MATCHES POSTED UP'S, which is the point of it: arched varsity block, Impact
# widened and tracked out, laid glyph by glyph along a circle. Three marks in one family -- the
# panel header here, the Graffiti masthead in Posted Up, and Posted Up's own logo.
#
# It has been a sprayed Segoe Script, a chisel-nib handstyle and a round-marker one on the way
# here. All three are in the history if they are ever wanted; none of them sat beside Posted
# Up's logo and looked like they belonged to the same pair of mods. See arched.
#
#   python tools/make_art.py

import io
import math
import os
import random
import re

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(HERE, 'data', 'icons')

FONT = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts', 'impact.ttf')

# THE STANDALONE'S MARK IS BLACKLETTER NOW. UnifrakturCook Bold, a Fraktur under the SIL Open
# Font License (tools/fonts/OFL-UnifrakturCook.txt); the font ships in the repo for this script
# and nowhere else -- what the mod ships is the rendered PNG. Bold because at header height a
# Fraktur's hairlines vanish, and Cook is the one that keeps its weight small.
FRAKTUR = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'fonts', 'UnifrakturCook-Bold.ttf')
WORD = 'Overspray'
FRAKTUR_SIZE = 320

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



# --------------------------------------------------------------------- arched
#
# The wordmark, as an arched block of type.
#
# THE SAME TREATMENT AS POSTED UP'S, deliberately, so the three marks read as one family: the
# panel header here, the Graffiti app's masthead in Posted Up, and Posted Up's own logo. The
# original is hoodrich/tools/make_logo.py and the four numbers below are copied from it --
# SWEEP, SQUEEZE, TRACK and the face. If those ever move there they have to move here too, or
# the family quietly stops being one.
#
# Impact, because the reference is a varsity block and no varsity face ships on a stock Windows
# box. Impact is the only heavy condensed one that does. Widened a little and TRACKED OUT,
# which is the part that matters: Impact sets almost solid, and without air between the letters
# an arch reads as a squashed headline rather than as a wordmark.
#
# LAID ALONG A CIRCLE GLYPH BY GLYPH, not warped as a finished image. Warping a bitmap smears
# the strokes, and at header size a smeared stroke is the only thing you can see.

SWEEP = 26.0         # degrees of arc the whole word covers
SQUEEZE = 1.15       # Impact is narrower than the reference
TRACK = 26           # pixels of air between letters, at ARCH_SIZE
ARCH_SIZE = 300      # per-glyph render height, before the arch


def _tiles(text, font):
    """Each character on its own tile, tight-cropped, plus the advance to the next."""
    out = []
    probe = ImageDraw.Draw(Image.new('L', (10, 10)))

    for ch in text:
        if ch == ' ':
            out.append((None, int(ARCH_SIZE * 0.26)))
            continue

        box = probe.textbbox((0, 0), ch, font=font)

        w = max(1, box[2] - box[0] + 8)
        h = max(1, box[3] - box[1] + 8)

        tile = Image.new('RGBA', (w, h), CLEAR)
        ImageDraw.Draw(tile).text((-box[0] + 4, -box[1] + 4), ch, font=font, fill=WHITE)

        if SQUEEZE != 1.0:
            tile = tile.resize((max(1, int(tile.width * SQUEEZE)), tile.height), Image.LANCZOS)

        out.append((tile, tile.width + TRACK))

    return out


def arched(text):
    """The word, bent over the top of a circle."""
    font = ImageFont.truetype(FONT, ARCH_SIZE)
    tiles = _tiles(text, font)

    total = sum(w for _, w in tiles)

    # The radius that makes this particular word cover SWEEP degrees. Derived rather than set,
    # so a longer word arches over a bigger circle and every mark ends up with the same amount
    # of curve in it -- a fixed radius would bend OVERSPRAY harder than GRAFFITI.
    radius = total / math.radians(SWEEP)

    pad = 300
    canvas = Image.new('RGBA', (int(total * 1.4) + pad, int(total * 0.8) + pad), CLEAR)

    cx = canvas.width / 2.0
    cy = canvas.height * 0.30 + radius        # the circle's centre, well below the word

    walked = -total / 2.0

    for tile, w in tiles:
        a = (walked + w / 2.0) / radius       # radians from the top of the circle

        if tile is not None:
            rot = tile.rotate(-math.degrees(a), resample=Image.BICUBIC, expand=True)

            px = cx + math.sin(a) * radius
            py = cy - math.cos(a) * radius

            canvas.alpha_composite(rot, (int(px - rot.width / 2), int(py - rot.height / 2)))

        walked += w

    return canvas.crop(canvas.getbbox())


def tag(text):
    return arched(text)


def fraktur(text):
    """
    The word in blackletter, with paint running off it.

    MIXED CASE. Blackletter set in capitals is unreadable -- the capitals are the ornate half
    of the alphabet and were never meant to stand next to each other -- so this is the one
    mark in the family that is not shouted.

    THE DRIPS COME OFF REAL STROKES. The bottom of every column of ink is measured and the
    drips hang from columns whose ink ends at the baseline -- not from descenders, which
    already hang -- spread along the word so they do not bunch. Each is a rounded run that
    thins as it falls and ends in a bead, the way paint does off a wet letter, and a little
    spatter sits under the baseline where a can would have thrown it.
    """
    font = ImageFont.truetype(FRAKTUR, FRAKTUR_SIZE)

    probe = ImageDraw.Draw(Image.new('L', (10, 10)))
    box = probe.textbbox((0, 0), text, font=font)

    pad = 140
    W = box[2] - box[0] + pad * 2
    H = box[3] - box[1] + pad * 2

    letters = Image.new('L', (W, H), 0)
    ImageDraw.Draw(letters).text((pad - box[0], pad - box[1]), text, font=font, fill=255)

    lp = letters.load()

    # The baseline: the row where most columns' ink stops. Descenders go below it.
    bottoms = []
    for x in range(W):
        low = -1
        for y in range(H - 1, -1, -1):
            if lp[x, y] > 128:
                low = y
                break
        bottoms.append(low)

    inked = [b for b in bottoms if b >= 0]
    counts = {}
    for b in inked:
        counts[b // 6] = counts.get(b // 6, 0) + 1
    baseline = max(counts, key=counts.get) * 6 + 3

    # Columns whose ink ends on the baseline, grouped into stems.
    stems = []
    run = None
    for x in range(W):
        on = bottoms[x] >= 0 and abs(bottoms[x] - baseline) <= 9
        if on and run is None:
            run = [x, x]
        elif on:
            run[1] = x
        elif run is not None:
            if run[1] - run[0] >= 8:
                stems.append(run)
            run = None

    rng = random.Random(1887)

    # Three drips, from stems spread across the word: one in each third, the widest stem
    # in that third, so they read as the word dripping rather than one letter leaking.
    wanted = []
    for third in range(3):
        lo = W * third / 3.0
        hi = W * (third + 1) / 3.0
        here = [st for st in stems if lo <= (st[0] + st[1]) / 2.0 < hi]
        if here:
            wanted.append(max(here, key=lambda st: st[1] - st[0]))

    ink = ImageDraw.Draw(letters)

    for st in wanted:
        cx = (st[0] + st[1]) // 2 + rng.randint(-3, 3)
        top = max(bottoms[max(0, min(W - 1, cx))], baseline - 2)
        length = rng.randint(60, 118)
        wide = max(6, (st[1] - st[0]) * 0.42)

        # Thinning as it falls: a stack of short segments, each narrower than the last.
        steps = 14
        for i in range(steps):
            t = i / float(steps)
            w = wide * (1.0 - 0.55 * t)
            y0 = top + length * t
            y1 = top + length * (t + 1.0 / steps) + 2
            ink.rectangle((cx - w / 2.0, y0, cx + w / 2.0, y1), fill=255)

        # The bead at the bottom.
        r = wide * 0.62
        ink.ellipse((cx - r, top + length - r * 0.6, cx + r, top + length + r * 1.1), fill=255)

    # Spatter under the baseline, denser near it.
    for _ in range(90):
        x = rng.randint(pad - 20, W - pad + 20)
        fall = abs(rng.gauss(0, 26))
        y = int(baseline + 4 + fall)
        if y >= H - 1:
            continue
        r = rng.choice((1, 1, 1, 2, 2, 3))
        a = int(255 * max(0.25, 1.0 - fall / 90.0))
        ImageDraw.Draw(letters).ellipse((x - r, y - r, x + r, y + r), fill=a)

    out = Image.merge('RGBA', (Image.new('L', (W, H), 255),) * 3 + (letters,))
    return out.crop(out.getbbox())


# The reveal: the mark arriving as if it were being sprayed on.
SPRAY_FRAMES = 8
SPRAY_BAND = 0.16        # how wide the wet edge is, as a fraction of the word
SPRAY_RAG = 0.045        # how far the edge wanders up and down the word
SPRAY_SCALE = 0.5        # frames are rendered at half the mark, and still oversampled
SPRAY_GRAIN = 3          # droplet size, in frame pixels


def spraying(mark):
    """
    The wordmark part-sprayed, one image per frame.

    THE SAME CANVAS EVERY TIME, uncropped, so the panel can swap frames without the mark
    jumping. Cropping each one to its own ink would centre a growing word on a shrinking box,
    which reads as it sliding in rather than as it arriving.

    The front is not a straight edge: each row takes its offset off a slow random walk, so the
    boundary wanders the way a real one does. Neighbouring rows have to agree about roughly
    where it is or the front turns to static rather than to paint.

    HALF SIZE AND COARSE DROPLETS, both for the same reason. Per-pixel noise at full size came
    to 664 KB for eight frames -- noise is the one thing PNG cannot compress, and every byte of
    it was detail nobody can see at seventy pixels tall. Grain in blocks reads MORE like a can
    and costs a fraction, and the mark is still drawn at over twice its screen size.
    """
    rng = random.Random(31337)

    W = max(1, int(mark.width * SPRAY_SCALE))
    H = max(1, int(mark.height * SPRAY_SCALE))

    alpha = mark.resize((W, H), Image.LANCZOS).split()[3]

    band = W * SPRAY_BAND

    rag = []
    walk = 0.0
    for _ in range(H):
        walk = walk * 0.86 + (rng.random() - 0.5) * W * SPRAY_RAG
        rag.append(walk)

    # One value per droplet-sized block, shared by every pixel in it and by every frame, so the
    # grain sits still on the wall while the front passes over it.
    gw = W // SPRAY_GRAIN + 2
    gh = H // SPRAY_GRAIN + 2
    grain = [[0.30 + 0.70 * rng.random() for _ in range(gw)] for _ in range(gh)]

    px = alpha.load()
    out = []

    for f in range(SPRAY_FRAMES):
        # Runs past the right-hand edge on the last frame, so it is the whole word and the
        # panel can hand over to logo.png without a step.
        front = (f + 1) / float(SPRAY_FRAMES) * (W + band * 2) - band

        frame = Image.new('L', (W, H), 0)
        fp = frame.load()

        for y in range(H):
            edge = front + rag[y]
            row = grain[y // SPRAY_GRAIN]

            for x in range(W):
                a = px[x, y]
                if not a:
                    continue

                t = (edge - x) / band

                if t >= 1.0:
                    fp[x, y] = a
                elif t > 0.0:
                    # Stepped, not smooth. A clean ramp is a wipe; a ramp broken into droplets
                    # is a can arriving -- and the steps are what lets it compress.
                    v = a * t * row[x // SPRAY_GRAIN]
                    fp[x, y] = int(v / 24) * 24

        out.append(Image.merge('RGBA', (Image.new('L', (W, H), 255),) * 3 + (frame,)))

    return out


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


def cap_tile(hole=76):
    """
    The stock cap as a phone TILE, which is a different drawing from the chip in the picker.

    A CAP IS TALLER THAN IT IS WIDE and this has to stay that way. The first go at it made the
    pad wider than tall, on the reasoning that a wheel tile is forced square and the portrait
    chip would be stretched to fit. Half of that is true and the conclusion does not follow:
    the FILE is square either way. This one returns a 64x64 canvas with the cap centred in it,
    so the cap inside can be any proportion it likes and simply carries margins at the sides.
    What gets stretched is a non-square file, which is what the picker's chip is.

    So the only thing this really changes is WEIGHT. The chip's 22-unit stroke is drawn for a
    thirty-pixel square and disappears at sixteen, which is the one size a tile has to survive.
    Heavier line, slightly bigger hole, same shape.
    """
    big = Image.new('RGBA', (S, S), CLEAR)
    d = ImageDraw.Draw(big)

    mid = S // 2

    body_w, body_h = 296, 384
    stem_w, stem_h = 100, 96
    stroke = 34

    top = (S - (body_h + stem_h)) // 2

    d.rounded_rectangle([mid - body_w // 2, top, mid + body_w // 2, top + body_h],
                        radius=78, outline=WHITE, width=stroke)

    d.rounded_rectangle([mid - stem_w // 2, top + body_h - stroke,
                         mid + stem_w // 2, top + body_h + stem_h],
                        radius=18, outline=WHITE, width=stroke)

    hole_y = top + body_h // 2

    d.ellipse([mid - hole, hole_y - hole, mid + hole, hole_y + hole], fill=WHITE)

    return big.resize((S // 8, S // 8), Image.LANCZOS)


def main():
    if not os.path.exists(FONT):
        raise SystemExit('no Impact at ' + FONT)

    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    tin = can()

    # The three nozzles, READ OUT OF Caps.cs rather than written here again.
    #
    # They were a second copy of the same three numbers, and the day the ladder moved up a step
    # the icons would have gone on saying what it used to be -- a picture of a setting that
    # quietly stops matching the setting is worse than no picture.
    #
    # Normalised against the smallest, because what the icon says is how much bigger this cap
    # is than the thin one. That stays true whatever the absolute numbers become.
    caps = os.path.join(HERE, 'src', 'Overspray', 'Paint', 'Caps.cs')

    found = re.findall(r'new Cap\("(\w+)",\s*([\d.]+)f\)',
                       io.open(caps, encoding='utf-8-sig').read())

    if not found:
        raise SystemExit('no caps found in ' + caps)

    least = min(float(w) for _, w in found)
    nozzles = [(n, float(w) / least) for n, w in found]

    if not os.path.exists(FRAKTUR):
        raise SystemExit('no UnifrakturCook at ' + FRAKTUR)

    mark = fraktur(WORD)
    mark.save(os.path.join(OUT, 'logo.png'))

    # And the same mark arriving, for the panel to run when it opens.
    for i, frame in enumerate(spraying(mark)):
        frame.save(os.path.join(OUT, 'logo_%d.png' % i), optimize=True)

    print('  overspray  logo_0..%d.png  %d KB the lot'
          % (SPRAY_FRAMES - 1,
             sum(os.path.getsize(os.path.join(OUT, 'logo_%d.png' % i))
                 for i in range(SPRAY_FRAMES)) // 1024))
    tin.save(os.path.join(OUT, 'can.png'))

    for name, width in nozzles:
        cap(width).save(os.path.join(OUT, 'cap_%s.png' % name))

    nozzle = cap(1.0)

    print('  overspray  cap_thin.png cap_stock.png cap_fat.png  %dx%d' % nozzle.size)
    print('    cap aspect %.4f   <- CapAspect in both pickers'
          % (nozzle.size[0] / float(nozzle.size[1])))
    print('  overspray  logo.png %dx%d   can.png %dx%d' % (mark.size + tin.size))

    # And the same mark for the app inside Posted Up.
    #
    # THE SAME WORD, ON PURPOSE NOW. The app's header has read OVERSPRAY since the app
    # existed -- the mod inside Posted Up is Overspray, and the tile is the way in -- and
    # for a while this wrote a GRAFFITI mark to files nothing read while the app went on
    # drawing an overspray.png nobody regenerated. The app reads overspray.png and
    # overspray_0..7.png, and those are what go across; the caps go with them because the
    # two pickers draw the same three icons.
    other = os.path.join(os.path.dirname(HERE), 'hoodrich', 'data', 'icons')

    if os.path.isdir(other):
        mark.save(os.path.join(other, 'overspray.png'))

        for i, frame in enumerate(spraying(mark)):
            frame.save(os.path.join(other, 'overspray_%d.png' % i), optimize=True)

        for name, width in nozzles:
            cap(width).save(os.path.join(other, 'cap_%s.png' % name))

        print('  hoodrich   overspray.png %dx%d, overspray_0..%d.png, the three caps'
              % (mark.size + (SPRAY_FRAMES - 1,)))
    else:
        print('  hoodrich   not beside this repo; skipped')

    print('    logo aspect %.4f   can aspect %.4f'
          % (mark.size[0] / float(mark.size[1]), tin.size[0] / float(tin.size[1])))


if __name__ == '__main__':
    main()
