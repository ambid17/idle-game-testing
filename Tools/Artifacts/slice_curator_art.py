"""Slices the AI-generated curator art (OpenRouter, via UnityMCP generate_image) into game assets.

Inputs (Assets/Textures/Artifacts/_generated, deleted after slicing - keep copies to re-run):
  CuratorPortrait_raw.png - full-bleed portrait -> CuratorPortrait.png (256px)
  Accessories_raw.png     - 5 accessories on flat magenta -> Accessory_*.png (128px, transparent)

The accessory lenses/runes are a lighter magenta close to the key colour, so the key uses a tight
threshold (the lenses still clear it) and only fills speckle-sized enclosed holes, so the halo centre
and the monocle chain loop stay see-through.

Usage: python Tools/Artifacts/slice_curator_art.py
"""
import os

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "Textures", "Artifacts")
GEN = os.path.join(ART, "_generated")

# Reading order of the sheet (top row left->right, then bottom row) -> output name, mirror?
# The player sprite faces right: the cape has to stream off to the left, the helmet badge and goggle
# lens face forward (right) with the strap wrapping round the back.
ACCESSORIES = [
    ("Accessory_PithHelmet", True),
    ("Accessory_Monocle", False),
    ("Accessory_Cape", True),
    ("Accessory_RuneGoggles", True),
    ("Accessory_RuneHalo", False),
]
# Lenses are drawn as solid magenta; make them see-through so the robot's glowing eye shows behind.
GLASS_LENSES = {"Accessory_Monocle", "Accessory_RuneGoggles"}
LENS_ALPHA = 110
KEY_THRESHOLD = 34
HOLE_FILL_MAX = 300
OUT_SIZE = 128
PADDING = 0.06


def lens_mask(arr):
    r, g, b, a = (arr[..., i].astype(int) for i in range(4))
    return (r > 225) & (g < 110) & (b > 170) & (a > 0)


def slice_portrait():
    im = Image.open(os.path.join(GEN, "CuratorPortrait_raw.png")).convert("RGB")
    im.resize((256, 256), Image.LANCZOS).save(os.path.join(ART, "CuratorPortrait.png"))


def slice_accessories():
    rgb = np.array(Image.open(os.path.join(GEN, "Accessories_raw.png")).convert("RGB")).astype(float)
    bg = np.median(np.concatenate([rgb[:8].reshape(-1, 3), rgb[-8:].reshape(-1, 3)]), axis=0)
    dist = np.linalg.norm(rgb - bg, axis=2)
    opaque = dist > KEY_THRESHOLD

    # Fill speckle-sized enclosed holes only - the halo middle and chain loop must stay see-through.
    holes = ndimage.binary_fill_holes(opaque) & ~opaque
    labels, n = ndimage.label(holes)
    sizes = ndimage.sum(holes, labels, range(1, n + 1))
    for i, size in enumerate(sizes, start=1):
        if size <= HOLE_FILL_MAX:
            opaque[labels == i] = True

    opaque = ndimage.binary_opening(opaque, iterations=1)
    # Merge each item's loose bits (chain links, glints) before picking the 5 biggest blobs.
    grouped, n = ndimage.label(ndimage.binary_dilation(opaque, iterations=14))
    sizes = ndimage.sum(opaque, grouped, range(1, n + 1))
    biggest = np.argsort(sizes)[::-1][:len(ACCESSORIES)] + 1
    boxes = []
    for label in biggest:
        ys, xs = np.nonzero((grouped == label) & opaque)
        boxes.append((label, ys.min(), ys.max(), xs.min(), xs.max()))
    # Rows first (split at the image middle), then left to right.
    boxes.sort(key=lambda b: (0 if (b[1] + b[2]) / 2 < rgb.shape[0] / 2 else 1, b[3]))

    alpha = (opaque * 255).astype(np.uint8)
    rgba = np.dstack([rgb.astype(np.uint8), alpha])
    for (name, mirror), (label, y0, y1, x0, x1) in zip(ACCESSORIES, boxes):
        crop = rgba[y0:y1 + 1, x0:x1 + 1].copy()
        crop[..., 3] = np.where((grouped[y0:y1 + 1, x0:x1 + 1] == label), crop[..., 3], 0)
        item = Image.fromarray(crop)
        if mirror:
            item = item.transpose(Image.FLIP_LEFT_RIGHT)
        w, h = item.size
        inner = OUT_SIZE * (1 - 2 * PADDING)
        scale = inner / max(w, h)
        item = item.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
        arr = np.array(item)
        arr[..., 3] = np.where(arr[..., 3] > 120, 255, 0)
        if name in GLASS_LENSES:
            arr[..., 3] = np.where(lens_mask(arr), LENS_ALPHA, arr[..., 3])
        out = Image.new("RGBA", (OUT_SIZE, OUT_SIZE), (0, 0, 0, 0))
        # Bottom-centre aligned: the sprite pivot is bottom-centre, like the automaton hats.
        out.paste(Image.fromarray(arr), ((OUT_SIZE - arr.shape[1]) // 2, OUT_SIZE - arr.shape[0] - round(OUT_SIZE * PADDING)))
        out.save(os.path.join(ART, f"{name}.png"))


if __name__ == "__main__":
    slice_portrait()
    slice_accessories()
