# §89.16 Heaven Of Bakery 테마 소품(두 번째 구상 시트, 건물 제외): 서브 맵 바닥 타일 3종(+무늬 없는 판) · 금 울타리 · 기둥 · 밀 깃발 · 금 가로등
# · 아몬드(통·반쪽·상자·자루) · 초콜릿(판·트러플·하트 봉봉·조각) · 밀(다발·밀가루 자루·국자) · 분홍 꽃덤불 · 작은 벚꽃나무
from lib import *
from lib import _parts
import random
from mathutils import noise
from objs_v2 import tube, leaf_blade, blob, smooth_parts
from hq_props import petal_flower, last, hq_blossom_tree
from hq_struct import lantern
from hq_misc import plank_box
from hq_nature import hq_bush

# ---------------------------------------------------------------- emblems

def almond_shape(L, loc, rot=(0, 0, 0), m='Almond', flat=1.0):
    """almond nut: pointed lathe along local X, slightly flattened, with shallow grooves"""
    prof = [(0, -L / 2)] + [(L * 0.32 * math.sin(math.pi * t) ** 0.8 * (1.0 - 0.35 * t), -L / 2 + L * t) for t in [k / 14 for k in range(1, 14)]] + [(0, L / 2)]
    o = lathe(prof, m, seg=28)
    for v in o.data.vertices:
        x, y, z = v.co
        g = 0.025 * L * noise.noise(Vector((x * 9 / L, y * 9 / L, z * 2 / L)))
        r = math.hypot(x, y)
        if r > 1e-4: v.co.x += x / r * g; v.co.y += y / r * g
        v.co.y *= 0.72 * flat
        v.co = Vector((v.co.z, v.co.x, v.co.y))          # long axis = X
    smooth_parts(o)
    o.location = loc; o.rotation_euler = rot
    return o

def wheat_ear(base, direction, L, m='Seed'):
    d = Vector(direction).normalized()
    tube([Vector(base), Vector(base) + d * L * 0.55], [0.012, 0.008], 'Stem', seg=6)
    for k in range(9):
        t = 0.45 + 0.055 * k
        p = Vector(base) + d * L * t
        side = d.cross(Vector((0, 0, 1))) if abs(d.z) < 0.95 else Vector((1, 0, 0))
        side.normalize()
        for s in (-1, 1):
            q = p + side * s * L * 0.035
            smooth_parts(uvs(L * 0.045, m, loc=q, scl=(0.7, 0.7, 1.5), seg=10, rings=6, rot=(0, 0, 0)))
    tube([Vector(base) + d * L * 0.95, Vector(base) + d * L * 1.15], [0.004, 0.001], m, seg=4)

def wheat_emblem(c, s, m='Gold', z_up=True):
    """flat gold wheat sprig on a surface (c = centre, s = height); lies on the XY plane when z_up, else on XZ (facing -Y)"""
    def P(x, y): return Vector((c[0] + x, c[1], c[2] + y)) if not z_up else Vector((c[0] + x, c[1] + y, c[2]))
    tube([P(0, -s * 0.5), P(0, s * 0.45)], [s * 0.035, s * 0.025], m, seg=8)
    for k in range(5):
        t = -0.15 + k * 0.14
        for sgn in (-1, 1):
            q = P(sgn * s * 0.09, s * t)
            o = uvs(s * 0.075, m, loc=q, scl=(0.6, 1.4, 0.35) if z_up else (0.6, 0.35, 1.4), seg=12, rings=6,
                    rot=(0, 0, -sgn * 0.5) if z_up else (0, sgn * 0.5, 0))
            smooth_parts(o)
    smooth_parts(uvs(s * 0.07, m, loc=P(0, s * 0.53), scl=(0.6, 1.5, 0.35) if z_up else (0.6, 0.35, 1.5), seg=12, rings=6))

def choco_emblem(c, s):
    box(s, s, s * 0.1, 'Choco', loc=(c[0], c[1], c[2] + s * 0.05), bevel=s * 0.03)
    for i in range(2):
        for j in range(2):
            box(s * 0.42, s * 0.42, s * 0.08, 'ChocoDark', loc=(c[0] + (i - 0.5) * s * 0.48, c[1] + (j - 0.5) * s * 0.48, c[2] + s * 0.13), bevel=s * 0.04)

# ---------------------------------------------------------------- 3 m floor tiles

THEME = {   # paving A, paving B, border band, ring, ring inlay
    'almond': ('Cream', 'Sand', 'Sand', 'CakeSponge', 'Cream'),
    'choco':  ('PinkWall', 'PinkStone', 'Banner', 'Banner', 'PinkWall'),
    'flour':  ('Cream', 'Glaze', 'Sand', 'Cream', 'Glaze'),
}

