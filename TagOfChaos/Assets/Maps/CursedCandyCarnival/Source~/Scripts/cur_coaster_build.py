# Cursed Candy Carnival roller coaster exit (Request1009Plan.md §30, step R1).
# Entrance (fanged castle): one scale, colour + normal baked from the 579k-triangle original onto fresh UVs (like §29),
#   a clean 4.4 x 4.2 m passage cut from the mouth through the closed back wall (dark tunnel material), and the front stairs
#   (with railings) copied, turned around and set under the back opening — up the front, through the mouth, down the back.
# Track: the rail scaled to 48 m long (own proportions); the supports are built from one free-standing post of the
#   received support model (base plate and collars kept, only the shaft stretched) under the rail every few metres, with
#   braces between neighbours — the received support set (1.9 x 1.6 footprint) does not fit under the rail (1.9 x 0.29).
#   Rail red, supports purple (reference image), both with baked ambient occlusion.
# Writes Models/Coaster/CUR_CoasterEntrance.fbx, CUR_CoasterTrack.fbx, the PNGs and coaster.json (Unity axes for points).
# blender -b --factory-startup --python cur_coaster_build.py -- <Source~ dir> <out dir> [quick]
import bpy, bmesh, sys, os, math, json, traceback
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:]
SRC = os.path.join(argv[0], "Coaster")
OUT = argv[1]
QUICK = len(argv) > 2 and argv[2] == "quick"
os.makedirs(OUT, exist_ok=True)
LOG = open(os.path.join(SRC, "build.txt"), "w")

ENTRANCE_SCALE = 9.0
ENTRANCE_TRIS = 60000
TEX = 256 if QUICK else 2048
AO_TEX = 256 if QUICK else 1024
PASS_HALF, PASS_HEIGHT = 2.2, 4.2     # walk-through passage — wide and tall enough for the monster too (body 3.6 m, 3 m)
STAIR_HALF = 2.7                      # front stairs with their railings
MOUTH_IN = -0.137                     # source units: where the inner mouth steps reach the passage floor
TRACK_LENGTH, TRACK_LIFT = 48.0, 1.5
POST_BASE = 1.0                       # post base plate width
POST_SPACING, BRACE_REACH = 3.5, 6.5
RAIL_COLOR, SUPPORT_COLOR, TUNNEL_COLOR = (0.86, 0.06, 0.1, 1), (0.33, 0.17, 0.8, 1), (0.08, 0.03, 0.09, 1)


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


def import_mesh(folder, name, textured):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, folder, name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    assert len(new) == 1, (name, [o.name for o in new])
    o = new[0]
    o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)
    o.name = name + "_src"
    if textured:
        mat = bpy.data.materials.new("SRC_" + name); mat.use_nodes = True
        nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
        def image(suffix, color):
            im = bpy.data.images.load(os.path.join(SRC, folder, name + suffix + ".png"))
            if not color: im.colorspace_settings.name = "Non-Color"
            n = nt.nodes.new("ShaderNodeTexImage"); n.image = im
            return n
        nt.links.new(image("", True).outputs["Color"], bsdf.inputs["Base Color"])
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(image("_normal", False).outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
        o.data.materials.clear(); o.data.materials.append(mat)
    return o


def decimate(o, target):
    t = tris(o)
    if t <= target: return
    md = o.modifiers.new("dec", "DECIMATE"); md.ratio = max(0.002, target / t); md.use_collapse_triangulate = True
    only(o); bpy.ops.object.modifier_apply(modifier=md.name)


def smart_uv(o):
    uv = o.data.uv_layers.new(name="BakeUV"); o.data.uv_layers.active = uv
    only(o)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003)
    bpy.ops.object.mode_set(mode="OBJECT")
    for layer in [l for l in o.data.uv_layers if l.name != "BakeUV"]: o.data.uv_layers.remove(layer)


def cycles(samples):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = samples
    sc.render.bake.margin = 8


def save(image):
    image.filepath_raw = os.path.join(OUT, image.name + ".png"); image.file_format = "PNG"; image.save()


