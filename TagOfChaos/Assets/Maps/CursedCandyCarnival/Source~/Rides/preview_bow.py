# Bow close-up + ship 3/4 of the saved viking blend (checks the figurehead against the reference).
import bpy, sys, os, math
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.open_mainfile(filepath=os.path.abspath(argv[0]))
import ride_common as rc
rc.scene = bpy.context.scene
ship = bpy.data.objects["VK_Ship"]; ship.animation_data_clear(); ship.rotation_euler = (0, 0, 0)
allp = [o for o in bpy.data.objects if o.name.startswith("VK_")]
st = rc.Stage(os.path.abspath(argv[1]), allp, samples=40)
for o in allp: o.hide_render = o is not ship
bpy.context.view_layer.update()
pz = 14.7 - 9.5
target = Vector((8.0, 0, pz + 1.9))
for name, cam_pos in (("vk_bow_front", Vector((15.5, -7.5, pz + 3.5))), ("vk_bow_side", Vector((6.0, -10.0, pz + 2.5)))):
    st.cam.location = cam_pos
    st.cam.rotation_euler = (target - cam_pos).to_track_quat("-Z", "Y").to_euler()
    st.cam_data.lens = 50
    st.ground.location.z = pz - 2.5
    rc.scene.render.resolution_x, rc.scene.render.resolution_y = 1000, 850
    rc.scene.render.filepath = os.path.join(os.path.abspath(argv[1]), name + ".png")
    bpy.ops.render.render(write_still=True)
    print("rendered", name, flush=True)
st.shoot("vk_part_ship", [ship], 18, 35)
