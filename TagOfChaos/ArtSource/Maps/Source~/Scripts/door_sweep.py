# MonsterPlayer passage sweep through every registered doorway (run after a map build; needs MAP).
# Rays run along the door normal from 7 m outside to 7 m inside, at lateral offsets -2.5/0/+2.5 m
# (body/skirt ~3.1 m wide) and heights 0.8 / 2.7 (arms) / 5.0 m (head). Any hit = blocked lane.
import bpy
from mathutils import Vector

bpy.context.window.scene = MAP.scene
dg = bpy.context.evaluated_depsgraph_get()
IGNORE = ('Terrain_', 'COL_Boundary', 'REF_', 'Water_', 'FloorPlanks', 'StarRug', 'GlassVault')
blocked = []
for d in MAP.doors:
    n = Vector((d['nx'], d['ny'], 0)).normalized()
    side = Vector((-n.y, n.x, 0))
    c = Vector((d['x'], d['y'], 0))
    g = MAP.h_at(d['x'], d['y'])
    for off in (-2.5, 0.0, 2.5):
        if abs(off) * 2 > d['width'] - 0.5:
            continue
        for z in (0.8, 2.7, 5.0):
            if z > d['height'] - 0.3:
                continue
            a = c + side * off - n * 7 + Vector((0, 0, g + z))
            hit, loc, nrm, idx, ob, mx = MAP.scene.ray_cast(dg, a, n, distance=14)
            if hit and not any(k in ob.name for k in IGNORE):
                blocked.append((d['wall'], d['x'], d['y'], off, z, ob.name.split('_', 1)[1][:30], round((loc - a).length, 1)))
                break
SWEEP = dict(doors=len(MAP.doors), blocked=blocked)
