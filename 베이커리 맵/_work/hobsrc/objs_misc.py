from lib import *
from lib import _new
from objs_struct import lamp_post, pillar, rail_between
import random
# ===== floor tiles 1 x 1 m, top at z=0.10 (base slab top 0.08 + raised pavers/inlays) =====
def tile_base(m='Sand'):
    box(1.0, 1.0, 0.08, m, loc=(0,0,0.04), bevel=0.01)
def paver(x, y, sx, sy, m, rot=0.0, h=0.03, z0=0.07):
    box(sx, sy, h, m, loc=(x, y, z0+h/2), rot=(0,0,rot), bevel=0.006)     # z0..z0+h, sinks into slab
def tile_diamond():
    tile_base('Sand'); 
    for (x, y) in ((-0.25,-0.25),(0.25,-0.25),(-0.25,0.25),(0.25,0.25)): paver(x, y, 0.47, 0.47, 'Cream')
    paver(0, 0, 0.42, 0.42, 'PinkStone', rot=math.pi/4, h=0.03, z0=0.077)   # 0.077..0.107: 7 mm above the cream, bottom not shared
def tile_circle():
    tile_base('Sand'); paver(0,0,0.96,0.96,'Cream')
    cyl(0.44, 0.04, 'Orange', loc=(0,0,0.087), seg=32); cyl(0.30, 0.04, 'Sand', loc=(0,0,0.094), seg=32)   # tops 0.107 / 0.114
def tile_star():
    tile_base('Sand'); paver(0,0,0.96,0.96,'Cream')
    pts = []
    for i in range(16):
        r = 0.42 if i % 2 == 0 else 0.17; a = math.pi*2*i/16; pts.append((r*math.cos(a), r*math.sin(a)))
    prism(pts, 0.085, 0.107, 'Purple'); cyl(0.09, 0.03, 'Gold', loc=(0,0,0.099), seg=16)
def tile_red():
    tile_base('Sand'); paver(0,0,0.96,0.96,'RedTile'); paver(0,0,0.5,0.5,'Orange',rot=math.pi/4,h=0.03,z0=0.077)
    cyl(0.07, 0.03, 'Gold', loc=(0,0,0.099), seg=16)
def tile_checker():
    tile_base('Sand')
    for i in range(3):
        for j in range(3): paver(-0.32+0.32*i, -0.32+0.32*j, 0.3, 0.3, 'Cream' if (i+j)%2 == 0 else 'PinkStone')
def tile_grass(flowers=False):
    box(1.0, 1.0, 0.09, 'Soil', loc=(0,0,0.045), bevel=0.01)
    box(0.96, 0.96, 0.03, 'Grass', loc=(0,0,0.09), bevel=0.01)       # z 0.075..0.105, sides start inside the soil
    random.seed(7 if flowers else 3)
    for k in range(26):
        x, y = random.uniform(-0.44,0.44), random.uniform(-0.44,0.44)
        cyl(0.035, 0.12, 'Leaf', loc=(x,y,0.14+0.0009*k), r2=0.0, seg=5)   # every blade bottom at its own depth (0.9 mm apart, all under the grass top)
    if flowers:
        for k in range(14):
            x, y = random.uniform(-0.42,0.42), random.uniform(-0.42,0.42)
            sph(0.04, 'Flower' if k%2 else 'FlowerY', loc=(x,y,0.15+random.uniform(0,0.02)), sub=1); cyl(0.07, 0.05, 'LeafDark', loc=(x,y,0.085+0.0009*k), seg=6)   # bottoms 0.060.. (under the blades' 0.080..)
# ===== landscaping =====
def bush_round(r=0.6, berries=False):
    random.seed(11)
    for k in range(6):
        a = k*1.05; sph(r*random.uniform(0.55,0.75), 'Leaf' if k%2 else 'LeafDark', loc=(r*0.45*math.cos(a), r*0.45*math.sin(a), r*0.6+random.uniform(-0.05,0.1)), sub=2)
    sph(r*0.7, 'Leaf', loc=(0,0,r*0.95), sub=2)
    if berries:
        for k in range(12):
            a = random.uniform(0, 2*math.pi); e = random.uniform(0.2, 1.2)
            sph(0.07, 'Berry', loc=(r*1.0*math.cos(a)*math.cos(e*0.6), r*1.0*math.sin(a)*math.cos(e*0.6), r*0.7+r*0.5*math.sin(e)), sub=1)
