# Escape-mode witch (exec'd by escape_assets.py). EscapePlan.md §1.2.
import math, random

# =====================================================================================
# WITCH (~90 m, stands outside the map, faces -Y = Unity +Z)
# =====================================================================================
def build_witch(h=90.0):
    u = 'WITCH'

    def robe(g):
        g.lathe([(0, 0), (h * 0.36, 0), (h * 0.3, h * 0.2), (h * 0.2, h * 0.5), (h * 0.14, h * 0.64), (0, h * 0.66)],
                'ME_Witch_Robe', seg=24, radial=lambda a: 1 + 0.06 * math.sin(a * 7))
        torus(g, 'M_Candy_Purple', (0, 0, h * 0.62), h * 0.15, h * 0.02)
    mesh_obj(u, 'Robe', robe)

    def head(g):
        g.blob('ME_Witch_Skin', (0, 0, h * 0.75), h * 0.11, sz=(1, 0.95, 1.1), seg=20)
        g.lathe([(0, 0), (h * 0.025, 0), (0, h * 0.09)], 'ME_Witch_Skin', seg=10, loc=(0, -h * 0.1, h * 0.74),
                rot=(math.pi / 2 + 0.5, 0, 0))                                           # crooked nose
        for s in (-1, 1):                                                                # stringy hair
            g.tube([(s * h * 0.09, h * 0.02, h * 0.82), (s * h * 0.14, h * 0.04, h * 0.7), (s * h * 0.15, h * 0.02, h * 0.58)],
                   h * 0.02, 'M_Pine_Dark', seg=8)
    mesh_obj(u, 'Head', head)

    def hat(g):
        g.lathe([(0, 0), (h * 0.22, 0), (h * 0.23, h * 0.012), (0, h * 0.012)], 'ME_Witch_Hat', seg=28,
                loc=(0, 0, h * 0.83))
        g.tube([(0, 0, h * 0.84), (0, 0, h * 0.95), (h * 0.02, h * 0.03, h * 1.03), (h * 0.07, h * 0.06, h * 1.06)],
               [h * 0.1, h * 0.07, h * 0.035, h * 0.008], 'ME_Witch_Hat', seg=16)
        torus(g, 'M_Gold', (0, 0, h * 0.855), h * 0.098, h * 0.012)
    mesh_obj(u, 'Hat', hat)

    def eyes(g):
        for s in (-1, 1):
            g.blob('ME_Witch_Eye', (s * h * 0.045, -h * 0.095, h * 0.78), h * 0.022, sz=(1, 0.6, 0.8), seg=10)
    mesh_obj(u, 'Eyes', eyes)

    def left_arm(g):
        g.tube([(-h * 0.13, 0, h * 0.6), (-h * 0.22, -h * 0.05, h * 0.42), (-h * 0.2, -h * 0.12, h * 0.32)],
               [h * 0.05, h * 0.04, h * 0.035], 'ME_Witch_Robe', seg=12)
        g.blob('ME_Witch_Skin', (-h * 0.2, -h * 0.14, h * 0.29), h * 0.05, seg=12)
    mesh_obj(u, 'ArmLeft', left_arm)

    # slam arm: pivot at the right shoulder, arm points along -Y (forward) at rest so a -X/+X pitch swings it
    shoulder = (h * 0.2, 0, h * 0.6)
    pivot = empty(u, 'SlamArm', shoulder)

    def right_arm(g):
        g.tube([(0, 0, 0), (0, -h * 0.22, 0), (0, -h * 0.4, 0)], [h * 0.055, h * 0.045, h * 0.04], 'ME_Witch_Robe', seg=12)
        g.blob('ME_Witch_Skin', (0, -h * 0.46, 0), h * 0.09, sz=(1.1, 1, 0.7), seg=14)
        for k in range(4):
            g.tube([((k - 1.5) * h * 0.035, -h * 0.5, 0), ((k - 1.5) * h * 0.04, -h * 0.57, -h * 0.03)],
                   h * 0.014, 'ME_Witch_Skin', seg=6)
    arm = mesh_obj(u, 'Arm', right_arm, parent=pivot)
    arm.location = (0, 0, 0)



BUILDS.append(('WITCH', build_witch, (), {}))
