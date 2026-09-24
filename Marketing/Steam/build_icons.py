"""Builds the app/shortcut icons from the in-game robot sprite (no AI calls).
Run from repo root: python Marketing/Steam/build_icons.py"""
from PIL import Image, ImageDraw, ImageFilter
import os

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, 'out')
ROBOT = 'Assets/Textures/Player/RobotMining.png'  # 8-frame horizontal strip
DIAMOND = 'Assets/Textures/Ores/8 diamond.png'


def robot_frame():
    strip = Image.open(ROBOT).convert('RGBA')
    fw = strip.width // 8
    f = strip.crop((0, 0, fw, strip.height))
    return f.crop(f.getbbox())


def build_icon(size=1024):
    s = size
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))

    # rounded-square plate: deep purple with a cyan core glow
    plate = Image.new('L', (s, s), 0)
    ImageDraw.Draw(plate).rounded_rectangle((0, 0, s - 1, s - 1), radius=int(s * 0.2), fill=255)
    base = Image.new('RGBA', (s, s), (22, 12, 44, 255))
    glow = Image.new('L', (s, s), 0)
    ImageDraw.Draw(glow).ellipse((s * 0.12, s * 0.14, s * 0.88, s * 0.9), fill=150)
    glow = glow.filter(ImageFilter.GaussianBlur(s * 0.12))
    base.paste((40, 170, 220, 255), (0, 0), glow)

    # faint diamond-ore texture in the lower third for "mine" context
    ore = Image.open(DIAMOND).convert('RGBA').resize((s, s), Image.NEAREST)
    fade = Image.linear_gradient('L').resize((s, s)).point(lambda v: int(max(v - 110, 0) * 0.55))
    base.paste(ore, (0, 0), fade)

    img.paste(base, (0, 0), plate)

    # neon rim
    rim = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(rim).rounded_rectangle((s * 0.015, s * 0.015, s * 0.985, s * 0.985),
                                          radius=int(s * 0.19), outline=(70, 235, 255, 255), width=max(2, s // 64))
    img.alpha_composite(rim.filter(ImageFilter.GaussianBlur(s / 256)))

    # robot, integer-scaled with nearest for crisp pixels
    bot = robot_frame()
    scale = max(1, int(s * 0.86 / bot.width))
    bot = bot.resize((bot.width * scale, bot.height * scale), Image.NEAREST)
    shadow = Image.new('RGBA', bot.size, (5, 0, 15, 0))
    shadow.putalpha(bot.getchannel('A').point(lambda v: int(v * 0.7)))
    shadow = shadow.filter(ImageFilter.GaussianBlur(s / 80))
    x, y = (s - bot.width) // 2, int(s * 0.52 - bot.height / 2)
    img.alpha_composite(shadow, (x + s // 60, y + s // 40))
    img.alpha_composite(bot, (x, y))
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    icon = build_icon(1024)
    icon.save(os.path.join(OUT, 'app_icon_1024.png'))
    # Steam client/shortcut icon (.ico, multi-res)
    icon.save(os.path.join(OUT, 'client_icon.ico'), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    # Steam community icon: 184x184 JPG, opaque
    comm = Image.new('RGB', (1024, 1024), (22, 12, 44))
    comm.paste(icon, (0, 0), icon)
    comm.resize((184, 184), Image.LANCZOS).save(os.path.join(OUT, 'community_icon_184x184.jpg'), quality=95)
    for n in (256, 32, 16):
        icon.resize((n, n), Image.LANCZOS).save(os.path.join(OUT, f'app_icon_{n}.png'))
    print('done')


if __name__ == '__main__':
    main()
