"""괴물쥐 — 굴의 네 발 짐승(11-18 사용자: *"두더지랑 괴물쥐 만들어보자"* · *"둘 다 네발 짐승"*).

큰 개만 한 쥐:
  - 몸 — 엉덩이가 높이 솟아 등이 굽었다 · 가슴은 낮고 좁다.
  - 머리 — 길게 좁아지는 주둥이 · 분홍 코 · 누런 앞니 둘(위아래) · 붉은 구슬눈 · 크고 얇은 둥근 귀(속이 분홍 · 왼귀는 찢겼다) · 수염.
  - 등줄기 — 목덜미에서 엉덩이까지 솟은 털 가시(괴물 · 앞이 길고 뒤로 짧아진다).
  - 손발 — 분홍 맨살 · 앞발은 가는 손가락 넷 · 뒷발은 길고 납작하다 · 검은 발톱.
  - 꼬리 — 길고 굵은 맨살 꼬리 · 마디 고리 · 땅에 끌며 옆으로 휜다.
  - 털 — 더러운 잿빛 갈색 · 배 · 턱 밑은 밝다 · 등 가시는 짙다.
조각 = (거리 함수, 색, 부위) — 부위는 게임 네발 관절(`Body` · `Head` · `Tail` · `LegFL/FR/BL/BR`).
실행: python rat.py renders/rat_v1"""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))
import sys
import numpy as np
from brute_preview import rbox
from quad_preview import (mouth_path, mouth_cut, mouth_inner, eye_paint, surface_along)
from quad_preview import (V, length, mirror, smin, smax, ell, sph, seg, UP, nrm, far, union, sunion, sstep, mixc, flat, frame, oell,
                          tuft, chain, chain_t, sheet)

# ── 색 ──
FUR = (0.38, 0.33, 0.29); FUR_D = (0.22, 0.19, 0.18); FUR_L = (0.62, 0.56, 0.48)
SKIN = (0.78, 0.56, 0.54); SKIN_D = (0.60, 0.40, 0.40); NOSE = (0.86, 0.50, 0.52)
TOOTH = (0.86, 0.70, 0.32); EYE = (0.78, 0.08, 0.06); DARK = (0.04, 0.03, 0.03); CLAW = (0.10, 0.08, 0.08); EAR_IN = (0.84, 0.56, 0.56)

# ── 몸 — 굽은 등 ──
def _torso(p):
    d = ell(p, V(0, 0.42, -0.06), V(0.24, 0.26, 0.44))
    d = smin(d, ell(p, V(0, 0.50, -0.30), V(0.28, 0.30, 0.30)), 0.12)            # 솟은 엉덩이
    d = smin(d, ell(p, V(0, 0.38, 0.22), V(0.20, 0.22, 0.24)), 0.10)             # 낮은 가슴
    return smax(d, 0.14 - p[..., 1], 0.05)
def body(p): return _torso(p)
def body_col(p):
    c = mixc(FUR, FUR_L, sstep(0.32, 0.20, p[..., 1]) * sstep(-0.35, -0.10, p[..., 2]))
    ridge = sstep(0.13, 0.04, np.abs(p[..., 0])) * sstep(-0.30, -0.18, p[..., 2]) * sstep(0.42, 0.32, p[..., 2]) * sstep(0.55, 0.62, p[..., 1])
    return mixc(c, FUR_D, ridge * 0.85)                                         # 가시가 솟는 등줄기는 짙다

# ── 머리 ──
HEAD_C = V(0, 0.48, 0.46)
TIP = V(0, 0.40, 0.80)                    # 주둥이 끝(코 뒤)
def head(p): return smax(_head0(p), -mouth_cut(p, MOUTH, 0.0075), 0.004)
def _head0(p):
    d = ell(p, HEAD_C, V(0.17, 0.16, 0.19))
    d = smin(d, seg(p, HEAD_C + V(0, -0.01, 0.06), TIP, 0.130, 0.055), 0.07)      # 주둥이
    d = smin(d, ell(p, V(0, 0.38, 0.56), V(0.10, 0.07, 0.14)), 0.05)              # 턱
    d = smin(d, ell(mirror(p), V(0.09, 0.44, 0.56), V(0.07, 0.07, 0.10)), 0.05)   # 볼(수염 자리)
    return smin(d, seg(p, HEAD_C + V(0, -0.06, -0.10), V(0, 0.40, 0.20), 0.13, 0.16), 0.06)   # 목
