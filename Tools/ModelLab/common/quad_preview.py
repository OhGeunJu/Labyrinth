"""네 발 짐승 미리보기 공용 — 도우미 + 한 장 판(옆 · 비스듬히 · 정면 · 뒤 · 내려다보기 · 사람 치비 눈금 · 머리 가까이).

부위는 게임 네발 관절 규격(`PawnRigBuilder.RigQuadruped` — Body · Head · Tail · LegFL/FR/BL/BR)으로 나눈다:
조각마다 `(거리 함수, 색, 부위)` — 부위는 뒤에 굽기(뼈 가중치)에서 쓴다. 미리보기 렌더러는 앞 둘만 본다.
단위 m · 발이 원점(y=0) · +z 앞 · +x 짐승의 왼쪽."""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _here); _sys.path.insert(0, _os.path.join(_here, '..', 'dwarf'))
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, sph, seg
import head_base as hb

UP = np.array([0, 1, 0], np.float32)
def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
def union(ds):
    out = ds[0]
    for d in ds[1:]: out = np.minimum(out, d)
    return out
def sunion(ds, k):
    out = ds[0]
    for d in ds[1:]: out = smin(out, d, k)
    return out
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def mixc(a, b, k): return np.asarray(a, np.float32) * (1 - k[..., None]) + np.asarray(b, np.float32) * k[..., None]
def flat(p, n, k):
    """법선 n 쪽으로 k배 납작하게 본 좌표 — 돌려준 점으로 잰 거리에 k를 곱해 쓴다."""
    n = nrm(n); h = (p * n).sum(-1); return p + n * (h / k - h)[..., None]
def frame(f, u):
    """앞 f · 위 u를 축으로 하는 바탕(열 = 옆 · 위 · 앞) — `(p - c) @ R`이 그 바탕의 좌표."""
    f = nrm(f); s = nrm(np.cross(u, f)); u = np.cross(f, s); return np.stack([s, u, f], 1)
def oell(p, c, R, r):
    """돌린 타원체 — R은 `frame`이 준 바탕 · r은 (옆, 위, 앞) 반지름."""
    r = np.asarray(r, np.float32); q = (p - c) @ R; k0 = length(q / r); k1 = length(q / (r * r))
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-6)
def tuft(m, a, dr, ln, r, n=None, k=0.55):
    """털 술 · 가시 — 밑동이 굵고 끝이 뾰족한 납작한 뿔."""
    dr = nrm(dr); n = nrm(np.cross(dr, UP) if n is None else n)
    return seg(flat(m - a, n, k) + a, a - dr * r * 0.6, a + dr * ln, r, 0.004) * k
def chain(p, pts, rs, k=0.03):
    d = far(p)
    for i in range(len(pts) - 1): d = smin(d, seg(p, pts[i], pts[i + 1], rs[i], rs[i + 1]), k)
    return d
def chain_t(p, pts):
    """마디 줄 위에서 가장 가까운 자리의 길이 몫(0 = 첫 점 · 1 = 끝) — 꼬리 고리 무늬 같은 색에."""
    L = [float(np.linalg.norm(pts[i + 1] - pts[i])) for i in range(len(pts) - 1)]; tot = sum(L)
    best = np.full(p.shape[:-1], 9.0, np.float32); tt = np.zeros(p.shape[:-1], np.float32); acc = 0.0
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]; ba = b - a
        h = np.clip(((p - a) * ba).sum(-1) / float((ba * ba).sum()), 0, 1)
        dd = length(p - a - ba * h[..., None]); m = dd < best
        best = np.where(m, dd, best); tt = np.where(m, (acc + h * L[i]) / tot, tt); acc += L[i]
    return tt

def surface_along(f, o, dr, lo=0.0, hi=1.5):
    """점 o에서 방향 dr로 나가며 거리장 f의 표면(안 → 밖)을 찾는다 — 털 술 · 가시의 뿌리 자리."""
    o = np.asarray(o, np.float32); dr = nrm(dr); a, b = lo, hi
    for _ in range(40):
        c = 0.5 * (a + b)
        if f((o + dr * c)[None, :])[0] < 0: a = c
        else: b = c
    return o + dr * (0.5 * (a + b))
def fluff(p):
    """털 덩이 — 부드러운 울퉁불퉁(-1 ~ 1) · 표면에서 빼면 복슬복슬한 윤곽."""
    x, y, z = p[..., 0], p[..., 1], p[..., 2]
    a = np.sin(x * 31 + 2 * np.sin(z * 17)) * np.sin(y * 29 + z * 23)
    b = np.sin(z * 37 + y * 13 + 2 * np.sin(x * 19))
    return 0.5 * a + 0.5 * b

def polyline(p, pts, r):
    d = far(p)
    for a, b in zip(pts, pts[1:]): d = np.minimum(d, seg(p, a, b, r, r))
    return d
def mouth_path(f, axis_at, nose_c, split_z, branch):
    """설치류 · 두더지 입 — 코 밑 인중이 내려와 갈라져 양옆 · 뒤로 가는 '人'자 선을 살 표면 위에서 찾는다.
    `axis_at(z)` — 주둥이 가운데 줄의 자리 · `branch` — [(z, 바깥 몫)…](0 = 바로 아래 · 1 = 옆). 돌려주는 것: 인중 두 점 + 오른쪽 가지 점들(+x · 거울로 쓴다)."""
    top = surface_along(f, nose_c, V(0, -0.75, 0.66))
    split = surface_along(f, axis_at(split_z), V(0, -1, 0))
    side = [surface_along(f, axis_at(z), V(k, -np.sqrt(max(1 - k * k, 0.0)), 0)) for z, k in branch]
    return [top, split], [split] + side
