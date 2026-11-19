from lib import *
from lib import _new, _parts, PAL
import random, bmesh
from mathutils import noise
from objs_v2 import tube, leaf_blade, blob, smooth_parts
V = Vector
for _k in ('OrangePeel','OrangeCut','Pith','StoneTile','Blossom','Foliage'): PAL.setdefault(_k, (0.8, 0.8, 0.8))
# ===== HOB HQ pilot props (concept sheet look): smooth sculpted forms, details as geometry, surface detail from hqmat textures =====

def last(): return _parts[-1]

def conform(o, body, offset, thick=0.0):
    """move verts of o that are inside body out to body surface + offset (leaves draped over a fruit)"""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get(); tree = BVHTree.FromObject(body, dg)
    mw = o.matrix_world; inv = mw.inverted(); bw = body.matrix_world; binv = bw.inverted()
    for v in o.data.vertices:
        p = binv @ (mw @ v.co); loc, nrm, idx, dist = tree.find_nearest(p)
        if loc is None: continue
        top = (v.index % 6) < 3 if v.index < len(o.data.vertices) - 0 else False   # leaf_blade rows: 3 top verts then 3 bottom
        want = offset + (thick if top else 0.0)
        if (p - loc).dot(nrm) < want: v.co = inv @ (bw @ (loc + nrm * want))

def grass_mound(r, h=0.18, seed=1, rocks=3, flowers=24, hub=0.0):
    """grassy dirt mound under props (concept: every prop sits on a mossy flowered patch)"""
    random.seed(seed)
    o = lathe([(0, h), (r * 0.5, h * 0.98), (r * 0.8, h * 0.8), (r * 0.97, h * 0.45), (r * 1.03, h * 0.12), (r * 0.98, 0), (0, 0)], 'Grass', seg=64)
    for v in o.data.vertices:
        if v.co.z > 0.01:
            a = math.atan2(v.co.y, v.co.x); v.co.z += h * 0.35 * noise.noise(V((math.cos(a) * 2, math.sin(a) * 2, seed)))
            k = 1 + 0.06 * noise.noise(V((math.cos(a) * 3, math.sin(a) * 3, seed + 4))); v.co.x *= k; v.co.y *= k
    smooth_parts(o)
    for k in range(rocks):
        a = random.uniform(0, 2 * math.pi); rr = r * random.uniform(0.75, 0.95)
        blob(r * random.uniform(0.08, 0.14), 'Rock', (rr * math.cos(a), rr * math.sin(a), h * 0.3), sub=2, lump=0.25, seed=k + seed, scl=(1.2, 1, 0.7))
    for k in range(flowers):
        a = random.uniform(0, 2 * math.pi); rr = r * math.sqrt(random.uniform(0.15, 0.85))
        flower((rr * math.cos(a), rr * math.sin(a), h * (1 - (rr / r) ** 2 * 0.5)), r * random.uniform(0.05, 0.075), ('Frosting', 'Glaze', 'Seed')[k % 3])
    for k in range(int(r * 40)):                                                # leafy clumps (3-5 broad leaves each), densest round the prop's foot
        a = random.uniform(0, 2 * math.pi); rr = r * (hub + (0.95 - hub) * random.random() ** 1.6); z = h * (1 - (rr / r) ** 2 * 0.6)
        for j in range(random.randint(3, 5)):
            b = random.uniform(0, 2 * math.pi)
            leaf_blade((rr * math.cos(a), rr * math.sin(a), z - 0.005), (math.cos(b), math.sin(b), 0.9), r * random.uniform(0.1, 0.16), r * 0.05, 'Leaf', curl=0.6, thick=0.006, seg=6)
    for k in range(int(r * 6)):                                                 # little round bushlets on the rim
        a = random.uniform(0, 2 * math.pi); rr = r * random.uniform(0.7, 0.95)
        blob(r * random.uniform(0.07, 0.11), 'Foliage', (rr * math.cos(a), rr * math.sin(a), h * 0.5), sub=3, lump=0.25, seed=k + seed * 7, scl=(1, 1, 0.8))

