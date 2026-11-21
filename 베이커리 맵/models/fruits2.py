# §90 M5·M6: 비닐하우스(뼈대 + 비닐 막 따로) · 수박 12 m · 포도 6 m · 사과 4.8 m · 딸기 4 m. 원점 = 바닥 가운데.
import bpy, bmesh, math, os, sys, random
from mathutils import Vector as V, Matrix, noise
OUT = sys.argv[-1]; os.makedirs(OUT, exist_ok=True)
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True); return bpy.context.scene
def nmat(name, col, rough, var=0.08, scale=5, metal=0.0, coat=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    n = nt.nodes.new("ShaderNodeTexNoise"); n.inputs["Scale"].default_value = scale; n.inputs["Detail"].default_value = 6
    r = nt.nodes.new("ShaderNodeValToRGB"); r.color_ramp.elements[0].color = (*[c * (1 - var) for c in col], 1); r.color_ramp.elements[1].color = (*[min(1, c * (1 + var)) for c in col], 1)
    nt.links.new(n.outputs[0], r.inputs[0]); nt.links.new(r.outputs[0], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if coat: b.inputs["Coat Weight"].default_value = coat
    return m, nt, b
def save(sc, objs, name):
    dg = bpy.context.evaluated_depsgraph_get(); ms = []
    for o in objs:
        m2 = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg); m2.transform(o.matrix_world)
        n_ = bpy.data.objects.new(o.name + "_M", m2); sc.collection.objects.link(n_); ms.append(n_)
    for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    for o in sc.objects: o.select_set(o in ms)
    bpy.context.view_layer.objects.active = ms[0]; bpy.ops.object.join()
    j = bpy.context.active_object; j.name = name
    for p in j.data.polygons: p.use_smooth = True
    print(name, "dims", tuple(round(x, 2) for x in j.dimensions), "tris", sum(len(p.vertices) - 2 for p in j.data.polygons))
    bpy.ops.wm.save_as_mainfile(filepath=OUT + f"/{name}_src.blend")
def leaf(sc, m, base, dirv, L, Wd, curl=0.25):
    bm = bmesh.new(); NL, NW = 14, 6; g = []
    side = dirv.cross(V((0, 0, 1))).normalized() if abs(dirv.z) < 0.95 else V((1, 0, 0)); up = side.cross(dirv).normalized()
    for i in range(NL + 1):
        t = i / NL; w = Wd * math.sin(math.pi * t) ** 0.7 + 0.01
        g.append([bm.verts.new(base + dirv * (L * t) + side * ((j / NW - 0.5) * 2 * w) + up * (abs(j / NW - 0.5) * 2 * w * 0.3 - curl * L * t * t)) for j in range(NW + 1)])
    for i in range(NL):
        for j in range(NW): bm.faces.new((g[i][j], g[i][j + 1], g[i + 1][j + 1], g[i + 1][j]))
    me = bpy.data.meshes.new("Leaf"); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("Leaf", me); sc.collection.objects.link(o); o.data.materials.append(m)
    o.modifiers.new("t", 'SOLIDIFY').thickness = 0.03 * L
    return o
def tube(sc, name, pts, r, m, order=4):
    cu = bpy.data.curves.new(name, 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = r; cu.bevel_resolution = 4; cu.use_fill_caps = True
    sp = cu.splines.new('NURBS'); sp.points.add(len(pts) - 1); sp.order_u = min(order, len(pts)); sp.use_endpoint_u = True
    for k, p in enumerate(pts): sp.points[k].co = (*p, 1)
    o = bpy.data.objects.new(name, cu); sc.collection.objects.link(o); o.data.materials.append(m); return o
# ================= §92-7·92-8 =================
WHICH = os.environ.get("WHICH", "all")
LEN, WID, HGT = 30.0, 14.0, 8.0
RIDGE_X, RIDGE_W, RIDGE_H, RIDGE_L = 4.0, 3.4, 0.5, 25.0
def ridge(sc, m, cx, seed):
    # 흙 두둑: 둥근 단면(가운데 높고 가장자리 낮음) + 갈아 놓은 결(가로 골) + 덩어리 노이즈, 양 끝은 둥글게 내려감
    bm = bmesh.new(); NY, NX = 120, 22; g = []
    for i in range(NY + 1):
        y = -RIDGE_L / 2 + RIDGE_L * i / NY; ty = i / NY
        endk = min(1.0, min(ty, 1 - ty) / 0.04) ** 0.6
        row = []
        for j in range(NX + 1):
            u = j / NX * 2 - 1
            hz = RIDGE_H * endk * max(0.0, math.cos(u * math.pi / 2)) ** 0.75
            hz += 0.035 * math.sin(y * 9.0 + seed) * (1 - abs(u)) * endk                    # 갈아 놓은 결
            hz += 0.04 * noise.noise(V((cx * 3 + u * 2.5, y * 1.7, seed))) * endk
            x = cx + u * RIDGE_W / 2 * (0.75 + 0.25 * endk)
            row.append(bm.verts.new((x, y, 0.06 + max(0.0, hz))))
        g.append(row)
    for i in range(NY):
        for j in range(NX): bm.faces.new((g[i][j], g[i][j + 1], g[i + 1][j + 1], g[i + 1][j]))
    me = bpy.data.meshes.new("Ridge"); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new("Ridge", me); sc.collection.objects.link(o); o.data.materials.append(m); return o
if WHICH in ("all", "gh"):
    sc = reset(); random.seed(2)
    M_FRAME, _, _ = nmat("Frame", (0.92, 0.92, 0.90), 0.35, var=0.03, metal=0.3)
    M_CURB, _, _ = nmat("Curb", (0.55, 0.45, 0.35), 0.8, var=0.2, scale=3)
    M_SOIL, _, _ = nmat("Soil", (0.10, 0.06, 0.035), 0.97, var=0.45, scale=14)
    M_FLOOR, _, _ = nmat("SoilFloor", (0.19, 0.12, 0.07), 0.95, var=0.35, scale=9)
    M_PLANK, _, _ = nmat("Plank", (0.45, 0.28, 0.15), 0.7, var=0.25, scale=4)
    objs = []
    def arch(y, r=0.12):
        pts = []
        for k in range(13):
            a = math.pi * k / 12
            pts.append(V((-(WID / 2) * math.cos(a), y, 0.35 + (HGT - 0.35) * math.sin(a))))
        return tube(sc, "Rib", pts, r, M_FRAME)
    for k in range(16): objs.append(arch(-LEN / 2 + LEN * k / 15))
    for k in range(7):
        a = math.pi * (k + 0.5) / 7
        x = -(WID / 2) * math.cos(a); z = 0.35 + (HGT - 0.35) * math.sin(a)
        objs.append(tube(sc, "Purlin", [V((x, -LEN / 2, z)), V((x, 0, z)), V((x, LEN / 2, z))], 0.07, M_FRAME, order=2))
    for sx in (-1, 1):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(sx * WID / 2, 0, 0.18)); c = bpy.context.active_object; c.scale = (0.45, LEN + 0.4, 0.36)
        bv = c.modifiers.new("b", 'BEVEL'); bv.width = 0.05; bv.segments = 2; c.data.materials.append(M_CURB); objs.append(c)
    for y in (-LEN / 2, LEN / 2):
        for sx in (-1, 1):
            objs.append(tube(sc, "DoorPost", [V((sx * 2.2, y, 0.0)), V((sx * 2.2, y, 5.2))], 0.1, M_FRAME, order=2))
        objs.append(tube(sc, "DoorTop", [V((-2.2, y, 5.2)), V((2.2, y, 5.2))], 0.1, M_FRAME, order=2))
    # §92-7 바닥 전체 흙(살짝 밝은 다진 흙) + 두둑 2줄(짙은 갈아 놓은 흙) + 가운데 통로 나무 디딤판
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0, 0.04)); fl = bpy.context.active_object; fl.scale = (WID - 1.0, LEN - 0.2, 1)
    bpy.ops.object.transform_apply(scale=True); fl.data.materials.append(M_FLOOR)
    sd = fl.modifiers.new("s", 'SUBSURF'); sd.subdivision_type = 'SIMPLE'; sd.levels = 4
    objs.append(fl)
    for sx in (-1, 1): objs.append(ridge(sc, M_SOIL, sx * RIDGE_X, 3 + sx))
    for k in range(13):
        y = -LEN / 2 + 2.2 + k * (LEN - 4.4) / 12
        bpy.ops.mesh.primitive_cube_add(size=1, location=(random.uniform(-0.1, 0.1), y, 0.08)); p = bpy.context.active_object
        p.scale = (1.6, 0.7, 0.07); p.rotation_euler = (0, 0, random.uniform(-0.08, 0.08))
        bv = p.modifiers.new("b", 'BEVEL'); bv.width = 0.03; bv.segments = 2; p.data.materials.append(M_PLANK); objs.append(p)
    save(sc, objs, "SK_Greenhouse")

