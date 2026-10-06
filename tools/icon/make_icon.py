"""
Иконка игры «Вязаная картинка»: клубок со спицами и ниткой-сердечком на розово-лавандовом фоне.

    pip install pillow numpy
    python3 tools/icon/make_icon.py

Пишет в Assets/Icons/:
    AppIcon.png            — 1024×1024, полная иконка (iOS, Google Play, старые Android)
    AppIconForeground.png  — 1024×1024 с прозрачностью, передний слой адаптивной иконки Android
    AppIconBackground.png  — 1024×1024, задний слой адаптивной иконки Android
Рисуется в 2048 и уменьшается вдвое — края гладкие.
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Icons")
S = 2048  # сторона холста при рисовании


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


Y, X = np.mgrid[0:S, 0:S].astype(np.float32) / S  # координаты 0..1, y вниз
PX = 1.0 / S


def cover(d, soft=1.5):
    """Покрытие по расстоянию до края фигуры (d < 0 — внутри), сглаживание в пару пикселей."""
    return np.clip(0.5 - d / (soft * PX), 0.0, 1.0)


def blend(img, color, alpha):
    a = alpha[..., None]
    img[..., :3] = img[..., :3] * (1 - a) + color * a
    img[..., 3] = img[..., 3] * (1 - alpha) + alpha


def seg_dist(ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = np.clip(((X - ax) * dx + (Y - ay) * dy) / max(dx * dx + dy * dy, 1e-12), 0, 1)
    return np.hypot(X - (ax + t * dx), Y - (ay + t * dy))


def polyline_dist(points):
    d = np.full(X.shape, 10.0, dtype=np.float32)
    for (ax, ay), (bx, by) in zip(points[:-1], points[1:]):
        d = np.minimum(d, seg_dist(ax, ay, bx, by))
    return d


def background(img):
    top, bottom = hex_rgb("#FFC2DC"), hex_rgb("#B59CFF")
    t = (Y * 0.85 + X * 0.15)[..., None]
    img[..., :3] = top * (1 - t) + bottom * t
    img[..., 3] = 1.0
    # Мягкое свечение за клубком.
    glow = np.clip(1 - np.hypot(X - 0.47, Y - 0.5) / 0.55, 0, 1) ** 2
    blend(img, hex_rgb("#FFFFFF"), glow * 0.35)
    # Едва заметная вязаная фактура: ряды «галочек».
    cell = 1 / 14
    cx = (X % cell) / cell
    cy = (Y % cell) / cell
    v = np.minimum(np.abs((cx - 0.5) * 1.2 - (cy - 0.85) * 0.75), np.abs((0.5 - cx) * 1.2 - (cy - 0.85) * 0.75))
    stroke = np.clip(1 - v / 0.07, 0, 1) * (cy > 0.2) * (cy < 0.9)
    blend(img, hex_rgb("#FFFFFF"), stroke * 0.10)


def needle(img, ax, ay, bx, by, width=0.034):
    d = seg_dist(ax, ay, bx, by) - width / 2
    blend(img, hex_rgb("#B9783F"), cover(d))
    blend(img, hex_rgb("#F2C48D"), cover(d + 0.006))
    # Блик вдоль спицы.
    nx, ny = -(by - ay), (bx - ax)
    n = np.hypot(nx, ny)
    off = 0.007
    d2 = seg_dist(ax + nx / n * off, ay + ny / n * off, bx + nx / n * off, by + ny / n * off) - 0.004
    blend(img, hex_rgb("#FFE3BF"), cover(d2) * 0.9)
    # Шарик на конце.
    knob = np.hypot(X - bx, Y - by) - 0.042
    blend(img, hex_rgb("#D98E12"), cover(knob))
    blend(img, hex_rgb("#FFC23C"), cover(knob + 0.007))
    blend(img, hex_rgb("#FFE7A0"), cover(np.hypot(X - bx + 0.012, Y - by + 0.012) - 0.013))


def yarn_ball(img, cx, cy, r):
    inside = np.hypot(X - cx, Y - cy) - r
    # Тень под клубком.
    shadow = np.clip(1 - np.hypot((X - cx) / 1.0, (Y - (cy + r * 0.95)) / 0.28) / (r * 0.9), 0, 1) ** 1.6
    blend(img, hex_rgb("#5A3E9E"), shadow * 0.22)
    mask = cover(inside)
    # Объём: светлее сверху слева, темнее снизу справа.
    u = np.clip(((X - cx) * 0.55 + (Y - cy) * 0.8) / r * 0.5 + 0.5, 0, 1)[..., None]
    light, dark = hex_rgb("#FF8DB8"), hex_rgb("#D0346F")
    body = light * (1 - u) + dark * u
    a = mask[..., None]
    img[..., :3] = img[..., :3] * (1 - a) + body * a
    img[..., 3] = np.maximum(img[..., 3], mask)

    def strands(ox, oy, radii, width, color, shade, alpha):
        for rr in radii:
            d = np.abs(np.hypot(X - ox, Y - oy) - rr) - width / 2
            sd = np.abs(np.hypot(X - ox, Y - oy) - (rr + width * 0.75)) - width / 3
            blend(img, shade, cover(sd) * mask * alpha * 0.8)
            blend(img, color, cover(d) * mask * alpha)

    # Нижние витки — по одной дуге, верхние — поперёк: так клубок выглядит смотанным.
    strands(cx - r * 1.9, cy + r * 1.6, [r * k for k in (2.05, 2.28, 2.51, 2.74)], r * 0.07, hex_rgb("#FF9FC4"), hex_rgb("#B82A63"), 0.75)
    strands(cx + r * 0.2, cy + r * 2.6, [r * k for k in (2.0, 2.2, 2.4, 2.6, 2.8)], r * 0.075, hex_rgb("#FFC4DA"), hex_rgb("#B82A63"), 1.0)
    # Блик и обод.
    hl = np.clip(1 - np.hypot((X - (cx - r * 0.38)) / 1.3, Y - (cy - r * 0.45)) / (r * 0.32), 0, 1) ** 1.5
    blend(img, hex_rgb("#FFFFFF"), hl * mask * 0.5)
    rim = np.abs(inside + r * 0.012) - r * 0.018
    blend(img, hex_rgb("#A82258"), cover(rim) * 0.85)


def heart_thread(img, start, center, size, width=0.024):
    # Нитка от клубка плавной дугой входит в ямочку сердечка сверху и обводит его — без пересечений.
    sx, sy = start
    hx, hy = center
    t = np.linspace(0, 1, 40)
    end = (hx, hy - size * 5 / 16)
    c1 = (sx + 0.05, sy - 0.01)
    c2 = (hx - size * 0.15, hy - size * 1.2)
    bez = [((1 - k) ** 3 * sx + 3 * (1 - k) ** 2 * k * c1[0] + 3 * (1 - k) * k ** 2 * c2[0] + k ** 3 * end[0],
            (1 - k) ** 3 * sy + 3 * (1 - k) ** 2 * k * c1[1] + 3 * (1 - k) * k ** 2 * c2[1] + k ** 3 * end[1]) for k in t]
    tt = np.linspace(np.pi * 2, np.pi * 4, 140)
    heart = [(hx + size * (16 * np.sin(a) ** 3) / 16, hy - size * (13 * np.cos(a) - 5 * np.cos(2 * a) - 2 * np.cos(3 * a) - np.cos(4 * a)) / 16)
             for a in tt]
    pts = bez + heart
    d = polyline_dist(pts)
    blend(img, hex_rgb("#A82258"), cover(d - width / 2))
    blend(img, hex_rgb("#FF5C93"), cover(d - width / 2 + 0.005))
    blend(img, hex_rgb("#FFB3CF"), cover(d - width * 0.12) * 0.9)


def foreground(img, scale=1.0, ox=0.0, oy=0.0):
    """Клубок, спицы и нитка. scale/ox/oy — чтобы уместить в безопасную зону адаптивной иконки."""
    def p(x, y):
        return (0.5 + (x - 0.5) * scale + ox, 0.5 + (y - 0.5) * scale + oy)

    cx, cy = p(0.45, 0.50)
    r = 0.30 * scale
    a1, b1 = p(0.40, 0.48), p(0.80, 0.11)
    a2, b2 = p(0.47, 0.52), p(0.90, 0.24)
    needle(img, *a1, *b1, width=0.034 * scale)
    needle(img, *a2, *b2, width=0.034 * scale)
    yarn_ball(img, cx, cy, r)
    heart_thread(img, p(0.68, 0.71), p(0.81, 0.81), 0.10 * scale, width=0.030 * scale)


def save(img, name):
    rgba = (np.clip(img, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").resize((1024, 1024), Image.LANCZOS).save(os.path.join(OUT, name))
    print("записано:", os.path.join("Assets", "Icons", name))


def main():
    os.makedirs(OUT, exist_ok=True)

    full = np.zeros((S, S, 4), dtype=np.float32)
    background(full)
    foreground(full)
    save(full, "AppIcon.png")

    back = np.zeros((S, S, 4), dtype=np.float32)
    background(back)
    save(back, "AppIconBackground.png")

    # Передний слой адаптивной иконки: всё важное — в центральных 66% (безопасная зона Android).
    front = np.zeros((S, S, 4), dtype=np.float32)
    # Масштаб и сдвиг подобраны так, что шарики спиц и сердечко целиком внутри круга радиусом 0.3.
    foreground(front, scale=0.55, ox=-0.03, oy=0.003)
    save(front, "AppIconForeground.png")


if __name__ == "__main__":
    main()
