import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys, numpy as np
import troll_preview as T
from PIL import Image, ImageDraw
J = T.J
print({k: round(float(v), 3) for k, v in J.items()}, 'HEAD_SHIFT', round(T.HEAD_SHIFT, 3), 'CF', T.CF)
y0, y1, z0, z1, N = J['Top'] - 0.8, J['Top'] + 0.4, -0.6, 1.2, 600
ys = np.linspace(y1, y0, N, dtype=np.float32); zs = np.linspace(z0, z1, int(N * (z1 - z0) / (y1 - y0)), dtype=np.float32)
Z, Y = np.meshgrid(zs, ys)
x = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
p = np.stack([np.full_like(Z, x), Y, Z], -1)
img = np.full(Z.shape + (3,), 30, np.uint8)
for f, c in [(T.torso, (90, 110, 130)), (T.head, (150, 120, 120)), (T.collar, (230, 230, 240))]:
    img[f(p) < 0] = c
im = Image.fromarray(img); d = ImageDraw.Draw(im)
for yy in np.arange(np.ceil(y0 * 10) / 10, y1, 0.1):
    r = int((y1 - yy) / (y1 - y0) * N); d.line([(0, r), (8, r)], fill=(255, 255, 0)); d.text((10, r - 5), f'{yy:.1f}', fill=(255, 255, 0))
for zz in np.arange(-0.6, 1.2, 0.1):
    c = int((zz - z0) / (z1 - z0) * Z.shape[1]); d.line([(c, N - 8), (c, N)], fill=(255, 255, 0)); d.text((c - 8, N - 20), f'{zz:.1f}', fill=(255, 255, 0))
im.save(sys.argv[1]); print('saved')
