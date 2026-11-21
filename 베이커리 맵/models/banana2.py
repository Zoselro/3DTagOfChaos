# 바나나 나무(§90 M4 → §92-9 송이를 기둥 둘레로 크게) A: 6.5 m(송이·꽃눈) / B: 4.8 m. 원점 = 밑동 바닥 가운데. 앞 = −Y.
import bpy, bmesh, math, os, sys, random
from mathutils import Vector as V, Matrix, Quaternion
OUT = sys.argv[-1]; os.makedirs(OUT, exist_ok=True)
def mat(name, col, rough, var=0.08, scale=6, sss=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    n = nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value = scale; n.inputs["Detail"].default_value = 6
    r = nt.nodes.new("ShaderNodeValToRGB"); r.color_ramp.elements[0].color = (*[c * (1 - var) for c in col], 1); r.color_ramp.elements[1].color = (*[min(1, c * (1 + var)) for c in col], 1)
    nt.links.new(n.outputs[0], r.inputs[0]); nt.links.new(r.outputs[0], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = rough
    return m
def leaf_mat():
    m = bpy.data.materials.new("Leaf"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    uv = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(uv.outputs["UV"], sep.inputs[0])
    # 가운데 잎맥(u≈0.5) 밝게 + 옆 잎결(v 방향 촘촘한 줄) + 가장자리 쪽 짙게
    mid = nt.nodes.new("ShaderNodeMath"); mid.operation = 'SUBTRACT'; mid.inputs[1].default_value = 0.5; nt.links.new(sep.outputs[0], mid.inputs[0])
    ab = nt.nodes.new("ShaderNodeMath"); ab.operation = 'ABSOLUTE'; nt.links.new(mid.outputs[0], ab.inputs[0])
    rib = nt.nodes.new("ShaderNodeMapRange"); rib.inputs["From Min"].default_value = 0.0; rib.inputs["From Max"].default_value = 0.035; rib.inputs["To Min"].default_value = 1.0; rib.inputs["To Max"].default_value = 0.0; rib.clamp = True
    nt.links.new(ab.outputs[0], rib.inputs["Value"])
    # 옆 잎결: 잎맥에서 비스듬히 퍼지는 줄 → (v + |u−0.5|·0.6)·90 의 sin
    k = nt.nodes.new("ShaderNodeMath"); k.operation = 'MULTIPLY'; k.inputs[1].default_value = 0.6; nt.links.new(ab.outputs[0], k.inputs[0])
    sm = nt.nodes.new("ShaderNodeMath"); sm.operation = 'ADD'; nt.links.new(sep.outputs[1], sm.inputs[0]); nt.links.new(k.outputs[0], sm.inputs[1])
    f = nt.nodes.new("ShaderNodeMath"); f.operation = 'MULTIPLY'; f.inputs[1].default_value = 140.0; nt.links.new(sm.outputs[0], f.inputs[0])
    sn = nt.nodes.new("ShaderNodeMath"); sn.operation = 'SINE'; nt.links.new(f.outputs[0], sn.inputs[0])
    vein = nt.nodes.new("ShaderNodeMapRange"); vein.inputs["From Min"].default_value = -1; vein.inputs["From Max"].default_value = 1; vein.inputs["To Min"].default_value = 0.0; vein.inputs["To Max"].default_value = 1.0; nt.links.new(sn.outputs[0], vein.inputs["Value"])
    edge = nt.nodes.new("ShaderNodeMapRange"); edge.inputs["From Min"].default_value = 0.1; edge.inputs["From Max"].default_value = 0.5; edge.inputs["To Min"].default_value = 0.0; edge.inputs["To Max"].default_value = 1.0; nt.links.new(ab.outputs[0], edge.inputs["Value"])
    noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 5
    c1 = nt.nodes.new("ShaderNodeMix"); c1.data_type = 'RGBA'; c1.inputs[6].default_value = (0.10, 0.36, 0.05, 1); c1.inputs[7].default_value = (0.05, 0.22, 0.03, 1)
    nt.links.new(edge.outputs[0], c1.inputs[0])
    c2 = nt.nodes.new("ShaderNodeMix"); c2.data_type = 'RGBA'; c2.blend_type = 'MULTIPLY'; c2.inputs[0].default_value = 0.35
    nt.links.new(c1.outputs[2], c2.inputs[6]); nt.links.new(vein.outputs[0], c2.inputs[7])
    c3 = nt.nodes.new("ShaderNodeMix"); c3.data_type = 'RGBA'; c3.inputs[7].default_value = (0.45, 0.62, 0.22, 1)
    nt.links.new(rib.outputs[0], c3.inputs[0]); nt.links.new(c2.outputs[2], c3.inputs[6])
    c4 = nt.nodes.new("ShaderNodeMix"); c4.data_type = 'RGBA'; c4.blend_type = 'OVERLAY'; c4.inputs[0].default_value = 0.25
    nt.links.new(c3.outputs[2], c4.inputs[6]); nt.links.new(noise.outputs["Color"], c4.inputs[7])
    nt.links.new(c4.outputs[2], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.5
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.15; nt.links.new(vein.outputs[0], bp.inputs["Height"]); nt.links.new(bp.outputs[0], b.inputs["Normal"])
    return m
def build(name, H, nleaves, bunch, seed):
    random.seed(seed)
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    M_TRUNK = mat("Trunk", (0.30, 0.22, 0.12), 0.8, var=0.35, scale=2.5)   # §92-9 야자수처럼 갈색 줄기
    M_DRY = mat("DrySheath", (0.30, 0.19, 0.09), 0.85, var=0.35, scale=9)
    M_LEAF = leaf_mat()
    M_BAN = mat("Banana", (0.98, 0.76, 0.08), 0.45, var=0.08)
    M_BTIP = mat("BananaTip", (0.32, 0.50, 0.12), 0.5)
    M_BEND = mat("BananaEnd", (0.20, 0.12, 0.06), 0.7)
    M_HEART = mat("Heart", (0.42, 0.08, 0.20), 0.4, var=0.15)
    M_STALK = mat("Stalk", (0.32, 0.45, 0.16), 0.6)
    objs = []
    # 줄기(가짜 줄기): 위로 갈수록 가늘고 살짝 휘며, 잎집 겹침 줄(세로)과 마디(가로)를 모양으로
    th = H * 0.62; bm = bmesh.new(); NS, NR = 28, 24; rings = []
    lean = V((random.uniform(-0.15, 0.15), random.uniform(-0.15, 0.15), 0))
    for i in range(NS + 1):
        t = i / NS; z = th * t
        r = (0.32 - 0.12 * t + 0.16 * max(0.0, 0.12 - t) / 0.12) * (H / 6.5) * (1 + 0.07 * abs(math.sin(t * 52)) ** 3)   # §92-9 야자수 마디 고리·넓은 밑동
        c = lean * (t * t)
        ring = []
        for k in range(NR):
            a = 2 * math.pi * k / NR
            rr = r * (1 + 0.035 * math.sin(a * 5 + t * 3))   # 잎집 겹침(세로 골)
            ring.append(bm.verts.new((c.x + rr * math.cos(a), c.y + rr * math.sin(a), z)))
        rings.append(ring)
    for i in range(NS):
        for k in range(NR):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % NR], rings[i + 1][(k + 1) % NR], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    me = bpy.data.meshes.new("Trunk"); bm.to_mesh(me); bm.free()
    tr = bpy.data.objects.new("Trunk", me); sc.collection.objects.link(tr); tr.data.materials.append(M_TRUNK); objs.append(tr)
    for p in me.polygons: p.use_smooth = True
    top = V((lean.x, lean.y, th))
    # 마른 잎집(줄기에 늘어져 붙은 갈색 띠) 몇 개
    for k in range(5):
        a = random.uniform(0, 2 * math.pi); z0 = th * random.uniform(0.15, 0.6); r0 = 0.30 * (H / 6.5)
        bm = bmesh.new(); vs = []
        for j in range(7):
            z = z0 - j * 0.18; w = 0.22 * (1 - j / 9)
            for s_ in (-1, 1):
                aa = a + s_ * w / r0
                vs.append(bm.verts.new((r0 * 1.04 * math.cos(aa) + 0.02 * j * math.cos(a), r0 * 1.04 * math.sin(aa) + 0.02 * j * math.sin(a), z)))
        for j in range(6): bm.faces.new((vs[2 * j], vs[2 * j + 1], vs[2 * j + 3], vs[2 * j + 2]))
        me = bpy.data.meshes.new("Dry"); bm.to_mesh(me); bm.free()
        d = bpy.data.objects.new("Dry", me); sc.collection.objects.link(d); d.data.materials.append(M_DRY)
        so = d.modifiers.new("t", 'SOLIDIFY'); so.thickness = 0.015; objs.append(d)
    # 잎: 길이 L, 폭 W — 잎자루(가는 부분) → 넓은 잎몸, 휘어 늘어지고, 잎맥을 따라 V자로 접힘, 가장자리 찢김
    def leaf(yaw, pitch, L, Wd, droop, tears):
        bm = bmesh.new(); NL, NW = 36, 12
        uvl = bm.loops.layers.uv.new("UVMap")
        petiole = 0.16
        def pos(t, u):
            w = 0.0 if t < petiole else Wd * math.sin(math.pi * min(1, (t - petiole) / (1 - petiole))) ** 0.55
            w = max(w, 0.035)
            x = (u - 0.5) * 2 * w
            arc = math.sin(t * math.pi * 0.55)                   # 위로 솟았다가
            z = arc * 0.18 * L - droop * (t ** 2.2) * L         # 끝은 축 처짐
            z += abs(x) * 0.22 - (abs(x) / max(w, 1e-3)) ** 2 * w * 0.12   # V자 접힘 + 가장자리 살짝 말림
            return V((x, t * L, z))
        tear_set = set(tears)
        rows = {}
        def vert(i, j, side):
            key = (i, j, side if (i in tear_set and (j <= 3 or j >= NW - 3)) else 0)
            if key not in rows:
                t, u = i / NL, j / NW; p = pos(t, u)
                if key[2] == 1: p += V((0, 0.012 * L, -0.01 * L))   # 갈라진 틈: 아래 조각을 조금 밀어 벌림
                rows[key] = bm.verts.new(p)
            return rows[key]
        for i in range(NL):
            for j in range(NW):
                vs = (vert(i, j, 1), vert(i, j + 1, 1), vert(i + 1, j + 1, -1), vert(i + 1, j, -1))
                if len(set(vs)) < 4: continue
                f = bm.faces.new(vs)
                for lp, uv in zip(f.loops, ((j / NW, i / NL), ((j + 1) / NW, i / NL), ((j + 1) / NW, (i + 1) / NL), (j / NW, (i + 1) / NL))):
                    lp[uvl].uv = uv
        me = bpy.data.meshes.new("Leaf"); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new("Leaf", me); sc.collection.objects.link(o); o.data.materials.append(M_LEAF)
        so = o.modifiers.new("t", 'SOLIDIFY'); so.thickness = 0.018
        for p in me.polygons: p.use_smooth = True
        o.rotation_euler = (math.radians(pitch), 0, math.radians(yaw)); o.location = top + V((0, 0, -0.05))
        return o
    for k in range(nleaves):
        yaw = 360 * k / nleaves + random.uniform(-14, 14)
        up = k % 3 == 0
        L = H * random.uniform(0.44, 0.55); Wd = L * 0.19
        tears = sorted(random.sample(range(12, 33), 4))
        objs.append(leaf(yaw, random.uniform(30, 45) if up else random.uniform(5, 18), L, Wd, random.uniform(0.45, 0.75), tears))
    # 잎자루 밑동을 감싸는 둥근 덩어리(잎이 나오는 자리)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.26 * H / 6.5, segments=20, ring_count=12, location=top); cr = bpy.context.active_object; cr.scale = (1, 1, 1.3); cr.data.materials.append(M_TRUNK); objs.append(cr)
    if bunch:
        # §92-9 송이: 잎 아래 꼭대기에서 굵은 송이 줄기(나무 기둥)가 늘어지고, 그 둘레를 바나나 손(층)이 빙 둘러 감싼다 → 끝에 보라 꽃눈
        S = H / 6.5
        out = V((0.0, -1.0, 0.0))
        base = top + out * 0.62 * S + V((0, 0, -0.35 * S))
        BL = 1.45 * S                                   # 바나나가 달린 기둥 길이
        stalk_pts = [top + V((0, -0.05, 0.05)), top + out * 0.45 * S + V((0, 0, 0.05 * S)), base, base + V((0, -0.03, -BL * 0.5)), base + V((0, -0.05, -BL - 0.35 * S))]
        cu = bpy.data.curves.new("BStalk", 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = 0.075 * S; cu.bevel_resolution = 4; cu.use_fill_caps = True
        sp = cu.splines.new('NURBS'); sp.points.add(len(stalk_pts) - 1); sp.order_u = 4; sp.use_endpoint_u = True
        for k, p in enumerate(stalk_pts): sp.points[k].co = (*p, 1)
        so_ = bpy.data.objects.new("BStalk", cu); sc.collection.objects.link(so_); so_.data.materials.append(M_STALK); objs.append(so_)
        def banana(root, dirv, L, r0):
            # 휜 원통(모서리 진 다섯 각) — 줄기에서 바깥으로 나와 위로 휜다, 끝은 초록, 꼭지는 갈색
            bm = bmesh.new(); NSg, NRg = 14, 10; rings = []
            side = dirv.cross(V((0, 0, 1))).normalized() if abs(dirv.z) < 0.95 else V((1, 0, 0)); upv = side.cross(dirv).normalized()
            for i in range(NSg + 1):
                t = i / NSg
                r = r0 * (math.sin(math.pi * (0.1 + 0.85 * t)) ** 0.45) * (0.55 if t < 0.12 else 1.0) + 0.006
                c = root + dirv * (L * t) + upv * (0.28 * L * math.sin(math.pi * t * 0.85)) + V((0, 0, 0.22 * L * t * t))   # 위로 휨
                ring = []
                for k in range(NRg):
                    a = 2 * math.pi * k / NRg; rr = r * (1 + 0.10 * math.cos(a * 5))
                    ring.append(bm.verts.new(c + (side * math.cos(a) + upv * math.sin(a)) * rr))
                rings.append(ring)
            for i in range(NSg):
                for k in range(NRg):
                    f = bm.faces.new((rings[i][k], rings[i][(k + 1) % NRg], rings[i + 1][(k + 1) % NRg], rings[i + 1][k]))
                    f.material_index = 2 if i == 0 else (1 if i >= NSg - 1 else 0)
            bm.faces.new(list(reversed(rings[0]))).material_index = 2; bm.faces.new(rings[-1]).material_index = 2
            me = bpy.data.meshes.new("Ban"); bm.to_mesh(me); bm.free()
            o = bpy.data.objects.new("Ban", me); sc.collection.objects.link(o)
            for m_ in (M_BAN, M_BTIP, M_BEND): o.data.materials.append(m_)
            for p in me.polygons: p.use_smooth = True
            return o
        hands = 5
        for h in range(hands):
            t = h / (hands - 1)
            c = base + V((0, -0.03 * t, -0.35 * S - BL * 0.95 * t))
            n = int(round(10 - 3 * t))                  # 위는 크고 많게, 아래로 작게
            L = (0.95 - 0.25 * t) * S; r0 = (0.115 - 0.025 * t) * S
            for row in range(1):                        # 손마다 한 줄 — 굵은 손가락이 줄기를 감싼다
                for k in range(n - row * 2):
                    a = 2 * math.pi * (k + 0.5 * row) / (n - row * 2) + h * 0.45
                    outd = V((math.cos(a), math.sin(a), 0))
                    d = (outd * 0.62 + V((0, 0, 0.78))).normalized()   # 위로 서서 줄기를 감싼다
                    objs.append(banana(c + outd * 0.09 * S, d, L, r0))
        # 꽃눈(보라 하트): 겹친 꽃턱잎
        tip = base + V((0, -0.05, -BL - 0.75 * S))
        for k in range(5):
            bpy.ops.mesh.primitive_uv_sphere_add(radius=(0.22 - 0.03 * k) * S, segments=16, ring_count=10, location=tip + V((0, 0, -0.07 * k * S)))
            ht = bpy.context.active_object; ht.scale = (1, 1, 1.8 - 0.15 * k); ht.rotation_euler = (0, 0, k * 0.7); ht.data.materials.append(M_HEART); objs.append(ht)
    dg = bpy.context.evaluated_depsgraph_get(); ms = []
    for o in objs:
        m2 = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg); m2.transform(o.matrix_world)
        n_ = bpy.data.objects.new(o.name + "_M", m2); sc.collection.objects.link(n_); ms.append(n_)
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    for o in sc.objects: o.select_set(o in ms)
    bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.join()
    j = bpy.context.active_object; j.name = name
    print(name, "dims", tuple(round(x, 2) for x in j.dimensions), "tris", sum(len(p.vertices) - 2 for p in j.data.polygons))
    bpy.ops.wm.save_as_mainfile(filepath=OUT + f"/{name}_src.blend")
build("SK_BananaTree_A", 6.5, 10, True, 5)
build("SK_BananaTree_B", 4.8, 8, True, 9)   # §92-9 B도 송이
