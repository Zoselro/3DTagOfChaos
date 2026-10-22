# Cursed Candy Carnival floor tiles (2026-10-22 §34): plum stone plaza at character scale (cookie 2 m).
# Base tiles are 2 m squares (1024 px per metre), pattern tiles 4 m squares (4096 px): albedo, OpenGL normal map and
# metal/smoothness (R = metallic, A = smoothness) — everything periodic, and every tile border is a grout line, so any tile
# can sit next to any other without a seam.
#   FL_Basic   running-bond slabs (rows 0.5 m, lengths 0.5~1 m), chips, cracks, pitting
#   FL_Wet     same stone, damp with glossy puddles in the low spots
#   FL_Cobble  rounded cobbles (~0.29 m Voronoi cells)
#   FL_Sewer   slabs with an iron manhole (spokes, drain slots) in a stone collar
#   FL_PatternCompass / FL_PatternWheel / FL_PatternDiamond / FL_PatternFlower  4 m inlaid decor tiles
# python3 make_floor.py <out dir> [scale 1.0 = full size, 0.25 = quick preview]
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
SCALE = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
os.makedirs(OUT, exist_ok=True)
F = np.float32

GROUT = 0.012            # half grout width (m) — a full joint is 2.4 cm
BEVEL = 0.022            # rounded slab edge
STONES = [(0.33, 0.24, 0.40), (0.29, 0.21, 0.36), (0.36, 0.26, 0.42), (0.26, 0.19, 0.31), (0.38, 0.25, 0.39),
          (0.31, 0.25, 0.42), (0.34, 0.22, 0.33)]
GROUT_C = (0.07, 0.05, 0.09)
GOLD = (0.86, 0.60, 0.24)
IRON = (0.17, 0.15, 0.19)


