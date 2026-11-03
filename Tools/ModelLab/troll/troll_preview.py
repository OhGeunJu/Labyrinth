"""Troll v2 preview — chibi troll. Units = head height, origin at feet, +z front."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image
from brute_preview import V, length, mirror, side, smin, smax, ell, rbox, sph, capsule, seg, torus, joints
from ogre_preview import render

def troll_joints():   # 트롤 — 하반신이 짧고 상체가 길다(사용자 11-02: "다리 좀 줄고 상체가 더 길어지면") · 다리 사이가 붙는다
    j = joints(1.55, 0.65, 0.60, 0.205)   # 다리 사이 — 허벅지가 거의 맞닿는다
    LEG_CUT = 0.10                                   # 다리를 줄인 만큼 몸통을 늘린다 — 머리 꼭대기 · 팔은 그대로
    j['Hip'] -= LEG_CUT
    j['Knee'] = (j['Hip'] + j['Ankle']) / 2
    return j
J = troll_joints()

# **굽은 허리** — 엉덩이 위로 올라갈수록 앞으로 민다(높이의 제곱). 혹을 붙이지 않고 몸통 자체가 굽는다.
LEAN = 0.32 * (0.76 / 0.86) ** 2   # 몸통이 길어져도 굽은 정도는 그대로(굽힘은 엉덩이부터 높이의 제곱)
def lean_at(y, j=J): return LEAN * max(y - j['Hip'], 0) ** 2
def bend(p, j=J):
    dy = np.maximum(p[..., 1] - j['Hip'], 0)
    return np.stack([p[..., 0], p[..., 1], p[..., 2] - LEAN * dy * dy], -1)
HEAD_FWD = 0.055   # 머리를 조금만 앞으로 — 굵은 목이 드러나 목줄이 걸린다(많이 빼면 목이 길어진다)
HEAD_SHIFT = lean_at(J['Top']) + HEAD_FWD; ARM_SHIFT = lean_at(J['ArmTop'])
HEAD_DROP = 0.12   # 머리가 굽은 등보다 낮게 처진다
WIDE = 1.18   # 얼굴을 가로로 늘린다 — 머리 좌표의 x를 나눈다(눈동자 · 홍채는 자리만 따라가고 모양은 둥글게)
def head_space0(p): return p - V(0, -HEAD_DROP, HEAD_SHIFT)
def head_space(p): q = head_space0(p); q[..., 0] = q[..., 0] / WIDE; return q
def arm_space(p): return p - V(0, 0, ARM_SHIFT)

# 등 비늘 판 — 가슴 타원의 등 · 어깨 위(굽기 전 좌표)
def back_pts(j=J):
    t = j['Top']; c = np.array([0, t - 0.06, -0.08], np.float32); r = np.array([0.42, 0.24, 0.27], np.float32)
    pts = []
    for el, n, w in ((0.7, 5, 1.2), (0.4, 6, 1.35), (0.1, 7, 1.4), (-0.25, 6, 1.2)):
        for az in np.linspace(-w, w, n):
            d = np.array([np.sin(az) * np.cos(el), np.sin(el), -np.cos(az) * np.cos(el)], np.float32)
            pts.append(c + r * d)
    return pts
PTS = back_pts()

def torso_raw(p, j=J):
    t, h = j['Top'], j['Hip']; m = mirror(p)
    chest = ell(p, V(0, t - 0.26, 0.04), V(0.48, 0.30, 0.32))
    # 어깨 — 등 가운데에서 팔까지 한 둥근 등성이로 흘러내린다(따로 솟은 혹 · 어깨 덩어리를 두지 않는다 — W자가 됐다)
    dome = ell(p, V(0, t - 0.18, -0.12), V(0.62, 0.30, 0.26))   # 머리 뒤에 선다(머리를 삼키지 않게)
    traps = ell(p, V(0, t - 0.10, 0.02), V(0.34, 0.16, 0.24))          # 목 뒤 — 머리로 이어진다
    gut = ell(p, V(0, h + 0.24, 0.08), V(0.40, 0.30, 0.34))
    hips = ell(p, V(0, h + 0.02, -0.02), V(0.34, 0.16, 0.26))
    d = smin(smin(chest, dome, 0.14), traps, 0.12)
    d = smin(d, smin(gut, hips, 0.10), 0.14)
    return d
def torso(p, j=J): return smin(torso_raw(bend(p, j), j) * 0.9, throat(p, j), 0.12)   # 목이 등과 한 덩어리로 이어진다

def neck_raw(p, j=J):   # 머리와 몸 사이를 메우는 살(머리 좌표)
    t = j['Top']
    return ell(p, V(0, t - 0.15, 0.08), V(0.24, 0.18, 0.20))   # 좁고 뒤로 — 앞으로 나오면 두꺼비 목이 된다

E = (0.128, 0.02, 0.418)   # 눈 자리(x · 꼭대기 위 · z — 머리 좌표)
def eye_local(q):   # 넓힌 머리 좌표에서 눈 둘레만 원래 너비로 — 자리는 따라가고 눈은 안 늘어난다
    q = q.copy(); q[..., 0] = E[0] + (q[..., 0] - E[0]) * WIDE; return q
def nostril(m, t, r, dz):
    q = m - V(0.015, t + 0.009, dz)
    a = 0.0   # 11자 — 돼지코 앞면에 곧게 선다
    q = np.stack([q[..., 0] * np.cos(a) - q[..., 1] * np.sin(a), q[..., 0] * np.sin(a) + q[..., 1] * np.cos(a), q[..., 2]], -1)
    return ell(q, V(0, 0, 0), r)
def nose_raw(p, t, d):   # 세모 코 — 윗변은 미간 밑 V 주름과 같은 모양 · 얼굴 면 자체를 부풀려 바닥이 면에 딱 붙는다
    yy = p[..., 1] - t
    hw = (0.046 - yy) * 1.1 - 0.003                                       # V 주름과 같은 기울기로 넓어진다
    tri = smax(np.abs(p[..., 0]) - hw, yy - 0.044, 0.004)                  # 꼭짓점은 V 끝
    k = np.clip((yy + 0.014) / 0.050, 0, 1)                                # 바닥 0 → 위 1
    bump = 0.013 * k * (2 - k)                                             # 위(V 밑)가 가장 높고 바닥에서 0 — 턱이 안 선다
    return smax(tri, d - bump, 0.004)
def ear_raw(m, t):   # 귀(머리 좌표 · 거울) — 납작한 귓바퀴 + 뒤 · 위로 살짝 뾰족한 끝 · 윤곽을 따라 판 오목
    ea = np.radians(35); qe = m - V(0.189, t - 0.03, 0.285)   # 눈 밑 높이(참고 그림) · 좁아진 볼에 뿌리를 붙인다
    eb = np.radians(28)   # 귓바퀴 면을 앞으로 돌린다 — 앞에서도 오목이 보이게(더 돌리면 나팔 귀)
    qe = np.stack([qe[..., 0] * np.cos(eb) + qe[..., 2] * np.sin(eb), qe[..., 1], -qe[..., 0] * np.sin(eb) + qe[..., 2] * np.cos(eb)], -1)   # 귀 — 머리에 바짝 붙어 뒤로 눕는다(옆으로 뻗으면 나팔 귀가 된다)
    qe = np.stack([qe[..., 0], qe[..., 1] * np.cos(ea) + qe[..., 2] * np.sin(ea), -qe[..., 1] * np.sin(ea) + qe[..., 2] * np.cos(ea)], -1)
    # 물방울 판 — 뿌리는 둥글고 뒤 · 위로 가늘어져 뾰족하다(옆에서 본 윤곽 · y는 조금 늘려 세로로 긴 귀)
    def drop(q, a, b, ra, rb):
        q2 = np.stack([np.zeros_like(q[..., 0]), q[..., 1] / 1.2, q[..., 2]], -1)
        return seg(q2, a, b, ra, rb)
    x = qe[..., 0]
    ear = smax(drop(qe, V(0, 0, 0), V(0, 0.008, -0.044), 0.030, 0.003), np.abs(x) - 0.013, 0.008)
    # 오목 — 같은 물방울을 줄여 판다: 위 · 뒤 테두리가 끝까지 따라가 뾰족하게 파이고 · 앞쪽 뿌리는 열린다 · 판을 뚫지 않는다
    hollow = smax(drop(qe, V(0, -0.002, 0.010), V(0, 0.007, -0.038), 0.021, 0.001), 0.004 - x, 0.004)
    ear = smax(ear, -hollow, 0.004)
    return ear
def head_raw(p, j=J):   # 배 모양 — 작은 정수리 · 큰 아래 얼굴 · 콧등 없이 콧구멍만
    t = j['Top']; m = mirror(p)
    ec = V(E[0], t + E[1], E[2])
    cran = ell(p, V(0, t + 0.05, 0.25), V(0.19, 0.16, 0.20))
    # **십자 얼굴** — 가로는 눈 높이의 눈두덩, 세로는 가운데로 내민 주둥이(콧구멍 → 입 → 턱).
    # 옆얼굴은 눈두덩부터 턱까지 매끈한 한 면이다 — 눈 위 · 광대에 튀어나온 뼈를 두지 않는다.
    brow = ell(m, V(0.07, t + 0.056, 0.42), V(0.085, 0.034, 0.06))      # 눈썹뼈 — 얼굴 안쪽(미간 쪽)에 몰린다 · 바깥은 매끈
    glabella = seg(p, V(0, t + 0.055, 0.445), V(0, t + 0.02, 0.47), 0.05, 0.016)   # 미간 — 위가 넓고 아래로 좁아지는 세모
    # 아래 얼굴은 넓적하지 않고 깊다(두꺼비 같지 않게) — 좁은 턱 · 턱 모서리에 씹는 근육 · 위아래 입술이 같은 깊이
    side = ell(p, V(0, t - 0.05, 0.30), V(0.148, 0.14, 0.18))   # 볼 바탕 — 좁게(광대 · 턱 모서리 사이가 홀쭉하게)
    jaw = ell(p, V(0, t - 0.155, 0.30), V(0.132, 0.10, 0.16))   # 턱 — 좁고 덜 처지게(턱살이 늘어지지 않게)
    masseter = ell(m, V(0.114, t - 0.11, 0.31), V(0.048, 0.083, 0.072))   # 씹는 근육
    muzzle = ell(p, V(0, t - 0.10, 0.40), V(0.12, 0.13, 0.10))          # 주둥이 — 콧구멍부터 입까지
    lowlip = ell(p, V(0, t - 0.165, 0.45), V(0.10, 0.04, 0.07))         # 아랫입술 — 윗입술과 같은 깊이로
    chin = ell(p, V(0, t - 0.23, 0.42), V(0.118, 0.062, 0.085))           # 단단한 턱끝 — 가로로 넓다(V자 턱이 아니다)
    # 콧날 판 — 눈두덩 끝에서 윗입술까지 앞으로 비스듬히 내려간다(옆에서 패이지 않고 곧게 흐른다)
    qs = p * np.array([0.86, 1, 1], np.float32)
    slope = seg(qs, V(0, t + 0.04, 0.40), V(0, t - 0.07, 0.47), 0.07, 0.065)
    ear = ear_raw(m, t)
    d = smin(smin(cran, side, 0.08), brow, 0.02)
    d = smin(smin(smin(d, jaw, 0.06), masseter, 0.05), muzzle, 0.05)
    # 참고 그림(사용자 11-01) — **광대와 턱 모서리가 넓다**(그 사이 볼은 평평)
    cheekbone = ell(m, V(0.118, t - 0.005, 0.37), V(0.07, 0.042, 0.065))    # 눈 밑 광대 — 옆으로 튀어나온다
    jawangle = ell(m, V(0.105, t - 0.17, 0.28), V(0.042, 0.075, 0.075))     # 턱 모서리 — 조금만(크면 어금니 뺀 볼처럼 둥글게 부푼다)
    d = smin(smin(d, cheekbone, 0.03), jawangle, 0.07)
    # 그 사이 볼은 파지 않는다 — 꺼진 게 아니라 광대 · 턱 모서리가 넓어 평평해 보인다
    d = smin(smin(smin(d, chin, 0.04), lowlip, 0.03), slope, 0.05)
    d = smin(d, glabella, 0.025)   # 미간 세모는 마지막에 얕게 붙여 모서리를 살린다
    # 코 — 콧등은 없고 콧구멍을 얹은 낮은 둔덕 · 그 위를 미간이 덮는다
    # 코 — 미간 밑 V 틈을 채우는 세모 코: 위는 미간에 묻혀 좁고 · 아래로 넓어지며 · 틈 깊이만큼 앞으로 나온다
    d = smin(d, nose_raw(p, t, d), 0.004)
    # 콧구멍 — 그리지 않고 코 아래쪽을 살짝 판다(11자)
    d = smax(d, -ell(m, V(0.016, t + 0.002, 0.514), V(0.0075, 0.015, 0.016)), 0.002)
    # 미간과 코 사이의 틈 — 세모를 통째로 파지 않고 두 덩어리가 만나는 선만 얕게 판다(뒤집힌 V · 안쪽 면은 그대로)
    c0, c1, c2 = V(0, t + 0.046, 0.494), V(0.021, t + 0.027, 0.499), V(0.042, t + 0.008, 0.479)   # 면 위 0.002(재서 놓았다)
    crease = np.minimum(capsule(m, c0, c1, 0.010), capsule(m, c1, c2, 0.010))
    d = smax(d, -crease, 0.008)
    d = smin(d, ear, 0.02)
    # 목 메움살은 머리에 안 넣는다 — 머리보다 넓어 목줄 걸 자리가 없어진다(드러난 목은 `throat`)
    me = eye_local(m)
    d = smax(d, -ell(me, ec + V(-0.006, 0, 0.022), V(0.052, 0.03, 0.04)), 0.012)   # 눈 자리 — 안쪽 끝(눈물 언덕)까지 판다
    # 눈꺼풀 — 위아래로 조여 가는 아몬드 · 윗선은 안쪽으로 내려간다(노려본다)
    shell = ell(me, ec, V(0.043, 0.032, 0.033))
    dx = me[..., 0] - ec[0]
    upper = smax(shell, (ec[1] + 0.012 + 0.10 * dx - 8.0 * dx * dx) - p[..., 1], 0.003)   # 휘어진 윗선 · 안쪽이 낮다
    lower = smax(shell, p[..., 1] - (ec[1] - 0.017 + 6.0 * dx * dx), 0.003)
    d = smin(d, np.minimum(upper, lower), 0.004)
    d = smax(d, -rbox(p, V(0, t - 0.12, 0.53), V(0.10, 0.029, 0.10), 0.02), 0.012)   # 살짝 벌린 입 — 모서리까지 열려 송곳니가 안에서 보인다
    d = smax(d, -rbox(p, V(0, t - 0.12, 0.40), V(0.11, 0.06, 0.07), 0.03), 0.01)    # 입 안 공간 — 이 뒤로 깊고 넓게(앞 틈은 그대로)
    return d
def lips(p, j=J):   # 입을 두른 두꺼운 입술 고리 — 아랫입술이 더 두껍다
    q = p - V(0, j['Top'] - 0.12, 0.535)
    q = np.stack([q[..., 0] / 1.6, q[..., 2], q[..., 1]], -1)
    thick = 0.012 + 0.010 * np.clip(-q[..., 2] / 0.06, 0, 1)
    return torus(q, V(0, 0, 0), 0.062, 0) - thick
def head(p, j=J): return head_raw(head_space(p), j)   # 목은 머리에 안 녹인다 — 녹이면 얼굴이 부푼다
def neck(p, j=J): return neck_raw(head_space(p), j)
NK0 = V(0, J['Top'] - 0.12, 0.18); NK1 = V(0, J['Top'] - 0.18, 0.33)   # 목 — 둔덕 속에서 머리 속까지(앞이 조금 처진다)
NR = 0.20
NW = 1.05   # 목의 가로 — 머리보다 좁게(넓으면 얼굴이 커 보인다) · 굵기는 몸통에 녹아 승모근으로 이어진다
def throat(p, j=J):   # 굵은 목 — 승모근이 등에서 그대로 이어져 머리에 닿는다(가는 목이 아니다)
    q = p * np.array([1 / NW, 1, 1], np.float32)
    return capsule(q, NK0, NK1, NR)
def caruncle(p, j=J):   # 눈 안쪽 끝의 눈물 언덕
    q = eye_local(mirror(head_space(p))); return sph(q, V(E[0] - 0.036, j['Top'] + E[1] - 0.004, E[2] + 0.005), 0.009)
def irises(p, j=J):
    q = eye_local(mirror(head_space(p))); return ell(q, V(E[0] - 0.005, j['Top'] + E[1], E[2] + 0.02), V(0.017, 0.017, 0.008))
def eyes(p, j=J):
    q = eye_local(mirror(head_space(p))); return ell(q, V(E[0], j['Top'] + E[1], E[2]), V(0.038, 0.027, 0.028))
def pupils(p, j=J):   # 흰자 안의 작은 눈동자 — 조금 안쪽을 본다
    q = eye_local(mirror(head_space(p))); return ell(q, V(E[0] - 0.005, j['Top'] + E[1], E[2] + 0.026), V(0.007, 0.007, 0.005))
def nostrils(p, j=J):   # 면에 겨우 비치는 어두운 틈 — 파지 않는다(선이 서지 않게)
    p = head_space(p)
    return smax(nostril(mirror(p), j['Top'], V(0.0042, 0.0115, 0.2), 0.45), head_raw(p, j) - 0.003, 0.002)
def mouth(p, j=J): return rbox(head_space(p), V(0, j['Top'] - 0.12, 0.37), V(0.11, 0.06, 0.07), 0.025)   # 입 안 뒷벽 — 송곳니(z .47)보다 뒤
def teeth(p, j=J):
    p = head_space(p)
    t = j['Top']; d = np.full(p.shape[:-1], 9.0, np.float32)
    for x in (-0.054, -0.018, 0.018, 0.054):   # 윗니 넷 — 큼직하고 네모나다
        d = np.minimum(d, rbox(p, V(x, t - 0.099, 0.495), V(0.014, 0.013, 0.012), 0.006))
    for x in (-0.048, -0.016, 0.016, 0.048):    # 아랫니 넷
        d = np.minimum(d, rbox(p, V(x, t - 0.139, 0.497), V(0.013, 0.012, 0.012), 0.006))
    for x in (-0.075, 0.075):                   # 아래 송곳니 — 아랫잇몸에 박혀 입 안에서 솟는다(입술 살을 안 뚫는다)
        d = np.minimum(d, seg(p, V(x, t - 0.15, 0.472), V(x * 0.95, t - 0.098, 0.474), 0.016, 0.005))
    return d

def body_near_neck(p, j=J): return np.minimum(torso(p, j), neck(p, j))
# 목줄 — 목을 한 바퀴 두르는 쇠고리: 턱 밑 목 앞(CF)에서 뒤로 갈수록 올라가는 기울어진 판을 따라 머리 · 목 덩어리를 감싼다.
# 몸 표면을 따라가면 굽은 등에서 고리가 안 닫힌다 — 머리 · 목만 감싸고, 뒤쪽은 등 둔덕 살에 파묻힌다.
NU = ((NK1 - NK0) / np.linalg.norm(NK1 - NK0)).astype(np.float32)   # 목 기둥 방향
# 쇠목줄 — **머리 좌표에서 저작한다**(몸에서 재면 머리와 규격이 어긋난다 · 가로 넓힘 `WIDE`도 머리를 따른다).
# 귀(z .285)보다 조금 뒤, 그 자리의 머리 단면을 바짝 두르는 타원 고리 — 위가 살짝 뒤로 눕는다.
_T = J['Top']
# 아래 끝은 그대로 두고 위 끝만 얼굴 쪽으로(사용자 11-02) — 등과 떨어지되 귀에는 안 닿게.
COLLAR_BOT_H = V(0, _T - 0.292, 0.246)   # 고리 맨 아래 중심선(머리 좌표)
COLLAR_TOP_H = V(0, _T + 0.232, 0.170)   # 고리 맨 위 중심선(머리 좌표) — 전엔 z .134(등에 붙었다)
COLLAR_C = (COLLAR_BOT_H + COLLAR_TOP_H) * 0.5
COLLAR_A = 0.195                         # 옆 반폭(머리 좌표)
COLLAR_B = float(np.linalg.norm(COLLAR_TOP_H - COLLAR_BOT_H)) * 0.5
COLLAR_W, COLLAR_T = 0.040, 0.018  # 띠 반너비 · 반두께
CT = ((COLLAR_TOP_H - COLLAR_BOT_H) / np.linalg.norm(COLLAR_TOP_H - COLLAR_BOT_H)).astype(np.float32)   # 고리 판 안의 위아래
CN = V(0, -CT[2], CT[1])                                                                                  # 고리 판의 법선
def collar(p, j=J):
    q = head_space(p) - COLLAR_C
    u, v, h = q[..., 0], (q * CT).sum(-1), (q * CN).sum(-1)
    e = (length(np.stack([u / COLLAR_A, v / COLLAR_B], -1)) - 1) * min(COLLAR_A, COLLAR_B)
    return smax(np.abs(e) - COLLAR_T, np.abs(h) - COLLAR_W, 0.008)
_bot = COLLAR_C - CT * (COLLAR_B + COLLAR_T)                 # 고리 맨 아래(머리 좌표) — 사슬이 여기서 곧게 떨어진다
CF = (_bot + V(0, -HEAD_DROP, HEAD_SHIFT)).astype(np.float32)   # 월드로
def chest_front(y, j=J, x=0.0):   # 가운데 줄에서 가슴 앞면의 z — 사슬을 살에 붙여 늘어뜨린다
    zs = np.linspace(1.2, -0.2, 700, dtype=np.float32)
    p = np.stack([np.full_like(zs, x), np.full_like(zs, y), zs], -1)
    d = body_near_neck(p, j); return float(zs[np.argmax(d < 0)])
# 사슬 고리 — 길쭉한 타원 고리(두 반원 + 곧은 두 변)가 90°씩 번갈아 돌며 **앞 고리 구멍에 꿰인다**(떨어진 고리를 늘어놓지 않는다).
LINK_H, LINK_R, LINK_W = 0.016, 0.016, 0.0075        # 곧은 변 반길이 · 반원 반지름(중심선) · 쇠 굵기(반)
LINK_PITCH = 2 * (LINK_H + LINK_R) - 2 * LINK_W       # 고리 사이 = 고리 안쪽 길이 — 앞 고리 안쪽 끝에 걸린다
LINK_HALF = LINK_H + LINK_R + LINK_W                  # 고리 바깥 반길이
def link(p, c, turn):   # 세로로 매달린 고리 하나 — turn이면 옆에서 보이게(90° 돈다)
    q = p - c
    a, n = (q[..., 2], q[..., 0]) if turn else (q[..., 0], q[..., 2])
    y = np.clip(q[..., 1], -LINK_H, LINK_H)
    d2 = length(np.stack([a, q[..., 1] - y], -1))     # 고리 판 안에서 곧은 변 · 반원까지
    return length(np.stack([d2 - LINK_R, n], -1)) - LINK_W
def hang(p, top, count, first_turn=False):   # top(걸린 자리)에서 곧장 아래로 늘어진 사슬
    d = np.full(p.shape[:-1], 9.0, np.float32)
    for i in range(count):
        c = top - V(0, LINK_HALF - 2 * LINK_W + LINK_PITCH * i, 0)
        d = np.minimum(d, link(p, c, (i % 2 == 1) != first_turn))
    return d
def chain(p, j=J):   # 목줄 맨 아래에 걸려 곧장 늘어진 사슬 네 고리(다섯이면 허리띠에 닿는다)(몸을 따라가지 않는다)
    return hang(p, CF + V(0, 0.010, 0), 4, first_turn=True)   # 첫 고리는 목줄 띠를 꿰도록 옆으로 선다
def belly(p, j=J):   # 밝은 배 — 살과 같은 면에 얇게 깔아 판처럼 안 뜨게 · 가슴에서 배로 좁아진다
    h = j['Hip']; q = bend(p, j)
    region = ell(q, V(0, h + 0.30, 0.34), V(0.27, 0.34, 0.22))
    return smax(torso(p, j) - 0.002, region, 0.06)
def loin(p, j=J):
    h = j['Hip']
    yc = h + 0.06
    flat = p.copy(); flat[..., 1] = yc                                   # 띠 가운데 높이의 허리 둘레를 곧게 세운다(비탈을 따라 쟁반처럼 안 벌어진다)
    waist = np.minimum(torso(flat, j), both(thigh)(flat))              # 허벅지 윗살이 허리까지 올라온다 — 같이 감아야 옆 · 앞이 안 묻힌다
    belt = smax(np.abs(waist - 0.020) - 0.020,                          # 허리 살 위에 감긴다 — 처진 배가 위를 살짝 덮는다
                np.abs(p[..., 1] - yc) - 0.05, 0.01)
    hem = h - 0.22 + 0.03 * np.sin(p[..., 0] * 40 + 1.0)
    front = smax(rbox(p, V(0, h - 0.06, 0.27), V(0.20, 0.13, 0.02), 0.015), hem - p[..., 1], 0.01)
    back = smax(rbox(p, V(0, h - 0.06, -0.27), V(0.24, 0.13, 0.02), 0.015), hem - p[..., 1], 0.01)
    return np.minimum(belt, np.minimum(front, back))

def upper(m, j=J):
    m = arm_space(m)
    ax = j['AX']
    arm = seg(m, V(ax, j['ArmTop'] - 0.06, -0.02), V(ax, j['Elbow'], 0.03), 0.19, 0.16)   # 위팔 — 어깨 쪽이 굵고 팔꿈치로 가늘어진다
    mid = (j['ArmTop'] + j['Elbow']) * 0.5 - 0.03
    arm = smin(arm, ell(m, V(ax, mid, 0.06), V(0.15, 0.14, 0.13)), 0.05)    # 이두 — 앞으로 불룩
    arm = smin(arm, ell(m, V(ax, mid + 0.03, -0.05), V(0.15, 0.15, 0.13)), 0.05)   # 삼두 — 뒤로 불룩
    return smin(arm, sph(m, V(ax - 0.06, j['ArmTop'] - 0.10, -0.02), 0.18), 0.08)   # 어깨 머리는 낮게
def fore(m, j=J):
    return fore_raw(arm_space(m), j)   # 아래팔 — 모양은 `fore_raw` 한 곳(장신구가 같은 살을 감는다)
def hand(m, j=J):
    m = arm_space(m)   # 손가락 둘 + 엄지(엄지까지 셋) — 주먹 · 마디마다 따로 서서 갈린다
    ax, w = j['AX'], j['Wrist']
    d = rbox(m, V(ax + 0.01, w - 0.10, 0.06), V(0.115, 0.11, 0.13), 0.09)   # 손바닥 — 손목 밑으로 곧게 매달린다
    f = np.full(m.shape[:-1], 9.0, np.float32)
    for z in (0.12, 0.0):   # 굵은 손가락 둘 — 아래로 곧게 · 끝만 살짝 앞으로
        a1, a2, a3 = V(ax + 0.03, w - 0.16, z), V(ax + 0.035, w - 0.25, z + 0.01), V(ax + 0.03, w - 0.31, z + 0.03)
        f = np.minimum(f, smin(capsule(m, a1, a2, 0.06), capsule(m, a2, a3, 0.055), 0.01))   # 붙이지 않는다 — 사이에 골이 선다
    d = smin(d, f, 0.015)
    thumb = smin(capsule(m, V(ax - 0.07, w - 0.08, 0.15), V(ax - 0.07, w - 0.16, 0.19), 0.05),   # 엄지 — 안쪽 앞에서 곧게 내려온다(갈고리로 안 휜다)
                 capsule(m, V(ax - 0.07, w - 0.16, 0.19), V(ax - 0.06, w - 0.22, 0.20), 0.045), 0.01)
    return smin(d, thumb, 0.02)
def thigh(m, j=J): return seg(m, V(j['LX'], j['Hip'], 0), V(j['LX'], j['Knee'] + 0.02, 0.03), 0.19, 0.165)   # 굵기는 v48 그대로 — 짧고 붙는다(사용자 11-02)
def shin(m, j=J): return shin_raw(m, j)   # 모양은 `shin_raw` 한 곳(발목 고랑이 같은 살을 감는다)
def foot(m, j=J):   # 넓적한 발 · 발가락 셋
    lx = j['LX']
    d = rbox(m, V(lx, 0.09, 0.08), V(0.19, 0.09, 0.24), 0.08)
    for x in (-0.10, 0.0, 0.10):
        d = smin(d, sph(m, V(lx + x, 0.06, 0.31), 0.06), 0.03)
    return d


# ── 고르는 장신구 — 축마다 한 벌(C#에서는 `{관절}.Pick.{축}.{벌}` · 양팔 · 양발은 한 축) ──
PICK = dict(Neck='Collar', Wrist='Shackle', Ankle='Shackle')   # Neck: Collar · Bones · Off / Wrist: Shackle · Wrap · Off / Ankle: Shackle · Off
BF = V(0, J['Top'] - 0.58, 0.50)   # 뼈 목걸이 앞 — 목줄보다 느슨해 가슴까지 처진다
BB = V(0, J['Top'] + 0.06, 0.18)   # 뼈 목걸이 뒤 — 목 뒤(v52 — 둔덕 꼭대기면 등에 걸친다)
_bn = np.cross(BB - BF, V(1, 0, 0)); BN = (_bn / np.linalg.norm(_bn)).astype(np.float32)
def bone_cord(p, j=J):   # 뼈 목걸이 끈 — 머리 · 목 · 가슴을 감는 가는 끈(뒤는 등 둔덕 속)
    h = ((p - BF) * BN).sum(-1)
    body = np.minimum(head(p, j), torso(p, j))
    return smax(np.abs(body - 0.018) - 0.008, np.abs(h) - 0.010, 0.004)   # v51 — 살 위 .010~.026(얇으면 메시에서 살에 묻힌다)
def cord_y(x):   # 가슴 앞에서 끈이 지나는 높이 — 판과 가슴 면이 만나는 자리를 잰다
    for y in np.linspace(float(BF[1]) - 0.05, float(BF[1]) + 0.25, 300):
        z = chest_front(float(y), x=x)
        if (np.array([x, y, z], np.float32) - BF) @ BN < 0: return float(y), z   # 법선은 아래 · 뒤를 본다
    return float(BF[1]), chest_front(float(BF[1]), x=x)
TEETH = [(x,) + cord_y(x) for x in (-0.13, -0.065, 0.0, 0.065, 0.13)]
TEETH = [(x, y, z + 0.012) for x, y, z in TEETH]
def bone_teeth(p, j=J):   # 끈에 매단 송곳니 넷 + 가운데 큰 뼈 — 아래로 뾰족하게
    d = np.full(p.shape[:-1], 9.0, np.float32)
    for i, (x, y, z) in enumerate(TEETH):
        z = chest_front(y - 0.04, x=x) + 0.034   # 매단 것은 가슴 면에 기댄다 — v51 살 밖으로(반지름 .018 + 끈 높이)
        if i == 2:   # 가운데 — 가로로 누운 뼈(양 끝이 불룩)
            b = capsule(p, V(x - 0.045, y - 0.035, z), V(x + 0.045, y - 0.035, z), 0.013)
            b = smin(b, np.minimum(sph(p, V(x - 0.05, y - 0.03, z), 0.02), sph(p, V(x + 0.05, y - 0.03, z), 0.02)), 0.01)
            d = np.minimum(d, smin(b, capsule(p, V(x, y, z), V(x, y - 0.03, z), 0.006), 0.006))
        else:
            d = np.minimum(d, seg(p, V(x, y - 0.005, z), V(x * 1.05, y - 0.085, z + 0.02), 0.018, 0.003))
    return d

def fore_raw(m, j=J):   # 아래팔(팔 좌표) — 팔꿈치 쪽 근육이 굵고 손목으로 가늘어진다 · 장신구도 이 살을 감는다
    ax = j['AX']
    d = seg(m, V(ax, j['Elbow'], 0.03), V(ax, j['Wrist'] + 0.06, 0.06), 0.165, 0.12)
    return smin(d, ell(m, V(ax + 0.01, j['Elbow'] - 0.10, 0.04), V(0.168, 0.16, 0.16)), 0.08)   # 팔뚝 위쪽 — 살짝만 볼록하고 손목으로 매끈하게 준다
def cuff(m, j=J):   # 손목 쇠고랑 — 팔뚝 살 위에 감긴 두꺼운 띠 + 아래로 늘어진 끊어진 사슬 세 고리
    m = arm_space(m); w = j['Wrist']
    band = smax(np.abs(fore_raw(m, j) - 0.019) - 0.019, np.abs(m[..., 1] - (w + 0.05)) - 0.04, 0.01)   # 진짜 손목 자리
    eye_c = V(j['AX'] + 0.168, w + 0.012, 0.06)                         # 걸쇠 — 팔찌 바깥 아래에 붙은 작은 고리(사슬이 여기 걸린다)
    eye = torus(np.stack([m[..., 0] - eye_c[0], m[..., 1] - eye_c[1], m[..., 2] - eye_c[2]], -1)[..., [0, 2, 1]], V(0, 0, 0), 0.018, 0.007)
    band = np.minimum(band, eye)
    links = hang(m, eye_c - V(0, 0.014, 0), 3, first_turn=True)        # 첫 고리가 걸쇠를 꿰도록 옆으로 선다   # 끊어진 사슬 세 고리 — 고랑 아래 바깥쪽에 걸려 늘어진다
    return np.minimum(band, links)
def wrap(m, j=J):   # 손목 가죽 끈 — 팔뚝을 비스듬히 감은 띠(골이 비스듬히 돈다)
    m = arm_space(m); w = j['Wrist']; ax = j['AX']
    ang = np.arctan2(m[..., 2] - 0.05, m[..., 0] - ax)
    ridge = 0.022 + 0.007 * np.cos(55 * m[..., 1] + 2 * ang)   # v51 — 골에서도 살 위 .015(전엔 .003까지 내려가 묻혔다)
    sh = fore_raw(m, j) - ridge
    return smax(sh, np.abs(m[..., 1] - (w + 0.09)) - 0.07, 0.01)
def shin_raw(m, j=J): return seg(m, V(j['LX'], j['Knee'], 0.03), V(j['LX'], j['Ankle'] + 0.08, 0.02), 0.165, 0.15)
def anklet(m, j=J):   # 발목 쇠고랑
    a = j['Ankle']
    return smax(np.abs(shin_raw(m, j) - 0.015) - 0.015, np.abs(m[..., 1] - (a + 0.11)) - 0.035, 0.008)   # 발목 자리 — 발등 바로 위   # 얇게 — 두 다리가 붙어 있어 두꺼우면 한 막대로 맞닿는다

def both(f): return lambda p: np.minimum(f(side(p, 1)), f(side(p, -1)))

SKIN = (0.50, 0.60, 0.68); BELLY = (0.58, 0.66, 0.70); DARK = (0.06, 0.04, 0.04); TOOTH = (0.86, 0.82, 0.62)
CLOTH = (0.36, 0.28, 0.20); METAL = (0.45, 0.45, 0.48)

def pieces():
    return [(torso, SKIN), (head, SKIN), (eyes, (0.86, 0.82, 0.66)), (irises, (0.55, 0.70, 0.82)), (caruncle, (0.80, 0.55, 0.55)), (pupils, DARK), (mouth, DARK), (teeth, TOOTH), (belly, BELLY),
            (loin, CLOTH),
            (both(upper), SKIN), (both(fore), SKIN), (both(hand), SKIN), (both(thigh), SKIN), (both(shin), SKIN), (both(foot), SKIN)] + picked()
BONE = (0.86, 0.82, 0.68); HIDE = (0.42, 0.30, 0.22)
def picked():
    out = []
    if PICK['Neck'] == 'Collar': out += [(collar, METAL), (chain, METAL)]
    if PICK['Neck'] == 'Bones': out += [(bone_cord, HIDE), (bone_teeth, BONE)]
    if PICK['Wrist'] == 'Shackle': out += [(both(cuff), METAL)]
    if PICK['Wrist'] == 'Wrap': out += [(both(wrap), HIDE)]
    if PICK['Ankle'] == 'Shackle': out += [(both(anklet), METAL)]
    return out

if __name__ == '__main__':
    out = sys.argv[1]; P = pieces()
    tiles = [render(P, a, 300, 380, 0.95, 2.35) for a in (0, 30, 90, 180)]
    sh = V(0, 0, HEAD_SHIFT + 0.30)
    PF = [((lambda p, f=f: f(p + sh)), c) for f, c in P]
    face = [render(PF, a, 240, 240, J['Top'] - 0.1, 0.6) for a in (0, 35, 90)]
    W, H = tiles[0].size
    sheet = Image.new('RGB', (W * 4, H + 240), (40, 40, 44))
    for i, tl in enumerate(tiles): sheet.paste(tl, (i * W, 0))
    for i, tl in enumerate(face): sheet.paste(tl, (i * 240 + 60, H))
    sheet.save(out); print('saved', out)
