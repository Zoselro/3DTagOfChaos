from lib import *
from lib import _new, _parts, PAL
import random, bmesh
from mathutils import noise
from objs_v2 import tube, leaf_blade, blob, smooth_parts, apple_fruit
from hq_props import grass_mound, flower, last, petal_flower, hq_cupcake, branch, conform
from hq_struct import lantern_pillar
V = Vector
for _k, _c in {'Pine': (0.16, 0.38, 0.22), 'Stripe': (0.82, 0.14, 0.26)}.items(): PAL.setdefault(_k, _c)
# ===== HOB HQ nature + sweets (concept sheet look) =====

def fruit_dots(blobs, rng, n, mat='Apple', r=(0.08, 0.12)):
    bpy.context.view_layer.update()
    for o in blobs:
        mw = o.matrix_world; vs = list(o.data.vertices)
        for k in range(n):
            v = rng.choice(vs); nrm = (mw.to_3x3() @ v.normal).normalized()
            if nrm.z < -0.3: continue
            rr = rng.uniform(*r); p = mw @ v.co + nrm * rr * 0.6
            apple_fruit(rr, (p.x, p.y, p.z - rr * 0.3), mat)

def trunk(h, base=0.26, top=0.11, lean=0.08, seed=0, roots=5):
    rng = random.Random(seed)
    pts = [V((lean * math.sin(t * 2.4 + seed), lean * 0.7 * math.cos(t * 1.9 + seed), h * t)) for t in [i / 10 for i in range(11)]]
    tube(pts, [base * 1.6 * (1 - t) ** 4 + (base - (base - top) * t) for t in [i / 10 for i in range(11)]], 'Bark', seg=14)
    for k in range(roots):
        a = k * 2 * math.pi / roots + rng.uniform(-0.3, 0.3); d = V((math.cos(a), math.sin(a), 0))
        tube([V((0, 0, base * 1.2)), d * base * 1.5 + V((0, 0, base * 0.3)), d * base * 2.6 + V((0, 0, -0.03))], [base * 0.45, base * 0.3, base * 0.08], 'Bark', seg=8)
    return pts

def hq_round_tree(h=3.2, seed=8, fruit=True):
    rng = random.Random(seed)
    pts = trunk(h * 0.55, seed=seed)
    cz = h * 0.72; blobs = []
    for k, (x, y, z, r) in enumerate(((0, 0, 0.2, 1.0), (0.6, 0.25, -0.12, 0.7), (-0.6, -0.3, -0.1, 0.72), (0.1, -0.62, 0.02, 0.66), (-0.2, 0.62, 0.05, 0.66), (0.3, 0.15, 0.62, 0.62), (-0.32, -0.1, 0.55, 0.58))):
        blobs.append(blob(r, 'Foliage', (x, y, cz + z), sub=3, lump=0.2, freq=2.0, seed=k + seed * 7))
    for a in (0.6, 2.7, 4.4):
        b0 = pts[6]; d = V((math.cos(a), math.sin(a), 1.0)).normalized()
        tube([b0, b0 + d * 0.5, b0 + d * 0.9], [0.08, 0.06, 0.04], 'Bark', seg=8)
    if fruit: fruit_dots(blobs, rng, 4, 'Apple', (0.09, 0.12))
    grass_mound(1.05, h=0.14, seed=seed + 3, rocks=3, flowers=16, hub=0.25)

def hq_pine(h=5.0, seed=3, ornaments=True):
    rng = random.Random(seed)
    trunk(h * 0.35, base=0.2, top=0.12, lean=0.03, seed=seed, roots=4)
    tiers = 5; R0 = h * 0.24
    for i in range(tiers):
        t = i / tiers; z0 = h * (0.18 + 0.78 * t); R = R0 * (1 - 0.78 * t) + 0.12; hh = h * 0.28 * (1 - 0.45 * t)
        prof = [(0, z0 + hh)]
        for j in range(1, 9):
            u = j / 8; prof.append((R * u ** 0.85, z0 + hh * (1 - u) ** 1.1))
        prof += [(R * 1.02, z0 - hh * 0.08), (R * 0.75, z0 - hh * 0.02), (R * 0.3, z0 + hh * 0.25), (0, z0 + hh * 0.3)]
        o = lathe(prof, 'Pine', seg=40)
        for v in o.data.vertices:                                               # scalloped drooping skirt edge + needle lumps
            a = math.atan2(v.co.y, v.co.x); rr = V((v.co.x, v.co.y, 0)).length
            if rr > R * 0.6:
                v.co.z -= R * 0.12 * (0.5 + 0.5 * math.sin(a * 9 + i)) * (rr / R) ** 2
            k = 1 + 0.06 * noise.noise(v.co * 2.2 + V((seed, i, 0))); v.co.x *= k; v.co.y *= k
        smooth_parts(o)
        if ornaments and i < tiers - 1:
            for k in range(5):
                a = rng.uniform(0, 2 * math.pi)
                apple_fruit(0.07, (R * 0.95 * math.cos(a), R * 0.95 * math.sin(a), z0 - hh * 0.02 - 0.06), 'Cherry')
    smooth_parts(uvs(0.09, 'Gold', loc=(0, 0, h * 1.0), seg=12, rings=8))
    grass_mound(R0 * 1.15, h=0.12, seed=seed + 5, rocks=3, flowers=12, hub=0.3)

