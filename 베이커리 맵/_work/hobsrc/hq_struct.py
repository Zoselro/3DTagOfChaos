from lib import *
from lib import _new, _parts, PAL
import random, bmesh
from mathutils import noise
from objs_v2 import tube, leaf_blade, blob, smooth_parts
from hq_props import flower, grass_mound, last
V = Vector
# ===== HOB HQ structures (concept sheet look). Same footprints/heights as the old builders so the map still fits. =====

def baluster(p, h, m='Cream', r=0.075):
    """turned stone baluster: foot, vase belly, neck, cap"""
    prof = [(0, 0), (r * 1.25, 0), (r * 1.25, h * 0.07), (r * 0.8, h * 0.12), (r * 0.7, h * 0.2), (r * 1.15, h * 0.42), (r * 1.0, h * 0.58),
            (r * 0.55, h * 0.74), (r * 0.6, h * 0.82), (r * 1.1, h * 0.9), (r * 1.15, h), (0, h)]
    smooth_parts(lathe(prof, m, loc=p, seg=10))

def stone_beam(a, b, w, hgt, m, z_off=0.0, bevel=0.015):
    a = V(a); b = V(b); d = b - a; L = d.length
    mid = (a + b) / 2 + V((0, 0, z_off))
    yaw = math.atan2(d.y, d.x); pitch = math.atan2(d.z, V((d.x, d.y, 0)).length)
    return box(L, w, hgt, m, loc=mid, rot=(0, -pitch, yaw), bevel=bevel)

def balustrade(pts, h=1.0, spacing=0.3, post_every=None):
    """stone balustrade along a polyline (z = walking surface): plinth, balusters, pink top rail with gold strip"""
    pts = [V(p) for p in pts]
    for a, b in zip(pts, pts[1:]):
        stone_beam(a, b, 0.24, 0.16, 'PinkStone', z_off=0.08)
        stone_beam(a, b, 0.26, 0.12, 'PinkStone', z_off=h - 0.06)
        stone_beam(a, b, 0.12, 0.035, 'Gold', z_off=h + 0.012, bevel=0.008)
    acc = 0.0; prev = pts[0]
    total = sum((b - a).length for a, b in zip(pts, pts[1:]))
    t = spacing * 0.5
    seg_i = 0; seg_start = 0.0
    lens = [(b - a).length for a, b in zip(pts, pts[1:])]
    while t < total - spacing * 0.3:
        while seg_i < len(lens) - 1 and t > seg_start + lens[seg_i]: seg_start += lens[seg_i]; seg_i += 1
        u = (t - seg_start) / max(lens[seg_i], 1e-6); p = pts[seg_i].lerp(pts[seg_i + 1], min(1, u))
        baluster(p + V((0, 0, 0.15)), h - 0.27)
        t += spacing

def lantern(p, s=1.0):
    """small gold lantern with glowing glass (for pillar tops)"""
    x, y, z = p
    smooth_parts(lathe([(0, z), (0.1 * s, z), (0.06 * s, z + 0.12 * s), (0, z + 0.12 * s)], 'Gold', loc=(x, y, 0), seg=12))   # loc 빠져 모델 원점에 금 받침이 떠 있었다(§89.15)
    zc = z + 0.12 * s; hl = 0.36 * s; r = 0.14 * s
    smooth_parts(lathe([(0, zc), (r * 0.8, zc), (r * 0.8, zc + hl), (0, zc + hl)], 'Glow', loc=(x, y, 0), seg=6))
    for k in range(6):
        a = 2 * math.pi * k / 6 + math.pi / 6; q = V((x + math.cos(a) * r * 0.86, y + math.sin(a) * r * 0.86, 0))
        tube([q + V((0, 0, zc)), q + V((0, 0, zc + hl))], [0.012 * s, 0.012 * s], 'Gold', seg=6)
    smooth_parts(lathe([(0, zc + hl), (r * 1.3, zc + hl), (r * 0.5, zc + hl + 0.16 * s), (0.03 * s, zc + hl + 0.24 * s), (0, zc + hl + 0.25 * s)], 'Gold', loc=(x, y, 0), seg=6))
    smooth_parts(uvs(0.035 * s, 'Cherry', loc=(x, y, zc + hl + 0.29 * s), seg=10, rings=6))

