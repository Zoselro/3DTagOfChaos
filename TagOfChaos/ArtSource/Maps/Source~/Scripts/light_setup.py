# Stage 5: per-map lighting pass. Expects MAP (maplib.Map) in namespace (run right after the map build).
# Adds local lights (LGT_*) next to light-giving assets, caps their number, tunes sun/sky, stores the
# Unity lighting recipe on the scene (scene['unity_lighting'] as JSON) for the stage-6 export.
import bpy, math, json, random

L = {
    # sky colour (Unity: Environment Lighting colour / skybox tint), ambient intensity, sun colour, sun W,
    # sun rotation (deg X, Z), fog colour, fog density (Unity exponential), light rules
    'CandyForest': dict(sky=(1.0, 0.74, 0.85), amb=1.0, sun=(1.0, 0.82, 0.90), sun_w=3.2, sun_rot=(50, 35),
                        fog=(0.98, 0.80, 0.88), fog_d=0.008,
                        rules=[('GiantMushroom', (0.75, 0.35, 1.0), 1500, 10, 1.0, 5.0),
                               ('MagicCrystal', (0.7, 0.4, 1.0), 300, 6, 0.5, 1.5),
                               ('Landmark_LollipopTree', (1.0, 0.5, 0.8), 20000, 60, 1.0, 20.0)], cap=40),
    'GingerbreadVillage': dict(sky=(0.03, 0.05, 0.14), amb=0.6, sun=(0.55, 0.62, 1.0), sun_w=0.5, sun_rot=(55, -30),
                               fog=(0.06, 0.08, 0.2), fog_d=0.006,
                               rules=[('CandyStreetLamp', (1.0, 0.55, 0.65), 250, 14, 0.34, 3.9),
                                      ('GlowDrop_Pink', (1.0, 0.4, 0.7), 60, 6, 0.25, 0.8),
                                      ('GlowDrop_Yellow', (1.0, 0.8, 0.3), 60, 6, 0.25, 0.8),
                                      ('GlowDrop_Cyan', (0.2, 0.9, 1.0), 60, 6, 0.25, 0.8),
                                      ('GingerHouse', (1.0, 0.75, 0.35), 300, 12, 0.5, 2.5),
                                      ('Landmark_ClockTower', (1.0, 0.8, 0.45), 6000, 40, 1.0, 17.0)], cap=140),
    'ChocolateFactory': dict(sky=(0.35, 0.22, 0.14), amb=0.8, sun=(1.0, 0.75, 0.5), sun_w=2.4, sun_rot=(60, 20),
                             fog=(0.45, 0.3, 0.2), fog_d=0.004,
                             rules=[('ArchWindow', (1.0, 0.75, 0.45), 900, 18, 0.34, 8.0),
                                    ('CandyMachine', (0.75, 0.25, 1.0), 250, 8, 0.6, 4.0),
                                    ('CakeTower', (1.0, 0.6, 0.4), 500, 12, 1.0, 5.0),
                                    ('Landmark_ChocolateTank', (1.0, 0.55, 0.3), 12000, 50, 1.0, 12.0)], cap=90),
    'CursedCandyCarnival': dict(sky=(0.01, 0.025, 0.04), amb=0.35, sun=(0.35, 0.55, 0.7), sun_w=0.25, sun_rot=(55, 160),
                                fog=(0.04, 0.08, 0.1), fog_d=0.007,
                                rules=[('CarnivalLamp', (1.0, 0.75, 0.35), 300, 14, 1.0, 5.0),
                                       ('Booth_Neon_A', (1.0, 0.3, 0.6), 150, 9, 0.3, 2.8),
                                       ('Booth_Neon_B', (0.2, 1.0, 0.9), 150, 9, 0.3, 2.8),
                                       ('Booth_Neon_C', (0.6, 0.3, 1.0), 150, 9, 0.3, 2.8),
                                       ('PumpkinLantern', (1.0, 0.6, 0.15), 80, 6, 0.3, 1.0),
                                       ('CircusTent_Small', (1.0, 0.3, 0.5), 400, 12, 0.6, 3.5),
                                       ('Carousel_Top', (0.3, 1.0, 0.9), 3000, 25, 1.0, 8.0),
                                       ('Carnival_GothicGate', (1.0, 0.55, 0.2), 2500, 25, 1.0, 12.0)], cap=150),
    'HauntedBakery': dict(sky=(0.05, 0.04, 0.07), amb=0.5, sun=(0.7, 0.62, 0.9), sun_w=0.4, sun_rot=(55, -40),
                          fog=(0.1, 0.08, 0.12), fog_d=0.005,
                          rules=[('WallLantern_Post', (1.0, 0.65, 0.35), 300, 12, 1.0, 3.3),
                                 ('Chalkboard_Awning', (1.0, 0.8, 0.4), 150, 8, 1.0, 5.0),
                                 ('Counter_Purple', (0.8, 0.5, 1.0), 200, 8, 1.0, 3.0),
                                 ('Table_Round', (1.0, 0.7, 0.45), 150, 7, 1.0, 3.5),
                                 ('Landmark_MagicOven', (1.0, 0.45, 0.12), 30000, 45, 1.0, 9.0),
                                 ('GingerHouse', (1.0, 0.75, 0.35), 250, 12, 0.6, 2.5),
                                 ('Pumpkin', (1.0, 0.55, 0.15), 50, 5, 0.15, 1.0)], cap=90),
}
# fixed key lights not tied to an asset instance: (x, y, z above ground, colour, W, range, shadow)
EXTRA = {
    'CandyForest': [(0, 0, 40, (1.0, 0.6, 0.85), 30000, 90, True)],
    'GingerbreadVillage': [(0, -14, 12, (1.0, 0.7, 0.4), 8000, 40, True), (0, 14, 12, (1.0, 0.7, 0.4), 8000, 40, False)],
    'ChocolateFactory': [(x, y, 18, (1.0, 0.72, 0.45), 16000, 55, x == 0 and y == 0)
                         for x in (-66, -22, 22, 66) for y in (-66, -22, 22, 66)] +
                        [(0, -22, 6, (1.0, 0.55, 0.25), 20000, 40, False)],
    'CursedCandyCarnival': [(0, 128, 50, (1.0, 0.35, 0.8), 150000, 110, False), (0, 120, 12, (0.7, 0.35, 1.0), 30000, 60, False),
                            (0, 0, 14, (0.3, 1.0, 0.9), 20000, 50, True), (139, 0, 12, (1.0, 0.6, 0.4), 25000, 70, False),
                            (-138, 0, 10, (0.3, 1.0, 0.9), 12000, 40, False), (0, -164, 12, (1.0, 0.55, 0.2), 8000, 40, False)],
    'HauntedBakery': [(0, 45, 8, (1.0, 0.45, 0.12), 40000, 60, True), (0, 0, 13, (1.0, 0.72, 0.45), 15000, 50, False),
                      (0, -35, 13, (1.0, 0.72, 0.45), 12000, 40, False), (-72, 10, 10, (0.55, 0.3, 0.9), 3000, 30, False),
                      (72, 0, 12, (1.0, 0.7, 0.45), 8000, 35, False)],
}
cfg = L[MAP.name]
sc = MAP.scene
col = MAP.c['Lighting']
# remove the blockout's old local lights (keep the sun)
for o in list(col.objects):
    if o.type == 'LIGHT' and o.data.type != 'SUN':
        bpy.data.objects.remove(o)
