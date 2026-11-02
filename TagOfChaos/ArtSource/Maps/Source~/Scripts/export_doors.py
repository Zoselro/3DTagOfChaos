# Door leaves for the doorways cut by wall() (GameScenePlan.md §3.7, D3~D5).
# Run in background Blender on a COPY of TagOfChaos_Maps.blend (this script never saves the .blend):
#   blender -b <copy>.blend --python export_doors.py
# Output per map that has doorways:
#   Assets/Maps/<Map>/Models/<Map>_Doors.fbx  - one leaf mesh per (width, height) and side, placed at the origin (mesh library)
#   Assets/Maps/<Map>/<Map>_Doors.json         - every door: id, Unity center/axes, size, wall thickness, leaf mesh names
# Leaf frame (Unity): origin = hinge axis at the leaf bottom, the LEFT leaf extends +X and the RIGHT leaf extends -X,
# thickness along Z, height along +Y. Blender axes are converted by the exporter (Unity (x, y, z) = (-x_b, z_b, -y_b)),
# so the Blender geometry is built along -X (left) / +X (right), thickness along Y, height along Z.
import bpy, bmesh, os, json, math
from mathutils import Vector

PROJ = r"F:\3DClaudeTagOfChaos\TagOfChaos"
SCR = os.path.join(PROJ, 'Assets', 'Maps', 'Source~', 'Scripts')
MAPS = [('ChocolateFactory', 'build_chocolatefactory.py', 'CHO'),
        ('CursedCandyCarnival', 'build_cursedcandycarnival.py', 'CUR'),
        ('HauntedBakery', 'build_hauntedbakery.py', 'HAU')]

LEAF_THICK = 0.2        # door leaf thickness (m)
HINGE_INSET = 0.14      # hinge axis inset from the jamb: half the leaf thickness + 4 cm so the leaf corner clears the jamb
CENTER_GAP = 0.03       # gap between the two leaves where they meet
BOTTOM_GAP = 0.03       # leaf bottom above the floor
TOP_GAP = 0.05          # leaf top below the lintel
WOOD, METAL = 'M_Wood_Dark', 'M_Metal_Dark'


def material(name, rgb):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = (*rgb, 1.0)
    return m


def box(bm, x0, x1, y0, y1, z0, z1, mat_index):
    vs = [bm.verts.new((x, y, z)) for x in (x0, x1) for y in (y0, y1) for z in (z0, z1)]
    idx = lambda i, j, k: vs[i * 4 + j * 2 + k]
    quads = [
        ((0, 0, 0), (0, 1, 0), (0, 1, 1), (0, 0, 1)), ((1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0)),
        ((0, 0, 0), (0, 0, 1), (1, 0, 1), (1, 0, 0)), ((0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)),
        ((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)), ((0, 0, 1), (0, 1, 1), (1, 1, 1), (1, 0, 1)),
    ]
    for q in quads:
        f = bm.faces.new([idx(*p) for p in q])
        f.material_index = mat_index