def head_col(p): return mixc(FUR, FUR_L, sstep(0.42, 0.34, p[..., 1]) * sstep(0.40, 0.55, p[..., 2]))
def nose(p): return smax(_nose0(p), -mouth_cut(p, MOUTH, 0.0075), 0.004)
def _nose0(p):
    m = mirror(p); d = ell(p, TIP + V(0, 0.010, 0.040), V(0.038, 0.030, 0.028))
    return smax(d, -ell(m, TIP + V(0.014, 0.004, 0.066), V(0.008, 0.010, 0.010)), 0.004)   # 콧구멍
# 입 — v13(사용자: *"밑에서 봤을 때 입이 대충 만들어진 느낌"* — v12는 옆에 어두운 납작 조각이었다): 코 밑 인중이 내려와 갈라지고
# 양옆 · 뒤로 가는 '人'자 홈을 살에 **파낸다** · 홈 바닥만 어둡다 · 앞니는 갈라진 자리에서 나온다.
def _muzzle(p): return np.minimum(_head0(p), _nose0(p))
def _axis(z):
    a, b = HEAD_C + V(0, -0.01, 0.06), TIP
    t = np.clip((z - a[2]) / (b[2] - a[2]), 0, 1); return a + (b - a) * t
MOUTH = mouth_path(_muzzle, _axis, TIP + V(0, 0.010, 0.040), 0.790, [(0.765, 0.40), (0.720, 0.70), (0.660, 0.90)])
SPLIT = MOUTH[0][1]
def teeth(p):
    """누런 앞니 둘 — v7(사용자: *"너무 길고 두껍고 · 뒤로 휘어야 · 너무 네모나서 이빨 같지 않다"*):
    짧고 가늘게 · 뿌리에서 끝으로 좁아지는 둥근 끌(앞뒤로 납작) · 아래로 갈수록 **뒤(목 쪽)로** 휜다 · 두 이 사이에 틈."""
    m = mirror(p)
    root = V(0.013, SPLIT[1] + 0.017, SPLIT[2] + 0.002); L = 0.075                                  # 뿌리는 윗입술 안 · 보이는 길이 약 5cm
    q = m - root
    t = np.clip(-q[..., 1] / L, 0, 1)
    q = np.stack([q[..., 0], q[..., 1], q[..., 2] + 0.030 * t * t], -1)          # 끝으로 갈수록 뒤(-z)로 휜다 → 펴 놓고 잰다
    d = seg(flat(q, V(0, 0, 1), 0.55), V(0, 0, 0), V(0, -L, 0), 0.0125, 0.0085) * 0.55   # 앞뒤로 납작한 둥근 끌
    d = smax(d, -(m[..., 0] - 0.0030), 0.002)                                  # 두 이 사이 틈
    return smax(d, (root[1] - L + 0.004) - m[..., 1] - 0.6 * np.abs(q[..., 0]) * 0, 0.003)   # 끝은 곧게 잘린 날
