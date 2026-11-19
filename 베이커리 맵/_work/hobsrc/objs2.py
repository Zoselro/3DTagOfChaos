from lib import *
from lib import _new
from objs_struct import lamp_post
from objs_misc import awning
import random
V = Vector
# =====================================================================
#  helpers
# =====================================================================
def crown(x, y, z, s=1.0, m='Gold'):
    """small crown: band ring + 5 spikes + jewel balls (bottom of band at z)"""
    lathe([(0.40*s, 0), (0.50*s, 0), (0.50*s, 0.30*s), (0.40*s, 0.30*s)], m, loc=(x, y, z), seg=20, closed=True)
    sph(0.36*s, 'Banner', loc=(x, y, z+0.16*s), scl=(1, 1, 0.75), sub=2)          # velvet cushion (inside band)
    for k in range(5):
        a = 2*math.pi*k/5
        px, py = x+0.45*s*math.cos(a), y+0.45*s*math.sin(a)
        cyl(0.09*s, 0.32*s, m, loc=(px, py, z+0.40*s), r2=0.0, seg=6)            # bottom 0.24 s: inside the band
        sph(0.05*s, m, loc=(px, py, z+0.57*s), sub=1)
        sph(0.045*s, 'Berry', loc=(x+0.505*s*math.cos(a+0.63), y+0.505*s*math.sin(a+0.63), z+0.15*s), sub=1)
    sph(0.07*s, m, loc=(x, y, z+0.47*s), sub=2)
def frame_vecs(t):
    t = t.normalized(); side = V((0, 1, 0)) if abs(t.y) < 0.9 else V((1, 0, 0))
    up = t.cross(side).normalized(); side = up.cross(t).normalized(); return side, up
def tube(pts, r, m, seg=8):
    """round tube along polyline pts"""
    shape = [(r*math.cos(2*math.pi*i/seg), r*math.sin(2*math.pi*i/seg)) for i in range(seg)]
    path = []
    for i, p in enumerate(pts):
        t = (pts[min(i+1, len(pts)-1)] - pts[max(i-1, 0)])
        s, u = frame_vecs(t); path.append((p, s, u))
    return sweep(path, shape, m)
def cane_path(h, rh, z0=0.0, n_up=10, n_hook=12):
    pts = [V((0, 0, z0 + h*i/n_up)) for i in range(n_up+1)]
    for i in range(1, n_hook+1):
        a = math.pi*i/n_hook; pts.append(V((rh - rh*math.cos(a), 0, z0 + h + rh*math.sin(a))))
    return pts
def striped_tube(pts, r, ma='White', mb='Stripe', per=2, seg=10):
    """alternating bands; every band overlaps the next by one segment, the b-bands are 4 % fatter (no shared surfaces)"""
    k = 0; i = 0
    while i < len(pts)-1:
        j = min(i+per, len(pts)-1)
        seg_pts = pts[i:j+1]
        if k % 2 == 0: tube(seg_pts, r, ma, seg)
        else:
            a = seg_pts[0].lerp(seg_pts[1], -0.15); b = seg_pts[-1].lerp(seg_pts[-2], -0.15)   # stretch a little past both ends
            tube([a] + seg_pts[1:-1] + [b], r*1.04, mb, seg)
        i = j; k += 1
def door_cut_front(target, y_face, depth, z0, w=2.6, h=3.4):
    """front (-Y) arched opening: rectangle + half circle (w wide, h tall)"""
    hr = h - w/2
    c = box(w, depth, hr+0.002, 'Cream', loc=(0, y_face, z0 + hr/2 - 0.001)); bool_diff(target, c)
    c2 = cyl(w/2, depth, 'Cream', loc=(0, y_face, z0 + hr), rot=(math.pi/2, 0, 0), seg=28); bool_diff(target, c2)
def door_trim(y, z0, w=2.6, h=3.4, m='Gold', depth=0.3):
    hr = h - w/2; R = w/2 + 0.1
    pts = [V((R*math.cos(math.pi*i/18), y, z0+hr+R*math.sin(math.pi*i/18))) for i in range(19)]
    c0 = V((0, y, z0+hr))
    sweep([(p, V((0, 1, 0)), (p-c0).normalized()) for p in pts], [(-depth/2, 0.16), (depth/2, 0.16), (depth/2, 0.0), (-depth/2, 0.0)], m)
    for x in (-R-0.08, R+0.08): box(0.16, depth, hr, m, loc=(x, y, z0+hr/2))
def shell_box(w, d, h, t, m, z0=0.0):
    o = box(w, d, h, m, loc=(0, 0, z0+h/2)); c = box(w-2*t, d-2*t, h+2, m, loc=(0, 0, z0+h/2)); bool_diff(o, c); return o
def window(x, y, z, yaw, w=1.0, h=1.3, frame='Gold'):
    """window pane slightly proud of a wall whose face is at (x,y); frame behind it"""
    n = V((math.cos(yaw-math.pi/2), math.sin(yaw-math.pi/2), 0))       # outward normal (yaw = wall direction)
    box(w, 0.10, h, 'Window', loc=(x+n.x*0.05, y+n.y*0.05, z), rot=(0, 0, yaw))
    box(w+0.2, 0.06, h+0.2, frame, loc=(x+n.x*0.02, y+n.y*0.02, z), rot=(0, 0, yaw))
    box(w+0.3, 0.16, 0.08, frame, loc=(x+n.x*0.08, y+n.y*0.08, z-h/2-0.13), rot=(0, 0, yaw))    # sill
# =====================================================================
#  TERRAIN
# =====================================================================
def _rock_column(x, y, rc, z0, z1, yaw, m):
    n = random.randint(5, 7); pts = []
    for i in range(n):
        a = 2*math.pi*i/n + random.uniform(-0.2, 0.2); rr = rc*random.uniform(0.75, 1.0)
        pts.append((rr*math.cos(a), rr*math.sin(a)))
    prism(pts, z0, z1, m, loc=(x, y, 0), rot=(random.uniform(-0.04, 0.04), random.uniform(-0.04, 0.04), yaw))
def plateau(R=30.0, H=12.0, seed=1):
    """floating-cliff island: rock column ring + core + grass cap; top surface z = H (walkable disc radius R)"""
    random.seed(seed)
    cyl(R-1.0, H, 'RockDark', loc=(0, 0, H/2-0.45), seg=64)                       # core -0.45 .. H-0.45 (inside cap)
    lathe([(0, H), (R+0.15, H), (R+0.45, H-0.22), (R+0.35, H-0.6), (R-0.4, H-0.75), (0, H-0.75)], 'Grass', seg=96)  # cap, top = H
    a = 0.0
    while a < 2*math.pi - 0.02:
        rc = random.uniform(1.3, 2.2); rr = R + random.uniform(-0.9, 0.5)
        _rock_column(rr*math.cos(a), rr*math.sin(a), rc, -0.5-random.uniform(0.0, 0.9), H-0.4-random.uniform(0.2, 2.6), random.uniform(0, 6.28), 'Rock' if random.random() < 0.6 else 'RockDark')
        a += rc*1.25/rr
    for k in range(int(R*1.2)):                                                     # moss drips under the rim
        b = random.uniform(0, 2*math.pi); rr = R + random.uniform(0.1, 0.35)
        sph(random.uniform(0.25, 0.5), 'Moss', loc=(rr*math.cos(b), rr*math.sin(b), H-0.5-random.uniform(0.0, 0.4)), scl=(1, 1, 0.6), sub=1)
    for k in range(int(R*0.6)):                                                     # base boulders
        b = random.uniform(0, 2*math.pi); rr = R + random.uniform(0.8, 2.6); s = random.uniform(0.5, 1.2)
        sph(s, 'RockDark', loc=(rr*math.cos(b), rr*math.sin(b), s*0.3+random.uniform(-0.1, 0.1)), scl=(1.2, 1.0, 0.7), sub=1, rot=(0, 0, b))
