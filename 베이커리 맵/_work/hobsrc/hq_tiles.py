import sys, os, json, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, numpy as np
import hqmat
# bake every HQ recipe into a seamless tile (vertical XZ plane: u = X, v = Z, size TILE metres) for the Unity HOB/TriPBR shader
OUT = sys.argv[sys.argv.index("--")+1]; RES = int(sys.argv[sys.argv.index("--")+2]); only = sys.argv[sys.argv.index("--")+3].split(",") if len(sys.argv) > sys.argv.index("--")+3 else None
TILE_M = {'Rock': 3.0, 'RockDark': 3.0, 'StoneGrey': 3.0, 'Grass': 2.0, 'Moss': 2.0, 'PinkStone': 2.5, 'StoneTile': 1.0, 'Foliage': 2.0, 'Blossom': 1.5,
          'Bark': 1.5, 'BarkDark': 1.5, 'Plank': 1.5, 'PlankDark': 1.5, 'Wood': 1.5, 'DarkWood': 1.5}
bpy.ops.wm.read_factory_settings(use_empty=True)
hqmat.TILE[0] = True
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 4; sc.render.bake.margin = 0
def pix(im): a = np.empty(len(im.pixels), dtype=np.float32); im.pixels.foreach_get(a); return a.reshape(im.size[1], im.size[0], 4)
def seamless(a):
    n = a.shape[0]; b = np.roll(np.roll(a, n // 2, 0), n // 2, 1)
    y, x = np.mgrid[0:n, 0:n]; d = np.minimum(np.minimum(x, n - 1 - x), np.minimum(y, n - 1 - y)) / (n * 0.22)
    w = np.clip(1.0 - d, 0, 1)[..., None] ** 1.5
    return a * (1 - w) + b * w
meta = {}
os.makedirs(OUT, exist_ok=True)
names = only or sorted(hqmat.R.keys())
for name in names:
    T = TILE_M.get(name, 1.0)
    bpy.ops.mesh.primitive_plane_add(size=T, location=(T / 2, 0, T / 2), rotation=(math.pi / 2, 0, 0))
    o = bpy.context.active_object; o.name = "tile_" + name
    m = hqmat.make(name); o.data.materials.append(m)
    b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'); metal = b.inputs['Metallic'].default_value
    b.inputs['Metallic'].default_value = 0.0
    node = m.node_tree.nodes.new('ShaderNodeTexImage'); m.node_tree.nodes.active = node
    res = {}
    for typ, kw, nc in (('DIFFUSE', {'pass_filter': {'COLOR'}}, False), ('NORMAL', {'normal_space': 'TANGENT'}, True), ('ROUGHNESS', {}, True)):
        im = bpy.data.images.new(f"{name}_{typ}", RES, RES, alpha=True)
        if nc: im.colorspace_settings.name = 'Non-Color'
        node.image = im; bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.bake(type=typ, **kw); res[typ] = seamless(pix(im))
    alb = res['DIFFUSE'].copy(); alb[..., 3] = 1.0 - res['ROUGHNESS'][..., 0]
    nrm = res['NORMAL'].copy(); v = nrm[..., :3] * 2 - 1; v /= np.linalg.norm(v, axis=-1, keepdims=True) + 1e-6; nrm[..., :3] = v * 0.5 + 0.5; nrm[..., 3] = 1
    for arr, suf, nc in ((alb, "Albedo", False), (nrm, "Normal", True)):
        im = bpy.data.images.new(f"{name}_{suf}", RES, RES, alpha=True)
        if nc: im.colorspace_settings.name = 'Non-Color'
        im.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel()); im.filepath_raw = os.path.abspath(os.path.join(OUT, f"HQ_{name}_{suf}.png")); im.file_format = 'PNG'; im.save()
    emis = tuple(b.inputs['Emission Color'].default_value)[:3] if m.get("hq_emissive") else None
    meta[name] = {"tile": T, "metal": metal, "emission": emis}
    bpy.data.objects.remove(o)
    print("TILE", name, T, metal, flush=True)
old = json.load(open(os.path.join(OUT, "tiles.json"))) if os.path.exists(os.path.join(OUT, "tiles.json")) else {}
old.update(meta)                                                                   # partial runs add to the existing table
json.dump(old, open(os.path.join(OUT, "tiles.json"), "w"), indent=1)
