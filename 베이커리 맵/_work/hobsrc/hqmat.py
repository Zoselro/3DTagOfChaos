import bpy, math
# ===== HOB HQ procedural materials (baked to textures per model by hqbake.py) =====
# every recipe: base colour field (+ pattern), roughness, metallic, bump height. Coordinates = Object space (metres) * scale.

def _hex(c): return (*c, 1.0)

class NB:
    def __init__(self, m):
        self.m = m; self.t = m.node_tree; self.n = self.t.nodes; self.l = self.t.links; self.x = -1600
    def add(self, typ, **kw):
        node = self.n.new(typ); node.location = (self.x, 0); self.x += 180
        for k, v in kw.items():
            if k.startswith("i_"):
                key = k[2:].replace("_", " ")
                node.inputs[key].default_value = v
            else: setattr(node, k, v)
        return node
    def link(self, a, b): self.l.new(a, b)

def _coords(nb, scale, distort=0.0):
    tc = nb.add('ShaderNodeTexCoord')
    mp = nb.add('ShaderNodeMapping'); mp.inputs['Scale'].default_value = (scale, scale, scale)
    nb.link(tc.outputs['Object'], mp.inputs['Vector'])
    return mp.outputs['Vector'], tc

def _ramp(nb, fac, stops):
    r = nb.add('ShaderNodeValToRGB'); cr = r.color_ramp
    while len(cr.elements) > 2: cr.elements.remove(cr.elements[-1])
    for i, (p, c) in enumerate(stops):
        e = cr.elements[i] if i < 2 else cr.elements.new(p)
        e.position = p; e.color = _hex(c)
    nb.link(fac, r.inputs['Fac']); return r.outputs['Color']

def _noise(nb, vec, scale, detail=6.0, rough=0.55, dist=0.0):
    t = nb.add('ShaderNodeTexNoise'); t.inputs['Scale'].default_value = scale; t.inputs['Detail'].default_value = detail
    t.inputs['Roughness'].default_value = rough; t.inputs['Distortion'].default_value = dist
    nb.link(vec, t.inputs['Vector']); return t

def _mix(nb, a, b, fac, blend='MIX'):
    mx = nb.add('ShaderNodeMix'); mx.data_type = 'RGBA'; mx.blend_type = blend
    if isinstance(fac, float): mx.inputs['Factor'].default_value = fac
    else: nb.link(fac, mx.inputs['Factor'])
    for sock, v in ((mx.inputs[6], a), (mx.inputs[7], b)):
        if isinstance(v, tuple): sock.default_value = _hex(v)
        else: nb.link(v, sock)
    return mx.outputs[2]

def _math(nb, op, a, b=None, clamp=False):
    m = nb.add('ShaderNodeMath'); m.operation = op; m.use_clamp = clamp
    for i, v in enumerate((a, b)):
        if v is None: continue
        if isinstance(v, (int, float)): m.inputs[i].default_value = v
        else: nb.link(v, m.inputs[i])
    return m.outputs[0]

def _sep_z(nb, tc_obj):
    s = nb.add('ShaderNodeSeparateXYZ'); nb.link(tc_obj, s.inputs['Vector']); return s

TILE = [False]
PREVIEW = [False]
R = {}   # name -> builder(nb, bsdf) returning (color_socket, rough_socket or float, bump_height_socket or None, bump_strength)

def recipe(name):
    def deco(fn): R[name] = fn; return fn
    return deco

def _top_moss(nb, color, moss=(0.36, 0.55, 0.18), amount=0.7, vec=None):
    """mix moss onto upward faces with a noisy edge (in Unity the HOB/TriPBR shader does this — off when baking tiles)"""
    if TILE[0]: return color, None
    geo = nb.add('ShaderNodeNewGeometry')
    sep = nb.add('ShaderNodeSeparateXYZ'); nb.link(geo.outputs['Normal'], sep.inputs['Vector'])
    nz = _noise(nb, vec if vec is not None else geo.outputs['Position'], 2.5, 4)
    edge = _math(nb, 'ADD', sep.outputs['Z'], _math(nb, 'MULTIPLY', nz.outputs['Fac'], 0.6))
    f = _math(nb, 'MULTIPLY', _math(nb, 'SUBTRACT', edge, 0.95), 4.0 * amount, clamp=True)
    mossc = _ramp(nb, nz.outputs['Fac'], [(0.3, tuple(c * 0.7 for c in moss)), (0.7, moss)])
    return _mix(nb, color, mossc, f), f

