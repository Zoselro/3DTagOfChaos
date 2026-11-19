import bpy, math, sys
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='out2/HOB_Props2.blend')
sc=bpy.context.scene
for o in list(sc.objects):
    if o.name!='HOB_WheatPatch_3m': bpy.data.objects.remove(o)
o=bpy.data.objects['HOB_WheatPatch_3m']; o.location=(0,0,0); me=o.data
ca=me.color_attributes['Col']; base=[v.co.copy() for v in me.vertices]
w=[0.0]*len(me.vertices); ph=[0.0]*len(me.vertices)
for li,l in enumerate(me.loops): c=ca.data[li].color; w[l.vertex_index]=c[0]; ph[l.vertex_index]=c[1]
sc.render.engine='CYCLES'; sc.cycles.samples=10; sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
wd=bpy.data.worlds.new('W'); sc.world=wd; wd.use_nodes=True; bg=next(n for n in wd.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs[0].default_value=(0.55,0.65,0.85,1); bg.inputs[1].default_value=1.0
sun=bpy.data.lights.new('S','SUN'); sun.energy=3.5; so=bpy.data.objects.new('S',sun); sc.collection.objects.link(so); so.rotation_euler=(math.radians(50),0,math.radians(35))
sc.render.resolution_x=480; sc.render.resolution_y=360
cd=bpy.data.cameras.new('C'); cam=bpy.data.objects.new('C',cd); sc.collection.objects.link(cam); sc.camera=cam
cam.location=(3.2,-4.2,2.4); cam.rotation_euler=(Vector((0,0,0.6))-cam.location).to_track_quat('-Z','Y').to_euler()
wind=Vector((1,0.35,0)).normalized()
N=24
for f in range(N):
    t=f/N*2.0  # 2 s loop
    for i,v in enumerate(me.vertices):
        b=base[i]; p=ph[i]*6.283
        gust=0.5+0.5*math.sin(t*math.pi + b.x*0.6)          # slow gust travelling across the field
        s=math.sin(t*math.pi*2 + b.x*1.3 + b.y*0.9 + p)        # per-clump sway
        amp=w[i]*(0.06+0.10*gust)
        off=wind*(amp*(0.6+0.4*s)) + Vector((-wind.y,wind.x,0))*(amp*0.25*math.sin(t*math.pi*4+p))
        v.co=b+off; v.co.z=b.z-0.35*w[i]*(amp*amp)/max(0.1,1.0)
    me.update(); sc.render.filepath=f'sway/f{f:02d}.png'; bpy.ops.render.render(write_still=True)
