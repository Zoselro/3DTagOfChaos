from lib import *
from lib import _new, PAL, _parts
import random, bmesh
from mathutils import noise
V = Vector
# ===== HOB quality v2: smooth organic shapes, piped/scalloped details, polish pass (smooth-by-angle + bevel + baked AO vertex colour) =====

for k, c in {'LeafLight': (0.42, 0.66, 0.24), 'BerryDeep': (0.62, 0.03, 0.08), 'Sprinkle1': (0.98, 0.45, 0.62), 'Sprinkle2': (0.55, 0.80, 0.98),
             'Sprinkle3': (0.99, 0.88, 0.40), 'Sprinkle4': (0.62, 0.90, 0.55), 'CakeSponge': (0.93, 0.74, 0.46), 'WrapperPink': (0.95, 0.55, 0.68),
             'Bark': (0.47, 0.30, 0.17), 'BarkDark': (0.33, 0.20, 0.11), 'Plank': (0.72, 0.50, 0.30), 'PlankDark': (0.52, 0.33, 0.18),
             'Glaze': (0.99, 0.97, 0.94), 'Pane': (0.62, 0.84, 0.96)}.items():
    PAL.setdefault(k, c)

def smooth_parts(o):
    for p in o.data.polygons: p.use_smooth = True
    return o

def blob(r, m, loc, sub=4, lump=0.18, freq=1.6, seed=0, scl=(1, 1, 1)):
    """smooth leafy lump: icosphere displaced by noise (no facets once smooth-shaded)"""
    o = sph(r, m, loc=loc, sub=sub, scl=scl)
    off = V((seed * 7.1, seed * 3.3, seed * 5.7))
    for v in o.data.vertices:
        n = v.co.normalized()
        d = noise.noise(n * freq + off) * lump + noise.noise(n * freq * 3.1 + off) * lump * 0.35
        v.co = v.co * (1.0 + d)
    return smooth_parts(o)

def tube(pts, radii, m, seg=12):
    bm = bmesh.new(); rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = V((0, 0, 1)) if abs(t.z) < 0.9 else V((1, 0, 0)); s = t.cross(a).normalized(); u = s.cross(t).normalized()
        rings.append([bm.verts.new(p + (s * math.cos(2 * math.pi * k / seg) + u * math.sin(2 * math.pi * k / seg)) * radii[i]) for k in range(seg)])
    for a in range(len(rings) - 1):
        for k in range(seg): bm.faces.new((rings[a][k], rings[a][(k + 1) % seg], rings[a + 1][(k + 1) % seg], rings[a + 1][k]))
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    return smooth_parts(_new(bm, "tube", m))

def leaf_blade(base, direction, length, width, m, curl=0.35, thick=0.012, seg=8):
    """pointed leaf with a centre fold and thickness, bends down toward its tip"""
    d = V(direction).normalized(); side = d.cross(V((0, 0, 1)))
    if side.length < 1e-3: side = V((1, 0, 0))
    side.normalize(); up = side.cross(d).normalized()
    bm = bmesh.new(); top = []; bot = []
    for i in range(seg + 1):
        t = i / seg; w = width * math.sin(math.pi * min(1.0, t * 1.08)) ** 0.8
        c = V(base) + d * (length * t) - up * (length * curl * t * t)
        fold = up * (w * 0.25)
        row_t = [bm.verts.new(c - side * w + up * thick), bm.verts.new(c + fold + up * thick), bm.verts.new(c + side * w + up * thick)]
        row_b = [bm.verts.new(c - side * w), bm.verts.new(c + fold), bm.verts.new(c + side * w)]
        top.append(row_t); bot.append(row_b)
    for i in range(seg):
        for j in range(2):
            bm.faces.new((top[i][j], top[i][j + 1], top[i + 1][j + 1], top[i + 1][j]))
            bm.faces.new((bot[i][j + 1], bot[i][j], bot[i + 1][j], bot[i + 1][j + 1]))
        bm.faces.new((top[i][0], top[i + 1][0], bot[i + 1][0], bot[i][0])); bm.faces.new((top[i + 1][2], top[i][2], bot[i][2], bot[i + 1][2]))
    bm.faces.new((top[0][0], bot[0][0], bot[0][1], top[0][1])); bm.faces.new((top[0][1], bot[0][1], bot[0][2], top[0][2]))
    return smooth_parts(_new(bm, "leaf", m))

