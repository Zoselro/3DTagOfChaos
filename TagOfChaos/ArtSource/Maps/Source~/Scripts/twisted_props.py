# Twisted candy props (TwistedCandyPlan.md §1.3·§2.2·V3) -> one FBX per prop unit.
# Run:  python(bpy) twisted_props.py -- <scripts_dir> <fbx_dir> [unit prefixes]
#       (or: blender -b --factory-startup --python twisted_props.py -- <scripts_dir> <fbx_dir>)
# Style: same low-poly kit as the maps (maplib.py + assets.py, G). "Broken toy" grotesque only — no blood or innards:
# hollow necks are dark inside, cookie breaks show crumb, eyes are buttons/candy.
# 1 unit = 1 m, Z-up, front = -Y (Unity +Z), origin = bottom centre.
# Objects per unit: 'Body' (+ 'Part_*') get a box collider in Unity; 'Decal_*' (flat floor marks) get none.
# Materials MT_* are rebuilt in Unity by TwistedPropBuilder (palette colours exact) and remapped by name.
# Palette slots MT_P0..MT_P9 = the paint palette (red, orange, yellow, lime, green, teal, blue, navy, purple, magenta):
# each map's props use the colours its scenery lacks (V2 measurement) so cookies have more places to blend in.
import bpy, bmesh, sys, os, math, random
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
SCRIPTS = argv[0] if len(argv) > 0 else os.path.dirname(os.path.abspath(__file__))
FBX_DIR = argv[1] if len(argv) > 1 else os.path.join(SCRIPTS, 'out')
ONLY = [p for p in argv[2].split(',') if p] if len(argv) > 2 else []

for lib in ('maplib.py', 'assets.py'):
    exec(open(os.path.join(SCRIPTS, lib), encoding='utf-8').read(), globals())

P = ['MT_P0_Red', 'MT_P1_Orange', 'MT_P2_Yellow', 'MT_P3_Lime', 'MT_P4_Green',
     'MT_P5_Teal', 'MT_P6_Blue', 'MT_P7_Navy', 'MT_P8_Purple', 'MT_P9_Magenta']
PAL.update({
    'MT_P0_Red': ((0.91, 0.15, 0.17), 0), 'MT_P1_Orange': ((0.96, 0.51, 0.12), 0), 'MT_P2_Yellow': ((1.0, 0.83, 0.0), 0),
    'MT_P3_Lime': ((0.55, 0.78, 0.25), 0), 'MT_P4_Green': ((0.0, 0.65, 0.32), 0), 'MT_P5_Teal': ((0.0, 0.66, 0.62), 0),
    'MT_P6_Blue': ((0.0, 0.45, 0.74), 0), 'MT_P7_Navy': ((0.11, 0.23, 0.42), 0), 'MT_P8_Purple': ((0.43, 0.25, 0.6), 0),
    'MT_P9_Magenta': ((0.63, 0.08, 0.42), 0),
    'MT_Pastel': ((0.82, 0.78, 0.80), 0), 'MT_Hollow': ((0.04, 0.03, 0.04), 0), 'MT_Button': ((0.03, 0.03, 0.03), 0),
    'MT_Cookie': ((0.55, 0.32, 0.16), 0), 'MT_CookieDark': ((0.32, 0.18, 0.09), 0), 'MT_Crumb': ((0.78, 0.56, 0.33), 0),
    'MT_Icing': ((0.88, 0.85, 0.78), 0), 'MT_EyeWhite': ((0.9, 0.88, 0.8), 0), 'MT_Wood': ((0.30, 0.22, 0.17), 0),
    'MT_Gold': ((0.62, 0.48, 0.2), 0), 'MT_Metal': ((0.32, 0.33, 0.36), 0), 'MT_Choco': ((0.22, 0.11, 0.05), 0),
    'MT_Dough': ((0.86, 0.76, 0.6), 0), 'MT_Flour': ((0.9, 0.9, 0.88), 0), 'MT_Glass': ((0.7, 0.85, 0.9), 0),
    'MT_Mold': ((0.5, 0.56, 0.42), 0), 'MT_Frosting': ((0.86, 0.72, 0.78), 0),
    'MT_Canvas_Torn': ((0.55, 0.5, 0.46), 0), 'MT_Gingerbread': ((0.58, 0.34, 0.17), 0),
    'MT_FloorBoard': ((0.4, 0.28, 0.2), 0), 'MT_Exit': ((1.0, 0.75, 0.3), 2.0),
})

COL = None
UNITS = {}
BUILDS = []
RND = random.Random(11)


def reset_scene():
    global COL
    bpy.ops.wm.read_factory_settings(use_empty=True)
    COL = bpy.context.scene.collection


def mesh_obj(unit, name, build):
    g = G()
    build(g)
    ob = bpy.data.objects.new(name, g.mesh('A_' + unit + '_' + name))
    COL.objects.link(ob)
    UNITS.setdefault(unit, []).append(ob)
    return ob


def unit(name):
    def deco(fn):
        BUILDS.append((name, fn))
        return fn
    return deco


# ---------------- small helpers on the G kit ----------------
def cyl(g, mat, loc, r, h, rot=(0, 0, 0), seg=12, r2=None):
    return g.lathe([(r, 0), (r if r2 is None else r2, h)], mat, seg=seg, loc=loc, rot=rot)


def cone(g, mat, loc, r, h, rot=(0, 0, 0), seg=8):
    return g.lathe([(r, 0), (0, h)], mat, seg=seg, loc=loc, rot=rot)


def disc(g, mat, loc, r, t, rot=(0, 0, 0), seg=16, segmat=None):
    return g.lathe([(0, -t / 2), (r, -t / 2), (r, t / 2), (0, t / 2)], mat, seg=seg, loc=loc, rot=rot, segmat=segmat)


def jag(verts, zmin, amount, rnd=RND):
    """Break the top edge: verts above zmin jump up/down (snapped / torn look)."""
    for v in verts:
        if v.co.z > zmin:
            v.co.z += rnd.uniform(-amount, amount)


def sag(verts, zcut, k):
    """Melt: verts below zcut droop further down (candy sliding off)."""
    for v in verts:
        if v.co.z < zcut:
            v.co.z -= (zcut - v.co.z) * k


def crumbs(g, cx, cy, n, spread, rnd=RND, mat='MT_Crumb'):
    for _ in range(n):
        g.blob(mat, (cx + rnd.uniform(-spread, spread), cy + rnd.uniform(-spread, spread), 0.04), rnd.uniform(0.04, 0.1), seg=6)


def doll(g, mat, s, loc, eyes=('MT_Button', 'MT_Button'), buttons=(), smile='MT_Icing', arms=(True, True), lean=0.0):
    """Chunky gingerbread doll standing at loc (feet), height ~1.4*s. eyes: material per eye or None (empty socket)."""
    x0, y0, z0 = loc
    lx = lambda z: x0 + lean * (z - z0)
    g.blob(mat, (lx(z0 + 0.62 * s), y0, z0 + 0.62 * s), 0.3 * s, sz=(1, 0.8, 1.15), seg=12)
    g.blob(mat, (lx(z0 + 1.12 * s), y0, z0 + 1.12 * s), 0.27 * s, sz=(1, 0.85, 1), seg=12)
    for side, keep in zip((-1, 1), arms):
        if keep:
            g.tube([(lx(z0 + 0.78 * s) + 0.2 * s * side, y0, z0 + 0.78 * s), (lx(z0 + 0.6 * s) + 0.44 * s * side, y0, z0 + 0.6 * s)], 0.1 * s, mat, seg=7)
        g.tube([(x0 + 0.13 * s * side, y0, z0 + 0.38 * s), (x0 + 0.16 * s * side, y0, z0 + 0.06 * s)], 0.11 * s, mat, seg=7)
    for side, em in zip((-1, 1), eyes):
        p = (lx(z0 + 1.16 * s) + 0.09 * s * side, y0 - 0.22 * s, z0 + 1.16 * s)
        g.blob(em if em else 'MT_Hollow', p, 0.045 * s, sz=(1, 0.6, 1), seg=8)
    for k, bm in enumerate(buttons):
        g.blob(bm, (lx(z0 + (0.72 - 0.16 * k) * s), y0 - 0.25 * s, z0 + (0.72 - 0.16 * k) * s), 0.05 * s, seg=8)
    if smile:
        z = z0 + 1.0 * s
        g.tube([(lx(z) - 0.1 * s, y0 - 0.22 * s, z + 0.03 * s), (lx(z), y0 - 0.24 * s, z), (lx(z) + 0.1 * s, y0 - 0.22 * s, z + 0.03 * s)], 0.018 * s, smile, seg=5)


