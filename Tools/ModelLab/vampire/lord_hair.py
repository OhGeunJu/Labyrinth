"""로드 뱀파이어 머리카락 — **매끈한 한 덩어리 + 낮게 묻은 다발**(11-18).

흐름(사용자):
  - v17~v22 가닥(납작한 띠)을 하나씩 얹었다 — *"가르마 · 방향"*은 생겼으나 골이 털 · 선처럼 읽혔다.
  - v24~v26 모양은 매끈 · 결은 칠로 — *"텍스처도 자연스럽지 않고 · 한 번에 만든 것처럼 부드럽게"*.
  - v27 면 몇 장의 매끈한 한 덩어리 — *"그 전에 머리카락 느낌 같던 게 사라져 버렸어"*.
  - **v28 — 둘 사이**: 바탕은 v27의 매끈한 한 덩어리(`top` + `curtain`), 그 위에 흐르는 다발(`Strand`)을 **낮게 묻어**(`SINK`) 넓게 녹인다 →
    날카로운 골 · 선 없이 머리 다발이 부드럽게 굽이친다. 색은 가는 결무늬 대신 다발마다 아주 조금 다른 빛 + 다발 가운데가 살짝 밝은 명암(`hair_col`).
네 벌(보낸 그림 `_refs/2026-11-15_vampire/lord_hair_*.png`):
  - `curtain` 더듬이 머리 — 이마 · 귀를 드러내고 뒤로 넘긴 윗머리 · 뒷머리는 목덜미까지 · 가운데 가르마에서 나온 더듬이 앞머리 두 갈래(턱 · 볼).
  - `slick`   올백 — 앞머리가 솟아 뒤로 넘긴 숱 · 목덜미에서 끝난다 · 귀는 드러난다.
  - `long`    장발 — 가운데 가르마 · 옆 · 뒤를 다 감싸 어깨 아래까지.
  - `lowtail` 묶음 — 올백 숱 + 목덜미에서 묶은 꽁지(꽁지 규칙 `hm.tail` — 끈 잘록 · 가늘→불룩→가늘 · 끝이 갈라진다).
단위 m · 머리 중심 원점 · +z 앞(`human.Head` 좌표)."""
import numpy as np
import human as hm
from brute_preview import V, length, smin, smax, seg
from dwarf import angle, band

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def D(el, az):   # 고도 el(수평 위 °) · 방위 az(+z 앞에서 +x로 °)
    e, a = np.radians(el), np.radians(az); return nrm([np.cos(e) * np.sin(a), np.sin(e), np.cos(e) * np.cos(a)])
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
def flat(p, n, k):
    n = nrm(n); h = (p * n).sum(-1); return p + n * (h / k - h)[..., None]
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

# ── 다발 ──
class Strand:
    """납작한 띠 다발 — 점 열 · 반지름 열 · 마디마다 납작하게 누를 법선(머리 면 바깥 · 떨어지는 구간은 수평 바깥)."""
    K = 0.24
    def __init__(self, pts, radii, face=None, cut=False, k=None):
        self.P = [np.asarray(q, np.float32) for q in pts]; self.R = list(radii); self.cut = cut
        if k is not None: self.K = k
        self.N = []
        for a, b in zip(self.P[:-1], self.P[1:]):
            m = (a + b) * 0.5; n = m.copy()
            if m[1] < -0.20: n[1] = 0.0
            if face is not None and m[1] < 0.0: n = np.asarray(face, np.float32)          # 넓은 면이 이쪽을 본다(얼굴 앞 가닥 — 앞에서 넓게 보이게)
            self.N.append(nrm(n + 1e-6))
        allp = np.stack(self.P); self.c = allp.mean(0); self.r = float(np.max(np.linalg.norm(allp - self.c, axis=1))) + max(self.R) + 0.01
    def sdf(self, p):
        b = length(p - self.c) - self.r; m = b < 0.02
        if not m.any(): return b
        q = p[m]; d = far(q)
        for i in range(len(self.P) - 1):
            a, bb, n = self.P[i], self.P[i + 1], self.N[i]
            qq = flat(q - a, n, self.K) + a
            d = smin(d, seg(qq, a, bb, self.R[i], self.R[i + 1]) * self.K, 0.010)
        if self.cut:                                                                          # 끝을 평평하게 자른다(둥근 끝 대신)
            t = nrm(self.P[-1] - self.P[-2]); d = np.maximum(d, ((q - self.P[-1]) * t).sum(-1))
        out = b.copy(); out[m] = d; return out
    def across(self, q):
        """(중심선까지 거리, 띠를 가로지르는 좌표) — 다발 명암(`hair_col`)이 쓴다."""
        best = np.full(q.shape[:-1], 9.0, np.float32); w = np.zeros(q.shape[:-1], np.float32)
        for i in range(len(self.P) - 1):
            a, b, n = self.P[i], self.P[i + 1], self.N[i]; ab = b - a
            t = np.clip(((q - a) * ab).sum(-1) / float((ab * ab).sum() + 1e-9), 0, 1)
            dv = q - (a + ab * t[..., None]); dist = length(dv)
            side = nrm(np.cross(ab, n) + 1e-6); ww = (dv * side).sum(-1)
            m = dist < best; best = np.where(m, dist, best); w = np.where(m, ww, w)
        return best, w