def theme_tile(theme, emblem=True, S=3.0, seed=1):
    rng = random.Random(seed)
    A, B, band, ring, inlay = THEME[theme]
    box(S, S, 0.1, 'StoneTile', loc=(0, 0, 0.05))
    n = 6; c = (S - 0.36) / n
    for i in range(n):
        for j in range(n):
            if emblem and math.hypot((i - n / 2 + 0.5) * c, (j - n / 2 + 0.5) * c) < S * 0.34: continue
            hh = 0.05 + rng.uniform(-0.006, 0.008)
            box(c - 0.035, c - 0.035, hh, A if rng.random() < 0.62 else B, loc=(-S / 2 + 0.18 + c * (i + 0.5), -S / 2 + 0.18 + c * (j + 0.5), 0.1 + hh / 2),
                rot=(rng.uniform(-0.01, 0.01), rng.uniform(-0.01, 0.01), rng.uniform(-0.02, 0.02)), bevel=0.014)
    for sgn in (-1, 1):                                                             # border band
        box(S, 0.16, 0.07, band, loc=(0, sgn * (S / 2 - 0.08), 0.135), bevel=0.012)
        box(0.16, S - 0.32, 0.07, band, loc=(sgn * (S / 2 - 0.08), 0, 0.135), bevel=0.012)
    for sx in (-1, 1):                                                              # gold corner triangles
        for sy in (-1, 1):
            x0 = sx * (S / 2 - 0.18); y0 = sy * (S / 2 - 0.18)
            prism([(x0, y0), (x0 - sx * 0.42, y0), (x0, y0 - sy * 0.42)], 0.15, 0.17, 'Gold')
    if not emblem:
        prism([(0.16, 0), (0, 0.16), (-0.16, 0), (0, -0.16)], 0.15, 0.165, 'Gold')
        return
    R0, R1 = S * 0.22, S * 0.32
    smooth_parts(lathe([(0, 0.13), (R0, 0.13), (R0, 0.15), (0, 0.15)], inlay, seg=64))
    smooth_parts(lathe([(R0, 0.1), (R1, 0.1), (R1, 0.16), (R0, 0.16)], ring, seg=64, closed=True))
    for r in (R0, R1): torus(r, 0.022, 'Gold', loc=(0, 0, 0.162), seg=72, rseg=6); smooth_parts(last())
    for k in range(8):                                                              # gold dots on the ring
        a = 2 * math.pi * k / 8 + math.pi / 8
        smooth_parts(uvs(0.035, 'Gold', loc=((R0 + R1) / 2 * math.cos(a), (R0 + R1) / 2 * math.sin(a), 0.165), scl=(1, 1, 0.5), seg=10, rings=6))
    if theme == 'almond': almond_shape(S * 0.3, (0, 0, 0.19), rot=(0, 0, 0.6), flat=0.55)
    elif theme == 'choco': choco_emblem((0, 0, 0.15), S * 0.2)
    else: wheat_emblem((0, 0, 0.165), S * 0.34)

def hq_tile_almond(): theme_tile('almond', True, seed=11)
def hq_tile_almond_plain(): theme_tile('almond', False, seed=12)
def hq_tile_choco(): theme_tile('choco', True, seed=13)
def hq_tile_choco_plain(): theme_tile('choco', False, seed=14)
def hq_tile_flour(): theme_tile('flour', True, seed=15)
def hq_tile_flour_plain(): theme_tile('flour', False, seed=16)

# ---------------------------------------------------------------- fence, post, banner, lamp

def fence_post(x, y, h=1.05, w=0.2):
    box(w + 0.06, w + 0.06, 0.12, 'Cream', loc=(x, y, 0.06), bevel=0.02)
    box(w, w, h, 'Cream', loc=(x, y, 0.12 + h / 2), bevel=0.02)
    box(w + 0.02, w + 0.02, 0.04, 'Gold', loc=(x, y, 0.12 + h * 0.18), bevel=0.006)
    box(w + 0.06, w + 0.06, 0.06, 'Cream', loc=(x, y, 0.15 + h), bevel=0.015)
    smooth_parts(lathe([(0, 0), (0.07, 0), (0.05, 0.05), (0.08, 0.12), (0.03, 0.2), (0, 0.24)], 'Gold', loc=(x, y, 0.18 + h), seg=16))

