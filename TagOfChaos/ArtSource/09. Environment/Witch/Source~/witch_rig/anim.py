# 마녀 동작 5개(Idle·Rise·Strike — Request1009Plan.md §18.2, Walk·TurnBack — §23 X3). 사용: bpy python anim.py -- <rigged.blend> <out.blend> [render_prefix tex]
# 자세는 "부모가 쉬는 자세일 때 기준" 회전으로 적는다(FK). 앞 = -Y, 위 = +Z, 블렌더 +X = 마녀의 왼쪽.
import bpy, sys, math
from mathutils import Vector, Quaternion, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
render_prefix = args[2] if len(args) > 2 else None
tex = args[3] if len(args) > 3 else None
bpy.ops.wm.open_mainfile(filepath=src)
rig = bpy.data.objects["WitchRig"]
FPS = 30
bpy.context.scene.render.fps = FPS

def axis_rot(axis, deg):
    return Quaternion(Vector(axis).normalized(), math.radians(deg))

def aim(bone_name, direction):
    """쉬는 자세의 뼈 방향을 direction(월드)으로 돌리는 회전."""
    b = rig.data.bones[bone_name]
    rest_dir = (b.tail_local - b.head_local).normalized()
    return rest_dir.rotation_difference(Vector(direction).normalized())

def to_local(bone_name, q_world):
    rest = rig.data.bones[bone_name].matrix_local.to_quaternion()
    return rest.inverted() @ q_world @ rest

SIDES = (("L", 1), ("R", -1))
FINGERS = ("Finger1", "Finger2", "Finger3")

def fist(amount):
    """주먹 쥐기(0 = 편 손, 1 = 꽉 쥠): 손가락 끝이 손바닥(아래)으로 말린다."""
    p = {}
    for S, s in SIDES:
        for f in FINGERS:
            for k, deg in ((1, 70), (2, 85), (3, 70)):
                p[f"{f}{k}.{S}"] = axis_rot((0, s, 0), deg * amount)
        p[f"Thumb1.{S}"] = axis_rot((0, 0, s), 35 * amount) @ axis_rot((0, s, 0), 30 * amount)
        p[f"Thumb2.{S}"] = axis_rot((0, s, 0), 60 * amount)
    return p

def idle(t):
    """허수아비처럼 팔을 벌린 채 숨쉬기·흔들림(t초)."""
    w = 2 * math.pi * t / 4.0
    p = {
        "Spine": axis_rot((1, 0, 0), 3 + 1.5 * math.sin(w)),
        "Chest": axis_rot((1, 0, 0), 2 + 1.5 * math.sin(w + 0.6)) @ axis_rot((0, 0, 1), 2 * math.sin(w * 0.5)),
        "Head": axis_rot((1, 0, 0), 4 + 2 * math.sin(w + 1.2)) @ axis_rot((0, 1, 0), 4 * math.sin(w * 0.5 + 0.3)),
        "Hat3": axis_rot((1, 0, 0), 6 * math.sin(w * 1.5)),
        "Hair.L": axis_rot((1, 0, 0), 4 * math.sin(w + 2)), "Hair.R": axis_rot((1, 0, 0), 4 * math.sin(w + 2.4)),
        "HairBack": axis_rot((1, 0, 0), -4 * math.sin(w + 1.7)),
        "SkirtFront": axis_rot((1, 0, 0), 4 * math.sin(w * 1.3)), "SkirtBack": axis_rot((1, 0, 0), -4 * math.sin(w * 1.3 + 0.5)),
        "SkirtLeft": axis_rot((0, 1, 0), -4 * math.sin(w * 1.2)), "SkirtRight": axis_rot((0, 1, 0), 4 * math.sin(w * 1.2 + 0.7)),
    }
    for S, s in SIDES:
        droop = 12 + 3 * math.sin(w + (0 if s > 0 else 1.1))
        p[f"UpperArm.{S}"] = axis_rot((0, 1, 0), s * droop)          # 팔 끝이 조금 처진다
        p[f"ForeArm.{S}"] = axis_rot((0, 1, 0), s * (6 + 2 * math.sin(w + 0.8)))
        p[f"Hand.{S}"] = axis_rot((0, 1, 0), s * 15)
        for f in FINGERS:
            p[f"{f}1.{S}"] = axis_rot((0, 1, 0), s * 10); p[f"{f}2.{S}"] = axis_rot((0, 1, 0), s * 12)
    return p