def apple_fruit(r, loc, m='Apple'):
    o = lathe([(r * f, r * z) for f, z in ((0.0, -0.92), (0.45, -0.88), (0.85, -0.55), (1.0, -0.05), (0.92, 0.5), (0.6, 0.85), (0.22, 0.78), (0.0, 0.62))], m, loc=loc, seg=20)
    smooth_parts(o)
    cyl(r * 0.07, r * 0.5, 'Stem', loc=(loc[0], loc[1], loc[2] + r * 0.85), seg=6)
    return o

# ---------------------------------------------------------------- trees

def tree_fruit_v2(h=3.4):
    random.seed(5)
    # trunk: curved taper with root flare + 2 branches
    pts = [V((0.06 * math.sin(t * 2.2), 0.04 * math.cos(t * 1.7), h * 0.62 * t)) for t in [i / 10 for i in range(11)]]
    rad = [0.30 * (1 - t) ** 3 + 0.13 * (1 - 0.45 * t) for t in [i / 10 for i in range(11)]]
    tube(pts, rad, 'Bark', seg=14)
    for a, l in ((0.6, 0.75), (2.7, 0.65), (4.4, 0.7)):
        b0 = V((0, 0, h * 0.46)); d = V((math.cos(a), math.sin(a), 1.1)).normalized()
        tube([b0, b0 + d * l * 0.5, b0 + d * l], [0.08, 0.06, 0.04], 'Bark', seg=8)
    for k in range(5):                                                            # roots
        a = k * 1.256 + 0.3; d = V((math.cos(a), math.sin(a), 0))
        tube([V((0, 0, 0.25)), d * 0.28 + V((0, 0, 0.08)), d * 0.48 + V((0, 0, -0.03))], [0.09, 0.06, 0.03], 'BarkDark', seg=8)
    # canopy: 7 lumpy blobs, two leaf shades
    cz = h * 0.78
    for k, (x, y, z, r) in enumerate(((0, 0, 0.18, 1.05), (0.62, 0.25, -0.12, 0.72), (-0.6, -0.3, -0.1, 0.74), (0.15, -0.68, 0.0, 0.66),
                                      (-0.2, 0.66, 0.02, 0.68), (0.35, 0.1, 0.62, 0.62), (-0.35, -0.05, 0.55, 0.6))):
        blob(r, 'Leaf' if k % 3 else 'LeafLight', (x, y, cz + z), sub=4, lump=0.16, seed=k + 3)
    # apples on the canopy surface (pushed slightly out)
    for k in range(18):
        a = random.uniform(0, 2 * math.pi); e = random.uniform(-0.35, 0.55)
        p = V((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))
        apple_fruit(0.11, (p.x * 1.18, p.y * 1.18, cz + 0.15 + p.z * 1.05))

def tree_round_v2(h=3.2):
    random.seed(8)
    pts = [V((0, 0, h * 0.6 * t)) for t in [i / 8 for i in range(9)]]
    tube(pts, [0.26 * (1 - t) ** 3 + 0.11 * (1 - 0.4 * t) for t in [i / 8 for i in range(9)]], 'Bark', seg=12)
    cz = h * 0.78
    for k, (x, y, z, r) in enumerate(((0, 0, 0.1, 1.0), (0.5, 0.2, -0.15, 0.66), (-0.5, -0.25, -0.12, 0.68), (0.05, -0.55, 0.05, 0.6), (0.1, 0.4, 0.5, 0.58))):
        blob(r, 'Leaf' if k % 2 else 'LeafLight', (x, y, cz + z), sub=4, lump=0.15, seed=k + 11)

# ---------------------------------------------------------------- fruit sculptures

