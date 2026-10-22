# Gyro drop textures (2026-10-22): tiling 512 px PNGs for the four parts, made with numpy + PIL.
# python3 make_textures.py <out dir>
import sys, os, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
SIZE = int(sys.argv[2]) if len(sys.argv) > 2 else 512   # noise textures are made at this size; drawn ones are drawn at 512 and scaled
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(1022)
N = SIZE
D = 512


def noise(scale, octaves=4):
    """Tiling value noise in 0..1."""
    acc = np.zeros((N, N))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        cells = scale * (2 ** o)
        g = rng.random((cells, cells))
        g = np.pad(g, ((0, 1), (0, 1)), mode="wrap")
        x = np.linspace(0, cells, N, endpoint=False)
        xi = x.astype(int); xf = x - xi
        xf = xf * xf * (3 - 2 * xf)
        a = g[xi][:, xi]; b = g[xi][:, xi + 1]; c = g[xi + 1][:, xi]; d = g[xi + 1][:, xi + 1]
        top = a * (1 - xf[None, :]) + b * xf[None, :]
        bot = c * (1 - xf[None, :]) + d * xf[None, :]
        acc += amp * (top * (1 - xf[:, None]) + bot * xf[:, None])
        total += amp; amp *= 0.5
    return acc / total


def save(name, rgb):
    Image.fromarray(np.clip(rgb * 255, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))


def tint(base, n, spread):
    return np.array(base)[None, None, :] * (1 - spread + spread * 2 * n[..., None])


