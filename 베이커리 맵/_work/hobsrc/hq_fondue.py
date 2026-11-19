# §89.17 초콜릿 분수 서브 맵: 초코 퐁듀 냄비(받침 + 녹은 초콜릿 + 꼬치) · 땅에 꽂힌 큰 퐁듀 꼬치 · 녹은 초콜릿 웅덩이(바닥에 붙음)
from lib import *
import random
from mathutils import noise
from objs_v2 import tube, blob, smooth_parts
from hq_props import last

def choco_surface(R, z, m='ChocoFlow', seg=48, rings=8, amp=0.025, seed=3, swirl=True):
    """flat molten chocolate disc with soft ripples / a swirl"""
    bm = bmesh.new(); grid = []
    c = bm.verts.new((0, 0, z + amp))
    for i in range(1, rings + 1):
        rr = R * i / rings; row = []
        for k in range(seg):
            a = 2 * math.pi * k / seg
            h = amp * math.sin(rr * 9.0 + (a * 2 if swirl else 0)) * (1 - i / rings * 0.6) + amp * 0.6 * noise.noise(Vector((math.cos(a) * rr * 1.5, math.sin(a) * rr * 1.5, seed)))
            if i == rings: h = 0.0
            row.append(bm.verts.new((math.cos(a) * rr, math.sin(a) * rr, z + h)))
        grid.append(row)
    for k in range(seg): bm.faces.new((c, grid[0][k], grid[0][(k + 1) % seg]))
    for i in range(rings - 1):
        for k in range(seg): bm.faces.new((grid[i][k], grid[i + 1][k], grid[i + 1][(k + 1) % seg], grid[i][(k + 1) % seg]))
    from lib import _new
    o = _new(bm, "choco", m); smooth_parts(o); return o

def dipped_strawberry(p, d, s, rng):
    """strawberry on a skewer, lower half dipped in chocolate (d = skewer direction, berry tip away from the stick)"""
    d = Vector(d).normalized()
    b = uvs(s * 0.5, 'Berry', loc=p, scl=(0.85, 0.85, 1.1), seg=16, rings=10)
    b.rotation_euler = d.to_track_quat('Z', 'Y').to_euler(); smooth_parts(b)
    c = uvs(s * 0.53, 'Choco', loc=p + d * s * 0.12, scl=(0.86, 0.86, 0.75), seg=16, rings=8)
    c.rotation_euler = d.to_track_quat('Z', 'Y').to_euler(); smooth_parts(c)
    for k in range(5):                                                        # green calyx leaves at the stick end
        a = 2 * math.pi * k / 5 + rng.uniform(-0.2, 0.2)
        s1, u1 = d.orthogonal().normalized(), d.cross(d.orthogonal()).normalized()
        q = p - d * s * 0.5 + (s1 * math.cos(a) + u1 * math.sin(a)) * s * 0.18
        tube([p - d * s * 0.48, q - d * s * 0.05], [s * 0.06, s * 0.01], 'Leaf', seg=4)

def marshmallow(p, d, s):
    o = cyl(s * 0.45, s * 0.7, 'Icing', loc=p, seg=16); o.rotation_euler = Vector(d).to_track_quat('Z', 'Y').to_euler(); smooth_parts(o)
    c = cyl(s * 0.47, s * 0.32, 'Choco', loc=p + Vector(d).normalized() * s * 0.22, seg=16); c.rotation_euler = o.rotation_euler; smooth_parts(c)