def cliff_wall(L=10.0, H=6.0, seed=3, depth=3.0, gap=None):
    """straight cliff module along X, face toward -Y, grass on top (z = H). gap=(x0,x1) keeps columns behind y>-1.4 there"""
    random.seed(seed)
    box(L, depth, H-0.3, 'RockDark', loc=(0, depth/2+0.2, (H-0.3)/2-0.45))
    box(L+0.6, depth+1.8, 0.7, 'Grass', loc=(0, depth/2-0.4, H-0.35), bevel=0.15)    # cap top = H, front lip y = -1.7
    x = -L/2
    while x < L/2:
        rc = random.uniform(0.7, 1.2); y = random.uniform(-0.6, 0.2)
        if gap and gap[0]-rc < x < gap[1]+rc: y = random.uniform(0.0, 0.4); rc = min(rc, 0.9)
        _rock_column(x, y, rc, -0.5-random.uniform(0, 0.6), H-0.6-random.uniform(0.1, 1.8), random.uniform(0, 6.28), 'Rock' if random.random() < 0.6 else 'RockDark')
        x += rc*1.3
    for k in range(int(L*1.5)):
        sph(random.uniform(0.2, 0.4)+0.003*k, 'Moss', loc=(-L/2+L*(k+random.uniform(0.1, 0.9))/int(L*1.5), -1.6+random.uniform(-0.15, 0.1), H-0.55-random.uniform(0, 0.3)), scl=(1, 0.7, 0.7), sub=1)
def waterfall_cliff(L=12.0, H=8.0):
    cliff_wall(L, H, seed=9, gap=(-2.0, 2.0))
    # water sheet: on the cap, over the lip, falls into the pool (UV v runs along the flow -> scroll in Unity)
    prof = [V((0, 0.3, H+0.04)), V((0, -1.2, H+0.04)), V((0, -1.9, H-0.15)), V((0, -2.25, H-0.8)), V((0, -2.45, H-2.5)), V((0, -2.6, 0.6)), V((0, -2.65, 0.15))]
    path = []
    for i, p in enumerate(prof):
        t = (prof[min(i+1, len(prof)-1)] - prof[max(i-1, 0)]).normalized(); up = t.cross(V((1, 0, 0))).normalized()
        if up.z < 0 and abs(t.z) < 0.5: up = -up
        if up.y > 0 and abs(t.z) >= 0.5: up = -up
        path.append((p, V((1, 0, 0)), up))
    sweep(path, [(-1.7, 0.0), (1.7, 0.0), (1.7, 0.06), (-1.7, 0.06)], 'Water')
    lathe([(2.3, 0.0), (2.9, 0.0), (2.9, 0.42), (2.3, 0.42)], 'StoneGrey', loc=(0, -3.6, 0), seg=40, closed=True)   # pool rim
    cyl(2.38, 0.36, 'Water', loc=(0, -3.6, 0.17), seg=40)                         # water top 0.35 (side buried in rim)
    random.seed(4)
    for k in range(14):
        a = random.uniform(-1.2, 1.2); sph(random.uniform(0.18, 0.35), 'White', loc=(a*1.4, -2.75+random.uniform(-0.3, 0.3), 0.35+random.uniform(-0.05, 0.08)), scl=(1, 1, 0.6), sub=1)
def cloud(s=1.0, seed=5):
    random.seed(seed)
    for k in range(9):
        x = random.uniform(-2.2, 2.2)*s; y = random.uniform(-0.8, 0.8)*s; r = random.uniform(0.8, 1.4)*s*(1.2-abs(x)/(3.0*s))
        sph(r, 'CloudPink' if k % 3 else 'White', loc=(x, y, r*0.5+random.uniform(0, 0.4)*s), scl=(1, 1, 0.7), sub=2)
def rock_bush():
    random.seed(21)
    sph(0.9, 'Rock', loc=(0, 0, 0.45), scl=(1.3, 1.0, 0.8), sub=2, rot=(0, 0, 0.4))
    sph(0.6, 'RockDark', loc=(0.9, 0.3, 0.3), scl=(1.0, 0.9, 0.75), sub=1)
    for (x, y, z, r) in ((-0.9, -0.3, 0.55, 0.6), (-0.4, 0.6, 0.75, 0.55), (-1.2, 0.4, 0.4, 0.45), (0.3, 0.75, 0.95, 0.45)):
        sph(r, 'Leaf' if r > 0.5 else 'LeafDark', loc=(x, y, z), sub=2)
def rock_mushroom():
    sph(1.0, 'Rock', loc=(0, 0, 0.4), scl=(1.4, 1.1, 0.75), sub=2, rot=(0, 0, 0.3))
    for (x, y, h, r) in ((0.3, 0.1, 1.1, 0.55), (-0.6, 0.4, 0.7, 0.35), (0.9, -0.4, 0.55, 0.28)):
        cyl(r*0.32, h, 'White', loc=(x, y, 0.5+h/2), seg=12, r2=r*0.26)
        lathe([(0, 0.5+h+r*0.55), (r*0.55, 0.5+h+r*0.45), (r, 0.5+h+r*0.1), (r*0.98, 0.5+h-0.02), (0, 0.5+h+0.02)], 'Mushroom', seg=20)
        for k in range(5):
            a = 2*math.pi*k/5 + h; sph(r*0.12, 'White', loc=(x+r*0.62*math.cos(a), y+r*0.62*math.sin(a), 0.5+h+r*0.36), scl=(1, 1, 0.5), sub=1)
