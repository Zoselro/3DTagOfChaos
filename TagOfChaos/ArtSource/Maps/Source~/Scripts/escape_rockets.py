# Escape-mode spy rockets, one look per map (EscapeVisualPlan.md §3.4). exec'd by escape_assets.py.
# Top-level objects per unit (Unity finds them by name directly under the prefab root):
#   Body               fuselage, nose, fins, window, slot housings (static)
#   Slot_00_Empty / Slot_00_Filled, Slot_01_Empty / Slot_01_Filled   what the slot looks like before / after a part
#                      (Unity shows one of the two; a toolbox also gives the Filled look). "Spin*" children turn.
#   Hatch              boarding door, origin = hinge (rotates around Z to open)
#   Flame              ignition flame, origin = nozzle (scaled 0 -> 1 when it ignites)
#   Board              where the spy stands to board
# Slots sit on the left (-X, Slot_00) and right (+X, Slot_01) sides; fins at 90/210/330 deg keep the front (-Y) clear.
import math, random

R_BODY = 0.9
FIN_ANGLES = (90, 210, 330)


def fin(g, mat, angle_deg, pts, thick=0.14, r0=R_BODY - 0.1):
    """Flat chunky fin: pts = [(radial offset, z), ...] outline, extruded thick, rotated to angle."""
    def build(gg):
        bm = gg.bm
        lo = [bm.verts.new((r0 + px, -thick / 2, pz)) for px, pz in pts]
        hi = [bm.verts.new((r0 + px, thick / 2, pz)) for px, pz in pts]
        mi = gg.mi(mat)
        f = bm.faces.new(lo); f.material_index = mi
        f = bm.faces.new(list(reversed(hi))); f.material_index = mi
        n = len(pts)
        for k in range(n):
            f = bm.faces.new((lo[k], lo[(k + 1) % n], hi[(k + 1) % n], hi[k])); f.material_index = mi
    placed(g, build, rot=(0, 0, math.radians(angle_deg)))


def side(angle_deg, r, z):
    a = math.radians(angle_deg)
    return (math.cos(a) * r, math.sin(a) * r, z)


# ---------------- doorway + cockpit (2026-10-03, Request1003bPlan.md §6) ----------------
# The fuselage used to be one closed solid, so opening the hatch only showed the same wall. cut_doorway() opens the hull
# behind the hatch (slightly smaller than the closed hatch, so it stays hidden while closed) and lines the cut with a
# double-sided jamb; cabin() adds an inward-facing room (walls, floor, ceiling) with a seat, a glowing console and a lamp
# so the open door shows a little cockpit. Unity code is unchanged: Cabin is a static part found by nobody.
def cut_doorway(g, half_w, z0, z1, wall, jamb_mat):
    """Call right after the body lathe, while g holds only the body. Door faces -Y; box |x| < half_w, z0 < z < z1."""
    bm = g.bm
    for co, no in (((-half_w, 0, 0), (1, 0, 0)), ((half_w, 0, 0), (1, 0, 0)), ((0, 0, z0), (0, 0, 1)), ((0, 0, z1), (0, 0, 1))):
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
    eps = 1e-3

    def in_box(c, pad):
        return abs(c.x) < half_w + pad and z0 - pad < c.z < z1 + pad and c.y < 0

    doomed = [f for f in bm.faces if in_box(f.calc_center_median(), -eps)]
    bmesh.ops.delete(bm, geom=doomed, context='FACES')
    rim = [e for e in bm.edges if len(e.link_faces) == 1 and all(in_box(v.co, eps) for v in e.verts)]
    mi = g.mi(jamb_mat)

    def inward(p):
        r = math.hypot(p.x, p.y)
        k = max(0.0, r - wall) / r if r > 1e-6 else 1.0
        return Vector((p.x * k, p.y * k, p.z))

    for e in rim:   # both windings: Unity draws one side only
        a, b = e.verts[0].co.copy(), e.verts[1].co.copy()
        for quad in ((a, b, inward(b), inward(a)), (inward(a), inward(b), b, a)):
            f = bm.faces.new([bm.verts.new(p) for p in quad])
            f.material_index = mi
            g.keep_winding.add(f)
    return len(doomed), len(rim)


