# CursedCandyCarnival blockout. Needs maplib namespace.
# carousel plaza r<=35 | promenade ring r=90 (10 m) + inner loop r=58 (6 m) | 7 spokes (north = hill path)
# north hill + ferris wheel (highest) | east market | west puppet theatre | south gate | rides in outer corners
import math


def booth(m, x, y, rotz, rnd, cat='GameplayProps'):
    place(m, rnd.choice(('Booth_Neon_A', 'Booth_Neon_B', 'Booth_Neon_C')), cat, x, y, rotz, rnd.uniform(1.0, 1.15))
    m.blockers.append(((x, y), 3.5))


def doll(m, x, y, s=1.0):
    place(m, ('CreepyDoll_A', 'CreepyDoll_B', 'CreepyDoll_C')[int(abs(x * 5 + y * 3)) % 3], 'Decoration', x, y,
          (x * 0.4) % 6.28, s * 0.5)


def build():
    m = Map('CursedCandyCarnival', 'M_Ground_Carnival')
    rnd = random.Random(44)

    # ---- ground paths ----
    m.cyl('CarouselPlaza', 'Ground', 'M_Grass_Teal', (0, 0, 0), 35, 0.04, seg=64)
    m.blockers.append(((0, 0), 35))
    m.path_ring('Promenade', 'Ground', 'M_Cookie_Stone', (0, 0), 90, 10)
    m.path_ring('InnerLoop', 'Ground', 'M_Cookie_Light', (0, 0), 58, 6)
    for deg in (0, 45, 135, 180, 225, 315):
        a = math.radians(deg)
        m.strip('Spoke', 'Ground', 'M_Cookie_Light', (35 * math.cos(a), 35 * math.sin(a)),
                (95 * math.cos(a), 95 * math.sin(a)), 7)
    m.strip('Spoke_Hill', 'Ground', 'M_Cookie_Light', (0, 35), (0, 100), 8)
    m.strip('MainAvenue', 'Ground', 'M_Cookie_Stone', (0, -35), (0, -172), 14)
    for deg in (0, 180):
        a = math.radians(deg)
        m.strip('OuterPath', 'Ground', 'M_Cookie_Light', (95 * math.cos(a), 0), (172 * math.cos(a), 0), 6)

    # ---- carousel ----
    # static base (walkable, collider) + rotating top (separate mesh, pivot at centre) for Unity animation
    def _carousel_base(g, rnd):
        g.lathe([(0, 0), (14.5, 0), (14.5, 0.25), (0, 0.26)], 'M_Cookie_Light', seg=48,
                radial=lambda a: 1 + 0.02 * math.cos(24 * a))
        g.tube(arc((0, 0, 0.3), 14.3, 0, 2 * math.pi, 48, plane='xy'), 0.18, 'M_Neon_Teal', seg=6)

    def _carousel_top(g, rnd):
        g.tube([(0, 0, 0.25), (0, 0, 14)], 1.1, 'M_Candy_Red', seg=14, ringmat=stripes('M_Candy_Red', 'M_Candy_White', 2))
        g.lathe([(16.5, 9.0), (15.5, 10.5), (8, 13.5), (1.2, 15.5), (0, 15.6)], 'M_Tent_Purple', seg=32,
                segmat=lambda k: 'M_Tent_Purple' if (k // 2) % 2 == 0 else 'M_Icing_Pink')          # striped roof
        for i in range(24):                                                                 # scalloped valance
            a = 2 * math.pi * i / 24
            g.lathe([(1.1, 0), (0.9, -0.9), (0, -1.1)], ('M_Icing_Pink', 'M_Tent_Purple')[i % 2], seg=8,
                    loc=(16.2 * math.cos(a), 16.2 * math.sin(a), 9.2), scale=(1, 0.35, 1), rot=(0, 0, a + math.pi / 2))
        g.tube(arc((0, 0, 9.1), 16.4, 0, 2 * math.pi, 48, plane='xy'), 0.2, 'M_Neon_Pink', seg=6)
        g.blob('M_Magic_Gem', (0, 0, 16.6), 1.6, seg=16)
        for k in range(10):                                                                 # cookie horses on poles
            a = 2 * math.pi * k / 10
            x, y = 10.5 * math.cos(a), 10.5 * math.sin(a)
            g.tube([(x, y, 0.25), (x, y, 9.0)], 0.14, 'M_Gold', seg=6)
            tx, ty = -math.sin(a), math.cos(a)                                               # facing = travel dir
            z = 2.0 + 0.6 * (k % 2)
            g.blob(('M_Cookie_Light', 'M_Candy_Pink', 'M_Cookie_Wall')[k % 3], (x, y, z), 1.0, sz=(1, 1, 0.6), seg=12,
                   rot=(0, 0, math.atan2(ty, tx)))
            g.blob(('M_Cookie_Light', 'M_Candy_Pink', 'M_Cookie_Wall')[k % 3], (x + tx * 1.0, y + ty * 1.0, z + 0.9), 0.5,
                   sz=(1.3, 0.8, 0.9), seg=10, rot=(0, 0, math.atan2(ty, tx)))
            for s in (-0.5, 0.5):
                g.tube([(x + tx * s, y + ty * s, z - 0.3), (x + tx * s * 1.3, y + ty * s * 1.3, z - 1.3)], 0.14,
                       'M_Cookie_Wall', seg=5)
            g.blob('M_Magic_Gem', (x + tx * 1.35, y + ty * 1.35, z + 1.05), 0.12, seg=6)
    ASSETS['Carousel_Base'] = _carousel_base
    ASSETS['Carousel_Top'] = _carousel_top
    place(m, 'Carousel_Base', 'MainStructures', 0, 0, 0)
    place(m, 'Carousel_Top', 'MainStructures', 0, 0, 0)['no_pad'] = True       # rotate this about Z in Unity
    m.empty('CandyCarousel_Pivot', 'MainStructures', (0, 0, 0), 3, 'ARROWS')
    for k in range(8):   # plaza benches / candy statues
        a = math.radians(k * 45 + 22.5)
        if k % 2:
            m.box('CookieBench', 'GameplayProps', 'M_Cookie_Light', (26 * math.cos(a), 26 * math.sin(a), 0), (3, 0.9, 0.9),
                  a + math.pi / 2)
        else:
            lollipop(m, 'GameplayProps', 26 * math.cos(a), 26 * math.sin(a), 5, 2, face=a)

    # ---- north hill + ferris wheel (landmark) ----
    hx, hy = 0, 136
    # highest point of the map: 8 m hill with an 18 m flat crown (flank <= 16 deg); wheel parts anchor on its top
    m.hill(hx, hy, 8, 62, plateau=18, name='FerrisHill')
    m.flat_circle(hx, hy, 16, fall=6, level=8.0)
    m.blockers.append(((hx, hy), 40))
    wheel_anchor = m.at(hx, hy)
    wheel_anchor.__enter__()
    hub = 42
    R = 36
    m.torus('CandyFerrisWheel_Rim', 'MainStructures', 'M_Neon_Purple', (hx, hy, hub), R, 1.0, rot=(math.pi / 2, 0, 0), seg=64)
    m.torus('CandyFerrisWheel_NeonRing', 'Lighting', 'M_Neon_Pink', (hx, hy - 1.2, hub), R * 0.85, 0.45, rot=(math.pi / 2, 0, 0), seg=64)
    m.torus('CandyFerrisWheel_InnerRim', 'Decoration', 'M_Candy_White', (hx, hy, hub), R * 0.62, 0.6, rot=(math.pi / 2, 0, 0))
    m.cyl('CandyFerrisWheel_Hub', 'MainStructures', 'M_Candy_Red', (hx, hy - 3, hub), 3, 6, seg=24, rot=(-math.pi / 2, 0, 0))
    for k in range(16):
        a = 2 * math.pi * k / 16
        m.cyl('CandyFerrisWheel_Spoke', 'Decoration', 'M_Candy_White', (hx, hy, hub), 0.35, R, seg=6,
              rot=(0, a, 0))
        gx, gz = R * math.sin(a), hub + R * math.cos(a)
        m.box('CandyFerrisWheel_Gondola', 'Decoration', ('M_Tent_Red', 'M_Tent_Blue', 'M_Glow_Yellow', 'M_Tent_Purple')[k % 4],
              (hx + gx, hy, gz - 4.5), (3.5, 3.5, 3.5))
    for s in (1, -1):
        for sy in (1, -1):
            dx = s * 16
            L = math.hypot(dx, hub)
            ang = math.atan2(dx, hub)
            m.cyl('CandyFerrisWheel_Leg', 'MainStructures', 'M_Metal_Dark', (hx + dx, hy + sy * 4, 0), 0.8, L, seg=8,
                  rot=(0, -ang, 0), tags=('no_pad',))
    wheel_anchor.__exit__()
    piv = m.empty('CandyFerrisWheel_Pivot', 'MainStructures', (hx, hy, hub), 6, 'ARROWS')
    piv['ax'], piv['ay'] = hx, hy

    # ---- east market (booth grid, 4 m alleys) ----
    for i, x in enumerate((112, 121, 130, 139, 148, 157, 166)):
        for j, y in enumerate(range(-54, 60, 12)):
            if abs(y) < 5 or rnd.random() < 0.15:
                continue
            booth(m, x, y, 0 if j % 2 else math.pi, rnd)
    for k in range(8):
        m.empty('FX_StringLights', 'Effects', (139, -54 + k * 15, 4.5), 3)

    # ---- west puppet theatre ----
    tx, ty, tw, td = -138, 0, 46, 34
    # doorways sized for MonsterPlayer (>= 7 m): 8 m side doors, 12 m front, 8.5 m lintels
    for p0, p1, gaps in (((tx - tw / 2, ty - td / 2), (tx + tw / 2, ty - td / 2), [(tw / 2, 8)]),
                         ((tx + tw / 2, ty - td / 2), (tx + tw / 2, ty + td / 2), [(td / 2, 12)]),
                         ((tx + tw / 2, ty + td / 2), (tx - tw / 2, ty + td / 2), [(tw / 2, 8)]),
                         ((tx - tw / 2, ty + td / 2), (tx - tw / 2, ty - td / 2), [])):
        wall(m, 'PuppetTheatre_Wall', p0, p1, 14, 1.0, gaps, mat='M_Tent_Purple', gap_h=8.5)
    m.box('PuppetTheatre_Roof', 'MainStructures', 'M_Tent_Red', (tx, ty, 14), (tw + 1, td + 1, 0.5), tags=('hide_top',))
    m.box('PuppetTheatre_Stage', 'MainStructures', 'M_Wood_Dark', (tx - 16, ty, 0), (10, 22, 0.28))
    m.box('PuppetTheatre_Curtain', 'Decoration', 'M_Tent_Red', (tx - 21, ty, 0.28), (1, 22, 9))
    for xx in (-130, -125, -120):          # seat blocks east of the N/S door lanes, 7 m centre aisle to the stage
        for yy in (-12.5, -8.5, 8.5, 12.5):
            m.box('PuppetTheatre_Seat', 'GameplayProps', 'M_Tent_Purple', (xx, ty + yy, 0), (1.5, 3, 1.0))
    for k in range(9):
        doll(m, tx - 17 + rnd.uniform(-2, 2), ty - 9 + k * 2.2, rnd.uniform(0.8, 1.4))
    m.blockers.append(((tx, ty), 28))
    m.empty('FX_TheatreFog', 'Effects', (tx, ty, 1), 8)

    # ---- south gate + ticket booths ----
    # gothic entrance gate (reference: dark towers, glowing arched windows, neon arch, jack-o-lantern on top)
    def _gate(g, rnd):
        for s in (-1, 1):
            x = s * 11
            g.rbox('M_Chocolate_Dark', (x, 0, 0), (6, 6, 15), 0.3)
            g.rbox('M_Gingerbread_Dark', (x, 0, 15), (6.6, 6.6, 1.2), 0.2)
            for z in (3.5, 9.0):                                            # glowing gothic windows (front & back)
                for yy in (-3.05, 3.05):
                    g.rbox('M_Jelly_Orange', (x, yy, z), (1.6, 0.2, 2.8), 0.1)
                    g.lathe([(0, 0), (0.8, 0), (0, 1.0)], 'M_Jelly_Orange', seg=12, loc=(x, yy, z + 2.8),
                            rot=(math.pi / 2, 0, 0), scale=(1, 1, 0.2))
            g.lathe([(4.0, 16.2), (3.0, 18.5), (0, 23.5)], 'M_Tent_Purple', seg=8)
            g.blob('M_Glow_Yellow', (x, 0, 24.0), 0.7, seg=10)
        g.tube(arc((0, 0, 8.5), 8.0, 0, math.pi, 24), 1.0, 'M_Chocolate_Dark', seg=10)
        g.tube(arc((0, -0.9, 8.5), 7.4, 0.08, math.pi - 0.08, 24), 0.35, 'M_Neon_Teal', seg=6)
        g.rbox('M_Chocolate_Dark', (0, 0, 16.2), (4.5, 1.2, 1.2), 0.3)
        # jack-o-lantern crest
        g.lathe([(0, 0), (1.6, 0.2), (2.2, 1.3), (1.8, 2.6), (0.4, 2.9), (0, 2.85)], 'M_Jelly_Orange', seg=20,
                loc=(0, 0, 17.3), radial=lambda a: 1 + 0.08 * abs(math.sin(4 * a)))
        for x in (-0.8, 0.8):
            g.lathe([(0, 0), (0.45, 0), (0, 0.55)], 'M_Glow_Yellow', seg=3, loc=(x, -2.05, 18.9), rot=(math.pi / 2, 0, 0))
        g.rbox('M_Glow_Yellow', (0, -2.1, 18.0), (1.6, 0.15, 0.35), 0.05)
        for s in (-1, 1):                                                   # bat-wing spikes
            g.lathe([(0.6, 0), (0, 3.5)], 'M_Chocolate_Dark', seg=4, loc=(s * 2.6, 0, 18.6), rot=(0, s * 0.9, 0))
    ASSETS['Carnival_GothicGate'] = _gate
    place(m, 'Carnival_GothicGate', 'MainStructures', 0, -164, 0)
    for s in (1, -1):
        place(m, 'Booth_Neon_C', 'GameplayProps', s * 18, -150, math.pi if s > 0 else 0)
    m.blockers.append(((0, -164), 14))

    # ---- rides in outer corners ----
    # NE circus tent
    place(m, 'CircusTent_Small', 'MainStructures', 120, 125, math.pi * 1.25, sxyz=(4.4, 4.4, 2.6))  # big top
    m.blockers.append(((120, 125), 26))
    # SE drop tower
    m.cyl('DropTower', 'MainStructures', 'M_Metal_Dark', (122, -122, 0), 3, 60, seg=16)
    m.torus('DropTower_Seats', 'MainStructures', 'M_Neon_Teal', (122, -122, 14), 5, 1.2)
    m.cyl('DropTower_Base', 'MainStructures', 'M_Candy_Teal', (122, -122, 0), 9, 2.5, seg=24)
    m.blockers.append(((122, -122), 10))
    # SW swing ride
    m.cyl('SwingRide_Pole', 'MainStructures', 'M_Candy_Red', (-122, -120, 0), 1.2, 16, seg=12)
    m.cyl('SwingRide_Top', 'MainStructures', 'M_Tent_Purple', (-122, -120, 14), 10, 3, r2=2, seg=24)
    for k in range(10):
        a = math.radians(k * 36)
        m.box('SwingRide_Seat', 'Decoration', 'M_Candy_White', (-122 + 9 * math.cos(a), -120 + 9 * math.sin(a), 4),
              (1.0, 1.0, 1.0), a)
    m.blockers.append(((-122, -120), 12))
    # NW bumper arena
    # low arena wall as 24 segments with two 9 m openings (south-east towards the promenade, north-west)
    bcx, bcy, br = -120, 118, 17.5
    gaps_deg = (-45, 135)
    for k in range(24):
        a0, a1 = 2 * math.pi * k / 24, 2 * math.pi * (k + 1) / 24
        mid = math.degrees((a0 + a1) / 2)
        if any(abs((mid - g + 180) % 360 - 180) < 15 for g in gaps_deg):
            continue
        m.strip('BumperArena_Wall', 'MainStructures', 'M_Candy_Teal', (bcx + br * math.cos(a0), bcy + br * math.sin(a0)),
                (bcx + br * math.cos(a1), bcy + br * math.sin(a1)), 1.0, h=1.2, road=False)
    for g in gaps_deg:
        m.doors.append(dict(wall='BumperArena_Wall', x=round(bcx + br * math.cos(math.radians(g)), 1),
                            y=round(bcy + br * math.sin(math.radians(g)), 1), width=round(2 * br * math.sin(math.radians(15)), 1),
                            height=8.0, nx=math.cos(math.radians(g)), ny=math.sin(math.radians(g))))
    for k in range(4):
        a = math.radians(k * 90)          # posts clear of the two openings (-45 / 135 deg)
        m.cyl('BumperArena_Post', 'MainStructures', 'M_Metal_Dark', (-120 + 17.5 * math.cos(a), 118 + 17.5 * math.sin(a), 0),
              0.5, 8, seg=8)
    m.cyl('BumperArena_Roof', 'MainStructures', 'M_Neon_Purple', (-120, 118, 8), 19, 1.2, seg=32, tags=('hide_top',))
    for k in range(6):
        m.box('BumperCar', 'GameplayProps', 'M_Candy_Pink', (-120 + rnd.uniform(-10, 10), 118 + rnd.uniform(-10, 10), 0),
              (2.4, 1.6, 1.0), rnd.uniform(0, 3))
    m.blockers.append(((-120, 118), 19))

    # ---- food stalls & dolls between the loops (r 40-85) ----
    def facing(x, y, rings=(35, 58, 90)):
        """rotz so the booth front (local -Y) faces the nearest walkway ring."""
        d, th = math.hypot(x, y), math.atan2(y, x)
        near = min(rings, key=lambda rr: abs(rr - d))
        return th + math.pi / 2 if near > d else th - math.pi / 2

    def stall(x, y, r):
        if r.random() < 0.7:
            booth(m, x, y, facing(x, y), r)
        else:
            doll(m, x, y, r.uniform(1.5, 3.0))
            m.blockers.append(((x, y), 2))
    def annulus(r):
        a = r.uniform(0, 2 * math.pi); d = r.uniform(40, 86)
        return d * math.cos(a), d * math.sin(a)
    m.scatter(annulus, 60, 11, 3, stall, seed=6)
    # outer area filler (r 96-165)
    def outer(r):
        return r.uniform(-168, 168), r.uniform(-168, 168)
    def outer_fn(x, y, r):
        if math.hypot(x, y) < 97:
            return
        k = r.random()
        if k < 0.35:
            booth(m, x, y, facing(x, y, (90,)), r)
        elif k < 0.55:
            pine(m, 'MainStructures', x, y, r.uniform(10, 16))
        elif k < 0.7:
            place(m, 'CircusTent_Small', 'MainStructures', x, y, facing(x, y, (90,)), r.uniform(0.9, 1.3))
            m.blockers.append(((x, y), 7))
        else:
            place(m, r.choice(('BalloonCluster', 'PumpkinLantern', 'CreepyDoll_A', 'CreepyDoll_C', 'Lollipop_Swirl')),
                  'GameplayProps', x, y, facing(x, y, (90,)), r.uniform(0.9, 1.4))
    m.scatter(outer, 110, 12, 3, outer_fn, seed=13)

    # ---- reference: pumpkin lanterns, balloons and string lights along the walks ----
    m.scatter(lambda r: (r.uniform(-168, 168), r.uniform(-168, 168)), 90, 9, 1.0,
              lambda x, y, r: place(m, r.choice(('PumpkinLantern', 'BalloonCluster', 'GumdropBush_B', 'PumpkinLantern')),
                                    'Decoration', x, y, r.uniform(0, 6.28), r.uniform(0.8, 1.2)), seed=41)

    # ---- lamps + string lights along the promenade ----
    for k in range(36):
        a = math.radians(k * 10)
        x, y = 96 * math.cos(a), 96 * math.sin(a)
        if m.clear_of_paths((x, y), 0.5):
            if k % 2:
                place(m, 'CarnivalLamp', 'GameplayProps', x, y, a)
            else:
                place(m, 'StringLightPole', 'Decoration', x, y, a + math.pi / 2)
    for k in range(20):
        a = math.radians(k * 18 + 9)
        m.sphere('FloorCandyLight', 'Lighting', ('M_Neon_Pink', 'M_Neon_Teal')[k % 2],
                 (84 * math.cos(a), 84 * math.sin(a), 0), 0.8, sz=(1, 1, 0.55), seg=12)

    # ---- fence + dark forest background ----
    for s in (1, -1):
        fence_line(m, (-171, s * 171), (-10, s * 171) if s < 0 else (171, s * 171), h=2.2, mat='M_Metal_Dark')
        if s < 0:
            fence_line(m, (10, -171), (171, -171), h=2.2, mat='M_Metal_Dark')
        fence_line(m, (s * 171, -171), (s * 171, 171), h=2.2, mat='M_Metal_Dark')
    rb = random.Random(3)
    for _ in range(260):
        a = rb.uniform(0, 2 * math.pi); d = rb.uniform(180, 290)
        x, y = d * math.cos(a), d * math.sin(a)
        if max(abs(x), abs(y)) < 180:
            continue
        pine(m, 'Background', x, y, rb.uniform(18, 32))

    # ---- lighting / fx ----
    m.light('Moon', 'SUN', (0, 0, 120), (0.45, 0.6, 0.9), 0.8, rot=(math.radians(55), 0, math.radians(160)))
    m.light('Wheel_Neon', 'POINT', (hx, hy - 10, hub), (1.0, 0.3, 0.8), 150000, 12)
    m.light('Carousel_Glow', 'POINT', (0, 0, 12), (0.7, 0.35, 1.0), 40000, 6)
    m.light('Market_Glow', 'POINT', (140, 0, 10), (1.0, 0.5, 0.7), 40000, 10)
    m.light('Theatre_Glow', 'POINT', (tx - 12, ty, 8), (0.3, 1.0, 0.9), 20000, 5)
    for k in range(6):
        m.empty('FX_Firework', 'Effects', (rnd.uniform(-120, 120), rnd.uniform(60, 170), 90), 6)
    m.empty('FX_FlickerLights_Group', 'Effects', (-122, -120, 16), 4)

    # ---- terrain: flat plaza & ride pads, rolling park ground, cursed ponds ----
    m.noise_amp = 0.45
    m.flat_circle(0, 0, 35, fall=12, level=0.0)
    m.flat_rect(139, 0, 32, 62, fall=10)              # market
    m.flat_rect(-138, 0, 25, 19, fall=10)             # puppet theatre
    m.flat_circle(0, -164, 16, fall=8)                # gate
    for x, y, r in ((120, 125, 26), (122, -122, 10), (-122, -120, 12), (-120, 118, 19)):
        m.flat_circle(x, y, r, fall=10)
    m.hill(-150, -160, 3.5, 30, name='Hill_SouthWest')
    m.hill(155, -158, 3.0, 26, name='Hill_SouthEast')
    m.hill(-62, 148, 3.5, 30, name='Hill_NorthWest')
    m.hill(62, -60, 2.0, 22, name='Hill_Inner')
    m.bowl(-60, -125, 2.0, 26, water='M_Water_Purple', name='CursedPond')
    m.bowl(60, -128, 1.8, 22, name='DryDip_South')
    m.bowl(-152, 62, 1.6, 20, water='M_Water_Teal', name='GlowPond_West')
    m.bowl(162, 165, 1.2, 14, name='DryDip_Corner')
    m.bowl(-62, 55, 1.2, 16, name='DryDip_Inner')

    # ---- density (reference 저주받은놀이공원: stalls, lanterns and props line every walkway) ----
    pal = [('PumpkinLantern', 0.6, 1.0), ('BalloonCluster', 0.8, 1.1), ('CreepyDoll_A', 0.4, 0.6),
           ('CreepyDoll_B', 0.4, 0.6), ('CookieCrate_B', 0.9, 1.1), ('GumdropBush_B', 0.6, 0.9),
           ('Lollipop_Swirl', 0.4, 0.6), ('ChocolateBarrel', 0.9, 1.1)]
    big = [('Booth_Neon_A', 1.0, 1.1), ('Booth_Neon_B', 1.0, 1.1), ('Booth_Neon_C', 1.0, 1.1),
           ('CircusTent_Small', 0.7, 0.9), ('CarnivalLamp', 1.0, 1.2)]
    m.dressed = dress_paths(m, pal, spacing=8.0, band=(0.8, 5.0), cluster=(1, 3), seed=44, big=big, big_every=4)
    m.finish_terrain()

    m.refs((8, -110))
    m.topcam()
    m.gamecam('GameCam_Avenue', (0, -110), (0, 136))
    m.gamecam('GameCam_Market', (116.5, 18), (160, 18))
    return m


MAP = build()
