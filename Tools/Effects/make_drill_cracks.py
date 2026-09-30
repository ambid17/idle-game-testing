"""Slices the AI-generated 2x2 drill-damage sheet into the MiningAutomaton's crack stages.

Source: a 1024x1024 sheet (4 panels, reading order = stage 1..4) on a flat magenta chroma-key
background (generate_image's transparent flag only fakes a checkerboard). Each panel is
keyed to real alpha, magenta spill in the art is neutralised, recentred on its content, and
box-downscaled to the same 128x128 / 128 PPU footprint as Assets/Textures/Effects/Crack_N.png.

Usage: python make_drill_cracks.py <sheet.png> <output_dir>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT_SIZE = 128
KEY_LO, KEY_HI = 40, 90       # colour distance from bg: <=LO fully transparent, >=HI opaque
ALPHA_CUTOFF = 0.45           # hard alpha after downscale, keeps crisp pixel-art edges
MAGENTA_SPILL = 12            # min(r,b) - g above this = magenta-contaminated pixel


def key_panel(panel: np.ndarray, bg: np.ndarray) -> np.ndarray:
    dist = np.sqrt(((panel - bg) ** 2).sum(-1))
    alpha = np.clip((dist - KEY_LO) / (KEY_HI - KEY_LO), 0, 1)
    rgb = panel.copy()
    edge = (alpha > 0) & (alpha < 1)
    rgb[edge] = np.clip((panel[edge] - bg * (1 - alpha[edge, None])) / alpha[edge, None], 0, 255)

    # Magenta-tinted pixels (the generator shaded the stage-1 scuff with the bg colour) become
    # the same dark warm grey as the rest of the gouges.
    spill = np.minimum(rgb[..., 0], rgb[..., 2]) - rgb[..., 1] > MAGENTA_SPILL
    lum = rgb[spill].mean(-1, keepdims=True) * 0.6
    rgb[spill] = lum * np.array([1.0, 0.85, 0.7])
    return np.dstack([rgb, alpha * 255])


def main() -> None:
    sheet = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(float)
    out_dir = Path(sys.argv[2])
    h, w, _ = sheet.shape
    border = np.concatenate([sheet[:8].reshape(-1, 3), sheet[-8:].reshape(-1, 3),
                             sheet[:, :8].reshape(-1, 3), sheet[:, -8:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    ph, pw = h // 2, w // 2

    for stage in range(4):
        r, c = divmod(stage, 2)
        rgba = key_panel(sheet[r * ph:(r + 1) * ph, c * pw:(c + 1) * pw], bg)
        img = Image.fromarray(rgba.astype(np.uint8), "RGBA")
        bbox = img.getchannel("A").point(lambda v: 255 if v > 20 else 0).getbbox()
        content = img.crop(bbox)
        canvas = Image.new("RGBA", (pw, ph), (0, 0, 0, 0))
        canvas.paste(content, ((pw - content.width) // 2, (ph - content.height) // 2))

        small = np.asarray(canvas.resize((OUT_SIZE, OUT_SIZE), Image.BOX)).astype(float)
        a = small[..., 3] / 255
        solid = a >= ALPHA_CUTOFF
        small[..., 3] = np.where(solid, 255, 0)
        Image.fromarray(small.astype(np.uint8), "RGBA").save(out_dir / f"DrillCrack_{stage + 1}.png")
        print(f"wrote DrillCrack_{stage + 1}.png bbox={bbox}")


if __name__ == "__main__":
    main()
