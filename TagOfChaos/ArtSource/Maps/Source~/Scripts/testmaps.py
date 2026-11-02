# Test maps for Request1009Plan.md §25 (G1): quadrant map + monster room + 4 buildings + 4 exits.
# Blender coords: X = east, Y = north, Z = up (exported to Unity as X east, Z north, Y up).
# Usage: blender -b --python testmaps.py -- <out_dir>
import bpy, math, os, sys

OUT = sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.dirname(__file__)
MAPS = ['CandyForest', 'GingerbreadVillage', 'ChocolateFactory', 'HauntedBakery', 'CursedCandyCarnival']

HALF = 70.0          # map half size (m)
ROOM = 12.0          # monster room half size
CHAMFER = 6.0        # monster room corner cut (doors sit on the cut)
WALL_H = 6.0
ROOM_H = 5.0
BLD = 7.0            # building half size
BLD_C = 41.0         # building center |x|,|y|
BLD_H = 4.5
DOOR_W = 4.0
EXIT_W = 6.0
QUADS = {1: (-1, 1), 2: (1, 1), 3: (-1, -1), 4: (1, -1)}  # guide numbering: 1 TL, 2 TR, 3 BL, 4 BR

MATS = {}


def setparent(o, parent):
    # objects are created at world positions; keep them there (parent inverse)
    bpy.context.view_layer.update()
    o.parent = parent
    o.matrix_parent_inverse = parent.matrix_world.inverted()


def mat(name):
    if name not in MATS:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        MATS[name] = m
    return MATS[name]


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()


def box(name, center, size, material, parent=None, rot_z=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center, rotation=(0, 0, rot_z))
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.data.materials.append(mat(material))
    if parent: setparent(o, parent)
    return o


def cyl(name, center, r, h, material, parent=None, verts=16, cone_top=None):
    if cone_top is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=center)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=cone_top, depth=h, location=center)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(mat(material))
    if parent: setparent(o, parent)
    return o


def sphere(name, center, r, material, parent=None):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=r, location=center)
    o = bpy.context.active_object
    o.name = name
    o.data.materials.append(mat(material))
    if parent: setparent(o, parent)
    return o


def empty(name, loc, parent=None, rot_z=0.0):
    o = bpy.data.objects.new(name, None)
    o.empty_display_type = 'PLAIN_AXES'
    o.location = loc
    o.rotation_euler = (0, 0, rot_z)
    bpy.context.scene.collection.objects.link(o)
    if parent: setparent(o, parent)
    return o


def wall_segment(name, a, b, h, t, material, parent):
    ax, ay = a
    bx, by = b
    L = math.hypot(bx - ax, by - ay)
    ang = math.atan2(by - ay, bx - ax)
    return box(name, ((ax + bx) / 2, (ay + by) / 2, h / 2), (L, t, h), material, parent, rot_z=ang)


def wall_with_gap(prefix, a, b, h, t, gap, material, parent):
    """Wall from a to b with a centered gap of width gap. Returns the gap center."""
    ax, ay = a
    bx, by = b
    L = math.hypot(bx - ax, by - ay)
    ux, uy = (bx - ax) / L, (by - ay) / L
    k = (L - gap) / 2
    wall_segment(prefix + '_A', a, (ax + ux * k, ay + uy * k), h, t, material, parent)
    wall_segment(prefix + '_B', (bx - ux * k, by - uy * k), b, h, t, material, parent)
    return ((ax + bx) / 2, (ay + by) / 2)


# ---------------------------------------------------------------- theme props
def prop(theme, name, x, y, parent, big=False):
    s = 1.3 if big else 1.0
    root = empty(name, (x, y, 0), parent)
    if theme == 'CandyForest':      # lollipop tree
        cyl(name + '_Stick', (x, y, 1.6 * s), 0.3 * s, 3.2 * s, 'TM_PropB', root, 8)
        sphere(name + '_Candy', (x, y, 3.6 * s), 1.3 * s, 'TM_PropA', root)
    elif theme == 'GingerbreadVillage':  # small gingerbread hut
        box(name + '_Body', (x, y, 1.2 * s), (2.4 * s, 2.4 * s, 2.4 * s), 'TM_PropA', root)
        cyl(name + '_Roof', (x, y, 3.1 * s), 2.0 * s, 1.4 * s, 'TM_PropB', root, 4, cone_top=0.0)
    elif theme == 'ChocolateFactory':  # crate + barrel
        box(name + '_Crate', (x, y, 1.2 * s), (2.4 * s, 2.4 * s, 2.4 * s), 'TM_PropA', root)
        cyl(name + '_Barrel', (x, y, 3.0 * s), 0.8 * s, 1.2 * s, 'TM_PropB', root, 12)
    elif theme == 'HauntedBakery':     # oven with chimney
        box(name + '_Oven', (x, y, 1.3 * s), (2.6 * s, 2.2 * s, 2.6 * s), 'TM_PropA', root)
        cyl(name + '_Chimney', (x + 0.6 * s, y, 3.2 * s), 0.35 * s, 1.4 * s, 'TM_PropB', root, 8)
    else:                              # carnival tent
        cyl(name + '_Base', (x, y, 1.1 * s), 1.4 * s, 2.2 * s, 'TM_PropA', root, 12)
        cyl(name + '_Top', (x, y, 2.9 * s), 1.7 * s, 1.4 * s, 'TM_PropB', root, 12, cone_top=0.0)
    return root


