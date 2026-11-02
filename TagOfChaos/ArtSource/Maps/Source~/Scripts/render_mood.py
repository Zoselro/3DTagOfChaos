# Lit mood renders (EEVEE) that follow the reference photos. Expects SCENE_NAME, OUT_DIR, MAP in namespace.
import bpy, os, math
from mathutils import Vector

MOOD = {
    #                 world colour, strength, sun colour, sun strength, mood camera (pos, target), hide roof, fog
    'CandyForest': ((1.0, 0.72, 0.84), 1.0, (1.0, 0.80, 0.88), 3.2, ((40, -69, 6), (0, 0, 20)), False,
                    ((1.0, 0.80, 0.88), 0.0005)),
    'GingerbreadVillage': ((0.015, 0.025, 0.08), 1.0, (0.55, 0.62, 1.0), 0.5, ((6, -122, 5), (0, -48, 7)), False,
                           ((0.2, 0.25, 0.5), 0.0003)),
    'ChocolateFactory': ((0.35, 0.20, 0.12), 0.9, (1.0, 0.72, 0.45), 2.2, ((6, -78, 7), (0, 0, 18)), False,
                         ((1.0, 0.7, 0.45), 0.0004)),
    'CursedCandyCarnival': ((0.008, 0.02, 0.03), 1.0, (0.35, 0.55, 0.7), 0.25, ((0, -205, 62), (0, 55, 12)), False,
                            ((0.2, 0.35, 0.4), 0.0003)),
    'HauntedBakery': ((0.04, 0.03, 0.05), 1.0, (0.7, 0.62, 0.9), 0.4, ((0, -44, 26), (0, 20, 2)), True, None),
}
sc = bpy.data.scenes[SCENE_NAME]
wc, ws, sunc, suns, (cpos, ctgt), hide_roof, fog = MOOD[SCENE_NAME]
fog = None   # EEVEE world volume blacks out the 350 m scene; haze is left to Unity fog (RenderSettings)
try:
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.look = 'AgX - Punchy'
except Exception:
    pass
for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE'):
    try:
        sc.render.engine = eng
        break
    except TypeError:
        continue
try:
    sc.eevee.taa_render_samples = 24
    sc.eevee.use_shadows = True
except Exception:
    pass
w = sc.world or bpy.data.worlds.new(f'{SCENE_NAME}_World')
sc.world = w
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
bg.inputs['Color'].default_value = (*wc, 1)
bg.inputs['Strength'].default_value = ws
# atmospheric haze (reference photos all have depth fog / glow)
nt = w.node_tree
out = next(n for n in nt.nodes if n.type == 'OUTPUT_WORLD')
vol = next((n for n in nt.nodes if n.type == 'VOLUME_SCATTER'), None)
if fog:
    if vol is None:
        vol = nt.nodes.new('ShaderNodeVolumeScatter')
    vol.inputs['Color'].default_value = (*fog[0], 1)
    vol.inputs['Density'].default_value = fog[1]
    nt.links.new(vol.outputs['Volume'], out.inputs['Volume'])
    try:
        sc.eevee.volumetric_end = 450
        sc.eevee.volumetric_tile_size = '8'
    except Exception:
        pass
elif vol is not None:
    nt.nodes.remove(vol)
refs = [o for o in MAP.ref.objects if o.type == 'MESH']
for o in refs:
    o.hide_render = True
if 'unity_lighting' not in sc:          # stage-5 light_setup owns the sun once it has run
    for o in MAP.c['Lighting'].objects:
        if o.type == 'LIGHT' and o.data.type == 'SUN':
            o.data.color = sunc
            o.data.energy = suns
cam = bpy.data.objects.get(f'{SCENE_NAME}_MoodCam')
if cam is None:
    cam = bpy.data.objects.new(f'{SCENE_NAME}_MoodCam', bpy.data.cameras.new(f'{SCENE_NAME}_MoodCam'))
    MAP.ref.objects.link(cam)
cam.data.lens = 26
cam.data.clip_end = 3000
p = Vector(cpos)
p.z += MAP.h_at(p.x, p.y)
t = Vector(ctgt)
cam.location = p
cam.rotation_mode = 'QUATERNION'
cam.rotation_quaternion = (t - p).to_track_quat('-Z', 'Y')
roof = [o for o in sc.objects if o.get('hide_top')]
for o in roof:
    o.hide_render = hide_roof
sc.render.resolution_x, sc.render.resolution_y = 1600, 900
written = []
for cam_ob in [cam] + [o for o in sc.objects if o.type == 'CAMERA' and 'GameCam' in o.name][:1]:
    sc.camera = cam_ob
    tag = 'Mood' if cam_ob is cam else 'MoodGameCam'
    sc.render.filepath = os.path.join(OUT_DIR, f'{SCENE_NAME}_{tag}.png')
    bpy.ops.render.render(write_still=True, scene=sc.name)
    written.append(sc.render.filepath)
for o in roof:
    o.hide_render = False
for o in refs:
    o.hide_render = False
sc.render.engine = 'BLENDER_WORKBENCH'
RESULT_MOOD = written
