# Cursed Candy Carnival carousel (Request1009Plan.md §29, step K1).
# Assembles the received body (platform, centre column, 16 poles, canopy) and one unicorn horse into one carousel:
# body scaled to the old floor disc (29.6 m wide, own proportions), one horse on every pole (the pole runs through the
# torso, heads facing the spin — counter-clockwise seen from above — alternating up/down). The high-poly sources
# (395k / 633k triangles) are reduced and their colour + normal are baked from the originals onto fresh UVs, so the
# textures do not smear the way plain decimation does (§28.16).
# Writes Models/Carousel/CUR_Carousel.fbx (CarouselBody, CarouselHorses, COL_Carousel), the baked PNGs and carousel.json.
# blender -b --factory-startup --python cur_carousel_build.py -- <Source~ dir> <out dir> [quick]
import bpy, bmesh, sys, os, math, json, traceback
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Carousel")
OUT = argv[1]
QUICK = len(argv) > 2 and argv[2] == "quick"
os.makedirs(OUT, exist_ok=True)
LOG = open(os.path.join(SRC, "build.txt"), "w")

BODY_DIAMETER = 29.6        # old CUR_Carousel_Base disc
BODY_TRIS, HORSE_TRIS = 30000, 6000
BODY_TEX, HORSE_TEX = (256, 256) if QUICK else (2048, 2048)  # one horse texture serves all 16
POLES = 16
HORSE_GAP = 0.88            # horse length / distance between neighbouring poles
RIDE = 0.2                  # horse feet this share of the free pole height above the platform
BOB = 0.08                  # alternate horses this share up / down


def log(*a):
    LOG.write(" ".join(str(x) for x in a) + "\n"); LOG.flush()


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def bbox(o):
    vs = [v.co for v in o.data.vertices]
    return (Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs))),
            Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs))))


