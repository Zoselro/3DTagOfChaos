# Stage 5 check: can the player see the landmark with the Unity third-person camera?
# Camera_Ctrl: target = cookie + 1.5 m, distance 3.2, default pitch 25 deg down, pitch range -7..80 deg, vFOV 60.
# For cookie positions sampled along every walkable path, the camera turns towards the landmark (horizontal),
# then we ray-cast to the landmark's beacon points: visible = inside the vertical FOV and not occluded.
import bpy, math, random
from mathutils import Vector

LM = {  # landmark beacon points (x, y, z above landmark ground) and object-name keys that count as the landmark
    'CandyForest': ((0, 0), [10, 17, 30, 46, 60, 72], ('Landmark', 'RuneBand')),
    'GingerbreadVillage': ((0, 0), [10, 17.5, 25, 32, 40], ('Landmark', 'ClockTower')),
    'ChocolateFactory': ((0, 0), [12, 24, 36, 46, 52, 62, 74, 86, 93], ('Landmark', 'Tank', 'Pipe')),
    'CursedCandyCarnival': ((0, 136), [10, 20, 34, 50, 66, 80], ('Ferris', 'Landmark')),
    'HauntedBakery': ((0, 69), [5, 10, 16, 22], ('Landmark', 'MagicOven', 'Chimney')),
}
(lx, ly), zs, keys = LM[MAP.name]
bpy.context.window.scene = MAP.scene
dg = bpy.context.evaluated_depsgraph_get()
g0 = MAP.h_at(lx, ly)
targets = [Vector((lx, ly, g0 + z)) for z in zs]
rnd = random.Random(3)
samples = []
for a, b, hw in MAP.roads:
    L = (b - a).length
    for k in range(int(L / 10) + 1):
        p = a + (b - a) * min(1, (k * 10 + 5) / max(L, 1))
        samples.append(p)
for c, r, hw in MAP.rings:
    n = int(2 * math.pi * r / 10)
    for k in range(n):
        t = 2 * math.pi * k / n
        samples.append(c + Vector((r * math.cos(t), r * math.sin(t), 0)))
if MAP.name == 'HauntedBakery':       # the oven is an indoor landmark: test from the workroom floor
    samples = [Vector((x, y, 0)) for x in range(-46, 47, 8) for y in range(-46, 52, 8)]
samples = [p for p in samples if max(abs(p.x), abs(p.y)) < 170 and (p.xy - Vector((lx, ly))).length > 25]


def seen(p, pitch_deg):
    t = Vector((p.x, p.y, MAP.h_at(p.x, p.y) + 1.5))
    h = Vector((lx - p.x, ly - p.y, 0)).normalized()
    pr = math.radians(pitch_deg)
    fwd = Vector((h.x * math.cos(pr), h.y * math.cos(pr), -math.sin(pr)))
    cam = t - fwd * 3.2
    for q in targets:
        d = q - cam
        elev = math.degrees(math.atan2(d.z, d.xy.length)) + pitch_deg      # angle above the view centre
        if abs(elev) > 30:
            continue
        o = cam
        for _ in range(4):           # glass vault / thin roof ribs are see-through: continue the ray past them
            rest = q - o
            hit, loc, nrm, idx, ob, mx = MAP.scene.ray_cast(dg, o, rest.normalized(), distance=rest.length)
            if hit and any(k in ob.name for k in ('GlassVault', 'RoofArch', 'TieBeam', 'REF_')):
                o = loc + rest.normalized() * 0.05
                continue
            break
        if not hit or any(k in ob.name for k in keys) or (loc - q).length < 3:
            return True
    return False


res = {'default_25': 0, 'look_up_-7': 0}
fails = []
for p in samples:
    a = seen(p, 25)
    b = a or seen(p, -7)
    res['default_25'] += a
    res['look_up_-7'] += b
    if not b:
        fails.append((round(p.x), round(p.y)))
N = max(1, len(samples))
VIS = dict(samples=len(samples), default_pct=round(100 * res['default_25'] / N), lookup_pct=round(100 * res['look_up_-7'] / N),
           fails=fails[:12])
