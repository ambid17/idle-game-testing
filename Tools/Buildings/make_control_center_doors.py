"""Splits the Control Center's double door out of controlCenter.png so it can slide open.

Writes, next to the building art:
  controlCenter.png           - same art with the doorway repainted as a dark interior
  controlCenterDoorLeft.png   - left door leaf   } cropped to the doorway bbox, so a child at
  controlCenterDoorRight.png  - right door leaf  } local (0, DOOR_LOCAL_Y) lines up exactly
  controlCenterDoorMask.png   - doorway shape for the SpriteMask that clips the sliding leaves

Re-runnable: if the leaves already exist they're pasted back first to rebuild the closed-door
source. The doorway polygon is hand-measured off the current art - re-measure if it changes.

Run from the repo root: python Tools/Buildings/make_control_center_doors.py
"""
import os

from PIL import Image, ImageDraw

DIR = "Assets/Textures/Buildings"
BASE = os.path.join(DIR, "controlCenter.png")
LEFT = os.path.join(DIR, "controlCenterDoorLeft.png")
RIGHT = os.path.join(DIR, "controlCenterDoorRight.png")
MASK = os.path.join(DIR, "controlCenterDoorMask.png")

# Doorway outline in source pixels (chamfered top corners), and the seam between the leaves.
DOORWAY = [(421, 920), (421, 790), (454, 757), (569, 757), (602, 790), (602, 920)]
BBOX = (421, 757, 603, 921)
SEAM_X = 512

INTERIOR_TOP = (6, 12, 18)
INTERIOR_BOTTOM = (14, 44, 54)
PIXEL = 4  # the art's chunky "pixel" size, so the interior shading bands match it


def main():
    base = Image.open(BASE).convert("RGBA")

    mask_full = Image.new("L", base.size, 0)
    ImageDraw.Draw(mask_full).polygon(DOORWAY, fill=255)

    # Rebuild the closed-door source if a previous run already cut the doors out.
    if os.path.exists(LEFT) and os.path.exists(RIGHT):
        for path in (LEFT, RIGHT):
            leaf = Image.open(path).convert("RGBA")
            base.alpha_composite(leaf, (BBOX[0], BBOX[1]))

    doors = Image.new("RGBA", base.size, (0, 0, 0, 0))
    doors.paste(base, (0, 0), mask_full)

    left = doors.copy()
    left.paste((0, 0, 0, 0), (SEAM_X, 0, base.size[0], base.size[1]))
    right = doors.copy()
    right.paste((0, 0, 0, 0), (0, 0, SEAM_X, base.size[1]))

    # Dark interior: banded vertical gradient, faintly lit toward the floor.
    interior = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(interior)
    height = BBOX[3] - BBOX[1]
    for y in range(BBOX[1], BBOX[3], PIXEL):
        t = (y - BBOX[1]) / max(1, height - PIXEL)
        t = t * t
        color = tuple(round(a + (b - a) * t) for a, b in zip(INTERIOR_TOP, INTERIOR_BOTTOM))
        draw.rectangle((BBOX[0], y, BBOX[2], y + PIXEL - 1), fill=color + (255,))
    base.paste(interior, (0, 0), mask_full)

    mask_sprite = Image.new("RGBA", base.size, (0, 0, 0, 0))
    mask_sprite.paste((255, 255, 255, 255), (0, 0), mask_full)

    base.save(BASE)
    left.crop(BBOX).save(LEFT)
    right.crop(BBOX).save(RIGHT)
    mask_sprite.crop(BBOX).save(MASK)

    center_y = (BBOX[1] + BBOX[3]) / 2
    print(f"door sprites {BBOX[2] - BBOX[0]}x{BBOX[3] - BBOX[1]}, "
          f"DOOR_LOCAL_Y = {(base.size[1] / 2 - center_y) / base.size[1]:.5f} (at 1024 PPU)")


if __name__ == "__main__":
    main()
