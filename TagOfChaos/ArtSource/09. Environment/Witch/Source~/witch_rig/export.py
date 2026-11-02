import bpy, sys
args = sys.argv[sys.argv.index("--") + 1:]
bpy.ops.wm.open_mainfile(filepath=args[0])
rig = bpy.data.objects["WitchRig"]; body = bpy.data.objects["Witch_Body"]
body.data.materials[0].name = "ME_WitchScarecrow"
# 각 동작을 NLA 트랙으로 — FBX의 take 이름이 동작 이름이 된다
rig.animation_data.action = None
for tr in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(tr)
for name in ("Idle", "Rise", "Strike", "Walk", "TurnBack"):
    tr = rig.animation_data.nla_tracks.new(); tr.name = name
    tr.strips.new(name, int(bpy.data.actions[name].frame_range[0]), bpy.data.actions[name])
bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); body.select_set(True)
bpy.ops.export_scene.fbx(filepath=args[1], use_selection=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_force_startend_keying=True,
    bake_anim_simplify_factor=0.0, mesh_smooth_type='FACE', use_armature_deform_only=False, path_mode='STRIP', embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=args[2])
print("exported")
