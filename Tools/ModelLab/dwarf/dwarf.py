"""드워프(남성) — 사람 치비 머리(Male_BroadSquare · head_base = 남성 오크 미리보기의 바탕 머리 · 렌더러)에 드워프 이목구비를 얹는다.
단위 m · 원점은 머리 한복판 · +z 앞. 실행: python dwarf.py renders/dwarf_v1.png"""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg, torus
from head_base import head, front, y_of, W, H, D, T_, R_, EYE_T, MOUTH_T, NOSE_T, EY, MY, NY, EX, ER, render

def NONE(p): return np.full(p.shape[:-1], 9.0, np.float32)
def chain(p, pts, r0, r1, k=0.012):   # 땋은 가닥 — 마디 타원을 줄줄이
    d = NONE(p); n = len(pts) - 1
    for i in range(n):
        a, b = pts[i], pts[i + 1]; r = r0 + (r1 - r0) * i / max(n - 1, 1)
        d = smin(d, ell(p, (a + b) * 0.5, V(r, float(np.linalg.norm(b - a)) * 0.62, r)), k)
    return d
def band(p, a, b, r):   # 가닥을 감는 고리
    ab = b - a; c = (a + b) * 0.5; L = float(np.linalg.norm(ab)); ax = ab / L
    q = p - c; h = (q * ax).sum(-1); rr = np.sqrt(np.maximum((q * q).sum(-1) - h * h, 0))
    return smax(rr - r, np.abs(h) - L * 0.5, 0.004)
def lock(p, pts, r0, r1, k=0.008):
    d = NONE(p); n = len(pts) - 1
    for i in range(n):
        d = smin(d, seg(p, pts[i], pts[i + 1], r0 + (r1 - r0) * i / n, r0 + (r1 - r0) * (i + 1) / n), k)
    return d

ZE = front(EYE_T); ZN = front(NOSE_T); ZM = front(MOUTH_T)

# ── 얼굴 ──
def brow_ridge(p):   # 두툼한 눈썹뼈 — 눈이 그 밑에 묻힌다
    m = mirror(p)
    return seg(m, V(0.02, EY + 0.055, ZE + 0.005), V(EX + ER * 1.3, EY + 0.06, ZE - 0.04), 0.034, 0.028)
def nose(p):         # 크고 둥근 코 — 짧은 콧대 끝에 공 · 콧방울
    m = mirror(p)
    bridge = seg(p, V(0, EY + 0.03, ZE - 0.005), V(0, NY + 0.015, ZN + 0.035), 0.022, 0.034)
    bulb = ell(p, V(0, NY + 0.005, ZN + 0.045), V(0.062, 0.056, 0.052))
    wing = sph(m, V(0.052, NY - 0.012, ZN + 0.018), 0.030)
    d = smin(smin(bridge, bulb, 0.02), wing, 0.015)
    return smax(d, -ell(m, V(0.024, NY - 0.045, ZN + 0.040), V(0.012, 0.008, 0.012)), 0.004)
def cheeks(p):       # 둥근 볼 — 수염 위로 살짝
    m = mirror(p)
    return sph(m, V(0.15, NY + 0.005, ZN - 0.055), 0.065)
def ear(p):          # 작은 사람 귀
    m = mirror(p); x = float(np.interp(EYE_T, T_, R_)) * W
    return ell(m, V(x - 0.005, EY - 0.02, -0.03), V(0.025, 0.065, 0.045))
def face(p):
    d = head(p)
    d = smin(d, brow_ridge(p), 0.03)
    d = smin(d, cheeks(p), 0.04)
    d = smin(d, nose(p), 0.012)
    return smin(d, ear(p), 0.01)
def surf_z(x, y):    # (x, y)에서 얼굴 앞면의 z — 앞에서 뒤로 걸어 들어가 처음 닿는 곳(눈 · 눈썹을 면에 붙인다)
    z = 0.6
    for _ in range(300):
        d = float(face(np.array([[x, y, z]], np.float32))[0])
        if d < 0.0005: break
        z -= max(d * 0.8, 0.0005)
    return z
