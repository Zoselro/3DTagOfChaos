# Render blockout views of a map scene (Workbench, fast).
# Expects: SCENE_NAME, OUT_DIR, MAP (maplib.Map) in namespace.
import bpy, os, math
from mathutils import Vector

sc = bpy.data.scenes[SCENE_NAME]
sc.render.engine = 'BLENDER_WORKBENCH'
sh = sc.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_shadows = True
sh.show_cavity = True
sc.display.render_aa = '8'
sc.render.image_settings.file_format = 'PNG'
if sc.world is None:
    sc.world = bpy.data.worlds.new(f'{SCENE_NAME}_World')
sc.world.color = (0.05, 0.05, 0.09)
roof = [o for o in sc.objects if o.get('hide_top')]
written = []
for ob in [o for o in sc.objects if o.type == 'CAMERA' and ('TopCam' in o.name or 'GameCam' in o.name)]:
    top = 'TopCam' in ob.name
    sc.camera = ob
    sc.render.resolution_x, sc.render.resolution_y = (1400, 1400) if top else (1600, 900)
    for o in roof:
        o.hide_render = top
    tag = ob.name.split('_', 1)[1]
    sc.render.filepath = os.path.join(OUT_DIR, f'{SCENE_NAME}_{tag}.png')
    bpy.ops.render.render(write_still=True, scene=sc.name)
    written.append(sc.render.filepath)
for o in roof:
    o.hide_render = False

# ---- terrain-only relief view (walkable terrain + water + decor rim) ----
keep = ('_Ground', '_Terrain', '_Background')
hidden = []


def find_lc(lc, col):
    if lc.collection == col:
        return lc
    for c in lc.children:
        r = find_lc(c, col)
        if r:
            return r


for cat, col in MAP.c.items():
    lc = find_lc(sc.view_layers[0].layer_collection, col)
    if not col.name.endswith(keep):
        lc.exclude = True
        hidden.append(lc)
bg_hidden = [o for o in MAP.c['Background'].objects if 'Terrain_Decor' not in o.name]
for o in bg_hidden:
    o.hide_render = True
ter_hidden = [o for o in MAP.c['Terrain'].objects if 'Water' not in o.name]
for o in ter_hidden:
    o.hide_render = True
rlc = [c for c in sc.view_layers[0].layer_collection.children if c.collection == MAP.ref][0]
rlc.exclude = True
cam = bpy.data.objects.get(f'{SCENE_NAME}_ReliefCam')
if cam is None:
    cam = bpy.data.objects.new(f'{SCENE_NAME}_ReliefCam', bpy.data.cameras.new(f'{SCENE_NAME}_ReliefCam'))
    MAP.ref.objects.link(cam)
cam.data.lens = 30
cam.data.clip_end = 3000
pos, tgt = Vector((-215, -265, 105)), Vector((0, -10, 0))
# inspection only: exaggerate heights x3 so 2-8 m hills/bowls read on a 350 m map (restored below)
exag = [o for o in list(MAP.c['Ground'].objects) + list(MAP.c['Terrain'].objects) + list(MAP.c['Background'].objects)
        if 'Terrain_' in o.name or 'Water' in o.name]
for o in exag:
    o.scale.z *= 3.0
    o.location.z *= 3.0
cam.location = pos
cam.rotation_mode = 'QUATERNION'
cam.rotation_quaternion = (tgt - pos).to_track_quat('-Z', 'Y')
sc.camera = cam
sc.render.resolution_x, sc.render.resolution_y = 1600, 1000
old_light = sh.light
sh.light = 'FLAT'
sh.light = 'STUDIO'
sc.render.filepath = os.path.join(OUT_DIR, f'{SCENE_NAME}_Terrain_Relief_x3.png')
bpy.ops.render.render(write_still=True, scene=sc.name)
written.append(sc.render.filepath)
for o in exag:
    o.scale.z /= 3.0
    o.location.z /= 3.0
for lc in hidden:
    lc.exclude = False
rlc.exclude = False
for o in bg_hidden + ter_hidden:
    o.hide_render = False
written.append(MAP.height_map_png(os.path.join(OUT_DIR, f'{SCENE_NAME}_Terrain_HeightMap.png')))
RESULT = written
