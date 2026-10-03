"""Cuts a magenta icon sheet into 64x64 sprites by grid cell rather than by blob, so an icon
made of separate pieces (a robot plus a "+", a drill plus speed lines) stays one sprite.
Use when the model honoured the requested grid; otherwise fall back to slice_icon_sheet.py.

  python Tools/UI/slice_icon_grid.py <sheet.png> 3x3 1=Assets/.../A.png 5=Assets/.../B.png …
      # cells numbered 1.. in reading order
"""
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

from slice_icon_sheet import cut, key_alpha

MIN_PIECE = 60


def main():
    sheet, grid, *targets = sys.argv[1:]
    cols, rows = map(int, grid.lower().split("x"))
    rgb = np.array(Image.open(sheet).convert("RGB")).astype(float)
    alpha = key_alpha(rgb)
    height, width = alpha.shape
    for target in targets:
        index, path = target.split("=", 1)
        index = int(index) - 1
        y0, x0 = (index // cols) * height // rows, (index % cols) * width // cols
        y1, x1 = y0 + height // rows, x0 + width // cols
        cell = alpha[y0:y1, x0:x1] > 0.5
        # Drop specks so a stray pixel doesn't stretch the crop box.
        labels, count = ndimage.label(cell)
        sizes = ndimage.sum(cell, labels, range(1, count + 1))
        cell = np.isin(labels, np.flatnonzero(sizes >= MIN_PIECE) + 1)
        ys, xs = np.nonzero(cell)
        box = (slice(y0 + ys.min(), y0 + ys.max() + 1), slice(x0 + xs.min(), x0 + xs.max() + 1))
        cut(rgb, alpha, box).save(path)
        print(f"cell {index + 1} -> {path}")


if __name__ == "__main__":
    main()