def lantern_pillar(x, y, z=0.0, h=1.2, w=0.42, lamp=True):
    box(w + 0.16, w + 0.16, 0.2, 'PinkStone', loc=(x, y, z + 0.1), bevel=0.03)
    box(w + 0.08, w + 0.08, 0.06, 'Gold', loc=(x, y, z + 0.23), bevel=0.01)
    box(w, w, h, 'PinkStone', loc=(x, y, z + 0.26 + h / 2), bevel=0.025)
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):                                # recessed gold panel each side
        box(0.02 if sx else w * 0.55, 0.02 if sy else w * 0.55, h * 0.6, 'Gold', loc=(x + sx * w / 2, y + sy * w / 2, z + 0.26 + h / 2), bevel=0.004)
    box(w + 0.14, w + 0.14, 0.08, 'Cream', loc=(x, y, z + 0.26 + h + 0.04), bevel=0.02)
    box(w + 0.06, w + 0.06, 0.05, 'Gold', loc=(x, y, z + 0.26 + h + 0.105), bevel=0.01)
    if lamp: lantern((x, y, z + 0.26 + h + 0.13), 1.1)
    else: smooth_parts(uvs(w * 0.35, 'Cream', loc=(x, y, z + 0.26 + h + 0.13 + w * 0.3), seg=16, rings=10))

def paving(x0, x1, y0, y1, z, rows, cols, m='StoneTile', seed=1, thick=0.08):
    rng = random.Random(seed); dx = (x1 - x0) / cols; dy = (y1 - y0) / rows
    for i in range(cols):
        for j in range(rows):
            hh = thick + rng.uniform(-0.008, 0.008)
            box(dx - 0.03, dy - 0.03, hh, m, loc=(x0 + dx * (i + 0.5), y0 + dy * (j + 0.5), z - thick / 2 + (hh - thick)),
                rot=(rng.uniform(-0.012, 0.012), rng.uniform(-0.012, 0.012), rng.uniform(-0.015, 0.015)), bevel=0.012)

def ivy(top, length, rng, m='Foliage'):
    """hanging vine: a few leaf clumps down a rock face"""
    p = V(top)
    for k in range(int(length / 0.35)):
        blob(rng.uniform(0.12, 0.2), m, (p.x, p.y, p.z - k * 0.35), sub=2, lump=0.3, seed=rng.randrange(999), scl=(1, 0.6, 1.2))
        if rng.random() < 0.3: flower((p.x, p.y - 0.12, p.z - k * 0.35), 0.06, ('Frosting', 'Glaze', 'Seed')[k % 3])

# ---------------------------------------------------------------- bridge

def hq_bridge(L=10.0, W=4.0, T=0.5):
    rng = random.Random(4)
    box(W, L, T - 0.08, 'PinkStone', loc=(0, 0, -T / 2 - 0.04))
    paving(-W / 2 + 0.25, W / 2 - 0.25, -L / 2, L / 2, 0.0, rows=12, cols=3, seed=4)
    for sgn in (-1, 1):
        box(0.34, L, 0.18, 'Cream', loc=(sgn * (W / 2 - 0.12), 0, 0.03), bevel=0.02)                 # curb
        box(0.1, L, T + 0.06, 'Gold', loc=(sgn * (W / 2 + 0.04), 0, -T / 2), bevel=0.01)            # gold edge band
        balustrade([(sgn * (W / 2 - 0.12), -L / 2 + 0.55, 0.12), (sgn * (W / 2 - 0.12), L / 2 - 0.55, 0.12)], h=1.0)
        for yy in (-L / 2 + 0.35, L / 2 - 0.35): lantern_pillar(sgn * (W / 2 - 0.12), yy, 0.0, h=0.95)
    # spandrel with a voussoir arch ring + gold keystone
    poly = [(-L / 2, -T + 0.02), (-L / 2, -T - 2.4), (-L / 2 + 0.6, -T - 2.4)]
    for i in range(1, 24):
        a = math.pi * i / 24; poly.append((-(L / 2 - 0.6) * math.cos(a), -T - 2.4 + 1.8 * math.sin(a)))
    poly += [(L / 2 - 0.6, -T - 2.4), (L / 2, -T - 2.4), (L / 2, -T + 0.02)]
    ob = prism(poly, -W / 2 + 0.3, W / 2 - 0.3, 'PinkStone')
    for v in ob.data.vertices: a_, b_, c_ = v.co; v.co = Vector((c_, a_, b_))
    Ra = L / 2 - 0.6
    for sgn in (-1, 1):
        x = sgn * (W / 2 - 0.27)
        for i in range(13):
            a0 = math.pi * i / 13; a1 = math.pi * (i + 1) / 13; am = (a0 + a1) / 2
            c = V((x, -math.cos(am) * (Ra * 1.0 + 0.18), -T - 2.4 + math.sin(am) * (1.8 + 0.18)))
            box(0.12, 0.62, 0.4, 'Gold' if i == 6 else 'Cream', loc=c, rot=(math.pi / 2 - am, 0, 0), bevel=0.02)
        for k in range(4):                                                         # vines over the sides
            ivy((x + sgn * 0.08, rng.uniform(-L / 2 + 1, L / 2 - 1), -0.2), rng.uniform(0.8, 1.8), rng)