class Tile:
    """Pixel grid of one square tile: x right, y up (row 0 = top of the image = v 1)."""

    def __init__(self, name, size_m, px_per_m, seed):
        self.name, self.L = name, size_m
        self.N = int(size_m * px_per_m * SCALE)
        self.px = size_m / self.N
        self.rng = np.random.default_rng(seed)
        c = (np.arange(self.N, dtype=F) + 0.5) * self.px
        self.X = np.broadcast_to(c[None, :], (self.N, self.N))
        self.Y = np.broadcast_to((size_m - c)[:, None], (self.N, self.N))
        self.H = np.zeros((self.N, self.N), F)                  # height (m)
        self.C = np.zeros((self.N, self.N, 3), F)               # albedo (linear-ish 0..1, saved as sRGB values)
        self.R = np.full((self.N, self.N), 0.8, F)              # roughness
        self.M = np.zeros((self.N, self.N), F)                  # metallic

    # ---- noise (periodic over the tile) ----
    def noise(self, cells, octaves=4, persistence=0.5):
        acc = np.zeros((self.N, self.N), F); amp = 1.0; total = 0.0
        for o in range(octaves):
            c = int(cells * 2 ** o)
            g = self.rng.random((c, c)).astype(F)
            g = np.pad(g, ((0, 1), (0, 1)), mode="wrap")
            t = (np.arange(self.N, dtype=F) + 0.5) / self.N * c
            i = t.astype(int); f = t - i; f = f * f * (3 - 2 * f)
            a = g[i][:, i]; b = g[i][:, i + 1]; cc = g[i + 1][:, i]; d = g[i + 1][:, i + 1]
            top = a * (1 - f) + b * f; bot = cc * (1 - f) + d * f
            acc += amp * (top * (1 - f[:, None]) + bot * f[:, None]); total += amp; amp *= persistence
        return acc / total

    def aa(self, sdf):
        """Anti-aliased inside mask from a signed distance (negative inside, metres)."""
        return np.clip(0.5 - sdf / (1.5 * self.px), 0, 1).astype(F)

    def wrap_d(self, a, c):
        d = np.abs(a - c)
        return np.minimum(d, self.L - d)

    # ---- stone fields ----
    def slabs(self, row_h=0.5, lengths=(0.5, 0.75, 1.0), region=None):
        """Running-bond slabs; joints always at x = 0 and y = 0 so the tile border is a grout line."""
        L = self.L
        rows = int(round(L / row_h))
        stone = np.zeros((self.N, self.N), F); hgt = np.zeros_like(stone); ids = np.full(stone.shape, -1, np.int32)
        k = 0
        for r in range(rows):
            y0 = r * row_h
            x = 0.0
            while x < L - 1e-6:
                w = float(self.rng.choice(lengths))
                if L - x - w < min(lengths) - 1e-6: w = L - x
                self._rect(x, y0, w, row_h, stone, hgt, ids, k)
                x += w; k += 1
        return stone, hgt, ids, k

    def _rect(self, x0, y0, w, h, stone, hgt, ids, k):
        i0 = max(0, int((self.L - (y0 + h)) / self.px) - 1); i1 = min(self.N, int((self.L - y0) / self.px) + 1)
        j0 = max(0, int(x0 / self.px) - 1); j1 = min(self.N, int((x0 + w) / self.px) + 1)
        X = self.X[i0:i1, j0:j1]; Y = self.Y[i0:i1, j0:j1]
        d = np.minimum(np.minimum(X - x0, x0 + w - X), np.minimum(Y - y0, y0 + h - Y))
        # chipped corners on some slabs
        for cx, cy in ((x0, y0), (x0 + w, y0), (x0, y0 + h), (x0 + w, y0 + h)):
            if self.rng.random() < 0.18:                         # broken corner: a jagged bite, not a round dot
                r = self.rng.uniform(0.025, 0.06)
                ang = np.arctan2(Y - cy, X - cx)
                jag = r * (1 + 0.35 * np.sin(ang * 7 + self.rng.uniform(0, 6)))
                d = np.minimum(d, np.hypot(X - cx, Y - cy) - jag)
        inside = d > 0
        m = self.aa(GROUT - d) * inside
        bevel = np.clip((d - GROUT) / BEVEL, 0, 1)
        prof = np.sqrt(1 - (1 - bevel) ** 2)                    # rounded edge
        tilt = (self.rng.uniform(-1, 1) * (X - x0 - w / 2) + self.rng.uniform(-1, 1) * (Y - y0 - h / 2)) * 0.004
        sel = m > stone[i0:i1, j0:j1]
        stone[i0:i1, j0:j1] = np.where(sel, m, stone[i0:i1, j0:j1])
        hgt[i0:i1, j0:j1] = np.where(sel, 0.012 * prof + tilt + 0.004, hgt[i0:i1, j0:j1])
        ids[i0:i1, j0:j1] = np.where(sel & (m > 0.5), k, ids[i0:i1, j0:j1])

    def cobbles(self, n=7):
        """Periodic Voronoi cobbles, n x n jittered cells over the tile."""
        L = self.L; cell = L / n
        seeds = (np.stack(np.meshgrid(np.arange(n), np.arange(n), indexing="ij"), -1) + 0.5 +
                 self.rng.uniform(-0.32, 0.32, (n, n, 2))) * cell
        ci = np.floor(self.X / cell).astype(int); cj = np.floor(self.Y / cell).astype(int)
        f1 = np.full(self.X.shape, 9.0, F); f2 = np.full(self.X.shape, 9.0, F); idf = np.zeros(self.X.shape, np.int32)
        for di in (-1, 0, 1):
            for dj in (-1, 0, 1):
                a = ci + di; b = cj + dj
                sx = seeds[a % n, b % n, 0] + np.floor_divide(a, n) * L
                sy = seeds[a % n, b % n, 1] + np.floor_divide(b, n) * L
                d = np.hypot(self.X - sx, self.Y - sy).astype(F)
                nid = (a % n) * n + (b % n)
                closer = d < f1
                f2 = np.where(closer, f1, np.minimum(f2, d)); idf = np.where(closer, nid, idf); f1 = np.where(closer, d, f1)
        edge = (f2 - f1) / 2                                     # distance to the cell border
        m = self.aa(GROUT * 1.3 - edge)
        dome = np.clip(1 - (f1 / (cell * 0.62)) ** 2, 0, 1)
        bev = np.clip((edge - GROUT) / 0.045, 0, 1)
        hgt = 0.006 + 0.014 * np.sqrt(1 - (1 - bev) ** 2) * (0.6 + 0.4 * dome)
        return m, hgt.astype(F), idf, n * n

    # ---- painting ----
    def paint_stone(self, stone, hgt, ids, count, palette=STONES, tint=0.0):
        cols = (np.array(palette, F) if len(palette) == count else                 # fixed order (checker) or random pick
                np.array([palette[int(self.rng.integers(len(palette)))] for _ in range(count)], F))
        cols = cols * self.rng.uniform(0.85, 1.12, (count, 1)).astype(F) if len(palette) != count else cols
        if tint: cols += np.array((tint, -tint * 0.3, tint * 0.4), F)
        base = cols[np.clip(ids, 0, count - 1)]
        n1 = self.noise(6 * self.L, 4)[..., None]; n2 = self.noise(40 * self.L, 2)[..., None]
        base = base * (0.82 + 0.3 * n1) * (0.9 + 0.18 * n2)
        mottled = self.noise(3 * self.L, 3)[..., None]
        base = base * (1 - 0.15 * mottled) + np.array((0.05, 0.0, 0.06), F) * mottled * 0.4      # plum blotches
        grout = np.array(GROUT_C, F) * (0.7 + 0.6 * self.noise(30 * self.L, 2))[..., None]
        ao = (0.62 + 0.38 * np.clip(hgt / 0.012, 0, 1))[..., None]
        s = stone[..., None]
        self.C = (base * ao) * s + grout * (1 - s)
        pits = self.noise(48 * self.L, 3)
        self.H = hgt * stone + 0.0025 * (pits - 0.5) * stone + 0.003 * self.noise(5 * self.L, 3) * stone
        self.R = (0.72 + 0.16 * self.noise(10 * self.L, 3)) * stone + 0.95 * (1 - stone)
        self.cracks(stone, ids, count)

    def cracks(self, stone, ids, count, chance=0.35):
        img = Image.new("L", (self.N, self.N), 0); d = ImageDraw.Draw(img)
        for _ in range(int(count * chance)):
            x, y = self.rng.uniform(0, self.N, 2)
            ang = self.rng.uniform(0, 2 * math.pi)
            pts = [(x, y)]
            for _ in range(int(self.rng.integers(4, 9))):
                ang += self.rng.uniform(-0.7, 0.7)
                step = self.rng.uniform(0.03, 0.08) / self.px
                x += math.cos(ang) * step; y += math.sin(ang) * step
                pts.append((x, y))
            d.line(pts, fill=255, width=max(1, int(0.004 / self.px)))
        cr = np.asarray(img.filter(ImageFilter.GaussianBlur(0.8)), F) / 255.0 * stone
        self.C *= (1 - 0.55 * cr)[..., None]
        self.H -= 0.004 * cr

    def border(self):
        """Tile edge = half a grout joint (adjacent tiles complete it)."""
        d = np.minimum(np.minimum(self.X, self.L - self.X), np.minimum(self.Y, self.L - self.Y))
        m = self.aa(GROUT - d)[..., None]
        grout = np.array(GROUT_C, F)
        self.C = self.C * m + grout * (1 - m)
        self.H = self.H * m[..., 0]
        self.R = self.R * m[..., 0] + 0.95 * (1 - m[..., 0])

    def inlay(self, sdf, color, height=0.0, rough=0.5, metal=0.0, wear=0.25):
        m = self.aa(sdf)
        n = self.noise(14 * self.L, 3)
        col = np.array(color, F) * (1 - wear * 0.6 + wear * n)[..., None]
        self.C = self.C * (1 - m[..., None]) + col * m[..., None]
        if height: self.H = self.H * (1 - m) + (height + 0.0015 * n) * m
        self.R = self.R * (1 - m) + (rough + 0.15 * (n - 0.5)) * m
        self.M = self.M * (1 - m) + metal * m
        return m

    def groove(self, sdf, width, depth=0.004, darken=0.5):
        m = self.aa(np.abs(sdf) - width)
        self.C *= (1 - darken * m)[..., None]
        self.H -= depth * m

    # ---- output ----
    def save(self):
        k = 1.6                                                   # normal strength
        dx = (np.roll(self.H, -1, 1) - np.roll(self.H, 1, 1)) / (2 * self.px)
        dy = -(np.roll(self.H, -1, 0) - np.roll(self.H, 1, 0)) / (2 * self.px)
        n = np.dstack((-dx * k, -dy * k, np.ones_like(dx)))
        n /= np.linalg.norm(n, axis=2, keepdims=True)
        to8 = lambda a: np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)
        Image.fromarray(to8(np.clip(self.C, 0, 1))).save(os.path.join(OUT, self.name + "_albedo.png"))
        Image.fromarray(to8(n * 0.5 + 0.5)).save(os.path.join(OUT, self.name + "_normal.png"))
        ms = np.dstack((self.M, self.M, self.M, 1 - np.clip(self.R, 0, 1)))
        Image.fromarray(to8(ms), "RGBA").save(os.path.join(OUT, self.name + "_metalSmooth.png"))
        print(self.name, self.N, "px", flush=True)


