"""Cuts each ore's tile art apart into little loose chunks (OreChunks.png).

The ore foregrounds are already drawn as separate nuggets/crystals on transparency, so the
chunks are just their connected pieces: the biggest few that aren't clipped by the tile edge,
each centred in its own cell. Art drawn as one connected mass (iron's lumps joined by veins) is
first opened up to drop the thin joins.

Sheet layout: one column per BlockTypeId (column index = id, so non-ore ids are empty columns),
one row per variant. Effects.WorldEffects reads it by that grid.

Usage: python make_ore_chunks.py <ore_foreground_dir> <out_png>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

CELL = 48          # px per chunk cell (tile art is 128 px per world unit)
FIT = 44           # chunks bigger than this are scaled down to fit their cell
VARIANTS = 4
MIN_AREA = 150     # smaller pieces are specks, not chunks
ALPHA_SOLID = 40
SKIP_IDS = {9}     # artifact: a single tablet, has its own rune debris


def pieces(alpha: np.ndarray) -> list[np.ndarray]:
    """Masks of the separate chunks in a tile, biggest first, edge-clipped ones last."""
    labels, count = ndimage.label(alpha)
    found = []
    for index in range(1, count + 1):
        mask = labels == index
        if mask.sum() < MIN_AREA:
            continue
        clipped = mask[0].any() or mask[-1].any() or mask[:, 0].any() or mask[:, -1].any()
        found.append((clipped, -int(mask.sum()), mask))
    found.sort(key=lambda entry: entry[:2])
    return [mask for _, _, mask in found]


def chunk_masks(alpha: np.ndarray) -> list[np.ndarray]:
    masks = pieces(alpha)
    if len(masks) >= 3:
        return masks
    # One connected mass: drop the thin joins so the lumps come apart.
    disk = np.hypot(*np.mgrid[-4:5, -4:5]) <= 4
    return pieces(ndimage.binary_opening(alpha, structure=disk))


def cut(tile: np.ndarray, mask: np.ndarray) -> Image.Image:
    rows, cols = np.where(mask)
    box = tile[rows.min():rows.max() + 1, cols.min():cols.max() + 1].copy()
    box[~mask[rows.min():rows.max() + 1, cols.min():cols.max() + 1]] = 0
    chunk = Image.fromarray(box, "RGBA")
    longest = max(chunk.size)
    if longest > FIT:
        chunk = chunk.resize((max(1, chunk.width * FIT // longest), max(1, chunk.height * FIT // longest)), Image.NEAREST)
    return chunk


def main() -> None:
    source_dir, out_path = Path(sys.argv[1]), Path(sys.argv[2])
    tiles = {}
    for path in source_dir.glob("*.png"):
        prefix = path.stem.split()[0]
        if prefix.isdigit() and int(prefix) not in SKIP_IDS:
            tiles[int(prefix)] = path

    columns = max(tiles) + 1
    sheet = Image.new("RGBA", (columns * CELL, VARIANTS * CELL), (0, 0, 0, 0))
    for block_id, path in sorted(tiles.items()):
        tile = np.asarray(Image.open(path).convert("RGBA"))
        masks = chunk_masks(tile[..., 3] > ALPHA_SOLID)
        if not masks:
            sys.exit(f"{path.name}: no chunks found")

        for variant in range(VARIANTS):
            # Fewer pieces than variants: repeat them.
            chunk = cut(tile, masks[variant % len(masks)])
            x = block_id * CELL + (CELL - chunk.width) // 2
            y = variant * CELL + (CELL - chunk.height) // 2
            sheet.paste(chunk, (x, y))
        print(f"{path.name}: {min(len(masks), VARIANTS)} chunks")

    sheet.save(out_path)
    print(f"wrote {out_path} ({columns} columns x {VARIANTS} rows)")


if __name__ == "__main__":
    main()