def hq_almond_tree(h=3.4, seed=31):
    rng = random.Random(seed)
    pts = trunk(h * 0.55, seed=seed)
    cz = h * 0.78; blobs = []
    for k, (x, y, z, r) in enumerate(((0, 0, 0.12, 1.05), (0.75, 0.3, -0.1, 0.75), (-0.7, -0.35, -0.12, 0.8), (0.1, -0.7, 0.05, 0.7), (-0.2, 0.7, 0.1, 0.7), (0.2, 0.1, 0.6, 0.6))):
        blobs.append(blob(r, 'LeafLight', (x, y, cz + z), sub=3, lump=0.2, seed=k + 60))
    bpy.context.view_layer.update()
    for o in blobs:                                                              # almonds + white almond blossoms
        mw = o.matrix_world
        for v in rng.sample(list(o.data.vertices), 14):
            nrm = (mw.to_3x3() @ v.normal).normalized(); p = mw @ v.co + nrm * 0.05
            if rng.random() < 0.55:
                a = uvs(0.07, 'Almond', loc=p, scl=(0.75, 0.75, 1.35), seg=10, rings=7); smooth_parts(a)
            else: petal_flower(p, nrm, 0.1, rng, 'Glaze')
    grass_mound(1.1, h=0.14, seed=seed, rocks=3, flowers=14, hub=0.25)

def hq_bush(r=0.6, berries=False, seed=11, flowers=0, mound=True):
    rng = random.Random(seed); blobs = []
    for k in range(6):
        a = k * 1.05; blobs.append(blob(r * rng.uniform(0.5, 0.68), 'Foliage', (r * 0.45 * math.cos(a), r * 0.45 * math.sin(a), r * 0.5 + rng.uniform(-0.05, 0.08)), sub=3, lump=0.22, seed=k + seed))
    blobs.append(blob(r * 0.66, 'Foliage', (0, 0, r * 0.85), sub=3, lump=0.2, seed=seed + 9))
    if berries: fruit_dots(blobs, rng, 5, 'Berry', (0.05, 0.07))
    if flowers:
        bpy.context.view_layer.update()
        for o in blobs:
            mw = o.matrix_world
            for v in rng.sample(list(o.data.vertices), flowers):
                nrm = (mw.to_3x3() @ v.normal).normalized()
                if nrm.z > -0.2: petal_flower(mw @ v.co + nrm * 0.02, nrm, r * 0.16, rng)
    if mound: grass_mound(r * 1.25, h=0.08, seed=seed, rocks=2, flowers=6, hub=0.4)

def hq_flower_bed(r=1.0):
    rng = random.Random(5)
    n = 14
    for i in range(n):                                                          # stone ring planter
        a = 2 * math.pi * (i + 0.5) / n; p = V((math.cos(a), math.sin(a), 0)) * r * 0.92
        box(2 * math.pi * r / n * 0.95, 0.2, 0.32, 'PinkStone', loc=(p.x, p.y, 0.16), rot=(0, 0, a + math.pi / 2), bevel=0.03)
    torus(r * 0.92, 0.03, 'Gold', loc=(0, 0, 0.33), seg=64, rseg=6); smooth_parts(last())
    smooth_parts(lathe([(0, 0.36), (r * 0.82, 0.3), (r * 0.82, 0.1), (0, 0.1)], 'Soil', seg=40))
    for k in range(9):
        a = rng.uniform(0, 2 * math.pi); rr = r * 0.55 * math.sqrt(rng.random())
        blob(r * 0.22, 'Foliage', (rr * math.cos(a), rr * math.sin(a), 0.42), sub=2, lump=0.25, seed=k, scl=(1, 1, 0.8))
    for k in range(26):
        a = rng.uniform(0, 2 * math.pi); rr = r * 0.7 * math.sqrt(rng.random())
        petal_flower(V((rr * math.cos(a), rr * math.sin(a), 0.55 + rng.uniform(0, 0.12))), V((0, 0, 1)), r * rng.uniform(0.09, 0.13), rng)

