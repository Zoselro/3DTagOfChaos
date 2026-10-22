import bpy, sys, json, os
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=a[0])
out = {}
used = {s.material.name for o in bpy.data.objects if o.type == "MESH" for s in o.material_slots if s.material}
for m in bpy.data.materials:
    if m.name not in used or not m.use_nodes: continue
    b = m.node_tree.nodes.get("Principled BSDF")
    tex = None
    for l in m.node_tree.links:
        if l.to_node == b and l.to_socket.name == "Base Color" and l.from_node.type == "TEX_IMAGE":
            tex = os.path.basename(l.from_node.image.filepath)
    out[m.name] = dict(tex=tex, color=list(b.inputs["Base Color"].default_value)[:3], metallic=b.inputs["Metallic"].default_value,
                       rough=b.inputs["Roughness"].default_value, emission=list(b.inputs["Emission Color"].default_value)[:3],
                       strength=b.inputs["Emission Strength"].default_value)
json.dump(out, open(a[1], "w"), indent=1)
print("MATS", len(out), sorted(out))
