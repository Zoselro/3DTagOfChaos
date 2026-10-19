import bpy, sys, os
argv = sys.argv[sys.argv.index("--") + 1:]
SRC = argv[0]; MODELS = argv[1]
out = open(os.path.join(SRC, "_split", "uvprobe.txt"), "w")
img = bpy.data.images.load(os.path.join(SRC, "GateItems", "GateItems_albedo.png"))
W, H = img.size; px = list(img.pixels)
def avg(o):
    uv = o.data.uv_layers.active.data; r = g = b = 0; n = 0
    for i in range(0, len(uv), 5):
        u, v = uv[i].uv; x = int((u % 1) * (W - 1)); y = int((v % 1) * (H - 1)); k = (y * W + x) * 4
        r += px[k]; g += px[k + 1]; b += px[k + 2]; n += 1
    return (round(r / n, 2), round(g / n, 2), round(b / n, 2)), [l.name for l in o.data.uv_layers]
bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, "_split", "GateItems.blend"))
img = bpy.data.images.load(os.path.join(SRC, "GateItems", "GateItems_albedo.png")); W, H = img.size; px = list(img.pixels)
for i in range(2, 6):
    o = bpy.data.objects["GateItems_%02d" % i]
    out.write("split %d %s\n" % (i, avg(o)))
bpy.ops.wm.read_factory_settings(use_empty=True)
img = bpy.data.images.load(os.path.join(SRC, "GateItems", "GateItems_albedo.png")); W, H = img.size; px = list(img.pixels)
for c in ["Red", "Blue", "Green", "Yellow"]:
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, "CF_Lollipop_%s.fbx" % c))
    o = [x for x in bpy.context.selected_objects if x.type == "MESH"][0]
    out.write("fbx %s %s\n" % (c, avg(o)))
out.write("size %d %d\n" % (W, H)); out.close()
