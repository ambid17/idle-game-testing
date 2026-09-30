"""Builds the parallax backdrop textures from the generated sheets.

Far: 2x2 opaque sheet -> per biome, flatten the model's radial focal glow (divide out a heavy
blur of luminance), then roll-blend the edges so it tiles both ways.
Mid/Near: per-biome object sheet on a flat blue background -> chroma-key, split into objects,
then scatter them on a torus (wrapping at the edges) into a transparent, seamless tile.
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

# Run from anywhere: python Tools/Backdrops/make_backdrops.py [preview_dir]
HERE = os.path.dirname(os.path.abspath(__file__)) + "/"
ROOT = HERE + "../../Assets/Textures/Backdrops/"
SCRATCH = sys.argv[1] if len(sys.argv) > 1 else HERE
BIOMES = ["Topsoil", "Crystal", "Overgrown", "Void"]
SHEETS = HERE + "sheets/"  # the raw generate_image outputs
# Topsoil's pebbled dirt cubes read as mineable blocks - keep them out of the backdrop.
EXCLUDE = {("Topsoil", 0), ("Topsoil", 3)}


def seamless_blend(a):
    n = a.shape[0]
    r = np.roll(a, (n // 2, n // 2), axis=(0, 1))
    t = np.linspace(0, 1, n)
    w1 = np.clip((1 - np.abs(t * 2 - 1)) * 2.5, 0, 1)
    w = np.minimum.outer(w1, w1)[..., None]
    return a * w + r * (1 - w)


def far_tiles():
    sheet = np.asarray(Image.open(SHEETS + "Backdrop_Far_Sheet.png").convert("RGB")).astype(np.float32)
    h, w = sheet.shape[:2]
    hh, hw = h // 2, w // 2
    inset = 6
    for k, name in enumerate(BIOMES):
        cx, cy = k % 2, k // 2
        q = sheet[cy * hh + inset:(cy + 1) * hh - inset, cx * hw + inset:(cx + 1) * hw - inset]
        lum = q.mean(axis=2)
        low = ndimage.gaussian_filter(lum, sigma=q.shape[0] / 6)
        # Remove the low-frequency glow, keep the silhouettes; target a flat mid-dark level.
        target = np.percentile(lum, 55)
        flat = q * (target / np.maximum(low, 1e-3))[..., None]
        flat = np.clip(flat, 0, 255)
        img = Image.fromarray(flat.astype(np.uint8)).resize((512, 512), Image.NEAREST)
        a = seamless_blend(np.asarray(img).astype(np.float32))
        Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(ROOT + f"Backdrop_{name}_Far.png")


def extract_objects(name):
    im = np.asarray(Image.open(SHEETS + f"Backdrop_Objects_{name}.png").convert("RGB")).astype(np.float32)
    border = np.concatenate([im[:8].reshape(-1, 3), im[-8:].reshape(-1, 3), im[:, :8].reshape(-1, 3), im[:, -8:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    dist = np.linalg.norm(im - bg, axis=2)
    alpha = np.clip((dist - 35) / 30, 0, 1)
    mask = alpha > 0.5
    mask = ndimage.binary_closing(mask, np.ones((3, 3)))
    # Shave the anti-aliased rim, which carries the blue background as a fringe.
    alpha = alpha * ndimage.binary_erosion(mask, iterations=2)
    labels, n = ndimage.label(ndimage.binary_dilation(mask, iterations=6))
    found = []
    for i, sl in enumerate(ndimage.find_objects(labels), start=1):
        region = labels[sl] == i
        a = alpha[sl] * region
        if a.sum() < 1500:  # specks / stray glow pixels
            continue
        rgba = np.dstack([im[sl], a * 255]).astype(np.uint8)
        cy = (sl[0].start + sl[0].stop) / 2
        cx = (sl[1].start + sl[1].stop) / 2
        found.append((int(cy > im.shape[0] / 2), cx, Image.fromarray(rgba, "RGBA")))
    # Sheet order: row-major 3x2 grid.
    found.sort(key=lambda f: (f[0], f[1]))
    objs = [f[2] for i, f in enumerate(found) if (name, i) not in EXCLUDE]
    return objs, bg


def scatter_tile(objs, size, count, scale_range, seed, min_gap):
    rng = np.random.default_rng(seed)
    canvas = np.zeros((size, size, 4), np.float32)
    placed = []
    attempts = 0
    while len(placed) < count and attempts < 2000:
        attempts += 1
        o = objs[rng.integers(len(objs))]
        s = rng.uniform(*scale_range)
        ow, oh = max(1, int(o.width * s)), max(1, int(o.height * s))
        cx, cy = rng.uniform(0, size), rng.uniform(0, size)
        # Toroidal spacing so the tile reads evenly and seams stay invisible.
        ok = True
        for (px, py, pr) in placed:
            dx = min(abs(cx - px), size - abs(cx - px))
            dy = min(abs(cy - py), size - abs(cy - py))
            if (dx * dx + dy * dy) ** 0.5 < (pr + max(ow, oh) / 2) * min_gap:
                ok = False
                break
        if not ok:
            continue
        placed.append((cx, cy, max(ow, oh) / 2))
        oi = o.resize((ow, oh), Image.NEAREST)
        if rng.random() < 0.5:
            oi = oi.transpose(Image.FLIP_LEFT_RIGHT)
        arr = np.asarray(oi).astype(np.float32)
        x0, y0 = int(cx - ow / 2), int(cy - oh / 2)
        # Paste with wraparound (over operator, premultiply-free since canvas starts empty-ish).
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                xa, ya = x0 + ox, y0 + oy
                xs0, ys0 = max(0, xa), max(0, ya)
                xs1, ys1 = min(size, xa + ow), min(size, ya + oh)
                if xs0 >= xs1 or ys0 >= ys1:
                    continue
                src = arr[ys0 - ya:ys1 - ya, xs0 - xa:xs1 - xa]
                dst = canvas[ys0:ys1, xs0:xs1]
                sa = src[..., 3:4] / 255
                da = dst[..., 3:4] / 255
                out_a = sa + da * (1 - sa)
                out_rgb = (src[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(out_a, 1e-6)
                canvas[ys0:ys1, xs0:xs1, :3] = out_rgb
                canvas[ys0:ys1, xs0:xs1, 3:4] = out_a * 255
    return Image.fromarray(np.clip(canvas, 0, 255).astype(np.uint8), "RGBA"), len(placed)


def main():
    far_tiles()
    report = []
    for k, name in enumerate(BIOMES):
        objs, bg = extract_objects(name)
        # Mid: smaller, denser - reads as a crowd of formations deeper in the cave.
        mid, nm = scatter_tile(objs, 2048, 12, (0.85, 1.15), seed=100 + k, min_gap=1.25)
        # Near: bigger, sparse - the odd formation close behind the tunnels.
        near, nn = scatter_tile(objs, 2048, 4, (1.8, 2.3), seed=200 + k, min_gap=1.4)
        mid.save(ROOT + f"Backdrop_{name}_Mid.png")
        near.save(ROOT + f"Backdrop_{name}_Near.png")
        report.append(f"{name}: bg={bg.astype(int)} objects={len(objs)} mid={nm} near={nn}")

    # Preview: per biome, far + mid + near composited, tiled 2x2.
    prev = Image.new("RGB", (1024, 1024))
    for k, name in enumerate(BIOMES):
        far = Image.open(ROOT + f"Backdrop_{name}_Far.png").convert("RGBA").resize((2048, 2048), Image.NEAREST)
        comp = far.copy()
        comp.alpha_composite(Image.open(ROOT + f"Backdrop_{name}_Mid.png"))
        comp.alpha_composite(Image.open(ROOT + f"Backdrop_{name}_Near.png"))
        tile = comp.convert("RGB").resize((512, 512), Image.NEAREST)
        prev.paste(tile, ((k % 2) * 512, (k // 2) * 512))
    prev.save(SCRATCH + "backdrop_preview.png")
    print("\n".join(report))


if __name__ == "__main__":
    main()
