import bpy, os, math
import numpy as np
import hqmat
# ===== bake HQ procedural materials of one joined model into one texture set (Unity Standard: Albedo · MetallicSmoothness · Normal · Emission) =====

def _img(name, res, non_color=False, alpha=True):
    if name in bpy.data.images: bpy.data.images.remove(bpy.data.images[name])
    im = bpy.data.images.new(name, res, res, alpha=alpha, float_buffer=False)
    if non_color: im.colorspace_settings.name = 'Non-Color'
    return im

def _pix(im): a = np.empty(len(im.pixels), dtype=np.float32); im.pixels.foreach_get(a); return a.reshape(im.size[1], im.size[0], 4)

def _set(im, arr): im.pixels.foreach_set(arr.astype(np.float32).ravel()); im.update()

def unwrap(o, margin=0.004):
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    me = o.data
    while me.uv_layers: me.uv_layers.remove(me.uv_layers[0])
    me.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=margin, area_weight=0.6, correct_aspect=True, scale_to_bounds=False)
    try: bpy.ops.uv.pack_islands(rotate=True, margin=margin)
    except Exception: pass
    bpy.ops.object.mode_set(mode='OBJECT')

def hq_materials(o):
    """swap lib.mat() materials (HOB_<name>) for the HQ procedural recipe of the same name"""
    me = o.data
    for i, m in enumerate(me.materials):
        nm = m.name.split("HOB_", 1)[-1].split(".")[0]
        me.materials[i] = hqmat.make(nm)

def _bake_into(o, img, typ, **kw):
    for m in o.data.materials:
        nt = m.node_tree; node = nt.nodes.get("BAKE_TARGET") or nt.nodes.new('ShaderNodeTexImage'); node.name = "BAKE_TARGET"
        node.image = img; nt.nodes.active = node; node.select = True
    bpy.ops.object.bake(type=typ, **kw)

def bake(o, outdir, res=1024, ao_dist=0.6, samples=12, ao_strength=0.55):
    sc = bpy.context.scene
    try: sc.render.engine = 'CYCLES'
    except TypeError: pass
    sc.cycles.device = 'CPU'; sc.cycles.samples = samples; sc.render.bake.margin = 6; sc.render.bake.use_clear = True
    if sc.world is None: sc.world = bpy.data.worlds.new("W")
    sc.world.light_settings.distance = ao_dist
    name = o.name
    hq_materials(o); unwrap(o)
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
    bsdfs = [(m, next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')) for m in o.data.materials]
    metal = [b.inputs['Metallic'].default_value for m, b in bsdfs]
    # albedo (metallic off so the diffuse colour pass is the base colour)
    for (m, b) in bsdfs: b.inputs['Metallic'].default_value = 0.0
    alb = _img(name + "_alb", res); _bake_into(o, alb, 'DIFFUSE', pass_filter={'COLOR'})
    nrm = _img(name + "_nrm", res, non_color=True); _bake_into(o, nrm, 'NORMAL', normal_space='TANGENT')
    rgh = _img(name + "_rgh", res, non_color=True); _bake_into(o, rgh, 'ROUGHNESS')
    ao = _img(name + "_ao", res, non_color=True); _bake_into(o, ao, 'AO')
    # metal mask: emission = metallic per material, bake EMIT; then the real emission
    saved = []
    for (m, b), mt in zip(bsdfs, metal):
        saved.append((tuple(b.inputs['Emission Color'].default_value), b.inputs['Emission Strength'].default_value,
                      [l.from_socket for l in b.inputs['Emission Color'].links]))
        for l in list(b.inputs['Emission Color'].links): m.node_tree.links.remove(l)
        b.inputs['Emission Color'].default_value = (mt, mt, mt, 1); b.inputs['Emission Strength'].default_value = 1.0
    met = _img(name + "_met", res, non_color=True); _bake_into(o, met, 'EMIT')
    for (m, b), (c, s, src) in zip(bsdfs, saved):
        b.inputs['Emission Color'].default_value = c; b.inputs['Emission Strength'].default_value = s
        for so in src: m.node_tree.links.new(so, b.inputs['Emission Color'])
    has_emit = any(m.get("hq_emissive") for m, b in bsdfs)
    emi = None
    if has_emit:
        emi = _img(name + "_emi", res); _bake_into(o, emi, 'EMIT')
    for (m, b), mt in zip(bsdfs, metal): b.inputs['Metallic'].default_value = mt
    # combine: albedo × soft AO, metal R + smoothness A
    A = _pix(alb); O = _pix(ao)[..., :1]; Rg = _pix(rgh)[..., :1]; M = _pix(met)[..., :1]
    A[..., :3] *= (1.0 - ao_strength * (1.0 - O)); A[..., 3] = 1.0
    MS = np.concatenate([M, M, M, 1.0 - Rg], axis=-1)
    _set(alb, A)
    msi = _img(name + "_ms", res, non_color=True); _set(msi, MS)
    os.makedirs(outdir, exist_ok=True)
    files = {}
    for im, suf in ((alb, "Albedo"), (msi, "MetalSmooth"), (nrm, "Normal")) + (((emi, "Emission"),) if emi else ()):
        p = os.path.abspath(os.path.join(outdir, f"{name}_{suf}.png")); im.filepath_raw = p; im.file_format = 'PNG'; im.save(); files[suf] = p
    # single preview material on the model (Unity rebuilds it from the PNGs)
    pm = bpy.data.materials.new("M_" + name); pm.use_nodes = True
    nt = pm.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    ta = nt.nodes.new('ShaderNodeTexImage'); ta.image = alb; nt.links.new(ta.outputs['Color'], b.inputs['Base Color'])
    tm = nt.nodes.new('ShaderNodeTexImage'); tm.image = msi
    sp = nt.nodes.new('ShaderNodeSeparateColor'); nt.links.new(tm.outputs['Color'], sp.inputs['Color']); nt.links.new(sp.outputs['Red'], b.inputs['Metallic'])
    inv = nt.nodes.new('ShaderNodeMath'); inv.operation = 'SUBTRACT'; inv.inputs[0].default_value = 1.0
    nt.links.new(tm.outputs['Alpha'], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs['Roughness'])
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = nrm
    nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(tn.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], b.inputs['Normal'])
    if emi:
        te = nt.nodes.new('ShaderNodeTexImage'); te.image = emi; nt.links.new(te.outputs['Color'], b.inputs['Emission Color']); b.inputs['Emission Strength'].default_value = 1.0
    o.data.materials.clear(); o.data.materials.append(pm)
    return files