# ---------------------------------------------------------------- stairs

def hq_stairs(H=3.0, W=4.0, step=0.2, tread=0.38, landing=2.0):
    n = int(round(H / step)); run = n * tread
    for i in range(n):
        z = (i + 1) * step
        box(W - 0.1, tread - 0.02, z - 0.04, 'PinkStone', loc=(0, i * tread + tread / 2, (z - 0.04) / 2))
        box(W - 0.1, tread + 0.04, 0.07, 'StoneTile', loc=(0, i * tread + tread / 2 - 0.03, z - 0.035), bevel=0.02)   # nosed tread slab
    box(W, landing, H - 0.06, 'PinkStone', loc=(0, run + landing / 2, (H - 0.06) / 2))
    paving(-W / 2 + 0.05, W / 2 - 0.05, run, run + landing, H, rows=3, cols=4, seed=9)
    for sgn in (-1, 1):
        x0 = sgn * (W / 2 - 0.05); x1 = sgn * (W / 2 + 0.35)
        pts = [(0, 0), (run, 0), (run + landing, 0), (run + landing, H + 0.25), (run, H + 0.25), (0, 0.45)]
        ob = prism([(y, z) for y, z in pts], min(x0, x1), max(x0, x1), 'PinkStone')
        for v in ob.data.vertices: a, b, c = v.co; v.co = Vector((c, a, b))
        xr = sgn * (W / 2 + 0.15)
        balustrade([(xr, 0.35, 0.45 + 0.35 * (H + 0.25 - 0.45) / run), (xr, run, H + 0.25)], h=0.95, spacing=0.32)
        balustrade([(xr, run, H + 0.25), (xr, run + landing - 0.35, H + 0.25)], h=0.95)
        lantern_pillar(xr, 0.0, 0.003, h=1.0)
        lantern_pillar(xr, run, H + 0.25 - 0.17, h=0.8)
        lantern_pillar(xr, run + landing - 0.2, H + 0.25 - 0.17, h=0.8)

# ---------------------------------------------------------------- spiral ramp (same centre line as objs_struct.spiral_ramp)

