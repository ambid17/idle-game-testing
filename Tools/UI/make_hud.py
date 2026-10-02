"""Draws the HUD's panel and slot backgrounds.

Nothing is AI-generated here: like make_panels.py, the pieces are drawn from the palette of the
buttons and scrollbars, since AI-drawn 9-slice art has smeared at odd sizes before.

    HudPanel.png         9-slice backing for the always-on HUD blocks (status bars, stats,
                         minimap frame, critter jar, tooltips): the modal frame's chamfered
                         navy outline and cyan frame, with gold brackets on the corners and a
                         slightly see-through dark body so the mine still reads behind it.
    HudSlot.png          9-slice square slot (ability / power-up slots, key badge): navy
                         outline with clipped corners, muted frame, dark body.
    HudSlot_Current.png  the same slot with a gold frame, for the selected ability.

Sprites are saved upscaled (SCALE texels per art pixel) for bilinear filtering, like the
buttons. Importer: pixelsPerUnit = 50 * SCALE (one art pixel = 2 UI pixels), spriteBorder =
PANEL_BORDER * SCALE for the panel and BORDER * SCALE for the slots.

    python Tools/UI/make_hud.py
"""
import numpy as np

from make_dropdown import OUTLINE, CYAN, CYAN_DARK, draw_frame, save
from make_panels import ROW_FRAME, MODAL_BODY, MODAL_SHADOW, CHAMFER

GOLD = (245, 181, 41)
GOLD_DARK = (208, 133, 21)
SLOT_BODY = (26, 30, 46)
SLOT_SHADOW = (18, 21, 34)

PANEL_SIZE = 17   # art pixels: 7 border + 3 stretch + 7 border
PANEL_BORDER = 7
BRACKET = 5       # how far the gold corner brackets run along each edge
BODY_ALPHA = 236


def draw_panel():
    last = PANEL_SIZE - 1
    out = np.zeros((PANEL_SIZE, PANEL_SIZE, 4), np.uint8)
    for y in range(PANEL_SIZE):
        for x in range(PANEL_SIZE):
            edge = min(x, y, last - x, last - y)
            # distance in from the chamfered corner, so the rings follow the cut
            along = min(x, last - x) + min(y, last - y)
            depth = min(edge, along - CHAMFER)
            if depth < 0:
                continue
            bracket = along <= BRACKET + depth
            if depth == 0:
                out[y, x] = (*OUTLINE, 255)
            elif depth == 1:
                out[y, x] = (*(GOLD if bracket else CYAN_DARK), 255)
            elif depth == 2:
                out[y, x] = (*(GOLD_DARK if bracket else MODAL_SHADOW), 255)
            else:
                out[y, x] = (*MODAL_BODY, BODY_ALPHA)
    return out


if __name__ == "__main__":
    save("HudPanel.png", draw_panel())
    save("HudSlot.png", draw_frame(ROW_FRAME, SLOT_BODY, SLOT_SHADOW))
    save("HudSlot_Current.png", draw_frame(GOLD, SLOT_BODY, SLOT_SHADOW))
