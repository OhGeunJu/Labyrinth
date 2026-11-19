"""포자두꺼비 — 굴의 네 발 짐승(11-19 사용자: *"키는 사람 크기 정도 · 등에 버섯보다는 포자가 있으면 좋겠어"*).

앉은 두꺼비(머리 꼭대기가 사람 키쯤 — 단위 몸 × `SIZE`):
  - 몸 — 앞이 들린 술통 · 배가 땅에 닿을 듯 처진다 · 목 없이 넓적한 머리가 몸에 녹는다.
  - 입 — 머리를 가로지르는 넓은 홈(살에 파낸다 · 바닥만 어둡다) · 콧구멍 둘은 주둥이 위의 작은 구멍.
  - 눈 — 머리 위로 솟은 금빛 눈 · 가로 동공 · 무거운 윗눈꺼풀 · 반짝임은 칠한다.
  - 포자 — **등에 구멍 난 포자 주머니 무리**(크고 작은 것이 섞여 살에 묻힌다 · 꼭대기에 터진 구멍 · 구멍 둘레에 포자 가루 색) ·
    눈 뒤 독샘 자리는 **체처럼 구멍이 숭숭한 큰 포자샘**.
  - 살 — 올리브 갈색 + 어두운 얼룩 · 사마귀 · 배와 턱 밑은 누런 흰색.
  - 다리 — 앞다리는 곧게 짚고(손가락 넷 · 끝이 동그란 혹) · 뒷다리는 옆구리에 접는다(물갈퀴 발가락 다섯).
조각 = (거리 함수, 색, 부위) — 부위는 게임 네발 관절(`Body` · `Head` · `Tail` · `LegFL/FR/BL/BR`). 꼬리는 없다.
실행: python toad.py renders/toad_v1"""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))
import sys
import numpy as np
from quad_preview import (V, length, mirror, smin, smax, ell, sph, seg, UP, nrm, far, sstep, mixc, frame, oell,
                          sheet, surface_along, fluff, polyline)

# ── 색 ──
# v4 — 균사림이 푸른 바이옴이라 푸른 두꺼비(사용자 11-19)
SKIN = (0.17, 0.32, 0.44); SKIN_D = (0.08, 0.16, 0.27); BELLY = (0.68, 0.77, 0.82); WART = (0.30, 0.52, 0.64)
POD = (0.36, 0.66, 0.76); POD_L = (0.66, 0.90, 0.95); PORE = (0.04, 0.08, 0.14); DUST = (0.60, 0.98, 1.00)
GOLD = (0.95, 0.66, 0.12); GOLD_D = (0.55, 0.22, 0.04); PUPIL = (0.03, 0.03, 0.03); MOUTH = (0.22, 0.09, 0.08)

def _grad(f, s, e=1e-3):
    s = np.asarray(s, np.float32); g = np.zeros(3, np.float32)
    for i in range(3):
        d = np.zeros(3, np.float32); d[i] = e
        g[i] = f((s + d)[None, :])[0] - f((s - d)[None, :])[0]
    return nrm(g)

# ── 몸 — v5(사용자가 보낸 그림의 느낌: 등이 높이 솟은 혹 · 머리는 낮게 앞으로 숙인다) ──
BODY_C = V(0, 0.50, -0.12)
R_BODY = frame(V(0, 0.15, 1), UP)
def _body0(p):
    d = oell(p, BODY_C, R_BODY, V(0.50, 0.44, 0.52))
    d = smin(d, ell(p, V(0, 0.28, 0.06), V(0.44, 0.24, 0.40)), 0.10)          # 처진 배
    d = smin(d, ell(p, V(0, 0.34, -0.44), V(0.42, 0.28, 0.26)), 0.10)         # 엉덩이
    return smax(d, 0.06 - p[..., 1], 0.05)

# ── 머리 — 낮고 넓적한 쐐기 · 두꺼운 눈두덩이 작은 눈을 덮는다 ──
HEAD_C = V(0, 0.42, 0.42)
def _head_base(p):
    d = ell(p, HEAD_C, V(0.38, 0.18, 0.30))
    d = smin(d, ell(p, V(0, 0.42, 0.60), V(0.27, 0.12, 0.15)), 0.07)          # 납작한 주둥이
    return smin(d, ell(p, V(0, 0.34, 0.44), V(0.35, 0.12, 0.26)), 0.08)       # 무거운 아래턱
