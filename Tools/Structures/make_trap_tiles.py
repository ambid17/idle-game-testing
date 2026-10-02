"""Procedural tiles and sprites for the trap-room set-pieces (Dart Corridor, Sealed Vault;
the Crusher Room's art comes from make_crusher_art.py). Everything is derived from the Ancient Brick tile so it matches the masonry it
sits in. Deterministic (fixed seed) - rerun freely; make_masonry_tiles.py must have run first.

    python Tools/Structures/make_trap_tiles.py
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
ORES = ROOT / "Assets/Textures/Ores"
HAZARDS = ROOT / "Assets/Textures/Hazards"
BRICK = ORES / "30 ancientBrick.png"

SIZE = 128
METAL = (126, 132, 142)
METAL_DARK = (58, 62, 72)
METAL_LIGHT = (188, 194, 204)
RECESS = (22, 20, 24)
WARN = (214, 96, 44)


def brick():
    return Image.open(BRICK).convert("RGBA")


def grain(rng, img, amount=10):
    """Per-pixel luma noise on the opaque pixels, so flat fills pick up the tile grain."""
    a = np.asarray(img, dtype=np.int16).copy()
    noise = rng.integers(-amount, amount + 1, size=a.shape[:2])
    a[..., :3] = np.clip(a[..., :3] + noise[..., None], 0, 255)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def metal_box(d, box, bevel=4):
    x0, y0, x1, y1 = box
    d.rectangle(box, fill=METAL)
    d.rectangle((x0, y0, x1, y0 + bevel - 1), fill=METAL_LIGHT)
    d.rectangle((x0, y0, x0 + bevel - 1, y1), fill=METAL_LIGHT)
    d.rectangle((x0, y1 - bevel + 1, x1, y1), fill=METAL_DARK)
    d.rectangle((x1 - bevel + 1, y0, x1, y1), fill=METAL_DARK)


def cracked_brick(rng):
    """The Sealed Vault's door: brick split by dark branching fractures."""
    img = brick()
    d = ImageDraw.Draw(img)

    def crack(x, y, angle, length, width):
        points = [(x, y)]
        for _ in range(length):
            angle += rng.uniform(-0.6, 0.6)
            x += np.cos(angle) * 7
            y += np.sin(angle) * 7
            points.append((x, y))
            if width > 2 and rng.random() < 0.3:
                crack(x, y, angle + rng.choice([-1, 1]) * rng.uniform(0.6, 1.1), length // 2, width - 2)
        d.line(points, fill=RECESS, width=width, joint="curve")
        d.line([(px + 1, py + 2) for px, py in points], fill=(150, 138, 130), width=1)

    crack(64, 60, -1.3, 9, 5)
    crack(64, 60, 1.9, 9, 5)
    crack(64, 60, 0.3, 8, 4)
    crack(64, 60, 3.4, 8, 4)
    return img


def pressure_plate(rng):
    """Floor block: brick with a raised metal plate along its top face."""
    img = brick()
    d = ImageDraw.Draw(img)
    d.rectangle((10, 0, SIZE - 11, 25), fill=RECESS)
    metal_box(d, (14, 0, SIZE - 15, 19))
    for x in (24, SIZE - 25):
        d.ellipse((x - 3, 7, x + 3, 13), fill=METAL_DARK)
    d.rectangle((40, 8, SIZE - 41, 11), fill=WARN)
    return grain(rng, img, 6)


def dart_trap(rng):
    """Wall block: brick with a metal-ringed bore. Symmetrical, since a trap fires out of
    whichever faces open onto dug-out ground."""
    img = brick()
    d = ImageDraw.Draw(img)
    c = SIZE // 2
    d.ellipse((c - 36, c - 36, c + 36, c + 36), fill=METAL_DARK)
    d.ellipse((c - 33, c - 34, c + 31, c + 30), fill=METAL)
    d.ellipse((c - 22, c - 22, c + 22, c + 22), fill=METAL_DARK)
    d.ellipse((c - 18, c - 18, c + 18, c + 18), fill=RECESS)
    for dx, dy in ((0, -29), (0, 29), (-29, 0), (29, 0)):
        d.ellipse((c + dx - 3, c + dy - 3, c + dx + 3, c + dy + 3), fill=WARN)
    return grain(rng, img, 6)


def dart():
    """Transparent sprite, half a cell long, pointing right."""
    img = Image.new("RGBA", (64, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((6, 6, 46, 9), fill=(96, 70, 48))
    d.polygon([(44, 2), (63, 8), (44, 13)], fill=METAL_LIGHT)
    d.polygon([(44, 8), (63, 8), (44, 13)], fill=METAL)
    d.polygon([(0, 1), (12, 6), (12, 9), (0, 14)], fill=WARN)
    return img


def main():
    rng = np.random.default_rng(31)
    HAZARDS.mkdir(parents=True, exist_ok=True)
    outputs = {
        ORES / "31 crackedBrick.png": cracked_brick(rng),
        ORES / "32 pressurePlate.png": pressure_plate(rng),
        ORES / "33 dartTrap.png": dart_trap(rng),
        HAZARDS / "dart.png": dart(),
    }
    for path, img in outputs.items():
        img.save(path)
        print(f"wrote {path}")


if __name__ == "__main__":
    main()