def raised(t):
    """두 팔을 머리 위로 들고 주먹을 꽉 쥔 자세."""
    p = {"Spine": axis_rot((1, 0, 0), -8), "Chest": axis_rot((1, 0, 0), -10), "Head": axis_rot((1, 0, 0), -12)}
    for S, s in SIDES:
        p[f"Shoulder.{S}"] = axis_rot((0, 1, 0), -s * 15)
        p[f"UpperArm.{S}"] = aim(f"UpperArm.{S}", (s * 0.35, -0.55, 0.76))   # 위·앞으로 들어 모자 앞에서 주먹이 보이게
        p[f"ForeArm.{S}"] = axis_rot((0, 0, 1), s * 40) @ axis_rot((0, 1, 0), -s * 35)  # 팔꿈치를 굽혀 두 주먹을 머리 앞 위에서 모은다
        p[f"Hand.{S}"] = axis_rot((0, 1, 0), -s * 10)
    p.update(fist(1.0))
    return p

def slam(t):
    """두 주먹으로 맵(앞쪽 땅)을 내리친 자세: 몸을 숙이고 무릎을 굽혀 주먹이 땅에 닿는다."""
    p = {"Hips": axis_rot((1, 0, 0), 25), "Spine": axis_rot((1, 0, 0), 30), "Chest": axis_rot((1, 0, 0), 25),
         "Head": axis_rot((1, 0, 0), -25), "Hat3": axis_rot((1, 0, 0), 25),
         "HairBack": axis_rot((1, 0, 0), 30), "Hair.L": axis_rot((1, 0, 0), 25), "Hair.R": axis_rot((1, 0, 0), 25),
         "SkirtBack": axis_rot((1, 0, 0), -20)}
    for S, s in SIDES:
        p[f"Shoulder.{S}"] = axis_rot((0, 0, 1), -s * 10)
        p[f"UpperArm.{S}"] = aim(f"UpperArm.{S}", (s * 0.18, -0.95, -0.25))  # 숙인 몸(부모) 기준 앞으로 뻗으면 땅을 향한다
        p[f"ForeArm.{S}"] = axis_rot((0, 0, 1), -s * 12)
        p[f"Thigh.{S}"] = axis_rot((1, 0, 0), -45)
        p[f"Shin.{S}"] = axis_rot((1, 0, 0), 60)
        p[f"Foot.{S}"] = axis_rot((1, 0, 0), -15)
    p.update(fist(1.0))
    return p

def hunched(t):
    """나타나기 시작: 고개를 숙이고 팔을 늘어뜨린 자세."""
    p = {"Spine": axis_rot((1, 0, 0), 15), "Chest": axis_rot((1, 0, 0), 15), "Head": axis_rot((1, 0, 0), 35)}
    for S, s in SIDES:
        p[f"UpperArm.{S}"] = axis_rot((0, 1, 0), s * 55)
        p[f"ForeArm.{S}"] = axis_rot((0, 1, 0), s * 20)
        p[f"Hand.{S}"] = axis_rot((0, 1, 0), s * 30)
    return p

HIPS_DROP = {"slam": -0.16}

def apply(pose, hips_drop=0.0):
    for pb in rig.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        q = pose.get(pb.name)
        pb.rotation_quaternion = to_local(pb.name, q) if q is not None else Quaternion()
        pb.location = Vector((0, 0, 0))
    rig.pose.bones["Hips"].location = Vector((0, hips_drop, 0))  # Hips 뼈의 +Y = 월드 +Z

def key(frame):
    for pb in rig.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        pb.keyframe_insert("location", frame=frame)

def blend(a, b, k):
    out = {}
    for n in set(a) | set(b):
        out[n] = a.get(n, Quaternion()).slerp(b.get(n, Quaternion()), k)
    return out

def smooth(k): return k * k * (3 - 2 * k)

def make_action(name, frames, sample):
    act = bpy.data.actions.new(name)
    rig.animation_data_create(); rig.animation_data.action = act
    for f in frames:
        pose, drop = sample(f / FPS)
        apply(pose, drop); key(f)
    act.use_fake_user = True
    return act

# ---- Idle: 4초 반복 ----
idle_frames = list(range(0, 121, 4))
make_action("Idle", idle_frames, lambda t: (idle(t), 0.0))

