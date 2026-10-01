"""Builds the PrimaryButton (cyan) / SecondaryButton (gold) 9-slice state sprites.

The AI sheet (2 columns x 4 rows on magenta) is only used for its palette. Its buttons were
slightly asymmetric and had faceted corners, which showed up as stray notches once 9-sliced
at odd sizes, so the bevel itself is drawn here: 1px outline, 2px mitred bevel, flat body.
All states share one silhouette, so swapping states never moves an edge.

Sprites are saved upscaled (SCALE texels per art pixel) for bilinear filtering, which keeps
line weights even when a button lands on a fractional size or position.

    python Tools/UI/slice_button_sheet.py
"""
import os
import numpy as np
from PIL import Image

from slice_scroll_slider_sheet import ROOT, slice_piece

SRC = os.path.join(ROOT, "Tools", "UI", "button_sheet_raw.png")
OUT = os.path.join(ROOT, "Assets", "Textures", "UI", "Controls")
ART_PIXEL = 8  # source pixels per art pixel in the AI sheet
SIZE = 11      # art pixels: 4 border + 3 stretch + 4 border
BEVEL = 2
SCALE = 8      # importer: spriteBorder = 4 * SCALE, pixelsPerUnit = 50 * SCALE


def sample_palette(piece):
    """Outline / top / side / bottom / body colours of an AI-drawn button."""
    h, w = piece.shape[:2]
    rgb = piece[..., :3].astype(float)
    mid_x, mid_y = slice(w // 2 - 3, w // 2 + 4), slice(h // 2 - 2, h // 2 + 3)
    return {
        "outline": np.median(rgb[0, mid_x], axis=0),
        "top": np.median(rgb[1, mid_x], axis=0),
        "side": np.median(rgb[mid_y, 1], axis=0),
        "bottom": np.median(rgb[h - 2, mid_x], axis=0),
        "body": np.median(rgb[mid_y, mid_x].reshape(-1, 3), axis=0),
    }


def draw(palette):
    last = SIZE - 1
    out = np.zeros((SIZE, SIZE, 4), float)
    for y in range(SIZE):
        for x in range(SIZE):
            depth = min(x, y, last - x, last - y)
            if depth == 0:
                if (x in (0, last)) and (y in (0, last)):
                    continue  # clipped corner
                key = "outline"
            elif depth > BEVEL:
                key = "body"
            elif y == depth:
                key = "top"
            elif last - y == depth:
                key = "bottom"
            else:
                key = "side"
            out[y, x, :3] = palette[key]
            out[y, x, 3] = 255
    return out


def shade(palette, fn):
    return {k: (v if k == "outline" else np.clip(fn(v), 0, 255)) for k, v in palette.items()}


def states(normal, disabled):
    lighter = shade(normal, lambda c: c + (255 - c) * 0.28)
    pressed = shade(normal, lambda c: c * 0.86)
    # pushed in: shadow falls from the top edge, a thin light catches the bottom
    pressed["top"], pressed["bottom"] = normal["bottom"] * 0.8, normal["body"]
    pressed["side"] = normal["bottom"] * 0.9
    return {"Normal": normal, "Highlighted": lighter, "Pressed": pressed, "Disabled": disabled}


def main():
    sheet = np.array(Image.open(SRC).convert("RGBA"))
    h, w = sheet.shape[0] // 4, sheet.shape[1] // 2
    cell = lambda r, c: slice_piece(sheet[r * h:(r + 1) * h, c * w:(c + 1) * w], ART_PIXEL)
    os.makedirs(OUT, exist_ok=True)
    for col, name in enumerate(("PrimaryButton", "SecondaryButton")):
        palettes = states(sample_palette(cell(0, col)), sample_palette(cell(3, col)))
        for state, palette in palettes.items():
            sprite = draw(palette).round().astype(np.uint8)
            sprite = sprite.repeat(SCALE, axis=0).repeat(SCALE, axis=1)
            Image.fromarray(sprite).save(os.path.join(OUT, f"{name}_{state}.png"))
            print(name, state, {k: [int(x) for x in v] for k, v in palette.items()})


if __name__ == "__main__":
    main()