def hq_hedge(L=2.0, h=0.9):
    rng = random.Random(7)
    box(L, 0.95, 0.18, 'PinkStone', loc=(0, 0, 0.09), bevel=0.03)
    for k in range(int(L / 0.32)):
        x = -L / 2 + 0.2 + k * 0.32
        for y in (-0.18, 0.18):
            blob(0.3, 'Foliage', (x + rng.uniform(-0.05, 0.05), y, 0.55 + rng.uniform(-0.05, 0.05)), sub=2, lump=0.2, seed=k * 2 + (y > 0), scl=(1, 1, 1.1))
    for k in range(10):
        petal_flower(V((rng.uniform(-L / 2 + 0.1, L / 2 - 0.1), rng.uniform(-0.35, 0.35), h + rng.uniform(-0.05, 0.05))), V((0, 0, 1)), 0.08, rng)

def hq_rock_bush():
    rng = random.Random(2)
    blob(0.9, 'Rock', (0, 0, 0.35), sub=3, lump=0.3, seed=4, scl=(1.4, 1.0, 0.75))
    blob(0.55, 'Rock', (0.9, 0.3, 0.2), sub=3, lump=0.3, seed=5, scl=(1.2, 1, 0.7))
    hq_bush(0.55, seed=21, flowers=3, mound=False)
    grass_mound(1.5, h=0.1, seed=6, rocks=4, flowers=12, hub=0.3)

def hq_rock_mushroom():
    rng = random.Random(3)
    blob(1.0, 'Rock', (0, 0, 0.4), sub=3, lump=0.3, seed=8, scl=(1.3, 1.0, 0.8))
    for k, (x, y, s) in enumerate(((0.6, -0.5, 0.45), (-0.4, -0.7, 0.32), (0.95, 0.2, 0.3), (-0.8, 0.1, 0.25))):
        z = 0.45 if k == 0 else 0.25
        smooth_parts(lathe([(0, z), (s * 0.28, z), (s * 0.22, z + s * 0.9), (0, z + s * 0.9)], 'Glaze', loc=(x, y, 0), seg=16))
        smooth_parts(lathe([(0, z + s * 1.25), (s * 0.5, z + s * 1.15), (s * 0.85, z + s * 0.92), (s * 0.82, z + s * 0.84), (s * 0.2, z + s * 0.86), (0, z + s * 0.86)], 'Mushroom', loc=(x, y, 0), seg=32))
    grass_mound(1.6, h=0.1, seed=9, rocks=3, flowers=14, hub=0.3)

# ---------------------------------------------------------------- fruits & sweets

def hq_grapes(s=1.8):
    rng = random.Random(6)
    for i in range(7):
        t = i / 6; rr = s * 0.24 * (1 - 0.75 * t) + 0.02; z = s * (0.95 - 0.62 * t)
        n = max(1, int(3 + 7 * (1 - t)))
        for k in range(n):
            a = 2 * math.pi * k / n + i * 0.6
            g = uvs(s * 0.085, 'Grape', loc=(rr * math.cos(a), rr * math.sin(a), z), scl=(1, 1, 1.08), seg=20, rings=14); smooth_parts(g)
    tube([V((0, 0, s * 0.95)), V((0, 0, s * 1.08)), V((s * 0.05, 0, s * 1.16))], [s * 0.03, s * 0.025, s * 0.02], 'Stem', seg=8)
    leaf_blade((s * 0.04, 0, s * 1.1), (1, 0.2, 0.3), s * 0.42, s * 0.2, 'Leaf', curl=0.3, thick=s * 0.012, seg=12)
    pts = [V((s * 0.06 + 0.06 * math.cos(t * 9) * t, 0.06 * math.sin(t * 9) * t - s * 0.05, s * 1.12 + t * 0.15)) for t in [i / 14 for i in range(15)]]
    tube(pts, [0.012] * 15, 'Stem', seg=6)
    grass_mound(s * 0.42, h=s * 0.06, seed=12, rocks=3, flowers=10, hub=0.35)

