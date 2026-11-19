from lib import *
from lib import _new
import random
V = Vector
# ===== Heaven Of Bakery material items (§89 D4) — two-handed, ~0.5 m, origin = centre (like ITEM_ChocolateOil) =====

def _sack_body(h, r, m, seed):
    """lumpy filled sack: lathe with a gathered neck"""
    random.seed(seed)
    prof = [(0, 0), (r * 0.78, 0.0), (r * 0.98, h * 0.12), (r, h * 0.35), (r * 0.95, h * 0.6), (r * 0.72, h * 0.78), (r * 0.30, h * 0.88), (r * 0.26, h * 0.9)]
    o = lathe(prof, m, seg=20)
    me = o.data
    for v in me.vertices:                                                         # soft lumps (filled with nuts/flour)
        if 0.02 < v.co.z < h * 0.85:
            a = math.atan2(v.co.y, v.co.x)
            k = 1.0 + 0.045 * math.sin(a * 3 + v.co.z * 9) + 0.03 * math.sin(a * 5 - v.co.z * 14)
            v.co.x *= k; v.co.y *= k
    return o

def item_almond_sack():
    h, r = 0.56, 0.24
    _sack_body(h, r, 'Burlap', 3)
    lathe([(r * 0.26, h * 0.88), (r * 0.36, h * 0.95), (r * 0.42, h * 1.02), (r * 0.30, h * 1.04), (r * 0.18, h * 0.96)], 'BurlapLight', seg=14, closed=True)   # frilled top
    torus(r * 0.29, 0.016, 'Banner', loc=(0, 0, h * 0.885), seg=20, rseg=6)       # red tie
    for k in range(7):                                                            # almonds peeking out of the top
        a = 2 * math.pi * k / 7; rr = r * 0.12
        uvs(0.035, 'Almond', loc=(rr * math.cos(a), rr * math.sin(a), h * 0.95 + 0.01 * (k % 2)), scl=(0.75, 0.75, 1.35), seg=10, rings=6, rot=(0.5 * math.cos(a), 0.5 * math.sin(a), a))
    uvs(0.035, 'Almond', loc=(0, 0, h * 0.99), scl=(0.75, 0.75, 1.35), seg=10, rings=6)
    # front badge: gold disc + almond emblem
    cyl(0.085, 0.02, 'Gold', loc=(0, -r * 1.0, h * 0.42), rot=(math.pi / 2 + 0.06, 0, 0), seg=20)
    uvs(0.045, 'Almond', loc=(0, -r * 1.0 - 0.02, h * 0.42), scl=(0.8, 0.45, 1.25), seg=12, rings=8)

def item_flour_sack():
    h, r = 0.6, 0.25
    _sack_body(h, r, 'White', 5)
    lathe([(r * 0.26, h * 0.88), (r * 0.34, h * 0.94), (r * 0.40, h * 1.0), (r * 0.28, h * 1.02), (r * 0.18, h * 0.95)], 'White', seg=14, closed=True)
    torus(r * 0.29, 0.016, 'Window', loc=(0, 0, h * 0.885), seg=20, rseg=6)       # blue tie
    torus(r * 1.005, 0.012, 'Window', loc=(0, 0, h * 0.22), seg=32, rseg=6)       # blue stripes
    torus(r * 1.0, 0.012, 'Window', loc=(0, 0, h * 0.30), seg=32, rseg=6)
    # wheat emblem on the front (gold): stem + 6 grains
    cyl(0.006, 0.16, 'Gold', loc=(0, -r * 1.02, h * 0.52), seg=6)
    for k in range(6):
        sgn = 1 if k % 2 else -1; z = h * 0.52 + 0.02 + 0.022 * (k // 2)
        uvs(0.016, 'WheatGold', loc=(sgn * 0.016, -r * 1.03, z), scl=(0.7, 0.5, 1.3), seg=8, rings=5, rot=(0, sgn * 0.5, 0))
    sph(0.05, 'White', loc=(0.06, -0.12, h * 0.98), scl=(1, 1, 0.4), sub=1)       # flour puff on top

def item_jam_jar():
    h, r = 0.5, 0.21
    lathe([(0, 0), (r * 0.92, 0), (r, 0.03), (r, h * 0.72), (r * 0.97, h * 0.74), (0, h * 0.74)], 'Jam', seg=28)        # jam seen through the glass
    lathe([(r * 0.97, h * 0.735), (r * 1.0, h * 0.735), (r * 1.0, h * 0.78), (r * 0.82, h * 0.86), (r * 0.80, h * 0.9), (r * 0.74, h * 0.9), (r * 0.76, h * 0.86), (r * 0.94, h * 0.78)], 'GlassRim', seg=28, closed=True)   # empty glass neck
    lathe([(0, h * 0.88), (r * 0.9, h * 0.88), (r * 0.92, h * 0.96), (0, h * 0.97)], 'Gold', seg=28)       # lid
    # gingham cloth cap: dome + frill
    lathe([(0, h * 1.08), (r * 0.6, h * 1.05), (r * 1.02, h * 0.98), (r * 1.08, h * 0.9), (r * 1.04, h * 0.86), (r * 0.9, h * 0.94), (0, h * 0.99)], 'White', seg=28, closed=True)
    torus(r * 0.87, 0.015, 'Banner', loc=(0, 0, h * 0.905), seg=24, rseg=7)       # ribbon
    # label: cream band + strawberry
    lathe([(r * 0.99, h * 0.30), (r * 1.03, h * 0.30), (r * 1.03, h * 0.58), (r * 0.99, h * 0.58)], 'Cream', seg=28, closed=True)
    sph(0.05, 'Berry', loc=(0, -r * 1.06, h * 0.44), scl=(1, 0.55, 1.15), sub=2)
    cyl(0.03, 0.012, 'Leaf', loc=(0, -r * 1.06, h * 0.495), r2=0.012, seg=6)
    for k in range(5):
        a = -math.pi / 2 + (k - 2) * 0.25; sph(0.006, 'Seed', loc=((r * 1.085) * math.cos(a) * 0.35, -r * 1.09, h * 0.42 + 0.02 * (k % 3)), sub=1)

def island_main(R=100.0, D=30.0, seed=15):
    """main fruit-field island: walkable top at z = 0 (grass), rock-column ring below, floating"""
    from objs2 import plateau
    plateau(R, D, seed)
    lathe([(0, -1.6), (R - 1.2, -1.6), (R * 0.82, -9.0), (R * 0.55, -17.0), (R * 0.25, -23.0), (0, -26.0)], 'RockDark', seg=64)   # floating underside (below the column feet)
