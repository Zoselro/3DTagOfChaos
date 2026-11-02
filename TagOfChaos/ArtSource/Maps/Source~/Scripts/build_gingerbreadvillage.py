# GingerbreadVillage blockout (cookie ruins village, EscapePlan.md P2). Needs maplib namespace.
# Plaza 60x60 + clock tower | cross main roads (12 m) | square ring road at +-100 (10 m)
# house lots on a 20 m grid (houses <= 15 m -> alleys >= 5 m) | chocolate stream y = -131 with bridges
import math


def build():
    m = Map('GingerbreadVillage', 'M_Ground_Village')
    rnd = random.Random(21)

    # ---- Ground: plaza & roads ----
    m.box('Plaza', 'Ground', 'M_Cookie_Light', (0, 0, 0), (60, 60, 0.04))
    m.blockers.append(((0, 0), 30))
    for a in range(4):
        dx, dy = rot2(1, 0, a * math.pi / 2)
        m.strip('MainRoad', 'Ground', 'M_Cookie_Stone', (30 * dx, 30 * dy), (172 * dx, 172 * dy), 12)
    for s in (1, -1):
        m.strip('RingRoad', 'Ground', 'M_Cookie_Stone', (-105, s * 100), (105, s * 100), 10)
        m.strip('RingRoad', 'Ground', 'M_Cookie_Stone', (s * 100, -95), (s * 100, 95), 10)
    for sx in (1, -1):
        for sy in (1, -1):
            m.cyl('SmallSquare', 'Ground', 'M_Cookie_Light', (sx * 100, sy * 100, 0), 13, 0.05, seg=32)
            m.blockers.append(((sx * 100, sy * 100), 13))

    # ---- Chocolate stream + bridges ----
    # stream bed carved into the terrain: 1.0 m deep, flat bed 5 m, bank-to-bank 15 m (bank slope ~17 deg max)
    m.channel([(-190, -131), (190, -131)], 1.0, 2.5, 7.5, water='M_Water_Chocolate', wd=0.5, name='ChocolateStream')
    m.blockers.append(((0, -131), 0))
    for x, w in ((0, 12), (-60, 4), (60, 4), (-140, 3), (140, 3)):
        with m.at(x, -131 + 9):          # deck rests on the bank height, spans the dip
            m.box('CookieBridge', 'MainStructures', 'M_Chocolate_Dark', (x, -131, 0), (w, 17, 0.35), tags=('no_pad',))
            for s in (1, -1):
                m.strip('CookieBridge_Rail', 'Decoration', 'M_Candy_Red', (x + s * (w / 2 + 0.2), -139.5),
                        (x + s * (w / 2 + 0.2), -122.5), 0.3, h=1.1, road=False)

    # ---- Landmark: clock tower ----
    # gingerbread clock tower (reference 과자마을 style: cookie walls, white icing trims, waffle roof with snow,
    # candy-cane corners, glowing windows). Clock faces at 17 m + rune band at 10 m read from afar.
    def _clocktower(g, rnd):
        # EscapeVisualPlan.md §5.5: the base is hollow with a 5 m door on the south (-Y) side; inside, the escape
        # model (escape_devices.py device_gingerbread) adds the round room and the spiral ramp down to the ruins.
        for x0, x1, y0, y1 in ((-7, 7, 5, 7), (-7, -5, -5, 5), (5, 7, -5, 5), (-7, -2.5, -7, -5), (2.5, 7, -7, -5)):
            g.rbox('M_Cookie_Stone', ((x0 + x1) / 2, (y0 + y1) / 2, 0), (x1 - x0, y1 - y0, 1.0), 0.3)   # plinth frame
        for x0, x1, y0, y1, z0, z1 in ((-6, 6, 5, 6, 1, 10), (-6, -5, -5, 5, 1, 10), (5, 6, -5, 5, 1, 10),
                                       (-6, -2.5, -6, -5, 1, 10), (2.5, 6, -6, -5, 1, 10), (-2.5, 2.5, -6, -5, 7, 10)):
            g.rbox('M_Cookie_Wall', ((x0 + x1) / 2, (y0 + y1) / 2, z0), (x1 - x0, y1 - y0, z1 - z0), 0.15)  # base walls
        g.tube([(-2.7, -6.15, 0.0), (-2.7, -6.15, 7.0)] + [(-2.7 * math.cos(math.pi * i / 10), -6.15, 7.0 + 0.8 * math.sin(math.pi * i / 10))
                for i in range(1, 10)] + [(2.7, -6.15, 7.0), (2.7, -6.15, 0.0)], 0.22, 'M_Sugar_White', seg=6)  # door icing
        g.rbox('M_Magic_Rune', (0, 0, 9.6), (12.6, 12.6, 0.9), 0.3)                      # glowing rune band
        g.rbox('M_Gingerbread_Dark', (0, 0, 10.4), (10, 10, 15), 0.45)                   # clock storey
        for k in range(4):
            a = k * math.pi / 2
            ca, sa = math.cos(a), math.sin(a)
            fx, fy = -sa * 5.15, -ca * 5.15
            face = (math.pi / 2, 0, -a)
            g.lathe([(0, -0.2), (4.2, -0.2), (4.3, 0.1), (0, 0.15)], 'M_Sugar_White', seg=36, loc=(fx, fy, 17.5), rot=face)
            g.lathe([(0, -0.1), (3.6, -0.1), (3.6, 0.25), (0, 0.3)], 'M_Window_Yellow', seg=36,
                    loc=(fx * 1.02, fy * 1.02, 17.5), rot=face)
            for h in range(12):                                                           # rune ticks
                t = 2 * math.pi * h / 12
                lx, lz = 3.1 * math.cos(t), 3.1 * math.sin(t)
                g.blob('M_Candy_Purple', (fx * 1.06 + ca * lx, fy * 1.06 - sa * lx, 17.5 + lz), 0.28, seg=8)
            g.tube([(fx * 1.08, fy * 1.08, 17.5), (fx * 1.08 + ca * 0.2, fy * 1.08 - sa * 0.2, 20.2)], 0.2,
                   'M_Chocolate_Dark', seg=6)
            g.tube([(fx * 1.08, fy * 1.08, 17.5), (fx * 1.08 + ca * 1.9, fy * 1.08 - sa * 1.9, 17.9)], 0.2,
                   'M_Chocolate_Dark', seg=6)
            # arched glowing windows on the base + icing arch (the south face has the door instead)
            if k != 0:
                g.rbox('M_Window_Yellow', (-sa * 6.05, -ca * 6.05, 3.0), (2.4, 0.2, 3.2), 0.1, rotz=-a)
                g.tube([(-sa * 6.1 + ca * x, -ca * 6.1 - sa * x, 6.3 + 1.1 * math.sin(math.pi * (x + 1.4) / 2.8))
                        for x in [i * 0.2 - 1.4 for i in range(15)]], 0.18, 'M_Sugar_White', seg=6)
            # wavy icing drips under the clock storey and under the roof
            for zz, r0 in ((10.3, 6.35), (25.3, 5.25)):
                g.tube([(-sa * r0 + ca * x, -ca * r0 - sa * x, zz - 0.25 * abs(math.sin(x * 1.4)))
                        for x in [i * 0.5 - r0 for i in range(int(r0 * 4) + 1)]], 0.28, 'M_Sugar_White', seg=6)
        for sx in (-1, 1):                                                                # candy-cane corners
            for sy in (-1, 1):
                g.tube([(sx * 6.2, sy * 6.2, 1.0), (sx * 6.2, sy * 6.2, 10.2)], 0.45, 'M_Candy_Red', seg=10,
                       ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
                g.tube([(sx * 5.2, sy * 5.2, 10.4), (sx * 5.2, sy * 5.2, 25.2)], 0.4, 'M_Candy_Red', seg=10,
                       ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
        g.rbox('M_Chocolate_Milk', (0, 0, 25.2), (12.5, 12.5, 0.8), 0.3)                  # balcony
        g.lathe([(8.2, 26.0), (6.5, 30), (3.5, 34.5), (0.8, 37.5), (0, 38)], 'M_Waffle', seg=4, rot=(0, 0, math.pi / 4),
                radial=lambda a: 1.0)                                                    # 4-sided waffle roof
        g.lathe([(7.4, 27.0), (6.3, 29.5), (5.0, 30.5), (0, 29.8)], 'M_Sugar_White', seg=4, rot=(0, 0, math.pi / 4),
                cap=False)                                                               # snow skirt
        g.lathe([(1.2, 37.5), (1.0, 38.5), (0.2, 44), (0, 44.2)], 'M_Candy_Purple', seg=12)
        g.blob('M_Magic_Gem', (0, 0, 45.6), 1.7, seg=16)
        for k in range(4):                                                               # gumdrops on roof corners
            a = k * math.pi / 2 + math.pi / 4
            g.lathe([(0.9, 0), (0.7, 0.8), (0, 1.1)], ('M_Glow_Pink', 'M_Glow_Cyan', 'M_Glow_Yellow', 'M_Glow_Pink')[k],
                    seg=12, loc=(7.6 * math.cos(a), 7.6 * math.sin(a), 26.0))
    ASSETS['Landmark_ClockTower'] = _clocktower
    place(m, 'Landmark_ClockTower', 'MainStructures', 0, 0, 0)
    m.blockers.append(((0, 0), 10))

    # ---- Plaza props: fountain, benches, shops, lamps ----
    fx, fy = 17, 17
    m.cyl('CookieFountain_Basin', 'GameplayProps', 'M_Cookie_Stone', (fx, fy, 0), 5.5, 0.9, seg=32)
    m.cyl('CookieFountain_Choco', 'Decoration', 'M_Chocolate_Liquid', (fx, fy, 0.9), 5.0, 0.05, seg=32)
    m.cyl('CookieFountain_Column', 'GameplayProps', 'M_Chocolate_Milk', (fx, fy, 0), 1.0, 3.5, seg=16)
    m.sphere('CookieFountain_Candy', 'Lighting', 'M_Magic_Gem', (fx, fy, 4.2), 1.0)
    for k in range(4):
        dx, dy = rot2(0, -8, k * math.pi / 2 + math.pi / 4)
        m.box('CookieBench', 'GameplayProps', 'M_Cookie_Light', (fx + dx, fy + dy, 0), (3, 0.9, 0.9),
              k * math.pi / 2 + math.pi / 4)
    for sx, sy in ((-17, 17), (-17, -17), (17, -17)):
        m.box('Planter', 'GameplayProps', 'M_Cookie_Stone', (sx, sy, 0), (6, 6, 0.9))
        candy_tree(m, 'Decoration', sx, sy, 5, 2.5, 1, rnd)
    for x in (-27, -9, 9, 27):
        for y in (-27, 27):
            lamp(m, x, y, light=(x in (-9, 9)))
            lamp(m, y, x)

    # ---- House lots ----
    # rows are staggered sideways (+-3 m per row) so the north-south alleys zig-zag instead of running
    # dead straight; within a row both neighbours shift together, so the alley width is kept.
    # max rotated half-extent 7.1 m + jitter 1.5 m on a 20 m pitch -> alleys >= 2.8 m
    axis = [40, 60, 80, 120, 140, 160]
    stagger = {40: 0, 60: 3, 80: -2, 120: 2, 140: -3, 160: 1}
    lots = []
    for sx in (1, -1):
        for sy in (1, -1):
            for ax in axis:
                for ay in axis:
                    x, y = sx * (ax + stagger[ay]), sy * ay
                    if y < 0 and -145 < y < -118:           # stream corridor
                        continue
                    lots.append((x, y))
    shops = {(40, 40), (-40, -40), (43, -60)}
    POND_LOTS = {(-122, 120): 'M_Water_Purple', (143, -60): 'M_Water_Teal', (-61, -160): '',
                 (-83, 60): '', (157, 140): 'M_Water_Chocolate'}
    ruin_rnd = random.Random(77)     # own stream: the ruin choice must not shift the other lots' random layout
    for x, y in lots:
        k = rnd.random()
        # facing: door toward the nearest main road / ring road
        if abs(x) < abs(y):
            rotz = 0.0 if x > 0 else math.pi
            rotz = -math.pi / 2 if x > 0 else math.pi / 2
        else:
            rotz = 0.0 if y > 0 else math.pi
        if (x, y) in shops:
            w, d, h = 14, 12, 7
            house(m, x, y, w, d, h, rotz, roof='M_Icing_Pink', wall='M_Cookie_Light')
            dx, dy = rot2(0, -d / 2 - 1.2, rotz)
            m.box('CandyShop_Awning', 'Decoration', 'M_Candy_Red', (x + dx, y + dy, 3.4), (w, 2.4, 0.3), rotz)
            lollipop(m, 'Decoration', x + dx * 1.3, y + dy * 1.3, 6, 1.6, face=rotz)
            continue
        pond = POND_LOTS.get((round(x), round(y)))
        if pond is not None:          # shallow pond lot (wet or dry dip) with a low fence
            m.bowl(x, y, 1.5, 13, water=pond or None, wd=0.6, name='PondLot')
            fence_line(m, (x - 9, y - 9), (x - 2, y - 9))
            fence_line(m, (x + 2, y - 9), (x + 9, y - 9))
            m.blockers.append(((x, y), 9))
            continue
        if k < 0.16:   # garden lot (hide spot)
            m.box('Garden', 'Terrain', 'M_Grass_Teal', (x, y, 0), (13, 13, 0.2))
            for p0, p1 in (((x - 6.5, y - 6.5), (x + 3.5, y - 6.5)), ((x + 6.5, y - 6.5), (x + 6.5, y + 6.5)),
                           ((x + 6.5, y + 6.5), (x - 6.5, y + 6.5)), ((x - 6.5, y + 6.5), (x - 6.5, y - 6.5))):
                fence_line(m, p0, p1)
            candy_tree(m, 'Decoration', x + 2, y + 2, 5, 2.2, 0, rnd)
            m.blockers.append(((x, y), 7))
            continue
        cursed = (x < 0 and y > 0 and rnd.random() < 0.6) or rnd.random() < 0.08
        w, d = rnd.uniform(8, 13), rnd.uniform(8, 12)
        h = rnd.choice((rnd.uniform(5, 7), rnd.uniform(7, 11)))      # mix of cottages and tall houses
        rotz += math.radians(rnd.uniform(-6, 6))
        # cookie ruins (EscapePlan.md P2): about two thirds of the ordinary houses have collapsed
        # EscapeVisualPlan.md §5.5 (decision Q3): the village above ground is whole again; the ruins moved underground
        ruined = ruin_rnd.random() < 0.0
        hx, hy = x + rnd.uniform(-1.5, 1.5), y + rnd.uniform(-1.5, 1.5)
        house(m, hx, hy, w, d, h, rotz,
              roof=rnd.choice(('M_Chocolate_Dark', 'M_Chocolate_Milk', 'M_Icing_Purple' if cursed else 'M_Icing_Pink')),
              wall=rnd.choice(('M_Cookie_Wall', 'M_Gingerbread_Dark', 'M_Cookie_Light')), cursed=cursed, ruined=ruined)
        if ruined:                    # fallen pieces in front of the house (decoration, no collider)
            dx, dy = rot2(ruin_rnd.uniform(-w / 3, w / 3), -d / 2 - 2.4, rotz)
            place(m, 'RuinRubble', 'Decoration', hx + dx, hy + dy, ruin_rnd.uniform(0, 6.28), ruin_rnd.uniform(0.8, 1.2))
        if cursed:
            m.empty('FX_CurseMist', 'Effects', (x, y, 1), 6)
        smoke = rnd.random() < 0.35
        if smoke and not ruined:
            m.empty('FX_ChimneySmoke', 'Effects', (x, y, h + 4), 2)

    # ---- small-square fountains at ring corners ----
    for sx in (1, -1):
        for sy in (1, -1):
            x, y = sx * 100, sy * 100
            m.cyl('SquareFountain', 'GameplayProps', 'M_Cookie_Stone', (x, y, 0), 3.5, 0.9, seg=24)
            m.cyl('SquareFountain_Candy', 'Decoration', 'M_Candy_Teal', (x, y, 0.9), 1.2, 2.5, r2=0.3, seg=12)

    # ---- street lamps along main + ring roads ----
    li = 0
    for a in range(4):
        for d in range(44, 170, 20):
            for s in (1, -1):
                x, y = rot2(d, s * 7.5, a * math.pi / 2)
                lamp(m, x, y, light=(li % 6 == 0))
                li += 1
    for s in (1, -1):
        for t in range(-90, 91, 30):
            if t == 0:
                continue                 # keep the main-road axis (sightline to the clock tower) clear
            lamp(m, t, s * 106.5)
            lamp(m, s * 106.5, t)

    # ---- outskirts fence + background ----
    for s in (1, -1):
        fence_line(m, (-171, s * 171), (-8, s * 171), h=1.5)
        fence_line(m, (8, s * 171), (171, s * 171), h=1.5)
        fence_line(m, (s * 171, -171), (s * 171, -8), h=1.5)
        fence_line(m, (s * 171, 8), (s * 171, 171), h=1.5)

    def bg(r):
        a = r.uniform(0, 2 * math.pi)
        d = r.uniform(185, 270)
        return d * math.cos(a), d * math.sin(a)
    rb = random.Random(4)
    for _ in range(120):
        x, y = bg(rb)
        if abs(x) < 180 and abs(y) < 180:
            continue
        # reference: giant cream-puff trees with chocolate chips and huge cupcakes around the village
        nm = rb.choice(('CreamPuffTree_B', 'CreamPuffTree_A', 'Cupcake_Choco', 'Cupcake_Pink', 'Lollipop_Red'))
        place(m, nm, 'Background', x, y, rb.uniform(0, 6.28), rb.uniform(2.0, 3.2) if 'Cupcake' in nm else rb.uniform(1.6, 2.4))

    # ---- reference props: glowing gumdrops, red lollipops, canes, cupcakes along streets & gardens ----
    def street_prop(x, y, r):
        nm = r.choice(('GlowDrop_Pink', 'GlowDrop_Yellow', 'GlowDrop_Cyan', 'GlowDrop_Pink', 'Lollipop_Red',
                       'CandyCane_Red', 'Cupcake_Choco', 'Cupcake_Pink', 'GumdropBush_A', 'CandyFlower_Patch'))
        place(m, nm, 'Decoration' if 'Glow' in nm or 'Flower' in nm else 'GameplayProps', x, y, r.uniform(0, 6.28),
              r.uniform(0.9, 1.3))
    m.scatter(lambda r: (r.uniform(-168, 168), r.uniform(-168, 168)), 260, 7, 1.2, street_prop, seed=31)

    # ---- lighting / effects ----
    m.light('Moon', 'SUN', (0, 0, 120), (0.62, 0.70, 1.0), 1.2, rot=(math.radians(55), 0, math.radians(-30)))
    m.light('ClockTower_Glow', 'POINT', (0, 0, 26), (0.8, 0.45, 1.0), 40000, 5)
    m.light('Plaza_Warm', 'POINT', (0, -18, 10), (1.0, 0.6, 0.35), 30000, 8)
    m.empty('FX_MagicSparkles', 'Effects', (0, 0, 49), 4)
    m.empty('FX_FountainSplash', 'Effects', (fx, fy, 4), 2)

    # ---- density (reference 과자마을: glowing gumdrops, lollipops, canes and cupcakes crowd the paths) ----
    pal = [('GlowDrop_Pink', 0.6, 1.0), ('GlowDrop_Yellow', 0.6, 1.0), ('GlowDrop_Cyan', 0.6, 1.0),
           ('Lollipop_Red', 0.35, 0.6), ('CandyCane_Red', 0.8, 1.3), ('Cupcake_Choco', 0.5, 0.8),
           ('Cupcake_Pink', 0.5, 0.8), ('GumdropBush_A', 0.6, 0.9), ('CandyFlower_Patch', 0.8, 1.2)]
    big = [('Cupcake_Choco', 1.5, 2.0), ('Cupcake_Pink', 1.5, 2.0), ('CreamPuffTree_A', 0.8, 1.0),
           ('Lollipop_Red', 0.8, 1.0)]
    m.dressed = dress_paths(m, pal, spacing=7.5, band=(0.8, 5.0), cluster=(1, 3), seed=22, big=big, big_every=6)

    # ---- terrain: rolling town ground, cursed hill in the NW quarter, flat plaza/squares ----
    m.noise_amp = 0.35
    # houses stand on pads, so hills under the housing blocks stay low and wide (pad-to-pad steps stay small)
    m.hill(-140, 140, 4, 50, name='CursedHill_NW')
    m.hill(140, 60, 2.6, 44, name='Hill_East')
    m.hill(-60, -85, 2.2, 38, name='Hill_SouthWest')
    m.hill(120, -165, 2.5, 28, name='Hill_SouthEdge')
    m.hill(35, 150, 2.0, 30, name='Hill_North')
    m.bowl(-150, -60, 1.6, 20, name='DryHollow_West')
    m.flat_rect(0, 0, 30, 30, fall=12, level=0.0)
    for sx in (1, -1):
        for sy in (1, -1):
            m.flat_circle(sx * 100, sy * 100, 13, fall=10)
    m.finish_terrain()

    m.refs((6, -52))
    m.topcam()
    m.gamecam('GameCam_MainRoad', (0, -70), (0, 0))
    m.gamecam('GameCam_Alley', (-50, 70), (-50, 130))
    return m


MAP = build()
