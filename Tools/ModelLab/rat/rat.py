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
# 털 빠진 맨살(v16 — 사용자: *"털이 빠졌으면 안으로 들어가 있어야지 왜 부풀어 있어"* — v15는 색만 칠해 볼록해 보였다):
# 그 자리를 털 두께만큼 **안으로 판다** · 둘레에 털이 끊긴 테가 살짝 선다 · 가장자리가 들쭉날쭉. 가시 뿌리는 파기 전 몸(`_torso`)에 박는다.
# v17 — 자리는 몸 겉에서 찾는다(v16은 중심이 겉에서 떨어져 작은 동그라미 구멍이 됐다) · 얕게 · 가장자리는 뜯긴 듯 들쭉날쭉 · 잿빛 맨살.
# v18 — 자리는 싸우다 물리고 긁히는 곳(사용자: *"머리나 어깨 · 옆구리 윗부분 · 다리 — 실제로 옆구리 아래를 공격받기보다는"*):
# 머리(귀 뒤 정수리 · 볼) · 어깨 둘 · 등 쪽 옆구리 윗부분 · 엉덩이 위 · 넓적다리 · 앞다리 위팔. 자리 표는 조각들이 다 선 뒤 `BALD_*`(파일 끝 쪽).
BALD_DEPTH, BALD_RIM = 0.005, 0.002
# **표식**(v20 — 사용자: *"쥐도 개체마다 다르게 · 3종류 · 흉터 — 반쯤 잘린 귀 · 이빨 자국"*) — 조각마다 목록:
#   ('bald', 자리, 반지름) 털 빠진 맨살 · ('scar', [겉의 점…], 반폭) 할퀸 흉터 홈(털 없는 맨살 띠) · ('pit', 자리, 반지름) 이빨 자국 구멍 하나.
# 셋 다 살을 판다(`dent`) — 칠만 하면 정점을 줄일 때 뭉개진다.
SCAR_COL = (0.64, 0.50, 0.48); PIT_COL = (0.30, 0.16, 0.16)
def _polyline_d(p, pts):
    d = np.full(p.shape[:-1], 9.0, np.float32)
    for a, b in zip(pts, pts[1:]):
        ab = b - a; h = np.clip(((p - a) * ab).sum(-1) / float((ab * ab).sum()), 0, 1)
        d = np.minimum(d, length(p - a - ab * h[..., None]))
    return d
def _marks(p, marks):
    """(맨살 몫 · 털 테 몫 · 테 안 그늘 · 흉터 몫 · 이빨 구멍 몫 · 파는 깊이)."""
    z = np.zeros(p.shape[:-1], np.float32)
    mask, rim, shade, scar, pit, depth = z.copy(), z.copy(), z.copy(), z.copy(), z.copy(), z.copy()
    for mk in marks:
        kind = mk[0]
        if kind == 'bald':
            _, cc, r = mk; q = p - cc
            jag = 0.016 * np.sin(q[..., 0] * 61 + q[..., 2] * 47) + 0.010 * np.sin(q[..., 1] * 113 + q[..., 2] * 89) + 0.006 * np.sin(q[..., 0] * 170 - q[..., 1] * 150)
            dist = length(q) + jag
            mask = np.maximum(mask, sstep(r + 0.006, r - 0.010, dist))
            rim = np.maximum(rim, np.exp(-((dist - (r + 0.010)) / 0.006) ** 2))
            shade = np.maximum(shade, np.exp(-((dist - (r - 0.006)) / 0.007) ** 2))
        elif kind == 'scar':
            _, pts, w = mk
            dd = _polyline_d(p, pts) + 0.0025 * np.sin(p[..., 0] * 140 + p[..., 1] * 90 + p[..., 2] * 110)
            scar = np.maximum(scar, sstep(w + 0.004, w - 0.001, dd))
            depth = np.maximum(depth, 0.0045 * sstep(w * 0.9, w * 0.2, dd))
            rim = np.maximum(rim, np.exp(-((dd - (w + 0.006)) / 0.004) ** 2) * 0.8)
        elif kind == 'pit':
            _, cc, r = mk; dd = length(p - cc)
            pit = np.maximum(pit, sstep(r * 1.05, r * 0.55, dd))
            scar = np.maximum(scar, sstep(r * 1.9, r * 1.3, dd))                  # 구멍 둘레의 아문 맨살
            depth = np.maximum(depth, 0.0055 * sstep(r, r * 0.2, dd))
    depth = np.maximum(depth, BALD_DEPTH * mask) - BALD_RIM * rim * (1 - np.maximum(mask, scar))
    return mask, rim, shade, scar, pit, depth
def dent(d, p, marks, k=1.0):
    """표식 자리를 판다 — 조각 거리장 d에 깊이를 더한다(`k` — 가는 다리는 얕게)."""
    if not marks: return d
    return (d + _marks(p, marks)[5] * k) * 0.8
