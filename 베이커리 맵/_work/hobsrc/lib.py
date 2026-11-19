import bpy, bmesh, math
from mathutils import Vector, Matrix
# ---------- materials ----------
PAL = {
 'PinkStone':(0.86,0.58,0.60), 'Cream':(0.96,0.89,0.76), 'Gold':(0.86,0.64,0.24), 'DarkWood':(0.33,0.19,0.11),
 'Wood':(0.58,0.36,0.18), 'Leaf':(0.22,0.48,0.18), 'LeafDark':(0.13,0.33,0.14), 'Pine':(0.16,0.38,0.22),
 'Berry':(0.82,0.08,0.12), 'Apple':(0.78,0.10,0.10), 'Orange':(0.98,0.55,0.10), 'OrangeFlesh':(1.0,0.72,0.25),
 'Grape':(0.38,0.12,0.42), 'Cherry':(0.62,0.03,0.08), 'Pumpkin':(0.95,0.52,0.18), 'Stem':(0.36,0.25,0.12),
 'Seed':(0.98,0.88,0.45), 'White':(0.97,0.95,0.93), 'Frosting':(0.98,0.78,0.85), 'Cup':(0.90,0.46,0.62),
 'Soil':(0.40,0.27,0.16), 'Grass':(0.36,0.58,0.22), 'Flower':(0.95,0.45,0.65), 'FlowerY':(0.98,0.85,0.3),
 'RedTile':(0.80,0.30,0.30), 'Purple':(0.55,0.35,0.70), 'Sand':(0.90,0.74,0.55), 'Banner':(0.62,0.08,0.20),
 'StoneGrey':(0.62,0.58,0.56), 'Canvas':(0.95,0.92,0.86), 'Stripe':(0.80,0.15,0.25), 'Window':(0.35,0.55,0.75),
 'Rock':(0.55,0.43,0.40), 'RockDark':(0.42,0.32,0.30), 'Moss':(0.30,0.52,0.20), 'CloudPink':(0.95,0.86,0.96),
 'Choco':(0.36,0.20,0.12), 'ChocoDark':(0.24,0.13,0.08), 'ChocoFlow':(0.40,0.22,0.12), 'Icing':(0.98,0.93,0.86),
 'Iron':(0.16,0.12,0.20), 'Almond':(0.62,0.38,0.20), 'WheatGold':(0.95,0.60,0.17), 'WheatStem':(0.86,0.60,0.22),
 'ChestRed':(0.62,0.10,0.16), 'Mushroom':(0.85,0.15,0.12), 'Wafer':(0.93,0.78,0.52), 'Sack':(0.82,0.72,0.55),
 'PinkWall':(0.95,0.78,0.80), 'RoofPink':(0.85,0.45,0.55),
 'Burlap':(0.58,0.40,0.22), 'BurlapLight':(0.70,0.52,0.30), 'Jam':(0.62,0.04,0.10), 'GlassRim':(0.80,0.90,0.95),
 'WheatGoldLight':(1.0,0.78,0.32), 'WheatAwn':(0.98,0.80,0.42), 'WheatLeaf':(0.78,0.62,0.22), 'WheatGoldDeep':(0.78,0.43,0.09),
}
EMIT = {'Glow':(1.0,0.78,0.40), 'Water':(0.45,0.75,1.0), 'Portal':(0.70,0.30,1.0), 'PurpleGlow':(0.62,0.30,0.95)}
_m = {}
def mat(name):
    if name in _m: return _m[name]
    m = bpy.data.materials.new("HOB_"+name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if name in EMIT:
        c = EMIT[name]; b.inputs['Base Color'].default_value = (*c, 1)
        b.inputs['Emission Color'].default_value = (*c, 1); b.inputs['Emission Strength'].default_value = 1.0 if name == 'Water' else 4.0
    else:
        c = PAL[name]; b.inputs['Base Color'].default_value = (*c, 1)
        b.inputs['Roughness'].default_value = 0.35 if name == 'Gold' else 0.7
        b.inputs['Metallic'].default_value = 0.8 if name == 'Gold' else 0.0
    m.diffuse_color = (*c, 1); _m[name] = m; return m
# ---------- primitives (each returns object) ----------
_parts = []
def _new(bm, name, m, loc=(0,0,0), rot=(0,0,0), scl=(1,1,1)):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])   # consistent outward normals (lathe/prism/sweep)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.location = loc; o.rotation_euler = rot; o.scale = scl
    o.data.materials.append(mat(m)); _parts.append(o); return o
