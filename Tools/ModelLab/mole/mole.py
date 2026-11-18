"""두더지 — 굴의 네 발 짐승(11-18 사용자: *"두더지랑 괴물쥐 만들어보자"* · *"둘 다 네발 짐승"*).

소만 한 거대 두더지(v4 — 사용자: *"별코 말고 일반 두더지 · 손발 비율 · 앞발이 땅을 딛게 · 귀여운 동그란 눈 · 소 정도 크기"*):
  - 몸 — 목 없는 술통(뒤가 조금 더 굵다) · 머리는 몸에서 원뿔로 좁아져 주둥이가 된다.
  - 코 — 분홍 맨살 주둥이 끝의 동그스름한 코(콧구멍 둘이 앞 · 아래를 본다).
  - 앞발 — 어깨 → 팔꿈치 → 손목으로 내려와 **넓적한 손바닥이 땅을 딛는다**(발가락 다섯 · 상아색 발톱이 앞으로) · 뒷발과 크기를 맞췄다(앞이 조금 크다).
  - 눈 — 동그란 검은 눈 + 반짝임(귀엽게) · 몽당 꼬리.
  - 크기 — 단위 몸(어깨 0.85)을 `SIZE`배로 키운다: 어깨 높이 약 1.4m · 몸길이 약 2.8m.
  - 털 — 우단 같은 짙은 잿빛 · 배는 조금 밝다.
조각 = (거리 함수, 색, 부위) — 부위는 게임 네발 관절(`Body` · `Head` · `Tail` · `LegFL/FR/BL/BR`).
실행: python mole.py renders/mole_v1"""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))
import sys
import numpy as np
from quad_preview import (V, length, mirror, smin, smax, ell, sph, seg, UP, nrm, far, union, sunion, sstep, mixc, flat, frame, oell,
                          tuft, chain, sheet, surface_along, fluff, mouth_path, mouth_cut, mouth_inner, eye_paint)

# ── 색 ──
FUR = (0.30, 0.27, 0.30); FUR_L = (0.46, 0.42, 0.43)                      # v7 — 조금 밝고 따뜻하게
PINK = (0.88, 0.58, 0.58); PINK_D = (0.66, 0.36, 0.38)
CLAW = (0.90, 0.85, 0.72); EYE = (0.03, 0.03, 0.04); MOUTH = (0.30, 0.08, 0.10); TOOTH = (0.94, 0.92, 0.84)

# ── 몸 ──
BODY_C = V(0, 0.52, -0.12)
def _body0(p):
    d = ell(p, BODY_C, V(0.36, 0.34, 0.50))
    d = smin(d, ell(p, V(0, 0.50, -0.30), V(0.38, 0.34, 0.36)), 0.10)          # 굵은 엉덩이
    d = smin(d, ell(p, V(0, 0.48, 0.14), V(0.32, 0.30, 0.30)), 0.10)           # 어깨
    return smax(d, 0.20 - p[..., 1], 0.06)                                    # 배가 다리 위에 뜬다
def body_col(p): return mixc(FUR, FUR_L, sstep(0.36, 0.22, p[..., 1]))
def limb_col(p): return mixc(FUR, FUR_L, sstep(0.26, 0.12, p[..., 1]) * 0.6)

# ── 머리 — 몸에서 원뿔로 좁아진다 ──
HEAD_C = V(0, 0.52, 0.50)
SNOUT = [V(0, 0.48, 0.70), V(0, 0.45, 0.80), V(0, 0.42, 0.87)]
def _head0(p):
    d = ell(p, HEAD_C, V(0.28, 0.26, 0.26))
    d = smin(d, seg(p, HEAD_C + V(0, -0.02, 0.08), SNOUT[0], 0.21, 0.15), 0.08)
    return smin(d, ell(p, V(0, 0.39, 0.56), V(0.20, 0.10, 0.16)), 0.06)      # 턱 밑
