import bpy, math, sys, random
from mathutils import Vector
out=sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.open_mainfile(filepath='outw/HOB_Wheat.blend')
sc=bpy.context.scene; sc.render.engine='CYCLES'; sc.cycles.samples=48; sc.cycles.use_denoising=True; sc.view_settings.view_transform='Standard'
w=bpy.data.worlds.new('W'); sc.world=w; w.use_nodes=True; bg=next(n for n in w.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs[0].default_value=(0.10,0.06,0.04,1); bg.inputs[1].default_value=0.6
def light(rot,e,col=(1,0.85,0.6)):
    L=bpy.data.lights.new('S','SUN'); L.energy=e; L.color=col; L.angle=0.1; o=bpy.data.objects.new('S',L); sc.collection.objects.link(o); o.rotation_euler=rot
light((math.radians(55),0,math.radians(-40)),4.0); light((math.radians(70),0,math.radians(150)),1.5,(1,0.7,0.5))
cd=bpy.data.cameras.new('C'); cam=bpy.data.objects.new('C',cd); sc.collection.objects.link(cam); sc.camera=cam
hero=bpy.data.objects['HOB_WheatEar_Hero']; c0=bpy.data.objects['HOB_WheatClump_LOD0']; c1=bpy.data.objects['HOB_WheatClump_LOD1']
for o in (hero,c0,c1): o.location=(0,0,0); o.hide_render=True
def shot(name,objs,camloc,target,lens=50,res=(720,900)):
    for o in sc.objects:
        if o.type=='MESH': o.hide_render = o not in objs
    sc.render.resolution_x,sc.render.resolution_y=res; cd.lens=lens; cd.clip_end=200
    cam.location=camloc; cam.rotation_euler=(Vector(target)-Vector(camloc)).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=out+'/'+name+'.png'; bpy.ops.render.render(write_still=True)
shot('wheat_ear_close',[hero],(0.42,-0.55,0.12),(0.02,0,0.14),lens=70)
shot('wheat_clump',[c0],(1.1,-1.6,1.0),(0,0,0.75),lens=45)
# field: copies of LOD0 near, LOD1 far
random.seed(5); objs=[]
for i in range(-7,8):
    for j in range(-7,8):
        src=c0 if abs(i)<4 and j<2 else c1
        o=src.copy(); sc.collection.objects.link(o); o.location=(i*0.42+random.uniform(-0.1,0.1), j*0.42+random.uniform(-0.1,0.1),0); o.rotation_euler=(0,0,random.uniform(0,6.28)); objs.append(o)
gm=bpy.data.meshes.new('g'); import bmesh; bm=bmesh.new(); bmesh.ops.create_grid(bm,x_segments=1,y_segments=1,size=4); bm.to_mesh(gm); g=bpy.data.objects.new('ground',gm); sc.collection.objects.link(g); mat=bpy.data.materials.get('HOB_Soil'); gm.materials.append(mat); objs.append(g)
bg.inputs[0].default_value=(0.55,0.62,0.85,1); bg.inputs[1].default_value=1.0
shot('wheat_field',objs,(3.2,-4.4,2.1),(0,0,0.7),lens=35,res=(1100,700))