# ---- Rise: 3초(나타나는 시간과 같음) — 숙인 자세에서 일어나 Idle로 ----
def rise_sample(t):
    k = smooth(min(1.0, t / 3.0))
    return blend(hunched(t), idle(t), k), 0.0
make_action("Rise", list(range(0, 91, 3)), rise_sample)

# ---- Strike: 들기(0~0.6초, 주먹 쥠) → 내리침(0.6~0.95초, 0.95초 = 충격) → 짚은 채(~1.8초) → 일어섬(~3.4초) ----
IMPACT = 0.95
def strike_sample(t):
    if t < 0.6:
        k = smooth(t / 0.6); return blend(idle(t), raised(t), k), 0.0
    if t < IMPACT:
        k = ((t - 0.6) / (IMPACT - 0.6)) ** 2  # 내리칠수록 빨라진다
        return blend(raised(t), slam(t), k), HIPS_DROP["slam"] * k
    if t < 1.8:
        bounce = 0.04 * math.sin(min(1.0, (t - IMPACT) / 0.25) * math.pi)
        return slam(t), HIPS_DROP["slam"] + bounce
    k = smooth(min(1.0, (t - 1.8) / 1.6))
    return blend(slam(t), idle(t), k), HIPS_DROP["slam"] * (1 - k)
strike_frames = sorted(set(list(range(0, 103, 2)) + [int(IMPACT * FPS), int(IMPACT * FPS) + 1]))
make_action("Strike", strike_frames, strike_sample)

# ---- Walk: 2.4초 반복(두 걸음) — 제자리 걸음, 이동·속도는 Unity(WitchPresenter)가 맞춘다 ----
# 발이 땅에 닿는 순간 = 주기의 0.25·0.75(다리가 가장 벌어진 때) — Unity의 발걸음 소리(WalkStepPhases)와 같다.
WALK_CYCLE = 2.4
def walk(t):
    p = idle(t * 0.6)
    w = 2 * math.pi * t / WALK_CYCLE
    sw = math.sin(w)
    p["Spine"] = axis_rot((1, 0, 0), 7) @ axis_rot((0, 0, 1), -5 * sw)
    p["Chest"] = axis_rot((1, 0, 0), 4) @ axis_rot((0, 0, 1), 8 * sw)           # 어깨는 다리와 반대로 비튼다
    p["Head"] = axis_rot((1, 0, 0), 6 + 2 * math.sin(2 * w)) @ axis_rot((0, 0, 1), -5 * sw)
    p["Hat3"] = axis_rot((1, 0, 0), 9 * math.sin(2 * w + 0.6))
    p["SkirtFront"] = axis_rot((1, 0, 0), 10 * abs(sw)); p["SkirtBack"] = axis_rot((1, 0, 0), -8 * abs(sw))
    p["SkirtLeft"] = axis_rot((0, 1, 0), -7 * sw); p["SkirtRight"] = axis_rot((0, 1, 0), -7 * sw)
    p["HairBack"] = axis_rot((1, 0, 0), -6 * math.sin(2 * w + 1.0))
    for S, s in SIDES:
        leg = sw if s > 0 else -sw                                                  # 왼다리 앞 = 오른다리 뒤
        p[f"Thigh.{S}"] = axis_rot((1, 0, 0), -28 * leg)                           # - = 앞으로
        p[f"Shin.{S}"] = axis_rot((1, 0, 0), 8 + 38 * max(0.0, math.sin(w + (0 if s > 0 else math.pi) + 1.2)))  # 앞으로 나갈 때 무릎을 굽힌다
        p[f"Foot.{S}"] = axis_rot((1, 0, 0), -8 * leg)
        droop = 12 + 3 * math.sin(w)
        p[f"UpperArm.{S}"] = axis_rot((0, 1, 0), s * droop) @ axis_rot((1, 0, 0), 14 * leg)   # 팔은 같은 쪽 다리와 반대로
        p[f"ForeArm.{S}"] = axis_rot((0, 1, 0), s * 8)
    return p, -0.012 - 0.014 * math.cos(2 * w)                                      # 걸음마다 엉덩이가 오르내린다
make_action("Walk", list(range(0, 73, 2)), walk)

