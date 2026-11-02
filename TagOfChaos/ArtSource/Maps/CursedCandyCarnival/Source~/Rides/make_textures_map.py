# Carnival map object textures (2026-10-22): striped canvas, iron, fountain stone, water, cotton candy, popcorn, liquid.
# python3 make_textures_map.py <out dir> [size]
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
N = int(sys.argv[2]) if len(sys.argv) > 2 else 1024
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(3110)


def noise(scale, octaves=4):
    acc = np.zeros((N, N)); amp = 1.0; total = 0.0
    for o in range(octaves):
        c = scale * (2 ** o)
        g = np.pad(rng.random((c, c)), ((0, 1), (0, 1)), mode="wrap")
        x = np.linspace(0, c, N, endpoint=False); i = x.astype(int); f = x - i; f = f * f * (3 - 2 * f)
        a = g[i][:, i]; b = g[i][:, i + 1]; cc = g[i + 1][:, i]; d = g[i + 1][:, i + 1]
        top = a * (1 - f) + b * f; bot = cc * (1 - f) + d * f
        acc += amp * (top * (1 - f[:, None]) + bot * f[:, None]); total += amp; amp *= 0.5
    return acc / total


def save(name, rgb):
    Image.fromarray(np.clip(rgb * 255, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))


def stripes(c1, c2, count, name, trim=None):
    """Vertical stripes along u (wrap a cylinder/cone: u = around)."""
    x = np.arange(N)
    band = ((x * count // N) % 2)[None, :].repeat(N, 0)
    img = np.where(band[..., None] == 0, np.array(c1), np.array(c2)).astype(float)
    img *= (0.88 + 0.24 * noise(12, 3))[..., None]                    # canvas
    weave = (np.sin(np.arange(N) * 0.9)[None, :] * np.sin(np.arange(N) * 0.9)[:, None]) * 0.03
    img += weave[..., None]
    if trim is not None:
        img[:N // 16] = np.array(trim) * (0.9 + 0.2 * noise(20, 2)[:N // 16, :, None])
    save(name, img)


stripes((0.88, 0.18, 0.45), (0.45, 0.12, 0.6), 8, "MO_TentStripes", trim=(0.95, 0.65, 0.2))
stripes((0.85, 0.08, 0.12), (0.95, 0.9, 0.82), 10, "MO_RedWhiteStripes", trim=(0.95, 0.65, 0.2))

# dark iron (fence, lamp, bench legs)
n = noise(16)
save("MO_Iron", np.array((0.14, 0.11, 0.17))[None, None, :] * (0.75 + 0.5 * n[..., None]))

# fountain stone: blue-grey blocks
n = noise(8)
img = np.array((0.42, 0.45, 0.58))[None, None, :] * (0.8 + 0.4 * n[..., None])
m = np.zeros((N, N))
for k in range(0, N, N // 4): m[k:k + 6, :] = 1
for r in range(4):
    for c in range(3):
        x = int((c / 3 + (0.5 / 3 if r % 2 else 0)) * N) % N
        m[r * N // 4:(r + 1) * N // 4, x:x + 6] = 1
m = np.array(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))) / 255.0
img = img * (1 - 0.5 * m[..., None]) + 0.5 * m[..., None] * np.array((0.18, 0.18, 0.26))
save("MO_FountainStone", img)

# water (turquoise with ripples)
r = noise(6, 3)
ripple = 0.5 + 0.5 * np.sin(r * 30)
save("MO_Water", np.array((0.15, 0.55, 0.75))[None, None, :] * (0.7 + 0.5 * ripple[..., None]))

# cotton candy (pink fluff)
n = noise(10, 5)
save("MO_CottonCandy", np.array((1.0, 0.55, 0.82))[None, None, :] * (0.75 + 0.35 * n[..., None]))

# popcorn kernels
n = noise(24, 4)
blobs = noise(32, 2)
img = np.where((blobs > 0.55)[..., None], np.array((1.0, 0.95, 0.78)), np.array((0.98, 0.78, 0.35))) * (0.8 + 0.35 * n[..., None])
save("MO_Popcorn", img)

# glowing cherry juice
n = noise(6, 3)
save("MO_CherryJuice", np.array((0.85, 0.05, 0.12))[None, None, :] * (0.7 + 0.5 * n[..., None]))

# wooden sign board with gold border (popcorn / candy signs)
D = 512
img = Image.new("RGB", (D, D // 2), (120, 40, 60))
d = ImageDraw.Draw(img)
d.rounded_rectangle((6, 6, D - 7, D // 2 - 7), 30, outline=(240, 175, 60), width=12)
for k in range(9):
    x = 50 + k * 52
    d.ellipse((x - 16, 70, x + 16, 102), fill=(255, 235, 170))
    d.ellipse((x - 12, 150, x + 12, 174), fill=(255, 200, 90))
img.resize((N, N // 2), Image.LANCZOS).save(os.path.join(OUT, "MO_SignBoard.png"))
print("map textures ok")
