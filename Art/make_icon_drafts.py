"""Draft Powder Man app icons. Each sprite is an ASCII grid drawn onto a 36x36 cell icon (12 px per cell)."""
import random, sys, os
from PIL import Image, ImageDraw

OUT = sys.argv[1]
SIZE, CELLS = 432, 36
CELL = SIZE // CELLS

SAND_LIGHT, SAND, SAND_DARK = (246, 222, 160), (230, 201, 131), (176, 141, 84)
PALETTE = {
    'k': (24, 22, 30),      # eyes / mouth
    'w': (255, 255, 255),   # eye shine
    'f': (255, 226, 92),    # fire yellow
    'F': (255, 140, 40),    # fire orange
    'r': (220, 60, 20),     # fire red
    'b': (70, 140, 235),    # water
}

def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))

def sand_shade(t):
    return lerp(SAND_LIGHT, SAND, t * 2) if t < 0.5 else lerp(SAND, SAND_DARK, (t - 0.5) * 2)

SPRITES = {
"A_standing": """
......SSSSSS......
.....SSSSSSSS.....
....SSSSSSSSSS....
....SSkkSSkkSS....
....SSkkSSkkSS....
....SSSSSSSSSS....
....SSSkSSkSSS....
.....SSSkkSSS.....
......SSSSSS......
...SSSSSSSSSSSS...
..SSSSSSSSSSSSSS..
.SSS.SSSSSSSS.SSS.
.SS..SSSSSSSS..SS.
.SS..SSSSSSSS..SS.
.g...SSSSSSSS...g.
.....SSSSSSSS.....
.....SSS..SSS.....
.....SSS..SSS.....
.....SSS..SSS.....
.....SSg..gSS.....
....g.S....S.g....
..ggSgSg..gSgSgg..
.gSSSSSSggSSSSSSg.
SSSSSSSSSSSSSSSSSS
""",
"B_face": """
.......SSSSSSSSSS.......
.....SSSSSSSSSSSSSS.....
....SSSSSSSSSSSSSSSS....
...SSSSSSSSSSSSSSSSSS...
..SSSSSSSSSSSSSSSSSSSS..
..SSSSSSSSSSSSSSSSSSSS..
.SSSSSkkkSSSSSSkkkSSSSS.
.SSSSkwkkkSSSSkwkkkSSSS.
.SSSSkkkkkSSSSkkkkkSSSS.
.SSSSkkkkkSSSSkkkkkSSSS.
.SSSSSkkkSSSSSSkkkSSSSS.
.SSSSSSSSSSSSSSSSSSSSSS.
.SSSSSSSSSSSSSSSSSSSSSS.
.SSSSSkSSSSSSSSSSkSSSSS.
.SSSSSSkkSSSSSSkkSSSSSS.
..SSSSSSSkkkkkkSSSSSSS..
..SSSSSSSSSSSSSSSSSSS...
...SSSSSSSSSSSSSSSS.g...
....SSSSSSSSSSSSS.S..g..
.....SSSSSSSSSS.g....g..
......SSSSSSS.S...g.....
.......SSSS.g......g....
.........S....g.......g.
.....g.......g....g.....
""",
"C_fire_hair": """
......F.....F.....
.....Ff..F..fF.F..
..F..fF.FfF.fF.F..
..FfFffFffffFffF..
...Ffffffffffff...
......SSSSSS......
.....SSSSSSSS.....
....SSSSSSSSSS....
....SSkkSSkkSS....
....SSkkSSkkSS....
....SSSSSSSSSS....
....SSSkkkkSSS....
.....SSSSSSSS.....
......SSSSSS......
...SSSSSSSSSSSS...
..SSSSSSSSSSSSSS..
.SSS.SSSSSSSS.SSS.
.SS..SSSSSSSS..SS.
.SS..SSSSSSSS..SS.
.....SSSSSSSS.....
.....SSS..SSS.....
.....SSS..SSS.....
....SSSS..SSSS....
""",
"D_sprinkle": """
..............SSSS....
..............SSSSg...
......SSSSSS...SS.....
.....SSSSSSSS..SS..g..
....SSSSSSSSSS.SS.....
....SSkkSSkkSS.SS.g...
....SSkkSSkkSS.SS.....
....SSSSSSSSSS.SS..g..
....SSSkSSkSSS.SS.....
.....SSSkkSSS..SS.g...
......SSSSSS...SS.....
...SSSSSSSSSSSSSS..g..
..SSSSSSSSSSSSSS......
.SSS.SSSSSSSS.....g...
.SS..SSSSSSSS.........
.SS..SSSSSSSS......g..
.....SSSSSSSS.........
.....SSS..SSS.....g...
.....SSS..SSS....gSg..
.....SSS..SSS...SSSSS.
...SSSSS..SSSS.SSSSSSS
""",
}

