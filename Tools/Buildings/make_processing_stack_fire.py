"""Splits the Processing Center's smokestack fire out of processing.png so it can animate.

Writes, next to the building art:
  processing.png                    - same art with the baked flame/smoke erased and the stack's
                                      rim recoloured cold (no fire glow)
  processingStackLit.png            - the original fire-lit rim, shown over the cold one while
                                      the fire burns
  ProcessingFire/processingFire_NN.png - looping flame + smoke flipbook frames

Re-runnable: the lit rim is pasted back first to rebuild the source. The flame is procedural
(scrolling tileable noise, so the loop is seamless), in the palette of the flame it replaces.
Rim/flame coordinates are hand-measured off the current art - re-measure if it changes.

Run from the repo root: python Tools/Buildings/make_processing_stack_fire.py
"""
import os

import numpy as np
from PIL import Image

DIR = "Assets/Textures/Buildings"
BASE = os.path.join(DIR, "processing.png")
LIT = os.path.join(DIR, "processingStackLit.png")
FRAME_DIR = os.path.join(DIR, "ProcessingFire")

# Everything above the stack's mouth in this column of the art is the baked flame and smoke.
ERASE = (190, 0, 345, 229)
# The stack's rim, which the art paints lit orange by the fire.
RIM = (206, 229, 306, 302)
# Where the flame sits: bottom-centre of the frame canvas, just inside the rim's lip.
RIM_BOTTOM = 277  # below this the region is the stack itself, only tinted by the glow
MOUTH = (256, 240)

PIXEL = 4  # the art's chunky "pixel" size
COLS, ROWS = 40, 38
FRAMES = 12
FLAME_PERIOD = 36  # noise tile heights in cells; each scrolls one whole tile per loop
SMOKE_PERIOD = 24
FLAME_HEIGHT = 20

YELLOW = (252, 190, 88)
ORANGE = (246, 134, 98)
EMBER = (232, 132, 116)
SMOKE = (210, 142, 134)

COLD_OUTLINE = np.array([14, 14, 30], dtype=float)
COLD_DARK = np.array([40, 44, 78], dtype=float)
COLD_LIGHT = np.array([96, 112, 156], dtype=float)


