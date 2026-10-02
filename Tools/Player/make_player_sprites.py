"""Builds the player sprite sheets at double the old resolution (was 128px cells of ~2-texel art
pixels at 100 PPU; now 2-texel art pixels at 200 PPU, so the robot is the same world size with
twice the art pixels across).

Three generated sources (all on flat magenta, in source/):
  PlayerBody.png    the robot, thrusters off. The only body used - every frame is this exact body.
  PlayerFlames.png  2x2 sheet of the robot hovering; only the thruster flames are taken from it.
  PlayerBlasts.png  2x2 sheet of the robot firing; only the muzzle blasts are taken from it (the
                    model drew them detached and oversized, so they are cut out by hand-measured
                    boxes, scaled down and pinned to the muzzle).

The model can't hold a pose across an animation sheet, so motion is procedural: bob, recoil, which
flame/blast is composited, flame stretch, antenna/eye pulse and a seeded spark spray.

Output (Assets/Textures/Player), 384x320 cells, 4 columns, read left-to-right then top-to-bottom:
  RobotMovement.png  1536x960  Idle 0-3, Move 0-3, Fly 0-3
  RobotMining.png    1536x640  Mining 0-7

Run from anywhere: python Tools/Player/make_player_sprites.py [preview_dir]
"""
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
OUT = HERE + "../../Assets/Textures/Player/"
PREVIEW = sys.argv[1] if len(sys.argv) > 1 else None

TEXELS = 2                # texels per art pixel
CW, CH = 192, 160         # cell size in art pixels (x TEXELS = 384x320)
COLUMNS = 4
SRC_GRID = 8              # PlayerBody.png draws each art pixel as an 8x8 block, aligned to 0

# Where the body's bounding box sits in the cell. Chosen so the body's main block is centred a
# little left of the pivot (cell centre) and its underside is 0.34 units below it, like the old art.
BODY_LEFT = 39
BODY_BOTTOM = 114

# Feature locations in body space (art pixels from the body bbox's top-left).
NOZZLE = (25, 83)         # top-centre of where the thruster flame starts
MUZZLE = (100, 59)        # centre of the cannon mouth
EYE = (66, 31, 13)        # centre x, y, radius
ANTENNA = (25, 4, 5)      # glowing tip centre x, y, radius

# Hand-measured boxes in PlayerBlasts.png (x0, y0, x1, y1) and the size each is scaled to.
BLASTS = {
    "flash": ((578, 262, 668, 338), 16),
    "star": ((668, 128, 942, 468), 36),
    "ball": ((362, 688, 578, 904), 32),
    "burst": ((590, 628, 992, 942), 40),
}


def key_magenta(rgb, keep_pink=False):
    """Chroma-key a flat magenta background into alpha, then kill the pink fringe."""
    a = rgb.astype(np.float32)
    dist = np.linalg.norm(a - np.array([252, 4, 252]), axis=2)
    alpha = np.clip((dist - 70) / 70, 0, 1)
    if not keep_pink:
        r, g, b = a[..., 0], a[..., 1], a[..., 2]
        # The robot has no magenta or purple, so anything in that family is background bleed
        # (fringe, or the soft halo the model paints round the antenna light).
        alpha[(r - g > 50) & (b - g > 50)] = 0
        alpha[(r > 1.6 * g) & (b > 1.6 * g) & (r + b > 100)] = 0
        alpha[(b - g > 60) & (r > 60)] = 0
    return np.dstack([a, alpha * 255])


def snap(rgba, block):
    """Collapse block x block source pixels into one art pixel: median colour of the opaque
    texels, opaque if most of the block is."""
    h, w = rgba.shape[0] // block, rgba.shape[1] // block
    cells = rgba[:h * block, :w * block].reshape(h, block, w, block, 4).transpose(0, 2, 1, 3, 4).reshape(h, w, -1, 4)
    solid = cells[..., 3] > 128
    out = np.zeros((h, w, 4), np.float32)
    for y, x in zip(*np.where(solid.mean(-1) >= 0.5)):
        out[y, x, :3] = np.median(cells[y, x][solid[y, x]][:, :3], axis=0)
        out[y, x, 3] = 255
    return out


