"""Derives the MiningAutomaton's crack stages (DrillCrack_N.png) from the player's (Crack_N.png).

Same crack shapes, but the cyan glow is replaced with a dark warm charcoal so automaton damage
reads as plain fractures rather than the player's energy cracks. The brighter a source pixel was,
the darker it comes out, so the core of the web stays the strongest part.

Run make_crack_mid.py first if the player's stages changed.

Usage: python make_drill_cracks.py <effects_dir>
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

STAGES = 4
FAINT = np.array([52.0, 42.0, 34.0])    # colour of the dimmest source pixels
DEEP = np.array([10.0, 8.0, 7.0])       # colour of the brightest source pixels


def main() -> None:
    effects = Path(sys.argv[1])
    for stage in range(1, STAGES + 1):
        src = np.asarray(Image.open(effects / f"Crack_{stage}.png").convert("RGBA")).astype(float)
        strength = (src[..., :3].max(-1) / 255)[..., None]
        rgb = FAINT + (DEEP - FAINT) * strength
        out = np.dstack([rgb, src[..., 3]]).round().astype(np.uint8)
        Image.fromarray(out, "RGBA").save(effects / f"DrillCrack_{stage}.png")
        print(f"wrote DrillCrack_{stage}.png")


if __name__ == "__main__":
    main()
