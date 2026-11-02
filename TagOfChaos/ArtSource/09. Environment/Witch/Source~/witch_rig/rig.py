# 마녀(허수아비) 리깅 — Request1009Plan.md §18.2. 사용: bpy python rig.py -- <witch.fbx> <out.blend>
# 1) 메시 줄이기(약 4만 정점) 2) 뼈대(손가락 포함, 앞 = -Y, 위 = +Z) 3) 자동 가중치 + 빠진 정점은 가장 가까운 뼈.
import bpy, bmesh, sys, math
import numpy as np
from mathutils import Vector

fbx, out = sys.argv[-2:]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
mesh.name = "Witch_Body"

# ---- 1. 메시 줄이기 ----
TARGET = 40000
bpy.context.view_layer.objects.active = mesh
mod = mesh.modifiers.new("dec", 'DECIMATE')
mod.ratio = TARGET / len(mesh.data.vertices)
bpy.ops.object.modifier_apply(modifier=mod.name)
print("verts after decimate", len(mesh.data.vertices))

V = np.array([mesh.matrix_world @ v.co for v in mesh.data.vertices])

def centroid(mask):
    return Vector(V[mask].mean(axis=0)) if mask.sum() else None

# ---- 2. 손가락 중심선(데이터로) ----
def finger_lines(side):
    x = V[:, 0] * side; y = V[:, 1]; z = V[:, 2]
    hand = (x > 0.74) & (z > 0.0) & (z < 0.2)
    lines = {}
    # 엄지: 앞쪽(-y)으로 갈라진 손가락
    th = hand & (y < -0.06)
    # 나머지 3개: y 띠로 나눈다
    bands = {"Index": (y > -0.03), "Middle": (y <= -0.03) & (y > -0.06)}
    rest = hand & (y >= -0.06)
    ys = np.sort(y[rest & (x > 0.86)])
    cut1, cut2 = np.percentile(ys, 33), np.percentile(ys, 66)
    groups = {"Finger1": rest & (y >= cut2), "Finger2": rest & (y < cut2) & (y >= cut1), "Finger3": rest & (y < cut1)}
    for name, g in groups.items():
        pts = []
        for x0 in (0.82, 0.875, 0.93):
            m = g & (np.abs(x - x0) < 0.02)
            if m.sum() == 0: m = g & (np.abs(x - x0) < 0.04)
            c = V[m].mean(axis=0); pts.append(Vector((c[0], c[1], c[2])))
        tip = V[g][np.argmax(x[g])]
        pts.append(Vector(tip))
        lines[name] = pts
    # 엄지: x 순서로 3점
    xs = x[th]
    pts = []
    for q in (0.05, 0.5, 0.97):
        x0 = np.quantile(xs, q)
        m = th & (np.abs(x - x0) < 0.012)
        c = V[m].mean(axis=0); pts.append(Vector(c))
    lines["Thumb"] = pts
    return lines

arm = bpy.data.armatures.new("WitchRig")
rig = bpy.data.objects.new("WitchRig", arm)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.edit_bones

def bone(name, head, tail, parent=None, connect=False, roll=0.0):
    b = eb.new(name); b.head = Vector(head); b.tail = Vector(tail); b.roll = roll
    if parent: b.parent = eb[parent]; b.use_connect = connect
    return b

Y = -0.02
bone("Root", (0, 0, -0.82), (0, 0.15, -0.82))
bone("Hips", (0, Y, -0.36), (0, Y, -0.2), "Root")
bone("Spine", (0, Y, -0.2), (0, Y, -0.02), "Hips", True)
bone("Chest", (0, Y, -0.02), (0, Y, 0.14), "Spine", True)
bone("Neck", (0, Y, 0.14), (0, -0.04, 0.22), "Chest", True)
bone("Head", (0, -0.04, 0.22), (0, -0.04, 0.40), "Neck", True)
bone("Hat1", (0, 0.0, 0.42), (0.02, 0.03, 0.58), "Head")
bone("Hat2", (0.02, 0.03, 0.58), (0.12, 0.08, 0.70), "Hat1", True)
bone("Hat3", (0.12, 0.08, 0.70), (0.30, 0.12, 0.70), "Hat2", True)
for side, s in (("L", -1), ("R", 1)):
    # 블렌더 기준 +X = 마녀의 왼쪽(앞이 -Y이므로). 이름은 마녀 기준.
    S = "L" if s > 0 else "R"
    bone(f"Shoulder.{S}", (s * 0.05, Y, 0.13), (s * 0.20, Y, 0.14), "Chest")
    bone(f"UpperArm.{S}", (s * 0.20, Y, 0.14), (s * 0.47, Y, 0.13), f"Shoulder.{S}", True)
    bone(f"ForeArm.{S}", (s * 0.47, Y, 0.13), (s * 0.73, -0.01, 0.125), f"UpperArm.{S}", True)
    bone(f"Hand.{S}", (s * 0.73, -0.01, 0.125), (s * 0.81, -0.01, 0.13), f"ForeArm.{S}", True)
    for fname, pts in finger_lines(s).items():
        prev = f"Hand.{S}"
        for k in range(len(pts) - 1):
            bn = f"{fname}{k + 1}.{S}"
            bone(bn, pts[k], pts[k + 1], prev, k > 0)
            prev = bn
    bone(f"Thigh.{S}", (s * 0.11, Y, -0.38), (s * 0.12, -0.03, -0.60), "Hips")
    bone(f"Shin.{S}", (s * 0.12, -0.03, -0.60), (s * 0.13, -0.02, -0.74), f"Thigh.{S}", True)
    bone(f"Foot.{S}", (s * 0.13, -0.02, -0.74), (s * 0.14, -0.20, -0.80), f"Shin.{S}", True)
    bone(f"Hair.{S}", (s * 0.12, -0.04, 0.30), (s * 0.18, -0.06, 0.02), "Head")