def _snout0(p):     # 분홍 맨살 주둥이
    return smin(seg(p, SNOUT[0] + V(0, 0.005, -0.03), SNOUT[1], 0.130, 0.100), seg(p, SNOUT[1], SNOUT[2], 0.100, 0.068), 0.03)
def snout_col(p): return mixc(PINK_D, PINK, sstep(0.70, 0.84, p[..., 2]))
NOSE_C = SNOUT[2] + V(0, 0.006, 0.036)
NOSTRIL_C = NOSE_C + V(0.020, -0.004, 0.050)
def _nostril_holes(p): return ell(mirror(p), NOSTRIL_C, V(0.010, 0.013, 0.018))
def nose(p): return smax(_nose0(p), -mouth_cut(p, MOUTH_P, 0.008), 0.004)
def _nose0(p):      # 동그스름한 분홍 코끝 — 콧구멍 둘을 **파낸다**(v9 — 사용자: *"콧구멍이 동그라미로 나와 있는 게 아니라 살짝 파져 있어야지"*)
    # v10 — 작게 · 앞으로 조금 길쭉하게(사용자: *"코를 조금만 줄여도 · 둥그런 게 약간 돼지코 같아서"* — v9는 납작한 원판 앞면이었다)
    return smax(ell(p, NOSE_C, V(0.058, 0.046, 0.058)), -_nostril_holes(p), 0.005)
def nose_col(p):    # 구멍 안은 짙게
    return mixc(PINK, (0.40, 0.16, 0.20), sstep(0.006, -0.002, _nostril_holes(p)))
# 입 — v11(사용자: *"밑에서 봤을 때 입이 대충 만들어진 느낌"* — v10은 주둥이 밑에 어두운 납작 조각을 붙였다): 코 밑에서 인중이 내려와
# 갈라지고 양옆 · 뒤로 가는 '人'자 홈을 살에 **파낸다** · 홈 바닥만 어둡다 · 앞니 둘은 갈라진 자리에서 나온다.
def _snout_all(p): return np.minimum(_snout0(p), _nose0(p))
def _axis(z):
    t = np.clip((z - SNOUT[0][2]) / (SNOUT[2][2] - SNOUT[0][2]), 0, 1); return SNOUT[0] + (SNOUT[2] - SNOUT[0]) * t
MOUTH_P = mouth_path(_snout_all, _axis, NOSE_C, 0.880, [(0.860, 0.35), (0.825, 0.62), (0.785, 0.82)])
SPLIT = MOUTH_P[0][1]
def snout(p): return smax(_snout0(p), -mouth_cut(p, MOUTH_P, 0.008), 0.004)
def mouth(p): return mouth_inner(p, MOUTH_P, 0.0045, 0.0065)
def teeth(p):
    m = mirror(p); a = SPLIT + V(0.010, 0.016, -0.004)
    return seg(m, a, a + V(0, -0.034, 0.002), 0.0085, 0.0060)
EYE_C = V(0.198, 0.590, 0.640); EYE_R = 0.044; EYE_N = nrm(V(0.75, 0.15, 0.65))
def eyes(p): return sph(mirror(p), EYE_C, EYE_R)
# 반짝임은 **칠한다**(v11 — 사용자: *"하얀 점을 꼭 동그라미로 넣어서 튀어나오게 하는 거밖에 답이 없는 거야?"*) — 눈알 정점 색(큰 점 + 작은 점).
eye_col = eye_paint(EYE, EYE_C, EYE_N, EYE_R)
def head_col(p): return mixc(FUR, FUR_L, sstep(0.45, 0.35, p[..., 1]))

# ── 앞발 — 어깨 → 팔꿈치 → 손목 · 손바닥이 땅을 딛는다 ──
SH = V(0.22, 0.42, 0.24); EL = V(0.28, 0.24, 0.28); WR = V(0.28, 0.09, 0.34)
PAW_F = V(0.29, 0.040, 0.40)                          # 앞 손바닥 가운데(땅 위)
def _arms0(p):
    m = mirror(p)
    d = smin(ell(m, SH + V(-0.02, -0.02, 0), V(0.13, 0.15, 0.14)), seg(m, SH, EL, 0.115, 0.100), 0.05)   # 굵은 어깨 · 위팔(파는 짐승)
    return smin(d, seg(m, EL, WR, 0.100, 0.080), 0.04)