# =====================================================================
#  MATERIAL BUILDINGS (walk-in, door at -Y 2.6 x 3.4 m, floor z = 0.3)
# =====================================================================
FZ = 0.3
def almond_factory():
    W, D, H = 10.0, 8.0, 6.0
    box(12.0, 10.0, FZ, 'PinkStone', loc=(0, 0, FZ/2), bevel=0.05)
    w = shell_box(W, D, H, 0.4, 'Cream', FZ); door_cut_front(w, -D/2, 1.6, FZ)
    door_trim(-D/2-0.1, FZ)
    box(W+0.8, D+0.8, 0.4, 'Wood', loc=(0, 0, FZ+H+0.2), bevel=0.06)                # roof slab
    for z in (FZ+1.0, FZ+H-0.35): box(W+0.12, D+0.12, 0.22, 'Gold', loc=(0, 0, z))  # gold bands (proud of walls)
    uvs(2.6, 'Almond', loc=(-1.2, 0.4, FZ+H+0.5), scl=(1.25, 0.95, 1.05), seg=28, rings=16)   # big almond dome
    cyl(1.5, 7.0, 'PinkWall', loc=(3.0, 2.0, FZ+H+0.3+3.5), seg=24)                  # tower
    torus(1.52, 0.12, 'Gold', loc=(3.0, 2.0, FZ+H+3.0), seg=32); torus(1.52, 0.12, 'Gold', loc=(3.0, 2.0, FZ+H+6.9), seg=32)
    uvs(1.3, 'Almond', loc=(3.0, 2.0, FZ+H+7.0+1.2), scl=(0.85, 0.85, 1.45), seg=20, rings=12)
    crown(3.0, 2.0, FZ+H+10.0, 0.9)
    cyl(0.5, 3.0, 'RedTile', loc=(-3.8, 2.8, FZ+H+1.6), seg=12); torus(0.52, 0.08, 'Gold', loc=(-3.8, 2.8, FZ+H+3.0), seg=16)
    for x in (-3.0, 3.0): window(x, -D/2, FZ+3.2, 0.0)
    for y in (-1.5, 1.5): window(W/2, y, FZ+3.2, math.pi/2); window(-W/2, y, FZ+3.2, -math.pi/2)
    sph(0.9, 'Almond', loc=(0, -D/2-0.25, FZ+4.6), scl=(1.3, 0.35, 0.9), sub=2)     # almond sign over the door
    # inside: hopper + conveyor with almonds
    lathe([(0.4, FZ+1.2), (1.6, FZ+3.0), (1.5, FZ+3.0), (0.3, FZ+1.2)], 'Iron', loc=(2.5, 1.5, 0), seg=16, closed=True)
    for (x, y) in ((1.7, 0.7), (3.3, 0.7), (1.7, 2.3), (3.3, 2.3)): box(0.15, 0.15, 3.0, 'Iron', loc=(x, y, FZ+1.5))
    box(5.0, 1.0, 0.25, 'DarkWood', loc=(-1.5, 2.6, FZ+0.95)); box(5.2, 1.1, 0.1, 'Iron', loc=(-1.5, 2.6, FZ+0.78))
    for (x, y) in ((-3.8, 2.3), (0.8, 2.3), (-3.8, 2.9), (0.8, 2.9)): box(0.1, 0.1, 0.73, 'Iron', loc=(x, y, FZ+0.365))
    random.seed(8)
    for k in range(9): uvs(0.14, 'Almond', loc=(-3.6+k*0.5, 2.6+random.uniform(-0.2, 0.2), FZ+1.15), scl=(1.4, 0.9, 0.7), seg=10, rings=6, rot=(0, 0, random.uniform(0, 3)))
def flour_mill():
    r, H = 3.6, 7.0
    lathe([(0, 0), (r+1.6, 0), (r+1.6, FZ), (0, FZ)], 'PinkStone', seg=40)
    w = lathe([(r-0.4, FZ), (r, FZ), (r-0.25, FZ+H), (r-0.65, FZ+H)], 'Cream', seg=40, closed=True); door_cut_front(w, -r, 2.0, FZ)
    door_trim(-r-0.05, FZ, depth=0.5)
    torus(r-0.02, 0.12, 'Gold', loc=(0, 0, FZ+1.0), seg=40); torus(r-0.2, 0.14, 'Gold', loc=(0, 0, FZ+H-0.1), seg=40)
    lathe([(0, FZ+H-0.2), (r+0.5, FZ+H-0.2), (r+0.4, FZ+H+0.1), (1.2, FZ+H+3.4), (0, FZ+H+3.8)], 'RoofPink', seg=40)
    sph(0.35, 'Gold', loc=(0, 0, FZ+H+3.95), sub=2)
    box(1.6, 1.4, 1.8, 'Cream', loc=(0, r-0.2, FZ+H-1.2))                             # gear housing at the back
    cyl(0.22, 1.2, 'Wood', loc=(0, r+0.8, FZ+H-1.2), rot=(math.pi/2, 0, 0), seg=12)   # axle stub (blades hub at y = r+1.35)
    for aa in (-math.pi/2+0.95, -math.pi/2-0.95, math.pi/2+0.75, math.pi/2-0.75):
            window((r-0.12)*math.cos(aa), (r-0.12)*math.sin(aa), FZ+3.8, aa+math.pi/2, w=0.8, h=1.1)
    # wheat sign + sacks at the door
    for (x, y, s) in ((-2.4, -r-0.9, 1.0), (-1.9, -r-1.3, 0.8), (2.3, -r-1.0, 0.9)): flour_sack(x, y, FZ, s)
def flour_mill_blades(L=5.2):
    """separate rotating part: origin = hub, sails in XZ plane, spin about local Y"""
    cyl(0.45, 0.5, 'Gold', loc=(0, -0.05, 0), rot=(math.pi/2, 0, 0), seg=16)
    sph(0.3, 'Gold', loc=(0, 0.3, 0), sub=2)
    for k in range(4):
        a = math.pi/4 + k*math.pi/2; d = V((math.cos(a), 0, math.sin(a))); n = V((-math.sin(a), 0, math.cos(a)))
        box(0.18, 0.14, L, 'Wood', loc=tuple(d*(L/2+0.3) + V((0, -0.05, 0))), rot=(0, -a+math.pi/2, 0))
        box(1.25, 0.05, L-1.0, 'Canvas', loc=tuple(d*(L/2+0.8) + n*0.72 + V((0, -0.16, 0))), rot=(0, -a+math.pi/2, 0))
        for j in range(5):
            box(1.5, 0.08, 0.07, 'Wood', loc=tuple(d*(1.3+j*(L-1.4)/4) + n*0.72 + V((0, -0.08, 0))), rot=(0, -a+math.pi/2, 0))
def flour_sack(x=0, y=0, z=0, s=1.0):
    sph(0.42*s, 'Sack', loc=(x, y, z+0.38*s), scl=(1.0, 0.85, 1.1), sub=2)
    cyl(0.16*s, 0.2*s, 'Sack', loc=(x, y, z+0.86*s), r2=0.24*s, seg=10)
    torus(0.15*s, 0.03*s, 'Banner', loc=(x, y, z+0.8*s), seg=12, rseg=6)
    sph(0.1*s, 'Gold', loc=(x, y-0.36*s, z+0.42*s), scl=(1, 0.35, 1), sub=1)         # wheat stamp
def sack():
    flour_sack(0, 0, 0, 1.0); cyl(0.5, 0.03, 'White', loc=(0.35, -0.45, 0.012), seg=16)   # flour spill