def bald_paint(c, p, marks):
    """맨살 색 — 잿빛에 거무스름한 얼룩 · 패인 가장자리는 그늘 · 털 테는 조금 짙다 · 흉터는 희끄무레한 살 · 이빨 구멍은 검붉다."""
    if not marks: return c
    mask, rim, shade, scar, pit, _ = _marks(p, marks)
    blot = 0.5 + 0.5 * np.sin(p[..., 0] * 41 + 3 * np.sin(p[..., 2] * 37)) * np.sin(p[..., 1] * 47 + p[..., 2] * 23)
    skin = mixc((0.52, 0.44, 0.42), (0.42, 0.35, 0.34), blot * 0.7)
    skin = mixc(skin, (0.38, 0.31, 0.30), shade * 0.45)
    c = mixc(c, FUR_D, np.clip(rim * (1 - np.maximum(mask, scar)), 0, 1) * 0.35)
    c = mixc(c, skin, mask)
    c = mixc(c, SCAR_COL, scar)
    return mixc(c, PIT_COL, pit)
def body(p, L=None): return dent(_torso(p), p, (L or look())['body'])
def body_col(p, L=None):
    c = mixc(FUR, FUR_L, sstep(0.32, 0.20, p[..., 1]) * sstep(-0.35, -0.10, p[..., 2]))
    ridge = sstep(0.13, 0.04, np.abs(p[..., 0])) * sstep(-0.30, -0.18, p[..., 2]) * sstep(0.42, 0.32, p[..., 2]) * sstep(0.55, 0.62, p[..., 1])
    c = mixc(c, FUR_D, ridge * 0.85)                                            # 가시가 솟는 등줄기는 짙다
    return bald_paint(c, p, (L or look())['body'])

# ── 머리 ──
HEAD_C = V(0, 0.48, 0.46)
TIP = V(0, 0.40, 0.80)                    # 주둥이 끝(코 뒤)
EYE_C = V(0.115, 0.535, 0.570)
def head(p, L=None): return dent(smax(_head0(p), -mouth_cut(p, MOUTH, 0.0075), 0.004), p, (L or look())['head'], 0.8)
def _head0(p):
    d = ell(p, HEAD_C, V(0.17, 0.16, 0.19))
    d = smin(d, seg(p, HEAD_C + V(0, -0.01, 0.06), TIP, 0.130, 0.055), 0.07)      # 주둥이
    d = smin(d, ell(p, V(0, 0.38, 0.56), V(0.10, 0.07, 0.14)), 0.05)              # 턱
    d = smin(d, ell(mirror(p), V(0.09, 0.44, 0.56), V(0.07, 0.07, 0.10)), 0.05)   # 볼(수염 자리)
    d = smin(d, seg(p, HEAD_C + V(0, -0.06, -0.10), V(0, 0.40, 0.20), 0.13, 0.16), 0.06)   # 목
    return smin(d, brow(p), 0.018)
# 노려보는 눈썹뼈(v14 — 사용자: *"쥐를 조금만 더 무섭게"*): 눈 위를 덮고 안쪽(코 쪽) 끝이 낮게 내려앉아 찡그린 눈매 — 털 살의 두둑.
def brow(p):
    m = mirror(p)
    return seg(m, EYE_C + V(-0.020, 0.012, 0.034), EYE_C + V(0.018, 0.036, -0.026), 0.021, 0.014)   # 눈 윗부분을 덮는다(반쯤 감은 노려봄)
def head_col(p, L=None):
    c = mixc(FUR, FUR_L, sstep(0.42, 0.34, p[..., 1]) * sstep(0.40, 0.55, p[..., 2]))
    c = bald_paint(c, p, (L or look())['head'])
    # 흉터는 뺐다(v16 — 사용자: *"흉터는 좀 아닌 것 같아 · 텍스처로 해도 될 것 같긴 한데"*) — 몹의 정점 수로는 가는 선을 정점 색이 못 든다.
    return c
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
def mouth(p): return mouth_inner(p, MOUTH, 0.004, 0.006, philtrum=False)   # 코 밑 세로 줄은 홈만(v16)
def eyes(p): return sph(mirror(p), EYE_C, 0.034)
eye_col = eye_paint(EYE, EYE_C, V(0.75, 0.25, 0.62), 0.034, spots=((0.75, 0.45, -0.35, 0.24), (0.9, -0.3, 0.3, 0.12)))   # 반짝임은 칠한다(v13)
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
def ears(p, L=None):
    m = mirror(p); q = _ear_q(p)
    thick, u, vs, _ = _sheet(q)
    th = EAR_TH * (0.75 + 0.5 * np.clip(-vs / EAR_H, 0, 1))                       # 아래(뿌리 쪽)가 조금 두껍다
    d = np.maximum(thick - th, _outline(u, vs)) * 0.7
    root = ell(m, EAR_C + EAR_R @ V(0, -0.078, -0.010), V(0.030, 0.026, 0.022))  # 뿌리 — 머리 살로 녹는다
    d = smin(d, root, 0.020)
    return ear_cuts(d, p, q, (L or look())['ear'])
