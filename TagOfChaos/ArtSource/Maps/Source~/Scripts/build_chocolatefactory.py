# ChocolateFactory blockout. Needs maplib namespace.
# Tank yard r<=34 (tank 55 m tall, visible everywhere) | conveyor loop +-55 | ring road +-100
# outer quadrants: NE production hall, NW tank farm, SW machine rooms, SE warehouse | river x = -140
import math


def conveyor(m, p0, p1, gap_every=0):
    m.strip('CookieConveyor', 'GameplayProps', 'M_Metal_Dark', p0, p1, 2.6, h=1.0, road=False)
    m.strip('CookieConveyor_Belt', 'Decoration', 'M_Chocolate_Dark', p0, p1, 2.2, h=0.08, z=1.0, road=False)


def machine(m, x, y, rotz=0.0, h=5.5):
    nm = ('CandyMachine_Press', 'CandyMachine_Mixer')[int(abs(x * 3 + y)) % 2]
    place(m, nm, 'GameplayProps', x, y, rotz + (int(abs(x + y)) % 4) * math.pi / 2, h / 5.5)
    m.blockers.append(((x, y), 3.5))


def crate_stack(m, x, y, rnd):
    with m.at(x, y):
        n = rnd.randint(1, 3)
        for i in range(n):
            nm = rnd.choice(('CookieCrate_A', 'CookieCrate_B', 'ChocolateBarrel'))
            place(m, nm, 'GameplayProps', x + rnd.uniform(-0.2, 0.2), y, rnd.uniform(-0.3, 0.3),
                  z=i * (1.6 if nm == 'CookieCrate_A' else 1.35 if nm == 'CookieCrate_B' else 1.6))
            if nm == 'ChocolateBarrel':
                break