# =====================================================================================
# CANDY FOREST (★) — palette fill: yellow, lime, green, teal, navy
# =====================================================================================
def headless_horse(body, mane, accent, base):
    def build_body(g):
        cyl(g, base, (0, 0, 0), 1.7, 0.3, seg=16)
        g.lathe([(1.72, 0.22), (1.78, 0.3), (1.6, 0.34)], accent, seg=16)
        g.tube([(0, 0, 0.3), (0, 0, 3.3), (0.12, 0.05, 3.75)], 0.08, accent, seg=8)       # pole, snapped top bent
        g.blob(body, (0, 0, 1.95), 0.95, sz=(1.0, 0.42, 0.45), seg=12)                    # barrel
        g.blob(body, (-0.85, 0, 1.97), 0.46, sz=(1, 0.85, 1), seg=10)                      # rump
        g.blob(body, (0.85, 0, 2.02), 0.48, sz=(1, 0.85, 1), seg=10)                       # chest
        for (hx, fx, fz) in ((0.85, 1.35, 0.95), (0.7, 0.85, 0.32), (-0.85, -1.15, 0.32), (-0.7, -0.75, 0.32)):
            for sy in (-0.2, 0.2):
                g.tube([(hx, sy, 1.75), (fx, sy, max(fz, 0.32) + 0.12)], 0.09, body, seg=6)
                g.blob(accent, (fx, sy, max(fz, 0.32) + 0.06), 0.11, sz=(1, 1, 0.6), seg=6)  # hooves
        neck = g.tube([(1.05, 0, 2.2), (1.35, 0, 2.75)], 0.28, body, seg=9)
        g.blob('MT_Hollow', (1.37, 0, 2.79), 0.24, sz=(1, 1, 0.18), rot=(0, math.radians(-29), 0), seg=10)  # empty neck
        for i in range(5):                                                                  # torn mane
            cone(g, mane, (1.0 + i * 0.08, 0, 2.3 + i * 0.1), 0.12, 0.38, rot=(0, math.radians(-55), 0), seg=4)
        g.tube([(-1.25, 0, 2.05), (-1.6, 0, 1.6), (-1.55, 0, 1.0)], [0.16, 0.13, 0.05], mane, seg=6)  # tail
        g.rbox(accent, (0, 0, 2.33), (0.75, 0.88, 0.1), bevel=0.04)                         # saddle
        for x in (-0.25, 0.25):
            g.rbox(accent, (x, 0, 1.6), (0.06, 0.86, 0.75), bevel=0.02)
    return build_body


def fallen_head(body, mane, accent):
    def build(g):
        a = math.radians(25)
        g.blob(body, (0, 0, 0.32), 0.34, sz=(1.25, 0.85, 0.92), rot=(0, 0, a), seg=10)     # skull
        g.tube([(0.25 * math.cos(a), 0.25 * math.sin(a), 0.28), (0.7 * math.cos(a), 0.7 * math.sin(a), 0.22)], [0.22, 0.17], body, seg=8)
        cone(g, accent, (-0.05, 0, 0.6), 0.09, 0.75, rot=(0, math.radians(-25), 0), seg=6)  # horn
        for dy in (-0.16, 0.16):
            cone(g, body, (-0.2, dy, 0.55), 0.07, 0.26, rot=(math.radians(dy * 80), 0, 0), seg=4)
        for i in range(3):
            cone(g, mane, (-0.35, -0.1 + i * 0.1, 0.4 + i * 0.05), 0.11, 0.32, rot=(0, math.radians(-110), 0), seg=4)
        g.blob('MT_Button', (0.1, -0.28, 0.42), 0.07, sz=(1, 0.5, 1), seg=8)                # one button eye left
        g.blob('MT_Hollow', (0.1, 0.28, 0.42), 0.06, sz=(1, 0.5, 1), seg=8)
        g.blob('MT_Hollow', (-0.38, 0.02, 0.3), 0.22, sz=(0.25, 1, 1), rot=(0, 0, a), seg=10)  # hollow cut
    return build


@unit('TW_HeadlessUnicorn')
def tw_headless_unicorn(u):
    mesh_obj(u, 'Body', headless_horse('MT_P5_Teal', 'MT_P7_Navy', 'MT_P2_Yellow', 'MT_P3_Lime'))
    h = mesh_obj(u, 'Part_Head', fallen_head('MT_P5_Teal', 'MT_P7_Navy', 'MT_P2_Yellow'))
    h.location = (2.4, -1.4, 0)


@unit('TW_HeadlessHorse_Carnival')
def tw_headless_horse_carnival(u):
    mesh_obj(u, 'Body', headless_horse('MT_P1_Orange', 'MT_P2_Yellow', 'MT_P3_Lime', 'MT_P1_Orange'))
    h = mesh_obj(u, 'Part_Head', fallen_head('MT_P1_Orange', 'MT_P2_Yellow', 'MT_P3_Lime'))
    h.location = (-2.3, -1.5, 0)
    h.rotation_euler = (0, 0, math.radians(150))


