import bpy, os, sys
sys.argv = ["x", "--", "none"]
exec(open(os.path.join(os.path.dirname(__file__), "shape.py"), encoding="utf-8").read().split("import sys\nargv")[0])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.splitext(find_fbx("Sailor"))[0].replace("_NoTail", "") + "_NoTail.fbx")
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"; sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
sc.display.shading.show_backface_culling = True
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.18, 0.18, 0.2)
shoot(os.path.join(OUT, "notail_close_cull.png"), (0, 0, -0.35), 0.55, (0, 1, 0), res=500)
sc.display.shading.show_backface_culling = False
shoot(os.path.join(OUT, "notail_close.png"), (0, 0, -0.35), 0.55, (0, 1, 0), res=500)
print("CLOSE_OK")
