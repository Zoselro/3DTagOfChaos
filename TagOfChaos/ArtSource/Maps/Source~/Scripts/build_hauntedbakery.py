# HauntedBakery blockout. Needs maplib namespace.
# Bakery building x -90..90, y -50..80 (walls 16 m): west storage wing | 3 m corridors | central workroom
# with the magic oven on the north wall | east shelf/shop wing. Backyard (north), market street (south),
# silos (east yard) and a dark wood (west yard).
import math

X0, X1, Y0, Y1, H = -90, 90, -50, 80, 16


def sack_pile(m, x, y, rnd):
    place(m, 'FlourSack_Pile', 'GameplayProps', x, y, rnd.uniform(0, 6.28), rnd.uniform(0.9, 1.3))


def shelf(m, x, y, rotz, cat='GameplayProps'):
    place(m, 'Shelf_Potion', cat, x, y, rotz, 1.25)


def cart(m, x, y, rotz):
    place(m, 'DeliveryCart', 'GameplayProps', x, y, rotz)
    m.blockers.append(((x, y), 4))


def build():
    m = Map('HauntedBakery', 'M_Ground_Bakery')
    rnd = random.Random(55)

    # ---- building floor + outer walls ----
    m.box('Bakery_Floor', 'Ground', 'M_Floor_Wood_Planks', (0, (Y0 + Y1) / 2, 0), (X1 - X0, Y1 - Y0, 0.04))
    # every doorway fits MonsterPlayer (>= 7 m wide, 8 m lintel). Side doors at x = +-56 open into the corridors.
    wall(m, 'Bakery_OuterWall', (X0, Y0), (X1, Y0), H, 1.5, [(90, 10), (34, 7), (146, 7)], mat='M_Brick_Oven', gap_h=8)
    wall(m, 'Bakery_OuterWall', (X1, Y0), (X1, Y1), H, 1.5, [(50, 8), (105, 8)], mat='M_Brick_Oven', gap_h=8)
    wall(m, 'Bakery_OuterWall', (X1, Y1), (X0, Y1), H, 1.5, [(34, 7), (146, 7)], mat='M_Brick_Oven', gap_h=8)
    wall(m, 'Bakery_OuterWall', (X0, Y1), (X0, Y0), H, 1.5, [(80, 8), (30, 8)], mat='M_Brick_Oven', gap_h=8)
    # round oven-glow windows at 10.5 m on the facade: readable from outside with the default camera
    for x in (-66, -22, 22, 66):
        for y, face in ((Y0 - 0.8, 0.0), (Y1 + 0.8, math.pi)):
            m.cyl('Bakery_GlowWindow', 'Lighting', 'M_Window_Glass_Orange', (x, y, 10.5), 2.6, 0.4, seg=24,
                  rot=(math.pi / 2, 0, face))
    for y in (-20, 50):
        for x, face in ((X0 - 0.8, math.pi / 2), (X1 + 0.8, -math.pi / 2)):
            m.cyl('Bakery_GlowWindow', 'Lighting', 'M_Window_Glass_Orange', (x, y, 10.5), 2.6, 0.4, seg=24,
                  rot=(math.pi / 2, 0, face))
    m.box('Bakery_Ceiling', 'MainStructures', 'M_Wood_Dark', (0, (Y0 + Y1) / 2, H), (X1 - X0 + 2, Y1 - Y0 + 2, 0.5),
          tags=('hide_top',))
    m.prism('Bakery_Roof', 'MainStructures', 'M_Chocolate_Dark', (0, (Y0 + Y1) / 2, H + 0.5), X1 - X0 + 6, Y1 - Y0 + 6, 22,
            tags=('hide_top',))

    # ---- partitions: west storage wing / corridors / east shop wing ----
    # corridors x -60..-52 and 52..60 -> 7 m clear between 1 m walls (MonsterPlayer arm span 6.3 m)
    wall(m, 'Storage_Wall', (-60, Y0), (-60, Y1), H, 1.0, [(25, 8), (100, 8)], mat='M_Wood_Dark', gap_h=8)
    wall(m, 'Corridor_Wall', (-52, Y0), (-52, Y1), H, 1.0, [(12, 8), (62, 8), (110, 8)], mat='M_Cookie_Wall', gap_h=8)
    wall(m, 'Storage_Divider', (X0, 15), (-60, 15), H, 1.0, [(15, 8)], mat='M_Wood_Dark', gap_h=8)
    wall(m, 'Shop_Wall', (60, Y0), (60, Y1), H, 1.0, [(30, 8), (95, 8)], mat='M_Wood_Dark', gap_h=8)
    wall(m, 'Corridor_Wall', (52, Y0), (52, Y1), H, 1.0, [(18, 8), (70, 8), (115, 8)], mat='M_Cookie_Wall', gap_h=8)
    wall(m, 'Shop_Divider', (60, 30), (X1, 30), H, 1.0, [(15, 8)], mat='M_Wood_Dark', gap_h=8)
    for yy in (Y0 + 4, Y1 - 4):            # corridor end markers only; the corridors stay clear of props
        m.blockers.append(((-56, yy), 4))
        m.blockers.append(((56, yy), 4))

    # ---- Landmark: magic oven (north wall of workroom) ----
    ox, oy = 0, 69
    # magic oven: brick beehive dome on a stone base, round cookie-shaped mouth with scalloped rim, glowing fire,
    # purple rune door as a SEPARATE mesh whose origin is the hinge (open it in Unity by rotating about Z)
    def _oven(g, rnd):
        g.rbox('M_Cookie_Stone', (0, 0, 0), (46, 24, 3.0), 0.6)
        g.lathe([(0, 3.0), (21, 3.0), (21.5, 6), (20, 12), (16, 18), (10, 22.5), (4, 24.5), (0, 25)], 'M_Brick_Oven',
                seg=40, loc=(0, 2, 0), scale=(1, 0.55, 1), radial=lambda a: 1 + 0.02 * math.cos(20 * a))
        for z in (8, 14, 19):                                           # brick courses
            r = {8: 20.9, 14: 18.2, 19: 13.5}[z]
            g.tube([(r * math.cos(t), 2 + 0.55 * r * math.sin(t), z) for t in [math.pi * i / 30 for i in range(31)]],
                   0.4, 'M_Brick_Wall', seg=6)
        c = (0, -9.8, 10.0)
        g.lathe([(0, -0.6), (9.6, -0.6), (10.2, 0.3), (0, 0.4)], 'M_Cookie_Light', seg=40, loc=c, rot=(math.pi / 2, 0, 0),
                radial=lambda a: 1 + 0.05 * abs(math.sin(8 * a)))        # cookie-shaped scalloped rim
        g.lathe([(0, 0.35), (8.2, 0.35), (0, 0.45)], 'M_Chocolate_Dark', seg=40, loc=c, rot=(math.pi / 2, 0, 0))
        g.lathe([(0, 0.2), (7.2, 0.3), (0, 0.5)], 'M_Oven_Fire', seg=32, loc=(0, -9.3, 9.2), rot=(math.pi / 2, 0, 0))
        for k in range(9):                                              # chocolate-chip dots on the rim
            a = 2 * math.pi * k / 9
            g.blob('M_Chocolate_Dark', (9.1 * math.cos(a), -10.5, 10 + 9.1 * math.sin(a)), 0.55, sz=(1, 0.5, 1), seg=8)
        g.tube(bend((8, 6, 20), (9.5, 6, 42), 1.5, 8), [3.4, 3.3, 3.2, 3.1, 3.0, 3.0, 3.1, 3.3, 3.6], 'M_Brick_Oven', seg=16)
        g.lathe([(4.0, 41.5), (4.2, 42.5), (3.2, 43.2), (0, 43.2)], 'M_Chocolate_Dark', seg=16, loc=(1.5, 0, 0))

    def _oven_door(g, rnd):                                             # origin = hinge (left edge of the mouth)
        g.lathe([(0, 0), (7.6, 0), (7.6, 0.6), (0, 0.6)], 'M_Wood_Dark', seg=32, loc=(7.8, 0, 0), rot=(math.pi / 2, 0, 0))
        g.tube(arc((7.8, -0.65, 0), 5.2, 0, 2 * math.pi, 32, plane='xz'), 0.35, 'M_Magic_Rune', seg=6)
        g.lathe([(0, 0), (1.2, 0), (0, 0.1)], 'M_Magic_Rune', seg=5, loc=(7.8, -0.7, 0), rot=(math.pi / 2, 0, 0))
        g.blob('M_Gold', (13.8, -0.8, 0), 0.6, seg=10)
    ASSETS['Landmark_MagicOven'] = _oven
    ASSETS['MagicOven_Door'] = _oven_door
    place(m, 'Landmark_MagicOven', 'MainStructures', ox, oy, 0)
    door = place(m, 'MagicOven_Door', 'MainStructures', ox - 7.8, oy - 11.0, -1.9, z=10.0)   # shown half open
    door['no_pad'] = True
    door['unity_note'] = 'Rotate about local Z (hinge). 0 = closed, -110 deg = open'
    m.blockers.append(((ox, oy), 22))

    # ---- workroom props ----
    def _dough_table(g, rnd, L=16, D=6):
        g.rbox('M_Wood_Light', (0, 0, 1.0), (L, D, 0.35), 0.12)
        for sx in (-1, 1):
            for sy in (-1, 1):
                g.lathe([(0.45, 0), (0.3, 0.5), (0.35, 1.0), (0, 1.0)], 'M_Wood_Dark', seg=10,
                        loc=(sx * (L / 2 - 0.8), sy * (D / 2 - 0.8), 0))
        g.lathe([(0, 1.35), (2.4, 1.35), (2.2, 1.7), (0, 1.8)], 'M_Cookie_Light', seg=24, loc=(-L * 0.2, 0, 0))   # dough
        g.tube([(L * 0.1, -1.2, 1.75), (L * 0.35, -1.2, 1.75)], 0.45, 'M_Wood_Dark', seg=12)                    # rolling pin
        for x in (L * 0.05, L * 0.4):
            g.tube([(x, -1.2, 1.75), (x + (0.8 if x > 1 else -0.8), -1.2, 1.75)], 0.18, 'M_Wood_Light', seg=8)
        for k, (x, y, sides) in enumerate(((L * 0.3, 1.3, 5), (L * 0.42, 1.2, 12), (-L * 0.42, 1.3, 3))):
            g.lathe([(0.9, 1.35), (0.9, 1.75), (0.8, 1.75), (0.8, 1.4)], 'M_Metal_Light', seg=sides, loc=(x, y, 0),
                    cap=False)                                                                                 # cookie cutters
        for k in range(4):                                                                                     # baked cookies
            g.lathe([(0, 1.35), (0.7, 1.35), (0.7, 1.5), (0, 1.55)], 'M_Cookie_Wall', seg=12,
                    loc=(-L * 0.42 + k * 1.7, -1.6, 0))

    def _mixing_bowl(g, rnd):
        g.lathe([(0, 0), (1.4, 0.05), (2.6, 1.4), (2.9, 2.2), (2.7, 2.25), (2.4, 1.5), (1.3, 0.35), (0, 0.3)],
                'M_Metal_Light', seg=28)
        g.lathe([(0, 1.6), (2.3, 1.6), (0, 1.62)], 'M_Cookie_Light', seg=28, cap=False)
        g.tube(bend((0.3, 0, 1.6), (1.8, 0.5, 4.2), 0.3, 6), 0.18, 'M_Wood_Dark', seg=8)
    ASSETS['Bakery_DoughTable'] = _dough_table
    ASSETS['Bakery_DoughTable_Small'] = lambda g, rnd: _dough_table(g, rnd, 8, 4)
    ASSETS['Bakery_MixingBowl'] = _mixing_bowl
    for x in (-26, 0, 26):
        place(m, 'Bakery_DoughTable', 'GameplayProps', x, -8, 0)
        m.blockers.append(((x, -8), 9))
    for x in (-22, 22):
        place(m, 'Bakery_DoughTable_Small', 'GameplayProps', x, 22, 0)
    for x in (-30, 30):
        for y in (-32, 10, 42):
            m.box('Pillar', 'MainStructures', 'M_Wood_Dark', (x, y, 0), (2, 2, H))
            m.box('Pillar_Cap', 'MainStructures', 'M_Wood_Dark', (x, y, H - 3), (3, 3, 0.8))
    for k, (x, y) in enumerate(((-12, 36), (12, 36), (-40, -30), (40, -30))):
        place(m, 'Bakery_MixingBowl', 'GameplayProps', x, y, k * 1.3)
    # reference (마녀의 유령빵집): purple rug + counters with trays, candy baskets & jars, chalkboard menu
    place(m, 'StarRug', 'Decoration', 0, 12, 0, 2.2, z=0.1)
    for x in (-24, 24):
        place(m, 'Counter_Purple', 'GameplayProps', x, -30, 0, 1.3)
        place(m, 'CandyBasket', 'Decoration', x + 6, -33, 0, 1.4)
    for x in (-40, -20, 20, 40):
        place(m, 'Chalkboard_Awning', 'Decoration', x, 57.5, 0, 1.4)       # on the oven-wall side
    for x, y in ((-44, 28), (44, 4)):      # clear of the corridor doorways (monster lanes)
        place(m, 'GiantCake', 'GameplayProps', x, y, 0, 1.0)
    for k in range(10):
        place(m, ('CandyJar_A', 'CandyJar_B')[k % 2], 'Decoration', -46 + k * 10.2, 51 + (k % 3), 0, 1.3)

    # ---- storage wing (dark): flour sacks + crates + barrels ----
    for (bx, by) in ((-66, -25), (-66, 50), (-75, 15), (-84, 0), (-84, 50)):   # keep door approaches clear (monster)
        m.blockers.append(((bx, by), 6))
    m.scatter(lambda r: (r.uniform(-88, -63), r.uniform(-48, 78)), 14, 7, 1.5, lambda x, y, r: sack_pile(m, x, y, r), seed=2)
    m.scatter(lambda r: (r.uniform(-88, -63), r.uniform(-48, 78)), 10, 6, 1.5,
              lambda x, y, r: place(m, r.choice(('CookieCrate_A', 'CookieCrate_B', 'ChocolateBarrel', 'Pumpkin')),
                                    'GameplayProps', x, y, r.uniform(0, 6.28), r.uniform(1.0, 1.3)), seed=3)

    # ---- shop wing (reference: display window with purple curtains, café tables, shelves of jars) ----
    for y in (-40, -28, -16, -4, 8, 20):
        shelf(m, 70, y, 0)
    for y in (54, 66, 76):                 # clear of the shop-wall door at y = 45 (monster lane)
        shelf(m, 68, y, math.pi / 2)
        shelf(m, 82, y, math.pi / 2)
    place(m, 'Counter_Purple', 'GameplayProps', 84, -30, math.pi / 2, 1.2)
    place(m, 'DisplayWindow', 'MainStructures', 72, Y0 + 1.5, 0, 1.3)
    place(m, 'DisplayWindow', 'MainStructures', -72, Y0 + 1.5, 0, 1.3)
    for (x, y) in ((64, -44), (80, -44)):
        place(m, 'Table_Round', 'GameplayProps', x, y + 20, 0, 1.4)
        for a in (0, 2.1, 4.2):
            place(m, 'Chair_Purple', 'GameplayProps', x + 2.6 * math.cos(a), y + 20 + 2.6 * math.sin(a), a + math.pi / 2, 1.3)
    place(m, 'StarRug', 'Decoration', 72, -24, 0, 1.2, z=0.1)
    for y in (-45, 5, 45):
        place(m, 'WallLantern_Post', 'Decoration', 88, y, math.pi, 1.2)
        place(m, 'WallLantern_Post', 'Decoration', -88, y, 0, 1.2)

    # ---- backyard (north): garden, carts, sheds, twisted trees ----
    m.strip('BackyardPath', 'Ground', 'M_Cookie_Stone', (-56, 82), (-56, 170), 7)
    m.strip('BackyardPath', 'Ground', 'M_Cookie_Stone', (56, 82), (56, 170), 7)
    m.strip('BackyardPath', 'Ground', 'M_Cookie_Stone', (-150, 125), (150, 125), 5)
    for (x, y) in ((-20, 105), (20, 105), (-20, 150), (20, 150), (-100, 150), (100, 150), (-100, 100), (100, 100)):
        m.box('GardenBed', 'Terrain', 'M_Grass_Teal', (x, y, 0), (22, 12, 0.25))
        for k in range(5):
            place(m, 'Pumpkin', 'Decoration', x - 8 + k * 4, y, k * 1.3, 1.1 + 0.2 * (k % 2), z=0.25)
        m.blockers.append(((x, y), 12))
    cart(m, -75, 110, 0.2)
    cart(m, 78, 138, -0.4)
    for (x, y, w, d) in ((-140, 100, 14, 10), (140, 150, 12, 12), (0, 165, 18, 8)):
        house(m, x, y, w, d, 6, 0, roof='M_Chocolate_Milk', wall='M_Wood_Light')
    m.scatter(lambda r: (r.uniform(-170, 170), r.uniform(84, 170)), 34, 11, 3,
              lambda x, y, r: twisted_tree(m, 'MainStructures', x, y, r.uniform(8, 14), r), seed=4)
    fence_line(m, (-172, 171), (172, 171), h=1.5, mat='M_Wood_Dark')

    # ---- front: market street & plaza (south) ----
    m.box('FrontPlaza', 'Ground', 'M_Cobblestone', (0, -80, 0), (120, 56, 0.04))
    m.strip('MarketStreet', 'Ground', 'M_Cobblestone', (-172, -110), (172, -110), 10)
    m.strip('FrontRoad', 'Ground', 'M_Cobblestone', (0, -108), (0, -172), 10)
    for x in range(-150, 151, 25):
        if abs(x) < 12:
            continue
        house(m, x, -150, 16, 12, rnd.uniform(6, 10), math.pi, roof=rnd.choice(('M_Chocolate_Dark', 'M_Icing_Purple')),
              wall=rnd.choice(('M_Cookie_Wall', 'M_Gingerbread_Dark')), cursed=rnd.random() < 0.3)
    for k in range(10):
        x = -50 + k * 11
        if abs(x) < 6:
            continue
        place(m, ('Booth_Neon_B', 'DeliveryCart', 'Booth_Neon_B', 'CandyBasket')[k % 4], 'GameplayProps', x, -92,
              0 if k % 4 != 1 else math.pi / 2, 1.0 if k % 4 != 3 else 2.0)
    m.blockers.append(((0, -80), 32))
    m.scatter(lambda r: (r.uniform(-170, 170), r.uniform(-172, -52)), 50, 8, 1.5,
              lambda x, y, r: place(m, r.choice(('Pumpkin', 'FlourSack_Pile', 'ChocolateBarrel', 'WitchSign_A',
                                                 'GumdropBush_B', 'CandyFlower_Patch')), 'Decoration', x, y,
                                    r.uniform(0, 6.28), r.uniform(0.9, 1.3)), seed=9)
    for x in range(-160, 161, 20):
        lamp(m, x, -103, light=(x % 60 == 0))

    # ---- side yards: east flour silos, west dark wood ----
    for (x, y) in ((115, 20), (135, 40), (155, 10), (125, -20), (150, -35)):
        m.cyl('FlourSilo', 'MainStructures', 'M_Sugar_White', (x, y, 0), 6, 24, seg=24)
        m.cyl('FlourSilo_Cap', 'MainStructures', 'M_Icing_Purple', (x, y, 24), 6.4, 4, r2=0.5, seg=24)
        m.blockers.append(((x, y), 7))
    m.scatter(lambda r: (r.uniform(-170, -96), r.uniform(-95, 80)), 40, 9, 3,
              lambda x, y, r: twisted_tree(m, 'MainStructures', x, y, r.uniform(10, 16), r), seed=5)
    m.scatter(lambda r: (r.uniform(98, 170), r.uniform(-95, 80)), 14, 10, 3,
              lambda x, y, r: sack_pile(m, x, y, r), seed=6)

    # ---- background ----
    rb = random.Random(8)
    for _ in range(120):
        a = rb.uniform(0, 2 * math.pi); d = rb.uniform(185, 280)
        x, y = d * math.cos(a), d * math.sin(a)
        if max(abs(x), abs(y)) < 180:
            continue
        if rb.random() < 0.5:
            twisted_tree(m, 'Background', x, y, rb.uniform(18, 28), rb)
        else:
            m.box('BG_TownHouse', 'Background', rb.choice(('M_Cookie_Wall', 'M_Gingerbread_Dark')), (x, y, 0),
                  (rb.uniform(10, 18), rb.uniform(10, 16), rb.uniform(10, 20)), rb.uniform(0, 3))

    # ---- lighting ----
    m.light('Moon', 'SUN', (0, 0, 120), (0.55, 0.62, 1.0), 0.9, rot=(math.radians(55), 0, math.radians(-40)))
    m.light('Oven_Fire', 'POINT', (ox, oy - 16, 7), (1.0, 0.45, 0.12), 200000, 8)
    for x in (-26, 0, 26):
        m.light('Workroom_Warm', 'POINT', (x, 5, 13), (1.0, 0.72, 0.45), 30000, 5)
    for y in (-20, 45):
        m.light('Storage_Dim', 'POINT', (-72, y, 10), (0.55, 0.3, 0.9), 4000, 4)
    m.light('Shop_Warm', 'POINT', (72, 0, 12), (1.0, 0.65, 0.4), 15000, 5)
    m.empty('FX_OvenMagic', 'Effects', (ox, oy - 16, 7), 6)
    m.empty('FX_OvenSmoke', 'Effects', (ox + 10, oy + 4, 52), 4)
    m.empty('FX_FlourDust', 'Effects', (-72, 0, 2), 6)
    m.empty('FX_ShadowMist', 'Effects', (-72, 60, 1), 6)

    # ---- interior finishing (reference 유령빵집: plank floor, timber beams, purple fabric, lots of furniture) ----
    def _planks(g, rnd):                                    # broad floorboards in two wood tones (low + readable)
        for i in range(int((X1 - X0) / 3)):
            x = X0 + 1.5 + i * 3
            g.rbox('M_Floor_Wood_Planks' if i % 2 else 'M_Wood_Light', (x, (Y0 + Y1) / 2, 0.0),
                   (2.9, Y1 - Y0 - 2, 0.06), 0.0)
    ASSETS['Bakery_FloorPlanks'] = _planks
    fp = place(m, 'Bakery_FloorPlanks', 'Decoration', 0, 0, 0, z=0.02)
    fp['no_pad'] = True

    def _beam_post(g, rnd):
        g.rbox('M_Wood_Dark', (0, 0, 0), (1.0, 1.0, H), 0.08)
        g.rbox('M_Wood_Dark', (0, 0, H - 2.5), (1.4, 1.4, 0.6), 0.08)
    ASSETS['Bakery_BeamPost'] = _beam_post
    for x in range(-84, 85, 12):
        for y in (Y0 + 1.2, Y1 - 1.2):
            if abs(x) < 8 or abs(abs(x) - 56) < 5 or abs(abs(x) - 60) < 1.5 or abs(abs(x) - 52) < 1.5:
                continue
            place(m, 'Bakery_BeamPost', 'MainStructures', x, y, 0)['no_pad'] = True
    for y in range(-40, 76, 12):
        if min(abs(y - g0) for g0 in (0, 50, 55)) < 5.5:
            continue                                            # side doors
        for x in (X0 + 1.2, X1 - 1.2):
            place(m, 'Bakery_BeamPost', 'MainStructures', x, y, 0)['no_pad'] = True
    for y in (Y0 + 1.2, Y1 - 1.2):
        m.box('Bakery_WallBeam', 'MainStructures', 'M_Wood_Dark', (0, y, H - 2.2), (X1 - X0 - 2, 1.0, 0.8))
    # café corners + rugs + baskets + pumpkins (workroom & shop)
    rc = random.Random(77)
    for (x, y) in ((-40, -40), (40, -40), (-38, 30), (38, 30), (-10, -42), (10, -42)):
        place(m, 'Table_Round', 'GameplayProps', x, y, 0, 1.4)
        for a in (0.3, 2.4, 4.5):
            place(m, 'Chair_Purple', 'GameplayProps', x + 2.6 * math.cos(a), y + 2.6 * math.sin(a), a + math.pi / 2, 1.3)
        place(m, 'CandyBasket', 'Decoration', x + 0.3, y, 0, 0.8, z=1.2)
    for (x, y, s) in ((-24, -44, 1.0), (24, -44, 1.0), (0, 40, 1.4)):
        place(m, 'StarRug', 'Decoration', x, y, rc.uniform(0, 1), s, z=0.1)
    for k in range(18):
        x, y = rc.uniform(-48, 48), rc.uniform(-46, 52)
        if m.clear_of_paths((x, y), 2) and abs(y + 8) > 6 and abs(x) > 4:
            place(m, rc.choice(('Pumpkin', 'CandyBasket', 'FlourSack_Pile', 'ChocolateBarrel', 'CandyJar_A')),
                  'Decoration', x, y, rc.uniform(0, 6.28), rc.uniform(0.9, 1.2))

    # ---- terrain: flat bakery + front plaza, garden hills behind, swamp in the west wood ----
    m.noise_amp = 0.4
    m.flat_rect(0, 15, 92, 67, fall=12, level=0.0)            # bakery building (indoor floor)
    m.flat_rect(0, -80, 60, 28, fall=10, level=0.0)           # front plaza
    m.hill(-120, 140, 4.0, 36, name='GardenHill_West')
    m.hill(122, 108, 3.2, 30, name='GardenHill_East')
    m.hill(-150, -85, 4.0, 34, name='WoodHill')
    m.hill(0, 158, 2.0, 22, name='GardenHill_Back')
    m.bowl(-135, -25, 2.0, 28, water='M_Water_Murky', plateau=3, name='WitchSwamp')
    m.bowl(-130, 45, 1.8, 22, name='DryHollow_Wood')
    m.bowl(152, 100, 1.5, 18, water='M_Water_Teal', name='GardenPond')
    m.bowl(140, -78, 1.4, 16, name='DryHollow_East')
    m.finish_terrain()

    m.refs((10, -30))
    m.topcam()
    m.gamecam('GameCam_Workroom', (0, -30), (0, 69))
    m.gamecam('GameCam_Corridor', (-53, -40), (-53, 40))
    return m


MAP = build()