def hq_cherries(s=1.2):
    for x in (-0.22, 0.22):
        c = uvs(s * 0.24, 'Cherry', loc=(x * s, 0, s * 0.27), scl=(1, 1, 0.92), seg=32, rings=20); smooth_parts(c)
        tube([V((x * s, 0, s * 0.48)), V((x * s * 0.6, 0, s * 0.8)), V((0, 0, s * 1.05))], [s * 0.025, s * 0.022, s * 0.02], 'Stem', seg=8)
    leaf_blade((0, 0, s * 1.03), (0.6, 0.5, 0.2), s * 0.38, s * 0.15, 'Leaf', curl=0.3, thick=s * 0.012, seg=12)
    grass_mound(s * 0.6, h=s * 0.06, seed=14, rocks=2, flowers=10, hub=0.4)

def hq_pumpkin(s=2.2):
    R = s * 0.5
    prof = [(0, 0.04), (0.35, 0.01), (0.75, 0.08), (0.97, 0.28), (1.0, 0.5), (0.92, 0.72), (0.65, 0.86), (0.3, 0.9), (0.12, 0.86), (0, 0.84)]
    o = lathe([(R * r, s * 0.8 * z) for r, z in prof], 'Pumpkin', seg=96)
    for v in o.data.vertices:                                                   # 10 lobes
        a = math.atan2(v.co.y, v.co.x); k = 1 + 0.07 * (abs(math.cos(a * 5)) - 0.6)
        v.co.x *= k; v.co.y *= k
    smooth_parts(o)
    pts = [V((0, 0, s * 0.66)), V((0.02 * s, 0, s * 0.78)), V((0.08 * s, 0.02 * s, s * 0.86)), V((0.13 * s, 0.02 * s, s * 0.84))]
    tube(pts, [s * 0.06, s * 0.05, s * 0.04, s * 0.03], 'Stem', seg=10)
    for a in (0.8, 3.0, 4.6):
        leaf_blade((0, 0, s * 0.68), (math.cos(a), math.sin(a), -0.1), s * 0.35, s * 0.16, 'Leaf', curl=0.5, thick=s * 0.012, seg=12)
    pts = [V((s * 0.05 + 0.1 * math.cos(t * 11) * t, 0.1 * math.sin(t * 11) * t, s * 0.68 + 0.05 * t)) for t in [i / 16 for i in range(17)]]
    tube(pts, [0.015] * 17, 'Leaf', seg=6)
    grass_mound(s * 0.6, h=s * 0.05, seed=15, rocks=3, flowers=12, hub=0.42)

def hq_cupcake_cream(): hq_cupcake(1.8, top='Cream', wrapper='Sprinkle2')

def hq_strawberry_cupcake(s=1.4):
    random.seed(8)
    hq_cupcake(s, top='Frosting', wrapper='Glaze')
    # swap the cherry for a mini strawberry (cherry = the last 'Cherry' uv sphere part) — add a strawberry on top
    o = lathe([(s * 0.11 * r, s * (1.0 + 0.2 * z)) for r, z in ((0, 0), (0.3, 0.05), (0.7, 0.3), (1.0, 0.62), (0.9, 0.9), (0.4, 1.0), (0, 1.0))], 'Berry', seg=24)
    smooth_parts(o)

def hq_choco_cake(s=1.0):
    smooth_parts(lathe([(0, 0), (1.05 * s, 0), (1.05 * s, 0.06 * s), (0, 0.06 * s)], 'Gold', seg=64))
    smooth_parts(lathe([(0, 0.06 * s), (0.95 * s, 0.06 * s), (0.95 * s, 0.75 * s), (0, 0.75 * s)], 'Choco', seg=64))
    torus(0.96 * s, 0.03 * s, 'Glaze', loc=(0, 0, 0.42 * s), seg=64, rseg=8); smooth_parts(last())
    from objs_v2 import drip_ring
    drip_ring(0.95 * s, 0.75 * s, 'Glaze', n=22, seed=4, maxlen=0.28 * s, thick=0.05 * s)
    smooth_parts(lathe([(0, 0.82 * s), (0.9 * s, 0.8 * s), (0.97 * s, 0.75 * s), (0, 0.75 * s)], 'Glaze', seg=64))
    for k in range(8):
        a = 2 * math.pi * k / 8
        o = lathe([(0.1 * s * r, z * 0.24 * s + 0.8 * s) for r, z in ((0, 0), (0.3, 0.05), (0.75, 0.32), (1.0, 0.65), (0.9, 0.92), (0.4, 1.0), (0, 1.0))], 'Berry', loc=(0.72 * s * math.cos(a), 0.72 * s * math.sin(a), 0), seg=20)
        smooth_parts(o)
    smooth_parts(uvs(0.12 * s, 'Cherry', loc=(0, 0, 0.95 * s), seg=24, rings=16))