# ---------------------------------------------------------------- map parts
def ground(parent):
    g = empty('Ground', (0, 0, 0), parent)
    box('Floor', (0, 0, -0.5), (HALF * 2, HALF * 2, 1.0), 'TM_Ground', g)
    t = 1.0
    # outer walls with a centered exit gap on each side
    wall_with_gap('Wall_N', (-HALF, HALF), (HALF, HALF), WALL_H, t, EXIT_W, 'TM_Wall', g)
    wall_with_gap('Wall_S', (HALF, -HALF), (-HALF, -HALF), WALL_H, t, EXIT_W, 'TM_Wall', g)
    wall_with_gap('Wall_E', (HALF, HALF), (HALF, -HALF), WALL_H, t, EXIT_W, 'TM_Wall', g)
    wall_with_gap('Wall_W', (-HALF, -HALF), (-HALF, HALF), WALL_H, t, EXIT_W, 'TM_Wall', g)
    return g


def monster_room(parent):
    r = empty('MonsterRoom', (0, 0, 0), parent)
    c, k = ROOM, CHAMFER
    # straight walls (face the cross arms)
    wall_segment('RoomWall_N', (-k, c), (k, c), ROOM_H, 0.6, 'TM_Room', r)
    wall_segment('RoomWall_S', (-k, -c), (k, -c), ROOM_H, 0.6, 'TM_Room', r)
    wall_segment('RoomWall_E', (c, -k), (c, k), ROOM_H, 0.6, 'TM_Room', r)
    wall_segment('RoomWall_W', (-c, -k), (-c, k), ROOM_H, 0.6, 'TM_Room', r)
    # chamfered corners with a monster door in each (one per quadrant)
    for q, (sx, sy) in QUADS.items():
        a, b = (sx * k, sy * c), (sx * c, sy * k)
        cx, cy = wall_with_gap(f'RoomCorner_Q{q}', a, b, ROOM_H, 0.6, DOOR_W, 'TM_Room', r)
        ang = math.atan2(b[1] - a[1], b[0] - a[0])
        box(f'MonsterDoor_{q - 1}', (cx, cy, ROOM_H / 2 - 0.25), (DOOR_W, 0.3, ROOM_H - 0.5), 'TM_MonsterDoor', r, rot_z=ang)
    box('RoomFloorMark', (0, 0, 0.02), (ROOM * 1.6, ROOM * 1.6, 0.04), 'TM_RoomFloor', r)
    empty('Spawn_Monster', (0, 0, 0.1), r)
    return r


def building(q, parent):
    sx, sy = QUADS[q]
    cx, cy = sx * BLD_C, sy * BLD_C
    b = empty(f'Building_Q{q}', (cx, cy, 0), parent)
    h = BLD
    wall_with_gap(f'BWall_Q{q}_N', (cx - h, cy + h), (cx + h, cy + h), BLD_H, 0.5, DOOR_W, 'TM_Building', b)
    wall_with_gap(f'BWall_Q{q}_S', (cx + h, cy - h), (cx - h, cy - h), BLD_H, 0.5, DOOR_W, 'TM_Building', b)
    wall_with_gap(f'BWall_Q{q}_E', (cx + h, cy + h), (cx + h, cy - h), BLD_H, 0.5, DOOR_W, 'TM_Building', b)
    wall_with_gap(f'BWall_Q{q}_W', (cx - h, cy - h), (cx - h, cy + h), BLD_H, 0.5, DOOR_W, 'TM_Building', b)
    for i, (px, py) in enumerate([(cx - h, cy - h), (cx + h, cy - h), (cx - h, cy + h), (cx + h, cy + h)]):
        cyl(f'BPillar_Q{q}_{i}', (px, py, BLD_H / 2 + 0.3), 0.6, BLD_H + 0.6, 'TM_BuildingTrim', b, 10)
    for i, (ox, oy, sx_, sy_) in enumerate([(0, h, h * 2 + 0.6, 0.6), (0, -h, h * 2 + 0.6, 0.6), (h, 0, 0.6, h * 2 + 0.6), (-h, 0, 0.6, h * 2 + 0.6)]):
        box(f'BRoofRing_Q{q}_{i}', (cx + ox, cy + oy, BLD_H + 0.3), (sx_, sy_, 0.3), 'TM_BuildingTrim', b)
    cyl(f'Socket_Q{q}', (cx, cy, 0.5), 0.9, 1.0, 'TM_Socket', b, 16)
    empty('Repair_Point', (cx, cy, 1.2), b)
    glow = cyl('ESC_Glow', (cx, cy, 5.5), 0.45, 9.0, 'TM_Glow', b, 12)
    return b


