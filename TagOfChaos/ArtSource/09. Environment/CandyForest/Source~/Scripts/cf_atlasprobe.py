# Atlas share per piece (2026-10-21): how much of the shared texture each split piece's UVs cover, and where.
# blender -b --factory-startup --python cf_atlasprobe.py -- <Source~ dir> <Set> [<Set> ...]
import bpy, sys, os
argv = sys.argv[sys.argv.index("--") + 1:]
SPLIT = os.path.join(argv[0], "_split")
out = open(os.path.join(SPLIT, "atlasprobe.txt"), "w")
for setname in argv[1:]:
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SPLIT, setname + ".blend"))
    for o in sorted((o for o in bpy.data.objects if o.type == "MESH"), key=lambda o: o.name):
        me = o.data
        uv = me.uv_layers.active.data
        area, us, vs = 0.0, [], []
        for p in me.polygons:
            pts = [uv[li].uv.copy() for li in p.loop_indices]
            us += [q.x for q in pts]; vs += [q.y for q in pts]
            for k in range(1, len(pts) - 1):
                a, b, c = pts[0], pts[k], pts[k + 1]
                area += abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2
        imgs = sorted({n.image.name + " %dx%d" % tuple(n.image.size) for m in me.materials if m and m.use_nodes
                       for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image})
        out.write("%s tris=%d uv_area=%.3f bbox u %.2f..%.2f v %.2f..%.2f images=%s\n" % (
            o.name, sum(len(p.vertices) - 2 for p in me.polygons), area, min(us), max(us), min(vs), max(vs), imgs))
out.write("DONE\n")
out.close()
