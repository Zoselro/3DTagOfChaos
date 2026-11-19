import bpy, math
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3)
o=bpy.context.active_object
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004)
try:
    bpy.ops.uv.pack_islands(rotate=True, margin=0.004); print("pack ok")
except Exception as e: print("ERR", e)
print([p.identifier for p in bpy.ops.uv.pack_islands.get_rna_type().properties])
