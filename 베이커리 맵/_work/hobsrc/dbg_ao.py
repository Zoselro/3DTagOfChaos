import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=os.path.abspath("outhq2/HOB_HQ_pilot2.blend"))
for o in bpy.data.objects:
    if o.type!='MESH': continue
    me=o.data; ca=me.color_attributes.get("Col")
    vals=np.array([d.color[0] for d in ca.data]) if ca else np.array([1])
    # per material mean AO
    loopmat=np.zeros(len(me.loops),dtype=int)
    for p in me.polygons:
        for li in p.loop_indices: loopmat[li]=p.material_index
    out=[]
    for i,m in enumerate(me.materials):
        sel=vals[loopmat==i] if ca else []
        out.append("%s:%.2f"%(m.name.replace("HOBHQ_",""), sel.mean() if len(sel) else -1))
    print(o.name, ca.domain if ca else None, ca.data_type if ca else None, " ".join(out))