def cake_shop():
    W, D, H = 9.0, 7.0, 4.6
    box(11.0, 9.0, FZ, 'PinkStone', loc=(0, 0, FZ/2), bevel=0.05)
    w = shell_box(W, D, H, 0.35, 'PinkWall', FZ); door_cut_front(w, -D/2, 1.4, FZ)
    door_trim(-D/2-0.1, FZ, m='White')
    prism([(-W/2-0.5, -D/2-0.5), (W/2+0.5, -D/2-0.5), (W/2+0.5, D/2+0.5), (-W/2-0.5, D/2+0.5)], FZ+H, FZ+H+0.3, 'Cream')
    for k in range(18):                                                              # frosting drips on the eave
        t = k/17.0; sph(0.28, 'Frosting', loc=(-W/2-0.4+t*(W+0.8), -D/2-0.52, FZ+H+0.08-0.12*(k % 2)), scl=(1, 0.55, 1.3), sub=1)
    # giant cake on the roof
    lathe([(0, FZ+H+0.3), (3.0, FZ+H+0.3), (3.0, FZ+H+1.9), (2.1, FZ+H+1.9), (2.1, FZ+H+3.1), (0, FZ+H+3.1)], 'Cream', seg=40)
    torus(3.02, 0.14, 'Frosting', loc=(0, 0, FZ+H+1.9), seg=40); torus(2.12, 0.12, 'Frosting', loc=(0, 0, FZ+H+3.1), seg=36)
    for k in range(8):
        a = 2*math.pi*k/8 + 0.2; strawberry_small(2.55*math.cos(a), 2.55*math.sin(a), FZ+H+1.95, 0.32)
    strawberry_small(0, 0, FZ+H+3.15, 0.6)
    for i in range(7):                                                               # striped awning (separate slats, gaps)
        x0 = -2.1 + i*0.6
        box(0.58, 1.5, 0.06, 'Stripe' if i % 2 == 0 else 'White', loc=(x0+0.29, -D/2-0.7, FZ+3.75), rot=(-0.35, 0, 0))
    for x in (-3.0, 3.0): window(x, -D/2, FZ+2.0, 0.0, w=1.5, h=1.4, frame='White')
    for y in (-1.2, 1.4): window(W/2, y, FZ+2.4, math.pi/2, frame='White'); window(-W/2, y, FZ+2.4, -math.pi/2, frame='White')
    # display counter inside
    box(4.0, 0.9, 1.0, 'Cream', loc=(0, 2.4, FZ+0.5)); box(4.1, 1.0, 0.08, 'Gold', loc=(0, 2.4, FZ+1.04))
    for x in (-1.3, 0.0, 1.3): cupcake_small(x, 2.4, FZ+1.08, 0.5)
def strawberry_small(x, y, z, s):
    sph(0.5*s, 'Berry', loc=(x, y, z+0.45*s), scl=(1, 1, 1.15), sub=2)
    cyl(0.3*s, 0.08*s, 'Leaf', loc=(x, y, z+0.98*s), r2=0.12*s, seg=6)
def cupcake_small(x, y, z, s):
    lathe([(0, z), (0.3*s, z), (0.4*s, z+0.4*s), (0, z+0.4*s)], 'Cup', seg=16)
    lathe([(0, z+0.38*s), (0.45*s, z+0.42*s), (0.35*s, z+0.62*s), (0.12*s, z+0.82*s), (0, z+0.86*s)], 'Frosting', seg=16)
    sph(0.1*s, 'Cherry', loc=(0, 0, z+0.92*s), sub=1)
def choco_house():
    W, D, H = 8.0, 8.0, 4.4
    box(10.0, 10.0, FZ, 'PinkStone', loc=(0, 0, FZ/2), bevel=0.05)
    w = shell_box(W, D, H, 0.4, 'Choco', FZ); door_cut_front(w, -D/2, 1.6, FZ)
    door_trim(-D/2-0.1, FZ, m='Icing')
    for (cx, cy) in ((-W/2, -D/2), (W/2, -D/2), (-W/2, D/2), (W/2, D/2)): box(0.7, 0.7, H+0.25, 'ChocoDark', loc=(cx, cy, FZ+H/2+0.075))   # corner posts
    box(W+0.12, D+0.12, 0.3, 'Icing', loc=(0, 0, FZ+H-0.17))                         # icing band (proud)
    random.seed(12)
    for side in range(4):
        for k in range(10):
            t = -W/2 + 0.4 + k*(W-0.8)/9; ln = random.uniform(0.25, 0.7)
            px, py = [(t, -D/2-0.07), (W/2+0.07, t), (t, D/2+0.07), (-W/2-0.07, t)][side]
            cyl(0.1, ln, 'Icing', loc=(px, py, FZ+H-0.3-ln/2+0.02), seg=8); sph(0.11, 'Icing', loc=(px, py, FZ+H-0.3-ln+0.02), sub=1)
    # roof: pitched chocolate-bar slabs with squares
    for sg in (1, -1):
        ang = sg*0.5
        box(W+1.0, D/2/math.cos(0.5)+0.6, 0.3, 'ChocoDark', loc=(0, sg*D/4*1.02, FZ+H+0.15+D/4*math.tan(0.5)), rot=(ang, 0, 0))
        for i in range(5):
            for j in range(3):
                x = -W/2+0.5+i*(W-1.0)/4*1.0; tt = (j-1)*1.4
                c = V((x, sg*D/4*1.02 + tt*math.cos(0.5)*sg, FZ+H+0.15+D/4*math.tan(0.5) + tt*math.sin(0.5)))
                nrm = V((0, -math.sin(ang), math.cos(ang)))
                box(1.4, 1.2, 0.16, 'Choco', loc=tuple(c + nrm*0.18), rot=(ang, 0, 0), bevel=0.04)
    for sx in (-1, 1): prism([(-D/2-0.1, FZ+H), (D/2+0.1, FZ+H), (0, FZ+H+D/2*math.tan(0.5))], -0.2, 0.2, 'Choco', loc=(sx*(W/2-0.2), 0, 0), rot=(math.pi/2, 0, math.pi/2))
    sph(0.8, 'ChocoDark', loc=(2.2, 1.6, FZ+H+2.6), sub=2); torus(0.62, 0.1, 'Icing', loc=(2.2, 1.6, FZ+H+3.05), seg=20)   # truffle chimney
    for x in (-2.3, 2.3): window(x, -D/2, FZ+2.2, 0.0, frame='Icing')
    for y in (-1.5, 1.5): window(W/2, y, FZ+2.2, math.pi/2, frame='Icing'); window(-W/2, y, FZ+2.2, -math.pi/2, frame='Icing')
def choco_fountain():
    lathe([(4.2, 0), (5.0, 0), (5.0, 0.7), (4.6, 0.75), (4.2, 0.7)], 'ChocoDark', seg=48, closed=True)   # basin rim
    cyl(4.3, 0.55, 'ChocoFlow', loc=(0, 0, 0.27), seg=48)                            # chocolate pool top 0.545
    torus(4.62, 0.09, 'Gold', loc=(0, 0, 0.76), seg=64)
    lathe([(0, 0.3), (0.9, 0.3), (0.7, 1.4), (0.55, 4.6), (0, 4.6)], 'Cream', seg=24)  # central column
    tiers = [(3.0, 1.6), (2.0, 2.9), (1.1, 4.0)]
    for i, (r, z) in enumerate(tiers):
        lathe([(0.5, z-0.4), (r, z), (r+0.12, z+0.25), (r-0.06, z+0.28), (0.5, z-0.1)], 'Cream', seg=40, closed=True)   # bowl
        cyl(r-0.04, 0.2, 'ChocoFlow', loc=(0, 0, z+0.12), seg=40)                    # chocolate in bowl, top z+0.22
        torus(r+0.08, 0.06, 'Gold', loc=(0, 0, z+0.24), seg=48)
        nz = tiers[i-1][1]+0.2 if i > 0 else 0.5    # curtain falls into the tier below
        lathe([(r+0.16, z+0.2), (r+0.24, z+0.2), (r+0.3, nz), (r+0.22, nz)], 'ChocoFlow', seg=40, closed=True)   # flowing curtain
    sph(0.55, 'ChocoDark', loc=(0, 0, 4.85), scl=(1, 1, 1.3), sub=2); cyl(0.2, 0.6, 'ChocoDark', loc=(0, 0, 5.75), r2=0.0, seg=10)
