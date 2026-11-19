import bpy, sys, math, os, json
from mathutils import Vector
blend, outdir = sys.argv[sys.argv.index("--")+1], sys.argv[sys.argv.index("--")+2]
names = sys.argv[sys.argv.index("--")+3].split(",") if len(sys.argv) > sys.argv.index("--")+3 else None
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'; sc.cycles.use_denoising = True
sc.render.resolution_x = sc.render.resolution_y = 360; sc.render.film_transparent = False
sc.view_settings.view_transform = 'Standard'
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND'); bg.inputs[0].default_value = (0.16,0.15,0.2,1); bg.inputs[1].default_value = 1.2
sun = bpy.data.lights.new("S", 'SUN'); sun.energy = 3.5; so = bpy.data.objects.new("S", sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(35))
cd = bpy.data.cameras.new("C"); cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
objs = [o for o in sc.objects if o.type == 'MESH' and o.name.startswith("HOB_")]
for o in objs: o.hide_render = True
for o in objs:
    if names and o.name not in names: continue
    o.hide_render = False
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]
    mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb))); mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
    ctr = (mn + mx) / 2; size = (mx - mn).length
    d = Vector((0.75, -1.0, 0.65)).normalized()
    cd.type = 'ORTHO'; cd.ortho_scale = size * 1.0
    cam.location = ctr + d * size * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    cd.clip_end = size * 10
    sc.render.filepath = os.path.join(outdir, o.name + ".png"); bpy.ops.render.render(write_still=True)
    o.hide_render = True
    print("R", o.name, flush=True)