def ear_cuts(d, p, q, cuts):
    """귀 찢김 — ('notch', 쪽(+1 = +x), (u, v), 반지름) 물린 홈 · ('half', 쪽, 자르는 높이 v) 위쪽이 들쭉날쭉 뜯겨 나간 반쪽 귀."""
    for c in cuts:
        side = (p[..., 0] > 0) if c[1] > 0 else (p[..., 0] < 0)
        if c[0] == 'notch':
            u, v = c[2]; cut = sph(q, V(u, v, 0), c[3])
            d = smax(d, -np.where(side, cut, 9.0), 0.003)
        elif c[0] == 'half':
            line = c[2] + 0.010 * np.sin(q[..., 0] * 95) + 0.006 * np.sin(q[..., 0] * 240 + 1.3)   # 뜯긴 가장자리
            d = np.where(side, smax(d, q[..., 1] - line, 0.002), d)
    return d
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
def front_legs(p, L=None): return dent(_front_legs0(p), p, (L or look())['fleg'], 0.6)
def _front_legs0(p):
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
        u = (i - 1.5) / 1.5; a = F_WR + V(u * 0.030 + u * 0.020, -0.045, 0.118)   # v14 — 길고 아래로 휜 갈고리(무섭게)
        mid = a + V(u * 0.004, 0.004, 0.026); tip = mid + V(u * 0.004, -0.028, 0.020)
        d = np.minimum(d, smin(seg(m, a, mid, 0.010, 0.007), seg(m, mid, tip, 0.007, 0.0015), 0.006))
    return d

# ── 뒷다리 — 굵은 넓적다리 · 길고 납작한 발 ──
B_HIP = V(0.20, 0.42, -0.30); B_KN = V(0.22, 0.25, -0.18); B_HEEL = V(0.21, 0.07, -0.40)
def back_legs(p, L=None): return dent(_back_legs0(p), p, (L or look())['bleg'], 0.8)
def _back_legs0(p):
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
        u = (i - 1.5) / 1.5; a = B_HEEL + V(u * 0.030 + u * 0.025, -0.050, 0.268)
        mid = a + V(u * 0.004, 0.004, 0.024); tip = mid + V(u * 0.004, -0.024, 0.018)
        d = np.minimum(d, smin(seg(m, a, mid, 0.011, 0.008), seg(m, mid, tip, 0.008, 0.0015), 0.006))
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

# 털 빠진 자리 — 조각 겉에서 찾는다(조각 가운데에서 방향으로 나가 닿는 곳 · 반지름).
def _spots(f, o, items, kind='bald'): return [(kind, surface_along(f, o, d), r) for d, r in items]
def _scar(f, o, dirs, w):   # 겉의 점들을 이은 흉터 홈
    return ('scar', [surface_along(f, o, d) for d in dirs], w)
def _bite(f, o, d, R, r=0.008):
    """이빨 자국 — 겉의 한 자리를 둘러 위 · 아래 두 줄로 늘어선 구멍(물린 턱 모양 · 타원)."""
    cs = surface_along(f, o, d); n = nrm(cs - o); t = nrm(np.cross(UP, n)); u = np.cross(n, t)
    out = []
    for k, th in enumerate(list(np.radians(np.linspace(25, 155, 5))) + list(np.radians(np.linspace(205, 335, 4)))):
        th = th + 0.12 * np.sin(k * 2.7)                                          # 가지런하지 않게 — 물린 자국은 흐트러진다
        pt = cs + (t * np.cos(th) + u * 0.48 * np.sin(th)) * R * (1 + 0.10 * np.sin(k * 1.9))
        out.append(('pit', surface_along(f, o, pt - o), r * (0.8 + 0.4 * abs(np.sin(th * 3.1)))))
    return out