def on_face(p, region, out, inner=0.010):   # 얼굴 살 위 `out` 두께의 껍데기 중 `region` 안 — 어느 각도에서 봐도 면에 붙는다(오크 문신과 같은 길)
    fd = face(p)
    shell = np.maximum(fd - out, -(fd + inner))
    return smax(np.maximum(shell, region), (ZE - 0.12) - p[..., 2], 0.002)   # 앞 반만(같은 x · y의 뒤통수에 안 생기게)
def eyes(p):         # 작고 깊은 눈 — 얼굴 면 위 4mm 껍데기(옛 판: 고정 z의 타원 → 옆에서 보면 볼 앞 허공에 떴다 — 11-03 게임 화면)
    m = mirror(p)
    return on_face(p, ell(m, V(EX, EY - 0.005, ZE), V(ER * 0.72, ER * 0.80, 0.3)), 0.004)
def glint(p):
    m = mirror(p)
    return on_face(p, ell(m, V(EX - ER * 0.22, EY + ER * 0.2, ZE), V(ER * 0.22, ER * 0.22, 0.3)), 0.007)
def fuzz(p, a=0.0025): return a * np.sin(p[..., 0] * 90 + p[..., 1] * 40)   # 털 결

# 눈썹 — 마디(x, y)만 적고 z는 그 자리의 얼굴 면에 얹는다(중심을 반지름의 1/4 묻는다) · 옛 판은 고정 z라 옆에서 이마 앞에 떴다.
BROW_SPECS = {   # 이름: (마디[(x, y)], r0, r1, k, 결)
    'Bushy':  ([(0.03, EY + 0.085), (EX, EY + 0.10), (EX + ER * 1.5, EY + 0.12), (EX + ER * 2.3, EY + 0.16)], 0.028, 0.008, 0.008, 0.0025),
    'Heavy':  ([(0.006, EY + 0.078), (EX * 0.6, EY + 0.088), (EX + ER * 0.6, EY + 0.090), (EX + ER * 1.9, EY + 0.080)], 0.036, 0.024, 0.012, 0.003),
    'Droop':  ([(0.03, EY + 0.08), (EX, EY + 0.105), (EX + ER * 1.7, EY + 0.085), (EX + ER * 2.4, EY + 0.03), (EX + ER * 2.8, EY - 0.03)], 0.030, 0.012, 0.012, 0.003),
    'Angry':  ([(0.022, EY + 0.048), (EX * 0.7, EY + 0.075), (EX + ER * 0.5, EY + 0.105), (EX + ER * 1.8, EY + 0.135)], 0.032, 0.012, 0.010, 0.0025),
    'Tufted': ([(0.03, EY + 0.085), (EX, EY + 0.095), (EX + ER * 1.5, EY + 0.095)], 0.026, 0.020, 0.01, 0.0025),
}
TUFTS = [(0.06, 0), (EX + 0.005, 1), (EX + ER * 1.3, 2)]   # 터럭 뭉치 셋의 밑동 x
def hug(pts, r0, r1):
    n = len(pts) - 1
    return [V(x, y, surf_z(x, y) - (r0 + (r1 - r0) * i / max(n, 1)) * 0.25) for i, (x, y) in enumerate(pts)]   # 가닥 중심을 면에 조금 묻는다(띄우면 옆에서 뿔처럼 솟았다)
_brow_cache = {}
def brow_fn(name):
    def f(p):
        if name not in _brow_cache:
            pts, r0, r1, k, fz = BROW_SPECS[name]
            tufts = [(V(x, EY + 0.10, surf_z(x, EY + 0.10) - 0.002), i) for x, i in TUFTS] if name == 'Tufted' else []
            _brow_cache[name] = (hug(pts, r0, r1), tufts)
        P, tufts = _brow_cache[name]; _, r0, r1, k, fz = BROW_SPECS[name]
        m = mirror(p); d = lock(m, P, r0, r1, k)
        for b0, i in tufts:
            d = smin(d, seg(m, b0, b0 + V(0.020 + 0.012 * i, 0.045 + 0.008 * i, -0.008 - 0.006 * i), 0.020, 0.005), 0.012)
        return d + fuzz(m, fz)
    return f
