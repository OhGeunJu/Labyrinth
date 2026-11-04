"""엘프 — 사람 치비 머리(Male_BroadSquare)를 좁히고 엘프 이목구비 · 뾰족귀 · 긴 머리를 얹는다.
단위 m · 원점은 머리 한복판 · +z 앞. 실행: python elf.py renders/elf_v1.png [고른 줄: 0,2]
바탕 머리 · 렌더러 · 가닥 도구는 드워프 미리보기(`../dwarf`)의 것을 그대로 쓴다."""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
for _p in (_os.path.join(_here, '..', 'common'), _os.path.join(_here, '..', 'dwarf')):
    if _p not in _sys.path: _sys.path.append(_p)
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, seg, torus
from head_base import head, front, W, H, T_, R_, EYE_T, MOUTH_T, NOSE_T, EY, MY, NY, EX, ER, render
from dwarf import NONE, lock, chain, band, rings_on, ease, angle, strands

NAR = 0.88                     # 얼굴 폭 — 사람 머리를 좁힌다(11-03 사용자: 원래 폭은 "빵떡" · 좁힌 쪽이 좋다)
EXn = EX * 0.94                # 눈은 덜 좁힌다 — 좁은 얼굴에 눈이 몰리면 어려 보인다
ZE = front(EYE_T); ZN = front(NOSE_T); ZM = front(MOUTH_T)
R0 = float(np.interp(EYE_T, T_, R_)) * W * NAR   # 귀 높이의 머리 옆 반폭

def nhead(p):
    q = np.stack([p[..., 0] / NAR, p[..., 1], p[..., 2]], -1)
    return head(q) * NAR

# ── 얼굴 ──
def nose(p):         # 곧고 가는 콧대 · 작은 끝
    bridge = seg(p, V(0, EY + 0.01, ZE + 0.000), V(0, NY + 0.000, ZN + 0.030), 0.012, 0.020)
    tip = ell(p, V(0, NY - 0.004, ZN + 0.028), V(0.026, 0.020, 0.022))
    return smin(bridge, tip, 0.012)
def cheekbones(p):   # 높은 광대 — 눈 바깥 아래
    m = mirror(p)
    return ell(m, V(EXn + 0.03, EY - 0.075, ZE - 0.05), V(0.06, 0.035, 0.05))

# 귀 — 잎 모양 판. 뿌리는 머리 옆 귀 자리, 끝은 (밖 · 위 · 뒤)로. 판의 두께 방향은 축과 '앞'에 수직.
EARS = {   # 이름: (길이, 밖, 위, 뒤, 뿌리 반폭)
    'Leaf':  (0.19, 0.62, 0.70, 0.45, 0.052),   # 짧은 잎 — 머리 옆선 위로 끝만 솟는다
    'Long':  (0.27, 0.60, 0.62, 0.50, 0.058),   # 길게 위 · 뒤로
    'Sweep': (0.40, 0.55, 0.38, 0.75, 0.060),   # 뒤로 길게 뻗는다 — 거의 눕는다
}
def ear_blade(p, spec):
    L, ox, uy, bz, w0 = spec
    m = mirror(p)
    root = V(R0 - 0.02, EY - 0.025, -0.035)
    u = np.array([ox, uy, -bz], np.float32); u /= np.linalg.norm(u)
    n = np.cross(u, np.array([0, 0, 1], np.float32)); n = np.cross(n, u); n /= np.linalg.norm(n)   # 판 법선 — 대략 앞(+z)을 본다
    n = n * 0.55 + np.array([1, 0, 0], np.float32) * 0.45; n -= u * (n @ u); n /= np.linalg.norm(n)   # 조금 밖으로 기울여 정면에서도 판이 보이게
    w = np.cross(n, u)
    q = m - root
    s = (q * u).sum(-1); a = (q * w).sum(-1); c = (q * n).sum(-1)
    t = np.clip(s / L, 0, 1)
    half = w0 * np.sqrt(np.maximum(1 - t, 0)) * (0.55 + 0.45 * np.sin(np.clip(t * 1.6 + 0.35, 0, np.pi)))   # 잎 — 밑에서 부풀고 끝은 뾰족
    a = a + 0.25 * w0 * t * t          # 끝이 살짝 위로 휜다(뒤 날 쪽)
    d2 = np.maximum(np.abs(a) - half, np.maximum(-s, s - L))
    d = smax(d2, np.abs(c + 0.010 * np.sin(np.clip(t, 0, 1) * np.pi)) - (0.014 + 0.008 * (1 - t)), 0.006)
    cup = smax(np.abs(a + 0.004) - half * 0.55, np.maximum(0.02 - s, s - L * 0.80), 0.004)   # 귓바퀴 안 오목
    cup = smax(cup, -(c - 0.004), 0.004)
    return smax(d, -cup, 0.004)

