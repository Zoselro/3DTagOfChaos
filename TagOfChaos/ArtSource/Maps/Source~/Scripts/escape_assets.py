# Escape-mode assets (EscapePlan.md §4.4, EscapeVisualPlan.md §2~§5) -> one FBX per prefab unit.
# Run headless:  blender -b --factory-startup --python escape_assets.py -- <scripts_dir> <blend_out> <fbx_dir> [unit prefixes]
#   unit prefixes (optional, comma separated) builds only matching units, e.g. "ITEM_,CHEST" (the .blend is then not saved).
# 1 unit = 1 m, Z-up, front = -Y (Unity +Z). Shared palette/geometry kit: maplib.py + assets.py (M(), G).
# Modules (same namespace, each appends (unit, fn, args, kwargs) to BUILDS):
#   escape_items.py   ITEM_<ItemId>       parts and tools (origin = centre between both hands)
#   escape_props.py   CHEST, JAR, JAR_Cookie, PASSENGER
#   escape_rockets.py SPY_Rocket_<Map>    rocket + Slot_nn_Empty / Slot_nn_Filled + Flame + Hatch
#   escape_devices.py ESC_<Map>           escape device + Slot_nn + Board + named animated parts
#   escape_witch.py   WITCH
# Each FBX keeps its parts as top-level objects so Unity finds them by name directly under the prefab root.
#
# Art style (EscapeVisualPlan.md §2): cute chunky cartoon dark fantasy. Round simple silhouettes, exaggerated
# proportions, no tiny details (readable from far away), soft low-gloss colours + slightly glossy glowing magic.
# Palette: deep purple, teal, dark grey, warm orange, golden cookie brown. No textures, few material slots.
import bpy, bmesh, sys, os, math, random
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
SCRIPTS = argv[0] if len(argv) > 0 else os.path.dirname(os.path.abspath(__file__))
BLEND_OUT = argv[1] if len(argv) > 1 else os.path.join(os.path.dirname(SCRIPTS), 'Escape', 'TagOfChaos_Escape.blend')
FBX_DIR = argv[2] if len(argv) > 2 else os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(SCRIPTS))),
                                                     '09. Environment', 'Escape', 'Models')
ONLY = [p for p in argv[3].split(',') if p] if len(argv) > 3 else []

for lib in ('maplib.py', 'assets.py'):
    exec(open(os.path.join(SCRIPTS, lib), encoding='utf-8').read(), globals())

