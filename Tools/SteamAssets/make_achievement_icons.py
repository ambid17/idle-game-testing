"""Converts generated achievement art into Steam-ready 256x256 JPGs.

For each source PNG named after its Steam API name (e.g. ACH_FIRST_BLOCK.png) this writes:
  <API_NAME>.jpg         - achieved icon
  <API_NAME>_locked.jpg  - unachieved icon (desaturated + darkened)

Usage: python make_achievement_icons.py <source_dir> <output_dir> [contact_sheet.png]
"""
import sys
from pathlib import Path

from PIL import Image, ImageEnhance, ImageOps

SIZE = 256
JPG_QUALITY = 95
LOCKED_BRIGHTNESS = 0.55
LOCKED_CONTRAST = 0.85


def make_locked(icon: Image.Image) -> Image.Image:
    gray = ImageOps.grayscale(icon).convert("RGB")
    gray = ImageEnhance.Contrast(gray).enhance(LOCKED_CONTRAST)
    return ImageEnhance.Brightness(gray).enhance(LOCKED_BRIGHTNESS)


def main() -> None:
    src_dir, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
    out_dir.mkdir(parents=True, exist_ok=True)

    pairs = []
    for src in sorted(src_dir.glob("ACH_*.png")):
        icon = ImageOps.fit(Image.open(src).convert("RGB"), (SIZE, SIZE), Image.LANCZOS)
        locked = make_locked(icon)
        icon.save(out_dir / f"{src.stem}.jpg", quality=JPG_QUALITY)
        locked.save(out_dir / f"{src.stem}_locked.jpg", quality=JPG_QUALITY)
        pairs.append((icon, locked))
        print(f"wrote {src.stem}")

    if len(sys.argv) > 3 and pairs:
        cols = 5
        rows = (len(pairs) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * SIZE, rows * SIZE * 2), "black")
        for i, (icon, locked) in enumerate(pairs):
            x, y = (i % cols) * SIZE, (i // cols) * SIZE * 2
            sheet.paste(icon, (x, y))
            sheet.paste(locked, (x, y + SIZE))
        sheet.save(sys.argv[3])


if __name__ == "__main__":
    main()
