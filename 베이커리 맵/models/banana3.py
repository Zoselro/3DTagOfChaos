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
    c1 = nt.nodes.new("ShaderNodeMix"); c1.data_type = 'RGBA'; c1.inputs[6].default_value = (0.48, 0.78, 0.12, 1); c1.inputs[7].default_value = (0.22, 0.52, 0.06, 1)
    nt.links.new(edge.outputs[0], c1.inputs[0])
    c2 = nt.nodes.new("ShaderNodeMix"); c2.data_type = 'RGBA'; c2.blend_type = 'MULTIPLY'; c2.inputs[0].default_value = 0.12
    nt.links.new(c1.outputs[2], c2.inputs[6]); nt.links.new(vein.outputs[0], c2.inputs[7])
    c3 = nt.nodes.new("ShaderNodeMix"); c3.data_type = 'RGBA'; c3.inputs[7].default_value = (0.62, 0.80, 0.28, 1)
    nt.links.new(rib.outputs[0], c3.inputs[0]); nt.links.new(c2.outputs[2], c3.inputs[6])
    c4 = nt.nodes.new("ShaderNodeMix"); c4.data_type = 'RGBA'; c4.blend_type = 'OVERLAY'; c4.inputs[0].default_value = 0.25
    nt.links.new(c3.outputs[2], c4.inputs[6]); nt.links.new(noise.outputs["Color"], c4.inputs[7])
    nt.links.new(c4.outputs[2], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.5
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.15; nt.links.new(vein.outputs[0], bp.inputs["Height"]); nt.links.new(bp.outputs[0], b.inputs["Normal"])
    return m
def build(name, H, nleaves, bunch, seed):
    # §92-9 (참고 그림): 휜 갈색 줄기(마디가 층층이) + 넓고 밝은 잎이 늘어짐 + 잎 밑 짧은 꼭지에 통통한 노란 송이 + 밑동 풀·돌
    random.seed(seed)
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    S = H / 6.5
    M_TRUNK = mat("Trunk", (0.40, 0.23, 0.11), 0.75, var=0.18, scale=3)
    M_RIM = mat("TrunkRim", (0.30, 0.17, 0.08), 0.8, var=0.15, scale=3)
    M_LEAF = leaf_mat()
    M_BAN = mat("Banana", (1.0, 0.80, 0.12), 0.4, var=0.06)
    M_BEND = mat("BananaEnd", (0.32, 0.19, 0.08), 0.7)
    M_STALK = mat("Stalk", (0.36, 0.22, 0.11), 0.7)
    M_GRASS = mat("Grass", (0.26, 0.58, 0.10), 0.6, var=0.25)
    M_ROCK = mat("Rock", (0.58, 0.52, 0.48), 0.9, var=0.2, scale=4)
    objs = []
    th = H * 0.66
    lean = V((0.0, 0.55 * S, 0.0))                       # 앞(-Y) 반대쪽으로 살짝 휨 → 송이는 앞
    def axis(t): return V((0.0, lean.y * (t * t), th * t))
    # 줄기: 마디 5개 — 마디마다 아래가 넓고 위로 좁아졌다가 위 끝에 살짝 둥근 테(다음 마디가 그 안에서 시작)
    NSEG = 5; bm = bmesh.new(); NR = 26; rings = []
    rows = 60
    for i in range(rows + 1):
        t = i / rows; seg = min(NSEG - 1, int(t * NSEG)); u = t * NSEG - seg
        base_r = (0.42 - 0.17 * t) * S
        r = base_r * (1.0 + 0.26 * (1 - u) ** 3 - 0.06 * u)          # 마디 아래 불룩 → 위로 좁아짐
        if i == 0: r *= 1.25
        c = axis(t); ring = []
        for k in range(NR):
            a = 2 * math.pi * k / NR
            rr = r * (1 + 0.025 * math.sin(a * 7 + seg))
            ring.append(bm.verts.new(c + V((rr * math.cos(a), rr * math.sin(a), 0))))
        rings.append(ring)
    for i in range(rows):
        for k in range(NR):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % NR], rings[i + 1][(k + 1) % NR], rings[i + 1][k]))
            u = ((i + 0.5) / rows) * NSEG % 1
            f.material_index = 1 if u < 0.12 else 0
    bm.faces.new(list(reversed(rings[0])))
    me = bpy.data.meshes.new("Trunk"); bm.to_mesh(me); bm.free()
    tr = bpy.data.objects.new("Trunk", me); sc.collection.objects.link(tr); tr.data.materials.append(M_TRUNK); tr.data.materials.append(M_RIM); objs.append(tr)
    for p in me.polygons: p.use_smooth = True
    top = axis(1.0)
    # 잎: 넓고 늘어진 바나나 잎(찢김 적게)
    def leaf(yaw, pitch, L, Wd, droop, tears):
        bm = bmesh.new(); NL, NW = 30, 12
        uvl = bm.loops.layers.uv.new("UVMap"); petiole = 0.12
        def pos(t, u):
            w = 0.0 if t < petiole else Wd * math.sin(math.pi * min(1, (t - petiole) / (1 - petiole))) ** 0.5
            w = max(w, 0.04 * S)
            x = (u - 0.5) * 2 * w
            z = math.sin(t * math.pi * 0.5) * 0.25 * L - droop * (t ** 2.0) * L
            z += abs(x) * 0.18 - (abs(x) / max(w, 1e-3)) ** 2 * w * 0.22   # 가운데 골 + 가장자리 아래로 말림
            return V((x, t * L, z))
        tear_set = set(tears); vmap = {}
        def vert(i, j, side):
            key = (i, j, side if (i in tear_set and (j <= 2 or j >= NW - 2)) else 0)
            if key not in vmap:
                p = pos(i / NL, j / NW)
                if key[2] == 1: p += V((0, 0.015 * L, -0.012 * L))
                vmap[key] = bm.verts.new(p)
            return vmap[key]
        for i in range(NL):
            for j in range(NW):
                vs = (vert(i, j, 1), vert(i, j + 1, 1), vert(i + 1, j + 1, -1), vert(i + 1, j, -1))
                if len(set(vs)) < 4: continue
                f = bm.faces.new(vs)
                for lp, uv in zip(f.loops, ((j / NW, i / NL), ((j + 1) / NW, i / NL), ((j + 1) / NW, (i + 1) / NL), (j / NW, (i + 1) / NL))): lp[uvl].uv = uv
        me = bpy.data.meshes.new("Leaf"); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new("Leaf", me); sc.collection.objects.link(o); o.data.materials.append(M_LEAF)
        o.modifiers.new("t", 'SOLIDIFY').thickness = 0.03 * S
        for p in me.polygons: p.use_smooth = True
        o.rotation_euler = (math.radians(pitch), 0, math.radians(yaw)); o.location = top + V((0, 0, 0.05 * S))
        return o
    for k in range(nleaves):
        yaw = 360 * k / nleaves + random.uniform(-10, 10)
        up = k % 3 == 0
        L = H * random.uniform(0.46, 0.56); Wd = L * 0.27
        tears = sorted(random.sample(range(12, 27), 2))
        objs.append(leaf(yaw, random.uniform(28, 42) if up else random.uniform(6, 18), L, Wd, random.uniform(0.55, 0.85), tears))
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.24 * S, segments=20, ring_count=12, location=top); cr = bpy.context.active_object; cr.scale = (1, 1, 0.9); cr.data.materials.append(M_TRUNK); objs.append(cr)
    if bunch:
        # 꼭지: 꼭대기에서 앞(-Y)으로 나와 아래로 굽은 짧고 굵은 갈색 줄기 → 끝에 송이
        stem_pts = [top + V((0, -0.05, -0.05)) * S, top + V((0, -0.45, 0.05)) * S, top + V((0, -0.75, -0.35)) * S, top + V((0, -0.82, -0.85)) * S]
        cu = bpy.data.curves.new("Stem", 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = 0.085 * S; cu.bevel_resolution = 4; cu.use_fill_caps = True
        sp = cu.splines.new('NURBS'); sp.points.add(len(stem_pts) - 1); sp.order_u = 4; sp.use_endpoint_u = True
        for k, p in enumerate(stem_pts): sp.points[k].co = (*p, 1)
        so_ = bpy.data.objects.new("Stem", cu); sc.collection.objects.link(so_); so_.data.materials.append(M_STALK); objs.append(so_)
        hub = top + V((0, -0.82, -0.85)) * S
        def banana(root, dirv, L, r0):
            bm = bmesh.new(); NSg, NRg = 14, 12; rings = []
            side = dirv.cross(V((0, 0, 1))).normalized() if abs(dirv.z) < 0.95 else V((1, 0, 0)); upv = side.cross(dirv).normalized()
            for i in range(NSg + 1):
                t = i / NSg
                r = r0 * (math.sin(math.pi * (0.06 + 0.88 * t)) ** 0.6) + 0.012 * S
                c = root + dirv * (L * t) + V((0, 0, 0.55 * L * t * t))        # 바나나 활 모양(끝이 위로)
                ring = []
                for k in range(NRg):
                    a = 2 * math.pi * k / NRg; rr = r * (1 + 0.06 * math.cos(a * 5))
                    ring.append(bm.verts.new(c + (side * math.cos(a) + upv * math.sin(a)) * rr))
                rings.append(ring)
            for i in range(NSg):
                for k in range(NRg):
                    f = bm.faces.new((rings[i][k], rings[i][(k + 1) % NRg], rings[i + 1][(k + 1) % NRg], rings[i + 1][k]))
                    f.material_index = 1 if (i == 0 or i >= NSg - 1) else 0
            bm.faces.new(list(reversed(rings[0]))).material_index = 1; bm.faces.new(rings[-1]).material_index = 1
            me = bpy.data.meshes.new("Ban"); bm.to_mesh(me); bm.free()
            o = bpy.data.objects.new("Ban", me); sc.collection.objects.link(o)
            o.data.materials.append(M_BAN); o.data.materials.append(M_BEND)
            for p in me.polygons: p.use_smooth = True
            return o
        # 송이: 손 3단(위 6·가운데 5·아래 4) — 꼭지 둘레 앞쪽 반원에 모여 아래로 달리고 끝이 위·바깥으로 휨
        for h, n in enumerate((6, 5, 4)):
            c = hub + V((0, 0, -0.38 * S * h))
            for k in range(n):
                a = math.radians(-150 + 300 * (k + 0.5) / n) - math.pi / 2       # 앞(-Y) 중심 반원
                outd = V((math.cos(a), math.sin(a), 0))
                d = (outd * 0.95 + V((0, 0, -0.3))).normalized()   # 바깥으로 나와 끝이 위로 휨(활 모양 upv)
                objs.append(banana(c + outd * 0.08 * S, d, (0.92 - 0.08 * h) * S, (0.17 - 0.015 * h) * S))
        # 송이 끝 꼭지(짧은 갈색 끝)
        bpy.ops.mesh.primitive_cone_add(radius1=0.07 * S, radius2=0.025 * S, depth=0.3 * S, vertices=10, location=hub + V((0, 0, -1.0 * S)), rotation=(math.pi, 0, 0))
        e = bpy.context.active_object; e.data.materials.append(M_STALK); objs.append(e)
    # 밑동: 뾰족한 풀 덤불(둘레) + 둥근 돌 2~3개
    gb = bmesh.new()
    for k in range(26):
        a = 2 * math.pi * k / 26 + random.uniform(-0.1, 0.1); dist = random.uniform(0.45, 0.95) * S
        base = V((math.cos(a) * dist, math.sin(a) * dist, 0.0)); outd = V((math.cos(a), math.sin(a), 0))
        Lb = random.uniform(0.45, 0.8) * S; w = 0.09 * S
        side = V((-outd.y, outd.x, 0))
        pts = []
        for i in range(6):
            t = i / 5; ww = w * (1 - t) ** 0.8
            p = base + outd * (Lb * 0.55 * t) + V((0, 0, Lb * math.sin(t * 1.3) * 0.8 - Lb * 0.25 * t * t))
            pts.append((gb.verts.new(p - side * ww), gb.verts.new(p + side * ww)))
        for i in range(5): gb.faces.new((pts[i][0], pts[i][1], pts[i + 1][1], pts[i + 1][0]))
    me = bpy.data.meshes.new("Grass"); gb.to_mesh(me); gb.free()
    g = bpy.data.objects.new("Grass", me); sc.collection.objects.link(g); g.data.materials.append(M_GRASS)
    g.modifiers.new("t", 'SOLIDIFY').thickness = 0.02 * S; objs.append(g)
    for k in range(3):
        a = random.uniform(0, 2 * math.pi); d = random.uniform(0.6, 1.0) * S
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=random.uniform(0.14, 0.24) * S, location=(math.cos(a) * d, math.sin(a) * d, 0.04 * S))
        rk = bpy.context.active_object; rk.scale = (1.3, 1.0, 0.6); rk.data.materials.append(M_ROCK); objs.append(rk)
        for p in rk.data.polygons: p.use_smooth = True
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
build("SK_BananaTree_A", 6.5, 9, True, 5)
build("SK_BananaTree_B", 4.8, 8, True, 9)   # §92-9 B도 송이
