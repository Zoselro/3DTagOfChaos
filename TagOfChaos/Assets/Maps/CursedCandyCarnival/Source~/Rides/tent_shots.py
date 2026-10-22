# extra tent pictures: front door, back door, interior through the door, top cut (roof hidden), with 2 m mannequin
import bpy, sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ride_common as rc
from mathutils import Vector
a = sys.argv[sys.argv.index("--")+1:]
bpy.ops.wm.open_mainfile(filepath=os.path.abspath(a[0])); rc.scene = bpy.context.scene
out = os.path.abspath(a[1])
keep = {"MO_Tent"}
for o in bpy.data.objects:
    if o.type == "MESH" and o.name not in keep and o.parent is None: o.hide_render = True
tent = bpy.data.objects["MO_Tent"]; tent.location = (0, 0, 0); tent.rotation_euler = (0, 0, 0)
for c in tent.children: c.hide_render = True
st = rc.Stage(out, [tent], samples=40)
# mannequins: one walking in the front door, one inside
def man(x, y):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.46, depth=1.4, location=(x, y, 0.9)); b = rc.active()
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.38, location=(x, y, 1.9)); h = rc.active()
    m = bpy.data.materials.new("Man"); m.use_nodes = True; m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.75, 0.75, 0.8, 1)
    for o in (b, h): o.data.materials.append(m)
    return [b, h]
men = man(0, -4.6) + man(-0.6, 0.6)
st.all = [tent] + men
vis = [tent] + men
st.shoot("tent_front", vis, 8, 0, res=(1200, 1000), fill=1.1)
st.shoot("tent_back", vis, 8, 180, res=(1200, 1000), fill=1.1)
st.shoot("tent_34", vis, 18, 35, res=(1200, 1000), fill=1.1)
# close-up of the torn door
cam = st.cam; st.cam_data.lens = 35
cam.location = (2.2, -9.5, 2.0); cam.rotation_euler = (Vector((0, -4.0, 1.6)) - cam.location).to_track_quat("-Z", "Y").to_euler()
for o in st.all: o.hide_render = o not in vis
rc.scene.render.resolution_x, rc.scene.render.resolution_y = 1200, 900
rc.scene.render.filepath = os.path.join(out, "tent_door_close.png"); bpy.ops.render.render(write_still=True)
# interior through the front door
cam.location = (0.3, -6.2, 1.5); cam.rotation_euler = (Vector((0, 2, 1.2)) - cam.location).to_track_quat("-Z", "Y").to_euler()
st.cam_data.lens = 24
rc.scene.render.filepath = os.path.join(out, "tent_inside.png"); bpy.ops.render.render(write_still=True)
# cutaway plan: camera high above, clip start just below the wall top -> roof and lantern are cut away
cam.location = (0, -2.0, 30.0); st.cam_data.lens = 50; st.cam_data.clip_start = 30.0 - 2.9; st.cam_data.clip_end = 80
cam.rotation_euler = (Vector((0, 0, 0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
rc.scene.render.resolution_x, rc.scene.render.resolution_y = 1100, 1100
rc.scene.render.filepath = os.path.join(out, "tent_cutaway.png"); bpy.ops.render.render(write_still=True)
print("TENT SHOTS DONE")
