from lib import *
from lib import _new, _parts, PAL
import random, bmesh
from mathutils import noise
from objs_v2 import tube, leaf_blade, blob, smooth_parts, apple_fruit, market_stall_v2, crate as fruit_crate
from hq_props import grass_mound, flower, last, petal_flower, hq_lamp
from hq_struct import lantern_pillar, paving
from hq_nature import hq_candy_cane
V = Vector
# ===== HOB HQ remaining props + floor tiles =====

def plank_box(w, d, h, loc, m='Plank', rng=None):
    rng = rng or random.Random(1); x, y, z = loc; n = max(2, int(h / 0.16))
    for i in range(n):
        zz = z + h * (i + 0.5) / n
        for sgn in (-1, 1):
            box(w, 0.04, h / n - 0.01, m, loc=(x, y + sgn * (d / 2 - 0.02), zz), rot=(0, 0, rng.uniform(-0.01, 0.01)), bevel=0.008)
            box(0.04, d - 0.08, h / n - 0.01, m, loc=(x + sgn * (w / 2 - 0.02), y, zz), bevel=0.008)
    for sx in (-1, 1):
        for sy in (-1, 1): box(0.07, 0.07, h + 0.02, 'PlankDark', loc=(x + sx * (w / 2 - 0.035), y + sy * (d / 2 - 0.035), z + h / 2), bevel=0.01)

def hq_crate():
    rng = random.Random(3)
    plank_box(1.1, 1.1, 0.95, (0, 0, 0), rng=rng)
    box(1.04, 1.04, 0.05, 'PlankDark', loc=(0, 0, 0.97), bevel=0.01)
    for sgn in (-1, 1):                                                         # diagonal brace + gold corner caps
        stone = box(1.3, 0.05, 0.1, 'PlankDark', loc=(0, sgn * 0.56, 0.48), rot=(0, 0.72, 0), bevel=0.008)
    for sx in (-1, 1):
        for sy in (-1, 1): box(0.1, 0.1, 0.1, 'Gold', loc=(sx * 0.52, sy * 0.52, 0.96), bevel=0.015)

def hq_barrel(h=1.0, r=0.4):
    n = 18
    for i in range(n):                                                          # bulging staves
        a = 2 * math.pi * i / n; bm_pts = []
        prof = [(r * 0.88, 0), (r * 1.0, h * 0.5), (r * 0.88, h)]
        o = lathe([(rr, z) for rr, z in [(r * 0.86, 0.0), (r * 0.97, h * 0.25), (r * 1.0, h * 0.5), (r * 0.97, h * 0.75), (r * 0.86, h)]] + [(r * 0.8, h), (r * 0.8, 0.0)], 'Plank', seg=n, closed=True)
        break
    smooth_parts(o)
    for z, rr in ((0.12, 0.9), (0.38, 0.985), (0.62, 0.985), (0.88, 0.9)):
        torus(r * rr + 0.012, 0.022, 'Gold', loc=(0, 0, h * z), seg=48, rseg=6); smooth_parts(last())
    smooth_parts(lathe([(0, h - 0.03), (r * 0.82, h - 0.03), (r * 0.82, h - 0.06), (0, h - 0.06)], 'PlankDark', seg=32))
    for k in range(5): apple_fruit(0.08, (r * 0.4 * math.cos(k * 1.26), r * 0.4 * math.sin(k * 1.26), h + 0.02), ('Apple', 'Orange', 'Berry', 'Apple', 'Cherry')[k])

def hq_bench(L=1.8):
    for x in (-L / 2 + 0.15, L / 2 - 0.15):                                       # scrolled gold legs
        for y in (-0.16, 0.16):
            tube([V((x, y, 0)), V((x, y + 0.04, 0.2)), V((x - 0.03 * (1 if x > 0 else -1), y, 0.42))], [0.035, 0.03, 0.03], 'Gold', seg=8)
        box(0.08, 0.5, 0.06, 'Gold', loc=(x, 0, 0.42), bevel=0.01)
        tube([V((x, 0.2, 0.44)), V((x, 0.24, 0.7)), V((x, 0.22, 0.95))], [0.03, 0.03, 0.03], 'Gold', seg=8)
    for k in range(4): box(L, 0.1, 0.05, 'Plank', loc=(0, -0.2 + 0.12 * k, 0.47), bevel=0.012)
    for k in range(3): box(L - 0.1, 0.04, 0.09, 'Plank', loc=(0, 0.23 + 0.005 * k, 0.62 + 0.13 * k), rot=(0.25, 0, 0), bevel=0.01)
    for x in (-L / 2 + 0.15, L / 2 - 0.15): smooth_parts(uvs(0.045, 'Gold', loc=(x, 0.22, 0.97), seg=12, rings=8))

