# Rebuild + export selected maps in one go, then quit (needs a window: export_unity switches scenes via context.window).
#   blender "Assets/Maps/Source~/TagOfChaos_Maps.blend" --python rebuild_map.py -- GingerbreadVillage[,OtherMap]
# Steps: build_all (rebuild the map scene, save .blend) -> export_unity (per-category FBX + .meta, lighting JSON,
# per-map .blend). Writes a report next to this script (rebuild_report.txt). Unity side: MapSceneBuilder.Build(map).
import bpy, sys, os, traceback

SCRIPTS = os.path.dirname(os.path.abspath(__file__))
REPORT_PATH = os.path.join(SCRIPTS, 'rebuild_report.txt')


def run():
    maps = sys.argv[sys.argv.index('--') + 1].split(',')
    out = []
    g = {'MAPS': maps, 'SKIP_VIEWS': True, '__file__': os.path.join(SCRIPTS, 'build_all.py')}
    exec(open(os.path.join(SCRIPTS, 'build_all.py'), encoding='utf-8').read(), g)
    for row in g['REPORT']:
        out.append('BUILD ' + repr(row)[:1500])
        if row[1] == 'ERROR':
            raise RuntimeError('build failed for ' + row[0])
    e = {'__file__': os.path.join(SCRIPTS, 'export_unity.py')}
    exec(open(os.path.join(SCRIPTS, 'export_unity.py'), encoding='utf-8').read(), e)
    guids, created, reused = e['prepare_materials']()
    out.append(f'MATERIALS created {created}')
    for m in maps:
        out.append('EXPORT ' + repr(e['export_map'](m, guids)))
        out.append('LIGHTING ' + repr(e['unity_lighting'](m)))
    out.append('BLENDS ' + repr(e['save_map_blends']()))
    bpy.ops.wm.save_mainfile()
    out.append('REBUILD_OK')
    return out


try:
    lines = run()
except Exception:
    lines = ['REBUILD_FAILED', traceback.format_exc()]
with open(REPORT_PATH, 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))
bpy.ops.wm.quit_blender()