def hq_chocolate_bar(L=2.0, W=1.0, T=0.4):
    smooth_parts(box(L, W, T * 0.5, 'Choco', loc=(0, 0, T * 0.25), bevel=0.03))
    nx, ny = 4, 2
    for i in range(nx):
        for j in range(ny):
            x = -L / 2 + L * (i + 0.5) / nx; y = -W / 2 + W * (j + 0.5) / ny
            b = box(L / nx - 0.06, W / ny - 0.06, T * 0.3, 'Choco', loc=(x, y, T * 0.5 + T * 0.1), bevel=0.04)
    smooth_parts(box(L * 0.42, W + 0.04, T * 0.82, 'Gold', loc=(L * 0.3, 0, T * 0.41), bevel=0.02))
    smooth_parts(box(L * 0.3, W + 0.06, T * 0.84, 'Banner', loc=(L * 0.36, 0, T * 0.42), bevel=0.02))

def hq_truffles():
    rng = random.Random(3)
    smooth_parts(lathe([(0, 0), (1.05, 0), (1.1, 0.08), (1.02, 0.12), (0, 0.1)], 'Gold', seg=64))
    for k, (x, y) in enumerate(((0, 0), (0.6, 0.15), (-0.55, 0.25), (0.15, 0.62), (0.1, -0.62), (-0.4, -0.45), (0.62, -0.42))):
        r = 0.3; c = uvs(r, 'Choco', loc=(x, y, 0.1 + r * 0.92), scl=(1, 1, 0.9), seg=28, rings=18); smooth_parts(c)
        m = ('Glaze', 'Frosting', 'Sprinkle3')[k % 3]
        pts = [V((x + r * 0.9 * math.cos(t), y + r * 0.9 * math.sin(t) * 0.95, 0.1 + r * 1.05 + 0.08 * math.sin(t * 3))) for t in [i / 24 * 2 * math.pi * 1.5 for i in range(25)]]
        tube(pts, [0.025] * 25, m, seg=6)

def hq_candy_cane(h=1.5, rh=0.32, r=0.07, loc=(0, 0, 0), yaw=0.0):
    from objs2 import cane_path
    pts = cane_path(h, rh, 0.0, 16, 18)
    q = Matrix.Rotation(yaw, 4, 'Z')
    pts = [V(loc) + (q @ p) for p in pts]
    # twisted stripes: two interleaved helical bands on a white core
    tube(pts, [r] * len(pts), 'Glaze', seg=16)
    from objs_v2 import tube as tb
    L = [0.0]
    for i in range(1, len(pts)): L.append(L[-1] + (pts[i] - pts[i - 1]).length)
    fine = []
    for i in range(len(pts) - 1):
        for j in range(6): fine.append(pts[i].lerp(pts[i + 1], j / 6))
    fine.append(pts[-1])
    for band in range(3):
        sp = []
        for i, p in enumerate(fine):
            t = (fine[min(i + 1, len(fine) - 1)] - fine[max(i - 1, 0)]).normalized(); a = V((0, 0, 1)) if abs(t.z) < 0.9 else V((1, 0, 0))
            s_ = t.cross(a).normalized(); u = s_.cross(t).normalized(); ang = i * 0.35 + band * 2.094
            sp.append(p + (s_ * math.cos(ang) + u * math.sin(ang)) * r * 0.78)
        tb(sp, [r * 0.34] * len(sp), 'Stripe', seg=8)
    return pts

def hq_candy_canes():
    for k, (x, y, h, yaw) in enumerate(((-0.4, 0, 2.2, 0.3), (0.35, 0.2, 1.8, 2.6), (0.05, -0.35, 1.5, 4.4))):
        hq_candy_cane(h, 0.3, 0.075, (x, y, 0), yaw)
    # bow ribbon
    for sgn in (-1, 1):
        smooth_parts(uvs(0.16, 'Banner', loc=(sgn * 0.18, 0, 0.9), scl=(1.3, 0.5, 0.8), seg=16, rings=10))
    smooth_parts(uvs(0.08, 'Banner', loc=(0, 0, 0.9), seg=12, rings=8))
    grass_mound(0.9, h=0.08, seed=17, rocks=2, flowers=10, hub=0.35)

