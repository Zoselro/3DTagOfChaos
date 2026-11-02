# TagOfChaos environment asset library (exec'd after maplib, same namespace).
# Every asset = ONE mesh datablock 'A_<Name>' with several material slots, origin at bottom centre,
# front = -Y. Instances share the mesh (Unity: one prefab per asset).
# Style guide: round simple silhouettes, exaggerated proportions, no tiny details, low-gloss + glowing magic.
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Quaternion


class G:
    """Tiny geometry kit on top of bmesh."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []
        self.keep_winding = set()   # faces whose winding is deliberate (inward rooms, double-sided jambs) — mesh() won't flip them

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _xf(self, verts, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
        Mx = Matrix.Translation(Vector(loc)) @ Euler_to_mat(rot) @ Matrix.Diagonal((*scale, 1))
        for v in verts:
            v.co = Mx @ v.co

    def lathe(self, prof, mat, seg=24, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), radial=None, segmat=None,
              cap=True):
        """Revolve profile [(r, z), ...] (bottom -> top) around Z. radial(a)->radius multiplier,
        segmat(k)->material name for angular segment k (stripes)."""
        bm = self.bm
        rings = []
        new = []
        for r, z in prof:
            if r < 1e-4:
                v = bm.verts.new((0, 0, z))
                rings.append([v])
                new.append(v)
            else:
                ring = []
                for k in range(seg):
                    a = 2 * math.pi * k / seg
                    rr = r * (radial(a) if radial else 1.0)
                    v = bm.verts.new((rr * math.cos(a), rr * math.sin(a), z))
                    ring.append(v)
                    new.append(v)
                rings.append(ring)
        faces = []
        for A, B in zip(rings[:-1], rings[1:]):
            for k in range(seg):
                if len(A) == 1 and len(B) == 1:
                    continue
                if len(A) == 1:
                    f = bm.faces.new((A[0], B[k], B[(k + 1) % seg]))
                elif len(B) == 1:
                    f = bm.faces.new((A[k], A[(k + 1) % seg], B[0]))
                else:
                    f = bm.faces.new((A[k], A[(k + 1) % seg], B[(k + 1) % seg], B[k]))
                f.material_index = self.mi(segmat(k) if segmat else mat)
                faces.append(f)
        if cap:
            for ring, top in ((rings[0], False), (rings[-1], True)):
                if len(ring) > 2:
                    f = bm.faces.new(ring if top else list(reversed(ring)))
                    f.material_index = self.mi(mat)
        self._xf(new, loc, rot, scale)
        return new

    def tube(self, pts, radii, mat, seg=12, ringmat=None, cap=True):
        """Sweep a circle along pts (list of xyz); radii float or list. ringmat(i)->material for segment i."""
        bm = self.bm
        P0 = [Vector(p) for p in pts]
        R0 = radii if isinstance(radii, (list, tuple)) else [radii] * len(P0)
        # resample so segments are <= ~0.3 m (smooth bends, fine candy stripes)
        P, R = [P0[0]], [R0[0]]
        for i in range(1, len(P0)):
            n = max(1, int(math.ceil((P0[i] - P0[i - 1]).length / 0.3)))
            for k in range(1, n + 1):
                t = k / n
                P.append(P0[i - 1].lerp(P0[i], t))
                R.append(R0[i - 1] + (R0[i] - R0[i - 1]) * t)
        T = []
        for i in range(len(P)):
            a = P[max(i - 1, 0)]
            b = P[min(i + 1, len(P) - 1)]
            T.append((b - a).normalized())
        n = T[0].orthogonal().normalized()
        rings = []
        for i, (p, t) in enumerate(zip(P, T)):
            n = (n - t * n.dot(t))
            if n.length < 1e-6:
                n = t.orthogonal()
            n.normalize()
            b = t.cross(n)
            ring = [bm.verts.new(p + (n * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * R[i])
                    for k in range(seg)]
            rings.append(ring)
        for i, (A, B) in enumerate(zip(rings[:-1], rings[1:])):
            for k in range(seg):
                f = bm.faces.new((A[k], A[(k + 1) % seg], B[(k + 1) % seg], B[k]))
                f.material_index = self.mi(ringmat(i) if ringmat else mat)
        if cap:
            for ring, rev in ((rings[0], True), (rings[-1], False)):
                f = bm.faces.new(list(reversed(ring)) if rev else ring)
                f.material_index = self.mi(mat)

    def rbox(self, mat, loc, size, bevel=0.15, rotz=0.0, rot=None):
        bm = self.bm
        res = bmesh.ops.create_cube(bm, size=1)
        vs = res['verts']
        for v in vs:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2] + size[2] / 2))
        edges = list({e for v in vs for e in v.link_edges})
        if bevel > 0:
            out = bmesh.ops.bevel(bm, geom=vs + edges, offset=min(bevel, min(size) * 0.45), segments=2,
                                  affect='EDGES', profile=0.5)
            vs = list({v for f in out['faces'] for v in f.verts} | set(v for v in vs if v.is_valid))
        vs = [v for v in vs if v.is_valid]
        for f in {f for v in vs for f in v.link_faces}:
            f.material_index = self.mi(mat)
        self._xf(vs, loc, rot if rot else (0, 0, rotz))
        return vs

    def blob(self, mat, loc, r, sz=(1, 1, 1), seg=16, jitter=0.0, rnd=None, rot=(0, 0, 0)):
        bm = self.bm
        res = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
        vs = res['verts']
        if jitter and rnd:
            for v in vs:
                v.co *= 1 + rnd.uniform(-jitter, jitter)
        for v in vs:
            v.co = Vector((v.co.x * sz[0], v.co.y * sz[1], v.co.z * sz[2]))
        for f in {f for v in vs for f in v.link_faces}:
            f.material_index = self.mi(mat)
        self._xf(vs, loc, rot)
        return vs

    def prism(self, mat, loc, L, D, H, rotz=0.0, overhang=0.0):
        bm = self.bm
        L2, D2 = L / 2 + overhang, D / 2 + overhang
        v = [bm.verts.new(p) for p in ((-L2, -D2, 0), (L2, -D2, 0), (L2, D2, 0), (-L2, D2, 0), (-L2, 0, H), (L2, 0, H))]
        for fi in ((0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5), (0, 3, 2, 1)):
            f = bm.faces.new([v[i] for i in fi])
            f.material_index = self.mi(mat)
            f.smooth = False
        self._xf(v, loc, (0, 0, rotz))

    def mesh(self, name, smooth_angle=35):
        me = bpy.data.meshes.new(name)
        for f in self.bm.faces:
            f.smooth = True
        self.bm.normal_update()
        bmesh.ops.recalc_face_normals(self.bm, faces=[f for f in self.bm.faces if f not in self.keep_winding])
        self.bm.to_mesh(me)
        self.bm.free()
        for mt in self.mats:
            me.materials.append(M(mt))
        try:
            me.set_sharp_from_angle(angle=math.radians(smooth_angle))
        except Exception:
            pass
        return me


def Euler_to_mat(rot):
    from mathutils import Euler
    return Euler(rot).to_matrix().to_4x4()


# ---------- curve helpers ----------
def arc(c, r, a0, a1, n, plane='xz', z=0.0):
    pts = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        if plane == 'xz':
            pts.append((c[0] + r * math.cos(a), c[1], c[2] + r * math.sin(a)))
        else:
            pts.append((c[0] + r * math.cos(a), c[1] + r * math.sin(a), c[2]))
    return pts


def bend(p0, p1, bulge, n=10, side=(1, 0, 0)):
    """Quadratic bezier from p0 to p1 bulging sideways."""
    p0, p1 = Vector(p0), Vector(p1)
    mid = (p0 + p1) / 2 + Vector(side) * bulge
    return [tuple((1 - t) ** 2 * p0 + 2 * (1 - t) * t * mid + t * t * p1) for t in [i / n for i in range(n + 1)]]


def stripes(a, b, every=2):
    return lambda i: a if (i // every) % 2 == 0 else b


# =====================================================================================
# ASSET BUILDERS  (each: def f(g, rnd) ; registered in ASSETS)
# =====================================================================================
ASSETS = {}


def asset(name):
    def deco(fn):
        ASSETS[name] = fn
        return fn
    return deco


# ---------------- shared ----------------
def _rock(g, rnd, mat, inner=None, r=1.6):
    g.blob(mat, (0, 0, r * 0.55), r, sz=(1.25, 1.0, 0.8), seg=10, jitter=0.12, rnd=rnd)
    if inner:   # broken candy: a sliced face showing the swirl
        bm = g.bm
        res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0.5, 0, 0),
                                     plane_no=(1, 0.3, 0.2), clear_outer=True)
        edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            fill = bmesh.ops.edgeloop_fill(bm, edges=edges)
            for f in fill['faces']:
                f.material_index = g.mi(inner)


@asset('CandyRock_Mint')
def _(g, rnd): _rock(g, rnd, 'M_Candy_Teal')


@asset('CandyRock_Choco')
def _(g, rnd): _rock(g, rnd, 'M_Chocolate_Dark', r=1.9)


@asset('CandyRock_Broken')
def _(g, rnd): _rock(g, rnd, 'M_Candy_Pink', inner='M_Candy_White', r=1.7)


@asset('GumdropBush_A')
def _(g, rnd):
    for i, (x, y, s, mt) in enumerate(((0, 0, 1.3, 'M_Leaf_Teal'), (1.3, 0.4, 0.95, 'M_Candy_Purple'),
                                       (-1.1, 0.5, 1.0, 'M_Leaf_Teal'), (0.2, -1.1, 0.8, 'M_Candy_Pink'))):
        g.lathe([(s, 0), (s, s * 0.3), (s * 0.85, s * 0.8), (s * 0.5, s * 1.1), (0, s * 1.2)], mt, seg=16, loc=(x, y, 0))


@asset('GumdropBush_B')
def _(g, rnd):
    for x, y, s in ((0, 0, 1.6), (1.6, -0.3, 1.1), (-1.5, -0.2, 1.2), (0.3, 1.3, 0.9), (-0.4, -1.3, 0.8)):
        g.lathe([(s, 0), (s, s * 0.3), (s * 0.85, s * 0.8), (s * 0.5, s * 1.1), (0, s * 1.2)],
                rnd.choice(('M_Leaf_Purple', 'M_Leaf_Teal')), seg=16, loc=(x, y, 0))
    g.blob('M_Magic_Gem', (0.3, -0.4, 2.2), 0.35, seg=10)


@asset('CandyFlower_Patch')
def _(g, rnd):
    for k in range(6):
        a = k * 1.1 + rnd.uniform(-0.3, 0.3)
        d = rnd.uniform(0.3, 1.4)
        x, y, h = d * math.cos(a), d * math.sin(a), rnd.uniform(0.8, 1.5)
        g.tube(bend((x, y, 0), (x + 0.1, y, h), 0.15, 6), 0.07, 'M_Leaf_Teal', seg=6)
        petal = rnd.choice(('M_Candy_Pink', 'M_Candy_Purple', 'M_Jelly_Orange'))
        g.lathe([(0, -0.05), (0.45, 0), (0.42, 0.08), (0, 0.1)], petal, seg=20, loc=(x + 0.1, y, h),
                rot=(rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), 0),
                radial=lambda a: 0.55 + 0.45 * abs(math.cos(2.5 * a)))
        g.blob('M_Lantern_Glow', (x + 0.1, y, h + 0.1), 0.14, seg=8)


@asset('MagicCrystal_Cluster')
def _(g, rnd):
    for k in range(5):
        h = rnd.uniform(1.2, 2.8)
        r = h * 0.18
        g.lathe([(r, 0), (r, h * 0.75), (0, h)], rnd.choice(('M_Magic_Gem', 'M_Neon_Teal')), seg=6,
                loc=(rnd.uniform(-0.7, 0.7), rnd.uniform(-0.7, 0.7), -0.1),
                rot=(rnd.uniform(-0.45, 0.45), rnd.uniform(-0.45, 0.45), rnd.uniform(0, 1)))
    g.blob('M_Rock', (0, 0, 0), 0.9, sz=(1.2, 1.1, 0.4), seg=10, jitter=0.1, rnd=rnd)


def _mush_small(g, rnd, x, y, h, r, cap):
    g.tube(bend((x, y, 0), (x, y, h), rnd.uniform(-0.15, 0.15) * h, 5), [r * 0.3] * 3 + [r * 0.22] * 3,
           'M_Mushroom_Stem', seg=10)
    g.lathe([(0, -0.05), (r, 0), (r * 0.95, r * 0.25), (r * 0.6, r * 0.55), (0, r * 0.65)], cap, seg=18,
            loc=(x, y, h - r * 0.1))


@asset('SmallMushroom_Cluster')
def _(g, rnd):
    for k in range(5):
        a = k * 1.3
        _mush_small(g, rnd, 0.9 * math.cos(a) * (k > 0), 0.9 * math.sin(a) * (k > 0), rnd.uniform(0.7, 1.6),
                    rnd.uniform(0.45, 0.8), rnd.choice(('M_Mushroom_Glow', 'M_Mushroom_Cap')))


@asset('CookieBench')
def _(g, rnd):
    for y in (-0.25, 0.25):
        g.rbox('M_Cookie_Light', (0, y, 0.45), (3.0, 0.45, 0.18), 0.08)
    g.rbox('M_Cookie_Light', (0, 0.42, 0.75), (3.0, 0.16, 0.6), 0.07)
    for x in (-1.2, 1.2):
        g.lathe([(0.35, 0), (0.35, 0.45), (0, 0.46)], 'M_Chocolate_Milk', seg=16, loc=(x, 0, 0))


@asset('CandyLantern_Post')
def _(g, rnd):
    pts = [(0, 0, z) for z in (0, 1, 2, 3)] + arc((0.55, 0, 3.0), 0.55, math.pi, 0, 8)[1:]
    g.tube(pts, 0.14, 'M_Candy_Red', seg=10, ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
    g.lathe([(0, -0.55), (0.3, -0.5), (0.42, -0.25), (0.35, 0.0), (0, 0.05)], 'M_Lantern_Glow', seg=16,
            loc=(1.1, 0, 2.95))


@asset('CookieCrate_A')
def _(g, rnd):
    g.rbox('M_Wood_Light', (0, 0, 0), (1.6, 1.6, 1.6), 0.06)
    for z in (0.15, 1.45):
        g.rbox('M_Wood_Dark', (0, 0, z - 0.1), (1.7, 1.7, 0.2), 0.04)
    g.lathe([(0.45, 0), (0.45, 0.08), (0, 0.1)], 'M_Cookie_Light', seg=20, loc=(0, -0.82, 0.8), rot=(math.pi / 2, 0, 0))


@asset('CookieCrate_B')
def _(g, rnd):
    g.rbox('M_Wood_Dark', (0, 0, 0), (2.0, 1.4, 1.2), 0.06)
    g.rbox('M_Chocolate_Milk', (0, 0, 1.2), (2.1, 1.5, 0.15), 0.05)
    for x in (-0.5, 0.5):
        g.lathe([(0.3, 0), (0.3, 0.1), (0, 0.12)], 'M_Candy_Pink', seg=12, loc=(x, -0.72, 0.6), rot=(math.pi / 2, 0, 0))


@asset('WitchSign_A')
def _(g, rnd):
    g.tube(bend((0, 0, 0), (0.1, 0, 3.0), 0.2, 6), 0.14, 'M_Wood_Dark', seg=8)
    g.rbox('M_Wood_Light', (0.2, -0.1, 2.0), (2.2, 0.15, 0.9), 0.08, rot=(0, 0.12, 0))
    g.lathe([(0.55, 0), (0.55, 0.06), (0.25, 0.1), (0, 0.9)], 'M_Candy_Purple', seg=12, loc=(0.3, -0.12, 2.95),
            rot=(0, 0.3, 0))
    g.blob('M_Magic_Rune', (0.2, -0.2, 2.45), 0.2, seg=8)


@asset('WitchSign_B')
def _(g, rnd):
    for x in (-0.9, 0.9):
        g.tube([(x, 0, 0), (x, 0, 2.2)], 0.12, 'M_Bark', seg=8)
    g.rbox('M_Gingerbread_Dark', (0, -0.05, 1.3), (2.4, 0.18, 0.8), 0.1)
    g.lathe([(0.35, 0), (0.35, 0.08), (0, 0.1)], 'M_Magic_Rune', seg=6, loc=(0, -0.16, 1.7), rot=(math.pi / 2, 0, 0))
    g.lathe([(0, 0), (0.3, 0.05), (0.3, 0.25), (0, 0.3)], 'M_Lantern_Glow', seg=10, loc=(0.9, 0, 2.2))


# ---------------- CandyForest ----------------
@asset('CandyTree_Swirl')
def _(g, rnd):
    trunk = bend((0, 0, 0), (0, 0, 8.2), 0.6, 12, side=(1, 0, 0))
    g.tube(trunk, [0.55 - 0.02 * i for i in range(13)], 'M_Candy_White', seg=12,
           ringmat=stripes('M_Candy_White', 'M_Candy_Pink', 1))
    c = (0, 0, 11.2)
    g.lathe([(0, -0.35), (3.4, -0.3), (3.5, 0.0), (3.4, 0.3), (0, 0.35)], 'M_Candy_Pink', seg=36, loc=c,
            rot=(math.pi / 2, 0, 0))
    spiral = [(c[0] + (0.25 + 0.5 * t) * math.cos(t * 1.0), -0.38, c[2] + (0.25 + 0.5 * t) * math.sin(t * 1.0))
              for t in [i * 0.2 for i in range(31)]]
    g.tube(spiral, 0.32, 'M_Candy_White', seg=8)
    spiral_b = [(x, 0.38, z) for x, _, z in spiral]
    g.tube(spiral_b, 0.32, 'M_Candy_White', seg=8)
    for s in (1, -1):   # wrapper leaves (cartoon bow)
        g.lathe([(0, 0), (0.9, 0.6), (1.4, 1.6), (0, 2.0)], 'M_Leaf_Teal', seg=10, loc=(s * 0.6, 0, 7.8),
                rot=(0, s * 1.1, 0))


@asset('CandyTree_Wrapped')
def _(g, rnd):
    g.tube(bend((0, 0, 0), (0.5, 0, 7.0), -0.8, 12), [0.7 - 0.03 * i for i in range(13)], 'M_Cookie_Wall', seg=12)
    for (x, y, z, s, mt, a) in ((0.5, 0, 9.0, 2.3, 'M_Candy_Purple', 0.2), (-1.4, 0.8, 7.9, 1.7, 'M_Candy_Pink', 1.2),
                                (2.3, -0.6, 7.7, 1.6, 'M_Candy_Teal', -0.7)):
        g.blob(mt, (x, y, z), s, sz=(1.25, 1, 0.95), seg=18, rot=(0, 0, a))
        for sgn in (1, -1):
            dx, dy = math.cos(a) * s * 1.2 * sgn, math.sin(a) * s * 1.2 * sgn
            g.lathe([(0.2, 0), (0.6 * s, s * 0.55), (0.25 * s, s * 0.7), (0, s * 0.72)], mt, seg=10,
                    loc=(x + dx, y + dy, z), rot=(0, sgn * math.pi / 2, a) if abs(math.cos(a)) > 0.5 else
                    (sgn * math.pi / 2, 0, a))


@asset('CandyTree_Puff')
def _(g, rnd):
    g.tube(bend((0, 0, 0), (-0.8, 0.3, 6.5), 1.0, 12), [0.6 - 0.025 * i for i in range(13)], 'M_Chocolate_Milk', seg=12)
    for (x, y, z, s) in ((-0.8, 0.3, 8.2, 2.5), (1.2, 0.2, 7.6, 1.8), (-2.6, 0.6, 7.4, 1.7), (-0.6, -1.4, 7.3, 1.6),
                         (-0.5, 1.8, 7.6, 1.6), (0.2, 0.0, 9.6, 1.6)):
        g.blob(rnd.choice(('M_Icing_Pink', 'M_Icing_Purple', 'M_Candy_Pink')), (x, y, z), s, seg=16, jitter=0.04, rnd=rnd)
    g.blob('M_Magic_Gem', (-0.3, -1.9, 8.4), 0.4, seg=10)


def _twisted(g, rnd, kind):
    lean = rnd.uniform(-1.2, 1.2)
    trunk = []
    for i in range(14):
        t = i / 13
        trunk.append((lean * t * t + 0.5 * math.sin(t * 5.0), 0.4 * math.cos(t * 4.0), 10.5 * t))
    g.tube(trunk, [1.05 - 0.06 * i for i in range(14)], 'M_Bark', seg=12)
    top = Vector(trunk[-3])
    for k in range(4):   # finger branches: out, up, then hook
        a = k * 1.57 + rnd.uniform(-0.4, 0.4)
        o = Vector((math.cos(a), math.sin(a), 0))
        base = Vector(trunk[9 + k % 3])
        pts = [base, base + o * 1.5 + Vector((0, 0, 0.8)), base + o * 3.0 + Vector((0, 0, 2.2)),
               base + o * 3.6 + Vector((0, 0, 3.4)), base + o * 3.2 + Vector((0, 0, 4.0))]
        g.tube([tuple(p) for p in pts], [0.38, 0.3, 0.22, 0.16, 0.13], 'M_Bark', seg=8)
        g.blob(rnd.choice(('M_Leaf_Purple', 'M_Leaf_Teal')), tuple(base + o * 3.4 + Vector((0, 0, 3.6))),
               rnd.uniform(1.3, 1.9), sz=(1, 1, 0.75), seg=12, jitter=0.1, rnd=rnd)
    g.blob('M_Leaf_Purple', tuple(top + Vector((0, 0, 1.2))), 2.2, sz=(1.1, 1.1, 0.8), seg=14, jitter=0.1, rnd=rnd)
    if kind == 'A':   # watching eye on the trunk
        p = Vector(trunk[4]) + Vector((0, -0.95, 0))
        g.blob('M_Candy_White', tuple(p), 0.55, sz=(1, 0.5, 0.75), seg=14)
        g.blob('M_Magic_Gem', tuple(p + Vector((0, -0.25, 0))), 0.25, sz=(1, 0.5, 1), seg=10)
    elif kind == 'B':  # glowing rune band
        c = Vector(trunk[3])
        g.tube(arc(tuple(c), 1.05, 0, 2 * math.pi, 16, plane='xy'), 0.16, 'M_Magic_Rune', seg=6)
    else:             # hanging lantern
        p = Vector(trunk[10]) + Vector((2.6, 0, 0.8))
        g.tube([tuple(p + Vector((0, 0, 1.2))), tuple(p + Vector((0, 0, 0.3)))], 0.05, 'M_Metal_Dark', seg=5)
        g.lathe([(0, -0.5), (0.35, -0.35), (0.4, 0), (0, 0.3)], 'M_Lantern_Glow', seg=12, loc=tuple(p))


for _k in 'ABC':
    ASSETS[f'TwistedTree_{_k}'] = (lambda k: (lambda g, rnd: _twisted(g, rnd, k)))(_k)


def _giant_mushroom(g, rnd, cap, tilt, spots):
    stem = bend((0, 0, 0), (tilt * 1.5, 0, 6.2), tilt * 1.2, 10)
    g.tube(stem, [1.1, 1.0, 0.9, 0.82, 0.76, 0.72, 0.7, 0.7, 0.72, 0.76, 0.8], 'M_Mushroom_Stem', seg=14)
    top = stem[-1]
    rot = (0, tilt * 0.35, 0)
    g.lathe([(0.6, -0.3), (4.2, 0), (4.3, 0.45), (3.6, 1.6), (2.2, 2.5), (0, 2.8)], cap, seg=32, loc=top, rot=rot)
    g.lathe([(0.7, -0.29), (4.0, -0.02), (0.7, -0.2)], 'M_Mushroom_Glow', seg=32, loc=top, rot=rot, cap=False)
    Rm = Euler_to_mat(rot)
    for k in range(spots):
        a = 2 * math.pi * k / spots + rnd.uniform(-0.2, 0.2)
        rr = rnd.uniform(1.5, 3.2)
        zz = 2.8 * (1 - (rr / 4.3) ** 2) + 0.1
        p = Vector(top) + Rm.to_3x3() @ Vector((rr * math.cos(a), rr * math.sin(a), zz))
        g.blob('M_Sugar_White', tuple(p), rnd.uniform(0.35, 0.55), sz=(1, 1, 0.45), seg=10)


ASSETS['GiantMushroom_A'] = lambda g, rnd: _giant_mushroom(g, rnd, 'M_Mushroom_Cap', 0.5, 7)
ASSETS['GiantMushroom_B'] = lambda g, rnd: _giant_mushroom(g, rnd, 'M_Candy_Pink', -0.8, 9)
ASSETS['GiantMushroom_C'] = lambda g, rnd: _giant_mushroom(g, rnd, 'M_Candy_Purple', 0.1, 6)


@asset('Pine_A')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 5)], 0.9, 'M_Bark', seg=8)
    for i, (r, z, h) in enumerate(((6.0, 3, 7), (4.8, 7, 6.5), (3.4, 11, 6), (2.0, 15, 5))):
        g.lathe([(0, z), (r, z + 0.6), (r * 0.9, z + 1.0), (0, z + h)], 'M_Pine_Dark', seg=14,
                radial=lambda a, i=i: 1 + 0.12 * math.sin(7 * a + i))


@asset('Pine_B')
def _(g, rnd):
    g.tube(bend((0, 0, 0), (0.5, 0, 4), 0.4, 5), 0.8, 'M_Bark', seg=8)
    for i, (r, z, h) in enumerate(((5.0, 2.5, 8), (3.6, 7.5, 7), (2.2, 12.5, 6))):
        g.lathe([(0, z), (r, z + 0.5), (r * 0.85, z + 1.1), (0, z + h)], 'M_Leaf_Teal', seg=12,
                radial=lambda a, i=i: 1 + 0.15 * math.sin(5 * a + 2 * i))


@asset('Lollipop_Swirl')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 4.6)], 0.2, 'M_Candy_White', seg=10)
    c = (0, 0, 6.4)
    g.lathe([(0, -0.25), (1.9, -0.22), (2.0, 0), (1.9, 0.22), (0, 0.25)], 'M_Candy_Purple', seg=32, loc=c,
            rot=(math.pi / 2, 0, 0))
    sp = [(c[0] + (0.15 + 0.3 * t) * math.cos(t), -0.26, c[2] + (0.15 + 0.3 * t) * math.sin(t))
          for t in [i * 0.2 for i in range(30)]]
    g.tube(sp, 0.2, 'M_Candy_White', seg=6)
    g.tube([(x, 0.26, z) for x, _, z in sp], 0.2, 'M_Candy_White', seg=6)


@asset('CandyCane_Arch')
def _(g, rnd):
    pts = [(-3, 0, 0), (-3, 0, 1.5), (-3, 0, 3)] + arc((0, 0, 3), 3, math.pi, 0, 16)[1:] + [(3, 0, 1.5), (3, 0, 0)]
    g.tube(pts, 0.4, 'M_Candy_Red', seg=12, ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
    g.lathe([(0, -0.6), (0.6, -0.4), (0.6, 0.2), (0, 0.5)], 'M_Lantern_Glow', seg=14, loc=(0, 0, 5.2))


@asset('Marshmallow_Stack')
def _(g, rnd):
    z = 0
    for k, (r, h) in enumerate(((1.3, 1.4), (1.1, 1.2), (0.9, 1.0))):
        g.lathe([(0, 0), (r * 0.9, 0), (r, h * 0.15), (r, h * 0.85), (r * 0.9, h), (0, h)],
                ('M_Icing_Pink', 'M_Sugar_White', 'M_Icing_Purple')[k], seg=20, loc=(0.15 * k, 0.1 * k, z),
                rot=(0.05 * k, -0.06 * k, 0))
        z += h * 0.98


@asset('Macaron_Rock')
def _(g, rnd):
    g.lathe([(0, 0), (1.9, 0.05), (2.0, 0.4), (1.7, 0.8), (0, 0.95)], 'M_Candy_Teal', seg=28, rot=(0.1, 0, 0))
    g.lathe([(0, 0.85), (1.75, 0.85), (1.75, 1.25), (0, 1.25)], 'M_Sugar_White', seg=28, rot=(0.1, 0, 0))
    g.lathe([(0, 1.2), (1.7, 1.25), (2.0, 1.6), (1.9, 2.0), (0, 2.15)], 'M_Candy_Teal', seg=28, rot=(0.1, 0, 0))


# =====================================================================================
# REFERENCE-IMAGE DRIVEN ASSETS (C:\Program Files\Blender Foundation\Map)
# =====================================================================================
def _swirl_disc(g, c, R, base, stripe, thick=0.6, face_rot=(math.pi / 2, 0, 0)):
    """Flat candy disc with a raised white spiral on both faces (lollipop)."""
    g.lathe([(0, -thick / 2), (R * 0.97, -thick / 2), (R, 0), (R * 0.97, thick / 2), (0, thick / 2)], base, seg=40,
            loc=c, rot=face_rot)
    k = R / 13.4                      # two full turns of the white swirl
    ca, sa = math.cos(face_rot[2]), math.sin(face_rot[2])
    for y in (-thick / 2 - 0.02, thick / 2 + 0.02):
        pts = []
        for t in [i * 0.2 for i in range(64)]:
            lx, ly, lz = (0.1 + k * t) * math.cos(t), y, (0.1 + k * t) * math.sin(t)
            pts.append((c[0] + lx * ca - ly * sa, c[1] + lx * sa + ly * ca, c[2] + lz))   # follow the disc's facing
        g.tube(pts, R * 0.075, stripe, seg=8)


# ---- CandyForest (사탕숲): pink/white sugar world ----
@asset('Lollipop_GiantPink')
def _(g, rnd):
    g.tube(bend((0, 0, 0), (0.3, 0, 6.5), 0.4, 8), 0.3, 'M_Candy_White', seg=10)
    _swirl_disc(g, (0.3, 0, 10.2), 3.8, 'M_Candy_Pink', 'M_Candy_White', 0.9)


@asset('Lollipop_SmallPink')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 3.2)], 0.14, 'M_Candy_White', seg=8)
    _swirl_disc(g, (0, 0, 4.6), 1.5, 'M_Icing_Pink', 'M_Candy_White', 0.4)


@asset('CandyCane_Tall')
def _(g, rnd):
    pts = [(0, 0, z) for z in (0, 1.5, 3, 4.5, 6)] + arc((0.9, 0, 6.0), 0.9, math.pi, 0.1, 10)[1:]
    g.tube(pts, 0.32, 'M_Candy_White', seg=12, ringmat=stripes('M_Candy_White', 'M_Candy_Red', 1))


@asset('CandyStick_Bundle')
def _(g, rnd):
    for k in range(4):
        a = k * 1.6
        x, y = 0.5 * math.cos(a), 0.5 * math.sin(a)
        h = rnd.uniform(2.5, 4.2)
        g.tube([(x, y, 0), (x + rnd.uniform(-0.5, 0.5), y + rnd.uniform(-0.5, 0.5), h)], 0.16, 'M_Candy_White', seg=8,
               ringmat=stripes('M_Candy_White', rnd.choice(('M_Candy_Pink', 'M_Candy_Red')), 1), cap=True)


@asset('CakeSlice')
def _(g, rnd):
    bm = g.bm
    R, A = 2.4, math.radians(55)
    layers = (('M_Cookie_Light', 0.0, 0.5), ('M_Icing_Pink', 0.5, 0.75), ('M_Cookie_Light', 0.75, 1.25),
              ('M_Icing_Pink', 1.25, 1.5), ('M_Sugar_White', 1.5, 1.8))
    for mat, z0, z1 in layers:
        pts = [(0, 0)] + [(R * math.cos(A * i / 6 - A / 2), R * math.sin(A * i / 6 - A / 2)) for i in range(7)]
        bot = [bm.verts.new((x, y, z0)) for x, y in pts]
        top = [bm.verts.new((x, y, z1)) for x, y in pts]
        n = len(pts)
        for i in range(n):
            f = bm.faces.new((bot[i], bot[(i + 1) % n], top[(i + 1) % n], top[i]))
            f.material_index = g.mi(mat)
        for ring, rev in ((bot, True), (top, False)):
            f = bm.faces.new(list(reversed(ring)) if rev else ring)
            f.material_index = g.mi(mat)
    g.blob('M_Candy_Red', (R * 0.7, 0, 2.05), 0.3, seg=10)


def _bare_tree(g, rnd, bark, h, n_branch):
    trunk = [(0.3 * math.sin(t * 3), 0.2 * math.cos(t * 2), h * 0.55 * t) for t in [i / 8 for i in range(9)]]
    g.tube(trunk, [h * 0.045 * (1 - 0.5 * i / 8) for i in range(9)], bark, seg=10)

    def branch(p, d, L, r, depth):
        if depth == 0 or r < 0.07:
            return
        end = p + d * L
        mid = p + d * (L * 0.5) + Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 0.2)) * L * 0.3
        g.tube([tuple(p), tuple(mid), tuple(end)], [r, r * 0.8, r * 0.6], bark, seg=6)
        for _ in range(2):
            nd = (d + Vector((rnd.uniform(-0.8, 0.8), rnd.uniform(-0.8, 0.8), rnd.uniform(0.1, 0.6)))).normalized()
            branch(end, nd, L * 0.7, r * 0.62, depth - 1)

    top = Vector(trunk[-1])
    for k in range(n_branch):
        a = 2 * math.pi * k / n_branch + rnd.uniform(-0.3, 0.3)
        d = Vector((math.cos(a), math.sin(a), rnd.uniform(0.6, 1.2))).normalized()
        branch(top - Vector((0, 0, rnd.uniform(0, h * 0.15))), d, h * 0.25, h * 0.028, 3)


ASSETS['BareTree_Pink_A'] = lambda g, rnd: _bare_tree(g, rnd, 'M_Bark_Pink', 16, 4)
ASSETS['BareTree_Pink_B'] = lambda g, rnd: _bare_tree(g, rnd, 'M_Bark_Pink', 22, 5)


@asset('SugarDrift')
def _(g, rnd):
    for k in range(4):
        g.blob('M_Sugar_White', (rnd.uniform(-2, 2), rnd.uniform(-1.5, 1.5), 0), rnd.uniform(1.2, 2.2),
               sz=(1.4, 1.0, 0.35), seg=14, jitter=0.05, rnd=rnd)


# ---- GingerbreadVillage (과자마을): night, waffle roofs, glowing gumdrops ----
def _waffle_roof(g, x0, W, D, H, zb, over=0.8):
    g.prism('M_Waffle', (x0, 0, zb), W, D, H, overhang=over)
    # waffle grid ribs on both slopes
    L2, D2 = W / 2 + over, D / 2 + over
    slope = math.atan2(H, D2)
    for s in (-1, 1):
        for i in range(1, 6):
            t = i / 6
            y, z = s * D2 * (1 - t), zb + H * t
            g.tube([(x0 - L2, y, z + 0.08), (x0 + L2, y, z + 0.08)], 0.09, 'M_Chocolate_Milk', seg=5, cap=True)
        for j in range(1, 8):
            xx = x0 - L2 + 2 * L2 * j / 8
            g.tube([(xx, s * D2, zb + 0.08), (xx, 0, zb + H + 0.08)], 0.09, 'M_Chocolate_Milk', seg=5, cap=True)
    # snowy icing blanket on the ridge
    g.tube([(x0 - L2 - 0.1, 0, zb + H), (x0 + L2 + 0.1, 0, zb + H)], 0.55, 'M_Sugar_White', seg=10)
    for s in (-1, 1):   # wavy icing trim along the eaves
        pts = [(x0 - L2 + 2 * L2 * i / 24, s * (D2 - 0.05), zb + 0.05 - 0.18 * abs(math.sin(i * 1.3))) for i in range(25)]
        g.tube(pts, 0.2, 'M_Sugar_White', seg=6)


def _ginger_house(g, rnd, W, D, Hw, Hr, cursed=False, tower=False, twin=False):
    wall = 'M_Cookie_Wall'
    g.rbox('M_Cookie_Stone', (0, 0, 0), (W + 0.8, D + 0.8, 0.35), 0.1)         # foundation
    g.rbox(wall, (0, 0, 0.3), (W, D, Hw), 0.3)
    if twin:
        _waffle_roof(g, -W / 4, W / 2, D, Hr, Hw + 0.3)
        _waffle_roof(g, W / 4, W / 2, D, Hr * 0.8, Hw + 0.3)
    else:
        _waffle_roof(g, 0, W, D, Hr, Hw + 0.3)
    # icing outlines on the walls
    for x in (-W / 2 - 0.05, W / 2 + 0.05):
        g.tube([(x, -D / 2 - 0.05, 0.4), (x, -D / 2 - 0.05, Hw + 0.2)], 0.14, 'M_Sugar_White', seg=6)
    g.tube([(-W / 2, -D / 2 - 0.06, Hw * 0.55)] + [(-W / 2 + W * i / 12, -D / 2 - 0.06, Hw * 0.55 + 0.15 * math.sin(i * 1.4))
                                                  for i in range(13)], 0.12, 'M_Sugar_White', seg=6)
    # arched door with icing frame
    g.rbox('M_Wood_Dark', (0, -D / 2 - 0.1, 0.3), (1.8, 0.3, 2.6), 0.2)
    g.tube(arc((0, -D / 2 - 0.2, 2.9), 0.9, 0, math.pi, 10), 0.15, 'M_Sugar_White', seg=6)
    # glowing windows (yellow, cursed = purple)
    glass = 'M_Window_Glass_Purple' if cursed else 'M_Window_Yellow'
    for x in (-W * 0.3, W * 0.3):
        g.rbox(glass, (x, -D / 2 - 0.08, Hw * 0.35), (1.8, 0.2, 1.6), 0.1)
        g.rbox('M_Sugar_White', (x, -D / 2 - 0.12, Hw * 0.35 - 0.2), (2.2, 0.25, 0.25), 0.1)
    g.lathe([(0, 0), (0.55, 0.02), (0.55, 0.1), (0, 0.12)], 'M_Sugar_White', seg=16,
            loc=(0, -D / 2 - 0.06, Hw + Hr * 0.45), rot=(math.pi / 2, 0, 0))                  # round attic window
    # candy-cane corner posts
    for x in (-W / 2 - 0.1, W / 2 + 0.1):
        g.tube([(x, -D / 2 - 0.1, 0.3), (x, -D / 2 - 0.1, Hw + 0.2)], 0.22, 'M_Candy_Red', seg=8,
               ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
    if tower:
        g.lathe([(1.6, 0), (1.6, Hw + 3.5), (0, Hw + 3.6)], wall, seg=20, loc=(W / 2 - 0.5, D / 2 - 0.5, 0.3))
        g.lathe([(2.0, 0), (0, 4.5)], 'M_Waffle', seg=20, loc=(W / 2 - 0.5, D / 2 - 0.5, Hw + 3.8))
        g.blob('M_Sugar_White', (W / 2 - 0.5, D / 2 - 0.5, Hw + 3.9), 2.0, sz=(1, 1, 0.25), seg=16)
    g.rbox('M_Chocolate_Dark', (W * 0.25, D * 0.2, Hw + Hr * 0.4), (1.2, 1.2, Hr * 0.9), 0.1)   # chimney
    g.blob('M_Sugar_White', (W * 0.25, D * 0.2, Hw + Hr * 1.3), 0.8, sz=(1, 1, 0.4), seg=12)
    if cursed:
        g.tube(arc((0, -D / 2 - 0.3, Hw * 0.8), 1.2, 0, 2 * math.pi, 12, plane='xz'), 0.12, 'M_Magic_Rune', seg=6)


def _ginger_ruin(g, rnd, W, D, Hw, tower=False):
    """Ruined cookie house (EscapePlan.md P2: GingerbreadVillage -> cookie ruins). The lower 3.4 m is a solid closed block
    (no way in, too tall to climb); broken wall stubs and a collapsed waffle roof sit on top of it."""
    wall, core = 'M_Cookie_Wall', 3.4
    g.rbox('M_Cookie_Stone', (0, 0, 0), (W + 0.8, D + 0.8, 0.35), 0.1)               # foundation
    g.rbox(wall, (0, 0, 0.3), (W, D, core), 0.3)
    n = 5
    for side in range(4):                                                               # ragged wall tops
        L = W if side % 2 == 0 else D
        for i in range(n):
            if rnd.random() < 0.3:
                continue
            h = rnd.uniform(0.5, max(0.8, Hw - core))
            t = -L / 2 + L * (i + 0.5) / n
            if side % 2 == 0:
                loc, size = (t, (D / 2 - 0.4) * (1 if side == 2 else -1), core + 0.25), (L / n * 0.95, 0.8, h)
            else:
                loc, size = ((W / 2 - 0.4) * (1 if side == 1 else -1), t, core + 0.25), (0.8, L / n * 0.95, h)
            g.rbox(wall, loc, size, 0.12, rot=(rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08), 0))
    g.rbox('M_Waffle', (W * 0.08, D * 0.05, core + 0.45), (W * 0.75, D * 0.65, 0.35), 0.1,
           rot=(0.22, -0.14, 0.1))                                                       # collapsed roof
    for x in (-W * 0.3, W * 0.3):                                                       # dark empty windows
        g.rbox('M_Chocolate_Dark', (x, -D / 2 - 0.06, 1.3), (1.6, 0.15, 1.4), 0.05)
    g.rbox('M_Wood_Dark', (0, -D / 2 - 0.1, 0.3), (1.8, 0.3, 2.4), 0.15)                 # boarded door
    for k in range(2):
        g.rbox('M_Wood_Light', (0, -D / 2 - 0.3, 0.9 + k * 0.9), (2.2, 0.12, 0.25), 0.03, rot=(0, 0.3 * (1 - 2 * k), 0))
    for x in (-W / 2 - 0.05, W / 2 + 0.05):                                             # cracked icing remains
        g.tube([(x, -D / 2 - 0.05, 0.4), (x, -D / 2 - 0.05, core - rnd.uniform(0.3, 1.2))], 0.14, 'M_Sugar_White', seg=6)
    if tower:                                                                           # broken round tower stump
        g.lathe([(1.6, 0), (1.6, core + 2.4), (1.2, core + 2.9), (0, core + 3.0)], wall, seg=12,
                loc=(W / 2 - 0.5, D / 2 - 0.5, 0.3), radial=lambda a: 1 + 0.08 * math.sin(a * 4))


@asset('RuinRubble')
def _(g, rnd):
    for k in range(7):
        a, r = rnd.uniform(0, 6.28), rnd.uniform(0.2, 1.6)
        s = rnd.uniform(0.35, 0.9)
        g.rbox(rnd.choice(('M_Cookie_Wall', 'M_Cookie_Stone', 'M_Gingerbread_Dark')), (math.cos(a) * r, math.sin(a) * r, 0),
               (s, s * rnd.uniform(0.6, 1.0), s * rnd.uniform(0.4, 0.8)), 0.08, rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), a))
    g.rbox('M_Waffle', (0.6, -0.4, 0.15), (2.0, 1.2, 0.25), 0.06, rot=(0.35, 0.1, 0.6))     # roof shard


ASSETS['GingerHouse_Cottage'] = lambda g, rnd: _ginger_house(g, rnd, 10, 9, 5.5, 4.5)
ASSETS['GingerHouse_Tall'] = lambda g, rnd: _ginger_house(g, rnd, 8, 8, 8.0, 5.0, tower=True)
ASSETS['GingerHouse_Twin'] = lambda g, rnd: _ginger_house(g, rnd, 13, 9, 5.5, 4.0, twin=True)
ASSETS['GingerHouse_Cursed'] = lambda g, rnd: _ginger_house(g, rnd, 9, 8, 7.0, 5.5, cursed=True)
ASSETS['GingerHouse_Shop'] = lambda g, rnd: _ginger_house(g, rnd, 12, 10, 6.0, 4.0)
ASSETS['GingerRuin_Cottage'] = lambda g, rnd: _ginger_ruin(g, rnd, 10, 9, 6.0)
ASSETS['GingerRuin_Tall'] = lambda g, rnd: _ginger_ruin(g, rnd, 8, 8, 7.5, tower=True)
ASSETS['GingerRuin_Twin'] = lambda g, rnd: _ginger_ruin(g, rnd, 13, 9, 5.5)


@asset('Lollipop_Red')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 5.5)], 0.22, 'M_Candy_White', seg=10)
    _swirl_disc(g, (0, 0, 8.0), 2.6, 'M_Candy_Red', 'M_Candy_White', 0.8)


@asset('CandyCane_Red')
def _(g, rnd):
    pts = [(0, 0, z) for z in (0, 1, 2, 3)] + arc((0.7, 0, 3.0), 0.7, math.pi, 0.2, 9)[1:]
    g.tube(pts, 0.22, 'M_Candy_Red', seg=10, ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))


def _cupcake(g, rnd, frosting, s=1.0):
    g.lathe([(1.2 * s, 0), (1.6 * s, 1.3 * s), (1.65 * s, 1.35 * s), (0, 1.35 * s)], 'M_Jelly_Orange', seg=24,
            radial=lambda a: 1 + 0.06 * math.cos(12 * a))
    g.lathe([(0, 1.2 * s), (1.9 * s, 1.3 * s), (1.8 * s, 1.9 * s), (1.2 * s, 2.5 * s), (0.4 * s, 2.9 * s), (0, 3.0 * s)],
            frosting, seg=24, radial=lambda a: 1 + 0.07 * math.sin(6 * a))
    g.lathe([(1.5 * s, 1.6 * s), (1.95 * s, 1.8 * s), (1.7 * s, 2.1 * s)], 'M_Sugar_White', seg=24, cap=False)
    for k in range(7):   # sprinkles (big enough to read)
        a = k * 0.9
        g.blob(('M_Candy_Pink', 'M_Candy_Teal', 'M_Jelly_Orange')[k % 3],
               (1.1 * s * math.cos(a), 1.1 * s * math.sin(a), 2.5 * s), 0.13 * s, sz=(2.2, 1, 1), seg=6, rot=(0, 0, a))


ASSETS['Cupcake_Choco'] = lambda g, rnd: _cupcake(g, rnd, 'M_Chocolate_Milk')
ASSETS['Cupcake_Pink'] = lambda g, rnd: _cupcake(g, rnd, 'M_Icing_Pink', 0.9)


def _glowdrop(g, rnd, mat):
    g.lathe([(1.2, 0), (1.15, 0.5), (0.85, 1.0), (0.4, 1.25), (0, 1.3)], mat, seg=20)


ASSETS['GlowDrop_Pink'] = lambda g, rnd: _glowdrop(g, rnd, 'M_Glow_Pink')
ASSETS['GlowDrop_Yellow'] = lambda g, rnd: _glowdrop(g, rnd, 'M_Glow_Yellow')
ASSETS['GlowDrop_Cyan'] = lambda g, rnd: _glowdrop(g, rnd, 'M_Glow_Cyan')


def _creampuff_tree(g, rnd, h):
    g.tube(bend((0, 0, 0), (0.3, 0, h * 0.55), 0.5, 8), [0.55, 0.5, 0.45, 0.42, 0.4, 0.4, 0.42, 0.45, 0.5],
           'M_Bark_Blue', seg=10)
    for (x, y, z, s) in ((0.3, 0, h * 0.72, h * 0.3), (1.6, 0.4, h * 0.62, h * 0.2), (-1.3, 0.3, h * 0.64, h * 0.21)):
        g.blob('M_Sugar_White', (x, y, z), s, sz=(1.1, 1.1, 0.8), seg=18, jitter=0.03, rnd=rnd)
        for k in range(6):   # chocolate chips
            a, b = rnd.uniform(0, 6.28), rnd.uniform(0.2, 1.2)
            p = Vector((x, y, z)) + Vector((math.cos(a) * math.cos(b) * 1.1, math.sin(a) * math.cos(b) * 1.1,
                                            math.sin(b) * 0.8)) * s * 0.97
            g.blob('M_Chocolate_Dark', tuple(p), s * 0.1, seg=8)


ASSETS['CreamPuffTree_A'] = lambda g, rnd: _creampuff_tree(g, rnd, 9)
ASSETS['CreamPuffTree_B'] = lambda g, rnd: _creampuff_tree(g, rnd, 13)


@asset('CandyStreetLamp')
def _(g, rnd):
    pts = [(0, 0, z) for z in (0, 1.5, 3, 4.5)] + arc((0.8, 0, 4.5), 0.8, math.pi, 0, 8)[1:]
    g.tube(pts, 0.18, 'M_Candy_Red', seg=10, ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
    g.lathe([(0.5, 0), (0.45, 0.2), (0, 0.25)], 'M_Chocolate_Dark', seg=16, loc=(0, 0, 0))
    g.blob('M_Glow_Pink', (1.6, 0, 3.9), 0.6, seg=16)
    g.lathe([(0, 0), (0.5, 0.05), (0.2, 0.3), (0, 0.35)], 'M_Chocolate_Dark', seg=12, loc=(1.6, 0, 4.4))


# ---- ChocolateFactory (초콜릿공장) ----
@asset('CandyPipe_Twist')
def _(g, rnd):
    pts = []
    for i in range(25):
        t = i / 24
        pts.append((1.2 * math.sin(t * 7), 1.2 * math.cos(t * 5), 14 * t))
    pts += arc((0, 0.9, 14), 3.5, math.pi, 0, 8, plane='xz')[1:]
    g.tube(pts, 0.75, 'M_Candy_Purple', seg=12, ringmat=stripes('M_Candy_Purple', 'M_Candy_White', 1))


@asset('GingerArch')
def _(g, rnd):
    for x in (-4, 4):
        g.rbox('M_Gingerbread_Dark', (x, 0, 0), (2.2, 2.2, 9), 0.5)
        for z in (2, 4.5, 7):
            g.blob(rnd.choice(('M_Candy_Pink', 'M_Candy_Teal', 'M_Jelly_Orange')), (x, -1.15, z), 0.4, sz=(1, 0.5, 1), seg=10)
    g.tube(arc((0, 0, 9), 4, math.pi, 0, 16), 1.1, 'M_Gingerbread_Dark', seg=12)
    g.tube(arc((0, -0.9, 9), 4, math.pi, 0, 16), 0.3, 'M_Sugar_White', seg=8)


@asset('CakeTower')
def _(g, rnd):
    z = 0
    for k, r in enumerate((3.2, 2.6, 2.0, 1.4, 0.9)):
        h = 1.6
        g.lathe([(0, z), (r, z), (r, z + h), (0, z + h)], 'M_Chocolate_Milk', seg=28)
        g.lathe([(r + 0.05, z + h - 0.1), (r + 0.2, z + h), (r * 0.8, z + h + 0.2), (0, z + h + 0.2)], 'M_Sugar_White',
                seg=28, radial=lambda a: 1 + 0.05 * math.sin(10 * a))
        z += h + 0.1
    g.blob('M_Candy_Teal', (0, 0, z + 0.4), 0.6, seg=12)


@asset('Lollipop_Rainbow')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 2.2)], 0.18, 'M_Candy_White', seg=8)
    c = (0, 0, 3.8)
    g.lathe([(0, -0.35), (1.5, -0.3), (1.6, 0), (1.5, 0.3), (0, 0.35)], 'M_Candy_Pink', seg=36, loc=c,
            rot=(math.pi / 2, 0, 0), segmat=lambda k: ('M_Candy_Pink', 'M_Jelly_Orange', 'M_Glow_Yellow', 'M_Candy_Teal',
                                                         'M_Candy_Purple')[(k // 3) % 5])


@asset('CreamBank')
def _(g, rnd):
    for k in range(6):
        g.blob('M_Sugar_White', (k * 2.2 - 5.5, rnd.uniform(-0.3, 0.3), 0), rnd.uniform(1.0, 1.5), sz=(1.4, 1, 0.55),
               seg=12, jitter=0.05, rnd=rnd)


@asset('CandyMachine_Press')
def _(g, rnd):
    g.rbox('M_Metal_Light', (0, 0, 0), (4, 3.2, 3.6), 0.35)
    g.rbox('M_Candy_Purple', (0, 0, 3.6), (3.4, 2.6, 1.2), 0.3)
    g.lathe([(1.6, 0), (1.9, 1.6), (0, 1.7)], 'M_Candy_Pink', seg=18, loc=(0, 0, 4.8))
    for x in (-2.05, 2.05):   # big gears (simple, readable)
        g.lathe([(0, -0.25), (1.1, -0.25), (1.1, 0.25), (0, 0.25)], 'M_Gold', seg=24, loc=(x, 0, 2.2),
                rot=(0, math.pi / 2, 0), radial=lambda a: 1 + 0.16 * (1 if math.sin(8 * a) > 0 else -1))
    g.blob('M_Warning_Purple', (0, -1.65, 3.0), 0.35, seg=10)
    g.tube(bend((0, -1.6, 1.0), (0, -3.2, 0.2), 0.4, 6, side=(0, 0, 1)), 0.45, 'M_Metal_Dark', seg=10)


@asset('CandyMachine_Mixer')
def _(g, rnd):
    g.lathe([(0, 0), (2.4, 0), (2.6, 2.5), (2.8, 2.7), (2.5, 2.8), (0, 2.6)], 'M_Metal_Light', seg=28)
    g.lathe([(0, 2.5), (2.4, 2.55), (0, 2.6)], 'M_Water_Chocolate', seg=28, cap=False)
    g.rbox('M_Candy_Teal', (-2.8, 0, 0), (1.4, 1.6, 5.5), 0.3)
    g.tube([(-2.8, 0, 5.3), (0, 0, 5.3), (0, 0, 2.0)], 0.3, 'M_Metal_Dark', seg=8)
    g.blob('M_Warning_Purple', (-2.8, -0.85, 4.6), 0.3, seg=10)


@asset('ChocolateBarrel')
def _(g, rnd):
    g.lathe([(0, 0), (0.85, 0), (1.0, 0.8), (0.85, 1.6), (0, 1.6)], 'M_Wood_Dark', seg=20)
    for z in (0.3, 1.3):
        g.lathe([(0.93, z - 0.08), (0.97, z), (0.93, z + 0.08)], 'M_Metal_Dark', seg=20, cap=False)
    g.lathe([(0, 1.58), (0.8, 1.6), (0, 1.62)], 'M_Water_Chocolate', seg=20, cap=False)


# ---- CursedCandyCarnival (저주받은놀이공원) ----
def _striped_cone(g, R, H, z, a, b, n=16, neon=None):
    g.lathe([(R, z), (R * 0.9, z + H * 0.2), (0.3, z + H)], a, seg=n * 2, segmat=lambda k: a if (k // 2) % 2 == 0 else b)
    if neon:
        g.tube(arc((0, 0, z), R + 0.05, 0, 2 * math.pi, 32, plane='xy'), 0.12, neon, seg=6)


@asset('CircusTent_Small')
def _(g, rnd):
    g.lathe([(5, 0), (5, 3), (0, 3.02)], 'M_Tent_Red', seg=32, segmat=lambda k: 'M_Tent_Red' if (k // 2) % 2 == 0 else 'M_Tent_Blue')
    _striped_cone(g, 5.6, 5.5, 3.0, 'M_Tent_Red', 'M_Candy_White', 16, neon='M_Neon_Pink')
    g.tube([(0, 0, 8.4), (0, 0, 10)], 0.1, 'M_Metal_Dark', seg=5)
    g.blob('M_Neon_Teal', (0, 0, 10), 0.35, seg=8)
    g.rbox('M_Chocolate_Dark', (0, -4.9, 0), (2.2, 0.4, 2.6), 0.2)


def _booth_neon(g, rnd, roof_a, roof_b, neon):
    g.rbox('M_Wood_Light', (0, 0, 0), (4.5, 3.5, 1.1), 0.12)
    g.rbox('M_Candy_White', (0, 0.9, 1.1), (4.5, 1.6, 1.6), 0.1)
    for x in (-2.1, 2.1):
        g.tube([(x, -1.6, 1.1), (x, -1.6, 3.0)], 0.1, 'M_Candy_Red', seg=6, ringmat=stripes('M_Candy_Red', 'M_Candy_White', 1))
    g.lathe([(3.4, 0), (0.2, 1.4)], roof_a, seg=16, loc=(0, 0, 3.0), scale=(0.75, 0.62, 1),
            segmat=lambda k: roof_a if k % 2 == 0 else roof_b)
    g.rbox(neon, (0, -1.8, 2.7), (3.2, 0.15, 0.5), 0.1)                    # glowing sign
    g.blob('M_Lantern_Glow', (-1.6, -1.7, 2.7), 0.25, seg=8)
    g.blob('M_Lantern_Glow', (1.6, -1.7, 2.7), 0.25, seg=8)


ASSETS['Booth_Neon_A'] = lambda g, rnd: _booth_neon(g, rnd, 'M_Tent_Red', 'M_Candy_White', 'M_Neon_Pink')
ASSETS['Booth_Neon_B'] = lambda g, rnd: _booth_neon(g, rnd, 'M_Tent_Purple', 'M_Candy_White', 'M_Neon_Teal')
ASSETS['Booth_Neon_C'] = lambda g, rnd: _booth_neon(g, rnd, 'M_Tent_Blue', 'M_Glow_Yellow', 'M_Neon_Purple')


@asset('PumpkinLantern')
def _(g, rnd):
    g.lathe([(0, 0), (0.9, 0.1), (1.3, 0.8), (1.1, 1.5), (0.3, 1.7), (0, 1.65)], 'M_Jelly_Orange', seg=24,
            radial=lambda a: 1 + 0.08 * abs(math.sin(4 * a)))
    g.tube([(0, 0, 1.6), (0.1, 0, 2.1)], 0.15, 'M_Leaf_Teal', seg=6)
    for x in (-0.45, 0.45):
        g.lathe([(0, 0), (0.28, 0), (0, 0.35)], 'M_Glow_Yellow', seg=3, loc=(x, -1.22, 0.95), rot=(math.pi / 2, 0, 0))
    g.rbox('M_Glow_Yellow', (0, -1.25, 0.45), (1.0, 0.1, 0.25), 0.05)


@asset('StringLightPole')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 6)], 0.15, 'M_Metal_Dark', seg=8)
    pts = [(x, 0, 6 - 1.2 * math.sin(math.pi * x / 10)) for x in [i * 0.5 for i in range(21)]]
    g.tube(pts, 0.04, 'M_Metal_Dark', seg=4)
    for i in range(1, 20, 2):
        x, _, z = pts[i]
        g.blob(('M_Neon_Pink', 'M_Glow_Yellow', 'M_Neon_Teal')[i % 3], (x, 0, z - 0.2), 0.18, seg=8)


def _doll(g, rnd, pose, broken):
    g.lathe([(0.9, 0), (1.0, 0.8), (0.7, 1.9), (0, 2.1)], 'M_Cookie_Light', seg=16)       # dress/body
    g.blob('M_Cookie_Wall', (0, 0, 2.7), 0.75, seg=16)                                       # head
    for s in (-1, 1):
        a = pose * s
        g.tube([(s * 0.55, 0, 1.8), (s * (0.9 + 0.3 * math.cos(a)), -0.1, 1.5 + 0.6 * math.sin(a))], 0.16,
               'M_Cookie_Wall', seg=6)
    g.blob('M_Magic_Gem', (-0.28, -0.66, 2.8), 0.16, seg=8)
    g.blob('M_Magic_Gem' if not broken else 'M_Chocolate_Dark', (0.28, -0.66, 2.8), 0.16 if not broken else 0.2, seg=8)
    g.tube(arc((0, -0.68, 2.55), 0.3, math.pi * 1.1, math.pi * 1.9, 6, plane='xz'), 0.04, 'M_Chocolate_Dark', seg=4)
    g.lathe([(0.65, 0), (0.6, 0.3), (0, 0.9)], 'M_Candy_Purple', seg=12, loc=(0, 0, 3.25), rot=(0.25, 0, 0))


ASSETS['CreepyDoll_A'] = lambda g, rnd: _doll(g, rnd, 0.6, False)
ASSETS['CreepyDoll_B'] = lambda g, rnd: _doll(g, rnd, -0.8, True)
ASSETS['CreepyDoll_C'] = lambda g, rnd: _doll(g, rnd, 1.4, False)


@asset('BalloonCluster')
def _(g, rnd):
    g.rbox('M_Wood_Dark', (0, 0, 0), (0.8, 0.8, 0.5), 0.1)
    for k in range(5):
        a = k * 1.25
        top = (1.0 * math.cos(a), 1.0 * math.sin(a), 4.5 + rnd.uniform(-0.6, 0.6))
        g.tube([(0, 0, 0.5), top], 0.03, 'M_Candy_White', seg=4)
        g.blob(('M_Neon_Pink', 'M_Candy_Teal', 'M_Candy_Purple', 'M_Glow_Yellow', 'M_Tent_Red')[k],
               (top[0], top[1], top[2] + 0.75), 0.75, sz=(1, 1, 1.18), seg=16)


@asset('CarnivalLamp')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 5.2)], 0.16, 'M_Metal_Dark', seg=8)
    g.lathe([(0.5, 0), (0.3, 0.4), (0.15, 0.5)], 'M_Metal_Dark', seg=12)
    for k in range(3):
        a = k * 2.09
        g.tube([(0, 0, 4.8), (0.8 * math.cos(a), 0.8 * math.sin(a), 5.3)], 0.07, 'M_Metal_Dark', seg=5)
        g.blob('M_Glow_Yellow', (0.8 * math.cos(a), 0.8 * math.sin(a), 5.1), 0.32, seg=10)


# ---- HauntedBakery (마녀의 유령빵집): warm wood + purple fabric ----
@asset('CandyJar_A')
def _(g, rnd):
    g.lathe([(0, 0), (0.6, 0), (0.75, 0.3), (0.75, 1.2), (0.5, 1.45), (0.5, 1.6), (0, 1.6)], 'M_Glass_Tint', seg=18)
    g.lathe([(0, 1.6), (0.58, 1.6), (0.58, 1.8), (0, 1.9)], 'M_Candy_Purple', seg=18)
    for k in range(6):
        g.blob(('M_Candy_Pink', 'M_Jelly_Orange', 'M_Candy_Teal')[k % 3], (0.3 * math.cos(k), 0.3 * math.sin(k), 0.3 + 0.12 * k),
               0.25, seg=8)


@asset('CandyJar_B')
def _(g, rnd):
    g.lathe([(0, 0), (0.5, 0), (0.5, 0.9), (0.65, 1.4), (0.35, 1.9), (0, 2.0)], 'M_Glass_Tint', seg=18)
    g.blob('M_Icing_Pink', (0, 0, 2.1), 0.3, seg=10)


@asset('Counter_Purple')
def _(g, rnd):
    g.rbox('M_Candy_Purple', (0, 0, 0), (7, 2, 1.1), 0.15)
    g.rbox('M_Wood_Light', (0, 0, 1.1), (7.3, 2.3, 0.18), 0.08)
    for x in (-2.3, 0, 2.3):
        g.lathe([(0, 0), (0.95, 0), (1.0, 0.1), (0, 0.08)], 'M_Metal_Light', seg=24, loc=(x, 0, 1.3))
    for x in (-2.3, 0, 2.3):
        g.lathe([(0.45, 0), (0.45, 0.05), (0, 0.06)], 'M_Gold', seg=4, loc=(x, -1.02, 0.55), rot=(math.pi / 2, 0, 0))


@asset('Shelf_Potion')
def _(g, rnd):
    g.rbox('M_Wood_Dark', (0, 0, 0), (4.5, 1.1, 3.6), 0.12)
    for z in (0.9, 1.9, 2.9):
        g.rbox('M_Wood_Light', (0, -0.1, z), (4.4, 1.1, 0.12), 0.04)
        for k in range(4):
            mt = ('M_Liquid_Purple', 'M_Liquid_Teal', 'M_Liquid_Pink', 'M_Jelly_Orange')[k]
            g.lathe([(0, 0), (0.3, 0), (0.32, 0.4), (0.12, 0.6), (0.12, 0.75), (0, 0.75)], mt, seg=12,
                    loc=(-1.6 + k * 1.05, -0.3, z + 0.12))


@asset('StarRug')
def _(g, rnd):
    g.lathe([(0, 0), (4.0, 0), (4.0, 0.03), (0, 0.04)], 'M_Candy_Purple', seg=40)
    g.lathe([(0, 0.04), (2.4, 0.04), (0, 0.06)], 'M_Glow_Yellow', seg=40,
            radial=lambda a: 0.38 + 0.62 * abs(math.cos(2.5 * a)) ** 4)


@asset('Chair_Purple')
def _(g, rnd):
    g.rbox('M_Wood_Dark', (0, 0, 0.45), (0.9, 0.9, 0.12), 0.05)
    g.rbox('M_Candy_Purple', (0, 0, 0.57), (0.85, 0.85, 0.15), 0.07)
    g.rbox('M_Candy_Purple', (0, 0.38, 0.6), (0.85, 0.18, 0.9), 0.08)
    for x in (-0.35, 0.35):
        for y in (-0.35, 0.35):
            g.tube([(x, y, 0), (x, y, 0.45)], 0.06, 'M_Wood_Dark', seg=6)


@asset('Table_Round')
def _(g, rnd):
    g.lathe([(0, 0.75), (1.0, 0.75), (1.0, 0.85), (0, 0.85)], 'M_Wood_Light', seg=24)
    g.lathe([(0.5, 0), (0.12, 0.2), (0.12, 0.75), (0, 0.75)], 'M_Wood_Dark', seg=12)
    g.lathe([(0, 0.85), (0.9, 0.85), (0.9, 0.87), (0, 0.88)], 'M_Candy_Purple', seg=24)


@asset('CandyBasket')
def _(g, rnd):
    g.lathe([(0, 0), (0.7, 0), (0.95, 0.7), (0.9, 0.75), (0, 0.72)], 'M_Straw', seg=18)
    for k in range(7):
        g.blob(('M_Candy_Pink', 'M_Candy_Purple', 'M_Jelly_Orange')[k % 3],
               (0.45 * math.cos(k), 0.45 * math.sin(k), 0.8), 0.28, seg=8)


@asset('Pumpkin')
def _(g, rnd):
    g.lathe([(0, 0), (0.8, 0.1), (1.1, 0.6), (0.9, 1.1), (0.2, 1.25), (0, 1.2)], 'M_Jelly_Orange', seg=24,
            radial=lambda a: 1 + 0.09 * abs(math.sin(4 * a)))
    g.tube(bend((0, 0, 1.15), (0.2, 0, 1.6), 0.1, 4), 0.12, 'M_Leaf_Teal', seg=6)


@asset('WallLantern_Post')
def _(g, rnd):
    g.tube([(0, 0, 0), (0, 0, 3.5)], 0.13, 'M_Metal_Dark', seg=8)
    g.tube([(0, 0, 3.4), (0.7, 0, 3.6)], 0.07, 'M_Metal_Dark', seg=5)
    g.lathe([(0, -0.6), (0.3, -0.55), (0.35, -0.1), (0.2, 0.05), (0, 0.1)], 'M_Lantern_Glow', seg=12, loc=(0.7, 0, 3.4))


@asset('Chalkboard_Awning')
def _(g, rnd):
    g.rbox('M_Wood_Light', (0, 0, 0), (4.2, 0.3, 3.2), 0.1)
    g.rbox('M_Chalkboard', (0, -0.12, 0.2), (3.8, 0.1, 2.7), 0.05)
    for i in range(8):     # scalloped purple awning
        x = -1.9 + i * 0.54
        g.lathe([(0.3, 0), (0.25, -0.35), (0, -0.4)], ('M_Candy_Purple', 'M_Icing_Purple')[i % 2], seg=10,
                loc=(x, -0.35, 3.4), scale=(1, 0.4, 1))
    g.rbox('M_Candy_Purple', (0, -0.3, 3.35), (4.4, 0.7, 0.2), 0.08)
    for i in range(5):
        g.blob('M_Glow_Yellow', (-1.6 + i * 0.8, -0.5, 3.7), 0.2, seg=8)


@asset('DisplayWindow')
def _(g, rnd):
    g.rbox('M_Wood_Light', (0, 0, 0), (7, 1.4, 1.1), 0.1)
    g.rbox('M_Candy_Purple', (0, 0, 1.1), (7, 1.4, 0.35), 0.08)
    for x in (-3.3, 3.3):
        g.rbox('M_Gold', (x, 0.3, 0), (0.4, 0.4, 4.2), 0.1)
    g.tube(arc((0, 0.3, 4.2), 3.3, 0, math.pi, 14), 0.25, 'M_Gold', seg=8)
    for s in (-1, 1):   # draped purple curtains
        pts = [(s * (3.1 - 0.6 * t), 0.1, 4.6 - 3.2 * t) for t in [i / 6 for i in range(7)]]
        g.tube(pts, [0.35, 0.45, 0.55, 0.6, 0.55, 0.5, 0.45], 'M_Icing_Purple', seg=10)
    g.tube([(-3.2, 0.1, 4.6)] + [(-3.2 + 6.4 * i / 10, 0.1, 4.6 - 0.5 * math.sin(math.pi * i / 10) ** 2) for i in range(11)],
           0.35, 'M_Icing_Purple', seg=10)


@asset('FlourSack_Pile')
def _(g, rnd):
    for k, (x, y, z) in enumerate(((0, 0, 0), (1.3, 0.2, 0), (0.6, -1.0, 0), (0.6, -0.3, 1.0))):
        g.lathe([(0, 0), (0.7, 0.05), (0.8, 0.5), (0.6, 1.0), (0.2, 1.15), (0.25, 1.35), (0, 1.35)], 'M_Flour', seg=14,
                loc=(x, y, z), rot=(rnd.uniform(-0.15, 0.15), rnd.uniform(-0.15, 0.15), 0))
        g.lathe([(0.35, 0), (0.35, 0.02), (0, 0.03)], 'M_Candy_Purple', seg=6, loc=(x, y - 0.76, z + 0.55),
                rot=(math.pi / 2, 0, 0))


@asset('GiantCake')
def _(g, rnd):
    z = 0
    for r, h, mat in ((3.0, 1.8, 'M_Chocolate_Milk'), (2.2, 1.6, 'M_Icing_Pink'), (1.4, 1.4, 'M_Chocolate_Milk')):
        g.lathe([(0, z), (r, z), (r, z + h), (0, z + h)], mat, seg=28)
        g.lathe([(r + 0.05, z + h - 0.4), (r + 0.18, z + h), (r * 0.7, z + h + 0.15), (0, z + h + 0.15)], 'M_Sugar_White',
                seg=28, radial=lambda a: 1 + 0.06 * math.sin(9 * a))
        z += h + 0.1
    g.blob('M_Candy_Red', (0, 0, z + 0.35), 0.45, seg=12)


@asset('DeliveryCart')
def _(g, rnd):
    g.rbox('M_Wood_Light', (0, 0, 1.0), (3.4, 6, 1.6), 0.15)
    g.rbox('M_Candy_Purple', (0, 0, 2.6), (3.6, 6.2, 0.3), 0.1)
    g.lathe([(1.8, 0), (1.8, 2.2), (0, 2.3)], 'M_Icing_Purple', seg=16, loc=(0, 0, 2.9), scale=(1, 1.7, 0.6),
            segmat=lambda k: 'M_Icing_Purple' if k % 2 == 0 else 'M_Sugar_White')
    for sx in (-1, 1):
        for sy in (-1, 1):
            g.lathe([(0, -0.15), (1.0, -0.15), (1.0, 0.15), (0, 0.15)], 'M_Wood_Dark', seg=16,
                    loc=(sx * 1.9, sy * 2.0, 1.0), rot=(0, math.pi / 2, 0), radial=lambda a: 1.0)
    g.rbox('M_Chalkboard', (0, -3.2, 1.6), (2.4, 0.15, 1.2), 0.08)
    g.lathe([(0.45, 0), (0.45, 0.05), (0.2, 0.08), (0, 0.6)], 'M_Candy_Purple', seg=10, loc=(0, -3.3, 2.3))


# =====================================================================================
# instancing API
# =====================================================================================
def get_asset(name):
    me = bpy.data.meshes.get('A_' + name)
    if me is None:
        g = G()
        ASSETS[name](g, random.Random(sum(map(ord, name))))
        me = g.mesh('A_' + name)
    return me


def place(m, name, cat, x, y, rotz=0.0, s=1.0, z=0.0, sxyz=None):
    ob = bpy.data.objects.new(m.nm(name), get_asset(name))
    m.c[cat].objects.link(ob)
    ob.location = (x, y, z)
    ob.rotation_euler = (0, 0, rotz)
    ob.scale = sxyz if sxyz else (s, s, s)
    ob['asset'] = name
    if m._anchor:
        ob['ax'], ob['ay'] = m._anchor[0]
    return ob


def pick(rnd, *names):
    return rnd.choice(names)


# ---- composite helpers re-routed to the asset library (map-aware) ----
_R = random.Random(99)


def candy_tree(m, cat, x, y, h, r, variant=0, rnd=None):
    rnd = rnd or _R
    if m.name == 'CandyForest':
        nm = pick(rnd, 'BareTree_Pink_A', 'BareTree_Pink_B', 'Lollipop_GiantPink', 'CandyCane_Tall', 'CandyTree_Puff',
                  'BareTree_Pink_A', 'BareTree_Pink_B', 'Lollipop_GiantPink')
    elif m.name == 'GingerbreadVillage':
        nm = pick(rnd, 'CreamPuffTree_A', 'CreamPuffTree_B', 'Cupcake_Choco', 'Lollipop_Red')
    else:
        nm = pick(rnd, 'CandyTree_Swirl', 'CandyTree_Wrapped', 'CandyTree_Puff')
    s = h / (14.0 if 'BareTree_Pink_B' in nm else 11.0 if 'Tree' in nm or 'Lollipop' in nm else 6.0)
    return place(m, nm, cat, x, y, rnd.uniform(0, 6.28), max(0.5, s))


def twisted_tree(m, cat, x, y, h, rnd):
    return place(m, pick(rnd, 'TwistedTree_A', 'TwistedTree_B', 'TwistedTree_C'), cat, x, y, rnd.uniform(0, 6.28), h / 12.0)


def mushroom(m, cat, x, y, h, r, glow=True):
    nm = ('GiantMushroom_A', 'GiantMushroom_B', 'GiantMushroom_C')[int(abs(x * 7 + y * 3)) % 3]
    return place(m, nm, cat, x, y, (x * 0.37) % 6.28, h / 8.5)


def pine(m, cat, x, y, h):
    return place(m, ('Pine_A', 'Pine_B')[int(abs(x + y)) % 2], cat, x, y, (x * 0.7) % 6.28, h / 20.0)


def lollipop(m, cat, x, y, h, r, mats=None, face=0.0, base=None):
    nm = {'CandyForest': 'Lollipop_SmallPink', 'GingerbreadVillage': 'Lollipop_Red',
          'ChocolateFactory': 'Lollipop_Rainbow'}.get(m.name, 'Lollipop_Swirl')
    return place(m, nm, cat, x, y, face, (h + 2 * r) / 7.0)


def house(m, x, y, w, d, h, rotz=0.0, roof=None, wall=None, cursed=False, cat='MainStructures', ruined=False):
    k = int(abs(x * 13 + y * 7)) % 4
    nm = 'GingerHouse_Cursed' if cursed else ('GingerHouse_Cottage', 'GingerHouse_Tall', 'GingerHouse_Twin',
                                              'GingerHouse_Cottage')[k]
    if ruined and not cursed:
        nm = nm.replace('GingerHouse_', 'GingerRuin_')
    base_w = {'GingerHouse_Cottage': 10, 'GingerHouse_Tall': 9.5, 'GingerHouse_Twin': 13, 'GingerHouse_Cursed': 9,
              'GingerRuin_Cottage': 10, 'GingerRuin_Tall': 9.5, 'GingerRuin_Twin': 13}[nm]
    s = min(1.25, max(0.8, w / base_w))
    o = place(m, nm, cat, x, y, rotz, s)
    m.blockers.append(((x, y), math.hypot(w, d) / 2))
    return o


def lamp(m, x, y, light=False):
    nm = {'GingerbreadVillage': 'CandyStreetLamp', 'CursedCandyCarnival': 'CarnivalLamp',
          'HauntedBakery': 'WallLantern_Post'}.get(m.name, 'CandyLantern_Post')
    with m.at(x, y):
        place(m, nm, 'GameplayProps', x, y, (x * 0.13) % 6.28)
        if light:
            m.light('Lamp_Light', 'POINT', (x, y, 4.5), (1.0, 0.62, 0.35), 400, 0.5)


# =====================================================================================
# DENSITY: dress the walkable routes like the reference photos (props piled along path edges)
# =====================================================================================
def dress_paths(m, palette, spacing=7.0, band=(1.2, 8.0), cluster=(2, 5), seed=1, cat='Decoration',
                big=None, big_every=4, rings=True, max_r=172):
    """palette: [(asset, scale_min, scale_max), ...]; big: same for occasional large cover props (GameplayProps).
    Clusters sit just outside each road/ring edge, never on the path (clear_of_paths) and never in blockers."""
    rnd = random.Random(seed)
    placed = 0
    edges = []
    for a, b, hw in m.roads:
        L = (b - a).length
        if L < 1:
            continue
        d = (b - a) / L
        n = Vector((-d.y, d.x, 0))
        for k in range(int(L / spacing)):
            t = (k + rnd.uniform(0.2, 0.8)) * spacing
            for s in (1, -1):
                edges.append((a + d * t, n * s, hw))
    if rings:
        for c, r, hw in m.rings:
            for k in range(int(2 * math.pi * r / spacing)):
                ang = (k + rnd.uniform(0.2, 0.8)) * spacing / r
                u = Vector((math.cos(ang), math.sin(ang), 0))
                for s in (1, -1):
                    edges.append((c + u * r, u * s, hw))
    for i, (p, n, hw) in enumerate(edges):
        base = p + n * (hw + rnd.uniform(*band))
        if max(abs(base.x), abs(base.y)) > max_r:
            continue
        use_big = big and i % big_every == 0
        cnt = 1 if use_big else rnd.randint(*cluster)
        with m.at(base.x, base.y):
            for j in range(cnt):
                q = base + Vector((rnd.uniform(-2.2, 2.2), rnd.uniform(-2.2, 2.2), 0)) * (0 if use_big else 1)
                if not m.clear_of_paths((q.x, q.y), 0.8):
                    continue
                nm, s0, s1 = rnd.choice(big if use_big else palette)
                o = place(m, nm, 'GameplayProps' if use_big else cat, q.x, q.y, rnd.uniform(0, 6.28), rnd.uniform(s0, s1))
                o['ax'], o['ay'] = q.x, q.y            # each piece follows the ground under itself
                placed += 1
    return placed