# ---- simple painted (noise variation) ----
def simple(base, var=0.12, scale=4.0, rough=0.6, metal=0.0, bump=0.15, bscale=18.0):
    def fn(nb, b):
        vec, tc = _coords(nb, 1.0)
        n = _noise(nb, vec, scale, 5)
        dark = tuple(max(0, c * (1 - var)) for c in base); light = tuple(min(1, c * (1 + var)) for c in base)
        col = _ramp(nb, n.outputs['Fac'], [(0.3, dark), (0.7, light)])
        bn = _noise(nb, vec, bscale, 8)
        return col, rough, bn.outputs['Fac'], bump
    return fn

for nm, c, rg, mt in (('Cream', (0.96, 0.88, 0.74), 0.45, 0), ('Glaze', (0.99, 0.96, 0.92), 0.3, 0), ('Frosting', (0.98, 0.76, 0.84), 0.35, 0),
                      ('Pane', (0.55, 0.78, 0.95), 0.08, 0), ('Stem', (0.40, 0.27, 0.12), 0.7, 0), ('Soil', (0.36, 0.24, 0.14), 0.9, 0),
                      ('Iron', (0.16, 0.12, 0.18), 0.4, 0.6), ('Canvas', (0.96, 0.93, 0.86), 0.8, 0), ('Stripe', (0.82, 0.14, 0.26), 0.7, 0),
                      ('CakeSponge', (0.92, 0.72, 0.44), 0.85, 0), ('WrapperPink', (0.93, 0.50, 0.65), 0.35, 0), ('Choco', (0.30, 0.16, 0.09), 0.25, 0),
                      ('Seed', (0.98, 0.86, 0.42), 0.5, 0), ('Sprinkle1', (0.98, 0.42, 0.62), 0.3, 0), ('Sprinkle2', (0.50, 0.78, 0.98), 0.3, 0),
                      ('Sprinkle3', (0.99, 0.86, 0.35), 0.3, 0), ('Sprinkle4', (0.55, 0.88, 0.50), 0.3, 0), ('OrangeFlesh', (1.0, 0.62, 0.12), 0.35, 0),
                      ('Pith', (0.99, 0.95, 0.85), 0.6, 0), ('Banner', (0.62, 0.06, 0.20), 0.7, 0), ('Grape', (0.36, 0.10, 0.42), 0.25, 0),
                      ('Cherry', (0.70, 0.02, 0.07), 0.15, 0), ('Pumpkin', (0.95, 0.52, 0.16), 0.45, 0), ('PinkWall', (0.95, 0.78, 0.80), 0.7, 0)):
    R[nm] = simple(c, rough=rg, metal=mt)

@recipe('Gold')
def _gold(nb, b):
    vec, tc = _coords(nb, 1.0)
    n = _noise(nb, vec, 9.0, 6)
    col = _ramp(nb, n.outputs['Fac'], [(0.25, (0.62, 0.40, 0.12)), (0.55, (0.93, 0.70, 0.30)), (0.8, (1.0, 0.86, 0.50))])
    b.inputs['Metallic'].default_value = 1.0
    return col, 0.28, _noise(nb, vec, 40.0, 4).outputs['Fac'], 0.05