def circle(t, cx, cy, r):
    return (np.hypot(t.X - cx, t.Y - cy) - r).astype(F)


def ring(t, cx, cy, r0, r1):
    d = np.hypot(t.X - cx, t.Y - cy)
    return np.maximum(r0 - d, d - r1).astype(F)


def polar(t, cx, cy):
    return np.hypot(t.X - cx, t.Y - cy).astype(F), np.arctan2(t.Y - cy, t.X - cx).astype(F)


def spokes(t, cx, cy, n, half_w, r0, r1, phase=0.0):
    r, a = polar(t, cx, cy)
    sector = 2 * np.pi / n
    phi = np.mod(a - phase + sector / 2, sector) - sector / 2
    across = np.abs(np.sin(phi)) * r
    return np.maximum(np.maximum(across - half_w, r0 - r), r - r1).astype(F)


def star(t, cx, cy, n, r_long, r_short, r_in, phase=0.0):
    """n-point star (alternating long / short points); returns sdf and a 'left half of each point' mask."""
    r, a = polar(t, cx, cy)
    sector = 2 * np.pi / n
    k = np.floor(np.mod(a - phase + sector / 2, 2 * np.pi) / sector).astype(int)
    phi = np.mod(a - phase + sector / 2, sector) - sector / 2
    R = np.where(k % 2 == 0, r_long, r_short).astype(F)
    x = r * np.cos(phi); y = np.abs(r * np.sin(phi))
    ax, ay = r_in * math.cos(sector / 2), r_in * math.sin(sector / 2)
    slope = ay / (R - ax)
    edge = (ay - slope * (x - ax) - y) / np.sqrt(1 + slope ** 2)
    sdf = np.where(x <= ax, -(ay - y + 0.0), -edge)
    sdf = np.maximum(sdf, x - R)
    left = (np.sin(phi) > 0).astype(F)
    return sdf.astype(F), left


