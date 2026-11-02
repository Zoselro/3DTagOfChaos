# Escape-mode parts and tools (EscapeVisualPlan.md §3.1). exec'd by escape_assets.py (same namespace).
# The same model is shown in the chest, on the ground (lifted 0.25 m), in the hands and in device/rocket slots.
# Two-handed parts are chunky ~0.5-0.6 m blobs, origin = centre between both hands, front = -Y.
# Rule: 2-4 big readable shapes per part, no tiny details; glow only where the part "works" (gems, glyphs, cores).
import math, random


def item(unit_id, build):
    mesh_obj('ITEM_' + unit_id, 'ITEM_' + unit_id, build)


def b_red_button(g):
    # big cookie base, purple bezel, puffy red dome, teal glow ring
    g.lathe([(0, -0.24), (0.27, -0.24), (0.29, -0.16), (0.25, -0.1), (0, -0.1)], 'ME_Cookie_Gold', seg=24)
    torus(g, 'ME_Purple_Deep', (0, 0, -0.1), 0.22, 0.045)
    g.lathe([(0, -0.1), (0.2, -0.1), (0.21, -0.02), (0.16, 0.08), (0.08, 0.13), (0, 0.14)], 'ME_Red_Button', seg=24)
    torus(g, 'ME_Glow_Teal', (0, 0, -0.07), 0.205, 0.02)


def b_seatbelt(g):
    # thick purple strap curled into a loop + big golden buckle with an orange latch
    pts = [(0.25 * math.cos(a), 0.06 * math.sin(a * 2), 0.18 * math.sin(a)) for a in [i * 2 * math.pi / 32 for i in range(33)]]
    g.tube(pts, 0.055, 'ME_Purple_Light', seg=8, cap=False)
    g.rbox('ME_Gold', (0, 0, -0.26), (0.24, 0.1, 0.16), bevel=0.04)
    g.rbox('ME_Orange_Warm', (0, -0.055, -0.22), (0.12, 0.04, 0.08), bevel=0.02)


def b_battery(g):
    # chubby teal cell, dark caps, orange lightning bolt on the front, gold terminal
    pill(g, 'ME_Teal', (0, 0, -0.01), 0.17, 0.3, seg=24)
    cyl(g, 'ME_Gray_Dark', (0, 0, -0.28), 0.175, 0.07, seg=24)
    cyl(g, 'ME_Gray_Dark', (0, 0, 0.19), 0.175, 0.06, seg=24)
    cyl(g, 'ME_Gold', (0, 0, 0.25), 0.06, 0.06, seg=12)
    for (x, z, rz) in ((0.03, 0.05, 0.5), (-0.03, -0.06, 0.5)):
        g.rbox('ME_Glow_Orange', (x, -0.165, z - 0.07), (0.07, 0.03, 0.15), bevel=0.01, rot=(0, rz, 0))


def b_toolbox(g):
    # rounded orange box, dark lid band, handle, a wrench poking out
    g.rbox('ME_Orange_Warm', (0, 0, -0.2), (0.6, 0.32, 0.3), bevel=0.07)
    g.rbox('ME_Gray_Dark', (0, 0, 0.06), (0.62, 0.34, 0.06), bevel=0.02)
    g.tube([(-0.16, 0, 0.1), (-0.16, 0, 0.24), (0.16, 0, 0.24), (0.16, 0, 0.1)], 0.035, 'ME_Purple_Deep', seg=8)
    for x in (-0.2, 0.2):
        g.rbox('ME_Gold', (x, -0.17, -0.02), (0.08, 0.03, 0.09), bevel=0.015)
    g.tube([(0.2, 0.05, 0.05), (0.32, 0.08, 0.28)], 0.03, 'ME_Gray_Light', seg=8)
    torus(g, 'ME_Gray_Light', (0.34, 0.085, 0.32), 0.06, 0.025, rot=(0, -0.4, 0))


def b_gear(g):
    # chunky 6-tooth dark gear standing up, teal gem in the middle
    disc_gear(g, 'ME_Gray_Dark', 0.2, 0.14, 6, rot=(math.pi / 2, 0, 0), tooth=(0.16, 0.14))
    g.blob('ME_Glow_Teal', (0, -0.07, 0), 0.08, sz=(1, 0.6, 1), seg=12)
    torus(g, 'ME_Gray_Light', (0, -0.072, 0), 0.1, 0.022, rot=(math.pi / 2, 0, 0))


def b_macaron_wheel(g):
    # two pink shells + cream filling + dark axle hub
    for y, s in ((-0.075, 1), (0.075, -1)):
        g.lathe([(0, 0), (0.24, 0), (0.26, 0.04 * s), (0.2, 0.1 * s), (0, 0.11 * s)], 'ME_Macaron_Pink', seg=28,
                loc=(0, y, 0), rot=(math.pi / 2 * -s, 0, 0))
    cyl(g, 'ME_Cream', (0, 0.075, 0), 0.235, 0.15, seg=28, rot=(math.pi / 2, 0, 0))
    cyl(g, 'ME_Gray_Dark', (0, 0.14, 0), 0.06, 0.28, seg=12, rot=(math.pi / 2, 0, 0))


