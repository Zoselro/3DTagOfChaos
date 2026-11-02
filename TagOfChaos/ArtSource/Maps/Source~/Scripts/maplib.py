# TagOfChaos map blockout library (Blender 5.2)
# 1 unit = 1 m, Z-up. Ground = 350 x 350 x 3 m box, top face at z = 0.
# Unity: exported with FBX defaults (Blender -Y -> Unity +Z).
import bpy, bmesh, math, random
from mathutils import Vector, Matrix, Euler

CATS = ['Ground', 'Terrain', 'MainStructures', 'GameplayProps',
        'Decoration', 'Background', 'Lighting', 'Effects']
HALF = 175.0

# name: (rgb, emission strength). Names that exist in the Unity project reuse the same M_* name.
PAL = {
    'M_Cookie_Light': ((0.85, 0.60, 0.30), 0), 'M_Cookie_Wall': ((0.72, 0.45, 0.20), 0),
    'M_Gingerbread_Dark': ((0.42, 0.22, 0.10), 0), 'M_Cookie_Stone': ((0.60, 0.43, 0.26), 0),
    'M_Chocolate_Dark': ((0.16, 0.08, 0.05), 0), 'M_Chocolate_Milk': ((0.36, 0.19, 0.09), 0),
    'M_Floor_Chocolate': ((0.27, 0.14, 0.07), 0),
    'M_Icing_Pink': ((0.96, 0.62, 0.76), 0), 'M_Icing_Purple': ((0.58, 0.36, 0.86), 0),
    'M_Candy_Red': ((0.86, 0.12, 0.16), 0), 'M_Candy_White': ((0.95, 0.93, 0.90), 0),
    'M_Candy_Pink': ((0.96, 0.45, 0.66), 0), 'M_Candy_Teal': ((0.18, 0.74, 0.70), 0),
    'M_Candy_Purple': ((0.52, 0.24, 0.78), 0), 'M_Jelly_Orange': ((1.0, 0.55, 0.15), 0),
    'M_Magic_Gem': ((0.72, 0.30, 1.0), 3), 'M_Magic_Rune': ((0.80, 0.45, 1.0), 4),
    'M_Mushroom_Cap': ((0.62, 0.28, 0.80), 0), 'M_Mushroom_Stem': ((0.90, 0.84, 0.78), 0),
    'M_Mushroom_Glow': ((0.95, 0.40, 0.90), 2.5), 'M_Lantern_Glow': ((1.0, 0.62, 0.30), 4),
    'M_Leaf_Purple': ((0.38, 0.16, 0.52), 0), 'M_Leaf_Teal': ((0.10, 0.45, 0.44), 0),
    'M_Bark': ((0.30, 0.18, 0.12), 0), 'M_Rock': ((0.42, 0.40, 0.44), 0),
    'M_Soil': ((0.30, 0.22, 0.20), 0), 'M_Wood_Dark': ((0.28, 0.16, 0.09), 0),
    'M_Wood_Light': ((0.62, 0.42, 0.24), 0), 'M_Sugar_White': ((0.97, 0.96, 0.98), 0),
    'M_Window_Glass_Orange': ((1.0, 0.70, 0.30), 3), 'M_Window_Glass_Purple': ((0.70, 0.35, 1.0), 3),
    'M_Gold': ((0.85, 0.66, 0.25), 0),
    # new, map specific (style guide: deep purple / teal / dark grey / warm orange / cookie golden brown)
    # grounds follow the reference photos: sugar-snow / cream frosting / brick+chocolate / dark asphalt / garden
    'M_Ground_CandyForest': ((0.94, 0.84, 0.88), 0), 'M_Ground_Village': ((0.92, 0.79, 0.64), 0),
    'M_Ground_Factory': ((0.74, 0.56, 0.40), 0), 'M_Ground_Carnival': ((0.16, 0.17, 0.20), 0),
    'M_Ground_Bakery': ((0.24, 0.30, 0.20), 0), 'M_Floor_Wood_Planks': ((0.55, 0.33, 0.19), 0),
    'M_Cobblestone': ((0.36, 0.34, 0.38), 0), 'M_Metal_Dark': ((0.20, 0.20, 0.23), 0),
    'M_Metal_Light': ((0.55, 0.56, 0.60), 0), 'M_Chocolate_Liquid': ((0.30, 0.15, 0.07), 0),
    'M_Brick_Oven': ((0.55, 0.28, 0.20), 0), 'M_Flour': ((0.95, 0.93, 0.88), 0),
    'M_Oven_Fire': ((1.0, 0.45, 0.10), 6), 'M_Neon_Pink': ((1.0, 0.30, 0.70), 5),
    'M_Neon_Teal': ((0.20, 1.0, 0.90), 5), 'M_Neon_Purple': ((0.60, 0.30, 1.0), 5),
    'M_Tent_Red': ((0.70, 0.10, 0.14), 0), 'M_Tent_Purple': ((0.36, 0.14, 0.50), 0),
    'M_Pine_Dark': ((0.06, 0.16, 0.14), 0), 'M_Hill_Pink': ((0.42, 0.30, 0.52), 0),
    'M_Grass_Teal': ((0.20, 0.42, 0.38), 0), 'M_Grass_Purple': ((0.32, 0.22, 0.40), 0),
    'M_Glass_Roof': ((0.70, 0.82, 0.95), 0.45), 'M_Warning_Purple': ((0.75, 0.20, 1.0), 5),
    'M_REF_Cookie': ((1.0, 0.85, 0.10), 0), 'M_REF_Monster': ((0.90, 0.10, 0.10), 0),
    # reference-image palette (C:\Program Files\Blender Foundation\Map)
    'M_Bark_Pink': ((0.78, 0.52, 0.60), 0), 'M_Bark_Blue': ((0.30, 0.34, 0.62), 0),
    'M_Waffle': ((0.62, 0.36, 0.14), 0), 'M_Window_Yellow': ((1.0, 0.82, 0.25), 5),
    'M_Glow_Pink': ((1.0, 0.35, 0.65), 1.6), 'M_Glow_Yellow': ((1.0, 0.75, 0.15), 1.6), 'M_Glow_Cyan': ((0.10, 0.85, 1.0), 1.6),
    'M_Tent_Blue': ((0.14, 0.24, 0.62), 0), 'M_Glass_Tint': ((0.80, 0.92, 0.98), 0),
    'M_Liquid_Purple': ((0.62, 0.30, 0.90), 2), 'M_Liquid_Teal': ((0.20, 0.80, 0.70), 2),
    'M_Liquid_Pink': ((1.0, 0.45, 0.70), 2), 'M_Chalkboard': ((0.12, 0.13, 0.13), 0), 'M_Straw': ((0.78, 0.62, 0.36), 0),
    'M_Snow_Path': ((0.99, 0.94, 0.96), 0), 'M_Sugar_Pink': ((0.98, 0.80, 0.87), 0),
    'M_Tile_White': ((0.95, 0.95, 0.97), 0), 'M_Tile_Cream': ((0.98, 0.88, 0.74), 0),
    'M_Floor_Brick': ((0.52, 0.30, 0.22), 0), 'M_Cobble_Dark': ((0.28, 0.28, 0.32), 0),
    'M_Stone_Tile': ((0.76, 0.58, 0.42), 0), 'M_Brick_Wall': ((0.62, 0.34, 0.26), 0),
    # shallow water (slightly glossy, see M())
    'M_Water_Chocolate': ((0.26, 0.13, 0.07), 0), 'M_Water_Teal': ((0.10, 0.42, 0.44), 0),
    'M_Water_Purple': ((0.34, 0.14, 0.50), 0), 'M_Water_Murky': ((0.12, 0.24, 0.22), 0),
}


