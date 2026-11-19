import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
bpy.ops.wm.open_mainfile(filepath=os.path.abspath("outhq2/HOB_HQ_pilot2.blend"))
for o in bpy.data.objects:
    if o.type!='MESH' or o.name not in ("HOB_Fruit_Apple","HOB_Fruit_Strawberry"): continue
    me=o.data; tree=BVHTree.FromPolygons([v.co for v in me.vertices],[p.vertices for p in me.polygons])
    ca=me.color_attributes["Col"]; stats={}
    for p in me.polygons:
        c=p.center; n=p.normal; cnt=0; q=c+n*1e-4
        for _ in range(30):
            h,nn,i,d=tree.ray_cast(q,n,100)
            if h is None: break
            cnt+=1; q=h+n*1e-4
        m=me.materials[p.material_index].name.replace("HOBHQ_","")
        s=stats.setdefault(m,[0,0,0.0]); s[0]+=1; s[1]+= cnt%2; s[2]+=np.mean([ca.data[l].color[0] for l in p.loop_indices])
    print(o.name, {k:(v[0], round(v[1]/v[0],2), round(v[2]/v[0],2)) for k,v in stats.items()})
