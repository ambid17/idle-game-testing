"""Splits the Depot's roll-up garage door and console screen out of depot.png so they can animate.

Writes, next to the building art:
  depot.png            - same art with the doorway repainted as a dark interior
  depotDoor.png        - the shutter, cropped to the doorway bbox, so a child at the printed
                         local position lines up exactly
  depotDoorMask.png    - doorway shape for the SpriteMask that clips the shutter as it rolls up
  depotConsoleLit.png  - the console screen beside the door, brightened, pulsed over the
                         original while the player is being refuelled/repaired
  depotFuelIcon.png, depotRepairIcon.png - tiny pixel icons that float up off the player
                         during that resupply (native resolution, import at 36 PPU)

Re-runnable: if the shutter already exists it's pasted back first to rebuild the closed-door
source. The doorway polygon is hand-measured off the current art - re-measure if it changes.

Run from the repo root: python Tools/Buildings/make_depot_door.py
"""
import os

import numpy as np
from PIL import Image, ImageDraw

DIR = "Assets/Textures/Buildings"
BASE = os.path.join(DIR, "depot.png")
DOOR = os.path.join(DIR, "depotDoor.png")
MASK = os.path.join(DIR, "depotDoorMask.png")
CONSOLE = os.path.join(DIR, "depotConsoleLit.png")

# Doorway outline in source pixels (chamfered top corners).
DOORWAY = [(393, 792), (393, 584), (417, 560), (609, 560), (633, 584), (633, 792)]
BBOX = (393, 560, 634, 793)
CONSOLE_BBOX = (654, 639, 712, 737)

FUEL_ICON = os.path.join(DIR, "depotFuelIcon.png")
REPAIR_ICON = os.path.join(DIR, "depotRepairIcon.png")

# Icon pixel maps: '#' outline, 'o' body, '+' highlight.
FUEL_ROWS = [
    "....#....",
    "...#o#...",
    "...#o#...",
    "..#ooo#..",
    ".#o+ooo#.",
    ".#o+ooo#.",
    ".#ooooo#.",
    "..#ooo#..",
    "...###...",
]
REPAIR_ROWS = [
    "...###...",
    "...#+#...",
    "...#o#...",
    "####o####",
    "#+ooooo##",
    "####o####",
    "...#o#...",
    "...#o#...",
    "...###...",
]
OUTLINE = (16, 18, 34, 255)
FUEL_COLORS = {"o": (255, 168, 56, 255), "+": (255, 226, 150, 255)}
REPAIR_COLORS = {"o": (84, 240, 130, 255), "+": (200, 255, 214, 255)}

INTERIOR_TOP = (6, 8, 16)
INTERIOR_BOTTOM = (20, 34, 56)
PIXEL = 4  # the art's chunky "pixel" size, so the interior shading bands match it


def local_position(bbox, size):
    cx, cy = (bbox[0] + bbox[2]) / 2, (bbox[1] + bbox[3]) / 2
    return f"({(cx - size / 2) / size:.5f}, {(size / 2 - cy) / size:.5f})"


def make_icon(rows, colors, path):
    icon = Image.new("RGBA", (len(rows[0]), len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "#":
                icon.putpixel((x, y), OUTLINE)
            elif ch in colors:
                icon.putpixel((x, y), colors[ch])
    icon.save(path)


def main():
    base = Image.open(BASE).convert("RGBA")

    mask_full = Image.new("L", base.size, 0)
    ImageDraw.Draw(mask_full).polygon(DOORWAY, fill=255)

    # Rebuild the closed-door source if a previous run already cut the door out.
    if os.path.exists(DOOR):
        base.alpha_composite(Image.open(DOOR).convert("RGBA"), (BBOX[0], BBOX[1]))

    door = Image.new("RGBA", base.size, (0, 0, 0, 0))
    door.paste(base, (0, 0), mask_full)

    # Dark interior: banded vertical gradient, faintly lit toward the floor.
    interior = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(interior)
    height = BBOX[3] - BBOX[1]
    for y in range(BBOX[1], BBOX[3], PIXEL):
        t = (y - BBOX[1]) / max(1, height - PIXEL)
        t = t * t
        color = tuple(round(a + (b - a) * t) for a, b in zip(INTERIOR_TOP, INTERIOR_BOTTOM))
        draw.rectangle((BBOX[0], y, BBOX[2], y + PIXEL - 1), fill=color + (255,))

    console = np.array(base.crop(CONSOLE_BBOX)).astype(float)
    console[..., :3] = np.clip(console[..., :3] * 1.7 + 45, 0, 255)

    base.paste(interior, (0, 0), mask_full)

    mask_sprite = Image.new("RGBA", base.size, (0, 0, 0, 0))
    mask_sprite.paste((255, 255, 255, 255), (0, 0), mask_full)

    base.save(BASE)
    door.crop(BBOX).save(DOOR)
    mask_sprite.crop(BBOX).save(MASK)
    Image.fromarray(console.astype(np.uint8)).save(CONSOLE)

    make_icon(FUEL_ROWS, FUEL_COLORS, FUEL_ICON)
    make_icon(REPAIR_ROWS, REPAIR_COLORS, REPAIR_ICON)

    size = base.size[0]
    print(f"door {BBOX[2] - BBOX[0]}x{height}; at 1024 PPU, child local positions: "
          f"door {local_position(BBOX, size)}, console {local_position(CONSOLE_BBOX, size)}, "
          f"door height {height / size:.5f}")


if __name__ == "__main__":
    main()
