"""Builds the player's in-between crack stage (Crack_2.png) from its neighbours.

Crack_1 is faint hairline fractures, Crack_3 is the fully lit crack web; the jump between them
was too abrupt. Stage 2 keeps Crack_1's hairlines and lights up only the core of Crack_3's web,
out to a ragged radius, so the glow reads as spreading outward from the impact point.

Usage: python make_crack_mid.py <effects_dir>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

REACH = 34          # mean lit radius in px (sprite is 128, so ~half the web's reach)
RAGGED = 0.22       # +/- fraction the radius wobbles by around the circle
FADE = 10           # px over which the lit web dims out past the radius
ART_PIXEL = 2       # the art is drawn on a 2px grid; the mask snaps to it


def main() -> None:
    effects = Path(sys.argv[1])
    hairline = np.asarray(Image.open(effects / "Crack_1.png").convert("RGBA")).astype(float)
    web = np.asarray(Image.open(effects / "Crack_3.png").convert("RGBA")).astype(float)
    size = web.shape[0]

    grid = (np.arange(size) // ART_PIXEL) * ART_PIXEL + ART_PIXEL / 2 - size / 2
    x, y = np.meshgrid(grid, grid)
    r, theta = np.hypot(x, y), np.arctan2(y, x)
    wobble = 0.5 * np.sin(3 * theta + 0.7) + 0.3 * np.sin(5 * theta + 2.1) + 0.2 * np.sin(9 * theta + 4.0)
    mask = np.clip((REACH * (1 + RAGGED * wobble) - r) / FADE + 1, 0, 1)

    web_a = web[..., 3:] / 255 * mask[..., None]
    hair_a = hairline[..., 3:] / 255
    out_a = web_a + hair_a * (1 - web_a)
    rgb = (web[..., :3] * web_a + hairline[..., :3] * hair_a * (1 - web_a)) / np.maximum(out_a, 1e-6)

    out = np.dstack([rgb, out_a * 255]).round().astype(np.uint8)
    Image.fromarray(out, "RGBA").save(effects / "Crack_2.png")
    print("wrote Crack_2.png")


if __name__ == "__main__":
    main()
