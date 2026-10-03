"""Greyscale copy of the Keystone sprite (SkyArtifact.png) for chambers whose Keystone was taken
in an earlier Dig - Story.SealChamber shows it faded in place of the live stone.

Run from the repo root:  python Tools/Story/make_keystone_taken.py
"""
from PIL import Image

SRC = "Assets/Textures/World/SkyArtifact.png"
DST = "Assets/Textures/Story/KeystoneTaken.png"

img = Image.open(SRC).convert("RGBA")
grey = img.convert("L")
# Flatten contrast a little so the dead stone reads as dull stone, not a lit silhouette.
grey = grey.point(lambda v: int(60 + v * 0.6))
out = Image.merge("RGBA", (grey, grey, grey, img.getchannel("A")))
out.save(DST)
print(f"wrote {DST} {out.size}")