def make_face(ear):
    spec = EARS[ear]
    def f(p):
        d = nhead(p)
        d = smin(d, nose(p), 0.012)
        return smin(d, ear_blade(p, spec), 0.018)
    return f

ASP = 1.35 ** 0.5   # 눈썹 높이를 잡는 눈 세로 비(`ChibiFaceLayout.DefaultEyeAspect`)
def eyes(p):         # 드워프 눈 느낌 — 진한 먹 눈 하나 + 반짝임(흰자 · 홍채는 고양이처럼 읽혔다 · 11-03 사용자)
    m = mirror(p)
    return ell(m, V(EXn, EY - 0.004, ZE - 0.014), V(ER * 0.82, ER * 0.92, 0.02))
def glint(p):
    m = mirror(p)
    return ell(m, V(EXn - ER * 0.24, EY + ER * 0.22, ZE - 0.004), V(ER * 0.24, ER * 0.24, 0.02))
def brows(p):        # 살갗에 칠한 눈썹 — 얼굴 곡면을 따라가는 얇은 띠 · 완만한 아치 · 안쪽 굵고 바깥 가늘게(솟은 가닥이면 붙인 것처럼 보인다)
    m = mirror(p)
    x0, x1 = 0.055, EXn + ER * 1.45
    t = np.clip((m[..., 0] - x0) / (x1 - x0), 0, 1)
    yc = EY + ER / ASP + 0.045 + 0.018 * np.sin(t * np.pi * 0.9) - 0.006 * t   # 가운데가 살짝 높은 아치
    th = 0.011 * (1 - t) + 0.004 * t
    band = np.maximum(np.abs(m[..., 1] - yc) - th, np.maximum(x0 - m[..., 0], m[..., 0] - x1))
    shell = np.abs(nhead(p) - 0.002) - 0.004
    return smax(smax(band, shell, 0.002), -m[..., 2], 0.002)
def mouth(p):        # 작은 입 — 얇은 선
    return rbox(p, V(0, MY + 0.005, ZM + 0.006), V(0.045, 0.005, 0.015), 0.004)

# ── 머리카락 ──
def scalp(p, th, low):
    # 숱 = 머리를 가운데에서 고르게 키운 껍데기. `nhead - th`로 부풀리면 정수리(원뿔꼴 꼭대기)에서는 회전축 위에서만 먹어
    # 가운데만 바늘처럼 솟고 둘레는 두께 0이 된다 — 모든 머리의 "튀어나온 정수리"가 그것이었다(11-03).
    s = 1 + np.asarray(th, np.float32) / 0.30
    s3 = s[..., None] if np.ndim(s) else s
    d = nhead(p / s3) * s
    return smax(d, low - p[..., 1], 0.02)