def paw(m, c, rx, rz, n_toe, toe_r):
    """땅을 딛는 넓적한 발 — 바닥이 평평하고 발가락이 앞으로 부채꼴."""
    d = smax(ell(m, c, V(rx, 0.050, rz)), -m[..., 1] + 0.006, 0.01)
    for i in range(n_toe):
        u = (i - (n_toe - 1) / 2) / ((n_toe - 1) / 2)
        a = c + V(u * rx * 0.78, -0.004, rz * 0.62 - 0.03 * u * u); b = a + V(u * 0.025, -0.012, 0.055)
        d = smin(d, seg(m, a, b, toe_r, toe_r * 0.82), 0.02)
    return d
def paw_claws(m, c, rx, rz, n_toe, ln, r):
    d = far(m)
    for i in range(n_toe):
        u = (i - (n_toe - 1) / 2) / ((n_toe - 1) / 2)
        a = c + V(u * rx * 0.78 + u * 0.030, 0.006, rz * 0.62 + 0.050 - 0.03 * u * u)
        mid = a + V(u * 0.010, 0.004, ln * 0.55); tip = a + V(u * 0.016, -0.030, ln)
        d = np.minimum(d, smin(seg(m, a, mid, r, r * 0.72), seg(m, mid, tip, r * 0.72, 0.004), 0.01))
    return d
def hands(p):
    m = mirror(p); return smin(paw(m, PAW_F, 0.115, 0.120, 5, 0.034), seg(m, WR, PAW_F + V(0, 0.02, -0.04), 0.080, 0.075), 0.04)
def claws_front(p): return paw_claws(mirror(p), PAW_F, 0.115, 0.120, 5, 0.110, 0.028)

# ── 뒷다리 — 넓적다리 → 무릎 → 뒤꿈치 · 발바닥이 땅을 딛는다(앞발보다 조금 작다) ──
HIP = V(0.22, 0.40, -0.36); KN = V(0.26, 0.22, -0.28); HEEL = V(0.26, 0.08, -0.44)
PAW_B = V(0.26, 0.040, -0.36)
def _legs0(p):
    m = mirror(p)
    d = smin(ell(m, HIP + V(0, -0.02, 0.02), V(0.13, 0.17, 0.17)), seg(m, HIP, KN, 0.110, 0.090), 0.05)
    return smin(d, seg(m, KN, HEEL, 0.085, 0.070), 0.04)
def feet(p):
    m = mirror(p); return smin(paw(m, PAW_B, 0.095, 0.110, 5, 0.026), seg(m, HEEL, PAW_B + V(0, 0.01, -0.05), 0.070, 0.060), 0.04)
def claws_back(p): return paw_claws(mirror(p), PAW_B, 0.095, 0.110, 5, 0.070, 0.020)

# ── 꼬리 — 몽당 ──
TAIL = [V(0, 0.54, -0.62), V(0, 0.60, -0.76), V(0, 0.68, -0.84)]
def _tail0(p): return chain(p, TAIL, [0.060, 0.048, 0.022], 0.02)

# ── 복슬복슬(v6 — 사용자: *"두더지는 뭔가 복슬복슬함이 없네"*) ──
# v7 — *"안 귀엽고 더러워 보여"*: 표면 울퉁불퉁(`fluff`)은 얼룩진 그늘을 만들어 걷었다. 살은 매끈하게 두고, **둥글고 통통한 털 뭉치**만
# 이음새 · 가장자리에 몇 개(목 둘레 · 볼 · 정수리 · 배 아래 · 엉덩이 · 손목 · 발목 · 꼬리 끝) — 인형처럼 몽글몽글한 윤곽.
# 뭉치는 그 부위 살에 녹여 붙인다(조각이 나뉘면 안쪽 선이 생긴다).
FUZZ = 0.0
def _tufts_on(f, specs):
    out = []
    for o, out_dir, dr, ln, r, n in specs:
        a = surface_along(f, o, out_dir) - nrm(out_dir) * 0.012
        out.append((a, nrm(dr), ln, r, n))
    return out
