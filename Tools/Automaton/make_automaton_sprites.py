"""Builds the Mining Automaton sprite sheets from one generated body frame.

The model can draw the drone on-model, but asking it for a 20-frame animated sheet gave random
flames/sparks per cell instead of per-row states. So only the body comes from the model (the top-left
cell of source/AutomatonSource.png, chroma-keyed off magenta). Every frame is that same body plus
procedural motion: hover bob, thruster flames under the three pods, eye blink/brighten, a drill whose
grooves roll along its axis (reads as spinning), and seeded spark sprays for mining.

Output matches the player's layout (Assets/Textures/Player/RobotMovement.png / RobotMining.png):
  AutomatonMovement.png  1536x128  Idle 0-3, Move 0-3, Fly 0-3
  AutomatonMining.png    1024x128  Mining 0-7

Run from anywhere: python Tools/Automaton/make_automaton_sprites.py [preview_dir]
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
OUT = HERE + "../../Assets/Textures/Automation/"
PREVIEW = sys.argv[1] if len(sys.argv) > 1 else None
CELL = 128
BODY_SIZE = 96      # longest side of the body in the cell - the player's content is ~94px
BODY_LIFT = 8       # raise the body so flames under the pods fit inside the cell

# Feature locations on the placed 128px body (measured off the base frame, before bob/shake).
PODS = [(33, 91), (57, 95), (77, 92)]   # thruster pod bottom-centers
EYE = (68, 49, 13)                      # center x, y, radius
DRILL_BOX = (92, 56, 112, 94)           # x0, y0, x1, y1 around the drill bit
DRILL_TIP = (102, 91)                   # where sparks spray from


def key_magenta(rgb):
    """Chroma-key a flat magenta background into alpha, then kill the pink fringe."""
    a = rgb.astype(np.float32)
    corners = np.concatenate([a[:8, :8].reshape(-1, 3), a[-8:, -8:].reshape(-1, 3)])
    dist = np.linalg.norm(a - np.median(corners, axis=0), axis=2)
    alpha = np.clip((dist - 70) / 70, 0, 1)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    pinkish = (r - g > 50) & (b - g > 50)            # the drone has no magenta; orange has low blue
    alpha[pinkish] = 0
    alpha = np.minimum(alpha, ndimage.grey_erosion(alpha, size=3))  # 1px erode at source res
    return np.dstack([a, alpha * 255]).clip(0, 255).astype(np.uint8)


def load_body():
    src = np.asarray(Image.open(HERE + "source/AutomatonSource.png").convert("RGB"))
    rgba = key_magenta(src)
    labels, _ = ndimage.label(ndimage.binary_dilation(rgba[..., 3] > 40, iterations=12))
    boxes = [s for s in ndimage.find_objects(labels) if (s[0].stop - s[0].start) * (s[1].stop - s[1].start) > 5000]
    top_left = min(boxes, key=lambda s: (s[0].start // 150, s[1].start))
    crop = Image.fromarray(rgba[top_left])
    crop = crop.crop(crop.getbbox())
    scale = BODY_SIZE / max(crop.size)
    crop = crop.resize((round(crop.width * scale), round(crop.height * scale)), Image.LANCZOS)
    body = Image.new("RGBA", (CELL, CELL), (0, 0, 0, 0))
    body.alpha_composite(crop, ((CELL - crop.width) // 2, (CELL - crop.height) // 2 - BODY_LIFT))
    out = np.asarray(body).astype(np.float32)
    out[..., 3] = np.where(out[..., 3] < 24, 0, out[..., 3])   # drop near-invisible halo pixels
    return out


def adjust_eye(img, factor):
    """factor < 1 dims (blink), > 1 brightens (mining effort)."""
    cx, cy, r = EYE
    yy, xx = np.mgrid[:CELL, :CELL]
    fall = np.clip(1 - np.hypot(xx - cx, yy - cy) / r, 0, 1)[..., None]
    rgb = img[..., :3] * (1 + (factor - 1) * fall)
    if factor > 1:
        rgb += np.array([30, 18, 0]) * (factor - 1) * fall
    img[..., :3] = rgb.clip(0, 255)
    return img


def spin_drill(img, step):
    """Roll the drill bit's pixels down its (vertical-ish) axis, column by column."""
    x0, y0, x1, y1 = DRILL_BOX
    region = img[y0:y1, x0:x1]
    # Drill pixels = the grey/dark bit (low saturation), not the amber arm joining it.
    sat = region[..., :3].max(-1) - region[..., :3].min(-1)
    mask = (region[..., 3] > 128) & (sat < 45)
    for c in range(region.shape[1]):
        rows = np.where(mask[:, c])[0]
        if len(rows) < 3:
            continue
        region[rows, c, :3] = np.roll(region[rows, c, :3], step, axis=0)
    return img


