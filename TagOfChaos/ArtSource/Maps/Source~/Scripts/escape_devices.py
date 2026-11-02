# Escape-mode cookie escape devices (exec'd by escape_assets.py). EscapeVisualPlan.md §5.
import math, random

# =====================================================================================
# ESCAPE DEVICES (footprint within r 3.2; Slot_nn = where parts are shown)
# =====================================================================================
def ring_slots(u, n, r, z, start=0):
    for i in range(n):
        a = -math.pi / 2 + 2 * math.pi * i / max(1, n)
        empty(u, f'Slot_{start + i:02d}', (r * math.cos(a), r * math.sin(a), z))


def frustum(g, mat, x0, x1, y0, y1, h, slope):
    """Flat deck x0..x1, y0..y1 with all four sides sloped outward by `slope` (walkable ramp edges)."""
    bm = g.bm
    lo = [bm.verts.new(p) for p in ((x0 - slope, y0 - slope, 0), (x1 + slope, y0 - slope, 0),
                                    (x1 + slope, y1 + slope, 0), (x0 - slope, y1 + slope, 0))]
    hi = [bm.verts.new(p) for p in ((x0, y0, h), (x1, y0, h), (x1, y1, h), (x0, y1, h))]
    mi = g.mi(mat)
    for f in [hi, list(reversed(lo))] + [(lo[k], lo[(k + 1) % 4], hi[(k + 1) % 4], hi[k]) for k in range(4)]:
        bm.faces.new(f).material_index = mi


# ---------------- CursedCandyCarnival: roller coaster that runs out of the map (EscapeVisualPlan.md §5.2) ----------------
# Travel direction = -Y (Unity +Z): EscapeMapSetup points it at the nearest map edge. Station rails sit at z 0 (low enough
# to step over), the lift hill and the run beyond the wall are visual only (no colliders). Unity moves the cars along the
# Path_nn empties (rail top, root space). Boarding deck on +X.
COASTER_RAIL = 0.14                       # rail top height = car origin height
COASTER_CARS = (-2.5, 0.1, 2.7)           # car centres (y) at rest, lead car first
COASTER_SEAT_ROWS = (-0.45, 0.55)


def coaster_height(y):
    """Track height (rail base) at y: flat station -> lift hill -> crest -> drop -> run-out."""
    def ease(t):
        t = min(1.0, max(0.0, t))
        return t * t * (3 - 2 * t)
    if y > -9:
        return 0.0
    if y > -37:
        return 14.0 * ease((-9 - y) / 28)
    if y > -55:
        return 14.0 + 0.6 * math.sin(math.pi * (-37 - y) / 18)
    if y > -70:
        return 14.0 - 10.0 * ease((-55 - y) / 15)
    return 4.0


def device_carnival():
    u = 'ESC_CursedCandyCarnival'
    ys = [6.0 - k * 0.5 for k in range(int((6.0 + 125.0) / 0.5) + 1)]           # +6 (behind the tail) .. -125

    def body(g):
        frustum(g, 'ME_Gray_Dark', 1.3, 4.1, -4.4, 4.6, 0.3, 0.9)                 # boarding deck, ramped on all sides
        for k in range(9):
            g.rbox('ME_Gold', (2.7, -4.0 + k, 0.3), (2.6, 0.08, 0.02), bevel=0.0)  # deck planks
        for x in (-0.6, 0.6):                                                     # station rails (low: walk over them)
            g.rbox('ME_Gray_Light', (x, -1.5, 0.0), (0.16, 15.0, COASTER_RAIL), bevel=0.03)
        for k in range(11):
            g.rbox('ME_Cookie_Dark', (0, 5.5 - k * 1.5, 0.0), (1.7, 0.3, 0.07), bevel=0.02)
        for x in (-2.3, 4.6):                                                     # entrance arch over the lead car
            cyl(g, 'ME_Purple_Deep', (x, -4.7, 0), 0.28, 5.0, seg=12)
            g.blob('ME_Gold', (x, -4.7, 5.1), 0.42, seg=12)
        g.rbox('ME_Purple_Deep', (1.15, -4.7, 4.5), (7.4, 0.5, 0.8), bevel=0.2)
        g.rbox('ME_Teal', (1.15, -4.98, 4.65), (5.0, 0.08, 0.5), bevel=0.04)      # sign board
        for k in range(3):                                                        # three candy stars on the sign
            star_prism(g, 'ME_Orange_Warm', (-0.35 + k * 1.5, -5.05, 4.9), 0.32, 0.14, 0.08)
    mesh_obj(u, 'Body', body)

    def bulbs_off(g):                                                             # unlit sockets (always visible)
        for p in BULBS:
            g.blob('ME_Gray_Light', p, 0.15, seg=8)
    BULBS = [(-2.3 + 6.9 * k / 9, -4.98, 5.3) for k in range(10)] + \
            [(x, -4.98, z) for z in (1.5, 2.6, 3.7) for x in (-2.3, 4.6)]
    mesh_obj(u, 'Bulbs_Off', bulbs_off)
    for i, p in enumerate(BULBS):                                                 # lit bulbs, switched on in order by Unity
        mesh_obj(u, f'Bulb_{i:02d}', lambda g: g.blob('ME_Glow_Orange', (0, 0, 0), 0.2, seg=10), loc=p)

    def track(g):                                                                 # lift hill + run-out (visual only)
        pts = [(0, y, coaster_height(y)) for y in ys if y <= -8.5]
        for x in (-0.6, 0.6):
            g.tube([(x, p[1], p[2] + COASTER_RAIL - 0.06) for p in pts[::2]], 0.08, 'ME_Gray_Light', seg=6, cap=False)
        g.tube([(0, p[1], p[2] - 0.2) for p in pts[::2]], 0.16, 'ME_Purple_Deep', seg=8, cap=False)
        for y in [p for p in ys if p <= -9][::3]:
            z = coaster_height(y)
            g.rbox('ME_Cookie_Dark', (0, y, z - 0.07), (1.6, 0.25, 0.07), bevel=0.02)
        for y in range(-12, -125, -6):                                            # support columns with a T head
            z = coaster_height(y)
            if z < 1.2:
                continue
            cyl(g, 'ME_Purple_Deep', (0, y, -3.0), 0.22, z + 2.7, seg=10)
            g.rbox('ME_Purple_Deep', (0, y, z - 0.45), (1.6, 0.35, 0.25), bevel=0.06)
    mesh_obj(u, 'Track', track)

    for k in range(0, len(ys), 6):                                                # path for the cars (rail top)
        empty(u, f'Path_{k // 6:02d}', (0, ys[k], coaster_height(ys[k]) + COASTER_RAIL))

    slot = 0
    for c, cy in enumerate(COASTER_CARS):
        lead = c == 0
        car = None

        def car_body(g, lead=lead):
            g.rbox('ME_Gray_Dark', (0, 0, 0.0), (1.5, 2.1, 0.3), bevel=0.08)               # chassis
            for x in (-0.68, 0.68):
                for y in (-0.7, 0.7):
                    cyl(g, 'ME_Gray_Light', (x, y, 0.2), 0.2, 0.16, seg=12, rot=(0, math.pi / 2, 0))
            g.rbox('ME_Purple_Deep', (0, 0, 0.3), (1.9, 2.4, 0.85), bevel=0.28)            # tub
            g.rbox('ME_Orange_Warm', (0, 0, 0.78), (1.96, 2.46, 0.16), bevel=0.06)        # stripe
            for row in COASTER_SEAT_ROWS:
                for x in (-0.42, 0.42):
                    g.rbox('ME_Orange_Warm', (x, row, 1.1), (0.68, 0.55, 0.18), bevel=0.07)   # cushion
                    g.rbox('ME_Cookie_Gold', (x, row + 0.3, 1.1), (0.68, 0.16, 0.75), bevel=0.07)  # backrest
            if lead:                                                                      # round nose + dashboard
                g.blob('ME_Purple_Deep', (0, -1.25, 0.75), 0.75, sz=(1.25, 0.55, 0.7), seg=16)
                for x in (-0.55, 0.55):
                    g.blob('ME_Glow_Teal', (x, -1.6, 0.85), 0.17, seg=10)
                g.rbox('ME_Gray_Dark', (0, -0.98, 1.1), (1.5, 0.22, 0.45), bevel=0.08)
                g.blob('ME_Gold', (0, -1.55, 1.2), 0.22, sz=(1, 0.6, 1), seg=10)          # gold emblem
            else:
                g.blob('ME_Gold', (0, -1.25, 0.6), 0.14, seg=8)                           # coupling
        car = mesh_obj(u, f'Car_{c}', car_body, loc=(0, cy, COASTER_RAIL))

        def lap_bar(g):     # origin = hinge on top of the backrests; Unity raises it until the train leaves
            g.tube([(-0.85, 0, 0), (-0.85, -0.5, -0.3), (0.85, -0.5, -0.3), (0.85, 0, 0)], 0.06, 'ME_Teal', seg=8)
        for r, row in enumerate(COASTER_SEAT_ROWS):
            mesh_obj(u, f'LapBar_{r}', lap_bar, loc=(0, row + 0.3, 1.85), parent=car)
            for s, x in enumerate((-0.42, 0.42)):
                empty(u, f'Seat_{c * 4 + r * 2 + s:02d}', (x, row, 1.28), parent=car)
        if lead:                                                                          # button on the dash, belt on the side
            empty(u, f'Slot_{slot:02d}', (0.45, -0.98, 1.72), parent=car); slot += 1
            empty(u, f'Slot_{slot:02d}', (1.15, -0.55, 0.9), parent=car); slot += 1
    for c in range(3):                                                                    # one battery bay per car (deck side)
        empty(u, f'Slot_{slot:02d}', (1.15, 0.45, 0.6), parent=bpy.data.objects[f'Car_{c}']); slot += 1
    for y in (-6.0, -7.3, -8.6):                                                          # broken rail spots to repair
        empty(u, f'Slot_{slot:02d}', (0.0, y, 0.15)); slot += 1

    def broken(g):                                                                        # cracked ties + warning cones
        for k, y in enumerate((-6.0, -7.3, -8.6)):
            g.rbox('ME_Orange_Warm', (1.05, y, 0.0), (0.3, 0.3, 0.05), bevel=0.02)
            cyl(g, 'ME_Orange_Warm', (1.05, y, 0.05), 0.12, 0.55, seg=10, r2=0.02)
    mesh_obj(u, 'Repair_Marks', broken)
    empty(u, 'Board', (2.7, 0.1, 0.3))


