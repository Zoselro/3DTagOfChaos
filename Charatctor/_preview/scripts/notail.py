# notail.py -- remove the bungeoppang boy's tail and close the hole on his back (original FBX untouched)
import bpy, bmesh, os, sys, mathutils
sys.argv = ["x", "--", "none"]
exec(open(os.path.join(os.path.dirname(__file__), "shape.py"), encoding="utf-8").read().split("import sys\nargv")[0])

src = find_fbx("Sailor")
out_fbx = os.path.splitext(src)[0] + "_NoTail.fbx"
o = load("Sailor")
shoot(os.path.join(OUT, "notail_before_back.png"), (0, 0, -0.4), 1.3, (0, 1, 0))
shoot(os.path.join(OUT, "notail_before_side.png"), (0, 0.15, -0.4), 1.3, (1, 0, 0))

v0 = coords(o); col = vertex_colors(o)
# golden-brown tail colour on the back of the shirt / shorts (the shirt is white, the shorts blue) or behind the legs
golden = (col[:, 0] > 0.5) & (col[:, 1] > 0.35) & (col[:, 1] < 0.78) & (col[:, 0] - col[:, 2] > 0.22)
warm = (col[:, 0] > col[:, 2] + 0.12) & (col[:, 0] > 0.3)   # on the torso the shirt is white and shorts / scarf blue: warm = tail
torso = (np.abs(v0[:, 0]) < 0.25) & (v0[:, 2] < -0.13) & (v0[:, 2] > -0.6) & (v0[:, 1] > -0.15)  # also the tail root pushed inside the body (front buttons are further forward)
stub = (warm & torso) | (golden & (np.abs(v0[:, 0]) < 0.22) & (v0[:, 2] <= -0.6) & (v0[:, 1] > -0.015))
print("INFO golden stub verts", int(stub.sum()))
bm = bmesh.new(); bm.from_mesh(o.data)
uv = bm.loops.layers.uv.active
bm.verts.ensure_lookup_table()
orig = bm.verts.layers.int.new("orig")
for v in bm.verts: v[orig] = v.index
# tail = everything behind the back surface (body back is at y <= 0.045) below the head + the tail-coloured stub fused to the back
tail = [v for v in bm.verts if (v.co.y > 0.055 and v.co.z < -0.13 and abs(v.co.x) < 0.4) or stub[v.index]]
print("INFO tail verts", len(tail))
bmesh.ops.delete(bm, geom=tail, context="VERTS")
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context="VERTS")
# small floating pieces left from the tail (islands far smaller than the body)
seen, islands = set(), []
for v in bm.verts:
    if v in seen: continue
    stack, isl = [v], []
    seen.add(v)
    while stack:
        a = stack.pop(); isl.append(a)
        for e in a.link_edges:
            b = e.other_vert(a)
            if b not in seen: seen.add(b); stack.append(b)
    islands.append(isl)
islands.sort(key=len, reverse=True)
small = [v for isl in islands[1:] if len(isl) < 300 and all(p.co.y > 0.0 and p.co.z < -0.1 for p in isl) for v in isl]
if small: bmesh.ops.delete(bm, geom=small, context="VERTS")
print("INFO removed islands verts", len(small))

# close the holes on the back: fill boundary loops, give the new faces the UVs those vertices already have
edges = [e for e in bm.edges if e.is_boundary and e.verts[0].co.z < -0.1 and e.verts[0].co.y > -0.16 and abs(e.verts[0].co.x) < 0.3]
before = set(bm.faces)
bmesh.ops.holes_fill(bm, edges=edges, sides=0)
new_faces = [f for f in bm.faces if f not in before]
for f in new_faces:
    for l in f.loops:
        src_loop = next((x for x in l.vert.link_loops if x.face not in new_faces), None)
        if src_loop is not None: l[uv].uv = src_loop[uv].uv
bmesh.ops.triangulate(bm, faces=new_faces)
patch = {v for f in bm.faces if f not in before for v in f.verts}
ring = set(patch)
for _ in range(3):
    for v in list(ring):
        for e in v.link_edges: ring.add(e.other_vert(v))
# recolour per loop: any face corner on the back whose texel is tail-golden takes the UV of the nearest shirt / shorts corner
mat = o.data.materials[0]
img = next(n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image)
W, H = img.size
px = np.empty(W * H * 4, dtype=np.float32); img.pixels.foreach_get(px); px = px.reshape(H, W, 4)
def texel(u):
    c = px[min(H - 1, max(0, int((u.y % 1.0) * H))), min(W - 1, max(0, int((u.x % 1.0) * W)))]
    return c[0] > c[2] + 0.12 and c[0] > 0.3
def on_back(co): return co.y > -0.08 and -0.6 < co.z < -0.13 and abs(co.x) < 0.27   # torso only: legs keep their own colour
from mathutils import kdtree
good = [l for f in bm.faces if on_back(f.calc_center_median()) for l in f.loops if not texel(l[uv].uv)]
kd = kdtree.KDTree(len(good))
def key(co): return mathutils.Vector((co.x, co.y, co.z * 6.0))  # same height first: shirt above the hem, shorts below
for i, l in enumerate(good): kd.insert(key(l.vert.co), i)
kd.balance()
recoloured = 0
filled = {f for f in bm.faces if f not in before}
for f in bm.faces:
    c = f.calc_center_median()
    if not on_back(c): continue
    mid = sum((l[uv].uv for l in f.loops), mathutils.Vector((0.0, 0.0))) / len(f.loops)
    if f in filled or texel(mid) or any(texel(l[uv].uv) for l in f.loops):
        _, i, _ = kd.find(key(c))
        target = good[i][uv].uv.copy()
        for l in f.loops: l[uv].uv = target      # one texel per face: no stretching across texture islands
        recoloured += 1
print("INFO recoloured faces", recoloured)
for _ in range(10):
    bmesh.ops.smooth_vert(bm, verts=list(ring), factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
print("INFO filled faces", len(new_faces), "boundary edges", len(edges))
bm.normal_update(); bm.to_mesh(o.data); bm.free(); o.data.update()

v = coords(o)
print("INFO new max y below head %.3f" % v[v[:, 2] < -0.13][:, 1].max())
shoot(os.path.join(OUT, "notail_after_back.png"), (0, 0, -0.4), 1.3, (0, 1, 0))
shoot(os.path.join(OUT, "notail_after_side.png"), (0, 0.15, -0.4), 1.3, (1, 0, 0))
shoot(os.path.join(OUT, "notail_after_full.png"), (0, 0, 0), 2.2, (0.7, 1, 0.15))
bpy.ops.export_scene.fbx(filepath=out_fbx, path_mode="COPY", embed_textures=True)
print("INFO exported", os.path.basename(out_fbx), os.path.getsize(out_fbx) // 1024, "KB")
print("NOTAIL_OK")
