"""Re-author a tile at the buildings' texel density: snap it to a GRID x GRID art-pixel grid with a
reduced palette and hard alpha, then store it nearest-upscaled x SCALE (96 x 4 = 384px, 384 PPU).

Each art pixel becomes a clean SCALE x SCALE texel block, like Assets/Textures/Buildings, instead of
the single-texel noise the 128px tiles carry.

Usage: python Tools/Tiles/pixelize.py SRC.png OUT.png [--colors N] [--tile] [--grid 96] [--scale 4]
  --colors N   palette size after quantising (default 16)
  --tile       source tiles edge-to-edge: resample with wrap-around so the result stays seamless
"""
import sys

import numpy as np
from PIL import Image

MIN_ALPHA = 0.5


def resample(rgba, grid, wrap):
    """Area-average down to the art grid, premultiplied so transparent texels can't tint edges."""
    a = rgba[..., 3:4] / 255.0
    premul = np.concatenate([rgba[..., :3] * a, a * 255.0], -1)
    h, w = premul.shape[:2]
    if wrap:
        premul = np.pad(premul, ((h, h), (w, w), (0, 0)), mode='wrap')
    bands = []
    for c in range(4):
        band = Image.fromarray(premul[..., c].astype(np.float32), 'F')
        if wrap:
            band = band.resize((grid * 3, grid * 3), Image.BOX).crop((grid, grid, grid * 2, grid * 2))
        else:
            band = band.resize((grid, grid), Image.BOX)
        bands.append(np.asarray(band))
    small = np.dstack(bands)
    alpha = small[..., 3] / 255.0
    rgb = np.where(alpha[..., None] > 1e-3, small[..., :3] / np.maximum(alpha[..., None], 1e-3), 0)
    return np.clip(rgb, 0, 255), alpha


def quantise(rgb, solid, colors):
    """Median-cut palette over the opaque pixels only, no dithering."""
    pixels = rgb[solid].astype(np.uint8).reshape(-1, 1, 3)
    pal = Image.fromarray(pixels, 'RGB').quantize(colors=colors, method=Image.MEDIANCUT, dither=Image.NONE)
    out = np.zeros_like(rgb, dtype=np.uint8)
    out[solid] = np.asarray(pal.convert('RGB')).reshape(-1, 3)
    return out


def pixelize(src, grid=96, scale=4, colors=16, wrap=False):
    rgba = np.asarray(Image.open(src).convert('RGBA')).astype(np.float32)
    rgb, alpha = resample(rgba, grid, wrap)
    solid = alpha >= MIN_ALPHA
    art = np.dstack([quantise(rgb, solid, colors), solid.astype(np.uint8) * 255])
    return Image.fromarray(art, 'RGBA').resize((grid * scale, grid * scale), Image.NEAREST)


def main():
    args = sys.argv[1:]
    opts = {'--colors': 16, '--grid': 96, '--scale': 4}
    for flag in list(opts):
        if flag in args:
            i = args.index(flag)
            opts[flag] = int(args[i + 1])
            del args[i:i + 2]
    wrap = '--tile' in args
    if wrap:
        args.remove('--tile')
    if len(args) != 2:
        sys.exit(__doc__)
    pixelize(args[0], opts['--grid'], opts['--scale'], opts['--colors'], wrap).save(args[1])


if __name__ == '__main__':
    main()
