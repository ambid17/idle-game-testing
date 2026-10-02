"""Cuts AI-generated UI icons off a flat magenta sheet into 64x64 transparent sprites.

The model doesn't honour the requested grid, so icons are found as connected non-magenta blobs
and listed in reading order; pick the ones to keep by index.

  python Tools/UI/slice_icon_sheet.py <sheet.png>                       # list blobs + preview
  python Tools/UI/slice_icon_sheet.py <sheet.png> 3=Assets/.../A.png …  # write blob 3 to A.png
  python Tools/UI/slice_icon_sheet.py <sheet.png> --size 128 --padding 17 7=Assets/.../Tile.png
      # the same icon as a power-up tile foreground: 128x128, with the margin the other tiles have
"""
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

SIZE = 64
PADDING = 2
MIN_AREA = 2000
HOLE_TOLERANCE = 18


def key_alpha(rgb):
    """Alpha from distance to the sheet's own magenta (sampled at the corner), with a narrow
    ramp so edges stay smooth. Only magenta connected to the sheet border is keyed, so purple
    accents inside an icon survive (enclosed holes of the exact key colour are still cut)."""
    distance = np.linalg.norm(rgb - rgb[2, 2], axis=-1)
    alpha = np.clip((distance - 40) / 50, 0, 1)
    labels, _ = ndimage.label(alpha < 1)
    border = set(np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]])))
    for label, region in enumerate(ndimage.find_objects(labels), start=1):
        if label in border:
            continue
        inside = labels[region] == label
        # An enclosed patch of the exact key colour is a see-through hole (a handle); anything
        # merely magenta-ish is part of the art.
        if np.median(distance[region][inside]) > HOLE_TOLERANCE:
            alpha[region][inside] = 1
    return alpha


def find_icons(sheet):
    rgb = np.array(sheet.convert("RGB")).astype(float)
    alpha = key_alpha(rgb)
    solid = ndimage.binary_closing(alpha > 0.5, iterations=6)
    labels, _ = ndimage.label(solid)
    boxes = [s for s in ndimage.find_objects(labels)
             if (s[0].stop - s[0].start) * (s[1].stop - s[1].start) >= MIN_AREA]
    row_height = max(s[0].stop - s[0].start for s in boxes) * 0.6
    boxes.sort(key=lambda s: (round(s[0].start / row_height), s[1].start))
    return rgb, alpha, boxes


def cut(rgb, alpha, box, size=SIZE, padding=PADDING):
    crop = np.dstack([rgb[box], alpha[box] * 255]).astype(np.uint8)
    icon = Image.fromarray(crop, "RGBA")
    scale = (size - 2 * padding) / max(icon.size)
    icon = icon.resize((max(1, round(icon.size[0] * scale)), max(1, round(icon.size[1] * scale))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(icon, ((size - icon.size[0]) // 2, (size - icon.size[1]) // 2))
    return out


def take_option(args, name, default):
    if name not in args:
        return default
    i = args.index(name)
    value = int(args[i + 1])
    del args[i:i + 2]
    return value


def main():
    args = sys.argv[1:]
    size = take_option(args, "--size", SIZE)
    padding = take_option(args, "--padding", PADDING)
    sheet = Image.open(args[0])
    rgb, alpha, boxes = find_icons(sheet)

    if len(args) == 1:
        preview = sheet.convert("RGB")
        draw = ImageDraw.Draw(preview)
        for i, box in enumerate(boxes):
            draw.rectangle((box[1].start, box[0].start, box[1].stop, box[0].stop), outline=(255, 255, 255), width=2)
            draw.text((box[1].start + 4, box[0].start + 4), str(i), fill=(255, 255, 255))
            print(i, (box[1].start, box[0].start, box[1].stop, box[0].stop))
        preview.save(args[0].replace(".png", "_blobs.png"))
        return

    for arg in args[1:]:
        index, path = arg.split("=", 1)
        cut(rgb, alpha, boxes[int(index)], size, padding).save(path)
        print(index, "->", path)


if __name__ == "__main__":
    main()