for i, (nm, d) in enumerate((("Front", (0, -1)), ("Back", (0, 1)), ("Left", (1, 0)), ("Right", (-1, 0)))):
    bone(f"Skirt{nm}", (d[0] * 0.12, Y + d[1] * 0.12, -0.25), (d[0] * 0.28, Y + d[1] * 0.25, -0.50), "Hips")
bone("HairBack", (0, 0.10, 0.30), (0, 0.18, 0.0), "Head")
bpy.ops.object.mode_set(mode='OBJECT')

# ---- 3. 가중치 ----
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True); rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')

# 가중치가 없는 정점(떨어진 머리카락·찢어진 천 조각 등)은 가장 가까운 뼈에 붙인다.
deform = [b for b in arm.bones if b.name != "Root"]
heads = np.array([rig.matrix_world @ b.head_local for b in deform]); tails = np.array([rig.matrix_world @ b.tail_local for b in deform])
def nearest_bone(p):
    ab = tails - heads; t = np.clip(((p - heads) * ab).sum(1) / np.maximum((ab * ab).sum(1), 1e-9), 0, 1)
    d = np.linalg.norm(p - (heads + ab * t[:, None]), axis=1)
    return deform[int(np.argmin(d))].name
groups = {g.index: g.name for g in mesh.vertex_groups}
fixed = 0
for v in mesh.data.vertices:
    if sum(g.weight for g in v.groups) < 1e-4:
        name = nearest_bone(np.array(mesh.matrix_world @ v.co))
        vg = mesh.vertex_groups.get(name) or mesh.vertex_groups.new(name=name)
        vg.add([v.index], 1.0, 'REPLACE'); fixed += 1
print("zero-weight vertices fixed:", fixed)
# 규칙으로 바로잡기: 모자(챙 포함)는 머리·모자 뼈만, 얼굴·머리통은 머리만 — 자동 가중치가 챙을 팔에 붙여 팔을 들면 챙이 휘었다.
def set_only(vidx, weights):
    for g in mesh.vertex_groups:
        g.remove([vidx])
    for name, w in weights.items():
        (mesh.vertex_groups.get(name) or mesh.vertex_groups.new(name=name)).add([vidx], w, 'REPLACE')
hat_fix = 0
for v in mesh.data.vertices:
    p = mesh.matrix_world @ v.co
    r = math.hypot(p.x, p.y + 0.02)
    if p.z > 0.40 or (p.z > 0.33 and r > 0.24):
        if p.z < 0.46: w = {"Head": 1.0}
        elif p.z < 0.58: k = (p.z - 0.46) / 0.12; w = {"Head": 1 - k, "Hat1": k}
        elif p.z < 0.68: k = (p.z - 0.58) / 0.10; w = {"Hat1": 1 - k, "Hat2": k}
        else: k = min(1.0, (p.z - 0.68) / 0.06 + (p.x - 0.1) * 2); k = max(0.0, k); w = {"Hat2": 1 - k, "Hat3": k} if k > 0 else {"Hat2": 1.0}
        set_only(v.index, w); hat_fix += 1
    elif p.z > 0.18 and r < 0.24:
        set_only(v.index, {"Head": 1.0}); hat_fix += 1
print("hat/head vertices set:", hat_fix)
# 한 정점에 뼈 4개까지(유니티 기본)
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=out)
print("bones", len(arm.bones))