# ---- TurnBack: 2초 — 맵을 등진 채 뒤돌아본다. 몸 전체의 180° 회전은 Unity가 같은 2초에 돌린다(TURN_SIGN 방향).
# 여기서는 고개 → 가슴 → 엉덩이 순서로 먼저 돌고(몸보다 앞섬), 발을 두 번 옮겨 딛는 동작만 담는다.
TURN_SECONDS = 2.0
TURN_SIGN = 1  # + = 위에서 볼 때 반시계(블렌더 Z) — Unity WitchPresenter.TurnSign과 같은 방향이어야 한다
def lead(k, peak_at, amount):
    """k(0~1) 동안 0 → amount(peak_at에서 최대) → 0 으로 앞섰다 돌아오는 비틀림."""
    if k <= peak_at: return amount * smooth(k / peak_at)
    return amount * (1 - smooth((k - peak_at) / (1 - peak_at)))
def turn_sample(t):
    k = min(1.0, t / TURN_SECONDS)
    p = idle(t)
    g = TURN_SIGN
    p["Head"] = axis_rot((0, 0, 1), g * lead(k, 0.35, 55)) @ axis_rot((1, 0, 0), 4)
    p["Chest"] = axis_rot((0, 0, 1), g * lead(k, 0.45, 25))
    p["Spine"] = axis_rot((0, 0, 1), g * lead(k, 0.5, 10)) @ axis_rot((1, 0, 0), 3)
    p["Hips"] = axis_rot((0, 0, 1), -g * lead(k, 0.55, 8))                            # 엉덩이는 살짝 늦게 따라온다
    p["Hat3"] = axis_rot((0, 0, 1), -g * lead(k, 0.5, 14))
    p["SkirtLeft"] = axis_rot((0, 1, 0), -g * lead(k, 0.6, 10)); p["SkirtRight"] = axis_rot((0, 1, 0), -g * lead(k, 0.6, 10))
    for (S, s), at in zip(SIDES, (0.3, 0.65)):                                       # 두 번 옮겨 딛는다
        step = max(0.0, 1 - abs(k - at) / 0.18)
        p[f"Thigh.{S}"] = axis_rot((1, 0, 0), -22 * smooth(step))
        p[f"Shin.{S}"] = axis_rot((1, 0, 0), 35 * smooth(step))
    return p, -0.01 * math.sin(k * math.pi)
make_action("TurnBack", list(range(0, 61, 2)), turn_sample)

rig.animation_data.action = bpy.data.actions["Idle"]
bpy.ops.wm.save_as_mainfile(filepath=out)

# 충격 순간 손 높이(땅 = -0.82)
rig.animation_data.action = bpy.data.actions["Strike"]
bpy.context.scene.frame_set(int(IMPACT * FPS))
for S in ("L", "R"):
    pb = rig.pose.bones[f"Hand.{S}"]
    print("impact hand", S, tuple(round(v, 3) for v in (rig.matrix_world @ pb.tail)))

if render_prefix:
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.samples = 12
    sc.render.resolution_x = 500; sc.render.resolution_y = 500
    body = bpy.data.objects["Witch_Body"]; mat = body.data.materials[0]; nt = mat.node_tree
    bsdf = [n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'][0]
    if tex and not any(n.type == 'TEX_IMAGE' for n in nt.nodes):
        img = nt.nodes.new('ShaderNodeTexImage'); img.image = bpy.data.images.load(tex); nt.links.new(img.outputs['Color'], bsdf.inputs['Base Color'])
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.7, 0.7, 0.75, 1)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2.6
    shots = [("Walk", 18, (3.4, -0.6, 0.2)), ("Walk", 54, (3.4, -0.6, 0.2)), ("TurnBack", 21, (0.8, 3.4, 0.4)), ("TurnBack", 42, (0.8, 3.4, 0.4)), ("Idle", 30, (0.8, -3.4, 0.3)), ("Strike", 18, (0.0, -3.4, 0.3)), ("Strike", 18, (3.4, 0.0, 0.3)), ("Strike", int(IMPACT * FPS), (3.4, -1.0, 0.0)), ("Strike", int(IMPACT * FPS), (1.2, -3.2, 0.6)), ("Rise", 0, (1.2, -3.2, 0.2))]
    for i, (act, f, pos) in enumerate(shots):
        rig.animation_data.action = bpy.data.actions[act]; sc.frame_set(f)
        cam.location = pos; cam.rotation_euler = (Vector((0, 0, -0.1)) - Vector(pos)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = f"{render_prefix}_{i}_{act}_{f}.png"; bpy.ops.render.render(write_still=True)