_CH, _MID = V(0, 0.40, 0.16), V(0, 0.45, -0.10)
LOOKS = {
    # A — v19 그대로(왼귀 작은 찢김 · 털 빠진 자리 여덟)
    'A': dict(body=_spots(_torso, _CH, [(V(0.75, 0.65, 0.05), 0.050), (V(-0.70, 0.70, -0.05), 0.044)]) +
                   _spots(_torso, _MID, [(V(0.55, 0.80, -0.20), 0.058), (V(-0.55, 0.75, -0.50), 0.048)]),
              head=_spots(_head0, HEAD_C, [(V(0.50, 0.80, -0.35), 0.030), (V(-0.80, 0.20, 0.30), 0.026)]) +
                   _spots(_head0, _axis(0.64), [(V(0.15, 1.0, 0.10), 0.024)]),
              bleg=_spots(_back_legs0, B_HIP + V(0, -0.06, 0.02), [(V(1.0, 0.10, 0.15), 0.042)]),
              fleg=_spots(_front_legs0, V(-0.16, 0.30, 0.23), [(V(-1.0, 0.0, 0.25), 0.026)]),
              ear=[('notch', 1, (0.086, 0.020), 0.020)]),
    # B — 오른귀(-x)가 반쯤 뜯겼다 · 목덜미에 이빨 자국 · 털 빠진 자리는 다른 데
    'B': dict(body=_bite(_torso, _CH, V(0.80, 0.55, 0.20), 0.058, 0.011) +
                   _spots(_torso, _MID, [(V(-0.60, 0.80, -0.15), 0.055), (V(0.70, 0.55, -0.60), 0.048)]),
              head=_spots(_head0, HEAD_C, [(V(-0.50, 0.80, -0.35), 0.028)]),
              bleg=[], fleg=_spots(_front_legs0, V(0.16, 0.30, 0.23), [(V(1.0, 0.0, 0.25), 0.024)]),
              ear=[('half', -1, 0.012)]),
    # C — 이마 · 콧등을 비스듬히 가로지른 할퀸 흉터 두 줄 · 왼 넓적다리(-x)에 이빨 자국 · 귀 찢김 셋
    'C': dict(body=_spots(_torso, _CH, [(V(0.90, 0.35, 0.10), 0.046)]) + _spots(_torso, _MID, [(V(-0.45, 0.85, -0.65), 0.050)]),
              head=[_scar(_head0, HEAD_C, [V(0.55, 0.80, 0.20), V(0.20, 0.95, 0.55), V(-0.25, 0.80, 0.85)], 0.006),
                    _scar(_head0, HEAD_C, [V(0.62, 0.62, 0.32), V(0.25, 0.80, 0.68), V(-0.18, 0.62, 0.95)], 0.005)],
              bleg=_bite(_back_legs0, V(-0.20, 0.36, -0.28), V(-1.0, 0.10, 0.10), 0.046, 0.010),
              fleg=[],
              ear=[('notch', 1, (0.086, 0.020), 0.020), ('notch', 1, (0.030, 0.084), 0.015), ('notch', -1, (0.075, 0.050), 0.017)]),
}
LOOK_NAMES = tuple(LOOKS)
_LOOK = _os.environ.get('RAT_LOOK', 'A')   # 미리보기 판이 볼 벌(일꾼 프로세스도 같은 값을 읽는다)
def look(): return LOOKS[_LOOK]
def leg_col(p, L=None):
    L = L or look(); return bald_paint(np.broadcast_to(np.asarray(FUR, np.float32), p.shape).copy(), p, L['bleg'] + L['fleg'])

VARIANT = ('head', 'ears', 'body', 'front_legs', 'back_legs')   # 벌마다 갈리는 조각(나머지는 세 벌이 같이 쓴다)
def pieces(head_only=False, look_name=None):
    L = LOOKS[look_name or _LOOK]
    def b(f): return lambda p: f(p, L)
    H = [(b(head), b(head_col), 'Head'), (nose, NOSE, 'Head'), (teeth, TOOTH, 'Head'), (mouth, DARK, 'Head'), (eyes, eye_col, 'Head'),
         (b(ears), ears_col, 'Head'), (whiskers, FUR_D, 'Head')]
    if head_only: return H
    return H + [(b(body), b(body_col), 'Body'), (spines, FUR_D, 'Body'),
                (b(front_legs), b(leg_col), 'LegF'), (front_hands, SKIN, 'LegF'), (front_claws, CLAW, 'LegF'),
                (b(back_legs), b(leg_col), 'LegB'), (back_feet, SKIN, 'LegB'), (back_claws, CLAW, 'LegB'), (tail, tail_col, 'Tail')]

VIEW = (0.42, 1.95)
CLOSE = ((0, 420, 0), (40, 460, 0), (90, 460, 0), (30, 460, 45), (20, 460, -40))   # 마지막 — 밑에서 올려다본다
CLOSE_C = V(0, 0.50, 0.56); CLOSE_SPAN = 0.62
SCALE_Z = -1.95; SCALE_SPAN = 2.45

if __name__ == '__main__':
    # 벌은 환경 변수로 고른다(판의 일꾼 프로세스가 모듈을 새로 읽는다): `RAT_LOOK=B python rat.py renders/rat_v20B`
    sheet('rat', sys.argv[1] if len(sys.argv) > 1 else 'renders/rat_v20', 'rat v20 look ' + _LOOK)
