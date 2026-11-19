# ===== §89.22 베이커리 건물 7채 내부 소품(콘셉트 시트: 과일 상자·게시판·잼 병·딸기 상자·황동 기계·짤주머니·거품기 + 공장별 기계) =====
# 모두 바닥 가운데가 원점, 앞 = -Y. 재질은 Unity에 이미 있는 M_HQ_* 이름만 쓴다.
from lib import *
from lib import _new, _parts, PAL
import random, bmesh
from mathutils import noise
from objs_v2 import tube, leaf_blade, blob, smooth_parts, apple_fruit
from hq_props import last
from hq_misc import plank_box
from hq_theme import almond_shape, wheat_ear, sack_body
V = Vector

def strawberry_s(p, s, rng, d=(0, 0, 1)):
    """작은 딸기(상자·병 장식용): 원뿔 몸 + 꼭지 잎 4장"""
    d = V(d).normalized(); q = d.to_track_quat('Z', 'Y')
    o = lathe([(0, 0), (s * 0.35, s * 0.25), (s * 0.5, s * 0.6), (s * 0.45, s * 0.85), (s * 0.2, s * 0.98), (0, s)], 'Berry', seg=14)
    o.rotation_euler = q.to_euler(); o.location = p; smooth_parts(o)
    for k in range(4):
        a = k * math.pi / 2 + rng.uniform(-0.2, 0.2)
        side = (q @ V((math.cos(a), math.sin(a), 0)))
        leaf_blade(V(p) + d * s * 0.98, side + d * 0.2, s * 0.42, s * 0.16, 'Leaf', curl=0.5, thick=s * 0.03, seg=6)

def heap(cx, cy, z, w, d, n, rng, kinds, r=(0.08, 0.11), dome=0.16):
    for k in range(n):
        x = rng.uniform(-w / 2, w / 2); y = rng.uniform(-d / 2, d / 2)
        zz = z + dome * (1 - abs(x) / (w / 2 + 1e-6)) * (1 - abs(y) / (d / 2 + 1e-6)) + rng.uniform(0, 0.04)
        m = rng.choice(kinds)
        if m == 'Straw': strawberry_s((cx + x, cy + y, zz - 0.04), rng.uniform(*r) * 1.4, rng, d=(rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0.4))
        else: apple_fruit(rng.uniform(*r), (cx + x, cy + y, zz), m)

# ---------------------------------------------------------------- 과일 창고
def hq_fruit_crate(fill=('Apple', 'Orange', 'Apple', 'Berry'), seed=4):
    """낮은 나무 상자 가득 과일(사과·오렌지) — 쌓아 둘 수 있는 크기 0.9 × 0.65 × 0.5"""
    rng = random.Random(seed)
    plank_box(0.9, 0.65, 0.42, (0, 0, 0), rng=rng)
    box(0.84, 0.6, 0.03, 'PlankDark', loc=(0, 0, 0.03))
    for sx in (-1, 1): box(0.04, 0.22, 0.06, 'PlankDark', loc=(sx * 0.47, 0, 0.33), bevel=0.01)    # 손잡이 턱
    heap(0, 0, 0.42, 0.74, 0.5, 26, rng, fill, dome=0.1)

def hq_strawberry_crate():
    """딸기 상자: 분홍 줄무늬 천을 깐 얕은 상자 + 딸기 더미 + 앞면 딸기 그림 판"""
    rng = random.Random(8)
    plank_box(0.8, 0.55, 0.3, (0, 0, 0), rng=rng)
    box(0.76, 0.5, 0.02, 'Stripe', loc=(0, 0, 0.27))
    heap(0, 0, 0.3, 0.66, 0.42, 30, rng, ('Straw',), r=(0.05, 0.065), dome=0.08)
    box(0.3, 0.02, 0.18, 'Cream', loc=(0, -0.29, 0.15), bevel=0.01)
    strawberry_s((0, -0.305, 0.1), 0.12, rng, d=(0, -0.2, 1))