def leaf_object(name, width, height, side):
    """side: +1 = LEFT leaf (Unity +X = Blender -X), -1 = RIGHT leaf."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    s = -side                                 # Blender x direction of the leaf length
    x_a, x_b = sorted((0.0, s * width))
    t = LEAF_THICK / 2
    box(bm, x_a, x_b, -t, t, 0.0, height, 0)                         # slab
    for frac in (0.12, 0.5, 0.88):                                    # three iron bands (slightly proud of the slab)
        zc = height * frac
        box(bm, x_a, x_b, -t - 0.02, t + 0.02, zc - 0.12, zc + 0.12, 1)
    hx = s * (width - 0.35)                                           # handles near the free edge, cookie hand height
    for sy in (-1, 1):
        y0, y1 = sorted((sy * t, sy * (t + 0.12)))
        box(bm, hx - 0.06, hx + 0.06, y0, y1, 0.95, 1.35, 1)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material(WOOD, (0.25, 0.14, 0.08)))
    me.materials.append(material(METAL, (0.12, 0.12, 0.14)))
    ob = bpy.data.objects.new(name, me)
    return ob


def to_unity(v):
    return [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)]


result = {}
for map_name, build_file, prefix in MAPS:
    ns = {}
    exec(open(os.path.join(SCR, 'maplib.py'), encoding='utf-8').read(), ns)
    exec(open(os.path.join(SCR, 'assets.py'), encoding='utf-8').read(), ns)
    walls = []
    original_wall = ns['wall']

    def recording_wall(m, base, p0, p1, h, t=1.0, gaps=(), mat='M_Cookie_Wall', cat='MainStructures', gap_h=None, tags=()):
        first = len(m.doors)
        original_wall(m, base, p0, p1, h, t, gaps, mat, cat, gap_h, tags)
        walls.append(dict(base=base, p0=p0, p1=p1, t=t, gap_h=gap_h, doors=list(range(first, len(m.doors)))))

    ns['wall'] = recording_wall
    exec(open(os.path.join(SCR, build_file), encoding='utf-8').read(), ns)
    m = ns['MAP']

    # room centre per wall group = centroid of that group's end points (interior partitions fall on the line itself)
    centroid = {}
    for w in walls:
        pts = centroid.setdefault(w['base'], [])
        pts += [Vector((*w['p0'], 0)), Vector((*w['p1'], 0))]
    centroid = {k: sum(v, Vector()) / len(v) for k, v in centroid.items()}

    doors, leaves = [], {}
    door_count = {}                           # per wall group — a room has several wall() calls, ids must stay unique (Room Prop key)
    for w in walls:
        u = (Vector((*w['p1'], 0)) - Vector((*w['p0'], 0))).normalized()
        for di in w['doors']:
            d = m.doors[di]
            k = door_count.get(w['base'], 0)
            door_count[w['base']] = k + 1
            has_lintel = w['gap_h'] is not None
            c = Vector((d['x'], d['y'], m.h_at(d['x'], d['y']) if hasattr(m, 'h_at') else 0.0))
            n = Vector((d['nx'], d['ny'], 0)).normalized()
            if (centroid[w['base']] - c).dot(n) < -1e-3:
                n = -n                        # normal points into the room
            width, height = d['width'], d['height']
            leaf_w = round(width / 2 - HINGE_INSET - CENTER_GAP / 2, 3)
            leaf_h = round(height - BOTTOM_GAP - TOP_GAP, 3)
            key = f"{prefix}_W{leaf_w:.2f}_H{leaf_h:.2f}"   # map prefix: one Blender session builds every map, same sizes must not collide
            if has_lintel and key not in leaves:
                leaves[key] = (leaf_w, leaf_h)
            doors.append(dict(
                id=f"{prefix}_{w['base']}_{k:02d}", wall=w['base'], lintel=has_lintel,
                center=to_unity(c), along=to_unity(u), inward=to_unity(n),
                width=width, height=height, wall_thickness=w['t'],
                leaf_width=leaf_w, leaf_height=leaf_h, bottom_gap=BOTTOM_GAP, hinge_inset=HINGE_INSET,
                leaf_left=f"DoorLeaf_{key}_L", leaf_right=f"DoorLeaf_{key}_R"))

    # mesh library scene
    sc = bpy.data.scenes.new(f"{map_name}_Doors")
    bpy.context.window.scene = sc
    objs = []
    for key, (lw, lh) in sorted(leaves.items()):
        for side, suffix in ((1, 'L'), (-1, 'R')):
            ob = leaf_object(f"DoorLeaf_{key}_{suffix}", lw, lh, side)
            sc.collection.objects.link(ob)
            objs.append(ob)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    out_dir = os.path.join(PROJ, 'Assets', 'Maps', map_name)
    fbx = os.path.join(out_dir, 'Models', f"{map_name}_Doors.fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
                             bake_anim=False, path_mode='RELATIVE', embed_textures=False, use_custom_props=False)
    with open(os.path.join(out_dir, f"{map_name}_Doors.json"), 'w', encoding='utf-8') as f:
        json.dump(dict(map=map_name, leaf_thickness=LEAF_THICK, doors=doors), f, indent=1)
    assert len({d['id'] for d in doors}) == len(doors), f"{map_name}: duplicate door ids"
    result[map_name] = dict(doors=len(doors), with_lintel=sum(1 for d in doors if d['lintel']), leaf_meshes=len(objs))

print("DOORS_EXPORTED", json.dumps(result))