def box(sx, sy, sz, m, loc=(0,0,0), rot=(0,0,0), bevel=0.0):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x*sx, v.co.y*sy, v.co.z*sz))
    if bevel > 0: bmesh.ops.bevel(bm, geom=bm.edges[:], offset=bevel, segments=2, affect='EDGES')
    return _new(bm, "box", m, loc, rot)
def cyl(r, h, m, loc=(0,0,0), rot=(0,0,0), seg=24, r2=None, cap=True):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=cap, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=h)
    return _new(bm, "cyl", m, loc, rot)
def sph(r, m, loc=(0,0,0), scl=(1,1,1), sub=3, rot=(0,0,0)):
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
    return _new(bm, "sph", m, loc, rot, scl)
def uvs(r, m, loc=(0,0,0), scl=(1,1,1), seg=24, rings=14, rot=(0,0,0)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    return _new(bm, "uvs", m, loc, rot, scl)
def torus(R, r, m, loc=(0,0,0), rot=(0,0,0), seg=32, rseg=10, arc=2*math.pi):
    bm = bmesh.new(); rings = []
    n = seg if arc >= 2*math.pi - 1e-6 else seg + 1
    for i in range(n):
        a = arc * i / seg; c = Vector((R*math.cos(a), R*math.sin(a), 0)); d = Vector((math.cos(a), math.sin(a), 0))
        ring = [bm.verts.new(c + d*(r*math.cos(2*math.pi*j/rseg)) + Vector((0,0,r*math.sin(2*math.pi*j/rseg)))) for j in range(rseg)]
        rings.append(ring)
    for i in range(len(rings) - (0 if n == seg and arc >= 2*math.pi-1e-6 else 1)):
        a, b = rings[i], rings[(i+1) % len(rings)]
        for j in range(rseg): bm.faces.new((a[j], a[(j+1)%rseg], b[(j+1)%rseg], b[j]))
    if arc < 2*math.pi - 1e-6:
        bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    return _new(bm, "torus", m, loc, rot)
def lathe(profile, m, loc=(0,0,0), seg=24, rot=(0,0,0), closed=False):
    # profile: list of (r, z). closed=True: the profile is a loop (ring walls, paving annuli) -> last joins first, no caps.
    # open profile: ends on the axis (r=0) close themselves; an end with r>0 gets a cap.
    bm = bmesh.new(); rings = []
    for (r, z) in profile:
        rings.append([bm.verts.new((r*math.cos(2*math.pi*i/seg), r*math.sin(2*math.pi*i/seg), z)) for i in range(seg)])
    n = len(rings) if closed else len(rings) - 1
    for k in range(n):
        a, b = rings[k], rings[(k+1) % len(rings)]
        for i in range(seg): bm.faces.new((a[i], a[(i+1)%seg], b[(i+1)%seg], b[i]))
    if not closed:
        if profile[0][0] > 1e-6: bm.faces.new(rings[0][::-1])
        if profile[-1][0] > 1e-6: bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-5)
    return _new(bm, "lathe", m, loc, rot)
def prism(pts2d, z0, z1, m, loc=(0,0,0), rot=(0,0,0)):
    bm = bmesh.new(); lo = [bm.verts.new((x, y, z0)) for x, y in pts2d]; hi = [bm.verts.new((x, y, z1)) for x, y in pts2d]
    bm.faces.new(lo[::-1]); bm.faces.new(hi); n = len(pts2d)
    for i in range(n): bm.faces.new((lo[i], lo[(i+1)%n], hi[(i+1)%n], hi[i]))
    return _new(bm, "prism", m, loc, rot)
