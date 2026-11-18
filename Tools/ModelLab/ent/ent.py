"""엔트(나무 종족) — **나무 느낌의 사람이 아니라, 그냥 나무에 어거지로 사람 느낌만 낸 것**(사용자 11-12 · 그림 둘 `_refs/2026-11-12_ent/`).
첫 판의 안 셋(고목 · 고사목 · 그루터기) 가운데 고사목만 통과했고, 그것도 "사람 몸에 껍질"이라 다시 빚었다. 지금 규칙:
  - **몸통과 머리는 한 줄기다** — 어깨 · 목 · 머리통이 없다. 줄기가 비틀리며 올라가다 꼭대기에서 굵은 가지 몇으로 갈라지고 잔가지 관으로 흩어진다.
  - **다리는 줄기가 밑에서 둘로 갈라진 것**(뒤집힌 Y) — 굵기가 서로 다르고, 발은 사방으로 뻗은 뿌리.
  - **팔은 줄기 옆에서 뻗은 가지** — 좌우 높이 · 굵기 · 길이가 다르다(한쪽 손이 크다) · 손은 길게 늘어진 가지 손가락 · 팔꿈치 · 어깨에서 곁가지가 난다.
  - **얼굴은 겨우 알아볼 만큼만 — 눈만 사람 눈이다**(어둡게 탄 주황 바탕 · 그보다 밝은 홍채 · 눈동자 · 껍질 눈꺼풀) · 코 · 입은 나무(가지 · 옹이 · 틈). **종류마다 얼굴이 다르다**(`FACES`).
  - **잎은 가지에만 달리고 계절은 종류마다 하나다** · 이끼 · 옹이 · 버섯 · 덩굴 · 겨우살이는 **장식**(붙은 개체도 안 붙은 개체도 있다 — `DECOR`).
  - 껍질 골은 **비틀려 감아 오른다**(곧은 세로 골은 기둥 같다) · 몸 여기저기 빈 옹이.
뼈대는 사람 것 그대로(관절 높이는 `dwarf/body.py` `dims` — 체형 한 줄) — 좌우가 다른 것은 살뿐이다.
종류 넷(관의 모양과 몸집 — `KINDS`): oak 굵은 몸 · 넓은 관 · 큰 왼손 / slim 가늘고 키 큰 몸 · 위로 솟은 관 / willow 굽은 몸 · 늘어진 버들가지 / snag 부러진 고목. **넷 다 봄 판 · 가을 판**(색만 다르다 — `PAL`).
단위 m · 발이 원점 · +z 앞. 실행: python ent.py renders/ent_v14 [full|heads|decor|bark|cmp](한 판 5~6분)"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.join(_here, '..', 'common')); _sys.path.insert(0, _os.path.join(_here, '..', 'dwarf'))
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, smin, smax, ell, sph, capsule, seg
import head_base as hb
import body as bd

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
def mixc(a, b, k): return np.asarray(a, np.float32) * (1 - k[..., None]) + np.asarray(b, np.float32) * k[..., None]
def noise(p, f=1.0):   # 싼 얼룩 — 사인 셋
    x, y, z = p[..., 0] * f, p[..., 1] * f, p[..., 2] * f
    return 0.5 * np.sin(x * 9.0 + y * 4.0) + 0.3 * np.sin(z * 13.0 - y * 7.0 + 1.3) + 0.2 * np.sin((x + z) * 21.0 + y * 11.0)
def bounded(f, p, c, r):
    b = length(p - c) - r
    m = b < 0.03
    if not m.any(): return b
    out = b.copy(); out[m] = f(p[m]); return out
def accel(f, c, rad):
    """조각 하나를 빠르게 — 둘레 공 밖은 공까지의 거리, 안은 성긴 격자에 미리 잰 값에서 칸 대각선만큼 뺀 것(아래로 어림)을 돌려주고,
    겉면 가까이(그 어림이 3cm 안)만 제대로 잰다. 가지 · 잎이 수백인 조각을 광선 걸음마다 다 재면 한 장에 2분이 걸렸다."""
    c = np.asarray(c, np.float32); cell = max(0.025, rad / 30.0); half = rad + 0.04
    n = int(np.ceil(2 * half / cell)) + 1; lo = c - half
    ax = np.arange(n, dtype=np.float32) * cell
    pts = np.stack(np.meshgrid(ax, ax, ax, indexing='ij'), -1).reshape(-1, 3) + lo
    G = np.concatenate([f(pts[i:i + 200000]) for i in range(0, len(pts), 200000)]).reshape(n, n, n).astype(np.float32)
    slack = cell * 0.87 + 0.004
    def g(p):
        b = length(p - c) - rad
        m = b < 0.03
        if not m.any(): return b
        out = b.copy(); q = p[m]
        ijk = np.clip(np.rint((q - lo) / cell), 0, n - 1).astype(np.int32); gv = G[ijk[:, 0], ijk[:, 1], ijk[:, 2]]
        res = gv - slack; nm = res < 0.03
        if nm.any(): res[nm] = f(q[nm])
        out[m] = res; return out
    g.c, g.rad = c, rad   # 뽑기가 이 둘레만 훑는다
    return g

DARK = (0.05, 0.04, 0.04); MOSS = (0.38, 0.46, 0.20); LEAF = (0.30, 0.50, 0.18); LEAF2 = (0.52, 0.68, 0.26); WILLOW = (0.46, 0.60, 0.22)
# 껍질 색 — 고르는 축(사용자 11-13: *"나무 색도 좀 짙은 거랑 추가해줘봐 · 다른 색도 생각나는 거 있으면"*). (껍질, 부러진 속살)
BARKS = {'ash':    ((0.47, 0.42, 0.35), (0.74, 0.62, 0.42)),   # 잿빛 갈색 — 처음부터 쓰던 색
         'walnut': ((0.27, 0.20, 0.15), (0.62, 0.46, 0.30)),   # 짙은 밤색
         'cedar':  ((0.46, 0.27, 0.18), (0.80, 0.56, 0.36)),   # 붉은 갈색
         'birch':  ((0.76, 0.74, 0.68), (0.86, 0.78, 0.60)),   # 자작 — 희끗한 껍질에 검은 골
         'frost':  ((0.56, 0.63, 0.66), (0.82, 0.86, 0.84)),   # 서리 — 푸르스름한 회백(게임의 3층 나무 서리나무와 맞출 수 있다)
         'ebony':  ((0.215, 0.20, 0.25), (0.40, 0.30, 0.27))}   # 흑단 — 검푸른 껍질(4층 나무 흑단)

#          이름        반폭(밑 엉덩이 허리 가슴 어깨)          깊이  손    팔K   다리K  몸통K 팔굵기 다리굵기 머리 어깨둥글림
HEAVY = ('EntHeavy', [0.26, 0.28, 0.27, 0.32, 0.34], 0.90, 0.36, 1.75, 2.40, 1.00, 1.0, 1.0, 1.0, 0.45)
SLIM  = ('EntSlim',  [0.19, 0.21, 0.20, 0.24, 0.26], 0.88, 0.32, 1.75, 2.65, 1.02, 1.0, 1.0, 1.0, 0.45)

def groove(th, y, n):   # 껍질 골 — 넓은 등성이 사이의 좁은 골(1이 골 바닥)
    return (1 - np.abs(np.sin(n * th * 0.5 + 1.2 * np.sin(3.5 * y + th * 2.0)))) ** 1.5

class Trunk:
    """줄기 — 높이마다 반폭을 잇는 통. 골이 비틀려 감아 오르고(twist), 좌우로 구불거리고(sway), 단면이 울퉁불퉁하다(lobe), 위로 갈수록 앞으로 기운다(lean)."""
    def __init__(s, prof, depth, n=9, amp=0.028, lean=0.0, y_lean=0.0, twist=1.6, sway=0.03, lobe=0.09):
        s.ys = np.array([a for a, _ in prof], np.float32); s.rs = np.array([b for _, b in prof], np.float32)
        s.depth, s.n, s.amp, s.lean, s.y_lean, s.twist, s.sw, s.lobe = depth, n, amp, lean, y_lean, twist, sway, lobe
    def zs(s, y): return s.lean * np.maximum(y - s.y_lean, 0) ** 2
    def sway(s, y): return s.sw * np.sin((y - s.ys[0]) * 2.6 + 0.6)
    def axis(s, y): return V(float(s.sway(y)), y, float(s.zs(y)))
    def polar(s, p):
        x = p[..., 0] - s.sway(p[..., 1]); z = p[..., 2] - s.zs(p[..., 1])
        return np.sqrt(x ** 2 + (z / s.depth) ** 2), np.arctan2(x, z) + s.twist * p[..., 1]
    def raw(s, p):   # 골 없는 통
        q, th = s.polar(p); y = p[..., 1]
        d = (q - np.interp(y, s.ys, s.rs) * (1 + s.lobe * np.cos(2 * th + 1.3 * y))) * s.depth
        return smax(smax(d, y - s.ys[-1], 0.05), s.ys[0] - y, 0.06)
    def __call__(s, p):
        _, th = s.polar(p)
        return (s.raw(p) + s.amp * groove(th, p[..., 1], s.n)) * 0.65
    def front(s, y, x=0.0):   # 높이 y에서 줄기 앞면의 z(x는 줄기 축에서 잰 옆 자리)
        c = s.axis(y); lo, hi = 0.0, 0.8
        for _ in range(24):
            mid = (lo + hi) * 0.5
            if float(s.raw(np.array([[c[0] + x, y, c[2] + mid]], np.float32))[0]) > 0: hi = mid
            else: lo = mid
        return c[2] + lo
    def side(s, y): return float(np.interp(y, s.ys, s.rs))
    def color(s, bark, moss=MOSS, split=False):
        """split이면 (껍질 몫 rgb, 이끼 몫 a) — 게임 셰이더가 `lerp(껍질 색, 이끼 색, a) × 몫`으로 칠한다(`_VertexAlt`). 몫은 이끼 쪽으로 1에 가깝게 당겨 둔다."""
        B = np.asarray(bark, np.float32)
        def c(p):
            _, th = s.polar(p)
            col = mixc(B * (1.0 + 0.12 * noise(p, 1.7)[..., None]), B * 0.42, np.clip(groove(th, p[..., 1], s.n) * 1.4, 0, 1))
            k = sstep(0.25, 0.60, noise(p, 2.9) - 1.6 * (p[..., 2] - s.zs(p[..., 1])) - 0.15)   # 이끼는 등 쪽 얼룩으로만
            if split: a = k * 0.7; return np.concatenate([col * (1 - a[..., None]) + a[..., None], a[..., None]], -1)
            return mixc(col, moss, k * 0.7)
        return c

def limb(p, a, b, ra, rb, n=6, amp=0.012):
    """가지 — 굵기가 변하는 대에 비틀린 골."""
    a, b = np.asarray(a, np.float32), np.asarray(b, np.float32); ba = b - a
    pa = p - a; h = np.clip((pa * ba).sum(-1) / (ba * ba).sum(), 0, 1)
    rad = pa - ba * h[..., None]; d = length(rad) - (ra + (rb - ra) * h)
    ax = ba / np.linalg.norm(ba); e1 = nrm(np.cross(ax, [0.31, 0.2, 0.93])); e2 = np.cross(ax, e1)
    th = np.arctan2((rad * e2).sum(-1), (rad * e1).sum(-1)) + 2.2 * h
    return (d + amp * groove(th, h * 1.5, n)) * 0.8
def bark_plain(bark, split=False):   # 가지 껍질 — 얼룩 + 가는 결
    B = np.asarray(bark, np.float32)
    def c(p):
        streak = sstep(0.55, 0.95, np.sin(p[..., 0] * 70.0 + p[..., 2] * 55.0 + 2.0 * np.sin(p[..., 1] * 9.0)))
        col = mixc(B * (1.0 + 0.12 * noise(p, 1.7)[..., None]), B * 0.50, streak * 0.8)
        return np.concatenate([col, np.zeros(col.shape[:-1] + (1,), np.float32)], -1) if split else col
    return c

def grow(base, dr, ln, r, depth, rng, spread=0.75, shrink=0.72, up=0.25):
    """가지치기 — 한 가지가 둘로 갈라지기를 depth번. (마디 목록, 끝점 목록)을 돌려준다."""
    segs, tips = [], []
    def rec(a, d, ln, r, k):
        d = nrm(d); b = a + d * ln
        segs.append((a, b, r, max(r * 0.62, 0.008)))
        if k == 0: tips.append((b, d)); return
        for _ in range(2):
            perp = nrm(np.cross(d, rng.normal(size=3)))
            rec(b, d + perp * spread * rng.uniform(0.55, 1.15) + V(0, up, 0), ln * shrink * rng.uniform(0.8, 1.15), max(r * 0.62, 0.008), k - 1)
    rec(np.asarray(base, np.float32), np.asarray(dr, np.float32), ln, r, depth)
    return segs, tips
def twigs(segs):
    """마디 묶음 — 마디를 한꺼번에 잰다(점 4만 개씩). 마디끼리는 굵기가 이어져 있어 매끈하게 녹일 것이 없다."""
    A = np.array([a for a, *_ in segs], np.float32); B = np.array([b for _, b, *_ in segs], np.float32)
    RA = np.array([r for *_, r, _ in segs], np.float32); RB = np.array([r for *_, r in segs], np.float32)
    BA = B - A; BB = (BA * BA).sum(-1)
    pts = np.concatenate([A, B]); c = (pts.min(0) + pts.max(0)) * 0.5; rad = float(length(pts - c).max()) + 0.10
    def f(p):
        out = np.empty(p.shape[0], np.float32)
        for i in range(0, p.shape[0], 16000):
            pa = p[i:i + 16000, None, :] - A[None]
            h = np.clip((pa * BA[None]).sum(-1) / BB[None], 0, 1)
            out[i:i + 16000] = (length(pa - BA[None] * h[..., None]) - (RA[None] + (RB - RA)[None] * h)).min(-1)
        return out
    g = accel(f, c, rad); g.segs = segs   # 뽑기는 마디를 도형으로 직접 짓는다(가는 대는 표면 그물로 뭉개진다)
    return g
def root_foot(p, c, r, toes):
    """뿌리 발 — 발목에서 땅으로 벌어지는 뿌리. toes = (옆, 앞, 길이) 목록."""
    d = sph(p, c + V(0, 0.02, 0), r)
    for sx, sz, ln in toes:
        dr = nrm([sx, 0, sz]); m = c + dr * (r * 0.9) + V(0, -c[1] * 0.45, 0); b = c + dr * (r * 0.9 + ln); b[1] = 0.022
        d = smin(d, np.minimum(seg(p, c, m, r * 0.72, r * 0.50), seg(p, m, b, r * 0.50, 0.016)), 0.03)
    return d
def leafy(p, blobs, bump=0.020, f=19.0):
    d = far(p)
    for c, r in blobs: d = smin(d, sph(p, c, r), 0.05)
    return (d + bump * np.sin(p[..., 0] * f) * np.sin(p[..., 1] * f + 1.0) * np.sin(p[..., 2] * f + 2.0)) * 0.7
def leaf_col(p): return mixc(LEAF, LEAF2, sstep(-0.2, 0.5, noise(p, 2.6) + 0.5 * (p[..., 1] - 2.2)))

def leaves(L, cols):
    """잎 묶음 — (밑동, 뻗는 쪽, 바깥, 길이, 반폭) 목록. 납작한 물방울 잎을 한꺼번에 잰다. 잎마다 `cols` 가운데 한 색(조각은 하나 — 색은 가장 가까운 잎의 것)."""
    A = np.array([l[0] for l in L], np.float32); Dr = np.array([nrm(l[1]) for l in L], np.float32)
    Up = np.array([nrm(np.asarray(l[2], np.float32) - d * float(np.dot(l[2], d))) for l, d in zip(L, Dr)], np.float32); Sd = np.cross(Dr, Up)
    LN = np.array([l[3] for l in L], np.float32); WD = np.array([l[4] for l in L], np.float32)
    CI = np.array(cols, np.float32)[np.random.default_rng(len(L)).integers(0, len(cols), len(L))]
    c = (A.min(0) + A.max(0)) * 0.5; rad = float(length(A - c).max()) + float(LN.max()) + 0.05
    def each(p):
        pa = p[:, None, :] - A[None]
        u, v, w = (pa * Dr[None]).sum(-1), (pa * Sd[None]).sum(-1), (pa * Up[None]).sum(-1) / 0.22
        h = np.clip(u / LN[None], 0, 1); r = WD[None] * np.sin(np.pi * h ** 0.75) + 0.004
        return (np.sqrt((u - h * LN[None]) ** 2 + v * v + w * w) - r) * 0.44
    def f(p):
        out = np.empty(p.shape[0], np.float32)
        for i in range(0, p.shape[0], 8000): out[i:i + 8000] = each(p[i:i + 8000]).min(-1)
        return out
    def col(p):
        out = np.empty((p.shape[0], 3), np.float32)
        for i in range(0, p.shape[0], 8000): out[i:i + 8000] = CI[each(p[i:i + 8000]).argmin(-1)]
        return out
    g = accel(f, c, rad); g.leafdata = (A, Dr, Up, Sd, LN, WD, CI)   # 뽑기는 잎을 도형으로 직접 짓는다(종이처럼 얇다)
    return g, col

# 종류 넷 — 몸집과 관의 꼴. **계절은 종류에 안 묶인다 — 넷 다 봄 판 · 가을 판이 있다**(사용자 11-13: *"네 개의 나무 다 가을버전 봄버전 — 그냥 색만 바꾸면 되잖아"*).
# 한 몸에 두 계절을 섞지는 않는다(11-12: *"봄 나무면 봄나무고 가을 나무면 가을나무지"*). 잎은 가지 끝에만 달린다(몸에 붙이면 *"중요부위를 가리는 털"* 같다).
#   oak 굵은 몸 · 넓게 벌어진 관 · 큰 왼손 / slim 가늘고 키 큰 몸 · 위로 솟은 관 / willow 굽은 몸 · 늘어진 버들가지 /
#   snag 부러진 고목 — 꼭대기가 비스듬히 부러져 속이 비었고, 살아남은 굵은 가지 하나가 한쪽으로 뻗어 잎을 단다(옛 `bare` · `leafy`가 봄 · 가을로 겹쳐 새로 넣었다)
#          체형   굽힘   팔 굵기(왼 · 오른)  손가락 길이(왼 · 오른)  다리 굵기(왼 · 오른)  관        씨앗
KINDS = {'oak':    (HEAVY, 0.10, (1.55, 0.90), (0.72, 0.34), (1.15, 0.92), 'wide',   7),
         'slim':   (SLIM,  0.07, (0.90, 1.05), (0.34, 0.44), (0.90, 1.00), 'up',     3),
         'willow': (HEAVY, 0.22, (1.05, 1.20), (0.40, 0.52), (1.00, 1.15), 'willow', 11),
         'snag':   (HEAVY, 0.15, (1.10, 1.35), (0.44, 0.60), (1.05, 1.10), 'snag',   5)}
ORDER = ('oak', 'slim', 'willow', 'snag'); SEASONS = ('spring', 'autumn')
# **장식 — 붙은 개체도 있고 안 붙은 개체도 있다**(사용자: *"기생체처럼 장식 같은 걸로 해서 나오는 애도 있고 안 나오는 애도"* — 게임에서는 고르기 축).
#   moss 이끼 방석 / beard 입 밑에 늘어진 이끼 수염 / knot1~6 옹이 혹과 구멍 — **자리마다 따로 켠다**(사용자 11-13: *"한번에 4개씩 달리는 게 아니라 하나씩 여러 곳에"*) /
#   vine 다리에서 몸으로 감아 오른 덩굴 / mistle 관에 얹힌 겨우살이
# **버섯은 없다** — 버섯은 죽은 나무에 나는데 엔트는 살아 있다(사용자 11-13: *"엔트는 살아있으니깐 버섯이 나면 안 되는 거 아니냐"* — 선반버섯 장식을 걷었다).
KNOTS = 6
DECOR = ('moss', 'beard') + tuple('knot%d' % (i + 1) for i in range(KNOTS)) + ('vine', 'mistle')
DEFAULT_DECOR = {'oak': ('knot1', 'mistle'), 'slim': ('moss', 'vine', 'knot4'), 'willow': ('moss', 'beard', 'knot2'), 'snag': ('moss', 'knot3', 'knot6')}

# 얼굴 — **종류마다 다르다**(사용자 11-12: *"얼굴 모양이 너무 획일화"*). 눈만 사람 눈이고 나머지는 나무다.
#   head  얼굴 줄기의 반폭 셋(× 가슴 반폭 — 얼굴 밑 · 가운데 · 꼭대기)
#   eye   (간격, 반폭, 반높이, 기울기 — 바깥 끝이 올라가면 +, 윗꺼풀이 덮는 몫, 왼눈 크기 비)
#   nose  (꼴 branch 부러진 가지 · hook 치켜든 잔가지 · knot 둥근 옹이, 길이, 처짐, 굵기)
#   mouth (입선의 점 — 옆 · 위, 굵기)   iris 홍채
# 눈 바탕(사람 눈의 흰자 자리)은 **희지 않다** — 어둡게 탄 주황이고 홍채가 그보다 밝다(사용자).
# 색은 어둡게 눌러 둔다(사용자: *"눈까리가 너무 징그럽잖아 — 색톤을 어둡게"* — 밝은 주황 홍채는 번들거렸다).
SCLERA = (0.11, 0.065, 0.035); IRIS = (0.50, 0.31, 0.11); IRIS_Y = (0.44, 0.37, 0.13); GLINT = (0.62, 0.52, 0.36)
# 계절의 색 한 벌 — 잎뿐 아니라 **이끼 · 덩굴 · 겨우살이도 계절을 탄다**(사용자 11-13: *"가을 나무에는 저렇게 푸릇푸릇한 색이면 안 되잖아"*).
PAL = {'spring': dict(leaf=(LEAF, LEAF2, (0.40, 0.60, 0.20)), strand=(WILLOW, LEAF), moss=(MOSS, (0.28, 0.38, 0.15)), vine=(0.17, 0.34, 0.15), mistle=(0.48, 0.56, 0.22), lichen=(0.56, 0.62, 0.34)),
       'autumn': dict(leaf=((0.80, 0.42, 0.12), (0.60, 0.21, 0.10), (0.84, 0.66, 0.18)), strand=((0.80, 0.62, 0.18), (0.64, 0.40, 0.12)), moss=((0.42, 0.37, 0.16), (0.32, 0.26, 0.12)),
                      vine=(0.48, 0.18, 0.11), mistle=(0.50, 0.46, 0.22), lichen=(0.62, 0.55, 0.28)),
       # 11-14 — 색은 게임에서 개체마다 돈다(팔레트 · `PaletteLook`). 여름(짙은 초록) · 늦가을(붉은 단풍)을 더했다.
       'summer': dict(leaf=((0.16, 0.36, 0.12), (0.24, 0.46, 0.15), (0.12, 0.30, 0.10)), strand=((0.26, 0.44, 0.14), (0.18, 0.36, 0.12)), moss=((0.26, 0.38, 0.14), (0.18, 0.28, 0.10)),
                      vine=(0.12, 0.28, 0.11), mistle=(0.36, 0.48, 0.16), lichen=(0.46, 0.54, 0.28)),
       'late':   dict(leaf=((0.62, 0.14, 0.08), (0.42, 0.08, 0.06), (0.74, 0.30, 0.10)), strand=((0.56, 0.30, 0.12), (0.40, 0.18, 0.08)), moss=((0.36, 0.30, 0.14), (0.26, 0.20, 0.10)),
                      vine=(0.36, 0.10, 0.08), mistle=(0.44, 0.40, 0.20), lichen=(0.56, 0.50, 0.30))}
PALETTES = ('spring', 'summer', 'autumn', 'late')   # 게임이 개체마다 고르는 계절 색(`bake_ent.py`가 목록에 적는다)
LEAF_SHAPES = ('A', 'B', 'C', 'D')                 # 잎 모양 — 고르기 축 `Leaf`
# **뽑기용 기준 벡터** — 색 자리에 칸 번호를 넣고 뽑으면 정점 색이 곧 "칸마다 몫"이 된다(색 섞기가 다 선형이라 맞다). 칸은 재질마다 셋:
#   잎 Leaf(잎 셋) · 이끼 Moss(이끼 둘 · 지의류) · 덩굴 Vine(버들가지 둘 · 덩굴) · 겨우살이 Mistle(하나). 조각이 어느 재질인가는 `Pieces.pal`.
_E = ((1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0))
PAL['code'] = dict(leaf=_E, strand=_E[:2], moss=_E[:2], lichen=_E[2], vine=_E[2], mistle=_E[0])
FACES = {
    'oak':    dict(head=(0.80, 0.72, 0.62), eye=(0.088, 0.058, 0.030, -0.22, 0.10, 0.84), nose=('branch', 0.150, 0.070, 0.030), iris=IRIS,
                   mouth=([(-0.085, -0.010), (-0.040, 0.006), (0.005, -0.004), (0.050, 0.008), (0.090, -0.014)], 0.012)),
    'slim':   dict(head=(0.82, 0.62, 0.50), eye=(0.062, 0.054, 0.022, 0.30, 0.0, 1.0), nose=('hook', 0.085, 0.020, 0.022), iris=IRIS_Y,
                   mouth=([(-0.016, 0.0), (0.016, 0.0)], 0.021)),
    'willow': dict(head=(0.90, 0.86, 0.70), eye=(0.098, 0.056, 0.028, -0.10, 0.45, 1.0), nose=('knot', 0.060, 0.020, 0.048), iris=IRIS_Y,
                   mouth=([(-0.095, -0.022), (-0.045, 0.004), (0.0, 0.010), (0.045, 0.004), (0.095, -0.022)], 0.013)),
    'snag':   dict(head=(0.78, 0.74, 0.70), eye=(0.078, 0.062, 0.034, 0.08, 0.0, 1.14), nose=('branch', 0.095, 0.030, 0.034), iris=IRIS,
                   mouth=([(-0.060, 0.016), (-0.020, -0.006), (0.025, -0.008), (0.065, 0.014)], 0.012))}

def masked(d, p, m, f):
    """m인 점에서만 f(d의 그 점들, p의 그 점들)로 고친다 — 얼굴 · 다리처럼 한곳에만 걸리는 손질을 몸 전체에서 재지 않는다."""
    if m.any(): d = d.copy(); d[m] = f(d[m], p[m])
    return d

class Pieces(list):
    """조각 목록 — 렌더러는 (거리, 색)만 읽고, 뽑기는 덧붙인 이름표를 읽는다: 장식 갈래(`tag` — 'core'면 늘 있는 몸) · 붙일 뼈(`bone` — None이면 정점마다 가까운 뼈)
    · 고를 수 있는 뼈(`allow` — None이면 손을 뺀 전부). **손가락은 다리 옆까지 늘어진다** — 거리만 보면 손가락이 다리 뼈를, 다리가 손 뼈를 받아 걸을 때
    둘이 붙어 늘어났다(11-14 사용자: *"다리하고 손이 붙어서 늘어나"*). 그래서 줄기(다리 포함)와 팔은 제 뼈만 고른다."""
    def __init__(s): super().__init__(); s.tag, s.bone, s.allow, s.pal, s.meta = 'core', None, None, None, []
    def append(s, x): super().append(x); s.meta.append((s.tag, s.bone, s.allow, s.pal))
    def __iadd__(s, xs):
        for x in xs: s.append(x)
        return s

def build(kind, season='spring', decor=None, bark='ash', split=False, leaf='A'):
    shape, lean, arm_k, fing, leg_k, crown, seed = KINDS[kind]; FC = FACES[kind]; PL = PAL[season]
    decor = DEFAULT_DECOR[kind] if decor is None else decor
    rng = np.random.default_rng(seed)
    D = bd.dims(shape); L, TOP, W, TH = D['lift'], D['top'], D['half'][3], D['torsoH']
    YT = TOP + 0.36                                          # 줄기 꼭대기 — 여기서 가지로 갈라진다
    h0, h1, h2 = FC['head']
    prof = [(L + 0.02, W * 0.84), (L + 0.22, W * 0.94), (L + 0.50 * TH, W * 0.80), (TOP - 0.12, W * 0.98), (TOP + 0.14, W * h0), (TOP + 0.28, W * h1), (YT, W * h2)]
    if crown != 'snag': prof.append((YT + (0.06 if crown == 'up' else 0.16), W * h2 * 0.50))   # 꼭대기는 판판하게 끊지 않고 가지 사이로 오므라든다
    T = Trunk(prof, D['depth'], lean=lean, y_lean=L + 0.25)
    BARK, HEART = ((1.0, 1.0, 1.0), (1.5, 1.4, 1.2)) if split else BARKS[bark]   # split — 껍질은 몫만(색은 게임이 고른다)
    bc = bark_plain(BARK, split); tc = T.color(BARK, PL['moss'][0], split)
    P = Pieces(); UP = V(0, 1, 0)
    SK = []   # 뼈대 선 — (뼈 이름, a, b, 반지름 a, 반지름 b). 뽑기가 정점마다 가장 가까운 뼈를 고른다(`bake_ent.py`)
    def on_trunk(y, a, off=0.0):   # 줄기 겉면의 점과 바깥 방향 — 둘레 각도 a(앞 0 · 옆 ±π/2)
        c = T.axis(y); r = T.side(y) + off
        return c + V(np.sin(a) * r, 0, np.cos(a) * r * T.depth), nrm([np.sin(a) * T.depth, 0, np.cos(a)])
    near = accel

    # 눈 — 사람 눈(바탕 · 홍채 · 눈동자 · 껍질 눈꺼풀). 줄기 겉면에 앉혀 겉면의 방향으로 판다(앞만 보고 파면 옆에서 눈알이 튀어나온다).
    ey = TOP + 0.17; gap, EW0, EH0, tilt, cover, ratio = FC['eye']
    eyes = []
    for s_ in (-1, 1):
        k = ratio if s_ < 0 else 2.0 - ratio; y = ey + 0.004 * s_
        c = V(T.axis(y)[0] + s_ * gap, y, T.front(y, s_ * gap)); ax = T.axis(y)
        n = nrm([(c[0] - ax[0]) * T.depth, 0, (c[2] - ax[2]) / T.depth]); t = np.cross(UP, n)
        eyes.append((c, n, t, s_, EW0 * k, EH0 * k))
    FCC = V(T.axis(ey - 0.05)[0], ey - 0.05, T.front(ey - 0.05))   # 얼굴 한가운데 — 얼굴 손질은 이 둘레에서만 잰다
    def eye_q(p, c, n, t, s_):   # 눈의 제 좌표 — (옆, 위(기울기를 편 것), 겉면 밖)
        q = p - c; du = (q * t).sum(-1)
        return du, q[..., 1] - tilt * s_ * du, (q * n).sum(-1)
    def almond(du, dv, dw, EW, EH):   # 눈구멍 — 위아래 두 호가 만나는 아몬드(윗꺼풀이 cover만큼 덮는다)
        e = 1 - np.clip(du / EW, -1, 1) ** 2
        return np.maximum(np.maximum(np.maximum(dv - EH * (1 - cover) * e, -EH * e - dv), np.abs(du) - EW), np.abs(dw + 0.012) - 0.034)
    def lid_pts(c, n, t, s_, EW, EH, sg):
        out = []
        for a in np.linspace(-EW * 1.05, EW * 1.05, 6):
            e = 1 - (a / (EW * 1.05)) ** 2; v = (EH * (1 - cover) * e + 0.006) if sg > 0 else -(EH * e + 0.003)
            out.append(c + t * a + UP * (v + tilt * s_ * a) + n * 0.003)
        return out
    lids = [(lid_pts(c, n, t, s_, EW, EH, sg), rr) for c, n, t, s_, EW, EH in eyes for sg, rr in ((1, 0.010 + 0.006 * cover), (-1, 0.006))]   # 위는 두껍게 · 아래는 가늘게
    def eye_piece(shape):   # 눈구멍 안에서만 보이는 조각
        def f(p):
            d = far(p)
            for c, n, t, s_, EW, EH in eyes:
                du, dv, dw = eye_q(p, c, n, t, s_)
                d = np.minimum(d, np.maximum(shape(du, dv, dw, EW, EH), almond(du, dv, dw, EW, EH) - 0.003))
            return d
        return near(f, FCC, 0.26)
    def ball(du, dv, dw, EW, EH): R = EW * 1.05; return np.sqrt(du * du + dv * dv + (dw + R + 0.004) ** 2) - R
    def iris(du, dv, dw, EW, EH):  r = min(EH * 0.92, EW * 0.48); return (np.sqrt(du * du + (dv + cover * EH * 0.35) ** 2) - r) * 0.9 + 9 * (np.abs(dw + 0.006) > 0.008)
    def pupil(du, dv, dw, EW, EH): r = min(EH * 0.92, EW * 0.48) * 0.42; return (np.sqrt(du * du + (dv + cover * EH * 0.35) ** 2) - r) * 0.9 + 9 * (np.abs(dw + 0.003) > 0.007)
    def glint(du, dv, dw, EW, EH): r = min(EH * 0.92, EW * 0.48); return (np.sqrt((du + r * 0.42) ** 2 + (dv + cover * EH * 0.35 - r * 0.40) ** 2) - r * 0.20) * 0.9 + 9 * (np.abs(dw + 0.0010) > 0.0090)   # 눈동자 판 앞 납작한 판(11-18 — 공이 솟았던 것 · 엔트는 4mm 칸으로 떠서 눈동자보다 4mm 앞)

    # 코 · 입 — 줄기 앞면에서 자리를 잰다.
    kind_n, n_ln, n_drop, n_r = FC['nose']; yn = ey - 0.06 - (0.02 if kind_n == 'knot' else 0.0)
    nb = V(T.axis(yn)[0] + 0.005, yn, T.front(yn, 0.0) - 0.03); nt = nb + V(-0.25 * n_ln, -n_drop, n_ln)
    def nose(p):
        if kind_n == 'hook':   # 치켜든 잔가지
            m = nb + V(0.0, -0.012, n_ln * 0.62); return np.minimum(seg(p, nb, m, n_r * 1.3, n_r), seg(p, m, m + V(0.01, 0.045, n_ln * 0.38), n_r, n_r * 0.5))
        if kind_n == 'knot':   # 둥근 옹이
            return smin(sph(p, nb + V(0, -n_drop, n_ln * 0.5 + 0.03), n_r), sph(p, nb + V(0.012, -n_drop + 0.01, n_ln * 0.5 + 0.03 + n_r * 0.75), n_r * 0.45), 0.02)
        return seg(p, nb, nt, n_r, n_r * 0.70)
    mp, m_r = FC['mouth']; my = ey - 0.150 - (0.02 if kind_n == 'knot' else 0.0)
    mpts = [V(T.axis(my)[0] + x, my + dy, T.front(my + dy, x) + 0.004) for x, dy in mp]

    # 다리 — 줄기가 밑에서 둘로 갈라진다(굵기가 다르다). 발은 사방으로 뻗은 뿌리.
    lx = D['legX']; legs = []
    for s_, k in ((-1, leg_k[0]), (1, leg_k[1])):
        r = D['legR'] * 0.92 * k
        Hp = V(s_ * lx * 0.75, D['hipY'] + 0.12, 0.0); Kn = V(s_ * lx * (1.50 + 0.20 * rng.uniform(-1, 1)), (D['hipY'] + 0.15) * 0.5 + 0.05 * rng.uniform(-1, 1), 0.07 * rng.uniform(-0.4, 1))
        An = V(s_ * lx * 2.00, 0.15, 0.03 * rng.uniform(-1, 1))
        toes = [(s_ * 0.9, 0.45, 0.24), (s_ * 0.2, 1.0, 0.34), (-s_ * 0.6, 0.75, 0.20), (s_ * 0.5, -0.9, 0.22), (-s_ * 0.3, -0.8, 0.14), (s_ * 1.0, -0.15, 0.16)]
        toes = [(a, b, ln * k * rng.uniform(0.75, 1.25)) for a, b, ln in toes]
        legs.append((Hp, Kn, An, r, toes))
    def legs_f(p):
        d = far(p)
        for Hp, Kn, An, r, toes in legs:
            e = smin(limb(p, Hp, Kn, r * 1.20, r * 0.92), sph(p, Kn, r * 0.98), 0.03)
            e = smin(e, limb(p, Kn, An, r * 0.88, r * 1.05), 0.03)
            d = np.minimum(d, smin(e, root_foot(p, An, r * 1.05, toes), 0.04))
        return d

    # 관의 굵은 가지 — **줄기 꼭대기가 그대로 갈라진 것이다**(사용자 11-13: *"머리랑 머리 위의 나무가 너무 안 이어져"*). 밑동은 줄기 속 깊이에서 굵게 나와
    # 줄기와 한 살로 녹고(`mains`), 그 위의 잔가지만 따로 얹는다. (방향, 길이, 밑동 굵기 × 가슴 반폭, 갈라지는 횟수, 밑동이 줄기 축에서 비켜난 자리)
    if crown == 'up':       spec = [(V(np.sin(a) * 0.95, 1.0, np.cos(a) * 0.80), 0.22, 0.34, 3, 0.05) for a in (1.15, 2.3, 3.95, 5.15)]; sp, up = 0.75, 0.28   # 옆으로 벌어져 솟는다(가운데에 몰리면 빗자루 같다 — 사용자 11-13)
    elif crown == 'willow': spec = [(V(np.sin(a) * 1.0, 0.75, np.cos(a) * 0.9 - 0.2), 0.34, 0.36, 3, 0.04) for a in (0.5, 1.7, 2.9, 4.1, 5.4)]; sp, up = 0.70, 0.05
    elif crown == 'snag':   spec = [(V(1.0, 0.75, 0.05), 0.18, 0.44, 3, 0.15), (V(-0.55, 1.0, -0.35), 0.14, 0.36, 3, 0.11),
                                    (V(-0.95, 0.55, 0.35), 0.07, 0.26, 2, 0.11, False), (V(0.25, 0.9, -0.85), 0.05, 0.24, 2, 0.09, False)]; sp, up = 0.75, 0.30   # 산 가지 둘 + 죽은 가지 둘(끝의 False — 잎이 없다)
    else:                   spec = [(V(np.sin(a) * 0.80, 0.90, np.cos(a) * 0.70 - 0.1), 0.36, 0.36, 3, 0.06) for a in (1.05, 2.1, 3.14, 4.2, 5.25)]; sp, up = 0.70, 0.30
    mains, segs, tips = [], [], []
    for dr, ln, rk, depth, off, *dead in spec:
        d0 = nrm(dr); start = T.axis(YT - (0.07 if crown == 'snag' else 0.10))   # 눈(꼭대기 아래 19cm)보다 위에서 나온다 — 더 깊으면 밑동이 눈을 덮는다 + V(d0[0], 0, d0[2]) * off
        sg, tp = grow(start, dr, ln * rng.uniform(0.85, 1.2) + 0.20, W * rk, depth, rng, spread=sp, up=up)
        mains.append(sg[0]); segs += sg[1:]; tips += [] if dead else tp
    def mains_f(p):
        d = far(p)
        for a, b, ra, rb in mains: d = np.minimum(d, seg(p, a, b, ra, rb))
        return d
    brk = lambda p: p[..., 1] - (YT + 0.02 + 0.45 * (p[..., 0] - T.sway(YT)) + 0.05 * np.abs(np.sin(p[..., 0] * 28.0)) + 0.03 * np.sin(p[..., 2] * 22.0))   # 부러진 면(오른쪽이 높다)
    hol_c, hol_r = T.axis(YT + 0.02), V(W * h2 * 0.66, 0.17, W * h2 * 0.66 * T.depth)

    def face_fix(d, p):
        for c, n, t, s_, EW, EH in eyes:
            du, dv, dw = eye_q(p, c, n, t, s_); d = smax(d, -almond(du, dv, dw, EW, EH), 0.005)
        for pts, rr in lids:
            for a_, b_ in zip(pts[:-1], pts[1:]): d = smin(d, capsule(p, a_, b_, rr), 0.006)
        cut = far(p)
        for a, b in zip(mpts[:-1], mpts[1:]): cut = np.minimum(cut, capsule(p, a, b, m_r))
        return smin(smax(d, -cut, 0.006), nose(p), 0.02)
    def trunk_f(p):
        d = T(p)
        if crown == 'snag': d = smax(smax(d, brk(p), 0.012), -ell(p, hol_c, hol_r), 0.02)      # 비스듬히 부러진 꼭대기 · 빈 속
        d = masked(d, p, length(p - FCC) < 0.36, face_fix)
        d = masked(d, p, p[..., 1] > YT - 0.45, lambda dd, q: smin(dd, mains_f(q), 0.075))   # 줄기가 가지로 갈라져 올라간다
        return masked(d, p, p[..., 1] < L + 0.60, lambda dd, q: smin(dd, legs_f(q), 0.055))   # 줄기가 다리로 갈라져 내려간다
    trunk = near(trunk_f, V(0, (YT + 0.5) * 0.5, 0), (YT + 0.5) * 0.5 + 0.75)
    def inner_f(p):   # 구멍 속 — 검다
        d = far(p)
        for a, b in zip(mpts[:-1], mpts[1:]): d = np.minimum(d, capsule(p, a - V(0, 0, 0.03), b - V(0, 0, 0.03), m_r + 0.004))
        return d
    def trunk_col(p):   # 부러진 가지 코는 끝의 부러진 면이 밝다
        if kind_n != 'branch': return tc(p)
        if split: c4 = tc(p); c4[..., :3] = mixc(c4[..., :3], HEART, sstep(n_r * 0.75, n_r * 0.45, length(p - nt)) * (((p - nt) * nrm(nt - nb)).sum(-1) > -0.004)); return c4
        return mixc(tc(p), HEART, sstep(n_r * 0.75, n_r * 0.45, length(p - nt)) * (((p - nt) * nrm(nt - nb)).sum(-1) > -0.004))
    TRUNK_B = ('Hips', 'Spine', 'LegL', 'LowerLegL', 'FootL', 'LegR', 'LowerLegR', 'FootR')   # 줄기 · 다리에 붙은 것이 고를 뼈
    P.allow = TRUNK_B
    P += [(trunk, trunk_col)]
    P.allow = None
    P.bone = 'Spine'
    P += [(near(inner_f, FCC, 0.26), DARK)]
    P += [(eye_piece(ball), SCLERA), (eye_piece(iris), FC['iris']), (eye_piece(pupil), DARK), (eye_piece(glint), GLINT)]
    if crown == 'snag': P.append((near(lambda p: np.maximum(np.maximum(ell(p, hol_c - V(0, 0.03, 0), hol_r * 0.97), brk(p) + 0.012), p[..., 1] - (YT - 0.012)), hol_c, 0.4), DARK))

    P.bone = None
    # 뼈대 선 — 줄기(엉덩이 · 등), 다리(넓적 · 정강 · 발)
    yh = D['hipY'] + 0.12; ys = np.linspace(L - 0.05, yh, 4).tolist() + np.linspace(yh, YT, 7)[1:].tolist()
    for y0, y1 in zip(ys[:-1], ys[1:]): SK.append(('Hips' if y1 <= yh + 1e-6 else 'Spine', T.axis(y0), T.axis(y1), T.side(y0), T.side(y1)))
    for (Hp, Kn, An, r, toes), sd in zip(legs, 'LR'):
        SK += [('Leg' + sd, Hp, Kn, r * 1.20, r * 0.92), ('LowerLeg' + sd, Kn, An, r * 0.88, r * 1.05)]
        for sx, sz, ln in toes: SK.append(('Foot' + sd, An, An + nrm([sx, 0, sz]) * (r * 0.9 + ln) + V(0, 0.022 - An[1], 0), r * 0.72, 0.016))
    # 팔 — 줄기 옆에서 뻗은 가지. 좌우 높이 · 굵기 · 길이가 다르다. 손은 늘어진 가지 손가락.
    extra, xtips, arms, arm_fs = [], [], [], []   # 곁가지 · 그 끝 · 팔 자리 · 팔의 거리 함수(장식이 읽는다)
    ex_side, xt_side = {'L': [], 'R': []}, {'L': [], 'R': []}   # 팔마다 — 곁가지 조각은 제 팔 뼈만 고른다(뽑기)
    for i, (s_, k) in enumerate(((-1, arm_k[0]), (1, arm_k[1]))):
        dy = 0.05 * rng.uniform(-1, 1); sy = D['shY'] + dy; c = T.axis(sy); ax = T.side(sy) * 0.95
        C = c + V(s_ * ax * 0.30, -0.16, 0.0); S = c + V(s_ * (ax + 0.09), 0.03, 0.0)
        E = c + V(s_ * (ax + 0.30 + 0.06 * rng.uniform(-1, 1)), (D['handY'] - sy) * 0.42, -0.05 + 0.07 * rng.uniform(-1, 1))
        Wr = c + V(s_ * (ax + 0.26 + 0.05 * rng.uniform(-1, 1)), D['handY'] - sy - 0.04 * k, 0.10)
        ra = 0.070 * k
        fl = fing[i]; n = 5 if fl > 0.5 else 4; fs = []
        for j in range(n):
            t = (j - (n - 1) * 0.5) / (n - 1); ln = fl * rng.uniform(0.70, 1.10)
            a = Wr + V(s_ * 0.01, -ra * 0.8, t * 0.11 * k); m = a + V(s_ * 0.03 * rng.uniform(-0.5, 1.5), -ln * 0.50, t * 0.10 + 0.03); b = m + V(-s_ * 0.05 * rng.uniform(0.2, 1.4), -ln * 0.50, 0.03 * rng.uniform(-1, 1.5))
            b[1] = max(b[1], 0.06); m[1] = max(m[1], 0.10)
            fs.append((a, m, b, ra * 0.50, ra * 0.34))
        th0 = Wr + V(-s_ * ra * 0.8, -ra * 0.3, ra * 0.6); fs.append((th0, th0 + V(-s_ * 0.02, -fl * 0.22, 0.05), th0 + V(-s_ * 0.05, -fl * 0.40, 0.07), ra * 0.46, ra * 0.30))
        def arm(p, C=C, S=S, E=E, Wr=Wr, ra=ra, fs=fs):
            d = smin(seg(p, C, S, ra * 1.75, ra * 1.30), limb(p, S, E, ra * 1.30, ra * 0.92), 0.04)
            d = smin(smin(d, sph(p, E, ra * 1.05), 0.03), limb(p, E, Wr, ra * 0.90, ra * 1.08), 0.03)
            d = smin(d, sph(p, Wr + V(0, -ra * 0.5, 0.01), ra * 1.25), 0.03)
            for a, m, b, r1, r2 in fs: d = smin(d, np.minimum(seg(p, a, m, r1, r2), seg(p, m, b, r2, 0.007)), 0.02)
            return d
        apts = np.array([C, S, E, Wr] + [f[2] for f in fs], np.float32); ac = (apts.min(0) + apts.max(0)) * 0.5
        sd = 'L' if s_ < 0 else 'R'
        P.allow = ('Spine', 'Arm' + sd, 'LowerArm' + sd, 'Hand' + sd)
        P.append((near(arm, ac, float(length(apts - ac).max()) + ra * 2.0), bc))
        P.allow = None
        arms.append((S, E, Wr, ra, s_)); arm_fs.append(arm)
        SK += [('Arm' + sd, S, E, ra * 1.30, ra * 0.92), ('LowerArm' + sd, E, Wr, ra * 0.90, ra * 1.08)] + [('Hand' + sd, a, b, r1, 0.007) for a, m, b, r1, r2 in fs]
        for sg, tp in (grow(E, V(s_ * 0.9, 0.5, -0.4), 0.15 * k, 0.020 * k, 2, rng), grow(S + V(0, ra, 0), V(s_ * 0.5, 1.0, -0.2), 0.20, 0.026, 2, rng)):
            extra += sg; xtips += tp; ex_side[sd] += sg; xt_side[sd] += tp; SK += [('Arm' + sd, a, b, ra_, rb_) for a, b, ra_, rb_ in sg]
    hip = legs[0][1]; hs = grow(hip, V(-1.0, 0.4, 0.3), 0.13, 0.018, 1, rng)[0]; extra += hs; SK += [('LegL', a, b, ra_, rb_) for a, b, ra_, rb_ in hs]
    # 곁가지는 난 자리마다 따로 조각 — 한 조각이면 왼 무릎 잔가지(바깥 위로 뻗어 늘어진 왼손 곁까지 간다)의 끝이 팔 뼈를 받아
    # 걸을 때 팔과 무릎 사이로 늘어났다(11-14 사용자 — `Spine.Ent.BarkFine`).
    for sd in 'LR':
        P.allow = ('Spine', 'Arm' + sd, 'LowerArm' + sd); P.append((twigs(ex_side[sd]), bc))
    P.allow = TRUNK_B; P.append((twigs(hs), bc)); P.allow = None
    P.bone = 'Spine'   # 관은 등뼈에 통째로 — 늘어진 버들가지가 팔 곁을 지나도 팔을 따라 움직이지 않게
    if segs: P.append((twigs(segs), bc))

    # 잎 — 가지 끝에만 달린다. 가지 끝마다 잎 뭉치 하나에 낱잎이 삐져나온다(낱잎만으로는 *"수가 너무 적어 보인다"* — 사용자 11-13). 색은 계절 한 벌.
    # **잎 모양은 넷 — 고르기 축**(11-14 사용자: *"잎 모양이 봄 가을 그리고 추가 두 개"* · 색은 따로 돈다 — 팔레트). `leaf`로 하나만 짓거나(미리보기) 전부(뽑기 — None).
    #   A 지금 잎(둥근 뭉치 + 낱잎 셋 — 난수 순서를 그대로 둬 장식까지 예전 그대로) / B 무성한 잔잎(작은 뭉치 여럿이 흩어진다) /
    #   C 늘어진 잎(뭉치가 처지고 긴 잎이 아래로) / D 뾰족 잎 다발(뭉치 없이 가는 잎이 부채처럼 뻗는다). 버드나무는 버들가지의 길이 · 수로 가른다.
    #   A 말고는 제 난수(`seed`에서 낸다)를 써서 A와 뒤의 장식을 안 흔든다.
    def season_col(p):   # 잎 뭉치의 색 — 계절 색 셋이 얼룩으로
        c0, c1, c2 = PL['leaf']; n = noise(p, 3.3)
        return mixc(mixc(c0, c1, sstep(-0.22, -0.10, n)), c2, sstep(0.14, 0.26, n))
    def tip_leaves(tp, per, size, R=None, droop=0.35, spread=0.7, narrow=0.21, out_k=0.5):   # 가지 끝마다 per장
        R = rng if R is None else R; out = []
        for b, d in tp:
            for _ in range(per):
                dr = nrm(d * out_k + R.normal(size=3) * spread + V(0, -droop, 0)); out.append((b - d * R.uniform(0, 0.05) + dr * 0.03, dr, np.cross(dr, R.normal(size=3)), size * R.uniform(0.75, 1.2), size * narrow))
        return out
    def by_color(Lv, cols): P.append(leaves(Lv, cols))
    def clumps(tp, r0, r1, R=None, per=1, scatter=0.0, drop=0.0):
        R = rng if R is None else R; bl = []
        for b, d in tp:
            for _ in range(per):
                off = R.normal(size=3) * scatter if scatter else 0.0
                bl.append((b + d * 0.04 + off + V(0, -drop, 0), R.uniform(r0, r1)))
        pts = np.array([c for c, _ in bl], np.float32); cc = (pts.min(0) + pts.max(0)) * 0.5
        P.append((near(lambda q: leafy(q, bl, 0.022, 30.0), cc, float(length(pts - cc).max()) + r1 + 0.08), season_col))
    def strands_of(tp, R, per, l0, l1, k=1.0):   # 늘어진 버들가지
        st = []
        for b, d in tp:
            for _ in range(per):
                a = b + V(*(R.uniform(-0.04, 0.04, 3))); ln = R.uniform(l0, l1); sw = V(R.uniform(-0.05, 0.05), 0, R.uniform(-0.05, 0.05))
                m1 = a + V(d[0] * 0.10, -ln * 0.30, d[2] * 0.10); m2 = m1 + sw + V(0, -ln * 0.35, 0); e = m2 + sw + V(0, -ln * 0.35, 0)
                st += [(a, m1, 0.016 * k, 0.020 * k), (m1, m2, 0.020 * k, 0.018 * k), (m2, e, 0.018 * k, 0.006 * k)]
        s0, s1 = PL['strand']
        P.pal = 'vine'; P.append((twigs(st), lambda p: mixc(s0, s1, sstep(-0.3, 0.4, noise(p, 3.0))))); P.pal = 'leaf'
    def crown_leaves(v, R):   # 관의 가지 끝
        if crown == 'willow':
            if v == 'A': strands_of(tips, R, 3, 0.45, 0.95)
            elif v == 'B': strands_of(tips, R, 4, 0.22, 0.50); by_color(tip_leaves(tips, 3, 0.080, R), PL['leaf'])
            elif v == 'C': strands_of(tips, R, 2, 0.85, 1.35, 0.8)
            else: strands_of(tips, R, 3, 0.40, 0.80); clumps(tips, 0.050, 0.075, R)
        elif v == 'A': clumps(tips, 0.085, 0.135, R); by_color(tip_leaves(tips, 3, 0.105, R), PL['leaf'])
        elif v == 'B': clumps(tips, 0.050, 0.080, R, per=3, scatter=0.060); by_color(tip_leaves(tips, 6, 0.075, R), PL['leaf'])
        elif v == 'C': clumps(tips, 0.055, 0.085, R, drop=0.05); by_color(tip_leaves(tips, 6, 0.150, R, droop=1.2, spread=0.4), PL['leaf'])
        else: by_color(tip_leaves(tips, 12, 0.130, R, droop=-0.1, spread=0.9, narrow=0.10, out_k=1.0), PL['leaf'])
    def side_leaves(v, R):   # 팔 곁가지 끝 — 팔마다 따로 조각(제 팔 뼈만). A는 예전 순서(뭉치 왼 · 오 → 낱잎 왼 · 오)
        arm_b = lambda sd: ('Spine', 'Arm' + sd, 'LowerArm' + sd)
        if v in ('A', 'B', 'C'):
            r0, r1, kw = {'A': (0.045, 0.070, {}), 'B': (0.030, 0.050, dict(per=2, scatter=0.035)), 'C': (0.035, 0.050, dict(drop=0.03))}[v]
            for sd in 'LR': P.allow = arm_b(sd); clumps(xt_side[sd], r0, r1, R, **kw)
        n, size, kw = {'A': (3, 0.085, {}), 'B': (4, 0.060, {}), 'C': (4, 0.110, dict(droop=1.2, spread=0.4)), 'D': (7, 0.100, dict(droop=-0.1, spread=0.9, narrow=0.10, out_k=1.0))}[v]
        for sd in 'LR': P.allow = arm_b(sd); by_color(tip_leaves(xt_side[sd], n, size, R, **kw), PL['leaf'])
        P.allow = None
    shapes = LEAF_SHAPES if leaf is None else (leaf,)
    rv = {v: rng if v == 'A' else np.random.default_rng(seed * 97 + ord(v)) for v in LEAF_SHAPES}
    P.pal = 'leaf'
    for v in shapes: P.tag = 'leaf' + v; crown_leaves(v, rv[v])
    P.bone = None
    for v in shapes: P.tag = 'leaf' + v; side_leaves(v, rv[v])
    P.tag, P.pal = 'core', None

    # ── 장식 — 붙은 개체도 있고 안 붙은 개체도 있다 ──
    def cushions(sites, col, bump=0.010, f=55.0):   # 겉면에 반쯤 묻힌 방석 덩이 — (겉면의 점, 바깥, 반지름)
        bl = [(pt - o * r * 0.45, r) for pt, o, r in sites]
        pts = np.array([c for c, _ in bl], np.float32); cc = (pts.min(0) + pts.max(0)) * 0.5
        P.append((near(lambda q: leafy(q, bl, bump, f), cc, float(length(pts - cc).max()) + 0.15), col))
    P.tag, P.pal = 'moss', 'moss'
    if 'moss' in decor:      # 이끼 방석 — 윗면과 그늘진 쪽에 조금씩
        k = 1.5 if kind == 'willow' else 1.0; sites = []
        for S, E, Wr, ra, s_ in arms:
            sites.append((S + UP * ra * 1.2, UP, 0.070 * k))
            for u in rng.uniform(0.1, 0.9, int(2 * k)): sites.append((S + (E - S) * u + nrm([s_ * 0.4, 1, 0]) * ra, nrm([s_ * 0.4, 1, 0]), rng.uniform(0.035, 0.055)))
            sites.append((E + UP * ra, UP, 0.045 * k))
        for a in rng.uniform(1.2, 2 * np.pi - 1.2, int(4 * k)): sites.append((*on_trunk(YT - 0.06, a), rng.uniform(0.04, 0.06)))
        for _ in range(int(7 * k)): sites.append((*on_trunk(rng.uniform(L + 0.25, TOP + 0.05), np.pi + rng.uniform(-1.3, 1.3)), rng.uniform(0.04, 0.075)))
        for Hp, Kn, An, r, toes in legs: sites += [(An + UP * r * 1.0, UP, 0.055), (Kn + nrm([0, 0.5, 1]) * r, nrm([0, 0.5, 1]), 0.045)]
        cushions(sites, lambda p, c0=PL['moss'][0], c1=PL['moss'][1]: mixc(c0, c1, sstep(-0.3, 0.4, noise(p, 6.0))))
    P.tag, P.bone, P.pal = 'beard', 'Spine', 'moss'
    if 'beard' in decor:     # 이끼 수염 — 입 밑에 늘어진 실이끼(잎이 아니다)
        st = []
        for a in np.linspace(-0.55, 0.55, 10):
            pt, o = on_trunk(my - 0.035 - 0.03 * rng.uniform(0, 1), a + rng.uniform(-0.04, 0.04)); ln = rng.uniform(0.12, 0.26) * (1.1 - 0.6 * abs(a))
            m1 = pt + o * 0.02 + V(rng.uniform(-0.01, 0.01), -ln * 0.4, 0); m2 = m1 + V(rng.uniform(-0.015, 0.015), -ln * 0.35, 0.005)
            st += [(pt, m1, 0.020, 0.017), (m1, m2, 0.017, 0.012), (m2, m2 + V(rng.uniform(-0.01, 0.01), -ln * 0.25, 0), 0.012, 0.004)]
        def beard_col(p, c0=PL['moss'][0], c1=PL['moss'][1], tip=PL['lichen'], y0=my - 0.03):   # 뿌리는 몸의 이끼 색 그대로 · 끝으로 갈수록 바랜다(한 색이면 붙인 것 같다 — 사용자 11-13)
            base = mixc(c0, c1, sstep(-0.3, 0.4, noise(p, 6.0)))
            return mixc(base, tip, sstep(y0 - 0.05, y0 - 0.24, p[..., 1] + 0.03 * noise(p, 9.0)))
        P.append((twigs(st), beard_col))
    # 옹이 — 껍질이 부푼 혹과 그 가운데의 검은 구멍. 자리 여섯이 저마다 따로 켜진다(가슴 · 왼 옆구리 · 등 어깨 · 오른 허벅지 · 왼팔 · 오른 아랫배).
    Hp, Kn, An, r, _ = legs[1]; S, E, Wr, ra, s_ = arms[0]
    kn_all = [(*on_trunk(L + 0.62 * TH, 0.25), 0.085), (*on_trunk(L + 0.30 * TH, -0.85), 0.060), (*on_trunk(TOP - 0.05, 2.4), 0.070),
              (Hp + (Kn - Hp) * 0.55 + nrm([0.4, 0, 1]) * r, nrm([0.4, 0, 1]), 0.055), (S + (E - S) * 0.5 + nrm([s_ * 0.3, 0, 1]) * ra, nrm([s_ * 0.3, 0, 1]), 0.045),
              (*on_trunk(L + 0.42 * TH, 1.05), 0.058)]
    P.bone = None
    def knot_q(p, pt, o):
        q = p - pt; h = (q * o).sum(-1); return h, length(q - o * h[..., None])
    for i, (pt, o, R) in enumerate(kn_all):   # 자리마다 따로 조각 — 게임에서 자리마다 따로 켜진다(`knot1`~)
        if 'knot%d' % (i + 1) not in decor: continue
        P.tag, P.pal = 'knot%d' % (i + 1), None; P.allow = ('Spine', 'ArmL', 'LowerArmL') if i == 4 else TRUNK_B   # 다섯째는 왼팔 위 · 나머지는 줄기 · 다리
        def burl(p, pt=pt, o=o, R=R):
            h, rad = knot_q(p, pt, o); return smax(np.sqrt(rad ** 2 + (h / 0.55) ** 2) - R * 1.25, -(np.sqrt(rad ** 2 + (h - R * 0.70) ** 2) - R * 0.62), 0.012)
        def pit(p, pt=pt, o=o, R=R):
            h, rad = knot_q(p, pt, o); return np.maximum(np.sqrt(rad ** 2 + (h - R * 0.25) ** 2) - R * 0.55, np.sqrt(rad ** 2 + (h / 0.55) ** 2) - R * 1.20)
        P += [(near(burl, pt, R * 1.6 + 0.05), bc), (near(pit, pt, R * 1.6 + 0.05), DARK)]
    P.tag, P.allow, P.pal = 'vine', TRUNK_B, 'vine'   # 덩굴은 다리 · 줄기를 감는다 — 어깨 곁 마디가 팔 뼈를 받으면 팔이 흔들릴 때 다리까지 늘어났다(11-14 OakSpring)
    if 'vine' in decor:      # 덩굴 — 오른 다리를 감아 올라 몸통을 돌아 어깨까지. 몸 겉면을 찾아 그 위에 얹는다(어림한 둘레에 띄우면 굵은 허벅지 · 어깨 밑동 속에 묻힌다)
        def body(p):   # 덩굴이 타는 몸 — 줄기는 골을 메운 겉(골 속으로 들어가면 등성이에 가린다)
            d = np.minimum(T.raw(p), legs_f(p))
            for f in arm_fs: d = np.minimum(d, f(p))
            return d
        ts = np.linspace(0.0, 0.7, 176, dtype=np.float32)
        def on_body(c, o):   # 속(c)에서 o 쪽으로 나가 처음 만나는 겉면 위의 점
            v = body(c[None, :] + o[None, :] * ts[:, None]); i = int(np.argmax(v > 0))
            if i == 0: return None
            t = ts[i - 1] + (ts[i] - ts[i - 1]) * float(v[i - 1] / (v[i - 1] - v[i]))
            return c + o * (t + 0.006)
        Hp, Kn, An, r, _ = legs[1]; pts = []
        u_top = np.clip((L - 0.06 - Kn[1]) / (Hp[1] - Kn[1]), 0.05, 0.9)                 # 가랑이 아래까지만 다리를 감는다(그 위는 두 다리와 줄기가 한 살이다)
        for u in np.linspace(0, 1, 44):
            w = u * (1 + u_top); c = An + (Kn - An) * w if w < 1 else Kn + (Hp - Kn) * (w - 1)
            a = u * 4.5 * np.pi; pts.append(on_body(c, V(np.sin(a), 0, np.cos(a))))      # 바깥 옆(+x)에서 끝난다
        c0 = Kn + (Hp - Kn) * u_top; y0 = L + 0.10
        for k in (0.33, 0.66): cc = c0 + (T.axis(y0) - c0) * k; pts.append(on_body(cc, V(1, 0, 0)))   # 다리 → 줄기로 넘어가는 자리
        for u in np.linspace(0, 1, 90):
            y = y0 + u * (D['shY'] - 0.10 - y0); a = 0.5 * np.pi + u * 2.4 * np.pi
            pts.append(on_body(T.axis(y), nrm([np.sin(a) * T.depth, 0, np.cos(a)])))
        pts = [q for q in pts if q is not None]
        P.append((twigs([(a, b, 0.008, 0.008) for a, b in zip(pts[:-1], pts[1:])]), PL['vine']))
        iv = []
        for a, b in list(zip(pts[:-1], pts[1:]))[1::2]:
            o = nrm(V(a[0], 0, a[2]) + rng.normal(size=3) * 0.2); ln = rng.uniform(0.030, 0.046); iv.append((a, nrm(o * 0.6 + rng.normal(size=3) * 0.5 + V(0, 0.3, 0)), o, ln, ln * 0.36))
        P.append(leaves(iv, (PL['vine'],)))
    P.tag, P.bone, P.allow, P.pal = 'mistle', 'Spine', None, 'mistle'
    if 'mistle' in decor:    # 겨우살이 — 관의 가지 갈림에 얹힌 둥근 덤불
        c = mains[min(1, len(mains) - 1)][1]; bl = [(c + V(*(rng.uniform(-0.05, 0.05, 3))), rng.uniform(0.07, 0.10)) for _ in range(5)]
        P.append((near(lambda q: leafy(q, bl, 0.020, 42.0), c, 0.30), PL['mistle']))
    # 관절 자리 — 게임의 뼈를 여기로 옮긴다(`Pawn/17`). 사람 뼈 자리에 두면 벌어진 다리 · 팔이 엉뚱한 축으로 돌아 늘어난다.
    joints = {'Hips': V(0, D['hipY'], 0), 'Spine': V(0, D['hipY'] + 0.05, 0), 'Head': V(0, TOP + 0.10, 0)}   # 몸통은 사람 자리(`BodyJoints`와 같다) — 크기를 키울 때 같이 큰다
    for (Hp, Kn, An, r, toes), sd in zip(legs, 'LR'): joints.update({'Leg' + sd: Hp, 'LowerLeg' + sd: Kn, 'Foot' + sd: An})
    for S, E, Wr, ra, s_ in arms: sd = 'L' if s_ < 0 else 'R'; joints.update({'Arm' + sd: S, 'LowerArm' + sd: E, 'Hand' + sd: Wr})
    return dict(pieces=P, meta=P.meta, skel=SK, joints=joints, bark_cols=(bc, trunk_col), head=V(float(T.sway(TOP + 0.22)), TOP + 0.22, float(T.zs(TOP + 0.22)) + 0.08))

FULL = (('spring', 0, 600), ('spring', 40, 600), ('spring', 150, 600), ('autumn', 0, 600), ('autumn', 40, 600))   # 계절 · 보기 각도 · 폭
CLOSE = (('spring', 0, 420), ('spring', 35, 440), ('autumn', 0, 420))
# 장식 줄 — (종류, 계절, 장식): 없음 · 하나씩 · 옹이는 자리 하나 / 셋 · 가을 색
DECOR_COLS = (('slim', 'spring', ()), ('slim', 'spring', ('moss',)), ('slim', 'spring', ('beard',)), ('slim', 'spring', ('knot1',)), ('slim', 'spring', ('knot2', 'knot4', 'knot6')),
              ('slim', 'spring', ('vine',)), ('slim', 'spring', ('mistle',)), ('slim', 'autumn', ('moss', 'beard', 'vine', 'mistle')))
BARK_ROWS = (('oak', 'spring'), ('willow', 'spring'), ('slim', 'autumn'), ('snag', 'autumn')); BARK_ORDER = tuple(BARKS)
LEAF_COLS = tuple(zip(LEAF_SHAPES, PALETTES))   # 잎 판 — 칸마다 잎 모양 하나 · 팔레트 하나(모양 넷 × 색 넷을 한 장에)
def job(a):
    mode, kind, i = a
    if mode == 'leaves':
        lf, pal = LEAF_COLS[i]; return np.asarray(hb.render(build(kind, pal, DEFAULT_DECOR[kind], leaf=lf)['pieces'], 25, 430, 860, 1.52, 3.30))
    if mode == 'bark': return np.asarray(hb.render(build(kind[0], kind[1], None, BARK_ORDER[i])['pieces'], 20, 430, 860, 1.52, 3.30))
    if mode == 'decor':
        kd, season, dc = DECOR_COLS[i]; return np.asarray(hb.render(build(kd, season, dc)['pieces'], 25, 430, 860, 1.52, 3.30))
    season, yaw, w = (FULL if mode == 'full' else CLOSE)[i]; m = build(kind, season)
    if mode == 'full': return np.asarray(hb.render(m['pieces'], yaw, w, 860, 1.52, 3.30))
    c = m['head']
    Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in m['pieces']]
    return np.asarray(hb.render(Q, yaw, w, 420, 0.0, 1.05))
def sheet(mode, cols, hgt, ims, out):
    im = Image.new('RGB', (sum(c[2] for c in cols), hgt * len(ORDER)), (40, 40, 44)); dr = ImageDraw.Draw(im)
    for r, kind in enumerate(ORDER):
        x = 0
        for i, (season, yaw, w) in enumerate(cols):
            im.paste(Image.fromarray(ims[(mode, kind, i)]), (x, r * hgt)); dr.text((x + 6, r * hgt + 18), season, fill=(200, 200, 210)); x += w
        dr.text((6, r * hgt + 4), kind, fill=(255, 220, 120))
    im.save(out); print('saved', out)
def compare(stem):
    """사용자가 보낸 그림(깃 밖 `_refs/2026-11-12_ent/`)과 나란히 — 위 그림 · 아래 넷의 정면(봄 줄 · 가을 줄)."""
    full = Image.open(stem + '_full.png'); w0, h = FULL[0][2], 860
    refs = [Image.open(_os.path.join(_here, '..', '_refs', '2026-11-12_ent', n)).convert('RGB') for n in ('1_three_ents.png', '2_tree_ent_canopy.png')]
    refs = [r.resize((int(r.width * 560 / r.height), 560), Image.LANCZOS) for r in refs]
    out = Image.new('RGB', (w0 * len(ORDER), 580 + h * 2), (40, 40, 44)); x = 10
    for r in refs: out.paste(r, (x, 10)); x += r.width + 20
    for row, col in enumerate((0, 3)):   # 봄 정면 · 가을 정면
        x0 = sum(c[2] for c in FULL[:col])
        for i in range(len(ORDER)): out.paste(full.crop((x0, i * h, x0 + w0, (i + 1) * h)), (i * w0, 580 + row * h))
    out.save(stem + '_cmp.png'); print('saved', stem + '_cmp.png')

if __name__ == '__main__':
    from multiprocessing import Pool
    stem = sys.argv[1]; only = sys.argv[2] if len(sys.argv) > 2 else 'all'
    on = lambda m: only in ('all', m)
    jobs = []
    if on('full'):  jobs += [('full', k, i) for k in ORDER for i in range(len(FULL))]
    if on('heads'): jobs += [('close', k, i) for k in ORDER for i in range(len(CLOSE))]
    if on('decor'): jobs += [('decor', '', i) for i in range(len(DECOR_COLS))]
    if on('bark'):  jobs += [('bark', r, i) for r in BARK_ROWS for i in range(len(BARK_ORDER))]
    if on('leaves'): jobs += [('leaves', k, i) for k in ORDER for i in range(len(LEAF_COLS))]
    if jobs:
        with Pool(min(14, len(jobs))) as pool: res = pool.map(job, jobs)
        ims = dict(zip(jobs, res))
        if on('full'):  sheet('full', FULL, 860, ims, stem + '_full.png')
        if on('heads'): sheet('close', CLOSE, 420, ims, stem + '_heads.png')
        if on('bark'):   # 껍질 색 판 — 줄: 종류(계절) · 칸: 껍질
            im = Image.new('RGB', (430 * len(BARK_ORDER), 860 * len(BARK_ROWS)), (40, 40, 44)); dr = ImageDraw.Draw(im)
            for r, row in enumerate(BARK_ROWS):
                for i, bk in enumerate(BARK_ORDER):
                    im.paste(Image.fromarray(ims[('bark', row, i)]), (430 * i, 860 * r)); dr.text((430 * i + 8, 860 * r + 6), '%s %s: %s' % (row[0], row[1], bk), fill=(255, 220, 120))
            im.save(stem + '_bark.png'); print('saved', stem + '_bark.png')
        if on('leaves'):   # 잎 모양 판 — 줄: 종류 · 칸: 잎 모양(팔레트)
            im = Image.new('RGB', (430 * len(LEAF_COLS), 860 * len(ORDER)), (40, 40, 44)); dr = ImageDraw.Draw(im)
            for r, kind in enumerate(ORDER):
                for i, (lf, pal) in enumerate(LEAF_COLS):
                    im.paste(Image.fromarray(ims[('leaves', kind, i)]), (430 * i, 860 * r)); dr.text((430 * i + 8, 860 * r + 6), '%s: leaf %s / %s' % (kind, lf, pal), fill=(255, 220, 120))
            im.save(stem + '_leaves.png'); print('saved', stem + '_leaves.png')
        if on('decor'):
            im = Image.new('RGB', (430 * len(DECOR_COLS), 860), (40, 40, 44)); dr = ImageDraw.Draw(im)
            for i, (kd, season, dc) in enumerate(DECOR_COLS):
                im.paste(Image.fromarray(ims[('decor', '', i)]), (430 * i, 0)); dr.text((430 * i + 8, 6), kd + ' ' + season + ': ' + (' + '.join(dc) if dc else '(none)'), fill=(255, 220, 120))
            im.save(stem + '_decor.png'); print('saved', stem + '_decor.png')
    if on('cmp') or on('full'): compare(stem)
