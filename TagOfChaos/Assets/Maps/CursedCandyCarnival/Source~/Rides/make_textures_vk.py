# Viking (pirate ship swing) textures (2026-10-22): 1024 px tiles in the same style as the gyro drop set.
# python3 make_textures_vk.py <out dir> [size]
import sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
D = 512
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(2210)


def noise(scale, octaves=4, aspect=1):
    acc = np.zeros((N, N)); amp = 1.0; total = 0.0
    for o in range(octaves):
        cy, cx = scale * (2 ** o), scale * (2 ** o) * aspect
        g = np.pad(rng.random((cy, cx)), ((0, 1), (0, 1)), mode="wrap")
        y = np.linspace(0, cy, N, endpoint=False); x = np.linspace(0, cx, N, endpoint=False)
        yi, xi = y.astype(int), x.astype(int)
        yf, xf = y - yi, x - xi
        yf = yf * yf * (3 - 2 * yf); xf = xf * xf * (3 - 2 * xf)
        a = g[yi][:, xi]; b = g[yi][:, xi + 1]; c = g[yi + 1][:, xi]; d = g[yi + 1][:, xi + 1]
        top = a * (1 - xf) + b * xf; bot = c * (1 - xf) + d * xf
        acc += amp * (top * (1 - yf[:, None]) + bot * yf[:, None]); total += amp; amp *= 0.5
    return acc / total


def save(name, rgb):
    Image.fromarray(np.clip(rgb * 255, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))


def blur_mask(m, r):
    return np.array(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r))) / 255.0


def planks(base, dark, rows, name):
    grain = noise(3, 5, aspect=8)                       # long streaks along u
    img = np.array(base)[None, None, :] * (0.78 + 0.44 * grain[..., None])
    seams = np.zeros((N, N))
    h = N // rows
    for r in range(rows):
        seams[r * h:r * h + max(3, N // 200), :] = 1
        x = (r * 0.37 % 1.0) * N
        seams[r * h:(r + 1) * h, int(x):int(x) + max(3, N // 250)] = 1   # butt joints
        img[r * h:(r + 1) * h] *= 0.9 + 0.2 * rng.random()
    s = blur_mask(seams, 1.5)
    img = img * (1 - 0.6 * s[..., None]) + np.array(dark)[None, None, :] * 0.6 * s[..., None]
    nails = np.zeros((N, N))
    for r in range(rows):
        for k in range(6):
            cx, cy = int((k + 0.5) * N / 6), int(r * h + h * 0.5)
            nails[max(0, cy - 4):cy + 4, max(0, cx - 4):cx + 4] = 1
    nails = blur_mask(nails, 1.0)
    img = img * (1 - 0.5 * nails[..., None]) + np.array((0.85, 0.6, 0.25))[None, None, :] * 0.5 * nails[..., None]
    save(name, img)


planks((0.55, 0.16, 0.12), (0.18, 0.05, 0.04), 8, "VK_HullWood")    # red-brown hull
planks((0.42, 0.25, 0.13), (0.15, 0.08, 0.04), 6, "VK_BarrelWood")
planks((0.32, 0.28, 0.38), (0.12, 0.1, 0.16), 5, "VK_DeckPlanks")    # grey-purple platform floor

# bone (figurehead skull, horns base)
n = noise(6)
img = np.array((0.9, 0.85, 0.76))[None, None, :] * (0.82 + 0.3 * n[..., None])
cracks = blur_mask((noise(20, 2) > 0.8).astype(float) * (noise(40, 1) > 0.6), 2.0)
img *= (1 - 0.18 * cracks[..., None])
save("VK_Bone", img)

# horn (dark red to ochre)
n = noise(5, 4, aspect=4)
img = np.array((0.55, 0.1, 0.12))[None, None, :] * (0.75 + 0.5 * n[..., None])
save("VK_Horn", img)

# vertical banner: purple cloth, gold border, orange skull
img = Image.new("RGB", (D // 2, D), (85, 30, 105))
d = ImageDraw.Draw(img)
w, h = D // 2, D
d.rectangle((0, 0, w - 1, h - 1), outline=(230, 160, 50), width=10)
c, cy = w // 2, h // 3
d.ellipse((c - 70, cy - 80, c + 70, cy + 40), fill=(240, 130, 40))
d.rounded_rectangle((c - 40, cy + 20, c + 40, cy + 70), 10, fill=(240, 130, 40))
d.ellipse((c - 50, cy - 40, c - 10, cy), fill=(60, 15, 40)); d.ellipse((c + 10, cy - 40, c + 50, cy), fill=(60, 15, 40))
d.polygon([(c, cy + 5), (c - 12, cy + 30), (c + 12, cy + 30)], fill=(60, 15, 40))
for k in range(5):                                    # tattered bottom
    x0 = k * w // 5
    d.polygon([(x0, h - 90), (x0 + w // 5, h - 90), (x0 + w // 10, h)], fill=(85, 30, 105))
d.rectangle((0, h - 92, w, h), fill=None)
img.resize((N // 2, N), Image.LANCZOS).save(os.path.join(OUT, "VK_Banner.png"))

# skull emblem with gold spikes (pirate medallion)
img = Image.new("RGB", (D, D), (70, 25, 100))
d = ImageDraw.Draw(img)
c = D // 2
import math
for k in range(8):
    a = k * math.pi / 4
    tip = (c + 250 * math.cos(a), c + 250 * math.sin(a))
    l = (c + 170 * math.cos(a - 0.25), c + 170 * math.sin(a - 0.25)); r = (c + 170 * math.cos(a + 0.25), c + 170 * math.sin(a + 0.25))
    d.polygon([tip, l, r], fill=(240, 175, 55))
d.ellipse((c - 195, c - 195, c + 195, c + 195), fill=(200, 40, 50))
d.ellipse((c - 170, c - 170, c + 170, c + 170), fill=(90, 35, 120))
d.ellipse((c - 120, c - 135, c + 120, c + 75), fill=(242, 236, 225))
d.rounded_rectangle((c - 70, c + 40, c + 70, c + 130), 16, fill=(242, 236, 225))
d.ellipse((c - 92, c - 70, c - 22, c + 5), fill=(35, 10, 40)); d.ellipse((c + 22, c - 70, c + 92, c + 5), fill=(35, 10, 40))
d.ellipse((c - 70, c - 48, c - 44, c - 22), fill=(255, 120, 40)); d.ellipse((c + 44, c - 48, c + 70, c - 22), fill=(255, 120, 40))
d.polygon([(c, c + 15), (c - 18, c + 50), (c + 18, c + 50)], fill=(35, 10, 40))
for k in (-42, -14, 14, 42):
    d.line((c + k, c + 85, c + k, c + 130), fill=(35, 10, 40), width=7)
img.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, "VK_SkullEmblem.png"))
print("viking textures ok")
