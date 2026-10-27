# analyze.py -- import each character FBX, report structure, render front/back/side previews
import bpy, os, sys, json, math, mathutils
ROOT = r"E:\3DTagOfChaos\Charatctor"
OUT = os.path.join(ROOT, "_preview")
os.makedirs(OUT, exist_ok=True)

def fbx_files():
    res = []
    for d in sorted(os.listdir(ROOT)):
        p = os.path.join(ROOT, d)
        if not os.path.isdir(p) or d.startswith("_"): continue
        for f in os.listdir(p):
            if f.lower().endswith(".fbx"): res.append(os.path.join(p, f))
    return res

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def setup_render(res=480):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light = "STUDIO"; sh.color_type = "TEXTURE"
    sc.render.resolution_x = res; sc.render.resolution_y = res
    sc.render.film_transparent = False
    sc.world = bpy.data.worlds.new("w") if not sc.world else sc.world
    sc.world.color = (0.18, 0.18, 0.2)

def bounds(objs):
    mn = mathutils.Vector((1e9,)*3); mx = mathutils.Vector((-1e9,)*3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ mathutils.Vector(c)
            mn = mathutils.Vector(map(min, mn, w)); mx = mathutils.Vector(map(max, mx, w))
    return mn, mx

def shoot(name, center, size, views):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("cam") or bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"; cam.data.ortho_scale = size * 1.15
    sc.camera = cam
    for tag, d in views:
        dv = mathutils.Vector(d).normalized()
        cam.location = center + dv * size * 3
        cam.rotation_euler = (-dv).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(OUT, f"{name}_{tag}.png")
        bpy.ops.render.render(write_still=True)

report = {}
for i, f in enumerate(fbx_files()):
    reset(); setup_render()
    bpy.ops.import_scene.fbx(filepath=f)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    info = []
    for o in bpy.context.scene.objects:
        e = {"name": o.name, "type": o.type}
        if o.type == "MESH":
            e.update(verts=len(o.data.vertices), mats=[m.name for m in o.data.materials if m],
                     shapekeys=[k.name for k in o.data.shape_keys.key_blocks] if o.data.shape_keys else [],
                     groups=len(o.vertex_groups))
        if o.type == "ARMATURE": e["bones"] = len(o.data.bones)
        info.append(e)
    mn, mx = bounds(meshes)
    c = (mn + mx) / 2; size = max(mx - mn)
    report[f"c{i}"] = {"file": os.path.basename(f), "min": list(mn), "max": list(mx), "objects": info}
    shoot(f"c{i}", c, size, [("front", (0, -1, 0)), ("back", (0, 1, 0)), ("side", (1, 0, 0))])
json.dump(report, open(os.path.join(OUT, "report.json"), "w"), indent=1)
print("ANALYZE_OK")