@recipe('Berry')
def _berry(nb, b):
    """strawberry skin: red gradient (deeper at the tip), voronoi seed cells — yellow seeds in dimples"""
    vec, tc = _coords(nb, 1.0)
    nz = _noise(nb, vec, 1.5, 3)
    grad = _ramp(nb, nz.outputs['Fac'], [(0.3, (0.80, 0.03, 0.06)), (0.7, (0.94, 0.08, 0.09))])
    v = nb.add('ShaderNodeTexVoronoi'); v.feature = 'F1'; v.inputs['Scale'].default_value = 9.0; v.inputs['Randomness'].default_value = 0.45
    nb.link(vec, v.inputs['Vector'])
    seedf = _math(nb, 'LESS_THAN', v.outputs['Distance'], 0.17)
    ring = _math(nb, 'MULTIPLY', _math(nb, 'SUBTRACT', 0.32, v.outputs['Distance'], clamp=True), 3.0, clamp=True)
    col = _mix(nb, grad, (0.98, 0.84, 0.36), seedf)
    col = _mix(nb, col, (0.45, 0.01, 0.04), _math(nb, 'MULTIPLY', ring, _math(nb, 'SUBTRACT', 1.0, seedf), clamp=True), 'MULTIPLY')
    h = _math(nb, 'ADD', _math(nb, 'MULTIPLY', _math(nb, 'MINIMUM', v.outputs['Distance'], 0.3), 2.0), _math(nb, 'MULTIPLY', seedf, 0.5))
    return col, 0.2, h, 0.6

@recipe('Apple')
def _apple(nb, b):
    vec, tc = _coords(nb, 1.0)
    st = nb.add('ShaderNodeMapping'); st.inputs['Scale'].default_value = (6.0, 6.0, 0.8); nb.link(tc.outputs['Object'], st.inputs['Vector'])
    streak = _noise(nb, st.outputs['Vector'], 3.0, 5, 0.6)
    base = _ramp(nb, streak.outputs['Fac'], [(0.35, (0.55, 0.03, 0.05)), (0.6, (0.82, 0.08, 0.07)), (0.85, (0.95, 0.55, 0.15))])
    col = base
    speck = nb.add('ShaderNodeTexVoronoi'); speck.inputs['Scale'].default_value = 60.0; nb.link(vec, speck.inputs['Vector'])
    col = _mix(nb, col, (0.98, 0.85, 0.6), _math(nb, 'LESS_THAN', speck.outputs['Distance'], 0.06))
    return col, 0.25, streak.outputs['Fac'], 0.04

@recipe('OrangePeel')
def _peel(nb, b):
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 28.0; nb.link(vec, v.inputs['Vector'])
    n = _noise(nb, vec, 3.0, 4)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.92, 0.40, 0.04)), (0.7, (1.0, 0.58, 0.10))])
    return col, 0.35, v.outputs['Distance'], 0.25

@recipe('OrangeCut')
def _cut(nb, b):
    vec, tc = _coords(nb, 1.0)
    st = nb.add('ShaderNodeMapping'); st.inputs['Scale'].default_value = (30.0, 30.0, 30.0); nb.link(tc.outputs['Object'], st.inputs['Vector'])
    v = nb.add('ShaderNodeTexVoronoi'); v.feature = 'F1'; v.inputs['Scale'].default_value = 1.0; nb.link(st.outputs['Vector'], v.inputs['Vector'])
    col = _ramp(nb, v.outputs['Distance'], [(0.1, (1.0, 0.75, 0.25)), (0.55, (1.0, 0.55, 0.08)), (0.9, (0.95, 0.42, 0.05))])
    return col, 0.15, v.outputs['Distance'], 0.3

@recipe('Leaf')
def _leaf(nb, b):
    vec, tc = _coords(nb, 1.0)
    n = _noise(nb, vec, 6.0, 5)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.10, 0.32, 0.08)), (0.7, (0.26, 0.54, 0.14))])
    w = nb.add('ShaderNodeTexWave'); w.wave_type = 'BANDS'; w.inputs['Scale'].default_value = 14.0; w.inputs['Distortion'].default_value = 3.0
    nb.link(vec, w.inputs['Vector'])
    col = _mix(nb, col, (0.55, 0.78, 0.30), _math(nb, 'GREATER_THAN', w.outputs['Fac'], 0.93))
    return col, 0.32, w.outputs['Fac'], 0.05

