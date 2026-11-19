import bpy, math, random
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
# per-vertex ambient occlusion by BVH ray casts (robust for thin parts and props pushed into each other) + soft sky term (down-facing a bit darker)
def _dirs(n=28, seed=3):
    rng = random.Random(seed); out = []
    for i in range(n):                                                        # cosine-weighted hemisphere around +Z (stratified)
        u = (i + rng.random()) / n; v = rng.random()
        r = math.sqrt(u); a = 2 * math.pi * v
        out.append(Vector((r * math.cos(a), r * math.sin(a), math.sqrt(max(0.0, 1 - u)))))
    return out
def vertex_ao(o, dist=0.5, rays=28, strength=0.9, sky=0.25, eps=None):
    me = o.data; me.calc_loop_triangles()
    tree = BVHTree.FromPolygons([v.co for v in me.vertices], [p.vertices for p in me.polygons], all_triangles=False, epsilon=0.0)
    dirs = _dirs(rays)
    size = max(o.dimensions) if max(o.dimensions) > 0 else 1.0
    eps = eps or size * 0.0025
    # corner normals (smooth-by-angle aware) — AO per corner, averaged per (vertex, normal) pair
    me.update()
    cn = [Vector(c.vector) for c in me.corner_normals]
    cache = {}; ao_loop = np.ones(len(me.loops), dtype=np.float32)
    for li, loop in enumerate(me.loops):
        n = cn[li]; key = (loop.vertex_index, round(n.x, 2), round(n.y, 2), round(n.z, 2))
        if key in cache: ao_loop[li] = cache[key]; continue
        p = me.vertices[loop.vertex_index].co + n * eps
        q = n.to_track_quat('Z', 'Y')
        occ = 0.0
        for d in dirs:
            w = q @ d
            hit, nrm, idx, dd = tree.ray_cast(p, w, dist)
            if hit is not None: occ += (1.0 - dd / dist) ** 0.5
        a = (1.0 - strength * occ / len(dirs)) ** 0.6
        a *= 1.0 - sky * (1.0 - (n.z * 0.5 + 0.5))                         # sky light: down-facing faces a little darker
        cache[key] = a; ao_loop[li] = a
    for c in list(me.color_attributes): me.color_attributes.remove(c)
    ca = me.color_attributes.new("Col", 'BYTE_COLOR', 'CORNER')
    cols = np.repeat(np.clip(ao_loop, 0, 1)[:, None], 4, axis=1); cols[:, 3] = 1.0
    ca.data.foreach_set("color", cols.ravel())
    return float(ao_loop.mean())
