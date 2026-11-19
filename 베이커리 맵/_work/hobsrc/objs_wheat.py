from lib import *
from lib import _new
import random
V = Vector
# ===== wheat v2 (§89 HB1): plump kernels in two alternating rows (4 visible columns), long awns, drooping ear, jointed stem + leaves.
# Each part gets its sway colour as it is made: R = weight (0 root .. 1 top, squared), G = stalk phase. Join keeps the attribute.

def _frame(d):
    d = d.normalized(); a = V((0, 0, 1)) if abs(d.z) < 0.9 else V((1, 0, 0))
    s = d.cross(a).normalized(); u = s.cross(d).normalized(); return s, u

def _paint(o, phase, height):
    me = o.data
    ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    for poly in me.polygons:
        for li in poly.loop_indices:
            z = (o.matrix_world @ me.vertices[me.loops[li].vertex_index].co).z
            w = max(0.0, min(1.0, z / height)); w = w * w
            ca.data[li].color = (w, phase, 0.0, 1.0)

def kernel(c, d, side, r, m, seg=6, rings=5, phase=0.0, height=1.0):
    """teardrop grain centred at c, long axis d, flattened across `side`, groove on the inner face"""
    d = d.normalized(); s = side.normalized(); u = d.cross(s).normalized()
    L = r * 2.6; bm = bmesh.new(); vs = []
    tip0 = bm.verts.new(c - d * (L * 0.45)); tip1 = bm.verts.new(c + d * (L * 0.55))
    for k in range(1, rings + 1):
        t = k / (rings + 1); rr = r * (math.sin(math.pi * t) ** 0.8) * (1.0 + 0.25 * (t - 0.5))
        ring = []
        for q in range(seg):
            a = 2 * math.pi * q / seg
            ca_, sa = math.cos(a), math.sin(a)
            g = 0.55 if abs(a - math.pi) < 0.6 else 1.0                       # groove (inner face)
            ring.append(bm.verts.new(c + d * (L * (t - 0.45)) + s * (rr * ca_ * g) + u * (rr * 0.8 * sa)))
        vs.append(ring)
    for k in range(rings - 1):
        for q in range(seg):
            bm.faces.new((vs[k][q], vs[k][(q + 1) % seg], vs[k + 1][(q + 1) % seg], vs[k + 1][q]))
    for q in range(seg):
        bm.faces.new((tip0, vs[0][(q + 1) % seg], vs[0][q])); bm.faces.new((tip1, vs[-1][q], vs[-1][(q + 1) % seg]))
    o = _new(bm, "kernel", m); _paint(o, phase, height); return o

def awn(p, d, length, r=0.0016, phase=0.0, height=1.0, m='WheatAwn'):
    d = d.normalized(); s, u = _frame(d); bm = bmesh.new(); tw = random.uniform(0, 2 * math.pi)   # random twist: parallel awns never share a plane
    base = [bm.verts.new(p + (s * math.cos(tw + 2 * math.pi * k / 3) + u * math.sin(tw + 2 * math.pi * k / 3)) * r) for k in range(3)]
    tip = bm.verts.new(p + d * length)
    bm.faces.new(base[::-1])
    for k in range(3): bm.faces.new((base[k], base[(k + 1) % 3], tip))
    o = _new(bm, "awn", m); _paint(o, phase, height); return o

