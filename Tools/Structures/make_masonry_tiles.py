"""Cuts the AI-generated masonry sheet (source/masonry_sheet_raw.png - OpenRouter via UnityMCP
generate_image, market.png as the style reference) into the structure tiles: Ancient Brick and
Hardpan. The sheet holds two variations of each; VARIANT picks which quadrant is used.

Each tile is cropped to the middle of the dark frame the model draws around it, so every tile
edge is half a mortar line and neighbouring tiles join into one full line - no seam repair.
make_trap_tiles.py derives its tiles from the brick, so rerun it afterwards.

    python Tools/Structures/make_masonry_tiles.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SHEET = Path(__file__).resolve().parent / "source/masonry_sheet_raw.png"
ORES = ROOT / "Assets/Textures/Ores"

SIZE = 128
COLORS = 20  # palette size after downscaling, keeps the cel-shaded tones flat
# (column, row) of the sheet's 2x2 grid.
VARIANT = {"30 ancientBrick.png": (0, 0), "28 hardpan.png": (0, 1)}


def is_magenta(a):
    r, g, b = a[..., 0].astype(int), a[..., 1].astype(int), a[..., 2].astype(int)
    return (r > 150) & (b > 90) & (g < 110) & (r - g > 90)


def cut(sheet, col, row):
    """The quadrant's tile, cropped to the centre of its dark outer frame."""
    half = sheet.shape[0] // 2
    quad = sheet[row * half:(row + 1) * half, col * half:(col + 1) * half]
    ys, xs = np.where(~is_magenta(quad))
    x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    tile = quad[y0:y1, x0:x1]

    # Frame thickness = how far the dark outline runs in from the left edge, mid-height rows.
    luma = tile[..., :3].max(axis=2)
    rows = luma[tile.shape[0] // 3: 2 * tile.shape[0] // 3]
    frame = int(np.median([np.argmax(r > 70) for r in rows]))
    inset = frame // 2
    return Image.fromarray(tile[inset:tile.shape[0] - inset, inset:tile.shape[1] - inset], "RGBA")


def main():
    sheet = np.asarray(Image.open(SHEET).convert("RGBA"))
    for name, (col, row) in VARIANT.items():
        tile = cut(sheet, col, row).resize((SIZE, SIZE), Image.BOX)
        tile = tile.convert("RGB").quantize(COLORS, dither=Image.Dither.NONE).convert("RGBA")
        tile.save(ORES / name)
        print(f"wrote {ORES / name}")


if __name__ == "__main__":
    main()