BROWS = {n: brow_fn(n) for n in BROW_SPECS}
e_bushy = BROWS['Bushy']
eyebrows = e_bushy

# ── 콧수염 · 수염 · 머리 ──
def mustache(p):     # 코 밑에서 입을 덮고 양옆으로 처진다
    m = mirror(p)
    pts = [V(0.015, NY - 0.050, ZN + 0.030), V(0.07, NY - 0.065, ZN + 0.020), V(0.12, MY - 0.005, ZM + 0.020), V(0.155, MY - 0.06, ZM - 0.01), V(0.16, MY - 0.11, ZM - 0.04)]
    return lock(m, pts, 0.036, 0.012, 0.015)
def sideburns(p):    # 귀 앞에서 턱으로 — 수염과 잇는다
    m = mirror(p)
    return seg(m, V(0.275, EY + 0.02, 0.0), V(0.24, MY - 0.06, 0.13), 0.045, 0.06)
def jaw_mass(p, th=0.045):   # 턱 · 볼 아래를 덮는 수염 덩어리
    ang = np.degrees(np.abs(np.arctan2(p[..., 0], p[..., 2])))
    shell = head(p) - th * (0.15 + 0.85 * (1 - ease((ang - 45) / 40)))         # 귀 쪽으로 갈수록 숱이 얇아진다 — 같은 두께로 자르면 귀 앞에 세로 판이 섰다(11-03 옆모습)
    d = smax(shell, p[..., 1] - (MY + 0.035 + 1.1 * p[..., 0] ** 2), 0.02)    # 윗선 — 가운데는 입 위 · 옆으로 갈수록 올라 볼 아래를 지나 귀 앞으로
    d = smax(d, (ang - 85) * 0.004, 0.01)                                    # 귀 앞까지만
    return smin(d, sideburns(p), 0.03)

def chest(x, y):     # 몸(셔츠 타원) 앞면 z — 수염이 가슴을 타고 흐르게
    q = 1 - (x / 0.52) ** 2 - ((y + 0.84) / 0.42) ** 2
    return -0.02 + 0.37 * np.sqrt(max(q, 0.0)) if y < -0.42 else -0.02
def drape(x, y, z, r): return V(x, y, max(z, chest(x, y) + r * 0.8))
def strands(p, a=0.006, f=75):   # 세로 결 — 덩어리가 털로 읽히게
    return a * np.sin(p[..., 0] * f + 0.6 * np.sin(p[..., 1] * 20))
def flat(p, s): return np.stack([p[..., 0], p[..., 1], p[..., 2] * s], -1)
def rings_on(p, pts, idxs, r):   # 가닥 마디 위에 쇠고리
    d = NONE(p)
    for i in idxs: d = np.minimum(d, band(p, pts[i], pts[i + 1], r))
    return d
def path(x0, y0, y1, z0, r, n, dx=0.0):
    return [drape(x0 + dx * i / n, y0 + (y1 - y0) * i / n, z0, r) for i in range(n + 1)]

def b_bushy(p):      # 덥수룩한 큰 수염 — 둥근 아랫단 · 양옆 쇠고리
    d = jaw_mass(p)
    vol = ell(p, V(0, MY - 0.17, ZM - 0.02), V(0.31, 0.25, 0.17)) + strands(p)
    d = smin(d, vol, 0.06)
    m = mirror(p); a, b = V(0.17, MY - 0.06, ZM + 0.03), V(0.18, MY - 0.12, ZM + 0.025)
    d = smin(d, seg(m, a, b + V(0, -0.05, 0), 0.05, 0.045), 0.02)
    return smin(d, mustache(p), 0.012), band(m, a, b, 0.055)