def strawberry_v2(s=2.0):
    R = s * 0.48
    prof = [(0.0, 0.02), (0.10, 0.05), (0.28, 0.18), (0.5, 0.38), (0.72, 0.62), (0.9, 0.82), (1.0, 0.95), (0.97, 1.03), (0.78, 1.1), (0.45, 1.13), (0.0, 1.12)]
    body = lathe([(R * r, s * 0.82 * z) for r, z in prof], 'Berry', seg=48)
    smooth_parts(body)
    # seeds: golden-angle spiral, each a little almond sitting in a dimple (pushed into the surface)
    gold = math.pi * (3 - math.sqrt(5)); n = 150
    def radius_at(z):
        zz = z / (s * 0.82)
        for (r0, z0), (r1, z1) in zip(prof, prof[1:]):
            if z0 <= zz <= z1 or (z1 < z0 and z1 <= zz <= z0): t = (zz - z0) / (z1 - z0 + 1e-9); return R * (r0 + (r1 - r0) * t)
        return 0.0
    for i in range(n):
        z = s * 0.82 * (0.1 + 0.9 * (i + 0.5) / n * 0.98)
        rr = radius_at(z)
        if rr < R * 0.12: continue
        a = i * gold
        p = V((rr * math.cos(a), rr * math.sin(a), z)); nrm = V((math.cos(a), math.sin(a), 0.35)).normalized()
        sd = uvs(s * 0.016, 'Seed', loc=p + nrm * s * 0.004, scl=(1, 0.55, 1.6), seg=8, rings=6)
        sd.rotation_mode = 'QUATERNION'; sd.rotation_quaternion = nrm.to_track_quat('Y', 'Z'); smooth_parts(sd)
    top = s * 0.82 * 1.1
    for k in range(8):                                                              # sepals
        a = k * math.pi / 4 + 0.2
        leaf_blade((0, 0, top), (math.cos(a), math.sin(a), 0.15), s * 0.3, s * 0.07, 'Leaf' if k % 2 else 'LeafLight', curl=0.45, thick=s * 0.008)
    tube([V((0, 0, top - 0.02)), V((0.02 * s, 0, top + s * 0.08)), V((0.05 * s, 0.01 * s, top + s * 0.15))], [s * 0.028, s * 0.024, s * 0.018], 'Stem', seg=8)

# ---------------------------------------------------------------- cupcake