def blade(p, L, a):
    d = V((0.25 * math.cos(a), 0.25 * math.sin(a), 1)).normalized()
    leaf_blade(p, d, L, L * 0.12, 'Leaf', curl=0.25, thick=0.003, seg=3)

def flower(p, s, m):
    for k in range(5):
        a = 2 * math.pi * k / 5
        sph(s * 0.55, m, loc=(p[0] + s * 0.6 * math.cos(a), p[1] + s * 0.6 * math.sin(a), p[2] + s * 0.15), scl=(1, 0.7, 0.35), rot=(0, 0, a), sub=1)
        smooth_parts(last())
    sph(s * 0.35, 'Seed', loc=(p[0], p[1], p[2] + s * 0.25), scl=(1, 1, 0.6), sub=1); smooth_parts(last())

def hq_strawberry(s=2.0):
    R = s * 0.48
    prof = [(0.0, 0.0), (0.14, 0.02), (0.38, 0.12), (0.62, 0.3), (0.82, 0.5), (0.95, 0.7), (1.0, 0.86), (0.96, 0.98), (0.8, 1.07), (0.5, 1.12), (0.0, 1.12)]
    body = lathe([(R * r, s * 0.84 * z + s * 0.08) for r, z in prof], 'Berry', seg=64)
    for v in body.data.vertices:                                                  # gentle lobes (not a perfect lathe)
        a = math.atan2(v.co.y, v.co.x); k = 1 + 0.035 * math.sin(a * 3 + v.co.z * 2) + 0.02 * noise.noise(v.co * 2.5)
        v.co.x *= k; v.co.y *= k
    smooth_parts(body)
    top = s * 0.08 + s * 0.84 * 1.1
    rng = random.Random(3)
    for k in range(10):                                                           # sepals drape over the shoulders
        a = k * 2 * math.pi / 10 + rng.uniform(-0.12, 0.12)
        base = V((math.cos(a) * R * 0.12, math.sin(a) * R * 0.12, top + s * 0.04))
        lf = leaf_blade(base, (math.cos(a), math.sin(a), -0.05), s * rng.uniform(0.34, 0.42), s * rng.uniform(0.09, 0.115), 'Leaf', curl=0.55, thick=s * 0.016, seg=14)
        conform(lf, body, s * 0.008, s * 0.014)
    smooth_parts(lathe([(0, top - 0.02), (R * 0.3, top - 0.03), (R * 0.22, top + s * 0.03), (0, top + s * 0.04)], 'Leaf', seg=24))
    tube([V((0, 0, top)), V((0.02 * s, 0, top + s * 0.1)), V((0.06 * s, 0.01 * s, top + s * 0.18)), V((0.1 * s, 0.01 * s, top + s * 0.21))],
         [s * 0.04, s * 0.032, s * 0.026, s * 0.02], 'Stem', seg=10)
    leaf_blade((0.08 * s, 0.01 * s, top + s * 0.19), (1, 0.4, 0.3), s * 0.26, s * 0.11, 'Leaf', curl=0.3, thick=s * 0.012, seg=12)
    grass_mound(s * 0.58, h=s * 0.12, seed=3, rocks=4, flowers=26, hub=0.35)

