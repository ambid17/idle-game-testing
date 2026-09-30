"""Procedural 'Ancient Brick' tile for set-piece structures (BlockTypeId.AncientBrick).

Built from the real dirt.png so the grain and palette match the rest of the mine: dirt is
darkened/cooled into stone, cut into a running-bond brick pattern with recessed mortar,
bevel-lit top-left edges and a few chips. Deterministic (fixed seed) - rerun freely.

    python Tools/Structures/make_ancient_brick.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets/Textures/Ores/1 dirt.png"
DST = ROOT / "Assets/Textures/Ores/30 ancientBrick.png"

SIZE = 128
ROWS = 4            # brick courses per tile (tiles seamlessly: 128 / 4 = 32px courses)
BRICKS_PER_ROW = 2  # 64px bricks, odd rows offset by half a brick
MORTAR = 3
BEVEL = 3


def main():
    rng = np.random.default_rng(30)
    dirt = np.asarray(Image.open(SRC).convert("RGB"), dtype=np.float32) / 255.0
    # Luma-only grain from the dirt, recoloured toward a cool, weathered sandstone-grey.
    luma = dirt @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
    grain = (luma - luma.mean()) * 1.1
    stone = np.array([0.43, 0.38, 0.36], dtype=np.float32)

    course_h = SIZE // ROWS
    brick_w = SIZE // BRICKS_PER_ROW
    out = np.zeros((SIZE, SIZE, 3), dtype=np.float32)
    ys, xs = np.mgrid[0:SIZE, 0:SIZE]

    course = ys // course_h
    offset = np.where(course % 2 == 1, brick_w // 2, 0)
    bx = (xs + offset) % SIZE
    in_x = bx % brick_w
    in_y = ys % course_h
    brick_id = course * BRICKS_PER_ROW + bx // brick_w

    # Per-brick tone variation so the wall doesn't read as a flat stamp.
    tones = rng.uniform(-0.06, 0.06, size=ROWS * BRICKS_PER_ROW)
    hue = rng.uniform(-0.02, 0.02, size=(ROWS * BRICKS_PER_ROW, 3))
    base = stone + tones[brick_id][..., None] + hue[brick_id]
    out = base + grain[..., None]

    mortar = (in_x < MORTAR) | (in_y < MORTAR)
    # Bevel: light top/left inner edge, dark bottom/right inner edge.
    light = ~mortar & ((in_x < MORTAR + BEVEL) | (in_y < MORTAR + BEVEL))
    dark = ~mortar & ((in_x >= brick_w - BEVEL) | (in_y >= course_h - BEVEL))
    out[light] += 0.07
    out[dark] -= 0.09
    out[mortar] = 0.16 + grain[mortar][..., None] * 0.5

    # A few chipped corners, darkened like the mortar.
    for _ in range(6):
        b = rng.integers(0, ROWS * BRICKS_PER_ROW)
        corner_x = rng.choice([MORTAR, brick_w - 1])
        corner_y = rng.choice([MORTAR, course_h - 1])
        r = rng.integers(3, 6)
        chip = (brick_id == b) & (np.abs(in_x - corner_x) + np.abs(in_y - corner_y) < r)
        out[chip] = 0.2 + grain[chip][..., None] * 0.5

    img = (np.clip(out, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img, "RGB").convert("RGBA").save(DST)
    print(f"wrote {DST}")


if __name__ == "__main__":
    main()