if WHICH in ("all", "melon"):
    # §92-8 수박: 줄무늬가 모이는 긴 축(X)의 끝(꼭지 자리)에 꼭지 — 예전에는 옆 배(위 가운데)에 꽂혀 있었다
    sc = reset()
    m = bpy.data.materials.new("Melon"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Object"], sep.inputs[0])
    at = nt.nodes.new("ShaderNodeMath"); at.operation = 'ARCTAN2'; nt.links.new(sep.outputs[2], at.inputs[0]); nt.links.new(sep.outputs[1], at.inputs[1])
    nz = nt.nodes.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 0.6; nz.inputs["Detail"].default_value = 3; nt.links.new(tc.outputs["Object"], nz.inputs[0])
    nzm = nt.nodes.new("ShaderNodeMath"); nzm.operation = 'MULTIPLY'; nzm.inputs[1].default_value = 0.35; nt.links.new(nz.outputs[0], nzm.inputs[0])
    ad = nt.nodes.new("ShaderNodeMath"); ad.operation = 'ADD'; nt.links.new(at.outputs[0], ad.inputs[0]); nt.links.new(nzm.outputs[0], ad.inputs[1])
    fq = nt.nodes.new("ShaderNodeMath"); fq.operation = 'MULTIPLY'; fq.inputs[1].default_value = 16.0; nt.links.new(ad.outputs[0], fq.inputs[0])
    sn = nt.nodes.new("ShaderNodeMath"); sn.operation = 'SINE'; nt.links.new(fq.outputs[0], sn.inputs[0])
    rp = nt.nodes.new("ShaderNodeValToRGB"); rp.color_ramp.elements[0].position = 0.40; rp.color_ramp.elements[0].color = (0.36, 0.60, 0.22, 1)
    rp.color_ramp.elements[1].position = 0.50; rp.color_ramp.elements[1].color = (0.03, 0.15, 0.04, 1)
    mr = nt.nodes.new("ShaderNodeMapRange"); mr.inputs["From Min"].default_value = -1; nt.links.new(sn.outputs[0], mr.inputs["Value"]); nt.links.new(mr.outputs[0], rp.inputs[0])
    nt.links.new(rp.outputs[0], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.35; b.inputs["Coat Weight"].default_value = 0.3
    bp = nt.nodes.new("ShaderNodeBump"); bp.inputs["Strength"].default_value = 0.1; nt.links.new(nz.outputs[0], bp.inputs["Height"]); nt.links.new(bp.outputs[0], b.inputs["Normal"])
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1, segments=64, ring_count=40, location=(0, 0, 4.6))
    mel = bpy.context.active_object; mel.scale = (6.0, 5.2, 4.6); mel.data.materials.append(m)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for v in mel.data.vertices: v.co *= 1 + 0.012 * noise.noise(v.co * 0.5)
    # 꼭지 쪽은 살짝 오목하게(꼭지가 박힌 자리)
    for v in mel.data.vertices:
        d = (V((v.co.x, v.co.y, v.co.z - 4.6)) - V((6.0, 0, 0))).length
        if d < 1.2: v.co.x -= 0.18 * (1 - d / 1.2) ** 2
    M_ST, _, _ = nmat("MelonStem", (0.35, 0.30, 0.12), 0.7)
    M_LF, _, _ = nmat("MelonLeaf", (0.12, 0.38, 0.08), 0.55, var=0.2)
    pole = V((5.88, 0.0, 4.6))
    st = tube(sc, "Stem", [pole, pole + V((0.45, 0.0, 0.12)), pole + V((0.9, 0.15, 0.45)), pole + V((1.35, 0.35, 0.55))], 0.2, M_ST)
    vine = tube(sc, "Vine", [pole + V((1.35, 0.35, 0.55)), pole + V((1.8, 0.6, 0.1)), pole + V((2.2, 1.0, -2.0)), V((8.4, 1.6, 0.3)), V((9.5, 2.6, 0.2)), V((10.2, 3.8, 0.2))], 0.14, M_ST)
    cr = tube(sc, "Tendril", [pole + V((1.35, 0.35, 0.55)) + V((0.35 * math.cos(t * 1.3), 0.35 * math.sin(t * 1.3), 0.08 * t)) + V((0.1 * t, -0.08 * t, 0)) for t in range(10)], 0.05, M_ST)
    lf = leaf(sc, M_LF, pole + V((1.25, 0.35, 0.55)), V((0.3, -0.9, 0.15)).normalized(), 2.4, 1.2)
    lf2 = leaf(sc, M_LF, V((9.0, 2.0, 0.3)), V((0.2, 0.9, 0.25)).normalized(), 2.2, 1.1, curl=0.05)
    save(sc, [mel, st, vine, cr, lf, lf2], "SK_Watermelon")

def lobed_leaf(bm, base, dirv, size, lobes, depth, tilt=0.35, width=0.85):
    # 잎 한 장: 잎자루 끝(base)에서 dirv 쪽으로 뻗은 타원(폭 width) — lobes>0이면 손바닥처럼 갈라짐, 가장자리 살짝 위로 오목
    dirv = dirv.normalized()
    side = dirv.cross(V((0, 0, 1))).normalized() if abs(dirv.z) < 0.95 else V((1, 0, 0)); up = side.cross(dirv).normalized()
    ctr = base + dirv * size * 0.5
    c = bm.verts.new(ctr + up * (-0.04 * size)); ring = []; N = max(16, lobes * 6)
    for k in range(N):
        th = 2 * math.pi * k / N
        r = 1.0 if lobes <= 0 else 1 - depth * (0.5 - 0.5 * math.cos(lobes * (th - math.pi)))
        r *= 1 + 0.06 * math.sin(th * 14) * (1 if lobes <= 0 else 0.5)          # 톱니
        ex = math.cos(th) * size * 0.5 * r; ey = math.sin(th) * size * 0.5 * width * r
        p = ctr + dirv * ex + side * ey + up * (tilt * 0.25 * size * (ey / (size * 0.5)) ** 2)
        ring.append(bm.verts.new(p))
    for k in range(N): bm.faces.new((c, ring[k], ring[(k + 1) % N]))
def finish(sc, bm, name, mats_by_index):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); sc.collection.objects.link(o)
    for mm in mats_by_index: o.data.materials.append(mm)
    o.modifiers.new("t", 'SOLIDIFY').thickness = 0.02
    return o