class Ribbon:
    """**납작한 띠**(단면이 얇은 직사각형 — 둥근 관을 누른 `Strand`와 다르다 · v41 사용자: *"메쉬 자체가 동그랗게 생겨 있잖아 — 누구 머리카락이 저렇게 동그래"*).
    점 열을 따라 띠의 폭(`W` 열)은 면을 따라 눕고 두께는 `T`로 얇다 · 모서리만 살짝 굴린다(`rc`) · 끝은 평평하게 잘린다."""
    def __init__(self, pts, widths, T=0.0035, rc=0.0015):
        self.P = [np.asarray(q, np.float32) for q in pts]; self.W = list(widths); self.rc = rc
        self.T = list(T) if np.ndim(T) else [T] * len(self.P)                                    # 두께 — 점마다 줄 수 있다(얼굴까지 채운 더듬이)
        self.F = []
        for a, b in zip(self.P[:-1], self.P[1:]):
            t = nrm(b - a); n = nrm((a + b) * 0.5)                                                # 면 바깥(머리 중심에서)
            side = nrm(np.cross(t, n)); n2 = np.cross(side, t)
            self.F.append((a, t, float(np.linalg.norm(b - a)), side, n2))
        allp = np.stack(self.P); self.c = allp.mean(0); self.r = float(np.max(np.linalg.norm(allp - self.c, axis=1))) + max(self.W) + 0.01
    def sdf(self, p):
        b = length(p - self.c) - self.r; m = b < 0.02
        if not m.any(): return b
        q = p[m]; d = far(q)
        for i, (a, t, L, side, n2) in enumerate(self.F):
            dq = q - a; s_ = (dq * t).sum(-1); h = np.clip(s_ / L, 0, 1)
            w = self.W[i] + (self.W[i + 1] - self.W[i]) * h; T = self.T[i] + (self.T[i + 1] - self.T[i]) * h
            dc = dq - t * (L * h)[..., None]                                                       # 마디 중심선의 가장 가까운 점에서
            u = np.abs((dc * side).sum(-1)) - (w - self.rc); v = np.abs((dc * n2).sum(-1)) - (T - self.rc)
            ax = np.abs((dc * t).sum(-1))                                                          # 마디 끝을 넘어간 만큼(끝을 평평하게 자르지 않는다 — v47 휘는 자리에 계단선이 보였다)
            box = length(np.stack([np.maximum(u, 0), np.maximum(v, 0), ax], -1)) + np.minimum(np.maximum(u, v), 0) - self.rc
            d = np.minimum(d, box)
        out = b.copy(); out[m] = d; return out

