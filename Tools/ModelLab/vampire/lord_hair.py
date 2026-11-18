"""로드 뱀파이어 머리카락 — **가닥이 흐르는 머리**(11-15 사용자: *"가르마들을 좀 신경써서 · 그냥 너무 짧은 머리 같아 — 긴 머리에서 나온 게 아니라 ·
머리카락이 향하는 방향이 없어서 그런가 · 늑대 작업하면서 잘 했잖아"*).

늑대인간 털(`werewolf.py` `fur` — 껍데기 + 살갗을 따라 누운 납작한 술)과 같은 길: 얇은 바탕 껍데기(`hm.scalp`) 위에 **납작한 띠 가닥**을 가르마 · 이마선에서
하나씩 뽑아 머리 면을 따라 흐르게 하고, 긴 머리는 귀 옆 · 뒤에서 아래로 떨어뜨린다. 가닥 사이가 갈라져 결(방향)이 보이고, 가르마는 가닥이 갈라지는 골로 읽힌다.
네 벌(보낸 그림 `_refs/2026-11-15_vampire/lord_hair_*.png`):
  - `slick`   올백 — 이마선(V선)에서 뒤로 나란히 넘긴 가닥 · 목덜미에서 끝난다.
  - `lowtail` 묶음 — 같은 올백 가닥이 목덜미 한 점으로 모이고 꽁지 규칙(`hm.tail` — 끈 잘록 · 가늘→불룩→가늘 · 끝이 갈라진다)으로 늘어뜨린다.
  - `long`    장발 — 가운데 가르마에서 양옆으로 흘러내려 귀 옆으로 어깨 아래까지 떨어진다.
  - `curtain` 흘러내린 머리 — 옆 가르마에서 한쪽은 이마를 비스듬히 가로질러 넘어가고 · 가는 가닥 둘이 얼굴 앞으로 흘러내린다 · 기장은 턱 ~ 어깨.
단위 m · 머리 중심 원점 · +z 앞(`human.Head` 좌표)."""
import numpy as np
import human as hm
from brute_preview import V, length, smin, smax, seg, mirror
from dwarf import angle, band

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def D(el, az):   # 고도 el(수평 위 °) · 방위 az(+z 앞에서 +x로 °)
    e, a = np.radians(el), np.radians(az); return nrm([np.cos(e) * np.sin(a), np.sin(e), np.cos(e) * np.cos(a)])
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
def flat(p, n, k):
    n = nrm(n); h = (p * n).sum(-1); return p + n * (h / k - h)[..., None]

class Strand:
    """납작한 띠 가닥 — 점 열 · 반지름 열 · 마디마다 납작하게 누를 법선(머리 면 바깥 · 떨어지는 구간은 수평 바깥)."""
    K = 0.30   # v18 — 더 납작하게(둥근 줄처럼 울퉁불퉁했다)
    def __init__(self, pts, radii):
        self.P = [np.asarray(q, np.float32) for q in pts]; self.R = list(radii)
        self.N = []
        for a, b in zip(self.P[:-1], self.P[1:]):
            m = (a + b) * 0.5; n = m.copy()
            if m[1] < -0.20: n[1] = 0.0                                              # 떨어지는 구간 — 수평 바깥으로 누른다
            self.N.append(nrm(n + 1e-6))
        allp = np.stack(self.P); self.c = allp.mean(0); self.r = float(np.max(np.linalg.norm(allp - self.c, axis=1))) + max(self.R) + 0.01
    def sdf(self, p):
        b = length(p - self.c) - self.r; m = b < 0.02
        if not m.any(): return b
        q = p[m]; d = far(q)
        for i in range(len(self.P) - 1):
            a, bb, n = self.P[i], self.P[i + 1], self.N[i]
            qq = flat(q - a, n, self.K) + a
            d = smin(d, seg(qq, a, bb, self.R[i], self.R[i + 1]) * self.K, 0.008)
        out = b.copy(); out[m] = d; return out

def S(h, d, off):    # 머리 겉면 + off 위의 점(방향 d)
    return hm.on_head(h, float(d[0]), float(d[1]), float(d[2]), off)
def taper(n, r0, r1, r2=None, root=0.012):
    """굵기 열 — 뿌리(root)에서 가늘게 나와 r0 → r1 → r2(v19 — 뿌리가 굵으면 이마선이 모자챙처럼 섰다)."""
    if r2 is None: r = [r0 + (r1 - r0) * i / (n - 1) for i in range(n)]
    else:
        h = n // 2; r = [r0 + (r1 - r0) * i / h for i in range(h)] + [r1 + (r2 - r1) * i / (n - h - 1) for i in range(n - h)]
    r[0] = root; r[1] = (root + r[1]) * 0.5
    return r