def exit_portal():
    """exit gate: emissive portal (HOB_Portal) inside a crowned stone arch; walk in toward +Y"""
    box(7.0, 4.0, 0.25, 'PinkStone', loc=(0, 0, 0.125), bevel=0.04)
    box(5.4, 3.0, 0.25, 'Cream', loc=(0, 0.2, 0.37), bevel=0.04)                   # step 2 (top 0.495)
    for x in (-2.3, 2.3):
        box(1.0, 1.2, 4.2, 'PinkStone', loc=(x, 0.2, 0.49+2.1), bevel=0.06)
        box(1.2, 1.4, 0.3, 'Gold', loc=(x, 0.2, 0.49+0.17)); box(1.2, 1.4, 0.3, 'Gold', loc=(x, 0.2, 0.49+3.9))
        crown(x, 0.2, 0.49+4.2, 0.6)
    R = 1.85; zc = 0.49+4.2
    pts = [V((R*math.cos(math.pi*i/20), 0.2, zc+R*math.sin(math.pi*i/20))) for i in range(21)]
    c0 = V((0, 0.2, zc))
    sweep([(p, V((0, 1, 0)), (p-c0).normalized()) for p in pts], [(-0.5, 0.6), (0.5, 0.6), (0.5, -0.05), (-0.5, -0.05)], 'PinkStone')
    sweep([(p+(p-c0).normalized()*0.6, V((0, 1, 0)), (p-c0).normalized()) for p in pts[2:-2]], [(-0.38, 0.14), (0.38, 0.14), (0.38, -0.02), (-0.38, -0.02)], 'Gold')
    crown(0, 0.2, zc+R+0.72, 1.3)
    out = [(1.85, 0.49+0.02)] + [(1.85*math.cos(math.pi*i/20), zc+1.85*math.sin(math.pi*i/20)) for i in range(21)] + [(-1.85, 0.49+0.02)]
    prism(out, -0.03, 0.03, 'Portal', loc=(0, 0.2, 0), rot=(math.pi/2, 0, 0))       # portal sheet, edges buried in pillars/arch
# =====================================================================
#  PROPS (crown / gold style)
# =====================================================================
def almond_tree(h=3.4):
    random.seed(31)
    cyl(0.2, h*0.6, 'Wood', loc=(0, 0, h*0.3), r2=0.12, seg=10)
    for a in (0.5, 2.6, 4.4):
        tube([V((0, 0, h*0.45)), V((0.5*math.cos(a), 0.5*math.sin(a), h*0.62)), V((0.9*math.cos(a), 0.9*math.sin(a), h*0.72))], 0.07, 'Wood', seg=6)
    for (x, y, z, r) in ((0, 0, h*0.85, 1.05), (0.75, 0.3, h*0.75, 0.75), (-0.7, -0.35, h*0.72, 0.8), (0.1, -0.7, h*0.9, 0.7), (-0.2, 0.7, h*0.95, 0.7)):
        sph(r, 'Leaf' if r > 0.75 else 'LeafDark', loc=(x, y, z), sub=2)
    for k in range(20):
        a = random.uniform(0, 2*math.pi); e = random.uniform(-0.2, 0.8); rr = 1.15
        uvs(0.11, 'Almond', loc=(rr*math.cos(a)*math.cos(e)*1.05, rr*math.sin(a)*math.cos(e)*1.05, h*0.82+rr*math.sin(e)*0.7), scl=(0.8, 0.8, 1.35), seg=8, rings=6, rot=(random.uniform(-0.5, 0.5), 0, a))
WHEAT_CLUMPS = []
def wheat_patch(W=3.0, D=3.0, seed=41):
    """dense wheat tile. Vertex colour R = sway weight (0 root .. 1 tip), G = clump phase -> wind shader"""
    random.seed(seed); WHEAT_CLUMPS.clear()
    box(W, D, 0.12, 'Soil', loc=(0, 0, 0.06), bevel=0.02)
    nx, ny = int(W/0.42), int(D/0.42); placed = []; tops = []
    for i in range(nx):
        for j in range(ny):
            cx = -W/2 + (i+0.5)*W/nx + random.uniform(-0.08, 0.08); cy = -D/2 + (j+0.5)*D/ny + random.uniform(-0.08, 0.08)
            WHEAT_CLUMPS.append((cx, cy))
            for k in range(random.randint(3, 4)):
                for _t in range(20):
                    x = cx + random.uniform(-0.12, 0.12); y = cy + random.uniform(-0.12, 0.12)
                    if all((x-px)**2 + (y-py)**2 > 0.07**2 for px, py in placed): break
                placed.append((x, y))
                for _t in range(20):
                    h = random.uniform(0.85, 1.15); lx, ly = random.uniform(-0.06, 0.06), random.uniform(-0.06, 0.06)   # slight lean
                    top = V((x+lx, y+ly, 0.1+h))
                    if all((top-q).length > 0.11 for q in tops): break
                tops.append(top)
                tube([V((x, y, 0.08 - 0.0007*k)), V((x+lx*0.5, y+ly*0.5, 0.1+h*0.55)), top], 0.012, 'WheatStem', seg=5)
                d = (top - V((x, y, 0.1))).normalized()
                hz = [(0.0, 0.0), (0.03, 0.03), (0.04, 0.10), (0.035, 0.18), (0.015, 0.24), (0.0, 0.26)]
                bm = bmesh.new(); rings = []
                s, u = frame_vecs(d)
                for (rr, zz) in hz:
                    rings.append([bm.verts.new(top + d*(zz-0.01) + s*(rr*math.cos(2*math.pi*q/6)) + u*(rr*1.3*math.sin(2*math.pi*q/6))) for q in range(6)])
                for a in range(len(rings)-1):
                    for q in range(6): bm.faces.new((rings[a][q], rings[a][(q+1) % 6], rings[a+1][(q+1) % 6], rings[a+1][q]))
                bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
                _new(bm, "head", 'WheatGold')
                tube([top + d*0.22, top + d*0.38 + s*0.03], 0.003, 'WheatGold', seg=3)         # awn
