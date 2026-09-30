"""Turn an AI-generated 2x2 ore sheet on flat magenta (#FF00FF) into four transparent 128px ore
foregrounds for Assets/Textures/Ores/Foreground (see ChunkTilemapView / BlockType.DrawDirtBehind).

The image generator can't emit real alpha, so ores are drawn on a chroma-key background and keyed
out here (keyed on the sampled background colour - the model's "magenta" drifts pinkish): alpha
ramps from 0 near the background to 1 away from it, magenta spill is removed from edge
pixels, and each quadrant (inset past the gutter) is downscaled to the tile size.

Usage: python Tools/OreForegrounds/split_ore_sheet.py sheet.png "4 coal" "7 emerald" "10 titaniumAlloy" "13 naniteOre"
  Names are row-major (top-left, top-right, bottom-left, bottom-right); "-" skips a quadrant.
  --out DIR     output folder (default Assets/Textures/Ores/Foreground)
  --preview P   also save a check sheet: result on magenta | on dirt | on tinted dirt
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

TILE = 128
DIRT = 'Assets/Textures/Ores/1 dirt.png'
# Max per-channel distance from the key: below LOW is background, above HIGH is fully opaque.
LOW, HIGH = 40, 110
# Fraction of each quadrant trimmed on every side, to drop the gutter and any bleed from neighbours.
INSET = 0.03
MIN_ISLAND_PX = 3


def key_out(rgb):
    # Background = the quadrant's most common colour (coarsely binned). Sampling only the border
    # fails when ore touches the edges.
    flat = rgb.reshape(-1, 3)
    bins = (flat // 16).astype(np.int32)
    codes = bins[:, 0] * 256 + bins[:, 1] * 16 + bins[:, 2]
    key = flat[codes == np.bincount(codes).argmax()].mean(axis=0)
    dist = np.abs(rgb - key).max(-1)
    alpha = np.clip((dist - LOW) / (HIGH - LOW), 0, 1)
    # Magenta spill: pull green up toward the red/blue average on semi-keyed edge pixels, so a pink
    # fringe doesn't survive the downscale.
    spill = np.clip(np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1], 0, None)
    edge = (alpha < 1)[..., None]
    rgb = np.where(edge, rgb - np.stack([spill, np.zeros_like(spill), spill], -1) * (1 - alpha[..., None]), rgb)
    return np.clip(rgb, 0, 255), alpha


def quadrant(sheet, index):
    h, w = sheet.shape[:2]
    qw, qh = w // 2, h // 2
    x0, y0 = (index % 2) * qw, (index // 2) * qh
    ix, iy = int(qw * INSET), int(qh * INSET)
    return sheet[y0 + iy:y0 + qh - iy, x0 + ix:x0 + qw - ix]


def to_tile(rgb, alpha):
    # Premultiply so transparent magenta can't bleed into edge colours while resampling.
    premul = np.dstack([rgb * alpha[..., None], alpha * 255]).astype(np.uint8)
    small = np.asarray(Image.fromarray(premul, 'RGBA').resize((TILE, TILE), Image.LANCZOS)).astype(np.float32)
    a = small[..., 3] / 255
    rgb = np.where(a[..., None] > 0, small[..., :3] / np.maximum(a[..., None], 1e-6), 0)
    # Crisp pixel-art edges: snap alpha, then drop stray specks left by the resample.
    solid = a > 0.5
    labels, n = ndimage.label(solid, structure=np.ones((3, 3)))
    sizes = ndimage.sum(solid, labels, range(1, n + 1))
    solid &= np.isin(labels, np.nonzero(sizes >= MIN_ISLAND_PX)[0] + 1)
    out = np.dstack([np.clip(rgb, 0, 255), solid * 255.0])
    return out.astype(np.uint8)


def main():
    args = sys.argv[1:]
    out_dir = 'Assets/Textures/Ores/Foreground'
    preview = None
    for flag in ('--out', '--preview'):
        if flag in args:
            i = args.index(flag)
            if flag == '--out':
                out_dir = args[i + 1]
            else:
                preview = args[i + 1]
            del args[i:i + 2]
    if len(args) != 5:
        sys.exit(__doc__)

    sheet = np.asarray(Image.open(args[0]).convert('RGB')).astype(np.float32)
    tiles = []
    for index, name in enumerate(args[1:]):
        if name == '-':
            continue
        rgb, alpha = key_out(quadrant(sheet, index))
        tile = to_tile(rgb, alpha)
        os.makedirs(out_dir, exist_ok=True)
        Image.fromarray(tile).save(os.path.join(out_dir, f'{name}.png'))
        tiles.append(tile)
        print(f'{name:24} coverage {(tile[..., 3] > 0).mean():.2f}')

    if preview:
        dirt = Image.open(DIRT).convert('RGBA')
        tinted = Image.fromarray((np.asarray(dirt.convert('RGB')) * np.array([0.5, 0.75, 1.0])).astype(np.uint8)).convert('RGBA')
        magenta = Image.new('RGBA', (TILE, TILE), (255, 0, 255, 255))
        img = Image.new('RGB', (3 * (TILE + 4), len(tiles) * (TILE + 4)), (20, 20, 20))
        for row, tile in enumerate(tiles):
            fg = Image.fromarray(tile)
            for col, base in enumerate((magenta, dirt, tinted)):
                img.paste(Image.alpha_composite(base, fg).convert('RGB'), (col * (TILE + 4), row * (TILE + 4)))
        img.resize((img.width * 2, img.height * 2), Image.NEAREST).save(preview)


if __name__ == '__main__':
    main()