def hq_notice_board():
    """게시판: 두 다리 나무 틀 + 작은 지붕 + 핀으로 꽂은 종이 6장 + 과일 그림"""
    rng = random.Random(5)
    for x in (-0.75, 0.75): box(0.1, 0.1, 2.0, 'PlankDark', loc=(x, 0, 1.0), bevel=0.015)
    box(1.5, 0.06, 1.0, 'Plank', loc=(0, 0, 1.3), bevel=0.01)                                   # 판
    for z in (0.78, 1.82): box(1.62, 0.1, 0.08, 'PlankDark', loc=(0, -0.01, z), bevel=0.012)     # 위아래 테
    for sgn in (-1, 1): box(0.95, 0.5, 0.05, 'RedTile', loc=(sgn * 0.42, 0, 2.08), rot=(0, sgn * 0.42, 0), bevel=0.01)   # 작은 지붕
    box(0.08, 0.5, 0.08, 'Gold', loc=(0, 0, 2.27), bevel=0.01)
    papers = [(-0.48, 1.5, 0.34, 0.42), (-0.05, 1.55, 0.32, 0.36), (0.42, 1.48, 0.36, 0.44), (-0.4, 1.05, 0.38, 0.3), (0.08, 1.06, 0.3, 0.34), (0.5, 1.02, 0.26, 0.3)]
    for i, (x, z, w, h) in enumerate(papers):
        box(w, 0.008, h, ('Canvas', 'Cream', 'White')[i % 3], loc=(x, -0.036, z), rot=(0, rng.uniform(-0.08, 0.08), 0))
        smooth_parts(uvs(0.018, ('Berry', 'Gold')[i % 2], loc=(x, -0.045, z + h / 2 - 0.04), seg=8, rings=5))
        for k in range(3): box(w * 0.7, 0.004, 0.015, 'PlankDark', loc=(x, -0.042, z + h * 0.15 - k * h * 0.15))   # 글줄
    apple_fruit(0.07, (-0.05, -0.06, 1.62), 'Apple'); apple_fruit(0.06, (0.42, -0.06, 1.56), 'Orange')
    for x in (-0.75, 0.75): box(0.34, 0.34, 0.06, 'PlankDark', loc=(x, 0, 0.03), bevel=0.01)      # 받침

# ---------------------------------------------------------------- 딸기잼 공장
def jam_jar(p, s=1.0, rng=None):
    rng = rng or random.Random(2); x, y, z = p
    smooth_parts(lathe([(0, 0), (0.16 * s, 0), (0.19 * s, 0.03 * s), (0.2 * s, 0.3 * s), (0.17 * s, 0.36 * s), (0.15 * s, 0.4 * s), (0.155 * s, 0.44 * s), (0, 0.44 * s)], 'Pane', loc=(x, y, z), seg=24))
    smooth_parts(lathe([(0, 0.02 * s), (0.18 * s, 0.02 * s), (0.185 * s, 0.31 * s), (0.15 * s, 0.35 * s), (0, 0.35 * s)], 'BerryDeep', loc=(x, y, z), seg=24))   # 잼(유리 안)
    smooth_parts(lathe([(0, 0.44 * s), (0.2 * s, 0.44 * s), (0.24 * s, 0.4 * s), (0.25 * s, 0.37 * s), (0.2 * s, 0.47 * s), (0, 0.48 * s)], 'Stripe', loc=(x, y, z), seg=24))  # 천 뚜껑
    torus(0.158 * s, 0.012 * s, 'Gold', loc=(x, y, z + 0.405 * s), seg=24, rseg=5)
    box(0.2 * s, 0.01 * s, 0.12 * s, 'Cream', loc=(x, y - 0.196 * s, z + 0.17 * s), bevel=0.004 * s)   # 이름표
    strawberry_s((x, y - 0.205 * s, z + 0.14 * s), 0.06 * s, rng, d=(0, -0.3, 1))

def hq_jam_jar():
    jam_jar((0, 0, 0), 1.4)

def hq_jam_shelf():
    """잼 병 선반: 3단 나무 선반 2 m에 잼 병 14개"""
    rng = random.Random(9)
    for x in (-0.95, 0.95): box(0.08, 0.45, 2.0, 'PlankDark', loc=(x, 0, 1.0), bevel=0.012)
    box(1.98, 0.04, 2.0, 'Plank', loc=(0, 0.21, 1.0))                                             # 뒤판
    for z in (0.12, 0.78, 1.44, 1.98): box(1.9, 0.44, 0.05, 'Plank', loc=(0, 0, z), bevel=0.01)
    for z in (0.145, 0.805, 1.465):
        n = 5 if z < 1 else 4
        for k in range(n):
            jam_jar((-0.72 + 1.44 * k / (n - 1) + rng.uniform(-0.04, 0.04), rng.uniform(-0.05, 0.03), z), rng.uniform(0.95, 1.15), rng)