def wheat_post(o):
    """sway colours: island phase = phase of its nearest clump (stem, head and awn of a stalk share it)"""
    me = o.data; bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table()
    isl = {}; cur = 0; cen = []
    for f in bm.faces:
        if f.index in isl: continue
        st = [f]; isl[f.index] = cur; acc = V((0, 0, 0)); n = 0
        while st:
            g = st.pop(); acc += g.calc_center_median(); n += 1
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in isl: isl[h.index] = cur; st.append(h)
        cen.append(acc/n); cur += 1
    bm.free(); random.seed(99); ph = [random.random() for _ in WHEAT_CLUMPS]
    iph = []
    for c in cen:
        best = min(range(len(WHEAT_CLUMPS)), key=lambda q: (WHEAT_CLUMPS[q][0]-c.x)**2 + (WHEAT_CLUMPS[q][1]-c.y)**2)
        iph.append(ph[best])
    ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    for poly in me.polygons:
        for li in poly.loop_indices:
            z = me.vertices[me.loops[li].vertex_index].co.z
            w = max(0.0, min(1.0, (z-0.1)/1.25)); w = w*w
            ca.data[li].color = (w, iph[isl[poly.index]], 0.0, 1.0)
def crown_fence(L=2.0):
    for x in (-L/2, L/2):
        box(0.16, 0.16, 1.1, 'Cream', loc=(x, 0, 0.55)); box(0.24, 0.24, 0.1, 'Gold', loc=(x, 0, 1.15)); crown(x, 0, 1.2, 0.26)
        box(0.26, 0.26, 0.12, 'Gold', loc=(x, 0, 0.06))
    for z in (0.35, 0.85): box(L-0.16, 0.07, 0.1, 'Gold', loc=(0, 0, z))
    for k in range(5):
        x = -L/2 + 0.08 + (k+1)*(L-0.16)/6
        cyl(0.025, 0.62, 'Gold', loc=(x, 0, 0.6), seg=6); sph(0.05, 'Gold', loc=(x, 0, 0.95), sub=1)
def chocolate_bar():
    box(2.0, 1.0, 0.16, 'ChocoDark', loc=(0, 0, 0.08), bevel=0.02)
    for i in range(4):
        for j in range(2): box(0.42, 0.4, 0.1, 'Choco', loc=(-0.72+i*0.48, -0.23+j*0.46, 0.16+0.04), bevel=0.03)
    box(0.84, 1.06, 0.24, 'Banner', loc=(0.6, 0, 0.11)); box(0.08, 1.08, 0.26, 'Gold', loc=(0.2, 0, 0.11))   # wrapper + foil edge
    crown(0.62, 0, 0.23, 0.22)
def truffles():
    lathe([(0, 0), (0.95, 0), (1.05, 0.08), (0.97, 0.1), (0, 0.04)], 'Cream', seg=32)     # plate
    for (x, y, m) in ((0, 0.32, 'Choco'), (-0.3, -0.18, 'ChocoDark'), (0.32, -0.16, 'Choco')):
        sph(0.3, m, loc=(x, y, 0.33), sub=2); torus(0.24, 0.035, 'Icing', loc=(x, y, 0.45), seg=16, rseg=6)
        sph(0.04, 'Berry', loc=(x, y, 0.64), sub=1)
def strawberry_choco_cake():
    lathe([(0, 0), (1.05, 0), (1.1, 0.04), (0.98, 0.06), (0, 0.06)], 'Gold', seg=40)
    lathe([(0, 0.05), (0.85, 0.05), (0.85, 0.75), (0, 0.75)], 'Choco', seg=40)
    lathe([(0, 0.72), (0.88, 0.72), (0.9, 0.79), (0, 0.8)], 'ChocoDark', seg=40)
    for k in range(16):
        a = 2*math.pi*k/16; ln = 0.12 + 0.12*((k*7) % 3)/2
        cyl(0.05, ln, 'ChocoDark', loc=(0.875*math.cos(a), 0.875*math.sin(a), 0.75-ln/2), seg=6); sph(0.055, 'ChocoDark', loc=(0.875*math.cos(a), 0.875*math.sin(a), 0.75-ln), sub=1)
    for k in range(7):
        a = 2*math.pi*k/7; strawberry_small(0.55*math.cos(a), 0.55*math.sin(a), 0.78, 0.22)
    strawberry_small(0, 0, 0.79, 0.32)
def candy_cane(h=2.0, rh=0.4, r=0.09):
    striped_tube(cane_path(h, rh), r)
def candy_canes():
    for (x, y, h, yaw) in ((0, 0, 2.0, 0.0), (0.6, 0.3, 1.5, 2.0), (-0.5, 0.35, 1.2, 4.0)):
        pts = [V((x + p.x*math.cos(yaw) - p.y*math.sin(yaw), y + p.x*math.sin(yaw) + p.y*math.cos(yaw), p.z - 0.05)) for p in cane_path(h, h*0.2)]
        striped_tube(pts, 0.08*h/2)
    cyl(0.025, 1.3, 'White', loc=(-0.2, -0.5, 0.6), seg=8)                          # lollipop
    cyl(0.45, 0.1, 'Stripe', loc=(-0.2, -0.5, 1.6), rot=(math.pi/2, 0, 0), seg=32)
    for sg in (1, -1):
        sp = [V((-0.2 + 0.4*(i/40)*math.cos(i*0.45), -0.5 - sg*0.055, 1.6 + 0.4*(i/40)*math.sin(i*0.45))) for i in range(2, 41)]
        sweep([(p, V((0, 1, 0)), (p - V((-0.2, p.y, 1.6))).normalized()) for p in sp], [(-0.01, -0.025), (0.01, -0.025), (0.01, 0.025), (-0.01, 0.025)], 'White')
def small_candy_cane(): candy_cane(1.2, 0.25, 0.06)
def signpost():
    cyl(0.09, 2.6, 'Wood', loc=(0, 0, 1.3), seg=8); crown(0, 0, 2.6, 0.3)
    for (z, yaw, m, ln) in ((2.15, 0.3, 'Cream', 1.4), (1.7, 2.6, 'PinkStone', 1.2), (1.25, 1.2, 'Cream', 1.3)):
        pts = [(0.05, -0.17), (ln-0.25, -0.17), (ln, 0.0), (ln-0.25, 0.17), (0.05, 0.17)]
        prism(pts, -0.04, 0.04, m, loc=(0, 0, z), rot=(math.pi/2, 0, yaw))
        prism([(p[0]*0.96+0.03, p[1]*0.6) for p in pts], 0.035, 0.05, 'Gold', loc=(0, 0, z), rot=(math.pi/2, 0, yaw))
def banner_lamp():
    lamp_post(h=3.0)
    box(1.1, 0.07, 0.07, 'Gold', loc=(0.6, 0, 2.7)); sph(0.06, 'Gold', loc=(1.15, 0, 2.7), sub=1)
    prism([(0.25, 2.66), (0.95, 2.66), (0.95, 1.55), (0.6, 1.75), (0.25, 1.55)], -0.02, 0.02, 'Banner', rot=(math.pi/2, 0, 0))
    prism([(0.45, 2.45), (0.75, 2.45), (0.7, 2.2), (0.5, 2.2)], 0.02, 0.04, 'Gold', rot=(math.pi/2, 0, 0))