def M(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    rgb, em = PAL[name]
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    # style guide: soft low-gloss everywhere, magic/emissive slightly glossy
    rough = 0.15 if name.startswith('M_Water') else (0.35 if em else 0.75)
    b.inputs['Roughness'].default_value = rough
    m.roughness = rough
    if em:
        b.inputs['Emission Color'].default_value = (*rgb, 1)
        b.inputs['Emission Strength'].default_value = em
    return m


def rot2(x, y, a):
    c, s = math.cos(a), math.sin(a)
    return x * c - y * s, x * s + y * c


class Map:
    def __init__(self, name, ground_mat):
        self.name = name
        sc = bpy.data.scenes.get(name)
        if sc:
            for o in list(sc.objects):
                bpy.data.objects.remove(o)
            for c in list(sc.collection.children_recursive):
                bpy.data.collections.remove(c)
            bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=False, do_recursive=True)
        else:
            sc = bpy.data.scenes.new(name)
        self.scene = sc
        sc.unit_settings.system = 'METRIC'
        self.top = bpy.data.collections.new(name)
        sc.collection.children.link(self.top)
        self.c = {}
        for cat in CATS:
            col = bpy.data.collections.new(f'{name}_{cat}')
            self.top.children.link(col)
            self.c[cat] = col
        self.ref = bpy.data.collections.new(f'{name}__Reference')
        sc.collection.children.link(self.ref)
        self.n = {}
        self.roads = []      # (p0, p1, half_width)
        self.rings = []      # (center, r, half_width)
        self.blockers = []   # (center, radius) circles to keep scatter away
        self.doors = []      # every doorway cut by wall(): used for the monster clearance check
        # terrain features (see TERRAIN section)
        self.ground_mat = ground_mat
        self.hills, self.bowls, self.channels, self.flats = [], [], [], []
        self.noise_amp = 0.4
        self.noise_seed = sum(map(ord, name))
        self._anchor = []
        self.terrain_ready = False
        # per-map material swaps (reference-image colours) for generic blockout pieces
        self.remap = {
            'CandyForest': {'M_Cookie_Stone': 'M_Snow_Path', 'M_Cookie_Light': 'M_Sugar_Pink'},
            'GingerbreadVillage': {'M_Cookie_Stone': 'M_Tile_White', 'M_Cookie_Light': 'M_Tile_Cream'},
            'ChocolateFactory': {'M_Cobblestone': 'M_Floor_Brick', 'M_Brick_Oven': 'M_Brick_Wall'},
            'CursedCandyCarnival': {'M_Cookie_Stone': 'M_Cobble_Dark', 'M_Cookie_Light': 'M_Cobble_Dark'},
            'HauntedBakery': {'M_Cobblestone': 'M_Stone_Tile'},
        }.get(name, {})
        # Ground placeholder (replaced by the terrain mesh in finish_terrain)
        self.box('Ground', 'Ground', ground_mat, (0, 0, -3), (350, 350, 3))
        self.boundary()

    # ---------- anchors: parts of one composite drop onto the terrain together ----------
    class _At:
        def __init__(self, m, x, y):
            self.m, self.xy = m, (x, y)
        def __enter__(self):
            self.m._anchor.append(self.xy)
        def __exit__(self, *a):
            self.m._anchor.pop()

    def at(self, x, y):
        return Map._At(self, x, y)

    # ---------- naming / linking ----------
    def nm(self, base):
        pre = f'{self.name[:3].upper()}_'
        k = self.n.get(base, 0)
        self.n[base] = k + 1
        return f'{pre}{base}' if k == 0 else f'{pre}{base}_{k:02d}'

    def _obj(self, base, bm, mat, cat, loc=(0, 0, 0), rot=(0, 0, 0), tags=()):
        name = self.nm(base)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        me.materials.append(M(self.remap.get(mat, mat)))
        ob = bpy.data.objects.new(name, me)
        (self.ref if cat == '_ref' else self.c[cat]).objects.link(ob)
        ob.location = loc
        ob.rotation_euler = rot
        for t in tags:
            ob[t] = True
        if self._anchor:
            ob['ax'], ob['ay'] = self._anchor[0]      # outermost composite wins
        if base.startswith('COL_'):
            ob.display_type = 'WIRE'
            ob.hide_render = True
        return ob

    # ---------- primitives (loc = bottom centre) ----------
    def box(self, base, cat, mat, loc, size, rotz=0, tags=()):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector((0, 0, size[2] / 2)), verts=bm.verts)
        return self._obj(base, bm, mat, cat, loc, (0, 0, rotz), tags)

    def cyl(self, base, cat, mat, loc, r, h, r2=None, seg=24, rot=(0, 0, 0), tags=()):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r,
                              radius2=r if r2 is None else r2, depth=h)
        bmesh.ops.translate(bm, vec=Vector((0, 0, h / 2)), verts=bm.verts)
        return self._obj(base, bm, mat, cat, loc, rot, tags)

    def sphere(self, base, cat, mat, loc, r, sz=(1, 1, 1), seg=20, rot=(0, 0, 0), tags=()):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(8, seg // 2), radius=r)
        bmesh.ops.scale(bm, vec=Vector(sz), verts=bm.verts)
        return self._obj(base, bm, mat, cat, loc, rot, tags)

    def torus(self, base, cat, mat, loc, R, r, rot=(0, 0, 0), seg=48, tseg=10, tags=()):
        bm = bmesh.new()
        rings = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring = []
            for j in range(tseg):
                b = 2 * math.pi * j / tseg
                d = R + r * math.cos(b)
                ring.append(bm.verts.new((d * math.cos(a), d * math.sin(a), r * math.sin(b))))
            rings.append(ring)
        for i in range(seg):
            for j in range(tseg):
                a, b = rings[i], rings[(i + 1) % seg]
                bm.faces.new((a[j], b[j], b[(j + 1) % tseg], a[(j + 1) % tseg]))
        return self._obj(base, bm, mat, cat, loc, rot, tags)

    def ring(self, base, cat, mat, loc, r_in, r_out, h=0.04, seg=96, tags=()):
        bm = bmesh.new()
        vs = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            c, s = math.cos(a), math.sin(a)
            vs.append([bm.verts.new((r * c, r * s, z)) for r in (r_in, r_out) for z in (0, h)])
        for i in range(seg):
            a, b = vs[i], vs[(i + 1) % seg]
            bm.faces.new((a[1], b[1], b[3], a[3]))  # top
            bm.faces.new((a[2], a[0], b[0], b[2]))  # inner
            bm.faces.new((a[3], b[3], b[2], a[2]))  # outer top edge
            bm.faces.new((a[0], a[1], b[1], b[0]))  # bottom
        return self._obj(base, bm, mat, cat, loc, (0, 0, 0), tags)

    def prism(self, base, cat, mat, loc, length, depth, height, rotz=0, tags=()):
        """Gable roof: ridge along local X."""
        bm = bmesh.new()
        L, D, H = length / 2, depth / 2, height
        v = [bm.verts.new(p) for p in ((-L, -D, 0), (L, -D, 0), (L, D, 0), (-L, D, 0), (-L, 0, H), (L, 0, H))]
        for f in ((0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5), (0, 3, 2, 1)):
            bm.faces.new([v[i] for i in f])
        return self._obj(base, bm, mat, cat, loc, (0, 0, rotz), tags)

    def wedge(self, base, cat, mat, loc, length, width, height, rotz=0, tags=()):
        """Ramp rising along local +X from loc (low end) to loc+length (high end)."""
        bm = bmesh.new()
        W = width / 2
        v = [bm.verts.new(p) for p in ((0, -W, 0), (length, -W, 0), (length, W, 0), (0, W, 0),
                                       (length, -W, height), (length, W, height))]
        for f in ((0, 1, 4), (3, 5, 2), (0, 4, 5, 3), (1, 2, 5, 4), (0, 3, 2, 1)):
            bm.faces.new([v[i] for i in f])
        return self._obj(base, bm, mat, cat, loc, (0, 0, rotz), tags)

    def strip(self, base, cat, mat, p0, p1, width, h=0.04, z=0.0, road=True, tags=()):
        p0, p1 = Vector((*p0[:2], 0)), Vector((*p1[:2], 0))
        d = p1 - p0
        L = d.length
        mid = (p0 + p1) / 2
        if road:
            self.roads.append((p0.copy(), p1.copy(), width / 2))
        return self.box(base, cat, mat, (mid.x, mid.y, z), (L, width, h), math.atan2(d.y, d.x), tags)

    def path_ring(self, base, cat, mat, center, r, width, h=0.04):
        self.rings.append((Vector((*center, 0)), r, width / 2))
        return self.ring(base, cat, mat, (*center, 0.0), r - width / 2, r + width / 2, h)

    def mound(self, base, loc, r_base, r_top, h, mat=None, cat='Terrain'):
        """Legacy blockout call -> now a smooth terrain hill (no object)."""
        return self.hill(loc[0], loc[1], h, r_base + (r_base - r_top) * 0.9, plateau=r_top * 0.6, name=base)

    def empty(self, base, cat, loc, size=2.0, etype='SPHERE'):
        ob = bpy.data.objects.new(self.nm(base), None)
        ob.empty_display_type = etype
        ob.empty_display_size = size
        ob.location = loc
        self.c[cat].objects.link(ob)
        return ob

    def light(self, base, ltype, loc, color, energy, radius=1.0, rot=(0, 0, 0)):
        ld = bpy.data.lights.new(self.nm(base), ltype)
        ld.color = color
        ld.energy = energy
        if ltype in ('POINT', 'SPOT', 'AREA') and hasattr(ld, 'shadow_soft_size'):
            ld.shadow_soft_size = radius
        ob = bpy.data.objects.new(ld.name, ld)
        ob.location = loc
        ob.rotation_euler = rot
        self.c['Lighting'].objects.link(ob)
        return ob

    # =====================================================================================
    # TERRAIN  (heightfield, smooth everywhere: smoothstep profiles, no steps, no overhangs)
    # walkable slope limit: max slope of a smoothstep bump = 1.5 * H / (R - plateau) <= tan(20 deg)
    # =====================================================================================
    # design limit 17 deg for hills/bowls: small undulation adds <= ~3 deg -> walkable terrain stays <= ~20 deg
    MAX_SLOPE = math.tan(math.radians(17))

    def hill(self, x, y, H, R, plateau=0.0, name='Hill'):
        s = 1.5 * H / (R - plateau)
        assert s <= self.MAX_SLOPE + 1e-3, f'{name} too steep: {math.degrees(math.atan(s)):.1f} deg'
        self.hills.append(dict(x=x, y=y, H=H, R=R, p=plateau, name=name, slope=math.degrees(math.atan(s))))

    def bowl(self, x, y, D, R, water=None, wd=0.7, plateau=0.0, name='Bowl'):
        """Shallow dish: depth D, radius R. water = material name or None (dry)."""
        s = 1.5 * D / (R - plateau)
        assert s <= self.MAX_SLOPE + 1e-3, f'{name} too steep: {math.degrees(math.atan(s)):.1f} deg'
        self.bowls.append(dict(x=x, y=y, D=D, R=R, p=plateau, water=water, wd=wd, name=name,
                               slope=math.degrees(math.atan(s))))

    def channel(self, pts, D, w_bottom, w_top, water=None, wd=0.6, name='Channel'):
        """Stream bed along a polyline; half widths w_bottom (flat bed) / w_top (bank edge)."""
        s = 1.5 * D / (w_top - w_bottom)
        assert s <= self.MAX_SLOPE + 1e-3, f'{name} too steep'
        self.channels.append(dict(pts=[tuple(p) for p in pts], D=D, wb=w_bottom, wt=w_top, water=water, wd=wd,
                                  name=name, slope=math.degrees(math.atan(s))))

    def flat_circle(self, x, y, r, fall=8.0, level=None):
        self.flats.append(dict(kind='c', x=x, y=y, r=r, fall=fall, level=level))

    def flat_rect(self, x, y, hw, hh, fall=8.0, rot=0.0, level=None):
        self.flats.append(dict(kind='r', x=x, y=y, hw=hw, hh=hh, rot=rot, fall=fall, level=level))

    # ---- vectorised height function ----
    @staticmethod
    def _ss(t):
        import numpy as np
        t = np.clip(t, 0.0, 1.0)
        return t * t * (3 - 2 * t)

    def _seg_dist(self, X, Y, a, b):
        import numpy as np
        ax, ay = a[0], a[1]
        dx, dy = b[0] - ax, b[1] - ay
        L2 = dx * dx + dy * dy or 1e-9
        t = np.clip(((X - ax) * dx + (Y - ay) * dy) / L2, 0, 1)
        return np.hypot(X - (ax + t * dx), Y - (ay + t * dy))

    def _base(self, X, Y):
        """Macro shape: hills - bowls - channels (+ decorative rise outside the play area)."""
        import numpy as np
        h = np.zeros_like(X, dtype=float)
        for f in self.hills:
            d = np.hypot(X - f['x'], Y - f['y'])
            h += f['H'] * (1 - self._ss((d - f['p']) / (f['R'] - f['p'])))
        for f in self.bowls:
            d = np.hypot(X - f['x'], Y - f['y'])
            h -= f['D'] * (1 - self._ss((d - f['p']) / (f['R'] - f['p'])))
        for f in self.channels:
            d = np.full_like(X, 1e9, dtype=float)
            for a, b in zip(f['pts'][:-1], f['pts'][1:]):
                d = np.minimum(d, self._seg_dist(X, Y, a, b))
            h -= f['D'] * (1 - self._ss((d - f['wb']) / (f['wt'] - f['wb'])))
        # decorative rim outside the ground (never walkable: boundary walls stop players at 175)
        e = self._ss((np.maximum(np.abs(X), np.abs(Y)) - 180.0) / 60.0)
        h += e * (9 + 5 * np.sin(X * 0.021 + 1.3) * np.cos(Y * 0.017 + 0.4) + 4 * np.sin((X + Y) * 0.011))
        return h

    def _noise(self, X, Y):
        import numpy as np
        rnd = random.Random(self.noise_seed)
        n = np.zeros_like(X, dtype=float)
        tot = 0.0
        for wl in (18, 23, 31, 42, 57, 76, 95):        # non-harmonic wavelengths -> no visible repetition
            a = rnd.uniform(0, math.pi)
            amp = (wl / 95.0) ** 0.6
            n += amp * np.sin((X * math.cos(a) + Y * math.sin(a)) * (2 * math.pi / wl) + rnd.uniform(0, 6.28))
            tot += amp
        return n / tot

    def _path_mask(self, X, Y):
        import numpy as np
        d = np.full_like(X, 1e9, dtype=float)
        for a, b, hw in self.roads:
            d = np.minimum(d, self._seg_dist(X, Y, a, b) - hw)
        for c, r, hw in self.rings:
            d = np.minimum(d, np.abs(np.hypot(X - c.x, Y - c.y) - r) - hw)
        return 1 - self._ss(d / 6.0)

    def _flat_weight(self, f, X, Y):
        import numpy as np
        if f['kind'] == 'c':
            dout = np.maximum(np.hypot(X - f['x'], Y - f['y']) - f['r'], 0)
        else:
            c, s = math.cos(-f['rot']), math.sin(-f['rot'])
            lx = (X - f['x']) * c - (Y - f['y']) * s
            ly = (X - f['x']) * s + (Y - f['y']) * c
            qx = np.maximum(np.abs(lx) - f['hw'], 0)
            qy = np.maximum(np.abs(ly) - f['hh'], 0)
            dout = np.hypot(qx, qy)
        return 1 - self._ss(dout / f['fall'])

    def height(self, X, Y):
        import numpy as np
        X = np.asarray(X, dtype=float)
        Y = np.asarray(Y, dtype=float)
        h = self._base(X, Y)
        h += self.noise_amp * self._noise(X, Y) * (1 - 0.85 * self._path_mask(X, Y))
        for f in self.flats:
            w = self._flat_weight(f, X, Y)
            h = h * (1 - w) + f['_lvl'] * w
        return h

    def h_at(self, x, y):
        return float(self.height([x], [y])[0]) if self.terrain_ready else 0.0

    # ---- build ----
    def _auto_pads(self):
        """Every grounded structure 3.5-40 m wide gets a flat pad at the smooth terrain height."""
        for cat in ('MainStructures', 'GameplayProps', 'Terrain'):
            for o in self.c[cat].objects:
                if o.type != 'MESH' or o.get('no_pad'):
                    continue
                if any(k in o.name for k in ('Tree', 'Trunk', 'Mushroom', 'Pine', 'Lamp', 'Pole', 'Stick', 'Leg',
                                             'Chimney', 'Silo', 'Doll', 'Lollipop', 'Rock', 'Sack', 'Booth',
                                             'Bench', 'Crate', 'Stall', 'Cart', 'CircusTent', 'Cupcake', 'CreamBank',
                                             'Balloon', 'Pumpkin', 'Glow', 'Cane', 'Macaron', 'Marshmallow', 'Cake')):
                    continue      # organic / thin things sit on the slope as they are
                mb = o.matrix_basis
                bb = [mb @ Vector(c) for c in o.bound_box]
                if min(v.z for v in bb) > 0.5:
                    continue
                w = max(v.x for v in bb) - min(v.x for v in bb)
                d = max(v.y for v in bb) - min(v.y for v in bb)
                if not (6.0 <= max(w, d) <= 40):
                    continue
                cx = (max(v.x for v in bb) + min(v.x for v in bb)) / 2
                cy = (max(v.y for v in bb) + min(v.y for v in bb)) / 2
                ax, ay = o.get('ax', cx), o.get('ay', cy)
                self.flats.append(dict(kind='r', x=cx, y=cy, hw=w / 2 + 0.6, hh=d / 2 + 0.6, rot=0.0,
                                       fall=4.0 + max(w, d) * 0.6, level=None, anchor=(ax, ay)))

    def finish_terrain(self, step=2.0):
        import numpy as np
        from mathutils.bvhtree import BVHTree
        self._auto_pads()
        # flat levels = smooth macro height at their anchor/centre
        for f in self.flats:
            if f['level'] is None:
                ax, ay = f.get('anchor', (f['x'], f['y']))
                f['_lvl'] = float(self._base(np.array([ax]), np.array([ay]))[0])
            else:
                f['_lvl'] = f['level']
        self.terrain_ready = True

        # ---- walkable grid ----
        n = int(round(350 / step)) + 1
        g = np.linspace(-HALF, HALF, n)
        X, Y = np.meshgrid(g, g)                      # [row=y, col=x]
        Z = self.height(X, Y)
        self.grid = (g, Z)

        # paint materials from the flat path/plaza pieces in the Ground collection, then remove them
        painters = [o for o in self.c['Ground'].objects
                    if o.type == 'MESH' and not o.name.split('_', 1)[1].startswith(('COL_', 'Ground'))]
        mats = [M(self.ground_mat)]
        vs, fs, fmat = [], [], []
        for o in painters:
            mat = o.data.materials[0]
            if mat not in mats:
                mats.append(mat)
            mw = o.matrix_basis
            base = len(vs)
            vs += [tuple(mw @ v.co) for v in o.data.vertices]
            for p in o.data.polygons:
                fs.append([base + i for i in p.vertices])
                fmat.append(mats.index(mat))
        bvh = BVHTree.FromPolygons(vs, fs) if fs else None
        for o in painters:
            bpy.data.objects.remove(o)
        old = [o for o in self.c['Ground'].objects if o.name.endswith('_Ground') or o.name == self.nm_peek('Ground')]
        for o in old:
            bpy.data.objects.remove(o)

        verts = [(float(g[j]), float(g[i]), float(Z[i, j])) for i in range(n) for j in range(n)]
        faces, fm = [], []
        for i in range(n - 1):
            for j in range(n - 1):
                a = i * n + j
                faces.append((a, a + 1, a + n + 1, a + n))
                if bvh:
                    cx, cy = (g[j] + g[j + 1]) / 2, (g[i] + g[i + 1]) / 2
                    hit = bvh.ray_cast(Vector((cx, cy, 50)), Vector((0, 0, -1)), 200)
                    fm.append(fmat[hit[2]] if hit[0] is not None else 0)
                else:
                    fm.append(0)
        # skirt down to z = -6 so the ground block is >= 3 m thick even under the deepest bowl
        border = [j for j in range(n)] + [j * n + (n - 1) for j in range(1, n)] + \
                 [(n - 1) * n + j for j in range(n - 2, -1, -1)] + [j * n for j in range(n - 2, 0, -1)]
        b0 = len(verts)
        for k in border:
            x, y, _ = verts[k]
            verts.append((x, y, -6.0))
        L = len(border)
        for k in range(L):
            a, b = border[k], border[(k + 1) % L]
            faces.append((b, a, b0 + k, b0 + (k + 1) % L))
            fm.append(0)
        faces.append(tuple(b0 + k for k in range(L)))
        fm.append(0)
        me = bpy.data.meshes.new(f'{self.name[:3].upper()}_Terrain_Walkable')
        me.from_pydata(verts, [], faces)
        for mt in mats:
            me.materials.append(mt)
        me.polygons.foreach_set('material_index', fm)
        me.update()
        for p in me.polygons:
            p.use_smooth = True
        tw = bpy.data.objects.new(me.name, me)
        self.c['Ground'].objects.link(tw)
        tw['unity_collider'] = 'MeshCollider (static, walkable)'

        # ---- decorative rim outside the ground (no collider) ----
        g2 = np.arange(-325, 325.1, 5.0)
        X2, Y2 = np.meshgrid(g2, g2)
        Z2 = self.height(X2, Y2) - 0.05
        n2 = len(g2)
        v2 = [(float(g2[j]), float(g2[i]), float(Z2[i, j])) for i in range(n2) for j in range(n2)]
        f2 = []
        for i in range(n2 - 1):
            for j in range(n2 - 1):
                if max(abs(g2[j]), abs(g2[j + 1])) <= 170 and max(abs(g2[i]), abs(g2[i + 1])) <= 170:
                    continue
                a = i * n2 + j
                f2.append((a, a + 1, a + n2 + 1, a + n2))
        me2 = bpy.data.meshes.new(f'{self.name[:3].upper()}_Terrain_Decor')
        me2.from_pydata(v2, [], f2)
        me2.materials.append(M(self.ground_mat))
        for p in me2.polygons:
            p.use_smooth = True
        td = bpy.data.objects.new(me2.name, me2)
        self.c['Background'].objects.link(td)
        td['unity_collider'] = 'None (decorative)'

        # ---- water surfaces ----
        for f in self.bowls:
            if not f['water']:
                continue
            bottom = float(self.height([f['x']], [f['y']])[0])
            lvl = bottom + f['wd']
            A = np.linspace(0, 2 * math.pi, 40, endpoint=False)
            Rr = np.arange(0.5, f['R'] * 1.5, 0.5)
            AA, RR = np.meshgrid(A, Rr)                        # [r, angle]
            HH = self.height(f['x'] + RR * np.cos(AA), f['y'] + RR * np.sin(AA))
            ring = []
            for k, a in enumerate(A):
                above = np.nonzero(HH[:, k] >= lvl)[0]
                r = Rr[above[0]] if len(above) else Rr[-1]
                ring.append((f['x'] + (r + 0.8) * math.cos(a), f['y'] + (r + 0.8) * math.sin(a), lvl))
            wm = bpy.data.meshes.new(self.nm('Water_' + f['name']))
            wm.from_pydata(ring, [], [tuple(range(len(ring)))])
            wm.materials.append(M(f['water']))
            wo = bpy.data.objects.new(wm.name, wm)
            self.c['Terrain'].objects.link(wo)
            wo['unity_collider'] = 'None (visual water, optional trigger)'
            f['water_level'] = round(lvl, 2)
        for f in self.channels:
            if not f['water']:
                continue
            pts = f['pts']
            samples = [self.h_at(*p) for p in pts]
            lvl = sorted(samples)[len(samples) // 2] + f['wd']
            w = f['wb'] + (f['wt'] - f['wb']) * 0.45
            for a, b in zip(pts[:-1], pts[1:]):
                self.strip('Water_' + f['name'], 'Terrain', f['water'], a, b, 2 * w, h=0.02, z=lvl, road=False)
            f['water_level'] = round(lvl, 2)

        # ---- drop everything onto the terrain ----
        skip = {tw.name, td.name}
        movers = []
        for cat, col in self.c.items():
            for o in col.all_objects:
                if o.name in skip or o.name.startswith(f'{self.name[:3].upper()}_Water_'):
                    continue
                if 'COL_Boundary' in o.name:
                    o.location.z = -6
                    o.scale.z = 4.0
                    continue
                movers.append(o)
        movers += list(self.ref.objects)
        AX = np.array([o.get('ax', o.location.x) for o in movers])
        AY = np.array([o.get('ay', o.location.y) for o in movers])
        HZ = self.height(AX, AY) if movers else []
        for o, dz in zip(movers, HZ):
            o.location.z += float(dz)
        return tw

    def height_map_png(self, path, step=1.0, scale=4):
        """Top-down height map: colour = height, dark lines = 1 m contours, thick = 5 m,
        light = paths, blue = water, white dots = hill tops, black dots = bowl centres."""
        import numpy as np
        g = np.arange(-HALF, HALF + 0.01, step)
        X, Y = np.meshgrid(g, g)
        Z = self.height(X, Y)
        lo, hi = -3.0, 10.0
        t = np.clip((Z - lo) / (hi - lo), 0, 1)
        stops = [(0.0, (0.05, 0.25, 0.30)), (0.23, (0.20, 0.45, 0.42)), (0.35, (0.36, 0.30, 0.48)),
                 (0.6, (0.62, 0.42, 0.62)), (0.8, (0.88, 0.62, 0.34)), (1.0, (1.0, 0.92, 0.70))]
        img = np.zeros(Z.shape + (4,))
        img[..., 3] = 1
        for (t0, c0), (t1, c1) in zip(stops[:-1], stops[1:]):
            msk = (t >= t0) & (t <= t1)
            u = ((t - t0) / (t1 - t0))[msk]
            for ch in range(3):
                img[..., ch][msk] = c0[ch] + (c1[ch] - c0[ch]) * u
        # slope shading
        gy, gx = np.gradient(Z, step)
        shade = np.clip(1 - 0.9 * (gx * 0.6 - gy * 0.6), 0.55, 1.25)
        img[..., :3] *= shade[..., None]
        # paths
        pm = self._path_mask(X, Y) > 0.99
        img[..., :3][pm] = img[..., :3][pm] * 0.45 + np.array([0.95, 0.85, 0.6]) * 0.55
        # water
        for f in self.bowls:
            if f.get('water_level') is not None:
                w = (Z < f['water_level']) & (np.hypot(X - f['x'], Y - f['y']) < f['R'] * 1.5)
                img[..., :3][w] = (0.15, 0.35, 0.85)
        for f in self.channels:
            if f.get('water_level') is not None:
                d = np.full_like(X, 1e9)
                for a, b in zip(f['pts'][:-1], f['pts'][1:]):
                    d = np.minimum(d, self._seg_dist(X, Y, a, b))
                w = (Z < f['water_level']) & (d < f['wt'])
                img[..., :3][w] = (0.15, 0.35, 0.85)
        # contours
        for lvl_step, dark in ((1.0, 0.7), (5.0, 0.35)):
            k = np.floor(Z / lvl_step)
            edge = np.zeros_like(Z, dtype=bool)
            edge[:, 1:] |= k[:, 1:] != k[:, :-1]
            edge[1:, :] |= k[1:, :] != k[:-1, :]
            img[..., :3][edge] *= dark
        # markers
        def dot(x, y, col, r=3):
            i, j = int((y + HALF) / step), int((x + HALF) / step)
            img[max(i - r, 0):i + r + 1, max(j - r, 0):j + r + 1, :3] = col
        for f in self.hills:
            dot(f['x'], f['y'], (1, 1, 1))
        for f in self.bowls:
            dot(f['x'], f['y'], (0, 0, 0))
        img = np.repeat(np.repeat(img, scale, 0), scale, 1)
        h, w = img.shape[:2]
        im = bpy.data.images.new('hm_tmp', w, h)
        im.pixels = img.astype(np.float32).ravel()
        im.filepath_raw = path
        im.file_format = 'PNG'
        im.save()
        bpy.data.images.remove(im)
        return path

    def nm_peek(self, base):
        return f'{self.name[:3].upper()}_{base}'

    def terrain_report(self):
        """Slope stats of the walkable grid, overall and on the painted paths."""
        import numpy as np
        g, Z = self.grid
        step = g[1] - g[0]
        gy, gx = np.gradient(Z, step)
        slope = np.degrees(np.arctan(np.hypot(gx, gy)))
        X, Y = np.meshgrid(g, g)
        pm = self._path_mask(X, Y) > 0.99
        inner = (np.abs(X) < 172) & (np.abs(Y) < 172)
        return dict(z_min=round(float(Z.min()), 2), z_max=round(float(Z[inner].max()), 2),
                    slope_max=round(float(slope[inner].max()), 1),
                    slope_p99=round(float(np.percentile(slope[inner], 99)), 1),
                    path_slope_max=round(float(slope[pm & inner].max()), 1) if pm.any() else None,
                    hills=[(f['name'], round(f['x']), round(f['y']), f['H'], f['R'], round(f['slope'], 1)) for f in self.hills],
                    bowls=[(f['name'], round(f['x']), round(f['y']), f['D'], f['R'], f['water'] or 'dry',
                            round(f['slope'], 1)) for f in self.bowls],
                    channels=[(f['name'], f['D'], f['wt'] * 2, f['water']) for f in self.channels])

    # ---------- boundary ----------
    def boundary(self, h=8.0):
        for i, (x, y, w, d) in enumerate(((0, HALF, 350, 1), (0, -HALF, 350, 1), (HALF, 0, 1, 350), (-HALF, 0, 1, 350))):
            self.box('COL_Boundary', 'Ground', 'M_Metal_Dark', (x, y, 0), (w, d, h))

    # ---------- scatter helpers ----------
    def clear_of_paths(self, p, margin):
        p = Vector((p[0], p[1], 0))
        from mathutils.geometry import intersect_point_line
        for a, b, hw in self.roads:
            q, f = intersect_point_line(p, a, b)
            f = min(max(f, 0), 1)
            if (p - (a + (b - a) * f)).length < hw + margin:
                return False
        for c, r, hw in self.rings:
            if abs((p - c).length - r) < hw + margin:
                return False
        for c, r in self.blockers:
            if (p - Vector((c[0], c[1], 0))).length < r + margin:
                return False
        return True

    def scatter(self, region, count, spacing, margin, fn, seed=1, tries=40):
        """region(rnd)->(x,y) candidate; fn(x,y,rnd) places the object. Poisson-ish rejection."""
        rnd = random.Random(seed)
        pts = []
        for _ in range(count * tries):
            if len(pts) >= count:
                break
            x, y = region(rnd)
            if abs(x) > HALF - 3 or abs(y) > HALF - 3:
                continue
            if not self.clear_of_paths((x, y), margin):
                continue
            if any((x - a) ** 2 + (y - b) ** 2 < spacing ** 2 for a, b in pts):
                continue
            pts.append((x, y))
            with self.at(x, y):
                fn(x, y, rnd)
        return pts

    # ---------- scale references (not exported) ----------
    def refs(self, cookie_pos):
        x, y = cookie_pos
        self.cyl('REF_Cookie_2m', '_ref', 'M_REF_Cookie', (x, y, self.h_at(x, y)), 0.46, 2.0, seg=16)
        # MonsterPlayer visible envelope (arm span 6.3 x depth 3.5 x height 5.3 m)
        self.box('REF_Monster_5m', '_ref', 'M_REF_Monster', (x + 5, y, self.h_at(x + 5, y)), (6.3, 3.5, 5.3))

    # MonsterPlayer (Assets/04. Prefabs/Resources/MonsterPlayer.prefab, root scale 3.14):
    # visible size ~6.3 m arm span x 3.5 m deep x 5.3 m tall (eyes 3.27 m); capsule r 0.31 / h 1.1.
    # Every doorway / indoor passage the monster must use: clear width >= 7 m, clear height >= 7 m.
    MONSTER_CLEAR_W = 7.0
    MONSTER_CLEAR_H = 7.0

    def door_report(self):
        bad = [d for d in self.doors if d['width'] < self.MONSTER_CLEAR_W or d['height'] < self.MONSTER_CLEAR_H]
        return dict(doors=len(self.doors), min_w=min((d['width'] for d in self.doors), default=None),
                    min_h=min((d['height'] for d in self.doors), default=None), too_small=bad[:10])

    def gamecam(self, name, cookie_xy, look_xy, ground_z=None, dist=3.2, height=1.5, pitch=25.0):
        if ground_z is None:
            ground_z = self.h_at(*cookie_xy)
        """Unity Camera_Ctrl default view: orbit target = cookie + 1.5 m, distance 3.2, pitch 25, vertical FOV 60."""
        cam = bpy.data.cameras.new(self.nm(name))
        cam.sensor_fit = 'VERTICAL'
        cam.angle = math.radians(60)
        cam.clip_end = 1500
        ob = bpy.data.objects.new(cam.name, cam)
        self.ref.objects.link(ob)
        t = Vector((cookie_xy[0], cookie_xy[1], ground_z + height))
        h = Vector((look_xy[0] - cookie_xy[0], look_xy[1] - cookie_xy[1], 0)).normalized()
        p = math.radians(pitch)
        fwd = Vector((h.x * math.cos(p), h.y * math.cos(p), -math.sin(p)))
        ob.location = t - fwd * dist
        ob.rotation_mode = 'QUATERNION'
        ob.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
        self.cyl('REF_Cookie_2m', '_ref', 'M_REF_Cookie', (cookie_xy[0], cookie_xy[1], ground_z), 0.46, 2.0, seg=16)
        return ob

    def topcam(self):
        cam = bpy.data.cameras.new(self.nm('TopCam'))
        cam.type = 'ORTHO'
        cam.ortho_scale = 356
        cam.clip_end = 1500
        ob = bpy.data.objects.new(cam.name, cam)
        self.ref.objects.link(ob)
        ob.location = (0, 0, 400)
        return ob


# ---------- shared composite blockout pieces ----------
def lollipop(m, cat, x, y, h, r, mats=('M_Candy_Pink', 'M_Candy_White'), face=0.0, base='CandyLollipop'):
    m.cyl(f'{base}_Stick', cat, 'M_Candy_White', (x, y, 0), max(0.15, r * 0.08), h, seg=12)
    rot = (math.pi / 2, 0, face)
    m.cyl(f'{base}_Disc', cat, mats[0], (x, y, h + r), r, r * 0.25, seg=32, rot=rot)
    m.torus(f'{base}_Swirl', cat, mats[1], (x, y, h + r), r * 0.62, r * 0.14, rot=rot, seg=32, tseg=8)


def candy_tree(m, cat, x, y, h, r, variant=0, rnd=None):
    trunk = ('M_Cookie_Wall', 'M_Chocolate_Milk', 'M_Gingerbread_Dark')[variant % 3]
    crown = ('M_Candy_Pink', 'M_Candy_Teal', 'M_Candy_Purple', 'M_Candy_Red')[(variant + (rnd.randint(0, 3) if rnd else 0)) % 4]
    m.cyl('CandyTree_Trunk', cat, trunk, (x, y, 0), r * 0.18, h, r2=r * 0.12, seg=10)
    m.sphere('CandyTree_Crown', cat, crown, (x, y, h), r, sz=(1, 1, 0.8), seg=16)
    a = rnd.uniform(0, math.pi) if rnd else 0
    for s in (1, -1):  # wrapper twists
        dx, dy = rot2(s * r * 1.15, 0, a)
        m.cyl('CandyTree_Wrap', cat, crown, (x + dx, y + dy, h), r * 0.45, r * 0.5, r2=0.05, seg=8,
              rot=(0, s * math.pi / 2, a))


def twisted_tree(m, cat, x, y, h, rnd):
    lean = rnd.uniform(-0.25, 0.25)
    m.cyl('TwistedTree_Trunk', cat, 'M_Bark', (x, y, 0), h * 0.08, h, r2=h * 0.03, seg=8, rot=(lean, lean * 0.5, 0))
    for k in range(3):
        a = rnd.uniform(0, 2 * math.pi)
        m.cyl('TwistedTree_Branch', cat, 'M_Bark', (x, y, h * rnd.uniform(0.55, 0.85)), h * 0.025, h * 0.35,
              r2=0.05, seg=6, rot=(rnd.uniform(0.7, 1.2), 0, a))
    m.sphere('TwistedTree_Leaves', cat, rnd.choice(('M_Leaf_Purple', 'M_Leaf_Teal')), (x, y, h * 0.9), h * 0.28,
             sz=(1, 1, 0.6), seg=12)


def mushroom(m, cat, x, y, h, r, glow=True):
    m.cyl('Mushroom_Stem', cat, 'M_Mushroom_Stem', (x, y, 0), r * 0.25, h, r2=r * 0.18, seg=12)
    m.sphere('Mushroom_Cap', cat, 'M_Mushroom_Glow' if glow else 'M_Mushroom_Cap', (x, y, h - r * 0.15), r,
             sz=(1, 1, 0.45), seg=18)


def pine(m, cat, x, y, h):
    m.cyl('Pine_Trunk', cat, 'M_Bark', (x, y, 0), h * 0.04, h * 0.25, seg=6)
    m.cyl('Pine_Crown', cat, 'M_Pine_Dark', (x, y, h * 0.2), h * 0.22, h * 0.8, r2=0, seg=8)


def house(m, x, y, w, d, h, rotz=0.0, roof='M_Chocolate_Dark', wall='M_Cookie_Wall', cursed=False, cat='MainStructures'):
    """Blockout gingerbread house; door faces local -Y."""
    m.box('House_Body', cat, wall, (x, y, 0), (w, d, h), rotz)
    m.prism('House_Roof', cat, roof, (x, y, h), w + 1.2, d + 1.2, max(2.5, d * 0.45), rotz)
    m.box('House_Icing', 'Decoration', 'M_Icing_Purple' if cursed else 'M_Icing_Pink', (x, y, h - 0.25),
          (w + 1.3, d + 1.3, 0.3), rotz)
    dx, dy = rot2(0, -d / 2 - 0.1, rotz)
    m.box('House_Door', 'Decoration', 'M_Chocolate_Milk', (x + dx, y + dy, 0), (1.6, 0.3, 2.8), rotz)
    for s in (-1, 1):
        wx, wy = rot2(s * w * 0.3, -d / 2 - 0.1, rotz)
        m.box('House_Window', 'Lighting', 'M_Window_Glass_Purple' if cursed else 'M_Window_Glass_Orange',
              (x + wx, y + wy, h * 0.45), (1.4, 0.25, 1.4), rotz)
    m.blockers.append(((x, y), math.hypot(w, d) / 2))


def lamp(m, x, y, light=False):
    m.cyl('CandyLamp_Pole', 'GameplayProps', 'M_Candy_Red', (x, y, 0), 0.18, 4.2, seg=10)
    m.sphere('CandyLamp_Lantern', 'Lighting', 'M_Lantern_Glow', (x, y, 4.5), 0.5, seg=12)
    if light:
        m.light('CandyLamp_Light', 'POINT', (x, y, 4.5), (1.0, 0.62, 0.35), 400, 0.5)


def fence_line(m, p0, p1, h=1.2, mat='M_Candy_White', cat='Decoration'):
    """Split into <= 8 m pieces so a long fence follows the terrain."""
    L = math.hypot(p1[0] - p0[0], p1[1] - p0[1])
    k = max(1, int(math.ceil(L / 8.0)))
    for i in range(k):
        a = (p0[0] + (p1[0] - p0[0]) * i / k, p0[1] + (p1[1] - p0[1]) * i / k)
        b = (p0[0] + (p1[0] - p0[0]) * (i + 1) / k, p0[1] + (p1[1] - p0[1]) * (i + 1) / k)
        m.strip('CandyFence', cat, mat, a, b, 0.2, h=h, road=False)


def _anchored(fn, xi):
    """Wrap a composite builder so all its parts share one terrain anchor (its x, y args)."""
    def w(m, *a, **k):
        with m.at(a[xi], a[xi + 1]):
            return fn(m, *a, **k)
    w.__name__ = fn.__name__
    return w


lollipop = _anchored(lollipop, 1)
candy_tree = _anchored(candy_tree, 1)
twisted_tree = _anchored(twisted_tree, 1)
mushroom = _anchored(mushroom, 1)
pine = _anchored(pine, 1)
house = _anchored(house, 0)
lamp = _anchored(lamp, 0)


def wall(m, base, p0, p1, h, t=1.0, gaps=(), mat='M_Cookie_Wall', cat='MainStructures', gap_h=None, tags=()):
    """Straight wall from p0 to p1 with door gaps [(distance_from_p0, width), ...].
    gap_h: if set, a lintel is added above each gap from gap_h to h."""
    p0, p1 = Vector((*p0, 0)), Vector((*p1, 0))
    d = p1 - p0
    L = d.length
    u = d / L
    cuts = sorted(gaps)
    for c, w in cuts:                       # doorway register (monster clearance check)
        q = p0 + u * c
        m.doors.append(dict(wall=base, x=round(q.x, 1), y=round(q.y, 1), width=w, height=gap_h if gap_h else h,
                            nx=-u.y, ny=u.x))              # door normal (through-direction)
        for s in (1, -1):                                  # keep both door approaches free of scattered props
            for k in (3.0, 7.0):
                m.blockers.append(((q.x - u.y * s * k, q.y + u.x * s * k), max(3.5, w / 2)))
    s = 0.0
    segs = []
    for c, w in cuts:
        segs.append((s, c - w / 2))
        s = c + w / 2
    segs.append((s, L))
    for a, b in segs:
        if b - a > 0.05:
            m.strip(base, cat, mat, p0 + u * a, p0 + u * b, t, h=h, road=False, tags=tags)
    if gap_h:
        for c, w in cuts:
            q = p0 + u * c
            m.box(base + '_Lintel', cat, mat, (q.x, q.y, gap_h), (w + 0.02, t, h - gap_h),
                  math.atan2(u.y, u.x), tags)