def b_braids(p):     # 짧은 덩어리 + 땋아 내린 다섯 가닥(가운데가 가장 길다) · 가닥마다 고리 둘
    d = jaw_mass(p)
    d = smin(d, ell(p, V(0, MY - 0.11, ZM - 0.05), V(0.25, 0.14, 0.15)) + strands(p), 0.05)
    m = mirror(p); y0 = MY - 0.17; rg = NONE(p)
    for x, L, side in ((0.0, 0.50, p), (0.075, 0.42, m), (0.15, 0.32, m)):
        pts = path(x, y0, y0 - L, ZM - 0.01, 0.034, 7, dx=x * 0.15)
        d = smin(d, chain(side, pts, 0.036, 0.026, 0.010), 0.02)
        rg = np.minimum(rg, rings_on(side, pts, (3, 6), 0.031))
    return smin(d, mustache(p), 0.012), rg
def b_forked(p):     # 짧은 수염 + 땋아 내린 두 가닥(쇠고리)
    d = jaw_mass(p)
    d = smin(d, ell(p, V(0, MY - 0.11, ZM - 0.08), V(0.23, 0.13, 0.14)) + strands(p), 0.05)
    m = mirror(p); pts = path(0.07, MY - 0.14, MY - 0.50, ZM - 0.03, 0.05, 5, dx=0.01)
    d = smin(d, chain(m, pts, 0.050, 0.036), 0.025)
    return smin(d, mustache(p), 0.012), rings_on(m, pts, (4,), 0.044)
def b_long(p):       # 배까지 늘어뜨린 긴 수염 — 가슴을 타고 흐르고 끝을 고리로 묶었다
    d = jaw_mass(p)
    q = flat(p, 1.7)
    pts = path(0, MY - 0.12, -1.02, ZM - 0.06, 0.12, 6)
    long_ = lock(q, [V(t[0], t[1], t[2] * 1.7) for t in pts], 0.24, 0.06, 0.03) / 1.7 + strands(p, 0.004, 45)
    d = smin(d, long_, 0.06)
    tip = pts[-2]; ring = band(p, tip + V(0, 0.04, 0), tip + V(0, -0.02, 0.0), 0.075)
    return smin(d, mustache(p), 0.012), ring
def b_box(p):        # 짧게 다듬은 네모 수염
    d = jaw_mass(p)
    d = smin(d, rbox(p, V(0, MY - 0.12, ZM - 0.07), V(0.20, 0.10, 0.12), 0.06), 0.05)
    return smin(d, mustache(p), 0.012), NONE(p)
def b_stache(p):     # 턱을 따라 짧게 깎은 수염 + 크게 기른 콧수염(끝이 턱 밑까지 처지고 고리로 묶었다)
    ang = angle(p)
    shell = head(p) - 0.022 * (0.15 + 0.85 * (1 - ease((ang - 45) / 40)))
    d = smax(shell, p[..., 1] - (MY + 0.02 + 1.1 * p[..., 0] ** 2), 0.02)
    d = smax(d, (ang - 85) * 0.004, 0.01)
    d = smin(d, ell(p, V(0, MY - 0.10, ZM - 0.06), V(0.13, 0.07, 0.08)) + strands(p, 0.003), 0.04)   # 턱끝 한 줌
    m = mirror(p)
    pts = [V(0.015, NY - 0.048, ZN + 0.032), V(0.08, NY - 0.062, ZN + 0.024), V(0.14, MY + 0.0, ZM + 0.025), V(0.175, MY - 0.07, ZM + 0.005),
           V(0.18, MY - 0.15, ZM - 0.02), V(0.175, MY - 0.23, ZM - 0.03)]
    st = lock(m, pts, 0.046, 0.022, 0.015) + strands(p, 0.003, 90)
    return smin(d, st, 0.012), band(m, pts[4], pts[5], 0.030)