def hq_gold_fence(L=2.2, h=1.05):
    for sx in (-1, 1): fence_post(sx * L / 2, 0, h)
    for z in (0.2, h - 0.02):
        box(L - 0.2, 0.12, 0.07, 'Cream', loc=(0, 0, z), bevel=0.015)
    box(L - 0.2, 0.05, 0.03, 'Gold', loc=(0, -0.045, h - 0.02), bevel=0.005)
    n = 9; seg = (L - 0.2) / n                                                      # gold balusters, every other one with a ring + ball
    for k in range(n):
        cx = -L / 2 + 0.1 + seg * (k + 0.5)
        zc = (0.24 + h - 0.06) / 2
        tube([Vector((cx, 0, 0.24)), Vector((cx, 0, h - 0.06))], [0.016, 0.016], 'Gold', seg=8)
        if k % 2 == 0:
            torus(0.07, 0.014, 'Gold', loc=(cx, 0, zc), rot=(math.pi / 2, 0, 0), seg=20, rseg=6); smooth_parts(last())
        else:
            smooth_parts(uvs(0.032, 'Gold', loc=(cx, 0, zc), seg=10, rings=6))
    box(L - 0.2, 0.05, 0.025, 'Gold', loc=(0, 0, zc), bevel=0.005)

def hq_gold_post(): fence_post(0, 0, h=1.3, w=0.26)

def hq_wheat_banner(H=2.7):
    smooth_parts(lathe([(0, 0), (0.2, 0), (0.18, 0.08), (0.08, 0.14), (0.05, 0.2), (0, 0.2)], 'Gold', seg=24))
    cyl(0.04, H, 'Gold', loc=(0, 0, H / 2 + 0.1), seg=12)
    cyl(0.03, 0.9, 'Gold', loc=(0, -0.0, H - 0.05), rot=(0, math.pi / 2, 0), seg=10)
    for sx in (-1, 1): smooth_parts(uvs(0.06, 'Gold', loc=(sx * 0.47, 0, H - 0.05), seg=10, rings=6))
    smooth_parts(lathe([(0, 0), (0.05, 0), (0.07, 0.07), (0.03, 0.16), (0, 0.22)], 'Gold', loc=(0, 0, H + 0.08), seg=12))
    # cloth: hangs under the cross bar, swallow-tail bottom, gold rim
    W, Hc = 0.78, 1.15; top = H - 0.1
    pts = [(-W / 2, top), (W / 2, top), (W / 2, top - Hc), (0, top - Hc + 0.22), (-W / 2, top - Hc)]
    o = prism(pts, -0.012, 0.012, 'Canvas')
    for v in o.data.vertices:
        x, y, z = v.co; v.co = Vector((x, z, y))
        v.co.y += 0.02 * math.sin(v.co.z * 4.0 + v.co.x * 3.0)
    for (a, b) in ((pts[1], pts[2]), (pts[2], pts[3]), (pts[3], pts[4]), (pts[4], pts[0])):
        tube([Vector((a[0], -0.0, a[1])), Vector((b[0], -0.0, b[1]))], [0.018, 0.018], 'Gold', seg=6)
    wheat_emblem((0, -0.03, top - Hc * 0.48), 0.55, z_up=False)

def hq_gold_lamp(H=2.6):
    smooth_parts(lathe([(0, 0), (0.24, 0), (0.24, 0.06), (0.16, 0.12), (0.1, 0.3), (0.07, 0.36), (0, 0.36)], 'Gold', seg=24))
    smooth_parts(lathe([(0.055, 0.36), (0.045, H * 0.6), (0.06, H * 0.62), (0.04, H * 0.64), (0.035, H - 0.05), (0.07, H), (0, H)], 'Gold', seg=14))
    torus(0.07, 0.015, 'Gold', loc=(0, 0, H * 0.3), seg=16, rseg=6); smooth_parts(last())
    lantern((0, 0, H), 1.35)

# ---------------------------------------------------------------- almond props

def hq_almond_big(): almond_shape(1.25, (0, 0, 0.3), rot=(0, 0.12, 0.3))

def hq_almond_half(L=1.0):
    # open shell: two halves (cupped) with the kernel inside
    for sgn in (-1, 1):
        prof = [(0, -L / 2)] + [(L * 0.34 * math.sin(math.pi * t) ** 0.8, -L / 2 + L * t) for t in [k / 12 for k in range(1, 12)]] + [(0, L / 2)]
        o = lathe(prof, 'Sand', seg=24)
        bm_keep = []
        for v in o.data.vertices:
            x, y, z = v.co
            v.co = Vector((z, x, y * 0.7))
        import bmesh
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, 0), plane_no=(0, -sgn, 0), clear_outer=True)
        bm.to_mesh(o.data); bm.free()
        mod = o.modifiers.new('s', 'SOLIDIFY'); mod.thickness = 0.03
        o.location = (0, sgn * 0.08, 0.17); o.rotation_euler = (sgn * 0.5, 0, 0); smooth_parts(o)
    almond_shape(L * 0.72, (0, 0, 0.19), m='Almond', flat=0.7)