# 눈은 머리 겉에서 자리를 찾고(v6 — v5는 눈두덩 · 볼이 눈을 다 덮었다) 눈두덩 · 볼은 눈에 맞춰 붙인다
EYE_R = 0.044; EYE_N = nrm(V(0.50, 0.10, 0.86))
EYE_C = surface_along(_head_base, V(0.12, 0.50, 0.45), V(0.55, 0.12, 0.83)) + EYE_N * 0.004
BROW_C = EYE_C + V(-0.026, 0.050, -0.052); CHEEK_C = EYE_C + V(0.002, -0.046, -0.040)
def _head0(p):
    d = _head_base(p); m = mirror(p)
    brow = oell(m, BROW_C, frame(V(0.30, -0.40, 1), UP), V(0.105, 0.036, 0.068))   # 눈두덩 — 안쪽 · 앞이 낮게 덮는다
    d = smin(d, brow, 0.05)
    return smin(d, ell(m, CHEEK_C, V(0.065, 0.026, 0.048)), 0.03)                  # 눈 밑 볼 — 눈이 가는 틈
def _skin0(p): return smin(_head0(p), _body0(p), 0.12)

# 입 — 주둥이 앞에서 양옆 뒤로 머리를 가로지르는 홈(살에 파낸다 · 바닥만 어둡다)
_MO = V(0, 0.38, 0.40)
MOUTH_P = [surface_along(_skin0, _MO, V(np.sin(a), -0.04 - 0.40 * (a / 1.5) ** 3, np.cos(a))) for a in np.radians(np.linspace(0, 88, 10))]
MOUTH_IN = [_MO + (q - _MO) * (1 - 0.010 / float(np.linalg.norm(q - _MO))) for q in MOUTH_P]
NOSTRIL = surface_along(_skin0, V(0.050, 0.44, 0.62), V(0.10, 0.55, 0.83))
def _cuts(p):
    m = mirror(p)
    return np.minimum(polyline(m, MOUTH_P, 0.011), sph(m, NOSTRIL, 0.013))

# ── 포자 — 등의 주머니 무리 + 눈 뒤의 큰 포자샘 ──
def _pod_layout():
    rng = np.random.default_rng(11); out = []
    for _ in range(6000):
        if len(out) >= 19: break
        d = nrm(rng.normal(size=3))
        if d[1] < 0.42 or d[2] > 0.22 or d[2] < -0.88: continue
        r = float(rng.uniform(0.045, 0.105)) if len(out) > 4 else float(rng.uniform(0.09, 0.115))
        s = surface_along(_body0, BODY_C + V(0, 0.04, 0), d)
        if any(np.linalg.norm(s - o[0]) < (r + o[2]) * 0.92 for o in out): continue
        if abs(s[0]) < 0.06 and r > 0.09: continue                           # 큰 것이 등줄기 한가운데 서지 않게
        out.append((s, _grad(_skin0, s), r))
    return [(s + n * r * 0.40, n, r) for s, n, r in out]                    # 살에 반쯤 묻힌다
PODS = _pod_layout()
PORES = [(c + n * r * 1.10, r * 0.21) for c, n, r in PODS]                   # 숨구멍 굴뚝 끝의 좁은 구멍(v2 — 넓은 구멍은 눈알 같았다)
GLAND_C = V(0.25, 0.700, 0.200); GLAND_R = frame(nrm(V(0.28, -0.22, -1)), UP); GLAND_S = V(0.095, 0.078, 0.175)
def _gland0(p): return oell(mirror(p), GLAND_C, GLAND_R, GLAND_S)
def _gland_pores():
    out = []
    for u, v in ((0.0, -0.55), (0.0, -0.15), (0.0, 0.25), (0.0, 0.62), (-0.45, -0.30), (-0.45, 0.20), (0.45, -0.05), (0.42, 0.45)):
        loc = V(u * GLAND_S[0], 0, v * GLAND_S[2]); o = GLAND_C + GLAND_R @ loc
        s = surface_along(_gland0, o, GLAND_R @ V(u * 0.6, 1, 0)); out.append((s, 0.020 - 0.004 * abs(v)))
    return out
GPORES = _gland_pores()

def _near(p, base, fn, th=0.06):
    """base가 th보다 먼 점은 셈을 건너뛴다(사마귀 · 주머니 수십 개의 값을 아낀다)."""
    out = np.full(p.shape[:-1], 9.0, np.float32); k = base < th
    if k.any(): out[k] = fn(p[k])
    return out
def _pods_raw(q):
    d = np.full(q.shape[:-1], 9.0, np.float32)
    for c, n, r in PODS:
        dome = oell(q, c, frame(n, V(0, 0, 1) if abs(n[1]) > 0.95 else UP), V(r, r, r * 0.78))   # 낮게 눌린 돔
        vent = seg(q, c + n * r * 0.55, c + n * r * 1.06, r * 0.34, r * 0.24)                       # 꼭대기 숨구멍 굴뚝
        d = np.minimum(d, smin(dome, vent, r * 0.18))
    for c, r in PORES: d = smax(d, -sph(q, c, r), 0.008)
    return d
