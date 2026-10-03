"""Cuts the Reseal/Release ending art out of the raw AI sheets.

  python Tools/Story/make_ending_art.py Tools/Story/source/TheBound_raw.png Tools/Story/source/VoidCave_raw.png

TheBound.png: the full-body Bound, magenta keyed out and cropped to the figure.
VoidCave.png: the cave backdrop, cropped to its painted rows (the rest is black).
"""
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

OUT = "Assets/Textures/Story/"


def cut_bound(path):
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.int32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    # Magenta: red and blue both high, green far below them.
    magenta = (r > 150) & (b > 150) & (g < 110) & (np.minimum(r, b) - g > 90)
    solid = ~magenta
    # Drop the anti-aliased fringe and any stray specks.
    solid = ndimage.binary_erosion(solid, iterations=1)
    labels, count = ndimage.label(solid)
    sizes = ndimage.sum(solid, labels, range(1, count + 1))
    solid = np.isin(labels, [i + 1 for i, size in enumerate(sizes) if size >= 40])
    rgba = np.dstack([rgb, np.where(solid, 255, 0)]).astype(np.uint8)
    rgba[~solid] = 0
    ys, xs = np.where(solid)
    pad = 4
    box = (max(xs.min() - pad, 0), max(ys.min() - pad, 0), xs.max() + pad + 1, ys.max() + pad + 1)
    image = Image.fromarray(rgba, "RGBA").crop(box)
    image.save(OUT + "TheBound.png")
    print("TheBound.png", image.size)


# The camera sees well below the floor, so the slabs carry on down as courses of the same stone,
# darkening into the void.
UNDERFLOOR_ROWS = 300
SLAB_BAND = (683, 713)  # rows of the cropped image holding one course of floor slabs


def add_underfloor(image):
    pixels = np.asarray(image).astype(np.float32)
    band = pixels[SLAB_BAND[0]:SLAB_BAND[1]]
    rows = []
    for i in range(UNDERFLOOR_ROWS):
        row = band[i % band.shape[0]]
        # Offset every other course by half a slab, like brickwork.
        if (i // band.shape[0]) % 2 == 1:
            row = np.roll(row, 61, axis=0)
        fade = max(0.0, 1.0 - i / (UNDERFLOOR_ROWS * 0.8)) ** 1.5 * 0.55
        rows.append(row * fade)
    out = np.concatenate([pixels[:SLAB_BAND[1]], np.stack(rows)]).clip(0, 255).astype(np.uint8)
    return Image.fromarray(out, "RGB")


def cut_cave(path):
    image = Image.open(path).convert("RGB")
    luma = np.asarray(image.convert("L")).astype(np.int32)
    rows = np.where(luma.max(axis=1) > 24)[0]
    top, bottom = rows.min(), rows.max() + 1
    image = image.crop((0, top, image.width, bottom))
    image = add_underfloor(image)
    image.save(OUT + "VoidCave.png")
    print("VoidCave.png", image.size, "rows", top, bottom)


if __name__ == "__main__":
    cut_bound(sys.argv[1])
    cut_cave(sys.argv[2])
