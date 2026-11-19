from lib import *
# ===== lamps / pillars (reused inside other objects) =====
def lamp_post(x=0, y=0, z=0, h=3.2, scale=1.0):
    s = scale
    lathe([(0.0,0),(0.32*s,0),(0.32*s,0.12*s),(0.22*s,0.2*s),(0.2*s,0.5*s),(0.12*s,0.6*s),(0.0,0.6*s)], 'Gold', loc=(x,y,z), seg=16)
    cyl(0.07*s, h*s, 'Gold', loc=(x,y,z+0.55*s+h*s/2), seg=12)
    lathe([(0.0,0),(0.16*s,0),(0.2*s,0.06*s),(0.0,0.06*s)], 'Gold', loc=(x,y,z+0.55*s+h*s), seg=12)
    top = z + 0.61*s + h*s
    for i in range(4):  # lantern frame posts
        a = math.pi/4 + i*math.pi/2
        cyl(0.025*s, 0.5*s, 'Gold', loc=(x+0.17*s*math.cos(a), y+0.17*s*math.sin(a), top+0.25*s), seg=6)
    cyl(0.13*s, 0.42*s, 'Glow', loc=(x,y,top+0.25*s), seg=12)              # glowing core (inside frame, no shared planes)
    cyl(0.26*s, 0.12*s, 'Gold', loc=(x,y,top+0.56*s), r2=0.05*s, seg=12)   # cap
    sph(0.06*s, 'Gold', loc=(x,y,top+0.66*s), sub=2)
def pillar(x, y, z=0, h=1.4, w=0.4, lamp=True):
    box(w+0.1, w+0.1, 0.18, 'Cream', loc=(x,y,z+0.09), bevel=0.02)
    box(w, w, h, 'PinkStone', loc=(x,y,z+0.18+h/2), bevel=0.02)
    box(w+0.12, w+0.12, 0.14, 'Cream', loc=(x,y,z+0.18+h+0.07), bevel=0.02)
    if lamp:
        t = z+0.18+h+0.14
        cyl(0.05, 0.25, 'Gold', loc=(x,y,t+0.12), seg=10)
        cyl(0.12, 0.3, 'Glow', loc=(x,y,t+0.38), seg=12)
        for i in range(4):
            a = math.pi/4 + i*math.pi/2
            cyl(0.02, 0.36, 'Gold', loc=(x+0.15*math.cos(a), y+0.15*math.sin(a), t+0.38), seg=6)
        cyl(0.22, 0.1, 'Gold', loc=(x,y,t+0.6), r2=0.04, seg=12)
def rail_between(a, b, z0, h=1.0, post=0.07):
    """gold balustrade from a to b (2D points), posts every ~0.35 m, top & bottom rails"""
    a = Vector((a[0], a[1], 0)); b = Vector((b[0], b[1], 0)); d = b - a; L = d.length; d.normalize()
    ang = math.atan2(d.y, d.x); mid = (a + b) / 2
    box(L, 0.09, 0.08, 'Gold', loc=(mid.x, mid.y, z0+h), rot=(0,0,ang))
    box(L, 0.07, 0.06, 'Gold', loc=(mid.x, mid.y, z0+0.12), rot=(0,0,ang))
    n = max(2, int(L / 0.35))
    for i in range(1, n):
        p = a + d * (L * i / n); cyl(0.025, h-0.12, 'Gold', loc=(p.x, p.y, z0+0.12+(h-0.12)/2), seg=6)