# ---------------------------------------------------------------- base tiles (2 m)
def basic(name="FL_Basic", seed=11, wet=False):
    t = Tile(name, 2.0, 1024, seed)
    stone, hgt, ids, count = t.slabs()
    t.paint_stone(stone, hgt, ids, count)
    if wet:
        # rain-soaked: the whole tile darker and glossy, water standing in the joints and in a few shallow dips
        t.C *= 0.78
        t.R = np.minimum(t.R, 0.42)
        joint = 1 - stone
        dips = np.clip((t.noise(5.0, 3) - 0.6) / 0.05, 0, 1) * np.clip(1 - t.H / 0.014, 0, 1)
        puddle = np.clip(joint + dips, 0, 1)
        puddle = np.asarray(Image.fromarray((puddle * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2)), F) / 255.0
        t.C = t.C * (1 - 0.25 * puddle[..., None])
        t.H = np.maximum(t.H, 0.006 * puddle)                       # water fills the joints flat
        t.R = t.R * (1 - puddle) + 0.05 * puddle
    t.border(); t.save()


def cobble():
    t = Tile("FL_Cobble", 2.0, 1024, 23)
    stone, hgt, ids, count = t.cobbles(7)
    t.paint_stone(stone, hgt, ids, count, tint=0.01)
    t.border(); t.save()