def mouth(p): return mouth_inner(p, MOUTH, 0.004, 0.006)
EYE_C = V(0.115, 0.535, 0.570)
def eyes(p): return sph(mirror(p), EYE_C, 0.034)
eye_col = eye_paint(EYE, EYE_C, V(0.75, 0.25, 0.62), 0.034, spots=((0.75, 0.45, -0.35, 0.36), (0.9, -0.3, 0.3, 0.17)))   # 반짝임은 칠한다(v13)
# 귀 — v11 새로 빚었다(사용자: *"원래 귀에 뭘 추가하지 말고 귀 자체를 새로 만들어"* · 사진 둘 `_refs/2026-11-18_rat/`).
# **얇은 판 한 장을 굽힌다** — 판의 모양(u 가로 · v 세로)은 아래가 좁고 위가 넓은 달걀꼴, 앞으로 오목하게 휘고(가장자리가 앞으로 온다),
# 윗부분은 같은 판이 굴대(반지름 ROLL_R)에 감기듯 **앞으로 말려 넘어간다** — 감기는 길이가 꼭대기에서 가장 길고 양옆으로 0이 되어 펴진다.
# 거리는 판 좌표로 되돌려 잰다(판이 펴진 자리 (u, v_s)와 판 두께 방향 거리) — 덧붙인 조각이 없어 이음새가 없다.
#   색 — 판 앞면(오목한 쪽 · 말린 데서는 안쪽)은 분홍, 뒷면은 짙은 살 · 뒷면 아래 뿌리는 털 · 왼귀(+x)는 가장자리가 찢겼다.
EAR_C = V(0.135, 0.640, 0.360); EAR_N = nrm(V(0.82, 0.10, 0.56))
EAR_R = frame(EAR_N, nrm(V(-0.15, 1, -0.40)))
EAR_W, EAR_H = 0.080, 0.086                     # 판 반폭 · 반높이(판 가운데 기준)
CUP = 4.0                                       # 가로 오목함
ROLL_R = 0.014; ROLL_L = 0.030                  # 말림 굴대 반지름 · 꼭대기에서 감기는 판 길이(약 200°)
EAR_TH = 0.0032
def _ear_q(p): return (mirror(p) - EAR_C) @ EAR_R
def _outline(u, v):
    """판 모양 — 달걀꼴(위가 넓다 · 아래가 좁다). 0 바깥 · 음수 안."""
    vn = v / EAR_H; w = EAR_W * (1 + 0.16 * np.clip(vn, -1, 1))
    return (length(np.stack([u / w, vn], -1)) - 1) * min(EAR_W, EAR_H) * 0.9
def _vtop(u):
    a = np.clip(u / (EAR_W * 1.16), -1, 1); return EAR_H * np.sqrt(1 - a * a)
def _sheet(q):
    """판 좌표로 되돌린다 → (판 두께 방향 거리, 펴진 자리 u, v_s, 앞면인가)."""
    u, v = q[..., 0], q[..., 1]
    z = q[..., 2] - CUP * u * u                                                  # 오목함을 편다
    L = ROLL_L * np.clip(1 - (u / (EAR_W * 1.05)) ** 2, 0, 1) ** 1.5             # 감기는 길이 — 꼭대기에서 길고 옆으로 0
    v0 = _vtop(u) - L                                                            # 말리기 시작하는 줄
    dv = v - v0; dz = z - ROLL_R
    r = length(np.stack([dv, dz], -1)); th = np.arctan2(dv, -dz)                 # 0 = 시작 · 90° = 위 · 180° = 앞으로 넘어와 아래
    th = np.where(th < -np.pi * 0.5, th + 2 * np.pi, th)
    in_roll = (dv > 0) | ((z > ROLL_R) & (th < L / ROLL_R + 0.3))
    in_roll = in_roll & (th >= 0) & (th <= np.maximum(L / ROLL_R, 1e-3))
    thick = np.where(in_roll, np.abs(r - ROLL_R), np.abs(z))
    vs = np.where(in_roll, v0 + ROLL_R * th, v)
    front = np.where(in_roll, r < ROLL_R, z > 0)
    past = (dv > 0) & ~in_roll                                                   # 감긴 끝을 넘은 자리 — 끝점까지
    tm = L / ROLL_R
    ex = v0 + ROLL_R * np.sin(tm); ez = ROLL_R - ROLL_R * np.cos(tm)
    endd = length(np.stack([v - ex, z - ez], -1))
    thick = np.where(past, endd, thick); vs = np.where(past, v0 + L, vs)
    return thick, u, vs, front
def ears(p):
    m = mirror(p); q = _ear_q(p)
    thick, u, vs, _ = _sheet(q)
    th = EAR_TH * (0.75 + 0.5 * np.clip(-vs / EAR_H, 0, 1))                       # 아래(뿌리 쪽)가 조금 두껍다
    d = np.maximum(thick - th, _outline(u, vs)) * 0.7
    root = ell(m, EAR_C + EAR_R @ V(0, -0.078, -0.010), V(0.030, 0.026, 0.022))  # 뿌리 — 머리 살로 녹는다
    d = smin(d, root, 0.020)
    notch = sph(p, EAR_C + EAR_R @ V(0.086, 0.020, 0), 0.020)                       # 왼귀(+x)만 찢겼다
    return smax(d, -np.where(p[..., 0] > 0, notch, 9.0), 0.003)