def tree_round(h=3.0):
    cyl(0.16, h*0.55, 'Wood', loc=(0,0,h*0.275), r2=0.11, seg=10)
    for (x, y, z, r) in ((0,0,h*0.75,0.9),(0.45,0.2,h*0.65,0.6),(-0.4,-0.25,h*0.62,0.62),(0.1,-0.45,h*0.85,0.55)):
        sph(r, 'Leaf', loc=(x,y,z), sub=2)
def tree_fruit(h=3.2):
    tree_round(h); random.seed(5)
    for k in range(16):
        a = random.uniform(0,2*math.pi); z = random.uniform(h*0.55, h*0.95)
        sph(0.09, 'Apple', loc=(1.0*math.cos(a), 1.0*math.sin(a), z), sub=1)
def tree_pine(h=4.0):
    cyl(0.14, h*0.25, 'Wood', loc=(0,0,h*0.125), seg=8)
    for k in range(4):
        r = 1.1 - k*0.24; z0 = h*0.18 + k*h*0.18
        cyl(r, h*0.32, 'Pine' if k%2 else 'LeafDark', loc=(0,0,z0+h*0.16), r2=0.02, seg=12)
def flower_bed(r=1.0):
    lathe([(0,0),(r,0),(r,0.35),(r-0.15,0.35),(r-0.15,0.28),(0,0.28)], 'StoneGrey', seg=24)
    cyl(r-0.15, 0.06, 'Soil', loc=(0,0,0.27), seg=24)             # z 0.24..0.30 (pokes 2 cm over the 0.28 floor)
    random.seed(9)
    for k in range(18):
        a = random.uniform(0,2*math.pi); d = random.uniform(0, r-0.3)
        x, y = d*math.cos(a), d*math.sin(a)
        cyl(0.02, 0.25, 'Leaf', loc=(x,y,0.42), seg=5); sph(0.07, ('Flower','FlowerY','Berry')[k%3], loc=(x,y,0.56), sub=1)
def hedge(L=2.0, h=0.9):
    box(L, 0.6, h, 'LeafDark', loc=(0,0,h/2), bevel=0.12)
    random.seed(4)
    for k in range(int(L*4)):
        sg = random.choice((-1, 1))
        sph(random.uniform(0.15, 0.2), 'Leaf', loc=(random.uniform(-L/2+0.2, L/2-0.2), sg*(0.3+random.uniform(0, 0.06)), random.uniform(0.2,h-0.1)), sub=1)   # varied depth/size: no two balls share a front
# ===== misc =====
def urn():
    lathe([(0,0),(0.35,0),(0.35,0.1),(0.18,0.18),(0.14,0.45),(0.3,0.6),(0.45,0.8),(0.48,0.95),(0.38,1.0),(0,1.0)], 'Cream', seg=24)
    torus(0.46, 0.04, 'Gold', loc=(0,0,0.95), seg=24, rseg=8); torus(0.16, 0.035, 'Gold', loc=(0,0,0.42), seg=16, rseg=6)
    sph(0.36, 'Leaf', loc=(0,0,1.05), scl=(1,1,0.5), sub=2)
def barrel(h=1.0, r=0.4):
    lathe([(0,0),(r*0.85,0),(r,h*0.5),(r*0.85,h),(0,h)], 'Wood', seg=20)
    for z in (0.12, 0.5, 0.88): torus(r*(0.86 if z != 0.5 else 1.0)+0.012, 0.025, 'DarkWood', loc=(0,0,h*z), seg=24, rseg=6)
