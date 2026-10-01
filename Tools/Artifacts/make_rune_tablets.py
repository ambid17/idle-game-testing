"""Builds the 20 rune-tablet artifact variants from the UI currency icon.

The source is Assets/Textures/Currency/artifactIcon_RuneTablet.png (64px). Its own rune is inpainted
out of the slate, and each variant gets one Elder Futhark glyph (rendered from Windows' Segoe UI
Historic) drawn in the icon's magenta glow style. Outputs, all into Assets/Textures/Artifacts:
  RuneTablet_XX.png  - 128px, the 64px tablet upscaled 2x (nearest) - tile foreground + UI icon
  RuneDebrisSheet.png - 20 small tablets stacked vertically (row = rune index) for dig debris

Usage: python Tools/Artifacts/make_rune_tablets.py [preview_path]
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Textures", "Currency", "artifactIcon_RuneTablet.png")
OUT_DIR = os.path.join(ROOT, "Assets", "Textures", "Artifacts")
FONT = r"C:\Windows\Fonts\seguihis.ttf"

# Order = rune index (RuneDefinition.Index). Append-only, same as the asset list.
GLYPHS = "ᚠᚢᚦᚨᚱᚷᚹᚺᚾᛇᛈᛉᛊᛏᛒᛖᛗᛚᛞᛟ"

# Slate area the glyph is centred in (64px source coords) and the box it's fitted to.
GLYPH_CENTER = (30.5, 29.0)
GLYPH_BOX = (15, 25)
DEBRIS_SIZE = 32

CORE_LIGHT = np.array([255, 150, 255])
CORE_DARK = np.array([200, 100, 220])
HALO = np.array([120, 58, 150])


def blank_tablet(src):
    rgb = src[..., :3].astype(float)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # Anything noticeably brighter/more saturated than the slate inside the panel is rune (or its halo).
    glyph = (r - g > 40) & (b - g > 50) & (r > 95) & (src[..., 3] > 0)
    panel = np.zeros(glyph.shape, bool)
    panel[12:47, 21:41] = True
    glyph &= panel
    glyph = ndimage.binary_dilation(glyph, iterations=1) & panel

    # Diffusion inpaint: repeatedly average known neighbours into the hole.
    filled = rgb.copy()
    known = ~glyph
    hole = glyph.copy()
    for _ in range(200):
        if not hole.any():
            break
        acc = np.zeros_like(filled)
        cnt = np.zeros(glyph.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
            k = np.roll(np.roll(known, dy, 0), dx, 1)
            v = np.roll(np.roll(filled, dy, 0), dx, 1)
            acc += v * k[..., None]
            cnt += k
        grow = hole & (cnt > 0)
        filled[grow] = acc[grow] / cnt[grow][:, None]
        known = known | grow
        hole = hole & ~grow

    # A touch of per-pixel noise so the patch doesn't read as a flat smear.
    rng = np.random.default_rng(7)
    noise = rng.normal(0, 3, filled.shape)
    filled[glyph] += noise[glyph]

    out = src.copy()
    out[..., :3] = np.clip(filled, 0, 255).astype(np.uint8)
    return out


def glyph_mask(ch):
    canvas = Image.new("L", (200, 200), 0)
    font = ImageFont.truetype(FONT, 120)
    ImageDraw.Draw(canvas).text((40, 10), ch, font=font, fill=255)
    bbox = canvas.getbbox()
    glyph = canvas.crop(bbox)
    gw, gh = glyph.size
    scale = min(GLYPH_BOX[0] / gw, GLYPH_BOX[1] / gh)
    w, h = max(1, round(gw * scale)), max(1, round(gh * scale))
    small = np.array(glyph.resize((w, h), Image.LANCZOS)) > 70
    # Strokes thinner than 1px vanish when downsampled; keep at least the skeleton.
    if small.sum() < 10:
        small = np.array(glyph.resize((w, h), Image.LANCZOS)) > 30

    mask = np.zeros((64, 64), bool)
    x0 = int(round(GLYPH_CENTER[0] - w / 2))
    y0 = int(round(GLYPH_CENTER[1] - h / 2))
    mask[y0:y0 + h, x0:x0 + w] = small
    return mask


def draw_rune(blank, mask, seed):
    out = blank.copy().astype(float)
    rng = np.random.default_rng(seed)
    halo = ndimage.binary_dilation(mask, iterations=1) & ~mask
    out[halo, :3] = out[halo, :3] * 0.35 + HALO * 0.65
    ys, xs = np.nonzero(mask)
    for y, x in zip(ys, xs):
        t = rng.uniform(0, 1)
        out[y, x, :3] = CORE_DARK * (1 - t) + CORE_LIGHT * t
    return np.clip(out, 0, 255).astype(np.uint8)


def to_debris(tablet64):
    im = Image.fromarray(tablet64)
    bbox = im.getchannel("A").getbbox()
    crop = im.crop(bbox)
    w, h = crop.size
    scale = (DEBRIS_SIZE - 2) / max(w, h)
    small = crop.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.BOX)
    arr = np.array(small)
    arr[..., 3] = np.where(arr[..., 3] > 110, 255, 0)
    frame = Image.new("RGBA", (DEBRIS_SIZE, DEBRIS_SIZE), (0, 0, 0, 0))
    frame.paste(Image.fromarray(arr), ((DEBRIS_SIZE - arr.shape[1]) // 2, (DEBRIS_SIZE - arr.shape[0]) // 2))
    return frame


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    src = np.array(Image.open(SOURCE).convert("RGBA"))
    blank = blank_tablet(src)

    tablets = []
    for i, ch in enumerate(GLYPHS):
        tablet = draw_rune(blank, glyph_mask(ch), seed=100 + i)
        tablets.append(tablet)
        Image.fromarray(tablet).resize((128, 128), Image.NEAREST).save(os.path.join(OUT_DIR, f"RuneTablet_{i + 1:02d}.png"))

    sheet = Image.new("RGBA", (DEBRIS_SIZE, DEBRIS_SIZE * len(tablets)), (0, 0, 0, 0))
    # Particle texture sheets count rows from the top.
    for i, tablet in enumerate(tablets):
        sheet.paste(to_debris(tablet), (0, i * DEBRIS_SIZE))
    sheet.save(os.path.join(OUT_DIR, "RuneDebrisSheet.png"))

    if len(sys.argv) > 1:
        preview = Image.new("RGBA", (5 * 136, 4 * 136), (40, 30, 20, 255))
        for i, tablet in enumerate(tablets):
            big = Image.fromarray(tablet).resize((128, 128), Image.NEAREST)
            preview.alpha_composite(big, ((i % 5) * 136 + 4, (i // 5) * 136 + 4))
        preview.save(sys.argv[1])
        Image.fromarray(blank).resize((256, 256), Image.NEAREST).save(sys.argv[1].replace(".png", "_blank.png"))
        sheet.resize((DEBRIS_SIZE * 4, DEBRIS_SIZE * 4 * len(tablets)), Image.NEAREST).crop((0, 0, 128, 128 * 5)).save(sys.argv[1].replace(".png", "_debris.png"))


if __name__ == "__main__":
    main()
