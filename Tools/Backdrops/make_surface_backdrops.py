"""Builds the strips where the mine meets the sky (ParallaxBackdrop's surfaceSoil and
horizonHills), procedurally in the same chunky pixel art as cave_layout.py.

- Backdrop_SurfaceSoil: the packed-earth back wall right under the surface, darker than the dirt
  tiles so they still read in front of it. Its top row is plain soil (ParallaxBackdrop clamps the
  strip vertically, so that row fills any gap up to the surface at steep camera angles). Its
  lower edge is a ragged cave ceiling with roots and drips, transparent below, where the cavern
  opens up behind it.
- Backdrop_HorizonFar / _Near: hill silhouettes along the horizon, hazed toward the sky colour.
  Their bottom GROUND_ROWS are plain dithered ground, which the shader repeats downward, so no
  sky shows between the hills and the surface when the camera looks down from above.

Everything wraps horizontally. Run from anywhere: python Tools/Backdrops/make_surface_backdrops.py
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
ROOT = HERE + "../../Assets/Textures/Backdrops/"
WIDTH = 2048
ART_PX = 4
BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16

# Dirt tile (Assets/Textures/Ores/1 dirt.png) tones, pushed back into shadow.
SOIL = [(26, 16, 14), (38, 24, 19), (50, 32, 25), (62, 40, 31), (76, 50, 39)]
PEBBLE = [(52, 50, 52), (74, 72, 74)]
ROOT_THREAD = [(66, 46, 34), (112, 92, 66)]
SKY = np.array([67, 178, 222], float)  # SkyLow far plane, near the horizon
# Bottom rows of the hill art that are plain ground, tiling vertically (a multiple of the Bayer
# period). ParallaxBackdrop.horizonGroundBand must equal GROUND_ROWS / the art's rows.
GROUND_ROWS = 16


def periodic(n, periods, rng, octaves=((1.0, 1),)):
    """Smooth noise in about [-1, 1] that wraps exactly over n samples (sum of whole sines)."""
    x = np.arange(n) / n * 2 * np.pi
    out = np.zeros(n)
    for amp, base in octaves:
        for k in range(base * periods, base * periods * 2):
            out += amp * rng.uniform(0.3, 1.0) * np.sin(k * x + rng.uniform(0, 2 * np.pi)) / k ** 0.6
    return out / (np.abs(out).max() + 1e-6)


def to_texture(art):
    return Image.fromarray(art, "RGBA").resize((art.shape[1] * ART_PX, art.shape[0] * ART_PX), Image.NEAREST)


def soil_wall(seed=11, rows=256):
    rng = np.random.default_rng(seed)
    w = WIDTH // ART_PX
    art = np.zeros((rows, w, 4), np.uint8)
    # Cave ceiling: how far down the earth reaches in each column (art px).
    ceiling = 150 + 22 * periodic(w, 3, rng, ((1.0, 1), (0.35, 4))) + 4 * periodic(w, 30, rng)
    ceiling = np.round(ceiling).astype(int)
    for _ in range(w // 9):  # drips and stubby stalactites off the ceiling
        c = rng.integers(0, w)
        ln = rng.integers(3, 14)
        half = rng.integers(1, 4)
        for dx in range(-half, half + 1):
            ceiling[(c + dx) % w] = max(ceiling[(c + dx) % w], ceiling[c] + ln - abs(dx) * (ln // max(1, half)))
    strata = periodic(w, 5, rng)
    grain = rng.random((rows, w))
    for x in range(w):
        for y in range(ceiling[x]):
            depth = y / ceiling[x]
            # Darker going down; wavering bands; speckled.
            tone = 3.2 - 2.0 * depth + 0.5 * np.sin((y + 6 * strata[x]) / 7.0) + (grain[y, x] - 0.5) * 1.4
            tone = int(np.clip(np.floor(tone + BAYER[y % 4, x % 4] - 0.5), 1, 4))
            art[y, x, :3] = SOIL[tone]
            art[y, x, 3] = 255
        # Ceiling rim: a dark lip with a lit row just above it.
        art[ceiling[x] - 1, x, :3] = SOIL[0]
        if ceiling[x] >= 3:
            art[ceiling[x] - 2, x, :3] = SOIL[1]
    for _ in range(w // 6):  # pebbles bedded in the soil
        x, y = rng.integers(0, w), rng.integers(4, 130)
        if y + 2 < ceiling[x]:
            for dx, dy, c in ((0, 0, 0), (1, 0, 0), (0, 1, 0), (1, 1, 0), (0, -1, 1), (1, -1, 1)):
                art[y + dy, (x + dx) % w, :3] = PEBBLE[c]
    for _ in range(w // 36):  # roots threading down through the earth and out of the ceiling
        x = rng.integers(0, w)
        y = rng.integers(0, 60)
        length = rng.integers(25, 110)
        for _ in range(length):
            if y >= rows:
                break
            inside = y < ceiling[x % w]
            art[y, x % w, :3] = ROOT_THREAD[1] if not inside else ROOT_THREAD[0]
            art[y, x % w, 3] = 255
            if not inside and rng.random() < 0.25:
                break
            y += 1
            if rng.random() < 0.45:
                x += rng.choice([-1, 1])
    art[0, :, :3] = SOIL[3]  # plain soil on top: the clamped row that fills toward the surface
    art[0, :, 3] = 255
    return to_texture(art)


def hills(seed, rows, base, height, colour, rim, trees):
    """Rolling hills: top silhouette, a lit rim, darker toward the ground, plain ground band."""
    rng = np.random.default_rng(seed)
    w = WIDTH // ART_PX
    art = np.zeros((rows, w, 4), np.uint8)
    profile = base + height * periodic(w, 2, rng, ((1.0, 1), (0.3, 3))) + 2 * periodic(w, 25, rng)
    tops = np.round(rows - 1 - GROUND_ROWS - profile).astype(int)  # hills stand on the ground band
    colour = np.array(colour, float)
    for x in range(w):
        for y in range(max(0, tops[x]), rows):
            # The shading bottoms out at the ground band, which stays one dithered tone so the
            # shader can repeat it downward without a visible step.
            depth = min(1.0, (y - tops[x]) / max(1, rows - GROUND_ROWS - tops[x]))
            shade = 1.0 - 0.18 * depth + 0.06 * (BAYER[y % 4, x % 4] - 0.5)
            c = colour * shade
            if y - tops[x] < 2:
                c = np.array(rim, float)
            art[y, x, :3] = np.clip(c, 0, 255)
            art[y, x, 3] = 255
    for _ in range(trees):  # clumps of round trees on the near ridge
        cx = rng.integers(0, w)
        for _ in range(rng.integers(2, 5)):
            tx = cx + rng.integers(-10, 11)
            r = rng.integers(3, 7)
            ty = tops[tx % w] - r + 2
            for dy in range(-r, r + 1):
                for dx in range(-r, r + 1):
                    if dx * dx + dy * dy <= r * r and 0 <= ty + dy < rows:
                        lit = dy < -r // 3 and dx < 0
                        c = np.array(rim if lit else colour, float) * (0.92 if dy > r // 2 else 1.0)
                        art[ty + dy, (tx + dx) % w, :3] = np.clip(c, 0, 255)
                        art[ty + dy, (tx + dx) % w, 3] = 255
    return to_texture(art)


def haze(colour, amount):
    return tuple(np.round(np.array(colour) * (1 - amount) + SKY * amount).astype(int))


def main():
    soil_wall().save(ROOT + "Backdrop_SurfaceSoil.png")
    grass = (58, 150, 112)
    hills(21, 128, 46, 26, haze(grass, 0.55), haze((110, 200, 150), 0.5), 0).save(ROOT + "Backdrop_HorizonFar.png")
    hills(22, 128, 26, 16, haze(grass, 0.25), haze((120, 210, 140), 0.2), 9).save(ROOT + "Backdrop_HorizonNear.png")


if __name__ == "__main__":
    main()