# ---------------- HauntedBakery: the magic oven landmark becomes the escape machine (EscapeVisualPlan.md §5.3) ----------------
# Built in the oven's own frame (build_hauntedbakery.py _oven: stone base 46 x 24 x 3 m, front face y -12, mouth centre
# (0, -9.8, 10), round door hinge plane y -11). Unity places the root on HAU_Landmark_MagicOven. The map door rolls aside
# (Unity +X = here -X), so the machine sits on the other half of the base ledge (+X) and the ramp climbs to the mouth.
OVEN_FRONT = -12.0
OVEN_LEDGE = 3.0
OVEN_RAMP = 12.0                      # ramp run (rise OVEN_LEDGE): 1:4 keeps the checker monster capsule off the slope


def device_bakery():
    u = 'ESC_HauntedBakery'

    def ramp(g):     # 5 m wide, 3 m rise over 12 m (1:4), then a 1.6 m flat landing in front of the door
        bm = g.bm
        y0, y1, y2 = OVEN_FRONT - OVEN_RAMP - 1.6, OVEN_FRONT - 1.6, OVEN_FRONT
        prof = [(y0, 0), (y2, 0), (y2, OVEN_LEDGE), (y1, OVEN_LEDGE)]                   # side profile (y, z)
        L = [bm.verts.new((-2.5, y, z)) for y, z in prof]
        R = [bm.verts.new((2.5, y, z)) for y, z in prof]
        mi = g.mi('ME_Cookie_Gold')
        bm.faces.new(list(reversed(L))).material_index = mi
        bm.faces.new(R).material_index = mi
        for k in range(4):
            bm.faces.new((L[k], L[(k + 1) % 4], R[(k + 1) % 4], R[k])).material_index = mi
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

    def treads(g):   # chocolate strips lying on the slope + gold landing mat (decor only: keeps the walkable slope smooth)
        y0 = OVEN_FRONT - OVEN_RAMP - 1.6
        tilt = math.atan2(OVEN_LEDGE, OVEN_RAMP)
        for k in range(1, 9):
            t = k / 9
            g.rbox('ME_Cookie_Dark', (0, y0 + OVEN_RAMP * t, OVEN_LEDGE * t - 0.02), (4.6, 0.25, 0.04), bevel=0.0, rot=(tilt, 0, 0))
        g.rbox('ME_Gold', (0, OVEN_FRONT - 0.8, OVEN_LEDGE - 0.01), (4.6, 1.2, 0.03), bevel=0.0)
    mesh_obj(u, 'Ramp_Treads', treads)

    mesh_obj(u, 'Ramp', ramp)

    def body(g):
        # machine housing on the +X half of the ledge (blocks the narrow ledge so no cookie-only strip is left)
        g.rbox('ME_Purple_Deep', (12.75, -10.85, OVEN_LEDGE), (20.5, 2.9, 5.5), bevel=0.45)
        g.rbox('ME_Gold', (12.75, -12.33, OVEN_LEDGE + 5.0), (20.6, 0.12, 0.35), bevel=0.05)       # gold trim
        g.rbox('ME_Gray_Dark', (-12.75, -9.9, OVEN_LEDGE), (20.5, 1.0, 5.0), bevel=0.2)            # door track (-X half)
        for x, z in ((8.0, 6.0), (12.2, 5.1)):                                                     # gear axles
            cyl(g, 'ME_Gray_Dark', (x, -12.2, z), 0.45, 0.5, seg=14, rot=(math.pi / 2, 0, 0))
        cyl(g, 'ME_Gray_Light', (18.0, -10.8, OVEN_LEDGE + 5.5), 1.0, 2.2, seg=18)                 # piston cylinder
        torus(g, 'ME_Gold', (18.0, -10.8, OVEN_LEDGE + 7.6), 1.0, 0.14, seg=18)
        cyl(g, 'ME_Gray_Dark', (5.0, -10.8, OVEN_LEDGE + 5.5), 0.75, 4.2, seg=16, r2=0.6)          # machine chimney
        g.lathe([(0.6, 0), (0.95, 0.4), (0.9, 0.7), (0, 0.7)], 'ME_Purple_Deep', seg=16, loc=(5.0, -10.8, OVEN_LEDGE + 9.7))
        g.lathe([(0, -0.15), (1.0, -0.15), (1.0, 0.15), (0, 0.15)], 'ME_Cream', seg=24, loc=(20.5, -12.35, 6.5),
                rot=(math.pi / 2, 0, 0))                                                           # gauge face
        torus(g, 'ME_Gold', (20.5, -12.5, 6.5), 1.0, 0.12, seg=24, rot=(math.pi / 2, 0, 0))
        # floor-level parts on the stone base front: button console, gear seat, battery bays, leaky pipe
        g.rbox('ME_Gray_Dark', (4.3, -12.35, 0.0), (1.5, 0.7, 1.9), bevel=0.15)
        g.rbox('ME_Gray_Dark', (6.6, -12.2, 0.9), (1.0, 0.4, 1.0), bevel=0.1)
        for x in (9.0, 11.0, 13.0):
            g.rbox('ME_Gray_Dark', (x, -12.2, 0.6), (1.2, 0.45, 1.5), bevel=0.12)
            g.rbox('ME_Teal', (x, -12.45, 0.65), (0.9, 0.1, 1.2), bevel=0.05)
        g.tube([(14.8, -12.45, 1.5), (23.0, -12.45, 1.5)], 0.32, 'ME_Gray_Light', seg=12)   # below the camera line
        for x in (16.0, 18.5, 21.0):
            torus(g, 'ME_Orange_Warm', (x, -12.45, 1.5), 0.36, 0.1, seg=14, rot=(0, math.pi / 2, 0))
    mesh_obj(u, 'Body', body)

    def gear(R, teeth):
        def b(g):
            disc_gear(g, 'ME_Gold', R, 0.5, teeth, rot=(math.pi / 2, 0, 0), tooth=(0.55, 0.5))
            g.blob('ME_Glow_Teal', (0, -0.3, 0), R * 0.28, sz=(1, 0.5, 1), seg=12)
        return b
    mesh_obj(u, 'Gear_A', gear(2.0, 12), loc=(8.0, -12.55, 6.0))
    mesh_obj(u, 'Gear_B', gear(1.35, 8), loc=(12.2, -12.55, 5.1))
    mesh_obj(u, 'Piston_Rod', lambda g: (cyl(g, 'ME_Gray_Light', (0, 0, -1.5), 0.45, 3.0, seg=12),
                                         g.blob('ME_Red_Button', (0, 0, 1.6), 0.7, sz=(1, 1, 0.6), seg=14)),
             loc=(18.0, -10.8, OVEN_LEDGE + 7.6))
    mesh_obj(u, 'Gauge_Needle', lambda g: g.rbox('ME_Red_Button', (0, 0, -0.08), (0.12, 0.08, 0.85), bevel=0.03),
             loc=(20.5, -12.55, 6.5))

    def oven_light(g):    # white glow that fills the mouth once the door is open
        g.lathe([(0, 0.0), (7.4, 0.0), (6.8, 0.25), (0, 0.4)], 'ME_Glow_White', seg=40, rot=(math.pi / 2, 0, 0))
    mesh_obj(u, 'Oven_Light', oven_light, loc=(0, -10.05, 10.0))
    empty(u, 'Smoke', (5.0, -10.8, OVEN_LEDGE + 10.6))
    empty(u, 'Mouth', (0, -10.4, 7.5))
    empty(u, 'Board', (0, OVEN_FRONT - 0.9, OVEN_LEDGE))
    slots = [(4.3, -12.75, 2.05), (6.6, -12.5, 1.4), (9.0, -12.55, 1.25), (11.0, -12.55, 1.25), (13.0, -12.55, 1.25),
             (16.0, -12.85, 1.5), (18.5, -12.85, 1.5), (21.0, -12.85, 1.5)]
    for i, p in enumerate(slots):
        empty(u, f'Slot_{i:02d}', p)