def hq_fondue_pot(R=1.1):
    """big fondue pot on a gold stand with a candle, molten chocolate with a swirl, 5 skewers leaning out"""
    rng = random.Random(17)
    lathe([(0, 0), (R * 0.75, 0), (R * 0.8, 0.08), (R * 0.55, 0.16), (R * 0.4, 0.5), (R * 0.45, 0.56), (0, 0.56)], 'Gold', seg=32)    # stand base
    for k in range(3):
        a = 2 * math.pi * k / 3
        tube([Vector((math.cos(a) * R * 0.55, math.sin(a) * R * 0.55, 0.12)), Vector((math.cos(a) * R * 0.95, math.sin(a) * R * 0.95, 0.75))], [0.05, 0.04], 'Gold', seg=8)
    cyl(0.14, 0.18, 'Icing', loc=(0, 0, 0.56), seg=16); uvs(0.05, 'Glow', loc=(0, 0, 0.8), scl=(0.7, 0.7, 1.6), seg=10, rings=6)            # candle
    prof = [(R * 0.5, 0.75), (R * 0.95, 0.85), (R * 1.05, 1.15), (R * 1.0, 1.55), (R * 1.08, 1.62), (R * 1.0, 1.66), (R * 0.92, 1.6), (R * 0.93, 1.2), (R * 0.82, 0.92), (R * 0.45, 0.85)]
    smooth_parts(lathe(prof, 'Pink' if False else 'WrapperPink', seg=40, closed=True))                                                   # ceramic pot
    torus(R * 1.04, 0.04, 'Gold', loc=(0, 0, 1.63), seg=40, rseg=8)
    choco_surface(R * 0.93, 1.5, seg=40, rings=7, amp=0.02, seed=4)
    for k in range(4):                                                        # drips over the rim
        a = 2 * math.pi * (k + 0.3) / 4 + rng.uniform(-0.3, 0.3)
        p = Vector((math.cos(a) * R * 1.02, math.sin(a) * R * 1.02, 1.62)); o = Vector((math.cos(a), math.sin(a), 0))
        L = rng.uniform(0.18, 0.4)
        tube([p, p + o * 0.04 + Vector((0, 0, -L * 0.6)), p + o * 0.03 + Vector((0, 0, -L))], [0.06, 0.045, 0.035], 'ChocoFlow', seg=8)
        smooth_parts(uvs(0.045, 'ChocoFlow', loc=p + o * 0.03 + Vector((0, 0, -L)), seg=10, rings=6))
    for k in range(5):                                                        # skewers: stick dips into the chocolate, fruit end out
        a = 2 * math.pi * k / 5 + 0.4
        o = Vector((math.cos(a), math.sin(a), 0)); base = Vector((math.cos(a) * R * 0.35, math.sin(a) * R * 0.35, 1.35))
        d = (o * 0.55 + Vector((0, 0, 1))).normalized(); L = 1.3
        tube([base, base + d * L], [0.018, 0.018], 'Wood', seg=6)
        if k % 2 == 0: dipped_strawberry(base + d * (L * 0.82), -d, 0.42, rng)
        else: marshmallow(base + d * (L * 0.8), -d, 0.38)

def hq_fondue_skewer(H=2.4):
    """giant skewer stuck in the ground: a dipped strawberry, a marshmallow and a banana slice, chocolate dripping"""
    rng = random.Random(23); d = Vector((0.12, 0.05, 1)).normalized()
    tube([Vector((0, 0, -0.1)), d * H], [0.05, 0.035], 'Wood', seg=8)
    smooth_parts(blob(0.32, 'ChocoFlow', (0, 0, 0.0), sub=2, lump=0.15, seed=4, scl=(1.6, 1.6, 0.18)))                                     # puddle at the foot
    dipped_strawberry(d * (H * 0.8), -d, 0.62, rng)
    marshmallow(d * (H * 0.52), d, 0.5)
    b = cyl(0.26, 0.16, 'Cream', loc=d * (H * 0.3), seg=20); b.rotation_euler = d.to_track_quat('Z', 'Y').to_euler(); smooth_parts(b)       # banana slice
    smooth_parts(uvs(0.2, 'Choco', loc=d * (H * 0.3) + Vector((0, 0, 0.06)), scl=(1.35, 1.35, 0.35), seg=16, rings=8))

def hq_choco_puddle(R=2.2, seed=5):
    """molten chocolate pool lying on the floor (walk-over decor, a few cm high) with a raised wavy rim and a couple of drops"""
    rng = random.Random(seed)
    bm = bmesh.new(); seg = 56; prof = []
    for k in range(seg):
        a = 2 * math.pi * k / seg
        prof.append(R * (1 + 0.18 * math.sin(a * 3 + seed) + 0.08 * math.sin(a * 7 + seed * 2)))
    from lib import _new
    c = bm.verts.new((0, 0, 0.05)); inner = []; outer = []
    for k in range(seg):
        a = 2 * math.pi * k / seg; r = prof[k]
        inner.append(bm.verts.new((math.cos(a) * r * 0.85, math.sin(a) * r * 0.85, 0.055 + 0.01 * math.sin(r * 8 + a * 3))))
        outer.append(bm.verts.new((math.cos(a) * r, math.sin(a) * r, 0.0)))
    for k in range(seg):
        bm.faces.new((c, inner[k], inner[(k + 1) % seg])); bm.faces.new((inner[k], outer[k], outer[(k + 1) % seg], inner[(k + 1) % seg]))
    o = _new(bm, "puddle", 'ChocoFlow'); smooth_parts(o)
    for k in range(3):
        a = rng.uniform(0, 6.28); r = R * rng.uniform(1.25, 1.5)
        smooth_parts(uvs(rng.uniform(0.12, 0.2), 'ChocoFlow', loc=(math.cos(a) * r, math.sin(a) * r, 0.0), scl=(1, 1, 0.25), seg=12, rings=6))