R['LeafLight'] = R['Leaf']

@recipe('Foliage')
def _foliage(nb, b):
    """tree/bush canopy: clustered leaf blobs (voronoi cells), light tips, shadowed gaps"""
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 5.5; nb.link(vec, v.inputs['Vector'])
    n = _noise(nb, vec, 2.0, 4)
    col = _ramp(nb, v.outputs['Distance'], [(0.0, (0.40, 0.66, 0.22)), (0.45, (0.26, 0.50, 0.15)), (0.85, (0.10, 0.27, 0.09))])
    col = _mix(nb, col, _ramp(nb, n.outputs['Fac'], [(0.3, (0.18, 0.40, 0.14)), (0.7, (0.42, 0.62, 0.20))]), 0.35)
    return col, 0.7, _math(nb, 'SUBTRACT', 1.0, v.outputs['Distance']), 0.5

@recipe('Blossom')
def _blossom(nb, b):
    """cherry blossom canopy: dense 5-petal flowers (voronoi cells + petal angle), pink shades, yellow centres"""
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 9.0; v.inputs['Randomness'].default_value = 0.9; nb.link(vec, v.inputs['Vector'])
    pet = nb.add('ShaderNodeTexVoronoi'); pet.feature = 'DISTANCE_TO_EDGE'; pet.inputs['Scale'].default_value = 27.0; nb.link(vec, pet.inputs['Vector'])
    shade = _ramp(nb, v.outputs['Color'], [(0.0, (0.97, 0.70, 0.82)), (0.5, (0.99, 0.82, 0.90)), (1.0, (0.92, 0.55, 0.72))])
    col = _mix(nb, (0.86, 0.48, 0.62), shade, _math(nb, 'MULTIPLY', pet.outputs['Distance'], 8.0, clamp=True))
    col = _mix(nb, col, (0.99, 0.86, 0.35), _math(nb, 'LESS_THAN', v.outputs['Distance'], 0.07))
    return col, 0.6, _math(nb, 'ADD', pet.outputs['Distance'], _math(nb, 'MULTIPLY', v.outputs['Distance'], -0.4)), 0.45

@recipe('Bark')
def _bark(nb, b):
    vec, tc = _coords(nb, 1.0)
    st = nb.add('ShaderNodeMapping'); st.inputs['Scale'].default_value = (5.0, 5.0, 1.2); nb.link(tc.outputs['Object'], st.inputs['Vector'])
    w = nb.add('ShaderNodeTexWave'); w.wave_type = 'BANDS'; w.bands_direction = 'X'; w.inputs['Scale'].default_value = 3.0; w.inputs['Distortion'].default_value = 6.0
    w.inputs['Detail'].default_value = 4.0; nb.link(st.outputs['Vector'], w.inputs['Vector'])
    col = _ramp(nb, w.outputs['Fac'], [(0.2, (0.24, 0.14, 0.07)), (0.6, (0.48, 0.30, 0.16)), (0.9, (0.62, 0.42, 0.24))])
    return col, 0.85, w.outputs['Fac'], 0.6

R['BarkDark'] = R['Bark']

@recipe('Plank')
def _plank(nb, b):
    vec, tc = _coords(nb, 1.0)
    st = nb.add('ShaderNodeMapping'); st.inputs['Scale'].default_value = (1.0, 8.0, 8.0); nb.link(tc.outputs['Object'], st.inputs['Vector'])
    w = nb.add('ShaderNodeTexWave'); w.wave_type = 'RINGS'; w.inputs['Scale'].default_value = 1.5; w.inputs['Distortion'].default_value = 8.0
    nb.link(st.outputs['Vector'], w.inputs['Vector'])
    col = _ramp(nb, w.outputs['Fac'], [(0.2, (0.42, 0.25, 0.12)), (0.7, (0.66, 0.44, 0.24)), (1.0, (0.76, 0.54, 0.32))])
    return col, 0.75, w.outputs['Fac'], 0.25

