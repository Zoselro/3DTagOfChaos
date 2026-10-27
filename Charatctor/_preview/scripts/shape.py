# shape.py -- bungeoppang boy fin tail + facial expression shape keys for the other two characters (preview only)
import bpy, os, math, mathutils
import numpy as np

ROOT = r"E:\3DTagOfChaos\Charatctor"
OUT = os.path.join(ROOT, "_preview")
EXPR = ["Neutral", "Happy", "Angry", "Sad", "Surprised"]


def find_fbx(token):
    for d in os.listdir(ROOT):
        p = os.path.join(ROOT, d)
        if os.path.isdir(p) and not d.startswith("_"):
            for f in os.listdir(p):
                if token in f and f.lower().endswith(".fbx"):
                    return os.path.join(p, f)


def load(token):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=find_fbx(token))
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.18, 0.18, 0.2)
    o = [o for o in sc.objects if o.type == "MESH"][0]
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


def coords(o):
    a = np.empty(len(o.data.vertices) * 3, dtype=np.float64)
    o.data.vertices.foreach_get("co", a)
    return a.reshape(-1, 3)


def shoot(path, center, scale, view=(0, -1, 0), res=420):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("cam")
    if cam is None:
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
        sc.collection.objects.link(cam)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = scale
    cam.data.clip_end = 100
    dv = mathutils.Vector(view).normalized()
    cam.location = mathutils.Vector(center) + dv * 5
    cam.rotation_euler = (-dv).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


def smooth(t):
    t = np.clip(t, 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------------- 1) bungeoppang boy: big fish tail -> small hand-sized fin ----------------

def fin_tail():
    o = load("Sailor")
    v = coords(o)
    shoot(os.path.join(OUT, "tail_before_back.png"), (0, 0, -0.4), 1.3, (0, 1, 0))
    shoot(os.path.join(OUT, "tail_before_side.png"), (0, 0.15, -0.4), 1.3, (1, 0, 0))
    # tail = everything behind the back surface (y > ~0.06) below the head; root at the back of the hips
    root = np.array([0.0, 0.07, -0.46])
    s = 0.18  # ~0.75 m tall tail -> ~0.13 m: about the size of the hand
    w = smooth((v[:, 1] - 0.05) / 0.03) * (v[:, 2] < -0.13) * (np.abs(v[:, 0]) < 0.4)  # the back surface is at y <= 0.045
    target = root + (v - root) * s
    nv = v + (target - v) * w[:, None]
    o.data.vertices.foreach_set("co", nv.ravel())
    o.data.update()
    moved = int((w > 0.01).sum())
    tv = nv[w > 0.5]
    print("INFO fin verts moved=%d size(m) x=%.3f y=%.3f z=%.3f" % (moved, *(tv.max(0) - tv.min(0))))
    shoot(os.path.join(OUT, "tail_after_back.png"), (0, 0, -0.4), 1.3, (0, 1, 0))
    shoot(os.path.join(OUT, "tail_after_side.png"), (0, 0.15, -0.4), 1.3, (1, 0, 0))
    shoot(os.path.join(OUT, "tail_after_full.png"), (0, 0, 0), 2.2, (0.7, 1, 0.15))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "BungeoppangBoy_FinTail.blend"))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "BungeoppangBoy_FinTail.fbx"), use_selection=False,
                             path_mode="COPY", embed_textures=True)


# ---------------- 2) expressions ----------------

def vertex_colors(o):
    """Per-vertex texture color (average of its loops' UV samples)."""
    mat = o.data.materials[0]
    img = next(n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)
    uv = np.empty(len(o.data.loops) * 2, dtype=np.float32)
    o.data.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    lv = np.empty(len(o.data.loops), dtype=np.int64)
    o.data.loops.foreach_get("vertex_index", lv)
    xi = np.clip((uv[:, 0] % 1.0) * w, 0, w - 1).astype(int)
    yi = np.clip((uv[:, 1] % 1.0) * h, 0, h - 1).astype(int)
    col = px[yi, xi, :3]
    n = len(o.data.vertices)
    acc = np.zeros((n, 3)); cnt = np.zeros(n)
    np.add.at(acc, lv, col); np.add.at(cnt, lv, 1)
    return acc / np.maximum(cnt, 1)[:, None]