def ear_gap(p, d, spec):   # 귀가 머리카락을 뚫고 나온다 — 귀 판 모양 그대로 바로 둘레만 덜어 낸다
    # 뿌리 둘레를 공으로 파면 귀 밑에 동그란 구멍이 남는다(11-04 사용자) — 머리카락이 귀에 붙어 갈라지게
    # 귀 판만 덜면 뿌리가 숱 두께 속에 묻혀 끝만 보인다 → 귀 판을 옆으로 밀어 낸 자국(바깥 · 안)까지 덜어
    # 귀를 덮은 숱 · 앞 판 · 뒷머리가 귀 모양 그대로 갈라진다(k < 0이 바깥)
    sx = np.sign(p[..., 0])
    cut = NONE(p)
    for k in (-0.064, -0.048, -0.032, -0.016, 0.0, 0.016):
        q = p.copy(); q[..., 0] = p[..., 0] + sx * k
        cut = np.minimum(cut, ear_blade(q, spec))
    return smax(d, -(cut - 0.004), 0.004)
def part_line(p):    # 가운데 가르마 — 정수리 앞에 얕은 골
    return 0.012 * np.exp(-(p[..., 0] / 0.012) ** 2) * (p[..., 2] > -0.05) * (p[..., 1] > 0.15)

def front_line(ang):   # 이마 → 관자놀이 → 귀 뒤 → 목덜미
    top = EY + 0.25; side = EY + 0.03; nape = -0.28
    return np.where(ang < 85, top + (side - top) * ease((ang - 15) / 70), side + (nape - side) * ease((ang - 100) / 60))

def lock_r(p, pts, radii, k=0.006):   # 마디마다 굵기를 주는 다발(여성 오크 꼬리와 같은 도구)
    d = NONE(p)
    for i in range(len(pts) - 1):
        d = smin(d, seg(p, pts[i], pts[i + 1], radii[i], radii[i + 1]), k)
    return d
def bez(a, b, c, e, n):
    return [a * (1 - t) ** 3 + b * 3 * (1 - t) ** 2 * t + c * 3 * (1 - t) * t * t + e * t ** 3 for t in np.linspace(0, 1, n + 1)]
def on_cap(x, y, z, off):   # 머리 겉면(nhead + off) 위의 점 — 가로로 밀어 맞춘다
    lo, hi = 0.5, 2.0
    for _ in range(30):
        s = (lo + hi) * 0.5
        if nhead(np.array([[x * s, y, z * s]], np.float32))[0] < off: lo = s
        else: hi = s
    return V(x * lo, y, z * lo)

def flock(p, pts, r0, r1, F=2.2, k=0.02):   # 납작한 가닥 — 앞뒤(z)로 얇고 옆으로 넓다(얇게 하면서 틈이 안 나게)
    q = p * np.array([1, 1, F], np.float32)
    return lock(q, [t * np.array([1, 1, F], np.float32) for t in pts], r0, r1, k) / F
def curtain_line(ang):   # 첫 번째 머리 — 가르마에서 이마 양쪽으로 둥글게 흘러내리다(C) 관자놀이에서 꺾여 곧게 떨어진다(앞 가닥이 잇는다)
    # 관자놀이에서 귀 앞까지는 구레나룻으로 턱께까지 내려온다 — 수평으로 두면 앞 판과 귀 사이에 맨살 띠가 남아
    # 앞 판이 따로 붙은 것처럼 보였다(11-04 사용자 "구레나룻 쪽이 자연스럽게 안 이어진다")
    top, temple, burn, nape = EY + 0.26, EY + 0.05, EY - 0.12, -0.28
    fr = np.where(ang < 50, top + (temple - top) * ease(ang / 50) ** 0.8, temple + (burn - temple) * ease((ang - 50) / 38))
    return np.where(ang < 90, fr, burn + (nape - burn) * ease((ang - 90) / 70))

def nlock(p, pts, r0, r1, n, F=2.2):   # 납작한 가닥 — 바깥 방향(n)으로 얇다(옆쪽 가닥도 판처럼 붙게)
    n = np.asarray(n, np.float32)
    def sq(q): return q + (F - 1) * (q @ n)[..., None] * n
    return lock(sq(p), [sq(t) for t in pts], r0, r1, 0.02) / F
