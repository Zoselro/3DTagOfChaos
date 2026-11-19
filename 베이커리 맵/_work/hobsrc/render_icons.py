import bpy, math, sys
from mathutils import Vector
out=sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.open_mainfile(filepath='out3/HOB_Items_Island.blend')
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=48; sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
sc.render.film_transparent=True
w=bpy.data.worlds.new('W'); sc.world=w; w.use_nodes=True; bg=next(n for n in w.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs[0].default_value=(0.8,0.8,0.85,1); bg.inputs[1].default_value=0.9
L=bpy.data.lights.new('S','SUN'); L.energy=3.2; so=bpy.data.objects.new('S',L); sc.collection.objects.link(so); so.rotation_euler=(math.radians(50),0,math.radians(-35))
cd=bpy.data.cameras.new('C'); cam=bpy.data.objects.new('C',cd); sc.collection.objects.link(cam); sc.camera=cam
for o in sc.objects:
    if o.type=='MESH': o.hide_render=True; o.location=(0,0,0)
for n in ('ITEM_AlmondSack','ITEM_FlourSack','ITEM_StrawberryJam'):
    o=bpy.data.objects[n]; o.hide_render=False
    sc.render.resolution_x=sc.render.resolution_y=256; cd.type='ORTHO'; cd.ortho_scale=0.78
    d=Vector((0.55,-1.0,0.45)).normalized(); cam.location=d*5; cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=f'{out}/{n}.png'; bpy.ops.render.render(write_still=True); o.hide_render=True
o=bpy.data.objects['HOB_Island_Main']; o.hide_render=False; sc.render.film_transparent=False; bg.inputs[0].default_value=(0.85,0.75,0.92,1)
sc.render.resolution_x=900; sc.render.resolution_y=560; cd.type='PERSP'; cd.lens=35; cd.clip_end=2000
cam.location=(230,-300,120); cam.rotation_euler=(Vector((0,0,-15))-cam.location).to_track_quat('-Z','Y').to_euler()
sc.render.filepath=f'{out}/HOB_Island_Main.png'; bpy.ops.render.render(write_still=True)