# ---------------- ChocolateFactory: chocolate steam train that leaves the map (EscapeVisualPlan.md §5.4) ----------------
# Travel direction = -Y (Unity +Z): EscapeMapSetup points it at the nearest map edge, like the roller coaster. Rails are
# flat; the station rails have colliders (low enough to step over), the rails beyond are visual only. Platform on +X.
# Slots (recipe order): macaron wheel x2 (front axle), cookie wheel x2 (rear axle), chocolate oil x2 (tanks on the
# boiler), gear x2 (side gearboxes). Wheel/gear anchors are turned so the part's axle points sideways; Unity spins them.
TRAIN_RAIL = 0.14
TRAIN_LOCO_Y, TRAIN_COACH_Y = -2.6, 3.4


def device_factory():
    u = 'ESC_ChocolateFactory'

    def body(g):
        frustum(g, 'ME_Gray_Dark', 1.6, 4.6, -6.0, 7.0, 0.3, 0.9)                       # platform, ramped edges
        for k in range(13):
            g.rbox('ME_Cookie_Gold', (3.1, -5.5 + k, 0.3), (2.8, 0.08, 0.02), bevel=0.0)
        for x in (-0.6, 0.6):                                                           # station rails (low)
            g.rbox('ME_Gray_Light', (x, 0.0, 0.0), (0.16, 20.0, TRAIN_RAIL), bevel=0.03)
        for k in range(14):
            g.rbox('ME_Cookie_Dark', (0, 9.5 - k * 1.5, 0.0), (1.7, 0.3, 0.07), bevel=0.02)
        for y in (-4.0, 5.5):                                                           # canopy posts (outer edge)
            cyl(g, 'ME_Purple_Deep', (4.3, y, 0.3), 0.22, 4.0, seg=12)
        g.rbox('ME_Teal', (3.1, 0.75, 4.2), (3.6, 11.5, 0.3), bevel=0.12)              # canopy roof (4.2 m up)
        g.rbox('ME_Gold', (3.1, 0.75, 4.15), (3.7, 11.6, 0.08), bevel=0.03)
        g.rbox('ME_Purple_Deep', (4.75, 0.75, 3.6), (0.15, 3.6, 0.6), bevel=0.05)     # station sign
        for k in range(3):
            star_prism(g, 'ME_Orange_Warm', (4.85, -0.45 + k * 1.2, 3.9), 0.22, 0.1, 0.08, rot=(0, 0, math.pi / 2))
    mesh_obj(u, 'Body', body)

    def track(g):    # rails from the station to the tunnel: 20 m, Unity stretches it to the tunnel distance
        for x in (-0.6, 0.6):
            g.rbox('ME_Gray_Light', (x, -10.0, 0.0), (0.16, 20.0, TRAIN_RAIL), bevel=0.03)
        for k in range(14):
            g.rbox('ME_Cookie_Dark', (0, -0.7 - k * 1.4, 0.0), (1.7, 0.3, 0.07), bevel=0.02)
    mesh_obj(u, 'Track', track, loc=(0, -10.0, 0))

    def tunnel(g):   # chocolate-rock tunnel the train disappears into (origin = mouth centre on the ground, runs to -Y)
        for x in (-2.6, 2.6):                                                           # side walls
            g.rbox('ME_Cookie_Dark', (x, -5.0, 0), (1.2, 10.0, 4.4), bevel=0.25)
        g.rbox('ME_Cookie_Dark', (0, -5.0, 4.2), (6.4, 10.0, 1.4), bevel=0.4)           # roof
        g.rbox('ME_Cookie_Dark', (0, -9.6, 0), (4.2, 0.8, 4.4), bevel=0.1)              # closed back
        for x in (-1.98, 1.98):                                                         # dark lining inside
            g.rbox('ME_Tunnel_Dark', (x, -5.0, 0), (0.04, 9.6, 4.2), bevel=0.0)
        g.rbox('ME_Tunnel_Dark', (0, -5.0, 4.18), (3.96, 9.6, 0.04), bevel=0.0)
        g.rbox('ME_Tunnel_Dark', (0, -9.18, 0), (3.96, 0.04, 4.2), bevel=0.0)
        g.tube([(-2.4, 0.15, 0), (-2.4, 0.15, 3.6)] + [(2.4 * math.cos(math.pi - t * math.pi / 8), 0.15,
               3.6 + 1.3 * math.sin(t * math.pi / 8)) for t in range(1, 8)] + [(2.4, 0.15, 3.6), (2.4, 0.15, 0)],
               0.35, 'ME_Purple_Deep', seg=10)                                          # portal arch
        g.blob('ME_Gold', (0, 0.3, 5.0), 0.45, sz=(1, 0.5, 1), seg=12)                  # keystone
        for x in (-2.4, 2.4):
            g.blob('ME_Glow_Orange', (x, 0.5, 3.0), 0.25, seg=10)                       # lanterns
    mesh_obj(u, 'Tunnel', tunnel, loc=(0, -30.0, 0))

    def loco(g):     # local: origin at rail top under the middle, front = -Y
        g.rbox('ME_Gray_Dark', (0, 0, 0.12), (1.6, 5.4, 0.35), bevel=0.1)                  # chassis
        cyl(g, 'ME_Cookie_Dark', (0, -3.0, 1.55), 0.95, 3.3, seg=24, rot=(-math.pi / 2, 0, 0))  # chocolate boiler
        for y in (-2.6, -1.6, -0.6):
            torus(g, 'ME_Gold', (0, y, 1.55), 0.97, 0.07, seg=24, rot=(math.pi / 2, 0, 0))
        cyl(g, 'ME_Gray_Dark', (0, -3.0, 1.55), 0.85, 0.25, seg=24, rot=(math.pi / 2, 0, 0))   # smokebox face
        g.blob('ME_Glow_Orange', (0, -3.28, 1.75), 0.32, sz=(1, 0.5, 1), seg=14)                # headlamp
        cyl(g, 'ME_Gray_Dark', (0, -2.3, 2.3), 0.32, 1.1, seg=14, r2=0.55)                     # chimney
        torus(g, 'ME_Gold', (0, -2.3, 3.4), 0.55, 0.08, seg=16)
        g.blob('ME_Gold', (0, -1.1, 2.45), 0.42, sz=(1, 1, 0.9), seg=14)                       # steam dome
        for x in (-1.1, 1.1):                    # oil tank shelves on the boiler sides (Request1009Plan.md §22.6 M2 — was on the roof, out of reach)
            g.rbox('ME_Gray_Dark', (x, -2.1, 1.05), (0.5, 0.56, 0.16), bevel=0.05)
        g.rbox('ME_Purple_Deep', (0, 1.55, 0.45), (1.9, 2.0, 2.3), bevel=0.2)                  # cab
        g.rbox('ME_Teal', (0, 1.55, 2.75), (2.2, 2.3, 0.25), bevel=0.1)
        for x in (-0.96, 0.96):
            g.rbox('ME_Glow_Orange', (x, 1.55, 1.55), (0.05, 0.9, 0.7), bevel=0.02)               # lit cab windows
        bm = g.bm                                                                              # cow catcher wedge
        v = [bm.verts.new(p) for p in ((-0.8, -3.0, 0.05), (0.8, -3.0, 0.05), (0, -3.9, 0.05),
                                       (-0.8, -3.0, 0.75), (0.8, -3.0, 0.75))]
        mi = g.mi('ME_Gold')
        for f in ((v[0], v[2], v[1]), (v[3], v[4], v[2]), (v[0], v[1], v[4], v[3]), (v[1], v[2], v[4]), (v[0], v[3], v[2])):
            bm.faces.new(f).material_index = mi
        for y in (-1.9, -0.2):                                                                 # wheel sockets (dark hubs)
            for x in (-0.82, 0.82):
                cyl(g, 'ME_Gray_Dark', (x, y, 0.32), 0.14, 0.1, seg=10, rot=(0, math.pi / 2 * (1 if x > 0 else -1), 0))
    lo = mesh_obj(u, 'Loco', loco, loc=(0, TRAIN_LOCO_Y, TRAIN_RAIL))
    turn = (0, 0, math.pi / 2)                                                                 # part axle -> sideways
    for i, (x, y) in enumerate(((0.98, -1.9), (-0.98, -1.9), (0.98, -0.2), (-0.98, -0.2))):    # wheels
        empty(u, f'Slot_{i:02d}', (x, y, 0.3), parent=lo, rot=turn)
    for i, x in enumerate((1.2, -1.2)):                                                        # oil tanks (side shelves, ~1.3 m above the platform)
        empty(u, f'Slot_{4 + i:02d}', (x, -2.1, 1.5), parent=lo)
    for i, x in enumerate((1.05, -1.05)):                                                      # gearboxes
        empty(u, f'Slot_{6 + i:02d}', (x, -1.05, 0.95), parent=lo, rot=turn)
    empty(u, 'Smoke', (0, -2.3, 3.55), parent=lo)
    mesh_obj(u, 'Whistle', lambda g: g.blob('ME_Glow_White', (0, 0, 0), 0.22, sz=(1, 1, 1.4), seg=12),
             loc=(0, 0.75, 3.15), parent=lo)

    def coach(g):    # open-window coach: walls are built around the windows so seated cookies show through
        g.rbox('ME_Gray_Dark', (0, 0, 0.1), (1.7, 4.6, 0.4), bevel=0.1)                        # underframe
        g.rbox('ME_Purple_Deep', (0, 0, 0.5), (2.0, 4.8, 0.7), bevel=0.12)                     # lower body + floor
        for x in (-0.95, 0.95):
            for y in (-2.25, -0.75, 0.75, 2.25):                                               # window pillars
                g.rbox('ME_Purple_Deep', (x, y, 1.2), (0.12, 0.3, 0.75), bevel=0.04)
        g.rbox('ME_Purple_Deep', (0, 0, 1.95), (2.0, 4.8, 0.45), bevel=0.1)                    # upper band
        for y in (-2.38, 2.38):
            g.rbox('ME_Purple_Deep', (0, y, 1.2), (2.0, 0.08, 0.75), bevel=0.02)               # end walls
        g.rbox('ME_Cookie_Gold', (0, 0, 2.4), (2.25, 5.1, 0.3), bevel=0.14)                    # roof
        g.rbox('ME_Orange_Warm', (0, 0, 1.12), (2.04, 4.84, 0.1), bevel=0.04)                  # stripe
        for y in (-1.6, 1.6):
            for x in (-0.82, 0.82):
                cyl(g, 'ME_Gray_Light', (x, y, 0.3), 0.3, 0.14, seg=14, rot=(0, math.pi / 2 * (1 if x > 0 else -1), 0))
        g.blob('ME_Gold', (0, -2.6, 0.45), 0.16, seg=8)                                        # coupling
    co = mesh_obj(u, 'Coach', coach, loc=(0, TRAIN_COACH_Y, TRAIN_RAIL))
    for k in range(6):                                                                         # seats seen through the windows
        empty(u, f'Seat_{k:02d}', ((-0.45, 0.45)[k % 2], -1.5 + (k // 2) * 1.5, 0.85), parent=co)
    empty(u, 'Board', (2.4, TRAIN_COACH_Y, 0.3))


# ---------------- GingerbreadVillage: clock tower -> spiral ramp -> underground stone ruins -> rune pedestals + altar portal ----------------
# (EscapeVisualPlan.md §5.5) Built in the clock tower's frame (origin = tower centre on the ground, door on -Y).
# Unity places the root on GIN_Landmark_ClockTower after the map is compacted and cuts the ground under the tower.
#   tower room: round (CornerFill), door threshold + small landing at z 0
#   spiral ramp: r 0.6..5, -90 deg (door) -> 720 deg, 10 m down (pitch 4.4 m per turn, 1:4 on the walking line)
#   tunnel east (+X) 5 m wide, 6.5 m high -> hall 40 x 40 m, floor -10, ceiling -1 (stays under the ground)
GIN_DEPTH = 10.0
GIN_R0, GIN_R1 = 0.6, 5.0
GIN_T0, GIN_T1 = -90.0, 720.0
GIN_HALL = (14.0, 54.0, -20.0, 20.0)       # x0, x1, y0, y1
GIN_ALTAR = (34.0, 0.0)
GIN_FLOOR_LIFT = 0.06                     # tower room floor above the ground plane


def gin_z(t):
    return -(t - GIN_T0) / (GIN_T1 - GIN_T0) * GIN_DEPTH


def helix(g, mat, t0, t1, ztop, zbot, r0=GIN_R0, r1=GIN_R1, step=4.0):
    """Ramp sector between angles t0..t1 (deg): top surface ztop(t), underside zbot(t)."""
    bm = g.bm
    n = max(1, int(math.ceil((t1 - t0) / step)))
    rows = []
    for k in range(n + 1):
        t = t0 + (t1 - t0) * k / n
        a = math.radians(t)
        c, si = math.cos(a), math.sin(a)
        rows.append([bm.verts.new((r * c, r * si, z)) for r, z in ((r0, ztop(t)), (r1, ztop(t)), (r1, zbot(t)), (r0, zbot(t)))])
    mi = g.mi(mat)
    for A, B in zip(rows[:-1], rows[1:]):
        for i in range(4):
            bm.faces.new((A[i], B[i], B[(i + 1) % 4], A[(i + 1) % 4])).material_index = mi
    bm.faces.new(list(reversed(rows[0]))).material_index = mi
    bm.faces.new(rows[-1]).material_index = mi


def ring_wall(g, mat, t0, t1, z0, z1, r0, r1, step=6.0):
    helix(g, mat, t0, t1, lambda t: z1, lambda t: z0, r0, r1, step)


def slab(g, mat, x0, x1, y0, y1, z0, z1):
    g.rbox(mat, ((x0 + x1) / 2, (y0 + y1) / 2, z0), (x1 - x0, y1 - y0, z1 - z0), bevel=0.0)


def wedge_ring(g, mat, t0, t1, z0, z1, outer, step=5.0):
    """Prisms from the r 5 circle out to outer(t) (distance along the ray) for t0..t1."""
    bm = g.bm
    mi = g.mi(mat)
    t = t0
    while t < t1 - 1e-6:
        ta, tb = t, min(t1, t + step)
        pts = []
        for tt, r in ((ta, GIN_R1), (tb, GIN_R1), (tb, outer(tb)), (ta, outer(ta))):
            a = math.radians(tt)
            pts.append((r * math.cos(a), r * math.sin(a)))
        if outer(ta) - GIN_R1 > 0.02 or outer(tb) - GIN_R1 > 0.02:
            lo = [bm.verts.new((x, y, z0)) for x, y in pts]
            hi = [bm.verts.new((x, y, z1)) for x, y in pts]
            bm.faces.new(list(reversed(lo))).material_index = mi
            bm.faces.new(hi).material_index = mi
            for i in range(4):
                bm.faces.new((lo[i], lo[(i + 1) % 4], hi[(i + 1) % 4], hi[i])).material_index = mi
        t = tb


def cobweb(g, corner, dirx, diry, z, size):
    cx, cy = corner
    spokes = [(cx + dirx * size * math.cos(a), cy + diry * size * math.sin(a)) for a in [i * math.pi / 8 for i in range(5)]]
    for x, y in spokes:
        g.tube([(cx, cy, z), (x, y, z - size * 0.35)], 0.04, 'ME_Cream', seg=4)
    for f in (0.35, 0.65, 0.95):
        g.tube([(cx + (x - cx) * f, cy + (y - cy) * f, z - size * 0.35 * f) for x, y in spokes], 0.035, 'ME_Cream', seg=4, cap=False)


def device_gingerbread():
    u = 'ESC_GingerbreadVillage'
    hx0, hx1, hy0, hy1 = GIN_HALL
    ax, ay = GIN_ALTAR
    floor = -GIN_DEPTH

    def body(g):
        # ---- tower room + spiral ramp ----
        sq = lambda t: GIN_R1 / max(abs(math.cos(math.radians(t))), abs(math.sin(math.radians(t))))
        wedge_ring(g, 'ME_Cookie_Dark', -58.7, 238.7, -0.6, 9.6, sq)                          # round room
        # door threshold + landing sit 6 cm above the ground so they never share a plane with the terrain (z-fighting)
        wedge_ring(g, 'ME_Cookie_Gold', -121.3, -58.7, -0.3, GIN_FLOOR_LIFT,
                   lambda t: 6.4 / max(0.2, abs(math.sin(math.radians(t)))))                    # door threshold
        helix(g, 'ME_Cookie_Gold', -115.0, GIN_T0, lambda t: GIN_FLOOR_LIFT, lambda t: -0.3)    # landing
        helix(g, 'ME_Cookie_Gold', GIN_T0, 360.0, gin_z, lambda t: gin_z(t) - 0.3)             # upper turn (thin)
        helix(g, 'ME_Cookie_Gold', 360.0, GIN_T1, gin_z, lambda t: floor - 0.3)                # lower turn (solid)
        helix(g, 'ME_Cookie_Gold', GIN_T1, GIN_T1 + 30, lambda t: floor, lambda t: floor - 0.3)  # bottom landing
        cyl(g, 'ME_Cookie_Gold', (0, 0, floor - 0.3), GIN_R0, GIN_DEPTH + 9.9, seg=16)            # centre column
        ring_wall(g, 'ME_Gray_Dark', 31.3, 328.7, floor - 0.3, 0.0, GIN_R1, GIN_R1 + 0.6)        # shaft wall
        ring_wall(g, 'ME_Gray_Dark', -31.3, 31.3, -3.5, 0.0, GIN_R1, GIN_R1 + 0.6)               # over the tunnel
        g.lathe([(0, floor - 0.3), (GIN_R1 + 0.6, floor - 0.3), (GIN_R1 + 0.6, floor - 0.25), (0, floor - 0.25)],
                'ME_Gray_Dark', seg=32)                                                            # shaft floor
        # ---- tunnel ----
        slab(g, 'ME_Gray_Dark', 4.0, hx0 + 0.5, -2.6, 2.6, floor - 0.3, floor)
        for y0, y1 in ((-3.2, -2.6), (2.6, 3.2)):
            slab(g, 'ME_Gray_Dark', 4.8, hx0, y0, y1, floor, -3.5)
        slab(g, 'ME_Gray_Dark', 4.8, hx0, -3.2, 3.2, -3.5, -3.0)
        # ---- hall: broken stone ruins of an old cookie village (2026-10-03, Request1003Plan.md §2) ----
        slab(g, 'ME_Stone_Dark', hx0, hx1, hy0, hy1, floor - 0.3, floor)
        slab(g, 'ME_Stone_Dark', hx0 - 0.6, hx0, hy0 - 0.6, -2.6, floor, -1.0)
        slab(g, 'ME_Stone_Dark', hx0 - 0.6, hx0, 2.6, hy1 + 0.6, floor, -1.0)
        slab(g, 'ME_Stone_Dark', hx0 - 0.6, hx0, -2.6, 2.6, -3.5, -1.0)
        slab(g, 'ME_Stone_Dark', hx1, hx1 + 0.6, hy0 - 0.6, hy1 + 0.6, floor, -1.0)
        for y0, y1 in ((hy0 - 0.6, hy0), (hy1, hy1 + 0.6)):
            slab(g, 'ME_Stone_Dark', hx0, hx1, y0, y1, floor, -1.0)
        slab(g, 'ME_Stone_Dark', hx0 - 0.6, hx1 + 0.6, hy0 - 0.6, hy1 + 0.6, -1.0, -0.55)
        # stone columns (one broken off at 3.2 m)
        for k, (x, y) in enumerate(GIN_COLUMNS):
            cyl(g, 'ME_Stone_Light', (x, y, floor), 1.25, 0.5, seg=10)                               # plinth
            cyl(g, 'ME_Stone_Light', (x, y, floor), 0.9, 3.2 if k == 3 else 8.5, seg=12)
            if k != 3:
                cyl(g, 'ME_Stone_Light', (x, y, floor + 8.3), 1.2, 0.7, seg=10)                       # capital
        # rune altar: walkable stone mound (1:3) with a stout rune pillar
        g.lathe([(0, floor), (5.5, floor), (2.6, floor + 0.95), (0, floor + 0.95)], 'ME_Stone_Light', seg=32,
                loc=(ax, ay, 0))
        g.lathe([(0, floor + 0.95), (0.6, floor + 0.95), (0.45, floor + 2.3), (0.65, floor + 2.5), (0, floor + 2.6)],
                'ME_Stone_Dark', seg=16, loc=(ax, ay, 0))
        # five separate rune pedestals round the altar (one rune each — Slot_00..04)
        for i, (px, py, a) in enumerate(gin_pedestals()):
            g.rbox('ME_Stone_Light', (px, py, floor), (1.5, 1.5, 0.3), bevel=0.08, rotz=a)
            g.rbox('ME_Stone_Dark', (px, py, floor + 0.3), (0.95, 0.95, 1.05), bevel=0.06, rotz=a)
            g.rbox('ME_Stone_Light', (px, py, floor + 1.35), (1.3, 1.3, 0.22), bevel=0.06, rotz=a)
        # collapsed stone houses (solid cores = colliders, like the old cookie houses: nothing to hide inside)
        rnd = random.Random(12)
        for cx, cy, w, d, h, rz in GIN_RUINS:
            ruin_stone_house(g, DECOR, cx, cy, w, d, h, rz, rnd)
    DECOR = G()
    mesh_obj(u, 'Body', body)

    def decor(g):
        # the shared decor kit collected by ruin_stone_house, plus flagstones, wall stones, stalactites, roots, rubble, torches
        g.bm = DECOR.bm
        g.mats = DECOR.mats
        rnd = random.Random(5)
        # flagstones (2 cm above the floor slab, no collider) with a few cracked / missing ones
        for ix in range(int(hx0) + 1, int(hx1) - 1, 3):
            for iy in range(int(hy0) + 1, int(hy1) - 1, 3):
                if rnd.random() < 0.12:
                    continue
                if math.hypot(ix + 1.5 - ax, iy + 1.5 - ay) < 5.6:
                    continue
                g.rbox(rnd.choice(('ME_Stone_Light', 'ME_Stone_Warm', 'ME_Stone_Light')), (ix + 1.5, iy + 1.5, floor),
                       (rnd.uniform(2.6, 2.9), rnd.uniform(2.6, 2.9), 0.03), bevel=0.0, rotz=rnd.uniform(-0.05, 0.05))
        for k in range(14):                                                                   # floor cracks
            x, y = rnd.uniform(hx0 + 2, hx1 - 2), rnd.uniform(hy0 + 2, hy1 - 2)
            pts, a = [], rnd.uniform(0, 6.3)
            for j in range(5):
                pts.append((x, y, floor + 0.04))
                a += rnd.uniform(-0.7, 0.7)
                x, y = x + 0.8 * math.cos(a), y + 0.8 * math.sin(a)
            g.tube(pts, 0.05, 'ME_Tunnel_Dark', seg=4)
        # rough stones jutting from the walls (visual only)
        for k in range(70):
            side = rnd.randrange(4)
            z = rnd.uniform(floor + 0.3, -1.6)
            if side == 0:
                x, y, rz = hx0 + 0.15, rnd.uniform(hy0 + 1, hy1 - 1), 0.0
                if abs(y) < 3.4 and z < -3.0:
                    continue                                                                  # keep the tunnel mouth clear
            elif side == 1:
                x, y, rz = hx1 - 0.15, rnd.uniform(hy0 + 1, hy1 - 1), 0.0
            else:
                x, y, rz = rnd.uniform(hx0 + 1, hx1 - 1), (hy0 + 0.15) if side == 2 else (hy1 - 0.15), math.pi / 2
            g.blob(rnd.choice(('ME_Stone_Dark', 'ME_Stone_Light', 'ME_Stone_Moss')), (x, y, z), rnd.uniform(0.5, 1.1),
                   sz=(0.45, 1.0, 0.7), seg=8, jitter=0.18, rnd=rnd, rot=(0, 0, rz))
        # stalactites (tips stay above floor + 5.5 m, none over the altar portal)
        for k in range(26):
            x, y = rnd.uniform(hx0 + 1.5, hx1 - 1.5), rnd.uniform(hy0 + 1.5, hy1 - 1.5)
            if math.hypot(x - ax, y - ay) < 7.5:
                continue
            L = rnd.uniform(1.0, 3.2)
            g.lathe([(0, -1.0), (rnd.uniform(0.35, 0.7), -1.0), (0.15, -1.0 - L * 0.7), (0, -1.0 - L)],
                    rnd.choice(('ME_Stone_Dark', 'ME_Stone_Light')), seg=7, loc=(x, y, 0))
        # roots hanging down the walls and from the ceiling
        for k in range(12):
            x, y = rnd.uniform(hx0 + 1, hx1 - 1), rnd.choice((hy0 + 0.4, hy1 - 0.4))
            pts = [(x, y, -1.0)]
            for j in range(5):
                pts.append((pts[-1][0] + rnd.uniform(-0.6, 0.6), y, pts[-1][2] - rnd.uniform(0.9, 1.5)))
            g.tube(pts, [0.16, 0.13, 0.1, 0.08, 0.05, 0.03], 'ME_Root', seg=6)
        # rubble piles and broken column drums (kept out of the altar ring and the tunnel path)
        for k in range(30):
            x, y = rnd.uniform(hx0 + 2, hx1 - 2), rnd.uniform(hy0 + 2, hy1 - 2)
            if math.hypot(x - ax, y - ay) < 10.0 or (x < ax and abs(y) < 4.0):
                continue
            g.blob(rnd.choice(('ME_Stone_Dark', 'ME_Stone_Light', 'ME_Stone_Warm')), (x, y, floor + 0.1),
                   rnd.uniform(0.25, 0.6), sz=(1.2, 1.0, 0.55), seg=7, jitter=0.25, rnd=rnd, rot=(0, 0, rnd.uniform(0, 6.3)))
        bx, by = GIN_COLUMNS[3]
        for dx, dy, rz in ((1.3, -1.0, 0.4), (2.6, -0.4, 1.3), (0.6, -2.4, 2.2)):                  # fallen drums of the broken column
            cyl(g, 'ME_Stone_Light', (bx + dx, by + dy, floor + 0.85), 0.85, 1.1, seg=12, rot=(math.pi / 2, 0, rz))
        for k, (x, y) in enumerate(GIN_COLUMNS[:3]):                                              # column cracks
            g.tube([(x + 0.92 * math.cos(1.2 * k), y + 0.92 * math.sin(1.2 * k), floor + z) for z in (1.0, 2.5, 4.0, 5.5)],
                   0.07, 'ME_Tunnel_Dark', seg=5)
        # moss on column bases and pedestals
        for x, y in GIN_COLUMNS + [(px, py) for px, py, a in gin_pedestals()]:
            for j in range(3):
                a = rnd.uniform(0, 6.3)
                g.blob('ME_Stone_Moss', (x + 0.9 * math.cos(a), y + 0.9 * math.sin(a), floor + 0.05), 0.35,
                       sz=(1.3, 1.0, 0.25), seg=8, jitter=0.2, rnd=rnd)
        # rune marks carved on each pedestal top (glow with the altar via Altar_Glow) and faint wall runes
        for x, y, rz in ((hx0 + 0.05, 9.0, math.pi / 2), (hx0 + 0.05, -9.0, math.pi / 2), (hx1 - 0.05, 9.0, -math.pi / 2),
                         (34.0, hy1 - 0.05, math.pi), (24.0, hy0 + 0.05, 0.0)):
            star_prism(g, 'ME_Glow_Purple', (x, y, -4.5), 0.5, 0.2, 0.06, points=4, rot=(0, 0, rz))
        # cobwebs in the hall corners and over the tunnel mouth
        for cx, cy, dx, dy in ((hx0, hy0, 1, 1), (hx0, hy1, 1, -1), (hx1, hy0, -1, 1), (hx1, hy1, -1, -1)):
            cobweb(g, (cx, cy), dx, dy, -1.1, 2.6)
        cobweb(g, (hx0 + 0.1, 2.6), 1, -1, -3.6, 1.6)
        for x, y in LANTERNS:                                                                 # stone torch posts
            cyl(g, 'ME_Stone_Dark', (x, y, floor), 0.22, 2.3, seg=8, r2=0.14)
            g.lathe([(0.0, 0.0), (0.32, 0.12), (0.36, 0.3), (0.0, 0.3)], 'ME_Stone_Light', seg=8, loc=(x, y, floor + 2.3))
            g.blob('ME_Glow_Orange', (x, y, floor + 2.75), 0.2, sz=(1, 1, 1.4), seg=10)
        for k in range(5):                                                                    # bands on the shaft column
            torus(g, 'ME_Purple_Deep', (0, 0, -9.0 + k * 3.8), GIN_R0 + 0.02, 0.09, seg=16)
    LANTERNS = [(hx0 + 2.5, -4.5), (hx0 + 2.5, 4.5), (24.0, -12.0), (24.0, 12.0), (44.0, -12.0), (44.0, 12.0), (9.0, -2.0)]
    mesh_obj(u, 'Decor', decor)
    for i, (x, y) in enumerate(LANTERNS):
        empty(u, f'Lantern_{i:02d}', (x, y, floor + 2.75))
    empty(u, f'Lantern_{len(LANTERNS):02d}', (2.6, 0.0, -3.0))                              # in the shaft
    for i, (x, y) in enumerate(((20.0, -12.0), (20.0, 12.0), (34.0, -14.0), (34.0, 14.0), (48.0, -12.0), (48.0, 12.0))):
        empty(u, f'CaveLight_{i:02d}', (x, y, floor + 6.0))                                     # soft cool fill (Unity adds lights)

    def altar_glow(g):    # recolored to cycling rainbow by Unity once all runes are in
        torus(g, 'ME_Glow_Purple', (ax, ay, floor + 0.97), 2.5, 0.09, seg=32)
        torus(g, 'ME_Glow_Purple', (ax, ay, floor + 2.55), 0.62, 0.08, seg=16)
        for k in range(4):
            a = k * math.pi / 2 + math.pi / 4
            g.tube([(ax + 0.62 * math.cos(a), ay + 0.62 * math.sin(a), floor + 1.0),
                    (ax + 0.5 * math.cos(a), ay + 0.5 * math.sin(a), floor + 2.3)], 0.06, 'ME_Glow_Purple', seg=5)
        for px, py, a in gin_pedestals():                                                     # ring carved on each pedestal top
            torus(g, 'ME_Glow_Purple', (px, py, floor + 1.58), 0.45, 0.05, seg=20)
    mesh_obj(u, 'Altar_Glow', altar_glow)

    pz = floor + 5.6                                                                        # portal above the pillar, facing -X
    mesh_obj(u, 'Portal_Ring', lambda g: torus(g, 'ME_Glow_White', (0, 0, 0), 2.6, 0.24, seg=40, rot=(0, math.pi / 2, 0)),
             loc=(ax, ay, pz))

    def swirl(g):
        g.lathe([(0, -0.03), (2.45, -0.03), (2.45, 0.03), (0, 0.03)], 'ME_Glow_Purple', seg=40, rot=(0, math.pi / 2, 0))
        for k in range(4):                                                                   # spiral arms
            a0 = k * math.pi / 2
            g.tube([(-0.06, 2.3 * (i / 12) * math.cos(a0 + i * 0.35), 2.3 * (i / 12) * math.sin(a0 + i * 0.35))
                    for i in range(13)], 0.1, 'ME_Glow_White', seg=5)
    mesh_obj(u, 'Portal_Swirl', swirl, loc=(ax, ay, pz))
    for i, (yy, zz) in enumerate([(0, 0)] + [(1.4 * math.cos(k * math.pi / 3), 1.4 * math.sin(k * math.pi / 3)) for k in range(6)]):
        empty(u, f'Float_{i:02d}', (ax - 0.4, ay + yy, pz + zz - 0.6))
    for i, (px, py, a) in enumerate(gin_pedestals()):                                       # one rune socket on top of each pedestal
        empty(u, f'Slot_{i:02d}', (px, py, floor + 1.85), rot=(0, 0, a))
    empty(u, 'Board', (ax - 3.2, ay, floor + 0.95))
    empty(u, 'Mouth', (ax, ay, pz))


# pedestals: radius 7.5 round the altar, 72 deg apart, none on the tunnel side (-X) so the path to the altar stays open
GIN_PEDESTAL_R = 7.5
GIN_COLUMNS = [(22.0, -9.0), (22.0, 9.0), (46.0, -9.0), (46.0, 9.0)]   # >= 3.6 m (monster width) from pedestals and ruins
GIN_RUINS = ((GIN_HALL[0] + 4, GIN_HALL[2] + 3, 8, 6, 4.6, 0.0), (GIN_HALL[0] + 4, GIN_HALL[3] - 3, 8, 6, 5.2, math.pi),
             (GIN_HALL[1] - 4, GIN_HALL[2] + 3, 8, 6, 4.2, 0.0), (GIN_HALL[1] - 4, GIN_HALL[3] - 3, 8, 6, 5.0, math.pi),
             (GIN_HALL[1] - 3, 0.0, 8, 6, 4.8, -math.pi / 2))


def gin_pedestals():
    ax, ay = GIN_ALTAR
    out = []
    for k in range(5):
        a = math.radians(k * 72.0)
        out.append((ax + GIN_PEDESTAL_R * math.cos(a), ay + GIN_PEDESTAL_R * math.sin(a), a + math.pi))  # face the altar
    return out


def ruin_stone_house(body, decor, cx, cy, w, d, h, rotz, rnd):
    """Collapsed stone house: a solid rubble core (collider), jagged broken walls on top, a fallen beam,
    dark empty windows, rubble at the foot and a few gingerbread remnants of the old cookie village."""
    def at(x, y):
        c, s_ = math.cos(rotz), math.sin(rotz)
        return cx + x * c - y * s_, cy + x * s_ + y * c
    core_h = h * 0.6
    body.rbox('ME_Stone_Warm', (cx, cy, -GIN_DEPTH), (w, d, core_h), bevel=0.25, rot=(0.0, 0.03, rotz))
    # jagged wall tops along the four sides
    for side in range(4):
        along = w if side % 2 == 0 else d
        n = int(along // 1.3)
        for k in range(n):
            t = -along / 2 + (k + 0.5) * along / n
            if side == 0:
                x, y = t, -d / 2 + 0.25
            elif side == 2:
                x, y = t, d / 2 - 0.25
            elif side == 1:
                x, y = w / 2 - 0.25, t
            else:
                x, y = -w / 2 + 0.25, t
            top = rnd.uniform(0.0, h - core_h + 0.4) * (0.4 if rnd.random() < 0.3 else 1.0)
            if top < 0.15:
                continue
            px, py = at(x, y)
            decor.rbox(rnd.choice(('ME_Stone_Warm', 'ME_Stone_Light')), (px, py, -GIN_DEPTH + core_h - 0.05),
                       (1.25, 0.5, top), bevel=0.08, rotz=rotz + (0 if side % 2 == 0 else math.pi / 2) + rnd.uniform(-0.06, 0.06))
    for ox in (-w * 0.28, w * 0.28):                                                         # dark empty windows
        x, y = at(ox, -d / 2 - 0.04)
        decor.rbox('ME_Tunnel_Dark', (x, y, -GIN_DEPTH + 1.3), (1.2, 0.08, 1.2), bevel=0.0, rotz=rotz)
    x, y = at(0, -d / 2 - 0.04)
    decor.rbox('ME_Tunnel_Dark', (x, y, -GIN_DEPTH), (1.4, 0.08, 2.2), bevel=0.0, rotz=rotz)          # blocked doorway
    bx, by = at(rnd.uniform(-w / 4, w / 4), 0)
    decor.rbox('ME_Root', (bx, by, -GIN_DEPTH + core_h + 0.4), (w * 0.9, 0.35, 0.3), bevel=0.05,
               rot=(0.0, 0.32, rotz + 0.3))                                                  # fallen roof beam
    for k in range(5):                                                                       # rubble at the foot (front)
        x, y = at(rnd.uniform(-w / 2, w / 2), -d / 2 - rnd.uniform(0.3, 1.2))
        decor.blob(rnd.choice(('ME_Stone_Warm', 'ME_Stone_Dark')), (x, y, -GIN_DEPTH + 0.1), rnd.uniform(0.25, 0.5),
                   sz=(1.2, 1.0, 0.6), seg=7, jitter=0.25, rnd=rnd)
    for k in range(3):                                                                       # gingerbread remnants
        x, y = at(rnd.uniform(-w / 2 + 0.5, w / 2 - 0.5), -d / 2 - 0.05)
        decor.rbox('ME_Cookie_Dark', (x, y, -GIN_DEPTH + rnd.uniform(0.6, core_h - 0.4)), (0.7, 0.12, 0.45), bevel=0.05,
                   rot=(rnd.uniform(-0.3, 0.3), 0.0, rotz))
        decor.blob('ME_Cream', (x, y - 0.02, -GIN_DEPTH + core_h - 0.2), 0.18, sz=(1, 0.5, 1.4), seg=8)
    for k in range(3):                                                                       # moss
        x, y = at(rnd.uniform(-w / 2, w / 2), rnd.uniform(-d / 2, d / 2))
        decor.blob('ME_Stone_Moss', (x, y, -GIN_DEPTH + core_h), 0.5, sz=(1.4, 1.0, 0.25), seg=8, jitter=0.2, rnd=rnd)


# ---------------- CandyForest: giant cake that breaks open into a candy rocket (EscapeVisualPlan.md §5.1) ----------------
CAKE_TIERS = [  # (radius, z bottom, height, sponge, shard count)
    (7.4, 0.35, 4.0, 'ME_Pink', 6),
    (5.3, 4.35, 3.2, 'ME_Cream', 4),
    (3.3, 7.55, 2.5, 'ME_Pink', 2),
]


def sector(g, mat, top_mat, R, z0, z1, a0, a1, seg=8):
    """Solid cake wedge (angles in radians) from the axis out to R, z0..z1; top face uses top_mat."""
    bm = g.bm
    ring = lambda z, r: [bm.verts.new((r * math.cos(a0 + (a1 - a0) * i / seg), r * math.sin(a0 + (a1 - a0) * i / seg), z))
                         for i in range(seg + 1)]
    c0, c1 = bm.verts.new((0, 0, z0)), bm.verts.new((0, 0, z1))
    lo, hi = ring(z0, R), ring(z1, R)
    m_side, m_top = g.mi(mat), g.mi(top_mat)
    for i in range(seg):
        f = bm.faces.new((lo[i], lo[i + 1], hi[i + 1], hi[i])); f.material_index = m_side          # outer skin
        f = bm.faces.new((c1, hi[i], hi[i + 1])); f.material_index = m_top                       # top
        f = bm.faces.new((c0, lo[i + 1], lo[i])); f.material_index = m_side                      # bottom
    f = bm.faces.new((c0, lo[0], hi[0], c1)); f.material_index = m_side                          # cut faces
    f = bm.faces.new((c0, c1, hi[-1], lo[-1])); f.material_index = m_side


def device_candy():
    u = 'ESC_CandyForest'

    def body(g):   # wide candy plate with a gentle slope (walkable), cream rim, 5 glass cell pedestals at the front
        g.lathe([(0, 0), (9.6, 0), (9.2, 0.15), (8.4, 0.35), (0, 0.35)], 'ME_Teal', seg=48)
        torus(g, 'ME_Cream', (0, 0, 0.32), 8.2, 0.12, seg=48)
        for k in range(5):
            x, y, _ = side(230 + k * 20, 8.9, 0)
            cyl(g, 'ME_Cookie_Gold', (x, y, 0.2), 0.45, 0.55, seg=12)
            torus(g, 'ME_Purple_Deep', (x, y, 0.72), 0.42, 0.06, seg=16)
    mesh_obj(u, 'Body', body)

    def intact(g):
        rnd = random.Random(4)
        for R, z0, h, sponge, _ in CAKE_TIERS:
            cyl(g, sponge, (0, 0, z0), R, h, seg=40)
            cyl(g, 'ME_Cream', (0, 0, z0 + h - 0.05), R + 0.12, 0.35, seg=40)                       # icing band
            for k in range(int(R * 3)):                                                            # cream drips
                a = 2 * math.pi * k / int(R * 3) + rnd.uniform(-0.05, 0.05)
                g.blob('ME_Cream', (math.cos(a) * (R + 0.08), math.sin(a) * (R + 0.08), z0 + h - 0.35 - rnd.uniform(0, 0.5)),
                       0.32, sz=(1, 1, 1.8), seg=8)
            for k in range(int(R * 1.6)):                                                          # strawberries on the rim
                a = 2 * math.pi * (k + 0.5) / int(R * 1.6)
                g.blob('ME_Red_Button', (math.cos(a) * (R - 0.6), math.sin(a) * (R - 0.6), z0 + h + 0.35), 0.45, seg=10)
        for k in range(3):                                                                         # candles on top
            x, y, _ = side(90 + k * 120, 1.4, 0)
            cyl(g, 'ME_Cream', (x, y, 10.05), 0.28, 1.6, seg=10)
            g.blob('ME_Glow_Orange', (x, y, 11.9), 0.3, sz=(1, 1, 1.6), seg=10)
    mesh_obj(u, 'Cake_Intact', intact)

    def cracks(g):    # glowing cracks that show while the cake shakes before it bursts
        rnd = random.Random(9)
        for R, z0, h, _, n in CAKE_TIERS:
            for k in range(n):
                a = 2 * math.pi * k / n
                pts = [(math.cos(a + rnd.uniform(-0.1, 0.1)) * (R + 0.06), math.sin(a + rnd.uniform(-0.1, 0.1)) * (R + 0.06),
                        z0 + h * t) for t in (0.0, 0.3, 0.55, 0.8, 1.0)]
                g.tube(pts, 0.12, 'ME_Glow_Orange', seg=6)
    mesh_obj(u, 'Cake_Cracks', cracks)

    idx = 0
    for R, z0, h, sponge, n in CAKE_TIERS:          # shards: wedges of each tier, origin at the wedge centre
        for k in range(n):
            a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
            am = (a0 + a1) / 2
            cx, cy, cz = math.cos(am) * R * 0.55, math.sin(am) * R * 0.55, z0 + h / 2
            idx += 1
            mesh_obj(u, f'Cake_Shard_{idx:02d}',
                     lambda g, R=R, z0=z0, h=h, s=sponge, a0=a0, a1=a1, cx=cx, cy=cy, cz=cz:
                     placed(g, lambda gg: sector(gg, s, 'ME_Cream', R, z0, z0 + h, a0, a1), loc=(-cx, -cy, -cz)),
                     loc=(cx, cy, cz))

    rocket = empty(u, 'Cake_Rocket', (0, 0, 0))     # Unity lowers this under the plate and raises it after the burst

    def rocket_body(g):
        g.lathe([(0, 0.35), (1.6, 0.35), (2.3, 1.6), (2.4, 5.5), (2.0, 7.6), (1.2, 9.4), (0.4, 10.4), (0, 10.6)],
                'ME_Pink', seg=32, segmat=lambda k: 'ME_Cream' if (k // 4) % 2 == 0 else 'ME_Pink')
        cut_doorway(g, 0.7, 1.62, 3.45, 0.42, 'ME_Purple_Deep')   # behind the hatch (closed hatch: |x| < 0.8, z 1.0..3.6) — Request1003bPlan.md §6
        g.blob('ME_Teal', (0, 0, 10.6), 0.45, seg=12)
        g.lathe([(0, 0.0), (1.2, 0.0), (1.6, 0.6), (0, 0.6)], 'ME_Gray_Dark', seg=24)                     # nozzle
        for a in (90, 210, 330):
            fin(g, 'ME_Teal', a, [(0, 0.3), (2.0, -0.1), (2.2, 0.6), (1.2, 2.4), (0.1, 4.2)], thick=0.35, r0=2.1)
        g.blob('ME_Glow_Teal', side(270, 2.25, 7.0), 0.75, sz=(1, 0.35, 1), seg=16)                     # window
        torus(g, 'ME_Purple_Deep', side(270, 2.28, 7.0), 0.8, 0.12, rot=(math.pi / 2, 0, 0))
        g.rbox('ME_Purple_Deep', (0, -2.35, 0.9), (2.0, 0.2, 0.18), bevel=0.05)                         # hatch frame
        g.rbox('ME_Purple_Deep', (0, -2.35, 3.6), (2.0, 0.2, 0.18), bevel=0.05)
    mesh_obj(u, 'RocketBody', rocket_body, parent=rocket)
    cabin(u, 'Cabin', 2.0, 1.62, 3.75, seats=3, parent=rocket)   # passengers' cockpit seen through the open hatch

    def hatch(g):
        g.rbox('ME_Cream', (0.8, 0, 0), (1.6, 0.2, 2.6), bevel=0.12)
        g.blob('ME_Red_Button', (1.35, -0.12, 1.2), 0.12, seg=8)
    mesh_obj(u, 'Hatch', hatch, loc=(-0.8, -2.42, 1.0), parent=rocket)

    def flame(g):
        g.lathe([(0, -3.4), (1.1, -0.9), (1.05, 0.0), (0, 0.0)], 'ME_Glow_Orange', seg=16)
        g.lathe([(0, -2.2), (0.6, -0.6), (0.55, 0.02), (0, 0.02)], 'ME_Glow_White', seg=12)
    mesh_obj(u, 'Flame', flame, loc=(0, 0, 0.05), parent=rocket)

    for k in range(5):                                # cell sockets (counter mode: Slot_00 .. Slot_nn)
        empty(u, f'Slot_{k:02d}', side(230 + k * 20, 8.9, 1.15))
    empty(u, 'Board', (0, -5.2, 0.35))


BUILDS.append(('ESC_CursedCandyCarnival', device_carnival, (), {}))
BUILDS.append(('ESC_HauntedBakery', device_bakery, (), {}))
BUILDS.append(('ESC_ChocolateFactory', device_factory, (), {}))
BUILDS.append(('ESC_GingerbreadVillage', device_gingerbread, (), {}))
BUILDS.append(('ESC_CandyForest', device_candy, (), {}))
