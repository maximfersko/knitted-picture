"""
Симуляция анимации вязания 2.0 кадрами (без Unity): те же формулы и тайминги, что в KnitBoard и YarnRopes.
Два клубка на станке вяжут «Сердечко»: шаг станка 65 мс, нить догоняет петлю пружиной (ω = 26),
через 0.1 с петля соскальзывает со спицы на место за 0.22 с (out-back, вытяжка 1.25 → 0.9 → 1),
петли ниже подтягиваются, столбик у спицы накидывает живую петлю, спица плавно поднимается.

    python3 tools/design/knit_anim.py SPRITES OUT_DIR   # кадры PNG и knit.mp4 (нужен ffmpeg)
"""
import math, os, subprocess, sys
from PIL import Image, ImageDraw, ImageFont

SPR, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)
K = 2
FPS = 30
W = 390
FONT_BOLD = 'Assets/Resources/Fonts/Main.ttf'
RED, BLUE = 'E8475F', '5AA9F0'
INK2, INK3 = (0x54, 0x4C, 0x7A, 255), (0x75, 0x6C, 0xA6, 255)

def spr(n): return Image.open(f'{SPR}/{n}.png').convert('RGBA')
cache = {}
def sized(n, w, h, rot=0):
    key = (n, round(w * K), round(h * K), round(rot))
    if key not in cache:
        im = spr(n).resize((max(1, round(w * K)), max(1, round(h * K))), Image.LANCZOS)
        if rot: im = im.rotate(rot, resample=Image.BICUBIC, expand=True)
        cache[key] = im
    return cache[key]

def ease_outback(t):
    c1 = 1.70158; c3 = c1 + 1
    return 1 + c3 * (t - 1) ** 3 + c1 * (t - 1) ** 2

# ---------- Модель (как LoomGame: петля в самом низком столбце, где цвет на фронте; натяжение 2) ----------
heart = ["000000000000", "001100001100", "011110011110", "111111111111", "111111111111",
         "011111111110", "001111111100", "000111111000", "000011110000", "000001100000"]
GW, GH, BAND = 12, 10, 2
def color_at(c, r): return RED if heart[GH - 1 - r][c] == '1' else BLUE
heights = [3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2]
def lowest(): return min(heights)
def find_target(col):
    best = None
    for c in range(GW):
        r = heights[c]
        if r >= GH or color_at(c, r) != col: continue
        if r > lowest() + BAND: continue
        if best is None or r < heights[best]: best = c
    return best

# ---------- Геометрия доски (KnitBoard) ----------
M, HEADER, LABEL = 7, 30, 20
bw = W - 32
grid_w = bw - 2 * M - LABEL - 8
cw = min(grid_w / GW, 30); rh = cw * 0.82; gap = max(rh * 0.55, 9)
gx = LABEL + (grid_w - cw * GW) / 2
bh = 2 * M + HEADER + GH * rh + gap + 14
vh = bh - 2 * M
bx, by = 16, 16
LOOM_Y = by + bh + 12
FRAME_H = LOOM_Y + 78 + 14

def aim():
    top = max(heights); low = lowest()
    grid_bottom = vh - 3
    content = GH * rh + gap; avail = grid_bottom - HEADER
    ny = HEADER + (avail - content) / 2 + (GH - top) * rh + gap / 2
    ny = min(ny, grid_bottom - gap / 2 - (top - low + 1) * rh)
    return top, max(ny, HEADER + gap / 2)

def row_y(r, n, ny):
    side = max(-1, min(1, (n - 0.5 - r) * 2))
    return ny + (n - 1 - r) * rh + rh / 2 + gap / 2 * side

# ---------- Станок ----------
slots = 5
bar_h = 66
well = min(bar_h - 14, (W - 32 - 24) / slots - 8)
step = (W - 32 - 24) / slots
spool = min(48, well + 2)
centres = [(16 + 12 + step * (i + 0.5), LOOM_Y + 12 + bar_h / 2 + 1) for i in range(slots)]