def render(name, art, rng):
    rows = [r for r in art.strip("\n").split("\n")]
    h, w = len(rows), max(len(r) for r in rows)
    rows = [r.ljust(w, '.') for r in rows]
    grid = [[None] * CELLS for _ in range(CELLS)]
    ox = (CELLS - w) // 2
    oy = 6 + max(0, (24 - h) // 2)  # bottom of sprite sits at cell row 6 (adaptive-icon safe zone)

    def at(x, y):
        return rows[y][x] if 0 <= y < h and 0 <= x < w else '.'

    for y in range(h):
        for x in range(w):
            ch = rows[y][x]
            if ch == '.':
                continue
            if ch in 'Sg':
                t = rng.uniform(0.25, 0.65)
                if ch == 'S':
                    if at(x - 1, y) == '.' or at(x, y - 1) == '.': t -= 0.3   # lit from top-left
                    if at(x + 1, y) == '.' or at(x, y + 1) == '.': t += 0.3
                col = sand_shade(min(1, max(0, t)))
            else:
                col = PALETTE[ch]
                if ch in 'fF' and rng.random() < 0.3:
                    col = PALETTE['F' if ch == 'f' else 'r']
            gy = oy + (h - 1 - y)          # ASCII top row = highest cell
            grid[gy][ox + x] = col

    bg = Image.new("RGB", (SIZE, SIZE))
    top, bottom = (30, 33, 46), (14, 16, 23)
    if name.startswith("C"):
        top, bottom = (52, 30, 30), (18, 14, 18)
    d = ImageDraw.Draw(bg)
    for y in range(SIZE):
        d.line([(0, y), (SIZE, y)], fill=lerp(top, bottom, y / SIZE))
    fg = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fg)
    for gy in range(CELLS):
        for gx in range(CELLS):
            c = grid[gy][gx]
            if c:
                py = SIZE - (gy + 1) * CELL
                fd.rectangle([gx * CELL, py, gx * CELL + CELL - 1, py + CELL - 1], fill=c + (255,))
    icon = bg.convert("RGBA")
    icon.alpha_composite(fg)
    return icon, bg, fg

def round_mask(img):
    m = Image.new("L", img.size, 0)
    ImageDraw.Draw(m).ellipse([0, 0, img.size[0] - 1, img.size[1] - 1], fill=255)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste(img, mask=m)
    return out

os.makedirs(OUT, exist_ok=True)
icons = []
for name, art in SPRITES.items():
    icon, bg, fg = render(name, art, random.Random(3))
    icon.save(f"{OUT}/{name}.png")
    bg.save(f"{OUT}/{name}_background.png")
    fg.save(f"{OUT}/{name}_foreground.png")
    icons.append((name, icon))

# Contact sheet: each draft big, as a round launcher icon, and at real phone size.
pad, big, mid, small = 30, 300, 150, 64
sheet = Image.new("RGBA", (pad + len(icons) * (big + pad), pad * 3 + big + mid + 40), (240, 240, 244, 255))
sd = ImageDraw.Draw(sheet)
for i, (name, icon) in enumerate(icons):
    x = pad + i * (big + pad)
    sheet.alpha_composite(icon.resize((big, big), Image.NEAREST), (x, pad))
    sd.text((x, pad + big + 6), name[0] + "  -  " + name[2:].replace("_", " "), fill=(30, 30, 40))
    r = round_mask(icon.resize((mid, mid), Image.LANCZOS))
    sheet.alpha_composite(r, (x, pad * 2 + big + 20))
    sheet.alpha_composite(icon.resize((small, small), Image.LANCZOS), (x + mid + 20, pad * 2 + big + 20 + (mid - small) // 2))
sheet.save(f"{OUT}/contact_sheet.png")
print("ok")