def almond_pile(cx, cy, z, w, d, n, rng, s=0.22):
    for k in range(n):
        x = cx + rng.uniform(-w / 2, w / 2); y = cy + rng.uniform(-d / 2, d / 2)
        almond_shape(s * rng.uniform(0.85, 1.15), (x, y, z + rng.uniform(0, 0.05)), rot=(rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), rng.uniform(0, 6.28)))

def hq_almond_crate():
    rng = random.Random(41)
    plank_box(1.1, 0.8, 0.55, (0, 0, 0.275), rng=rng)
    smooth_parts(blob(0.42, 'Almond', (0, 0, 0.5), sub=3, lump=0.15, seed=4, scl=(1.25, 0.85, 0.42)))   # heaped almonds over the rim
    almond_pile(0, 0, 0.62, 0.8, 0.5, 30, rng)
    for k in range(3): almond_shape(0.22, (rng.uniform(-0.4, 0.4), -0.55 - rng.uniform(0, 0.2), 0.05), rot=(0, 0, rng.uniform(0, 6.28)))

def sack_body(h, r, m='Burlap', seed=7, open_top=0.7):
    prof = [(0, 0), (r * 0.8, 0.0), (r * 1.0, h * 0.12), (r * 1.06, h * 0.4), (r * 0.98, h * 0.7), (r * open_top, h * 0.92), (r * open_top * 1.08, h)]
    o = lathe(prof, m, seg=28)
    for v in o.data.vertices:
        x, y, z = v.co; rr = math.hypot(x, y)
        if rr > 1e-4:
            f = 1 + 0.06 * noise.noise(Vector((x * 3, y * 3, z * 2 + seed)))
            v.co.x *= f; v.co.y *= f
    smooth_parts(o)
    return o

def hq_almond_sack():
    rng = random.Random(42); h, r = 0.9, 0.42
    sack_body(h, r)
    smooth_parts(lathe([(0, h * 0.95), (r * 0.72, h * 0.95), (r * 0.5, h * 1.06), (0, h * 1.1)], 'Almond', seg=24))
    almond_pile(0, 0, h * 1.04, 0.5, 0.5, 12, rng, s=0.18)
    torus(r * 0.74, 0.025, 'BurlapLight', loc=(0, 0, h * 0.97), seg=28, rseg=6); smooth_parts(last())
    for k in range(3):                                                              # leaves beside it
        a = 0.6 + k * 0.5
        leaf_blade((0.45 * math.cos(a), 0.45 * math.sin(a), 0.02), (math.cos(a), math.sin(a), 0.6), 0.45, 0.16, 'Leaf', curl=0.3)
    almond_shape(0.2, (-0.5, -0.2, 0.06), rot=(0, 0, 0.8))

# ---------------------------------------------------------------- chocolate props

def hq_choco_block(s=1.1):
    # a big chocolate bar lying on the ground, one corner broken off
    box(s, s * 0.75, 0.16, 'Choco', loc=(0, 0, 0.08), rot=(0, 0, 0.25), bevel=0.03)
    c, sn = math.cos(0.25), math.sin(0.25)
    for i in range(3):
        for j in range(2):
            if i == 2 and j == 1: continue
            x = (i - 1) * s * 0.31; y = (j - 0.5) * s * 0.36
            box(s * 0.27, s * 0.31, 0.08, 'ChocoDark', loc=(x * c - y * sn, x * sn + y * c, 0.2), rot=(0, 0, 0.25), bevel=0.03)
    box(s * 0.27, s * 0.31, 0.2, 'Choco', loc=(s * 0.75, -s * 0.25, 0.1), rot=(0.3, 0.2, 1.2), bevel=0.03)   # broken piece

def hq_truffle(r=0.42):
    rng = random.Random(51)
    smooth_parts(uvs(r, 'Choco', loc=(0, 0, r * 0.9), scl=(1, 1, 0.9), seg=32, rings=18))
    for k in range(5):                                                              # white drizzle waves
        a0 = rng.uniform(0, 6.28); pts = []
        for t in range(13):
            a = a0 + t * 0.26; z = r * 0.9 + r * 0.55 * math.sin(t * 0.9 + k)
            rr = math.sqrt(max(0.0, r * r - (z - r * 0.9) ** 2 / 0.81)) + 0.01
            pts.append(Vector((rr * math.cos(a), rr * math.sin(a), z)))
        tube(pts, [0.02] * len(pts), 'Glaze', seg=6)
    smooth_parts(lathe([(0, 0), (r * 0.9, 0), (r * 0.95, 0.06), (0, 0.06)], 'WrapperPink', seg=32))

