"""Cuts the explosion particle sprites out of the AI-generated sheet.

Usage (from repo root): python Tools/Effects/make_explosion_fx.py
Reads Tools/Effects/explosion_fx_raw.png (cloud puffs + spiky blast flashes on magenta, drawn
with Tools/Effects/reveal_fx_raw.png as the style reference) and writes two vertical strips to
Assets/Textures/Effects, one sprite per row, for Effects.WorldEffects:
  ExplosionFirePuffs.png - 4 fireballs, 128x128 cells
  ExplosionFlash.png     - 2 blast flashes, 128x128 cells
The model drew the puffs pale cream with only an orange rim, so they are gradient-mapped by
brightness onto a flat fire ramp (pale-yellow highlight, yellow body, orange, red rim); the navy
outline is kept. Needs numpy, scipy, Pillow.
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

from make_reveal_fx import find_blobs, cut, strip

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "explosion_fx_raw.png")
OUT = os.path.join(HERE, "..", "..", "Assets", "Textures", "Effects")

CELL = 128
PUFF_COUNT = 4
FLASH_COUNT = 2

# (minimum brightness, colour) - brightest first; anything darker than the last band is outline.
FIRE_RAMP = [
    (222, (255, 246, 176)),
    (185, (255, 201, 60)),
    (135, (255, 138, 30)),
    (75, (214, 58, 28)),
]


def fire_map(sprite):
    rgba = np.array(sprite)
    brightness = rgba[..., :3].astype(float) @ [0.299, 0.587, 0.114]
    out = rgba.copy()
    remaining = np.ones(brightness.shape, bool)
    for threshold, colour in FIRE_RAMP:
        band = remaining & (brightness >= threshold)
        out[band, :3] = colour
        remaining &= ~band
    return Image.fromarray(out, "RGBA")


def despill(sprite):
    """Edge pixels blended with the magenta background (strong red + blue, little green) are
    dropped; the fire's reds carry almost no blue, so they survive."""
    rgba = np.array(sprite).astype(int)
    r, g, b = rgba[..., 0], rgba[..., 1], rgba[..., 2]
    rgba[(r > 120) & (b > 90) & (g < r - 60) & (b > g + 20), 3] = 0
    return Image.fromarray(rgba.astype(np.uint8), "RGBA")


def main():
    rgb = np.array(Image.open(RAW).convert("RGB"))
    blobs = find_blobs(rgb)

    # Flashes are spiky stars (sparse in their box) with a pure white core; puffs fill ~75%.
    flashes = [b for b in blobs if b[1].mean() < 0.55]
    puffs = [b for b in blobs if b[1].mean() >= 0.55]
    # The pale beige puff in the top-left corner has no orange rim: it's a dust cloud, skip it.
    puffs = [b for b in puffs if (rgb[b[0]][b[1]] @ [1, -1, 0] > 100).mean() > 0.05]
    print(f"found {len(puffs)} fire puffs, {len(flashes)} flashes")
    if len(puffs) < PUFF_COUNT or len(flashes) < FLASH_COUNT:
        raise SystemExit("sheet does not contain enough sprites")

    puffs = sorted(puffs, key=lambda b: -b[2])[:PUFF_COUNT]
    flashes = sorted(flashes, key=lambda b: -b[2])[:FLASH_COUNT]

    strip([fire_map(despill(cut(rgb, box, mask, CELL))) for box, mask, _ in puffs], CELL).save(os.path.join(OUT, "ExplosionFirePuffs.png"))
    strip([despill(cut(rgb, box, mask, CELL)) for box, mask, _ in flashes], CELL).save(os.path.join(OUT, "ExplosionFlash.png"))


if __name__ == "__main__":
    main()
