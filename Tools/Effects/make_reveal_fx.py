"""Cuts the building-reveal particle sprites out of the AI-generated sheet.

Usage (from repo root): python Tools/Effects/make_reveal_fx.py
Reads Tools/Effects/reveal_fx_raw.png (dust puffs + sparkles on a magenta-ish background with
grid lines) and writes two vertical strips to Assets/Textures/Effects, one sprite per row, for
ParticleSystem texture-sheet animation (Buildings.BuildingRevealCinematicPlayer):
  RevealDustPuffs.png  - 4 puffs, 128x128 cells
  RevealSparkles.png   - 2 sparkles, 64x64 cells
Needs numpy, scipy, Pillow.
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, "reveal_fx_raw.png")
OUT = os.path.join(HERE, "..", "..", "Assets", "Textures", "Effects")

PUFF_CELL = 128
SPARKLE_CELL = 64
PUFF_COUNT = 4
SPARKLE_COUNT = 2
KEY_TOLERANCE = 60  # RGB distance from the background colour that still counts as background


def find_blobs(rgb):
    background = rgb[4, 4].astype(int)
    distance = np.sqrt(((rgb.astype(int) - background) ** 2).sum(axis=2))
    foreground = distance > KEY_TOLERANCE
    labels, count = ndimage.label(foreground)
    blobs = []
    for index, box in enumerate(ndimage.find_objects(labels), start=1):
        mask = labels[box] == index
        height, width = mask.shape
        # The grid lines are one sprawling, nearly empty component; specks are noise.
        if mask.sum() < 400 or mask.mean() < 0.2:
            continue
        # Holes fully enclosed by the outline belong to the sprite (pale highlights can sit
        # close to nothing, but never to the background colour - this is just a safety net).
        mask = ndimage.binary_fill_holes(mask)
        blobs.append((box, mask, width * height))
    return blobs


def cut(rgb, box, mask, cell):
    rgba = np.dstack([rgb[box], (mask * 255).astype(np.uint8)])
    sprite = Image.fromarray(rgba, "RGBA")
    scale = (cell - 4) / max(sprite.size)
    size = (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale)))
    sprite = sprite.resize(size, Image.NEAREST)
    canvas = Image.new("RGBA", (cell, cell), (0, 0, 0, 0))
    canvas.paste(sprite, ((cell - size[0]) // 2, (cell - size[1]) // 2))
    return canvas


def strip(sprites, cell):
    sheet = Image.new("RGBA", (cell, cell * len(sprites)), (0, 0, 0, 0))
    for row, sprite in enumerate(sprites):
        sheet.paste(sprite, (0, row * cell))
    return sheet


def main():
    rgb = np.array(Image.open(RAW).convert("RGB"))
    blobs = find_blobs(rgb)

    # Sparkles are thin crosses: they fill far less of their bounding box than a puff does.
    sparkles = [b for b in blobs if b[1].mean() < 0.45]
    puffs = [b for b in blobs if b[1].mean() >= 0.45]
    print(f"found {len(puffs)} puffs, {len(sparkles)} sparkles")
    if len(puffs) < PUFF_COUNT or len(sparkles) < SPARKLE_COUNT:
        raise SystemExit("sheet does not contain enough sprites")

    # Largest first, so the strip always holds the same sprites in the same rows.
    puffs = sorted(puffs, key=lambda b: -b[2])[:PUFF_COUNT]
    sparkles = sorted(sparkles, key=lambda b: -b[2])[:SPARKLE_COUNT]

    strip([cut(rgb, box, mask, PUFF_CELL) for box, mask, _ in puffs], PUFF_CELL).save(os.path.join(OUT, "RevealDustPuffs.png"))
    strip([cut(rgb, box, mask, SPARKLE_CELL) for box, mask, _ in sparkles], SPARKLE_CELL).save(os.path.join(OUT, "RevealSparkles.png"))


if __name__ == "__main__":
    main()