def hq_spiral_ramp(H=12.0, R=6.0, W=3.4, straight=4.0, T=0.45):
    pts = []; N1 = 8; Ns = 48
    for i in range(N1 + 1): pts.append(Vector((0, straight * i / N1, 0)))
    for k, (cy, sgn) in enumerate(((straight + R, 1), (straight + 3 * R, -1))):
        for i in range(1, Ns + 1):
            a = -math.pi / 2 + math.pi * i / Ns; pts.append(Vector((sgn * R * math.cos(a), cy + R * math.sin(a), 0)))
    y0 = straight + 4 * R
    for i in range(1, N1 + 1): pts.append(Vector((0, y0 + straight * i / N1, 0)))
    Ls = [0.0]
    for i in range(1, len(pts)): Ls.append(Ls[-1] + (pts[i] - pts[i - 1]).length)
    tot = Ls[-1]
    for i, p in enumerate(pts): p.z = H * Ls[i] / tot
    def frame(i):
        p = pts[i]; t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]); t.z = 0; t.normalize()
        return p, V((t.y, -t.x, 0)), V((0, 0, 1))
    path = [frame(i) for i in range(len(pts))]
    sweep(path, [(-W / 2, -0.08), (W / 2, -0.08), (W / 2, -T), (-W / 2, -T)], 'PinkStone')
    sweep(path, [(-W / 2 + 0.2, 0.0), (W / 2 - 0.2, 0.0), (W / 2 - 0.2, -0.1), (-W / 2 + 0.2, -0.1)], 'StoneTile')    # paved walking surface
    inner = path[1:-1]
    for sgn in (-1, 1):
        sweep(inner, [(sgn * (W / 2), 0.06), (sgn * (W / 2 + 0.12), 0.06), (sgn * (W / 2 + 0.12), -T - 0.05), (sgn * W / 2, -T - 0.05)][::(1 if sgn > 0 else -1)], 'Gold')
        sweep(inner, [(sgn * (W / 2 - 0.22), 0.14), (sgn * (W / 2 - 0.01), 0.14), (sgn * (W / 2 - 0.01), -0.05), (sgn * (W / 2 - 0.22), -0.05)][::(1 if sgn > 0 else -1)], 'Cream')
        rail = [p + sd * sgn * (W / 2 - 0.115) + V((0, 0, 0.12)) for (p, sd, up) in inner]
        balustrade(rail[::3] + [rail[-1]], h=1.0, spacing=0.34)
    for i in range(0, len(pts), 8):                                                 # ornate support columns
        p, sd, up = path[i]
        if p.z < 1.2: continue
        for sgn in (-1, 1):
            q = p + sd * sgn * (W / 2 - 0.4); h = p.z - T + 0.06
            smooth_parts(lathe([(0, 0), (0.46, 0), (0.46, 0.18), (0.36, 0.3), (0.3, 0.45), (0.27, h - 0.55), (0.34, h - 0.42), (0.42, h - 0.3), (0.42, h - 0.18), (0, h - 0.18)], 'Cream', loc=(q.x, q.y, 0.0), seg=16))
            box(0.9, 0.9, 0.2, 'PinkStone', loc=(q.x, q.y, h - 0.09), bevel=0.03)
            cyl(0.47, 0.06, 'Gold', loc=(q.x, q.y, 0.2), seg=24)
    for i in (0, len(pts) - 1):
        p, sd, up = path[i]
        for sgn in (-1, 1):
            q = p + sd * sgn * (W / 2 + 0.5); lantern_pillar(q.x, q.y, p.z if i else 0.0, h=1.0)
    rng = random.Random(7)
    for k in range(10):                                                             # flower boxes / vines on the outer edges
        i = rng.randrange(10, len(pts) - 10); p, sd, up = path[i]; sgn = rng.choice((-1, 1))
        ivy(p + sd * sgn * (W / 2 + 0.14) + V((0, 0, -0.1)), rng.uniform(0.6, 1.4), rng)
    return tot

# ---------------------------------------------------------------- plateau / island cliffs

def hq_rock_column(x, y, rc, z0, z1, yaw, m, rng):
    """chunky faceted rock column: irregular 6-8 sided, wobbling rings, bevelled top, noise-displaced faces"""
    n = rng.randint(6, 8); rings = 6; bm = bmesh.new(); vs = []
    base = [rc * rng.uniform(0.72, 1.0) for _ in range(n)]; ang = [2 * math.pi * i / n + rng.uniform(-0.18, 0.18) for i in range(n)]
    for j in range(rings + 1):
        t = j / rings; z = z0 + (z1 - z0) * t; sh = 1.0 - 0.18 * max(0.0, t - 0.85) / 0.15
        off = V((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 0)) * rc
        vs.append([bm.verts.new(V((math.cos(ang[i] + yaw) * base[i] * sh, math.sin(ang[i] + yaw) * base[i] * sh, z)) + off * math.sin(math.pi * t)) for i in range(n)])
    top = bm.verts.new(V((0, 0, z1 + rc * 0.12)) + V((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0)) * rc)
    for j in range(rings):
        for i in range(n): bm.faces.new((vs[j][i], vs[j][(i + 1) % n], vs[j + 1][(i + 1) % n], vs[j + 1][i]))
    for i in range(n): bm.faces.new((vs[-1][i], vs[-1][(i + 1) % n], top))
    bm.faces.new(vs[0][::-1])
    o = _new(bm, "column", m, loc=(x, y, 0))
    for v in o.data.vertices:
        v.co += V((v.co.x, v.co.y, 0)).normalized() * 0.08 * rc * noise.noise(v.co * 0.9 + V((x, y, 0)) * 0.3)
    return o