def hq_candy_cane_s():
    hq_candy_cane(1.2, 0.26, 0.06, (0, 0, 0), 0.0)
    for sgn in (-1, 1): smooth_parts(uvs(0.11, 'Banner', loc=(0.0, sgn * 0.12, 0.85), scl=(0.5, 1.3, 0.8), seg=14, rings=8))

def hq_choco_pillar(h=3.5):
    smooth_parts(lathe([(0, 0), (0.55, 0), (0.55, 0.2), (0.45, 0.3), (0.4, h - 0.5), (0.5, h - 0.4), (0.5, h - 0.25), (0, h - 0.25)], 'Choco', seg=32))
    for z in (0.5, 1.4, 2.3):
        torus(0.41, 0.04, 'Gold', loc=(0, 0, z), seg=48, rseg=6); smooth_parts(last())
    from objs_v2 import drip_ring
    drip_ring(0.5, h - 0.28, 'Glaze', n=12, seed=2, maxlen=0.4, thick=0.06)
    smooth_parts(lathe([(0, h + 0.05), (0.45, h - 0.12), (0.52, h - 0.25), (0, h - 0.25)], 'Glaze', seg=32))
    smooth_parts(uvs(0.16, 'Cherry', loc=(0, 0, h + 0.12), seg=20, rings=14))

def hq_choco_fountain():
    """3-tier chocolate fountain with flowing drips, stone basin with gold rim and candle pillars (concept)"""
    rng = random.Random(4)
    smooth_parts(lathe([(0, 0), (5.0, 0), (5.0, 0.45), (4.6, 0.55), (4.4, 0.25), (0, 0.25)], 'PinkStone', seg=96))
    torus(4.85, 0.09, 'Gold', loc=(0, 0, 0.52), seg=128, rseg=8); smooth_parts(last())
    smooth_parts(lathe([(0, 0.3), (4.45, 0.3), (4.45, 0.38), (0, 0.38)], 'Choco', seg=96))           # chocolate pool
    from objs_v2 import drip_ring
    for (r, z, hh) in ((2.6, 0.3, 1.5), (1.7, 1.9, 1.2), (0.95, 3.2, 1.0)):
        smooth_parts(lathe([(0, z), (r * 0.35, z), (r * 0.3, z + hh * 0.6), (r * 0.95, z + hh * 0.85), (r, z + hh), (0, z + hh)], 'Choco', seg=64))
        drip_ring(r, z + hh, 'ChocoFlow', n=int(r * 10), seed=int(r * 10), maxlen=hh * 0.7, thick=0.1)
        torus(r * 0.98, 0.05, 'Gold', loc=(0, 0, z + hh * 0.85), seg=64, rseg=6); smooth_parts(last())
    smooth_parts(uvs(0.5, 'Choco', loc=(0, 0, 4.6), scl=(1, 1, 1.2), seg=32, rings=20))
    smooth_parts(uvs(0.25, 'Cherry', loc=(0, 0, 5.3), seg=24, rings=16))
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        lantern_pillar(4.75 * math.cos(a), 4.75 * math.sin(a), 0.45, h=0.6)

# ---------------------------------------------------------------- cake houses

def hq_house_cake():
    from objs_v2 import house_cake_v2
    house_cake_v2()