def b_cookie_wheel(g):
    # thick golden cookie with big chocolate chips + axle
    rnd = random.Random(7)
    cyl(g, 'ME_Cookie_Gold', (0, 0.07, 0), 0.27, 0.14, seg=28, rot=(math.pi / 2, 0, 0))
    torus(g, 'ME_Cookie_Dark', (0, 0, 0), 0.265, 0.035, rot=(math.pi / 2, 0, 0))
    for k in range(6):
        a, r = k * 1.05 + rnd.uniform(-0.2, 0.2), rnd.uniform(0.1, 0.19)
        g.blob('ME_Oil_Choco', (r * math.cos(a), -0.075, r * math.sin(a)), 0.045, sz=(1, 0.7, 1), seg=8)
    cyl(g, 'ME_Gray_Dark', (0, 0.13, 0), 0.06, 0.26, seg=12, rot=(math.pi / 2, 0, 0))


def b_chocolate_oil(g):
    # round glass flask with chocolate inside, cookie cork
    g.lathe([(0, -0.27), (0.17, -0.26), (0.24, -0.12), (0.22, 0.04), (0.09, 0.14), (0.09, 0.22), (0, 0.22)], 'ME_Glass', seg=24)
    g.lathe([(0, -0.25), (0.15, -0.24), (0.215, -0.12), (0.2, 0.0), (0, 0.0)], 'ME_Oil_Choco', seg=24)
    cyl(g, 'ME_Cookie_Gold', (0, 0, 0.2), 0.1, 0.09, seg=16)
    torus(g, 'ME_Purple_Deep', (0, 0, 0.13), 0.1, 0.025)


def b_rune(glow):
    def b(g):
        # upright round cookie-stone tablet with a big glowing star glyph on the front
        g.lathe([(0, -0.07), (0.26, -0.07), (0.28, 0.0), (0.26, 0.07), (0, 0.07)], 'ME_Gray_Light', seg=10,
                rot=(math.pi / 2, 0, 0))
        torus(g, 'ME_Cookie_Dark', (0, 0, 0), 0.27, 0.03, rot=(math.pi / 2, 0, 0))
        star_prism(g, glow, (0, -0.075, 0), 0.17, 0.075, 0.03, points=4)
    return b


def b_candy_cell(glow):
    def b(g):
        # wrapped hard candy that glows inside, twisted wrapper ends, metal terminal caps
        g.blob(glow, (0, 0, 0), 0.17, sz=(1.3, 1, 1), seg=20)
        for s in (-1, 1):
            g.lathe([(0.06, 0), (0.13, 0.09), (0.05, 0.14)], 'ME_Cream', seg=8, loc=(0.2 * s, 0, 0), rot=(0, math.pi / 2 * s, 0))
            cyl(g, 'ME_Gray_Light', (0.2 * s, 0, 0), 0.065, 0.05, seg=12, rot=(0, math.pi / 2 * s, 0))
    return b


# ---------------- tools (one hand) ----------------
def b_hammer(g):
    # squeaky hammer: golden handle, big pink accordion head
    cyl(g, 'ME_Gold', (0, 0, -0.24), 0.03, 0.36, seg=10)
    g.lathe([(0, -0.16), (0.1, -0.16), (0.11, -0.1), (0.11, 0.1), (0.1, 0.16), (0, 0.16)], 'ME_Pink', seg=20,
            loc=(0, 0, 0.2), rot=(0, math.pi / 2, 0))
    for x in (-0.07, 0.0, 0.07):
        torus(g, 'ME_Purple_Light', (x, 0, 0.2), 0.112, 0.016, rot=(0, math.pi / 2, 0))


def b_stun_gun(g):
    # chunky purple body, dark grip, teal glowing prongs and orb
    g.rbox('ME_Purple_Deep', (0, 0.02, -0.02), (0.1, 0.32, 0.12), bevel=0.04)
    g.rbox('ME_Gray_Dark', (0, 0.11, -0.17), (0.08, 0.09, 0.17), bevel=0.03, rot=(0.25, 0, 0))
    for x in (-0.03, 0.03):
        cyl(g, 'ME_Gray_Light', (x, -0.14, 0.04), 0.012, 0.06, seg=6, rot=(math.pi / 2, 0, 0))
    g.blob('ME_Glow_Teal', (0, -0.2, 0.04), 0.035, seg=10)


def b_balloon(g):
    g.blob('ME_Balloon', (0, 0, 0.02), 0.14, sz=(1, 1, 1.15), seg=18)
    g.lathe([(0.0, -0.17), (0.035, -0.15), (0.0, -0.125)], 'ME_Balloon', seg=8)


ITEM_BUILDS = [('RedButton', b_red_button), ('Seatbelt', b_seatbelt), ('Battery', b_battery), ('Toolbox', b_toolbox),
               ('Gear', b_gear), ('MacaronWheel', b_macaron_wheel), ('CookieWheel', b_cookie_wheel),
               ('ChocolateOil', b_chocolate_oil), ('Hammer', b_hammer), ('StunGun', b_stun_gun), ('WaterBalloon', b_balloon)]
for _c in ('Red', 'Pink', 'Blue', 'Yellow'):
    ITEM_BUILDS.append(('Rune_' + _c, b_rune('ME_Rune_' + _c)))
for _c in ('Red', 'Orange', 'Yellow', 'Green'):
    ITEM_BUILDS.append(('CandyCell_' + _c, b_candy_cell('ME_Cell_' + _c)))
for _iid, _fn in ITEM_BUILDS:
    BUILDS.append(('ITEM_' + _iid, item, (_iid, _fn), {}))
