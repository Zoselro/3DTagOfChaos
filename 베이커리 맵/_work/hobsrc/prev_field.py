import bpy, sys, math
from mathutils import Vector
blend, out = sys.argv[sys.argv.index("--")+1:sys.argv.index("--")+3]
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene
src = bpy.data.objects['HOB_WheatPatch_LOD0']; src.location = (0, 0, 0)
lod1 = bpy.data.objects['HOB_WheatPatch_LOD1']; lod1.location = (0, 30, 0)
for i in range(-3, 4):
    for j in range(-3, 4):
        if i == 0 and j == 0: continue
        o = src.copy(); o.location = (i * 2.0, j * 2.0, 0); o.rotation_euler = (0, 0, ((i * 7 + j * 3) % 4) * math.pi / 2); sc.collection.objects.link(o)
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0)); g = bpy.context.object
m = bpy.data.materials.new("soil"); m.use_nodes = True; m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.40, 0.27, 0.16, 1); g.data.materials.append(m)
bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(30))); bpy.context.object.data.energy = 4
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.75, 1.0, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
cam = bpy.data.cameras.new("c"); co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
co.location = (5.5, -7.5, 2.6); d = Vector((0, 0, 0.6)) - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); cam.lens = 30
sc.render.engine = 'CYCLES'; sc.cycles.samples = 24; sc.cycles.device = 'CPU'; sc.render.resolution_x = 960; sc.render.resolution_y = 540
sc.render.filepath = out; bpy.ops.render.render(write_still=True)