# escape-only colours (ME_*), same material rules as the map palette (M()): (rgb, emission strength)
PAL.update({
    # --- common art-style palette (§2) ---
    'ME_Purple_Deep': ((0.24, 0.11, 0.36), 0), 'ME_Purple_Light': ((0.52, 0.30, 0.72), 0),
    'ME_Teal': ((0.10, 0.58, 0.56), 0), 'ME_Gray_Dark': ((0.19, 0.19, 0.23), 0), 'ME_Gray_Light': ((0.52, 0.53, 0.58), 0),
    'ME_Orange_Warm': ((1.0, 0.52, 0.16), 0), 'ME_Cookie_Gold': ((0.82, 0.56, 0.26), 0),
    'ME_Cookie_Dark': ((0.48, 0.27, 0.12), 0), 'ME_Cream': ((0.99, 0.95, 0.88), 0), 'ME_Gold': ((0.95, 0.74, 0.24), 0),
    'ME_Glow_Teal': ((0.20, 1.0, 0.88), 2.0), 'ME_Glow_Orange': ((1.0, 0.58, 0.18), 2.0),
    'ME_Glow_Purple': ((0.78, 0.45, 1.0), 2.0), 'ME_Glow_White': ((1.0, 0.97, 0.88), 3.0),
    'ME_Glass': ((0.75, 0.90, 1.0), 0),          # Unity makes every *Glass* material transparent
    'ME_Red_Button': ((0.92, 0.12, 0.16), 0.5), 'ME_Pink': ((1.0, 0.55, 0.72), 0),
    'ME_Tunnel_Dark': ((0.03, 0.02, 0.03), 0),
    # --- stone ruins under the gingerbread village (Request1003Plan.md §2) ---
    'ME_Stone_Light': ((0.44, 0.42, 0.40), 0), 'ME_Stone_Dark': ((0.20, 0.20, 0.21), 0),
    'ME_Stone_Warm': ((0.40, 0.33, 0.26), 0), 'ME_Stone_Moss': ((0.20, 0.30, 0.16), 0), 'ME_Root': ((0.22, 0.14, 0.08), 0),
    # --- rocket cockpit seen through the open hatch (Request1003bPlan.md §6): faint glow so it reads inside the hull's shadow ---
    'ME_Cabin_Wall': ((0.66, 0.50, 0.88), 0.35), 'ME_Cabin_Floor': ((0.34, 0.28, 0.44), 0.2), 'ME_Cabin_Seat': ((0.55, 0.22, 0.66), 0.3),
    # --- per-item colours ---
    'ME_Oil_Choco': ((0.26, 0.12, 0.05), 0), 'ME_Macaron_Pink': ((1.0, 0.62, 0.76), 0),
    'ME_Rune_Red': ((1.0, 0.15, 0.20), 1), 'ME_Rune_Pink': ((1.0, 0.35, 0.80), 1),
    'ME_Rune_Blue': ((0.20, 0.45, 1.0), 1), 'ME_Rune_Yellow': ((1.0, 0.90, 0.15), 1),
    'ME_Cell_Red': ((1.0, 0.15, 0.15), 0.6), 'ME_Cell_Orange': ((1.0, 0.45, 0.05), 0.6),
    'ME_Cell_Yellow': ((1.0, 0.90, 0.15), 0.6), 'ME_Cell_Green': ((0.30, 0.95, 0.30), 0.6),
    'ME_Balloon': ((0.30, 0.85, 1.0), 0),
    # --- legacy names still used by rockets/devices/witch modules ---
    'ME_Rubber_Black': ((0.08, 0.08, 0.09), 0), 'ME_Button_Red': ((0.95, 0.10, 0.10), 0.6),
    'ME_Strap_Blue': ((0.25, 0.36, 0.55), 0), 'ME_Battery_Green': ((0.50, 0.85, 0.20), 0.4),
    'ME_Toolbox_Orange': ((0.88, 0.36, 0.12), 0), 'ME_Hammer_Pink': ((1.0, 0.40, 0.60), 0),
    'ME_Hammer_Yellow': ((1.0, 0.85, 0.25), 0), 'ME_Stun_Body': ((0.18, 0.22, 0.30), 0),
    'ME_Stun_Spark': ((0.30, 0.85, 1.0), 4), 'ME_Chest_Wood': ((0.55, 0.33, 0.18), 0),
    'ME_Chest_Lid': ((0.45, 0.26, 0.14), 0), 'ME_Rocket_Window': ((0.55, 0.95, 1.0), 1.5),
    'ME_Rocket_Flame': ((1.0, 0.55, 0.15), 4), 'ME_Altar_Glow': ((0.85, 0.50, 1.0), 4),
    'ME_Witch_Robe': ((0.18, 0.08, 0.25), 0), 'ME_Witch_Skin': ((0.55, 0.75, 0.45), 0),
    'ME_Witch_Eye': ((1.0, 0.90, 0.20), 6), 'ME_Witch_Hat': ((0.12, 0.05, 0.18), 0),
    'ME_Rail': ((0.40, 0.40, 0.46), 0), 'ME_Cart_Red': ((0.80, 0.15, 0.30), 0),
})

COL = None
UNITS = {}    # fbx name -> [objects]
BUILDS = []   # (unit, fn, args, kwargs) registered by the modules


def reset_scene():
    global COL
    bpy.ops.wm.read_factory_settings(use_empty=True)
    COL = bpy.context.scene.collection


def mesh_obj(unit, name, build, loc=(0, 0, 0), parent=None, rot=(0, 0, 0)):
    g = G()
    build(g)
    ob = bpy.data.objects.new(name, g.mesh('A_' + unit + '_' + name))
    COL.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    if parent:
        ob.parent = parent
    UNITS.setdefault(unit, []).append(ob)
    return ob


def empty(unit, name, loc=(0, 0, 0), parent=None, rot=(0, 0, 0)):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.2
    COL.objects.link(ob)
    ob.location = loc
    ob.rotation_euler = rot
    if parent:
        ob.parent = parent
    UNITS.setdefault(unit, []).append(ob)
    return ob


