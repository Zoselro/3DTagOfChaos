from lib import *
from objs_struct import pillar, lamp_post
import random
# ===== fruit sculptures (s = overall height in m, base at z=0) =====
def strawberry(s=2.0):
    body = uvs(0.5, 'Berry', loc=(0,0,s*0.45), scl=(s*0.95, s*0.95, s*1.0), seg=24, rings=16)
    # taper the bottom (strawberry shape)
    for v in body.data.vertices:
        if v.co.z < 0: f = 1 + v.co.z*1.1; v.co.x *= max(0.25, f); v.co.y *= max(0.25, f)
    random.seed(1)
    for k in range(40):
        a = random.uniform(0,2*math.pi); e = random.uniform(-0.7, 0.5)
        rr = s*0.5*0.95*math.cos(e) * (max(0.25, 1+math.sin(e)*1.1) if e < 0 else 1)
        sph(s*0.022, 'Seed', loc=(rr*math.cos(a)*0.98, rr*math.sin(a)*0.98, s*0.45+s*0.5*math.sin(e)), sub=1)
    for k in range(6):
        a = k*math.pi/3
        cyl(s*0.09, s*0.04, 'Leaf', loc=(s*0.17*math.cos(a), s*0.17*math.sin(a), s*0.93), rot=(0.35*math.sin(a), -0.35*math.cos(a), 0), r2=s*0.01, seg=6)
    cyl(s*0.03, s*0.15, 'Stem', loc=(0,0,s*1.0), seg=8)
def apple(s=2.0):
    a = uvs(0.5, 'Apple', loc=(0,0,s*0.5), scl=(s*1.05, s*1.05, s*0.95))
    for v in a.data.vertices:  # dimple on top
        d = math.hypot(v.co.x, v.co.y)
        if v.co.z > 0.3 and d < 0.25: v.co.z -= (0.25-d)*0.5
    cyl(s*0.03, s*0.2, 'Stem', loc=(0,0,s*0.95), rot=(0.2,0,0), seg=8)
    sph(s*0.12, 'Leaf', loc=(s*0.12,0,s*1.0), scl=(1.6,0.6,0.2), rot=(0,-0.4,0.3), sub=2)
def orange(s=1.6, cut=True):
    o = uvs(0.5, 'Orange', loc=(0,0,s*0.5), scl=(s,s,s))
    if cut:  # cut the front half; flesh disk slightly inside the cut so no shared plane
        cutter = box(s*1.2, s*0.6, s*1.2, 'Orange', loc=(0, -s*0.3, s*0.5)); bool_diff(o, cutter)
        cyl(s*0.47, s*0.04, 'OrangeFlesh', loc=(0, s*0.005-s*0.02, s*0.5), rot=(math.pi/2,0,0), seg=32)   # y -0.035s..+0.005s
        for k in range(10):
            a = 2*math.pi*k/10
            box(s*0.42, s*0.012, s*0.02, 'Cream', loc=(s*0.21*math.cos(a), -s*0.03, s*0.5+s*0.21*math.sin(a)), rot=(0, -a, 0))
        cyl(s*0.05, s*0.02, 'Cream', loc=(0, -s*0.034, s*0.5), rot=(math.pi/2,0,0), seg=12)
def grapes(s=1.8):
    random.seed(3)
    rows = [(0.0,0.35,5),(0.25,0.3,4),(0.48,0.25,3),(0.66,0.18,3),(0.8,0.1,1)]
    for z, r, n in rows:
        for k in range(n*2):
            a = 2*math.pi*k/(n*2) + z
            sph(s*0.11, 'Grape', loc=(s*r*math.cos(a), s*r*math.sin(a), s*(0.95-z)), sub=2)
    cyl(s*0.03, s*0.2, 'Stem', loc=(0,0,s*1.05), seg=8)
    sph(s*0.2, 'Leaf', loc=(s*0.15,0,s*1.05), scl=(1.4,0.8,0.18), sub=2)