def mouth_cut(p, path, r):
    """입 홈 — 살에서 빼는 거리장(거울)."""
    ph, br = path; m = mirror(p)
    return np.minimum(polyline(p, ph, r), polyline(m, br, r))
def mouth_inner(p, path, r, sink, philtrum=True):
    """입 홈 바닥의 어두운 줄 — 홈 안으로 `sink`만큼 들어간 가는 관(살 위로 안 나온다).
    `philtrum=False` — 인중(코 밑 세로 줄)에는 안 깐다(홈만 남는다 — 11-19 괴물쥐: *"코 밑에 검은 선은 왜 있는 거야"*)."""
    ph, br = path; m = mirror(p)
    ph = [a + V(0, sink, -sink * 0.5) for a in ph]; br = [a + V(-sink * 0.6, sink, 0) for a in br]
    d = polyline(m, br, r)
    return np.minimum(polyline(p, ph, r), d) if philtrum else d
def eye_paint(base, center, n, R, spots=((0.80, 0.38, -0.30, 0.34), (0.90, -0.30, 0.25, 0.16))):
    """눈 색 — 반짝임을 **칠한다**(튀어나온 공이 아니다). spots — (앞 · 위 · 옆 몫, 각 반지름 라디안)."""
    n = nrm(n); t = nrm(np.cross(UP, n)); u = np.cross(n, t)
    dirs = [(nrm(n * a + u * b + t * c), r) for a, b, c, r in spots]
    def col(p):
        q = mirror(p) - center; q = q / np.maximum(length(q), 1e-6)[..., None]
        k = np.zeros(p.shape[:-1], np.float32)
        for d, r in dirs: k = np.maximum(k, sstep(np.cos(r), np.cos(r * 0.7), (q * d).sum(-1)))
        return mixc(base, (1, 1, 1), k)
    return col

def shift(P, o):
    o = np.asarray(o, np.float32)
    return [((lambda f: lambda p: f(p - o))(pc[0]), ((lambda g: lambda p: g(p - o))(pc[1]) if callable(pc[1]) else pc[1])) for pc in P]
def plain(P): return [(pc[0], pc[1]) for pc in P]

def _human():
    import body as bd
    P, _ = bd.figure(bd.MALE, bd.dw.b_box, bd.dw.h_bald, bd.dw.BROWN, bd.dw.e_bushy); return P

# (yaw, 폭, 내려다보는 각) — 90 = 옆(머리가 왼쪽) · 0 = 정면
FULL = ((90, 640, 0), (40, 560, 0), (0, 440, 0), (145, 560, 0), (35, 560, 50))
def _job(a):
    kind, mod, i = a
    m = __import__(mod)
    if kind == 'full':
        yaw, w, pitch = FULL[i]; cy, span = m.VIEW
        if pitch: cy, span = cy * 0.5, span * 1.15   # 내려다보면 몸이 화면 위로 눕는다
        return np.asarray(hb.render(plain(m.pieces()), yaw, w, 480, cy, span, pitch=pitch))
    if kind == 'scale':
        mid = V(0, 0, m.SCALE_Z * 0.5)       # 짐승과 사람 사이를 가운데로
        Q = shift(plain(m.pieces()), -mid) + shift(_human(), V(0, 0, m.SCALE_Z) - mid)
        return np.asarray(hb.render(Q, 90, 640, 480, getattr(m, 'SCALE_SPAN', 2.05) * 0.42, getattr(m, 'SCALE_SPAN', 2.05)))
    yaw, w, pitch = m.CLOSE[i]
    return np.asarray(hb.render(shift(plain(m.pieces(head_only=True)), -getattr(m, 'CLOSE_C', m.HEAD_C)), yaw, w, 420, 0.0, m.CLOSE_SPAN, pitch=pitch))

def sheet(mod, stem, title):
    """한 장 판 — 첫 줄: 옆 · 비스듬히 · 정면 / 둘째 줄: 뒤 · 내려다보기 · 사람 치비 눈금 / 셋째 줄: 머리 가까이."""
    from multiprocessing import Pool
    m = __import__(mod)
    jobs = [('full', mod, i) for i in range(len(FULL))] + [('scale', mod, 0)] + [('close', mod, i) for i in range(len(m.CLOSE))]
    with Pool(min(len(jobs), _os.cpu_count() or 4)) as pool: res = pool.map(_job, jobs)
    top = res[:len(FULL) + 1]; low = res[len(FULL) + 1:]
    rows = [top[:3], top[3:], low]
    wd = max(sum(t.shape[1] for t in r) for r in rows); hs = [max(t.shape[0] for t in r) for r in rows]
    im = Image.new('RGB', (wd, sum(hs)), (40, 40, 44)); dr = ImageDraw.Draw(im); y = 0
    for r, h in zip(rows, hs):
        x = 0
        for t in r: im.paste(Image.fromarray(t), (x, y)); x += t.shape[1]
        y += h
    dr.text((6, 4), title, fill=(255, 220, 120))
    _os.makedirs(_os.path.dirname(_os.path.abspath(stem)) or '.', exist_ok=True)
    im.save(stem + '.png'); print('saved', stem + '.png')
