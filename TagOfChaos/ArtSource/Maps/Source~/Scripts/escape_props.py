# Escape-mode shared props (EscapeVisualPlan.md §3.2, §3.3, §4.3). exec'd by escape_assets.py (same namespace).
#   CHEST       hollow cookie-wood chest: Body (floor + 4 walls, empty inside) + Lid (origin = back hinge, -X opens)
#   JAR         cookie jar trophy for the monster-win result (glass, cookie lid, purple ribbon)
#   JAR_Cookie  mini gingerbread cookie stacked inside the jar (one per caught cookie)
#   PASSENGER   gingerbread cookie figure (origin at the feet) used when cookies board vehicles
import math, random

CHEST_W, CHEST_D, CHEST_H, CHEST_T = 1.0, 0.7, 0.6, 0.07


def build_chest():
    u = 'CHEST'

    def body(g):
        W, D, H, T = CHEST_W, CHEST_D, CHEST_H, CHEST_T
        g.rbox('ME_Cookie_Dark', (0, 0, 0), (W, D, 0.09), bevel=0.03)                       # floor (dark inside)
        for y in (-(D / 2 - T / 2), D / 2 - T / 2):                                         # front / back walls
            g.rbox('ME_Cookie_Gold', (0, y, 0), (W, T, H), bevel=0.03)
        for x in (-(W / 2 - T / 2), W / 2 - T / 2):                                         # side walls
            g.rbox('ME_Cookie_Gold', (x, 0, 0), (T, D - 2 * T, H), bevel=0.03)
        for x in (-0.36, 0.36):                                                             # purple bands outside
            g.rbox('ME_Purple_Deep', (x, -D / 2 - 0.01, 0.02), (0.1, 0.03, H - 0.02), bevel=0.01)
            g.rbox('ME_Purple_Deep', (x, D / 2 + 0.01, 0.02), (0.1, 0.03, H - 0.02), bevel=0.01)
        for y in (-(D / 2 - T / 2), D / 2 - T / 2):                                         # top rim (frame only:
            g.rbox('ME_Purple_Deep', (0, y, H - 0.06), (W + 0.03, T + 0.03, 0.07), bevel=0.02)  #  the inside stays open)
        for x in (-(W / 2 - T / 2), W / 2 - T / 2):
            g.rbox('ME_Purple_Deep', (x, 0, H - 0.06), (T + 0.03, D - 2 * T, 0.07), bevel=0.02)
        g.rbox('ME_Gold', (0, -D / 2 - 0.04, H - 0.2), (0.16, 0.06, 0.18), bevel=0.03)      # lock
        for x in (-0.4, 0.4):                                                               # little feet
            for y in (-0.25, 0.25):
                g.blob('ME_Cookie_Dark', (x, y, 0.0), 0.06, sz=(1, 1, 0.6), seg=8)
    mesh_obj(u, 'Body', body)

    def lid(g):  # domed half cylinder extending to the front (-Y) from the hinge at the origin
        L, R = CHEST_W + 0.04, CHEST_D / 2 + 0.02
        g.lathe([(0, -L / 2), (R, -L / 2), (R, L / 2), (0, L / 2)], 'ME_Cookie_Gold', seg=20, loc=(0, -CHEST_D / 2, 0),
                rot=(0, math.pi / 2, 0), scale=(0.6, 1, 1))
        for x in (-0.36, 0.36):
            g.lathe([(0, -0.05), (R + 0.02, -0.05), (R + 0.02, 0.05), (0, 0.05)], 'ME_Purple_Deep', seg=20,
                    loc=(x, -CHEST_D / 2, 0), rot=(0, math.pi / 2, 0), scale=(0.6, 1, 1))
        half_cut(g, 0.0)
        g.rbox('ME_Gold', (0, -CHEST_D - 0.035, -0.06), (0.14, 0.05, 0.12), bevel=0.02)       # lock hasp
    mesh_obj(u, 'Lid', lid, loc=(0, CHEST_D / 2, CHEST_H))


def build_jar():
    u = 'JAR'

    def glass(g):
        g.lathe([(0, 0), (0.42, 0), (0.5, 0.1), (0.53, 0.55), (0.5, 0.85), (0.4, 0.98), (0.38, 1.06), (0, 1.06)],
                'ME_Glass', seg=32)
    mesh_obj(u, 'Glass', glass)

    def lid(g):
        cyl(g, 'ME_Cookie_Gold', (0, 0, 1.04), 0.44, 0.13, seg=28)
        g.blob('ME_Cookie_Gold', (0, 0, 1.2), 0.1, sz=(1, 1, 0.8), seg=12)
        torus(g, 'ME_Purple_Deep', (0, 0, 0.99), 0.4, 0.04)
        g.blob('ME_Purple_Deep', (0, -0.42, 0.98), 0.08, sz=(1.4, 0.6, 1), seg=10)           # ribbon bow
    mesh_obj(u, 'Lid', lid)
    empty(u, 'CookieStack', (0, 0, 0.05))                                                   # cookies pile up from here


def gingerbread(g, s=1.0, flat=1.0, base=(0, 0, 0)):
    """Chunky gingerbread figure, feet at base; s = height scale (1 = 1.4 m), flat squashes the depth."""
    x0, y0, z0 = base
    sz = (1, flat, 1)
    g.blob('ME_Cookie_Gold', (x0, y0, z0 + 0.62 * s), 0.3 * s, sz=(1, 0.8 * flat, 1.15), seg=16)      # body
    g.blob('ME_Cookie_Gold', (x0, y0, z0 + 1.12 * s), 0.27 * s, sz=sz, seg=16)                        # head
    for side in (-1, 1):
        g.tube([(x0 + 0.2 * s * side, y0, z0 + 0.78 * s), (x0 + 0.42 * s * side, y0, z0 + 0.62 * s)], 0.1 * s,
               'ME_Cookie_Gold', seg=8)                                                               # arms
        g.tube([(x0 + 0.13 * s * side, y0, z0 + 0.38 * s), (x0 + 0.16 * s * side, y0, z0 + 0.06 * s)], 0.11 * s,
               'ME_Cookie_Gold', seg=8)                                                               # legs
        g.blob('ME_Purple_Deep', (x0 + 0.09 * s * side, y0 - 0.24 * s * flat, z0 + 1.16 * s), 0.04 * s, seg=8)   # eyes
    for k in range(2):
        g.blob('ME_Cream', (x0, y0 - 0.27 * s * flat, z0 + (0.7 - 0.16 * k) * s), 0.045 * s, seg=8)               # icing buttons
    g.tube([(x0 - 0.1 * s, y0 - 0.25 * s * flat, z0 + 1.03 * s), (x0, y0 - 0.27 * s * flat, z0 + 0.99 * s),
            (x0 + 0.1 * s, y0 - 0.25 * s * flat, z0 + 1.03 * s)], 0.02 * s, 'ME_Cream', seg=6)              # smile


def build_jar_cookie():
    mesh_obj('JAR_Cookie', 'JAR_Cookie', lambda g: gingerbread(g, s=0.2, flat=0.45))


def build_passenger():
    mesh_obj('PASSENGER', 'PASSENGER', lambda g: gingerbread(g, s=1.0, flat=1.0))


BUILDS.append(('CHEST', build_chest, (), {}))
BUILDS.append(('JAR', build_jar, (), {}))
BUILDS.append(('JAR_Cookie', build_jar_cookie, (), {}))
BUILDS.append(('PASSENGER', build_passenger, (), {}))