if WHICH in ("all", "straw"):
    # §92-7 딸기 포기 줄(두둑 하나, 길이 24 m): 세 갈래 잎 + 흰 꽃 + 작은 빨간 딸기. 원점 = 두둑 가운데 바닥.
    sc = reset(); random.seed(31)
    M_SL, _, _ = nmat("StrawLeaf", (0.12, 0.40, 0.08), 0.5, var=0.25)
    M_SB, _, _ = nmat("StrawBerry", (0.85, 0.06, 0.08), 0.3, var=0.1, coat=0.3)
    M_SF, _, _ = nmat("StrawFlower", (0.97, 0.96, 0.92), 0.5, var=0.03)
    M_SC, _, _ = nmat("StrawCenter", (0.95, 0.75, 0.15), 0.5)
    M_SS, _, _ = nmat("StrawStem", (0.22, 0.42, 0.12), 0.6)
    objs = []; bl = bmesh.new()
    def ridge_z(x): return 0.06 + RIDGE_H * max(0.0, math.cos((x / (RIDGE_W / 2)) * math.pi / 2)) ** 0.75
    for line in (-0.62, 0.62):
        y = -11.8
        while y < 11.8:
            px = line + random.uniform(-0.08, 0.08); pz = ridge_z(px) - 0.02; base = V((px, y, pz))
            for k in range(6):   # 잎 6장(세 갈래)
                a = 2 * math.pi * k / 6 + random.uniform(-0.3, 0.3)
                d = V((math.cos(a), math.sin(a), 0)); stem_top = base + d * 0.18 + V((0, 0, 0.26 + random.uniform(-0.04, 0.06)))
                for f in (-0.55, 0, 0.55):
                    dd = (d + d.cross(V((0, 0, 1))) * f).normalized(); dd.z = 0.15
                    lobed_leaf(bl, stem_top, dd.normalized(), 0.2, 0, 0.0, 0.6, 0.75)
            for k in range(random.randint(1, 2)):   # 흰 꽃
                a = random.uniform(0, 2 * math.pi); cpos = base + V((0.2 * math.cos(a), 0.2 * math.sin(a), 0.3))
                for pk in range(5):
                    pa = 2 * math.pi * pk / 5
                    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.035, segments=8, ring_count=4, location=cpos + V((0.035 * math.cos(pa), 0.035 * math.sin(pa), 0)))
                    o = bpy.context.active_object; o.scale = (1, 1, 0.35); o.data.materials.append(M_SF); objs.append(o)
                bpy.ops.mesh.primitive_uv_sphere_add(radius=0.022, segments=8, ring_count=4, location=cpos + V((0, 0, 0.01))); o = bpy.context.active_object; o.data.materials.append(M_SC); objs.append(o)
            for k in range(random.randint(2, 3)):   # 작은 딸기(두둑 옆으로 늘어짐)
                a = random.uniform(0, 2 * math.pi); bp_ = base + V((0.28 * math.cos(a), 0.28 * math.sin(a), 0.12))
                bpy.ops.mesh.primitive_uv_sphere_add(radius=0.055, segments=12, ring_count=8, location=bp_)
                o = bpy.context.active_object; o.data.materials.append(M_SB); objs.append(o)
                for v in o.data.vertices: v.co.z *= 1.25; v.co.x *= 1 + 0.25 * min(0, v.co.z) / 0.07; v.co.y *= 1 + 0.25 * min(0, v.co.z) / 0.07   # 아래로 뾰족
                lobed_leaf(bl, bp_ + V((0, 0, 0.06)), V((random.uniform(-1, 1), random.uniform(-1, 1), 0.2)), 0.07, 5, 0.6, 0.2, 1.0)   # 꼭지 잎
            y += random.uniform(0.85, 1.1)
    lo = finish(sc, bl, "Leaves", [M_SL]); objs.append(lo)
    save(sc, objs, "SK_StrawberryRow")