# ===== HOB_SpiralRamp: S-ramp, field (z 0) -> plateau (z H) =====
def spiral_ramp(H=12.0, R=6.0, W=3.4, straight=4.0, T=0.45):
    # centerline: straight up y, semicircle right (center (0,straight+R)), semicircle left (center (0,straight+3R)), straight
    pts = []
    N1 = 8; Ns = 48
    for i in range(N1+1): pts.append(Vector((0, straight*i/N1, 0)))
    for k, (cy, sgn) in enumerate(((straight+R, 1), (straight+3*R, -1))):
        for i in range(1, Ns+1):
            a = -math.pi/2 + math.pi*i/Ns
            pts.append(Vector((sgn*R*math.cos(a), cy + R*math.sin(a), 0)))
    y0 = straight+4*R
    for i in range(1, N1+1): pts.append(Vector((0, y0 + straight*i/N1, 0)))
    # arc length -> height
    L = [0.0]
    for i in range(1, len(pts)): L.append(L[-1] + (pts[i]-pts[i-1]).length)
    tot = L[-1]
    for i, p in enumerate(pts): p.z = H * L[i] / tot
    def frame(i):
        p = pts[i]; t = (pts[min(i+1,len(pts)-1)] - pts[max(i-1,0)]); t.z = 0; t.normalize()
        side = Vector((t.y, -t.x, 0)); return p, side, Vector((0,0,1))
    path = [frame(i) for i in range(len(pts))]
    deck = [(-W/2, 0), (W/2, 0), (W/2, -T), (-W/2, -T)]
    sweep(path, deck, 'PinkStone')
    # gold edge trims: outside the deck sides (touch only along a line, no shared plane)
    inner = path[1:-1]   # trims/curbs/rails stop one step short of the deck ends: their end caps never share the deck's end planes
    for sgn in (-1, 1):
        sweep(inner, [(sgn*(W/2+0.0), 0.06), (sgn*(W/2+0.12), 0.06), (sgn*(W/2+0.12), -T-0.05), (sgn*(W/2+0.0), -T-0.05)][::(1 if sgn>0 else -1)], 'Gold')
        # cream curb on deck edge (raised 8 cm, inside the deck volume from below)
        sweep(inner, [(sgn*(W/2-0.19), 0.10), (sgn*(W/2-0.01), 0.10), (sgn*(W/2-0.01), -0.05), (sgn*(W/2-0.19), -0.05)][::(1 if sgn>0 else -1)], 'Cream')
        # top rail
        sweep(inner, [(sgn*(W/2-0.12), 1.05), (sgn*(W/2-0.03), 1.05), (sgn*(W/2-0.03), 0.97), (sgn*(W/2-0.12), 0.97)][::(1 if sgn>0 else -1)], 'Gold')
    # rail posts + support columns
    acc = 0.0
    for i in range(1, len(pts)):
        acc += (pts[i]-pts[i-1]).length
        p, sd, up = path[i]
        if acc >= 0.6 or i == len(pts)-1:
            acc = 0.0
            for sgn in (-1, 1):
                q = p + sd*sgn*(W/2-0.075); cyl(0.03, 0.95, 'Gold', loc=(q.x, q.y, q.z+0.55), seg=6)
    for i in range(0, len(pts), 6):
        p, sd, up = path[i]
        if p.z < 1.0: continue
        for sgn in (-1, 1):
            q = p + sd*sgn*(W/2-0.35); h = p.z - T + 0.06   # top 6 cm inside the deck (no gap, no shared face)
            cyl(0.28, h-0.01, 'Cream', loc=(q.x, q.y, 0.01+(h-0.01)/2), seg=16)   # starts inside the base drum
            cyl(0.38, 0.25, 'PinkStone', loc=(q.x, q.y, 0.125), seg=16)
            cyl(0.36, 0.22, 'Gold', loc=(q.x, q.y, p.z - T - 0.13), seg=16)
    # lamps at both ends
    for i in (0, len(pts)-1):
        p, sd, up = path[i]
        for sgn in (-1, 1):
            q = p + sd*sgn*(W/2+0.5); pillar(q.x, q.y, p.z if i else 0.0, h=1.0)   # bottom on field, top on the plateau
    return tot