BEARDS = {'Bushy': b_bushy, 'Braids': b_braids, 'Forked': b_forked, 'Long': b_long, 'Box': b_box, 'Stache': b_stache}

# ── 머리카락 ──
def ease(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)
def angle(p): return np.degrees(np.abs(np.arctan2(p[..., 0], p[..., 2])))
def scalp(p, th, low):   # 머리 껍데기 + 아랫선(low = 각도마다의 높이) 위만 남긴다 · 끝은 둥글게 녹인다
    d = head(p) - th
    d = smax(d, p[..., 1] - (H * 0.5 + th * 0.4), 0.05)                       # 정수리 — 껍데기를 그냥 부풀리면 꼭지가 솟는다
    return smax(d, low - p[..., 1], 0.02)
def hairline(ang, front_y):   # 이마 → 귀 위 → 목덜미로 내려가는 선
    side = EY + 0.05; nape = -0.30
    return np.where(ang < 90, front_y + (side - front_y) * ease((ang - 20) / 70), side + (nape - side) * ease((ang - 95) / 70))
def h_bald(p): return NONE(p), NONE(p)
def h_fringe(p):     # 정수리는 벗어지고 귀 뒤 · 뒤통수만 — 윗선이 뒤로 갈수록 올라간다
    ang = angle(p)
    top = 0.02 + 0.10 * ease((ang - 100) / 70) + 0.018 * np.sin(np.radians(ang) * 14)   # 윗단이 들쭉날쭉 — 곧은 선이면 판으로 읽힌다
    d = scalp(p, 0.030, -0.30)
    d = smax(d, p[..., 1] - top, 0.02)
    front_edge = 100 - 20 * ease((-p[..., 1] - 0.0) / 0.25)                   # 앞선은 비스듬히 — 아래로 갈수록 귀 쪽으로
    d = smax(d, (front_edge - ang) * 0.004, 0.02)
    d = d + 0.03 * ease((-0.16 - p[..., 1]) / 0.12)                            # 목덜미로 갈수록 숱이 준다 — 아랫단이 판 끝으로 서지 않게
    d = smax(d, -(p[..., 1] + 0.27 + 0.025 * np.sin(np.radians(ang) * 18)), 0.015)
    return d + 0.006 * np.sin(np.radians(ang) * 40 + p[..., 1] * 15), NONE(p)
def h_long(p):       # 뒤로 넘긴 긴 머리 — 어깨 뒤까지
    ang = angle(p)
    cap = scalp(p, 0.035, hairline(ang, EY + 0.20))
    m = mirror(p); fall = NONE(p)
    for x, L, side in ((0.0, -0.66, p), (0.10, -0.60, m), (0.19, -0.50, m)):   # 뒤는 가닥 다섯 — 한 판이면 벽이 된다
        z = -0.40 + 0.5 * x * x
        fall = smin(fall, lock(side, [V(x * 0.8, 0.15, z + 0.02), V(x, -0.10, z - 0.06), V(x * 1.08, -0.35, z - 0.08), V(x * 1.12, L, z - 0.04)], 0.085, 0.045, 0.02), 0.012)
    side = lock(m, [V(0.24, 0.0, -0.15), V(0.28, -0.25, -0.16), V(0.31, -0.46, -0.14)], 0.07, 0.05, 0.03)
    d = smin(smin(cap, fall, 0.08), side, 0.05)
    return d + strands(p, 0.004, 55), NONE(p)
def h_braid(p):      # 뒤로 빗어 넘겨 굵게 한 가닥 땋았다
    ang = angle(p)
    cap = scalp(p, 0.032, hairline(ang, EY + 0.20)) + strands(p, 0.004, 90)
    pts = [V(0, 0.05, -0.36), V(0, -0.08, -0.40), V(0, -0.20, -0.42), V(0, -0.32, -0.43), V(0, -0.44, -0.42), V(0, -0.56, -0.40), V(0, -0.66, -0.38)]
    d = smin(cap, chain(p, pts, 0.07, 0.05, 0.012), 0.05)
    return d, rings_on(p, pts, (2, 5), 0.062)