R['PlankDark'] = R['Plank']; R['Wood'] = R['Plank']; R['DarkWood'] = R['Plank']

@recipe('PinkStone')
def _pinkstone(nb, b):
    """carved pink stone blocks: brick pattern, per-block tint, chipped worn edges, moss in the joints on top faces"""
    vec, tc = _coords(nb, 1.0)
    br = nb.add('ShaderNodeTexBrick'); br.inputs['Scale'].default_value = 2.4; br.inputs['Mortar Size'].default_value = 0.025
    br.inputs['Mortar Smooth'].default_value = 0.3; br.inputs['Brick Width'].default_value = 0.6; br.inputs['Row Height'].default_value = 0.3
    br.inputs['Color1'].default_value = _hex((0.88, 0.62, 0.60)); br.inputs['Color2'].default_value = _hex((0.78, 0.52, 0.52))
    br.inputs['Mortar'].default_value = _hex((0.50, 0.34, 0.32)); br.offset = 0.5
    nb.link(vec, br.inputs['Vector'])
    n = _noise(nb, vec, 8.0, 8)
    col = _mix(nb, br.outputs['Color'], _ramp(nb, n.outputs['Fac'], [(0.35, (0.70, 0.46, 0.44)), (0.7, (0.95, 0.75, 0.72))]), 0.3, 'OVERLAY')
    col, f = _top_moss(nb, col, amount=0.45, vec=vec)
    h = _math(nb, 'ADD', _math(nb, 'MULTIPLY', br.outputs['Fac'], -1.0), _math(nb, 'MULTIPLY', n.outputs['Fac'], 0.25))
    return col, 0.75, h, 0.5

@recipe('StoneTile')
def _stonetile(nb, b):
    """worn pale-pink paving stone: mottled, fine pits, lighter worn top"""
    vec, tc = _coords(nb, 1.0)
    n = _noise(nb, vec, 6.0, 8, 0.6)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.80, 0.60, 0.55)), (0.55, (0.92, 0.76, 0.70)), (0.8, (0.99, 0.90, 0.84))])
    pit = nb.add('ShaderNodeTexVoronoi'); pit.inputs['Scale'].default_value = 40.0; nb.link(vec, pit.inputs['Vector'])
    col = _mix(nb, col, (0.62, 0.46, 0.42), _math(nb, 'LESS_THAN', pit.outputs['Distance'], 0.05))
    return col, 0.75, _math(nb, 'ADD', n.outputs['Fac'], _math(nb, 'MULTIPLY', pit.outputs['Distance'], 0.5)), 0.45

@recipe('Rock')
def _rock(nb, b):
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.feature = 'DISTANCE_TO_EDGE'; v.inputs['Scale'].default_value = 1.6; nb.link(vec, v.inputs['Vector'])
    n = _noise(nb, vec, 5.0, 8)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.48, 0.34, 0.31)), (0.7, (0.70, 0.55, 0.50))])
    col = _mix(nb, col, (0.30, 0.20, 0.18), _math(nb, 'LESS_THAN', v.outputs['Distance'], 0.04))
    col, f = _top_moss(nb, col, amount=0.9, vec=vec)
    return col, 0.85, _math(nb, 'ADD', _math(nb, 'MULTIPLY', v.outputs['Distance'], 2.0, clamp=True), _math(nb, 'MULTIPLY', n.outputs['Fac'], 0.3)), 0.7

R['RockDark'] = R['Rock']; R['StoneGrey'] = R['Rock']

