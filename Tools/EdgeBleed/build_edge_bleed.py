"""Regenerates Assets/Textures/Terrain/EdgeBleed/*.png (the dirt debris drawn in mined cells).

Every tile is derived from one distance field - the distance from each pixel to the nearest
solid neighbor cell - pushed through a single alpha falloff profile. Because neighboring mined
cells measure distance to the same solid cells, their strips line up exactly at the shared tile
edge, and the dirt-facing edge is always fully opaque and unshaded so it meets the real Dirt
tile without a line.

Usage (from the repo root):  python Tools/EdgeBleed/build_edge_bleed.py [output_dir]
"""
import itertools
import os
import sys

import numpy as np
from PIL import Image

TILE = 128
SRC = "Assets/Textures/Ores/1 dirt.png"
OUT = "Assets/Textures/Terrain/EdgeBleed"

# Alpha by distance (px) from the solid cell: fully solid for the first few px, gone by ~18px.
PROFILE_DIST = [4.5, 5.5, 6.5, 7.5, 8.5, 9.5, 10.5, 11.5, 12.5, 13.5, 14.5, 15.5, 16.5, 17.5, 18.5]
PROFILE_ALPHA = [255, 251, 245, 232, 212, 185, 154, 120, 88, 58, 33, 16, 6, 2, 0]

# Radius of the fillet where two adjacent strips meet (the concave elbow of an L-shaped wall).
# Must stay well under TILE - strip width so it never reaches a tile edge and breaks tiling.
ELBOW_RADIUS = 10.0

# The thin leading lip is darkened, easing to the Dirt tile's exact color where it turns solid.
SHADE_MIN = 0.5

ORTHOS = "NSEW"
DIAGS = ["NE", "NW", "SE", "SW"]


def smooth_min(a, b, k):
    h = np.clip(k - np.abs(a - b), 0, None) / k
    return np.minimum(a, b) - h * h * k * 0.25


def distance_field(orthos, diags):
    ys, xs = np.mgrid[0:TILE, 0:TILE].astype(float) + 0.5
    side = {"N": ys, "S": TILE - ys, "E": TILE - xs, "W": xs}
    far = np.full((TILE, TILE), 1e9)

    d = far.copy()
    for o in orthos:
        d = np.minimum(d, side[o])
    for a, b in itertools.product("NS", "EW"):
        if a in orthos and b in orthos:
            d = np.minimum(d, smooth_min(side[a], side[b], ELBOW_RADIUS))
    for dg in diags:
        d = np.minimum(d, np.hypot(side[dg[0]], side[dg[1]]))
    return d


def build(orthos, diags, dirt):
    d = distance_field(orthos, diags)
    alpha = np.interp(d, PROFILE_DIST, PROFILE_ALPHA)
    shade = SHADE_MIN + (1 - SHADE_MIN) * alpha / 255.0
    out = np.empty((TILE, TILE, 4))
    out[..., :3] = dirt[..., :3] * shade[..., None]
    out[..., 3] = alpha
    return Image.fromarray(np.round(out).clip(0, 255).astype(np.uint8), "RGBA")


def tile_name(orthos, diags):
    if not orthos and not diags:
        return "EdgeBleed_None"
    return "_".join(["EdgeBleed", orthos or "None"] + list(diags))


def all_combos():
    for n in range(len(ORTHOS) + 1):
        for combo in itertools.combinations(ORTHOS, n):
            orthos = "".join(combo)
            # A corner only gets a nub when neither flanking side is solid.
            free = [dg for dg in DIAGS if dg[0] not in orthos and dg[1] not in orthos]
            for m in range(len(free) + 1):
                for diags in itertools.combinations(free, m):
                    yield orthos, diags


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else OUT
    os.makedirs(out_dir, exist_ok=True)
    dirt = np.array(Image.open(SRC).convert("RGBA")).astype(float)
    count = 0
    for orthos, diags in all_combos():
        build(orthos, diags, dirt).save(os.path.join(out_dir, tile_name(orthos, diags) + ".png"))
        count += 1
    print(f"wrote {count} tiles to {out_dir}")


if __name__ == "__main__":
    main()