class Lift:
    """앞머리 부풂 — 이마선 위에서 솟아 앞 → 뒤(z) · 가운데 → 옆(|x|)으로 완만히 준다. 바탕 숱과 다발이 같은 값을 쓴다."""
    def __init__(self, top_y, amount): self.top_y, self.L = top_y, amount
    def weight(self, p):
        p = np.asarray(p, np.float32)
        side = np.clip(1 - (np.abs(p[..., 0]) / 0.36) ** 2, 0, 1)                           # 옆으로는 둥글게 완만히(v29 — 관자놀이에서 빨리 줄어 숱이 오목하게 꺼지고 그늘이 졌다 · `_refs/…/user_markup_v28_temple.png`)
        return sstep(self.top_y - 0.01, self.top_y + 0.08, p[..., 1]) * sstep(-0.24, 0.14, p[..., 2]) * side
    def __call__(self, p): return self.L * self.weight(p)

def S(h, d, off, lift=None):    # 머리 겉면 + off(+ 그 자리의 부풂) 위의 점(방향 d)
    q = hm.on_head(h, float(d[0]), float(d[1]), float(d[2]), off)
    return q if lift is None else hm.on_head(h, float(d[0]), float(d[1]), float(d[2]), off + float(lift(q)))
def taper(n, r0, r1, r2=None, root=0.012):
    if r2 is None: r = [r0 + (r1 - r0) * i / (n - 1) for i in range(n)]
    else:
        h = n // 2; r = [r0 + (r1 - r0) * i / h for i in range(h)] + [r1 + (r2 - r1) * i / (n - h - 1) for i in range(n - h)]
    r[0] = root; r[1] = (root + r[1]) * 0.5
    return r
def fall(start, n, y_end, out=0.03, back=0.0):
    s = np.asarray(start, np.float32); o = nrm([s[0], 0, s[2]] if abs(s[0]) + abs(s[2]) > 1e-4 else [0, 0, -1])
    return [s + o * out * (i / n) + V(0, (y_end - s[1]) * (i / n), -back * (i / n)) for i in range(1, n + 1)]

def slick_strands(h, lift, tail=None):
    out = []
    for u in np.linspace(-0.94, 0.94, 15):
        ths = np.linspace(46 + 16 * u * u, 222, 11); pts = []
        for k, t in enumerate(ths):
            q = S(h, nrm([u * 1.05, np.sin(np.radians(t)), np.cos(np.radians(t))]), 0.006, lift)
            if tail is not None and k >= 6:
                w = ((k - 5) / (len(ths) - 6)) ** 1.3; q = q * (1 - w) + tail * w
            pts.append(q)
        out.append(Strand(pts, taper(len(pts), 0.034, 0.044, 0.030 if tail is None else 0.020)))
    return out
def long_strands(h, lift, y_end=-0.72):
    out = []
    for el in (50, 66, 82, 98, 114, 130):
        P = D(el, 0) if el <= 90 else D(180 - el, 180)
        for s in (-1, 1):
            pts = [S(h, nrm(np.cos(np.radians(ph)) * P + np.sin(np.radians(ph)) * np.array([s, 0, 0], np.float32)), 0.009, lift) for ph in np.linspace(4, 92, 8)]
            front = el < 60
            pts += fall(pts[-1] + V(0.016 * s, 0, 0.0), 5, y_end + (0.10 if front else 0.0), out=0.045, back=0.02 if not front else 0.0)
            out.append(Strand(pts, taper(len(pts), 0.034, 0.050, 0.034)))
    for x in (-0.18, -0.06, 0.06, 0.18):
        pts = [S(h, nrm([x, np.sin(np.radians(t)), np.cos(np.radians(t))]), 0.02) for t in np.linspace(125, 200, 5)]
        pts += fall(pts[-1], 4, y_end - 0.04, out=0.03)
        out.append(Strand(pts, taper(len(pts), 0.040, 0.054, 0.036)))
    return out
CURTAIN_END = -0.30     # 1번 뒷머리 끝 — 목덜미(v35 사용자: *"뒷머리도 많이 짧아야 해"*)
LOCK_END = -0.26        # 1번 더듬이 긴 갈래 끝 — 턱
def curtain_strands(h, lift, y_end=CURTAIN_END):
    """1번 뒤 · 윗머리 다발 — 이마를 드러내고 뒤로 넘긴다(올백 다발)."""
    return slick_strands(h, lift)
