import bpy, sys, math, os
from mathutils import Vector
blend, outdir = sys.argv[sys.argv.index("--")+1], sys.argv[sys.argv.index("--")+2]
names = sys.argv[sys.argv.index("--")+3].split(",") if len(sys.argv) > sys.argv.index("--")+3 else None
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 64; sc.cycles.device = 'CPU'; sc.cycles.use_denoising = True
sc.render.resolution_x = sc.render.resolution_y = 560
sc.view_settings.view_transform = 'Standard'; sc.view_settings.exposure = -0.2
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND'); bg.inputs[0].default_value = (0.10, 0.08, 0.09, 1); bg.inputs[1].default_value = 1.0
def light(name, typ, energy, rot, color, size=1.0):
    L = bpy.data.lights.new(name, typ); L.energy = energy; L.color = color
    if typ == 'SUN': L.angle = math.radians(8)
    o = bpy.data.objects.new(name, L); sc.collection.objects.link(o); o.rotation_euler = rot; return o
light("Key", 'SUN', 4.0, (math.radians(50), 0, math.radians(35)), (1.0, 0.90, 0.78))
light("Fill", 'SUN', 1.2, (math.radians(65), 0, math.radians(-120)), (0.75, 0.82, 1.0))
light("Rim", 'SUN', 2.0, (math.radians(-60), 0, math.radians(160)), (1.0, 0.85, 0.9))
cd = bpy.data.cameras.new("C"); cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
objs = [o for o in sc.objects if o.type == 'MESH']
for o in objs: o.hide_render = True
for o in objs:
    if names and o.name not in names: continue
    o.hide_render = False
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))); mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    ctr = (mn + mx) / 2; size = (mx - mn).length
    d = Vector((0.6, -1.0, 0.55)).normalized()
    cd.type = 'PERSP'; cd.lens = 70; cam.location = ctr + d * size * 2.1; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler(); cd.clip_end = size * 20
    sc.render.filepath = os.path.join(outdir, o.name + ".png"); bpy.ops.render.render(write_still=True)
    o.hide_render = True
    print("R", o.name, flush=True)
