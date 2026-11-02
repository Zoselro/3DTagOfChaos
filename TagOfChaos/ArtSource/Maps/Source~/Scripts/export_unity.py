# Stage 6: export the map blockouts to Unity (Assets/Maps/...). No Unity code is added or changed.
# - Per map: one FBX per category + Unity .meta (material remap by name, Generate Colliders where walkable/solid)
# - Common: materials that do not already exist in the project (.mat + .meta), one FBX per asset (prefab source)
# - Per map .blend in Source~/Maps, README per map, lighting JSON in Unity coordinates.
# FBX settings match Source~/../리소스/WitchCookieHouse/BlenderScripts/wch_export.py (-Z forward, Y up, baked axes).
# Unity position of a Blender point (x, y, z) = (-x, z, -y).
import bpy, os, json, uuid, math, re, shutil

# project root = four folders up from this script (Assets/Maps/Source~/Scripts); fallback for exec without __file__
PROJ = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..')) \
    if '__file__' in globals() else r"E:\3DClaudeTagOfChaos\TagOfChaos"
ASSETS = os.path.join(PROJ, 'Assets')
MAPS = os.path.join(ASSETS, 'Maps')
COMMON = os.path.join(MAPS, 'Common')
SRC = os.path.join(MAPS, 'Source~')
CATS = ['Ground', 'Terrain', 'MainStructures', 'GameplayProps', 'Decoration', 'Background', 'Lighting', 'Effects']
COLLIDE = {'Ground', 'Terrain', 'MainStructures', 'GameplayProps'}
TEMPLATE_META = os.path.join(ASSETS, '09. Environment', 'WitchCookieProps', 'Decoration', 'Cookie_Bench.fbx.meta')


def newguid():
    return uuid.uuid4().hex


# ---------------------------------------------------------------- materials
def existing_materials():
    """name -> guid of every .mat already in the project (first hit wins), excluding Assets/Maps."""
    found = {}
    for root, dirs, files in os.walk(ASSETS):
        if root.startswith(MAPS) or any(k in root for k in ('Photon', 'TextMesh Pro', 'NatureStarterKit2')):
            continue
        for f in files:
            if f.endswith('.mat.meta'):
                name = f[:-9]
                if name in found:
                    continue
                with open(os.path.join(root, f), encoding='utf-8', errors='ignore') as fh:
                    m = re.search(r'guid:\s*([0-9a-f]{32})', fh.read())
                if m:
                    found[name] = m.group(1)
    return found