def hq_plateau(R=30.0, H=12.0, seed=1, bushes=True):
    rng = random.Random(seed); random.seed(seed)
    cyl(R - 1.0, H, 'RockDark', loc=(0, 0, H / 2 - 0.45), seg=64)
    cap = lathe([(0, H)] + [(R * f, H) for f in (0.08, 0.16, 0.26, 0.38, 0.5, 0.62, 0.74, 0.84, 0.92)] + [(R - 1.0, H), (R + 0.15, H), (R + 0.5, H - 0.18), (R + 0.55, H - 0.45), (R + 0.3, H - 0.75), (R - 0.4, H - 0.85), (0, H - 0.85)], 'Grass', seg=128)
    for v in cap.data.vertices:
        rr = V((v.co.x, v.co.y, 0)).length
        if rr > R - 0.2 and v.co.z < H - 0.05: v.co += V((v.co.x, v.co.y, 0)).normalized() * 0.25 * noise.noise(v.co * 0.4 + V((seed, 0, 0)))
    smooth_parts(cap)
    a = 0.0
    while a < 2 * math.pi - 0.02:
        rc = rng.uniform(1.3, 2.4); rr = R + rng.uniform(-0.9, 0.4)
        hq_rock_column(rr * math.cos(a), rr * math.sin(a), rc, -0.5 - rng.uniform(0.0, 1.2), H - 0.5 - rng.uniform(0.2, 2.8), rng.uniform(0, 6.28), 'Rock', rng)
        a += rc * 1.2 / rr
    # moss cushions + flowering bushes spilling over the rim, vines down the columns
    for k in range(int(R * 2.2)):
        b = rng.uniform(0, 2 * math.pi); rr = R + rng.uniform(0.0, 0.5)
        blob(rng.uniform(0.35, 0.7), 'Foliage', (rr * math.cos(b), rr * math.sin(b), H - 0.45 - rng.uniform(0.0, 0.3)), sub=2, lump=0.3, seed=k + seed * 100, scl=(1, 1, 0.65))
    if bushes:
        for k in range(int(R * 1.1)):
            b = rng.uniform(0, 2 * math.pi); rr = R - rng.uniform(0.6, 1.4)
            blob(rng.uniform(0.5, 0.85), 'Foliage', (rr * math.cos(b), rr * math.sin(b), H + 0.2), sub=3, lump=0.28, seed=k + seed * 50, scl=(1, 1, 0.8))
            for j in range(4):
                c = b + rng.uniform(-0.03, 0.03); q = rr + rng.uniform(-0.4, 0.4)
                flower((q * math.cos(c), q * math.sin(c), H + 0.65 + rng.uniform(-0.1, 0.15)), 0.12, ('Frosting', 'Glaze', 'Seed', 'Sprinkle1')[j])
    for k in range(int(R * 0.7)):
        b = rng.uniform(0, 2 * math.pi); rr = R + 0.45
        ivy((rr * math.cos(b), rr * math.sin(b), H - 0.7), rng.uniform(1.0, 3.5), rng)
    for k in range(int(R * 0.6)):
        b = rng.uniform(0, 2 * math.pi); rr = R + rng.uniform(0.8, 2.6); s = rng.uniform(0.5, 1.2)
        blob(s, 'Rock', (rr * math.cos(b), rr * math.sin(b), s * 0.3), sub=2, lump=0.25, seed=k + 7, scl=(1.2, 1.0, 0.7))

def hq_island(R=100.0, D=30.0, seed=15):
    hq_plateau(R, D, seed, bushes=False)
    smooth_parts(lathe([(0, -1.6), (R - 1.2, -1.6), (R * 0.82, -9.0), (R * 0.55, -17.0), (R * 0.25, -23.0), (0, -26.0)], 'Rock', seg=96))

# ---------------------------------------------------------------- plaza