def hq_brass_boiler():
    """황동 잼 솥 기계: 다리 셋 위 둥근 솥 + 둥근 지붕 뚜껑·압력계·바퀴 밸브·내려가는 관·김"""
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        tube([V((math.cos(a) * 0.55, math.sin(a) * 0.55, 0)), V((math.cos(a) * 0.42, math.sin(a) * 0.42, 0.55))], [0.06, 0.05], 'Iron', seg=8)
    prof = [(0, 0.45), (0.45, 0.45), (0.62, 0.6), (0.68, 0.95), (0.66, 1.35), (0.6, 1.45), (0.0, 1.45)]
    smooth_parts(lathe(prof, 'Gold', seg=40))
    for z in (0.75, 1.15): torus(0.69, 0.025, 'Iron', loc=(0, 0, z), seg=40, rseg=6)
    smooth_parts(lathe([(0, 1.45), (0.55, 1.45), (0.48, 1.7), (0.3, 1.85), (0.08, 1.9), (0.08, 2.1), (0.14, 2.14), (0, 2.16)], 'Gold', seg=32))
    for k in range(12):                                                            # 리벳
        a = 2 * math.pi * k / 12
        smooth_parts(uvs(0.025, 'Iron', loc=(math.cos(a) * 0.69, math.sin(a) * 0.69, 0.95), seg=8, rings=5))
    # 압력계
    g = V((0.0, -0.66, 1.25))
    cyl(0.16, 0.06, 'Gold', loc=g, rot=(math.pi / 2, 0, 0), seg=24); cyl(0.13, 0.065, 'White', loc=g + V((0, -0.005, 0)), rot=(math.pi / 2, 0, 0), seg=24)
    box(0.012, 0.01, 0.1, 'Iron', loc=g + V((0.02, -0.04, 0.02)), rot=(0, 0.6, 0))
    # 앞쪽 수도꼭지 + 바퀴 밸브
    tube([V((0, -0.62, 0.75)), V((0, -0.9, 0.75)), V((0, -0.95, 0.6))], [0.05, 0.05, 0.045], 'Gold', seg=10)
    t = V((0.0, -0.92, 0.92)); torus(0.12, 0.018, 'Berry', loc=t, rot=(0, 0, 0), seg=24, rseg=6)
    for k in range(4):
        a = math.pi * k / 2; tube([t, t + V((math.cos(a) * 0.12, math.sin(a) * 0.12, 0))], [0.012, 0.012], 'Berry', seg=5)
    tube([t + V((0, 0, -0.17)), t], [0.02, 0.02], 'Gold', seg=6)
    smooth_parts(uvs(0.07, 'BerryDeep', loc=(0, -0.96, 0.48), scl=(1, 1, 1.4), seg=10, rings=6))   # 떨어지는 잼 방울
    # 위로 올라가 옆으로 꺾이는 굵은 관
    tube([V((0.3, 0.3, 1.8)), V((0.45, 0.45, 2.4)), V((0.75, 0.75, 2.55)), V((1.0, 1.0, 2.2)), V((1.0, 1.0, 0.0))], [0.09, 0.09, 0.09, 0.09, 0.09], 'Gold', seg=14)
    for z in (0.4, 1.2, 2.0): torus(0.11, 0.025, 'Iron', loc=(1.0, 1.0, z), seg=20, rseg=5)
    box(0.5, 0.5, 0.08, 'Iron', loc=(1.0, 1.0, 0.04), bevel=0.01)
    jam_jar((-0.25, -1.1, 0), 1.0); jam_jar((0.25, -1.15, 0), 0.9)

# ---------------------------------------------------------------- 디저트 공방(사과·오렌지)
def hq_piping_bag():
    """짤주머니(탁자 위 0.45 m): 천 원뿔 + 금 깍지 + 위가 꼬인 입구 + 끝에 짜낸 크림"""
    prof = [(0.0, 0.0), (0.025, 0.0), (0.03, 0.05), (0.09, 0.18), (0.13, 0.3), (0.12, 0.36), (0.06, 0.4), (0.03, 0.44), (0.0, 0.45)]
    b = lathe(prof, 'Canvas', seg=24); smooth_parts(b)
    for v in b.data.vertices:
        a = math.atan2(v.co.y, v.co.x); v.co.x *= 1 + 0.06 * math.sin(a * 5 + v.co.z * 20); v.co.y *= 1 + 0.06 * math.sin(a * 5 + v.co.z * 20)
    lathe([(0, -0.07), (0.008, -0.07), (0.03, 0.0), (0.0, 0.0)], 'Gold', seg=8)                    # 별 깍지(8각)
    tube([V((0, 0, 0.44)), V((0.02, 0.01, 0.5)), V((0.05, 0.0, 0.53))], [0.03, 0.022, 0.015], 'Canvas', seg=8)
    smooth_parts(lathe([(0, -0.07), (0.05, -0.085), (0.04, -0.1), (0.0, -0.12)], 'Frosting', seg=12))
    for o in list(_parts):                                                          # 비스듬히 눕힘(탁자 위)
        o.rotation_euler = (0, 1.25, 0.3); o.location = (0, 0, 0.13)