if WHICH in ("all", "vine"):
    # §92-7 수박 덩굴 줄(두둑 하나, 길이 24 m): 구불구불한 덩굴 + 갈라진 큰 잎 + 덩굴손 + 노란 꽃
    sc = reset(); random.seed(41)
    M_VL, _, _ = nmat("VineLeaf", (0.13, 0.38, 0.08), 0.55, var=0.25)
    M_VS, _, _ = nmat("VineStem", (0.24, 0.40, 0.12), 0.6)
    M_VF, _, _ = nmat("VineFlower", (0.98, 0.85, 0.15), 0.5)
    objs = []; bl = bmesh.new()
    def ridge_z(x): return 0.06 + RIDGE_H * max(0.0, math.cos((x / (RIDGE_W / 2)) * math.pi / 2)) ** 0.75
    for line in (-0.5, 0.5):
        pts = []; y = -11.8; k = 0
        while y <= 11.8:
            x = line + 0.45 * math.sin(y * 0.9 + line * 3); pts.append(V((x, y, ridge_z(x) + 0.03))); y += 0.8; k += 1
        objs.append(tube(sc, "Vine", pts, 0.035, M_VS, order=3))
        for p in pts:
            for s_ in (-1, 1):
                d = V((s_ * random.uniform(0.6, 1.0), random.uniform(-0.6, 0.6), 0.35)).normalized()
                lobed_leaf(bl, p + V((0, 0, 0.05)), d, random.uniform(0.38, 0.52), 5, 0.45, 0.6, 1.0)
            if random.random() < 0.35:
                objs.append(tube(sc, "Tendril", [p + V((0.12 * math.cos(t * 1.4), 0.12 * math.sin(t * 1.4), 0.03 * t)) + V((0.05 * t, 0, 0)) for t in range(8)], 0.012, M_VS))
            if random.random() < 0.25:
                for pk in range(5):
                    pa = 2 * math.pi * pk / 5
                    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.05, segments=8, ring_count=4, location=p + V((0.05 * math.cos(pa), 0.05 * math.sin(pa), 0.22)))
                    o = bpy.context.active_object; o.scale = (1, 1, 0.35); o.data.materials.append(M_VF); objs.append(o)
    lo = finish(sc, bl, "Leaves", [M_VL]); objs.append(lo)
    save(sc, objs, "SK_MelonVineRow")