def _gland(p):
    d = _gland0(p); m = mirror(p)
    for c, r in GPORES: d = smax(d, -sph(m, c, r), 0.005)
    return d
def _pore_k(p):
    """구멍 안(1) · 둘레 가루(0~1)."""
    dark = np.zeros(p.shape[:-1], np.float32); dust = np.zeros(p.shape[:-1], np.float32)
    for c, r in PORES:
        t = length(p - c) / r; dark = np.maximum(dark, sstep(1.35, 1.05, t)); dust = np.maximum(dust, sstep(2.6, 1.4, t))
    return dark, dust

# ── 사마귀 ──
def _wart_layout():
    rng = np.random.default_rng(5); out = []
    for _ in range(4000):
        if len(out) >= 70: break
        d = nrm(rng.normal(size=3))
        if d[1] < -0.05: continue
        o = HEAD_C if d[2] > 0.55 else BODY_C
        s = surface_along(_skin0, o, d)
        if s[1] < 0.30: continue
        if any(np.linalg.norm(s - c) < 0.07 for c, _, _ in out): continue
        if any(np.linalg.norm(s - c) < r * 1.3 for c, _, r in PODS): continue
        if np.linalg.norm(mirror(s[None, :])[0] - EYE_C) < 0.11: continue
        out.append((s, _grad(_skin0, s), float(rng.uniform(0.012, 0.026))))
    return [(s + n * r * 0.15, r) for s, n, r in out]
WARTS = _wart_layout()
def _warts_raw(q):
    d = np.full(q.shape[:-1], 9.0, np.float32)
    for c, r in WARTS: d = np.minimum(d, sph(q, c, r))
    return d

def skin(p):
    s = _skin0(p)
    d = smin(s, _near(p, s, _warts_raw, 0.05), 0.012)
    d = smin(d, _near(p, s, _pods_raw, 0.14), 0.035)
    return smax(d, -_cuts(p), 0.006)
def skin_col(p):
    y, z = p[..., 1], p[..., 2]
    blot = sstep(0.30, 0.65, fluff(p * 0.45))
    c = mixc(SKIN, SKIN_D, np.maximum(blot * 0.75, sstep(0.62, 0.90, y) * 0.35))
    s = _skin0(p)
    c = mixc(c, WART, sstep(0.004, -0.010, _near(p, s, _warts_raw, 0.05)) * 0.85)
    belly = np.maximum(sstep(0.30, 0.15, y), sstep(0.40, 0.32, y) * sstep(0.26, 0.40, z) * sstep(0.34, 0.18, np.abs(p[..., 0])))
    c = mixc(c, BELLY, belly)
    # 포자 주머니 · 포자샘 — 살에서 솟은 자리는 누런 주머니 색 · 꼭대기로 갈수록 밝다 · 구멍 안은 검고 둘레에 가루
    pod = _near(p, s, _pods_raw, 0.14)
    kp = sstep(0.015, -0.002, pod)
    top = np.zeros(p.shape[:-1], np.float32)
    for c0, n, r in PODS: top = np.maximum(top, sstep(0.1, 0.9, ((p - c0) @ n) / r) * sstep(r * 1.6, r * 0.9, length(p - c0)))
    pc = mixc(POD, POD_L, top * 0.8)
    pc = mixc(pc, (0.16, 0.34, 0.46), sstep(0.60, 0.85, fluff(p * 3.2)) * 0.40)          # 주머니 겉의 짙은 반점
    dark, dust = _pore_k(p)
    pc = mixc(pc, DUST, dust * 0.9); pc = mixc(pc, PORE, dark)
    return mixc(c, pc, kp)

# ── 눈 — 금빛 · 가로 동공 · 반짝임은 칠한다 ──
def eyes(p): return sph(mirror(p), EYE_C, EYE_R)
_ET = nrm(np.cross(UP, EYE_N)); _EU = np.cross(EYE_N, _ET)
_HL = [(nrm(EYE_N * 0.80 + _EU * 0.38 - _ET * 0.30), 0.30), (nrm(EYE_N * 0.90 - _EU * 0.30 + _ET * 0.25), 0.15)]
GLOW = (0.62, 1.00, 0.42); GLOW_C = (1.00, 1.00, 0.78)
def eye_col(p):
    q = mirror(p) - EYE_C; q = q / np.maximum(length(q), 1e-6)[..., None]
    a = q @ EYE_N
    return mixc(mixc((0.10, 0.30, 0.12), GLOW, sstep(0.0, 0.6, a)), GLOW_C, sstep(0.85, 0.97, a))