def hq_whisk():
    """거품기: 나무 손잡이 + 철사 고리 8개"""
    for k in range(8):
        a = math.pi * k / 8; pts = []
        for i in range(13):
            t = math.pi * i / 12; r = 0.07 * math.sin(t)
            pts.append(V((math.cos(a) * r, math.sin(a) * r, -0.04 - 0.22 * (1 - math.cos(t)) / 2 * 1.0 - 0.0)))
        tube(pts, [0.004] * len(pts), 'Iron', seg=5)
    smooth_parts(lathe([(0, 0.0), (0.02, 0.0), (0.024, 0.06), (0.02, 0.22), (0.026, 0.25), (0, 0.26)], 'Wood', seg=12))
    torus(0.021, 0.006, 'Gold', loc=(0, 0, 0.0), seg=12, rseg=4)
    for o in list(_parts): o.rotation_euler = (0, 1.48, 0.6); o.location = (0, 0, 0.035)

def hq_work_table():
    """작업대 2.0 × 0.9: 두꺼운 상판(밀가루 묻음) + 아래 선반에 그릇 셋"""
    box(2.0, 0.9, 0.1, 'Plank', loc=(0, 0, 0.9), bevel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1): box(0.1, 0.1, 0.86, 'PlankDark', loc=(sx * 0.88, sy * 0.36, 0.43), bevel=0.015)
    box(1.8, 0.75, 0.04, 'Plank', loc=(0, 0, 0.22), bevel=0.01)
    smooth_parts(blob(0.32, 'White', (0.35, 0.05, 0.955), sub=2, lump=0.2, seed=3, scl=(1.2, 0.9, 0.04)))      # 밀가루 자국
    for k, x in enumerate((-0.6, 0.0, 0.6)):
        smooth_parts(lathe([(0, 0.24), (0.12, 0.24), (0.2, 0.3), (0.22, 0.38), (0.2, 0.38), (0.18, 0.31), (0, 0.27)], ('Cream', 'WrapperPink', 'White')[k], loc=(x, 0, 0), seg=24))

def hq_oven():
    """둥근 벽돌 화덕: 돌 받침 + 반구 지붕 + 빛나는 아궁이 + 굴뚝"""
    box(2.2, 1.8, 0.9, 'StoneGrey', loc=(0, 0, 0.45), bevel=0.04)
    o = uvs(1.0, 'PinkStone', loc=(0, 0.1, 0.9), scl=(1.0, 0.85, 0.95), seg=32, rings=16)
    me = o.data
    for v in me.vertices:
        if v.co.z < 0: v.co.z = 0
    smooth_parts(o)
    for k in range(9):                                                              # 아궁이 둘레 벽돌 아치
        a = math.pi * k / 8
        box(0.18, 0.22, 0.12, 'RedTile', loc=(math.cos(a) * 0.48, -0.78, 0.92 + math.sin(a) * 0.48), rot=(0, -a + math.pi / 2, 0), bevel=0.015)
    cyl(0.42, 0.12, 'ChocoDark', loc=(0, -0.74, 1.05), rot=(math.pi / 2, 0, 0), seg=24)                         # 어두운 아궁이 속
    smooth_parts(blob(0.22, 'Glow', (0, -0.72, 1.0), sub=2, lump=0.25, seed=2, scl=(1.3, 0.6, 0.7)))            # 불빛
    cyl(0.16, 1.0, 'StoneGrey', loc=(0.4, 0.45, 2.0), seg=16); cyl(0.2, 0.1, 'Iron', loc=(0.4, 0.45, 2.5), seg=16)
    box(1.0, 0.1, 0.06, 'Iron', loc=(0, -0.92, 0.55), bevel=0.01)                                              # 앞 손잡이 막대
    tube([V((0.9, -0.85, 0.2)), V((1.2, -1.0, 1.5))], [0.025, 0.025], 'Wood', seg=6)                            # 화덕 삽
    box(0.3, 0.02, 0.36, 'Wood', loc=(1.22, -1.02, 1.65), rot=(0.1, 0.2, 0), bevel=0.01)

