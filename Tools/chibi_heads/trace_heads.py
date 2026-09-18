# 림월드 머리형 열두 장(ref/)에서 살색 윤곽·눈 자리를 재어 heads.json으로 낸다.
# 다음은 gen_profiles.py (→ Assets/Editor/ChibiHeadProfiles.cs). 확인은 preview.py.
import numpy as np, os, sys, json
from PIL import Image

class ndimage:
    @staticmethod
    def label(mask):
        h, w = mask.shape
        lab = np.zeros((h, w), int); k = 0
        for y in range(h):
            for x in range(w):
                if mask[y, x] and lab[y, x] == 0:
                    k += 1; stack = [(y, x)]; lab[y, x] = k
                    while stack:
                        cy, cx = stack.pop()
                        for ny, nx in ((cy-1,cx),(cy+1,cx),(cy,cx-1),(cy,cx+1)):
                            if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and lab[ny, nx] == 0:
                                lab[ny, nx] = k; stack.append((ny, nx))
        return lab, k
    @staticmethod
    def sum(mask, lab, ids):
        return [int((lab == i).sum()) for i in ids]

D = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'ref')
names = ['Male_AverageNormal','Male_AveragePointy','Male_AverageWide','Male_NarrowNormal','Male_NarrowPointy','Male_NarrowWide',
         'Female_AverageNormal','Female_AveragePointy','Female_AverageWide','Female_NarrowNormal','Female_NarrowPointy','Female_NarrowWide']