def bench(L=1.8):
    for x in (-L/2+0.15, L/2-0.15):
        box(0.08, 0.5, 0.45, 'Gold', loc=(x,0,0.225)); box(0.08, 0.08, 0.5, 'Gold', loc=(x,0.22,0.7))
    for k in range(3): box(L, 0.13, 0.05, 'Wood', loc=(0, -0.17+k*0.15, 0.47))
    for k in range(2): box(L, 0.05, 0.12, 'Wood', loc=(0, 0.25, 0.65+k*0.18))
def awning(w, d, z, slope=0.35, nstripe=8):
    # one mesh, stripes by face material (no overlapping planes)
    import bmesh as _b
    bm = _b.new(); verts = []
    for i in range(nstripe+1):
        x = -w/2 + w*i/nstripe
        verts.append((bm.verts.new((x, -d/2, z)), bm.verts.new((x, d/2, z+slope))))
    fs = []
    for i in range(nstripe): fs.append(bm.faces.new((verts[i][0], verts[i+1][0], verts[i+1][1], verts[i][1])))
    _b.ops.solidify(bm, geom=bm.faces[:], thickness=0.04)
    o = _new(bm, "awning", 'Canvas'); o.data.materials.append(mat('Stripe'))
    for p in o.data.polygons:
        k = int((p.center.x + w/2) / (w/nstripe)); p.material_index = 1 if k % 2 else 0
    # valance
    for i in range(nstripe):
        x = -w/2 + w*(i+0.5)/nstripe
        box(w/nstripe-0.004, 0.03, 0.16, 'Stripe' if i%2 else 'Canvas', loc=(x, -d/2-0.035, z-0.06))
def market_stall():
    box(2.0, 1.0, 0.9, 'Wood', loc=(0,0,0.45), bevel=0.02); box(2.06, 1.06, 0.06, 'DarkWood', loc=(0,0,0.93))
    for x in (-0.95, 0.95):
        for y in (-0.45, 0.45): cyl(0.05, 1.39, 'DarkWood', loc=(x,y,(0.93+2.32)/2), seg=8)
    awning(2.3, 1.3, 2.3)
    random.seed(2)
    for k in range(10): sph(0.09, ('Apple','Orange','Berry','Grape')[k%4], loc=(-0.8+0.18*k, random.uniform(-0.3,0.3), 1.04), sub=1)
    barrel(0.7, 0.28) if False else None
def fruit_cart():
    box(1.8, 1.0, 0.5, 'Wood', loc=(0,0,0.8), bevel=0.02)
    for x in (-0.55, 0.55):
        for y in (-0.56, 0.56): torus(0.4, 0.05, 'DarkWood', loc=(x,y,0.4), rot=(math.pi/2,0,0), seg=24, rseg=6); cyl(0.06, 0.06, 'Gold', loc=(x,y,0.4), rot=(math.pi/2,0,0), seg=8)
    cyl(0.04, 1.3, 'DarkWood', loc=(0,0,0.4), rot=(math.pi/2,0,0), seg=8)
    for x in (-0.85, 0.85):
        for y in (-0.45, 0.45): cyl(0.04, 1.3, 'DarkWood', loc=(x,y,1.05+0.65), seg=8)
    awning(2.0, 1.2, 2.3)
    random.seed(8)
    for k in range(14): sph(0.11, ('Apple','Orange','Berry','Grape','Cherry')[k%5], loc=(random.uniform(-0.7,0.7), random.uniform(-0.35,0.35), 1.12+random.uniform(0,0.08)), sub=1)
    box(0.08, 1.4, 0.06, 'DarkWood', loc=(-1.2, 0, 0.8), rot=(0,0,0))
def banner_pole():
    lathe([(0,0),(0.3,0),(0.3,0.15),(0.15,0.3),(0,0.3)], 'Gold', seg=16)
    cyl(0.06, 3.6, 'Gold', loc=(0,0,0.3+1.8), seg=12); box(1.4, 0.06, 0.06, 'Gold', loc=(0,0,3.6))
    sph(0.1, 'Gold', loc=(0,0,3.98), sub=2)
    for x in (-0.38, 0.38):
        pts = [(-0.25, 0.0), (0.25, 0.0), (0.25, -1.3), (0.0, -1.5), (-0.25, -1.3)]
        prism([(x+a, 3.55+b) for a, b in pts], -0.015, 0.015, 'Banner', rot=(math.pi/2, 0, 0))
        prism([(x+a*0.5, 3.0+b*0.5) for a, b in [(0,0.35),(0.3,0),(0,-0.35),(-0.3,0)]], -0.03, 0.03, 'Gold', rot=(math.pi/2, 0, 0))