def hq_cake_stand():
    """케이크 진열대: 금 3단 받침 + 층마다 케이크·컵케이크"""
    for z, r in ((0.0, 0.45), (0.5, 0.36), (0.95, 0.27)):
        smooth_parts(lathe([(0, z + 0.02), (r, z + 0.02), (r + 0.02, z + 0.04), (r, z + 0.05), (0, z + 0.05)], 'Gold', seg=32))
    tube([V((0, 0, 0.02)), V((0, 0, 1.3))], [0.025, 0.025], 'Gold', seg=8)
    smooth_parts(lathe([(0, 0.05), (0.3, 0.05), (0.3, 0.3), (0.28, 0.33), (0, 0.33)], 'CakeSponge', seg=32))
    smooth_parts(lathe([(0, 0.3), (0.31, 0.3), (0.32, 0.34), (0, 0.36)], 'Frosting', seg=32))
    rng = random.Random(4)
    for k in range(8):
        a = 2 * math.pi * k / 8; strawberry_s((math.cos(a) * 0.24, math.sin(a) * 0.24, 0.34), 0.07, rng)
    for k in range(5):
        a = 2 * math.pi * k / 5
        p = (math.cos(a) * 0.25, math.sin(a) * 0.25, 0.55)
        smooth_parts(lathe([(0, 0), (0.06, 0), (0.075, 0.08), (0, 0.08)], 'WrapperPink', loc=p, seg=12))
        smooth_parts(lathe([(0, 0.07), (0.08, 0.08), (0.06, 0.12), (0.03, 0.15), (0, 0.17)], ('Frosting', 'Icing', 'Choco')[k % 3], loc=p, seg=12))
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4; apple_fruit(0.06, (math.cos(a) * 0.15, math.sin(a) * 0.15, 1.04), ('Apple', 'Orange')[k % 2])

def hq_apple_press():
    """사과 압착기: 나무 틀 + 금 나사 기둥 + 둥근 손잡이 + 통(사과) + 주스 받는 그릇"""
    rng = random.Random(6)
    for x in (-0.55, 0.55): box(0.14, 0.14, 1.9, 'PlankDark', loc=(x, 0, 0.95), bevel=0.02)
    box(1.3, 0.24, 0.2, 'PlankDark', loc=(0, 0, 1.8), bevel=0.02); box(1.3, 0.7, 0.14, 'Plank', loc=(0, 0, 0.07), bevel=0.02)
    pts = [V((0.07 * math.cos(t * 9), 0.07 * math.sin(t * 9), 1.0 + t * 1.05)) for t in [i / 40 for i in range(41)]]
    tube([V((0, 0, 1.0)), V((0, 0, 2.05))], [0.05, 0.05], 'Gold', seg=10); tube(pts, [0.02] * 41, 'Gold', seg=5)   # 나사 기둥 + 나사산
    torus(0.32, 0.025, 'Wood', loc=(0, 0, 2.1), seg=24, rseg=6)
    for k in range(4): a = math.pi * k / 2; tube([V((0, 0, 2.1)), V((math.cos(a) * 0.32, math.sin(a) * 0.32, 2.1))], [0.02, 0.02], 'Wood', seg=5)
    cyl(0.36, 0.06, 'Plank', loc=(0, 0, 0.98), seg=24)                                                          # 누름판
    for k in range(14):
        a = 2 * math.pi * k / 14; box(0.06, 0.03, 0.75, 'Plank', loc=(math.cos(a) * 0.4, math.sin(a) * 0.4, 0.52), rot=(0, 0, a))
    for z in (0.3, 0.75): torus(0.42, 0.018, 'Iron', loc=(0, 0, z), seg=28, rseg=5)
    heap(0, 0, 0.85, 0.5, 0.5, 9, rng, ('Apple',), r=(0.08, 0.1), dome=0.05)
    tube([V((0, -0.4, 0.16)), V((0, -0.62, 0.14))], [0.03, 0.025], 'Gold', seg=8)
    smooth_parts(lathe([(0, 0), (0.18, 0), (0.22, 0.14), (0.2, 0.14), (0.16, 0.03), (0, 0.03)], 'Plank', loc=(0, -0.75, 0), seg=20))
    smooth_parts(lathe([(0, 0.03), (0.17, 0.03), (0.19, 0.1), (0, 0.1)], 'Orange', loc=(0, -0.75, 0), seg=20))