if WHICH in ("all", "trellis"):
    # §92-7 포도 시렁(하우스 안 전체): 나무 기둥 2줄 × 6 + 가로 들보 + 세로 철선 + 위를 덮는 포도 잎 덩굴. 포도 송이는 맵 설치에서 단다.
    sc = reset(); random.seed(51)
    M_WD, _, _ = nmat("TrellisWood", (0.40, 0.26, 0.14), 0.75, var=0.25, scale=4)
    M_WR, _, _ = nmat("Wire", (0.6, 0.6, 0.6), 0.4, metal=0.8)
    M_GL, _, _ = nmat("GrapeLeaf", (0.16, 0.42, 0.09), 0.55, var=0.25)
    M_GV, _, _ = nmat("GrapeVine", (0.33, 0.22, 0.12), 0.75)
    objs = []; TH = 4.6; ys = [-12 + 4.8 * k for k in range(6)]
    for y in ys:
        for sx in (-1, 1):
            bpy.ops.mesh.primitive_cylinder_add(radius=0.11, depth=TH, vertices=10, location=(sx * RIDGE_X, y, TH / 2)); o = bpy.context.active_object; o.data.materials.append(M_WD); objs.append(o)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, y, TH)); o = bpy.context.active_object; o.scale = (2 * RIDGE_X + 0.8, 0.16, 0.16); o.data.materials.append(M_WD); objs.append(o)
    for x in [-RIDGE_X - 0.3 + 0.95 * k for k in range(10)]:
        objs.append(tube(sc, "Wire", [V((x, ys[0], TH + 0.1)), V((x, 0, TH + 0.08)), V((x, ys[-1], TH + 0.1))], 0.015, M_WR, order=2))
    for y in ys:   # 기둥을 타고 오르는 덩굴
        for sx in (-1, 1):
            objs.append(tube(sc, "Climb", [V((sx * RIDGE_X + 0.13 * math.cos(t * 1.1), y + 0.13 * math.sin(t * 1.1), 0.1 + t * TH / 12)) for t in range(13)], 0.03, M_GV, order=3))
    bl = bmesh.new()
    for k in range(950):   # 위를 덮는 잎(빈틈이 조금 있게)
        x = random.uniform(-RIDGE_X - 0.5, RIDGE_X + 0.5); y = random.uniform(ys[0] - 0.5, ys[-1] + 0.5)
        if abs(x) < 1.2 and random.random() < 0.45: continue   # 가운데 통로 위는 성기게(빛이 든다)
        d = V((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(-0.2, 0.25))).normalized()
        lobed_leaf(bl, V((x, y, TH + 0.12 + random.uniform(-0.12, 0.18))), d, random.uniform(0.45, 0.66), 5, 0.32, 0.4, 1.0)
    for y in ys[:-1]:   # 들보 사이 늘어진 잎 몇 장
        for sx in (-1, 1):
            for k in range(4):
                p = V((sx * RIDGE_X + random.uniform(-0.3, 0.3), y + random.uniform(0.5, 4.3), TH - random.uniform(0.1, 0.6)))
                lobed_leaf(bl, p, V((random.uniform(-1, 1), random.uniform(-1, 1), -0.6)).normalized(), 0.5, 5, 0.32, 0.3, 1.0)
    lo = finish(sc, bl, "Canopy", [M_GL]); objs.append(lo)
    save(sc, objs, "SK_GrapeTrellis")
