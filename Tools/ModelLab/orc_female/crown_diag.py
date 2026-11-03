"""정수리 검은 점 진단 — 확대해서 (1) 보통 그림 (2) 조각마다 색 · 선 없음 (3) 그 점의 깊이를 본다."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import numpy as np
from PIL import Image, ImageDraw
import hair_v2 as Hv
import orc_preview as OP
from brute_preview import length

P = Hv.pieces(Hv.h_high_pony)
S = 360; cy = 0.26; span = 0.42   # 정수리만
a = OP.render(P, 0, S, S, cy, span)

# 조각 번호 · 깊이를 직접 뽑는다(render와 같은 광선)
def scene(p):
    ds = np.stack([f(p) for f, _ in P], 0); return ds.min(0), ds.argmin(0)
fwd = np.array([0, 0, -1], np.float32); right = np.array([1, 0, 0], np.float32); up = np.array([0, 1, 0], np.float32)
xs = (np.arange(S) / S - 0.5) * span; ys = (0.5 - np.arange(S) / S) * span
X, Y = np.meshgrid(xs, ys)
org = (np.array([0, cy, 0], np.float32) - fwd * 4 + X[..., None] * right + Y[..., None] * up).astype(np.float32)
t = np.zeros((S, S), np.float32); hit = np.zeros((S, S), bool)
for _ in range(220):
    d, _ = scene(org + fwd * t[..., None]); hit |= d < 0.0015
    t = np.where(hit, t, np.minimum(t + np.maximum(d * 0.8, 0.001), 30))
_, idx = scene(org + fwd * t[..., None])
names = ['head', 'mouth', 'teeth', 'tusk', 'eyes', 'glint', 'lash', 'lips', 'body', 'ear', 'HAIR', 'LEATHER', 'METAL', 'BUZZ']
pal = np.array([(90, 150, 60), (150, 30, 30), (230, 230, 200), (230, 230, 200), (0, 0, 0), (255, 255, 255), (0, 0, 0), (60, 100, 40),
                (120, 80, 40), (255, 140, 0), (90, 80, 200), (200, 120, 60), (180, 180, 190), (255, 0, 255)], np.uint8)
b = np.where(hit[..., None], pal[np.minimum(idx, len(pal) - 1)], np.array([30, 30, 34], np.uint8))
# 깊이 — 가까울수록 밝게(머리 다발 사이 골이 얼마나 깊은지)
th = np.where(hit, t, np.nan); lo, hi = np.nanpercentile(th, 2), np.nanpercentile(th, 98)
c = np.where(hit, 255 - np.clip((t - lo) / (hi - lo + 1e-6), 0, 1) * 255, 20).astype(np.uint8)
im = Image.new('RGB', (S * 3, S)); im.paste(a, (0, 0)); im.paste(Image.fromarray(b), (S, 0)); im.paste(Image.fromarray(c).convert('RGB'), (S * 2, 0))
d = ImageDraw.Draw(im)
for i, n in enumerate(['normal', 'piece ids (no outline)', 'depth (near = bright)']): d.text((i * S + 6, 4), n, fill=(255, 220, 120))
im.save('crown_diag.png')
u, cnt = np.unique(idx[hit], return_counts=True); print({names[k] if k < len(names) else k: int(n) for k, n in zip(u, cnt)})