def bake_low(hi, name, target, size, extrusion, ray):
    """Reduce a copy of the high-poly source and bake its colour and normal onto fresh UVs."""
    lo = hi.copy(); lo.data = hi.data.copy(); lo.name = name; lo.data.name = name
    bpy.context.scene.collection.objects.link(lo)
    decimate(lo, target)
    smart_uv(lo)
    col = bpy.data.images.new(name + "_albedo", size, size)
    nrm = bpy.data.images.new(name + "_normal", size, size); nrm.colorspace_settings.name = "Non-Color"
    mat = bpy.data.materials.new("M_" + name); mat.use_nodes = True
    nt = mat.node_tree; bsdf = nt.nodes["Principled BSDF"]
    cn = nt.nodes.new("ShaderNodeTexImage"); cn.image = col
    nn = nt.nodes.new("ShaderNodeTexImage"); nn.image = nrm
    nt.links.new(cn.outputs["Color"], bsdf.inputs["Base Color"])
    lo.data.materials.clear(); lo.data.materials.append(mat)
    cycles(1)
    sc = bpy.context.scene
    sc.render.bake.use_selected_to_active = True; sc.render.bake.cage_extrusion = extrusion; sc.render.bake.max_ray_distance = ray
    for image, node, kind in ((col, cn, "DIFFUSE"), (nrm, nn, "NORMAL")):
        nt.nodes.active = node
        only(hi, lo, active=lo)
        if kind == "DIFFUSE": bpy.ops.object.bake(type="DIFFUSE", pass_filter={"COLOR"})
        else: bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
        save(image)
    log("BAKED", name, "tris", tris(lo), "tex", size)
    return lo


def plain_material(name, color):
    mat = bpy.data.materials.new(name); mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = color
    return mat


def bake_ao(o, size):
    """Ambient occlusion of everything in the scene onto the object's own fresh UVs (Unity occlusion map)."""
    smart_uv(o)
    img = bpy.data.images.new(o.name + "_ao", size, size)
    mat = o.data.materials[0]
    node = mat.node_tree.nodes.new("ShaderNodeTexImage"); node.image = img
    mat.node_tree.nodes.active = node
    cycles(16 if not QUICK else 4)
    bpy.context.scene.render.bake.use_selected_to_active = False
    only(o)
    bpy.ops.object.bake(type="AO")
    save(img)
    mat.node_tree.nodes.remove(node)
    log("AO", o.name, size)


def box_mesh(name, lo, hi, material):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((lo.x + (v.co.x + 0.5) * (hi.x - lo.x), lo.y + (v.co.y + 0.5) * (hi.y - lo.y), lo.z + (v.co.z + 0.5) * (hi.z - lo.z)))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    me.materials.append(material)
    return o


def unity(p):  # Blender (x, y, z) -> Unity (-x, z, -y)
    return [round(-p[0], 3), round(p[2], 3), round(-p[1], 3)]