def _column(a):   # 둘레 각도 a(앞 0 · 정뒤 180)에서 숱 겉면이 가장 바깥인 자리 — 가닥은 거기서 곧게 떨어진다
    dx, dz = np.sin(np.radians(a)), np.cos(np.radians(a))
    best = None
    for y in np.linspace(-0.12, 0.12, 13):
        q = on_cap(dx * 0.3, y, dz * 0.3, 0.012)
        if best is None or np.hypot(q[0], q[2]) > np.hypot(best[0], best[2]): best = q
    return best
BACK_ANGLES = np.linspace(100, 180, 9)   # 귀 뒤부터 정뒤까지 둘러 붙인다(뒤통수 가운데만 덮으면 양끝이 턱으로 선다)
_COLS = {round(float(a), 2): _column(a) for a in BACK_ANGLES}
def back_fall(p, L=-0.80):   # 뒤로 늘어진 생머리 — 숱 겉면에 붙어 귀 뒤까지 두르고, 정수리 곡선 그대로 곧게 떨어진다
    m = mirror(p); d = NONE(p)
    for a in BACK_ANGLES:
        dx, dz = np.sin(np.radians(a)), np.cos(np.radians(a)); nrm = V(dx, 0, dz)
        side = p if a >= 179.9 else m
        c = _COLS[round(float(a), 2)]
        y_end = L + 0.14 * ((180 - a) / 80) ** 2            # 가운데가 길고 귀 뒤로 갈수록 짧다
        top = on_cap(dx * 0.3, 0.17, dz * 0.3, -0.004)        # 위는 숱 속에서 — 이음매에 턱이 안 서게
        pts = [top, V(c[0], 0.04, c[2]), V(c[0], -0.30, c[2]) + nrm * 0.004, V(c[0], y_end, c[2]) + nrm * 0.012]
        d = smin(d, nlock(side, pts, 0.055, 0.034, nrm), 0.02)
    return d

def panel(p, pts, radii, F=1.8):   # 넓적한 머리 판 — 얼굴 옆(x)으로 얇고 앞뒤로 넓다
    q = p * np.array([F, 1, 1], np.float32)
    return lock_r(q, [t * np.array([F, 1, 1], np.float32) for t in pts], [r * F for r in radii], 0.02) / F
# 앞 가닥 — 이마 곡선의 끝(관자놀이)에서 숱과 이어져 꺾이고, 얼굴 옆으로 곧게 떨어져 가슴까지
CURL = [on_cap(0.15, EY + 0.17, 0.25, 0.010), on_cap(0.21, EY + 0.07, 0.19, 0.012)]
def front_locks(p, L=-0.62):
    m = mirror(p); x1, _, z1 = (float(v) for v in CURL[1])
    pts = [CURL[0], CURL[1], V(x1 + 0.020, EY - 0.08, z1 - 0.04), V(x1 + 0.030, MY - 0.12, z1 - 0.07), V(x1 + 0.030, L, z1 - 0.10)]
    return panel(m, pts, [0.030, 0.042, 0.048, 0.044, 0.026])

def plait(p, pts, r0, r1, n=14):   # 땋은 가닥 — 길을 따라 누운 마디(가운데가 불룩 · 마디 사이가 잘록)
    P = np.array(pts, np.float32); seglen = np.linalg.norm(np.diff(P, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seglen)]); u = np.linspace(0, s[-1], n * 2 + 1)
    Q = np.stack([np.interp(u, s, P[:, i]) for i in range(3)], -1).astype(np.float32)
    d = NONE(p)
    for i in range(n):
        a, c, b = Q[2 * i], Q[2 * i + 1], Q[2 * i + 2]; r = r0 + (r1 - r0) * i / max(n - 1, 1)
        d = smin(d, np.minimum(seg(p, a, c, r * 0.62, r), seg(p, c, b, r, r * 0.62)), 0.004)
    return d

def h_straight(p, spec):   # 가운데 가르마 긴 생머리 — 뒤는 허리께 · 앞 두 가닥
    cap = scalp(p, 0.030, curtain_line(angle(p))) + part_line(p)   # 골은 더한다(빼면 정수리에 등줄기가 솟는다)
    d = smin(cap, back_fall(p), 0.04)
    d = smin(d, front_locks(p), 0.03)   # 윗머리를 넉넉히 녹인다 — 혹이 안 서게
    return ear_gap(p, d, spec) + strands(p, 0.0015, 55), NONE(p), NONE(p)   # 결은 얕게(깊으면 갈라져 보인다)
