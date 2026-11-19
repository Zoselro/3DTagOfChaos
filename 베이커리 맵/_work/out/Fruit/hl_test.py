
import bpy, numpy as np, math, os
with bpy.data.libraries.load(r"F:\3DTagOfChaos\베이커리 맵\_work\out\Fruit\lo_test.blend") as (src, dst): dst.objects = src.objects
lo = dst.objects[0]; bpy.context.scene.collection.objects.link(lo)
bpy.context.view_layer.objects.active = lo; lo.select_set(True)
def rng(tag):
    me = lo.data; u = np.zeros(len(me.loops) * 2); me.uv_layers.active.data.foreach_get("uv", u); u = u.reshape(-1, 3, 2)
    a = np.abs(np.cross(u[:, 1] - u[:, 0], u[:, 2] - u[:, 0])).sum() / 2
    print(tag, u.reshape(-1,2).min(0).round(3), u.reshape(-1,2).max(0).round(3), "area %.3f" % a, flush=True)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
bpy.ops.object.mode_set(mode="OBJECT"); rng("smart"); bpy.ops.object.mode_set(mode="EDIT")
import bmesh
def islands():
    bm = bmesh.new(); bm.from_mesh(lo.data); uvl = bm.loops.layers.uv.active
    bm.faces.ensure_lookup_table(); seen = set(); n = 0
    # 이어진 UV 섬 수(같은 UV 좌표를 공유하는 모서리로 연결)
    for f in bm.faces:
        if f.index in seen: continue
        n += 1; st = [f]; seen.add(f.index)
        while st:
            g = st.pop()
            for l in g.loops:
                e = l.edge
                for lf in e.link_faces:
                    if lf.index in seen: continue
                    # 공유 모서리의 UV가 같으면 같은 섬
                    a = [lp for lp in lf.loops if lp.edge == e][0]
                    if (a[uvl].uv - l.link_loop_next[uvl].uv).length < 1e-6 and (a.link_loop_next[uvl].uv - l[uvl].uv).length < 1e-6:
                        seen.add(lf.index); st.append(lf)
    bm.free(); return n
orig = lo
for rep in (10, 40):
    sm = orig.copy(); sm.data = orig.data.copy(); bpy.context.scene.collection.objects.link(sm)
    md = sm.modifiers.new("s", "SMOOTH"); md.factor = 1.0; md.iterations = rep
    for o in bpy.context.scene.objects: o.select_set(o == sm)
    bpy.context.view_layer.objects.active = sm; bpy.ops.object.modifier_apply(modifier="s")
    lo = sm
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.0, area_weight=0.0, scale_to_bounds=False)
    bpy.ops.uv.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(shape_method="AABB", rotate=False, margin_method="FRACTION", margin=0.0025)
    bpy.ops.object.mode_set(mode="OBJECT")
    # 같은 위상 → UV를 그대로 원본에 복사
    u = np.zeros(len(sm.data.loops) * 2); sm.data.uv_layers.active.data.foreach_get("uv", u)
    orig.data.uv_layers.active.data.foreach_set("uv", u)
    nisl = islands(); lo = orig; rng("smooth %d islands %d" % (rep, nisl))
