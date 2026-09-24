"""Builds Steam store graphics from the two AI key-art masters + a locally rendered Orbitron logo.
Run from repo root: python Marketing/Steam/build_steam_assets.py"""
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops
import os

ROOT = os.path.dirname(os.path.abspath(__file__))
FONT = 'Assets/Fonts/Orbitron-Regular.ttf'
WIDE = os.path.join(ROOT, 'source', 'SteamKeyArt_Wide.png')
TALL = os.path.join(ROOT, 'source', 'SteamKeyArt_Tall.png')
OUT = os.path.join(ROOT, 'out')
TITLE = 'DRILLIONAIRE'


def render_logo(width):
    """Neon Orbitron wordmark on transparent bg, fitted to `width` px."""
    size = 400
    font = ImageFont.truetype(FONT, size)
    # Orbitron-Regular is thin; fake a heavier weight with a stroke.
    stroke = size // 60
    l, t, r, b = font.getbbox(TITLE, stroke_width=stroke)
    pad = size // 2
    w, h = r - l + pad * 2, b - t + pad * 2
    org = (pad - l, pad - t)

    mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask).text(org, TITLE, font=font, fill=255, stroke_width=stroke, stroke_fill=255)

    # vertical gold -> cyan gradient fill
    grad = Image.new('RGBA', (1, h))
    top, mid, bot = (255, 226, 120), (255, 170, 60), (60, 235, 255)
    y0, y1 = pad, h - pad
    for y in range(h):
        f = min(max((y - y0) / max(y1 - y0, 1), 0), 1)
        a, c, f2 = (top, mid, f / 0.55) if f < 0.55 else (mid, bot, (f - 0.55) / 0.45)
        grad.putpixel((0, y), tuple(int(a[i] + (c[i] - a[i]) * f2) for i in range(3)) + (255,))
    grad = grad.resize((w, h))

    # dark outline + outer neon glow
    outline = mask.filter(ImageFilter.MaxFilter(size // 22 * 2 + 1))
    glow = mask.filter(ImageFilter.MaxFilter(size // 8 * 2 + 1)).filter(ImageFilter.GaussianBlur(size // 7))

    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    img.paste((40, 220, 255, 255), (0, 0), glow.point(lambda v: int(v * 0.85)))
    img.paste((14, 10, 30, 255), (0, 0), outline)
    img.paste(grad, (0, 0), mask)
    # thin inner highlight line on top edge
    hl = ImageChops.subtract(mask, mask.transform(mask.size, Image.AFFINE, (1, 0, 0, 0, 1, -size // 40)))
    img.paste((255, 255, 240, 255), (0, 0), hl.point(lambda v: v // 2))

    img = img.crop(img.getbbox())
    return img.resize((width, round(img.height * width / img.width)), Image.LANCZOS)


def cover(img, w, h, fx=0.5, fy=0.5):
    """Scale to cover w x h, then crop around focal point (fx, fy)."""
    s = max(w / img.width, h / img.height)
    im = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    x = int(min(max(im.width * fx - w / 2, 0), im.width - w))
    y = int(min(max(im.height * fy - h / 2, 0), im.height - h))
    return im.crop((x, y, x + w, y + h))


def with_logo(bg, logo_w_frac, cx_frac, cy_frac):
    bg = bg.convert('RGBA')
    logo = render_logo(int(bg.width * logo_w_frac))
    x = int(bg.width * cx_frac - logo.width / 2)
    y = int(bg.height * cy_frac - logo.height / 2)
    # soft dark scrim behind the wordmark so it reads over busy art
    scrim = Image.new('L', bg.size, 0)
    ImageDraw.Draw(scrim).ellipse((x - logo.width * 0.12, y - logo.height * 0.5,
                                   x + logo.width * 1.12, y + logo.height * 1.5), fill=170)
    scrim = scrim.filter(ImageFilter.GaussianBlur(logo.height * 0.6))
    bg.paste((10, 6, 22, 255), (0, 0), scrim)
    bg.alpha_composite(logo, (x, y))
    return bg.convert('RGB')


# name: (source, w, h, focus(fx, fy), logo(width_frac, cx, cy) or None)
LAYOUT = {
    'header_capsule_920x430.png':   ('wide', 920, 430, (0.5, 0.29), (0.52, 0.30, 0.80)),
    'small_capsule_462x174.png':    ('wide', 462, 174, (0.5, 0.30), (0.86, 0.50, 0.50)),
    'main_capsule_1232x706.png':    ('wide', 1232, 706, (0.5, 0.30), (0.50, 0.29, 0.83)),
    'vertical_capsule_748x896.png': ('tall', 748, 896, (0.5, 0.5), (0.86, 0.50, 0.89)),
    'library_capsule_600x900.png':  ('tall', 600, 900, (0.5, 0.5), (0.88, 0.50, 0.89)),
    'library_hero_3840x1240.png':   ('wide', 3840, 1240, (0.5, 0.45), None),
}


def page_background(src):
    """Steam recommends a subdued, low-contrast page background."""
    bg = cover(src, 1438, 810, 0.5, 0.35).filter(ImageFilter.GaussianBlur(3))
    dark = Image.new('RGB', bg.size, (12, 8, 24))
    return Image.blend(bg, dark, 0.6)


def main():
    os.makedirs(OUT, exist_ok=True)
    srcs = {'wide': Image.open(WIDE).convert('RGB'), 'tall': Image.open(TALL).convert('RGB')}
    for name, (src, w, h, focus, logo) in LAYOUT.items():
        img = cover(srcs[src], w, h, *focus)
        if logo:
            img = with_logo(img, *logo)
        img.save(os.path.join(OUT, name))
        print(name, img.size)

    page_background(srcs['wide']).save(os.path.join(OUT, 'page_background_1438x810.png'))

    logo = render_logo(1180)
    canvas = Image.new('RGBA', (1280, 720), (0, 0, 0, 0))
    canvas.alpha_composite(logo, ((1280 - logo.width) // 2, (720 - logo.height) // 2))
    canvas.save(os.path.join(OUT, 'library_logo_1280x720.png'))


if __name__ == '__main__':
    main()