def hq_heart_bonbon(r=0.38):
    smooth_parts(lathe([(0, 0), (r, 0), (r * 1.02, r * 0.35), (r * 0.85, r * 0.75), (r * 0.4, r * 0.95), (0, r * 0.98)], 'Choco', seg=32))
    pts = []
    for k in range(40):
        t = 2 * math.pi * k / 40
        x = 16 * math.sin(t) ** 3; y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((x * r * 0.022, y * r * 0.022 + r * 0.05))
    o = prism(pts, r * 0.95, r * 1.06, 'Frosting'); smooth_parts(o)

def hq_choco_cube(s=0.6):
    box(s, s, s, 'ChocoDark', loc=(0, 0, s / 2), rot=(0, 0, 0.3), bevel=0.05)
    box(s * 0.7, s * 0.7, 0.04, 'Choco', loc=(0, 0, s + 0.01), rot=(0, 0, 0.3), bevel=0.02)

# ---------------------------------------------------------------- wheat / flour props

def hq_wheat_bundle(n=20, H=1.5):
    rng = random.Random(61)
    for k in range(n):
        a = rng.uniform(0, 6.28); rr = rng.uniform(0, 0.14)
        base = Vector((rr * math.cos(a), rr * math.sin(a), 0.02))
        tilt = Vector((math.cos(a) * (0.18 + rr), math.sin(a) * (0.18 + rr), 1)).normalized()
        L = H * rng.uniform(0.85, 1.05)
        tube([base, base + tilt * L * 0.55], [0.012, 0.009], 'Seed', seg=5)
        wheat_ear(base + tilt * L * 0.5, tilt, L * 0.5)
    torus(0.18, 0.04, 'Banner', loc=(0, 0, H * 0.32), seg=24, rseg=8); smooth_parts(last())
    torus(0.19, 0.012, 'Gold', loc=(0, 0, H * 0.32), seg=24, rseg=6); smooth_parts(last())

def hq_flour_sack():
    h, r = 1.0, 0.45
    sack_body(h, r, 'Burlap', 9, open_top=0.8)
    smooth_parts(blob(r * 0.62, 'White', (0, 0, h * 0.98), sub=3, lump=0.18, seed=9, scl=(1, 1, 0.45)))
    torus(r * 0.82, 0.03, 'BurlapLight', loc=(0, 0, h * 0.99), seg=28, rseg=6); smooth_parts(last())
    wheat_emblem((0, -r * 1.07, h * 0.45), 0.42, z_up=False)
    smooth_parts(blob(0.28, 'White', (0.35, -0.42, 0.02), sub=3, lump=0.2, seed=3, scl=(1.2, 0.9, 0.18)))

def hq_flour_scoop():
    # wooden scoop lying on a flour heap
    smooth_parts(blob(0.5, 'White', (0.15, 0, 0.0), sub=3, lump=0.22, seed=5, scl=(1.3, 1.0, 0.42)))
    cup = lathe([(0, 0), (0.18, 0.0), (0.21, 0.12), (0.22, 0.2), (0.2, 0.2), (0.19, 0.13), (0.16, 0.03), (0, 0.03)], 'Plank', seg=24)
    cup.location = (0.1, 0, 0.22); cup.rotation_euler = (0, -1.25, 0.2); smooth_parts(cup)
    smooth_parts(blob(0.15, 'White', (0.22, 0.03, 0.27), sub=2, lump=0.2, seed=6, scl=(1, 1, 0.6)))
    tube([Vector((-0.08, -0.04, 0.2)), Vector((-0.65, -0.18, 0.12))], [0.045, 0.04], 'PlankDark', seg=10)
    smooth_parts(uvs(0.05, 'PlankDark', loc=(-0.66, -0.18, 0.12), seg=10, rings=6))

# ---------------------------------------------------------------- plants

def hq_pink_flower_bush():
    rng = random.Random(71)
    hq_bush(0.75, seed=71, flowers=0)
    for k in range(26):
        a = rng.uniform(0, 6.28); e = rng.uniform(0.2, 1.2)
        n = Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))
        p = Vector((0, 0, 0.45)) + Vector((n.x * 0.78, n.y * 0.78, n.z * 0.55))
        petal_flower(p, n, rng.uniform(0.09, 0.13), rng, 'Frosting' if k % 3 else 'Blossom')

def hq_blossom_tree_s(): hq_blossom_tree(h=3.0, seed=9)
