from lib import *
from objs_struct import pillar
# ===== walk-through entrance buildings (doors front -Y and back +Y, 2.4 x 3.2 m) — ramp entrances =====
DOOR_W, DOOR_H = 2.4, 3.2
def shell_cyl(r, h, wall, m, z0=0.0, seg=40):
    lathe([(r-wall, z0), (r, z0), (r, z0+h), (r-wall, z0+h)], m, seg=seg, closed=True)   # ring wall
def door_cut(target, r, z0=0.0):
    c = box(DOOR_W, r*2.6, DOOR_H, 'Cream', loc=(0, 0, z0 + DOOR_H/2 - 0.001)); bool_diff(target, c)
    # arch top
    c2 = cyl(DOOR_W/2, r*2.6, 'Cream', loc=(0, 0, z0+DOOR_H-0.001), rot=(math.pi/2,0,0), seg=24); bool_diff(target, c2)
def door_frames(r, z0=0.0):
    for sgn in (-1, 1):
        y = sgn*(r+0.02)
        pts = []
        for i in range(17):
            a = math.pi*i/16; pts.append(Vector(((DOOR_W/2+0.08)*math.cos(a), y, z0+DOOR_H+(DOOR_W/2+0.08)*math.sin(a))))
        c0 = Vector((0, y, z0+DOOR_H))
        sweep([(p, Vector((0,1,0)), (p-c0).normalized()) for p in pts], [(-0.12,0.14),(0.12,0.14),(0.12,0.0),(-0.12,0.0)], 'Gold')   # radial profile: no degenerate ends
        for x in (-(DOOR_W/2+0.08), DOOR_W/2+0.08): box(0.14, 0.24, DOOR_H, 'Gold', loc=(x, y, z0+DOOR_H/2))
def house_cake():
    r = 3.6; h = 4.6
    box(9.0, 9.0, 0.3, 'PinkStone', loc=(0,0,0.15), bevel=0.04)                 # base slab (floor inside)
    w = lathe([(r-0.35,0.3),(r,0.3),(r,h),(r-0.35,h)], 'Cream', seg=48, closed=True); door_cut(w, r, 0.3)
    torus(r+0.05, 0.12, 'Frosting', loc=(0,0,h), seg=48, rseg=10)
    torus(r+0.03, 0.08, 'Frosting', loc=(0,0,1.4), seg=48, rseg=8)
    lathe([(0,h-0.02),(r+0.35,h-0.02),(r+0.2,h+0.6),(r-0.6,h+1.6),(r-1.6,h+2.3),(0,h+2.5)], 'Frosting', seg=48)  # whipped roof
    for k in range(10):
        a = 2*math.pi*k/10 + 0.31
        sph(0.35, 'Cream', loc=((r+0.1)*math.cos(a), (r+0.1)*math.sin(a), h+0.15), scl=(1,1,0.8), sub=2)
    sph(0.7, 'Berry', loc=(0,0,h+2.9), scl=(1,1,1.1), sub=3); cyl(0.05, 0.4, 'Stem', loc=(0,0,h+3.7), seg=8)
    for a in (math.pi/2+0.9, math.pi/2-0.9, -math.pi/2+0.9, -math.pi/2-0.9):   # windows (frames proud of the wall)
        x, y = (r+0.03)*math.cos(a), (r+0.03)*math.sin(a)
        box(0.9, 0.12, 1.2, 'Window', loc=(x, y, 2.6), rot=(0,0,a+math.pi/2)); box(1.1, 0.08, 1.4, 'Gold', loc=((r+0.0)*math.cos(a), (r+0.0)*math.sin(a), 2.6), rot=(0,0,a+math.pi/2))
    door_frames(r, 0.3)
    for x in (-3.9, 3.9): pillar(x, -3.9, 0.3, h=1.1)
def house_tier():
    tiers = [(3.8, 3.6), (2.8, 2.4), (1.9, 2.0)]; z = 0.3
    box(9.0, 9.0, 0.3, 'PinkStone', loc=(0,0,0.15), bevel=0.04)
    for i, (r, h) in enumerate(tiers):
        if i == 0:
            w = lathe([(r-0.35,z),(r,z),(r,z+h),(r-0.35,z+h)], 'Cream', seg=48, closed=True); door_cut(w, r, z)
            lathe([(0,z+h-0.25),(r-0.34,z+h-0.25),(r-0.34,z+h-0.01),(0,z+h-0.01)], 'Cream', seg=48)   # ceiling slab 1 cm under the next tier
        else:
            lathe([(0,z),(r,z),(r,z+h),(0,z+h)], 'Cream' if i == 1 else 'PinkStone', seg=48)
        torus(r+0.04, 0.13, 'Frosting', loc=(0,0,z+h), seg=48, rseg=10)
        for k in range(14 - 3*i):
            a = 2*math.pi*k/(14-3*i)
            sph(0.18, 'Berry', loc=((r+0.12)*math.cos(a), (r+0.12)*math.sin(a), z+h+0.15), sub=2)
        z += h
    sph(0.5, 'Berry', loc=(0,0,z+0.45), sub=3)
    door_frames(3.8, 0.3)
def house_strawberry():
    r = 3.8
    box(9.4, 9.4, 0.3, 'Leaf', loc=(0,0,0.15), bevel=0.04)
    outer = uvs(1.0, 'Berry', loc=(0,0,3.4), scl=(r, r, 3.6), seg=40, rings=24)
    for v in outer.data.vertices:
        if v.co.z < 0: f = 1 + v.co.z*0.35; v.co.x *= f; v.co.y *= f
    inner = uvs(1.0, 'Berry', loc=(0,0,3.4), scl=(r-0.4, r-0.4, 3.2), seg=40, rings=24); bool_diff(outer, inner)
    floor = box(r*3, r*3, 3.0, 'Berry', loc=(0,0,0.3-1.5-0.0)); bool_diff(outer, floor)     # flat bottom at the slab top
    door_cut(outer, r, 0.3)
    import random; random.seed(6)
    for k in range(60):
        a = random.uniform(0, 2*math.pi); e = random.uniform(-0.3, 0.8)
        if abs(math.sin(a)) > 0.75 and e < 0.25: continue        # keep the doors clear
        rr = r*math.cos(e)*(1+min(0,math.sin(e))*0.35); z = 3.4 + 3.6*math.sin(e)
        if z < 0.6: continue
        sph(0.12, 'Seed', loc=(rr*1.005*math.cos(a), rr*1.005*math.sin(a), z), scl=(1,1,1.4), sub=1)
    for k in range(7):
        a = 2*math.pi*k/7
        cyl(1.2, 0.25, 'Leaf', loc=(1.4*math.cos(a), 1.4*math.sin(a), 6.75), rot=(0.4*math.sin(a), -0.4*math.cos(a), 0), r2=0.1, seg=8)
    cyl(0.25, 1.0, 'Stem', loc=(0,0,7.4), seg=10)
    for a in (0.0, math.pi):
        box(1.0, 0.25, 1.0, 'Window', loc=((r-0.1)*math.cos(a), 0, 3.8), rot=(0,0,a+math.pi/2))
    door_frames(r-0.15, 0.3)