# 커튼 앞머리(11-04 사용자 그림 `_refs/2026-11-04_elf/want_curtain_bangs.png`) — 가운데 가르마에서 갈라져 이마 위로 내려온다.
# 다발(관)을 이마에서 띄워 붙이면 옆에서 볼록볼록 혹이 선다(v17 · 사용자) → 이마를 감싸는 매끈한 한 장(숱 껍데기)으로 빚고,
# 모양은 아랫단 곡선 · 뾰족한 다발 끝 · 바깥으로 쓸리는 결(골)이 낸다. 두께는 뿌리에서 숱과 같고 아랫단으로 얇아진다.
def hem_A(ang):   # A자 — 가운데(가르마)가 높고 바깥으로 갈수록 길어져 눈썹 바깥께로 · 꼭지는 둥글게(뾰족하면 어색하다 · v19 사용자)
    return np.interp(ang, [0, 8, 18, 30, 44, 58, 72], [EY + 0.17, EY + 0.155, EY + 0.125, EY + 0.10, EY + 0.08, EY + 0.065, EY + 0.05])
def hem_Arch(ang):   # 가르마에서 둥근 ∧로 흘러내리는 단(v20 맨 아래 줄 · 사용자 "좋아 보인다") — 숱 이마선이 단 아래까지 덮어 이 모양이 된다
    return np.interp(ang, [0, 3, 8, 14, 26, 38, 46, 54, 64, 72], [EY + 0.235, EY + 0.215, EY + 0.165, EY + 0.148, EY + 0.14, EY + 0.145, EY + 0.12, EY + 0.06, EY - 0.02, EY - 0.06])
def hem_M(ang):   # M자 — 사용자 그림 `_refs/2026-11-04_elf/want_bangsM_slit.png`: 이마는 가운데 좁은 세로 틈으로만 드러난다.
    # 틈 위 끝은 가운데가 살짝 파인 M · 양옆 앞머리는 틈 옆으로 곧게 떨어져 눈썹께에서 이마를 덮고, 틈 아래 끝은 바깥으로 살짝 벌어진다
    # 위는 거의 평평하고 가운데만 살짝 파인다 · 위 모서리는 둥글게 · 옆은 곧게(v22는 좁고 낮아 ∧로 읽혔다)
    return np.interp(ang, [0, 2, 4, 8, 11, 13, 14.5, 15.5, 16.5, 18, 21, 26, 34, 45, 55, 64, 72],
                     [EY + 0.24, EY + 0.25, EY + 0.262, EY + 0.265, EY + 0.26, EY + 0.245, EY + 0.22, EY + 0.18, EY + 0.13, EY + 0.10, EY + 0.08, EY + 0.07, EY + 0.065, EY + 0.065, EY + 0.04, EY - 0.02, EY - 0.06])
def bangs(p, hem, reach=72, tip=0.0):
    ang = angle(p); h = hem(ang)
    if tip:   # 다발 끝 — 둥근 물결로 아주 얕게(세모 물결 · 깊은 끝은 뾰족했다 · 결 골은 선으로 읽혀 뺐다 · v19 사용자)
        ph = (ang + 160 * (EY + 0.30 - p[..., 1])) / 15.0
        h = h - tip * (0.5 - 0.5 * np.cos(ph * 2 * np.pi)) * ease(ang / 10)   # 가르마 자리는 물결을 안 탄다(꼭지 뾰족이)
    th = 0.010 + 0.022 * ease((p[..., 1] - h) / 0.10)      # 아랫단에서 얇고 위로 숱 두께
    d = scalp(p, th, h)
    return smax(d, (ang - reach) * np.pi / 180 * 0.28, 0.01)   # 앞에만 — 옆은 숱 · 구레나룻이 든다