def cupcake_v2(s=1.8, top='Frosting'):
    random.seed(4)
    # pleated wrapper (sinusoidal ripple around), cake dome peeking above it
    bm = bmesh.new(); seg = 96; rows = 8; rings = []
    for j in range(rows + 1):
        t = j / rows; z = s * 0.42 * t; r = s * (0.31 + 0.1 * t)
        rings.append([bm.verts.new(((r + s * 0.012 * math.cos(24 * 2 * math.pi * i / seg)) * math.cos(2 * math.pi * i / seg),
                                    (r + s * 0.012 * math.cos(24 * 2 * math.pi * i / seg)) * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    for j in range(rows):
        for i in range(seg): bm.faces.new((rings[j][i], rings[j][(i + 1) % seg], rings[j + 1][(i + 1) % seg], rings[j + 1][i]))
    bm.faces.new(rings[0][::-1])
    w = _new(bm, "wrapper", 'WrapperPink'); smooth_parts(w)
    lathe([(s * 0.405, s * 0.405), (s * 0.43, s * 0.44), (s * 0.36, s * 0.5), (0, s * 0.52)], 'CakeSponge', seg=64); smooth_parts(_parts[-1])
    # piped swirl: star cross-section swept along a cone helix
    turns = 3.2; steps = 220; star = []
    for k in range(16):
        a = 2 * math.pi * k / 16; rr = (1.0 if k % 2 == 0 else 0.78)
        star.append((math.cos(a) * rr, math.sin(a) * rr))
    path = []
    for i in range(steps + 1):
        t = i / steps; a = 2 * math.pi * turns * t
        R = s * (0.36 * (1 - t) ** 0.9 + 0.02); z = s * (0.5 + 0.44 * t)
        c = V((R * math.cos(a), R * math.sin(a), z))
        tang = V((-math.sin(a), math.cos(a), 0)); up = V((math.cos(a), math.sin(a), 0)) * 0.2 + V((0, 0, 1))
        side = tang.cross(up).normalized(); up2 = side.cross(tang).normalized()
        th = s * 0.085 * (1 - 0.55 * t) * (min(1.0, i / 8) ** 0.5)
        path.append((c, side * th, up2 * th))
    bm = bmesh.new(); rings = []
    for c, sd, up in path: rings.append([bm.verts.new(c + sd * x + up * y) for x, y in star])
    for k in range(len(rings) - 1):
        for i in range(16): bm.faces.new((rings[k][i], rings[k][(i + 1) % 16], rings[k + 1][(i + 1) % 16], rings[k + 1][i]))
    tipc = path[-1][0] + V((0, 0, s * 0.05)); tip = bm.verts.new(tipc)
    for i in range(16): bm.faces.new((rings[-1][i], rings[-1][(i + 1) % 16], tip))
    bm.faces.new(rings[0][::-1])
    smooth_parts(_new(bm, "swirl", top))
    # sprinkles
    for k in range(60):
        t = random.uniform(0.02, 0.9); a = random.uniform(0, 2 * math.pi)
        R = s * (0.36 * (1 - t) ** 0.9 + 0.02) + s * 0.07 * (1 - 0.55 * t); z = s * (0.5 + 0.44 * t) + s * 0.04
        sp = cyl(s * 0.008, s * 0.035, 'Sprinkle%d' % (k % 4 + 1), loc=(R * math.cos(a), R * math.sin(a), z), rot=(random.uniform(0, 3), random.uniform(0, 3), 0), seg=6)
        smooth_parts(sp)
    apple_fruit(s * 0.08, (0, 0, s * 1.02), 'Cherry')

# ---------------------------------------------------------------- market stall

def scallop_awning(w, d, z, slope=0.4, nstripe=10, drop=0.22):
    """curved striped canopy (sags between front and back) with a scalloped valance — stripes by face material"""
    bm = bmesh.new(); nx = nstripe * 4; ny = 8; grid = []
    for i in range(nx + 1):
        x = -w / 2 + w * i / nx; row = []
        for j in range(ny + 1):
            t = j / ny; y = -d / 2 + d * t
            row.append(bm.verts.new((x, y, z + slope * t + 0.10 * math.sin(math.pi * t))))
        grid.append(row)
    for i in range(nx):
        for j in range(ny): bm.faces.new((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.035)
    o = _new(bm, "awning", 'Canvas'); o.data.materials.append(mat('Stripe'))
    for p in o.data.polygons:
        k = int((p.center.x + w / 2) / (w / nstripe)); p.material_index = 1 if k % 2 else 0
    smooth_parts(o)
    # scalloped valance: one half-disc flap per stripe, slightly curled outward
    for i in range(nstripe):
        x0 = -w / 2 + w * i / nstripe; x1 = x0 + w / nstripe; bm = bmesh.new(); pts = []
        for k in range(13):
            t = k / 12; x = x0 + (x1 - x0) * t
            pts.append((x, -d / 2 - 0.01 - 0.04 * math.sin(math.pi * t), z - 0.02 - drop * (0.55 + 0.45 * math.sin(math.pi * t))))
        top_l = bm.verts.new((x0, -d / 2 - 0.01, z + 0.01)); top_r = bm.verts.new((x1, -d / 2 - 0.01, z + 0.01))
        vs = [bm.verts.new(p) for p in pts]
        bm.faces.new([top_l] + vs + [top_r])
        bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.025)
        smooth_parts(_new(bm, "flap", 'Stripe' if i % 2 else 'Canvas'))

def crate(w, d, h, loc, fill=None):
    x, y, z = loc
    for sgn in (-1, 1):
        box(w, 0.03, h, 'Plank', loc=(x, y + sgn * (d / 2 - 0.015), z + h / 2), bevel=0.006)
        box(0.03, d - 0.06, h, 'Plank', loc=(x + sgn * (w / 2 - 0.015), y, z + h / 2), bevel=0.006)
    box(w - 0.06, d - 0.06, 0.02, 'PlankDark', loc=(x, y, z + 0.03))
    if fill:
        n = int(w / 0.13); m = int(d / 0.13)
        for i in range(n):
            for j in range(m):
                fx = x - w / 2 + 0.065 + (w - 0.06) * i / max(1, n - 1) * 0.9; fy = y - d / 2 + 0.065 + (d - 0.06) * j / max(1, m - 1) * 0.9
                apple_fruit(0.06, (fx, fy, z + h - 0.02 + 0.02 * ((i + j) % 2)), fill[(i * 3 + j) % len(fill)])

def market_stall_v2():
    random.seed(2)
    # counter: plank front + top board with rounded edge
    for k in range(6):
        box(2.0, 0.05, 0.14, 'Plank' if k % 2 else 'PlankDark', loc=(0, -0.48, 0.1 + 0.15 * k), bevel=0.012)
    box(2.0, 0.9, 0.85, 'PlankDark', loc=(0, 0.02, 0.45), bevel=0.02)
    box(2.16, 1.08, 0.07, 'Plank', loc=(0, 0, 0.92), bevel=0.025)
    # turned posts (lathe) + base feet
    for x in (-1.0, 1.0):
        for y in (-0.5, 0.5):
            lathe([(0, 0), (0.07, 0), (0.07, 0.05), (0.05, 0.1), (0.045, 2.1), (0.06, 2.18), (0.06, 2.25), (0, 2.27)], 'Glaze' if y < 0 else 'Plank', loc=(x, y, 0.0), seg=14)
            smooth_parts(_parts[-1])
    scallop_awning(2.5, 1.45, 2.25)
    # fruit crates on the counter + sign board
    crate(0.55, 0.4, 0.16, (-0.62, -0.05, 0.955), ['Apple', 'Apple', 'Cherry'])
    crate(0.55, 0.4, 0.16, (0.0, -0.05, 0.955), ['Orange'])
    crate(0.55, 0.4, 0.16, (0.62, -0.05, 0.955), ['Berry', 'BerryDeep'])
    box(1.2, 0.05, 0.32, 'Glaze', loc=(0, -0.53, 2.05), bevel=0.02); box(1.26, 0.04, 0.38, 'Plank', loc=(0, -0.51, 2.05), bevel=0.015)
    for k, mm in enumerate(('Apple', 'Orange', 'Berry')):
        apple_fruit(0.07, (-0.3 + 0.3 * k, -0.57, 2.05), mm)
    crate(0.6, 0.45, 0.3, (0.75, -0.95, 0.0), ['Apple'])

# ---------------------------------------------------------------- cake house

def drip_ring(r, z, m, n=28, seed=3, maxlen=0.6, thick=0.09):
    """frosting edge: rounded rim + drips of varied length hanging down the wall"""
    random.seed(seed)
    torus(r + 0.02, thick * 1.2, m, loc=(0, 0, z), seg=72, rseg=12); smooth_parts(_parts[-1])
    for k in range(n):
        a = 2 * math.pi * (k + random.uniform(-0.2, 0.2)) / n; L = random.uniform(0.15, maxlen)
        c = V(((r + 0.02) * math.cos(a), (r + 0.02) * math.sin(a), 0))
        pts = [c + V((0, 0, z - L * t)) for t in (0, 0.35, 0.7, 1.0)]
        tube(pts, [thick * 0.9, thick * 0.8, thick * 0.75, thick * 0.01], m, seg=10)
        sph(thick * 0.95, m, loc=(c.x, c.y, z - L + thick * 0.6), sub=2); smooth_parts(_parts[-1])

def bead_ring(r, z, m, n=40, br=0.11):
    for k in range(n):
        a = 2 * math.pi * k / n
        sph(br, m, loc=(r * math.cos(a), r * math.sin(a), z), scl=(1, 1, 0.85), sub=2); smooth_parts(_parts[-1])

def arched_window(a, r, zc, w=0.9, h=1.3):
    """recessed pane + gold arched frame + sill, facing out along angle a (wall radius r)"""
    d = V((math.cos(a), math.sin(a), 0)); rot = (0, 0, a + math.pi / 2)
    c = d * (r + 0.01)
    box(w, 0.06, h - w / 2, 'Pane', loc=(c.x, c.y, zc - w / 4), rot=rot)
    cyl(w / 2, 0.06, 'Pane', loc=(c.x, c.y, zc + h / 2 - w / 2), rot=(math.pi / 2, 0, a + math.pi / 2), seg=24)
    # frame: sweep around the arch + sides
    side = V((-d.y, d.x, 0)); pts = []
    top = zc + h / 2 - w / 2
    for i in range(17):
        t = math.pi * i / 16; pts.append(c + d * 0.04 + side * (math.cos(t) * (w / 2 + 0.05)) + V((0, 0, top + math.sin(t) * (w / 2 + 0.05))))
    tube(pts, [0.05] * len(pts), 'Gold', seg=8)
    for sgn in (-1, 1):
        p0 = c + d * 0.04 + side * (sgn * (w / 2 + 0.05))
        tube([p0 + V((0, 0, zc - h / 2)), p0 + V((0, 0, top))], [0.05, 0.05], 'Gold', seg=8)
    box(w + 0.3, 0.2, 0.08, 'Glaze', loc=(c.x + d.x * 0.08, c.y + d.y * 0.08, zc - h / 2 - 0.04), rot=rot, bevel=0.02)
    # mullion cross
    box(0.04, 0.04, h - 0.1, 'Gold', loc=(c.x + d.x * 0.04, c.y + d.y * 0.04, zc), rot=rot)
    box(w - 0.04, 0.04, 0.04, 'Gold', loc=(c.x + d.x * 0.04, c.y + d.y * 0.04, zc - 0.05), rot=rot)

def house_cake_v2():
    from objs_house import door_cut, door_frames, DOOR_W, DOOR_H
    from objs_struct import pillar
    random.seed(9)
    r = 3.6; h = 4.6
    # cake-plate base: scalloped rim plate
    lathe([(0, 0), (4.4, 0), (4.6, 0.12), (4.5, 0.26), (4.25, 0.3), (0, 0.3)], 'PinkStone', seg=72); smooth_parts(_parts[-1])
    bead_ring(4.38, 0.3, 'Glaze', n=56, br=0.12)
    w = lathe([(r - 0.35, 0.3), (r, 0.3), (r, h), (r - 0.35, h)], 'CakeSponge', seg=72, closed=True); door_cut(w, r, 0.3)
    # cream filling band (layer cake) + bottom bead border
    torus(r + 0.02, 0.11, 'Glaze', loc=(0, 0, 2.2), seg=72, rseg=10); smooth_parts(_parts[-1])
    bead_ring(r + 0.06, 0.42, 'Frosting', n=48, br=0.13)
    drip_ring(r, h, 'Frosting', n=30, maxlen=0.85, thick=0.12)
    # whipped roof: smooth dome + piped rosettes ring
    lathe([(0, h - 0.02), (r + 0.25, h - 0.02), (r + 0.18, h + 0.35), (r - 0.4, h + 1.15), (r - 1.4, h + 1.9), (r - 2.6, h + 2.3), (0, h + 2.42)], 'Frosting', seg=72)
    smooth_parts(_parts[-1])
    for k in range(14):
        a = 2 * math.pi * k / 14 + 0.2; rr = r - 0.15
        lathe([(0, 0), (0.32, 0.0), (0.3, 0.16), (0.18, 0.3), (0.06, 0.42), (0, 0.46)], 'Glaze', loc=(rr * math.cos(a), rr * math.sin(a), h + 0.32), seg=16)
        smooth_parts(_parts[-1])
    for k in range(90):                                                            # sprinkles on the roof
        t = random.uniform(0.1, 0.92); a = random.uniform(0, 2 * math.pi); rr = (r - 0.4) * (1 - t) + 0.2; z = h + 1.15 + 1.2 * t - 0.25 * (1 - t)
        cyl(0.025, 0.12, 'Sprinkle%d' % (k % 4 + 1), loc=(rr * math.cos(a), rr * math.sin(a), z + 0.05), rot=(random.uniform(0, 3), random.uniform(0, 3), 0), seg=6)
        smooth_parts(_parts[-1])
    # giant strawberry on top
    z0 = h + 2.3
    sb = lathe([(0, 0), (0.28, 0.08), (0.62, 0.42), (0.75, 0.78), (0.7, 1.05), (0.4, 1.22), (0, 1.25)], 'Berry', loc=(0, 0, z0), seg=40); smooth_parts(sb)
    for k in range(7):
        a = k * 2 * math.pi / 7
        leaf_blade((0, 0, z0 + 1.22), (math.cos(a), math.sin(a), 0.2), 0.55, 0.13, 'Leaf' if k % 2 else 'LeafLight', curl=0.4, thick=0.02)
    for a in (math.pi / 2 + 0.9, math.pi / 2 - 0.9, -math.pi / 2 + 0.9, -math.pi / 2 - 0.9):
        arched_window(a, r, 2.9)
    door_frames(r, 0.3)
    for x in (-3.9, 3.9): pillar(x, -3.9, 0.3, h=1.1)

# ---------------------------------------------------------------- polish pass

def polish(o, angle=40.0, bevel=0.0, ao=True, ao_dist=1.0, ao_samples=64):
    """smooth-by-angle (curves smooth, hard edges stay sharp) + optional bevel + AO baked to the vertex colour 'Col' (RGB = occlusion)"""
    me = o.data
    for p in me.polygons: p.use_smooth = True
    try:
        me.set_sharp_from_angle(angle=math.radians(angle))
    except Exception:
        pass
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    if bevel > 0:
        md = o.modifiers.new("bv", 'BEVEL'); md.width = bevel; md.segments = 2; md.limit_method = 'ANGLE'; md.angle_limit = math.radians(angle)
        md.clamp_overlap = True; md.harden_normals = False
        bpy.ops.object.modifier_apply(modifier="bv")
    if ao:
        sc = bpy.context.scene
        try: sc.render.engine = 'CYCLES'
        except TypeError: pass
        sc.cycles.samples = ao_samples; sc.cycles.device = 'CPU'
        if sc.world is None: sc.world = bpy.data.worlds.new("W")
        sc.world.light_settings.distance = ao_dist
        for c in list(me.color_attributes): me.color_attributes.remove(c)
        ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
        me.color_attributes.active_color = ca
        for m in me.materials:                      # cycles bake needs node materials (already) — nothing else to set for AO
            pass
        bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')
    return o