# ---------------- geometry helpers (all take the G kit) ----------------
def placed(g, fn, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    """Build fn(g) at the origin, then move/rotate/scale only the new vertices."""
    before = set(g.bm.verts)   # bmesh reuses freed slots, so new verts are found by set difference, not index
    fn(g)
    new = [v for v in g.bm.verts if v not in before]
    from mathutils import Matrix
    Mx = Matrix.Translation(Vector(loc)) @ Euler_to_mat(rot) @ Matrix.Diagonal((*scale, 1))
    for v in new:
        v.co = Mx @ v.co
    return new


def cyl(g, mat, loc, r, h, seg=20, rot=(0, 0, 0), r2=None):
    """Closed cylinder standing on loc (bottom centre), optional taper r2."""
    g.lathe([(0, 0), (r, 0), (r2 if r2 is not None else r, h), (0, h)], mat, seg=seg, loc=loc, rot=rot)


def pill(g, mat, loc, r, h, seg=20, rot=(0, 0, 0)):
    """Capsule: straight part h, rounded ends radius r, centred on loc, axis Z before rot."""
    steps = [i * (math.pi / 2) / 4 for i in range(1, 5)]
    prof = [(0, -h / 2 - r)] + [(r * math.sin(a), -h / 2 - r * math.cos(a)) for a in steps]
    prof += [(r * math.sin(a), h / 2 + r * math.cos(a)) for a in reversed(steps)] + [(0, h / 2 + r)]
    g.lathe(prof, mat, seg=seg, loc=loc, rot=rot)


def torus(g, mat, loc, R, r, seg=24, rot=(0, 0, 0)):
    pts = [(R * math.cos(2 * math.pi * k / seg), R * math.sin(2 * math.pi * k / seg), 0) for k in range(seg + 1)]
    placed(g, lambda gg: gg.tube(pts, r, mat, seg=10, cap=False), loc, rot)


def disc_gear(g, mat, R, thick, teeth, loc=(0, 0, 0), rot=(0, 0, 0), hole=0.0, tooth=(0.12, 0.11)):
    def build(gg):
        gg.lathe([(hole, -thick / 2), (R, -thick / 2), (R, thick / 2), (hole, thick / 2)] if hole > 0 else
                 [(0, -thick / 2), (R, -thick / 2), (R, thick / 2), (0, thick / 2)], mat, seg=32)
        for k in range(teeth):
            a = 2 * math.pi * k / teeth
            gg.rbox(mat, (math.cos(a) * (R + tooth[0] * 0.35), math.sin(a) * (R + tooth[0] * 0.35), -thick / 2),
                    (tooth[0], tooth[1], thick), bevel=0.03, rotz=a)
    placed(g, build, loc, rot)


def half_cut(g, verts_filter_z=0.0):
    """Remove everything below z (in current coordinates) and close the cut (used for domed lids)."""
    bm = g.bm
    res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, 0, verts_filter_z),
                                 plane_no=(0, 0, 1), clear_inner=True)
    edges = [e for e in res['geom_cut'] if isinstance(e, bmesh.types.BMEdge)]
    if edges:
        bmesh.ops.holes_fill(bm, edges=edges, sides=0)


def star_prism(g, mat, loc, R, r, depth, points=5, rot=(0, 0, 0)):
    """Flat chunky star (rune glyphs, decals)."""
    def build(gg):
        bm = gg.bm
        ring_lo, ring_hi = [], []
        for k in range(points * 2):
            a = math.pi / 2 + math.pi * k / points
            rr = R if k % 2 == 0 else r
            ring_lo.append(bm.verts.new((rr * math.cos(a), -depth / 2, rr * math.sin(a))))
            ring_hi.append(bm.verts.new((rr * math.cos(a), depth / 2, rr * math.sin(a))))
        n = len(ring_lo)
        mi = gg.mi(mat)
        f = bm.faces.new(ring_lo); f.material_index = mi
        f = bm.faces.new(list(reversed(ring_hi))); f.material_index = mi
        for k in range(n):
            f = bm.faces.new((ring_lo[k], ring_lo[(k + 1) % n], ring_hi[(k + 1) % n], ring_hi[k])); f.material_index = mi
    placed(g, build, loc, rot)


# ---------------- modules ----------------
for mod in ('escape_items.py', 'escape_props.py', 'escape_rockets.py', 'escape_devices.py', 'escape_witch.py'):
    exec(open(os.path.join(SCRIPTS, mod), encoding='utf-8').read(), globals())


# =====================================================================================
# export
# =====================================================================================
def export_unit(unit, path):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in UNITS[unit]:
        ob.select_set(True)
    kw = dict(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'}, apply_unit_scale=True,
              apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
              use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, add_leaf_bones=False,
              bake_anim=False, path_mode='RELATIVE', embed_textures=False, use_custom_props=False)
    bpy.ops.export_scene.fbx(**kw)


DONE = []


def finish_unit(unit):
    """Export the unit, then rename its objects so the next unit can reuse plain names (Body, Slot_00, ...)."""
    export_unit(unit, os.path.join(FBX_DIR, unit + '.fbx'))
    for ob in UNITS[unit]:
        ob.name = unit + '|' + ob.name
    DONE.append(unit)


def main():
    reset_scene()
    os.makedirs(FBX_DIR, exist_ok=True)
    for unit, fn, args, kw in BUILDS:
        if ONLY and not any(unit.startswith(p) for p in ONLY):
            continue
        fn(*args, **kw)
        finish_unit(unit)

    # lay units out side by side in the .blend for review only (each FBX was exported at its own origin)
    x = 0.0
    for unit, obs in UNITS.items():
        for ob in obs:
            if ob.parent is None:
                ob.location.x += x
        x += 120.0 if unit == 'WITCH' else (30.0 if unit.startswith('ESC_') else 8.0)
    if not ONLY:
        os.makedirs(os.path.dirname(BLEND_OUT), exist_ok=True)
        bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
    print('ESCAPE_ASSETS_OK', len(DONE), ','.join(DONE))


main()
