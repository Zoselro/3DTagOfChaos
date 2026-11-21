# §92-4 water balloon: translucent rubber skin, water inside (flat surface ~70 %), air bubbles, tied knot
import bpy, bmesh, math, os, sys
from mathutils import Vector
OUT = os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.read_factory_settings(use_empty=True)
H = 0.29      # body height (bottom to neck start)
R = 0.135     # max radius
def profile(t):   # t: 0 bottom .. 1 top of body; radius
    # round bottom, widest at 0.42, tapering to neck
    if t <= 0.42: return R * math.sin(math.acos(1 - t / 0.42)) if t < 0.42 else R
    u = (t - 0.42) / 0.58
    return R * (1 - u ** 1.6 * 0.86)
def lathe(name, inset=0.0, segs=48, rings=40, top_t=1.0):
    bm = bmesh.new(); verts = []
    zb = -H * 0.5 + 0.005
    bot = bm.verts.new((0, 0, zb + inset))
    rings_v = []
    for i in range(1, rings + 1):
        t = i / rings * top_t
        r = max(profile(t) - inset, 0.004)
        z = zb + t * H
        ring = [bm.verts.new((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)]
        rings_v.append(ring)
    for k in range(segs): bm.faces.new((bot, rings_v[0][k], rings_v[0][(k + 1) % segs]))
    for a, b in zip(rings_v, rings_v[1:]):
        for k in range(segs): bm.faces.new((a[k], b[k], b[(k + 1) % segs], a[(k + 1) % segs]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob, rings_v
def mat(name, rgba, rough=0.1, alpha=1.0, trans=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = rgba; b.inputs["Roughness"].default_value = rough
    b.inputs["Alpha"].default_value = alpha
    if trans: b.inputs["Transmission Weight"].default_value = trans; b.inputs["IOR"].default_value = 1.33
    m.diffuse_color = (rgba[0], rgba[1], rgba[2], alpha)
    return m
M_SKIN = mat("ME_BalloonSkin", (0.35, 0.72, 1.0, 1), 0.08, 0.35)
M_WATER = mat("ME_BalloonWater", (0.08, 0.45, 0.95, 1), 0.02, 0.75, 0.6)
M_KNOT = mat("ME_BalloonKnot", (0.16, 0.5, 0.92, 1), 0.35)
M_BUB = mat("ME_BalloonBubble", (0.9, 0.97, 1.0, 1), 0.02, 0.45)
# skin: closed body + neck
skin, rings = lathe("ITEM_WaterBalloon", 0.0)
top = rings[-1]
bm = bmesh.new(); bm.from_mesh(skin.data); bm.verts.ensure_lookup_table()
skin.data.materials.append(M_SKIN)
bm.free()
skin.data.polygons.foreach_set("use_smooth", [True] * len(skin.data.polygons))
# neck: short tube + cap above body top
zt = -H * 0.5 + 0.005 + H
rtop = profile(1.0)
bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=rtop * 0.9, depth=0.03, location=(0, 0, zt + 0.012))
neck = bpy.context.object; neck.name = "Neck"; neck.data.materials.append(M_KNOT)
# knot: squashed torus + tiny lip
bpy.ops.mesh.primitive_torus_add(major_radius=0.016, minor_radius=0.009, major_segments=24, minor_segments=12, location=(0, 0, zt + 0.032))
knot = bpy.context.object; knot.name = "Knot"; knot.scale = (1, 0.8, 0.75); knot.data.materials.append(M_KNOT)
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.012, location=(0.008, 0, zt + 0.045))
lip = bpy.context.object; lip.name = "Lip"; lip.scale = (1.4, 0.8, 0.55); lip.rotation_euler = (0, 0.5, 0); lip.data.materials.append(M_KNOT)
for o in (neck, knot, lip):
    for p in o.data.polygons: p.use_smooth = True
bpy.ops.object.select_all(action="DESELECT")
for o in (neck, knot, lip): o.select_set(True)
bpy.context.view_layer.objects.active = knot; bpy.ops.object.join(); knot = bpy.context.object; knot.name = "Knot"
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
# water: inset body, cut flat at 70 % height
water, wr = lathe("Water", 0.007)
water.data.materials.append(M_WATER)
bm = bmesh.new(); bm.from_mesh(water.data)
zcut = -H * 0.5 + 0.005 + H * 0.66
res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, zcut), plane_no=(0, 0, 1), clear_outer=True)
edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
bmesh.ops.edgeloop_fill(bm, edges=edges)
bm.to_mesh(water.data); bm.free()
for p in water.data.polygons: p.use_smooth = abs(p.normal.z) < 0.99
# bubbles: a few small spheres near the water surface + one stuck to the wall
import random; random.seed(924)
bubs = []
for i, (x, y, z, r) in enumerate([(0.03, 0.02, zcut - 0.025, 0.010), (-0.045, 0.01, zcut - 0.05, 0.007), (0.01, -0.05, zcut - 0.09, 0.006), (-0.02, -0.02, zcut - 0.13, 0.005), (0.06, -0.03, zcut - 0.07, 0.008)]):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=6, radius=r, location=(x, y, z)); b = bpy.context.object
    b.data.materials.append(M_BUB); bubs.append(b)
bpy.ops.object.select_all(action="DESELECT")
for b in bubs: b.select_set(True)
bpy.context.view_layer.objects.active = bubs[0]; bpy.ops.object.join(); bub = bpy.context.object; bub.name = "Bubbles"
for p in bub.data.polygons: p.use_smooth = True
# parent all to skin
for o in (water, knot, bub): o.parent = skin
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "ITEM_WaterBalloon.fbx"), use_selection=True, apply_unit_scale=True, bake_space_transform=True,
                         axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, object_types={"MESH"})
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "balloon.blend"))
# preview render
scn = bpy.context.scene; scn.render.engine = "CYCLES"; scn.cycles.samples = 64; scn.cycles.device = "CPU"
scn.render.resolution_x = 900; scn.render.resolution_y = 700; scn.render.film_transparent = False
for m in (M_SKIN, M_BUB):
    m.blend_method = "BLEND" if hasattr(m, "blend_method") else None
w = bpy.data.worlds.new("W"); scn.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.85, 0.95, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
bpy.ops.object.light_add(type="AREA", location=(0.4, -0.5, 0.6)); L = bpy.context.object; L.data.energy = 60; L.data.size = 0.5; L.rotation_euler = (0.9, 0, 0.6)
bpy.ops.mesh.primitive_plane_add(size=4, location=(0, 0, -H * 0.5)); g = bpy.context.object; g.data.materials.append(mat("Ground", (0.55, 0.8, 0.45, 1), 0.8))
bpy.ops.object.camera_add(location=(0.42, -0.62, 0.18)); cam = bpy.context.object; scn.camera = cam
d = Vector((0, 0, 0.0)) - cam.location; cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler(); cam.data.lens = 55
scn.render.filepath = os.path.join(OUT, "preview.png"); bpy.ops.render.render(write_still=True)
print("done")