def h_crest(p):      # 옆은 밀고 가운데 볏 — 머리 가운데 선을 따라 같은 높이로 잇는다(이마 · 목덜미에서만 잦아든다) · 뒤로 짧은 꽁지
    th = np.arctan2(p[..., 2], p[..., 1])                       # 정수리 0 · 앞(+z)으로 + · 뒤로 -
    T0, T1 = 0.80, -1.95                                         # 이마 위 → 뒤통수 아래
    hm = 0.10 * ease((T0 - th) / 0.45) * ease((th - T1) / 0.55)  # 높이(m) — 양 끝만 부드럽게 오르내린다
    hm = hm + 0.010 * np.sin(th * 26) * ease((T0 - th) / 0.45)   # 볏 끝의 잔 들쭉날쭉(높이는 그대로)
    # 머리를 감싸는 타원에서 잰다 — 바탕 머리는 정수리가 한 점으로 모여 그것을 부풀리면 기둥이 솟는다
    R = np.array([0.31, 0.345, 0.30], np.float32)
    q = p / (R * (1 + hm[..., None] / 0.32))
    shell = (length(q) - 1) * 0.30
    fin = smax(shell, np.abs(p[..., 0]) - 0.06, 0.025)
    fin = smax(fin, np.maximum(th - T0, T1 - th) * 0.2, 0.02)
    tail = lock(p, [V(0, 0.02, -0.34), V(0, -0.10, -0.40), V(0, -0.24, -0.40)], 0.06, 0.035, 0.02)
    return smin(fin, tail, 0.04), band(p, V(0, -0.02, -0.355), V(0, -0.07, -0.385), 0.05)
# ── 긴 머리 넷(11-03 사용자: *"긴머리 헤어가 좀 부족한듯"*) ──
# 드워프 몸통(머리 좌표 · `body.py` DWARF에서 잰 값) — 꼭대기 y -0.418 · 옆 반폭 r(y) · 앞면은 타원(깊이/폭 0.937 · 가운데 z -0.045).
# 어깨 · 가슴으로 떨어지는 가닥은 이 앞면에 얹는다(몸통을 뚫지 않게).
_TY = [-0.418, -0.475, -0.588, -0.759, -1.30]; _TR = [0.245, 0.347, 0.381, 0.375, 0.37]
def torso_front(x, y):
    if y > -0.418: return -9.0
    r = float(np.interp(-y, [-t for t in _TY], _TR))
    return -0.045 + 0.937 * np.sqrt(max(r * r - x * x, 0.0))
def on_torso(x, y, z, r): return V(x, y, max(z, torso_front(x, y) + r * 0.85))
def wave(p, a=0.010, f=9):   # 굵은 물결 — 긴 머리의 가장자리가 들쭉날쭉하게(세로 결보다 굵다)
    return a * np.sin(p[..., 1] * f + 2.0 * np.sin(p[..., 0] * 12))

def h_mane(p):       # 갈기 — 덥수룩한 긴 머리가 뒤 · 옆으로 어깨 아래까지 퍼진다(늙은 왕)
    ang = angle(p)
    cap = scalp(p, 0.045, hairline(ang, EY + 0.17))
    m = mirror(p); fall = NONE(p)
    for a, L, side in ((180, -0.80, p), (150, -0.78, m), (120, -0.70, m), (98, -0.58, m)):   # 뒤 → 옆 — 위는 머리에 붙고 아래로 갈수록 바깥으로 퍼진다
        ra = np.radians(a); top = V(0.30 * np.sin(ra), 0.10, 0.30 * np.cos(ra) - 0.04)
        bot = V(0.40 * np.sin(ra), L, 0.40 * np.cos(ra) - 0.06)
        mid = (top + bot) * 0.5 + V(0.04 * np.sin(ra), 0, 0.04 * np.cos(ra))
        fall = smin(fall, lock(side, [top, mid, bot], 0.11, 0.07, 0.03), 0.035)
    d = smin(cap, fall, 0.07)
    return d + wave(p) + strands(p, 0.004, 60), NONE(p)