class Feature:
    def __init__(self, v, idx, pad=1.15):
        p = v[idx]
        lo, hi = np.percentile(p, 4, axis=0), np.percentile(p, 96, axis=0)
        self.c = (lo + hi) / 2
        self.r = np.maximum((hi - lo) / 2 * pad, 0.012)
        self.yback = np.percentile(p[:, 1], 90)
        self.yfront = np.percentile(p[:, 1], 5)

    def weight(self, v, grow=0.7, depth=0.05, front_gap=None):
        d = np.sqrt(((v[:, 0] - self.c[0]) / self.r[0]) ** 2 + ((v[:, 2] - self.c[2]) / self.r[2]) ** 2)
        w = (1 - smooth((d - 1) / grow)) * (v[:, 1] < self.yback + depth)
        if front_gap is not None:                     # leave glasses / monocle that float in front of the face
            w = w * (v[:, 1] > self.yfront - front_gap)
        return w


def find_face(v, col, eye_z, x_lim, white_feature_mustache, glasses):
    lum = col @ np.array([0.3, 0.59, 0.11])
    sat = col.max(1) - col.min(1)
    front = v[:, 1] < np.percentile(v[:, 1], 30)
    white = (col.min(1) > 0.62) & (sat < 0.12) & front

    def darkest(region, extra):
        # the darkest texels of a region (eyes / glasses / chocolate mustache) -- threshold relative to that region
        return region & (lum < np.percentile(lum[region], 5) + extra)

    def brightest(region, extra):
        # greyish-white texels (brows / white mustache) -- the brightest low-saturation part of the region
        r = region & (sat < 0.2)
        return r & (lum > np.percentile(lum[r], 92) - extra)

    eye_region = front & (np.abs(v[:, 0]) < x_lim) & (np.abs(v[:, 2] - eye_z) < 0.13)
    dark = darkest(eye_region, 0.04)
    eyes = []
    for side in (-1, 1):
        box = dark & (v[:, 0] * side > 0.02)
        idx = np.where(box)[0]
        if glasses:
            # the frame ring hides the eye in its middle: take dark texels close to the ring centre
            p = v[idx]
            rc = (p.min(0) + p.max(0)) / 2
            near = front & (lum < 0.25) & (np.hypot(v[:, 0] - rc[0], v[:, 2] - rc[2]) < 0.05)
            idx = np.where(near)[0]
            if len(idx) < 5:
                f = Feature(v, np.where(box)[0]); f.c = rc.copy(); f.r = np.array([0.02, 0.02, 0.035])
                eyes.append(f)
                continue
        else:
            c = np.median(v[idx], axis=0)
            dd = np.hypot(v[idx, 0] - c[0], v[idx, 2] - c[2])
            idx = idx[dd <= 0.11]                     # only the eye oval: drop stray chocolate chips around it
        eyes.append(Feature(v, idx, pad=1.25))
    brows = []
    for e in eyes:
        side = np.sign(e.c[0])
        region = front & (v[:, 0] * side > 0.0) & (np.abs(v[:, 0] - e.c[0]) < 0.14) & \
            (v[:, 2] > e.c[2] + e.r[2] / 1.25 + 0.02) & (v[:, 2] < e.c[2] + e.r[2] + 0.17)
        idx = np.where(brightest(region, 0.08))[0]
        idx = idx[v[idx, 2] < v[idx, 2].min() + 0.08]  # the lowest bright cluster = the brow, not a hat / cream band above it
        b = Feature(v, idx, pad=1.1)
        b.c[2] -= 0.018; b.r[2] += 0.03               # include the dark line under the brow tuft
        b.ztop = v[idx, 2].max() + 0.01               # never pull the hat / cream band above the brow
        brows.append(b)
    mz = min(e.c[2] - e.r[2] for e in eyes)
    mregion = front & (np.abs(v[:, 0]) < 0.3) & (v[:, 2] < mz - 0.005) & (v[:, 2] > mz - 0.26)
    mbox = brightest(mregion, 0.08) if white_feature_mustache else darkest(mregion, 0.05)
    mustache = Feature(v, np.where(mbox)[0], pad=1.05)
    return eyes, brows, mustache