def sewer():
    t = Tile("FL_Sewer", 2.0, 1024, 37)
    stone, hgt, ids, count = t.slabs()
    t.paint_stone(stone, hgt, ids, count)
    c = 1.0
    # stone collar: 12 blocks
    r, a = polar(t, c, c)
    collar = t.inlay(ring(t, c, c, 0.56, 0.72), (0.27, 0.21, 0.32), height=0.012, rough=0.8)
    blk = np.abs(np.sin((a + 0.13) * 6)) * r
    t.groove(np.where(r > 0.56, blk, 9.0).astype(F) * (r < 0.73), 0.006, depth=0.008, darken=0.7)
    t.groove(ring(t, c, c, 0.56, 0.72), 0.007, depth=0.008, darken=0.75)
    # iron cover
    t.inlay(circle(t, c, c, 0.56), IRON, height=0.010, rough=0.5, metal=0.7, wear=0.4)
    t.inlay(ring(t, c, c, 0.48, 0.555), (0.22, 0.19, 0.23), height=0.014, rough=0.45, metal=0.75)
    slots = np.maximum(ring(t, c, c, 0.14, 0.46), -spokes(t, c, c, 8, 0.045, 0.0, 1.0))
    slots = np.maximum(slots, -ring(t, c, c, 0.29, 0.335))
    m = t.inlay(slots, (0.015, 0.01, 0.02), height=-0.03, rough=0.95, metal=0.0, wear=0.0)
    t.inlay(circle(t, c, c, 0.13), (0.24, 0.2, 0.24), height=0.016, rough=0.4, metal=0.8)
    t.inlay(circle(t, c, c, 0.05), GOLD, height=0.018, rough=0.35, metal=0.9)
    rust = np.clip((t.noise(8, 4) - 0.55) / 0.1, 0, 1) * (r < 0.56) * (1 - m)
    t.C = t.C * (1 - 0.5 * rust[..., None]) + np.array((0.32, 0.12, 0.08), F) * 0.5 * rust[..., None]
    t.border(); t.save()


# ---------------------------------------------------------------- pattern tiles (4 m)
def compass():
    t = Tile("FL_PatternCompass", 4.0, 1024, 41)
    stone, hgt, ids, count = t.slabs()
    t.paint_stone(stone, hgt, ids, count)
    c = 2.0
    t.inlay(circle(t, c, c, 1.62), (0.2, 0.15, 0.25), height=0.012, rough=0.7)
    t.inlay(ring(t, c, c, 1.52, 1.6), GOLD, height=0.013, rough=0.35, metal=0.85)
    t.inlay(ring(t, c, c, 1.22, 1.28), GOLD, height=0.013, rough=0.35, metal=0.85)
    r, a = polar(t, c, c)
    for k in range(16):                                           # small diamonds in the band
        ang = k * math.pi / 8 + math.pi / 16
        px, py = c + 1.4 * math.cos(ang), c + 1.4 * math.sin(ang)
        d = (np.abs(t.X - px) + np.abs(t.Y - py)) / math.sqrt(2) - 0.06
        t.inlay(d.astype(F), (0.82, 0.32, 0.55), height=0.013, rough=0.4)
    t.inlay(circle(t, c, c, 1.22), (0.36, 0.27, 0.44), height=0.012, rough=0.65)
    sdf, left = star(t, c, c, 8, 1.15, 0.72, 0.2, phase=math.pi / 2)
    m = t.aa(sdf)
    col = np.where(left[..., None] > 0, np.array(GOLD, F), np.array((0.62, 0.36, 0.16), F))
    short = np.where(np.abs(np.sin(2 * (np.arctan2(t.Y - c, t.X - c) - math.pi / 2))) > 0.5, 1.0, 0.0).astype(F)
    col = np.where(short[..., None] > 0, np.where(left[..., None] > 0, np.array((0.78, 0.42, 0.66), F), np.array((0.5, 0.24, 0.45), F)), col)
    t.C = t.C * (1 - m[..., None]) + col * m[..., None]
    t.H = t.H * (1 - m) + 0.014 * m; t.M = t.M * (1 - m) + 0.8 * m * (1 - short); t.R = t.R * (1 - m) + 0.38 * m
    t.groove(sdf, 0.005, depth=0.003, darken=0.6)
    t.inlay(circle(t, c, c, 0.17), (0.3, 0.12, 0.25), height=0.016, rough=0.4)
    t.inlay(circle(t, c, c, 0.09), GOLD, height=0.017, rough=0.3, metal=0.9)
    t.border(); t.save()