MAT_TEMPLATE = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 46, guid: 0000000000000000f000000000000000, type: 0}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: [{keywords}]
  m_InvalidKeywords: []
  m_LightmapFlags: {lmflags}
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: {queue}
  stringTagMap: {tags}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _BumpScale: 1
    - _Cutoff: 0.5
    - _DstBlend: {dst}
    - _GlossMapScale: 1
    - _Glossiness: {gloss}
    - _GlossyReflections: 1
    - _Metallic: 0
    - _Mode: {mode}
    - _OcclusionStrength: 1
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: {src}
    - _UVSec: 0
    - _ZWrite: {zwrite}
    m_Colors:
    - _Color: {{r: {r:.4f}, g: {g:.4f}, b: {b:.4f}, a: {a:.4f}}}
    - _EmissionColor: {{r: {er:.4f}, g: {eg:.4f}, b: {eb:.4f}, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
NATIVE_META = """fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def write_material(name, folder):
    """Unity Built-in Standard material from the Blender material (colour, roughness, emission)."""
    mat = bpy.data.materials.get(name)
    rgb, em, rough = (0.8, 0.8, 0.8), 0.0, 0.75
    if mat is not None:
        rgb = tuple(mat.diffuse_color[:3])
        rough = mat.roughness
        if mat.use_nodes:
            b = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
            if b:
                rgb = tuple(b.inputs['Base Color'].default_value[:3])
                em = b.inputs['Emission Strength'].default_value
    invisible = name == 'M_Invisible_Collider'
    kw = dict(name=name, r=rgb[0], g=rgb[1], b=rgb[2], a=0.0 if invisible else 1.0,
              gloss=round(1 - rough, 2), er=rgb[0] * em, eg=rgb[1] * em, eb=rgb[2] * em,
              keywords='_EMISSION' if em > 0 else ('_ALPHABLEND_ON' if invisible else ''),
              lmflags=1 if em > 0 else 4, mode=2 if invisible else 0, src=5 if invisible else 1,
              dst=10 if invisible else 0, zwrite=0 if invisible else 1, queue=3000 if invisible else -1,
              tags='\n    RenderType: Transparent' if invisible else '{}')
    path = os.path.join(folder, name + '.mat')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(MAT_TEMPLATE.format(**kw))
    guid = newguid()
    meta = path + '.meta'
    if os.path.exists(meta):            # keep an existing guid on re-export
        with open(meta, encoding='utf-8') as fh:
            guid = re.search(r'guid:\s*([0-9a-f]{32})', fh.read()).group(1)
    with open(meta, 'w', encoding='utf-8', newline='\n') as f:
        f.write(NATIVE_META.format(guid=guid))
    return guid


# ---------------------------------------------------------------- fbx + meta
def fbx_meta(fbx_path, mat_guids, colliders):
    with open(TEMPLATE_META, encoding='utf-8') as f:
        t = f.read()
    guid = newguid()
    if os.path.exists(fbx_path + '.meta'):
        with open(fbx_path + '.meta', encoding='utf-8') as fh:
            m = re.search(r'guid:\s*([0-9a-f]{32})', fh.read())
            guid = m.group(1) if m else guid
    ext = ''.join("  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n"
                  f"      name: {n}\n    second: {{fileID: 2100000, guid: {g}, type: 2}}\n"
                  for n, g in sorted(mat_guids.items()))
    t = re.sub(r'guid: [0-9a-f]{32}', f'guid: {guid}', t, count=1)
    # no materials (e.g. Effects): an empty mapping must be written as {} or Unity reports a YAML error
    t = re.sub(r'  externalObjects:(?: \{\})?\n(?:  - first:\n(?:.*\n){4})*',
               ('  externalObjects:\n' + ext) if ext else '  externalObjects: {}\n', t)
    t = t.replace('    addColliders: 0', f'    addColliders: {1 if colliders else 0}')
    t = t.replace('  animationType: 2', '  animationType: 0')
    with open(fbx_path + '.meta', 'w', encoding='utf-8', newline='\n') as f:
        f.write(t)
    return guid


def export_selection(path):
    kw = dict(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'}, apply_unit_scale=True,
              apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
              use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
              bake_anim=False, path_mode='RELATIVE', embed_textures=False, use_custom_props=False)
    # Blender logs "Cannot register a valid material index" for every shared-mesh instance; verified harmless
    # (parsed FBX: every instance keeps all material connections and the geometry is shared) -> keep log quiet
    import io, contextlib
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        r = bpy.ops.export_scene.fbx(**kw)
    EXPORT_LOG['warnings'] = EXPORT_LOG.get('warnings', 0) + buf.getvalue().count('WARNING')
    return r


EXPORT_LOG = {}


def mats_of(objs):
    s = set()
    for o in objs:
        if o.type == 'MESH':
            s |= {m.name for m in o.data.materials if m}
    return s


# ---------------------------------------------------------------- drivers
def prepare_materials():
    """Returns name -> guid for every material the maps/assets use (existing project mats reused)."""
    os.makedirs(os.path.join(COMMON, 'Materials'), exist_ok=True)
    if 'M_Invisible_Collider' not in bpy.data.materials:
        mi = bpy.data.materials.new('M_Invisible_Collider')
        mi.diffuse_color = (1, 0, 1, 0.3)
    used = set()
    for sc in bpy.data.scenes:
        if sc.name in MAP_NAMES:
            used |= mats_of(sc.objects)
    used |= mats_of([o for o in bpy.data.objects if o.type == 'MESH' and o.data.name.startswith('A_')])
    used.add('M_Invisible_Collider')
    exist = existing_materials()
    out, created, reused = {}, [], []
    for n in sorted(used):
        if n in exist:
            out[n] = exist[n]
            reused.append(n)
        else:
            out[n] = write_material(n, os.path.join(COMMON, 'Materials'))
            created.append(n)
    return out, created, reused


MAP_NAMES = ['CandyForest', 'GingerbreadVillage', 'ChocolateFactory', 'CursedCandyCarnival', 'HauntedBakery']


def export_map(name, guids):
    sc = bpy.data.scenes[name]
    bpy.context.window.scene = sc
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    inv = bpy.data.materials['M_Invisible_Collider']
    for o in sc.objects:                                  # boundary walls render invisible in Unity
        if o.type == 'MESH' and 'COL_' in o.name:
            o.data.materials.clear()
            o.data.materials.append(inv)
    folder = os.path.join(MAPS, name, 'Models')
    os.makedirs(folder, exist_ok=True)
    top = bpy.data.collections[name]
    report = []
    groups = {}
    for cat in CATS:
        col = bpy.data.collections[f'{name}_{cat}']
        objs = [o for o in col.all_objects if o.type in ('MESH', 'EMPTY')]
        if cat == 'Terrain':                              # visual water: own FBX without colliders
            water = [o for o in objs if '_Water_' in o.name]
            objs = [o for o in objs if o not in water]
            groups['Water'] = water
        groups[cat] = objs
    for key, objs in groups.items():
        if not objs:                                      # nothing left in this group: drop a stale FBX from older builds
            for stale in (os.path.join(folder, f'{name}_{key}.fbx'), os.path.join(folder, f'{name}_{key}.fbx.meta')):
                if os.path.exists(stale):
                    os.remove(stale)
            continue
        bpy.ops.object.select_all(action='DESELECT')
        for o in objs:
            o.hide_set(False)
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        path = os.path.join(folder, f'{name}_{key}.fbx')
        export_selection(path)
        mg = {n: guids[n] for n in mats_of(objs) if n in guids}
        fbx_meta(path, mg, key in COLLIDE)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == 'MESH')
        report.append(dict(fbx=os.path.basename(path), objects=len(objs), tris=tris,
                           colliders=key in COLLIDE, kb=round(os.path.getsize(path) / 1024)))
    bpy.ops.object.select_all(action='DESELECT')
    return report


def export_assets(guids):
    folder = os.path.join(COMMON, 'Models')
    os.makedirs(folder, exist_ok=True)
    tmp = bpy.data.scenes.get('_AssetExport') or bpy.data.scenes.new('_AssetExport')
    bpy.context.window.scene = tmp
    done = []
    for me in sorted([m for m in bpy.data.meshes if m.name.startswith('A_')], key=lambda m: m.name):
        name = me.name[2:]
        ob = bpy.data.objects.new(name, me)
        tmp.collection.objects.link(ob)
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        path = os.path.join(folder, name + '.fbx')
        export_selection(path)
        fbx_meta(path, {n.name: guids[n.name] for n in me.materials if n and n.name in guids}, False)
        bpy.data.objects.remove(ob)
        done.append((name, len(me.vertices)))
    bpy.data.scenes.remove(tmp)
    return done


def save_map_blends():
    folder = os.path.join(SRC, 'Maps')
    os.makedirs(folder, exist_ok=True)
    out = []
    for n in MAP_NAMES:
        p = os.path.join(folder, f'{n}.blend')
        bpy.data.libraries.write(p, {bpy.data.scenes[n]}, path_remap='RELATIVE_ALL', fake_user=True)
        out.append((os.path.basename(p), round(os.path.getsize(p) / 1024 / 1024, 1)))
    return out


def unity_lighting(name):
    """Lighting recipe with Unity coordinates (-x, z, -y) -> Assets/Maps/<Map>/<Map>_Lighting.json."""
    sc = bpy.data.scenes[name]
    rec = json.loads(sc['unity_lighting'])
    col = bpy.data.collections[f'{name}_Lighting']
    pts = []
    for lo in col.objects:
        if lo.type == 'LIGHT' and lo.data.type != 'SUN':
            p = lo.location
            pts.append(dict(name=lo.name, pos_unity=(round(-p.x, 2), round(p.z, 2), round(-p.y, 2)),
                            color=tuple(round(c, 3) for c in lo.data.color), range=lo.get('unity_range', 10),
                            intensity=round(lo.data.energy / 250.0, 2), shadows=bool(lo.data.use_shadow)))
    rec['point_lights'] = pts
    rec['coordinate_note'] = 'Unity position = (-blender_x, blender_z, -blender_y); FBX exported -Z forward / Y up.'
    path = os.path.join(MAPS, name, f'{name}_Lighting.json')
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(rec, f, indent=1)
    return path, len(pts)
