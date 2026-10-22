# Shared Blender helpers for the carnival ride models (gyro drop, viking): textured materials, primitives that register
# into named parts, world-scale box UVs (tiling textures stay sharp in Unity — no decimation, no rebake), part finishing
# (apply modifiers/transforms, UV, join, triangulate n-gons), FBX export (Unity axes) and Cycles part shots.
import bpy, bmesh, os, math
from mathutils import Vector

scene = None
TEX = ""
MAT = {}
UV_SIZE = {}
parts = {}


def reset(tex_dir):
    global scene, TEX
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    TEX = tex_dir
    MAT.clear(); UV_SIZE.clear(); parts.clear()
    return scene


def material(key, name, tex=None, color=(1, 1, 1), metallic=0.0, rough=0.6, emission=None, strength=0.0, uv=2.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if tex:
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(os.path.join(TEX, tex + ".png"))
        node.interpolation = "Cubic"
        nt.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (*color, 1)
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1)
        bsdf.inputs["Emission Strength"].default_value = strength
    MAT[key] = m
    UV_SIZE[key] = uv
    return m


def active():
    return bpy.context.active_object


def add(part, obj, mat, bevel=0.0, uv="box", smooth=False):
    obj.data.materials.clear()
    obj.data.materials.append(MAT[mat])
    obj["rc_mat"] = mat
    obj["rc_uv"] = uv
    if smooth:
        for p in obj.data.polygons: p.use_smooth = True
    if bevel > 0:
        b = obj.modifiers.new("bevel", "BEVEL")
        b.width = bevel; b.segments = 2; b.limit_method = "ANGLE"
    parts.setdefault(part, []).append(obj)
    return obj