@recipe('Grass')
def _grass(nb, b):
    vec, tc = _coords(nb, 1.0)
    n = _noise(nb, vec, 3.0, 6)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.30, 0.50, 0.16)), (0.7, (0.48, 0.66, 0.22))])
    fl = nb.add('ShaderNodeTexVoronoi'); fl.inputs['Scale'].default_value = 14.0; nb.link(vec, fl.inputs['Vector'])
    col = _mix(nb, col, _ramp(nb, fl.outputs['Color'], [(0.0, (0.98, 0.6, 0.75)), (0.5, (0.99, 0.95, 0.9)), (1.0, (0.99, 0.85, 0.35))]),
               _math(nb, 'LESS_THAN', fl.outputs['Distance'], 0.08))
    return col, 0.9, n.outputs['Fac'], 0.3

@recipe('Moss')
def _moss(nb, b):
    """moss carpet for the TriPBR top layer: clumpy greens, tiny pink/white/yellow flowers"""
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 9.0; nb.link(vec, v.inputs['Vector'])
    n = _noise(nb, vec, 4.0, 6)
    col = _ramp(nb, v.outputs['Distance'], [(0.0, (0.46, 0.66, 0.22)), (0.5, (0.30, 0.52, 0.16)), (0.95, (0.14, 0.32, 0.10))])
    col = _mix(nb, col, _ramp(nb, n.outputs['Fac'], [(0.3, (0.22, 0.42, 0.12)), (0.7, (0.52, 0.68, 0.24))]), 0.4)
    fl = nb.add('ShaderNodeTexVoronoi'); fl.inputs['Scale'].default_value = 16.0; fl.inputs['Randomness'].default_value = 1.0; nb.link(vec, fl.inputs['Vector'])
    pick = _math(nb, 'GREATER_THAN', fl.outputs['Color'], 0.82)
    col = _mix(nb, col, _ramp(nb, fl.outputs['Color'], [(0.82, (0.99, 0.62, 0.78)), (0.9, (0.99, 0.97, 0.92)), (0.97, (0.99, 0.86, 0.32))]),
               _math(nb, 'MULTIPLY', _math(nb, 'LESS_THAN', fl.outputs['Distance'], 0.11), pick))
    return col, 0.85, _math(nb, 'SUBTRACT', 1.0, v.outputs['Distance']), 0.6

@recipe('Glow')
def _glow(nb, b):
    b.inputs['Emission Color'].default_value = _hex((1.0, 0.78, 0.40)); b.inputs['Emission Strength'].default_value = 3.0
    return (1.0, 0.86, 0.55), 0.2, None, 0.0

@recipe('Choco')
def _choco(nb, b):
    """glossy milk chocolate: soft swirls, slight mottling"""
    vec, tc = _coords(nb, 1.0)
    n = _noise(nb, vec, 3.0, 4, 0.5, 1.5)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.22, 0.11, 0.06)), (0.7, (0.38, 0.20, 0.10))])
    return col, 0.18, n.outputs['Fac'], 0.08

R['ChocoDark'] = lambda nb, b: (lambda c: (c[0], 0.2, c[2], c[3]))(simple((0.15, 0.07, 0.04), rough=0.2)(nb, b))
R['ChocoFlow'] = R['Choco']

@recipe('Sand')
def _sand(nb, b):
    """packed pink-beige path: pebbles + fine grain"""
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 14.0; nb.link(vec, v.inputs['Vector'])
    n = _noise(nb, vec, 20.0, 8)
    col = _ramp(nb, v.outputs['Color'], [(0.0, (0.80, 0.62, 0.52)), (0.5, (0.90, 0.74, 0.62)), (1.0, (0.96, 0.84, 0.72))])
    col = _mix(nb, col, _ramp(nb, n.outputs['Fac'], [(0.3, (0.7, 0.55, 0.47)), (0.7, (0.98, 0.88, 0.78))]), 0.3)
    return col, 0.85, _math(nb, 'ADD', v.outputs['Distance'], _math(nb, 'MULTIPLY', n.outputs['Fac'], 0.3)), 0.45