# ---------- Хронология ----------
STEP = 0.065
REACH, LAND = 0.1, 0.22
events = []   # (t, slot, c, r, color)
t = 0.4
spools = [(0, RED, 40), (1, BLUE, 40)]
while t < 5.4:
    for slot, col, loops in spools:
        c = find_target(col)
        if c is None: continue
        r = heights[c]
        heights[c] += 1
        events.append((t, slot, c, r, col))
    t += STEP * 2.2   # игрок-середнячок: станок не всегда вяжет каждый шаг (кадры читаемее)
final_heights = heights[:]
heights = [3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 2, 2]
start = heights[:]

# ---------- Кадры ----------
font_title = ImageFont.truetype(FONT_BOLD, 16 * K)
font_row = ImageFont.truetype(FONT_BOLD, 13 * K)
font_num = ImageFont.truetype(FONT_BOLD, 12 * K)

def nine(img, name, x, y, w, h, bpx, bpt):
    im = spr(name); sw, sh = im.size; b = bpx; bp = round(bpt * K)
    W2, H2 = round(w * K), round(h * K)
    key = ('nine', name, W2, H2)
    if key not in cache:
        out = Image.new('RGBA', (W2, H2))
        xs = [(0, b, 0, bp), (b, sw - b, bp, W2 - bp), (sw - b, sw, W2 - bp, W2)]
        ys = [(0, b, 0, bp), (b, sh - b, bp, H2 - bp), (sh - b, sh, H2 - bp, H2)]
        for sx0, sx1, dx0, dx1 in xs:
            for sy0, sy1, dy0, dy1 in ys:
                if dx1 > dx0 and dy1 > dy0:
                    out.alpha_composite(im.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.LANCZOS), (dx0, dy0))
        cache[key] = out
    img.alpha_composite(cache[key], (round(x * K), round(y * K)))

def put(img, im, x, y, pivot=(0.5, 0.5)):
    img.alpha_composite(im, (round(x * K - im.width * pivot[0]), round(y * K - im.height * pivot[1])))