def candy_arch(W=4.0, H=3.4):
    for x in (-W/2, W/2):
        striped_tube([V((x, 0, -0.05 + H*i/12)) for i in range(13)], 0.22)
        lathe([(0, 0), (0.45, 0), (0.45, 0.2), (0.3, 0.3), (0, 0.3)], 'Gold', loc=(x, 0, 0), seg=16)
    pts = [V((W/2*math.cos(math.pi*i/16), 0, H + W/2*0.55*math.sin(math.pi*i/16))) for i in range(17)]
    striped_tube(pts, 0.2)
    box(1.8, 0.12, 0.6, 'Cream', loc=(0, -0.02, H+0.85)); box(1.9, 0.09, 0.7, 'Gold', loc=(0, 0.04, H+0.85))
    crown(0, 0, H+1.2, 0.45)
def wood_footbridge(L=6.0, W=2.0, rise=0.6):
    n = 15
    for i in range(n):
        t = (i+0.5)/n; x = -L/2 + t*L; z = 0.15 + rise*math.sin(math.pi*t); dz = rise*math.pi*math.cos(math.pi*t)/L
        box(L/n-0.04, W, 0.1, 'Wood', loc=(x, 0, z), rot=(0, -math.atan(dz), 0))
    for sg in (-1, 1):
        y = sg*(W/2-0.06)
        pts = [V((-L/2 + L*i/20, y, 0.1 + rise*math.sin(math.pi*i/20) - 0.12)) for i in range(21)]
        tube(pts, 0.07, 'DarkWood', seg=6)                                          # stringer under the planks
        for i in range(0, 21, 4):
            p = pts[i]; cyl(0.06, 1.0, 'DarkWood', loc=(p.x, y, p.z + 0.5), seg=8)
        tube([V((p.x, y, p.z + 0.95)) for p in pts], 0.05, 'Wood', seg=6)
        crown(pts[0].x, y, pts[0].z + 1.0, 0.18); crown(pts[-1].x, y, pts[-1].z + 1.0, 0.18)
def crown_banner():
    lathe([(0, 0), (0.3, 0), (0.3, 0.15), (0.12, 0.3), (0, 0.3)], 'Gold', seg=16)
    cyl(0.05, 3.4, 'Gold', loc=(0, 0, 1.95), seg=10); sph(0.1, 'Gold', loc=(0, 0, 3.7), sub=2)
    box(1.0, 0.06, 0.06, 'Gold', loc=(0.5, 0, 3.45))
    prism([(0.08, 3.42), (0.95, 3.42), (0.95, 1.6), (0.52, 1.9), (0.08, 1.6)], -0.02, 0.02, 'Banner', rot=(math.pi/2, 0, 0))
    prism([(0.3, 2.95), (0.74, 2.95), (0.74, 2.7), (0.3, 2.7)], 0.02, 0.035, 'Gold', rot=(math.pi/2, 0, 0))
    for k in range(3):
        x = 0.36 + k*0.16; prism([(x-0.06, 2.95), (x+0.06, 2.95), (x, 3.12)], 0.02, 0.035, 'Gold', rot=(math.pi/2, 0, 0))
def crate(s=1.0):
    box(s, s, s, 'Wood', loc=(0, 0, s/2))
    for sg in (-1, 1):                                                             # frame boards on the 4 vertical edges + brace
        for sg2 in (-1, 1): box(0.12, 0.12, s+0.02, 'DarkWood', loc=(sg*(s/2-0.04), sg2*(s/2-0.04), s/2))
        box(s+0.02, 0.1, 0.12, 'DarkWood', loc=(0, sg*(s/2+0.01), 0.07)); box(s+0.02, 0.1, 0.12, 'DarkWood', loc=(0, sg*(s/2+0.01), s-0.07))
        box(0.1, s-0.02, 0.12, 'DarkWood', loc=(sg*(s/2+0.01), 0, 0.09)); box(0.1, s-0.02, 0.12, 'DarkWood', loc=(sg*(s/2+0.01), 0, s-0.09))
        box(0.08, s*1.25, 0.1, 'DarkWood', loc=(sg*(s/2+0.03), 0, s/2), rot=(math.pi/4, 0, 0))
def treasure_chest():
    box(1.2, 0.8, 0.6, 'ChestRed', loc=(0, 0, 0.3))
    cyl(0.4, 1.18, 'ChestRed', loc=(0, 0, 0.6), rot=(0, math.pi/2, 0), seg=20)   # 1 cm inside the box ends
    for x in (-0.45, 0.45):
        box(0.1, 0.84, 0.61, 'Gold', loc=(x, 0, 0.3)); cyl(0.42, 0.088, 'Gold', loc=(x, 0, 0.6), rot=(0, math.pi/2, 0), seg=20)
    box(0.18, 0.08, 0.24, 'Gold', loc=(0, -0.42, 0.55)); sph(0.05, 'Berry', loc=(0, -0.47, 0.57), sub=1)
    random.seed(51)
    for k in range(10):
        cx, cy = random.uniform(-0.4, 0.4), random.uniform(-0.22, 0.22); tilt = math.asin(cy/0.4)
        cyl(0.07, 0.02, 'Gold', loc=(cx, cy*1.02, 0.6+math.sqrt(0.16-cy*cy)+0.012+0.004*k), rot=(-tilt, random.uniform(-0.15, 0.15), 0), seg=12)   # lying on the curved lid
def crown_stone_pillar(h=2.2):
    box(0.9, 0.9, 0.3, 'PinkStone', loc=(0, 0, 0.15), bevel=0.04)
    box(0.6, 0.6, h, 'Cream', loc=(0, 0, 0.3+h/2-0.01), bevel=0.04)
    box(0.8, 0.8, 0.22, 'PinkStone', loc=(0, 0, 0.3+h+0.1), bevel=0.04)
    box(0.68, 0.68, 0.12, 'Gold', loc=(0, 0, 0.3+0.5)); box(0.68, 0.68, 0.12, 'Gold', loc=(0, 0, 0.3+h-0.3))
    crown(0, 0, 0.3+h+0.21, 0.55)
def choco_pillar(h=2.4):
    box(0.9, 0.9, 0.3, 'ChocoDark', loc=(0, 0, 0.15), bevel=0.04)
    cyl(0.32, h, 'Choco', loc=(0, 0, 0.29+h/2), seg=16)
    for k in range(8):
        a = 2*math.pi*k/8; box(0.08, 0.08, h-0.3, 'ChocoDark', loc=(0.31*math.cos(a), 0.31*math.sin(a), 0.29+h/2), rot=(0, 0, a))
    lathe([(0, 0.29+h-0.05), (0.5, 0.29+h-0.05), (0.52, 0.29+h+0.15), (0, 0.29+h+0.2)], 'Icing', seg=20)
    for k in range(8):
        a = 2*math.pi*k/8 + 0.39; ln = 0.15 + 0.1*(k % 3)
        cyl(0.06, ln, 'Icing', loc=(0.45*math.cos(a), 0.45*math.sin(a), 0.29+h-ln/2+0.02), seg=6); sph(0.065, 'Icing', loc=(0.45*math.cos(a), 0.45*math.sin(a), 0.29+h-ln+0.02), sub=1)
    sph(0.28, 'ChocoDark', loc=(0, 0, 0.29+h+0.4), sub=2); sph(0.08, 'Berry', loc=(0, 0, 0.29+h+0.7), sub=1)
