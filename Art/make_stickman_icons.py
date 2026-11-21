"""Draft Powder Man stick-man app icons, drawn on a 36x36 cell grid (12 px per cell), y = 0 at the bottom.
Usage: python3 Art/make_stickman_icons.py Art/IconDrafts/stickman"""
import math, random, sys, os
from PIL import Image, ImageDraw

OUT = sys.argv[1]
SIZE, CELLS = 432, 36
CELL = SIZE // CELLS
SAND_LIGHT, SAND, SAND_DARK = (246, 222, 160), (230, 201, 131), (176, 141, 84)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def sand_shade(t):
    t = min(1, max(0, t))
    return lerp(SAND_LIGHT, SAND, t * 2) if t < 0.5 else lerp(SAND, SAND_DARK, (t - 0.5) * 2)


class Canvas:
    def __init__(self, seed):
        self.cells = {}  # (x, y) -> 'S' solid body or 'g' loose grain
        self.rng = random.Random(seed)

    def dot(self, x, y, kind='S'):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < CELLS and 0 <= y < CELLS and self.cells.get((x, y)) != 'S':
            self.cells[(x, y)] = kind

    def line(self, x0, y0, x1, y1, thick=2):
        """A pixel line, thickened sideways so every stroke is exactly 2 cells wide."""
        steep = abs(y1 - y0) >= abs(x1 - x0)
        steps = max(1, int(round(max(abs(x1 - x0), abs(y1 - y0)))))
        for i in range(steps + 1):
            t = i / steps
            x, y = int(round(x0 + (x1 - x0) * t)), int(round(y0 + (y1 - y0) * t))
            for k in range(thick):
                self.dot(x + k, y) if steep else self.dot(x, y - k)

    def line_chunky(self, x0, y0, x1, y1, thick=2):
        """The first-draft line: softer, chunkier strokes. Used by the running pose (the app icon)."""
        steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(steps + 1):
            t = i / steps
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            for dx in range(thick):
                for dy in range(thick):
                    self.dot(x - (thick - 1) / 2 + dx, y - (thick - 1) / 2 + dy)

    def ring(self, cx, cy, r, thick=2):
        for y in range(CELLS):
            for x in range(CELLS):
                d = math.hypot(x - cx, y - cy)
                if r - thick < d <= r:
                    self.dot(x, y)

    def pile(self, cx, base, half_width, height):
        for x in range(int(cx - half_width), int(cx + half_width) + 1):
            h = height * (1 - abs(x - cx) / (half_width + 1))
            for y in range(base, base + max(1, int(round(h + self.rng.uniform(-0.4, 0.4))))):
                self.dot(x, y)

    def grains(self, pts):
        for x, y in pts:
            self.dot(x, y, 'g')

    def image(self, bg_top=(30, 33, 46), bg_bottom=(14, 16, 23)):
        bg = Image.new("RGB", (SIZE, SIZE))
        d = ImageDraw.Draw(bg)
        for y in range(SIZE):
            d.line([(0, y), (SIZE, y)], fill=lerp(bg_top, bg_bottom, y / SIZE))
        fg = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        fd = ImageDraw.Draw(fg)
        for (x, y), kind in self.cells.items():
            t = self.rng.uniform(0.25, 0.65)
            if kind == 'S':
                if (x - 1, y) not in self.cells or (x, y + 1) not in self.cells: t -= 0.25  # lit from top-left
                if (x + 1, y) not in self.cells or (x, y - 1) not in self.cells: t += 0.25
            py = SIZE - (y + 1) * CELL
            fd.rectangle([x * CELL, py, x * CELL + CELL - 1, py + CELL - 1], fill=sand_shade(t) + (255,))
        icon = bg.convert("RGBA")
        icon.alpha_composite(fg)
        return icon, bg, fg