meta = {}
try:
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # ================================================================ entrance
    hi = import_mesh("Entrance", "Entrance", True)
    lo_b, hi_b = bbox(hi)
    s = ENTRANCE_SCALE
    hi.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(-Vector(((lo_b.x + hi_b.x) / 2, (lo_b.y + hi_b.y) / 2, lo_b.z))))
    lo_b, hi_b = bbox(hi)
    bvh = BVHTree.FromObject(hi, bpy.context.evaluated_depsgraph_get())
    # passage floor: ray down inside the existing tunnel (behind the mouth)
    hit = bvh.ray_cast(Vector((0, 0.3 * s, 0.32 * hi_b.z)), Vector((0, 0, -1)))  # inside the tunnel (floor ~23%, roof ~39% of the height)
    floor_z = hit[0].z
    hit = bvh.ray_cast(Vector((0, hi_b.y + 1.0, floor_z + 1.2)), Vector((0, -1, 0)))
    back_y = hit[0].y
    mouth_y = MOUTH_IN * s
    log("ENTRANCE scale", s, "size", tuple(round(v, 2) for v in hi_b - lo_b), "floor", round(floor_z, 2), "back wall", round(back_y, 2), "mouth", round(mouth_y, 2))

    ent = bake_low(hi, "CoasterEntrance", ENTRANCE_TRIS, TEX, 0.03, 0.25)
    tunnel_mat = plain_material("M_CoasterTunnel", TUNNEL_COLOR)

    # passage: cut from the mouth through the closed back wall
    cutter = box_mesh("Cutter", Vector((-PASS_HALF, mouth_y, floor_z - 0.03)), Vector((PASS_HALF, back_y + 0.8, floor_z + PASS_HEIGHT)), tunnel_mat)
    md = ent.modifiers.new("cut", "BOOLEAN"); md.operation = "DIFFERENCE"; md.object = cutter; md.solver = "EXACT"
    if hasattr(md, "material_mode"): md.material_mode = "TRANSFER"
    only(ent); bpy.ops.object.modifier_apply(modifier=md.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    if tunnel_mat.name not in [m.name for m in ent.data.materials if m]: ent.data.materials.append(tunnel_mat)

    # back stairs: the front stairs (in front of the passage, under its floor) turned around under the back opening
    bm = bmesh.new(); bm.from_mesh(ent.data)
    stair = [f for f in bm.faces if all(v.co.y < mouth_y and abs(v.co.x) < STAIR_HALF and v.co.z < floor_z + 0.3 for v in f.verts)]
    foot_y = min(v.co.y for f in stair for v in f.verts)
    dup = bmesh.ops.duplicate(bm, geom=stair)
    dverts = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMVert)]
    dfaces = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
    for v in dverts: v.co.y = -v.co.y + (back_y + mouth_y)
    bmesh.ops.reverse_faces(bm, faces=dfaces)
    bm.to_mesh(ent.data); bm.free(); ent.data.update()
    back_foot_y = -foot_y + (back_y + mouth_y)
    log("STAIRS faces", len(stair), "front foot", round(foot_y, 2), "back foot", round(back_foot_y, 2))
    meta["entrance"] = dict(scale=s, size=[round(v, 2) for v in (hi_b - lo_b)], floor=round(floor_z, 3), pass_half=PASS_HALF, pass_height=PASS_HEIGHT,
                            stair_half=STAIR_HALF, front_foot=round(foot_y, 3), mouth=round(mouth_y, 3), back_wall=round(back_y, 3),
                            back_foot=round(back_foot_y, 3), half_x=round(max(-lo_b.x, hi_b.x), 2), tris=tris(ent),
                            note="Blender axes: front is -y; Unity z = -y")
    bpy.data.objects.remove(hi, do_unlink=True)
    only(ent)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CUR_CoasterEntrance.fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP", embed_textures=False)
    ent.hide_render = True

    # ================================================================ track
    rail = import_mesh("Track", "Track", False)
    a, b = bbox(rail)
    rs = TRACK_LENGTH / (b.x - a.x)
    rail.data.transform(Matrix.Translation((0, 0, TRACK_LIFT)) @ Matrix.Scale(rs, 4) @ Matrix.Translation(-Vector(((a.x + b.x) / 2, (a.y + b.y) / 2, a.z))))
    rail.name = "CoasterRail"; rail.data.name = "CoasterRail"
    rail.data.materials.append(plain_material("M_CoasterRail", RAIL_COLOR))
    ra, rb = bbox(rail)
    log("RAIL scale", round(rs, 3), "size", tuple(round(v, 2) for v in rb - ra), "tris", tris(rail))
    rbvh = BVHTree.FromObject(rail, bpy.context.evaluated_depsgraph_get())

    # post prototype: the smallest free-standing island of the support set
    sup = import_mesh("Support", "Support", False)
    bm = bmesh.new(); bm.from_mesh(sup.data); bm.verts.ensure_lookup_table()
    seen, best = set(), None
    for v in bm.verts:
        if v.index in seen: continue
        stack, comp = [v], []
        seen.add(v.index)
        while stack:
            q = stack.pop(); comp.append(q)
            for e in q.link_edges:
                w = e.other_vert(q)
                if w.index not in seen: seen.add(w.index); stack.append(w)
        if best is None or len(comp) < len(best): best = comp
    keep = set(v.index for v in best)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS")
    proto = bpy.data.meshes.new("PostProto"); bm.to_mesh(proto); bm.free()
    bpy.data.objects.remove(sup, do_unlink=True)
    pa = Vector((min(v.co.x for v in proto.vertices), min(v.co.y for v in proto.vertices), min(v.co.z for v in proto.vertices)))
    pb = Vector((max(v.co.x for v in proto.vertices), max(v.co.y for v in proto.vertices), max(v.co.z for v in proto.vertices)))
    ps = POST_BASE / max(pb.x - pa.x, pb.y - pa.y)
    proto.transform(Matrix.Scale(ps, 4) @ Matrix.Translation(-Vector(((pa.x + pb.x) / 2, (pa.y + pb.y) / 2, pa.z))))
    ph = (pb.z - pa.z) * ps
    shaft_r = POST_BASE * 0.18
    log("POST proto verts", len(proto.vertices), "scale", round(ps, 2), "height", round(ph, 2))

    # where the posts go: under the rail. A column crossed by one rail layer gets a post up to that rail (tall posts
    # under the high loops, like the reference); where layers stack, the post stops under the lowest one.
    def layers(x, y):
        found, z = [], 0.02
        while True:
            h = rbvh.ray_cast(Vector((x, y, z)), Vector((0, 0, 1)))
            if h[0] is None: return found
            found.append(h[0].z)
            z = h[0].z + 0.8  # step past this rail's thickness
    cands = []
    x = ra.x + 0.5
    while x < rb.x:
        y = ra.y
        while y <= rb.y:
            ls = layers(x, y)
            if ls and ls[0] > 1.0: cands.append((len(ls) > 1, Vector((x, y, ls[0]))))
            y += 0.5
        x += 0.5
    cands.sort(key=lambda c: (c[0], -c[1].z))  # single-layer first, tallest first
    posts = []
    for stacked, c in cands:
        if all((Vector((c.x, c.y)) - Vector((p.x, p.y))).length >= POST_SPACING for p in posts): posts.append(c)
    log("POSTS", len(posts), "from candidates", len(cands), "single-layer", sum(1 for st, _ in cands if not st))

    bm = bmesh.new()
    zb, zt = ph * 0.15, ph * 0.85
    for p in posts:
        top = p.z - 0.05
        stretch = (top - ph + (zt - zb)) / (zt - zb)
        tmp = bmesh.new(); tmp.from_mesh(proto)
        for v in tmp.verts:
            z = v.co.z
            if z >= zt: z = z + (top - ph)
            elif z > zb: z = zb + (z - zb) * max(0.0, stretch)
            v.co = Vector((v.co.x + p.x, v.co.y + p.y, z))
        me = bpy.data.meshes.new("tmp"); tmp.to_mesh(me); tmp.free()
        bm.from_mesh(me); bpy.data.meshes.remove(me)
    # braces: each post to its two nearest neighbours — one diagonal, one level bar
    def brace(p0, p1, r):
        d = p1 - p0
        res = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=r, radius2=r, depth=d.length)
        rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        bmesh.ops.transform(bm, matrix=Matrix.Translation((p0 + p1) / 2) @ rot, verts=res["verts"])
    pairs = set()
    for i, p in enumerate(posts):
        near = sorted((j for j in range(len(posts)) if j != i), key=lambda j: (Vector((posts[j].x, posts[j].y)) - Vector((p.x, p.y))).length)[:2]
        for j in near:
            if (Vector((posts[j].x, posts[j].y)) - Vector((p.x, p.y))).length <= BRACE_REACH: pairs.add((min(i, j), max(i, j)))
    for i, j in pairs:
        a0, b0 = posts[i], posts[j]
        h = min(a0.z, b0.z)
        brace(Vector((a0.x, a0.y, h * 0.3)), Vector((b0.x, b0.y, h * 0.75)), shaft_r * 0.55)
        brace(Vector((a0.x, a0.y, h * 0.5)), Vector((b0.x, b0.y, h * 0.5)), shaft_r * 0.45)
    me = bpy.data.meshes.new("CoasterSupports"); bm.to_mesh(me); bm.free()
    supports = bpy.data.objects.new("CoasterSupports", me); bpy.context.scene.collection.objects.link(supports)
    me.materials.append(plain_material("M_CoasterSupport", SUPPORT_COLOR))
    log("SUPPORTS posts", len(posts), "braces", len(pairs) * 2, "tris", tris(supports))

    # ground plane only for the occlusion bake
    ground = box_mesh("Ground", Vector((-60, -40, -0.2)), Vector((60, 40, 0)), plain_material("M_Ground", (0.5, 0.5, 0.5, 1)))
    bake_ao(rail, AO_TEX)
    bake_ao(supports, AO_TEX)
    bpy.data.objects.remove(ground, do_unlink=True)
    only(rail, supports)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "CUR_CoasterTrack.fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", use_tspace=True, add_leaf_bones=False,
                             bake_anim=False, path_mode="STRIP", embed_textures=False)
    meta["track"] = dict(scale=round(rs, 3), size=[round(v, 2) for v in (rb - ra)], lift=TRACK_LIFT, posts=len(posts),
                         rail_tris=tris(rail), support_tris=tris(supports))
    json.dump(meta, open(os.path.join(OUT, "coaster.json"), "w"), indent=1)
    log("EXPORT", meta)

    # previews
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"; sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "MATERIAL"
    sc.render.resolution_x = 1000; sc.render.resolution_y = 700; sc.render.image_settings.file_format = "JPEG"
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam)
    cam.data.lens = 35; sc.camera = cam
    ent.hide_render = False
    ent.location = (0, rb.y + 1 - (back_foot_y), 0)  # entrance in front of the track for the picture only
    for tag, target, elev, azim, dist in (("track", Vector((0, 0, 9)), 18, -25, 60), ("entrance_front", Vector((0, ent.location.y, 6)), 10, -20, 34),
                                          ("entrance_back", Vector((0, ent.location.y + 6, 4)), 15, 160, 30), ("all", Vector((0, 0, 8)), 15, -15, 70)):
        sc.display.shading.color_type = "TEXTURE" if tag.startswith("entrance") else "MATERIAL"
        cam.rotation_euler = (math.radians(90 - elev), 0, math.radians(azim))
        bpy.context.view_layer.update()
        cam.location = target + (cam.matrix_world.to_3x3() @ Vector((0, 0, 1))) * dist
        sc.render.filepath = os.path.join(SRC, "preview_%s.jpg" % tag)
        bpy.ops.render.render(write_still=True)
    log("DONE")
except Exception:
    log("FAIL", traceback.format_exc())
LOG.close()