def build():
    m = Map('ChocolateFactory', 'M_Ground_Factory')
    rnd = random.Random(33)

    # ---- roads / floor markings ----
    m.cyl('TankYard_Floor', 'Ground', 'M_Floor_Chocolate', (0, 0, 0), 34, 0.04, seg=64)
    m.blockers.append(((0, 0), 34))
    for a in range(4):
        dx, dy = rot2(1, 0, a * math.pi / 2)
        m.strip('FactoryRoad', 'Ground', 'M_Cobblestone', (34 * dx, 34 * dy), (172 * dx, 172 * dy), 12)
    for s in (1, -1):
        m.strip('RingRoad', 'Ground', 'M_Cobblestone', (-105, s * 100), (105, s * 100), 10)
        m.strip('RingRoad', 'Ground', 'M_Cobblestone', (s * 100, -95), (s * 100, 95), 10)

    # ---- Landmark: giant chocolate tank ----
    def _tank(g, rnd):
        R = 14
        g.lathe([(0, 0), (R + 1.2, 0), (R + 1.2, 1.5), (R, 2.0), (R, 46), (0, 46)], 'M_Chocolate_Milk', seg=48,
                radial=lambda a: 1 + 0.025 * math.cos(24 * a))                          # ribbed chocolate body
        g.lathe([(R + 0.3, 45.5), (R + 1.1, 46.5), (R * 0.85, 50), (R * 0.5, 53), (0, 54)], 'M_Candy_Pink', seg=48)
        for k in range(16):                                                              # cream drips over the rim
            a = 2 * math.pi * k / 16
            L = 1.5 + 1.8 * abs(math.sin(k * 2.3))
            g.tube([((R + 0.9) * math.cos(a), (R + 0.9) * math.sin(a), 46.8),
                    ((R + 0.9) * math.cos(a), (R + 0.9) * math.sin(a), 46.8 - L)], [0.9, 0.6], 'M_Sugar_White', seg=8)
        g.tube(arc((0, 0, 46.6), R + 0.8, 0, 2 * math.pi, 48, plane='xy'), 0.8, 'M_Sugar_White', seg=8)
        for z in (6, 18, 30, 40):
            g.tube(arc((0, 0, z), R + 0.35, 0, 2 * math.pi, 48, plane='xy'), 0.45, 'M_Metal_Dark', seg=6)
        g.tube(arc((0, 0, 12), R + 0.5, 0, 2 * math.pi, 48, plane='xy'), 0.6, 'M_Warning_Purple', seg=6)
        g.tube(arc((0, 0, 24), R + 0.5, 0, 2 * math.pi, 48, plane='xy'), 0.5, 'M_Magic_Rune', seg=6)
        # catwalk ring at 26 m with rail
        g.lathe([(R, 25.6), (R + 3.2, 25.6), (R + 3.2, 26.0), (R, 26.0)], 'M_Metal_Light', seg=48, cap=False)
        g.tube(arc((0, 0, 27.3), R + 3.0, 0, 2 * math.pi, 48, plane='xy'), 0.12, 'M_Metal_Dark', seg=5)
        # spout pouring a chocolate stream into the outflow pool (south side)
        g.tube([(0, -R + 0.5, 8), (0, -R - 2.5, 8), (0, -R - 4.0, 6.5)], 1.6, 'M_Metal_Dark', seg=12)
        g.tube(bend((0, -R - 4.3, 6.2), (0, -R - 6.0, 0.1), 0.6, 8, side=(0, -1, 0)), [1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6,
               1.8, 2.2], 'M_Water_Chocolate', seg=10)
        g.lathe([(0, 0), (6, 0.02), (6.2, 0.15), (0, 0.18)], 'M_Water_Chocolate', seg=32, loc=(0, -R - 6.5, 0))
        # ladder
        for z in range(2, 26, 2):
            g.tube([(R + 0.25, -0.8, z), (R + 0.25, 0.8, z)], 0.12, 'M_Metal_Dark', seg=5)
        for yy in (-0.8, 0.8):
            g.tube([(R + 0.25, yy, 0), (R + 0.25, yy, 26)], 0.14, 'M_Metal_Dark', seg=5)
    ASSETS['Landmark_ChocolateTank'] = _tank
    place(m, 'Landmark_ChocolateTank', 'MainStructures', 0, 0, 0)

    # striped candy steam-stack on the dome: pokes far above the 22 m hall walls so the tank reads from the yards
    def _spire(g, rnd):
        g.tube([(0, 0, 53), (0, 0, 88)], [2.6, 1.6], 'M_Candy_Purple', seg=16, ringmat=stripes('M_Candy_Purple', 'M_Candy_White', 3))
        g.lathe([(3.2, 87.5), (3.4, 89), (2.0, 90.5), (0, 90.6)], 'M_Chocolate_Dark', seg=16)
        g.blob('M_Magic_Gem', (0, 0, 93), 2.8, seg=18)
        for z in (62, 74):
            g.tube(arc((0, 0, z), 2.4 - (z - 53) * 0.028, 0, 2 * math.pi, 24, plane='xy'), 0.5, 'M_Warning_Purple', seg=6)
    ASSETS['Landmark_ChocolateTank_Spire'] = _spire
    place(m, 'Landmark_ChocolateTank_Spire', 'MainStructures', 0, 0, 0)['no_pad'] = True
    m.empty('FX_TankSteam', 'Effects', (0, 0, 91), 6)

    # ---- main factory hall: everything inside the ring road is indoors (walls 22 m, glass roof) ----
    HW, HH = 93, 22
    for p0, p1 in (((-HW, -HW), (HW, -HW)), ((HW, -HW), (HW, HW)), ((HW, HW), (-HW, HW)), ((-HW, HW), (-HW, -HW))):
        # doorways sized for MonsterPlayer (>= 7 m wide / high): 14 m main + two 8 m side doors, 9 m lintel
        wall(m, 'MainHall_Wall', p0, p1, HH, 1.5, [(HW, 14), (HW - 50, 8), (HW + 50, 8)], mat='M_Brick_Oven', gap_h=9)
        m.roads.append((Vector((*p0, 0)), Vector((*p1, 0)), 1.5))   # keep scatter off the walls
    # arched glass vault over the hall (reference: glass arcade roof) + iron ribs + tie beams
    def _vault(g, rnd):
        bm = g.bm
        n = 40
        rows = []
        for i in range(n + 1):
            t = math.pi * i / n
            y, z = HW * math.cos(t), 17 * math.sin(t)
            rows.append([bm.verts.new((x, y, z)) for x in (-HW, HW)])
        for i in range(n):
            f = bm.faces.new((rows[i][0], rows[i][1], rows[i + 1][1], rows[i + 1][0]))
            f.material_index = g.mi('M_Glass_Roof')
    ASSETS['FactoryGlassVault'] = _vault
    ob = place(m, 'FactoryGlassVault', 'MainStructures', 0, 0, 0, z=HH)
    ob['hide_top'] = True
    ob['no_pad'] = True
    for t in range(-80, 81, 20):
        m.box('MainHall_TieBeam', 'MainStructures', 'M_Metal_Dark', (t, 0, HH - 1.2), (1.2, 2 * HW, 1.2), tags=('hide_top',))
    ASSETS['FactoryRoofArch'] = lambda g, rnd: g.tube(
        [(0, HW * math.cos(t), 17.1 * math.sin(t)) for t in [math.pi * i / 40 for i in range(41)]], 0.8, 'M_Metal_Dark', seg=8)
    for t in range(-88, 89, 11):
        ob = place(m, 'FactoryRoofArch', 'MainStructures', t, 0, 0, z=HH)
        ob['hide_top'] = True
        ob['no_pad'] = True
    # tall arched windows glowing on the inner brick walls (reference)
    def _archwin(g, rnd):
        g.rbox('M_Chocolate_Dark', (0, 0, 0), (5.2, 0.5, 9.0), 0.15)
        g.rbox('M_Window_Yellow', (0, -0.2, 0.4), (4.4, 0.3, 8.2), 0.1)
        g.tube(arc((0, -0.35, 9.0), 2.6, 0, math.pi, 12), 0.35, 'M_Chocolate_Dark', seg=6)
        g.lathe([(0, 0), (2.2, 0), (0, 0.02)], 'M_Window_Yellow', seg=16, loc=(0, -0.2, 9.0), rot=(math.pi / 2, 0, 0),
                cap=False)
        for x in (-1.1, 1.1):
            g.rbox('M_Chocolate_Dark', (x, -0.35, 0.4), (0.25, 0.2, 8.2), 0.05)
        g.rbox('M_Chocolate_Dark', (0, -0.35, 4.4), (4.4, 0.2, 0.25), 0.05)
    ASSETS['ArchWindow'] = _archwin
    for s, rz in ((1, 0.0), (-1, math.pi)):          # asset front is -Y: face into the hall
        for t in range(-84, 85, 12):
            if abs(t) < 9 or abs(abs(t) - 50) < 6:
                continue            # keep the doorways clear
            place(m, 'ArchWindow', 'Decoration', t, s * (HW - 0.8), rz, z=6.0)['no_pad'] = True
            place(m, 'ArchWindow', 'Decoration', s * (HW - 0.8), t, -s * math.pi / 2, z=6.0)['no_pad'] = True

    # ---- chocolate river ring inside the hall (painted walkable chocolate floor) + cream banks ----
    m.path_ring('ChocolateRiver_Hall', 'Ground', 'M_Water_Chocolate', (0, 0), 44, 9)
    rb = random.Random(5)
    for k in range(28):
        a = 2 * math.pi * k / 28 + 0.05
        if k % 7 == 0:
            continue                                            # gaps so the river stays crossable everywhere
        for rr in (38.8, 49.2):
            place(m, 'CreamBank', 'Decoration', rr * math.cos(a), rr * math.sin(a), a + math.pi / 2,
                  rb.uniform(0.7, 0.9))
    # reference props inside the hall
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        place(m, 'CandyPipe_Twist', 'MainStructures', 70 * math.cos(a), 70 * math.sin(a), a, 1.0)
    for k, a in enumerate((0.0, math.pi / 2, math.pi, 1.5 * math.pi)):
        place(m, 'GingerArch', 'MainStructures', 34.5 * math.cos(a), 34.5 * math.sin(a), a + math.pi / 2, 1.0)
    for (x, y) in ((24, 24), (-24, 24), (24, -24), (-24, -24)):
        place(m, 'CakeTower', 'GameplayProps', x, y, 0, 1.0)
    for k in range(16):
        a = 2 * math.pi * k / 16 + math.pi / 16          # offset: the four aisle axes stay open towards the tank
        place(m, 'Lollipop_Rainbow', 'Decoration', 60 * math.cos(a), 60 * math.sin(a), a, rb.uniform(1.0, 1.6))
    m.cyl('ChocolateTank_Spout', 'MainStructures', 'M_Metal_Dark', (0, -14, 3), 1.6, 3, seg=16,
          rot=(math.pi / 2, 0, 0))
    m.cyl('ChocolateTank_OutflowPool', 'Terrain', 'M_Chocolate_Liquid', (0, -20, 0), 5, 0.05, seg=32)
    for k in range(6):   # legs / hide pillars around the tank
        a = math.radians(k * 60 + 30)
        m.box('ChocolateTank_Leg', 'MainStructures', 'M_Metal_Dark', (18 * math.cos(a), 18 * math.sin(a), 0),
              (2.5, 2.5, 10), a)
    m.blockers.append(((0, 0), 20))

    # ---- big pipes from the tank to the four corner buildings ----
    # run at 17 m: under the glass vault (22-39 m) and the tie beams (20.8 m), above the windows (<= 15.6 m);
    # they leave the hall through the corner walls and end on / in the building below
    PZ = 17.0
    for k, (tx, ty) in enumerate(((135, 135), (-135, 135), (-135, -135), (135, -135))):
        a = math.atan2(ty, tx)
        R_END = 152.0                     # stand-pipe in the open yard just in front of the corner building
        L = R_END - 14
        # cylinder axis +Z -> rot X 90 gives -Y -> rot Z (a + 90) points it along angle a
        m.cyl('ChocolatePipe_Main', 'MainStructures', 'M_Candy_Purple', (14 * math.cos(a), 14 * math.sin(a), PZ),
              2.2, L, seg=16, rot=(math.pi / 2, 0, a + math.pi / 2), tags=('no_pad',))
        for t in (0.3, 0.6, 0.9):
            m.torus('ChocolatePipe_Stripe', 'Decoration', 'M_Candy_White',
                    ((14 + L * t) * math.cos(a), (14 + L * t) * math.sin(a), PZ), 2.3, 0.35,
                    rot=(math.pi / 2, 0, a + math.pi / 2), seg=24, tseg=6)
        m.torus('ChocolatePipe_WallCollar', 'Decoration', 'M_Metal_Dark',
                (93 * math.sqrt(2) * math.cos(a), 93 * math.sqrt(2) * math.sin(a), PZ), 2.7, 0.5,
                rot=(math.pi / 2, 0, a + math.pi / 2), seg=24, tseg=6)
        # stand-pipe down to the ground outside the ring road (clear of every roof, wall and doorway)
        m.cyl('ChocolatePipe_Down', 'MainStructures', 'M_Candy_Purple', (R_END * math.cos(a), R_END * math.sin(a), 0),
              2.0, PZ + 1.2, seg=16, tags=('no_pad',))
        m.cyl('ChocolatePipe_Valve', 'Decoration', 'M_Metal_Dark', (R_END * math.cos(a), R_END * math.sin(a), 0),
              2.8, 1.2, seg=16, tags=('no_pad',))
        m.blockers.append(((R_END * math.cos(a), R_END * math.sin(a)), 4))

    # ---- conveyor loop around the yard (+-55) with 12 m road gaps and 4 m corner gaps ----
    for s in (1, -1):
        for a0, a1 in ((-51, -6), (6, 51)):
            conveyor(m, (a0, s * 55), (a1, s * 55))
            conveyor(m, (s * 55, a0), (s * 55, a1))

    # ---- inner quadrants (between 60 and 94): candy machines + raised platforms with ramps ----
    for sx in (1, -1):
        for sy in (1, -1):
            for (u, v) in ((66, 66), (88, 64), (62, 90)):
                machine(m, sx * u, sy * v)
            # platform 14 x 14 at h 3 + ramp 9 m (18.4 deg)
            px, py = sx * 82, sy * 82
            m.box('WorkPlatform', 'Terrain', 'M_Metal_Light', (px, py, 0), (14, 14, 3))
            m.wedge('WorkPlatform_Ramp', 'Terrain', 'M_Metal_Dark', (px - sx * 16, py, 0), 9, 4, 3,
                    rotz=0 if sx > 0 else math.pi)
            m.blockers.append(((px, py), 10))
            m.empty('FX_Steam', 'Effects', (sx * 68, sy * 68, 9), 2)

    # ---- NE: production hall (walls 16 m, doors 6 m) ----
    cx, cy, W = 138, 138, 58
    h = 16
    for p0, p1, gaps in (((cx - W / 2, cy - W / 2), (cx + W / 2, cy - W / 2), [(W / 2, 10)]),
                         ((cx + W / 2, cy - W / 2), (cx + W / 2, cy + W / 2), []),   # 5 m to the perimeter wall:
                         ((cx + W / 2, cy + W / 2), (cx - W / 2, cy + W / 2), []),   # no doors on these sides
                         ((cx - W / 2, cy + W / 2), (cx - W / 2, cy - W / 2), [(W / 2, 10)])):
        wall(m, 'ProductionHall_Wall', p0, p1, h, 1.2, gaps, mat='M_Brick_Oven', gap_h=8.5)
    m.box('ProductionHall_Roof', 'MainStructures', 'M_Glass_Roof', (cx, cy, h), (W + 1, W + 1, 0.6), tags=('hide_top',))
    for i, y in enumerate((cy - 16, cy, cy + 16)):
        conveyor(m, (cx - 20, y), (cx + 12, y))
        machine(m, cx + 20, y + (4 if i != 1 else 0))
    m.blockers.append(((cx, cy), 30))
    m.light('ProductionHall_Light', 'POINT', (cx, cy, 13), (1.0, 0.6, 0.3), 50000, 10)

    # ---- NW: tank farm ----
    for (u, v) in ((118, 118), (152, 118), (118, 152), (152, 152)):
        x, y = -u, v
        m.cyl('SmallTank', 'MainStructures', 'M_Metal_Light', (x, y, 0), 8, 26, seg=32)
        m.sphere('SmallTank_Dome', 'MainStructures', 'M_Chocolate_Milk', (x, y, 26), 8, sz=(1, 1, 0.5), seg=24)
        m.torus('SmallTank_Rune', 'Lighting', 'M_Magic_Rune', (x, y, 12), 8.2, 0.3)
        m.blockers.append(((x, y), 9))
    m.strip('TankFarm_Catwalk', 'MainStructures', 'M_Metal_Dark', (-118, 135), (-152, 135), 3, h=0.4, z=12, road=False)

    # ---- SW: machine rooms (3 small buildings, 4 m alleys) ----
    for (u, v, w, d) in ((120, 120, 22, 18), (150, 124, 16, 26), (125, 150, 26, 14), (156, 156, 14, 14)):
        x, y = -u, -v
        for p0, p1, gaps in (((x - w / 2, y - d / 2), (x + w / 2, y - d / 2), [(w / 2, 7.5)]),
                             ((x + w / 2, y - d / 2), (x + w / 2, y + d / 2), [(d / 2, 7.5)]),
                             ((x + w / 2, y + d / 2), (x - w / 2, y + d / 2), []),
                             ((x - w / 2, y + d / 2), (x - w / 2, y - d / 2), [])):
            wall(m, 'MachineRoom_Wall', p0, p1, 9, 0.8, gaps, mat='M_Brick_Oven', gap_h=7.5)
        m.box('MachineRoom_Roof', 'MainStructures', 'M_Metal_Dark', (x, y, 9), (w + 0.8, d + 0.8, 0.5), tags=('hide_top',))
        # machine in the back corner (doors are on the -y and +x walls) so the monster lane stays open
        if min(w, d) >= 16:
            machine(m, x - w / 2 + 3.0, y + d / 2 - 3.0, h=4)
        else:
            crate_stack(m, x - w / 2 + 1.6, y + d / 2 - 1.6, rnd)
        m.blockers.append(((x, y), max(w, d) / 2 + 1))

    # ---- SE: warehouse + crate yard ----
    wx, wy, ww, wd = 145, -118, 52, 30
    for p0, p1, gaps in (((wx - ww / 2, wy - wd / 2), (wx + ww / 2, wy - wd / 2), [(ww / 2, 8)]),
                         ((wx + ww / 2, wy - wd / 2), (wx + ww / 2, wy + wd / 2), []),
                         ((wx + ww / 2, wy + wd / 2), (wx - ww / 2, wy + wd / 2), [(12, 8), (40, 8)]),
                         ((wx - ww / 2, wy + wd / 2), (wx - ww / 2, wy - wd / 2), [(wd / 2, 8)])):
        wall(m, 'Warehouse_Wall', p0, p1, 12, 1.0, gaps, mat='M_Wood_Dark', gap_h=8)
    m.box('Warehouse_Roof', 'MainStructures', 'M_Chocolate_Dark', (wx, wy, 12), (ww + 1, wd + 1, 0.5), tags=('hide_top',))
    for i in range(4):
        for j in (0, 2):                   # middle row left open: 12 m aisle from the west door (monster lane)
            crate_stack(m, wx - 18 + i * 12, wy - 8 + j * 8, rnd)
    m.blockers.append(((wx, wy), 30))
    m.scatter(lambda r: (r.uniform(112, 170), r.uniform(-170, -140)), 22, 5, 2, lambda x, y, r: crate_stack(m, x, y, r), seed=8)

    # ---- chocolate river (west, x = -140 from y -95..95 region between hall zones) ----
    m.channel([(-140, -100), (-140, 100)], 1.0, 2.5, 7.5, water='M_Water_Chocolate', wd=0.5, name='ChocolateRiver')
    m.blockers.append(((-140, 0), 5))
    for y in (-60, 0, 60):
        with m.at(-140 + 9, y):
            m.box('RiverBridge', 'MainStructures', 'M_Metal_Dark', (-140, y, 0), (17, 6 if y else 12, 0.35), tags=('no_pad',))
    m.scatter(lambda r: (r.uniform(-170, -112), r.uniform(-90, 90)), 14, 12, 3,
              lambda x, y, r: abs(x + 140) > 8 and machine(m, x, y), seed=12)

    # ---- filler cover in the open yards: sheds, silos, pipe racks, crate piles ----
    m.blockers.append(((-135, 135), 28))
    m.blockers.append(((-138, -138), 34))

    def filler(x, y, r):
        k = r.random()
        rz = r.choice((0, math.pi / 2))
        if k < 0.35:
            w, d = r.uniform(10, 16), r.uniform(8, 12)
            m.box('StorageShed', 'MainStructures', r.choice(('M_Brick_Oven', 'M_Wood_Dark', 'M_Metal_Light')),
                  (x, y, 0), (w, d, r.uniform(5, 8)), rz)
            m.prism('StorageShed_Roof', 'MainStructures', 'M_Chocolate_Dark', (x, y, 8 if w > 13 else 6.5), w + 0.8, d + 0.8, 2.5, rz)
        elif k < 0.55:
            m.cyl('Silo', 'MainStructures', 'M_Metal_Light', (x, y, 0), r.uniform(3.5, 5), r.uniform(14, 22), seg=24)
        elif k < 0.8:
            L = r.uniform(12, 18)
            for s in (-1, 1):
                ox, oy = rot2(s * L / 2, 0, rz)
                m.box('PipeRack_Post', 'GameplayProps', 'M_Metal_Dark', (x + ox, y + oy, 0), (0.8, 0.8, 6))
            m.cyl('PipeRack_Pipe', 'MainStructures', 'M_Candy_Purple', (x - rot2(L / 2, 0, rz)[0], y - rot2(L / 2, 0, rz)[1], 6.5),
                  1.0, L, seg=12, rot=(math.pi / 2, 0, rz + math.pi / 2))
            m.box('PipeRack_Crate', 'GameplayProps', 'M_Wood_Light', (x, y, 0), (3, 3, 2.4), rz)
        else:
            for i in range(r.randint(3, 6)):
                crate_stack(m, x + r.uniform(-5, 5), y + r.uniform(-5, 5), r)
        m.blockers.append(((x, y), 8))

    m.scatter(lambda r: (r.uniform(-170, 170), r.uniform(-170, 170)), 70, 20, 4, filler, seed=17)
    m.scatter(lambda r: (r.uniform(-94, 94), r.uniform(-94, 94)), 16, 14, 3,
              lambda x, y, r: max(abs(x), abs(y)) > 58 and crate_stack(m, x, y, r), seed=18)

    # ---- chimneys (skyline) ----
    for (x, y) in ((165, 165), (-165, 165), (-165, -165), (165, -165), (110, 165), (-165, 110)):
        m.cyl('Chimney', 'MainStructures', 'M_Brick_Oven', (x, y, 0), 3, 45, r2=2.4, seg=16)
        m.empty('FX_ChimneySteam', 'Effects', (x, y, 47), 4)

    # ---- perimeter wall with gate ----
    for s in (1, -1):
        wall(m, 'FactoryWall', (-172, s * 172), (172, s * 172), 12, 1.5, [(172, 14)], mat='M_Brick_Oven')
        wall(m, 'FactoryWall', (s * 172, -172), (s * 172, 172), 12, 1.5, [(172, 14)], mat='M_Brick_Oven')

    # ---- lighting / fx ----
    m.light('Sky', 'SUN', (0, 0, 120), (1.0, 0.72, 0.5), 1.2, rot=(math.radians(50), 0, math.radians(20)))
    m.light('Tank_Glow', 'POINT', (0, -20, 12), (1.0, 0.55, 0.25), 60000, 10)
    m.light('Tank_Rune_Glow', 'POINT', (0, 0, 50), (0.7, 0.35, 1.0), 40000, 10)
    for sx in (1, -1):
        for sy in (1, -1):
            m.light('Yard_Light', 'POINT', (sx * 75, sy * 75, 14), (1.0, 0.6, 0.3), 30000, 6)
    m.empty('FX_TankSteam', 'Effects', (0, 0, 54), 6)

    # ---- terrain: indoor hall + building yards flat, rolling outer yards with chocolate spills ----
    m.noise_amp = 0.3
    m.flat_rect(0, 0, 95, 95, fall=10, level=0.0)                 # main hall floor
    m.flat_rect(138, 138, 31, 31, fall=8)                         # production hall
    m.flat_circle(-135, 135, 30, fall=8)                          # tank farm
    m.flat_rect(-138, -138, 32, 32, fall=8)                       # machine rooms
    m.flat_rect(145, -118, 28, 17, fall=8)                        # warehouse
    for s in (1, -1):                                             # perimeter wall footing
        m.flat_rect(0, s * 172, 175, 3, fall=14)
        m.flat_rect(s * 172, 0, 3, 175, fall=14)
    m.hill(133, 55, 2.5, 30, name='SlagHill_East')
    m.hill(55, 133, 2.8, 28, name='SlagHill_North')
    m.hill(-55, -133, 2.8, 28, name='SlagHill_South')
    m.hill(-118, -40, 2.0, 18, name='Hill_WestBank')
    m.bowl(150, -58, 1.5, 18, water='M_Water_Chocolate', name='ChocolateSpill_East')
    m.bowl(-58, 150, 1.4, 16, water='M_Water_Purple', name='WitchSludge_North')
    m.bowl(58, -150, 1.5, 20, name='DryPit_South')
    m.bowl(-162, -55, 1.3, 15, name='DryPit_West')

    # ---- density (reference 초콜릿공장: candy, cream and crates heaped along the chocolate river and aisles) ----
    pal = [('CookieCrate_A', 0.9, 1.2), ('CookieCrate_B', 0.9, 1.2), ('ChocolateBarrel', 0.9, 1.3),
           ('Lollipop_Rainbow', 0.8, 1.4), ('CandyRock_Choco', 0.5, 0.8), ('CandyJar_A', 1.0, 1.4),
           ('GumdropBush_B', 0.6, 0.9), ('Cupcake_Choco', 0.5, 0.8)]
    big = [('CakeTower', 0.6, 0.8), ('CandyMachine_Press', 0.9, 1.1), ('CandyMachine_Mixer', 0.9, 1.1),
           ('GiantCake', 0.8, 1.0)]
    m.dressed = dress_paths(m, pal, spacing=8.0, band=(0.8, 5.0), cluster=(1, 3), seed=33, big=big, big_every=5)
    m.finish_terrain()

    m.refs((6, -72))
    m.topcam()
    m.gamecam('GameCam_Road', (0, -75), (0, 0))
    m.gamecam('GameCam_Hall', (138, 116), (138, 150))
    return m


MAP = build()