def _body_tufts():
    sp = []
    for i in range(7):                                      # 배 아래 술 — 옆구리 아래 가장자리에서 아래 · 바깥으로
        z = -0.46 + 0.74 * i / 6
        sp.append((V(0, 0.42, z), V(1, -0.75, 0), V(0.35, -1, 0.05 * np.sin(i * 1.7)), 0.075, 0.038, V(0, 0, 1)))
    for i in range(5):                                      # 엉덩이 둘레
        a = np.radians(-70 + 140 * i / 4); o = V(np.sin(a) * 0.9, np.cos(a), -0.5)
        sp.append((V(0, 0.52, -0.45), o, o * 0.4 + V(0, -0.4, -0.8), 0.070, 0.036, None))
    return _tufts_on(_body0, sp)
def _head_tufts():
    sp = []
    for k, (dy, dz) in enumerate(((0.02, 0.0), (-0.06, 0.03), (-0.12, 0.0))):   # 볼 술
        sp.append((HEAD_C + V(0, dy, dz), V(1, -0.2, 0.15), V(0.6, -0.5, -0.6), 0.060, 0.032, V(0, 0, 1)))
    sp.append((HEAD_C + V(0, 0.02, -0.04), V(0, 1, 0.1), V(0, 0.6, -0.8), 0.065, 0.036, V(1, 0, 0)))   # 정수리 술
    return _tufts_on(_head0, sp)
def _cuff(c, rad, y_drop):                                  # 손목 · 발목 털 — 다리를 둘러 아래로 늘어진다
    out = []
    for i in range(6):
        a = np.radians(60 * i + 15); o = V(np.sin(a), 0, np.cos(a))
        out.append((c + o * rad, nrm(o * 0.45 + V(0, -1, 0)), y_drop, 0.034, nrm(np.cross(o, UP))))
    return out
# 목 — v8(사용자: *"목에 털만 자연스럽게 — 지금은 좀 이상하네"*): v7은 머리 · 몸이 다른 조각이라 이음새에 선이 그어지고, 그 위에 털 술이
# 고리로 사방에 뻗어 목도리 같았다. 이제 머리와 몸을 **한 털 살**로 녹이고(`_fur0` — 목이 굵게 이어진다), 이음새 위에 **뒤로 누운 넓적한 털 결**이
# 두 줄로 겹쳐 어깨 쪽으로 흐른다(판이 살에 붙어 눕는다 — 서지 않는다). 머리 · 몸 뼈 무게는 굽기에서 자리(z)로 가른다(게임 네발 관절과 같은 셈).
def _fur0(p): return smin(_head0(p), _body0(p), 0.10)
def _neck_locks():
    out = []
    for row, (z, ln, r) in enumerate(((0.34, 0.13, 0.055), (0.24, 0.12, 0.052))):
        n = 5 if row == 0 else 6
        for i in range(n):
            a = np.radians(-72 + 144 * (i + 0.5 * row) / (n - 1 + 0.5 * row))
            o = V(np.sin(a), np.cos(a), 0.15)
            base = surface_along(_fur0, V(0, 0.48, z), o) - nrm(o) * 0.010
            dr = nrm(V(0, 0, -1) * 0.92 + nrm(o) * 0.10 + V(0, -0.15 * abs(np.sin(a)), 0))   # 뒤로 · 살결 따라
            out.append((base, dr, ln, r, nrm(o)))                                            # 납작한 면이 바깥을 본다 → 살에 붙어 눕는다
    return out