def only(*objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def import_textured(name):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, name, name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    assert len(new) == 1, (name, [o.name for o in new])
    o = new[0]
    o.data.transform(o.matrix_world)
    o.matrix_world = Matrix.Identity(4)
    o.name = name + "_hi"
    mat = bpy.data.materials.new("SRC_" + name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    def image(suffix, color):
        im = bpy.data.images.load(os.path.join(SRC, name, name + suffix + ".png"))
        if not color: im.colorspace_settings.name = "Non-Color"
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = im
        return n
    base = image("", True)
    nt.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    nrm = image("_normal", False)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nrm.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    o.data.materials.clear(); o.data.materials.append(mat)
    return o


def decimate(o, target):
    t = tris(o)
    if t <= target: return
    md = o.modifiers.new("dec", "DECIMATE")
    md.ratio = max(0.002, target / t)
    md.use_collapse_triangulate = True
    only(o)
    bpy.ops.object.modifier_apply(modifier=md.name)


def bake_low(hi, name, target, size, extrusion, ray):
    lo = hi.copy(); lo.data = hi.data.copy(); lo.name = name; lo.data.name = name
    bpy.context.scene.collection.objects.link(lo)
    decimate(lo, target)
    uv = lo.data.uv_layers.new(name="BakeUV")
    lo.data.uv_layers.active = uv
    only(lo)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")
    for layer in [l for l in lo.data.uv_layers if l.name != "BakeUV"]:
        lo.data.uv_layers.remove(layer)

    col = bpy.data.images.new(name + "_albedo", size, size)
    nrm = bpy.data.images.new(name + "_normal", size, size)
    nrm.colorspace_settings.name = "Non-Color"
    mat = bpy.data.materials.new("M_" + name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    cn = nt.nodes.new("ShaderNodeTexImage"); cn.image = col
    nn = nt.nodes.new("ShaderNodeTexImage"); nn.image = nrm
    nt.links.new(cn.outputs["Color"], bsdf.inputs["Base Color"])
    lo.data.materials.clear(); lo.data.materials.append(mat)

    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 1
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = extrusion
    sc.render.bake.max_ray_distance = ray
    sc.render.bake.margin = 8
    for image, node, kind in ((col, cn, "DIFFUSE"), (nrm, nn, "NORMAL")):
        nt.nodes.active = node
        only(hi, lo, active=lo)
        if kind == "DIFFUSE":
            bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
        else:
            bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
        image.filepath_raw = os.path.join(OUT, image.name + ".png")
        image.file_format = "PNG"
        image.save()
    # the normal map feeds the exported material too (Unity reads the albedo; normal is assigned by the editor tool)
    nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nn.outputs["Color"], nm.inputs["Color"])
    nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    log("BAKED", name, "tris", tris(lo), "tex", size)
    return lo


def slice_counts(o, axis_xy, reach, step):
    """Vertex count per z slice within `reach` of a vertical axis."""
    counts = {}
    ax, ay = axis_xy
    for v in o.data.vertices:
        if (v.co.x - ax) ** 2 + (v.co.y - ay) ** 2 <= reach * reach:
            k = int(v.co.z / step)
            counts[k] = counts.get(k, 0) + 1
    return counts


def find_poles(o, height):
    """Angles and radius of the ride poles: angular vertex histogram in the free band between platform and canopy."""
    band = [v.co for v in o.data.vertices if height * 0.25 < v.co.z < height * 0.45]  # below the canopy valance
    rmax = max(math.hypot(c.x, c.y) for c in band)
    hist = [0] * 360
    for c in band:
        r = math.hypot(c.x, c.y)
        if r > rmax * 0.55: hist[int(math.degrees(math.atan2(c.y, c.x)) % 360)] += 1
    peaks = []
    for a in sorted(range(360), key=lambda i: -hist[i]):
        if len(peaks) == POLES: break
        if all(min(abs(a - p), 360 - abs(a - p)) > 12 for p in peaks): peaks.append(a)
    poles = []
    for a in sorted(peaks):
        near = [c for c in band if math.hypot(c.x, c.y) > rmax * 0.55
                and min(abs(math.degrees(math.atan2(c.y, c.x)) % 360 - a), 360 - abs(math.degrees(math.atan2(c.y, c.x)) % 360 - a)) < 1.5]
        cx = sum(c.x for c in near) / len(near); cy = sum(c.y for c in near) / len(near)
        poles.append((math.degrees(math.atan2(cy, cx)) % 360, math.hypot(cx, cy)))
    return poles


def pole_span(o, axis_xy, height):
    """Platform top and canopy bottom along one pole: thin slices hold only the pole, busy slices are platform / canopy."""
    step = height / 200
    counts = slice_counts(o, axis_xy, height * 0.012, step)
    mid = int(height * 0.35 / step)
    thin = sorted(counts.get(k, 0) for k in range(mid - 10, mid + 10))[10]
    busy = max(8, thin * 4)
    k = mid
    while k > 0 and counts.get(k, 0) <= busy: k -= 1
    bottom = (k + 1) * step
    k = mid
    while k < 200 and counts.get(k, 0) <= busy: k += 1
    top = k * step
    return bottom, top


try:
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # ---- body: own proportions, 29.6 m wide, centred, standing on z = 0
    body_hi = import_textured("Body")
    lo, hi = bbox(body_hi)
    d = hi - lo
    s = BODY_DIAMETER / max(d.x, d.y)
    body_hi.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))))
    lo, hi = bbox(body_hi)
    H = hi.z
    poles = find_poles(body_hi, H)
    spans = [pole_span(body_hi, (r * math.cos(math.radians(a)), r * math.sin(math.radians(a))), H) for a, r in poles]
    floor_z = sorted(b for b, _ in spans)[len(spans) // 2]
    roof_z = sorted(t for _, t in spans)[len(spans) // 2]
    radius = sum(r for _, r in poles) / len(poles)
    log("BODY scale", round(s, 3), "size", tuple(round(v, 2) for v in hi - lo), "tris", tris(body_hi))
    log("POLES", [(round(a, 1), round(r, 2)) for a, r in poles])
    log("SPANS", [(round(b, 2), round(t, 2)) for b, t in spans], "-> floor", round(floor_z, 2), "roof", round(roof_z, 2), "radius", round(radius, 2))

    # ---- horse: torso on the pole, length HORSE_GAP of the pole spacing
    horse_hi = import_textured("Horse")
    lo, hi = bbox(horse_hi)
    d = hi - lo
    chord = 2 * radius * math.sin(math.pi / POLES)
    hs = HORSE_GAP * chord / d.y
    torso = [v.co for v in horse_hi.data.vertices if lo.z + d.z * 0.5 < v.co.z < lo.z + d.z * 0.75 and abs(v.co.x - (lo.x + hi.x) / 2) < d.x * 0.3]
    ty = sum(c.y for c in torso) / len(torso)
    horse_hi.data.transform(Matrix.Scale(hs, 4) @ Matrix.Translation(-Vector(((lo.x + hi.x) / 2, ty, lo.z))))
    hl, hh = bbox(horse_hi)
    horse_h = hh.z
    free = roof_z - floor_z - horse_h
    log("HORSE scale", round(hs, 3), "size", tuple(round(v, 2) for v in hh - hl), "torso y", round(ty * hs, 2), "free", round(free, 2))
    assert free > 0, "horse taller than the pole"

    body_lo = bake_low(body_hi, "CarouselBody", BODY_TRIS, BODY_TEX, 0.03, 0.2)
    horse_lo = bake_low(horse_hi, "CarouselHorse", HORSE_TRIS, HORSE_TEX, 0.015, 0.1)

    # ---- 16 horses: pole through the torso, head (-Y) along the counter-clockwise tangent, bobbing heights
    copies = []
    for i, (a, r) in enumerate(poles):
        c = horse_lo.copy(); c.data = horse_lo.data.copy()
        bpy.context.scene.collection.objects.link(c)
        th = math.radians(a)
        z = floor_z + free * (RIDE + (BOB if i % 2 == 0 else -BOB))
        c.data.transform(Matrix.Translation((r * math.cos(th), r * math.sin(th), z)) @ Matrix.Rotation(th + math.pi, 4, "Z"))
        copies.append(c)
    only(*copies)
    bpy.ops.object.join()
    horses = bpy.context.view_layer.objects.active
    horses.name = "CarouselHorses"; horses.data.name = "CarouselHorses"

    # ---- collision: convex hull of the body's outermost points (5 height bands x 24 directions + the finial), so the
    # hull stays under Unity's 256-face convex limit without bulging (platform to finial — nothing to walk or hide in)
    pts = {}
    for v in body_lo.data.vertices:
        c = v.co
        k = (min(4, int(c.z / H * 5)), int((math.degrees(math.atan2(c.y, c.x)) % 360) / 15))
        if k not in pts or c.x * c.x + c.y * c.y > pts[k].x ** 2 + pts[k].y ** 2: pts[k] = c.copy()
    pts["top"] = max((v.co for v in body_lo.data.vertices), key=lambda c: c.z).copy()
    pts["bottom"] = Vector((0, 0, 0))
    bm = bmesh.new()
    for c in pts.values(): bm.verts.new(c)
    hull = bmesh.ops.convex_hull(bm, input=bm.verts[:])
    bmesh.ops.delete(bm, geom=list(set(hull["geom_interior"]) | set(hull["geom_unused"])), context="VERTS")
    me = bpy.data.meshes.new("COL_Carousel"); bm.to_mesh(me); bm.free()
    col = bpy.data.objects.new("COL_Carousel", me)
    bpy.context.scene.collection.objects.link(col)
    assert tris(col) <= 255, tris(col)

    for o in (body_hi, horse_hi, horse_lo):
        bpy.data.objects.remove(o, do_unlink=True)
    only(body_lo, horses, col)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CUR_Carousel.fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=True,
                             add_leaf_bones=False, bake_anim=False, path_mode="STRIP", embed_textures=False)
    # Unity axes: x = -x, y = z, z = -y
    meta = dict(diameter=round(BODY_DIAMETER, 2), height=round(H, 2), floor=round(floor_z, 2), roof=round(roof_z, 2),
                pole_radius=round(radius, 2), horse_height=round(horse_h, 2),
                tris=dict(body=tris(body_lo), horses=tris(horses), collision=tris(col)))
    json.dump(meta, open(os.path.join(OUT, "carousel.json"), "w"), indent=1)
    log("EXPORT", meta)

    # preview
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = 1000; sc.render.resolution_y = 800
    sc.render.image_settings.file_format = "JPEG"
    col.hide_render = True
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
    cam.data.type = "PERSP"; cam.data.lens = 35; sc.camera = cam
    for tag, elev, azim, dist in (("iso", 22, -30, 2.0), ("low", 6, -15, 1.0)):
        cam.rotation_euler = (math.radians(90 - elev), 0, math.radians(azim))
        bpy.context.view_layer.update()
        cam.location = Vector((0, 0, H * 0.4)) + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * BODY_DIAMETER * dist
        sc.render.filepath = os.path.join(SRC, "preview_%s.jpg" % tag)
        bpy.ops.render.render(write_still=True)
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
LOG.close()