sun = next((o for o in col.objects if o.type == 'LIGHT' and o.data.type == 'SUN'), None)
if sun is None:
    sun = MAP.light('Moon', 'SUN', (0, 0, 120), cfg['sun'], cfg['sun_w'])
sun.data.color = cfg['sun']
sun.data.energy = cfg['sun_w']
sun.rotation_euler = (math.radians(cfg['sun_rot'][0]), 0, math.radians(cfg['sun_rot'][1]))

rnd = random.Random(5)
inst = [o for c in MAP.c.values() for o in c.objects if o.get('asset')]
made = []
rules = sorted(cfg['rules'], key=lambda r: 'Landmark' not in r[0] and 'Carousel' not in r[0] and 'Gate' not in r[0])
for key, color, watt, rng, prob, zoff in rules:        # landmark lights first so the cap never drops them
    cands = [o for o in inst if o['asset'].startswith(key)]
    rnd.shuffle(cands)
    for o in cands:
        if len([x for x in made if x[0] == key]) >= max(1, int(cfg['cap'] * (0.5 if 'Landmark' not in key else 1))):
            break
        if rnd.random() > prob:
            continue
        s = o.scale.z
        ld = bpy.data.lights.new(f'LGT_{key}', 'POINT')
        ld.color = color
        ld.energy = watt * (s ** 2)
        ld.shadow_soft_size = 0.4
        ld.use_shadow = 'Landmark' in key or watt >= 3000
        try:
            ld.use_custom_distance = True
            ld.cutoff_distance = rng * s
        except Exception:
            pass
        lo = bpy.data.objects.new(ld.name, ld)
        col.objects.link(lo)
        lo.location = (o.location.x, o.location.y, o.location.z + zoff * s)
        lo['unity_range'] = round(rng * s, 1)
        made.append((key, lo))
    if len(made) >= cfg['cap']:
        break
for (x, y, z, color, watt, rng, shadow) in EXTRA.get(MAP.name, []):
    ld = bpy.data.lights.new('LGT_Key', 'POINT')
    ld.color, ld.energy, ld.shadow_soft_size, ld.use_shadow = color, watt, 2.0, shadow
    try:
        ld.use_custom_distance = True
        ld.cutoff_distance = rng
    except Exception:
        pass
    lo = bpy.data.objects.new(ld.name, ld)
    col.objects.link(lo)
    lo.location = (x, y, MAP.h_at(x, y) + z)
    lo['unity_range'] = rng
    made.append(('Key', lo))
# Unity recipe (Blender Z-up -> Unity Y-up: (x, y, z) -> (x, z, -y) with FBX default axes)
recipe = dict(
    ambient_color=cfg['sky'], ambient_intensity=cfg['amb'], fog=True, fog_mode='Exponential',
    fog_color=cfg['fog'], fog_density=cfg['fog_d'],
    directional=dict(color=cfg['sun'], intensity=round(min(cfg['sun_w'], 4) / 3.2, 2),
                     euler_x=cfg['sun_rot'][0], euler_y=-cfg['sun_rot'][1]),
    point_lights=[dict(name=lo.name, pos_unity=(round(lo.location.x, 2), round(lo.location.z, 2), round(-lo.location.y, 2)),
                       color=tuple(round(c, 3) for c in lo.data.color), range=lo['unity_range'],
                       intensity=round(lo.data.energy / 250.0, 2), shadows=bool(lo.data.use_shadow))
                  for _, lo in made])
sc['unity_lighting'] = json.dumps(recipe)
import os
_ldir = os.path.join(os.path.dirname(OUT_DIR), '..', 'Lighting')
os.makedirs(_ldir, exist_ok=True)
with open(os.path.join(_ldir, f'{MAP.name}_UnityLighting.json'), 'w', encoding='utf-8') as fh:
    json.dump(recipe, fh, indent=1)
LIGHT_REPORT = dict(lights=len(made), by_type={k: sum(1 for x in made if x[0] == k) for k, *_ in cfg['rules']})