NECK_T = _neck_locks()
BODY_T = _body_tufts(); HEAD_T = _head_tufts()
def _tset(p, T, k=0.025):
    m = mirror(p); d = far(p)
    for a, dr, ln, r, n in T: d = smin(d, tuft(m, a, dr, ln * 0.70, r * 1.45, n=n, k=0.80), 0.035)
    return d
def _locks(p):
    m = mirror(p); d = far(p)
    for a, dr, ln, r, n in NECK_T: d = smin(d, tuft(m, a, dr, ln, r, n=n, k=0.45), 0.02)
    return d
def fur(p):   return smin(smin(_fur0(p), _tset(p, BODY_T + HEAD_T), 0.025), _locks(p), 0.018)   # 머리 + 몸 한 털 살
def body(p):  return _body0(p)
def fur_col(p):
    k = sstep(0.22, 0.36, p[..., 2])                                           # 앞(머리)과 뒤(몸)의 배 밝기 선을 잇는다
    return mixc(body_col(p), head_col(p), k)
def arms(p):  return smin(_arms0(p) - FUZZ * 0.7 * fluff(p * 1.3), _tset(p, _cuff(WR + V(0, 0.05, 0), 0.075, 0.075)), 0.02)
def legs(p):  return smin(_legs0(p) - FUZZ * 0.7 * fluff(p * 1.3), _tset(p, _cuff(HEEL + V(0, 0.05, 0.02), 0.065, 0.065)), 0.02)
TAIL_T = [(TAIL[-1] + V(0, -0.005, 0), nrm(V(0.5 * np.cos(a), 1, -0.5 * abs(np.sin(a)) - 0.3)), 0.060, 0.022, None) for a in np.radians([0, 120, 240])]
def tail(p): return smin(_tail0(p), _tset(p, TAIL_T), 0.015)

def _pieces(head_only=False):
    H = [(snout, snout_col, 'Head'), (nose, nose_col, 'Head'), (mouth, MOUTH, 'Head'),
         (teeth, TOOTH, 'Head'), (eyes, eye_col, 'Head')]
    if head_only:
        cut = HEAD_C[2] - 0.06
        return H + [(lambda p: np.maximum(fur(p), cut - p[..., 2]), fur_col, 'Body')]
    return H + [(fur, fur_col, 'Body'), (arms, limb_col, 'LegF'), (hands, PINK, 'LegF'), (claws_front, CLAW, 'LegF'),
                (legs, limb_col, 'LegB'), (feet, PINK, 'LegB'), (claws_back, CLAW, 'LegB'), (tail, lambda p: mixc(PINK_D, FUR, sstep(0.64, 0.70, p[..., 1])), 'Tail')]

# 크기 — 위 수치는 단위 몸(어깨 약 0.85m). 소만 하게 `SIZE`배(발이 원점이라 그대로 곱한다).
SIZE = 1.6
def pieces(head_only=False):
    S = SIZE
    return [((lambda f: lambda p: f(p / S) * S)(f), ((lambda g: lambda p: g(p / S))(c) if callable(c) else c), part)
            for f, c, part in _pieces(head_only)]

VIEW = (0.42 * SIZE, 1.55 * SIZE)       # 온몸 보기 — 가운데 높이 · 세로 폭
CLOSE = ((0, 420, 0), (40, 460, 0), (90, 460, 0), (30, 460, 45), (20, 460, -40))   # 마지막 — 밑에서 올려다본다
CLOSE_C = V(0, 0.49, 0.64) * SIZE; CLOSE_SPAN = 0.80 * SIZE
SCALE_Z = -2.20; SCALE_SPAN = 3.7         # 사람 치비를 두는 자리(옆 보기에서 꼬리 뒤) · 비교 그림 세로 폭

if __name__ == '__main__':
    sheet('mole', sys.argv[1] if len(sys.argv) > 1 else 'renders/mole_v11', 'mole v11')
