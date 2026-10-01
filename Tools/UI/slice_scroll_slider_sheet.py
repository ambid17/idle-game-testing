"""Slices the AI-generated scrollbar/slider sheet (2x2 on magenta) into 9-sliceable sprites.

Each piece is chroma-keyed, cropped, and resampled onto its art-pixel grid (one output
pixel per art pixel) so the sprites stay crisp with point filtering.

    python Tools/UI/slice_scroll_slider_sheet.py
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "Tools", "UI", "scroll_slider_sheet_raw.png")
OUT = os.path.join(ROOT, "Assets", "Textures", "UI", "Controls")
ART_PIXEL = 16  # source pixels per art pixel
NAMES = [["ScrollbarTrack", "ScrollbarHandle"], ["SliderFill", "SliderKnob"]]
# 9-slice border per sprite (must match the importer's spriteBorder); the knob isn't sliced
BORDERS = {"ScrollbarTrack": 7, "ScrollbarHandle": 6, "SliderFill": 4}


def slice_piece(quad):
    rgb = quad[..., :3].astype(int)
    solid = ~((rgb[..., 0] > 150) & (rgb[..., 1] < 90))  # magenta bg + its darker drop shadow
    ys, xs = np.where(solid)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    rows = round((y1 - y0) / ART_PIXEL)
    cols = round((x1 - x0) / ART_PIXEL)
    ch, cw = (y1 - y0) / rows, (x1 - x0) / cols
    out = np.zeros((rows, cols, 4), np.uint8)
    for r in range(rows):
        for c in range(cols):
            ya, yb = int(y0 + r * ch), int(y0 + (r + 1) * ch)
            xa, xb = int(x0 + c * cw), int(x0 + (c + 1) * cw)
            if solid[ya:yb, xa:xb].mean() < 0.5:
                continue
            # median of the cell's inner half dodges the blurry cell edges
            my, mx = (yb - ya) // 4, (xb - xa) // 4
            inner = rgb[ya + my:yb - my, xa + mx:xb - mx].reshape(-1, 3)
            color = np.median(inner, axis=0)
            if color[0] > 150 and color[1] < 90:
                continue  # shadow cell that only partly overlaps the piece
            out[r, c, :3] = color
            out[r, c, 3] = 255
    return out


def make_sliceable(piece, b):
    """Makes everything inside the border uniform along its stretch axis.

    Any corner detail left in a stretched strip gets smeared across the whole bar,
    so each edge strip repeats its middle line and the center is one flat colour.
    """
    h, w = piece.shape[:2]
    piece[:, b:w - b] = piece[:, w // 2][:, None]
    piece[b:h - b, :] = piece[h // 2, :][None, :]


def main():
    sheet = np.array(Image.open(SRC).convert("RGBA"))
    h, w = sheet.shape[0] // 2, sheet.shape[1] // 2
    os.makedirs(OUT, exist_ok=True)
    for r in range(2):
        for c in range(2):
            piece = slice_piece(sheet[r * h:(r + 1) * h, c * w:(c + 1) * w])
            if NAMES[r][c] == "SliderFill":
                # drop the AI's stray second band so the fill 9-slices with a 2px border
                piece[2:7, 1:-1] = piece[12, 1:-1]
            if NAMES[r][c] in BORDERS:
                make_sliceable(piece, BORDERS[NAMES[r][c]])
            Image.fromarray(piece).save(os.path.join(OUT, NAMES[r][c] + ".png"))
            print(NAMES[r][c], piece.shape[1], "x", piece.shape[0])


if __name__ == "__main__":
    main()