def hq_orange_juicer():
    """오렌지 즙 짜는 기계: 금 받침 + 반 자른 오렌지 + 레버 + 유리 주스 병 2"""
    smooth_parts(lathe([(0, 0), (0.35, 0), (0.38, 0.06), (0.2, 0.12), (0.12, 0.6), (0.18, 0.66), (0, 0.66)], 'Gold', seg=28))
    smooth_parts(lathe([(0, 0.66), (0.3, 0.66), (0.33, 0.72), (0.3, 0.74), (0, 0.72)], 'White', seg=28))
    smooth_parts(lathe([(0, 0.72), (0.16, 0.72), (0.2, 0.8), (0, 0.84)], 'OrangeCut', seg=20))
    tube([V((0.25, 0.0, 0.66)), V((0.3, 0.0, 1.1)), V((0.0, 0.0, 1.2)), V((-0.35, 0.0, 1.0))], [0.03, 0.03, 0.03, 0.03], 'Gold', seg=8)
    smooth_parts(uvs(0.05, 'Berry', loc=(-0.37, 0, 0.99), seg=10, rings=6))
    for k, x in enumerate((-0.55, 0.55)):
        smooth_parts(lathe([(0, 0), (0.09, 0), (0.1, 0.22), (0.07, 0.28), (0.07, 0.32), (0, 0.32)], 'Pane', loc=(x, -0.15, 0), seg=16))
        smooth_parts(lathe([(0, 0.01), (0.085, 0.01), (0.09, 0.18), (0, 0.18)], 'OrangeFlesh', loc=(x, -0.15, 0), seg=16))
    for k in range(4):
        a = 1.2 * k; lathe([(0, 0), (0.09, 0.0), (0.1, 0.05), (0, 0.06)], 'OrangeCut', loc=(0.2 + 0.1 * math.cos(a), -0.35 + 0.08 * math.sin(a), 0), seg=16)

# ---------------------------------------------------------------- 밀 공장
def hq_millstone():
    """맷돌: 나무 틀 위 두 겹 돌 바퀴 + 나무 곡물 깔때기 + 밀가루 받는 자루"""
    rng = random.Random(3)
    box(1.8, 1.8, 0.5, 'Plank', loc=(0, 0, 0.25), bevel=0.03)
    for sx in (-1, 1):
        for sy in (-1, 1): box(0.14, 0.14, 0.55, 'PlankDark', loc=(sx * 0.82, sy * 0.82, 0.27), bevel=0.02)
    for z, h in ((0.5, 0.25), (0.75, 0.28)):
        o = cyl(0.75, h, 'StoneGrey', loc=(0, 0, z + h / 2), seg=40); smooth_parts(o)
        for v in o.data.vertices: v.co += v.co.normalized() * 0.01 * noise.noise(v.co * 6)
    for k in range(8):                                                              # 돌 홈
        a = math.pi * k / 4; box(0.6, 0.03, 0.02, 'RockDark', loc=(math.cos(a) * 0.38, math.sin(a) * 0.38, 1.035), rot=(0, 0, a))
    cyl(0.1, 0.4, 'Wood', loc=(0, 0, 1.2), seg=12)
    smooth_parts(lathe([(0.08, 1.35), (0.45, 1.85), (0.5, 1.9), (0.42, 1.9), (0.06, 1.42)], 'Plank', seg=8, closed=True))   # 깔때기(8각)
    smooth_parts(blob(0.38, 'Seed', (0, 0, 1.78), sub=2, lump=0.15, seed=4, scl=(1.0, 1.0, 0.3)))
    for sx in (-1, 1): box(0.1, 0.1, 1.5, 'PlankDark', loc=(sx * 0.55, 0, 1.5), bevel=0.015)
    box(1.2, 0.1, 0.1, 'PlankDark', loc=(0, 0, 2.2), bevel=0.015)
    tube([V((0.78, 0.0, 0.62)), V((1.1, 0.0, 0.5))], [0.06, 0.05], 'Plank', seg=8)                              # 밀가루 내려오는 홈통
    sack_body(0.7, 0.3, 'Sack', seed=5, open_top=0.6);
    for o in _parts[-3:]:
        if o.location.length < 1e-6: o.location = (1.3, 0, 0)
    smooth_parts(blob(0.2, 'White', (1.3, 0, 0.68), sub=2, lump=0.2, seed=6, scl=(1, 1, 0.5)))

