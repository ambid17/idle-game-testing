"""Builds the sky parallax textures (ParallaxBackdrop's sky bands) from the generated sheets.

Same pipeline as make_backdrops.py: far planes are opaque, flattened and made seamless; mid/near
planes are keyed objects (clouds, space bodies) scattered on a torus into transparent tiles.

Far sheet: 2x2 of labelled panels on white (low sky, high sky, upper atmosphere, space) - the
model insisted on writing a label in each panel's top-left, so each is cropped below it.
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_backdrops as mb  # noqa: E402

# Run from anywhere: python Tools/Backdrops/make_sky_backdrops.py [preview_dir]
ROOT = mb.ROOT
SHEETS = mb.SHEETS
SCRATCH = sys.argv[1] if len(sys.argv) > 1 else mb.HERE
BANDS = ["SkyLow", "SkyHigh", "SkyUpper", "Space"]
# Panel boxes on the far sheet, cropped below the label: (x0, y0, size).
FAR_CROPS = [(45, 105, 380), (535, 105, 380), (45, 595, 380), (535, 595, 380)]
# The pink galaxy's soft glow blends into the green key and keys out as a grey-green halo.
mb.EXCLUDE.add(("Space", 4))


def far_tiles():
    sheet = np.asarray(Image.open(SHEETS + "Backdrop_Sky_Far_Sheet.png").convert("RGB")).astype(np.float32)
    for name, (x0, y0, size) in zip(BANDS, FAR_CROPS):
        q = sheet[y0:y0 + size, x0:x0 + size]
        # Divide out the panel's vertical gradient so the tile repeats without banding.
        low = ndimage.gaussian_filter(q, sigma=(size / 5, size / 5, 0))
        flat = np.clip(q * (q.reshape(-1, 3).mean(0) / np.maximum(low, 1e-3)), 0, 255)
        img = Image.fromarray(flat.astype(np.uint8)).resize((512, 512), Image.NEAREST)
        a = mb.seamless_blend(np.asarray(img).astype(np.float32))
        Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(ROOT + f"Backdrop_{name}_Far.png")


def main():
    far_tiles()
    clouds, _ = mb.extract_objects("Clouds")
    space, _ = mb.extract_objects("Space")
    # (band, plane, objects, count, scale range, seed, min gap)
    layouts = [
        ("SkyLow", "Mid", clouds, 6, (0.45, 0.65), 1, 1.4),
        ("SkyLow", "Near", clouds, 3, (0.9, 1.1), 2, 1.5),
        ("SkyHigh", "Mid", clouds, 12, (0.6, 0.9), 3, 1.1),
        ("SkyHigh", "Near", clouds, 5, (1.3, 1.7), 4, 1.2),
        ("SkyUpper", "Mid", clouds, 4, (0.4, 0.6), 5, 1.6),
        ("SkyUpper", "Near", space, 2, (1.0, 1.3), 6, 2.0),
        ("Space", "Mid", space, 10, (0.8, 1.2), 7, 1.8),
        ("Space", "Near", space, 3, (1.8, 2.4), 8, 2.0),
    ]
    for band, plane, objs, count, scale, seed, gap in layouts:
        tile, n = mb.scatter_tile(objs, 2048, count, scale, seed=seed, min_gap=gap)
        tile.save(ROOT + f"Backdrop_{band}_{plane}.png")
        print(f"{band}_{plane}: {n}/{count} placed")

    prev = Image.new("RGB", (1024, 1024))
    for k, band in enumerate(BANDS):
        comp = Image.open(ROOT + f"Backdrop_{band}_Far.png").convert("RGBA").resize((2048, 2048), Image.NEAREST)
        for plane in ("Mid", "Near"):
            comp.alpha_composite(Image.open(ROOT + f"Backdrop_{band}_{plane}.png"))
        prev.paste(comp.convert("RGB").resize((512, 512), Image.NEAREST), ((k % 2) * 512, (k // 2) * 512))
    prev.save(os.path.join(SCRATCH, "sky_backdrop_preview.png"))


if __name__ == "__main__":
    main()