def tube_pts(pts, radii, m, seg=5, phase=0.0, height=1.0):
    bm = bmesh.new(); rings = []
    for i, p in enumerate(pts):
        t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]; s, u = _frame(t)
        rings.append([bm.verts.new(p + (s * math.cos(2 * math.pi * k / seg) + u * math.sin(2 * math.pi * k / seg)) * radii[i]) for k in range(seg)])
    for a in range(len(rings) - 1):
        for k in range(seg): bm.faces.new((rings[a][k], rings[a][(k + 1) % seg], rings[a + 1][(k + 1) % seg], rings[a + 1][k]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    o = _new(bm, "stem", m); _paint(o, phase, height); return o

def leaf(p, out, length, width, phase, height, rng):
    """curved ribbon leaf from the stem node, droops toward its tip (two-sided in Unity — Cull Off)"""
    out = V((out.x, out.y, 0)).normalized(); side = V((-out.y, out.x, 0)); bm = bmesh.new(); rows = []
    for i in range(7):
        t = i / 6; w = width * math.sin(math.pi * min(1.0, t * 1.15 + 0.05)) * (1 - 0.6 * t)
        c = p + out * (length * t) + V((0, 0, length * (0.55 * t - 0.75 * t * t)))
        tw = side * math.cos(t * 0.8) + V((0, 0, 1)) * math.sin(t * 0.8) * 0.3
        rows.append((bm.verts.new(c - tw * w), bm.verts.new(c + tw * w)))
    for i in range(6): bm.faces.new((rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0]))
    o = _new(bm, "leaf", 'WheatLeaf'); _paint(o, phase, height); return o

def stalk(root, h, lean, rng, phase, height, lod=0):
    """one wheat stalk: 3-segment stem with 2 nodes, 1-2 leaves, drooping ear (lod 0 = every grain, 1 = fewer coarse grains)"""
    n = 10; pts = []; radii = []
    for i in range(n + 1):
        t = i / n
        pts.append(root + V((lean.x * t * t, lean.y * t * t, h * t)))
        node = any(abs(t - nt) < 0.051 for nt in (0.32, 0.62))
        radii.append(0.010 * (1 - 0.35 * t) * (1.35 if node else 1.0))
    tube_pts(pts, radii, 'WheatStem', seg=5 if lod == 0 else 4, phase=phase, height=height)
    if lod == 0:
        for k, nt in enumerate((0.32, 0.62)[: rng.randint(1, 2)]):
            a = rng.uniform(0, 2 * math.pi)
            leaf(root + V((lean.x * nt * nt, lean.y * nt * nt, h * nt)), V((math.cos(a), math.sin(a), 0)), rng.uniform(0.24, 0.34), 0.017, phase, height, rng)
    top = pts[-1]; axis = (pts[-1] - pts[-2]).normalized()
    hang = V((lean.x, lean.y, 0)); hang = hang.normalized() if hang.length > 1e-4 else V((1, 0, 0))
    ear(top, axis, hang, rng, phase, height, lod)

def ear(base, axis, hang, rng, phase, height, lod=0):
    pairs = 11 if lod == 0 else 6; length = rng.uniform(0.20, 0.24); droop = rng.uniform(0.2, 0.45)
    r = 0.0165 if lod == 0 else 0.021
    pts = []
    for i in range(pairs + 1):
        t = i / pairs
        d = (axis * (1 - droop * t) + hang * (droop * t)).normalized()
        pts.append(base + (axis * (length * t)) + hang * (droop * length * t * t * 0.6))
    tube_pts(pts, [0.006] * len(pts), 'WheatStem', seg=4, phase=phase, height=height)               # rachis
    for i in range(pairs):
        t = (i + 0.5) / pairs; p = pts[i].lerp(pts[i + 1], 0.5); d = (pts[i + 1] - pts[i]).normalized()
        s, u = _frame(d); rot = (i % 2) * (math.pi / 2)
        for k in (0, 1):
            a = rot + k * math.pi; out = s * math.cos(a) + u * math.sin(a)
            rr = r * (1.0 - 0.3 * t) * rng.uniform(0.92, 1.08)
            kd = (d * 0.85 + out * 0.45).normalized()
            m = 'WheatGoldDeep' if t < 0.15 else ('WheatGoldLight' if rng.random() < 0.35 else 'WheatGold')
            kernel(p + out * rr * 0.62, kd, out.cross(kd), rr, m, seg=6 if lod == 0 else 4, rings=5 if lod == 0 else 3, phase=phase, height=height)
            if lod == 0 or k == 0:
                awn(p + out * rr * 0.62 + kd * rr * 1.3, (kd * 0.6 + d * 0.4).normalized(), length * rng.uniform(0.45, 0.75), r=0.0026, phase=phase, height=height)
    awn(pts[-1], (pts[-1] - pts[-2]).normalized(), length * 0.6, r=0.003, phase=phase, height=height)

def wheat_clump(n=9, seed=3, lod=0, spread=0.15):
    """one clump (prefab unit for fields): n stalks 0.85-1.2 m, own phase each"""
    rng = random.Random(seed); random.seed(seed + 100); height = 1.45
    used = []
    for k in range(n):
        for _t in range(40):                                                   # stems never closer than 4 cm (no shared surfaces)
            a = rng.uniform(0, 2 * math.pi); rr = spread * math.sqrt(rng.random())
            if all((rr * math.cos(a) - x) ** 2 + (rr * math.sin(a) - y) ** 2 > 0.04 ** 2 for x, y in used): break
        used.append((rr * math.cos(a), rr * math.sin(a)))
        root = V((rr * math.cos(a), rr * math.sin(a), -0.006 - 0.0013 * k))   # inside the mound, every stem end at its own depth
        lean = V((math.cos(a), math.sin(a), 0)) * rng.uniform(0.02, 0.12)
        stalk(root, rng.uniform(0.8, 1.05), lean, rng, rng.random(), height, lod)
    _paint(cyl(spread + 0.04, 0.03, 'Soil', loc=(0, 0, -0.012), seg=10), 0.0, height)                    # tiny dirt mound (root cover)

def wheat_ear_hero():
    """single ear close-up for the comparison render"""
    rng = random.Random(9); random.seed(109)
    ear(V((0, 0, 0)), V((0, 0, 1)), V((1, 0, 0)), rng, 0.5, 0.3, 0)
    tube_pts([V((0, 0, -0.25 + 0.025 * i)) for i in range(11)], [0.0045] * 11, 'WheatStem', phase=0.5, height=0.3)

def smooth_grains(o):
    """grains/stems smooth-shaded (exported with FACE smoothing flags) — the rest stays flat like the other HOB props"""
    names = {i for i, m in enumerate(o.data.materials) if m and m.name in ('HOB_WheatGold', 'HOB_WheatGoldLight', 'HOB_WheatGoldDeep', 'HOB_WheatStem')}
    for p in o.data.polygons: p.use_smooth = p.material_index in names

# ===== §89.17 dense field patch (2 m square, tiles seamlessly on a grid): light stalks so a whole island can be wheat.
# lod 0 ≈ 70 tris/stalk (3-seg stem, spindle ear, 3 awns), lod 1 ≈ 26 tris/stalk (1-seg stem, coarse ear, 1 awn), same stalks.

def _spindle(base, axis, hang, length, r, droop, seg, rings, phase, height, rng):
    pts = []; radii = []
    for i in range(rings + 1):
        t = i / rings
        d = (axis * (1 - droop * t) + hang * (droop * t)).normalized()
        pts.append(base + axis * (length * t) + hang * (droop * length * t * t * 0.6))
        radii.append(r * (0.35 + 0.65 * math.sin(math.pi * min(1.0, 0.15 + t * 0.85)) ** 0.7))
    m = 'WheatGoldLight' if rng.random() < 0.3 else ('WheatGoldDeep' if rng.random() < 0.15 else 'WheatGold')
    tube_pts(pts, radii, m, seg=seg, phase=phase, height=height)
    return pts

def light_stalk(root, h, lean, rng, phase, height, lod):
    n = 3 if lod == 0 else 1
    pts = [root + V((lean.x * (i / n) ** 2, lean.y * (i / n) ** 2, h * i / n)) for i in range(n + 1)]
    tube_pts(pts, [0.009 * (1 - 0.3 * i / n) for i in range(n + 1)], 'WheatStem', seg=3, phase=phase, height=height)
    top = pts[-1]; axis = (pts[-1] - pts[-2]).normalized()
    hang = V((lean.x, lean.y, 0)); hang = hang.normalized() if hang.length > 1e-4 else V((1, 0, 0))
    L = rng.uniform(0.17, 0.22)
    ep = _spindle(top, axis, hang, L, 0.024 if lod == 0 else 0.03, rng.uniform(0.15, 0.4), 5 if lod == 0 else 3, 4 if lod == 0 else 2, phase, height, rng)
    tip = (ep[-1] - ep[-2]).normalized()
    for k in range(3 if lod == 0 else 1):
        a = rng.uniform(0, 2 * math.pi); s, u = _frame(tip)
        d = (tip + (s * math.cos(a) + u * math.sin(a)) * 0.35).normalized()
        awn(ep[-1] - tip * (0.03 + 0.04 * k), d, L * rng.uniform(0.5, 0.8), r=0.003, phase=phase, height=height)

def wheat_patch(W=2.0, n=110, lod=0, seed=7):
    """W x W square of wheat centred on the origin, stalks scattered with a minimum gap; lod 1 = same stalks, coarse geometry (keeps the field dense from afar)"""
    rng = random.Random(seed); random.seed(seed + 100); height = 1.45
    pts = []
    tries = 0
    while len(pts) < n and tries < n * 60:
        tries += 1
        x = rng.uniform(-W / 2, W / 2); y = rng.uniform(-W / 2, W / 2)
        if all((x - a) ** 2 + (y - b) ** 2 > 0.11 ** 2 for a, b in pts): pts.append((x, y))
    for k, (x, y) in enumerate(pts):
        r2 = random.Random(seed * 1000 + k)                                  # same stalk in both LODs
        h = r2.uniform(0.85, 1.12); a = r2.uniform(0, 2 * math.pi); lean = V((math.cos(a), math.sin(a), 0)) * r2.uniform(0.02, 0.1); ph = r2.random()
        light_stalk(V((x, y, -0.02)), h, lean, r2, ph, height, lod)
