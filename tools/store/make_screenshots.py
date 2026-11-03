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
import argparse
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[2]
FONT = ROOT / "Assets/Resources/Fonts/Main.ttf"
W, H = 1080, 1920
THEMES = {
    "tactile": ((255, 249, 237), (255, 207, 154), (112, 43, 31), (238, 87, 72)),
    "transformation": ((22, 48, 99), (43, 102, 192), (255, 255, 255), (255, 194, 60)),
    "collection": ((255, 250, 236), (184, 224, 204), (20, 77, 59), (23, 148, 95)),
}


def background(theme):
    top, bottom, _, _ = THEMES[theme]
    img = Image.new("RGB", (W, H))
    px = img.load()
    for y in range(H):
        t = y / H
        c = tuple(int(top[i] * (1 - t) + bottom[i] * t) for i in range(3))
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


def compose(shot_path, caption, theme="tactile"):
    img = background(theme)
    draw = ImageDraw.Draw(img)
    _, _, ink, accent = THEMES[theme]
    brand = ImageFont.truetype(str(FONT), 28)
    draw.text((W / 2, 58), "ВЯЗАНАЯ КАРТИНКА", font=brand, fill=ink, anchor="ma")
    font_size = 72
    while True:
        font = ImageFont.truetype(str(FONT), font_size)
        lines = wrap(draw, caption, font, W - 140)
        if len(lines) <= 3 or font_size <= 42:
            break
        font_size -= 2
    y = 126
    for line in lines:
        draw.text((W / 2, y), line, font=font, fill=ink, anchor="ma")
        y += font_size + 16
    draw.rounded_rectangle((W // 2 - 50, y + 12, W // 2 + 50, y + 20), 4, fill=accent)

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
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("raw", type=Path)
    parser.add_argument("captions", type=Path)
    parser.add_argument("out", type=Path)
    parser.add_argument("--theme", choices=[*THEMES, "all"], default="tactile")
    args = parser.parse_args()
    raw, captions_file, out = args.raw, args.captions, args.out
    shots = sorted([p for p in raw.iterdir() if p.suffix.lower() in (".png", ".jpg", ".jpeg")],
                   key=lambda p: [int(s) if s.isdigit() else s for s in re.split(r"(\d+)", p.name)])
    if not shots:
        sys.exit("Нет исходных снимков игры: сначала сохрани реальный Game View в папку raw.")
    captions = [c.strip() for c in captions_file.read_text(encoding="utf-8").splitlines() if c.strip()]
    if len(captions) < len(shots):
        sys.exit(f"Подписей {len(captions)}, а скриншотов {len(shots)}: нужно по подписи на каждый.")
    out.mkdir(parents=True, exist_ok=True)
    themes = list(THEMES) if args.theme == "all" else [args.theme]
    for theme in themes:
        target = out / theme if args.theme == "all" else out
        target.mkdir(parents=True, exist_ok=True)
        for i, (shot, caption) in enumerate(zip(shots, captions), 1):
            compose(shot, caption, theme).save(target / f"screenshot-{i}.png")
            print(f"{theme}/screenshot-{i}.png  ←  {shot.name}: {caption}")


if __name__ == "__main__":
    main()