def cabin(u, name, r_in, z0, z1, seats=1, parent=None):
    """Inward-facing room r_in x (z0..z1) behind the door: faintly glowing lilac walls/ceiling, floor and seats, seats facing the
    back-wall console (screen + buttons), lamp. Sizes scale with r_in (spy rocket ~0.78 m, cake rocket ~2 m)."""
    s = r_in / 0.78

    def build(g):
        walls = g.lathe([(r_in, z0), (r_in, z1)], 'ME_Cabin_Wall', seg=24, cap=False)
        wall_faces = list({f for v in walls for f in v.link_faces})
        bmesh.ops.reverse_faces(g.bm, faces=wall_faces)   # seen from inside (and invisible from outside, so the door is open)
        g.keep_winding.update(wall_faces)
        cyl(g, 'ME_Cabin_Floor', (0, 0, z0 - 0.06), r_in, 0.06, seg=24)     # floor (top faces up)
        cyl(g, 'ME_Cabin_Wall', (0, 0, z1), r_in, 0.06, seg=24)              # ceiling (bottom faces down)
        g.blob('ME_Glow_White', (0, 0, z1 - 0.06 * s), 0.09 * s, sz=(1, 1, 0.5), seg=10)   # lamp
        # console on the back wall (+Y), facing the door
        cy = r_in - 0.16 * s
        g.rbox('ME_Gray_Light', (0, cy, z0), (0.7 * s, 0.26 * s, 0.55 * s), bevel=0.04 * s)
        g.rbox('ME_Glow_Teal', (0, cy - 0.12 * s, z0 + 0.62 * s), (0.5 * s, 0.03 * s, 0.3 * s), bevel=0.01 * s)   # screen
        for k, m in enumerate(('ME_Glow_Orange', 'ME_Red_Button', 'ME_Glow_Teal')):
            g.blob(m, ((k - 1) * 0.16 * s, cy - 0.12 * s, z0 + 0.5 * s), 0.045 * s, seg=8)
        # seats in a row, facing the console
        for i in range(seats):
            x = (i - (seats - 1) / 2) * 0.62 * s
            sy = cy - 0.55 * s
            g.rbox('ME_Cabin_Seat', (x, sy, z0), (0.44 * s, 0.4 * s, 0.32 * s), bevel=0.06 * s)            # cushion
            g.rbox('ME_Cabin_Seat', (x, sy - 0.2 * s, z0), (0.44 * s, 0.1 * s, 0.85 * s), bevel=0.05 * s)  # backrest
    return mesh_obj(u, name, build, parent=parent)