def flower_bush():
    random.seed(61)
    for k in range(5):
        a = k*1.26; sph(random.uniform(0.38, 0.5), 'Leaf' if k % 2 else 'LeafDark', loc=(0.35*math.cos(a), 0.35*math.sin(a), 0.4+random.uniform(-0.05, 0.08)), sub=2)
    sph(0.48, 'Leaf', loc=(0, 0, 0.62), sub=2)
    for k in range(16):
        a = random.uniform(0, 2*math.pi); e = random.uniform(0.1, 1.3)
        p = V((0.78*math.cos(a)*math.cos(e*0.7), 0.78*math.sin(a)*math.cos(e*0.7), 0.42+0.5*math.sin(e)))
        sph(0.09, 'Flower' if k % 3 else 'FlowerY', loc=tuple(p), scl=(1, 1, 0.6), sub=1); sph(0.035, 'FlowerY' if k % 3 else 'Flower', loc=tuple(p*1.06), sub=1)
def strawberry_cupcake(s=1.4):
    lathe([(0, 0), (s*0.32, 0), (s*0.42, s*0.42), (0, s*0.42)], 'Cup', seg=24)
    for k in range(12):
        a = 2*math.pi*k/12; box(s*0.03, s*0.06, s*0.40, 'Cream', loc=(s*0.375*math.cos(a), s*0.375*math.sin(a), s*0.20), rot=(0, 0, a))
    for (r, z) in ((0.38, 0.47), (0.3, 0.6), (0.2, 0.72)): torus(s*r, s*0.09, 'Frosting', loc=(0, 0, s*z), seg=32, rseg=12)
    strawberry_small(0, 0, s*0.72, s*0.32)
    for k in range(10):
        a = 2*math.pi*k/10; sph(s*0.025, 'Berry' if k % 2 else 'White', loc=(s*0.4*math.cos(a), s*0.4*math.sin(a), s*0.52), sub=1)
# =====================================================================
#  TAGGER ROOM (술래방): gothic iron cage gazebo, 4 doorways (±X, ±Y), purple glow
# =====================================================================
def tagger_gazebo(R=5.5):
    lathe([(0, 0), (R+1.4, 0), (R+1.4, 0.2), (R+0.8, 0.2), (R+0.8, 0.4), (0, 0.4)], 'StoneGrey', seg=64)   # stepped base, floor 0.4
    lathe([(R-0.6, 0.375), (R+0.5, 0.375), (R+0.5, 0.43), (R-0.6, 0.43)], 'PinkStone', seg=64, closed=True)  # floor ring (proud 3 cm)
    pts = []
    for i in range(16):
        r = 2.6 if i % 2 == 0 else 1.1; a = 2*math.pi*i/16; pts.append((r*math.cos(a), r*math.sin(a)))
    prism(pts, 0.37, 0.415, 'Purple')                                              # floor star
    torus(3.3, 0.04, 'PurpleGlow', loc=(0, 0, 0.405), seg=64, rseg=6)              # rune circle
    H = 4.2; posts = [2*math.pi*k/8 + math.pi/8 for k in range(8)]
    for a in posts:
        x, y = R*math.cos(a), R*math.sin(a)
        box(0.28, 0.28, H-0.05, 'Iron', loc=(x, y, 0.4+(H-0.05)/2-0.03), rot=(0, 0, a))
        box(0.44, 0.44, 0.3, 'Iron', loc=(x, y, 0.55), rot=(0, 0, a))
        sph(0.18, 'PurpleGlow', loc=(x*1.04, y*1.04, 0.4+H-0.6), sub=2)             # lantern orb (in front of the post)
        cyl(0.11, 0.5, 'Gold', loc=(x, y, 0.4+H+0.2), r2=0.0, seg=8)                 # finial
    torus(R, 0.12, 'Iron', loc=(0, 0, 0.4+H), seg=64, rseg=8)
    torus(R, 0.07, 'Gold', loc=(0, 0, 0.4+H-0.35), seg=64, rseg=6)
    for k in range(8):
        a1, a2 = posts[k], posts[(k+1) % 8]; mid = (a1+a2)/2 if k < 7 else (a1+a2+2*math.pi)/2
        door = (k % 2 == 1)                                                        # segments centred on 0/90/180/270° -> doorways
        p1 = V((R*math.cos(a1), R*math.sin(a1), 0)); p2 = V((R*math.cos(a2), R*math.sin(a2), 0))
        if door:                                                                   # pointed (gothic) arch, apex under the ring
            span = (p2-p1).length; zb = 0.4+2.9; apex = 0.4+H-0.15
            arch = []
            for i in range(13):
                t = i/12.0; q = p1.lerp(p2, t); lift = (apex-zb)*(1-abs(2*t-1)**1.6) ; arch.append(V((q.x, q.y, zb+lift)))
            tube(arch, 0.07, 'Iron', seg=6)
            for t in (0.25, 0.5, 0.75):                                            # tracery drops
                q = p1.lerp(p2, t); z = zb+(apex-zb)*(1-abs(2*t-1)**1.6)
                tube([V((q.x, q.y, z+0.02)), V((q.x, q.y, 0.4+H-0.06))], 0.035, 'Iron', seg=5)
        else:                                                                      # barred wall
            for i in range(1, 8):
                q = p1.lerp(p2, i/8.0); tube([V((q.x, q.y, 0.35)), V((q.x, q.y, 0.4+H-0.02))], 0.035, 'Iron', seg=6)
                sph(0.06, 'Gold', loc=(q.x, q.y, 0.4+H-0.55), sub=1)
            for z in (1.3, 2.9):
                tube([p1.lerp(p2, t)+V((0, 0, 0.4+z)) for t in (0.0, 0.5, 1.0)], 0.04, 'Iron', seg=6)
    # roof: 8 ogee ribs to a spire
    top = 0.4+H+4.0
    for a in posts:
        d = V((math.cos(a), math.sin(a), 0)); rib = []
        for i in range(15):
            t = i/14.0; r = R*(1-t)**0.75*(1+0.25*math.sin(math.pi*t)); z = 0.4+H+0.05+(top-0.4-H)*t**1.35
            rib.append(d*r + V((0, 0, z)))
        tube(rib, 0.09, 'Iron', seg=6)
    for (rr, zz) in ((R*0.78, 0.4+H+1.0), (R*0.48, 0.4+H+2.2)):
        torus(rr, 0.05, 'Iron', loc=(0, 0, zz), seg=48, rseg=6)
    cyl(0.32, 1.0, 'Iron', loc=(0, 0, top+0.35), r2=0.12, seg=12)
    sph(0.32, 'PurpleGlow', loc=(0, 0, top+1.05), sub=2); cyl(0.06, 0.9, 'Gold', loc=(0, 0, top+1.75), r2=0.0, seg=8)
    cyl(0.025, 2.3, 'Iron', loc=(0, 0, top-1.3), seg=6)                           # hanging lantern
    sph(0.42, 'PurpleGlow', loc=(0, 0, top-2.8), sub=2)
    for k in range(4):
        a = k*math.pi/2 + math.pi/4; tube([V((0, 0, top-2.35)), V((0.38*math.cos(a), 0.38*math.sin(a), top-2.85)), V((0, 0, top-3.3))], 0.025, 'Iron', seg=5)
