"""Tiles and sprites for the trap-room set-pieces (Dart Corridor, Sealed Vault; the Crusher
Room's art comes from make_crusher_art.py). The Dart Corridor's fixtures are cut from the
AI-generated sheet (source/dart_sheet_raw.png - OpenRouter via UnityMCP generate_image,
crusher_sheet_raw.png as the style reference) and laid over the Ancient Brick tile so they match
the masonry they sit in; the Cracked Brick is procedural. Deterministic (fixed seed) - rerun
freely; make_masonry_tiles.py must have run first.

    python Tools/Structures/make_trap_tiles.py
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SHEET = Path(__file__).resolve().parent / "source/dart_sheet_raw.png"
ORES = ROOT / "Assets/Textures/Ores"
HAZARDS = ROOT / "Assets/Textures/Hazards"
BRICK = ORES / "30 ancientBrick.png"

SIZE = 128
RECESS = (22, 20, 24)
# Quadrant interiors of the 1024 sheet (inside the magenta gutters).
QUADS = {"plate": (70, 70, 494, 494), "trap": (530, 70, 954, 494), "dart": (70, 530, 494, 954)}
BACKGROUND_LUMA = 26
# Rows of each cut subject that hold the metal fixture; the model's own stonework is dropped.
PLATE_ROWS = (62, 148)  # the plate's front lip and the slot it sinks into, not its top face
TRAP_ROWS = (0, 183)
DART_LENGTH = 64        # half a cell


def brick():
    return Image.open(BRICK).convert("RGBA")


def cut(name):
    """The quadrant's subject, cropped to its bounds, with the black backdrop made transparent.
    Only backdrop connected to the quadrant edge is keyed, so dark outlines and recesses stay."""
    quad = Image.open(SHEET).convert("RGBA").crop(QUADS[name])
    a = np.asarray(quad).copy()
    dark = a[..., :3].max(axis=2) <= BACKGROUND_LUMA
    labels, _ = ndimage.label(dark)
    edge = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    backdrop = np.isin(labels, edge[edge != 0])
    a[backdrop] = 0
    ys, xs = np.where(~backdrop)
    return Image.fromarray(a, "RGBA").crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def crisp(img, size):
    """Downscale, then snap alpha to on/off so edges stay hard like the rest of the pixel art."""
    out = np.asarray(img.resize(size, Image.BOX)).copy()
    out[..., 3] = np.where(out[..., 3] >= 128, 255, 0)
    out[out[..., 3] == 0] = 0
    return Image.fromarray(out, "RGBA")


def fixture(name, rows):
    """The subject's metal rows, scaled to span the full cell width."""
    src = cut(name)
    src = src.crop((0, rows[0], src.width, rows[1]))
    return crisp(src, (SIZE, round(src.height * SIZE / src.width)))


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


def pressure_plate():
    """Floor block: brick with a metal plate along its top face, over the slot it presses into."""
    img = brick()
    img.alpha_composite(fixture("plate", PLATE_ROWS))
    return img


def dart_trap():
    """Wall block: brick with a launcher housing across it, an arrow port at each side. The
    corridor's traps only ever face left or right (the structure mirrors but never rotates)."""
    img = brick()
    housing = fixture("trap", TRAP_ROWS)
    img.alpha_composite(housing, (0, (SIZE - housing.height) // 2))
    return img


def dart():
    """Transparent sprite, half a cell long, pointing right."""
    src = cut("dart")
    return crisp(src, (DART_LENGTH, round(src.height * DART_LENGTH / src.width)))


def main():
    rng = np.random.default_rng(31)
    HAZARDS.mkdir(parents=True, exist_ok=True)
    outputs = {
        ORES / "31 crackedBrick.png": cracked_brick(rng),
        ORES / "32 pressurePlate.png": pressure_plate(),
        ORES / "33 dartTrap.png": dart_trap(),
        HAZARDS / "dart.png": dart(),
    }
    for path, img in outputs.items():
        img.save(path)
        print(f"wrote {path}")


if __name__ == "__main__":
    main()