def hq_round_plaza(r=8.0, h=0.8):
    lathe([(0, 0), (r, 0), (r, h - 0.06), (0, h - 0.06)], 'PinkStone', seg=96)
    rng = random.Random(12)
    # radial paving: rings of curved blocks
    for (r0, r1, n, m) in ((2.3, 3.3, 18, 'StoneTile'), (3.3, 4.5, 24, 'Cream'), (4.8, 6.0, 32, 'StoneTile'), (6.0, r - 0.3, 40, 'Sand')):
        for i in range(n):
            a0 = 2 * math.pi * i / n + 0.004; a1 = 2 * math.pi * (i + 1) / n - 0.004; seg_ = 4; bm = bmesh.new(); vb = []; vt = []
            hh = h + 0.02 + rng.uniform(-0.006, 0.006)
            for j in range(seg_ + 1):
                a = a0 + (a1 - a0) * j / seg_
                for rr in (r0 + 0.02, r1 - 0.02):
                    vb.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), h - 0.07))); vt.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), hh)))
            for j in range(seg_):
                q = [2 * j, 2 * j + 2, 2 * j + 3, 2 * j + 1]
                bm.faces.new([vt[k] for k in q]); bm.faces.new([vb[k] for k in q[::-1]])
                bm.faces.new((vb[2 * j], vb[2 * j + 2], vt[2 * j + 2], vt[2 * j])); bm.faces.new((vt[2 * j + 1], vt[2 * j + 3], vb[2 * j + 3], vb[2 * j + 1]))
            bm.faces.new((vb[0], vt[0], vt[1], vb[1])); bm.faces.new((vb[-1], vb[-2], vt[-2], vt[-1]))
            _new(bm, "pave", m)
    smooth_parts(lathe([(0, h - 0.01), (2.0, h - 0.01), (2.0, h + 0.025), (0, h + 0.025)], 'RedTile', seg=64))
    torus(2.15, 0.07, 'Gold', loc=(0, 0, h + 0.01), seg=96, rseg=8); smooth_parts(last())
    torus(4.65, 0.08, 'Gold', loc=(0, 0, h + 0.01), seg=128, rseg=8); smooth_parts(last())
    torus(r - 0.06, 0.1, 'Gold', loc=(0, 0, h - 0.04), seg=160, rseg=8); smooth_parts(last())
    pts = [((1.6 if k % 2 == 0 else 0.6) * math.cos(2 * math.pi * k / 16), (1.6 if k % 2 == 0 else 0.6) * math.sin(2 * math.pi * k / 16)) for k in range(16)]
    prism(pts, h + 0.02, h + 0.04, 'Gold')
    for k in range(4):
        a = k * math.pi / 2
        for i in range(4):
            z = h - (i + 1) * 0.2; d = r + 0.2 + i * 0.4; zz = max(z, 0.02)
            box(3.0, 0.42, zz, 'StoneTile' if i % 2 else 'PinkStone', loc=(d * math.cos(a), d * math.sin(a), zz / 2), rot=(0, 0, a + math.pi / 2), bevel=0.02)
        for sgn in (-1, 1):
            x = (r + 0.9) * math.cos(a) + sgn * 1.8 * math.cos(a + math.pi / 2); y = (r + 0.9) * math.sin(a) + sgn * 1.8 * math.sin(a + math.pi / 2)
            lantern_pillar(x, y, 0.0, h=1.1)
    for k in range(16):                                                             # flower beds around the rim
        a = 2 * math.pi * k / 16 + math.pi / 16
        if any(abs(math.remainder(a - q * math.pi / 2, 2 * math.pi)) < 0.35 for q in range(4)): continue
        p = V((math.cos(a), math.sin(a), 0)) * (r + 0.6)
        blob(0.45, 'Foliage', (p.x, p.y, 0.3), sub=3, lump=0.25, seed=k + 40, scl=(1.3, 1.3, 0.7))
        for j in range(5): flower((p.x + random.uniform(-0.3, 0.3), p.y + random.uniform(-0.3, 0.3), 0.62), 0.1, ('Frosting', 'Glaze', 'Seed', 'Sprinkle1', 'Frosting')[j])

# ---------------------------------------------------------------- §89.13 sky islands (floating, top at z = H) + switchback ramp

