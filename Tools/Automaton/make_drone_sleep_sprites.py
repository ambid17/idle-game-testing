"""Builds the "asleep" sprite variants for the Fuel/Storage drones plus the floating Z sprite.

Used by Automation.DroneSleepVisual: idle drones swap to <Name>_Sleep.png (lamp eye powered down
to dark glass with a closed-eyelid curve, thruster rings faded out) and emit SleepZ.png particles.

Procedural off the existing single-frame drone art, same approach as make_automaton_sprites.py -
the eye/thruster regions are found by luminance, so re-run if the source art changes.

    python Tools/Automaton/make_drone_sleep_sprites.py
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
TEX = ROOT / "Assets" / "Textures" / "Automation"

DRONES = ["FuelDrone", "StorageDrone"]


def luminance(rgb):
    return rgb @ np.array([0.299, 0.587, 0.114])


def find_eye(rgba):
    """Largest bright blob in the top half = the lamp eye. Returns (cx, cy, radius)."""
    lum = luminance(rgba[..., :3])
    bright = (rgba[..., 3] > 0.5) & (lum > 0.7)
    bright[64:, :] = False
    labels, count = ndimage.label(bright)
    sizes = ndimage.sum(bright, labels, range(1, count + 1))
    core = labels == (int(np.argmax(sizes)) + 1)
    ys, xs = np.nonzero(core)
    cx, cy = xs.mean(), ys.mean()
    # The lamp's lit glass extends past the brightest core into a softer halo ring.
    radius = max(xs.max() - xs.min(), ys.max() - ys.min()) / 2 + 3.5
    return cx, cy, radius


def make_sleep_variant(name):
    src = np.array(Image.open(TEX / f"{name}.png").convert("RGBA")).astype(float) / 255
    out = src.copy()
    h, w = src.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    lum = luminance(src[..., :3])
    opaque = src[..., 3] > 0.05

    cx, cy, r = find_eye(src)
    eye = opaque & ((xx - cx) ** 2 + (yy - cy) ** 2 <= r * r) & (lum > 0.35)

    # Glow colour sampled from the eye core, used for the dim eyelid line.
    core = eye & (lum > 0.8)
    glow = src[core, :3].mean(axis=0)

    # Eye -> dark glass: keep a hint of hue, crush brightness.
    grey = lum[eye][:, None]
    out[eye, :3] = (src[eye, :3] * 0.35 + grey * 0.15) * 0.45 + np.array([0.04, 0.05, 0.08])

    # Closed-eyelid curve "‿" across the lower-middle of the eye, 2px thick, dim glow.
    lid_half_width = r * 0.6
    for x in range(int(cx - lid_half_width), int(cx + lid_half_width) + 1):
        t = (x - cx) / lid_half_width
        y = cy - r * 0.05 + (1 - t * t) * r * 0.22
        for dy in (0, 1):
            py = int(round(y)) + dy
            if 0 <= py < h and eye[py, x]:
                out[py, x, :3] = glow * (0.95 if dy == 0 else 0.6)

    # Thruster rings sit outside the hull at mid-height on both sides: fade their bright pixels.
    sides = (xx < 36) | (xx > 92)
    band = (yy > 45) & (yy < 80)
    thruster = opaque & sides & band & (lum > 0.45)
    out[thruster, 3] *= 0.25
    out[thruster, :3] *= 0.6

    Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(TEX / f"{name}_Sleep.png")
    print(f"{name}: eye centre=({cx:.1f},{cy:.1f}) r={r:.1f}, eye px={eye.sum()}, thruster px={thruster.sum()}")


# 5x5 "Z" glyph, drawn at 2 texture px per art pixel to sit a little chunkier than the drone's
# 1px detail so it reads at gameplay zoom. White fill + dark outline; tinted in-engine.
Z_GLYPH = [
    "#####",
    "...#.",
    "..#..",
    ".#...",
    "#####",
]


def make_sleep_z():
    scale, pad = 2, 1
    gh, gw = len(Z_GLYPH), len(Z_GLYPH[0])
    fill = np.zeros((gh + 2 * pad, gw + 2 * pad), bool)
    for y, row in enumerate(Z_GLYPH):
        for x, c in enumerate(row):
            fill[y + pad, x + pad] = c == "#"
    outline = ndimage.binary_dilation(fill, structure=np.ones((3, 3))) & ~fill

    img = np.zeros(fill.shape + (4,), np.uint8)
    img[outline] = (20, 24, 40, 255)
    img[fill] = (255, 255, 255, 255)
    Image.fromarray(img).resize((fill.shape[1] * scale, fill.shape[0] * scale), Image.NEAREST).save(TEX / "SleepZ.png")
    print("SleepZ written", fill.shape[1] * scale, "px")


if __name__ == "__main__":
    for drone in DRONES:
        make_sleep_variant(drone)
    make_sleep_z()