def hq_sack():
    from objs_items import _sack_body
    h, r = 0.95, 0.42
    _sack_body(h, r, 'Burlap', 7); smooth_parts(_parts[-1])
    smooth_parts(lathe([(r * 0.26, h * 0.88), (r * 0.36, h * 0.95), (r * 0.42, h * 1.02), (r * 0.30, h * 1.04), (r * 0.18, h * 0.96)], 'BurlapLight', seg=16, closed=True))
    torus(r * 0.29, 0.025, 'Banner', loc=(0, 0, h * 0.885), seg=24, rseg=8); smooth_parts(last())
    smooth_parts(uvs(0.09, 'White', loc=(0.05, -0.05, h * 1.0), scl=(1.2, 1, 0.5), seg=16, rings=8))
    box(0.35, 0.02, 0.24, 'Cream', loc=(0, -r * 1.0, h * 0.42), rot=(0.06, 0, 0), bevel=0.01)
    for k in range(3): smooth_parts(uvs(0.03, 'Berry', loc=(-0.08 + 0.08 * k, -r * 1.02, h * 0.42), scl=(1, 0.5, 1), seg=8, rings=6))

def hq_urn():
    prof = [(0, 0), (0.32, 0), (0.32, 0.08), (0.2, 0.14), (0.16, 0.3), (0.28, 0.45), (0.44, 0.7), (0.46, 0.85), (0.38, 0.98), (0.4, 1.02), (0.48, 1.08), (0.46, 1.14), (0.36, 1.12), (0.0, 1.0)]
    smooth_parts(lathe(prof, 'Cream', seg=40))
    for z in (0.08, 0.86, 1.08): torus(0.33 if z < 0.5 else 0.47, 0.025, 'Gold', loc=(0, 0, z), seg=48, rseg=6); smooth_parts(last())
    rng = random.Random(4)
    blob(0.38, 'Foliage', (0, 0, 1.2), sub=3, lump=0.25, seed=4, scl=(1.1, 1.1, 0.7))
    for k in range(9):
        a = rng.uniform(0, 2 * math.pi); rr = rng.uniform(0.05, 0.3)
        petal_flower(V((rr * math.cos(a), rr * math.sin(a), 1.42 + rng.uniform(-0.05, 0.05))), V((math.cos(a) * 0.3, math.sin(a) * 0.3, 1)).normalized(), 0.1, rng)
    for k in range(4):
        a = k * 1.57 + 0.4
        leaf_blade((0.38 * math.cos(a), 0.38 * math.sin(a), 1.15), (math.cos(a), math.sin(a), -0.4), 0.35, 0.09, 'Leaf', curl=0.5, thick=0.01, seg=8)

def hq_signpost():
    tube([V((0, 0, 0)), V((0.02, 0, 1.4)), V((0, 0.01, 2.8))], [0.08, 0.07, 0.065], 'Plank', seg=10)
    rng = random.Random(9)
    for k, (z, yaw, flip) in enumerate(((2.35, 0.3, 1), (1.85, -0.5, -1), (1.4, 0.9, 1))):
        L = 1.1; d = V((math.cos(yaw), math.sin(yaw), 0))
        pts = [(-0.05, -0.16), (L - 0.2, -0.16), (L, 0.0), (L - 0.2, 0.16), (-0.05, 0.16)]
        o = prism([(x * flip, y) for x, y in pts], -0.03, 0.03, 'Plank', loc=(0, 0, z), rot=(math.pi / 2, 0, yaw))
        smooth_parts(box(0.06, 0.07, 0.07, 'Gold', loc=(0, 0, z), bevel=0.01))
    smooth_parts(uvs(0.09, 'Gold', loc=(0, 0, 2.85), seg=12, rings=8))
    grass_mound(0.6, h=0.06, seed=19, rocks=2, flowers=8, hub=0.25)

