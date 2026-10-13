"""
Макет экрана уровня 2.0 без Unity: спрайты рисует dotnet/KnittedPicture.ArtPreview (тот же код, что в игре),
раскладка повторяет формулы GameScreen/KnitBoard. Нужен, чтобы сверять пропорции с Figma.

    dotnet run -c Release --project dotnet/KnittedPicture.ArtPreview -- OUT
    python3 tools/design/screen_mock.py OUT/sprites mock.png
"""
import math
import sys
from PIL import Image, ImageDraw, ImageFont

SPR = sys.argv[1]
OUT = sys.argv[2]
K = 2  # пикселей на точку
W, H = 390, 800
PAD = 16
FONT_BOLD = 'Assets/Resources/Fonts/Main.ttf'
FONT_REG = 'Assets/Resources/Fonts/Regular.ttf'

def hexc(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

def spr(name):
    return Image.open(f'{SPR}/{name}.png').convert('RGBA')

img = Image.new('RGBA', (W * K, H * K), hexc('#F7F1EA'))
dr = ImageDraw.Draw(img)

def put(im, x, y, w, h, pivot=(0, 0), alpha=1.0, rot=0):
    """x, y — точка опоры в точках (y вниз), pivot — доля (0..1) от левого верхнего угла."""
    im = im.resize((max(1, round(w * K)), max(1, round(h * K))), Image.LANCZOS)
    if rot:
        im = im.rotate(rot, resample=Image.BICUBIC, expand=True)
    if alpha < 1:
        a = im.getchannel('A').point(lambda v: int(v * alpha)); im.putalpha(a)
    px = round(x * K - im.width * pivot[0]); py = round(y * K - im.height * pivot[1])
    img.alpha_composite(im, (px, py))

def nine(im, x, y, w, h, border_px, border_pt):
    """9-slice: border_px — края в текстуре, border_pt — их размер на экране в точках."""
    sw, sh = im.size; b = border_px; bp = round(border_pt * K)
    W2, H2 = round(w * K), round(h * K)
    out = Image.new('RGBA', (W2, H2), (0, 0, 0, 0))
    xs = [(0, b, 0, bp), (b, sw - b, bp, W2 - bp), (sw - b, sw, W2 - bp, W2)]
    ys = [(0, b, 0, bp), (b, sh - b, bp, H2 - bp), (sh - b, sh, H2 - bp, H2)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if dx1 <= dx0 or dy1 <= dy0: continue
            out.alpha_composite(im.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS), (dx0, dy0))
    img.alpha_composite(out, (round(x * K), round(y * K)))

def text(s, x, y, size, color, bold=True, anchor='la'):
    f = ImageFont.truetype(FONT_BOLD if bold else FONT_REG, round(size * K))
    dr.text((x * K, y * K), s, font=f, fill=color, anchor=anchor)

def pill(x, y, w, h, fill, shadow=True):
    if shadow:
        dr.rounded_rectangle((x * K, (y + 2) * K, (x + w) * K, (y + h + 2) * K), radius=h / 2 * K, fill=hexc('#D3C8F3'))
    dr.rounded_rectangle((x * K, y * K, (x + w) * K, (y + h) * K), radius=h / 2 * K, fill=fill)

INK, INK2, INK3 = hexc('#2B2350'), hexc('#544C7A'), hexc('#756CA6')
RED, BLUE, PINK, YEL = 'E8475F', '5AA9F0', 'F27BC0', 'FFD23F'

# ---------- Раскладка (GameScreen.ComputeLayout) ----------
BAR_Y = 68; TOP = BAR_Y + 22; LOOM_H = 78; BOOSTER = 64
cols = 4
col_w = (W - 2 * PAD) / cols
S = 54
DEPTH = [1, 0.84, 0.72]
def field_height():
    top = 6 + S * 0.5
    for d in (1, 2): top += S * (DEPTH[d - 1] + DEPTH[d]) * 0.5 * 0.96
    return top + S * DEPTH[2] * 0.5 + 14
def depth_y(d):
    y = field_h - 6 - S * 0.5
    for i in range(1, d + 1): y -= S * (DEPTH[i - 1] + DEPTH[i]) * 0.5 * 0.96
    return y
boosters_y = H - 10 - BOOSTER
M = 7; HEADER = 30; LABEL = 20
GW, GH = 12, 10
_gw = (W - 2 * PAD) - 2 * M - LABEL - 8; _cw = min(_gw / GW, 30); _rh = _cw * 0.82; _gap = max(_rh * 0.55, 9)
needed = 2 * M + HEADER + GH * _rh + _gap + 14
for size in range(58, 39, -2):
    S = min(size, col_w - 16)
    field_h = field_height()
    field_y = boosters_y - 8 - field_h
    region = field_y - 10 - TOP
    if region - 12 - LOOM_H >= needed:
        break
board_h = max(200, min(needed, region - 12 - LOOM_H))
extra = max(0, region - 12 - LOOM_H - board_h)
board_y = TOP + extra * 0.4
loom_y = board_y + board_h + 12

# ---------- Шапка ----------
dr.ellipse((PAD * K, 8 * K, (PAD + 44) * K, 52 * K), fill=hexc('#FFFFFF'))
for bx in (PAD + 17, PAD + 24):
    dr.rounded_rectangle((bx * K, 21 * K, (bx + 4) * K, 39 * K), radius=2 * K, fill=INK)
lw = 120
pill(W / 2 - lw / 2, 10, lw, 40, hexc('#FFFFFF'))
text('Уровень 24', W / 2, 30, 18, INK, anchor='mm')
cw_ = 96
pill(W - PAD - cw_, 8, cw_, 44, hexc('#FFFFFF'))
dr.ellipse(((W - PAD - cw_ + 12) * K, 17 * K, (W - PAD - cw_ + 38) * K, 43 * K), fill=hexc('#FFC23C'), outline=hexc('#D98E0A'), width=2 * K)
text('1 250', W - PAD - cw_ + 44, 30, 17, INK, anchor='lm')
tx = PAD + 8; tw = W - 2 * PAD - 8 - 58
dr.rounded_rectangle((tx * K, (BAR_Y - 5) * K, (tx + tw) * K, (BAR_Y + 5) * K), radius=5 * K, fill=hexc('#E9E3D5'))
frac = 0.6
dr.rounded_rectangle((tx * K, (BAR_Y - 5) * K, (tx + tw * frac) * K, (BAR_Y + 5) * K), radius=5 * K, fill=hexc('#8B5CF6'))
dr.rounded_rectangle(((tx + 5) * K, (BAR_Y - 4) * K, (tx + tw * frac - 5) * K, (BAR_Y - 2) * K), radius=1 * K, fill=(255, 255, 255, 90))
try:
    moth = Image.open(sys.argv[3]).convert('RGBA')
    put(moth, tx + tw * frac + 8, BAR_Y - 3, 34, 34, (0.5, 0.5), rot=90)
except Exception:
    pass

# ---------- Доска (KnitBoard) ----------
bw = W - 2 * PAD
bh = board_h
bx, by = PAD, board_y
shadow = Image.new('RGBA', (round((bw + 20) * K), round((bh + 24) * K)), (0, 0, 0, 0))
ImageDraw.Draw(shadow).rounded_rectangle((10 * K, 14 * K, (bw + 10) * K, (bh + 14) * K), radius=24 * K, fill=(91, 69, 174, 50))
from PIL import ImageFilter
img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(8 * K)), (round((bx - 10) * K), round((by - 6) * K)))
nine(spr('wood'), bx, by, bw, bh, 96, 32)
vx, vy, vw, vh = bx + M, by + M, bw - 2 * M, bh - 2 * M
paper = spr('paper')
view = Image.new('RGBA', (round(vw * K), round(vh * K)))
for py in range(0, view.height, 128 * K // 3):
    for px in range(0, view.width, 128 * K // 3):
        view.alpha_composite(paper.resize((128 * K // 3, 128 * K // 3)), (px, py))
img.alpha_composite(view, (round(vx * K), round(vy * K)))
text('Схема · «Сердечко»', vx + 12, vy + 16, 16, INK2, anchor='lm')

heart = ["000000000000", "001100001100", "011110011110", "111111111111", "111111111111",
         "011111111110", "001111111100", "000111111000", "000011110000", "000001100000"]
heights = [6, 6, 6, 6, 6, 6, 6, 5, 5, 5, 5, 5]
grid_w = bw - 2 * M - LABEL - 8
cw = min(grid_w / GW, 30); rh = cw * 0.82; gap = max(rh * 0.55, 9)
gx = LABEL + (grid_w - cw * GW) / 2
top_row = max(heights); lowest = min(heights)
text(f'ряд {lowest + 1} из {GH}', vx + vw - 12, vy + 16, 13, INK3, anchor='rm')
grid_bottom = vh - 3
content = GH * rh + gap; avail = grid_bottom - HEADER
if content <= avail:
    ny = HEADER + (avail - content) / 2 + (GH - top_row) * rh + gap / 2
else:
    ny = max(grid_bottom - top_row * rh - gap / 2, HEADER + gap / 2 + min(3.2, GH - top_row) * rh)
lowest_vis = grid_bottom - gap / 2 - (top_row - lowest + 1) * rh
ny = min(ny, lowest_vis); ny = max(ny, HEADER + gap / 2)
n = top_row
def row_y(r):
    side = max(-1, min(1, (n - 0.5 - r) * 2))
    return ny + (n - 1 - r) * rh + rh / 2 + gap / 2 * side
def color_of(c, r):
    return RED if heart[GH - 1 - r][c] == '1' else BLUE
paper_rgb = (0xFB, 0xF7, 0xEF)
def lerp(a, b, t): return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))
sq = Image.new('RGBA', (60, 60))
for r in range(GH):
    for c in range(GW):
        if r < heights[c]: continue
        front = r == heights[c]; nxt = r == heights[c] + 1
        pale = 0 if front else 0.45 if nxt else 0.62
        col = lerp(hexc(color_of(c, r))[:3], paper_rgb, pale)
        cx = vx + gx + c * cw + cw / 2; cy = vy + row_y(r)
        if cy < vy + HEADER - 4 or cy > vy + vh: continue
        dr.rounded_rectangle(((cx - cw * 0.39) * K, (cy - rh * 0.37) * K, (cx + cw * 0.39) * K, (cy + rh * 0.37) * K), radius=cw * 0.2 * K, fill=col + (255,))
        if front:
            s = rh * 0.98
            put(spr('frame'), cx, cy, s, s, (0.5, 0.5), alpha=0.6)
for r in range(GH):
    if r - n + 1 > 0.5 and vy + row_y(r) > vy + HEADER:
        text(str(r + 1), vx + LABEL / 2, vy + row_y(r), 12, INK3, anchor='mm')
sw = cw * 1.06; sh = sw * 120 / 96
for r in range(GH):
    for c in range(GW):
        if r >= heights[c]: continue
        cx = vx + gx + c * cw + cw / 2; bottom = vy + row_y(r) + rh / 2
        if bottom - sh > vy + vh: continue
        put(spr('stitch_' + color_of(c, r)), cx, bottom, sw, sh, (0.5, 1 - 14 / 120 * 0.6))
# обрезка окна доски: вернуть рамку поверх выступов снизу
# спица и живые петли
th = max(9, rh * 0.62)
needle_y = by + M + ny
nine(spr('needle'), bx + 2, needle_y - th / 2, bw - 4, th, 30, th)
put(spr('knob'), bx + bw + 1, needle_y, th * 1.15, th * 1.15, (0.5, 0.5))
for c in range(GW):
    if heights[c] != top_row: continue
    lw_ = cw * 1.12
    put(spr('loop_' + color_of(c, top_row - 1)), bx + M + gx + c * cw + cw / 2, needle_y - lw_ * 0.06, lw_, lw_ * 60 / 64, (0.5, 0.5))

# плашка комбо
cx0 = W - PAD - 132 - 6; cy0 = by - 16
pill(cx0, cy0, 132, 32, hexc('#FFFFFF'))
dr.polygon([((cx0 + 22) * K, (cy0 + 7) * K), ((cx0 + 15) * K, (cy0 + 17) * K), ((cx0 + 20) * K, (cy0 + 17) * K), ((cx0 + 17) * K, (cy0 + 26) * K), ((cx0 + 26) * K, (cy0 + 14) * K), ((cx0 + 21) * K, (cy0 + 14) * K)], fill=hexc('#D98E0A'))
text('Комбо ×3', cx0 + 32, cy0 + 15, 15, INK, anchor='lm')

# ---------- Станок ----------
for x, ang in ((W * 0.52, 4), (W * 0.6, -3)):
    put(spr('loomneedle'), x, loom_y + 10, W * 0.62, 12, (0.5, 0.5), rot=ang)
bar_h = LOOM_H - 12
nine(spr('loombar'), PAD, loom_y + 12, W - 2 * PAD, bar_h, 96, 96 / 2.7)
slots = 5
well = min(bar_h - 14, (W - 2 * PAD - 24) / slots - 8)
step = (W - 2 * PAD - 24) / slots
slot_spool = min(48, well + 2)
centres = []
for i in range(slots):
    cx = PAD + 12 + step * (i + 0.5); cy = loom_y + 12 + bar_h / 2 + 1
    centres.append((cx, cy))
    if i == 4:
        g = spr('glow'); put(g, cx, cy, well * 1.32, well * 1.32, (0.5, 0.5))
    put(spr('well'), cx, cy, well, well, (0.5, 0.5))
def ball(name, cx, cy, size, overlays=(), badge=None, deep=False, scale=1.0):
    size *= scale
    art = size / (124 / 160)
    put(spr('shadow'), cx + size * 0.04, cy + size * 0.48, size * 0.95, size * 0.3, (0.5, 0.5), alpha=0.5 if deep else 1)
    put(spr(name), cx, cy - size * 0.02, art, art, (0.5, 0.5))
    for o in overlays:
        put(spr(o), cx, cy - size * 0.02, art, art, (0.5, 0.5))
    if badge is not None:
        bh_ = round(size * 0.44)
        bx_ = cx - size / 2 + size - bh_ * 0.78; by_ = cy - size / 2 + size - bh_ * 0.86
        put(spr('badge'), bx_, by_, bh_, bh_)
        text(str(badge), bx_ + bh_ / 2, by_ + bh_ / 2, bh_ * 0.5, INK, anchor='mm')
for i, col in enumerate((RED, BLUE)):
    cx, cy = centres[i]
    ball('ball_' + col, cx, cy - 2, slot_spool)

# рабочая нить: вверх вдоль левого края доски, над спицей дугой к петле (как YarnRopes)
def cubic(a, b, c, d, t):
    u = 1 - t
    return (u**3*a[0] + 3*u*u*t*b[0] + 3*u*t*t*c[0] + t**3*d[0], u**3*a[1] + 3*u*u*t*b[1] + 3*u*t*t*c[1] + t**3*d[1])
sx, sy = centres[0][0], centres[0][1] - 2 - slot_spool * 0.32
ex, ey = bx + M + gx + 7 * cw + cw / 2, needle_y
edge = bx + M * 0.5
mid = (edge, ey - 22)
rise = max(30, sy - mid[1])
P = [(sx, sy), (sx, sy - rise * 0.35 - 16), (edge, mid[1] + rise * 0.4), mid]
Q = [mid, (edge, mid[1] - 22), (ex + (edge - ex) * 0.3, ey - 34), (ex, ey)]
pts = [cubic(*P, i / 30) for i in range(31)] + [cubic(*Q, i / 30) for i in range(1, 31)]
rope_c = hexc('#' + RED)
for w_, col in ((4.2, lerp(rope_c[:3], (60, 20, 40), 0.35)), (3.0, rope_c[:3]), (1.2, lerp(rope_c[:3], (255, 255, 255), 0.35))):
    dr.line([(x * K, y * K) for x, y in pts], fill=col + (255,), width=round(w_ * K), joint='curve')

# ---------- Поле ----------
field = [  # (столбец, глубина, цвет, накладки, петли)
    (0, 2, 'F4EEE4', (), None), (1, 2, '1FA67A', ('knots2',), None), (2, 2, 'F27BC0', (), None), (3, 2, None, (), None),
    (0, 1, '5AA9F0', (), None), (1, 1, 'F27BC0', (), None), (2, 1, '8E52D9', ('padlock',), None), (3, 1, 'E8475F', (), None),
    (0, 0, 'E8475F', (), 12), (1, 0, 'F27BC0', ('ribbon',), 8), (2, 0, '5AA9F0', (), 16), (3, 0, 'FFD23F', ('ice1',), 6),
]
for c, d, col, ov, loops in field:
    cx = PAD + col_w * (c + 0.5); cy = field_y + depth_y(d)
    if col is None:
        ball('hidden', cx, cy, S, scale=DEPTH[d], deep=True); text('?', cx, cy - 2, S * DEPTH[d] * 0.56, hexc('#8B5CF6'), anchor='mm'); continue
    name = ('deep_' if d > 0 else 'ball_') + col
    ball(name, cx, cy, S, ov, loops, deep=d > 0, scale=DEPTH[d])

# ---------- Бустеры ----------
step_b = (W - 2 * PAD) / 4
x0 = W / 2 - step_b * 3 / 2
for i, (name, badge, bcol, fcol) in enumerate((('b_scissors', '3', '#2B2350', '#FFFFFF'), ('b_undo', 'даром', '#17945F', '#FFFFFF'), ('b_slot', '90', '#FFC23C', '#2B2350'))):
    x = x0 + step_b * (i + 0.5) - BOOSTER / 2; y = boosters_y
    dr.rounded_rectangle((x * K, (y + 3) * K, (x + BOOSTER) * K, (y + BOOSTER + 3) * K), radius=19 * K, fill=hexc('#D3C8F3'))
    dr.rounded_rectangle((x * K, y * K, (x + BOOSTER) * K, (y + BOOSTER) * K), radius=19 * K, fill=hexc('#FFFFFF'))
    put(spr(name), x + BOOSTER / 2, y + BOOSTER / 2, BOOSTER * 0.78, BOOSTER * 0.78, (0.5, 0.5))
    f = ImageFont.truetype(FONT_BOLD, 13 * K)
    tw_ = dr.textlength(badge, font=f) / K + (26 if name == 'b_slot' else 14)
    tw_ = max(24, tw_)
    bx_ = x + BOOSTER - tw_ * 0.7; by_ = y - 10
    dr.rounded_rectangle((bx_ * K, by_ * K, (bx_ + tw_) * K, (by_ + 24) * K), radius=12 * K, fill=hexc(bcol))
    text(badge, bx_ + tw_ / 2 + (6 if name == 'b_slot' else 0), by_ + 12, 13, hexc(fcol), anchor='mm')
    if name == 'b_slot':
        dr.ellipse(((bx_ + 5) * K, (by_ + 5) * K, (bx_ + 19) * K, (by_ + 19) * K), fill=hexc('#D98E0A'))

img.save(OUT)
print('saved', OUT, 'board', round(bw), 'x', round(bh), 'field_y', round(field_y), 'loom_y', round(loom_y))