paper = spr('paper').resize((128 * K // 3, 128 * K // 3))
n_anim, ny_anim = None, None
rope = {0: None, 1: None}
dip, dipv = 0.0, 0.0   # [pos, vel, last_pull, reach, flow]
frames = int(6.2 * FPS)
dt = 1 / FPS
for f in range(frames):
    now = f * dt
    # модель на момент now: что уже связано (pending — в модели, но ещё не село)
    model = start[:]
    for (t0, slot, c, r, col) in events:
        if t0 <= now: model[c] = max(model[c], r + 1)
    heights = model
    top, ny_t = aim()
    if n_anim is None: n_anim, ny_anim = top, ny_t
    k = 1 - math.exp(-dt * 10)
    n_anim += (top - n_anim) * k; ny_anim += (ny_t - ny_anim) * k

    img = Image.new('RGBA', (W * K, round(FRAME_H * K)), (0xF7, 0xF1, 0xEA, 255))
    dr = ImageDraw.Draw(img)
    nine(img, 'wood', bx, by, bw, bh, 96, 32)
    vx, vy = bx + M, by + M
    view = Image.new('RGBA', (round((bw - 2 * M) * K), round(vh * K)))
    for py in range(0, view.height, paper.height):
        for px in range(0, view.width, paper.width):
            view.alpha_composite(paper, (px, py))
    vd = ImageDraw.Draw(view)
    def lerp(a, b, t): return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))
    def hexc(h): return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))
    # клетки схемы
    for r in range(GH):
        for c in range(GW):
            if r < model[c] and not any(e[2] == c and e[3] == r and e[0] + REACH > now for e in events if e[0] <= now): continue
            front = r == model[c]
            pending = r < model[c]
            pale = 0 if (front or pending) else 0.45 if r == model[c] + 1 else 0.62
            col = lerp(hexc(color_at(c, r)), (0xFB, 0xF7, 0xEF), pale)
            cx = gx + c * cw + cw / 2; cy = row_y(r, n_anim, ny_anim)
            vd.rounded_rectangle(((cx - cw * 0.39) * K, (cy - rh * 0.37) * K, (cx + cw * 0.39) * K, (cy + rh * 0.37) * K), radius=cw * 0.2 * K, fill=col + (255,))
            if front:
                s = rh * 0.98
                fr = sized('frame', s, s)
                put(view, fr, cx, cy)
    for r in range(GH):
        if r - n_anim + 1 > 0.5:
            vd.text((LABEL / 2 * K, row_y(r, n_anim, ny_anim) * K), str(r + 1), font=font_num, fill=INK3, anchor='mm')
    # полотно
    sw = cw * 1.06; sh = sw * 120 / 96
    for r in range(GH):
        for c in range(GW):
            if r >= model[c]: continue
            ev = next((e for e in events if e[2] == c and e[3] == r and e[0] <= now), None)
            cx = gx + c * cw + cw / 2; bottom = row_y(r, n_anim, ny_anim) + rh / 2
            sx, sy, dy = 1.0, 1.0, 0.0
            if ev is not None:
                tl = (now - ev[0] - REACH) / LAND
                if tl < 0: continue
                if tl < 1:
                    drop = max(rh * 0.6, row_y(r, n_anim, ny_anim) - (ny_anim + gap / 2))
                    dy = -drop * (1 - ease_outback(tl))
                    stretch = 1.25 + (0.9 - 1.25) * (tl / 0.6) if tl < 0.6 else 0.9 + 0.1 * (tl - 0.6) / 0.4
                    squeeze = 0.55 + 0.45 * tl / 0.25 if tl < 0.25 else 1
                    sx = squeeze * (2 - stretch) * 0.5 + 0.5 * squeeze; sy = stretch * squeeze
            # подтяжка: петля выше села недавно
            for kk in (1, 2):
                above = next((e for e in events if e[2] == c and e[3] == r + kk and e[0] <= now), None)
                if above is not None:
                    tt = (now - above[0] - REACH - LAND - 0.05 * (kk - 1)) / 0.2
                    if 0 <= tt <= 1:
                        dy += max(1.2, rh * 0.07) / kk * math.sin(tt * math.pi)
            im = sized('stitch_' + color_at(c, r), sw * sx, sh * sy)
            put(view, im, cx, bottom + dy, (0.5, 1 - 14 / 120 * 0.6))
    # шапка поверх
    hdr = Image.new('RGBA', (view.width, round(HEADER * K)))
    for px in range(0, hdr.width, paper.width): hdr.alpha_composite(paper, (px, 0))
    view.alpha_composite(hdr, (0, 0))
    vd = ImageDraw.Draw(view)
    vd.text((12 * K, 16 * K), 'Схема · «Сердечко»', font=font_title, fill=INK2, anchor='lm')
    vd.text(((bw - 2 * M - 12) * K, 16 * K), f'ряд {min(model) + 1} из {GH}', font=font_row, fill=INK3, anchor='rm')
    img.alpha_composite(view, (round(vx * K), round(vy * K)))
    # спица и петли
    th = max(9, rh * 0.62)
    landed_now = sum(1 for e in events if 0 <= now - e[0] - REACH < dt)
    dipv += landed_now * max(18, rh * 1.1)
    for _ in range(4):
        h_ = dt / 4
        dipv += (-900 * dip - 2 * 0.55 * 30 * dipv) * h_
        dip = max(-1, min(3.5, dip + dipv * h_))
    needle_y = vy + ny_anim + dip
    nine(img, 'needle', bx + 2, needle_y - th / 2, bw - 4, th, 30, th)
    put(img, sized('knob', th * 1.15, th * 1.15), bx + bw + 1, needle_y)
    for c in range(GW):
        if model[c] != top or top == 0: continue
        ev = next((e for e in events if e[2] == c and e[3] == top - 1 and e[0] <= now), None)
        scale = 1
        if ev is not None:
            tl = (now - ev[0] - REACH) / 0.2
            if tl < 0: continue
            scale = 0.2 + 0.8 * ease_outback(min(1, tl))
        lw = cw * 1.12 * scale
        put(img, sized('loop_' + color_at(c, top - 1), lw, lw * 60 / 64), vx + gx + c * cw + cw / 2, needle_y - lw * 0.06)
    # станок
    for x, ang in ((W * 0.52, 4), (W * 0.6, -3)):
        put(img, sized('loomneedle', W * 0.62, 12, ang), x, LOOM_Y + 10)
    nine(img, 'loombar', 16, LOOM_Y + 12, W - 32, bar_h, 96, 96 / 2.7)
    for i, (cx, cy) in enumerate(centres):
        put(img, sized('well', well, well), cx, cy)
    for slot, col, _ in spools:
        cx, cy = centres[slot]
        last = [e for e in events if e[1] == slot and e[0] <= now]
        wob = 0
        if last:
            tl = (now - last[-1][0]) / 0.12
            if tl < 1: wob = math.sin(tl * math.pi * 2) * 9
        art = spool / (124 / 160)
        put(img, sized('ball_' + col, art, art, wob), cx, cy - 2 - spool * 0.02)
    # нити (YarnRopes: пружина конца, край доски, натяг)
    for slot, col, _ in spools:
        last = [e for e in events if e[1] == slot and e[0] <= now]
        sxp, syp = centres[slot][0], centres[slot][1] - 2 - spool * 0.32
        if not last: continue
        e = last[-1]
        c, r = e[2], e[3]
        tx = vx + gx + c * cw + cw / 2
        ty = max(vy + row_y(r, n_anim, ny_anim) - rh * 0.5, vy + ny_anim)
        st = rope[slot]
        if st is None: st = rope[slot] = [[sxp, syp], [0, 0], 0, 0, 0]
        for _ in range(4):
            h_ = dt / 4; w_ = 26
            for i in (0, 1):
                tgt = (tx, ty)[i]
                st[1][i] += (w_ * w_ * (tgt - st[0][i]) - 2 * w_ * st[1][i]) * h_
                st[0][i] += st[1][i] * h_
        st[3] = min(1, st[3] + dt * 7)
        tug = max(0, 1 - (now - e[0]) * 6)
        slack = 1 - 0.35 * tug
        side_left = tx - bx < bx + bw - tx
        edge_t = (bx + M * 0.5) if side_left else (bx + bw - M * 0.5)
        if len(st) < 6: st.append(edge_t)
        st[5] += (edge_t - st[5]) * (1 - math.exp(-dt * 6))
        edge = st[5]
        ex, ey = st[0]
        bottom = by + bh
        top_y = min(ey, ty) - 22
        under = max(bottom + 8, syp - 26)
        W_ = [(sxp, syp), (sxp + (edge - sxp) * 0.15, (syp + under) / 2), ((sxp + edge) / 2, under),
              (edge, bottom - 14), (edge, top_y), (ex + (edge - ex) * 0.3, ey - 26 * slack), (ex, ey)]
        def cr(t):
            n = len(W_) - 1; f = min(max(t, 0), 1) * n; i = min(int(f), n - 1); u = f - i
            p0 = W_[max(0, i - 1)]; p1 = W_[i]; p2 = W_[i + 1]; p3 = W_[min(n, i + 2)]
            return tuple(0.5 * (2 * p1[k] + (p2[k] - p0[k]) * u + (2 * p0[k] - 5 * p1[k] + 4 * p2[k] - p3[k]) * u * u + (3 * p1[k] - p0[k] - 3 * p2[k] + p3[k]) * u ** 3) for k in (0, 1))
        pts = [cr(i / 60) for i in range(61)]
        n_vis = max(2, int(len(pts) * st[3]))
        pts = pts[:n_vis]
        base = hexc(col)
        for w_, cc in ((4.0, lerp(base, (60, 20, 40), 0.35)), (2.8, base), (1.1, lerp(base, (255, 255, 255), 0.4))):
            dr.line([(x * K, y * K) for x, y in pts], fill=cc + (255,), width=round(w_ * K), joint='curve')
        # бегущие витки на нити
        flow = (now * (1.2 + 2.5 * tug)) % 1
        for i in range(0, len(pts) - 1, 3):
            j = (i + int(flow * 3)) % (len(pts) - 1)
            x0, y0 = pts[j]
            dr.ellipse(((x0 - 0.6) * K, (y0 - 0.6) * K, (x0 + 0.6) * K, (y0 + 0.6) * K), fill=lerp(base, (255, 255, 255), 0.5) + (255,))
    img.convert('RGB').save(f'{OUT}/f{f:04d}.png')

subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-framerate', str(FPS), '-i', f'{OUT}/f%04d.png',
                '-vf', 'scale=780:-2', '-pix_fmt', 'yuv420p', f'{OUT}/knit.mp4'], check=True)
print('frames', frames, 'mp4', f'{OUT}/knit.mp4')