def ears_col(p):
    q = _ear_q(p); _, u, vs, front = _sheet(q)
    edge = _outline(u, vs)
    inner = front * sstep(-0.004, -0.012, edge)
    c = mixc(SKIN_D, EAR_IN, inner.astype(np.float32))
    c = mixc(c, (0.68, 0.44, 0.44), inner * sstep(-0.030, -0.050, edge) * 0.5)     # 깊은 가운데는 조금 짙다
    base_fur = sstep(-0.045, -0.075, vs) * (1 - 0.6 * front)                       # 뿌리 · 뒷면 아래는 털
    return mixc(c, FUR, np.clip(base_fur, 0, 1))
def whiskers(p):
    m = mirror(p); d = far(p)
    for i, (dy, dz) in enumerate(((0.030, 0.0), (0.010, -0.02), (-0.012, -0.01))):
        a = V(0.055, 0.430 + dy, 0.700 + dz); b = a + nrm(V(0.80, dy * 3.0 - 0.10, -0.25)) * 0.20
        d = np.minimum(d, seg(m, a, b, 0.006, 0.003))
    return d

# ── 등 가시 — 목덜미에서 엉덩이까지 ──
# v3 — 몸 표면에 뿌리를 박는다(사용자: *"등줄기가 딱 안 달라붙어 있고 좀 떠 있는 느낌"* — v2는 높이를 식으로 짐작해 등 위에 떠 있었다):
# 자리마다 몸 거리장을 위에서 아래로 재 표면 높이를 찾고, 뿌리는 그보다 3cm 안 · 가시는 표면을 따라 뒤로 눕는다 · 등줄기 털도 짙게 칠한다.
def surface_y(f, x, z, lo=0.2, hi=1.4):
    a, b = lo, hi
    for _ in range(40):
        c = 0.5 * (a + b)
        if f(V(x, c, z)[None, :])[0] < 0: a = c
        else: b = c
    return 0.5 * (a + b)
def _spines():
    out = []
    for i in range(9):
        t = i / 8.0
        z = 0.30 - 0.50 * t                     # v5 — 엉덩이 꼭대기 앞에서 끝난다(사용자: *"등털이 너무 길게 내려와 · 아까 정도 길이"*)
        ln = 0.16 - 0.06 * t
        for sx in (-1, 0, 1):
            if sx and i % 2: continue
            x = 0.055 * sx; y = surface_y(_torso, x, z)
            dz = 0.02; slope = (surface_y(_torso, x, z - dz) - surface_y(_torso, x, z + dz)) / (2 * dz)   # 뒤로 갈수록 오르면 +
            a = V(x, y - 0.030, z); dr = nrm(V(0.30 * sx, 0.95 + 0.6 * slope, -0.75))   # v4 — 더 선다(v3는 누워 털처럼 보였다)
            out.append((a, dr, ln * (0.75 if sx else 1.0) + 0.07, 0.046))
    return out
SPINES = _spines()
def spines(p):
    d = far(p)
    for a, dr, ln, r in SPINES: d = smin(d, tuft(p, a, dr, ln, r, k=0.55), 0.015)
    return d

# ── 앞다리 — 가는 팔 · 분홍 손 ──
F_SH = V(0.15, 0.36, 0.24); F_EL = V(0.17, 0.19, 0.20); F_WR = V(0.16, 0.06, 0.30)
def front_legs(p):
    m = mirror(p)
    return smin(seg(m, F_SH, F_EL, 0.075, 0.055), seg(m, F_EL, F_WR, 0.050, 0.038), 0.03)
def front_hands(p):
    m = mirror(p); d = ell(m, F_WR + V(0, -0.025, 0.035), V(0.050, 0.025, 0.055))
    for i in range(4):
        u = (i - 1.5) / 1.5; a = F_WR + V(u * 0.030, -0.035, 0.070); b = a + V(u * 0.020, -0.012, 0.050)
        d = smin(d, seg(m, a, b, 0.014, 0.010), 0.012)
    return d
def front_claws(p):
    m = mirror(p); d = far(p)
    for i in range(4):
        u = (i - 1.5) / 1.5; a = F_WR + V(u * 0.030 + u * 0.020, -0.045, 0.118); d = np.minimum(d, seg(m, a, a + V(0, -0.012, 0.030), 0.009, 0.002))
    return d