def diamond_field(t, a_side, dark, light):
    """Harlequin diamonds (45 deg), periodic over the tile."""
    s2 = math.sqrt(2)
    u = (t.X + t.Y) / s2 / a_side; v = (t.X - t.Y) / s2 / a_side
    iu, iv = np.floor(u), np.floor(v)
    fu, fv = u - iu, v - iv
    d = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv)) * a_side
    stone = t.aa(GROUT - d)
    bev = np.clip((d - GROUT) / BEVEL, 0, 1)
    hgt = (0.004 + 0.012 * np.sqrt(1 - (1 - bev) ** 2)).astype(F)
    ids = ((iu + iv) % 2).astype(np.int32)
    return stone, hgt, ids


def diamond():
    t = Tile("FL_PatternDiamond", 4.0, 1024, 53)
    a_side = 4.0 / (math.sqrt(2) * 6)
    stone, hgt, ids = diamond_field(t, a_side, None, None)
    t.paint_stone(stone, hgt, ids, 2, palette=[(0.36, 0.2, 0.42), (0.17, 0.12, 0.22)])
    # palette picked at random for the two ids — force the checker order
    s2 = math.sqrt(2)
    u = (t.X + t.Y) / s2 / a_side; v = (t.X - t.Y) / s2 / a_side
    for gu in range(-12, 13):                                     # small gold studs at the diamond corners
        for gv in range(-12, 13):
            x = (gu + gv) * a_side / s2; y = (gu - gv) * a_side / s2
            if -0.1 <= x <= 4.1 and -0.1 <= y <= 4.1:
                d = np.hypot(t.X - x, t.Y - y) - 0.035
                sl = (slice(max(0, int((4.0 - y - 0.1) / t.px)), min(t.N, int((4.0 - y + 0.1) / t.px) + 1)),
                      slice(max(0, int((x - 0.1) / t.px)), min(t.N, int((x + 0.1) / t.px) + 1)))
                if sl[0].start >= sl[0].stop or sl[1].start >= sl[1].stop: continue
                m = t.aa(d[sl])
                t.C[sl] = t.C[sl] * (1 - m[..., None]) + np.array(GOLD, F) * m[..., None]
                t.H[sl] = t.H[sl] * (1 - m) + 0.014 * m
                t.M[sl] = t.M[sl] * (1 - m) + 0.85 * m
                t.R[sl] = t.R[sl] * (1 - m) + 0.35 * m
    t.border(); t.save()


def wheel():
    t = Tile("FL_PatternWheel", 4.0, 1024, 61)
    a_side = 4.0 / (math.sqrt(2) * 6)
    stone, hgt, ids = diamond_field(t, a_side, None, None)
    t.paint_stone(stone, hgt, ids, 2, palette=[(0.22, 0.15, 0.27), (0.15, 0.11, 0.19)])
    c = 2.0
    bronze = (0.55, 0.36, 0.2)
    t.inlay(circle(t, c, c, 1.58), (0.13, 0.1, 0.16), height=0.012, rough=0.75)             # dark wheel bed
    t.inlay(ring(t, c, c, 1.36, 1.54), bronze, height=0.016, rough=0.4, metal=0.85)
    t.groove(ring(t, c, c, 1.36, 1.54), 0.006, depth=0.004, darken=0.6)
    r, a = polar(t, c, c)
    for k in range(24):                                           # rim bolts
        ang = k * math.pi / 12
        t.inlay(circle(t, c + 1.45 * math.cos(ang), c + 1.45 * math.sin(ang), 0.03), (0.8, 0.6, 0.32), height=0.019, rough=0.3, metal=0.9)
    t.inlay(spokes(t, c, c, 12, 0.045, 0.28, 1.37), bronze, height=0.015, rough=0.42, metal=0.85)
    t.inlay(ring(t, c, c, 0.22, 0.34), bronze, height=0.017, rough=0.38, metal=0.85)
    t.inlay(circle(t, c, c, 0.22), (0.5, 0.18, 0.3), height=0.016, rough=0.45)
    t.inlay(circle(t, c, c, 0.08), GOLD, height=0.019, rough=0.3, metal=0.9)
    for k in range(12):                                           # pink inlays between the spokes
        ang = (k + 0.5) * math.pi / 6
        px, py = c + 0.95 * math.cos(ang), c + 0.95 * math.sin(ang)
        d = (np.abs(t.X - px) + np.abs(t.Y - py)) / math.sqrt(2) - 0.07
        t.inlay(d.astype(F), (0.78, 0.3, 0.52), height=0.013, rough=0.4)
    t.border(); t.save()


