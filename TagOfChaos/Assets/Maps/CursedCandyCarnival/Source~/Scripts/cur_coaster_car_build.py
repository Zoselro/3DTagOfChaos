# Roller coaster train (2026-10-21, Request1009Plan.md §31 Q2): splits the received Car (one mesh: dragon-head car + 6 seat cars)
# into 7 cars at the gaps between them, puts each car's origin at the middle of its wheels on the wheel bottoms, scales the train
# so a car is as wide as the rail ribbon, turns it so the head points along Blender +Y and exports Models/Coaster/CUR_CoasterTrain.fbx
# (Car_0 = head ... Car_6) with the original UVs (no decimation, so no rebake). Also writes CoasterTrain_metal.png
# (R = metallic, A = 1 - roughness, Unity Standard) and coaster_train.json (per-car pivot along the train and wheel spans, m).
# blender -b --factory-startup --python cur_coaster_car_build.py -- <Source~ dir> <Models/Coaster dir>
import bpy, bmesh, sys, os, json, math, shutil, traceback
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Coaster", "Car")
OUT = argv[1]
log_file = open(os.path.join(argv[0], "Coaster", "car_build.txt"), "w")

SCALE = 10.5                  # car width 0.17 -> 1.8 m (rail ribbon 1.83 m)
CUTS = [-0.5101, -0.2361, 0.0239, 0.2479, 0.4899, 0.7079]  # gaps between cars along x (source units, cur_car_probe.py)
WHEEL_BAND = 0.015            # wheel-bottom vertices: within this of the lowest point


def log(*a):
    log_file.write(" ".join(str(x) for x in a) + "\n"); log_file.flush()


def car_mesh(src, lo, hi, name):
    """Copy of src keeping only faces whose centre lies in [lo, hi) along x."""
    bm = bmesh.new(); bm.from_mesh(src.data)
    gone = [f for f in bm.faces if not (lo <= f.calc_center_median().x < hi)]
    bmesh.ops.delete(bm, geom=gone, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for m in src.data.materials: me.materials.append(m)
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def metal_map():
    """Unity Standard metallic map from the received metallic + roughness."""
    met = bpy.data.images.load(os.path.join(SRC, "Car_metallic.png"))
    rou = bpy.data.images.load(os.path.join(SRC, "Car_roughness.png"))
    w, h = met.size
    if tuple(rou.size) != (w, h): rou.scale(w, h)
    m = np.array(met.pixels[:]).reshape(h, w, 4); r = np.array(rou.pixels[:]).reshape(h, w, 4)
    outp = np.zeros_like(m); outp[..., 0] = m[..., 0]; outp[..., 1] = m[..., 0]; outp[..., 2] = m[..., 0]; outp[..., 3] = 1.0 - r[..., 0]
    img = bpy.data.images.new("CoasterTrain_metal", w, h, alpha=True)
    img.pixels = outp.ravel().tolist()
    img.filepath_raw = os.path.join(OUT, "CoasterTrain_metal.png"); img.file_format = "PNG"; img.save()
    return w, h


try:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "Car.fbx"))
    src = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
    src.data.transform(src.matrix_world); src.matrix_world.identity()
    vs = np.array([v.co[:] for v in src.data.vertices])
    zlo = vs[:, 2].min()
    edges = [vs[:, 0].min() - 1e-3] + CUTS + [vs[:, 0].max() + 1e-3]
    # head (-x) -> +Y, then scale; pivot of car k = middle of its wheels on the wheel bottoms
    turn = Matrix.Rotation(math.radians(-90), 4, "Z")
    meta = {"scale": SCALE, "cars": []}
    cars = []
    for k in range(7):
        o = car_mesh(src, edges[k], edges[k + 1], "Car_%d" % k)
        P = np.array([v.co[:] for v in o.data.vertices])
        W = P[P[:, 2] < zlo + WHEEL_BAND][:, 0]
        w0, w1 = W.min(), W.max()
        pivot = Vector(((w0 + w1) / 2, 0.0, zlo))
        o.data.transform(Matrix.Scale(SCALE, 4) @ turn @ Matrix.Translation(-pivot))
        o.location = (turn @ pivot.to_4d()).to_3d() * SCALE
        o.location.z = 0.0
        size = [round((P[:, i].max() - P[:, i].min()) * SCALE, 3) for i in range(3)]
        # along the train (head first): distance of this pivot behind the head pivot, front/rear wheel offsets from the pivot
        meta["cars"].append(dict(name=o.name, pivot=round(float(pivot.x) * SCALE, 4), length=size[0], width=size[1], height=size[2],
                                 wheel_half=round(float(w1 - w0) / 2 * SCALE, 4),
                                 front=round(float(pivot.x - P[:, 0].min()) * SCALE, 4), back=round(float(P[:, 0].max() - pivot.x) * SCALE, 4),
                                 tris=sum(len(p.vertices) - 2 for p in o.data.polygons)))
        cars.append(o)
        log(o.name, meta["cars"][-1])
    head = meta["cars"][0]["pivot"]
    for c in meta["cars"]: c["behind_head"] = round(c["pivot"] - head, 4)
    bpy.data.objects.remove(src)

    for name in ("Car.png", "Car_normal.png"):
        shutil.copyfile(os.path.join(SRC, name), os.path.join(OUT, name.replace("Car", "CoasterTrain_albedo" if name == "Car.png" else "CoasterTrain")))
    meta["metal_size"] = metal_map()

    bpy.ops.object.select_all(action="DESELECT")
    for o in cars: o.select_set(True)
    bpy.context.view_layer.objects.active = cars[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CUR_CoasterTrain.fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP", embed_textures=False)
    meta["train_length"] = round(meta["cars"][-1]["behind_head"] + meta["cars"][-1]["back"] + meta["cars"][0]["front"], 3)
    json.dump(meta, open(os.path.join(OUT, "coaster_train.json"), "w"), indent=1)
    log("train length", meta["train_length"])
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
log_file.close()