def face_locks(h, lift):
    """1번 **더듬이 앞머리 두 갈래**(덧그린 그림 `_refs/2026-11-15_vampire/user_markup_v35_bangs.png`) — 머리와 녹이지 않는 따로 된 메시 두 덩어리
    (v37 사용자: *"메쉬가 두 덩어리고 잘리면"*). v38 사용자: *"왜 길게 떨어져 · 왜 둥글어 — 사진 보면 약간 휜 느낌인 일직선 · 너무 두꺼워"* →
    가르마에서 얼굴 옆을 따라 **면에 붙어 거의 일직선으로**(살짝 휜다) 내려온다 · 얇고 납작한 띠(`Ribbon`) · 끝은 뾰족하다(v43).
    길이는 갈래마다 다르다(그림 왼쪽(-x) 턱 옆 · 오른쪽 볼). `build`가 셋째 값으로 따로 내고, 굽기에서도 조각 하나씩이다."""
    out = []
    for s, (el1, az1), W in ((-1, (-30, 60), 0.032), (1, (-8, 62), 0.029)):            # 반폭(띠 폭 6cm 남짓 · v42 — 가는 줄처럼 보였다) · 두 갈래 사이 거리는 v44 그대로(v46 사용자 — 앞으로 옮기니 서로 가까워졌다)
        el0, az0 = 68, 5
        pts = []
        for t in np.linspace(0, 1, 21):                                                     # 촘촘히(v46 — 일곱 마디는 꺾인 선이 보였다)
            el = el0 + (el1 - el0) * t; az = az0 + (az1 - az0) * t ** 0.45                 # 위에서 일찍 바깥으로 벌어진 뒤 얼굴 바깥쪽을 따라 거의 곧게(v39 — 얼굴을 가로질러 눈 위를 지났다)
            pts.append(S(h, D(el, s * az), 0.012 + 0.024 * t ** 1.4, lift))              # 끝으로 갈수록 얼굴에서 떠 밖으로 나온다(v45 사용자: *"좀 더 밖으로 나오게"* — 일부러 띄운다 · 11-19 얼굴까지 채웠다가 되돌림)
        n_ = len(pts); Wd = [W * 0.55] + [max(W * (1.0 - sstep(0.45, 1.0, i / (n_ - 1))), 0.0016) for i in range(1, n_)]   # 반폭 — 뿌리 좁게 · 끝은 한 점으로 뾰족하게(v43 사용자: *"뾰족해야지"*)
        out.append(Ribbon(pts, Wd))
    return out
# ── 바탕 한 덩어리(v27) ──
def top(h, p, low, lift, th=0.016, part_x=None, cut_ears=True, side=0.0):
    """윗머리 한 장 — 바탕 숱 + 앞머리 부풂 · 가르마는 매끈한 골 하나(`part_x`) · `side` — 정수리에서 옆으로 내려갈수록 이만큼 두꺼워진다
    (긴 머리 커튼과 한 장으로 잇는다 — v32 사용자: *"윗머리랑 긴 머리랑 이어지는 느낌보다는 따로 노는 느낌"*)."""
    d = hm.scalp(h, p, th + lift(p) + side * sstep(0.18, 0.0, p[..., 1]), low)
    if part_x is not None:
        g = np.exp(-((p[..., 0] - part_x) / 0.010) ** 2) * sstep(h.EY + 0.10, h.EY + 0.18, p[..., 1]) * sstep(-0.16, -0.02, p[..., 2])
        d = d + 0.006 * g
    return hm.ear_cut(h, p, d) if cut_ears else d
def curtain(h, p, y_end, open_deg, t=0.042, top_y=0.06, flare=0.03):
    """옆 · 뒷머리 커튼 한 장 — 머리 둘레를 두른 원통 껍데기 · 얼굴 앞(방위 `open_deg` 안)만 열린다 · 아래로 조금씩 벌어진다 · 끝은 둥글다."""
    y = p[..., 1]; s = sstep(top_y, y_end, y)
    rx = h.W * 0.5 * 0.97 + t + flare * s; rz = h.D * 0.5 * 0.97 + t + flare * s
    rad = np.sqrt((p[..., 0] / rx) ** 2 + (p[..., 2] / rz) ** 2)
    ring = np.maximum(rad - 1, (1 - t / np.minimum(rx, rz) * 1.6) - rad) * np.minimum(rx, rz)
    d = smax(ring, np.maximum(y - top_y, y_end - y), 0.03)
    return smax(d, (open_deg - angle(p)) * np.pi / 180 * 0.20, 0.02)