def fence(L=2.0):
    box(L, 0.1, 0.1, 'Gold', loc=(0,0,1.0)); box(L, 0.08, 0.08, 'Gold', loc=(0,0,0.15)); box(L, 0.2, 0.12, 'PinkStone', loc=(0,0,0.06))
    n = int(L/0.25)
    for i in range(1, n):
        x = -L/2 + L*i/n; cyl(0.022, 0.8, 'Gold', loc=(x,0,0.58), seg=6)
        if i % 2 == 0: sph(0.045, 'Gold', loc=(x,0,0.62), sub=1)
def gate(W=2.2):
    for x in (-W/2-0.25, W/2+0.25): pillar(x, 0, 0, h=2.2)
    pts = []
    for i in range(17):
        a = math.pi*i/16; pts.append(Vector((-(W/2+0.12)*math.cos(a), 0, 2.2+0.6*math.sin(a))))
    c0 = Vector((0,0,2.2))
    sweep([(p, Vector((0,1,0)), (p-c0).normalized()) for p in pts], [(-0.08, 0.1), (0.08, 0.1), (0.08, -0.02), (-0.08, -0.02)], 'Gold')
    for sgn in (-1, 1):   # two open-able leaves (half width each), thin gold bars
        x0 = sgn*W/4
        box(W/2-0.06, 0.05, 0.06, 'Gold', loc=(x0, 0, 1.9)); box(W/2-0.06, 0.05, 0.06, 'Gold', loc=(x0, 0, 0.12))
        for k in range(6):
            cyl(0.02, 1.78, 'Gold', loc=(x0 - (W/2-0.12)/2 + (W/2-0.12)*k/5, 0, 1.01), seg=6)
def candy_cane_lamp():
    lathe([(0,0),(0.3,0),(0.3,0.12),(0.12,0.25),(0,0.25)], 'Cream', seg=16)
    # cane: straight part + hook, stripes as alternating mesh bands (face materials), one mesh
    import bmesh as _b
    pts = [Vector((0,0,0.25+3.0*i/30)) for i in range(31)]
    for i in range(1, 25):
        a = math.pi*i/24; pts.append(Vector((0.45-0.45*math.cos(a), 0, 3.25+0.45*math.sin(a))))
    bm = _b.new(); rings = []; r = 0.09; seg = 12
    for k, p in enumerate(pts):
        t = (pts[min(k+1,len(pts)-1)] - pts[max(k-1,0)]).normalized()
        u = t.cross(Vector((0,1,0))).normalized(); v = t.cross(u)
        rings.append([bm.verts.new(p + (u*math.cos(2*math.pi*j/seg) + v*math.sin(2*math.pi*j/seg))*r) for j in range(seg)])
    for k in range(len(rings)-1):
        for j in range(seg):
            f = bm.faces.new((rings[k][j], rings[k][(j+1)%seg], rings[k+1][(j+1)%seg], rings[k+1][j])); f.material_index = 1 if ((k + j//3) // 2) % 2 else 0
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    o = _new(bm, "cane", 'White'); o.data.materials.append(mat('Stripe'))
    end = pts[-1]
    cyl(0.03, 0.25, 'Gold', loc=(end.x, 0, end.z-0.2), seg=8); cyl(0.13, 0.32, 'Glow', loc=(end.x, 0, end.z-0.48), seg=12)
    for i in range(4):
        a = math.pi/4 + i*math.pi/2; cyl(0.02, 0.38, 'Gold', loc=(end.x+0.16*math.cos(a), 0.16*math.sin(a), end.z-0.48), seg=6)
    cyl(0.22, 0.08, 'Gold', loc=(end.x, 0, end.z-0.27), r2=0.08, seg=12)
