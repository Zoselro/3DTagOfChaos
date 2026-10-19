# Split each Meshy FBX set into pieces (3D bbox clustering of loose parts),
# save a .blend per set and render a numbered overview for naming.
# blender -b --factory-startup --python cf_split.py -- <source_dir> <set1,set2,...>
import bpy, sys, os, json, math, traceback
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]
SETS = argv[1].split(",")
OUT = os.path.join(SRC, "_split")
os.makedirs(OUT, exist_ok=True)
LOG = open(os.path.join(OUT, "log.txt"), "a", encoding="utf-8")


def log(*a):
    LOG.write(" ".join(str(x) for x in a) + "\n"); LOG.flush()


def make_material(name, d):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    def tex(sfx, colorspace):
        p = os.path.join(d, name + sfx)
        if not os.path.exists(p): return None
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(p)
        n.image.colorspace_settings.name = colorspace
        return n
    a = tex("_albedo.png", "sRGB")
    if a: nt.links.new(a.outputs["Color"], bsdf.inputs["Base Color"]); nt.nodes.active = a
    r = tex("_roughness.png", "Non-Color")
    if r: nt.links.new(r.outputs["Color"], bsdf.inputs["Roughness"])
    mt = tex("_metallic.png", "Non-Color")
    if mt: nt.links.new(mt.outputs["Color"], bsdf.inputs["Metallic"])
    n = tex("_normal.png", "Non-Color")
    if n:
        nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(n.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    if a: nt.nodes.active = a
    return m


def world_bbox(o):
    cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))), Vector((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))


def overlap(a, b, pad):
    return all(a[0][i] - pad <= b[1][i] and b[0][i] - pad <= a[1][i] for i in range(3))


def cluster(objs, pad):
    boxes = [world_bbox(o) for o in objs]
    groups = [[i] for i in range(len(objs))]
    gb = list(boxes)
    changed = True
    while changed:
        changed = False
        for i in range(len(groups)):
            if not groups[i]: continue
            for j in range(i + 1, len(groups)):
                if not groups[j]: continue
                if overlap(gb[i], gb[j], pad):
                    groups[i] += groups[j]; groups[j] = []
                    gb[i] = (Vector(tuple(min(gb[i][0][k], gb[j][0][k]) for k in range(3))), Vector(tuple(max(gb[i][1][k], gb[j][1][k]) for k in range(3))))
                    changed = True
    return [g for g in groups if g]


def split_set(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    d = os.path.join(SRC, name)
    bpy.ops.import_scene.fbx(filepath=os.path.join(d, name + ".fbx"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in bpy.context.scene.objects: o.select_set(o in meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1: bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != obj: bpy.data.objects.remove(o, do_unlink=True)
    mat = make_material(name, d)
    obj.data.materials.clear(); obj.data.materials.append(mat)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.00005)
    bpy.ops.mesh.separate(type="LOOSE"); bpy.ops.object.mode_set(mode="OBJECT")
    parts = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    size = max((world_bbox(o)[1] - world_bbox(o)[0]).length for o in parts)
    groups = cluster(parts, 0.0)
    log(name, "parts", len(parts), "groups", len(groups))
    pieces = []
    for g in groups:
        objs = [parts[i] for i in g]
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1: bpy.ops.object.join()
        pieces.append(bpy.context.view_layer.objects.active)
    # drop dust (tiny bits)
    tris = lambda o: sum(len(p.vertices) - 2 for p in o.data.polygons)
    pieces.sort(key=lambda o: (round(world_bbox(o)[0].y, 1), world_bbox(o)[0].x))
    report = []
    for i, o in enumerate(pieces):
        lo, hi = world_bbox(o)
        o.name = "%s_%02d" % (name, i)
        report.append({"i": i, "min": list(lo), "max": list(hi), "tris": tris(o)})
    json.dump(report, open(os.path.join(OUT, name + ".json"), "w"), indent=1)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"))
    render_overview(name, pieces)


def render_overview(name, pieces):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 1600; sc.render.resolution_y = 1000
    sc.render.image_settings.file_format = "JPEG"
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for o in pieces:
        a, b = world_bbox(o)
        lo = Vector(tuple(min(lo[k], a[k]) for k in range(3))); hi = Vector(tuple(max(hi[k], b[k]) for k in range(3)))
        t = bpy.data.objects.new("lbl" + o.name, bpy.data.curves.new("lbl" + o.name, "FONT"))
        t.data.body = o.name[-2:]; t.data.size = max(0.06, (b - a).length * 0.12)
        t.location = Vector(((a.x + b.x) / 2, a.y, b.z + 0.02)); t.rotation_euler = (math.radians(90), 0, 0)
        sc.collection.objects.link(t)
    c = (lo + hi) / 2; ext = (hi - lo).length
    for tag, rot in (("front", (math.radians(90), 0, 0)), ("iso", (math.radians(60), 0, math.radians(-30)))):
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
        cam.data.type = "ORTHO"; cam.data.ortho_scale = ext * 1.05
        cam.rotation_euler = rot
        cam.location = c + cam.matrix_world.to_3x3() @ Vector((0, 0, ext * 3))
        cam.rotation_euler = rot
        bpy.context.view_layer.update()
        cam.location = c + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * ext * 3
        sc.camera = cam
        sc.render.filepath = os.path.join(OUT, name + "_" + tag + ".jpg")
        bpy.ops.render.render(write_still=True)


for s in SETS:
    try:
        split_set(s); log("OK", s)
    except Exception:
        log("FAIL", s, traceback.format_exc())
log("DONE")
LOG.close()