def hq_sky_island(R=30.0, H=6.0, D=22.0, seed=1, bushes=True):
    """floating sub-map island: grassy cap + rock-column band (z 0..H) + craggy inverted cone underneath (tip at z = -D)"""
    rng = random.Random(seed + 500); random.seed(seed + 500)
    cyl(R - 1.0, H + 0.1, 'RockDark', loc=(0, 0, (H - 0.9 - 1.0) / 2), seg=64)        # top stays under the grass cap (no coincident faces)
    cap = lathe([(0, H)] + [(R * f, H) for f in (0.08, 0.16, 0.26, 0.38, 0.5, 0.62, 0.74, 0.84, 0.92) if R * f < R - 1.2] + [(R - 1.0, H), (R + 0.15, H), (R + 0.5, H - 0.18), (R + 0.55, H - 0.45), (R + 0.3, H - 0.75), (R - 0.4, H - 0.85), (0, H - 0.85)], 'Grass', seg=128)
    for v in cap.data.vertices:
        rr = V((v.co.x, v.co.y, 0)).length
        if rr > R - 0.2 and v.co.z < H - 0.05: v.co += V((v.co.x, v.co.y, 0)).normalized() * 0.25 * noise.noise(v.co * 0.4 + V((seed, 0, 0)))
    smooth_parts(cap)
    a = 0.0
    while a < 2 * math.pi - 0.02:                                                   # rock-column band around the rim
        rc = rng.uniform(1.2, 2.2); rr = R + rng.uniform(-0.9, 0.3)
        hq_rock_column(rr * math.cos(a), rr * math.sin(a), rc, -1.2 - rng.uniform(0.0, 1.5), H - 0.5 - rng.uniform(0.2, 1.8), rng.uniform(0, 6.28), 'Rock', rng)
        a += rc * 1.2 / rr
    # craggy underside: noisy inverted cone + hanging rock lumps
    prof = [(R - 0.6, -0.8), (R * 0.93, -D * 0.12), (R * 0.8, -D * 0.3), (R * 0.62, -D * 0.5), (R * 0.42, -D * 0.68), (R * 0.22, -D * 0.86), (R * 0.06, -D * 0.98), (0, -D)]
    und = lathe([(0, -0.8)] + prof, 'Rock', seg=72)
    for v in und.data.vertices:
        rr = V((v.co.x, v.co.y, 0)).length
        if rr > 0.5: v.co += V((v.co.x, v.co.y, 0)).normalized() * (0.07 * R) * noise.noise(v.co * (2.2 / R) + V((seed * 3.1, 0, 0)))
        v.co.z += 0.06 * D * noise.noise(v.co * (1.7 / R) + V((0, seed * 1.7, 0)))
    for k in range(int(R * 0.9)):
        t = rng.uniform(0.05, 0.85); b = rng.uniform(0, 2 * math.pi)
        rr = R * (0.93 - 0.85 * t) * rng.uniform(0.92, 1.02); z = -D * (0.1 + 0.85 * t)
        s = rng.uniform(0.06, 0.13) * R * (1.0 - 0.5 * t)
        blob(s, 'Rock' if k % 3 else 'RockDark', (rr * math.cos(b), rr * math.sin(b), z), sub=2, lump=0.3, seed=k + seed * 70, scl=(1.0, 1.0, rng.uniform(1.2, 1.8)))
    for k in range(int(R * 0.35)):                                                  # moss patches on the upper underside
        b = rng.uniform(0, 2 * math.pi); rr = R * rng.uniform(0.88, 0.97)
        blob(rng.uniform(0.6, 1.2), 'Moss', (rr * math.cos(b), rr * math.sin(b), -D * rng.uniform(0.04, 0.14)), sub=2, lump=0.3, seed=k + seed * 90, scl=(1.4, 1.4, 0.5))
    for k in range(int(R * 2.2)):                                                   # moss cushions spilling over the rim
        b = rng.uniform(0, 2 * math.pi); rr = R + rng.uniform(0.0, 0.5)
        blob(rng.uniform(0.35, 0.7), 'Foliage', (rr * math.cos(b), rr * math.sin(b), H - 0.45 - rng.uniform(0.0, 0.3)), sub=2, lump=0.3, seed=k + seed * 100, scl=(1, 1, 0.65))
    if bushes:
        for k in range(int(R * 1.1)):
            b = rng.uniform(0, 2 * math.pi); rr = R - rng.uniform(0.6, 1.4)
            blob(rng.uniform(0.5, 0.85), 'Foliage', (rr * math.cos(b), rr * math.sin(b), H + 0.2), sub=3, lump=0.28, seed=k + seed * 50, scl=(1, 1, 0.8))
            for j in range(4):
                c = b + rng.uniform(-0.03, 0.03); q = rr + rng.uniform(-0.4, 0.4)
                flower((q * math.cos(c), q * math.sin(c), H + 0.65 + rng.uniform(-0.1, 0.15)), 0.12, ('Frosting', 'Glaze', 'Seed', 'Sprinkle1')[j])
    for k in range(int(R * 1.0)):                                                   # long vines hanging off the rim into the sky
        b = rng.uniform(0, 2 * math.pi); rr = R + 0.45
        ivy((rr * math.cos(b), rr * math.sin(b), H - 0.7), rng.uniform(2.0, 6.0), rng)