def fall(start, n, y_end, out=0.03, back=0.0):
    """떨어지는 구간 — start에서 아래로 곧게(바깥으로 out · 뒤로 back 조금씩)."""
    s = np.asarray(start, np.float32); o = nrm([s[0], 0, s[2]] if abs(s[0]) + abs(s[2]) > 1e-4 else [0, 0, -1])
    return [s + o * out * (i / n) + V(0, (y_end - s[1]) * (i / n), -back * (i / n)) for i in range(1, n + 1)]

# ── 바탕 껍데기(가닥 사이 틈을 메운다) ──
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def front_mask(p, top_y):
    """앞머리 자리 — 이마 위 앞쪽(방위 45° 안 · 이마선 위). 여기는 매끈하게 솟고 결 그늘을 걷는다(v21 사용자: *"앞머리는 좀 부드럽고 자연스럽게 올라가게 ·
    그 뒤에 지금처럼 표현"*)."""
    return sstep(55, 25, angle(p)) * sstep(top_y - 0.02, top_y + 0.03, p[..., 1]) * (p[..., 2] > 0)
def shell(h, p, top_y, side_y, nape, th=0.010, lift=0.024):
    low = hm.male_line(h, angle(p), top_y, top_y + 0.004, side_y, side_y - 0.03, nape)
    up = sstep(top_y - 0.005, top_y + 0.07, p[..., 1]) * sstep(60, 20, angle(p))            # 이마선에서 위로 부드럽게 솟는 앞머리
    return hm.ear_cut(h, p, hm.scalp(h, p, th + lift * up, low))

# ── 올백 · 묶음 ──
def slick_strands(h, tail=None):
    out = []
    for u in np.linspace(-0.94, 0.94, 17):
        th0 = 52 + 14 * u * u                                                               # 가닥은 앞머리(매끈하게 솟는 자리) 뒤에서 시작한다(v22 — 이마선에 뿌리가 모여 혹이 졌다)                               # 이마선(바탕 숱의 선에 맞춘다 — v19는 맨 이마에서 뿌리가 떠 띠처럼 보였다) — 가운데 V · 관자놀이로 오른다
        ths = np.linspace(th0, 222, 11)
        pts = []
        for k, t in enumerate(ths):
            d = nrm([u * 1.05, np.sin(np.radians(t)), np.cos(np.radians(t))])
            off = 0.004 + 0.008 * np.sin(np.pi * k / (len(ths) - 1)) ** 0.8                 # 정수리 쪽이 조금 부푼다(v17은 네모 모자처럼 섰다)
            q = S(h, d, off)
            if tail is not None and k >= 6:                                                 # 묶음 — 목덜미 한 점으로 모은다
                w = ((k - 5) / (len(ths) - 6)) ** 1.3; q = q * (1 - w) + tail * w
            pts.append(q)
        out.append(Strand(pts, taper(len(pts), 0.030, 0.040, 0.026 if tail is None else 0.018)))
    return out
# ── 장발 · 가운데 가르마 ──
def long_strands(h, y_end=-0.72):
    out = []
    for el in (50, 64, 78, 92, 106, 120, 134):
        P = D(el, 0) if el <= 90 else D(180 - el, 180)
        for s in (-1, 1):
            pts = []
            for k, ph in enumerate(np.linspace(4, 92, 8)):
                d = nrm(np.cos(np.radians(ph)) * P + np.sin(np.radians(ph)) * np.array([s, 0, 0], np.float32))
                pts.append(S(h, d, 0.009))                                            # 같은 높이로 눕는다(v19는 층이 져 비늘 같았다)
            front = el < 60
            pts += fall(pts[-1] + V(0.012 * s, 0, 0.0), 5, y_end + (0.10 if front else 0.0), out=0.035, back=0.02 if not front else 0.0)
            out.append(Strand(pts, taper(len(pts), 0.030, 0.046, 0.026)))
    for x in (-0.20, -0.13, -0.065, 0.0, 0.065, 0.13, 0.20):                                   # 뒤 — 정수리에서 등으로 곧게(v21 — 뒷머리를 다 감싼다)
        pts = [S(h, nrm([x, np.sin(np.radians(t)), np.cos(np.radians(t))]), 0.02) for t in np.linspace(125, 200, 5)]
        pts += fall(pts[-1], 4, y_end - 0.04, out=0.02)
        out.append(Strand(pts, taper(len(pts), 0.036, 0.050, 0.030)))
    return out