def put(img, x, y, color, a=255):
    if 0 <= x < CELL and 0 <= y < CELL:
        img[y, x, :3] = color
        img[y, x, 3] = max(img[y, x, 3], a)


FLAME = [(255, 250, 215), (255, 222, 90), (255, 150, 40), (220, 70, 30)]


def draw_flames(img, length, rng, dy=0):
    """Pixel flames under each pod: hot core at the nozzle, fading to red at the tip."""
    for px, py in PODS:
        n = max(1, length + rng.integers(-1, 2))
        for i in range(n):
            t = i / max(1, n - 1)
            half = 2 if t < 0.5 else (1 if t < 0.85 else 0)
            color = FLAME[min(3, int(t * 4))]
            for dx in range(-half, half + 1):
                if half and abs(dx) == half and rng.random() < 0.4:
                    continue
                put(img, px + dx, py + 1 + i + dy, color)
    return img


SPARK = [(255, 255, 230), (255, 230, 110), (255, 170, 50), (140, 110, 90)]


def draw_sparks(img, rng, count, dx, dy):
    tx, ty = DRILL_TIP[0] + dx, DRILL_TIP[1] + dy
    # Contact flash at the tip, flickering in size like the player's mining flash.
    r = int(rng.integers(2, 4))
    for fy in range(-r, r + 1):
        for fx in range(-r, r + 1):
            d = abs(fx) + abs(fy)
            if d <= r:
                put(img, tx + fx, ty + fy, SPARK[0] if d < r - 1 else SPARK[1])
    for _ in range(count):
        ang = rng.uniform(-1.3, 0.9)          # spray right and down-right, away from the drill
        dist = rng.uniform(2, 15)
        x = int(round(tx + np.cos(ang) * dist))
        y = int(round(ty + np.sin(ang) * dist * 0.8))
        color = SPARK[min(3, int(dist / 6))]
        put(img, x, y, color)
        if dist < 10:                          # chunkier 2x2 sparks near the tip
            for ox, oy in ((1, 0), (0, 1), (1, 1)):
                put(img, x + ox, y + oy, color)


def shift(img, dx, dy):
    out = np.zeros_like(img)
    ys, xs = slice(max(0, dy), CELL + min(0, dy)), slice(max(0, dx), CELL + min(0, dx))
    ys0, xs0 = slice(max(0, -dy), CELL + min(0, -dy)), slice(max(0, -dx), CELL + min(0, -dx))
    out[ys, xs] = img[ys0, xs0]
    return out


def frame(body, rng, bob=0, dx=0, flame=2, eye=1.0, drill_step=None, sparks=0):
    img = body.copy()
    if drill_step is not None:
        img = spin_drill(img, drill_step)
    img = adjust_eye(img, eye)
    img = shift(img, dx, bob)
    img = draw_flames(img, flame, rng, bob)
    if sparks:
        draw_sparks(img, rng, sparks, dx, bob)
    return Image.fromarray(img.clip(0, 255).astype(np.uint8))


def sheet(frames):
    out = Image.new("RGBA", (CELL * len(frames), CELL), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        out.alpha_composite(f, (i * CELL, 0))
    return out


def main():
    body = load_body()
    rng = np.random.default_rng(7)  # fixed seed -> reruns are byte-identical

    idle = [frame(body, rng, bob=b, flame=f, eye=e) for b, f, e in
            [(0, 2, 1.0), (-1, 3, 1.0), (-2, 2, 0.45), (-1, 1, 1.0)]]
    move = [frame(body, rng, bob=b, flame=f) for b, f in [(0, 5), (-1, 6), (0, 5), (1, 7)]]
    fly = [frame(body, rng, bob=b, flame=f) for b, f in [(-1, 11), (-2, 13), (-1, 10), (-2, 14)]]
    shake = [(0, 0), (1, 0), (0, 1), (-1, 0), (0, 0), (1, 1), (0, -1), (-1, 0)]
    mine = [frame(body, rng, bob=sy, dx=sx, flame=3, eye=1.35, drill_step=i, sparks=22 + 6 * (i % 2))
            for i, (sx, sy) in enumerate(shake)]

    movement = sheet(idle + move + fly)
    mining = sheet(mine)
    movement.save(OUT + "AutomatonMovement.png")
    mining.save(OUT + "AutomatonMining.png")

    if PREVIEW:
        for name, s in [("movement", movement), ("mining", mining)]:
            bg = Image.new("RGBA", s.size, (38, 30, 48, 255))
            bg.alpha_composite(s)
            bg.resize((s.width * 2, s.height * 2), Image.NEAREST).save(os.path.join(PREVIEW, f"automaton_{name}_preview.png"))


if __name__ == "__main__":
    main()