def box(part, size, loc, mat, rot=(0, 0, 0), bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = active(); o.scale = size
    return add(part, o, mat, bevel)


def cyl(part, r, depth, loc, mat, verts=24, rot=(0, 0, 0), bevel=0.03, r2=None, smooth=False):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = active()
    if smooth:  # smooth sides, keep caps flat
        for p in o.data.polygons: p.use_smooth = len(p.vertices) == 4
    return add(part, o, mat, bevel)


def sphere(part, r, loc, mat, seg=12, scale=(1, 1, 1), rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=max(6, seg // 2), radius=r, location=loc, rotation=rot)
    o = active(); o.scale = scale
    return add(part, o, mat, smooth=True)


def torus(part, R, r, loc, mat, rot=(0, 0, 0), seg=32):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=8, location=loc, rotation=rot)
    return add(part, active(), mat, smooth=True)


def mesh_obj(part, name, verts, faces, mat, bevel=0.0, smooth=False):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate()
    o = bpy.data.objects.new(name, me)
    scene.collection.objects.link(o)
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    return add(part, o, mat, bevel, smooth=smooth)


def tube(part, points, radius, mat, name="tube", res=6):
    """Round tube along a poly line (gold trims along curves)."""
    cu = bpy.data.curves.new(name, "CURVE"); cu.dimensions = "3D"
    cu.bevel_depth = radius; cu.bevel_resolution = 2; cu.use_fill_caps = True
    sp = cu.splines.new("POLY"); sp.points.add(len(points) - 1)
    for p, q in zip(sp.points, points): p.co = (q[0], q[1], q[2], 1)
    o = bpy.data.objects.new(name, cu); scene.collection.objects.link(o)
    o.data.materials.append(MAT[mat])
    o["rc_mat"] = mat; o["rc_uv"] = "box"
    parts.setdefault(part, []).append(o)
    return o


def bar(part, p0, p1, thick, mat, round_=False, bevel=0.015):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    mid = (p0 + p1) / 2
    rot = d.to_track_quat("Z", "Y").to_euler()
    if round_:
        return cyl(part, thick / 2, d.length, mid, mat, verts=10, rot=rot, bevel=0.0, smooth=True)
    return box(part, (thick, thick, d.length), mid, mat, rot=rot, bevel=bevel)


def plane(part, w, h, loc, rot, mat, thickness=0.03):
    """Upright flat sheet with UV 0..1 over the face (flags, banners)."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=loc, rotation=rot)
    o = active(); o.scale = (w, h, 1)
    sol = o.modifiers.new("solid", "SOLIDIFY"); sol.thickness = thickness
    return add(part, o, mat, uv="keep")


def medallion(part, center, yaw, r, mat, ring_mat="gold", depth=0.16):
    """Disc facing along yaw (outward normal), the image covers the face, gold ring around."""
    rot = (math.pi / 2, 0, yaw + math.pi / 2)
    o = cyl(part, r, depth, center, mat, verts=32, rot=rot, bevel=0.0)
    o["rc_uv"] = "disc"; o["rc_disc_r"] = r
    torus(part, r, 0.08 + r * 0.04, center, ring_mat, rot=rot, seg=32)
    return o


def ring_of(n, r, offset_deg=0.0, skip=None):
    for i in range(n):
        a = math.radians(offset_deg + i * 360.0 / n)
        if skip and skip(a): continue
        yield a, Vector((r * math.cos(a), r * math.sin(a), 0))


# ---------------------------------------------------------------- UV
def uv_box(o, size):
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")  # one named layer for every piece (joined meshes keep a single UV set)
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            p = l.vert.co
            u, v = (p.y, p.z) if ax == 0 else ((p.x, p.z) if ax == 1 else (p.x, p.y))
            l[uv].uv = (u / size, v / size)
    bm.to_mesh(me); bm.free()


def uv_disc(o, r):
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")  # one named layer for every piece (joined meshes keep a single UV set)
    center = sum((v.co for v in bm.verts), Vector()) / len(bm.verts)
    cap = max(bm.faces, key=lambda f: f.calc_area()).normal.normalized()
    side = cap.orthogonal().normalized(); up = cap.cross(side).normalized()
    if up.z < 0: up = -up
    side = up.cross(cap).normalized()
    for f in bm.faces:
        for l in f.loops:
            d = l.vert.co - center
            l[uv].uv = (0.5 + d.dot(side) / (2 * r), 0.5 + d.dot(up) / (2 * r))
    bm.to_mesh(me); bm.free()


def uv_cyl(o, repeats, vsize):
    """Wrap around the object's own vertical axis: u = angle (repeats times around), v = height / vsize.
    Faces crossing the seam are shifted so they do not stretch over the whole texture."""
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    uv = bm.loops.layers.uv.get("UVMap") or bm.loops.layers.uv.new("UVMap")
    cx = sum(v.co.x for v in bm.verts) / len(bm.verts); cy = sum(v.co.y for v in bm.verts) / len(bm.verts)
    for f in bm.faces:
        us = [(math.atan2(l.vert.co.y - cy, l.vert.co.x - cx) / (2 * math.pi)) % 1.0 for l in f.loops]
        if max(us) - min(us) > 0.5: us = [u + 1.0 if u < 0.5 else u for u in us]
        for l, u in zip(f.loops, us):
            l[uv].uv = (u * repeats, l.vert.co.z / vsize)
    bm.to_mesh(me); bm.free()


def finish(part, origin):
    objs = parts[part]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target="MESH")
    objs = [o for o in bpy.context.selected_objects]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in objs:
        mode = o.get("rc_uv", "box")
        if mode == "box": uv_box(o, UV_SIZE.get(o.get("rc_mat", ""), 2.0))
        elif mode == "disc": uv_disc(o, o["rc_disc_r"])
        elif mode == "cyl": uv_cyl(o, o.get("rc_rep", 1.0), o.get("rc_vsize", 4.0))
    for ob in objs:                                    # pieces without UVs get an empty UVMap so join keeps one set
        if "UVMap" not in ob.data.uv_layers: ob.data.uv_layers.new(name="UVMap")
        for l in [l for l in ob.data.uv_layers if l.name != "UVMap"]: ob.data.uv_layers.remove(l)
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = part; o.data.name = part
    o.data.uv_layers.active = o.data.uv_layers["UVMap"]; o.data.uv_layers["UVMap"].active_render = True
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(o.data); bm.free()
    scene.cursor.location = origin
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    scene.cursor.location = (0, 0, 0)
    return o


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


# ---------------------------------------------------------------- export
def export(objs, path, types=frozenset({"MESH"})):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types=set(types), apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False, bake_anim=False,
                             path_mode="STRIP", embed_textures=False)


# ---------------------------------------------------------------- renders
class Stage:
    def __init__(self, out, all_objs, samples=48):
        sc = scene
        self.out, self.all = out, all_objs
        sc.render.engine = "CYCLES"
        sc.cycles.device = "CPU"
        sc.cycles.samples = samples
        sc.cycles.use_denoising = True
        sc.view_settings.view_transform = "AgX"
        sc.view_settings.exposure = 0.6
        world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
        world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.016, 0.024, 1)
        self._light("Key", (-28, -40, 45), 60000, (1.0, 0.85, 0.75), 18)
        self._light("Rim", (35, 30, 30), 30000, (0.65, 0.45, 1.0), 16)
        self._light("Fill", (40, -30, 8), 9000, (1.0, 0.55, 0.4), 12)
        bpy.ops.mesh.primitive_plane_add(size=600, location=(0, 0, -0.01)); self.ground = active()
        gm = bpy.data.materials.new("Ground"); gm.use_nodes = True
        gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.02, 0.022, 0.03, 1)
        gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
        self.ground.data.materials.append(gm)
        self.cam_data = bpy.data.cameras.new("Cam")
        self.cam = bpy.data.objects.new("Cam", self.cam_data); sc.collection.objects.link(self.cam); sc.camera = self.cam

    def _light(self, name, loc, energy, color, size):
        l = bpy.data.lights.new(name, "AREA"); l.energy = energy; l.color = color; l.size = size
        o = bpy.data.objects.new(name, l); scene.collection.objects.link(o); o.location = loc
        o.rotation_euler = (Vector((0, 0, 12)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()

    @staticmethod
    def bbox(objs):
        lo = Vector((1e9,) * 3); hi = -lo
        for o in objs:
            for c in o.bound_box:
                w = o.matrix_world @ Vector(c)
                lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
        return lo, hi

    def shoot(self, name, visible, elev, azim, res=(1100, 1100), fill=1.12, lens=50, frame_objs=None):
        for o in self.all: o.hide_render = o not in visible
        bpy.context.view_layer.update()
        lo, hi = self.bbox(frame_objs or visible)
        glo, _ = self.bbox(visible)
        self.ground.location.z = glo.z - 0.01
        c = (lo + hi) / 2
        w, h = res
        scene.render.resolution_x, scene.render.resolution_y = res
        self.cam_data.lens = lens
        half = math.atan(18.0 / lens)
        half_h = half if w >= h else math.atan(math.tan(half) * w / h)
        half_v = half if h >= w else math.atan(math.tan(half) * h / w)
        e = math.radians(elev)
        wide = max(hi.x - lo.x, hi.y - lo.y)
        span_h = wide / 2 * fill
        span_v = ((hi.z - lo.z) * math.cos(e) + wide * math.sin(e)) / 2 * fill
        dist = max(span_h / math.tan(half_h), span_v / math.tan(half_v)) + wide / 2
        d = Vector((math.cos(e) * math.sin(math.radians(azim)), -math.cos(e) * math.cos(math.radians(azim)), math.sin(e)))
        self.cam.location = c + d * dist
        self.cam.rotation_euler = (c - self.cam.location).to_track_quat("-Z", "Y").to_euler()
        self.cam_data.clip_end = dist * 4
        scene.render.filepath = os.path.join(self.out, name + ".png")
        bpy.ops.render.render(write_still=True)
        print("rendered", name, flush=True)