def soft_skin(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    luma = 0.3 * r + 0.5 * g + 0.2 * b
    return np.clip((luma - 50) / 70.0, 0, 1)

def edge_left(row, thr=0.5):
    idx = np.where(row >= thr)[0]
    if len(idx) == 0: return None
    i = idx[0]
    if i == 0: return 0.0
    a, b = row[i - 1], row[i]
    return (i - 1) + (thr - a) / (b - a) if b > a else float(i)

def edge_right(row, thr=0.5):
    idx = np.where(row >= thr)[0]
    if len(idx) == 0: return None
    i = idx[-1]
    if i == len(row) - 1: return float(i)
    a, b = row[i], row[i + 1]
    return i + (a - thr) / (a - b) if a > b else float(i)

out = {}
for n in names:
    a = np.array(Image.open(os.path.join(D, f'{n}.png')).convert('RGBA')).astype(float)
    m = soft_skin(a)
    hard = m >= 0.5
    lab, k = ndimage.label(hard)
    sizes = ndimage.sum(hard, lab, range(1, k + 1))
    # head = component containing the topmost skin pixel
    ys, xs = np.where(hard)
    top = ys.argmin()
    head_id = lab[ys[top], xs[top]]
    head = lab == head_id
    rest = [(s, j + 1) for j, s in enumerate(sizes) if j + 1 != head_id]
    rest.sort(reverse=True)
    body = lab == rest[0][1]
    hands = [lab == j for s, j in rest[1:] if s > 30]

    hy, hx = np.where(head)
    y0, y1 = hy.min(), hy.max()
    # fill eye holes for the outline: use soft mask but only between outer edges
    rows = []
    for y in range(y0, y1 + 1):
        row = m[y].copy()
        # restrict to the head component's span (avoid hands)
        span = np.where(head[y])[0]
        if len(span) == 0: continue
        lo, hi = max(0, span.min() - 1), min(len(row) - 1, span.max() + 1)
        r2 = np.zeros_like(row); r2[lo:hi + 1] = row[lo:hi + 1]
        l, r = edge_left(r2), edge_right(r2)
        rows.append((y, l, r))
    ys_ = np.array([r[0] for r in rows], float)
    ls = np.array([r[1] for r in rows]); rs = np.array([r[2] for r in rows])
    # subpixel top/bottom: extend half a pixel
    ytop, ybot = ys_.min() - 0.5, ys_.max() + 0.5
    H = ybot - ytop
    cx = np.median((ls + rs) / 2)
    halfw = np.maximum((rs - ls) / 2, 0)
    W = halfw.max() * 2
    # eyes: dark pixels inside head bbox rows, enclosed (skin to left and right on the same row)
    dark = m < 0.5
    inside = np.zeros_like(dark)
    for (y, l, r) in rows:
        inside[y, int(np.ceil(l)) + 1:int(np.floor(r))] = True
    holes = dark & inside
    hl, hk = ndimage.label(holes)
    eyes = []
    for j in range(1, hk + 1):
        py, px = np.where(hl == j)
        if len(py) < 6: continue
        eyes.append(dict(cx=float(px.mean()), cy=float(py.mean()), w=float(px.max() - px.min() + 1), h=float(py.max() - py.min() + 1), n=int(len(py))))
    eyes.sort(key=lambda e: e['cx'])
    # 진한 알맹이만 — 번짐(안티앨리어싱)과 속눈썹을 뺀다
    luma = 0.3 * a[..., 0] + 0.5 * a[..., 1] + 0.2 * a[..., 2]
    cores = []
    for j in range(1, hk + 1):
        py, px = np.where(hl == j)
        if len(py) < 6: continue
        sel = luma[py, px] < 45
        py, px = py[sel], px[sel]
        if len(py) < 4: continue
        widths = {y: (px[py == y].max() - px[py == y].min() + 1) for y in np.unique(py)}
        mw = max(widths.values())
        rows_ = [y for y, w_ in widths.items() if w_ >= 0.6 * mw]
        b = np.isin(py, rows_)
        bx, by = px[b], py[b]
        cores.append(dict(cx=float(bx.mean()), cy=float(by.mean()), w=float(bx.max()-bx.min()+1), h=float(by.max()-by.min()+1)))
    cores.sort(key=lambda e: e['cx'])
    # body
    by, bx = np.where(body)
    bH = by.max() - by.min() + 1
    # body width below the hands: measure widest row in the lower 60% of the body
    bw = [np.where(body[y])[0] for y in range(by.min() + int(bH * 0.35), by.max() + 1)]
    bW = max(w_.max() - w_.min() + 1 for w_ in bw if len(w_))
    # whole figure: head top to body bottom
    figH = by.max() + 0.5 - ytop
    handW = [np.where(h.any(axis=0))[0] for h in hands]
    handW = [w_.max() - w_.min() + 1 for w_ in handW if len(w_)]

    # profile: t from 0 (chin) to 1 (top); sample half-width normalised by W (so max 0.5)
    t_rows = (ybot - (ys_ + 0.5 - 0.5)) / H   # row center y+0.5 → t
    t_rows = (ybot - (ys_ + 0.5)) / H
    prof = halfw / W
    out[n] = dict(H=H, W=W, aspect=W / H, cx=cx, t=t_rows.tolist(), r=prof.tolist(),
                  eyes=[dict(t=(ybot - e['cy'] - 0.5) / H, dx=(e['cx'] - cx) / W, w=e['w'] / W, h=e['h'] / H, n=e['n']) for e in eyes],
                  cores=[dict(t=(ybot - e['cy']) / H, dx=(e['cx'] - cx) / W, wpx=e['w'], hpx=e['h']) for e in cores],
                  bodyH=bH, bodyW=bW, figH=figH, headOverBody=H / bH, headWOverBodyW=W / bW, headOverFig=H / figH,
                  headBottomToBodyTop=(by.min() - ybot), handW=[w / W for w in handW])
    e = out[n]
    print(n, 'cores', [(round(c['t'],3), round(c['dx'],3), c['wpx'], c['hpx']) for c in out[n]['cores']])
    print(f"{n:22s} W {W:5.1f} H {H:5.1f} asp {W/H:.3f}  body W {bW} H {bH}  head/bodyW {W/bW:.3f} head/bodyH {H/bH:.3f} head/fig {H/figH:.3f} overlap {by.min()-ybot:.1f}  eyes {[(round(x['t'],3), round(x['dx'],3), round(x['w'],3), round(x['h'],3)) for x in e['eyes']]} hands {[round(w,2) for w in e['handW']]}")

json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'heads.json'), 'w'), indent=1, default=float)
