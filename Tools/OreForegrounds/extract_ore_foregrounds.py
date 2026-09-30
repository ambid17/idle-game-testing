"""Cut the baked-in dirt background out of an ore/power-up tile, leaving a transparent foreground.

Author new ore art on the real `1 dirt.png` pixels (or any close dirt approximation); any pixel
that still matches dirt, or falls in dirt's brown band, becomes transparent. ChunkTilemapView
draws the biome-tinted dirt tile underneath at runtime, and UI draws the dirt icon behind it
(Image.SetIcon / BlockType.IconBackground). Point the BlockType's Tile and Icon at the output and
tick DrawDirtBehind. The original opaque sources were deleted once extracted (in git before the
commit that added this note) - the outputs in Assets/Textures/Ores/Foreground are the source of truth.

Usage: python Tools/OreForegrounds/extract_ore_foregrounds.py [--keep-browns] [--preview out.png] src.png [...]
  --keep-browns  skip the brown-band pass (for art with intentional brown, e.g. the chest's wood)
Writes Assets/Textures/Ores/Foreground/<same name>.png per source.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

DIRT = 'Assets/Textures/Ores/1 dirt.png'
OUT_DIR = 'Assets/Textures/Ores/Foreground'
# Per-channel max difference from dirt: below LOW is background, above HIGH is fully opaque.
LOW, HIGH = 14, 30
MIN_ISLAND_PX = 5   # drop isolated specks (compression/regen noise), keep real 1px lines in larger shapes
MAX_HOLE_PX = 4     # fill pinholes inside shapes
# AI-generated "dirt" only approximates the real texture, leaving brown smudges that don't
# pixel-match - those pixels are dropped by colour too (unless --keep-browns).
# Real dirt sits at hue 9-21, sat 0.22-0.38, light 0.21-0.39; the AI smudges run a little lighter
# and warmer. Grey pebbles, dark outlines and saturated ore colours fall outside this band.
DIRT_HUE = (6, 30)
DIRT_SAT = (0.15, 0.52)
DIRT_LIGHT = (0.12, 0.58)


def hsl(rgb):
    r, g, b = [rgb[..., i] / 255 for i in range(3)]
    mx, mn = np.maximum(np.maximum(r, g), b), np.minimum(np.minimum(r, g), b)
    light = (mx + mn) / 2
    delta = mx - mn
    sat = np.where(delta == 0, 0, delta / (1 - np.abs(2 * light - 1) + 1e-6))
    hue = np.where(mx == r, ((g - b) / (delta + 1e-6)) % 6,
                   np.where(mx == g, (b - r) / (delta + 1e-6) + 2, (r - g) / (delta + 1e-6) + 4)) * 60
    return hue, sat, light


def is_dirt_brown(rgb):
    hue, sat, light = hsl(rgb)
    return (hue >= DIRT_HUE[0]) & (hue <= DIRT_HUE[1]) & (sat >= DIRT_SAT[0]) & (sat <= DIRT_SAT[1])         & (light >= DIRT_LIGHT[0]) & (light <= DIRT_LIGHT[1])


def extract(path, dirt, drop_browns):
    src = np.asarray(Image.open(path).convert('RGBA')).astype(np.float32)
    diff = np.abs(src[..., :3] - dirt).max(-1)
    if drop_browns:
        diff = np.where(is_dirt_brown(src[..., :3]), 0, diff)
    alpha = np.clip((diff - LOW) / (HIGH - LOW), 0, 1)

    solid = alpha > 0.5
    labels, n = ndimage.label(solid, structure=np.ones((3, 3)))
    sizes = ndimage.sum(solid, labels, range(1, n + 1))
    solid &= np.isin(labels, np.nonzero(sizes >= MIN_ISLAND_PX)[0] + 1)
    holes, n = ndimage.label(~solid)
    sizes = ndimage.sum(~solid, holes, range(1, n + 1))
    solid |= np.isin(holes, np.nonzero(sizes <= MAX_HOLE_PX)[0] + 1)

    # Keep soft edges only where they border a kept shape; everything else is fully in or out.
    near = ndimage.binary_dilation(solid, structure=np.ones((3, 3)))
    alpha = np.where(solid, 1.0, np.where(near, alpha, 0.0))
    out = src.copy()
    out[..., 3] = alpha * src[..., 3]
    return out.astype(np.uint8), float(alpha.mean())


def main():
    args = sys.argv[1:]
    keep_browns = '--keep-browns' in args
    if keep_browns:
        args.remove('--keep-browns')
    preview = None
    if '--preview' in args:
        i = args.index('--preview')
        preview = args[i + 1]
        del args[i:i + 2]
    if not args:
        sys.exit(__doc__)

    dirt = np.asarray(Image.open(DIRT).convert('RGB')).astype(np.float32)
    os.makedirs(OUT_DIR, exist_ok=True)
    results = []
    for path in args:
        out, coverage = extract(path, dirt, drop_browns=not keep_browns)
        name = os.path.basename(path)
        Image.fromarray(out).save(os.path.join(OUT_DIR, name))
        results.append((path, out))
        print(f'{name:32} coverage {coverage:.2f}')

    if preview:
        # Preview: original | foreground on magenta | foreground on a slate-tinted dirt.
        tinted = (dirt * np.array([0.5, 0.75, 1.0])).astype(np.uint8)
        cols = 3
        sheet = Image.new('RGB', (cols * 3 * 132, ((len(results) + cols - 1) // cols) * 132), (20, 20, 20))
        for i, (path, fg) in enumerate(results):
            x, y = (i % cols) * 3 * 132, (i // cols) * 132
            fg_img = Image.fromarray(fg)
            sheet.paste(Image.open(path).convert('RGB'), (x, y))
            magenta = Image.new('RGBA', fg_img.size, (255, 0, 255, 255))
            sheet.paste(Image.alpha_composite(magenta, fg_img).convert('RGB'), (x + 132, y))
            base = Image.fromarray(tinted).convert('RGBA')
            sheet.paste(Image.alpha_composite(base, fg_img).convert('RGB'), (x + 264, y))
        sheet.save(preview)

if __name__ == '__main__':
    main()
