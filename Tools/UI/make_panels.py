"""Draws the list-row, modal-frame and run-modifier card sprites.

Nothing is AI-generated here: like make_dropdown.py, the pieces are drawn from the palette of
the buttons and scrollbars, since AI-drawn 9-slice art has smeared at odd sizes before.

    ListRow_{Normal,Highlighted,Pressed}.png  9-slice selectable list row (goods exchange,
                          recipe list): navy outline, muted frame that turns cyan on hover.
    ListRow_Selected.png  9-slice overlay for the chosen row: bright cyan frame, teal body.
    ModalFrame.png        9-slice box for modals: chamfered navy outline, cyan frame with
                          gold brackets on the corners, inner shadow line, dark body.

    RunModifierCard_{Blessing,Gamble}_{Normal,Highlighted}.png  9-slice offer card: the
                          modal frame's chamfered shape in the kind's colour (cyan blessing,
                          gold gamble) with a solid header band for the kind label. Top
                          spriteBorder = (CARD_HEADER + 2) * SCALE (header, rule and shadow row), the rest MODAL_BORDER * SCALE.

Sprites are saved upscaled (SCALE texels per art pixel) for bilinear filtering, like the
buttons. Importer: pixelsPerUnit = 50 * SCALE (one art pixel = 2 UI pixels), spriteBorder =
ROW_BORDER * SCALE for the rows and MODAL_BORDER * SCALE for the modal frame.

    python Tools/UI/make_panels.py
"""
import numpy as np

from make_dropdown import OUTLINE, TROUGH, LIST_BODY, CYAN, CYAN_DARK, CYAN_LIGHT, draw_frame, lighten, save

ROW_FRAME = (70, 80, 104)
SELECTED_BODY = (14, 92, 104)
MODAL_BODY = (30, 34, 50)
MODAL_SHADOW = (20, 23, 38)
GOLD = (245, 181, 41)
GOLD_DARK = (208, 133, 21)
ROW_BORDER = 4
MODAL_SIZE = 17   # art pixels: 7 border + 3 stretch + 7 border
MODAL_BORDER = 7
CHAMFER = 2
BRACKET = 5       # how far the gold corner brackets run along each edge

def draw_modal():
    last = MODAL_SIZE - 1
    out = np.zeros((MODAL_SIZE, MODAL_SIZE, 4), np.uint8)
    for y in range(MODAL_SIZE):
        for x in range(MODAL_SIZE):
            edge = min(x, y, last - x, last - y)
            # distance in from the chamfered corner, so the rings follow the cut
            along = min(x, last - x) + min(y, last - y)
            depth = min(edge, along - CHAMFER)
            if depth < 0:
                continue
            rings = (OUTLINE, GOLD, GOLD_DARK) if along <= BRACKET + depth else (OUTLINE, CYAN_DARK, CYAN)
            colour = rings[depth] if depth < 3 else MODAL_SHADOW if depth == 3 else MODAL_BODY
            out[y, x] = (*colour, 255)
    return out


CARD_HEADER = 28   # art pixels of header band (56 UI px), holds the kind label
CARD_KINDS = {
    # frame, bright ring + header band, light (hover ring), body, body shadow
    "Blessing": (CYAN_DARK, CYAN, CYAN_LIGHT, (20, 48, 56), (14, 34, 42)),
    "Gamble": ((202, 152, 39), (235, 177, 45), (253, 226, 138), (58, 36, 24), (40, 24, 16)),
}


def draw_card(frame, band, ring, body, shadow):
    width, height = MODAL_SIZE, CARD_HEADER + 1 + 3 + MODAL_BORDER
    out = np.zeros((height, width, 4), np.uint8)
    for y in range(height):
        for x in range(width):
            edge = min(x, y, width - 1 - x, height - 1 - y)
            corner = min(x, width - 1 - x) + min(y, height - 1 - y) - CHAMFER
            depth = min(edge, corner)
            if depth < 0:
                continue
            if depth == 0:
                colour = OUTLINE
            elif depth == 1:
                colour = frame
            elif y < CARD_HEADER:
                colour = ring if depth == 2 and y == 2 else band  # top highlight row, then band
            elif y == CARD_HEADER:
                colour = OUTLINE  # rule under the header
            elif depth == 2:
                colour = ring
            elif depth == 3 or y == CARD_HEADER + 1:
                colour = shadow
            else:
                colour = body
            out[y, x] = (*colour, 255)
    return out


if __name__ == "__main__":
    save("ListRow_Normal.png", draw_frame(ROW_FRAME, LIST_BODY, LIST_BODY))
    save("ListRow_Highlighted.png", draw_frame(CYAN_DARK, lighten(LIST_BODY, 0.1), lighten(LIST_BODY, 0.1)))
    save("ListRow_Pressed.png", draw_frame(CYAN, TROUGH, LIST_BODY))
    save("ListRow_Selected.png", draw_frame(CYAN_LIGHT, SELECTED_BODY, SELECTED_BODY))
    save("ModalFrame.png", draw_modal())
    for kind, (frame, band, light, body, shadow) in CARD_KINDS.items():
        save(f"RunModifierCard_{kind}_Normal.png", draw_card(frame, band, band, body, shadow))
        save(f"RunModifierCard_{kind}_Highlighted.png", draw_card(band, band, light, lighten(body, 0.12), body))