def exits(parent):
    root = empty('Exits', (0, 0, 0), parent)
    spots = {'N': (0, HALF, 0.0), 'S': (0, -HALF, math.pi), 'E': (HALF, 0, -math.pi / 2), 'W': (-HALF, 0, math.pi / 2)}
    for key, (x, y, rz) in spots.items():
        e = empty(f'Exit_{key}', (x, y, 0), root, rot_z=rz)
        # frame across the gap (local X along the wall)
        ca, sa = math.cos(rz), math.sin(rz)
        def at(lx, ly):
            return (x + lx * ca - ly * sa, y + lx * sa + ly * ca)
        for s in (-1, 1):
            px, py = at(s * (EXIT_W / 2 + 0.6), 0)
            box(f'ExitPillar_{key}_{"L" if s < 0 else "R"}', (px, py, 3.5), (1.2, 1.6, 7.0), 'TM_ExitFrame', e, rot_z=rz)
        box(f'ExitLintel_{key}', (x, y, 7.4), (EXIT_W + 2.4, 1.6, 0.8), 'TM_ExitFrame', e, rot_z=rz)
        d = box('Door', (x, y, 3.0), (EXIT_W, 0.5, 6.0), 'TM_ExitDoor', e, rot_z=rz)
        d.name = f'Door_{key}'
        px, py = at(0, 4.0)
        box(f'ExitPad_{key}', (px, py, -0.5), (EXIT_W + 2, 8.0, 1.0), 'TM_ExitPad', e, rot_z=rz)
        tx, ty = at(0, 3.0)
        empty('Trigger', (tx, ty, 1.0), e).name = f'ExitTrigger_{key}'
    return root


def dividers(theme, parent):
    d = empty('Dividers', (0, 0, 0), parent)
    n = 0
    # each arm: props every 6.5 m from the room edge to 58 m (the exit plaza stays open); offsets alternate a little
    for axis, sign in (('y', 1), ('y', -1), ('x', 1), ('x', -1)):
        t = ROOM + 3.0
        i = 0
        while t <= 58.0:
            off = 1.2 if i % 2 else -1.2
            x, y = (off, sign * t) if axis == 'y' else (sign * t, off)
            prop(theme, f'Divider_{n:02d}', x, y, d, big=(i % 3 == 0))
            n += 1
            i += 1
            t += 6.5
    return d


# quadrant-local points (u = |x|, v = |y|) mirrored into each quadrant
CHEST_UV = [(20, 20), (20, 32), (20, 50), (20, 62), (30, 62), (52, 62), (62, 62), (62, 50), (62, 30), (52, 20)]
ROCKET_UV = [(60, 24), (26, 58), (58, 58)]
RAINBOW_UV = [(26, 44), (44, 26)]
DECOR_UV = [(30, 22), (56, 30), (34, 56), (24, 26)]
SPAWN_UV = (54, 40)


def markers(theme, parent):
    m = empty('Markers', (0, 0, 0), parent)
    deco = empty('Decoration', (0, 0, 0), parent)
    chest = 0
    rocket = 0
    rainbow = 0
    for q, (sx, sy) in QUADS.items():
        for (u, v) in CHEST_UV:
            empty(f'CHEST_Slot_{chest:02d}', (sx * u, sy * v, 0.0), m, rot_z=math.atan2(-sy * v, -sx * u) + math.pi / 2)
            chest += 1
        for (u, v) in RAINBOW_UV:
            empty(f'RainbowSpawn_{rainbow:02d}', (sx * u, sy * v, 0.0), m)
            rainbow += 1
        for (u, v) in DECOR_UV:
            prop(theme, f'Deco_Q{q}_{u}_{v}', sx * u, sy * v, deco, big=True)
        empty(f'Spawn_Cookie_Q{q}', (sx * SPAWN_UV[0], sy * SPAWN_UV[1], 0.1), m)
    for i, q in enumerate([1, 2, 3, 4, 1, 4]):  # 6 rocket candidates
        sx, sy = QUADS[q]
        u, v = ROCKET_UV[i % len(ROCKET_UV)]
        empty(f'RocketSpawn_{rocket:02d}', (sx * u, sy * v, 0.0), m)
        rocket += 1


def build(theme):
    clear()
    root = empty(f'TestMap_{theme}', (0, 0, 0))
    ground(root)
    monster_room(root)
    for q in QUADS: building(q, root)
    exits(root)
    dividers(theme, root)
    markers(theme, root)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f'TestMap_{theme}.blend'))
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'TestMap_{theme}.fbx'), use_selection=True,
                             object_types={'EMPTY', 'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False, use_mesh_modifiers=True,
                             mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False, bake_anim=False,
                             path_mode='RELATIVE', embed_textures=False, use_custom_props=False)
    print('TESTMAP_OK', theme, len(bpy.data.objects))


for t in MAPS:
    build(t)
