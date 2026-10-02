"""Builds Assets/Textures/Critters/CritterShop.png from the generated source/CritterShopSource.png.

The source is a 1024px image on a flat pink background. It is keyed, halved to 2-source-pixel
texels (so the building is ~480 texels wide - double the old 246) and laid out bottom-centre on a
512x512 canvas, because the sprite's pivot is bottom-centre and the shop is placed on the cave floor.

Prints the pixels-per-unit that keeps the old world width; that value goes in the .meta.

Run from anywhere: python Tools/Critters/make_critter_shop.py [preview_dir]
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
OUT = HERE + "../../Assets/Textures/Critters/CritterShop.png"
PREVIEW = sys.argv[1] if len(sys.argv) > 1 else None
SIZE = 512
WORLD_WIDTH = 246 / 52      # the old sprite's content width in units (246px at 52 PPU)


def key_background(rgb):
    """Key out the flat background (sampled from the corners). The building has no pink, so every
    background-coloured pixel goes, including pockets between mushroom stems."""
    a = rgb.astype(np.float32)
    corners = np.concatenate([a[:8, :8].reshape(-1, 3), a[:8, -8:].reshape(-1, 3)])
    solid = np.linalg.norm(a - np.median(corners, axis=0), axis=2) > 80
    solid = ndimage.binary_erosion(solid)                    # 1px in: drops the blended pink fringe
    labels, n = ndimage.label(solid)
    solid = labels == 1 + int(np.argmax(ndimage.sum(solid, labels, range(1, n + 1))))  # drop stray specks
    return np.dstack([a, solid * 255.0])


def halve(rgba):
    """2x2 box average, premultiplied so the background can't tint the edge, then hard alpha."""
    alpha = rgba[..., 3:4] / 255.0
    premul = np.concatenate([rgba[..., :3] * alpha, alpha], -1)
    h, w = premul.shape[0] // 2, premul.shape[1] // 2
    small = premul[:h * 2, :w * 2].reshape(h, 2, w, 2, 4).mean((1, 3))
    cover = small[..., 3]
    rgb = small[..., :3] / np.maximum(cover[..., None], 1e-3)
    return np.dstack([rgb, np.where(cover >= 0.5, 255, 0)]).clip(0, 255).astype(np.uint8)


def main():
    src = np.asarray(Image.open(HERE + "source/CritterShopSource.png").convert("RGB"))
    art = Image.fromarray(halve(key_background(src)), "RGBA")
    art = art.crop(art.getbbox())
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.alpha_composite(art, ((SIZE - art.width) // 2, SIZE - art.height))
    out.save(OUT)
    print(f"content {art.width}x{art.height}, pixels per unit {art.width / WORLD_WIDTH:.1f}")

    if PREVIEW:
        bg = Image.new("RGBA", out.size, (38, 30, 48, 255))
        bg.alpha_composite(out)
        bg.resize((SIZE * 2, SIZE * 2), Image.NEAREST).save(os.path.join(PREVIEW, "critter_shop_preview.png"))


if __name__ == "__main__":
    main()