def hq_grain_bin():
    """곡물 통: 큰 나무 통 + 밀 이삭 가득 + 금 띠"""
    rng = random.Random(7)
    smooth_parts(lathe([(0.62, 0), (0.7, 0.5), (0.7, 1.0), (0.66, 1.1), (0.6, 1.1), (0.64, 1.0), (0.64, 0.5), (0.56, 0.04), (0, 0.04), (0, 0)], 'Plank', seg=32))
    for z in (0.2, 0.6, 1.0): torus(0.7, 0.025, 'Gold', loc=(0, 0, z), seg=40, rseg=5)
    smooth_parts(blob(0.6, 'Seed', (0, 0, 1.0), sub=3, lump=0.1, seed=2, scl=(1.0, 1.0, 0.25)))
    for k in range(22):
        a = rng.uniform(0, 6.28); r = rng.uniform(0, 0.45)
        b = V((math.cos(a) * r, math.sin(a) * r, 1.0)); d = V((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 1)).normalized()
        tube([b, b + d * 0.5], [0.008, 0.006], 'WheatStem', seg=4); wheat_ear(b + d * 0.5, d, 0.16)

# ---------------------------------------------------------------- 아몬드 공장
def hq_almond_roaster():
    """아몬드 볶는 통: 무쇠 화덕 위 회전 구리 원통 + 손잡이 + 볶은 아몬드 받는 쟁반"""
    rng = random.Random(4)
    box(1.6, 1.0, 0.8, 'Iron', loc=(0, 0, 0.4), bevel=0.04)
    box(0.7, 0.04, 0.3, 'ChocoDark', loc=(0, -0.5, 0.35), bevel=0.02); smooth_parts(blob(0.15, 'Glow', (0, -0.47, 0.32), sub=2, lump=0.3, seed=3, scl=(1.6, 0.4, 0.8)))
    for sx in (-1, 1): box(0.12, 0.6, 0.6, 'Iron', loc=(sx * 0.78, 0, 1.05), bevel=0.02)
    o = cyl(0.48, 1.4, 'Gold', loc=(0, 0, 1.25), rot=(0, math.pi / 2, 0), seg=32); smooth_parts(o)
    for x in (-0.55, 0.0, 0.55): torus(0.49, 0.025, 'Iron', loc=(x, 0, 1.25), rot=(0, math.pi / 2, 0), seg=32, rseg=5)
    box(0.36, 0.02, 0.26, 'ChocoDark', loc=(0.2, -0.48, 1.3), bevel=0.01)                                       # 투입구
    tube([V((0.85, 0, 1.25)), V((1.05, 0, 1.25)), V((1.05, 0, 0.95)), V((1.15, 0, 0.95))], [0.035] * 4, 'Iron', seg=8)
    smooth_parts(uvs(0.06, 'Wood', loc=(1.18, 0, 0.95), seg=10, rings=6))
    cyl(0.12, 1.2, 'Iron', loc=(-0.5, 0.35, 1.6), seg=12)
    smooth_parts(lathe([(0, 0.0), (0.55, 0.0), (0.6, 0.08), (0.56, 0.08), (0.5, 0.03), (0, 0.03)], 'Gold', loc=(0, -1.0, 0.0), seg=32))
    for k in range(26):
        a = rng.uniform(0, 6.28); r = rng.uniform(0, 0.45)
        almond_shape(rng.uniform(0.09, 0.12), (math.cos(a) * r, -1.0 + math.sin(a) * r, 0.06 + rng.uniform(0, 0.04)), rot=(rng.uniform(1.2, 1.9), 0, rng.uniform(0, 6.28)))

def hq_almond_shelf():
    """아몬드 저장 선반: 2단 선반 + 유리병(아몬드) + 자루"""
    rng = random.Random(12)
    for x in (-0.95, 0.95): box(0.08, 0.5, 1.6, 'PlankDark', loc=(x, 0, 0.8), bevel=0.012)
    for z in (0.1, 0.8, 1.55): box(1.9, 0.48, 0.05, 'Plank', loc=(0, 0, z), bevel=0.01)
    for k in range(4):
        x = -0.65 + 0.43 * k
        smooth_parts(lathe([(0, 0), (0.15, 0), (0.17, 0.38), (0.12, 0.45), (0.12, 0.5), (0, 0.5)], 'Pane', loc=(x, 0, 0.825), seg=20))
        smooth_parts(lathe([(0, 0.02), (0.15, 0.02), (0.16, 0.3), (0, 0.32)], 'Almond', loc=(x, 0, 0.825), seg=20))
        cyl(0.13, 0.05, 'Gold', loc=(x, 0, 1.35), seg=16)
    for k, x in enumerate((-0.5, 0.45)):
        sack_body(0.55, 0.26, 'Burlap', seed=3 + k, open_top=0.6)
        for o in _parts[-3:]:
            if o.location.length < 1e-6: o.location = (x, 0, 0.125)