def trim(img):
    ys, xs = np.where(img[..., 3] > 0)
    return img[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def resize(img, w, h):
    """Nearest for colour (keeps flat pixel-art tones), box for coverage."""
    rgb = Image.fromarray(img[..., :3].clip(0, 255).astype(np.uint8)).resize((w, h), Image.NEAREST)
    a = Image.fromarray(img[..., 3].clip(0, 255).astype(np.uint8)).resize((w, h), Image.BOX)
    alpha = np.where(np.asarray(a) >= 128, 255, 0)
    return np.dstack([np.asarray(rgb), alpha]).astype(np.float32)


def load_body():
    src = np.asarray(Image.open(HERE + "source/PlayerBody.png").convert("RGB"))
    return trim(snap(key_magenta(src), SRC_GRID))


def load_flames():
    """The four flames from PlayerFlames.png (short, short, long, longer), in body art pixels."""
    src = np.asarray(Image.open(HERE + "source/PlayerFlames.png").convert("RGB"))
    rgba = key_magenta(src)
    r, g, b = rgba[..., 0], rgba[..., 1], rgba[..., 2]
    warm = (rgba[..., 3] > 128) & (r > 200) & (b < 140) & (r - b > 90)
    half = src.shape[0] // 2
    flames = []
    for qy in (0, 1):
        for qx in (0, 1):
            quad = np.zeros_like(warm)
            sl = (slice(qy * half, (qy + 1) * half), slice(qx * half, (qx + 1) * half))
            quad[sl] = warm[sl]
            labels, n = ndimage.label(ndimage.binary_dilation(quad, iterations=3))
            biggest = 1 + int(np.argmax(ndimage.sum(quad, labels, range(1, n + 1))))
            box = ndimage.find_objects(labels)[biggest - 1]
            crop = rgba[box].copy()
            crop[..., 3] = np.where(quad[box] & (labels[box] == biggest), 255, 0)
            flames.append(trim(crop))
    return flames


def load_blasts():
    src = np.asarray(Image.open(HERE + "source/PlayerBlasts.png").convert("RGB"))
    rgba = key_magenta(src, keep_pink=True)
    out = {}
    for name, ((x0, y0, x1, y1), size) in BLASTS.items():
        crop = rgba[y0:y1, x0:x1].copy()
        if name == "ball":   # overlaps the robot in the source: keep only the disc
            yy, xx = np.mgrid[:crop.shape[0], :crop.shape[1]]
            crop[np.hypot(xx - crop.shape[1] / 2, yy - crop.shape[0] / 2) > crop.shape[0] / 2] = 0
        crop = trim(crop)
        scale = size / max(crop.shape[:2])
        out[name] = resize(crop, max(1, round(crop.shape[1] * scale)), max(1, round(crop.shape[0] * scale)))
    return out


def paste(cell, img, x, y):
    h, w = img.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(CW, x + w), min(CH, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    part = img[y0 - y:y1 - y, x0 - x:x1 - x]
    mask = part[..., 3] > 0
    cell[y0:y1, x0:x1][mask] = part[mask]


def glow(body, spot, factor):
    """factor < 1 dims, > 1 brightens the lit pixels around a feature."""
    cx, cy, r = spot
    yy, xx = np.mgrid[:body.shape[0], :body.shape[1]]
    fall = np.clip(1.3 - np.hypot(xx - cx, yy - cy) / r, 0, 1)[..., None]
    lit = (body[..., 2:3] > 140) & (body[..., 3:4] > 0)      # cyan/white glass only, not the housing
    rgb = body[..., :3]
    target = rgb * factor if factor < 1 else rgb + (255 - rgb) * (factor - 1)
    body[..., :3] = np.where(lit, rgb + (target - rgb) * fall, rgb)
    return body


SPARK = [(255, 255, 230), (255, 230, 110), (255, 170, 50), (120, 235, 255)]


def sparks(cell, rng, count, ox, oy):
    for _ in range(count):
        ang = rng.uniform(-1.2, 1.2)
        dist = rng.uniform(8, 30)
        x = int(round(ox + np.cos(ang) * dist))
        y = int(round(oy + np.sin(ang) * dist * 0.8))
        color = SPARK[int(rng.integers(0, 4))]
        size = 2 if dist < 20 else 1
        if 0 <= x < CW - size and 0 <= y < CH - size:
            cell[y:y + size, x:x + size, :3] = color
            cell[y:y + size, x:x + size, 3] = 255


def frame(body, flame, flame_len, bob=0, dx=0, eye=1.0, antenna=1.0, blast=None, flip=False, rng=None, spark_count=0):
    b = glow(glow(body.copy(), EYE, eye), ANTENNA, antenna)
    bx = BODY_LEFT + dx
    by = BODY_BOTTOM - body.shape[0] + bob
    cell = np.zeros((CH, CW, 4), np.float32)

    f = resize(flame, flame.shape[1] * flame_len // flame.shape[0] if flame_len < flame.shape[0] else flame.shape[1], flame_len)
    paste(cell, f, bx + NOZZLE[0] - f.shape[1] // 2, by + NOZZLE[1])
    paste(cell, b, bx, by)
    if blast is not None:
        if flip:
            blast = blast[::-1]
        mx, my = bx + MUZZLE[0], by + MUZZLE[1]
        paste(cell, blast, mx - blast.shape[1] // 5, my - blast.shape[0] // 2)   # blast starts just inside the mouth
        sparks(cell, rng, spark_count, mx + 6, my)
    return cell


def sheet(frames):
    rows = (len(frames) + COLUMNS - 1) // COLUMNS
    out = np.zeros((rows * CH, COLUMNS * CW, 4), np.uint8)
    for i, f in enumerate(frames):
        y, x = (i // COLUMNS) * CH, (i % COLUMNS) * CW
        out[y:y + CH, x:x + CW] = f.clip(0, 255).astype(np.uint8)
    img = Image.fromarray(out, "RGBA")
    return img.resize((img.width * TEXELS, img.height * TEXELS), Image.NEAREST)


def main():
    body = load_body()
    short_a, short_b, long_a, long_b = load_flames()
    blasts = load_blasts()
    rng = np.random.default_rng(11)  # fixed seed -> reruns are byte-identical

    idle = [frame(body, f, n, bob=b, eye=e, antenna=a) for f, n, b, e, a in
            [(short_a, 8, 0, 1.0, 1.0), (short_b, 10, -1, 1.0, 1.25), (short_a, 9, -2, 1.0, 1.5), (short_b, 7, -1, 1.0, 1.25)]]
    move = [frame(body, f, n, bob=b, dx=d) for f, n, b, d in
            [(short_a, 14, 0, 0), (short_b, 17, -1, 1), (short_a, 15, 0, 0), (short_b, 18, 1, 1)]]
    fly = [frame(body, f, n, bob=b) for f, n, b in
           [(long_a, 30, -1), (long_b, 36, -2), (long_a, 32, -1), (long_b, 38, -2)]]
    shots = [("flash", False, 0, 4), ("ball", False, -2, 6), ("star", False, -3, 16), ("burst", False, -1, 22),
             ("flash", True, 0, 4), ("star", True, -3, 14), ("ball", True, -2, 8), ("burst", True, -1, 20)]
    mine = [frame(body, short_a if i % 2 else short_b, 9 + i % 2, dx=recoil, bob=(i % 4 == 2), eye=1.3,
                  blast=blasts[name], flip=flip, rng=rng, spark_count=count)
            for i, (name, flip, recoil, count) in enumerate(shots)]

    movement = sheet(idle + move + fly)
    mining = sheet(mine)
    movement.save(OUT + "RobotMovement.png")
    mining.save(OUT + "RobotMining.png")

    if PREVIEW:
        Image.fromarray(body.astype(np.uint8), "RGBA").resize((body.shape[1] * 8, body.shape[0] * 8), Image.NEAREST) \
            .save(os.path.join(PREVIEW, "player_body_preview.png"))
        for name, s in [("movement", movement), ("mining", mining)]:
            bg = Image.new("RGBA", s.size, (38, 30, 48, 255))
            bg.alpha_composite(s)
            bg.save(os.path.join(PREVIEW, f"player_{name}_preview.png"))


if __name__ == "__main__":
    main()
