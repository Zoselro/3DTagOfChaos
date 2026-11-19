import bpy, math, random
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath='outw/HOB_Wheat.blend')
sc=bpy.context.scene
src=bpy.data.objects['HOB_WheatClump_LOD0']
for o in list(sc.objects):
    if o.type=='MESH': o.hide_render=True
random.seed(2); objs=[]
for i in range(-3,4):
    for j in range(-3,3):
        o=src.copy(); o.data=src.data.copy(); sc.collection.objects.link(o); o.hide_render=False
        o.location=(i*0.45+random.uniform(-0.1,0.1), j*0.45+random.uniform(-0.1,0.1),0); o.rotation_euler=(0,0,random.uniform(0,6.28)); objs.append(o)
bpy.context.view_layer.update()
data=[]
for o in objs:
    me=o.data; mw=o.matrix_world; inv=mw.inverted()
    ca=me.color_attributes['Col']; w=[0.0]*len(me.vertices); ph=[0.0]*len(me.vertices)
    for li,l in enumerate(me.loops): c=ca.data[li].color; w[l.vertex_index]=c[0]; ph[l.vertex_index]=c[1]
    data.append((o,[mw@v.co for v in me.vertices],w,ph,inv))
gm=bpy.data.meshes.new('g'); import bmesh; bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=1,y_segments=1,size=3); bm.to_mesh(gm); g=bpy.data.objects.new('ground',gm); sc.collection.objects.link(g); gm.materials.append(bpy.data.materials.get('HOB_Soil'))
sc.render.engine='CYCLES'; sc.cycles.samples=12; sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
wd=bpy.data.worlds.new('W'); sc.world=wd; wd.use_nodes=True; bg=next(n for n in wd.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs[0].default_value=(0.55,0.65,0.88,1)
L=bpy.data.lights.new('S','SUN'); L.energy=4; L.color=(1,0.88,0.7); so=bpy.data.objects.new('S',L); sc.collection.objects.link(so); so.rotation_euler=(math.radians(50),0,math.radians(-35))
sc.render.resolution_x=560; sc.render.resolution_y=380
cd=bpy.data.cameras.new('C'); cam=bpy.data.objects.new('C',cd); sc.collection.objects.link(cam); sc.camera=cam; cd.lens=35
cam.location=(2.6,-3.4,1.7); cam.rotation_euler=(Vector((0,0,0.75))-cam.location).to_track_quat('-Z','Y').to_euler()
wind=Vector((1,0.35,0)).normalized(); perp=Vector((-wind.y,wind.x,0))
for f in range(24):
    t=f/24*2.0
    for o,base,w,ph,inv in data:
        me=o.data
        for i,v in enumerate(me.vertices):
            b=base[i]; p=ph[i]*6.2832
            gust=0.5+0.5*math.sin(t*math.pi+b.x*0.6); sway=math.sin(t*math.pi*2+b.x*1.3+b.y*0.9+p)
            amp=w[i]*(0.06+0.10*gust); off=wind*(amp*(0.6+0.4*sway))+perp*(amp*0.25*math.sin(t*math.pi*4+p))
            q=b+off; q.z-=0.35*w[i]*off.length_squared
            v.co=inv@q
        me.update()
    sc.render.filepath=f'sway2/f{f:02d}.png'; bpy.ops.render.render(write_still=True)