# ── 흘러내린 머리 · 옆 가르마 ──
def curtain_strands(h, y_end=-0.25):   # 턱까지(v21 사용자: *"첫번째 머리는 턱까지만 오는 짧은 머리"*)
    out = []; PX = -0.20                                                                      # 가르마 — 왼쪽(-x)으로 치우친다
    for el in (50, 64, 78, 92, 106, 120):
        if el <= 90: P = nrm([PX, np.sin(np.radians(el)), np.cos(np.radians(el))])
        else: P = nrm([PX * 0.6, np.sin(np.radians(180 - el)), -np.cos(np.radians(180 - el))])
        for s in (-1, 1):
            n_sw = 8 if s > 0 else 6
            pts = []
            for k, ph in enumerate(np.linspace(4, 95 if s > 0 else 80, n_sw)):
                side = np.array([s, 0, 0], np.float32)
                dip = np.array([0, -0.35, 0.25], np.float32) * (k / n_sw) if (s > 0 and el < 60) else 0.0   # 이마를 비스듬히 가로질러 내려간다
                d = nrm(np.cos(np.radians(ph)) * P + np.sin(np.radians(ph)) * side + dip)
                pts.append(S(h, d, 0.009))
            pts += fall(pts[-1] + V(0.010 * s, 0, 0.0), 3, y_end + (0.04 if el < 60 else -0.01 if el > 90 else 0.0), out=0.02)
            out.append(Strand(pts, taper(len(pts), 0.028, 0.044, 0.020)))
    for k, (az, r) in enumerate(((10, 0.010), (24, 0.009))):                                  # 얼굴 앞으로 흘러내리는 가는 가닥 둘
        pts = [S(h, D(el, az + 4 * k), 0.014 + 0.004 * k) for el in (44, 30, 16, 2, -12)]
        out.append(Strand(pts, taper(len(pts), r * 1.6, r * 1.5, 0.005)))
    for x in (-0.10, 0.04):                                                                   # 뒤 — 목덜미 아래로
        pts = [S(h, nrm([x, np.sin(np.radians(t)), np.cos(np.radians(t))]), 0.018) for t in np.linspace(120, 205, 5)]
        pts += fall(pts[-1], 2, y_end - 0.02, out=0.015)
        out.append(Strand(pts, taper(len(pts), 0.034, 0.044, 0.026)))
    return out

TOP_Y = {'slick': 0.205, 'lowtail': 0.205, 'long': 0.210, 'curtain': 0.200}   # 이마선 높이(EY 위)
def strands_of(h, style):
    if style in ('slick', 'lowtail'):
        tie = S(h, D(-12, 180), 0.030) if style == 'lowtail' else None
        return slick_strands(h, tie), tie
    return (long_strands(h) if style == 'long' else curtain_strands(h)), None
def groove_col(st, col, top_y=None):
    """결 그늘 — 숱 위 점이 가닥 띠에서 멀수록(가닥 사이 골 · 껍데기만 있는 곳) 짙다 → 머리카락이 흐르는 방향대로 결이 그어진다."""
    c0 = np.asarray(col, np.float32); c1 = c0 * 0.55
    def f(p):
        d = far(p)
        for s_ in st: d = np.minimum(d, s_.sdf(p))
        k = np.clip((d - 0.0005) / 0.006, 0, 1)
        if top_y is not None: k = k * (1 - front_mask(p, top_y))
        k = k[..., None]
        return c0 * (1 - k) + c1 * k
    return f
def back_sheet(h, p, y_end):
    """뒷머리 한 장 — 머리 둘레를 두른 얇은 원통 껍데기(귀 뒤부터 정뒤까지 · 정수리 밑에서 등까지) · 가닥 사이를 메운다."""
    t = 0.040; rx, rz = h.W * 0.5 * 0.97 + t, h.D * 0.5 * 0.97 + t
    rad = np.sqrt((p[..., 0] / rx) ** 2 + (p[..., 2] / rz) ** 2)
    ring = np.maximum(rad - 1, (1 - t / min(rx, rz) * 1.6) - rad) * min(rx, rz)
    d = smax(ring, np.maximum(p[..., 1] - 0.06, (y_end - 0.02) - p[..., 1]), 0.03)
    return smax(d, p[..., 2] - (-0.02 + 0.10 * sstep(-0.05, 0.20, p[..., 1])), 0.04)
def build(h, style):
    """(거리장, 장식 거리장 | None) — 머리 중심 좌표."""
    EY = h.EY
    if style in ('slick', 'lowtail'):
        tie = S(h, D(-12, 180), 0.030) if style == 'lowtail' else None
        st = slick_strands(h, tie)
        def f(p):
            d = shell(h, p, EY + 0.205, EY + 0.08, -0.20 if style == 'slick' else -0.16)
            for s_ in st: d = smin(d, s_.sdf(p), 0.010)
            if tie is not None: d = smin(d, hm.tail(p, tie + V(0, -0.01, -0.01), 0.02, 0.05, 0.40, 0.85), 0.02)
            return d
        deco = (lambda p: band(p, tie + V(0, 0.012, 0.002), tie + V(0, -0.026, -0.012), 0.040)) if tie is not None else None
        return f, deco
    if style == 'long':
        st = long_strands(h)
        def f(p):
            d = smin(shell(h, p, EY + 0.210, EY + 0.04, -0.20, 0.012), back_sheet(h, p, -0.72), 0.03)
            for s_ in st: d = smin(d, s_.sdf(p), 0.012)
            return d
        return f, None
    st = curtain_strands(h)
    def f(p):
        d = shell(h, p, EY + 0.200, EY + 0.04, -0.20, 0.012)
        for s_ in st: d = smin(d, s_.sdf(p), 0.010)
        return d
    return f, None