def mouth(p): return polyline(mirror(p), MOUTH_IN, 0.006)
TUSK = (0.90, 0.84, 0.48)
TUSK_A = surface_along(_skin0, V(0.15, 0.34, 0.50), V(0.45, -0.1, 0.89)) - V(0.008, 0, 0.008)
def tusks(p):
    m = mirror(p); b = TUSK_A + V(0.012, 0.085, 0.010)
    return smin(seg(m, TUSK_A, (TUSK_A + b) / 2 + V(0.006, 0, 0.008), 0.020, 0.015), seg(m, (TUSK_A + b) / 2 + V(0.006, 0, 0.008), b, 0.015, 0.003), 0.01)

# ── 앞다리 — 곧게 짚는다 · 손가락 넷 · 끝이 동그란 혹 ──
SH = V(0.32, 0.30, 0.30); EL = V(0.39, 0.16, 0.38); WR = V(0.36, 0.06, 0.46); HAND = V(0.37, 0.028, 0.52)
FINGERS = [HAND + V(np.sin(a) * 0.13, -0.006, np.cos(a) * 0.13) for a in np.radians((-35, -5, 25, 60))]
def arms(p):
    m = mirror(p)
    d = smin(seg(m, SH, EL, 0.100, 0.085), seg(m, EL, WR, 0.085, 0.065), 0.03)
    d = smin(d, ell(m, HAND, V(0.065, 0.028, 0.060)), 0.03)
    for f in FINGERS: d = smin(smin(d, seg(m, HAND, f, 0.024, 0.017), 0.015), sph(m, f, 0.024), 0.01)
    return d

# ── 뒷다리 — 옆구리에 접는다 · 물갈퀴 발가락 다섯 ──
HIP = V(0.32, 0.26, -0.40); KNEE = V(0.50, 0.24, -0.04); ANK = V(0.43, 0.09, -0.32); FOOT = V(0.56, 0.030, -0.14)
TOES = [FOOT + V(np.sin(a) * L, -0.004, np.cos(a) * L) for a, L in zip(np.radians((-25, 0, 22, 45, 72)), (0.13, 0.17, 0.21, 0.18, 0.13))]
def legs(p):
    m = mirror(p)
    d = smin(seg(m, HIP, KNEE, 0.150, 0.100), ell(m, V(0.40, 0.30, -0.20), V(0.12, 0.17, 0.24)), 0.06)   # 굵은 넓적다리
    d = smin(d, seg(m, KNEE, ANK, 0.085, 0.058), 0.04)
    d = smin(d, seg(m, ANK, FOOT, 0.055, 0.045), 0.03)
    for t in TOES: d = smin(smin(d, seg(m, FOOT, t, 0.022, 0.014), 0.012), sph(m, t, 0.020), 0.008)
    web = far(p)
    for a, b in zip(TOES, TOES[1:]):                                          # 발가락 사이 물갈퀴 — 얇은 판
        c = (FOOT + a * 0.8 + b * 0.8) / 2.6; web = np.minimum(web, ell(m, c, V(0.075, 0.008, 0.075)))
    return smin(d, web, 0.01)
def limb_col(p):
    y = p[..., 1]
    c = mixc(SKIN, SKIN_D, sstep(0.30, 0.65, fluff(p * 0.45)) * 0.7)
    return mixc(c, BELLY, sstep(0.10, 0.02, y) * 0.5)

def _pieces(head_only=False):
    H = [(eyes, eye_col, 'Head'), (mouth, MOUTH, 'Head'), (tusks, TUSK, 'Head')]
    if head_only: return H + [(skin, skin_col, 'Body')]          # 가까이 보기 — 다리만 뺀다(등의 포자도 본다)
    return H + [(skin, skin_col, 'Body'), (arms, limb_col, 'LegF'), (legs, limb_col, 'LegB')]

# 크기 — 위 수치는 단위 몸(머리 꼭대기 약 0.9). 사람 키쯤 되게 `SIZE`배.
SIZE = 1.85
def pieces(head_only=False):
    S = SIZE
    return [((lambda f: lambda p: f(p / S) * S)(f), ((lambda g: lambda p: g(p / S))(c) if callable(c) else c), part)
            for f, c, part in _pieces(head_only)]

VIEW = (0.50 * SIZE, 1.28 * SIZE)
CLOSE = ((0, 420, 0), (40, 460, 0), (90, 460, 0), (150, 460, 35), (30, 460, 50))   # 넷째 — 뒤에서 등 · 다섯째 — 위에서
CLOSE_C = V(0, 0.55, 0.30) * SIZE; CLOSE_SPAN = 0.85 * SIZE
SCALE_Z = -2.40; SCALE_SPAN = 3.2

if __name__ == '__main__':
    sheet('toad', sys.argv[1] if len(sys.argv) > 1 else 'renders/toad_v1', sys.argv[2] if len(sys.argv) > 2 else 'spore toad v1')
