# Rebuild map blockouts in the open Blender file, render views, save.
# Usage (Blender Python): exec(open(r'...\Scripts\build_all.py', encoding='utf-8').read(), {'MAPS': None, '__file__': path})
#   MAPS: list of map names or None (all); MOOD: True to add EEVEE mood renders; FRESH_ASSETS: rebuild asset meshes
import bpy, os, time, traceback

SCRIPTS = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else \
    r"F:\3DClaudeTagOfChaos\TagOfChaos\Assets\Maps\Source~\Scripts"
OUT = os.path.join(os.path.dirname(SCRIPTS), 'Renders', 'Blockout')
ALL = [('CandyForest', 'build_candyforest.py'), ('GingerbreadVillage', 'build_gingerbreadvillage.py'),
       ('ChocolateFactory', 'build_chocolatefactory.py'), ('CursedCandyCarnival', 'build_cursedcandycarnival.py'),
       ('HauntedBakery', 'build_hauntedbakery.py')]
want = globals().get('MAPS') or [n for n, _ in ALL]
if globals().get('FRESH_ASSETS'):
    for me in [x for x in bpy.data.meshes if x.name.startswith('A_')]:
        bpy.data.meshes.remove(me)
if globals().get('FRESH_MATS'):          # palette changed: rebuild every M_* (rebuild all maps in the same call)
    for mt in [x for x in bpy.data.materials if x.name.startswith('M_')]:
        bpy.data.materials.remove(mt)
REPORT = []
for name, f in ALL:
    if name not in want:
        continue
    t = time.time()
    try:
        ns = {}
        exec(open(os.path.join(SCRIPTS, 'maplib.py'), encoding='utf-8').read(), ns)
        exec(open(os.path.join(SCRIPTS, 'assets.py'), encoding='utf-8').read(), ns)
        exec(open(os.path.join(SCRIPTS, f), encoding='utf-8').read(), ns)
        m = ns['MAP']
        ns.update(SCENE_NAME=name, OUT_DIR=OUT)
        if globals().get('LIGHTS'):
            exec(open(os.path.join(SCRIPTS, 'light_setup.py'), encoding='utf-8').read(), ns)
        if globals().get('SWEEP'):
            exec(open(os.path.join(SCRIPTS, 'door_sweep.py'), encoding='utf-8').read(), ns)
        if globals().get('VISIBILITY'):
            exec(open(os.path.join(SCRIPTS, 'visibility.py'), encoding='utf-8').read(), ns)
        if not globals().get('SKIP_VIEWS'):
            exec(open(os.path.join(SCRIPTS, 'render_views.py'), encoding='utf-8').read(), ns)
        if globals().get('MOOD'):
            exec(open(os.path.join(SCRIPTS, 'render_mood.py'), encoding='utf-8').read(), ns)
        REPORT.append((name, len(m.scene.objects), {c: len(col.all_objects) for c, col in m.c.items()},
                       [os.path.basename(p) for p in ns.get('RESULT', []) + ns.get('RESULT_MOOD', [])],
                       round(time.time() - t, 1), m.terrain_report() if m.terrain_ready else None,
                       ns.get('LIGHT_REPORT'), ns.get('VIS'), m.door_report(), ns.get('SWEEP')))
    except Exception:
        REPORT.append((name, 'ERROR', traceback.format_exc()[-1800:]))
bpy.ops.wm.save_mainfile()