FOLDS = 11              # 장발 커튼 둘레의 세로 주름 수
def fold_phase(p): return np.arctan2(p[..., 0], -p[..., 2]) * FOLDS + 0.8 * np.sin(p[..., 1] * 7.0)   # 주름 — 아래로 내려가며 살짝 비틀린다
def long_sheet(h, p, y_end, open_deg, t=0.044):
    """장발 옆 · 뒷머리 한 장(v39 사용자: *"옆에 오돌토돌함은 그냥 없애 버리고 전체적으로 머리카락 같은 느낌 · 밑에 긴 머리는 일직선이면 안 돼"*) —
    둘레에 부드러운 세로 주름(머리 다발의 굽이) · 아래로 떨어지며 바깥으로 물결치듯 벌어졌다가 끝에서 안으로 말린다 · 끝은 다발마다 길이가 달라 들쭉날쭉하다."""
    y = p[..., 1]; ph = fold_phase(p)
    s = sstep(0.0, y_end, y)                                                                 # 0 위 → 1 끝
    wave = 0.030 * np.sin(np.pi * s) + 0.010 * np.sin(s * 9.0) - 0.020 * sstep(0.80, 1.0, s)   # 바깥으로 물결치며 벌어지고 끝에서 안으로 말린다
    fold = 0.010 * np.cos(ph) * sstep(0.05, 0.30, s)                                         # 세로 주름(위는 잔잔하다)
    rx = h.W * 0.5 * 0.97 + t + wave + fold; rz = h.D * 0.5 * 0.97 + t + wave + fold
    rad = np.sqrt((p[..., 0] / rx) ** 2 + (p[..., 2] / rz) ** 2)
    th = t * (1 - 0.55 * sstep(0.70, 1.0, s))                                                # 끝으로 얇아진다
    ring = np.maximum(rad - 1, (1 - th / np.minimum(rx, rz) * 1.6) - rad) * np.minimum(rx, rz)
    end = y_end + 0.07 * (0.5 + 0.5 * np.cos(ph * 0.5 + 1.0)) + 0.03 * np.sin(ph * 1.3)       # 다발마다 다른 끝(들쭉날쭉)
    d = smax(ring, np.maximum(y - 0.0, end - y), 0.03)
    return smax(d, (open_deg - angle(p)) * np.pi / 180 * 0.20, 0.02)
def male_low(h, top_y, side_y, nape):
    return lambda p: hm.male_line(h, angle(p), top_y, top_y + 0.004, side_y, side_y - 0.03, nape)
TOP_Y = {'slick': 0.205, 'lowtail': 0.205, 'long': 0.210, 'curtain': 0.205}   # 이마선 높이(EY 위)
LIFT = {'slick': 0.026, 'lowtail': 0.026, 'long': 0.016, 'curtain': 0.024}     # 앞머리 부풂(m)
SINK = {'slick': 0.007, 'lowtail': 0.007, 'long': 0.021, 'curtain': 0.009}   # 다발을 바탕 속으로 묻는 깊이 — 굽이만 남고 골 · 선은 안 생긴다
BLEND = {'slick': 0.022, 'lowtail': 0.022, 'long': 0.042, 'curtain': 0.026}   # 장발 v37 — 더 깊이 · 넓게(옆머리 두드러기)  # 바탕과 다발을 녹이는 반지름
# 장발은 깊이 · 넓게(v30 사용자: *"3번 머리도 옆머리가 두드러기 난 거 같아"* — 가르마에서 내려온 다발들이 귀 높이 옆에서 모여 겹쳤다)
def split(style):
    """벌 이름 → (바탕 벌, 더듬이를 다나) — 더듬이는 따로 켜고 끄는 조각이다(v44 사용자: *"더듬이가 있는 버전의 2번 · 4번 머리를 넣고 없는 버전의 1번 머리도"*).
    `curtain`은 더듬이가 기본 · `curtain_plain`은 없음 · `slick_bangs` · `lowtail_bangs`는 더듬이를 단다."""
    base = style.split('_')[0]
    return base, style.endswith('_bangs') or style == 'curtain'