def add_key(o, name, nv):
    k = o.shape_key_add(name=name, from_mix=False)
    k.data.foreach_set("co", nv.ravel())
    return k


def eye_shape(v, e, sz=1.0, sx=1.0, dz=0.0, arch=0.0):
    w = e.weight(v, grow=0.35, depth=0.03, front_gap=0.012)
    t = v.copy()
    t[:, 0] = e.c[0] + (v[:, 0] - e.c[0]) * sx
    t[:, 2] = e.c[2] + (v[:, 2] - e.c[2]) * sz + dz
    u = np.clip((v[:, 0] - e.c[0]) / e.r[0], -1, 1)
    t[:, 2] += arch * e.r[2] * (1 - u * u)
    return (t - v) * w[:, None]


def brow_shape(v, b, dz=0.0, tilt=0.0):
    w = b.weight(v, grow=0.35, front_gap=0.03) * (1 - smooth((v[:, 2] - b.ztop) / 0.02))
    inner = -np.sign(b.c[0])                       # toward the nose
    d = np.zeros_like(v)
    d[:, 2] = dz + tilt * (v[:, 0] - b.c[0]) * inner
    return d * w[:, None]


def mustache_shape(v, m, curve=0.0, dz=0.0, sz=1.0):
    w = m.weight(v, grow=0.4)
    u = np.clip((v[:, 0] - m.c[0]) / m.r[0], -1, 1)
    d = np.zeros_like(v)
    d[:, 2] = curve * u * u + dz + (v[:, 2] - m.c[2]) * (sz - 1)
    return d * w[:, None]


def mouth_outline(kind, W, n=32):
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        ca, sa = math.cos(a), math.sin(a)
        if kind == "Neutral":
            x, z = 0.32 * W * ca, 0.05 * W * sa
            z += 0.05 * W * (ca * ca)
        elif kind == "Happy":
            x = 0.5 * W * ca
            z = (0.04 * W * sa if sa > 0 else 0.38 * W * sa) + 0.12 * W * ca * ca
        elif kind == "Angry":
            x, z = 0.42 * W * ca, 0.09 * W * sa
            z -= 0.16 * W * ca * ca
        elif kind == "Sad":
            x, z = 0.3 * W * ca, 0.06 * W * sa
            z -= 0.18 * W * ca * ca
        else:  # Surprised
            x, z = 0.17 * W * ca, 0.24 * W * sa
        pts.append((x, z))
    return pts


def build_mouth(target, anchor, W):
    sc = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()

    def surf(x, z):
        hit, loc, *_ = sc.ray_cast(dg, mathutils.Vector((x, -3, z)), mathutils.Vector((0, 1, 0)))
        return (loc.y - 0.004) if hit else anchor[1]

    shapes = {k: [(anchor[0], 0.0)] + mouth_outline(k, W) for k in EXPR}
    def verts(k):
        return [(anchor[0] + x, surf(anchor[0] + x, anchor[2] + z), anchor[2] + z) for x, z in [(0, 0)] + shapes[k][1:]]
    base = verts("Neutral")
    n = len(base) - 1
    faces = [(0, 1 + i, 1 + (i + 1) % n) for i in range(n)]
    me = bpy.data.meshes.new("Mouth")
    me.from_pydata(base, [], faces)
    mo = bpy.data.objects.new("Mouth", me)
    sc.collection.objects.link(mo)
    mat = bpy.data.materials.new("MouthMat")
    mat.diffuse_color = (0.25, 0.05, 0.06, 1)
    me.materials.append(mat)
    mo.shape_key_add(name="Basis")
    for k in EXPR[1:]:
        key = mo.shape_key_add(name=k, from_mix=False)
        for i, p in enumerate(verts(k)):
            key.data[i].co = p
    return mo


# expression recipe: eyes / brows / mustache
RECIPE = {
    "Happy":     dict(eye=dict(sz=0.5, dz=0.004, arch=0.35), brow=dict(dz=0.028, tilt=0.0), must=dict(curve=0.028)),
    "Angry":     dict(eye=dict(sz=0.75, dz=-0.004), brow=dict(dz=-0.02, tilt=-0.6), must=dict(curve=-0.012, sz=0.85)),
    "Sad":       dict(eye=dict(sz=0.8, dz=-0.008, arch=-0.12), brow=dict(dz=0.006, tilt=0.5), must=dict(curve=-0.03)),
    "Surprised": dict(eye=dict(sz=1.25, sx=1.1), brow=dict(dz=0.05, tilt=0.0), must=dict(dz=0.008)),
}