def waving():
    c = Canvas(1)
    c.ring(18, 26, 4.5)
    c.line(18, 21, 18, 14)            # body
    c.line(18, 19, 12, 14)            # left arm down
    c.line(18, 19, 23, 20)            # right arm: out to the elbow...
    c.line(23, 20, 25, 25)            # ...and up, waving
    c.line(18, 14, 14, 7)             # legs
    c.line(18, 14, 22, 7)
    c.line(11, 6, 26, 6, 1)           # ground
    c.grains([(26.5, 27), (27.5, 24.5), (26, 22.5), (11, 12), (11.5, 9.5)])
    return c


def running():
    """The app icon."""
    c = Canvas(2)
    c.ring(21, 24, 4.6)
    c.line_chunky(20, 19, 17, 13)            # body, leaning forward
    c.line_chunky(19.5, 17.5, 24, 15.5)      # front arm
    c.line_chunky(19.5, 17.5, 14.5, 18.5)    # back arm
    c.line_chunky(17, 13, 21, 10.5)          # front leg
    c.line_chunky(21, 10.5, 22, 7)
    c.line_chunky(17, 13, 13, 11)            # back leg
    c.line_chunky(13, 11, 10.5, 8.5)
    c.grains([(11, 18), (9, 17.5), (12.5, 15), (8, 15), (10, 13), (6, 16), (7, 12.5), (5, 13.5),
              (8.5, 10), (4, 11), (6, 9), (11.5, 16.5), (13, 20), (10.5, 20.5)])
    return c


def pouring():
    c = Canvas(3)
    c.ring(13, 26, 4.5)
    c.line(13, 21, 13, 14)            # body
    c.line(13, 19, 8, 14)             # left arm down
    c.line(13, 19, 21, 20)            # right arm held out
    c.line(13, 14, 10, 7)             # legs
    c.line(13, 14, 16, 7)
    # stream from the hand onto a pile
    c.grains([(22.5, 19), (22.5, 17.5), (23, 16), (22.5, 14.5), (23, 13), (22.5, 11.5), (23, 10), (23, 8.5)])
    c.pile(23, 6, 5, 2.5)
    c.line(7, 6, 18, 6, 1)            # ground
    return c


def crumbling():
    c = Canvas(4)
    c.ring(18, 26, 4.5)
    c.line(18, 21, 18, 16)            # top half of the body still solid
    c.line(18, 19, 12, 22)            # arms flung up: "whoops"
    c.line(18, 19, 24, 22)
    # the bottom half has turned to grains, falling onto a pile
    c.grains([(18, 14.5), (17, 13), (19, 12.5), (16, 11.5), (18.5, 11), (20, 10.5), (15, 10), (17.5, 9.5),
              (21, 9), (14.5, 13.5), (21.5, 13)])
    c.pile(18, 6, 8, 3)
    return c


DRAFTS = {"1_waving": waving, "2_running": running, "3_pouring": pouring, "4_crumbling": crumbling}


def round_mask(img):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).ellipse([0, 0, img.size[0] - 1, img.size[1] - 1], fill=255)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(img, mask=m)
    return out


os.makedirs(OUT, exist_ok=True)
icons = []
for name, make in DRAFTS.items():
    icon, bg, fg = make().image()
    icon.save(f"{OUT}/{name}.png")
    bg.save(f"{OUT}/{name}_background.png")
    fg.save(f"{OUT}/{name}_foreground.png")
    icons.append((name, icon))

pad, big, mid, small = 30, 300, 150, 64
sheet = Image.new("RGBA", (pad + len(icons) * (big + pad), pad * 3 + big + mid + 40), (240, 240, 244, 255))
sd = ImageDraw.Draw(sheet)
for i, (name, icon) in enumerate(icons):
    x = pad + i * (big + pad)
    sheet.alpha_composite(icon.resize((big, big), Image.NEAREST), (x, pad))
    sd.text((x, pad + big + 6), name.replace("_", "  -  "), fill=(30, 30, 40))
    sheet.alpha_composite(round_mask(icon.resize((mid, mid), Image.LANCZOS)), (x, pad * 2 + big + 20))
    sheet.alpha_composite(icon.resize((small, small), Image.LANCZOS), (x + mid + 20, pad * 2 + big + 20 + (mid - small) // 2))
sheet.save(f"{OUT}/contact_sheet.png")
print("ok")