# ── 뒷다리 — 굵은 넓적다리 · 길고 납작한 발 ──
B_HIP = V(0.20, 0.42, -0.30); B_KN = V(0.22, 0.25, -0.18); B_HEEL = V(0.21, 0.07, -0.40)
def back_legs(p):
    m = mirror(p)
    d = ell(m, B_HIP + V(0, -0.04, 0.02), V(0.10, 0.17, 0.18))
    return smin(d, smin(seg(m, B_HIP, B_KN, 0.090, 0.065), seg(m, B_KN, B_HEEL, 0.060, 0.040), 0.03), 0.05)
def back_feet(p):
    m = mirror(p); d = seg(m, B_HEEL + V(0, -0.02, 0), B_HEEL + V(0, -0.035, 0.20), 0.040, 0.034)
    for i in range(4):
        u = (i - 1.5) / 1.5; a = B_HEEL + V(u * 0.030, -0.045, 0.21); b = a + V(u * 0.025, -0.005, 0.060)
        d = smin(d, seg(m, a, b, 0.015, 0.011), 0.012)
    return d
def back_claws(p):
    m = mirror(p); d = far(p)
    for i in range(4):
        u = (i - 1.5) / 1.5; a = B_HEEL + V(u * 0.030 + u * 0.025, -0.050, 0.268); d = np.minimum(d, seg(m, a, a + V(0, -0.012, 0.030), 0.010, 0.002))
    return d

# ── 꼬리 — 길고 굵은 맨살 ──
# v3 — 애니메이션을 위해(사용자: *"꼬리에 나중에 애니메이션이 들어갈 거 같은데 알아서"*): 쉬는 자세는 **좌우 대칭**(엉덩이에서 뒤 · 아래로 내려와
# 땅을 따라 곧게 뻗고 끝이 살짝 든다) — 옆으로 휘는 것은 애니메이션 몫(마디마다 좌우로 흔든다). 마디 점이 곧 꼬리 뼈 사슬(`TAIL_BONES` — 굽기에서
# 정점마다 가까운 두 마디에 무게를 나눈다 · 지금 네발 관절의 `Tail` 하나를 이 사슬로 늘린다).
TAIL = [V(0, 0.48, -0.56), V(0, 0.36, -0.73), V(0, 0.19, -0.88), V(0, 0.07, -1.04), V(0, 0.05, -1.24), V(0, 0.06, -1.44), V(0, 0.11, -1.62)]
TAIL_R = [0.075, 0.064, 0.052, 0.042, 0.032, 0.022, 0.010]
TAIL_BONES = TAIL[:-1]
def tail(p): return chain(p, TAIL, TAIL_R, 0.02)
def tail_col(p):
    t = chain_t(p, TAIL); ring = (np.sin(t * 2 * np.pi * 26) > 0.55).astype(np.float32)
    return mixc(SKIN, SKIN_D, ring * 0.8)

def pieces(head_only=False):
    H = [(head, head_col, 'Head'), (nose, NOSE, 'Head'), (teeth, TOOTH, 'Head'), (mouth, DARK, 'Head'), (eyes, eye_col, 'Head'),
         (ears, ears_col, 'Head'), (whiskers, FUR_D, 'Head')]
    if head_only: return H
    return H + [(body, body_col, 'Body'), (spines, FUR_D, 'Body'),
                (front_legs, FUR, 'LegF'), (front_hands, SKIN, 'LegF'), (front_claws, CLAW, 'LegF'),
                (back_legs, FUR, 'LegB'), (back_feet, SKIN, 'LegB'), (back_claws, CLAW, 'LegB'), (tail, tail_col, 'Tail')]

VIEW = (0.42, 1.95)
CLOSE = ((0, 420, 0), (40, 460, 0), (90, 460, 0), (30, 460, 45), (20, 460, -40))   # 마지막 — 밑에서 올려다본다
CLOSE_C = V(0, 0.50, 0.56); CLOSE_SPAN = 0.62
SCALE_Z = -1.95; SCALE_SPAN = 2.45

if __name__ == '__main__':
    sheet('rat', sys.argv[1] if len(sys.argv) > 1 else 'renders/rat_v13', 'rat v13')