def expressions(token, label, eye_z, x_lim, white_mustache, glasses=False):
    o = load(token)
    v = coords(o)
    col = vertex_colors(o)
    eyes, brows, must = find_face(v, col, eye_z, x_lim, white_mustache, glasses)
    for nm, f in [("eyeL", eyes[0]), ("eyeR", eyes[1]), ("browL", brows[0]), ("browR", brows[1]), ("mustache", must)]:
        print("INFO %s %s c=(%.3f,%.3f,%.3f) r=(%.3f,%.3f)" % (label, nm, f.c[0], f.c[1], f.c[2], f.r[0], f.r[2]))
    # props that must keep their shape: gold monocle / chain, and dark glasses frames
    lum = col @ np.array([0.3, 0.59, 0.11])
    gold = (col[:, 1] > 0.5) & (col[:, 0] - col[:, 1] < 0.16) & (col[:, 1] - col[:, 2] > 0.25)  # yellow gold, not orange cookie skin
    gold &= v[:, 1] < min(e.yfront for e in eyes) - 0.004     # the monocle ring in front of the face, not gold paint on the skin
    protect = gold | ((lum < 0.12) & glasses & ~np.any([e.weight(v, grow=0.01) > 0.5 for e in eyes], axis=0))
    keep = 1.0 - protect.astype(float)
    o.shape_key_add(name="Basis")
    for k, rc in RECIPE.items():
        d = sum(eye_shape(v, e, **rc["eye"]) for e in eyes) + sum(brow_shape(v, b, **rc["brow"]) for b in brows) \
            + mustache_shape(v, must, **rc["must"])
        add_key(o, k, v + d * keep[:, None])
    W = must.r[0] * 1.1
    mouth_z = must.c[2] - must.r[2] + (0.03 if white_mustache else -0.022)  # a big white mustache covers the chin: sit on its lower edge
    mouth = build_mouth(o, (0.0, must.c[1], mouth_z), min(W, 0.2))
    top = max(b.c[2] + b.r[2] for b in brows) + 0.06
    bottom = must.c[2] - must.r[2] - 0.08
    center = (0, 0, (top + bottom) / 2)
    scale = (top - bottom) * 1.25
    for k in EXPR:
        for kb in list(o.data.shape_keys.key_blocks)[1:] + list(mouth.data.shape_keys.key_blocks)[1:]:
            kb.value = 1.0 if kb.name == k else 0.0
        shoot(os.path.join(OUT, "%s_%s.png" % (label, k)), center, scale)
    for kb in list(o.data.shape_keys.key_blocks)[1:] + list(mouth.data.shape_keys.key_blocks)[1:]:
        kb.value = 0.0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, label + "_Expressions.blend"))


import sys
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
job = argv[0] if argv else "all"
if job in ("all", "tail"):
    fin_tail()
if job == "probe":
    for tok, ez in (("Crumblesworth", 0.2), ("Macaron", 0.16)):
        o = load(tok); v = coords(o); col = vertex_colors(o)
        lum = col @ np.array([0.3, 0.59, 0.11])
        box = (np.abs(v[:, 0]) < 0.3) & (np.abs(v[:, 2] - ez) < 0.13)
        print("INFO", tok, "ymin=%.3f p30=%.3f" % (v[:, 1].min(), np.percentile(v[:, 1], 30)), "box n=%d" % box.sum(),
              "lum pct", np.round(np.percentile(lum[box], [1, 5, 10, 25, 50]), 3), "y of darkest", np.round(v[box][np.argsort(lum[box])[:5], 1], 3))
if job in ("all", "face", "count"):
    expressions("Crumblesworth", "MontBlancCount", 0.20, 0.25, False)
if job in ("all", "face"):
    expressions("Macaron", "MacaronGrandpa", 0.16, 0.30, True, glasses=True)
print("SHAPE_OK")