def hq_switch_ramp(H=24.0, W=3.4, lane=5.0, leg=26.0, top_extra=3.0, landing=3.0, T=0.45):
    """3-leg switchback ramp. Local +Y = toward the sky island. Leg 1 at x = +lane (entry at y = 0, z = 0),
    hairpin to the middle lane, back to y = 0, hairpin to x = -lane, leg 3 up to y = leg + top_extra (z = H), then a flat landing.
    Returns (top end x, top end y)."""
    r = lane / 2.0; Nh = 24; Nl = 26
    pts = []
    for i in range(Nl + 1): pts.append(Vector((lane, leg * i / Nl, 0)))                         # leg 1  (+Y)
    for i in range(1, Nh + 1):                                                                  # hairpin at +Y end: x lane -> 0
        a = math.pi * i / Nh; pts.append(Vector((r + r * math.cos(a), leg + r * math.sin(a), 0)))
    for i in range(1, Nl + 1): pts.append(Vector((0, leg - leg * i / Nl, 0)))                    # leg 2  (-Y)
    for i in range(1, Nh + 1):                                                                  # hairpin at y = 0: x 0 -> -lane
        a = math.pi * i / Nh; pts.append(Vector((-r + r * math.cos(a), -r * math.sin(a), 0)))
    y3 = leg + top_extra
    for i in range(1, Nl + 1): pts.append(Vector((-lane, y3 * i / Nl, 0)))                       # leg 3  (+Y) to the top
    Ls = [0.0]
    for i in range(1, len(pts)): Ls.append(Ls[-1] + (pts[i] - pts[i - 1]).length)
    tot = Ls[-1]
    for i, p in enumerate(pts): p.z = H * Ls[i] / tot
    for i in range(1, 5): pts.append(Vector((-lane, y3 + landing * i / 4, H)))                   # flat landing
    def frame(i):
        p = pts[i]; t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]); t.z = 0; t.normalize()
        return p, V((t.y, -t.x, 0)), V((0, 0, 1))
    path = [frame(i) for i in range(len(pts))]
    sweep(path, [(-W / 2, -0.08), (W / 2, -0.08), (W / 2, -T), (-W / 2, -T)], 'PinkStone')
    sweep(path, [(-W / 2 + 0.2, 0.0), (W / 2 - 0.2, 0.0), (W / 2 - 0.2, -0.1), (-W / 2 + 0.2, -0.1)], 'StoneTile')
    inner = path[1:-1]
    for sgn in (-1, 1):
        sweep(inner, [(sgn * (W / 2), 0.06), (sgn * (W / 2 + 0.12), 0.06), (sgn * (W / 2 + 0.12), -T - 0.05), (sgn * W / 2, -T - 0.05)][::(1 if sgn > 0 else -1)], 'Gold')
        sweep(inner, [(sgn * (W / 2 - 0.22), 0.14), (sgn * (W / 2 - 0.01), 0.14), (sgn * (W / 2 - 0.01), -0.05), (sgn * (W / 2 - 0.22), -0.05)][::(1 if sgn > 0 else -1)], 'Cream')
        rail = [p + sd * sgn * (W / 2 - 0.115) + V((0, 0, 0.12)) for (p, sd, up) in inner]
        # hairpin inner sides are tight: thin the rail points so posts do not pile up
        keep = [rail[0]]
        for q in rail[1:]:
            if (q - keep[-1]).length > 0.9: keep.append(q)
        balustrade(keep + [rail[-1]], h=1.0, spacing=0.5)
    for i in range(0, len(pts) - 4, 10):                                            # ornate support columns
        p, sd, up = path[i]
        if p.z < 1.2: continue
        for sgn in (-1, 1):
            q = p + sd * sgn * (W / 2 - 0.4); h = p.z - T + 0.06
            smooth_parts(lathe([(0, 0), (0.42, 0), (0.42, 0.18), (0.33, 0.3), (0.28, 0.45), (0.25, h - 0.55), (0.32, h - 0.42), (0.4, h - 0.3), (0.4, h - 0.18), (0, h - 0.18)], 'Cream', loc=(q.x, q.y, 0.0), seg=14))
            box(0.84, 0.84, 0.2, 'PinkStone', loc=(q.x, q.y, h - 0.09), bevel=0.03)
            cyl(0.44, 0.06, 'Gold', loc=(q.x, q.y, 0.2), seg=20)
    p, sd, up = path[0]
    for sgn in (-1, 1):
        q = p + sd * sgn * (W / 2 + 0.5); lantern_pillar(q.x, q.y, 0.0, h=1.0)
    p, sd, up = path[-1]
    for sgn in (-1, 1):
        q = p + sd * sgn * (W / 2 + 0.5); lantern_pillar(q.x, q.y, H, h=1.0)
    rng = random.Random(17)
    for k in range(14):
        i = rng.randrange(10, len(pts) - 10); p, sd, up = path[i]; sgn = rng.choice((-1, 1))
        ivy(p + sd * sgn * (W / 2 + 0.14) + V((0, 0, -0.1)), rng.uniform(0.6, 1.6), rng)
    return (-lane, y3 + landing), tot