def flower():
    t = Tile("FL_PatternFlower", 4.0, 1024, 71)
    stone, hgt, ids, count = t.slabs()
    t.paint_stone(stone, hgt, ids, count)
    c = 2.0
    t.inlay(circle(t, c, c, 1.65), (0.2, 0.14, 0.24), height=0.012, rough=0.7)
    t.inlay(ring(t, c, c, 1.56, 1.63), GOLD, height=0.013, rough=0.35, metal=0.85)
    lobes = np.full(t.X.shape, 9.0, F)
    for k in range(4):
        ang = k * math.pi / 2
        lobes = np.minimum(lobes, circle(t, c + 0.62 * math.cos(ang), c + 0.62 * math.sin(ang), 0.52))
    for k in range(4):
        ang = k * math.pi / 2 + math.pi / 4
        lobes = np.minimum(lobes, circle(t, c + 0.92 * math.cos(ang), c + 0.92 * math.sin(ang), 0.3))
    lobes = np.minimum(lobes, circle(t, c, c, 0.6))
    t.inlay(lobes - 0.05, GOLD, height=0.013, rough=0.35, metal=0.85)                       # gold outline
    t.inlay(lobes + 0.0, (0.72, 0.26, 0.5), height=0.014, rough=0.45)
    inner = np.full(t.X.shape, 9.0, F)
    for k in range(4):
        ang = k * math.pi / 2
        inner = np.minimum(inner, circle(t, c + 0.62 * math.cos(ang), c + 0.62 * math.sin(ang), 0.32))
    t.inlay(inner, (0.88, 0.45, 0.68), height=0.015, rough=0.4)
    t.groove(inner, 0.005, depth=0.002, darken=0.45)
    t.inlay(circle(t, c, c, 0.3), (0.36, 0.12, 0.3), height=0.016, rough=0.4)
    t.inlay(ring(t, c, c, 0.3, 0.36), GOLD, height=0.017, rough=0.32, metal=0.9)
    sdf, left = star(t, c, c, 8, 0.24, 0.16, 0.07)
    t.inlay(sdf, GOLD, height=0.018, rough=0.3, metal=0.9)
    for k in range(8):                                            # diamonds in the outer band
        ang = k * math.pi / 4 + math.pi / 8
        px, py = c + 1.38 * math.cos(ang), c + 1.38 * math.sin(ang)
        d = (np.abs(t.X - px) + np.abs(t.Y - py)) / math.sqrt(2) - 0.08
        t.inlay(d.astype(F), (0.8, 0.36, 0.6), height=0.013, rough=0.4)
    t.border(); t.save()


ONLY = set(sys.argv[3].split(",")) if len(sys.argv) > 3 else None
for name, fn in (("FL_Basic", basic), ("FL_Wet", lambda: basic("FL_Wet", 13, wet=True)), ("FL_Cobble", cobble),
                 ("FL_Sewer", sewer), ("FL_PatternCompass", compass), ("FL_PatternWheel", wheel),
                 ("FL_PatternDiamond", diamond), ("FL_PatternFlower", flower)):
    if ONLY is None or name in ONLY: fn()
print("floor textures ok")