def sweep(path, shape, m, closed=False):
    """path: list of (pos Vector, side Vector, up Vector); shape: list of (s, u) in cross-section."""
    bm = bmesh.new(); rings = []
    for p, sd, up in path: rings.append([bm.verts.new(p + sd*s + up*u) for s, u in shape])
    n = len(shape)
    for k in range(len(rings)-1):
        a, b = rings[k], rings[k+1]
        for i in range(n): bm.faces.new((a[i], a[(i+1)%n], b[(i+1)%n], b[i]))
    if not closed: bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bm.normal_update(); bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _new(bm, "sweep", m)
def bool_diff(target, cutter):
    md = target.modifiers.new("b", 'BOOLEAN'); md.operation = 'DIFFERENCE'; md.object = cutter; md.solver = 'EXACT'
    bpy.context.view_layer.objects.active = target; bpy.ops.object.modifier_apply(modifier="b")
    _parts.remove(cutter); bpy.data.objects.remove(cutter)
def start():
    _parts.clear()
def finish(name):
    """join parts into one object named name, origin at (0,0,0) = base center"""
    objs = list(_parts); _parts.clear()
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = name; o.data.name = name
    # merge duplicate material slots
    seen = {}; 
    for i, s in enumerate(o.material_slots): seen.setdefault(s.material.name, i)
    me = o.data
    remap = [seen[s.material.name] for s in o.material_slots]
    for p in me.polygons: p.material_index = remap[p.material_index]
    # remove unused slots
    used = sorted(set(p.material_index for p in me.polygons)); newidx = {u: k for k, u in enumerate(used)}
    mats = [o.material_slots[u].material for u in used]
    final = [newidx[p.material_index] for p in me.polygons]
    me.materials.clear()                       # (clearing resets every polygon's index to 0 — reassign after)
    for mm in mats: me.materials.append(mm)
    for p, k in zip(me.polygons, final): p.material_index = k
    for p in me.polygons: p.use_smooth = False
    bm = bmesh.new(); bm.from_mesh(me)
    big = [f for f in bm.faces if len(f.verts) > 4]
    if big: bmesh.ops.triangulate(bm, faces=big, quad_method='BEAUTY', ngon_method='EAR_CLIP')   # concave n-gons (star, arch) -> safe triangles
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bm.to_mesh(me); bm.free()
    return o

def uv_planar(o, axis_u=0, axis_v=2, scale=1.0):
    """box-free planar UVs (water/chocolate flow scroll along v)"""
    me = o.data
    if not me.uv_layers: me.uv_layers.new(name="UVMap")
    uv = me.uv_layers.active.data
    for poly in me.polygons:
        for li in poly.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = (co[axis_u]*scale, co[axis_v]*scale)
def sway_colors(o, base_z, height):
    """vertex colour for wind: R = sway weight (0 root .. 1 tip), G = per-clump phase (0..1)"""
    import random as _r
    me = o.data; bm = bmesh.new(); bm.from_mesh(me)
    isl = {}; cur = 0
    for f in bm.faces:
        if f.index in isl: continue
        st = [f]; isl[f.index] = cur
        while st:
            g = st.pop()
            for e in g.edges:
                for h in e.link_faces:
                    if h.index not in isl: isl[h.index] = cur; st.append(h)
        cur += 1
    bm.free(); _r.seed(17); phase = [_r.random() for _ in range(cur)]
    ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    for poly in me.polygons:
        ph = phase[isl[poly.index]]
        for li in poly.loop_indices:
            z = me.vertices[me.loops[li].vertex_index].co.z
            w = max(0.0, min(1.0, (z - base_z) / height)); w = w*w
            ca.data[li].color = (w, ph, 0.0, 1.0)