# ---------------------------------------------------------------- 초콜릿 공장
def hq_tempering_table():
    """초콜릿 굳히는 대리석 탁자: 흰 대리석 상판 + 녹은 초콜릿 웅덩이 + 틀 6칸 + 주걱"""
    box(2.0, 1.0, 0.12, 'White', loc=(0, 0, 0.92), bevel=0.02)
    for sx in (-1, 1):
        for sy in (-1, 1): box(0.12, 0.12, 0.86, 'Gold', loc=(sx * 0.86, sy * 0.4, 0.43), bevel=0.02)
    box(1.8, 0.8, 0.05, 'PlankDark', loc=(0, 0, 0.22), bevel=0.01)
    smooth_parts(blob(0.4, 'ChocoFlow', (-0.45, 0.05, 0.985), sub=3, lump=0.18, seed=5, scl=(1.2, 0.9, 0.05)))
    for k in range(6):
        x = 0.25 + 0.22 * (k % 3); y = -0.18 + 0.3 * (k // 3)
        box(0.18, 0.24, 0.05, 'WrapperPink', loc=(x, y, 1.0), bevel=0.01)
        box(0.14, 0.2, 0.03, ('Choco', 'ChocoDark', 'Icing')[k % 3], loc=(x, y, 1.02), bevel=0.008)
    tube([V((-0.2, -0.3, 1.0)), V((-0.05, -0.38, 1.02))], [0.012, 0.012], 'Wood', seg=5)
    box(0.12, 0.08, 0.01, 'Iron', loc=(-0.26, -0.27, 0.99), rot=(0, 0, 0.5))
    smooth_parts(lathe([(0, 0), (0.18, 0), (0.22, 0.2), (0, 0.18)], 'Gold', loc=(-0.75, -0.3, 0.98), seg=20))
    smooth_parts(lathe([(0, 0.15), (0.2, 0.15), (0, 0.17)], 'ChocoFlow', loc=(-0.75, -0.3, 0.98), seg=20))

def hq_cocoa_sacks():
    """카카오 자루 더미: 자루 3 + 쏟아진 카카오 콩"""
    rng = random.Random(14)
    for k, (x, y, s) in enumerate(((-0.35, 0, 1.0), (0.35, 0.05, 0.9), (0.0, 0.3, 0.95))):
        sack_body(0.75 * s, 0.32 * s, 'Burlap', seed=20 + k, open_top=0.55)
        for o in _parts[-3:]:
            if o.location.length < 1e-6: o.location = (x, y, 0)
    for k in range(18):
        a = rng.uniform(-1.2, 0.3); r = rng.uniform(0.35, 0.7)
        almond_shape(rng.uniform(0.07, 0.09), (math.cos(a) * r, -0.3 + math.sin(a) * r * 0.5, 0.03), rot=(1.57, 0, rng.uniform(0, 6.28)), m='ChocoDark')

# ---------------------------------------------------------------- 공통
def hq_stool():
    smooth_parts(lathe([(0, 0.6), (0.2, 0.6), (0.22, 0.64), (0.2, 0.68), (0, 0.68)], 'Plank', seg=20))
    for k in range(3):
        a = 2 * math.pi * k / 3; tube([V((math.cos(a) * 0.12, math.sin(a) * 0.12, 0.6)), V((math.cos(a) * 0.2, math.sin(a) * 0.2, 0.0))], [0.025, 0.025], 'PlankDark', seg=6)
    torus(0.15, 0.015, 'Gold', loc=(0, 0, 0.28), seg=20, rseg=4)

BAKERY = {"HOB_FruitCrateFull": hq_fruit_crate, "HOB_OrangeCrateFull": lambda: hq_fruit_crate(('Orange', 'Orange', 'OrangeCut'), 6), "HOB_StrawberryCrate": hq_strawberry_crate,
          "HOB_NoticeBoard": hq_notice_board, "HOB_JamJar": hq_jam_jar, "HOB_JamShelf": hq_jam_shelf, "HOB_BrassBoiler": hq_brass_boiler,
          "HOB_PipingBag": hq_piping_bag, "HOB_Whisk": hq_whisk, "HOB_WorkTable": hq_work_table, "HOB_BrickOven": hq_oven, "HOB_CakeStand": hq_cake_stand,
          "HOB_ApplePress": hq_apple_press, "HOB_OrangeJuicer": hq_orange_juicer, "HOB_Millstone": hq_millstone, "HOB_GrainBin": hq_grain_bin,
          "HOB_AlmondRoaster": hq_almond_roaster, "HOB_AlmondShelf": hq_almond_shelf, "HOB_TemperingTable": hq_tempering_table, "HOB_CocoaSacks": hq_cocoa_sacks,
          "HOB_Stool": hq_stool}
