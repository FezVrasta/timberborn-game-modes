#!/usr/bin/env python3
"""Draws the mod's cover: three stacked buttons like the game mode list, the
middle one picked, in the brush strokes of Timber Empires' icons
(Models/Icons/make_icons.py, found next to this repo), on a blueprint-paper
background like the Mod Uploader's.

    python3 Tools/make_cover.py [Timber Empires checkout]

Writes Media/cover.png (1920x1080, for the Workshop and mod.io pages) and
GameModes/thumbnail.png (800x450, the one the game's mod list shows).
"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
TE = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT.parent / "TimberEmpires"
sys.path.insert(0, str(TE / "Models" / "Icons"))
import make_icons as mi  # noqa: E402

W, H = 1920, 1080
ART = 1000                      # the drawing's square
mi.SIZE = ART
mi.SCALE = ART * mi.SS / 100

PAPER = (28, 62, 104)
PAPER_LIGHT = (52, 102, 156)
LINE = (150, 190, 228)


def background():
    rnd = random.Random(3)
    # Paper lit from the middle
    cx, cy = W * 0.5, H * 0.46
    img = Image.new("RGB", (W, H))
    px = img.load()
    maxd = math.hypot(W / 2, H / 2)
    for y in range(H):
        for x in range(W):
            k = min(1, math.hypot(x - cx, (y - cy) * 1.25) / maxd)
            k = k ** 1.4
            px[x, y] = tuple(int(PAPER_LIGHT[i] + (PAPER[i] - PAPER_LIGHT[i]) * k) for i in range(3))
    img = img.convert("RGBA")

    # The grid: fine every 30px, heavier every 150px, drawn a touch uneven
    grid = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(grid)
    for n, x in enumerate(range(0, W + 1, 30)):
        major = n % 5 == 0
        d.line([(x, 0), (x, H)], fill=LINE + (70 if major else 26,), width=2 if major else 1)
    for n, y in enumerate(range(0, H + 1, 30)):
        major = n % 5 == 0
        d.line([(0, y), (W, y)], fill=LINE + (70 if major else 26,), width=2 if major else 1)
    img.alpha_composite(grid)

    # Drafting doodles off to the sides: a compass arc, a dimension line, a title block
    doo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(doo)
    ink = LINE + (95,)
    d.arc([-260, 620, 340, 1220], 270, 360, fill=ink, width=3)
    d.arc([-200, 680, 280, 1160], 270, 360, fill=ink, width=2)
    d.arc([1560, -300, 2160, 300], 90, 180, fill=ink, width=3)
    d.arc([1620, -240, 2100, 240], 90, 180, fill=ink, width=2)
    for x0, y0, x1 in ((120, 130, 520), (1400, 960, 1800)):
        d.line([(x0, y0), (x1, y0)], fill=ink, width=3)
        for x in (x0, x1):
            d.line([(x, y0 - 14), (x, y0 + 14)], fill=ink, width=3)
            d.line([(x - 8, y0 + 8), (x + 8, y0 - 8)], fill=ink, width=3)
    d.rectangle([1450, 860, 1830, 940], outline=ink, width=3)
    d.line([(1450, 900), (1830, 900)], fill=ink, width=2)
    d.line([(1600, 860), (1600, 940)], fill=ink, width=2)
    img.alpha_composite(doo)
    return img


def figure(g, x, y, w=2.8):
    """A settler standing with its feet at y, 20 high."""
    g.circle(x, y - 17, 3.6, w=w)
    g.line((x, y - 12), (x, y - 5), w=w + 0.4)
    g.line((x, y - 5), (x - 3.5, y), w=w)
    g.line((x, y - 5), (x + 3.5, y), w=w)


def drawing():
    """Three cards side by side, the game mode list: Co-op, PvE and PvP, PvE picked. 100 grid.
    Returns the strokes and the cards' fill."""
    g = mi.Glyph()
    top, bottom = 24, 76
    fill = []
    for n, (left, right) in enumerate(((6, 31), (37.5, 62.5), (69, 94))):
        picked = n == 1
        cx = (left + right) / 2
        g.line((left, top), (right, top), (right, bottom), (left, bottom), (left, top), taper=False, w=3.2 if picked else 2.4)
        fill.append([(left, top), (right, top), (right, bottom), (left, bottom)])
        # Its picture: Co-op, two settlers side by side holding hands; PvE, a settler and a bandit camp; PvP, crossed swords
        if n == 0:
            figure(g, cx - 6.5, 55)
            figure(g, cx + 6.5, 55)
            g.line((cx - 6.5, 47.5), (cx + 6.5, 47.5), w=2.6)
        elif n == 1:
            figure(g, cx - 7, 55)
            g.line((cx - 7, 47.5), (cx - 3, 47.5), w=2.6)
            g.line((cx - 1, 55), (cx + 4.5, 40), (cx + 10, 55), (cx - 1, 55), taper=False, w=2.6)
            g.line((cx + 4.5, 40), (cx + 4.5, 36), (cx + 8.5, 37.5), w=2.2)
        else:
            for d in (-1, 1):
                # A blade from the top corner down to its hilt, with the guard across and the grip past it
                g.line((cx + 9 * d, 33), (cx - 6 * d, 52), w=2.8)
                g.line((cx - 2.8 * d - 3 * d, 46.8), (cx - 2.8 * d + 3 * d, 51.2), w=2.4)
                g.line((cx - 6 * d, 52), (cx - 8 * d, 55), w=3)
        # The card's name, as a stroke
        g.line((cx - 7, 63), (cx + 7, 63), w=3.2 if picked else 2.4)
        if picked:
            # A second outline inside, and the check on the corner
            g.line((left + 3, top + 3), (right - 3, top + 3), (right - 3, bottom - 3), (left + 3, bottom - 3), (left + 3, top + 3), taper=False, w=1.5)
            g.line((cx - 4, 70), (cx - 1, 73), (cx + 5, 67.5), w=2.8)
    return g, fill