# purple stone bricks (platform wall, tower plinth, crown drum)
n = noise(8)
img = tint((0.33, 0.17, 0.52), n, 0.18)
rows = 4; bh = N // rows
mortar = np.zeros((N, N), bool)
for r in range(rows):
    y0 = r * bh
    mortar[y0:y0 + 6, :] = True
    off = (bh // 1) if r % 2 else 0
    for c in range(3):
        x = (c * N // 3 + off // 2) % N
        mortar[y0:y0 + bh, max(0, x - 3):x + 3] = True
edge = Image.fromarray((mortar * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))
e = np.array(edge) / 255.0
img = img * (1 - 0.55 * e[..., None]) + np.array((0.12, 0.06, 0.2))[None, None, :] * 0.55 * e[..., None]
img *= (0.85 + 0.3 * noise(32, 2))[..., None]
save("GD_PurpleStone", img)

# floor flagstones (platform top)
n = noise(6)
img = tint((0.36, 0.28, 0.55), n, 0.15)
g = np.zeros((N, N))
for k in range(0, N, N // 4):
    g[k:k + 5, :] = 1; g[:, k:k + 5] = 1
g = np.array(Image.fromarray((g * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(2))) / 255.0
img = img * (1 - 0.5 * g[..., None]) + 0.5 * g[..., None] * np.array((0.15, 0.1, 0.25))
img *= (0.88 + 0.24 * noise(24, 2))[..., None]
save("GD_FloorTiles", img)

# red painted metal with panel seams (tower lattice, rails, ring rim)
n = noise(10)
img = tint((0.78, 0.07, 0.12), n, 0.1)
seam = np.zeros((N, N)); seam[::N // 4, :] = 1; seam[:, ::N // 4] = 1
seam = np.array(Image.fromarray((seam * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.5))) / 255.0
img = img * (1 - 0.4 * seam[..., None])
img *= (0.9 + 0.2 * noise(48, 2))[..., None]
save("GD_RedMetal", img)

# purple painted metal (tower pillars, ring plate)
n = noise(10)
img = tint((0.38, 0.16, 0.66), n, 0.12)
img = img * (1 - 0.35 * seam[..., None])
img *= (0.9 + 0.2 * noise(48, 2))[..., None]
save("GD_PurpleMetal", img)

# gold trim
n = noise(16)
img = tint((0.95, 0.62, 0.16), n, 0.2)
img *= (0.85 + 0.3 * noise(64, 2))[..., None]
save("GD_Gold", img)

# black seat padding with stitches
n = noise(20)
img = tint((0.06, 0.05, 0.07), n, 0.3)
st = np.zeros((N, N)); st[:, 60:64] = 1; st[:, N - 64:N - 60] = 1
st *= (np.arange(N)[:, None] // 12 % 2)
img += 0.12 * st[..., None]
save("GD_BlackPad", img)

# red seat leather
n = noise(14)
img = tint((0.85, 0.1, 0.14), n, 0.15)
img += 0.08 * st[..., None]
save("GD_RedSeat", img)

# flag: red with a skull & crossbones
img = Image.new("RGB", (D, D // 2), (200, 20, 30))
d = ImageDraw.Draw(img)
cx, cy = D // 2, D // 4
d.line((cx - 90, cy - 70, cx + 90, cy + 70), fill=(235, 225, 210), width=22)
d.line((cx - 90, cy + 70, cx + 90, cy - 70), fill=(235, 225, 210), width=22)
d.ellipse((cx - 62, cy - 80, cx + 62, cy + 40), fill=(240, 232, 218))
d.rectangle((cx - 34, cy + 20, cx + 34, cy + 62), fill=(240, 232, 218))
d.ellipse((cx - 44, cy - 40, cx - 8, cy - 2), fill=(40, 10, 15))
d.ellipse((cx + 8, cy - 40, cx + 44, cy - 2), fill=(40, 10, 15))
for k in (-20, 0, 20):
    d.line((cx + k, cy + 30, cx + k, cy + 62), fill=(40, 10, 15), width=5)
img.resize((N, N // 2), Image.LANCZOS).save(os.path.join(OUT, "GD_Flag.png"))

# emblem: orange demon face on dark purple disc with gold ring
img = Image.new("RGB", (D, D), (60, 20, 70))
d = ImageDraw.Draw(img)
d.ellipse((8, 8, D - 8, D - 8), fill=(225, 150, 40))
d.ellipse((40, 40, D - 40, D - 40), fill=(80, 25, 90))
c = D // 2
d.polygon([(c - 150, c - 60), (c - 110, c - 190), (c - 60, c - 90)], fill=(240, 110, 30))   # horns
d.polygon([(c + 150, c - 60), (c + 110, c - 190), (c + 60, c - 90)], fill=(240, 110, 30))
d.ellipse((c - 130, c - 110, c + 130, c + 140), fill=(240, 110, 30))
d.polygon([(c - 90, c - 30), (c - 20, c + 10), (c - 90, c + 20)], fill=(60, 10, 20))         # eyes
d.polygon([(c + 90, c - 30), (c + 20, c + 10), (c + 90, c + 20)], fill=(60, 10, 20))
d.polygon([(c - 70, c + 60), (c + 70, c + 60), (c, c + 115)], fill=(60, 10, 20))              # mouth
for k in range(-50, 60, 25):
    d.polygon([(c + k - 9, c + 60), (c + k + 9, c + 60), (c + k, c + 80)], fill=(245, 235, 215))
img.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, "GD_Emblem.png"))

# skull medallion face (crown)
img = Image.new("RGB", (D, D), (70, 25, 100))
d = ImageDraw.Draw(img)
d.ellipse((8, 8, D - 8, D - 8), fill=(230, 160, 50))
d.ellipse((36, 36, D - 36, D - 36), fill=(90, 35, 120))
d.ellipse((c - 140, c - 160, c + 140, c + 90), fill=(242, 236, 225))
d.rounded_rectangle((c - 80, c + 40, c + 80, c + 150), 20, fill=(242, 236, 225))
d.ellipse((c - 105, c - 80, c - 25, c + 10), fill=(35, 10, 40))
d.ellipse((c + 25, c - 80, c + 105, c + 10), fill=(35, 10, 40))
d.polygon([(c, c + 20), (c - 22, c + 60), (c + 22, c + 60)], fill=(35, 10, 40))
for k in (-48, -16, 16, 48):
    d.line((c + k, c + 95, c + k, c + 150), fill=(35, 10, 40), width=8)
img.resize((N, N), Image.LANCZOS).save(os.path.join(OUT, "GD_Skull.png"))
print("textures ok")