def tile_noise(rng, cols, period, cell):
    """Value noise that tiles vertically over `period` rows."""
    grid = rng.random((period // cell, cols // cell + 2))
    ys = np.arange(period) / cell
    xs = np.arange(cols) / cell
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    ty = (ys - y0)[:, None]
    tx = (xs - x0)[None, :]
    ty = ty * ty * (3 - 2 * ty)
    tx = tx * tx * (3 - 2 * tx)
    y1 = (y0 + 1) % grid.shape[0]
    a = grid[y0][:, x0] * (1 - tx) + grid[y0][:, x0 + 1] * tx
    b = grid[y1][:, x0] * (1 - tx) + grid[y1][:, x0 + 1] * tx
    return a * (1 - ty) + b * ty


def scrolled(noise, frame, rows):
    """`noise` risen by frame/FRAMES of its period, as a rows-tall field (row 0 = bottom)."""
    period = noise.shape[0]
    shift = frame * period // FRAMES
    idx = (np.arange(rows) - shift) % period
    return noise[idx]


def make_frames():
    rng = np.random.default_rng(7)
    flame_noise = tile_noise(rng, COLS, FLAME_PERIOD, 4) * 0.65 + tile_noise(rng, COLS, FLAME_PERIOD, 2) * 0.35
    smoke_noise = tile_noise(rng, COLS, SMOKE_PERIOD, 6) * 0.7 + tile_noise(rng, COLS, SMOKE_PERIOD, 3) * 0.3
    sparks = [(rng.integers(-9, 12), rng.random(), rng.integers(14, 26), rng.random() * 0.3) for _ in range(5)]

    ys = np.arange(ROWS)[:, None].astype(float)
    xs = np.arange(COLS)[None, :].astype(float)
    cx = COLS / 2 - 0.5

    frames = []
    for f in range(FRAMES):
        phase = 2 * np.pi * f / FRAMES
        img = np.zeros((ROWS, COLS, 4), dtype=np.uint8)

        # Smoke: a column leaning downwind, widening and thinning out as it climbs.
        rise = np.clip((ys - 9) / (ROWS - 9), 0, 1)
        centre = cx + 1.5 + 9.0 * rise ** 1.3 + np.sin(phase + ys * 0.35) * 0.8
        width = 6.5 + 5.5 * rise
        column = np.clip(1 - np.abs(xs - centre) / width, 0, 1)
        density = scrolled(smoke_noise, f, ROWS) * 0.9 + column * 0.8 - rise * 0.3
        density -= np.clip((ys - (ROWS - 6)) / 5, 0, 1) * 0.8  # thin out before the canvas top
        smoke = (density > 0.68) & (ys >= 8) & (column > 0)
        img[smoke] = SMOKE + (255,)
        img[smoke & (rise > 0.72)] = SMOKE + (170,)

        # Flame: a tapering tongue, hottest at the base centre.
        sway = np.sin(phase + ys * 0.45) * 0.07 * ys
        height = np.clip(ys / FLAME_HEIGHT, 0, 1)
        half_width = 8.5 * (1 - height) ** 0.6 + 0.01
        body = 1 - np.abs(xs - cx - sway) / half_width
        heat = body * 0.9 - height * 0.75 + scrolled(flame_noise, f, ROWS) * 0.85
        heat[body <= 0] = 0
        img[heat > 0.3] = EMBER + (255,)
        img[heat > 0.48] = ORANGE + (255,)
        img[heat > 0.84] = YELLOW + (255,)

        # Sparks thrown clear of the flame, each on its own looping rise.
        for dx, offset, reach, wobble in sparks:
            t = (f / FRAMES + offset) % 1.0
            y = int(6 + t * reach)
            x = int(cx + dx + np.sin(phase + offset * 6) * 1.5 + t * 4 * wobble * 3)
            if t < 0.8 and 0 <= x < COLS and y < ROWS:
                img[y, x] = YELLOW + (255,)

        frames.append(Image.fromarray(img[::-1]).resize((COLS * PIXEL, ROWS * PIXEL), Image.NEAREST))
    return frames


def cool_rim(base):
    """Recolours the fire-lit rim into the cold blue-grey of the rest of the stack."""
    px = np.array(base).astype(float)
    region = px[RIM[1]:RIM[3], RIM[0]:RIM[2]]
    rgb = region[..., :3]
    lum = (rgb[..., 0] * 0.3 + rgb[..., 1] * 0.5 + rgb[..., 2] * 0.2) / 255
    # The lit lip and drips are far brighter than unlit metal would be - pull them down.
    glow = np.clip((rgb[..., 0] - rgb[..., 2] - 40) / 80, 0, 1)
    lum = lum * (1 - 0.45 * glow)
    lo, hi = 0.22, 0.55
    t = np.clip((lum - lo) / (hi - lo), 0, 1)[..., None]
    dark_t = np.clip(lum / lo, 0, 1)[..., None]
    cold = np.where(lum[..., None] < lo,
                    COLD_OUTLINE + (COLD_DARK - COLD_OUTLINE) * dark_t,
                    COLD_DARK + (COLD_LIGHT - COLD_DARK) * t)
    # Full strength on the rim itself, fading out down the stack so there's no seam.
    rows = np.arange(RIM[1], RIM[3])
    strength = np.clip((RIM[3] - rows) / (RIM[3] - RIM_BOTTOM), 0, 1)[:, None, None]
    region[..., :3] = rgb + (cold - rgb) * strength
    px[RIM[1]:RIM[3], RIM[0]:RIM[2]] = region
    return Image.fromarray(px.astype(np.uint8))


def main():
    base = Image.open(BASE).convert("RGBA")

    # Rebuild the lit-rim source if a previous run already cooled it.
    if os.path.exists(LIT):
        base.paste(Image.open(LIT).convert("RGBA"), (RIM[0], RIM[1]))

    base.crop(RIM).save(LIT)
    base.paste((0, 0, 0, 0), ERASE)
    cool_rim(base).save(BASE)

    os.makedirs(FRAME_DIR, exist_ok=True)
    frames = make_frames()
    for i, frame in enumerate(frames):
        frame.save(os.path.join(FRAME_DIR, f"processingFire_{i:02d}.png"))

    size = base.size[0]
    w, h = frames[0].size
    rim_cx, rim_cy = (RIM[0] + RIM[2]) / 2, (RIM[1] + RIM[3]) / 2
    print(f"{FRAMES} frames {w}x{h}; at 1024 PPU, child local positions: "
          f"fire ({(MOUTH[0] - size / 2) / size:.5f}, {(size / 2 - (MOUTH[1] - h / 2)) / size:.5f}), "
          f"lit rim ({(rim_cx - size / 2) / size:.5f}, {(size / 2 - rim_cy) / size:.5f})")


if __name__ == "__main__":
    main()