def main():
    img = background()
    g, sheet = drawing()
    SS = mi.SS
    strokes = g.mask.resize((ART, ART), Image.LANCZOS)

    # The sheet's paper, lighter than the board, and the buttons' soft shadow
    paper = Image.new("L", (ART * SS, ART * SS), 0)
    for button in sheet:
        ImageDraw.Draw(paper).polygon([(x * mi.SCALE, y * mi.SCALE) for x, y in button], fill=255)
    paper = paper.resize((ART, ART), Image.LANCZOS)

    ox, oy = (W - ART) // 2, (H - ART) // 2 + 10
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    shadow = Image.new("L", (W, H), 0)
    shadow.paste(ImageChops.lighter(paper, strokes), (ox + 10, oy + 22))
    shadow = shadow.filter(ImageFilter.GaussianBlur(22)).point(lambda v: int(v * 0.55))
    black = Image.new("RGBA", (W, H), (6, 14, 30, 0))
    black.putalpha(shadow)
    img.alpha_composite(black)

    sheet_col = Image.new("RGBA", (ART, ART), (74, 128, 184, 0))
    sheet_col.putalpha(paper.point(lambda v: int(v * 0.55)))
    layer.alpha_composite(sheet_col, (ox, oy))

    # A glow behind the picked button
    glow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(glow).ellipse([W / 2 - 230, oy + 250, W / 2 + 230, oy + 750], fill=120)
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    warm = Image.new("RGBA", (W, H), (230, 200, 130, 0))
    warm.putalpha(glow.point(lambda v: int(v * 0.5)))
    layer.alpha_composite(warm)

    gold = Image.new("RGBA", (ART, ART), mi.GOLD + (0,))
    gold.putalpha(strokes)
    layer.alpha_composite(gold, (ox, oy))
    img.alpha_composite(layer)

    # Grain, then the vignette
    rnd = random.Random(11)
    noise = Image.effect_noise((W, H), 38).convert("L")
    grain = Image.new("RGBA", (W, H), (255, 255, 255, 0))
    grain.putalpha(noise.point(lambda v: max(0, v - 128) // 6))
    img.alpha_composite(grain)
    vig = Image.new("L", (W // 4, H // 4), 0)
    vp = vig.load()
    for y in range(vig.height):
        for x in range(vig.width):
            dx, dy = (x / vig.width - 0.5) * 2, (y / vig.height - 0.5) * 2
            vp[x, y] = int(255 * min(1, max(0, (math.hypot(dx, dy * 1.1) - 0.55) / 0.95)) ** 1.6 * 0.7)
    vig = vig.resize((W, H), Image.BICUBIC)
    dark = Image.new("RGBA", (W, H), (4, 10, 22, 0))
    dark.putalpha(vig)
    img.alpha_composite(dark)

    out = img.convert("RGB")
    (ROOT / "Media").mkdir(exist_ok=True)
    out.save(ROOT / "Media" / "cover.png")
    out.resize((800, 450), Image.LANCZOS).save(ROOT / "GameModes" / "thumbnail.png")


main()
