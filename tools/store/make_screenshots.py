#!/usr/bin/env python3
"""
Скриншоты для RuStore и Google Play из обычных скриншотов с телефона.

Как пользоваться:
  1. Сними скриншоты игры на телефоне (без записи экрана и всплывающих окон поверх) и положи их
     в папку, например docs/store/raw/, с именами 1.png, 2.png, ... в нужном порядке.
  2. Подписи — в docs/store/captions.txt, по одной строке на скриншот (порядок тот же).
  3. python3 tools/store/make_screenshots.py docs/store/raw docs/store/captions.txt docs/store/screenshots

Результат: PNG 1080×1920 (9:16) — подходит и Google Play (2–8 штук), и RuStore.
Нужен Python 3 с Pillow: pip install pillow
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONT = ROOT / "Assets/Resources/Fonts/Main.ttf"
W, H = 1080, 1920
TOP = (255, 226, 238)
BOTTOM = (205, 188, 255)
INK = (43, 35, 80)


def background():
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        t = y / H
        c = tuple(int(TOP[i] * (1 - t) + BOTTOM[i] * t) for i in range(3))
        for x in range(W):
            px[x, y] = c
    return img.convert("RGBA")


def wrap(draw, text, font, width):
    words, lines, line = text.split(), [], ""
    for word in words:
        probe = (line + " " + word).strip()
        if draw.textlength(probe, font=font) <= width:
            line = probe
        else:
            lines.append(line)
            line = word
    lines.append(line)
    return lines


def compose(shot_path, caption):
    img = background()
    draw = ImageDraw.Draw(img)
    font = ImageFont.truetype(str(FONT), 72)
    y = 110
    for line in wrap(draw, caption, font, W - 140):
        draw.text((W / 2, y), line, font=font, fill=INK, anchor="ma")
        y += 88

    shot = Image.open(shot_path).convert("RGBA")
    top = y + 50
    max_w, max_h = W - 160, H - top - 60
    scale = min(max_w / shot.width, max_h / shot.height)
    shot = shot.resize((int(shot.width * scale), int(shot.height * scale)), Image.LANCZOS)
    x = (W - shot.width) // 2
    radius = 48

    shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((x, top + 18, x + shot.width, top + 18 + shot.height), radius, fill=(80, 50, 140, 90))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(28)))
    frame = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle((x - 14, top - 14, x + shot.width + 14, top + shot.height + 14), radius + 12, fill=(255, 255, 255, 255))
    img.alpha_composite(frame)
    mask = Image.new("L", shot.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, shot.width - 1, shot.height - 1), radius, fill=255)
    img.paste(shot, (x, top), mask)
    return img.convert("RGB")


def main():
    if len(sys.argv) != 4:
        print(__doc__)
        sys.exit(1)
    raw, captions_file, out = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
    shots = sorted([p for p in raw.iterdir() if p.suffix.lower() in (".png", ".jpg", ".jpeg")], key=lambda p: p.stem)
    captions = [c.strip() for c in captions_file.read_text(encoding="utf-8").splitlines() if c.strip()]
    if len(captions) < len(shots):
        sys.exit(f"Подписей {len(captions)}, а скриншотов {len(shots)}: нужно по подписи на каждый.")
    out.mkdir(parents=True, exist_ok=True)
    for i, (shot, caption) in enumerate(zip(shots, captions), 1):
        compose(shot, caption).save(out / f"screenshot-{i}.png")
        print(f"screenshot-{i}.png  ←  {shot.name}: {caption}")


if __name__ == "__main__":
    main()