_TB = None
def _twin_pts():     # 귀 뒤에서 땋아 어깨 위로 넘겨 가슴에 늘어뜨린다
    global _TB
    if _TB is None:
        pts = [V(0.22, 0.04, -0.20), V(0.29, -0.10, -0.12), V(0.32, -0.24, -0.02), V(0.32, -0.34, 0.10), V(0.31, -0.40, 0.22)]
        for y in (-0.50, -0.62, -0.74, -0.86):
            pts.append(on_torso(0.29 - (y + 0.5) * 0.08, y, 0.0, 0.05))
        _TB = pts
    return _TB
def h_twinbraids(p): # 두 가닥 땋아 어깨 너머 가슴으로 · 고리 둘씩
    ang = angle(p)
    cap = scalp(p, 0.034, hairline(ang, EY + 0.19)) + strands(p, 0.004, 90)
    m = mirror(p); pts = _twin_pts()
    d = smin(cap, chain(m, pts, 0.060, 0.042, 0.012), 0.04)
    return d, rings_on(m, pts, (3, 7), 0.052)

def h_longfringe(p): # 정수리는 벗어지고 옆뒤 머리만 어깨까지 기른다(늙은 장인)
    ang = angle(p)
    ring_ = scalp(p, 0.032, -0.10)
    ring_ = smax(ring_, p[..., 1] - (0.03 + 0.08 * ease((ang - 110) / 60) + 0.015 * np.sin(np.radians(ang) * 12)), 0.02)
    ring_ = smax(ring_, (95 - ang) * 0.004, 0.02)
    m = mirror(p); fall = NONE(p)
    for x, L, side in ((0.0, -0.62, p), (0.11, -0.58, m), (0.21, -0.52, m)):
        z = -0.38 + 0.6 * x * x
        fall = smin(fall, lock(side, [V(x * 0.9, 0.0, z + 0.04), V(x, -0.20, z - 0.04), V(x * 1.08, -0.40, z - 0.06), V(x * 1.12, L, z - 0.03)], 0.08, 0.045, 0.02), 0.012)
    side = lock(m, [V(0.27, 0.02, -0.12), V(0.30, -0.22, -0.12), V(0.32, -0.42, -0.10)], 0.06, 0.045, 0.03)
    d = smin(smin(ring_, fall, 0.06), side, 0.05)
    return d + wave(p, 0.006) + strands(p, 0.004, 55), NONE(p)

def h_topknot(p):    # 정수리 뒤 상투 + 등으로 늘어진 꽁지(전사) · 상투를 묶은 고리
    ang = angle(p)
    cap = scalp(p, 0.030, hairline(ang, EY + 0.20)) + strands(p, 0.003, 100)
    knot = ell(p, V(0, 0.36, -0.13), V(0.085, 0.075, 0.085))
    tail = lock(p, [V(0, 0.38, -0.20), V(0, 0.28, -0.34), V(0, 0.08, -0.42), V(0, -0.18, -0.44), V(0, -0.46, -0.42)], 0.07, 0.035, 0.02)
    d = smin(smin(cap, knot, 0.04), tail, 0.03)
    return d + strands(p, 0.004, 70), band(p, V(0, 0.33, -0.10), V(0, 0.37, -0.17), 0.075)

HAIRS = {'Bald': h_bald, 'Fringe': h_fringe, 'Long': h_long, 'Braid': h_braid, 'Crest': h_crest,
         'Mane': h_mane, 'TwinBraids': h_twinbraids, 'LongFringe': h_longfringe, 'Topknot': h_topknot}

def body(p): return ell(p, V(0, -0.84, -0.02), V(0.52, 0.42, 0.37))