def h_bangs_with(hem, line, reach=72, tip=0.0):
    def f(p, spec):    # 앞머리 생머리 — 뒤 · 옆은 생머리(`h_straight`) 그대로, 앞 판 대신 커튼 앞머리
        ang = angle(p)
        low = np.where(ang < 90, line(ang), curtain_line(ang))
        cap = scalp(p, 0.030, low) + part_line(p)
        d = smin(cap, back_fall(p), 0.04)
        d = smin(d, bangs(p, hem, reach, tip), 0.015)
        return ear_gap(p, d, spec) + strands(p, 0.0012, 55) * (ang > 70), NONE(p), NONE(p)
    return f
h_bangs = h_bangs_with(hem_A, curtain_line, tip=0.006)
h_bangs_arch = h_bangs_with(hem_Arch, curtain_line)
h_bangs_m = h_bangs_with(hem_M, lambda a: np.maximum(curtain_line(a), hem_M(a)))   # 숱 이마선이 단보다 내려오면 평평한 단을 가려 다시 비스듬한 A가 된다(v20)
def h_halfup(p, spec):     # 반묶음 — 관자놀이에서 땋은 두 가닥이 뒤통수에서 만나고 아래는 풀어 내렸다
    cap = scalp(p, 0.030, front_line(angle(p))) + part_line(p)   # 두 번째 머리 앞은 C자 그대로(사용자)
    d = smin(cap, back_fall(p, -0.72), 0.04)
    m = mirror(p)
    path = [(0.20, EY + 0.17, 0.12), (0.26, EY + 0.15, 0.02), (0.27, EY + 0.13, -0.10), (0.22, EY + 0.11, -0.22), (0.13, EY + 0.09, -0.30), (0.03, EY + 0.07, -0.33)]
    pts = [on_cap(x, y, z, 0.062) for x, y, z in path]
    br = plait(m, pts, 0.026, 0.020)   # v5 장식 — 따로 칠한다(숱과 같은 색으로 녹이면 안 보인다)
    clasp = ell(p, pts[-1] * np.array([0, 1, 1], np.float32) + V(0, 0, -0.012), V(0.05, 0.034, 0.024))
    return ear_gap(p, d, spec) + strands(p, 0.0012, 45), clasp, br   # 결은 얕게
# 꽁지 굵기 — 끈 밑에서 가늘게 나와 부풀었다가 끝으로 준다(여성 오크 꼬리 `TAIL_R`)
TAIL_R = [0.030, 0.050, 0.074, 0.080, 0.070, 0.056, 0.042, 0.026, 0.012]
TIE = V(0, 0.27, -0.31)
def h_tail(p, spec):       # 뒤로 빗어 넘겨 높이 묶은 꽁지 + 이마 띠(서클릿)
    ang = angle(p)
    cap = scalp(p, 0.028, front_line(ang) + 0.02) + strands(p, 0.003, 90)
    tail = NONE(p)
    for dx, dz, s in ((-0.05, 0.00, 0.88), (-0.025, -0.03, 1.0), (0.0, 0.02, 1.06), (0.025, -0.02, 1.0), (0.05, 0.01, 0.88), (0.0, -0.05, 0.95)):   # 끝이 말꼬리처럼 갈라진다(v6 · 사용자)
        pts = bez(TIE, TIE + V(dx * 0.3, 0.13, -0.05), TIE + V(dx * 0.9, 0.10, -0.34), TIE + V(dx * 2.0, -0.90 * s, -0.20 + dz), 8)   # 끈에서 위로 솟았다가 휘어 내린다
        tail = smin(tail, lock_r(p, pts, TAIL_R), 0.03)
    d = smin(cap, tail + strands(p, 0.002, 60), 0.03)
    tie = band(p, TIE + V(0, 0.01, 0.005), TIE + V(0, 0.045, -0.01), 0.040)   # 끈 — 잘록한 목보다 살짝만 굵다
    circ = smax(np.abs(nhead(p) - 0.034) - 0.006, np.abs(p[..., 1] - (EY + 0.17 - 0.25 * np.maximum(-p[..., 2], 0))) - 0.014, 0.003)   # 이마에서 뒤로 내려간다
    gem = sph(p, V(0, EY + 0.175, front(EYE_T + 0.25) + 0.032), 0.022)
    return ear_gap(p, d, spec), np.minimum(np.minimum(tie, circ), gem), NONE(p)