def hq_treasure_chest():
    rng = random.Random(8)
    plank_box(1.2, 0.8, 0.55, (0, 0, 0), m='Plank', rng=rng)
    lid = cyl(0.4, 1.22, 'Plank', loc=(0, 0, 0.55), rot=(0, math.pi / 2, 0), seg=28); smooth_parts(lid)
    for x in (-0.5, 0.0, 0.5):
        torus(0.41, 0.025, 'Gold', loc=(x, 0, 0.55), rot=(0, math.pi / 2, 0), seg=32, rseg=6); smooth_parts(last())
    box(0.18, 0.06, 0.22, 'Gold', loc=(0, -0.42, 0.5), bevel=0.02); smooth_parts(uvs(0.035, 'Cherry', loc=(0, -0.46, 0.52), seg=10, rings=6))

def hq_footbridge():
    L = 6.2; W = 1.8; rng = random.Random(5)
    n = 22
    for i in range(n):                                                          # arched planks
        t = i / (n - 1); y = -L / 2 + L * t; z = 0.25 + 1.0 * math.sin(math.pi * t)
        a = math.atan2(1.0 * math.pi * math.cos(math.pi * t), L)
        box(W, L / n - 0.02, 0.07, 'Plank', loc=(y, 0, z), rot=(0, -a, 0), bevel=0.01) if False else box(L / n - 0.02, W, 0.07, 'Plank', loc=(y, 0, z), rot=(0, -a, 0), bevel=0.01)
    for sgn in (-1, 1):
        pts = [V((-L / 2 + L * t, sgn * (W / 2 - 0.05), 0.2 + 1.0 * math.sin(math.pi * t))) for t in [i / 16 for i in range(17)]]
        tube(pts, [0.07] * 17, 'PlankDark', seg=8)
        rail = [p + V((0, 0, 0.75)) for p in pts]
        tube(rail, [0.045] * 17, 'Plank', seg=8)
        for p in pts[::2]:
            tube([p, p + V((0, 0, 0.75))], [0.035, 0.035], 'Plank', seg=6)
        for p in (pts[0], pts[-1]): smooth_parts(uvs(0.07, 'Gold', loc=p + V((0, 0, 0.82)), seg=10, rings=6))
    for k in range(6): blob(0.25, 'Foliage', (rng.choice((-L / 2 - 0.1, L / 2 + 0.1)), rng.uniform(-W / 2, W / 2), 0.15), sub=2, lump=0.3, seed=k)

def hq_candy_arch(W=4.4, H=4.6):
    from objs2 import cane_path
    pts = [V((-W / 2, 0, 0)) + V((0, 0, H * 0.62 * i / 10)) for i in range(11)]
    R = W / 2
    pts += [V((-R * math.cos(math.pi * i / 20), 0, H * 0.62 + R * 0.75 * math.sin(math.pi * i / 20))) for i in range(1, 21)]
    pts += [V((W / 2, 0, H * 0.62 * (1 - i / 10))) for i in range(1, 11)]
    tube(pts, [0.16] * len(pts), 'Glaze', seg=16)
    fine = []
    for i in range(len(pts) - 1):
        for j in range(6): fine.append(pts[i].lerp(pts[i + 1], j / 6))
    fine.append(pts[-1])
    for band in range(3):
        sp = []
        for i, p in enumerate(fine):
            t = (fine[min(i + 1, len(fine) - 1)] - fine[max(i - 1, 0)]).normalized(); a = V((0, 1, 0))
            s_ = t.cross(a).normalized(); u = s_.cross(t).normalized(); ang = i * 0.3 + band * 2.094
            sp.append(p + (s_ * math.cos(ang) + u * math.sin(ang)) * 0.125)
        tube(sp, [0.055] * len(sp), 'Stripe', seg=8)
    top = V((0, 0, H * 0.62 + R * 0.75))
    for sgn in (-1, 1): smooth_parts(uvs(0.3, 'Banner', loc=top + V((sgn * 0.32, -0.1, 0.05)), scl=(1.3, 0.45, 0.85), seg=16, rings=10))
    smooth_parts(uvs(0.14, 'Banner', loc=top + V((0, -0.14, 0.05)), seg=12, rings=8))
    for sgn in (-1, 1):
        leaf_blade(top + V((sgn * 0.1, -0.2, -0.05)), (sgn * 0.3, -0.2, -1), 0.8, 0.12, 'Banner', curl=0.1, thick=0.02, seg=8)
    for x in (-W / 2, W / 2):
        smooth_parts(lathe([(0, 0), (0.32, 0), (0.32, 0.18), (0.22, 0.26), (0, 0.26)], 'PinkStone', loc=(x, 0, 0), seg=20))
        blob(0.4, 'Foliage', (x, 0.0, 0.25), sub=3, lump=0.25, seed=int(x * 10) + 50, scl=(1.3, 1.3, 0.7))
        for j in range(5): flower((x + random.uniform(-0.3, 0.3), random.uniform(-0.3, 0.3), 0.55), 0.09, ('Frosting', 'Glaze', 'Seed', 'Frosting', 'Sprinkle1')[j])

