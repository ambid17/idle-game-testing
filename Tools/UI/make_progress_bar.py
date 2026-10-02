"""Draws the Processing Center queue slot's progress bar sprites.

Nothing is AI-generated here: both pieces are drawn from the same palette as the buttons and
scrollbars (slice_button_sheet.py / slice_scroll_slider_sheet.py), since AI-drawn 9-slice art
has smeared at odd sizes before.

    ProgressBarTrack.png  9-slice trough: navy outline with clipped corners, inner top shadow.
    ProgressBarFill.png   cyan bar with diagonal stripes, a highlight row and a shadow row.
                          Used as a horizontally Filled image, so it is a full-width strip
                          rather than a 9-slice.

Sprites are saved upscaled (SCALE texels per art pixel) for bilinear filtering, like the
buttons. Importer: pixelsPerUnit = 50 * SCALE (one art pixel = 2 UI pixels), track
spriteBorder = TRACK_BORDER * SCALE.

    python Tools/UI/make_progress_bar.py
"""
import os
import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Textures", "UI", "Controls")
SCALE = 8

OUTLINE = (23, 28, 55)
TROUGH = (59, 66, 82)
TROUGH_SHADOW = (45, 51, 69)
FILL = (27, 222, 224)
FILL_STRIPE = (11, 172, 177)
FILL_HIGHLIGHT = (170, 252, 250)
FILL_SHADOW = (10, 164, 168)

TRACK_SIZE = 11   # art pixels: 4 border + 3 stretch + 4 border
TRACK_BORDER = 4
FILL_WIDTH = 96   # art pixels; the bar is 200 UI px wide with a 4px inset each side
FILL_HEIGHT = 8
STRIPE_PERIOD = 8
STRIPE_WIDTH = 3


def draw_track():
    last = TRACK_SIZE - 1
    out = np.zeros((TRACK_SIZE, TRACK_SIZE, 4), np.uint8)
    for y in range(TRACK_SIZE):
        for x in range(TRACK_SIZE):
            depth = min(x, y, last - x, last - y)
            if depth == 0:
                if x in (0, last) and y in (0, last):
                    continue  # clipped corner
                colour = OUTLINE
            elif y == 1:
                colour = TROUGH_SHADOW
            else:
                colour = TROUGH
            out[y, x] = (*colour, 255)
    return out


def draw_fill():
    out = np.zeros((FILL_HEIGHT, FILL_WIDTH, 4), np.uint8)
    for y in range(FILL_HEIGHT):
        for x in range(FILL_WIDTH):
            if y == 0:
                colour = FILL_HIGHLIGHT
            elif y == FILL_HEIGHT - 1:
                colour = FILL_SHADOW
            elif (x + y) % STRIPE_PERIOD < STRIPE_WIDTH:
                colour = FILL_STRIPE  # leans forward, in the direction the bar fills
            else:
                colour = FILL
            out[y, x] = (*colour, 255)
    return out


def save(name, art):
    image = Image.fromarray(art, "RGBA")
    image = image.resize((image.width * SCALE, image.height * SCALE), Image.NEAREST)
    path = os.path.join(OUT, name)
    image.save(path)
    print(f"{path}  {image.width}x{image.height}")


if __name__ == "__main__":
    save("ProgressBarTrack.png", draw_track())
    save("ProgressBarFill.png", draw_fill())