# ===== HOB_Stairs: straight stair, rise H over run, width W, + landing =====
def stairs(H=3.0, W=4.0, step=0.2, tread=0.38, landing=2.0):
    n = int(round(H/step)); run = n*tread
    for i in range(n):
        z = (i+1)*step
        box(W, tread, z, 'Cream' if i % 2 == 0 else 'PinkStone', loc=(0, i*tread + tread/2, z/2))   # steps abut (touching faces face each other = hidden)
    box(W, landing, H, 'PinkStone', loc=(0, run + landing/2, H/2))
    # side walls (stringers) as sloped prisms, outside the step width
    for sgn in (-1, 1):
        x0 = sgn*(W/2-0.05); x1 = sgn*(W/2+0.35)   # stringer swallows the step ends
        pts = [(0, 0), (run, 0), (run+landing, 0), (run+landing, H+0.25), (run, H+0.25), (0, 0.45)]
        ob = prism([(y, z) for y, z in pts], min(x0,x1), max(x0,x1), 'PinkStone')
        ob.rotation_euler = (0, 0, 0)
        # prism built in (x=y,y=z,z=x) -> rotate to place: map (a,b,c)->(c,a,b)
        me = ob.data
        for v in me.vertices: a, b, c = v.co; v.co = Vector((c, a, b))
        # gold rail along the slope
        for sx in (sgn*(W/2+0.175),):
            rail_between((sx, 0.2), (sx, run), 0.0, h=0.0001)  if False else None
        steps_rail_z = lambda y: 0.45 + (H+0.25-0.45)*min(1, y/run)
        a = Vector((sgn*(W/2+0.175), 0.1, steps_rail_z(0.1)+0.05)); b = Vector((sgn*(W/2+0.175), run, steps_rail_z(run)+0.05))
        d = b - a; L = d.length; mid = (a+b)/2; pitch = math.atan2(d.z, d.y)
        box(0.1, L, 0.08, 'Gold', loc=(mid.x, mid.y, mid.z+0.95), rot=(pitch, 0, 0))
        for k in range(1, int(L/0.35)):
            p = a + d*(k/int(L/0.35)); cyl(0.025, 0.95, 'Gold', loc=(p.x, p.y, p.z+0.475), seg=6)
        rail_between((sgn*(W/2+0.175), run), (sgn*(W/2+0.175), run+landing-0.2), H+0.25)   # ends inside the corner pillars
        pillar(sgn*(W/2+0.175), -0.05, 0.003, h=1.0)
        pillar(sgn*(W/2+0.175), run, H+0.25-0.17, h=0.8)          # base top 1 cm above the stringer top
        pillar(sgn*(W/2+0.175), run+landing-0.2, H+0.25-0.17, h=0.8)
# ===== HOB_Bridge: deck between plateaus with railings and arch =====
def bridge(L=10.0, W=4.0, T=0.5):
    box(W, L, T, 'PinkStone', loc=(0, 0, -T/2))
    for sgn in (-1, 1):
        box(0.14, L, T+0.1, 'Gold', loc=(sgn*(W/2+0.07), 0, -T/2-0.02))
        box(0.19, L-0.04, 0.12, 'Cream', loc=(sgn*(W/2-0.105), 0, 0.065))
        rail_between((sgn*(W/2-0.1), -L/2+0.3), (sgn*(W/2-0.1), L/2-0.3), 0.0)
        for yy in (-L/2+0.35, L/2-0.35): pillar(sgn*(W/2-0.1), yy, 0.0, h=0.9)
    # arch under: solid spandrel with an arch opening (one prism in the YZ plane, inside the deck by 2 cm)
    poly = [(-L/2, -T+0.02), (-L/2, -T-2.4), (-L/2+0.6, -T-2.4)]
    for i in range(1, 24):
        a = math.pi*i/24; poly.append((-(L/2-0.6)*math.cos(a), -T-2.4+1.8*math.sin(a)))
    poly += [(L/2-0.6, -T-2.4), (L/2, -T-2.4), (L/2, -T+0.02)]
    ob = prism(poly, -W/2+0.3, W/2-0.3, 'Cream')
    for v in ob.data.vertices: a_, b_, c_ = v.co; v.co = Vector((c_, a_, b_))