def eye_lollipop(a, b, iris):
    def build(g):
        g.tube([(0, 0, 0), (0.08, 0, 1.8), (0.2, 0, 3.3)], 0.13, 'MT_Icing', seg=7)
        vs = disc(g, a, (0.22, 0, 3.4), 1.25, 0.32, rot=(math.radians(90), 0, 0), seg=24,
                  segmat=lambda k: a if (k // 2) % 2 == 0 else b)
        sag(vs, 3.25, 0.7)                                                                 # lower half melts down
        for i in range(8):                                                                 # drips
            ang = RND.uniform(-2.5, -0.65)
            x, z = 0.22 + math.cos(ang) * 1.05, 3.4 + math.sin(ang) * 1.05 - 0.45
            g.blob(a if i % 2 else b, (x, RND.uniform(-0.08, 0.08), z), 0.1, sz=(1, 1, RND.uniform(2.5, 4.5)), seg=6)
        for i in range(4):                                                                 # puddle
            g.blob(a if i % 2 else b, (RND.uniform(-0.8, 1.0), RND.uniform(-0.6, 0.6), 0.0), RND.uniform(0.35, 0.65), sz=(1, 0.8, 0.07), seg=10)
        g.blob('MT_EyeWhite', (0.24, -0.3, 3.45), 0.45, seg=14)
        g.blob(iris, (0.26, -0.68, 3.42), 0.22, sz=(1, 0.35, 1), seg=12)
        g.blob('MT_Button', (0.27, -0.75, 3.41), 0.1, sz=(1, 0.4, 1), seg=8)
    return build


@unit('TW_EyeLollipop')
def tw_eye_lollipop(u):
    mesh_obj(u, 'Body', eye_lollipop('MT_P3_Lime', 'MT_P4_Green', 'MT_P7_Navy'))


@unit('TW_EyeLollipop_Warm')
def tw_eye_lollipop_warm(u):
    mesh_obj(u, 'Body', eye_lollipop('MT_P0_Red', 'MT_P2_Yellow', 'MT_P4_Green'))


@unit('TW_MeltingTree')
def tw_melting_tree(u):
    def build(g):
        g.tube([(0, 0, 0), (0.3, 0.1, 1.6), (-0.1, 0, 3.2), (0.2, -0.1, 4.2)], [0.42, 0.3, 0.22, 0.16], 'MT_Wood', seg=8)
        g.tube([(0.1, 0, 2.6), (1.3, 0.2, 3.3), (1.9, 0.1, 3.1)], [0.12, 0.08, 0.04], 'MT_Wood', seg=6)    # bare branches
        g.tube([(0.0, 0, 3.0), (-1.2, -0.2, 3.8), (-1.6, -0.3, 4.3)], [0.11, 0.07, 0.03], 'MT_Wood', seg=6)
        vs = g.blob('MT_P4_Green', (0.15, 0, 4.6), 1.5, sz=(1.1, 1.0, 0.7), seg=14, jitter=0.12, rnd=RND)
        sag(vs, 4.4, 0.9)                                                                  # canopy slides down
        for i in range(10):
            ang = RND.uniform(0, 2 * math.pi); r = RND.uniform(0.9, 1.5)
            x, y = 0.15 + math.cos(ang) * r, math.sin(ang) * r
            top = 4.1 - RND.uniform(0.0, 0.4)
            g.tube([(x, y, top), (x * 1.02, y * 1.02, top - RND.uniform(0.8, 2.2))], [0.12, 0.05], 'MT_P3_Lime' if i % 3 else 'MT_P4_Green', seg=6)
        for i in range(3):
            g.blob('MT_P3_Lime', (RND.uniform(-1.2, 1.2), RND.uniform(-1.0, 1.0), 0), RND.uniform(0.4, 0.7), sz=(1, 0.9, 0.06), seg=10)
    mesh_obj(u, 'Body', build)


@unit('TW_MoldyCake')
def tw_moldy_cake(u):
    def build(g):
        g.lathe([(1.6, 0), (1.62, 1.1), (1.5, 1.2), (0, 1.2)], 'MT_Frosting', seg=20)                       # bottom tier
        top = g.lathe([(1.05, 0), (1.07, 0.85), (0.95, 0.95), (0, 0.95)], 'MT_P7_Navy', seg=18, loc=(0.15, 0, 1.2), rot=(math.radians(8), 0, 0))
        jag(top, 2.0, 0.06)
        g.rbox('MT_Hollow', (0.9, -0.95, 0.15), (0.22, 0.5, 1.05), bevel=0.02, rotz=math.radians(25))     # crack
        g.rbox('MT_Crumb', (0.75, -1.2, 0.0), (0.5, 0.6, 0.12), bevel=0.04)                                  # fallen chunk
        for k in range(16):                                                                                  # icing drips
            a = 2 * math.pi * k / 16
            g.blob('MT_Icing', (1.6 * math.cos(a), 1.6 * math.sin(a), 1.0), 0.13, sz=(1, 1, RND.uniform(1.5, 3)), seg=6)
        for k in range(9):                                                                                   # mould patches
            a = RND.uniform(0, 2 * math.pi); z = RND.uniform(0.2, 1.0)
            g.blob('MT_P3_Lime' if k % 2 else 'MT_Mold', (1.6 * math.cos(a), 1.6 * math.sin(a), z), RND.uniform(0.2, 0.4), sz=(1, 1, 0.8), seg=8)
        for k in range(4):
            a = RND.uniform(0, 2 * math.pi)
            g.blob('MT_P3_Lime', (0.15 + 0.7 * math.cos(a), 0.7 * math.sin(a), 2.15), RND.uniform(0.15, 0.3), sz=(1, 1, 0.6), seg=8)
        g.tube([(0.1, 0, 2.15), (0.12, 0, 2.6)], 0.07, 'MT_P2_Yellow', seg=6)                               # candle
        g.tube([(0.12, 0, 2.6), (0.5, -0.1, 2.5)], 0.07, 'MT_P2_Yellow', seg=6)                              # snapped, hanging
    mesh_obj(u, 'Body', build)


# =====================================================================================
# GINGERBREAD VILLAGE (★★) — palette fill: yellow, lime, green, teal
# =====================================================================================
@unit('TW_BrokenGingerDoll')
def tw_broken_ginger_doll(u):
    s = 2.2
    def build(g):
        g.blob('MT_Cookie', (0, 0.2, 0.75 * s), 0.55 * s, sz=(1, 0.55, 1.25), rot=(math.radians(-14), 0, 0), seg=12)   # slumped torso
        g.blob('MT_Cookie', (0.15 * s, 0.25, 1.62 * s), 0.42 * s, sz=(1, 0.7, 0.95), rot=(0, math.radians(22), 0), seg=12)  # tilted head
        for x in (-0.25 * s, 0.25 * s):
            g.tube([(x, 0.1, 0.2 * s), (x * 1.1, -0.85 * s, 0.17 * s)], 0.17 * s, 'MT_Cookie', seg=8)            # legs forward
            for z in (0.35, 0.6):
                g.tube([(x * 1.05, -z * s, 0.18 * s - 0.02), (x * 1.05, -z * s - 0.05, 0.18 * s)], 0.185 * s, 'MT_Icing', seg=8)
        g.tube([(-0.5 * s, 0.15, 1.0 * s), (-0.9 * s, -0.1, 0.55 * s)], 0.15 * s, 'MT_Cookie', seg=8)             # remaining arm
        g.blob('MT_Crumb', (0.55 * s, 0.25, 1.05 * s), 0.15 * s, sz=(0.5, 1, 1), seg=8)                         # broken shoulder (crumb)
        g.tube([(-0.35 * s, -0.05, 1.32 * s), (0, -0.25 * s, 1.36 * s), (0.4 * s, -0.05, 1.32 * s)], 0.09 * s, 'MT_P5_Teal', seg=8)  # scarf
        g.tube([(0.3 * s, -0.12 * s, 1.3 * s), (0.42 * s, -0.3 * s, 0.85 * s)], [0.09 * s, 0.06 * s], 'MT_P5_Teal', seg=6)
        for k, mt in enumerate(('MT_P2_Yellow', 'MT_P4_Green', 'MT_P2_Yellow')):
            g.blob(mt, (0, -0.3 * s + k * 0.03, (1.0 - 0.22 * k) * s), 0.08 * s, sz=(1, 0.5, 1), seg=8)
        g.blob('MT_Button', (0.0, -0.08 * s, 1.72 * s), 0.07 * s, sz=(1, 0.5, 1), seg=8)
        g.blob('MT_Hollow', (0.3 * s, -0.05 * s, 1.72 * s), 0.08 * s, sz=(1, 0.4, 1), seg=8)                     # missing eye
        for k in (0, 1, 3, 4):                                                                                  # cracked smile
            a = math.radians(200 + k * 35)
            g.blob('MT_Icing', (0.15 * s + math.cos(a) * 0.18 * s, -0.1 * s, 1.55 * s + math.sin(a) * 0.09 * s), 0.03 * s, seg=6)
        g.rbox('MT_CookieDark', (0.12 * s, -0.12 * s, 1.75 * s), (0.03, 0.05, 0.25 * s), bevel=0.0, rot=(0, math.radians(35), 0))  # crack
        crumbs(g, 1.4, -0.6, 22, 0.7)
    mesh_obj(u, 'Body', build)
    def arm(g):
        g.tube([(0, 0, 0.17 * s), (0.75 * s, 0, 0.17 * s)], 0.15 * s, 'MT_Cookie', seg=8)
        g.blob('MT_Crumb', (0, 0, 0.17 * s), 0.14 * s, sz=(0.4, 1, 1), seg=8)
        g.tube([(0.5 * s, 0, 0.17 * s - 0.01), (0.55 * s, 0, 0.17 * s)], 0.16 * s, 'MT_Icing', seg=8)
    a = mesh_obj(u, 'Part_Arm', arm)
    a.location = (1.6, -1.4, 0)
    a.rotation_euler = (0, 0, math.radians(35))


@unit('TW_EyelessGuard')
def tw_eyeless_guard(u):
    def build(g):
        g.rbox('MT_P5_Teal', (0, 0, 0), (1.6, 1.6, 0.3), bevel=0.06)                                           # plinth
        doll(g, 'MT_Cookie', 2.0, (0, 0, 0.3), eyes=(None, None), buttons=('MT_P2_Yellow', 'MT_P4_Green', 'MT_P2_Yellow'), smile=None)
        for z in (1.25, 1.45):                                                                                 # coat icing stripes
            g.lathe([(0.52, 0), (0.55, 0.06), (0.52, 0.12)], 'MT_P4_Green', seg=14, loc=(0, 0, z), scale=(1, 0.8, 1))
        g.lathe([(0.32, 0), (0.36, 0.1), (0.3, 1.0), (0.42, 1.08), (0, 1.1)], 'MT_P3_Lime', seg=12, loc=(0, 0, 2.8), rot=(0, math.radians(-8), 0))  # tall hat
        g.lathe([(0.45, 0), (0.47, 0.06), (0, 0.06)], 'MT_P2_Yellow', seg=12, loc=(0, 0, 2.78))
        g.tube([(0.95, -0.05, 0.3), (0.95, -0.05, 3.6)], 0.05, 'MT_Gold', seg=6,
               ringmat=lambda i: 'MT_P2_Yellow' if i % 2 == 0 else 'MT_P0_Red')                               # candy spear
        cone(g, 'MT_P2_Yellow', (0.95, -0.05, 3.6), 0.14, 0.4, seg=6)
        g.rbox('MT_CookieDark', (-0.12, -0.24, 2.2), (0.03, 0.04, 0.3), bevel=0.0, rot=(0, math.radians(-25), 0))   # crack over eye
    mesh_obj(u, 'Body', build)


@unit('TW_CrackedFence')
def tw_cracked_fence(u):
    def build(g):
        L = 6.0
        g.rbox('MT_Cookie', (-1.6, 0, 0.9), (2.8, 0.14, 0.22), bevel=0.05)                                     # rails (broken middle)
        g.rbox('MT_Cookie', (1.75, 0, 0.9), (2.4, 0.14, 0.22), bevel=0.05, rot=(0, math.radians(-7), 0))
        g.rbox('MT_Cookie', (0, 0, 0.35), (L, 0.14, 0.2), bevel=0.05)
        for i in range(9):
            x = -L / 2 + 0.35 + i * (L - 0.7) / 8
            if i == 4:
                continue
            h = 0.7 if i in (3, 6) else RND.uniform(1.5, 1.8)
            vs = g.rbox('MT_Cookie', (x, 0, 0), (0.42, 0.12, h), bevel=0.06)
            if h < 1:
                jag(vs, h - 0.1, 0.12)
                g.blob('MT_Crumb', (x, 0, h), 0.12, sz=(1.6, 0.8, 0.5), seg=6)
            else:
                cone(g, 'MT_P5_Teal' if i % 2 else 'MT_P2_Yellow', (x, 0, h), 0.24, 0.3, seg=4, rot=(0, 0, math.radians(45)))
                g.rbox('MT_P5_Teal' if i % 2 == 0 else 'MT_P2_Yellow', (x, -0.08, h * 0.55), (0.44, 0.05, 0.12), bevel=0.02)  # icing band
        g.rbox('MT_Cookie', (0.4, -0.9, 0), (0.42, 1.7, 0.12), bevel=0.05, rotz=math.radians(20))                 # fallen picket
        crumbs(g, 0.2, -0.4, 14, 0.8)
    mesh_obj(u, 'Body', build)


# =====================================================================================
# CHOCOLATE FACTORY (★★) — palette fill: lime, green, teal, blue, purple
# =====================================================================================
@unit('TW_MeltedDollMold')
def tw_melted_doll_mold(u):
    def build(g):
        for x in (-1.3, 1.3):
            for y in (-0.8, 0.8):
                g.rbox('MT_Metal', (x, y, 0), (0.14, 0.14, 1.0 if y < 0 else 1.4), bevel=0.02)
        g.rbox('MT_P5_Teal', (0, 0, 1.0), (3.0, 1.9, 0.22), bevel=0.05, rot=(math.radians(-12), 0, 0))         # tilted tray
        for i, x in enumerate((-0.9, 0.0, 0.9)):
            vs = g.blob('MT_Choco', (x, -0.1 + i * 0.05, 1.32), 0.42, sz=(0.6, 1.2, 0.35), seg=10)            # melted dolls
            sag(vs, 1.25, 0.9)
            g.blob('MT_Choco', (x, -0.35, 1.45), 0.22, sz=(1, 0.8, 0.7), seg=8)                               # heads
            g.blob('MT_P8_Purple', (x - 0.06, -0.5, 1.5), 0.04, seg=6)                                       # candy eyes
            g.blob('MT_P8_Purple', (x + 0.06, -0.5, 1.47), 0.04, seg=6)
            g.tube([(x, -0.9, 1.0), (x + 0.05, -1.0, 0.5), (x, -1.05, 0.02)], [0.09, 0.06, 0.04], 'MT_Choco', seg=6)  # drips
        g.blob('MT_Choco', (0, -1.1, 0), 1.1, sz=(1.3, 0.6, 0.05), seg=12)                                    # puddle
        g.rbox('MT_P3_Lime', (1.9, -0.2, 0), (0.8, 0.6, 0.5), bevel=0.05, rotz=math.radians(15))                # spare mould box
    mesh_obj(u, 'Body', build)


@unit('TW_FacelessDoughRow')
def tw_faceless_dough_row(u):
    def build(g):
        L = 6.0
        for x in (-2.7, -0.9, 0.9, 2.7):
            for y in (-0.55, 0.55):
                g.rbox('MT_P6_Blue', (x, y, 0), (0.18, 0.18, 0.8), bevel=0.03)
        g.rbox('MT_P6_Blue', (0, -0.6, 0.8), (L, 0.12, 0.22), bevel=0.04)
        g.rbox('MT_P6_Blue', (0, 0.6, 0.8), (L, 0.12, 0.22), bevel=0.04)
        g.rbox('MT_Metal', (0, 0, 0.86), (L - 0.2, 1.05, 0.08), bevel=0.02)                                    # belt
        for k in range(7):
            cyl(g, 'MT_P8_Purple', (-2.7 + k * 0.9, -0.6, 0.83), 0.12, 1.2, rot=(math.radians(-90), 0, 0), seg=8)  # rollers
        for i in range(5):
            x = -2.4 + i * 1.2
            if i == 4:                                                                                         # last one fell over
                vs_lean = 1.2
                doll(g, 'MT_Dough', 0.9, (x, 0, 0.94), eyes=('MT_Dough', 'MT_Dough'), smile=None, lean=vs_lean)
            else:
                doll(g, 'MT_Dough', 0.9 + (i % 2) * 0.08, (x, 0, 0.94), eyes=('MT_Dough', 'MT_Dough'), smile=None)  # faceless
        g.rbox('MT_P4_Green', (3.4, 0.0, 0), (0.7, 1.2, 1.3), bevel=0.06)                                     # end hopper
    mesh_obj(u, 'Body', build)


@unit('TW_BrokenPacker')
def tw_broken_packer(u):
    def build(g):
        g.rbox('MT_P4_Green', (0, 0, 0), (2.4, 1.8, 2.6), bevel=0.12)
        g.rbox('MT_Hollow', (0, -0.91, 0.9), (1.3, 0.04, 0.9), bevel=0.0)                                     # dark mouth
        g.rbox('MT_P8_Purple', (0, 0, 2.6), (1.6, 1.2, 0.4), bevel=0.08)
        g.tube([(0.9, -0.5, 2.6), (1.6, -0.9, 2.4), (1.9, -1.1, 1.3)], 0.12, 'MT_P8_Purple', seg=8)              # broken arm
        g.blob('MT_Metal', (1.9, -1.1, 1.2), 0.25, sz=(1, 1, 0.6), seg=8)
        for k in range(4):
            vs = g.rbox('MT_P3_Lime' if k % 2 == 0 else 'MT_P8_Purple', (RND.uniform(-1.6, 1.6), RND.uniform(-2.0, -1.2), 0),
                        (0.6, 0.6, RND.uniform(0.35, 0.55)), bevel=0.04, rotz=RND.uniform(0, 1.2))
            jag(vs, 0.25, 0.1)                                                                                # crushed boxes
        for i in range(3):
            g.rbox('MT_Metal', (-0.6 + i * 0.6, -0.92, 2.1), (0.25, 0.06, 0.25), bevel=0.02)                     # dials
    mesh_obj(u, 'Body', build)


# =====================================================================================
# CURSED CARNIVAL (★★★) — palette fill: orange, yellow, lime
# =====================================================================================
@unit('TW_ClownSign')
def tw_clown_sign(u):
    def build(g):
        for x in (-1.7, 1.7):
            g.tube([(x, 0.1, 0), (x, 0.1, 4.6)], 0.12, 'MT_P2_Yellow', seg=8,
                   ringmat=lambda i: 'MT_P2_Yellow' if i % 2 == 0 else 'MT_P0_Red')
        g.rbox('MT_P3_Lime', (0, 0.12, 1.6), (4.1, 0.16, 3.0), bevel=0.06)                                    # frame
        vs = g.rbox('MT_P2_Yellow', (0, 0.0, 1.75), (3.7, 0.12, 2.7), bevel=0.02)                             # board
        for v in vs:                                                                                           # torn top-right corner
            if v.co.x > 1.0 and v.co.z > 3.8:
                v.co.z -= (v.co.x - 1.0) * 0.9
        g.rbox('MT_P2_Yellow', (1.6, -0.25, 2.2), (0.9, 0.06, 1.0), bevel=0.0, rot=(math.radians(20), 0, math.radians(15)))  # hanging flap
        disc(g, 'MT_Icing', (-0.2, -0.08, 3.1), 1.0, 0.08, rot=(math.radians(90), 0, 0), seg=20)              # face
        for k in range(7):                                                                                     # orange hair puffs
            a = math.radians(-20 + k * 37)
            if 80 < math.degrees(a) < 100:
                continue
            g.blob('MT_P1_Orange', (-0.2 + 1.05 * math.cos(a), -0.12, 3.1 + 0.95 * math.sin(a) * 0.6 + 0.2), 0.36, sz=(1, 0.5, 1), seg=8)
        g.blob('MT_P0_Red', (-0.2, -0.2, 3.05), 0.2, seg=10)                                                  # nose
        g.blob('MT_Button', (-0.55, -0.15, 3.4), 0.12, sz=(1, 0.4, 1.3), seg=8)
        g.blob('MT_Hollow', (0.15, -0.15, 3.4), 0.13, sz=(1, 0.4, 1.3), seg=8)                               # scratched-out eye
        g.tube([(-0.85, -0.14, 2.75), (-0.2, -0.16, 2.45), (0.45, -0.14, 2.75)], 0.07, 'MT_P0_Red', seg=6)   # stiff grin
        g.rbox('MT_P1_Orange', (0, -0.1, 0.55), (3.2, 0.06, 0.55), bevel=0.02)                               # name band
    mesh_obj(u, 'Body', build)


@unit('TW_TicketDoll')
def tw_ticket_doll(u):
    def build(g):
        g.rbox('MT_P1_Orange', (0, 0, 0), (2.4, 2.0, 1.2), bevel=0.06)                                        # counter base
        g.rbox('MT_P2_Yellow', (0, -1.0, 0.2), (2.42, 0.04, 0.8), bevel=0.0)                                  # front stripe panel
        for x in (-1.15, 1.15):
            g.rbox('MT_P1_Orange', (x, 0, 1.2), (0.1, 2.0, 1.5), bevel=0.02)                                  # side posts
        g.rbox('MT_P1_Orange', (0, 0.95, 1.2), (2.4, 0.1, 1.5), bevel=0.02)                                   # back wall
        g.lathe([(1.75, 0), (0, 1.1)], 'MT_P2_Yellow', seg=8, loc=(0, 0, 2.7), rot=(0, 0, math.radians(22.5)),
                segmat=lambda k: 'MT_P2_Yellow' if k % 2 == 0 else 'MT_P1_Orange', scale=(1, 0.85, 1))       # striped roof
        doll(g, 'MT_Icing', 1.05, (0, 0.3, 0.75), eyes=('MT_Button', 'MT_Button'), smile='MT_P0_Red', arms=(True, False))  # frozen-smile doll
        g.lathe([(0.2, 0), (0.18, 0.25), (0, 0.27)], 'MT_P3_Lime', seg=10, loc=(0.0, 0.3, 2.0))               # little hat
        g.rbox('MT_P3_Lime', (-0.4, -0.5, 1.2), (0.3, 0.02, 0.18), bevel=0.0, rotz=math.radians(10))         # ticket on counter
        g.blob('MT_P0_Red', (0.11, 0.07, 1.98), 0.05, seg=6)                                                  # painted cheek
        g.blob('MT_P0_Red', (-0.11, 0.07, 1.98), 0.05, seg=6)
    mesh_obj(u, 'Body', build)


@unit('TW_DeflatedBalloons')
def tw_deflated_balloons(u):
    def build(g):
        g.tube([(0, 0, 0), (0, 0, 2.6)], 0.08, 'MT_Wood', seg=6)
        g.rbox('MT_P1_Orange', (0, 0, 0), (0.7, 0.7, 0.2), bevel=0.05)
        cols = ['MT_P3_Lime', 'MT_P2_Yellow', 'MT_P1_Orange', 'MT_P3_Lime', 'MT_P2_Yellow', 'MT_P1_Orange']
        for i, c in enumerate(cols):
            a = i * 1.05
            if i < 3:                                                                                          # still hanging, half deflated
                p = (math.cos(a) * 0.7, math.sin(a) * 0.7, 2.2 - i * 0.25)
                vs = g.blob(c, p, 0.45, sz=(1, 1, 0.75), seg=10, jitter=0.15, rnd=RND)
                sag(vs, p[2], 0.6)
                g.tube([(0, 0, 2.5), p], 0.015, 'MT_Icing', seg=4)
            else:                                                                                              # flat on the ground
                p = (math.cos(a) * 1.3, math.sin(a) * 1.3, 0.06)
                g.blob(c, p, 0.55, sz=(1, 0.85, 0.12), seg=10, jitter=0.2, rnd=RND)
                g.tube([(0, 0, 2.4), (p[0] * 0.6, p[1] * 0.6, 0.8), p], 0.015, 'MT_Icing', seg=4)
    mesh_obj(u, 'Body', build)


# =====================================================================================
# HAUNTED BAKERY (★★) — palette fill: yellow, lime, green, purple
# =====================================================================================
@unit('TW_CrackedWeddingCake')
def tw_cracked_wedding_cake(u):
    def build(g):
        g.lathe([(1.4, 0), (1.42, 0.9), (0, 0.9)], 'MT_Icing', seg=20)
        g.lathe([(1.0, 0), (1.02, 0.8), (0, 0.8)], 'MT_Icing', seg=18, loc=(0, 0, 0.9))
        top = g.lathe([(0.65, 0), (0.67, 0.7), (0, 0.7)], 'MT_Icing', seg=16, loc=(0.08, 0, 1.7), rot=(0, math.radians(6), 0))
        jag(top, 2.3, 0.05)
        for z, r, m in ((0.75, 1.43, 'MT_P8_Purple'), (1.55, 1.03, 'MT_P3_Lime'), (2.22, 0.68, 'MT_P8_Purple')):
            g.lathe([(r, 0), (r + 0.03, 0.06), (r, 0.12)], m, seg=18, loc=(0, 0, z))
        g.rbox('MT_Hollow', (1.0, -1.0, 0.1), (0.1, 0.1, 1.95), bevel=0.0, rot=(0, math.radians(8), math.radians(-45)))  # crack seam
        doll(g, 'MT_Icing', 0.45, (0.0, 0.0, 2.4), eyes=('MT_Button', None), smile=None, arms=(True, False))          # last doll on top
        g.lathe([(0.12, 0), (0.1, 0.1), (0, 0.12)], 'MT_P2_Yellow', seg=8, loc=(0.0, 0.0, 3.0))
        for k in range(6):
            a = RND.uniform(0, 2 * math.pi)
            g.blob('MT_P4_Green', (1.42 * math.cos(a), 1.42 * math.sin(a), RND.uniform(0.2, 0.7)), 0.12, sz=(1, 1, 0.8), seg=6)  # sugar flowers
    mesh_obj(u, 'Body', build)
    def fallen(g):
        doll(g, 'MT_Icing', 0.45, (0, 0, 0), eyes=('MT_Button', 'MT_Button'), smile=None)
    f = mesh_obj(u, 'Part_FallenDoll', fallen)
    f.location = (1.8, -0.8, 0.12)
    f.rotation_euler = (math.radians(90), 0, math.radians(40))


@unit('TW_JarDoll')
def tw_jar_doll(u):
    def build(g):
        g.rbox('MT_Wood', (0, 0, 0), (1.5, 1.5, 0.6), bevel=0.06)                                             # stand
        g.lathe([(0.65, 0), (0.7, 0.08), (0.62, 0.35), (0, 0.35)], 'MT_P4_Green', seg=16, loc=(0, 0, 0.6))   # base cap
        g.lathe([(0, 0), (0.66, 0), (0.72, 0.15), (0.74, 1.4), (0.6, 1.65), (0.5, 1.7), (0, 1.7)], 'MT_Glass', seg=24, loc=(0, 0, 0.7))
        g.lathe([(0.56, 0), (0.58, 0.18), (0, 0.22)], 'MT_P2_Yellow', seg=16, loc=(0, 0, 2.38))                # lid
        g.blob('MT_P2_Yellow', (0, 0, 2.65), 0.1, seg=8)
        g.rbox('MT_P4_Green', (0, -0.75, 0.78), (0.7, 0.03, 0.2), bevel=0.0)                                   # label (below the doll)
        doll(g, 'MT_Cookie', 0.75, (0.05, 0.05, 1.0), eyes=('MT_Button', 'MT_Button'), buttons=('MT_P8_Purple',), lean=0.35)  # floating doll
        for k in range(6):
            g.blob('MT_Glass', (RND.uniform(-0.4, 0.4), RND.uniform(-0.4, 0.4), RND.uniform(1.0, 2.2)), RND.uniform(0.04, 0.08), seg=6)
    mesh_obj(u, 'Body', build)


@unit('TW_FlourSack')
def tw_flour_sack(u):
    def build(g):
        vs = g.blob('MT_P3_Lime', (0, 0, 0.55), 0.6, sz=(0.9, 0.7, 1.0), seg=12, jitter=0.08, rnd=RND)       # sack
        jag(vs, 0.9, 0.12)
        g.tube([(0, 0, 1.1), (0.0, 0.0, 1.3)], 0.18, 'MT_P3_Lime', seg=8)
        g.tube([(-0.2, 0, 1.2), (0.2, 0, 1.2)], 0.06, 'MT_P8_Purple', seg=6)                                 # tie
        g.blob('MT_Hollow', (0.25, -0.45, 0.5), 0.2, sz=(1, 0.3, 1.4), seg=8)                                 # tear
        for k in range(6):                                                                                     # flour pile
            g.blob('MT_Flour', (0.4 + RND.uniform(-0.3, 0.6), -0.9 + RND.uniform(-0.3, 0.3), 0), RND.uniform(0.25, 0.45), sz=(1, 1, 0.35), seg=8)
        g.rbox('MT_P2_Yellow', (-0.8, 0.3, 0), (0.6, 0.6, 0.6), bevel=0.05, rotz=0.4)                           # crate beside
    mesh_obj(u, 'Body', build)
    def prints(g):
        for i in range(8):                                                                                      # floury footprints walk away
            side = 0.18 if i % 2 else -0.18
            g.blob('MT_Flour', (side, -1.6 - i * 0.55, 0.0), 0.11, sz=(1, 1.7, 0.08), seg=8)
            g.blob('MT_Flour', (side, -1.42 - i * 0.55, 0.0), 0.06, sz=(1.2, 1, 0.08), seg=6)
    mesh_obj(u, 'Decal_Prints', prints)


# =====================================================================================
# WAITING ROOM (★★)
# =====================================================================================
@unit('TW_HangingJellyBears')
def tw_hanging_jelly_bears(u):
    def build(g):
        for x in (-2.2, 2.2):
            g.tube([(x, 0, 0), (x, 0, 2.9)], 0.1, 'MT_P8_Purple', seg=8,
                   ringmat=lambda i: 'MT_P8_Purple' if i % 2 == 0 else 'MT_Icing')
            g.blob('MT_P2_Yellow', (x, 0, 3.0), 0.16, seg=8)
        g.tube([(-2.2, 0, 2.85), (0, 0, 2.45), (2.2, 0, 2.85)], 0.03, 'MT_Icing', seg=5)                      # sagging garland
        cols = ['MT_P0_Red', 'MT_P4_Green', 'MT_P2_Yellow', 'MT_P1_Orange', 'MT_P0_Red']
        for i, c in enumerate(cols):
            x = -1.6 + i * 0.8
            top = 2.45 + abs(x) / 2.2 * 0.4 - 0.05
            g.tube([(x, 0, top + 0.05), (x, 0, top - 0.45)], 0.012, 'MT_Icing', seg=4)                       # ornament string on head top
            z = top - 0.9
            tilt = math.radians(RND.uniform(-12, 12))
            g.blob(c, (x, 0, z), 0.2, sz=(1, 0.8, 1.15), rot=(0, tilt, 0), seg=10)                            # body
            g.blob(c, (x, 0, z + 0.32), 0.15, sz=(1, 0.85, 1), seg=10)                                       # head
            for sx in (-1, 1):
                g.blob(c, (x + 0.11 * sx, 0, z + 0.45), 0.06, seg=6)                                          # ears
                g.blob(c, (x + 0.1 * sx, 0, z - 0.22), 0.08, sz=(1, 1, 1.3), seg=6)                         # legs
                g.blob(c, (x + 0.2 * sx, 0, z + 0.05), 0.07, sz=(1.3, 1, 1), seg=6)                          # arms
            g.blob('MT_Button', (x - 0.05, -0.13, z + 0.34), 0.025, seg=6)
            if i != 2:
                g.blob('MT_Button', (x + 0.05, -0.13, z + 0.34), 0.025, seg=6)                              # one bear lost an eye
    mesh_obj(u, 'Body', build)


# =====================================================================================
# HIDING SPOTS (TwistedCandyPlan.md §2.3, V4) — enterable, 2+ doorways the monster fits through (≥ 6 m wide, 8 m high),
# partitions that break sight lines, palette-coloured furniture. Collision is explicit (COL_* boxes); visuals have none.
# Furniture either touches a wall or leaves ≥ 5 m (no cookie-only slots, MapPassabilityCheck).
# Empties: Light_* (interior / exit lights, Unity adds Light), Zone_* (indoor sound box: object scale = box size).
# =====================================================================================
def col_box(u, name, x, y, z, sx, sy, sz, rotz=0.0):
    def build(g):
        g.rbox('MT_Hollow', (0, 0, 0), (sx, sy, sz), bevel=0.0)
    ob = mesh_obj(u, 'COL_' + name, build)
    ob.location = (x, y, z)
    ob.rotation_euler = (0, 0, rotz)
    return ob


def marker(u, name, loc, size=(1, 1, 1)):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.5
    COL.objects.link(ob)
    ob.location = loc
    ob.scale = size
    UNITS.setdefault(u, []).append(ob)
    return ob


TENT_R, TENT_WALL, TENT_SEG = 10.0, 9.0, 10   # fits the map's 20 m tents it replaces
CRATE_X = 7.6                                   # crate rows: back face 1.4 m behind, touching the round wall
CURTAIN_Y = 3.5                                 # 5.8 m entry hall behind each door, 6.6 m between the curtains


@unit('TW_Shelter_CircusTent')
def tw_shelter_circus_tent(u):
    seg_angle = 2 * math.pi / TENT_SEG
    chord = 2 * TENT_R * math.sin(seg_angle / 2) + 0.1
    doors = {2, 7}                                  # segment centres at 90° (+Y, back) and 270° (−Y, front); chord 6.2 m

    def seg_centre(k):
        a = (k + 0.5) * seg_angle
        return a, TENT_R * math.cos(seg_angle / 2) * math.cos(a), TENT_R * math.cos(seg_angle / 2) * math.sin(a)

    def body(g):
        for k in range(TENT_SEG):
            a, x, y = seg_centre(k)
            mat = 'MT_P0_Red' if k % 2 == 0 else 'MT_Canvas_Torn'
            if k in doors:                          # lintel over the doorway + torn flaps on both sides
                g.rbox(mat, (x, y, 8.0), (0.3, chord, TENT_WALL - 8.0), bevel=0.0, rot=(0, 0, a))
                for side in (-1, 1):
                    fx, fy = x + math.cos(a + math.pi / 2) * side * (chord / 2 - 0.4), y + math.sin(a + math.pi / 2) * side * (chord / 2 - 0.4)
                    vs = g.rbox('MT_Canvas_Torn', (fx + math.cos(a) * 0.4, fy + math.sin(a) * 0.4, 3.0), (0.08, 1.2, 5.2), bevel=0.0, rot=(0, math.radians(14), a + side * 0.5))
                    jag([v for v in vs], -1, 0.0)
                g.rbox('MT_Exit', (x - math.cos(a) * 0.4, y - math.sin(a) * 0.4, 8.6), (0.2, 1.4, 0.5), bevel=0.05, rot=(0, 0, a))   # glowing exit lamp inside
                continue
            vs = g.rbox(mat, (x, y, 0), (0.3, chord, TENT_WALL), bevel=0.0, rot=(0, 0, a))
            if k in (1, 5):
                jag(vs, TENT_WALL - 0.3, 0.6)       # torn top edge
        roof = g.lathe([(TENT_R + 0.8, 0), (1.0, 6.0), (0.2, 6.6)], 'MT_P0_Red', seg=TENT_SEG * 2, loc=(0, 0, TENT_WALL - 0.4),
                       segmat=lambda k: 'MT_P0_Red' if k % 2 == 0 else 'MT_Canvas_Torn', cap=False)
        jag(roof, -1, 0.0)
        for v in roof:                              # sagging, torn hem
            if v.co.z < TENT_WALL:
                v.co.z += RND.uniform(-0.5, 0.2)
        g.tube([(0, 0, TENT_WALL + 6.6), (0, 0, 8.6)], 0.06, 'MT_Metal', seg=5)              # chain from the peak (no king pole —
        g.lathe([(0.1, 0), (1.3, 0.25), (1.2, 0.45), (0, 0.5)], 'MT_Gold', seg=10, loc=(0, 0, 8.1), rot=(math.radians(9), 0, 0))  # pole/curtain slots too narrow for the monster)
        for k in range(6):                                                                   # broken chandelier, a few candles left
            if k in (1, 4):
                continue
            a = k * math.pi / 3
            g.tube([(1.1 * math.cos(a), 1.1 * math.sin(a), 8.5), (1.1 * math.cos(a), 1.1 * math.sin(a), 8.9)], 0.07, 'MT_Icing', seg=5)
        g.lathe([(TENT_R - 0.5, 0), (TENT_R - 0.5, 0.06), (0, 0.06)], 'MT_FloorBoard', seg=TENT_SEG * 2)   # sawdust floor
        for y0 in (-CURTAIN_Y, CURTAIN_Y):           # curtain partitions (break the sight line door-to-door)
            g.rbox('MT_P7_Navy', (0, y0, 0), (4.0, 0.25, 6.0), bevel=0.04)
            for x0 in (-2.0, 2.0):
                g.tube([(x0, y0, 0), (x0, y0, 6.4)], 0.12, 'MT_Gold', seg=6)
            g.rbox('MT_P9_Magenta', (0, y0 - 0.15 * (1 if y0 < 0 else -1), 5.2), (4.0, 0.1, 0.8), bevel=0.0)  # tassel band
        # palette crate rows against the side walls (3 crates each, touching the wall), small crates stacked on top
        for x0, row, tops in ((CRATE_X, ('MT_P0_Red', 'MT_P1_Orange', 'MT_P2_Yellow'), ('MT_P8_Purple', 'MT_P6_Blue')),
                              (-CRATE_X, ('MT_P3_Lime', 'MT_P4_Green', 'MT_P5_Teal'), ('MT_P6_Blue', 'MT_P8_Purple'))):
            for i, m in enumerate(row):
                g.rbox(m, (x0, (i - 1) * 2.85, 0), (2.8, 2.8, 2.8), bevel=0.08)
            g.rbox(tops[0], (x0, -1.4, 2.8), (1.8, 1.8, 1.8), bevel=0.06, rotz=0.3)
            g.rbox(tops[1], (x0, 2.0, 2.8), (1.6, 1.6, 1.6), bevel=0.06, rotz=-0.4)
    mesh_obj(u, 'Body', body)

    for k in range(TENT_SEG):
        if k in doors:
            continue
        a, x, y = seg_centre(k)
        col_box(u, 'Wall_%02d' % k, x, y, 0, 0.5, chord, TENT_WALL, rotz=a)
    for i, y0 in enumerate((-CURTAIN_Y, CURTAIN_Y)):
        col_box(u, 'Curtain_%d' % i, 0, y0, 0, 4.4, 0.4, 6.0)
    for i, x0 in enumerate((CRATE_X, -CRATE_X)):
        col_box(u, 'Crates_%d' % i, x0, 0, 0, 2.8, 8.5, 2.8)
    marker(u, 'Light_Inside_A', (0, -6.5, 7.0)); marker(u, 'Light_Inside_B', (0, 6.5, 7.0))
    marker(u, 'Light_Exit_Front', (0, -8.6, 7.6)); marker(u, 'Light_Exit_Back', (0, 8.6, 7.6))
    # indoor sound boxes stay inside the round wall (r 9.5): a cross + a square
    marker(u, 'Zone_A', (0, 0, 4.5), (17.0, 8.6, 9.0)); marker(u, 'Zone_B', (0, 0, 4.5), (8.6, 17.0, 9.0))
    marker(u, 'Zone_C', (0, 0, 4.5), (13.2, 13.2, 9.0))


HX, HY, HWALL, HT = 8.0, 7.0, 9.0, 0.6       # gingerbread house half sizes (fits a 15 m cottage lot), wall height, wall thickness


@unit('TW_Shelter_GingerHouse')
def tw_shelter_ginger_house(u):
    # wall pieces (x0, x1, y0, y1, z0, z1) — doorways: front x∈[-3,3], back x∈[1.5,7.5]; window left y∈[-2,2] z∈[1.2,4.2]
    T = HT
    walls = [
        (-HX, -3.0, -HY - T / 2, -HY + T / 2, 0, HWALL), (3.0, HX, -HY - T / 2, -HY + T / 2, 0, HWALL),          # front
        (-3.0, 3.0, -HY - T / 2, -HY + T / 2, 8.0, HWALL),                                                    # front lintel
        (-HX, 1.5, HY - T / 2, HY + T / 2, 0, HWALL), (7.5, HX, HY - T / 2, HY + T / 2, 0, HWALL),               # back
        (1.5, 7.5, HY - T / 2, HY + T / 2, 8.0, HWALL),
        (-HX - T / 2, -HX + T / 2, -HY, -2.0, 0, HWALL), (-HX - T / 2, -HX + T / 2, 2.0, HY, 0, HWALL),          # left + window
        (-HX - T / 2, -HX + T / 2, -2.0, 2.0, 0, 1.2), (-HX - T / 2, -HX + T / 2, -2.0, 2.0, 4.2, HWALL),
        (HX - T / 2, HX + T / 2, -HY, HY, 0, HWALL),                                                          # right
        (-1.2, -0.8, 0.0, HY, 0, HWALL),                                                                      # partition
    ]
    inner = HT / 2
    furniture = [  # (name, mat, x0, x1, y0, y1, h)
        ('Cupboard', 'MT_P2_Yellow', -HX + inner, -4.2, HY - inner - 1.2, HY - inner, 3.2),
        ('Bookshelf', 'MT_P6_Blue', -HX + inner, -HX + inner + 0.9, 2.6, HY - inner - 1.2, 3.4),
        ('Barrels', 'MT_P5_Teal', -HX + inner, -HX + inner + 1.6, -HY + inner, -HY + inner + 1.6, 2.8),
        ('Bed', 'MT_P4_Green', -0.8, 1.4, 3.5, HY - inner, 1.0),
        ('Sofa', 'MT_P3_Lime', HX - inner - 1.4, HX - inner, 0.0, 3.5, 1.4),
        ('Chest', 'MT_P0_Red', HX - inner - 1.6, HX - inner, -HY + inner, -HY + inner + 1.0, 1.0),
        ('Fireplace', 'MT_Metal', HX - inner - 0.7, HX - inner, -HY + inner + 1.0, -HY + inner + 3.5, 3.0),
    ]

    def body(g):
        for i, (x0, x1, y0, y1, z0, z1) in enumerate(walls):
            vs = g.rbox('MT_Gingerbread', ((x0 + x1) / 2, (y0 + y1) / 2, z0), (x1 - x0, y1 - y0, z1 - z0), bevel=0.05)
            if i == 10:
                for v in vs:                                  # cracked corner: the right wall's front top sags
                    if v.co.y < -5 and v.co.z > HWALL - 0.5:
                        v.co.z -= 1.2
        g.rbox('MT_CookieDark', (HX + T / 2, -HY + 1.0, 6.2), (0.1, 0.25, 2.6), bevel=0.0, rot=(math.radians(18), 0, 0))   # crack
        g.prism('MT_CookieDark', (0, 0, HWALL), 2 * HX + 0.4, 2 * HY + 0.4, 5.0, overhang=0.8)                       # roof
        for side in (-1, 1):                                  # icing eaves
            g.tube([(-HX - 1.2, side * (HY + 1.2), HWALL - 0.05), (HX + 1.2, side * (HY + 1.2), HWALL - 0.05)], 0.22, 'MT_Icing', seg=8)
        g.tube([(-HX - 1.2, 0, HWALL + 5.0), (HX + 1.2, 0, HWALL + 5.0)], 0.25, 'MT_Icing', seg=8)
        for k in range(8):                                    # gumdrops on the ridge (two fell off)
            if k in (2, 5):
                continue
            g.blob(['MT_P0_Red', 'MT_P2_Yellow', 'MT_P4_Green', 'MT_P8_Purple', 'MT_P9_Magenta'][k % 5], (-7 + k * 2, 0, HWALL + 5.3), 0.45, sz=(1, 1, 0.8), seg=10)
        g.rbox('MT_Gingerbread', (5.0, 2.5, HWALL + 2.0), (1.4, 1.4, 4.5), bevel=0.05)        # chimney
        for x0, x1, yy in ((-3.0, 3.0, -HY - T / 2 - 0.05), (1.5, 7.5, HY + T / 2 + 0.05)):  # icing door frames + exit lamps
            for xx in (x0, x1):
                g.rbox('MT_Icing', (xx, yy, 0), (0.35, 0.2, 8.0), bevel=0.05)
            g.rbox('MT_Icing', ((x0 + x1) / 2, yy, 8.0), (x1 - x0 + 0.35, 0.2, 0.35), bevel=0.05)
            g.rbox('MT_Exit', ((x0 + x1) / 2, yy + (0.5 if yy < 0 else -0.5), 7.4), (1.2, 0.2, 0.45), bevel=0.05)
        for z in (1.2, 4.2):                                  # window sill / head
            g.rbox('MT_Icing', (-HX - T / 2 - 0.05, 0, z - 0.1), (0.2, 4.4, 0.2), bevel=0.04)
        g.rbox('MT_P7_Navy', (-HX + 0.5, -2.6, 1.2), (0.1, 1.2, 3.2), bevel=0.0, rot=(0, 0, 0.0))  # torn curtain
        g.rbox('MT_P7_Navy', (-HX + 0.5, 2.3, 2.0), (0.1, 0.8, 2.4), bevel=0.0, rot=(math.radians(10), 0, 0))
        g.rbox('MT_P9_Magenta', (HX + T / 2 + 0.1, 3.0, 2.0), (0.15, 1.6, 2.6), bevel=0.0, rot=(math.radians(25), 0, 0))  # hanging broken shutter
        g.rbox('MT_FloorBoard', (0, 0, 0), (2 * HX - 0.2, 2 * HY - 0.2, 0.06), bevel=0.0)
        for name, mat, x0, x1, y0, y1, h in furniture:
            g.rbox(mat, ((x0 + x1) / 2, (y0 + y1) / 2, 0), (x1 - x0, y1 - y0, h), bevel=0.08)
        g.rbox('MT_P1_Orange', (HX - inner - 0.75, -HY + inner + 2.25, 0.3), (0.1, 1.6, 1.2), bevel=0.02)   # embers in the fireplace
        g.rbox('MT_P8_Purple', (3.5, -2.5, 0.06), (4.0, 3.0, 0.03), bevel=0.0)              # rug
        g.blob('MT_Pastel', (-5.8, HY - 1.2, 3.6), 0.35, sz=(1, 1, 1.2), seg=8)             # cracked doll on the cupboard
    mesh_obj(u, 'Body', body)

    for i, (x0, x1, y0, y1, z0, z1) in enumerate(walls):
        col_box(u, 'Wall_%02d' % i, (x0 + x1) / 2, (y0 + y1) / 2, z0, x1 - x0, y1 - y0, z1 - z0)
    for name, mat, x0, x1, y0, y1, h in furniture:
        col_box(u, name, (x0 + x1) / 2, (y0 + y1) / 2, 0, x1 - x0, y1 - y0, h)
    marker(u, 'Light_Inside_A', (-4.5, -2.0, 7.0)); marker(u, 'Light_Inside_B', (3.5, 2.0, 7.0))
    marker(u, 'Light_Exit_Front', (0, -HY + 1.0, 7.0)); marker(u, 'Light_Exit_Back', (4.5, HY - 1.0, 7.0))
    marker(u, 'Zone_A', (0, 0, HWALL / 2), (2 * HX - HT, 2 * HY - HT, HWALL))


# =====================================================================================
def export_unit(u, path):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in UNITS[u]:
        ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
                             bake_anim=False, path_mode='RELATIVE', embed_textures=False, use_custom_props=False)


def main():
    reset_scene()
    os.makedirs(FBX_DIR, exist_ok=True)
    done = []
    for u, fn in BUILDS:
        if ONLY and not any(u.startswith(p) for p in ONLY):
            continue
        fn(u)
        export_unit(u, os.path.join(FBX_DIR, u + '.fbx'))
        for ob in UNITS[u]:
            ob.name = u + '|' + ob.name
        done.append(u)
    print('TWISTED_PROPS_OK', len(done), ','.join(done))


main()