def cherries(s=1.2):
    for x in (-0.22, 0.22): sph(s*0.25, 'Cherry', loc=(s*x, 0, s*0.25), sub=3)
    for x in (-0.22, 0.22):
        a = Vector((s*x, 0, s*0.48)); b = Vector((0, 0, s*1.05)); d = b - a; L = d.length
        cyl(s*0.02, L, 'Stem', loc=((a+b)/2), rot=(0, math.atan2(d.x, d.z), 0), seg=6)
    sph(s*0.12, 'Leaf', loc=(s*0.1,0,s*1.05), scl=(1.6,0.6,0.2), sub=2)
def pumpkin(s=2.2):
    for k in range(10):
        a = 2*math.pi*k/10
        sph(s*0.3, 'Pumpkin', loc=(s*0.22*math.cos(a), s*0.22*math.sin(a), s*0.33), scl=(1,0.75,1.05), rot=(0,0,a), sub=3)
    sph(s*0.36, 'Pumpkin', loc=(0,0,s*0.36), sub=3)
    cyl(s*0.06, s*0.22, 'Stem', loc=(0,0,s*0.72), r2=s*0.04, seg=8)
def cupcake(s=1.8, top='Frosting'):
    lathe([(0,0),(s*0.32,0),(s*0.42,s*0.42),(0,s*0.42)], 'Cup', seg=24)
    for k in range(12):
        a = 2*math.pi*k/12; box(s*0.03, s*0.06, s*0.40, 'Cream', loc=(s*0.375*math.cos(a), s*0.375*math.sin(a), s*0.20), rot=(0, 0, a))   # ribs stop under the cup rim
    for i, (r, z) in enumerate(((0.38,0.47),(0.3,0.6),(0.2,0.72),(0.1,0.82))):
        torus(s*r, s*0.09, top, loc=(0,0,s*z), seg=32, rseg=12)
    sph(s*0.1, 'Cherry', loc=(0,0,s*0.95), sub=2); cyl(s*0.01, s*0.12, 'Stem', loc=(s*0.02,0,s*1.07), rot=(0,0.3,0), seg=6)
# ===== fruit field round plaza (과일 밭 바닥): r, with steps on 4 sides =====
def round_plaza(r=8.0, h=0.8):
    lathe([(0,0),(r,0),(r,h),(0,h)], 'StoneGrey', seg=64)
    torus(r-0.02, 0.06, 'Gold', loc=(0,0,h-0.03), seg=96, rseg=8)
    # paving rings: separate raised annuli (non overlapping radii, all tops 2 cm above the slab)
    rings = [(0.0, 2.0, 'RedTile'), (2.0, 2.3, 'Gold'), (2.3, 4.5, 'Cream'), (4.5, 4.8, 'PinkStone'), (4.8, r-0.3, 'Sand')]
    for r0, r1, m in rings:
        if r0 < 1e-6: lathe([(0, h-0.01), (r1, h-0.01), (r1, h+0.02), (0, h+0.02)], m, seg=64)   # centre disc
        else: lathe([(r0, h-0.01), (r1, h-0.01), (r1, h+0.02), (r0, h+0.02)], m, seg=64, closed=True)   # annulus
    for k in range(4):
        a = k*math.pi/2
        for i in range(4):
            z = h - (i+1)*0.2; d = r + 0.2 + i*0.4
            zz = max(z, 0.02)
            box(3.0, 0.4, zz, 'PinkStone' if i%2 else 'Cream', loc=(d*math.cos(a), d*math.sin(a), zz/2), rot=(0,0,a+math.pi/2))
        for sgn in (-1,1):
            x = (r+0.9)*math.cos(a) + sgn*1.8*math.cos(a+math.pi/2); y = (r+0.9)*math.sin(a) + sgn*1.8*math.sin(a+math.pi/2)
            pillar(x, y, 0.0, h=1.2)