def hq_candy_cane_lamp():
    hq_candy_cane(3.0, 0.45, 0.09, (0, 0, 0.3), 0.0)
    smooth_parts(lathe([(0, 0), (0.4, 0), (0.4, 0.15), (0.25, 0.3), (0, 0.3)], 'PinkStone', seg=24))
    from hq_struct import lantern
    tube([V((0.9, 0, 3.3)), V((0.9, 0, 3.05))], [0.015, 0.015], 'Gold', seg=6)
    lantern((0.9, 0, 2.6), 1.0)
    for sgn in (-1, 1): smooth_parts(uvs(0.13, 'Banner', loc=(sgn * 0.14, -0.08, 2.0), scl=(1.3, 0.5, 0.85), seg=14, rings=8))

def hq_fruit_cart():
    rng = random.Random(3)
    plank_box(2.0, 1.1, 0.5, (0, 0, 0.55), rng=rng)
    for sgn in (-1, 1):                                                         # spoked wheels
        c = V((0.5, sgn * 0.62, 0.48))
        torus(0.45, 0.05, 'PlankDark', loc=c, rot=(math.pi / 2, 0, 0), seg=40, rseg=8); smooth_parts(last())
        torus(0.47, 0.02, 'Gold', loc=c, rot=(math.pi / 2, 0, 0), seg=40, rseg=6); smooth_parts(last())
        for k in range(8):
            a = math.pi * k / 4; tube([c, c + V((math.cos(a) * 0.43, 0, math.sin(a) * 0.43))], [0.025, 0.025], 'Plank', seg=6)
        smooth_parts(uvs(0.08, 'Gold', loc=c + V((0, sgn * 0.03, 0)), seg=12, rings=8))
    for sgn in (-1, 1): tube([V((-1.0, sgn * 0.4, 0.7)), V((-1.6, sgn * 0.42, 0.6)), V((-2.1, sgn * 0.45, 0.45))], [0.04, 0.035, 0.03], 'Plank', seg=8)
    box(0.1, 1.0, 0.1, 'PlankDark', loc=(-0.9, 0, 0.3), bevel=0.01); box(0.08, 0.08, 0.3, 'PlankDark', loc=(-0.9, 0, 0.15))
    for k in range(40):                                                         # heaped fruit
        x = rng.uniform(-0.85, 0.85); y = rng.uniform(-0.45, 0.45); z = 1.02 + 0.18 * (1 - abs(x) / 0.9) * (1 - abs(y) / 0.5) + rng.uniform(0, 0.06)
        apple_fruit(rng.uniform(0.08, 0.11), (x, y, z), rng.choice(('Apple', 'Orange', 'Berry', 'Apple', 'Grape')))
    for x in (-0.95, 0.95):
        tube([V((x, -0.5, 1.05)), V((x, -0.5, 1.9))], [0.03, 0.03], 'Gold', seg=6); tube([V((x, 0.5, 1.05)), V((x, 0.5, 1.9))], [0.03, 0.03], 'Gold', seg=6)
    from objs_v2 import scallop_awning
    scallop_awning(2.2, 1.3, 1.95, slope=0.25, nstripe=8, drop=0.16)

