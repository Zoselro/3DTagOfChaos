import bpy, os
ROOT = r"E:\3DTagOfChaos\Charatctor"
f = [os.path.join(ROOT, d, x) for d in os.listdir(ROOT) if os.path.isdir(os.path.join(ROOT, d)) for x in os.listdir(os.path.join(ROOT, d)) if "Sailor" in x and x.endswith(".fbx")][0]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=f)
o = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
mw = o.matrix_world
vs = [mw @ v.co for v in o.data.vertices]
for z0 in [x / 20 for x in range(-19, 6)]:
    sl = [v for v in vs if z0 <= v.z < z0 + 0.05]
    c = [v.y for v in sl if abs(v.x) < 0.08]
    b = [v.y for v in sl if 0.2 < abs(v.x) < 0.4]
    print("INFO z=%.2f center maxY=%s body maxY=%s n=%d" % (z0, round(max(c), 3) if c else None, round(max(b), 3) if b else None, len(sl)))
print("PROBE_OK")