def lift_of(h, style): b, _ = split(style); return Lift(h.EY + TOP_Y[b], LIFT[b])
def strands_of(h, style):
    lf = lift_of(h, style); style, _ = split(style)
    if style in ('slick', 'lowtail'):
        tie = hm.on_head(h, *D(-12, 180), 0.030) if style == 'lowtail' else None
        return slick_strands(h, lf, tie), tie
    return (long_strands(h, lf) if style == 'long' else curtain_strands(h, lf)), None
def strands_min(st, p):
    d = far(p)
    for s_ in st: d = np.minimum(d, s_.sdf(p))
    return d

def hair_col(col, st=None, folds=False):
    """다발마다 아주 조금 다른 빛 + 다발 가운데가 살짝 밝다 + 아래로 갈수록 조금 어둡다(가는 결무늬는 없다)."""
    c0 = np.asarray(col, np.float32)
    def f(p):
        k = 1.0 - 0.14 * sstep(0.05, -0.45, p[..., 1])
        if folds: k = k * (1.0 - 0.10 * (0.5 - 0.5 * np.cos(fold_phase(p))) * sstep(-0.02, -0.15, p[..., 1]))   # 주름 골이 살짝 어둡다
        if st:
            best = np.full(p.shape[:-1], 9.0, np.float32); w = np.zeros(p.shape[:-1], np.float32); idx = np.zeros(p.shape[:-1], np.float32)
            for i, s_ in enumerate(st):
                d, ww = s_.across(p); m = d < best
                best = np.where(m, d, best); w = np.where(m, ww, w); idx = np.where(m, float(i), idx)
            k = k * (1.0 + 0.045 * np.sin(idx * 2.399)) * (1.0 + 0.07 * np.exp(-(w / 0.016) ** 2) - 0.05)
        return np.clip(c0[None] * k[..., None], 0, 1).reshape(p.shape[:-1] + (3,))
    return f

def build(h, style):
    """(거리장, 장식 거리장 | None, 따로 된 조각 거리장 목록) — 머리 중심 좌표 · 더듬이를 다는 벌은 가운데 가르마 골도 낸다."""
    EY = h.EY; lf = lift_of(h, style); st, tie = strands_of(h, style)
    style, bangs = split(style)
    parts = [(lambda p, l=l: l.sdf(p)) for l in face_locks(h, lf)] if bangs else []          # 더듬이 두 갈래 — 따로 된 조각
    b0 = split(style)[0]
    def with_locks(base, p): return smin(base, strands_min(st, p) + SINK[b0], BLEND[b0])
    if style in ('slick', 'lowtail'):
        low = male_low(h, EY + 0.205, EY + 0.08, -0.20 if style == 'slick' else -0.16)
        def f(p):
            d = with_locks(top(h, p, low(p), lf, 0.016, part_x=0.0 if bangs else None), p)
            if tie is not None: d = smin(d, hm.tail(p, tie + V(0, -0.01, -0.01), 0.02, 0.05, 0.40, 0.85), 0.02)
            return d
        deco = (lambda p: band(p, tie + V(0, 0.012, 0.002), tie + V(0, -0.026, -0.012), 0.040)) if tie is not None else None
        return f, deco, parts
    if style == 'long':
        low = male_low(h, EY + 0.210, EY + 0.04, -0.20)
        def f(p):
            return smin(top(h, p, low(p), lf, 0.018, part_x=0.0, cut_ears=False, side=0.030), long_sheet(h, p, -0.72, 62), 0.06)   # 다발은 걷었다(옆 두드러기 · v39)
        return f, None, []
    low = male_low(h, EY + 0.205, EY + 0.07, -0.22)                                        # 1번 — 올백 이마선(이마 · 귀가 드러난다)
    def f(p):
        return with_locks(smin(top(h, p, low(p), lf, 0.016, part_x=0.0 if bangs else None, side=0.022), curtain(h, p, CURTAIN_END, 112, 0.040, 0.00, 0.03), 0.05), p)
    return f, None, parts