def hq_apple(s=2.0):
    R = s * 0.5
    prof = [(0.0, 0.06), (0.25, 0.02), (0.6, 0.1), (0.9, 0.3), (1.0, 0.52), (0.95, 0.75), (0.78, 0.93), (0.48, 1.0), (0.22, 0.97), (0.08, 0.94), (0.0, 0.93)]
    body = lathe([(R * r, s * z) for r, z in prof], 'Apple', seg=64)
    for v in body.data.vertices:
        a = math.atan2(v.co.y, v.co.x); k = 1 + 0.03 * math.sin(a * 5) * math.sin(math.pi * min(1, v.co.z / s)) + 0.015 * noise.noise(v.co * 2)
        v.co.x *= k; v.co.y *= k
    smooth_parts(body)
    tube([V((0, 0, s * 0.8)), V((0.03 * s, 0, s * 0.95)), V((0.07 * s, 0.01 * s, s * 1.06))], [s * 0.03, s * 0.025, s * 0.02], 'Stem', seg=10)
    leaf_blade((0.06 * s, 0.01 * s, s * 1.0), (1, 0.3, 0.25), s * 0.36, s * 0.13, 'Leaf', curl=0.25, thick=s * 0.012, seg=12)
    grass_mound(s * 0.55, h=s * 0.08, seed=5, rocks=3, flowers=12)

def hq_orange(s=1.6):
    """orange with its front sliced off: radial segments + white pith ring on the cut face"""
    R = s * 0.5; cz = R + s * 0.05; cut = R * 0.42
    peel = uvs(R, 'OrangePeel', loc=(0, 0, cz), seg=72, rings=48); smooth_parts(peel)
    bool_diff(peel, box(R * 3, R * 2, R * 3, 'OrangePeel', loc=(0, -cut - R, cz)))
    rc = math.sqrt(R * R - cut * cut)
    rim = cyl(rc, R * 0.02, 'Pith', loc=(0, -cut + R * 0.005, cz), rot=(math.pi / 2, 0, 0), seg=72); smooth_parts(rim)
    n = 11
    for k in range(n):                                                            # segment wedges, slightly domed, gaps = pith lines
        a0 = 2 * math.pi * k / n; a1 = 2 * math.pi * (k + 1) / n; bm = bmesh.new()
        ctr = bm.verts.new((0, 0, 0.006)); ring = []
        for j in range(9):
            a = a0 + (a1 - a0) * (0.07 + 0.86 * j / 8); rr = rc * 0.9
            ring.append(bm.verts.new((rr * 0.18 * math.cos(a), rr * 0.18 * math.sin(a), 0.01)))
        outer = []
        for j in range(9):
            a = a0 + (a1 - a0) * (0.07 + 0.86 * j / 8); rr = rc * 0.9
            outer.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), 0.004)))
        for j in range(8):
            bm.faces.new((ring[j], ring[j + 1], outer[j + 1], outer[j]))
            bm.faces.new((ctr, ring[j + 1], ring[j]))
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=R * 0.02)
        sg = _new(bm, "seg", 'OrangeCut', loc=(0, -cut - R * 0.012, cz), rot=(math.pi / 2, 0, 0)); smooth_parts(sg)
    top = V((0, 0, cz + R * 0.98))
    tube([top - V((0, 0, 0.03)), top + V((0.01, 0, s * 0.07))], [s * 0.028, s * 0.022], 'Stem', seg=8)
    for k in range(2):
        a = 1.2 + k * 2.0
        leaf_blade(top + V((0, 0, s * 0.05)), (math.cos(a), math.sin(a), 0.25), s * 0.32, s * 0.12, 'Leaf', curl=0.35, thick=s * 0.014, seg=12)
    grass_mound(s * 0.62, h=s * 0.09, seed=7, rocks=3, flowers=14, hub=0.4)