SKIN = (0.86, 0.66, 0.52); NOSE = (0.84, 0.54, 0.44); METAL = (0.60, 0.62, 0.66)
DARK = (0.07, 0.05, 0.05); WHITE = (0.95, 0.95, 0.95); SHIRT = (0.30, 0.34, 0.42)
RED = (0.62, 0.24, 0.10); BROWN = (0.42, 0.24, 0.12); GREY = (0.72, 0.70, 0.66); GINGER = (0.70, 0.36, 0.14)
def memo(fn):
    cache = {}
    def part(i):
        def f(p):
            if cache.get('k') is not p: cache['k'] = p; cache['v'] = fn(p)
            return cache['v'][i]
        return f
    return part
def pieces(beard, hair, col, brow=e_bushy):   # 수염 · 머리 · 눈썹은 따로 고른다
    b, h = memo(beard), memo(hair)
    return [(face, SKIN), (eyes, DARK), (glint, WHITE), (brow, col), (body, SHIRT),
            (b(0), col), (b(1), METAL), (h(0), col), (h(1), METAL)]

# 한 줄에 하나 — (수염, 머리, 색)
COMBOS = [('Bushy', 'Fringe', RED), ('Braids', 'Braid', GINGER), ('Long', 'Long', GREY), ('Forked', 'Bald', BROWN), ('Box', 'Long', BROWN), ('Stache', 'Crest', GINGER), ('Stache', 'Fringe', GREY)]

S, Hh, VIEWS = 300, 420, (0, 50, 155)
def row(c):   # 조합을 그대로 받는다 — 자식 프로세스는 모듈을 새로 읽어 거른 COMBOS를 모른다
    bn, hn, col = c
    return [np.asarray(render(pieces(BEARDS[bn], HAIRS[hn], col), a, S, Hh, -0.27, 1.60)) for a in VIEWS]

BROW_SHOW = [('Bushy', 'Stache', 'Bald', BROWN), ('Heavy', 'Box', 'Fringe', RED), ('Droop', 'Long', 'Long', GREY), ('Angry', 'Braids', 'Crest', GINGER), ('Tufted', 'Bushy', 'Bald', GREY)]
def brow_row(c):   # 얼굴 가까이 — 앞 · 비스듬히 · 옆 · 옆 뒤쪽
    en, bn, hn, col = c
    return [np.asarray(render(pieces(BEARDS[bn], HAIRS[hn], col, BROWS[en]), a, 300, 300, 0.02, 0.80)) for a in (0, 40, 80, 100)]

if __name__ == '__main__':
    from multiprocessing import Pool
    out = sys.argv[1]
    if len(sys.argv) > 2 and sys.argv[2] == 'brows':   # 눈썹 보기: python dwarf.py out.png brows
        with Pool(len(BROW_SHOW)) as pool: rows = pool.map(brow_row, BROW_SHOW)
        sheet = Image.new('RGB', (1200, 300 * len(rows)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
        for r, ims in enumerate(rows):
            for c, im in enumerate(ims): sheet.paste(Image.fromarray(im), (c * 300, r * 300))
            d.text((6, r * 300 + 4), f'brow {BROW_SHOW[r][0]}  (beard {BROW_SHOW[r][1]} / hair {BROW_SHOW[r][2]})', fill=(255, 220, 120))
        sheet.save(out); print('saved', out); sys.exit()
    if len(sys.argv) > 2: COMBOS[:] = [COMBOS[int(i)] for i in sys.argv[2].split(',')]   # 고른 줄만: python dwarf.py out.png 5,6
    with Pool(len(COMBOS)) as pool: rows = pool.map(row, COMBOS)
    sheet = Image.new('RGB', (S * 3, Hh * len(COMBOS)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for r, ims in enumerate(rows):
        for c, im in enumerate(ims): sheet.paste(Image.fromarray(im), (c * S, r * Hh))
        bn, hn, _ = COMBOS[r]; d.text((6, r * Hh + 4), f'beard {bn} / hair {hn}', fill=(255, 220, 120))
    sheet.save(out); print('saved', out)
