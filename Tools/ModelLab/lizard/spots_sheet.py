"""도마뱀 점 무늬 비교 — 예전(고른 동그라미 · 격자 7.5cm) ↔ 지금(성긴 찌그러진 반점 · 11cm). C# `LizSpots`를 그대로 옮겨 평면(몸 겉면 대신 z = 0.03)에 찍는다.
실행: python spots_sheet.py → renders/lizard_spots.png"""
import os, numpy as np
from PIL import Image, ImageDraw, ImageFont
def H(c, k):
    v = np.sin(c[..., 0] * 127.1 + c[..., 1] * 311.7 + c[..., 2] * 74.7 + k) * 43758.5453; return v - np.floor(v)
def ss(e0, e1, x): t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def old(p, cell=0.075, r=0.020):
    g = np.floor(p / cell); best = np.full(p.shape[:-1], 9.0)
    for d in np.stack(np.meshgrid(*[[-1, 0, 1]] * 3, indexing='ij'), -1).reshape(-1, 3):
        c = g + d; at = (c + np.stack([0.2 + 0.6 * H(c, 0), 0.2 + 0.6 * H(c, 1.3), 0.2 + 0.6 * H(c, 2.7)], -1)) * cell
        rr = r * (0.75 + 0.5 * H(c, 4.1)); best = np.minimum(best, np.linalg.norm(p - at, axis=-1) - rr)
    return ss(0.004, -0.004, best)
def new(p, cell=0.110, r=0.030, keep=0.55):
    g = np.floor(p / cell); best = np.full(p.shape[:-1], 9.0)
    def nz(v): return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-9)
    for d in np.stack(np.meshgrid(*[[-1, 0, 1]] * 3, indexing='ij'), -1).reshape(-1, 3):
        c = g + d; on = H(c, 9.7) <= keep
        at = (c + np.stack([0.2 + 0.6 * H(c, 0), 0.2 + 0.6 * H(c, 1.3), 0.2 + 0.6 * H(c, 2.7)], -1)) * cell
        rr = r * (0.6 + 1.0 * H(c, 4.1))
        d1 = nz(np.stack([H(c, 5.3) - .5, H(c, 6.1) - .5, H(c, 7.9) - .5], -1)); d2 = nz(np.stack([H(c, 8.3) - .5, H(c, 3.3) - .5, H(c, 2.1) - .5], -1))
        a = np.linalg.norm(p - at, axis=-1) - rr
        b = np.linalg.norm(p - (at + d1 * (rr * 0.75)[..., None]), axis=-1) - rr * 0.65
        e = np.where(H(c, 6.6) < 0.5, np.linalg.norm(p - (at + d2 * (rr * 0.70)[..., None]), axis=-1) - rr * 0.55, 9.0)
        best = np.where(on, np.minimum(best, np.minimum(a, np.minimum(b, e))), best)
    best = best + 0.004 * np.sin(p[..., 0] * 61 + p[..., 1] * 47) * np.sin(p[..., 2] * 53 - p[..., 1] * 29)
    return ss(0.004, -0.004, best)
S, W = 0.8, 360   # 80cm 판
xs = np.linspace(0, S, W); X, Y = np.meshgrid(xs, xs); P = np.stack([X, Y, np.full_like(X, 0.03)], -1)
here = os.path.dirname(os.path.abspath(__file__))
TONES = [('Tokay', (0.48, 0.62, 0.86), (0.95, 0.45, 0.15)), ('Leopard', (0.95, 0.74, 0.16), (0.20, 0.12, 0.10)), ('Phelsuma', (0.40, 0.75, 0.18), (0.84, 0.18, 0.10)), ('Leachie', (0.24, 0.14, 0.15), (0.64, 0.22, 0.32))]
mo, mn = old(P), new(P)
im = Image.new('RGB', (W * len(TONES) + 10 * (len(TONES) - 1), W * 2 + 60), (40, 40, 44)); dr = ImageDraw.Draw(im)
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 16) if os.path.exists('C:/Windows/Fonts/malgun.ttf') else None
for i, (name, sk, sp) in enumerate(TONES):
    for r, m in enumerate((mo, mn)):
        c = np.asarray(sk)[None, None] * (1 - m[..., None]) + np.asarray(sp)[None, None] * m[..., None]
        im.paste(Image.fromarray((c * 255).astype(np.uint8)), (i * (W + 10), 30 + r * (W + 30)))
    dr.text((i * (W + 10) + 6, 6), name + ' — 예전', fill=(230, 230, 230), font=font)
    dr.text((i * (W + 10) + 6, W + 36), name + ' — 지금', fill=(255, 220, 120), font=font)
out = os.path.join(here, 'renders', 'lizard_spots.png'); im.save(out); print('saved', out)

# ── 배치 8벌(`LizSpotShift` — 배치마다 (1.37, 2.11, 0.73) × 번호만큼 민다) ──
W8 = 240; xs8 = np.linspace(0, 0.6, W8); X8, Y8 = np.meshgrid(xs8, xs8)
im8 = Image.new('RGB', (W8 * 4 + 30, W8 * 2 + 60), (40, 40, 44)); dr8 = ImageDraw.Draw(im8)
sk, sp = np.asarray((0.48, 0.62, 0.86)), np.asarray((0.95, 0.45, 0.15))
for i in range(8):
    P8 = np.stack([X8 + i * 1.37, Y8 + i * 2.11, np.full_like(X8, 0.03 + i * 0.73)], -1); m = new(P8)
    c = sk[None, None] * (1 - m[..., None]) + sp[None, None] * m[..., None]
    x, y = (i % 4) * (W8 + 10), 30 + (i // 4) * (W8 + 30)
    im8.paste(Image.fromarray((c * 255).astype(np.uint8)), (x, y)); dr8.text((x + 6, y - 22), '배치 %d' % (i + 1), fill=(255, 220, 120), font=font)
out8 = os.path.join(here, 'renders', 'lizard_spots_layouts.png'); im8.save(out8); print('saved', out8)
