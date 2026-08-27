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

    # A small tip ON TOP of the cap rather than beside it, so it reads as a nozzle you press
    # instead of a spout you pour from.
    box(84, 26, 106, 48, 4)

    # Label band, punched out, so the silhouette still has something to read against when it
    # rotates during the shake.
    box(30, 178, 120, 236, 8, fill=0)
    box(38, 190, 112, 224, 5, fill=255)
    box(46, 198, 104, 216, 3, fill=0)

    img = img.resize((W // S, H // S), Image.LANCZOS)

    return Image.merge('RGBA', (Image.new('L', img.size, 255),) * 3 + (img,))


def main():
    if not os.path.exists(FONT):
        raise SystemExit('no Impact at ' + FONT)

    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    tin = can()

    mark = wordmark(TEXT)
    mark.save(os.path.join(OUT, 'logo.png'))
    tin.save(os.path.join(OUT, 'can.png'))
    print('  overspray  logo.png %dx%d   can.png %dx%d' % (mark.size + tin.size))

    # And the same treatment for the app inside Posted Up.
    #
    # A DIFFERENT WORD ON PURPOSE. The tile there is called Graffiti and the mod it lives in
    # is called Posted Up, so a header reading OVERSPRAY would be a third name for a thing
    # that already has two. What carries across is the look, not the wordmark.
    other = os.path.join(os.path.dirname(HERE), 'hoodrich', 'data', 'icons')

    if os.path.isdir(other):
        g = wordmark('GRAFFITI')
        g.save(os.path.join(other, 'graffiti.png'))
        tin.save(os.path.join(other, 'spraycan.png'))
        print('  hoodrich   graffiti.png %dx%d   spraycan.png %dx%d' % (g.size + tin.size))
        print('    graffiti aspect %.4f' % (g.size[0] / float(g.size[1])))
    else:
        print('  hoodrich   not beside this repo; skipped')

    print('    logo aspect %.4f   can aspect %.4f'
          % (mark.size[0] / float(mark.size[1]), tin.size[0] / float(tin.size[1])))


if __name__ == '__main__':
    main()
