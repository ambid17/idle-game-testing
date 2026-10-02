"""Cuts the AI-generated crusher sheet (source/crusher_sheet_raw.png - OpenRouter via UnityMCP
generate_image, market.png as the style reference) into the Crusher Room's three sprites:
the Crusher block tile, the piston head and the stretchable piston shaft.

    python Tools/Structures/make_crusher_art.py
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[2]
SHEET = Path(__file__).resolve().parent / "source/crusher_sheet_raw.png"
ORES = ROOT / "Assets/Textures/Ores"
HAZARDS = ROOT / "Assets/Textures/Hazards"

SIZE = 128
# Quadrant interiors of the 1024 sheet (inside the magenta gutters).
QUADS = {"block": (70, 70, 494, 494), "head": (530, 70, 954, 494), "shaft": (70, 530, 494, 954)}
BACKGROUND_LUMA = 26
HEAD_WIDTH = 108   # of 128 - CrusherPiston.blockerWidthCells matches this
SHAFT_WIDTH = 30


def cut(name):
    """The quadrant's subject, cropped to its bounds, with the black backdrop made transparent.
    Only backdrop connected to the quadrant edge is keyed, so dark outlines and recesses stay."""
    quad = Image.open(SHEET).convert("RGBA").crop(QUADS[name])
    a = np.asarray(quad).copy()
    dark = a[..., :3].max(axis=2) <= BACKGROUND_LUMA
    labels, _ = ndimage.label(dark)
    edge = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    backdrop = np.isin(labels, edge[edge != 0])
    a[backdrop] = 0
    ys, xs = np.where(~backdrop)
    return Image.fromarray(a, "RGBA").crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))


def crisp(img, size):
    """Downscale, then snap alpha to on/off so edges stay hard like the rest of the pixel art."""
    out = np.asarray(img.resize(size, Image.BOX)).copy()
    out[..., 3] = np.where(out[..., 3] >= 128, 255, 0)
    out[out[..., 3] == 0] = 0
    return Image.fromarray(out, "RGBA")


def block():
    """Full opaque cell - anything the key removed inside the square is dark recess."""
    img = crisp(cut("block"), (SIZE, SIZE))
    tile = Image.new("RGBA", (SIZE, SIZE), (22, 20, 24, 255))
    tile.alpha_composite(img)
    return tile


def head():
    """Bottom-aligned so the spike tips sit on the cell's lower edge."""
    src = cut("head")
    h = round(src.height * HEAD_WIDTH / src.width)
    img = crisp(src, (HEAD_WIDTH, h))
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.alpha_composite(img, ((SIZE - HEAD_WIDTH) // 2, SIZE - h))
    return out


def shaft():
    """One row from the rod's plain middle, repeated - it's stretched vertically in game."""
    src = cut("shaft")
    row = src.crop((0, src.height // 2, src.width, src.height // 2 + 1))
    img = crisp(row.resize((src.width, 8), Image.NEAREST), (SHAFT_WIDTH, SIZE))
    out = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    out.alpha_composite(img, ((SIZE - SHAFT_WIDTH) // 2, 0))
    return out


def main():
    outputs = {
        ORES / "34 crusher.png": block(),
        HAZARDS / "crusherHead.png": head(),
        HAZARDS / "crusherShaft.png": shaft(),
    }
    for path, img in outputs.items():
        img.save(path)
        print(f"wrote {path}")


if __name__ == "__main__":
    main()