@recipe('Wafer')
def _wafer(nb, b):
    vec, tc = _coords(nb, 1.0)
    br = nb.add('ShaderNodeTexBrick'); br.inputs['Scale'].default_value = 10.0; br.inputs['Brick Width'].default_value = 0.5; br.inputs['Row Height'].default_value = 0.5
    br.offset = 0.0; br.inputs['Mortar Size'].default_value = 0.08; br.inputs['Color1'].default_value = _hex((0.94, 0.76, 0.46)); br.inputs['Color2'].default_value = _hex((0.90, 0.70, 0.40))
    br.inputs['Mortar'].default_value = _hex((0.72, 0.50, 0.26)); nb.link(vec, br.inputs['Vector'])
    return br.outputs['Color'], 0.7, _math(nb, 'MULTIPLY', br.outputs['Fac'], -1.0), 0.6

@recipe('Icing')
def _icing(nb, b):
    vec, tc = _coords(nb, 1.0); n = _noise(nb, vec, 5.0, 4)
    return _ramp(nb, n.outputs['Fac'], [(0.3, (0.94, 0.88, 0.82)), (0.7, (1.0, 0.97, 0.94))]), 0.25, n.outputs['Fac'], 0.1

@recipe('CloudPink')
def _cloud(nb, b):
    vec, tc = _coords(nb, 1.0); n = _noise(nb, vec, 0.8, 3)
    return _ramp(nb, n.outputs['Fac'], [(0.3, (0.86, 0.80, 0.96)), (0.7, (0.99, 0.95, 1.0))]), 0.9, n.outputs['Fac'], 0.05

@recipe('Burlap')
def _burlap(nb, b):
    vec, tc = _coords(nb, 1.0)
    w1 = nb.add('ShaderNodeTexWave'); w1.wave_type = 'BANDS'; w1.bands_direction = 'X'; w1.inputs['Scale'].default_value = 60.0; nb.link(vec, w1.inputs['Vector'])
    w2 = nb.add('ShaderNodeTexWave'); w2.wave_type = 'BANDS'; w2.bands_direction = 'Z'; w2.inputs['Scale'].default_value = 60.0; nb.link(vec, w2.inputs['Vector'])
    weave = _math(nb, 'MULTIPLY', w1.outputs['Fac'], w2.outputs['Fac'])
    n = _noise(nb, vec, 4.0, 4)
    col = _ramp(nb, n.outputs['Fac'], [(0.3, (0.52, 0.36, 0.20)), (0.7, (0.70, 0.52, 0.32))])
    col = _mix(nb, col, (0.40, 0.27, 0.14), _math(nb, 'MULTIPLY', weave, 0.5))
    return col, 0.9, weave, 0.35

R['BurlapLight'] = R['Burlap']; R['Sack'] = R['Burlap']

@recipe('Window')
def _window(nb, b):
    vec, tc = _coords(nb, 1.0); n = _noise(nb, vec, 2.0, 3)
    b.inputs['Emission Color'].default_value = _hex((1.0, 0.85, 0.55)); b.inputs['Emission Strength'].default_value = 0.6
    return _ramp(nb, n.outputs['Fac'], [(0.3, (0.45, 0.70, 0.92)), (0.7, (0.72, 0.88, 0.98))]), 0.05, None, 0.0
R['Pane'] = R['Window']

@recipe('Mushroom')
def _mush(nb, b):
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 4.0; nb.link(vec, v.inputs['Vector'])
    return _mix(nb, (0.82, 0.12, 0.12), (0.99, 0.96, 0.9), _math(nb, 'LESS_THAN', v.outputs['Distance'], 0.18)), 0.4, v.outputs['Distance'], 0.15

@recipe('Almond')
def _almond(nb, b):
    vec, tc = _coords(nb, 1.0)
    st = nb.add('ShaderNodeMapping'); st.inputs['Scale'].default_value = (20.0, 20.0, 6.0); nb.link(tc.outputs['Object'], st.inputs['Vector'])
    w = nb.add('ShaderNodeTexWave'); w.inputs['Scale'].default_value = 1.0; w.inputs['Distortion'].default_value = 4.0; nb.link(st.outputs['Vector'], w.inputs['Vector'])
    return _ramp(nb, w.outputs['Fac'], [(0.2, (0.45, 0.26, 0.12)), (0.7, (0.68, 0.44, 0.24))]), 0.6, w.outputs['Fac'], 0.3

