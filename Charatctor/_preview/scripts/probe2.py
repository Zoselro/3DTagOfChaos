import sys, os, numpy as np
sys.argv = ["x", "--", "none"]
exec(open(os.path.join(os.path.dirname(__file__), "shape.py"), encoding="utf-8").read().split("import sys\nargv")[0])
o = load("Macaron"); v = coords(o); col = vertex_colors(o)
lum = col @ np.array([0.3, 0.59, 0.11])
for side in (-1, 1):
    box = (np.abs(v[:, 0] - side * 0.2) < 0.09) & (np.abs(v[:, 2] - 0.17) < 0.09) & (v[:, 1] < -0.2)
    dk = box & (lum < 0.1)
    h, e = np.histogram(v[dk, 1], bins=12)
    print("INFO side", side, "dark n", dk.sum(), "skin y med %.3f" % np.median(v[box & (lum > 0.3), 1]), list(zip(np.round(e[:-1], 3), h)))
    for lo, hi in zip(e[:-1], e[1:]):
        m = dk & (v[:, 1] >= lo) & (v[:, 1] < hi)
        if m.sum():
            p = v[m]; print("INFO   y %.3f..%.3f n=%d x %.3f..%.3f z %.3f..%.3f" % (lo, hi, m.sum(), p[:, 0].min(), p[:, 0].max(), p[:, 2].min(), p[:, 2].max()))
print("PROBE_OK")