HAIRS = {'Straight': h_straight, 'HalfUp': h_halfup, 'Tail': h_tail, 'Bangs': h_bangs, 'BangsM': h_bangs_m, 'BangsArch': h_bangs_arch}

def body(p): return ell(p, V(0, -0.86, -0.02), V(0.40, 0.42, 0.30))   # 좁은 어깨(개략)

SKIN = (0.93, 0.80, 0.70); DARK = (0.07, 0.05, 0.05); WHITE = (0.97, 0.97, 0.97); LIP = (0.72, 0.45, 0.42)
EYEW = (0.96, 0.95, 0.92); BLUE = (0.30, 0.52, 0.72); GREEN = (0.30, 0.58, 0.40)
TUNIC = (0.28, 0.38, 0.26); GOLD = (0.82, 0.66, 0.28); SILVER = (0.74, 0.76, 0.80)
PLAT = (0.92, 0.88, 0.74); BLOND = (0.86, 0.68, 0.36); SNOW = (0.88, 0.90, 0.92); RAVEN = (0.16, 0.13, 0.13)

def memo2(fn, spec):
    cache = {}
    def part(i):
        def f(p):
            if cache.get('k') is not p: cache['k'] = p; cache['v'] = fn(p, spec)
            return cache['v'][i]
        return f
    return part
def pieces(ear, hair, col, eyec, metal):
    h = memo2(HAIRS[hair], EARS[ear])
    return [(make_face(ear), SKIN), (eyes, DARK), (glint, WHITE), (brows, col), (mouth, LIP),
            (body, TUNIC), (h(0), col), (h(1), metal), (h(2), tuple(c * 0.80 for c in col))]

COMBOS = [('Leaf', 'Straight', PLAT, BLUE, SILVER), ('Long', 'HalfUp', BLOND, GREEN, GOLD), ('Sweep', 'Tail', SNOW, BLUE, GOLD),
          ('Long', 'Straight', RAVEN, GREEN, SILVER), ('Sweep', 'HalfUp', PLAT, BLUE, SILVER),
          ('Leaf', 'Bangs', BLOND, BLUE, SILVER), ('Long', 'Bangs', RAVEN, GREEN, SILVER),
          ('Leaf', 'BangsM', BLOND, BLUE, SILVER), ('Long', 'BangsM', RAVEN, GREEN, SILVER),
          ('Long', 'BangsArch', RAVEN, GREEN, SILVER)]

S, Hh, VIEWS = 300, 420, (0, 50, 120, 180)
def row(job):
    (e, hn, col, ec, mt), views = job
    return [np.asarray(render(pieces(e, hn, col, ec, mt), a, S, Hh, -0.20, 1.70)) for a in views]

if __name__ == '__main__':
    from multiprocessing import Pool
    out = sys.argv[1]
    if len(sys.argv) > 2: COMBOS[:] = [COMBOS[int(i)] for i in sys.argv[2].split(',')]
    views = tuple(int(a) for a in sys.argv[3].split(',')) if len(sys.argv) > 3 else VIEWS   # 보기 각도(기본 0,50,120,180)
    with Pool(len(COMBOS)) as pool: rows = pool.map(row, [(c, views) for c in COMBOS])
    sheet = Image.new('RGB', (S * len(views), Hh * len(COMBOS)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for r, ims in enumerate(rows):
        for c, im in enumerate(ims): sheet.paste(Image.fromarray(im), (c * S, r * Hh))
        e, hn = COMBOS[r][:2]; d.text((6, r * Hh + 4), f'ear {e} / hair {hn}', fill=(255, 220, 120))
    sheet.save(out); print('saved', out)