def hq_house_tier():
    from objs_v2 import drip_ring, bead_ring, arched_window, leaf_blade as lb
    from objs_house import door_cut, door_frames
    random.seed(10)
    lathe([(0, 0), (4.4, 0), (4.6, 0.12), (4.5, 0.26), (4.25, 0.3), (0, 0.3)], 'PinkStone', seg=72); smooth_parts(last())
    bead_ring(4.38, 0.3, 'Glaze', n=56, br=0.12)
    w = lathe([(3.25, 0.3), (3.6, 0.3), (3.6, 3.6), (3.25, 3.6)], 'CakeSponge', seg=72, closed=True); door_cut(w, 3.6, 0.3)
    drip_ring(3.6, 3.6, 'Frosting', n=30, maxlen=0.8, thick=0.12)
    smooth_parts(lathe([(0, 3.55), (3.62, 3.55), (3.62, 3.7), (0, 3.7)], 'Frosting', seg=72))
    smooth_parts(lathe([(0, 3.7), (2.5, 3.7), (2.5, 6.2), (0, 6.2)], 'Cream', seg=64))
    drip_ring(2.5, 6.2, 'Glaze', n=22, maxlen=0.6, thick=0.1)
    smooth_parts(lathe([(0, 6.2), (1.5, 6.2), (1.5, 7.9), (0, 7.9)], 'Frosting', seg=48))
    drip_ring(1.5, 7.9, 'Glaze', n=14, maxlen=0.45, thick=0.08)
    for (r, z, n) in ((3.65, 3.65, 18), (2.52, 6.25, 14), (1.52, 7.95, 10)):
        for k in range(n):
            a = 2 * math.pi * k / n; c = uvs(0.13 if r > 2 else 0.1, 'Berry', loc=(r * math.cos(a), r * math.sin(a), z + 0.12), seg=14, rings=10); smooth_parts(c)
    smooth_parts(uvs(0.45, 'Glaze', loc=(0, 0, 8.25), scl=(1, 1, 0.6), seg=24, rings=14))
    smooth_parts(uvs(0.28, 'Cherry', loc=(0, 0, 8.7), seg=24, rings=16))
    for a in (math.pi / 2 + 0.9, math.pi / 2 - 0.9, -math.pi / 2 + 0.9, -math.pi / 2 - 0.9): arched_window(a, 3.6, 2.2, w=0.8, h=1.1)
    for a in (0.0, math.pi): arched_window(a, 2.5, 5.0, w=0.6, h=0.9)
    door_frames(3.6, 0.3)

def hq_house_strawberry():
    from objs_v2 import bead_ring, arched_window
    from objs_house import door_cut, door_frames
    rng = random.Random(12)
    lathe([(0, 0), (4.6, 0), (4.7, 0.12), (4.6, 0.25), (0, 0.25)], 'PinkStone', seg=72); smooth_parts(last())
    prof = [(0.0, 0.25), (2.5, 0.25), (3.7, 1.0), (4.3, 2.4), (4.4, 3.8), (4.0, 5.2), (3.0, 6.2), (1.6, 6.7), (0, 6.75)]
    body = lathe([(r, z) for r, z in prof], 'Berry', seg=96, closed=False)
    for v in body.data.vertices:
        a = math.atan2(v.co.y, v.co.x); k = 1 + 0.03 * math.sin(a * 4 + v.co.z)
        v.co.x *= k; v.co.y *= k
    door_cut(body, 4.3, 0.25)
    smooth_parts(body)
    for k in range(10):                                                         # giant sepals on the roof
        a = 2 * math.pi * k / 10
        lf = leaf_blade((math.cos(a) * 0.4, math.sin(a) * 0.4, 6.85), (math.cos(a), math.sin(a), 0.05), 2.3, 0.55, 'Leaf', curl=0.6, thick=0.08, seg=14)
        conform(lf, body, 0.05, 0.08)
    tube([V((0, 0, 6.8)), V((0.1, 0, 7.4)), V((0.4, 0.05, 7.8))], [0.18, 0.15, 0.12], 'Stem', seg=12)
    for a in (math.pi / 2 + 1.0, math.pi / 2 - 1.0, -math.pi / 2 + 1.0, -math.pi / 2 - 1.0):
        arched_window(a, 4.3, 3.0, w=0.9, h=1.2)
    door_frames(4.3, 0.25)
    bead_ring(4.55, 0.25, 'Glaze', n=60, br=0.12)
    for k in range(14):                                                         # flower bed ring around the base
        a = 2 * math.pi * k / 14 + 0.2
        if abs(math.remainder(a + math.pi / 2, 2 * math.pi)) < 0.5 or abs(math.remainder(a - math.pi / 2, 2 * math.pi)) < 0.5: continue
        p = V((math.cos(a), math.sin(a), 0)) * 4.75
        blob(0.4, 'Foliage', (p.x, p.y, 0.3), sub=2, lump=0.25, seed=k, scl=(1.2, 1.2, 0.8))
        for j in range(3): flower((p.x + rng.uniform(-0.25, 0.25), p.y + rng.uniform(-0.25, 0.25), 0.62), 0.1, ('Frosting', 'Glaze', 'Seed')[j])