def hq_cloud(scale=1.6, seed=5):
    rng = random.Random(seed)
    for k in range(9):
        a = rng.uniform(0, 2 * math.pi); rr = scale * rng.uniform(0.2, 1.2)
        r = scale * rng.uniform(0.45, 0.85) * (1.2 - rr / (scale * 1.6))
        blob(r, 'CloudPink', (rr * math.cos(a) * 1.6, rr * math.sin(a) * 0.8, r * 0.55 + rng.uniform(0, scale * 0.3)), sub=3, lump=0.12, freq=1.2, seed=k + seed)
    blob(scale * 0.9, 'CloudPink', (0, 0, scale * 0.75), sub=3, lump=0.1, seed=seed + 99)

# ---------------------------------------------------------------- floor tiles (1 x 1 m modules)

def tile_slab(m='StoneTile'):
    box(1.0, 1.0, 0.06, m, loc=(0, 0, 0.03))

def hq_tile_checker():
    rng = random.Random(1); tile_slab()
    for i in range(4):
        for j in range(4):
            hh = 0.05 + rng.uniform(-0.005, 0.006)
            box(0.23, 0.23, hh, 'StoneTile' if (i + j) % 2 else 'PinkStone', loc=(-0.375 + 0.25 * i, -0.375 + 0.25 * j, 0.06 + hh / 2), rot=(0, 0, rng.uniform(-0.03, 0.03)), bevel=0.012)

def hq_tile_diamond():
    rng = random.Random(2); tile_slab()
    for i in range(4):
        for j in range(4): box(0.23, 0.23, 0.05, 'StoneTile', loc=(-0.375 + 0.25 * i, -0.375 + 0.25 * j, 0.085), rot=(0, 0, rng.uniform(-0.03, 0.03)), bevel=0.012)
    prism([(0.36, 0), (0, 0.36), (-0.36, 0), (0, -0.36)], 0.105, 0.12, 'RedTile'); prism([(0.2, 0), (0, 0.2), (-0.2, 0), (0, -0.2)], 0.11, 0.126, 'Gold')

def hq_tile_circle():
    rng = random.Random(3); tile_slab()
    smooth_parts(lathe([(0, 0.11), (0.46, 0.11), (0.48, 0.09), (0.48, 0.06), (0, 0.06)], 'StoneTile', seg=48))
    torus(0.33, 0.025, 'Gold', loc=(0, 0, 0.11), seg=48, rseg=6); smooth_parts(last())
    for k in range(8):
        a0 = 2 * math.pi * k / 8 + 0.03; a1 = 2 * math.pi * (k + 1) / 8 - 0.03
        prism([(0.06 * math.cos(a0), 0.06 * math.sin(a0)), (0.3 * math.cos(a0), 0.3 * math.sin(a0)), (0.3 * math.cos(a1), 0.3 * math.sin(a1)), (0.06 * math.cos(a1), 0.06 * math.sin(a1))], 0.105, 0.118, 'Sand' if k % 2 else 'Cream')

def hq_tile_red():
    rng = random.Random(4); tile_slab()
    for i in range(2):
        for j in range(2):
            box(0.47, 0.47, 0.05, 'RedTile', loc=(-0.25 + 0.5 * i, -0.25 + 0.5 * j, 0.085), rot=(0, 0, rng.uniform(-0.02, 0.02)), bevel=0.015)
    prism([(0.12, 0), (0, 0.12), (-0.12, 0), (0, -0.12)], 0.105, 0.12, 'Gold')

def hq_tile_grass(flowers=False):
    rng = random.Random(6)
    box(1.0, 1.0, 0.08, 'Soil', loc=(0, 0, 0.04), bevel=0.01)
    smooth_parts(box(0.98, 0.98, 0.06, 'Grass', loc=(0, 0, 0.1), bevel=0.02))
    for k in range(30):
        x = rng.uniform(-0.45, 0.45); y = rng.uniform(-0.45, 0.45); b = rng.uniform(0, 6.28)
        leaf_blade((x, y, 0.12), (0.2 * math.cos(b), 0.2 * math.sin(b), 1), 0.1, 0.012, 'Leaf', curl=0.3, thick=0.003, seg=3)
    if flowers:
        for k in range(10): petal_flower(V((rng.uniform(-0.42, 0.42), rng.uniform(-0.42, 0.42), 0.16)), V((0, 0, 1)), 0.05, rng)