@recipe('RedTile')
def _redtile(nb, b):
    vec, tc = _coords(nb, 1.0); n = _noise(nb, vec, 8.0, 6)
    return _ramp(nb, n.outputs['Fac'], [(0.3, (0.66, 0.22, 0.24)), (0.7, (0.86, 0.38, 0.38))]), 0.6, n.outputs['Fac'], 0.3

for _nm, _c in (('Water', (0.45, 0.75, 1.0)), ('Portal', (0.70, 0.30, 1.0)), ('PurpleGlow', (0.62, 0.30, 0.95))):
    def _mk(c):
        def fn(nb, b):
            b.inputs['Emission Color'].default_value = _hex(c); b.inputs['Emission Strength'].default_value = 2.5
            return c, 0.15, None, 0.0
        return fn
    R[_nm] = _mk(_c)

@recipe('Pine')
def _pine(nb, b):
    """conifer needles: layered dark blue-green clumps with light tips"""
    vec, tc = _coords(nb, 1.0)
    v = nb.add('ShaderNodeTexVoronoi'); v.inputs['Scale'].default_value = 8.0; nb.link(vec, v.inputs['Vector'])
    w = nb.add('ShaderNodeTexWave'); w.wave_type = 'BANDS'; w.bands_direction = 'Z'; w.inputs['Scale'].default_value = 25.0; w.inputs['Distortion'].default_value = 6.0; nb.link(vec, w.inputs['Vector'])
    col = _ramp(nb, v.outputs['Distance'], [(0.0, (0.30, 0.55, 0.26)), (0.5, (0.17, 0.38, 0.18)), (0.9, (0.07, 0.20, 0.11))])
    col = _mix(nb, col, (0.36, 0.60, 0.30), _math(nb, 'MULTIPLY', w.outputs['Fac'], 0.3))
    return col, 0.75, _math(nb, 'ADD', _math(nb, 'SUBTRACT', 1.0, v.outputs['Distance']), _math(nb, 'MULTIPLY', w.outputs['Fac'], 0.4)), 0.55

def make(name):
    """material HOB_<name> built from its recipe (falls back to a plain colour)"""
    key = "HOBHQ_" + name + ("_T" if TILE[0] else "")
    if key in bpy.data.materials: return bpy.data.materials[key]
    m = bpy.data.materials.new(key); m.use_nodes = True
    nb = NB(m); b = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    if name not in R:                                                             # any palette colour without its own recipe: mottled paint of that colour
        import lib
        R[name] = simple(lib.PAL.get(name, (0.8, 0.8, 0.8)))
    fn = R[name]
    col, rough, h, strength = fn(nb, b)
    if PREVIEW[0]:                                                                # vertex AO / gradient multiply (what Unity does)
        at = nb.add('ShaderNodeVertexColor'); at.layer_name = "Col"
        col = _mix(nb, col, at.outputs['Color'], 1.0, 'MULTIPLY')
    if isinstance(col, tuple): b.inputs['Base Color'].default_value = _hex(col)
    else: nb.link(col, b.inputs['Base Color'])
    if isinstance(rough, (int, float)): b.inputs['Roughness'].default_value = rough
    else: nb.link(rough, b.inputs['Roughness'])
    if h is not None and strength > 0:
        bp = nb.add('ShaderNodeBump'); bp.inputs['Strength'].default_value = strength; bp.inputs['Distance'].default_value = 0.02
        nb.link(h, bp.inputs['Height']); nb.link(bp.outputs['Normal'], b.inputs['Normal'])
    m["hq_emissive"] = name in ('Glow', 'Water', 'Portal', 'PurpleGlow', 'Window', 'Pane')
    return m
