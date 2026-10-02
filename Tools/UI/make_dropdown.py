"""Draws the Options dropdown (TMP_Dropdown) sprites.

Nothing is AI-generated here: like make_progress_bar.py, the pieces are drawn from the palette
of the buttons and scrollbars, since AI-drawn 9-slice art has smeared at odd sizes before.

    DropdownField_{Normal,Highlighted,Pressed}.png  9-slice closed field: navy outline with
                          clipped corners, a 1px cyan frame, dark trough with a top shadow.
    DropdownList.png      9-slice panel behind the open option list (same frame, darker body).
    DropdownArrow.png     cyan down chevron with a navy outline.
    DropdownCheck.png     pale cyan tick with a navy outline, marks the current option.

Sprites are saved upscaled (SCALE texels per art pixel) for bilinear filtering, like the
buttons. Importer: pixelsPerUnit = 50 * SCALE (one art pixel = 2 UI pixels), 9-slice
spriteBorder = BORDER * SCALE.

    python Tools/UI/make_dropdown.py
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
LIST_BODY = (38, 44, 66)
CYAN = (27, 222, 224)
CYAN_DARK = (11, 172, 177)
CYAN_LIGHT = (170, 252, 250)

SIZE = 11   # art pixels: 4 border + 3 stretch + 4 border
BORDER = 4

ARROW = [
    ".#######.",
    "#ooooooo#",
    ".#ooooo#.",
    "..#ooo#..",
    "...#o#...",
    "....#....",
]

CHECK = [
    ".......##.",
    "......#oo#",
    ".....#oo#.",
    ".##.#oo#..",
    "#oo#oo#...",
    ".#ooo#....",
    "..#o#.....",
    "...#......",
]


def draw_frame(frame, body, shadow):
    last = SIZE - 1
    out = np.zeros((SIZE, SIZE, 4), np.uint8)
    for y in range(SIZE):
        for x in range(SIZE):
            depth = min(x, y, last - x, last - y)
            if depth == 0:
                if x in (0, last) and y in (0, last):
                    continue  # clipped corner
                colour = OUTLINE
            elif depth == 1:
                colour = frame
            elif y == 2:
                colour = shadow
            else:
                colour = body
            out[y, x] = (*colour, 255)
    return out


def draw_glyph(rows, fill):
    out = np.zeros((len(rows), len(rows[0]), 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "#":
                out[y, x] = (*OUTLINE, 255)
            elif ch == "o":
                out[y, x] = (*fill, 255)
    return out


def lighten(colour, amount):
    return tuple(int(round(c + (255 - c) * amount)) for c in colour)


def save(name, art):
    image = Image.fromarray(art, "RGBA")
    image = image.resize((image.width * SCALE, image.height * SCALE), Image.NEAREST)
    path = os.path.join(OUT, name)
    image.save(path)
    print(f"{path}  {image.width}x{image.height}")


if __name__ == "__main__":
    save("DropdownField_Normal.png", draw_frame(CYAN_DARK, TROUGH, TROUGH_SHADOW))
    save("DropdownField_Highlighted.png", draw_frame(CYAN_LIGHT, lighten(TROUGH, 0.12), TROUGH))
    save("DropdownField_Pressed.png", draw_frame(CYAN, TROUGH_SHADOW, LIST_BODY))
    save("DropdownList.png", draw_frame(CYAN_DARK, LIST_BODY, LIST_BODY))
    save("DropdownArrow.png", draw_glyph(ARROW, CYAN))
    save("DropdownCheck.png", draw_glyph(CHECK, CYAN_LIGHT))
