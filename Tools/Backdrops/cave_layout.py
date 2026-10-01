"""Composes a biome's mid/near parallax tile as a cave set instead of a random scatter.

The tile is split into horizontal bands (BANDS per plane - must match BackdropPlane.Bands on
the BiomeBackdrop assets). Every band holds one or two rock ledges jutting from the back wall,
drawn procedurally in the biome's palette at the art's own pixel scale. Each formation from the
sheet has a role:
- floor: stands on a ledge, its base tucked behind the ledge's lip;
- hang: hangs under a ledge, its top (usually a chunk of rock) buried in the ledge's underside.

So nothing floats. Nothing crosses a band edge either, and the tile's rows at the band edges stay
empty. ParallaxBackdrop snaps each biome boundary to a band edge, so the cut between two biomes
never slices a formation in half.
"""
import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree

SIZE = 2048
# Bands per tile; ParallaxBackdrop snaps biome cuts to these, so keep in sync with the assets.
BANDS = {"Mid": 3, "Near": 2}
# Texture pixels per art pixel: the sheet art is ~4 px per art pixel at 1x.
ART_PX = {"Mid": 4, "Near": 8}
# Formation scale (sheet px -> tile px) - near formations ~2x mid so the planes read as depths.
OBJ_SCALE = {"Mid": 1.0, "Near": 2.0}
# Ledges per band (min, max) and ledge length in art px.
LEDGES_PER_BAND = {"Mid": (1, 2), "Near": (1, 1)}
# Bare ledge either side of its formations, in art px.
EXTRA_LENGTH = {"Mid": (24, 56), "Near": (14, 30)}
# Ledge body thickness range in art px (before the underside's rock masses).
THICK = {"Mid": (7, 12), "Near": (6, 10)}

# Sheet index -> (role, fraction of the object's height buried in the ledge for hangers).
ROLES = {
    "Topsoil": {1: ("hang", 0.2), 2: ("hang", 0.18), 4: ("floor", 0), 5: ("hang", 0.2)},
    "Crystal": {0: ("floor", 0), 1: ("floor", 0), 2: ("hang", 0.14), 3: ("floor", 0),
                4: ("hang", 0.28), 5: ("floor", 0)},
    "Overgrown": {0: ("hang", 0.28), 1: ("floor", 0), 2: ("hang", 0.24), 3: ("hang", 0.14),
                  4: ("hang", 0.28), 5: ("floor", 0)},
    "Void": {0: ("floor", 0), 1: ("floor", 0), 2: ("floor", 0), 3: ("floor", 0),
             4: ("hang", 0.26), 5: ("floor", 0), 6: ("floor", 0)},
}

# Rock tones dark -> light (outline, shadow, base, lit, highlight), sampled from each sheet's own
# rock (Topsoil spire, Crystal rock pile, Overgrown pillar, Void monolith).
ROCK = {
    "Topsoil": [(51, 36, 38), (100, 54, 48), (132, 74, 60), (162, 96, 76), (192, 132, 106)],
    "Crystal": [(22, 27, 56), (40, 55, 92), (55, 78, 118), (75, 108, 148), (100, 148, 182)],
    "Overgrown": [(22, 24, 34), (42, 44, 54), (60, 62, 70), (80, 83, 89), (110, 112, 118)],
    "Void": [(16, 15, 30), (44, 43, 68), (62, 61, 87), (82, 83, 106), (112, 114, 134)],
}
MOSS = [(36, 56, 33), (55, 81, 42), (78, 114, 47), (100, 143, 52), (131, 180, 67)]
ROOT_THREAD = [(150, 128, 92), (195, 174, 130)]
PEBBLE = [(96, 96, 100), (140, 140, 146), (186, 186, 190)]
CYAN = [(30, 70, 100), (78, 202, 224), (101, 255, 239), (189, 255, 253)]
PURPLE = [(70, 40, 120), (155, 72, 208), (193, 117, 237), (235, 195, 254)]
MAGENTA = [(120, 30, 100), (201, 53, 153), (224, 67, 168)]
VINE = [(40, 70, 35), (78, 114, 47), (110, 160, 60)]


# 4x4 ordered-dither thresholds in (0, 1).
BAYER = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) + 0.5) / 16