def hq_cupcake(s=1.8, top='Frosting', wrapper='WrapperPink'):
    random.seed(4)
    bm = bmesh.new(); seg = 128; rows = 10; rings = []
    for j in range(rows + 1):
        t = j / rows; z = s * 0.42 * t; r = s * (0.31 + 0.1 * t)
        rings.append([bm.verts.new(((r + s * 0.014 * math.cos(28 * 2 * math.pi * i / seg)) * math.cos(2 * math.pi * i / seg),
                                    (r + s * 0.014 * math.cos(28 * 2 * math.pi * i / seg)) * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    for j in range(rows):
        for i in range(seg): bm.faces.new((rings[j][i], rings[j][(i + 1) % seg], rings[j + 1][(i + 1) % seg], rings[j + 1][i]))
    bm.faces.new(rings[0][::-1]); smooth_parts(_new(bm, "wrapper", wrapper))
    torus(s * 0.42, s * 0.018, 'Gold', loc=(0, 0, s * 0.42), seg=96, rseg=8); smooth_parts(last())
    torus(s * 0.315, s * 0.014, 'Gold', loc=(0, 0, s * 0.01), seg=96, rseg=8); smooth_parts(last())
    sp = lathe([(s * 0.41, s * 0.4), (s * 0.45, s * 0.45), (s * 0.4, s * 0.52), (0, s * 0.54)], 'CakeSponge', seg=96)
    for v in sp.data.vertices: v.co += v.co.normalized() * s * 0.012 * noise.noise(v.co * 6)
    smooth_parts(sp)
    star = [((1.0 if k % 2 == 0 else 0.74) * math.cos(2 * math.pi * k / 16), (1.0 if k % 2 == 0 else 0.74) * math.sin(2 * math.pi * k / 16)) for k in range(16)]
    turns = 2.6; steps = 300; path = []
    for i in range(steps + 1):
        t = i / steps; a = 2 * math.pi * turns * t
        R = s * (0.3 * (1 - t) ** 0.9 + 0.01); z = s * (0.58 + 0.42 * t)
        c = V((R * math.cos(a), R * math.sin(a), z)); tang = V((-math.sin(a), math.cos(a), 0.15)).normalized()
        up = (V((math.cos(a), math.sin(a), 0)) * 0.25 + V((0, 0, 1))).normalized(); side = tang.cross(up).normalized(); up2 = side.cross(tang).normalized()
        th = s * 0.15 * (1 - 0.62 * t) * (min(1.0, i / 10) ** 0.5); tw = a * 0.5
        path.append((c, side, up2, th, tw))
    bm = bmesh.new(); rings = []
    for c, sd, up, th, tw in path:
        rings.append([bm.verts.new(c + (sd * (x * math.cos(tw) - y * math.sin(tw)) + up * (x * math.sin(tw) + y * math.cos(tw))) * th) for x, y in star])
    for k in range(len(rings) - 1):
        for i in range(16): bm.faces.new((rings[k][i], rings[k][(i + 1) % 16], rings[k + 1][(i + 1) % 16], rings[k + 1][i]))
    tip = bm.verts.new(path[-1][0] + V((0, 0, s * 0.06)))
    for i in range(16): bm.faces.new((rings[-1][i], rings[-1][(i + 1) % 16], tip))
    bm.faces.new(rings[0][::-1]); smooth_parts(_new(bm, "swirl", top))
    for k in range(80):
        t = random.uniform(0.02, 0.88); a = random.uniform(0, 2 * math.pi)
        R = s * (0.3 * (1 - t) ** 0.9 + 0.01) + s * 0.14 * (1 - 0.62 * t); z = s * (0.58 + 0.42 * t) + s * 0.05
        cyl(s * 0.008, s * 0.04, 'Sprinkle%d' % (k % 4 + 1), loc=(R * math.cos(a), R * math.sin(a), z), rot=(random.uniform(0, 3), random.uniform(0, 3), 0), seg=8)
        smooth_parts(last())
    ch = uvs(s * 0.095, 'Cherry', loc=(0, 0, s * 1.1), scl=(1, 1, 0.92), seg=32, rings=20); smooth_parts(ch)
    tube([V((0, 0, s * 1.17)), V((s * 0.03, 0, s * 1.25)), V((s * 0.08, 0, s * 1.3))], [s * 0.01, s * 0.009, s * 0.008], 'Stem', seg=8)
    grass_mound(s * 0.5, h=s * 0.05, seed=9, rocks=2, flowers=10)

def hq_lamp(h=3.6):
    """ornate gold lantern post on a pink stone pedestal (concept 'lamp post'), glass glows"""
    box(0.9, 0.9, 0.25, 'PinkStone', loc=(0, 0, 0.125), bevel=0.04); box(0.7, 0.7, 0.5, 'PinkStone', loc=(0, 0, 0.5), bevel=0.04)
    box(0.8, 0.8, 0.08, 'Gold', loc=(0, 0, 0.78), bevel=0.02)
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):                               # gold medallions on the pedestal sides
        p0 = V((sx * 0.355, sy * 0.355, 0.5))
        cyl(0.13, 0.02, 'Gold', loc=p0, rot=(math.pi / 2 if sy else 0, math.pi / 2 if sx else 0, 0), seg=24); smooth_parts(last())
        sph(0.05, 'Frosting', loc=p0 + V((sx, sy, 0)) * 0.015, scl=(1, 1, 1), sub=2); smooth_parts(last())
    for z in (0.26, 0.74):
        box(0.74, 0.74, 0.03, 'Gold', loc=(0, 0, z), bevel=0.01)
    prof = [(0, 0.8), (0.2, 0.8), (0.2, 0.86), (0.15, 0.92), (0.16, 1.0), (0.11, 1.08), (0.09, 1.3), (0.075, h - 1.0), (0.11, h - 0.95), (0.11, h - 0.9),
            (0.08, h - 0.86), (0.08, h - 0.72), (0.13, h - 0.68), (0.14, h - 0.62), (0, h - 0.6)]
    smooth_parts(lathe(prof, 'Gold', seg=32))
    for k in range(4):                                                            # scroll brackets under the lantern
        a = k * math.pi / 2; d = V((math.cos(a), math.sin(a), 0))
        pts = [V((0, 0, h - 1.05)) + d * 0.08, V((0, 0, h - 0.92)) + d * 0.22, V((0, 0, h - 0.74)) + d * 0.2, V((0, 0, h - 0.66)) + d * 0.12]
        tube(pts, [0.025, 0.022, 0.02, 0.02], 'Gold', seg=8)
    zc = h - 0.62; hl = 0.7; r = 0.3
    smooth_parts(lathe([(0, zc), (r * 0.8, zc), (r * 0.8, zc + hl), (0, zc + hl)], 'Glow', seg=6))
    for k in range(6):                                                            # cage frame
        a = 2 * math.pi * k / 6 + math.pi / 6; p = V((math.cos(a) * r * 0.86, math.sin(a) * r * 0.86, 0))
        tube([p + V((0, 0, zc - 0.02)), p * 1.12 + V((0, 0, zc + hl * 0.5)), p + V((0, 0, zc + hl + 0.02))], [0.018, 0.018, 0.018], 'Gold', seg=8)
    smooth_parts(lathe([(0, zc - 0.06), (r * 1.05, zc - 0.06), (r * 1.05, zc), (0, zc)], 'Gold', seg=6))
    smooth_parts(lathe([(0, zc + hl), (r * 1.25, zc + hl), (r * 1.2, zc + hl + 0.05), (r * 0.5, zc + hl + 0.25), (0.06, zc + hl + 0.34), (0, zc + hl + 0.36)], 'Gold', seg=6))
    smooth_parts(uvs(0.06, 'Gold', loc=(0, 0, zc + hl + 0.42), seg=16, rings=10))
    tube([V((0, 0, zc + hl + 0.46)), V((0, 0, zc + hl + 0.58))], [0.015, 0.002], 'Gold', seg=6)
    for k in range(6):                                                            # roof curls + jewel
        a = 2 * math.pi * k / 6 + math.pi / 6; d = V((math.cos(a), math.sin(a), 0))
        tube([d * r * 1.2 + V((0, 0, zc + hl + 0.04)), d * r * 1.45 + V((0, 0, zc + hl + 0.12)), d * r * 1.35 + V((0, 0, zc + hl + 0.2)), d * r * 1.2 + V((0, 0, zc + hl + 0.16))],
             [0.016, 0.016, 0.014, 0.01], 'Gold', seg=8)
    sph(0.05, 'Cherry', loc=(0, 0, zc + hl + 0.33), sub=2); smooth_parts(last())
    for z in (1.1, 1.6, h - 1.2):
        smooth_parts(lathe([(0.0, z - 0.04), (0.12, z - 0.04), (0.14, z), (0.12, z + 0.04), (0.0, z + 0.04)], 'Gold', seg=24))
    grass_mound(0.75, h=0.06, seed=11, rocks=3, flowers=10)

def branch(p, d, L, r, depth, out, rng):
    pts = [p]; rad = [r]; q = V(p); dd = V(d)
    for i in range(5):
        dd = (dd + V((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), rng.uniform(-0.05, 0.15)))).normalized()
        q = q + dd * (L / 5); pts.append(q.copy()); rad.append(r * (1 - 0.55 * (i + 1) / 5))
    tube(pts, rad, 'Bark', seg=max(6, 12 - depth * 3))
    if depth >= 2: out.append((q, r)); return
    for k in range(2 if depth else 3):
        a = rng.uniform(0, 2 * math.pi); nd = (dd * 0.6 + V((math.cos(a), math.sin(a), 0.5)) * 0.7).normalized()
        branch(pts[3 + (k % 2)], nd, L * 0.65, rad[3] * 0.85, depth + 1, out, rng)
    out.append((q, r))

def hq_blossom_tree(h=4.2, seed=2):
    rng = random.Random(seed)
    # trunk with root flare + twist
    pts = [V((0.12 * math.sin(t * 3), 0.08 * math.cos(t * 2.3), h * 0.42 * t)) for t in [i / 10 for i in range(11)]]
    tube(pts, [0.42 * (1 - t) ** 4 + 0.2 * (1 - 0.4 * t) for t in [i / 10 for i in range(11)]], 'Bark', seg=16)
    for k in range(6):
        a = k * 1.047 + 0.2; d = V((math.cos(a), math.sin(a), 0))
        tube([V((0, 0, 0.35)), d * 0.38 + V((0, 0, 0.1)), d * 0.7 + V((0, 0, -0.02))], [0.12, 0.08, 0.03], 'Bark', seg=8)
    tips = []
    for k in range(3):
        a = k * 2.1 + 0.4
        branch(pts[-1], V((math.cos(a) * 0.6, math.sin(a) * 0.6, 0.8)).normalized(), h * 0.45, 0.13, 0, tips, rng)
    # canopy: blossom blobs around every branch tip + a central mass
    for i, (q, r) in enumerate(tips):
        blob(rng.uniform(0.55, 0.8), 'Blossom', (q.x, q.y, q.z + 0.15), sub=3, lump=0.22, freq=2.2, seed=i + seed * 10)
    blob(1.1, 'Blossom', (0, 0, h * 0.82), sub=3, lump=0.2, freq=2.0, seed=99)
    bpy.context.view_layer.update()
    canopy = [o for o in _parts if o.data.materials and o.data.materials[0].name.endswith('Blossom')]
    for cb in canopy:
        mw = cb.matrix_world
        for v in cb.data.vertices:
            nrm = (mw.to_3x3() @ v.normal).normalized()
            if nrm.z < -0.55 or rng.random() > 0.75: continue
            for j in range(2):
                jit = V((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 0.06
                petal_flower(mw @ v.co + nrm * 0.015 + jit, (nrm + jit * 3).normalized(), rng.uniform(0.11, 0.17), rng)
    grass_mound(1.6, h=0.18, seed=13, rocks=5, flowers=40)
    for k in range(26):                                                           # fallen blossoms on the grass
        a = rng.uniform(0, 2 * math.pi); rr = rng.uniform(0.6, 1.5)
        petal_flower(V((rr * math.cos(a), rr * math.sin(a), 0.18 * max(0, 1 - (rr / 1.6) ** 2 * 0.6) + 0.02)), V((0, 0, 1)), rng.uniform(0.09, 0.13), rng)

def petal_flower(p, nrm, size, rng, mat=None):
    """5 cupped rounded petals (2 quads each) + small fan centre, facing along nrm"""
    q = nrm.to_track_quat('Z', 'Y'); spin = rng.uniform(0, 6.28)
    bm = bmesh.new()
    for k in range(5):
        a = spin + 2 * math.pi * k / 5; d = V((math.cos(a), math.sin(a), 0)); sd = V((-d.y, d.x, 0))
        v0 = bm.verts.new(d * size * 0.08 + V((0, 0, size * 0.06)))
        v1 = bm.verts.new(d * size * 0.45 + sd * size * 0.36 + V((0, 0, size * 0.16)))
        v2 = bm.verts.new(d * size * 0.95 + sd * size * 0.22 + V((0, 0, size * 0.28)))
        v3 = bm.verts.new(d * size * 0.95 - sd * size * 0.22 + V((0, 0, size * 0.28)))
        v4 = bm.verts.new(d * size * 0.45 - sd * size * 0.36 + V((0, 0, size * 0.16)))
        bm.faces.new((v0, v1, v2, v3, v4))
    for v in bm.verts: v.co = q @ v.co + p
    o = _new(bm, "petals", mat or ('Frosting', 'Glaze', 'Frosting', 'Sprinkle1')[rng.randrange(4)]); smooth_parts(o)
    bm = bmesh.new(); c = bm.verts.new(V((0, 0, size * 0.14))); ring = [bm.verts.new(V((math.cos(2 * math.pi * k / 5) * size * 0.12, math.sin(2 * math.pi * k / 5) * size * 0.12, size * 0.08))) for k in range(5)]
    for k in range(5): bm.faces.new((c, ring[k], ring[(k + 1) % 5]))
    for v in bm.verts: v.co = q @ v.co + p
    _new(bm, "centre", 'Seed' if rng.random() < 0.5 else 'Cherry')

def hq_tile_star():
    """modular floor tile: chunky worn stone blocks around a gold/purple star inlay"""
    rng = random.Random(21)
    box(1.0, 1.0, 0.06, 'StoneTile', loc=(0, 0, 0.03))
    n = 4; c = 1.0 / n
    for i in range(n):
        for j in range(n):
            x = -0.5 + c * (i + 0.5); y = -0.5 + c * (j + 0.5)
            hgt = 0.05 + rng.uniform(-0.006, 0.008)
            b = box(c - 0.018, c - 0.018, hgt, 'StoneTile', loc=(x, y, 0.06 + hgt / 2), rot=(rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02), rng.uniform(-0.03, 0.03)), bevel=0.012)
            for v in b.data.vertices:
                if v.co.z > 0: v.co.z += 0.004 * noise.noise(V((x * 9 + v.co.x * 7, y * 9 + v.co.y * 7, 1)))
    pts = [((0.42 if k % 2 == 0 else 0.16) * math.cos(2 * math.pi * k / 16), (0.42 if k % 2 == 0 else 0.16) * math.sin(2 * math.pi * k / 16)) for k in range(16)]
    prism(pts, 0.105, 0.122, 'Gold')
    pts2 = [((0.3 if k % 2 == 0 else 0.11) * math.cos(2 * math.pi * k / 16 + math.pi / 8), (0.3 if k % 2 == 0 else 0.11) * math.sin(2 * math.pi * k / 16 + math.pi / 8)) for k in range(16)]
    prism(pts2, 0.106, 0.126, 'Grape')
    smooth_parts(lathe([(0, 0.13), (0.075, 0.128), (0.08, 0.115), (0.07, 0.105), (0, 0.105)], 'Frosting', seg=24))
