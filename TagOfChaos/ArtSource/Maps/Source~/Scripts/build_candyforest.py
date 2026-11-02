# CandyForest blockout. Needs maplib namespace (exec'd first).
# Layout (radius from centre): plaza r<=40 with the escape cake in the middle | inner ring path r=70 | outer ring path r=130 | dense forest r>=150
import math


def build():
    m = Map('CandyForest', 'M_Ground_CandyForest')
    rnd = random.Random(7)

    # ---- Ground: paths ----
    m.cyl('Plaza', 'Ground', 'M_Cookie_Light', (0, 0, 0), 40, 0.04, seg=64)
    m.blockers.append(((0, 0), 40))
    m.path_ring('CookiePath_InnerRing', 'Ground', 'M_Cookie_Stone', (0, 0), 70, 8)
    m.path_ring('CookiePath_OuterRing', 'Ground', 'M_Cookie_Stone', (0, 0), 130, 8)
    for k in range(6):                                   # 6 wide radial paths
        a = math.radians(k * 60)
        m.strip('CookiePath_Radial', 'Ground', 'M_Cookie_Stone',
                (40 * math.cos(a), 40 * math.sin(a)), (134 * math.cos(a), 134 * math.sin(a)), 8)
    for k in range(6):                                   # 6 narrow trails between them
        a = math.radians(k * 60 + 30)
        m.strip('CookieTrail', 'Ground', 'M_Cookie_Light',
                (74 * math.cos(a), 74 * math.sin(a)), (126 * math.cos(a), 126 * math.sin(a)), 3.5)
    for k in (0, 2, 4):                                  # trails out to the forest edge (dead ends w/ loop pockets)
        a = math.radians(k * 60 + 30)
        m.strip('CookieTrail_Edge', 'Ground', 'M_Cookie_Light',
                (134 * math.cos(a), 134 * math.sin(a)), (160 * math.cos(a), 160 * math.sin(a)), 3.5)
        cx, cy = 163 * math.cos(a), 163 * math.sin(a)
        m.cyl('Trail_Pocket', 'Ground', 'M_Cookie_Light', (cx, cy, 0), 7, 0.04, seg=24)
        m.blockers.append(((cx, cy), 7))

    # ---- Centre: kept empty for the giant escape cake (EscapeVisualPlan.md §5.1) ----
    # The old giant lollipop tree landmark is gone; Unity's EscapeMapSetup puts the ~16 m cake (escape device) in the
    # middle of the plaza after compaction. The plaza cover ring moved out to r 37 (~15 m after compaction) so the
    # monster still has a 3.6 m lane around the cake.
    m.blockers.append(((0, 0), 30))
    for k in range(8):
        a = math.radians(k * 45 + 22)
        x, y = 37 * math.cos(a), 37 * math.sin(a)
        nm = ('Lollipop_GiantPink', 'Marshmallow_Stack', 'Macaron_Rock', 'CakeSlice')[k % 4]
        place(m, nm, 'GameplayProps', x, y, a + math.pi / 2, (0.55, 1.4, 1.3, 1.6)[k % 4])

    # ---- Terrain: hills (slope <= 20 deg) ----
    hills = [(100, 90, 18, 6, 4), (100, 150, 20, 7, 4.5), (100, 330, 18, 6, 4), (40, 105, 12, 4, 2.5), (40, 255, 12, 4, 2.5)]
    for r, deg, rb, rt, h in hills:
        a = math.radians(deg)
        x, y = r * math.cos(a), r * math.sin(a)
        m.mound('Hill', (x, y, 0), rb, rt, h, 'M_Hill_Pink')

    # ---- Chocolate pond (angle 45, r 100) ----
    px, py = 100 * math.cos(math.radians(45)), 100 * math.sin(math.radians(45))
    m.bowl(px, py, 2.5, 32, water='M_Water_Chocolate', wd=0.8, plateau=4, name='ChocolatePond')
    m.blockers.append(((px, py), 24))
    for k in range(7):
        a = math.radians(k * 51)
        place(m, ('CandyRock_Choco', 'CandyRock_Broken', 'CandyRock_Mint')[k % 3], 'GameplayProps',
              px + 27 * math.cos(a), py + 27 * math.sin(a), a, rnd.uniform(1.0, 1.4))

    # ---- Purple mushroom grove (angle 225, r 100) = deep/cursed side ----
    gx, gy = 100 * math.cos(math.radians(225)), 100 * math.sin(math.radians(225))
    def grove(x, y, r):
        s = r.uniform(0.6, 1.8)
        mushroom(m, 'MainStructures', x, y, 5 * s + 3, 3 * s + 1)
    m.scatter(lambda r: (gx + r.uniform(-30, 30), gy + r.uniform(-30, 30)), 22, 8, 2.5, grove, seed=11)
    m.blockers.append(((gx, gy), 30))

    # ---- Forest scatter: twisted trees inside (r 44-126, deep side), candy trees outside ----
    def inner(r):
        a = r.uniform(0, 2 * math.pi); d = r.uniform(44, 126)
        return d * math.cos(a), d * math.sin(a)
    def place_inner(x, y, r):
        ang = math.degrees(math.atan2(y, x)) % 360
        if 170 < ang < 280:          # deep cursed side only; the rest follows the pink reference forest
            twisted_tree(m, 'MainStructures', x, y, r.uniform(9, 16), r)
        else:
            candy_tree(m, 'MainStructures', x, y, r.uniform(8, 14), r.uniform(3, 5), r.randint(0, 2), r)
    m.scatter(inner, 170, 9, 3.0, place_inner, seed=3)

    def outer(r):
        a = r.uniform(0, 2 * math.pi); d = r.uniform(136, 172)
        return d * math.cos(a), d * math.sin(a)
    def place_outer(x, y, r):
        candy_tree(m, 'Background', x, y, r.uniform(14, 22), r.uniform(4.5, 7), r.randint(0, 2), r)
    m.scatter(outer, 150, 8, 2.0, place_outer, seed=5)
    m.scatter(lambda r: (r.uniform(-172, 172), r.uniform(-172, 172)), 60, 9, 2.0,
              lambda x, y, r: (math.hypot(x, y) > 150) and candy_tree(m, 'Background', x, y, r.uniform(16, 24),
                                                                      r.uniform(5, 7), r.randint(0, 2), r), seed=9)

    # ---- Decoration: path edges like the reference (canes, sticks, marshmallows, sugar drifts) ----
    for k in range(6):
        a = math.radians(k * 60)
        for d in range(50, 128, 16):
            for s in (-1, 1):
                ox, oy = rot2(d + rnd.uniform(-3, 3), s * rnd.uniform(6.5, 8.5), a)
                nm = rnd.choice(('CandyCane_Tall', 'CandyStick_Bundle', 'Marshmallow_Stack', 'SugarDrift',
                                 'Lollipop_SmallPink', 'CandyFlower_Patch', 'GumdropBush_A'))
                place(m, nm, 'Decoration', ox, oy, rnd.uniform(0, 6.28), rnd.uniform(0.8, 1.2))

    # ---- candy clutter between the trees (reference: macarons, cake, marshmallows, fallen canes) ----
    def clutter(x, y, r):
        ang = math.degrees(math.atan2(y, x)) % 360
        if 170 < ang < 280:      # deep / cursed side: mushrooms + crystals
            nm = r.choice(('SmallMushroom_Cluster', 'MagicCrystal_Cluster', 'GumdropBush_B', 'CandyRock_Broken'))
        else:
            nm = r.choice(('Macaron_Rock', 'Marshmallow_Stack', 'CakeSlice', 'SugarDrift', 'CandyRock_Broken',
                           'CandyStick_Bundle', 'Lollipop_SmallPink', 'GumdropBush_A', 'CandyFlower_Patch'))
        place(m, nm, 'GameplayProps' if nm in ('Macaron_Rock', 'CakeSlice', 'Marshmallow_Stack') else 'Decoration',
              x, y, r.uniform(0, 6.28), r.uniform(0.9, 1.6))
    m.scatter(inner, 220, 6, 2.5, clutter, seed=21)
    for deg in (0, 120, 240):          # candy-cane arches where three radial paths leave the plaza
        a = math.radians(deg)
        place(m, 'CandyCane_Arch', 'Decoration', 41 * math.cos(a), 41 * math.sin(a), a + math.pi / 2, 1.6)

    # ---- Lighting ----
    m.light('Moon', 'SUN', (0, 0, 120), (0.70, 0.78, 1.0), 1.5, rot=(math.radians(50), 0, math.radians(35)))
    m.light('Cake_Glow', 'POINT', (0, 0, 30), (1.0, 0.45, 0.75), 50000, 6)   # warm pink light over the escape cake
    m.light('Grove_Glow', 'POINT', (gx, gy, 12), (0.75, 0.35, 1.0), 40000, 8)
    m.light('Pond_Glow', 'POINT', (px, py, 8), (0.25, 0.9, 0.85), 15000, 6)

    # ---- Effects anchors ----
    for k, (r, deg) in enumerate(((100, 225), (70, 200), (70, 250), (45, 180), (130, 225))):
        a = math.radians(deg)
        m.empty('FX_PurpleFog', 'Effects', (r * math.cos(a), r * math.sin(a), 1), 8)
    for k in range(6):
        a = math.radians(k * 60 + 30)
        m.empty('FX_Fireflies', 'Effects', (30 * math.cos(a), 30 * math.sin(a), 3), 3)

    # ---- density (reference 사탕숲: candy piled up along every path) ----
    pal = [('Marshmallow_Stack', 0.7, 1.3), ('Macaron_Rock', 0.6, 1.1), ('CakeSlice', 0.8, 1.3),
           ('CandyStick_Bundle', 0.9, 1.4), ('Lollipop_SmallPink', 0.7, 1.2), ('CandyCane_Tall', 0.5, 0.9),
           ('SugarDrift', 0.8, 1.4), ('CandyRock_Broken', 0.5, 0.9), ('GumdropBush_A', 0.7, 1.0),
           ('CandyFlower_Patch', 0.9, 1.3)]
    big = [('Macaron_Rock', 1.8, 2.6), ('Marshmallow_Stack', 1.8, 2.4), ('CakeSlice', 2.2, 3.0),
           ('Lollipop_GiantPink', 0.6, 0.8)]
    m.dressed = dress_paths(m, pal, spacing=6.5, band=(1.0, 7.0), cluster=(2, 4), seed=12, big=big, big_every=5)
    # landmark base: a ring of giant sweets around the centre (cover inside the open plaza)
    # 2026-10-03: the two macaron rocks of this ring (k = 0 and 5 -> CAN_Macaron_Rock_165 / _166) stood in the way of the
    # cake rocket rising from the centre and were removed (Request1003bPlan.md §5). The name is still consumed so every
    # later object keeps its name; Unity's MapSceneBuilder.RemovedObjects lists the same names.
    for k in range(10):
        a = 2 * math.pi * k / 10
        nm = ('Macaron_Rock', 'Marshmallow_Stack', 'CakeSlice', 'CandyRock_Broken', 'SugarDrift')[k % 5]
        if nm == 'Macaron_Rock':
            m.nm(nm)
            continue
        place(m, nm, 'GameplayProps', 9.5 * math.cos(a), 9.5 * math.sin(a), a, (1.6, 1.5, 1.9, 1.3, 2.0)[k % 5])

    # ---- terrain: extra hills / bowls (smooth, <= 20 deg), flat plaza ----
    P = lambda r, deg: (r * math.cos(math.radians(deg)), r * math.sin(math.radians(deg)))
    m.noise_amp = 0.5
    m.hill(*P(150, 0), 7, 40, name='Hill_EastRidge')
    m.hill(*P(150, 120), 6, 38, name='Hill_NorthWest')
    m.hill(*P(60, 20), 2.5, 22, name='Hill_PlazaEdge')
    m.hill(-60, -92, 3, 25, name='Hill_Grove')
    m.bowl(*P(60, 280), 2.0, 24, name='DryHollow_South')
    m.bowl(*P(140, 190), 2.5, 26, name='DryHollow_West')
    m.bowl(-82, -62, 1.5, 16, name='DryHollow_Grove')
    m.bowl(*P(140, 300), 2.2, 24, water='M_Water_Teal', name='SyrupPond')
    m.bowl(*P(125, 75), 1.8, 18, water='M_Water_Purple', name='CursedPuddle')
    m.flat_circle(0, 0, 40, fall=12, level=0.0)
    m.finish_terrain()

    m.refs((12, -55))
    m.topcam()
    a = math.radians(300)
    m.gamecam('GameCam_Plaza', (56 * math.cos(a), 56 * math.sin(a)), (0, 0))
    a = math.radians(240)
    m.gamecam('GameCam_Grove', (86 * math.cos(a), 86 * math.sin(a)), (gx, gy))
    return m


MAP = build()