class ArtLayer:
    """An RGBA canvas at art-pixel resolution, wrapping in x (the tile repeats sideways)."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.rgba = np.zeros((h, w, 4), np.uint8)
        # Rows the current band may draw into - threads and drips are clipped to it.
        self.ymin, self.ymax = 0, h

    def put(self, x, y, color):
        if self.ymin <= y < self.ymax:
            self.rgba[y, x % self.w, :3] = color
            self.rgba[y, x % self.w, 3] = 255

    def filled(self, x, y):
        return 0 <= y < self.h and self.rgba[y, x % self.w, 3] > 0


def value_noise(n, period, rng):
    """1D smooth noise in [-1, 1], n samples, features ~period samples apart."""
    knots = rng.uniform(-1, 1, n // max(1, period) + 3)
    xs = np.arange(n) / max(1, period)
    i = xs.astype(int)
    f = xs - i
    f = f * f * (3 - 2 * f)
    return knots[i] * (1 - f) + knots[i + 1] * f


def ledge_shape(length, top, thick, rng, min_bottom=None):
    """Per-column (top, bottom) rows of a ledge, in art px: a flat-topped outcrop with rounded
    ends whose underside sags into one or two rock masses. min_bottom forces depth where hanging
    formations are buried in it."""
    x = np.arange(length)
    t = x / max(1, length - 1)
    cap_len = max(3.0, length * 0.12)
    caps = np.clip(np.minimum(t, 1 - t) * length / cap_len, 0, 1)
    caps = np.sqrt(1 - (1 - caps) ** 2)  # round, not pointy
    depth = thick * (0.85 + 0.2 * value_noise(length, 9, rng))
    for _ in range(rng.integers(1, 3)):
        c = rng.uniform(0.25, 0.75) * length
        w = rng.uniform(0.15, 0.3) * length
        depth += thick * rng.uniform(0.4, 0.9) * np.clip(1 - ((x - c) / w) ** 2, 0, 1) ** 0.7
    if min_bottom is not None and min_bottom.any():
        # Round the forced depth off into a sagging rock mass instead of a box.
        req = np.where(min_bottom > 0, min_bottom - top, -10**6).astype(float)
        sag = np.max(req[None, :] - (x[:, None] - x[None, :]) ** 2 / (1.5 * max(4, thick)), axis=1)
        depth = np.maximum(depth, sag)
    depth = np.maximum(2, depth * caps + value_noise(length, 3, rng))
    tops = top + np.round(value_noise(length, 11, rng) + (1 - caps) * depth * 0.5).astype(int)
    bottoms = tops + np.round(depth).astype(int)
    # Stubby stalactites along the underside.
    for _ in range(max(1, length // 12)):
        c = rng.integers(2, max(3, length - 2))
        ln = rng.integers(2, 3 + thick // 2)
        w = rng.integers(1, 3)
        for dx in range(-w, w + 1):
            if 0 <= c + dx < length and caps[c + dx] > 0.8:
                bottoms[c + dx] += max(0, ln - abs(dx) * 2)
    if min_bottom is not None:
        bottoms = np.maximum(bottoms, min_bottom)
    return tops, np.maximum(bottoms, tops + 2)


def draw_rock(layer, x0, tops, bottoms, palette, rng):
    """Chunky stacked-stone shading in the sheets' style: dark outline, lit stone tops, dark
    cracks between stones, darker toward the underside and the ends (where it meets the wall)."""
    length = len(tops)
    y0, y1 = int(tops.min()), int(bottoms.max()) + 1
    h = y1 - y0
    mask = np.zeros((h, length), bool)
    for x in range(length):
        mask[tops[x] - y0:bottoms[x] - y0, x] = True
    # Voronoi stones, wider than tall.
    n = max(3, int(mask.sum() / 30))
    seeds = np.column_stack([rng.uniform(0, h, n), rng.uniform(0, length, n)])
    yy, xx = np.mgrid[0:h, 0:length]
    _, cell = cKDTree(seeds).query(np.column_stack([yy.ravel(), xx.ravel()]))
    cell = cell.reshape(h, length)
    stone_tone = rng.choice([2, 2, 2, 3, 3], n)

    outside = ~np.pad(mask, 1)
    for y in range(h):
        for x in range(length):
            if not mask[y, x]:
                continue
            py, px = y + 1, x + 1
            depth = y + y0 - tops[x]
            span = max(1, bottoms[x] - tops[x])
            if outside[py - 1, px] or outside[py + 1, px] or outside[py, px - 1] or outside[py, px + 1]:
                tone = 0
            elif depth == 1:
                tone = 4
            else:
                c = cell[y, x]
                tone = stone_tone[c]
                below = y + 1 < h and mask[y + 1, x] and cell[y + 1, x] != c
                right = x + 1 < length and mask[y, x + 1] and cell[y, x + 1] != c
                above = y > 0 and mask[y - 1, x] and cell[y - 1, x] != c
                left = x > 0 and mask[y, x - 1] and cell[y, x - 1] != c
                if below or right:
                    tone = 0 if tone <= 1 else 1
                elif above or left:
                    tone = min(4, tone + 1)
                if depth == 2 and tone >= 2:
                    tone = 3
                if depth > span * 0.62:
                    tone = max(1 if tone > 0 else 0, tone - 1)
                if min(x, length - 1 - x) < 3 and tone > 1:
                    tone -= 1
            layer.put(x0 + x, y + y0, palette[tone])
    return mask, y0


def accent(layer, biome, x0, tops, bottoms, rng):
    length = len(tops)
    inner = [x for x in range(length) if bottoms[x] - tops[x] > 3]
    if not inner:
        return
    if biome == "Topsoil":
        for _ in range(max(1, length // 12)):  # embedded pebbles
            x = rng.choice(inner)
            y = rng.integers(tops[x] + 3, max(tops[x] + 4, bottoms[x] - 1))
            for dx, dy, c in ((0, 0, 1), (1, 0, 1), (0, 1, 0), (1, 1, 0), (0, -1, 2)):
                if layer.filled(x0 + x + dx, y + dy):
                    layer.put(x0 + x + dx, y + dy, PEBBLE[c])
        for _ in range(max(2, length // 6)):  # pale root threads hanging out of the underside
            hanging_thread(layer, x0 + rng.integers(1, length - 1), bottoms, x0, rng, ROOT_THREAD, 4, 14)
    elif biome == "Crystal":
        for _ in range(max(1, length // 16)):
            x = rng.choice(inner[2:-2] or inner)
            crystal(layer, x0 + x, tops[x] + 1, rng.integers(3, 8), PURPLE if rng.random() < 0.5 else CYAN, -1)
        for _ in range(max(1, length // 20)):
            x = rng.choice(inner[2:-2] or inner)
            crystal(layer, x0 + x, bottoms[x] - 1, rng.integers(2, 5), CYAN, 1)
    elif biome == "Overgrown":
        # A moss cap over the walking surface, dripping over the front here and there.
        for x in range(length):
            depth = 1 + int(rng.random() < 0.5) + int(rng.random() < 0.2)
            if rng.random() < 0.12:
                depth += rng.integers(2, 5)
            for d in range(depth):
                y = tops[x] + d
                if y < bottoms[x]:
                    layer.put(x0 + x, y, MOSS[0] if d == 0 and rng.random() < 0.3 else MOSS[min(4, 4 - d)])
        for _ in range(max(2, length // 8)):
            hanging_thread(layer, x0 + rng.integers(1, length - 1), bottoms, x0, rng, VINE, 5, 20, leaves=True)
    elif biome == "Void":
        for _ in range(max(1, length // 18)):  # glowing cracks wandering through the stone
            x = rng.choice(inner)
            y = tops[x] + 2
            for _ in range(rng.integers(4, 12)):
                if not (tops[x % length] + 1 < y < bottoms[x % length] - 1):
                    break
                layer.put(x0 + x, y, MAGENTA[2] if rng.random() < 0.6 else MAGENTA[1])
                if rng.random() < 0.6:
                    y += 1
                else:
                    x = min(length - 1, max(0, x + rng.choice([-1, 1])))
        for _ in range(max(1, length // 12)):
            hanging_thread(layer, x0 + rng.integers(2, length - 2), bottoms, x0, rng, MAGENTA[1:], 2, 5)


def hanging_thread(layer, x, bottoms, x0, rng, colors, lo, hi, leaves=False):
    i = x - x0
    if not 0 <= i < len(bottoms):
        return
    y = bottoms[i]
    for k in range(rng.integers(lo, hi)):
        layer.put(x, y + k, colors[-1] if k == 0 else colors[1 + (k % 2) % (len(colors) - 1)])
        if leaves and k % 4 == 2:
            layer.put(x + (1 if k % 8 == 2 else -1), y + k, colors[-1])
        if rng.random() < 0.12:
            x += rng.choice([-1, 1])


def crystal(layer, x, base, height, colors, direction):
    """A two-column shard: lit left face, base right face, dark outline, pointing up (-1) or down."""
    for k in range(height):
        y = base + direction * k
        tip = k >= height - 2
        layer.put(x - 1, y, colors[0])
        layer.put(x, y, colors[3] if tip else colors[2])
        layer.put(x + 1, y, colors[1] if not tip else colors[0])
        layer.put(x + 2, y, colors[0])
    layer.put(x, base + direction * height, colors[0])


def draw_support(layer, x0, tops, bottoms, direction, end_row, palette, rng, width, centre):
    """The rock buttress a ledge grows out of: a column of the ledge's own stone, in shadow,
    running from behind the ledge up to the ceiling (direction -1) or down into the gloom (+1)
    and fading out into the cave's darkness before the band edge. That's what makes a ledge read
    as attached to the cave rather than floating in front of it."""
    start = int(np.median(tops + (bottoms - tops) // 2))
    span = abs(end_row - start)
    if span < 6:
        return
    # Per row along the column: half-width (tapering away from the ledge, wobbling) and centre.
    rows = np.arange(span)
    taper = 1 - 0.35 * rows / span
    wobble = value_noise(span, 9, rng) * 1.5 + value_noise(span, 3, rng) * 0.6
    drift = np.round(value_noise(span, 23, rng) * width * 0.15).astype(int)
    half_l = np.maximum(2, np.round(width / 2 * taper + wobble)).astype(int)
    half_r = np.maximum(2, np.round(width / 2 * taper - wobble * 0.7 + value_noise(span, 5, rng))).astype(int)

    # Stone it like the ledge, then push it back: darker and fading out with distance.
    col = ArtLayer(width * 2 + 8, span)
    lefts = np.full(width * 2 + 8, span)
    rights = np.zeros(width * 2 + 8, int)
    cx = width + 4
    for r in range(span):
        for x in range(cx + drift[r] - half_l[r], cx + drift[r] + half_r[r]):
            lefts[x] = min(lefts[x], r)
            rights[x] = max(rights[x], r + 1)
    used = np.where(rights > 0)[0]
    shade = [tuple(int(c * 0.8) for c in tone) for tone in palette]
    draw_rock(col, used[0], lefts[used], rights[used], shade, rng)
    for r in range(span):
        dist = r / span
        # Solid near the ledge, then an ordered-dither fade into the dark - it stays pixel art.
        alpha = np.clip((0.95 - dist) / 0.5, 0, 1)
        dim = 1 - 0.4 * dist
        y = start + direction * r
        if not layer.ymin <= y < layer.ymax:
            continue
        for x in np.where(col.rgba[r, :, 3] > 0)[0]:
            xx = x0 + centre + x - cx
            if alpha <= BAYER[y % 4, xx % 4]:
                continue
            layer.rgba[y, xx % layer.w, :3] = (col.rgba[r, x, :3] * dim).astype(np.uint8)
            layer.rgba[y, xx % layer.w, 3] = 255


def object_info(img):
    a = np.asarray(img)[..., 3] > 100
    rows = np.where(a.any(1))[0]
    cols = np.where(a.any(0))[0]
    return rows[0], rows[-1], cols[0], cols[-1]


def paste_wrap(canvas, img, x0, y0):
    arr = np.asarray(img).astype(np.float32)
    size = canvas.shape[1]
    oh, ow = arr.shape[:2]
    for ox in (-size, 0, size):
        xa = x0 + ox
        xs0, xs1 = max(0, xa), min(size, xa + ow)
        ys0, ys1 = max(0, y0), min(canvas.shape[0], y0 + oh)
        if xs0 >= xs1 or ys0 >= ys1:
            continue
        src = arr[ys0 - y0:ys1 - y0, xs0 - xa:xs1 - xa]
        dst = canvas[ys0:ys1, xs0:xs1]
        sa = src[..., 3:4] / 255
        da = dst[..., 3:4] / 255
        out_a = sa + da * (1 - sa)
        dst[..., :3] = (src[..., :3] * sa + dst[..., :3] * da * (1 - sa)) / np.maximum(out_a, 1e-6)
        dst[..., 3:4] = out_a * 255


def compose(biome, plane, objects, seed):
    """objects: {sheet index: RGBA image}. Returns the seamless RGBA tile."""
    rng = np.random.default_rng(seed)
    px = ART_PX[plane]
    grid = SIZE // px
    bands = BANDS[plane]
    margin = 3
    roles = ROLES[biome]
    floors = [objects[i] for i, (r, _) in roles.items() if r == "floor" and i in objects]
    hangs = [(objects[i], e) for i, (r, e) in roles.items() if r == "hang" and i in objects]

    behind = np.zeros((SIZE, SIZE, 4), np.float32)  # formations, drawn under the ledges
    rock = ArtLayer(grid, grid)
    supports = ArtLayer(grid, grid)  # buttresses, behind everything else

    def scaled(img, s):
        s *= OBJ_SCALE[plane]
        return img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.NEAREST)

    for b in range(bands):
        b0 = int(np.ceil(b * grid / bands)) + margin
        b1 = int((b + 1) * grid / bands) - margin
        rock.ymin, rock.ymax = b0, b1
        supports.ymin, supports.ymax = b0 - margin + 1, b1 + margin - 1
        count = rng.integers(LEDGES_PER_BAND[plane][0], LEDGES_PER_BAND[plane][1] + 1)
        start = rng.integers(0, grid)
        slot = grid // count
        for k in range(count):
            # Each ledge leans one way: a shelf with formations standing on it, an overhang with
            # things hanging under it, or both when the band has room.
            kind = rng.choice(["floor", "hang", "both"], p=[0.4, 0.35, 0.25])
            f_imgs, h_imgs = [], []
            if kind in ("floor", "both") and floors:
                hero = floors[rng.integers(len(floors))]
                f_imgs.append(scaled(hero, rng.uniform(0.9, 1.1)))
                for _ in range(rng.integers(0, 3)):
                    f_imgs.append(scaled(floors[rng.integers(len(floors))], rng.uniform(0.45, 0.65)))
            if kind in ("hang", "both") and hangs:
                for _ in range(rng.integers(1, 3)):
                    img, embed = hangs[rng.integers(len(hangs))]
                    h_imgs.append((scaled(img, rng.uniform(0.8, 1.05) if not h_imgs else rng.uniform(0.55, 0.75)), embed))
            if not f_imgs and not h_imgs:
                continue

            # Fit the band's height: the floor stack above the lip, the ledge and hangers below.
            thick = int(rng.integers(*THICK[plane]))
            def heights(fs, hs):
                fh = max([object_info(i)[1] - object_info(i)[0] + 1 for i in fs] or [0]) / px
                hh = max([(object_info(i)[1] - object_info(i)[0] + 1) / px + 3 for i, _ in hs] or [0])
                return fh, max(hh, thick * 2.2 + 4)
            fh, hh = heights(f_imgs, h_imgs)
            room = b1 - b0
            if fh + hh > room:
                s = (room - thick * 2.2 - 4) / max(1, fh + hh - thick * 2.2 - 4) * 0.97
                f_imgs = [i.resize((max(1, int(i.width * s)), max(1, int(i.height * s))), Image.NEAREST) for i in f_imgs]
                h_imgs = [(i.resize((max(1, int(i.width * s)), max(1, int(i.height * s))), Image.NEAREST), e) for i, e in h_imgs]
                fh, hh = heights(f_imgs, h_imgs)
            slack = max(0, room - fh - hh)
            top = int(b0 + fh + rng.uniform(0.2, 0.8) * slack)

            # Lay formations out left to right along the ledge, hero in the middle.
            f_w = [int(np.ceil((object_info(i)[3] - object_info(i)[2] + 1) / px)) for i in f_imgs]
            h_w = [int(np.ceil((object_info(i)[3] - object_info(i)[2] + 1) / px)) for i, _ in h_imgs]
            gap = 3
            # Drop sidekicks until the cluster fits its slot (the hero always stays).
            while len(f_w) > 1 and sum(f_w) + gap * (len(f_w) + 1) > slot - 10:
                f_imgs.pop()
                f_w.pop()
            while len(h_w) > 1 and sum(h_w) + gap * (len(h_w) + 1) > slot - 10:
                h_imgs.pop()
                h_w.pop()
            length = max(sum(f_w) + gap * (len(f_w) + 1), sum(h_w) + gap * (len(h_w) + 1)) + int(rng.integers(*EXTRA_LENGTH[plane]))
            length = min(length, slot - 6)
            x0 = int(start + k * slot + rng.integers(0, max(1, slot - length - 4)))

            order = list(range(len(f_imgs)))
            if len(order) > 1:  # hero (index 0) goes between the smaller ones
                rest = order[1:]
                order = rest[:len(rest) // 2] + [0] + rest[len(rest) // 2:]
            used = sum(f_w[i] for i in order) + gap * (len(order) - 1)
            fx = x0 + (length - used) // 2 + int(rng.integers(-3, 4))
            floor_spots = []
            for i in order:
                floor_spots.append((f_imgs[i], fx))
                fx += f_w[i] + gap

            used = sum(h_w) + gap * (len(h_w) - 1)
            hx = x0 + (length - used) // 2 + int(rng.integers(-4, 5))
            min_bottom = np.zeros(length, int)
            hang_spots = []
            for (img, embed), w in zip(h_imgs, h_w):
                r0, r1, c0, c1 = object_info(img)
                bury = int((r1 - r0 + 1) * embed / px)
                y_top = top + 2
                hang_spots.append((img, hx, y_top))
                # The ledge must be at least as deep as the buried part, plus a lip.
                a = np.asarray(img)[..., 3] > 100
                row = min(r1, r0 + bury * px)
                cols = np.where(a[r0:row + 1].any(0))[0]
                for c in range(cols.min() // px - 1, cols.max() // px + 2):
                    i = hx + c - c0 // px - x0
                    if 0 <= i < length:
                        min_bottom[i] = max(min_bottom[i], y_top + bury + 2)
                hx += w + gap

            tops, bottoms = ledge_shape(length, top, thick, rng, np.where(min_bottom > 0, min_bottom, 0))
            bottoms = np.minimum(bottoms, b1 - 1)
            # The ledge juts from a rock pillar that runs up to the ceiling and down into the gloom.
            width = max(6, int(length * rng.uniform(0.35, 0.55)))
            centre = int(length * rng.uniform(0.35, 0.65))
            for direction, end in ((-1, b0 - margin + 1), (1, b1 + margin - 1)):
                draw_support(supports, x0, tops, bottoms, direction, end, ROCK[biome], rng, width, centre)
            draw_rock(rock, x0, tops, bottoms, ROCK[biome], rng)
            accent(rock, biome, x0, tops, bottoms, rng)

            for img, hx_, y_top in hang_spots:
                r0, _, c0, _ = object_info(img)
                paste_wrap(behind, img, hx_ * px - c0, y_top * px - r0)
            for img, fx_ in floor_spots:
                r0, r1, c0, c1 = object_info(img)
                cols = range(fx_ - x0, fx_ - x0 + int(np.ceil((c1 - c0 + 1) / px)))
                ground = max(tops[c % length] for c in cols if 0 <= c < length) if any(0 <= c < length for c in cols) else top
                # Sink the base 3 art px so the ledge's lip covers it.
                paste_wrap(behind, img, fx_ * px - c0, (ground + 3) * px - r1 - 1)

    def upscale(layer):
        return np.asarray(Image.fromarray(layer.rgba, "RGBA").resize((SIZE, SIZE), Image.NEAREST)).astype(np.float32)

    def over(top, under):
        sa = top[..., 3:4] / 255
        da = under[..., 3:4] / 255
        oa = sa + da * (1 - sa)
        rgb = (top[..., :3] * sa + under[..., :3] * da * (1 - sa)) / np.maximum(oa, 1e-6)
        return np.concatenate([rgb, oa * 255], axis=2)

    out = over(upscale(rock), over(behind, upscale(supports)))
    # Band edges must stay empty so a biome cut there never shows a sliced formation.
    for b in range(bands + 1):
        y = min(SIZE - 1, round(b * SIZE / bands))
        assert out[max(0, y - 2):y + 2, :, 3].max() == 0, f"{biome} {plane}: content on band edge {b}"
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