def rocket_shell(u, body_mat, nose_mat, fin_mat, band_mat, stripe=None, window_mat='ME_Glow_Teal', extra=None):
    """Shared fuselage + hatch + flame + board; extra(g) adds map-specific decoration to the Body."""
    def body(g):
        segmat = (lambda k: stripe if (k // 3) % 2 == 0 else body_mat) if stripe else None
        g.lathe([(0, 0.55), (0.62, 0.55), (R_BODY, 1.1), (R_BODY + 0.05, 2.3), (R_BODY, 3.3)], body_mat, seg=24, segmat=segmat)
        cut_doorway(g, 0.36, 1.12, 2.24, 0.15, band_mat)   # behind the hatch (closed hatch: |x| < 0.42, z 1.02..2.32); jamb reaches the cabin wall
        g.lathe([(R_BODY, 3.3), (0.72, 4.0), (0.42, 4.55), (0.12, 4.85), (0, 4.9)], nose_mat, seg=24)
        g.blob(nose_mat, (0, 0, 4.9), 0.14, seg=10)
        torus(g, band_mat, (0, 0, 3.3), R_BODY + 0.01, 0.07)
        torus(g, band_mat, (0, 0, 1.1), R_BODY - 0.04, 0.07)
        g.lathe([(0, 0.1), (0.5, 0.1), (0.66, 0.6), (0, 0.6)], 'ME_Gray_Dark', seg=20)                   # nozzle
        for a in FIN_ANGLES:
            fin(g, fin_mat, a, [(0, 0.15), (0.75, -0.05), (0.85, 0.3), (0.55, 0.9), (0.05, 1.9)])
        # round window above the hatch
        g.blob(window_mat, side(270, R_BODY - 0.05, 3.0), 0.3, sz=(1, 0.4, 1), seg=16)
        torus(g, band_mat, side(270, R_BODY - 0.02, 3.0), 0.32, 0.06, rot=(math.pi / 2, 0, 0))
        # hatch frame
        g.rbox(band_mat, (0, -R_BODY - 0.02, 0.95), (1.0, 0.08, 0.12), bevel=0.03)
        g.rbox(band_mat, (0, -R_BODY - 0.02, 2.35), (1.0, 0.08, 0.12), bevel=0.03)
        # side slot housings (dark recess plates)
        for a in (180, 0):
            g.rbox('ME_Gray_Dark', side(a, R_BODY + 0.02, 1.35), (0.18, 0.7, 0.85), bevel=0.06, rotz=math.radians(a))
        if extra:
            extra(g)
    mesh_obj(u, 'Body', body)
    cabin(u, 'Cabin', R_BODY - 0.12, 1.12, 2.42)

    def hatch(g):   # door extends +X from the hinge at origin, faces -Y
        g.rbox(body_mat, (0.42, 0, 0), (0.84, 0.1, 1.3), bevel=0.06)
        g.rbox(band_mat, (0.72, -0.06, 0.55), (0.1, 0.06, 0.25), bevel=0.02)                          # handle
    mesh_obj(u, 'Hatch', hatch, loc=(-0.42, -R_BODY - 0.06, 1.02))

    def flame(g):   # hangs down from the nozzle (origin)
        g.lathe([(0, -1.4), (0.45, -0.35), (0.42, 0.0), (0, 0.0)], 'ME_Glow_Orange', seg=16)
        g.lathe([(0, -0.9), (0.25, -0.25), (0.22, 0.02), (0, 0.02)], 'ME_Glow_White', seg=12)
    mesh_obj(u, 'Flame', flame, loc=(0, 0, 0.12))
    empty(u, 'Board', (0, -R_BODY - 1.2, 0))


# ---------------- slot looks (each slot: one Empty object + one Filled object at the housing) ----------------
def slot_battery(u, idx, angle):
    x, y, z = side(angle, R_BODY + 0.13, 1.35)
    rz = math.radians(angle)

    def empty_look(g):   # open hatch flap + dark bay + red warning light
        g.rbox('ME_Gray_Dark', (0, 0, -0.32), (0.12, 0.5, 0.64), bevel=0.04)
        g.rbox('ME_Gray_Light', (0.25, -0.0, -0.36), (0.42, 0.5, 0.06), bevel=0.02, rot=(0, 0.3, 0))     # flap hanging open
        g.blob('ME_Red_Button', (0.04, 0, 0.42), 0.07, seg=10)
    mesh_obj(u, f'Slot_{idx:02d}_Empty', empty_look, loc=(x, y, z), rot=(0, 0, rz))

    def filled_look(g):  # closed hatch with the teal battery showing + teal "ok" light
        g.rbox('ME_Gray_Light', (0.02, 0, -0.32), (0.12, 0.52, 0.64), bevel=0.04)
        pill(g, 'ME_Teal', (0.09, 0, 0), 0.13, 0.3, seg=16)
        g.rbox('ME_Glow_Orange', (0.21, 0, -0.06), (0.03, 0.06, 0.12), bevel=0.01)
        g.blob('ME_Glow_Teal', (0.04, 0, 0.42), 0.07, seg=10)
    mesh_obj(u, f'Slot_{idx:02d}_Filled', filled_look, loc=(x, y, z), rot=(0, 0, rz))


def slot_seatbelt(u, idx, angle):
    x, y, z = side(angle, R_BODY + 0.13, 1.35)
    rz = math.radians(angle)

    def seat(g, filled):
        g.rbox('ME_Purple_Light', (0.12, 0, -0.38), (0.3, 0.55, 0.22), bevel=0.08)                     # seat cushion
        g.rbox('ME_Purple_Light', (0.0, 0, -0.18), (0.12, 0.55, 0.6), bevel=0.06)                       # backrest
        for yy in (-0.24, 0.24):
            g.rbox('ME_Gold', (0.14, yy, -0.2), (0.08, 0.06, 0.1), bevel=0.02)                          # anchors
        if filled:   # strap across the seat + buckle
            g.tube([(0.1, -0.24, 0.3), (0.2, -0.05, -0.02), (0.18, 0.24, -0.16)], 0.045, 'ME_Purple_Deep', seg=8)
            g.rbox('ME_Gold', (0.22, 0.02, -0.12), (0.08, 0.14, 0.1), bevel=0.02)
            g.blob('ME_Glow_Teal', (0.04, 0, 0.45), 0.06, seg=10)
        else:
            g.blob('ME_Red_Button', (0.04, 0, 0.45), 0.06, seg=10)
    mesh_obj(u, f'Slot_{idx:02d}_Empty', lambda g: seat(g, False), loc=(x, y, z), rot=(0, 0, rz))
    mesh_obj(u, f'Slot_{idx:02d}_Filled', lambda g: seat(g, True), loc=(x, y, z), rot=(0, 0, rz))


def slot_gear(u, idx, angle):
    x, y, z = side(angle, R_BODY + 0.13, 1.35)
    rz = math.radians(angle)

    def empty_look(g):   # bare axle stub + red light
        cyl(g, 'ME_Gray_Light', (0, 0, 0), 0.06, 0.2, seg=10, rot=(0, math.pi / 2, 0))
        g.blob('ME_Red_Button', (0.04, 0, 0.42), 0.07, seg=10)
    mesh_obj(u, f'Slot_{idx:02d}_Empty', empty_look, loc=(x, y, z), rot=(0, 0, rz))

    filled = empty(u, f'Slot_{idx:02d}_Filled', (x, y, z), rot=(0, 0, rz))

    def gear(g):
        disc_gear(g, 'ME_Gray_Dark', 0.2, 0.1, 6, rot=(0, math.pi / 2, 0), tooth=(0.15, 0.13))
        g.blob('ME_Glow_Teal', (0.05, 0, 0), 0.07, sz=(0.6, 1, 1), seg=10)
    spin = mesh_obj(u, 'SpinGear', gear, loc=(0.15, 0, 0), parent=filled)
    mesh_obj(u, 'OkLight', lambda g: g.blob('ME_Glow_Teal', (0, 0, 0), 0.07, seg=10), loc=(0.04, 0, 0.42), parent=filled)


def slot_oil(u, idx, angle):
    x, y, z = side(angle, R_BODY + 0.13, 1.35)
    rz = math.radians(angle)

    def tank(g, filled):
        cyl(g, 'ME_Gray_Dark', (0.12, 0, -0.45), 0.2, 0.08, seg=16)
        cyl(g, 'ME_Glass', (0.12, 0, -0.37), 0.18, 0.66, seg=16)
        cyl(g, 'ME_Gray_Dark', (0.12, 0, 0.29), 0.2, 0.08, seg=16)
        if filled:
            cyl(g, 'ME_Oil_Choco', (0.12, 0, -0.36), 0.16, 0.55, seg=16)
            g.blob('ME_Glow_Teal', (0.04, 0, 0.47), 0.06, seg=10)
        else:
            g.blob('ME_Red_Button', (0.04, 0, 0.47), 0.06, seg=10)
    mesh_obj(u, f'Slot_{idx:02d}_Empty', lambda g: tank(g, False), loc=(x, y, z), rot=(0, 0, rz))
    mesh_obj(u, f'Slot_{idx:02d}_Filled', lambda g: tank(g, True), loc=(x, y, z), rot=(0, 0, rz))


def slot_rune(u, idx, angle):
    x, y, z = side(angle, R_BODY + 0.13, 1.35)
    rz = math.radians(angle)

    def socket(g, filled):
        g.lathe([(0, 0), (0.3, 0), (0.32, 0.06), (0.24, 0.1), (0, 0.1)], 'ME_Gray_Light', seg=12,
                rot=(0, math.pi / 2, 0))
        if filled:
            star_prism(g, 'ME_Glow_Purple', (0.12, 0, 0), 0.2, 0.09, 0.04, points=4, rot=(0, 0, math.pi / 2))
        else:
            g.lathe([(0, 0.08), (0.18, 0.08), (0.18, 0.11), (0, 0.11)], 'ME_Gray_Dark', seg=12, rot=(0, math.pi / 2, 0))
    mesh_obj(u, f'Slot_{idx:02d}_Empty', lambda g: socket(g, False), loc=(x, y, z), rot=(0, 0, rz))
    mesh_obj(u, f'Slot_{idx:02d}_Filled', lambda g: socket(g, True), loc=(x, y, z), rot=(0, 0, rz))


def slot_cell(u, idx, angle):
    x, y, z = side(angle, R_BODY + 0.13, 1.35)
    rz = math.radians(angle)

    def capsule(g, filled):
        cyl(g, 'ME_Gray_Dark', (0.12, 0, -0.42), 0.17, 0.08, seg=16)
        pill(g, 'ME_Glass', (0.12, 0, 0), 0.15, 0.45, seg=16)
        cyl(g, 'ME_Gray_Dark', (0.12, 0, 0.34), 0.17, 0.08, seg=16)
        if filled:
            g.blob('ME_Glow_Orange', (0.12, 0, 0), 0.12, sz=(1, 1, 1.3), seg=14)
            g.blob('ME_Glow_Teal', (0.04, 0, 0.5), 0.06, seg=10)
        else:
            g.blob('ME_Red_Button', (0.04, 0, 0.5), 0.06, seg=10)
    mesh_obj(u, f'Slot_{idx:02d}_Empty', lambda g: capsule(g, False), loc=(x, y, z), rot=(0, 0, rz))
    mesh_obj(u, f'Slot_{idx:02d}_Filled', lambda g: capsule(g, True), loc=(x, y, z), rot=(0, 0, rz))


# ---------------- per-map rockets ----------------
def carnival_extra(g):      # ride rocket: neon stripes on the nose, bulbs around the band
    for k in range(12):
        a = 2 * math.pi * k / 12
        g.blob('ME_Glow_Orange', (math.cos(a) * (R_BODY + 0.06), math.sin(a) * (R_BODY + 0.06), 3.3), 0.06, seg=8)


def bakery_extra(g):        # rolling-pin rocket: chocolate drips + cream icing collar
    rnd = random.Random(5)
    torus(g, 'ME_Cream', (0, 0, 3.6), 0.86, 0.08)
    for k in range(9):
        a = 2 * math.pi * k / 9 + 0.2
        g.blob('ME_Oil_Choco', (math.cos(a) * 0.83, math.sin(a) * 0.83, 3.5 - rnd.uniform(0, 0.25)), 0.09, sz=(1, 1, 2.0), seg=8)


def factory_extra(g):       # chocolate bar rocket: raised chocolate squares
    for zz in (1.4, 1.9, 2.4, 2.9):
        for a in (40, 140, 220, 320):
            g.rbox('ME_Cookie_Dark', side(a, R_BODY - 0.02, zz), (0.12, 0.4, 0.4), bevel=0.06, rotz=math.radians(a))


def ginger_extra(g):        # cookie-stone rocket: white icing zigzag rings
    for zz in (1.6, 2.8):
        pts = [(math.cos(a) * (R_BODY + 0.05), math.sin(a) * (R_BODY + 0.05), zz + 0.08 * math.sin(a * 8))
               for a in [i * 2 * math.pi / 48 for i in range(49)]]
        g.tube(pts, 0.04, 'ME_Cream', seg=6, cap=False)


def candy_extra(g):         # lollipop rocket: swirl disc on the nose
    torus(g, 'ME_Cream', (0, 0, 4.0), 0.7, 0.06)
    torus(g, 'ME_Pink', (0, 0, 4.25), 0.55, 0.06)


ROCKETS = {
    'CursedCandyCarnival': (dict(body_mat='ME_Cream', nose_mat='ME_Purple_Deep', fin_mat='ME_Purple_Light',
                                 band_mat='ME_Purple_Deep', stripe='ME_Red_Button', extra=carnival_extra),
                            (slot_seatbelt, slot_battery)),
    'HauntedBakery': (dict(body_mat='ME_Cookie_Gold', nose_mat='ME_Cookie_Dark', fin_mat='ME_Purple_Deep',
                           band_mat='ME_Gray_Dark', window_mat='ME_Glow_Orange', extra=bakery_extra),
                      (slot_gear, slot_battery)),
    'ChocolateFactory': (dict(body_mat='ME_Oil_Choco', nose_mat='ME_Cookie_Dark', fin_mat='ME_Gold',
                              band_mat='ME_Gold', extra=factory_extra),
                         (slot_oil, slot_gear)),
    'GingerbreadVillage': (dict(body_mat='ME_Cookie_Gold', nose_mat='ME_Teal', fin_mat='ME_Cookie_Dark',
                                band_mat='ME_Purple_Deep', window_mat='ME_Glow_Purple', extra=ginger_extra),
                           (slot_rune, slot_rune)),
    'CandyForest': (dict(body_mat='ME_Pink', nose_mat='ME_Teal', fin_mat='ME_Teal', band_mat='ME_Purple_Deep',
                         stripe='ME_Cream', extra=candy_extra),
                    (slot_cell, slot_cell)),
}


def build_rocket(u, shell_kw, slots):
    rocket_shell(u, **shell_kw)
    slots[0](u, 0, 180)
    slots[1](u, 1, 0)


for _mp, (_kw, _slots) in ROCKETS.items():
    BUILDS.append(('SPY_Rocket_' + _mp, build_rocket, ('SPY_Rocket_' + _mp, _kw, _slots), {}))
