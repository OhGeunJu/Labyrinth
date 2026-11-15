"""리자드맨(2층 지저늪) — 사람 치비 몸(`dwarf/body.py` `dims` = C# `ChibiPawnCreator.Proto`)을 굽히고 꼬리 · 짐승 다리 · 빚은 머리를 얹는다.
종류 넷(사용자 11-11: *"머리는 세 개 다"* → 뱀은 실제 뱀 둘로 — *"도마뱀을 잘해서 뱀도 현실감 있게"*):
  lizard  도마뱀 — 납작한 쐐기 머리 · 눈 위에서 뒤통수로 도는 가시 테(볏도마뱀붙이 — 사용자 그림 6) · 큰 눈
  python  비단뱀 — 길고 낮은 머리 · 네모진 주둥이 · 구슬 눈 · 입술의 노란 줄 · 매끈한 검푸른 몸(사용자 그림 8)
  viper   숲살모사 — 짧고 넓은 세모 머리 · 가는 목 · 큰 눈 · 온몸에 일어선 비늘(사용자 그림 7 · 9)
  croc    악어   — 덩치가 크다(몸도 따로 굵다) · 넓고 무거운 턱 · 눈 위 굵은 턱 · 큰 송곳니 · 골판
눈은 고르기 축이다(사용자: *"뾰족한 눈 · 귀여운 눈 · 그 중간으로 넓게"*) — 아래 `EYE` 표.
몸 — 엘프 비례(키를 키워 머리와 비율을 맞춘다) · **굽은 등**(트롤과 같은 식 — 허리 위로 높이의 제곱만큼 앞으로) · 머리는 그 앞에 처져 달린다 ·
다리는 **짐승 다리**(무릎 앞 · 뒤꿈치 들림 · 발끝으로 선다). 뼈대는 사람 것 그대로 두고 모양만 그렇게 빚는다 — 정강이 뼈가 정강이 + 발등을 한 덩이로 든다.
단위 m · 발이 원점 · +z 앞. 머리 부품은 머리 중심 원점.
실행: python lizard.py renders/lizard_v9 [full|heads|eyes|crest|snakes] — snakes는 뱀 둘의 색 여덟 줄(2분)"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.join(_here, '..', 'common')); _sys.path.insert(0, _os.path.join(_here, '..', 'dwarf'))
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg
import head_base as hb
import body as bd

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def union(ds):
    out = ds[0]
    for d in ds[1:]: out = np.minimum(out, d)
    return out
def bounded(f, p, c, r):
    """c 둘레 r 밖은 경계까지의 거리만 — 비싼 조각을 그 근처에서만 돈다."""
    b = length(p - c) - r
    m = b < 0.03
    if not m.any(): return b
    out = b.copy(); out[m] = f(p[m]); return out
def squash(p, y0, sy):
    """y0을 기준으로 세로를 sy배 눌러 본다 — 돌려준 점으로 잰 거리에 sy를 곱해 쓴다."""
    q = p.copy(); q[..., 1] = y0 + (p[..., 1] - y0) / sy; return q
def thin(p, sx):
    q = p.copy(); q[..., 0] = p[..., 0] / sx; return q
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def cone(p, a, b, r): return seg(p, V(*a), V(*b), r, 0.003)
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
UP = np.array([0, 1, 0], np.float32)

class Leaves:
    """뒤로 누워 겹친 잎 비늘 한 묶음(숲살모사 — 사용자 11-12: *"가시는 징그럽다 · 사진과 안 닮았다"*) — 곤두선 가시가 아니라
    살에 누운 납작한 잎이고, 끝만 살짝 들려 앞줄이 뒷줄 밑동을 덮는다(솔방울). 좌우 대칭 — 받는 점은 |x|로 접어 잰다.
    색은 밑동이 어둡고 끝이 밝다(`tint`) — 겹친 잎이 한 장씩 읽힌다."""
    def __init__(s, flat=0.36): s.L = []; s.flat = flat; s.c = None
    def add(s, base, back, n, ln, wd, lift=0.30):
        n = nrm(n); back = np.asarray(back, np.float32); back = nrm(back - n * float(np.dot(back, n)))
        d = nrm(back + n * lift); sd = nrm(np.cross(n, d)); up = np.cross(d, sd)
        s.L.append((np.asarray(base, np.float32) - d * (wd * 0.25), d, sd, up, float(ln), float(wd)))
    def bound(s):
        pts = np.array([b + d * (ln * 0.5) for b, d, _, _, ln, _ in s.L], np.float32)
        s.c = (pts.min(0) + pts.max(0)) * 0.5; s.r = float(length(pts - s.c).max()) + max(l[4] for l in s.L)
    def _eval(s, p):
        m = mir(p); d = far(p); t = np.zeros(p.shape[:-1], np.float32)
        for b, dr, sd, up, ln, wd in s.L:
            q = m - b
            bd_ = length(q - dr * (ln * 0.5)) - (ln * 0.5 + wd * 0.5)
            near = bd_ < 0.02
            if not near.any(): d = np.minimum(d, bd_); continue
            qn = q[near]; u = (qn * dr).sum(-1); v = (qn * sd).sum(-1); w = (qn * up).sum(-1) / s.flat
            hh = np.clip(u / ln, 0, 1); r = 0.005 + (wd * 0.5 - 0.005) * (1 - hh) ** 0.75   # 둥근 밑동 → 뾰족한 끝
            e = (np.sqrt((u - hh * ln) ** 2 + v * v + w * w) - r) * s.flat * 1.6
            dn = d[near]; better = e < dn
            tn = t[near]; tn[better] = hh[better]; t[near] = tn
            d[near] = np.minimum(dn, e)
            far_ = ~near; d[far_] = np.minimum(d[far_], bd_[far_])
        return d, t
    def dist(s, p):
        if s.c is None: s.bound()
        return bounded(lambda q: s._eval(q)[0], p, s.c, s.r)
    def tint(s, dark, light, e0=0.25, e1=0.95):
        """밑동(dark) → 끝(light) — e0~e1에서 넘어간다."""
        D, Lc = np.asarray(dark, np.float32), np.asarray(light, np.float32)
        def c(p):
            k = sstep(e0, e1, s._eval(p)[1])[..., None]
            return D * (1 - k) + Lc * k
        return c
def grad(f, c, e=0.004):
    c = np.asarray(c, np.float32)
    return nrm([float(f((c + np.array(v, np.float32) * e)[None, :])[0] - f((c - np.array(v, np.float32) * e)[None, :])[0]) for v in ([1, 0, 0], [0, 1, 0], [0, 0, 1])])
def surf(f, a, dr, hi=0.6):
    """a(살 속)에서 dr 쪽으로 나가 살 겉면을 만나는 점 — 살 밖에서 시작하면 None."""
    a = np.asarray(a, np.float32); dr = nrm(dr)
    if float(f(a[None, :])[0]) > 0: return None
    lo = 0.0
    for _ in range(22):
        mid = (lo + hi) * 0.5
        if float(f((a + dr * mid)[None, :])[0]) > 0: hi = mid
        else: lo = mid
    return a + dr * lo

AMBER = (0.95, 0.72, 0.16); RED = (0.86, 0.16, 0.12); GREEN = (0.62, 0.82, 0.25); PALE = (0.80, 0.87, 0.92); BLACK = (0.05, 0.05, 0.06)
GREY = (0.62, 0.58, 0.52); YELLOW = (0.93, 0.80, 0.20)
BONE = (0.90, 0.86, 0.72); DARK = (0.06, 0.05, 0.05); MOUTH = (0.30, 0.07, 0.08); TONGUE = (0.20, 0.05, 0.08); WHITE = (0.96, 0.96, 0.96)
LEATHER = (0.34, 0.22, 0.13); CLOTH = (0.42, 0.14, 0.11)

# ── 몸 — 종류마다 체형 한 줄. 악어만 덩치가 크다 ─────────────────────────────────────────────────────────
#          이름      반폭(밑 엉덩이 허리 가슴 어깨)          깊이  손    팔K   다리K  몸통K 팔굵기 다리굵기 머리 어깨둥글림
LIZARD = ('Lizard', [0.23, 0.26, 0.27, 0.33, 0.35], 0.88, 0.32, 1.12, 1.37, 1.05, 1.15, 1.12, 1.0, 0.42)
CROC   = ('Croc',   [0.29, 0.33, 0.36, 0.44, 0.47], 1.02, 0.39, 1.12, 1.30, 1.12, 1.45, 1.32, 1.0, 0.34)
# **공룡 악어**(`brute` — 11-14 사용자: *"악어 버전을 좀 더 공룡 느낌으로 · 덩치가 컸으면 · 일반 리자드맨이랑 따로 빼서"* — 그림은 깃 밖
# `_refs/2026-11-14_lizard_Croc/`). 리자드맨과 다른 큰 종족으로 갈 몸이다 — 더 굽은 등 · 넓은 어깨 · 굵은 팔뚝과 큰 손 · 굵고 짧은 다리 · 굵은 꼬리.
# 몸 전체를 `BRUTE_S`배로 키워 짓는다(크기는 메시가 든다 — 엔트와 같다).
BRUTE  = ('Brute',  [0.30, 0.33, 0.33, 0.50, 0.58], 0.90, 0.42, 1.15, 1.95, 1.10, 1.40, 1.05, 1.0, 0.30)   # 사용자가 고쳐 그린 비율 · 굵은 팔다리 · **다리를 더 길게**(v12 — 다리K 1.70 → 1.95 · 몸통은 그대로 — v11은 골반을 넓혀 허리가 굵어졌다)
BRUTE_S = 1.45
#           체형     머리 배수  굽힘    목 굵기  꼬리 굵기
BODY = {'brute':  (BRUTE,  0.95, 0.42, 0.250, 1.50),
        'lizard': (LIZARD, 1.00, 0.30, 0.150, 1.00),
        'python': (LIZARD, 1.00, 0.30, 0.160, 1.10),
        'viper':  (LIZARD, 1.00, 0.30, 0.122, 0.95),
        'croc':   (CROC,   1.16, 0.34, 0.215, 1.38)}
HEAD_FWD, HEAD_DROP = 0.17, 0.17   # 머리가 굽은 어깨보다 앞으로 · 아래로
HEAD_POSE = {'brute': (0.30, 0.12)}   # 종류마다 다른 머리 자리(앞으로 · 아래로) — 공룡 악어는 굵은 목 끝에 머리가 낮게 앞으로 나온다

NECK_TAPER = {'brute': 0.78}   # 목이 머리 쪽으로 가늘어지는 비(없으면 1) — 공룡 악어는 머리가 작아 목이 머리통보다 굵으면 얼굴이 목에 묻힌다(v6 · 사용자)
def set_body(kind):
    """그 종류의 몸 수치를 모듈 전역에 세운다 — 아래 몸 함수들이 읽는다(한 프로세스가 한 그림만 그린다)."""
    global D, HK, LEAN, NECK_R, TAIL_K, Y0, TOP, HO, TAIL, TAIL_R, _TZ, _TY, _TR, NECK_TIP
    NECK_TIP = NECK_TAPER.get(kind, 1.0)
    shape, HK, LEAN, NECK_R, TAIL_K = BODY[kind]
    D = bd.dims(shape); TOP = D['top']; Y0 = D['lift'] + 0.22 * D['torsoH']
    fwd, drop = HEAD_POSE.get(kind, (HEAD_FWD, HEAD_DROP))
    HO = V(0, TOP + bd.HeadGap + hb.H * 0.5 - drop, bd.HeadZ + float(zs(TOP)) + fwd)
    # 꼬리 — 엉덩이에서 뒤로 내려가 땅 가까이에서 눕는다. 3차 곡선 열두 마디 · 굵기는 뿌리에서 끝까지 한 번에 준다.
    tp = np.array([[D['lift'] + 0.115, -0.08], [D['lift'] - 0.02, -0.48 * TAIL_K ** 0.5], [0.11 * TAIL_K, -0.78 * TAIL_K ** 0.5], [0.10 * TAIL_K, -1.26 * TAIL_K ** 0.5]], np.float32)
    t = np.linspace(0, 1, 13)
    b = ((1 - t) ** 3)[:, None] * tp[0] + (3 * (1 - t) ** 2 * t)[:, None] * tp[1] + (3 * (1 - t) * t ** 2)[:, None] * tp[2] + (t ** 3)[:, None] * tp[3]
    TAIL = [V(0, y, z) for y, z in b]; TAIL_R = [0.168 * TAIL_K * (1 - u) ** 1.25 + 0.006 for u in t]
    _TZ = np.array([-z for _, z in b], np.float32); _TY = np.array([y for y, _ in b], np.float32); _TR = np.array(TAIL_R, np.float32)

def zs(y):   # 굽은 등 — 허리(Y0) 위로 올라갈수록 앞으로(높이의 제곱 · 트롤과 같은 식)
    return LEAN * np.maximum(y - Y0, 0) ** 2
def bend(p):   # 굽기 전 좌표로
    q = p.copy(); q[..., 2] = p[..., 2] - zs(p[..., 1]); return q

def mir(p): return np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
def torso(p):
    b = bend(p)
    y = b[..., 1] - D['lift']; t = y / D['torsoH']
    r = bd.torso_r(D['half'], t, D['torsoH'], D['topR'])
    q = np.sqrt(b[..., 0] ** 2 + (b[..., 2] / D['depth']) ** 2)
    return np.maximum((q - r) * D['depth'], np.maximum(-y, y - D['torsoH'])) * 0.9
def back_z(y):
    t = (y - D['lift']) / D['torsoH']; return -float(bd.torso_r(D['half'], t, D['torsoH'], D['topR'])) * D['depth'] + float(zs(y))
def neck(p):   # 굽은 어깨에서 앞 · 위로 뻗어 머리통 뒤 밑으로
    return seg(p, V(0, TOP - 0.08, float(zs(TOP)) - 0.05), HO + V(0, -0.07, -0.13) * HK, NECK_R * 1.18, NECK_R * NECK_TIP)
def arm_z(): return float(zs(D['shY']))
def arms(p): return capsule(mir(p), V(D['armX'], D['handY'], arm_z() + 0.04), V(D['armX'], D['shY'], arm_z()), D['armR'])
def hand_c(): return V(D['armX'], D['handY'] - D['hand'] * 0.25, arm_z() + 0.04)
def hands(p): return sph(mir(p), hand_c(), D['hand'] * 0.5)
def hand_claws(p):
    q = mir(p); c = hand_c(); r = D['hand'] * 0.5
    return union([cone(q, (c[0] + dx, c[1] - r * 0.55, c[2] + 0.045), (c[0] + dx * 1.3, c[1] - r * 1.35, c[2] + 0.085), 0.022 * r / 0.118) for dx in (-0.05, 0.0, 0.05)])

# 짐승 다리 — 엉덩이 → 무릎(앞) → 뒤꿈치(뒤 · 들림) → 발볼(땅). 허벅지는 굵고 아래로 가늘어진다.
def leg_pts():
    x, L, r = D['legX'], D['lift'], D['legR']
    return (V(x, D['hipY'], 0.0), V(x, L * 0.66, 0.135), V(x, L * 0.30, -0.105), V(x, 0.060, 0.055)), r
def legs(p):
    (h, k, a, b), r = leg_pts(); q = mir(p)
    d = seg(q, h, k, r * 1.22, r * 1.02)
    d = smin(d, seg(q, k, a, r * 0.98, r * 0.66), 0.035)
    return smin(d, seg(q, a, b, r * 0.66, r * 0.70), 0.03)
TOES = (-0.075, 0.0, 0.075)
def feet(p):   # 발끝으로 선다 — 발볼에서 벌어진 발가락 셋
    (_, _, _, b), r = leg_pts(); q = mir(p); k = r / 0.106
    pad = ell(q, b + V(0, -0.005, 0.02), V(0.105 * k, 0.055, 0.10 * k))
    toes = union([seg(q, b + V(dx * 0.6 * k, -0.012, 0.05), b + V(dx * 1.4 * k, -0.02, 0.215 * k), 0.042 * k, 0.032 * k) for dx in TOES])
    return smin(pad, toes, 0.03)
def foot_claws(p):
    (_, _, _, b), r = leg_pts(); q = mir(p); k = r / 0.106
    return union([cone(q, b + V(dx * 1.4 * k, -0.018, 0.21 * k), b + V(dx * 1.6 * k, -0.045, 0.30 * k), 0.026 * k) for dx in TOES])

def tail(p):
    d = seg(p, TAIL[0], TAIL[1], TAIL_R[0], TAIL_R[1])
    for i in range(1, len(TAIL) - 1): d = smin(d, seg(p, TAIL[i], TAIL[i + 1], TAIL_R[i], TAIL_R[i + 1]), 0.02)
    return d
SPINE_GAP = 0.030   # 가시 사이 — 머리 가시 테 · 목 · 등 · 꼬리가 다 같다(11-14 사용자: *"꼬리 · 등 · 머리 쪽 뿔 간격이 너무 다르다"*)
def fringe_rows():
    """가시 테가 이어지는 등의 가시 두 줄(볏도마뱀붙이 · 11-14 사용자: *"등이랑 이어지는 뿔들도 두 갈래짜리가 아니라 다른 한 줄"*) —
    머리 가시 테 끝(뒤통수 옆 아래)에서 목 뒤를 지나 등 양쪽으로, 꼬리 윗면 양옆을 따라 끝까지(*"꼬리까지 쭉"*). 오른쪽(+x)만 적고 거울로 쓴다.
    줄을 촘촘히 짚어 겉면 위의 길을 만든 뒤 그 길을 따라 `SPINE_GAP`마다 하나씩 놓는다. (밑동, 끝, 굵기) 목록."""
    body = lambda q: smin(torso(q), neck(q), 0.05)
    skin_tail = lambda q: smin(torso(q), tail(q), 0.06)
    na, nb = HO + V(0, -0.07, -0.13) * HK, V(0, TOP - 0.08, float(zs(TOP)) - 0.05)   # 목 축(머리 쪽 → 어깨)
    guide = []   # (속 점, 나가는 쪽, 크기, 눕는 쪽, 겉면 함수)
    for t in np.linspace(0.15, 1.0, 14):                                 # 목
        guide.append((na + (nb - na) * t, nrm([0.78, 0.15, -0.60]), 1.0, None, body))
    for y in np.linspace(TOP - 0.12, D['lift'] + 0.28, 24):             # 등 — 엉덩이로 갈수록 작다
        guide.append((V(0, y, float(zs(y))), nrm([0.62, 0.05, -0.78]), 0.55 + 0.45 * (y - D['lift']) / D['torsoH'], None, body))
    for i in range(1, len(TAIL) - 1):                                    # 꼬리 — 윗면 양옆 · 꼬리 끝 쪽으로 눕는다
        for u in np.linspace(0, 1, 6, endpoint=False):
            c = TAIL[i] + (TAIL[i + 1] - TAIL[i]) * u; r = TAIL_R[i] + (TAIL_R[i + 1] - TAIL_R[i]) * u
            ax = nrm(TAIL[min(i + 1, len(TAIL) - 1)] - TAIL[i - 1]); up = nrm(np.cross(np.cross(ax, UP), ax)); side = nrm(np.cross(up, ax))
            if side[0] < 0: side = -side
            guide.append((c, nrm(up * 0.70 + side * 0.72), 0.35 + 0.65 * r / TAIL_R[0], ax, skin_tail))
    path = []   # 겉면 위의 길 — (점, 나가는 쪽, 크기, 눕는 쪽)
    for a, dr, k, lay, f in guide:
        b = surf(f, a, dr)
        if b is not None: path.append((b, dr, k, lay))
    out, acc = [], SPINE_GAP * 0.5
    for (b0, d0, k0, l0), (b1, d1, k1, l1) in zip(path[:-1], path[1:]):
        L = float(np.linalg.norm(b1 - b0))
        while acc <= L:
            t = acc / max(L, 1e-6); b = b0 + (b1 - b0) * t; dr = nrm(d0 + (d1 - d0) * t); k = k0 + (k1 - k0) * t
            lay = l0 if l0 is not None else l1
            tip = nrm(dr * 0.70 + lay * 0.65) if lay is not None else nrm(dr * 0.75 + V(0, 0.15, -0.55))   # 꼬리는 꼬리 끝으로 · 몸은 뒤로 눕는다
            out.append((b - dr * 0.006, b + tip * (0.040 * k), 0.016 * k))
            acc += SPINE_GAP
        acc -= L
    return out
FROWS = None
def torso_rad(y):   # 그 높이의 몸통 반지름(굽기 전)
    return float(bd.torso_r(D['half'], (y - D['lift']) / D['torsoH'], D['torsoH'], D['topR']))
def brute_muscles(p):
    """몸통의 살 — **덩이를 붙이지 않는다**(v5는 가슴근 두 덩이가 여자 가슴 같았다 — 사용자: *"몸은 자연스럽게 · 억지로 근육 붙이니깐"*).
    가슴 전체가 하나의 두꺼운 통으로 부풀고(가운데 하나 · 넓게 녹인다) 목에서 어깨로 승모근이 완만하게 흐른다."""
    q = mir(p)
    y1 = TOP - 0.24; chest = ell(p, V(0, y1, float(zs(y1)) + torso_rad(y1) * D['depth'] * 0.30), V(torso_rad(y1) * 1.02, 0.24, torso_rad(y1) * D['depth'] * 0.92))
    sh = brute_arm_pts()[0]   # 승모근 — 목 옆에서 높이 솟아 올라간 어깨로 흐른다(트롤처럼 — 사용자: *"승모근을 더 올리고 어깨 위치를 더 올려도"*). 머리보다 뒤에 선다
    trap = seg(q, V(0.11, TOP + 0.14, float(zs(TOP)) - 0.08), V(float(sh[0]) - 0.04, float(sh[1]) + 0.07, float(sh[2]) - 0.02), 0.165, 0.115)   # v10 — 더 크고 높게(사용자: *"승모근 더 키워봐"*)
    return smin(chest, trap, 0.08)
BRUTE_SH_UP = 0.09   # 어깨 관절을 이만큼 올린다(트롤처럼 솟은 어깨)
def brute_arm_pts():
    az = arm_z(); r = D['armR']
    sh = V(D['armX'], D['shY'] + BRUTE_SH_UP, az); wr = V(D['armX'] + 0.025, D['hem'] + 0.03, az + 0.15)   # 손목이 허리띠 높이 · 조금 앞
    el = sh + (wr - sh) * 0.50 + V(0.050, 0.0, -0.075)   # 팔꿈치 — 뒤 · 바깥(힘을 뺀 팔이 살짝 굽어 늘어진다)
    return sh, el, wr, r
def brute_arms(p):
    """굵은 팔 — **한 줄기로 흐른다**(v9는 삼각근 · 이두 · 삼두 · 팔뚝 덩이가 따로 불거져 울퉁불퉁했다 — 사용자: *"팔 좀 더 자연스럽게"*):
    둥근 어깨 → 팔꿈치로 가늘어지는 위팔 → 다시 굵어지는 팔뚝 → 손목. 살 덩이는 둘만(위팔 앞 · 팔뚝) 넓게 녹인다."""
    q = mir(p); sh, el, wr, r = brute_arm_pts(); lu = float(np.linalg.norm(el - sh)); ll = float(np.linalg.norm(wr - el))
    d = seg(q, sh, el, r * 1.04, r * 0.84)
    d = smin(d, seg(q, el, wr, r * 0.84, r * 0.66), 0.06)                                                # 아래팔 뼈대
    d = smin(d, ell(q, sh + V(0.02, 0.015, 0.0), V(r * 1.26, r * 1.12, r * 1.26)), 0.10)                # 둥근 어깨
    d = smin(d, ell(q, sh + (el - sh) * 0.50 + V(0, 0, r * 0.20), V(r * 0.88, lu * 0.40, r * 0.92)), 0.09)   # 위팔 앞의 살
    return smin(d, ell(q, el + (wr - el) * 0.34 + V(r * 0.05, 0, r * 0.04), V(r * 1.02, ll * 0.40, r * 1.02)), 0.09)   # 팔뚝
BRUTE_HAND_TURN = -35.0   # 손을 세로축으로 돌리는 각(도) — 0이면 손바닥이 몸 쪽을 곧게 본다 · 음수면 손바닥이 몸 쪽 · 뒤로 비스듬히(힘을 뺀 손 · 정면에서 손가락이 어긋나 보인다 · v16)
BRUTE_PALM_T = 0.48      # 손바닥 두께(반폭 × 손 반지름) — v15 0.34는 너무 얇았다
def _hand_rot():
    a = np.radians(BRUTE_HAND_TURN); ca, sa = np.cos(a), np.sin(a)
    return np.array([[ca, 0, sa], [0, 1, 0], [-sa, 0, ca]], np.float32)   # 손 좌표 → 몸 좌표(세로축 둘레)
def brute_fingers():
    """손가락 셋 + 엄지(오른손 · 거울로 쓴다 · +x가 바깥) — 저마다 (밑동, 첫 마디, 둘째 마디, 끝). **손바닥이 몸 쪽(허벅지)을 본다**(v15 — 사용자:
    *"그냥 손바닥이 몸 쪽을 보게 해 놓으면 안 됐던 거야?"*): 손가락은 앞뒤로 나란히 서고 몸 쪽(손바닥 쪽)으로 살짝 말린다 · 가운데가 조금 길다 ·
    엄지는 손 앞 가장자리에서 손바닥 쪽으로 붙어 내려온다(앞으로 안 튀어나온다)."""
    _, _, wr, _ = brute_arm_pts(); h = D['hand'] * 0.5; c = wr + V(0, -h * 0.60, 0.0)
    R = _hand_rot(); T = lambda v: c + R @ (np.asarray(v, np.float32) - c)   # 손 둘레로 돌린다
    fs = []
    for dz, k in ((-0.60, 0.92), (0.0, 1.06), (0.60, 0.94)):
        a = c + V(0, -h * 0.50, dz * h * 0.60)
        m1 = a + V(-h * 0.10, -h * 0.58 * k, dz * h * 0.10)
        m2 = m1 + V(-h * 0.18, -h * 0.40 * k, dz * h * 0.04)
        fs.append((a, m1, m2, m2 + V(-h * 0.22, -h * 0.26 * k, 0)))
    a = c + V(-h * 0.22, -h * 0.02, h * 0.70); m1 = a + V(-h * 0.22, -h * 0.36, h * 0.04); m2 = m1 + V(-h * 0.10, -h * 0.28, -h * 0.04)
    fs.append((a, m1, m2, m2 + V(-h * 0.04, -h * 0.16, -h * 0.06)))   # 엄지 — 앞 가장자리에서 손바닥 쪽으로
    return c, h, [tuple(T(v) for v in f) for f in fs]
def brute_hands(p):
    q = mir(p); c, h, fs = brute_fingers(); _, _, wr, r = brute_arm_pts()
    ql = c + (q - c) @ _hand_rot()                                                                       # 손 좌표로(돌림을 되돌린다)
    d = ell(ql, c, V(h * BRUTE_PALM_T, h * 0.74, h * 0.84))                                               # 손바닥 — 넓적한 판(몸 쪽을 본다)
    d = smin(d, ell(q, (wr + c) * 0.5, V(r * 0.52, h * 0.55, r * 0.62)), 0.05)                          # 손목 → 손바닥으로 넓어진다
    for a, m1, m2, t in fs:
        f = np.minimum(np.minimum(seg(q, a, m1, h * 0.25, h * 0.22), seg(q, m1, m2, h * 0.22, h * 0.19)), seg(q, m2, t, h * 0.19, h * 0.15))
        d = smin(d, f, 0.03)
    return d
def brute_hand_claws(p):   # 손톱 — 손가락이 말리는 쪽으로 뻗고 끝이 굽는다
    q = mir(p); _, h, fs = brute_fingers(); d = far(p)
    for a, m1, m2, t in fs:
        dr = nrm(t - m2); c1 = t + dr * h * 0.28 + V(0, -h * 0.06, 0)
        d = np.minimum(d, np.minimum(seg(q, t - dr * h * 0.04, c1, h * 0.15, h * 0.09), seg(q, c1, c1 + nrm(dr + V(0, -0.8, 0)) * h * 0.24, h * 0.09, 0.004)))
    return d
BRUTE_LEG_X = 0.215   # 다리 자리 — 엉덩이 폭에서 안 낸다(좁은 허리에 다리가 가운데로 붙어 두 발이 겹쳤다)
def brute_leg_pts():
    """**역관절**(v13 — 사용자: *"다리 역관절을 더 확실하게"*) — 엉덩이(몸통 밑) → 무릎(앞으로 크게) → 뒤꿈치(뒤 · 위로 높이 — 땅에 안 닿는다) →
    발볼(발가락으로 선다). 발등뼈가 뒤꿈치에서 비스듬히 길게 내려온다. v8~12는 앞에서 곧은 기둥으로 보이게 꺾임을 줄였었다."""
    L_, r = D['lift'], D['legR']; x = BRUTE_LEG_X
    return (V(x, L_ - 0.01, 0.0), V(x, L_ * 0.64, 0.240), V(x, L_ * 0.25, -0.150), V(x, 0.062, 0.060)), r   # v14 — 무릎을 더 앞 · 위로, 뒤꿈치를 조금 내려 종아리가 길다
def brute_legs(p):
    (h, k, a, b), r = brute_leg_pts(); q = mir(p); lt = float(np.linalg.norm(k - h)); ls = float(np.linalg.norm(a - k))
    d = seg(q, h, k, r * 1.12, r * 0.94)
    d = smin(d, ell(q, h + (k - h) * 0.50 + V(r * 0.06, 0, r * 0.26), V(r * 0.96, lt * 0.44, r * 1.06)), 0.06)   # 넓적다리 근육(앞으로 — 옆으로 부풀면 두 다리가 붙는다)
    d = smin(d, seg(q, k, a, r * 0.86, r * 0.56), 0.04)                                                # 정강이 — 뒤꿈치로 가늘어진다
    d = smin(d, ell(q, k + (a - k) * 0.32 + V(0, r * 0.10, -r * 0.30), V(r * 0.78, ls * 0.30, r * 0.80)), 0.05)   # 장딴지 — 정강이 뒤
    d = smin(d, sph(q, a, r * 0.60), 0.03)                                                             # 뒤꿈치 마디
    return smin(d, seg(q, a, b, r * 0.56, r * 0.62), 0.03)                                             # 발등뼈 — 비스듬히 길게
# 공룡 발 — 발가락 셋이 **앞을 향해 조금만 벌어진다**(앞에서 보면 발톱 셋이 W로 — 사용자가 고쳐 그린 그림).
# **마디가 있다**(v9는 곧은 막대였다 — 사용자: *"발가락 좀 더 자연스럽게"*): 발등에서 부채꼴로 갈라져 → 가운데 마디가 솟고 → 끝이 땅으로 내려간다.
# 발톱은 발가락 끝 위에서 나와 갈고리처럼 굽어 땅에 닿는다. (밑동 x, 끝 x, 길이 z)
BTOES = ((-0.075, -0.150, 0.225), (0.0, 0.0, 0.265), (0.075, 0.150, 0.225))
def brute_toe_pts():
    (_, _, a, b), r = brute_leg_pts(); out = []
    for x0, x1, z in BTOES:
        base = b + V(x0, 0.004, 0.035); tip = b + V(x1, -0.020, z)
        knuck = base + (tip - base) * 0.52 + V(0, 0.034, 0)   # 가운데 마디 — 솟는다
        out.append((base, knuck, tip))
    return a, b, r, out
def brute_feet(p):
    a, b, r, toes = brute_toe_pts(); q = mir(p)
    d = ell(q, b + V(0, 0.000, -0.005), V(0.120, 0.058, 0.105))                                  # 발볼
    d = smin(d, seg(q, b + V(0, 0.030, -0.03), b + V(0, 0.012, 0.075), 0.085, 0.095), 0.04)      # 발등 — 발가락으로 넓어진다
    for base, kn, tip in toes:
        d = smin(d, np.minimum(seg(q, base, kn, 0.054, 0.048), seg(q, kn, tip, 0.048, 0.038)), 0.035)
    return d
def brute_foot_claws(p):   # 갈고리 발톱 — 발가락 끝 위에서 나와 앞으로 뻗다 굽어 내려온다
    a, b, r, toes = brute_toe_pts(); q = mir(p); d = far(p)
    for base, kn, tip in toes:
        dr = nrm(V(float(tip[0] - kn[0]), 0, float(tip[2] - kn[2])))
        c0 = tip + V(0, 0.016, 0) - dr * 0.015; c1 = c0 + dr * 0.085 + V(0, 0.012, 0); c2 = c1 + dr * 0.070 + V(0, -0.050, 0)
        d = np.minimum(d, np.minimum(seg(q, c0, c1, 0.042, 0.030), seg(q, c1, c2, 0.030, 0.005)))
    return d
def brute_plates():
    """공룡 악어의 골판 — 목 뒤 · 등 · 꼬리 한가운데로 큰 판 한 줄(어깨에서 가장 크고 꼬리 끝으로 작아진다) + 꼬리 양옆 작은 가시.
    **판은 다 등뼈에서 바깥으로 선다**(꼬리 쪽으로 조금만 쓸린다) — v3는 등의 판만 아래로 누워 목 · 꼬리의 판과 방향이 달랐다
    (사용자: *"등에 혼자 반대 방향으로 나온 뿔도 좀 걸려"*). 등 양옆 가시 줄 · 혹 비늘은 걷었다. (한가운데 판, 옆 가시) — 저마다 (밑동, 끝, 굵기)."""
    body = lambda q: smin(torso(q), neck(q), 0.05)
    skin_tail = lambda q: smin(torso(q), tail(q), 0.06)
    na, nb = HO + V(0, -0.07, -0.13) * HK, V(0, TOP - 0.08, float(zs(TOP)) - 0.05)
    mid, side = [], []
    def plate(b, n, lay, ln, r): mid.append((b - n * 0.03, b + n * ln + lay * ln * 0.18, r))
    for t in np.linspace(0.10, 0.95, 4):                                  # 목 뒤
        a = na + (nb - na) * t; n = nrm([0, 0.80, -0.60]); b = surf(body, a, n)
        if b is not None: plate(b, n, nrm([0, -0.5, -0.85]), 0.09 + 0.07 * t, 0.056)
    for i, y in enumerate(np.linspace(TOP - 0.06, D['lift'] + 0.22, 6)):  # 등 — 어깨에서 가장 크다
        a = V(0, y, float(zs(y))); n = nrm([0, 0.45 - 0.07 * i, -1.0]); b = surf(body, a, n)
        if b is not None: plate(b, n, V(0, -1, 0), 0.175 - 0.016 * i, 0.060)
    for i in range(1, len(TAIL) - 1):                                      # 꼬리 — 윗면 한가운데 · 끝으로 작아진다
        ax = nrm(TAIL[i + 1] - TAIL[i - 1]); up = nrm(np.cross(np.cross(ax, UP), ax)); k = TAIL_R[i] / TAIL_R[0]
        b = surf(skin_tail, TAIL[i], up)
        if b is not None: plate(b, up, ax, 0.05 + 0.10 * k, 0.020 + 0.042 * k)
        sd = nrm(np.cross(up, ax)); sd = sd if sd[0] > 0 else -sd
        if 2 <= i < 8:
            dr = nrm(up * 0.75 + sd * 0.66); bs = surf(skin_tail, TAIL[i], dr)
            if bs is not None: side.append((bs - dr * 0.01, bs + nrm(dr + ax * 0.3) * (0.03 + 0.05 * k), 0.012 + 0.022 * k))
    return mid, side
BPLATES = None
def brute_plates_f(p):
    global BPLATES
    if BPLATES is None: BPLATES = brute_plates()
    mid, side = BPLATES; d = far(p)
    q = thin(p, 0.34)                                                      # 판은 옆으로 납작하다
    for a, b, r in mid: d = np.minimum(d, seg(q, a, b, r, 0.008) * 0.34)
    m = mir(p)
    for a, b, r in side: d = np.minimum(d, seg(m, a, b, r, 0.004))
    return d
def fringe_rows_f(p):
    global FROWS
    if FROWS is None: FROWS = fringe_rows()
    m = mir(p); d = far(p)
    for a, b, r in FROWS: d = np.minimum(d, seg(m, a, b, r, 0.003))
    return d
def ridge(p):   # 등줄기에서 꼬리로 낮은 돌기 한 줄 — 끝으로 갈수록 작아지다 사라진다
    d = far(p)
    for i in range(1, 9):
        c, r = TAIL[i], TAIL_R[i]; ax = nrm(TAIL[i + 1] - TAIL[i - 1]); up = nrm(np.cross(np.cross(ax, UP), ax))
        d = np.minimum(d, seg(p, c + up * (r - 0.02), c + up * (r + 0.015 + r * 0.42) + ax * 0.03, 0.022 + r * 0.14, 0.004))
    for y in np.linspace(D['lift'] + 0.22, TOP - 0.08, 5):
        zb = back_z(y); k = TAIL_K ** 0.5
        d = np.minimum(d, seg(p, V(0, y, zb + 0.03), V(0, y + 0.02, zb - 0.075 * k), 0.042 * k, 0.004))
    return d
def body_leaves(lift=1.0):
    """살모사 몸의 잎 비늘 — 등(옆구리까지) · 어깨 · 꼬리 윗면. 아래(꼬리에선 끝) 쪽으로 누워 겹친다. 배 · 팔다리 안쪽은 매끈하다."""
    back, tl = Leaves(), Leaves()
    ln, wd = 0.136, 0.106; y = TOP + 0.005; r = 0
    while y > D['lift'] + 0.06:
        t = (y - D['lift']) / D['torsoH']; rr = float(bd.torso_r(D['half'], min(t, 0.985), D['torsoH'], D['topR'])); z0 = float(zs(y))
        amax = 1.50 if y > D['hem'] + 0.07 else 1.05          # 허리끈 아래는 꼬리 뿌리 둘레만
        for c in range(9):
            a = (c + (0.5 if r % 2 else 0.0)) * wd * 0.90 / max(rr, 0.08)
            if a > amax: break
            pt = V(rr * np.sin(a), y, -rr * D['depth'] * np.cos(a) + z0)
            n = nrm([np.sin(a) * D['depth'], 0.10, -np.cos(a)])
            back.add(pt, [0, -1, -2 * LEAN * max(y - Y0, 0)], n, ln, wd, 0.24 * lift)
        y -= ln * 0.50; r += 1
    def wrap(lv, a, b, ra, rb, out, a0, a1, ln, wd, lift, s0=0.0, s1=1.0):
        """a → b로 가는 굵은 선(팔 · 허벅지 · 목)의 둘레에 줄 맞춰 — out 쪽이 각도 0, 잎은 b 쪽으로 눕는다."""
        a, b = np.asarray(a, np.float32), np.asarray(b, np.float32); L = float(np.linalg.norm(b - a)); ax_ = (b - a) / L
        o = np.asarray(out, np.float32); o = nrm(o - ax_ * float(np.dot(o, ax_))); sd = np.cross(ax_, o)
        u = s0 * L; r = 0
        while u < s1 * L:
            rad = ra + (rb - ra) * u / L; da = wd * 0.90 / rad
            ang = a0 + (0.5 * da if r % 2 else 0.0)
            while ang <= a1:
                n = o * np.cos(ang) + sd * np.sin(ang); pt = a + ax_ * u + n * rad
                fl = V(-1, 1, 1) if pt[0] < 0 else V(1, 1, 1)
                lv.add(pt * fl, ax_ * fl, n * fl, ln, wd, lift)
                ang += da
            u += ln * 0.50; r += 1
    ax, sy, az, ar = D['armX'], D['shY'], arm_z(), D['armR']
    limbs = Leaves()
    for a_ in (-1.9, -1.1, -0.3):   # 어깨 꼭지 한 줄
        n = nrm([np.cos(a_) * 0.75, 0.66, np.sin(a_) * 0.75]); limbs.add(V(ax, sy, az) + n * ar, [n[0], -1, n[2]], n, 0.108, 0.090, 0.20 * lift)
    wrap(limbs, V(ax, sy, az), V(ax, D['handY'] + 0.10, az + 0.04), ar, ar, [1, 0, 0], -2.1, 0.35, 0.108, 0.090, 0.20 * lift, 0.04, 1.0)
    (hp, kn, _, _), lr = leg_pts()
    wrap(limbs, hp, kn, lr * 1.22, lr * 1.02, [1, 0, -0.5], -1.5, 0.75, 0.112, 0.094, 0.20 * lift, 0.30, 0.92)
    nb = HO + V(0, -0.07, -0.13) * HK; na = V(0, TOP - 0.08, float(zs(TOP)) - 0.05)
    wrap(back, nb, na, NECK_R, NECK_R * 1.18, [0, 0.35, -1], 0.0, 1.45, 0.104, 0.086, 0.20 * lift, 0.30, 0.95)
    # 꼬리 — 곡선을 따라 고리마다 엇갈려
    s_ = 0.10
    P = np.array(TAIL, np.float32); seglen = np.linalg.norm(P[1:] - P[:-1], axis=1); cum = np.concatenate([[0], np.cumsum(seglen)]); r = 0
    while s_ < cum[-1] - 0.10:
        i = min(int(np.searchsorted(cum, s_)) - 1, len(P) - 2); f = (s_ - cum[i]) / seglen[i]
        c = P[i] + (P[i + 1] - P[i]) * f; rad = TAIL_R[i] + (TAIL_R[i + 1] - TAIL_R[i]) * f
        ax_ = nrm(P[i + 1] - P[i]); up = nrm(np.cross(np.cross(ax_, UP), ax_)); sd = np.array([1, 0, 0], np.float32)
        l2 = 0.050 + rad * 0.62; w2 = l2 * 0.80
        for cc in range(7):
            a = (cc + (0.5 if r % 2 else 0.0)) * w2 * 0.90 / max(rad, 0.02)
            if a > 1.95: break
            n = up * np.cos(a) + sd * np.sin(a)
            tl.add(c + n * rad, ax_, n, l2, w2, 0.24 * lift)
        s_ += l2 * 0.50; r += 1
    return back, limbs, tl
def belt(p): return np.maximum(torso(p) - 0.014, np.abs(p[..., 1] - (D['hem'] + 0.02)) - 0.042)
def flap(p):   # 앞 가리개 — 뒤는 꼬리가 나와 비운다
    t = (D['hem'] - D['lift']) / D['torsoH']; zf = float(bd.torso_r(D['half'], t, D['torsoH'], D['topR'])) * D['depth']
    return rbox(p, V(0, D['hem'] - 0.155, zf + 0.012), V(0.105 * D['half'][1] / 0.246, 0.185, 0.014), 0.012)
def body_col(skin, belly):   # 배 · 꼬리 밑은 배 색
    S, B = np.asarray(skin, np.float32), np.asarray(belly, np.float32)
    def c(p):
        zr = p[..., 2] - zs(p[..., 1])
        front = sstep(0.02, 0.10, zr) * sstep(0.19, 0.13, np.abs(p[..., 0]) * 0.246 / D['half'][1]) * sstep(D['lift'], D['lift'] + 0.06, p[..., 1])
        ay = np.interp(-p[..., 2], _TZ, _TY); ar = np.interp(-p[..., 2], _TZ, _TR)
        under = sstep(-0.22, -0.34, p[..., 2]) * sstep(-0.15, -0.55, (p[..., 1] - ay) / np.maximum(ar, 0.01)) * sstep(D['lift'] + 0.2, D['lift'] + 0.1, p[..., 1])
        k = np.maximum(front, under)
        return S * (1 - k[..., None]) + B * k[..., None]
    return c

# ── 머리 — 저마다 (머리통 · 위턱 · 아래턱 · 입선 높이 · 눈 자리 · 덧붙이 · 콧구멍)를 낸다 ─────────────────────
G = 0.007   # 입선의 반 틈

class Head:
    def __init__(s, name, skin, belly, eye):
        s.name, s.skin, s.belly, s.eye_default = name, skin, belly, eye
        s.teeth = s.tongue = None

# 도마뱀의 가시 테 — 볏도마뱀붙이처럼(11-14 사용자 사진: *"뿔이 눈을 따라 쭉 내려가잖아 — 지금은 머리 위에 있는 뿔 같다"*).
#   ① 속눈썹 — 눈알 윗선을 따라 앞에서 뒤로 감싼다(가시는 눈 중심에서 바깥으로 · 둑 없음)
#   ② 머리 옆 가장자리 — 눈 뒤에서 뒤통수 옆까지(살은 테두리 둑)
#   ③ 뒤통수 옆에서 아래로 꺾여 목으로 내려간다 — 그대로 등 양쪽 두 줄로 이어진다(`fringe_rows` — 몸)
EYE_C = V(0.172, 0.085, 0.135)                                                                         # 머리의 눈 자리(`lizard_head`)
LASH = [V(0.118, 0.142, 0.212), V(0.168, 0.166, 0.160), V(0.206, 0.160, 0.100), V(0.226, 0.140, 0.052)]   # ① 눈알 윗선
FRINGE = [V(0.226, 0.150, 0.052), V(0.238, 0.178, -0.040), V(0.232, 0.180, -0.140),                      # ② 가장자리
          V(0.212, 0.128, -0.200), V(0.188, 0.050, -0.236), V(0.160, -0.040, -0.262)]                     # ③ 뒤통수 옆에서 아래로
def fringe_bank(p):
    m = mirror(p); d = far(p)
    for a, b in zip(FRINGE[:-1], FRINGE[1:]): d = np.minimum(d, capsule(m, a, b, 0.030))
    return d
def fringe_spines(p):
    m = mirror(p); d = far(p)
    for (a, b), n in zip(zip(LASH[:-1], LASH[1:]), (3, 3, 2)):          # ① 속눈썹 — 짧고 가늘다
        for i in range(n):
            # 바깥(머리 옆)으로 젖혀진다(11-14 사용자: *"속눈썹은 안쪽 말고 바깥쪽으로"*) — 눈에서 나가는 쪽 + 옆 + 조금 위
            c = a + (b - a) * ((i + 0.5) / n); out = nrm(nrm(c - EYE_C) * 0.55 + V(1, 0, 0) * 0.80 + UP * 0.15)
            d = np.minimum(d, seg(m, c - out * 0.004, c + out * 0.030, 0.010, 0.002))
    for a, b in zip(FRINGE[:-1], FRINGE[1:]):                                 # ② · ③ 가장자리 — 바깥 · 뒤로(몸과 같은 간격 `SPINE_GAP`)
        n = max(1, int(round(float(np.linalg.norm(b - a)) * HK / SPINE_GAP)))
        for i in range(n):
            c = a + (b - a) * ((i + 0.5) / n)
            down = sstep(0.17, 0.05, float(c[1]))                               # 내려가는 자리에선 뒤 · 바깥으로 눕는다
            out = nrm(nrm([c[0], 0, (c[2] + 0.05) * 0.5]) * 0.92 + UP * (0.34 - 0.40 * down) + V(0, 0, -0.30 * down))
            ln = 0.034 + 0.014 * np.sin(np.pi * (i + 0.5) / n)
            d = np.minimum(d, seg(m, c + out * 0.012, c + out * (0.026 + ln), 0.015, 0.003))
    return d
def seat_eye(h, depth=0.42, exact=False):
    """눈알을 살 겉면에 앉힌다 — 눈 방향으로 밀어 눈알 중심이 살 속 `depth × 반지름`에 오게(손으로 맞춘 자리가 살에 묻히거나 떴다)."""
    def skin0(p): return smin(smin(h.cran(p), h.extra(p), 0.05), np.minimum(h.upper(p), h.lower(p)), 0.05)
    if exact:   # 눌린 조각(주둥이)은 거리가 작게 나와 눈이 더 깊이 묻힌다 — 눈 방향 선 위에서 겉면을 직접 찾는다
        a = h.eye - h.eye_n * 0.12; pt = surf(skin0, a, h.eye_n)
        if pt is not None: h.eye = (pt - h.eye_n * (depth * h.eye_r)).astype(np.float32); return
    c = h.eye.copy()
    for _ in range(8): c = c - h.eye_n * (float(skin0(c[None, :])[0]) + depth * h.eye_r)   # 살 속이면(음수) 밖으로 민다
    h.eye = c.astype(np.float32)
def comb(c, r):   # 이구아나 볏 — 정수리에서 목 뒤로, 옆으로 납작한 가시
    def f(p):
        q = thin(p, 0.42); d = far(p)
        for i, th in enumerate(np.linspace(8, 128, 9)):
            a = np.radians(th); dr = np.array([0, np.cos(a), -np.sin(a)], np.float32)
            base = np.asarray(c, np.float32) + dr * np.array([0, r[1], r[2]], np.float32)
            ln = 0.050 + 0.065 * np.sin(np.pi * i / 8)
            d = np.minimum(d, seg(q, base - dr * 0.03, base + dr * ln + V(0, 0, -0.035), 0.050, 0.006))
        return d * 0.42
    return f

def lizard_head(crest='fringe'):
    h = Head('lizard', (0.36, 0.50, 0.27), (0.80, 0.76, 0.52), 'bigslit')
    C, R = (0, 0.04, -0.05), (0.245, 0.255, 0.28)
    flat = crest in ('fringe', 'bare')   # 가시 테 머리는 윗면이 납작한 쐐기다 · `bare` — 같은 머리통에 가시만 없다(11-14 사용자: *"저 두개골 그대로 뿔 없는 모습으로도"*)
    h.cran = lambda p: smax(ell(p, V(*C), V(*R)), p[..., 1] - 0.238, 0.05) if flat else ell(p, V(*C), V(*R))
    h.upper = lambda p: seg(squash(p, -0.01, 0.72), V(0, 0.0, 0.08), V(0, -0.035, 0.42), 0.190, 0.105) * 0.72
    h.lower = lambda p: seg(squash(p, -0.10, 0.60), V(0, -0.10, 0.02), V(0, -0.105, 0.39), 0.165, 0.088) * 0.60
    h.mouth_y = lambda p: -0.082 - 0.03 * p[..., 2] + 0.9 * np.clip(0.10 - p[..., 2], 0, 1) ** 2   # 뒤쪽이 올라간다
    h.eye, h.eye_r, h.eye_n = V(0.172, 0.085, 0.135), 0.058, nrm([0.80, 0.12, 0.58])
    def extra(p):
        m = mirror(p)
        cheek = sph(m, V(0.150, -0.065, 0.0), 0.098)
        if flat: return smin(fringe_bank(p), cheek, 0.03)
        return smin(seg(m, V(0.075, 0.120, 0.220), V(0.228, 0.172, 0.01), 0.038, 0.040), cheek, 0.03)
    h.extra = extra
    h.spikes = {'fringe': fringe_spines, 'comb': comb(C, R), 'none': far, 'bare': far}[crest]
    h.spike_col = (0.74, 0.76, 0.50) if flat else (0.24, 0.36, 0.20)
    h.nostril, h.nostril_r = V(0.045, 0.028, 0.452), 0.016
    h.jaw_w = 0.19; h.reach = 0.66
    return h

def top_y(skin0, x, z, lo=-0.06, hi=0.45):
    """(x, z)에서 살 윗면의 높이 — 위에서 아래로 좁혀 찾는다. 비늘을 살 위에 앉힐 때 쓴다."""
    for _ in range(22):
        mid = (lo + hi) * 0.5
        if float(skin0(np.array([[x, mid, z]], np.float32))[0]) > 0: hi = mid
        else: lo = mid
    return (lo + hi) * 0.5

def head_leaves(h, skin0, z0, z1, ln0, ln1, ratio, lift0, lift1, brow_lift=None):
    """머리 윗면 · 옆면에 뒤로 누운 잎 비늘을 줄 맞춰 깐다 — z0(앞)에서 z1(뒤)로 가며 커지고 더 들린다. 눈 둘레 · 입술은 비운다."""
    lv = Leaves(); z = z0; r = 0
    while z > z1:
        t = np.clip((z0 - z) / (z0 - z1), 0, 1); ln = ln0 + (ln1 - ln0) * t; wd = ln * ratio
        top = surf(skin0, V(0, -0.03, z), UP)
        if top is None: break
        rad = max(float(top[1]) + 0.03, 0.06)
        for c in range(12):
            phi = (c + (0.5 if r % 2 else 0.0)) * wd * 0.92 / rad
            if phi > 1.85: break
            pt = surf(skin0, V(0, -0.03, z), [np.sin(phi), np.cos(phi), 0])
            if pt is None or pt[1] < h.mouth_y(pt[None, :])[0] + 0.045: continue
            de = float(np.linalg.norm(pt - h.eye))
            if de < h.eye_r * 1.45: continue
            brow = brow_lift is not None and de < h.eye_r * 2.3 and pt[1] > h.eye[1]   # 눈 위 줄 — 눈썹
            lv.add(pt, [0, 0, -1], grad(skin0, pt), ln * (1.10 if brow else 1.0), wd, brow_lift if brow else lift0 + (lift1 - lift0) * t ** 2)
        z -= ln * 0.46; r += 1
    return lv

# 매끈한 뱀 — 머리는 하나, 색이 넷(사용자 그림: 보엘렌비단뱀 · 누런 구렁이 · 인디고 · 붉은 뱀). 눈은 둥근 눈동자에 테(`ring`).
#            살                  배                  눈 테               무늬 색
SNAKE = {'boelen':  ((0.07, 0.08, 0.11), (0.92, 0.82, 0.42), (0.80, 0.66, 0.26), (0.95, 0.80, 0.16)),
         'kingrat': ((0.90, 0.74, 0.18), (0.96, 0.90, 0.58), (0.56, 0.22, 0.10), (0.06, 0.06, 0.07)),
         'indigo':  ((0.08, 0.09, 0.14), (0.24, 0.27, 0.35), (0.84, 0.46, 0.20), (0.74, 0.30, 0.13)),
         'red':     ((0.80, 0.25, 0.16), (0.95, 0.66, 0.38), (0.10, 0.08, 0.08), (0.07, 0.05, 0.06))}
def line2(a, b, a0, b0, a1, b1):
    """(a, b) 평면에서 선분 (a0, b0)–(a1, b1)까지의 거리 — 칠하는 줄에 쓴다."""
    da, db = a1 - a0, b1 - b0
    t = np.clip(((a - a0) * da + (b - b0) * db) / (da * da + db * db), 0, 1)
    return np.sqrt((a - a0 - t * da) ** 2 + (b - b0 - t * db) ** 2)
def along(p):
    """몸을 따라 내려가는 자 — 몸통에선 높이, 꼬리에선 뒤로 간 거리를 이어 붙인다(띠 무늬가 꼬리에서 꺾이지 않게)."""
    w = sstep(-0.18, -0.36, p[..., 2]) * sstep(D['lift'] + 0.28, D['lift'] + 0.10, p[..., 1])
    return p[..., 1] * (1 - w) + (D['lift'] + 0.14 + p[..., 2]) * w, w

def python_head(crest=None, tone=None):
    """매끈한 뱀 머리 — 길고 낮다 · 윗면이 평평하고 눈 위에서 주둥이로 모서리(눈썹 판)가 선다 · 둥글게 좁아지는 주둥이 · 큰 둥근 눈.
    비늘은 빚지 않는다(매끈한 큰 판) — 종류는 색과 줄이 가른다: 눈 밑으로 내려오는 검은 줄 · 입술 세로 줄 · 정수리 판의 금."""
    tone = tone or 'boelen'; skin, belly, ring, mark = SNAKE[tone]
    h = Head('python', skin, belly, 'ring'); h.eye_col = ring; h.tone = tone
    S = np.asarray(skin, np.float32)
    h.cran = lambda p: smax(ell(p, V(0, 0.0, -0.07), V(0.200, 0.146, 0.27)), p[..., 1] - 0.108, 0.045)
    h.upper = lambda p: smax(seg(squash(p, -0.01, 0.62), V(0, -0.005, 0.03), V(0, -0.022, 0.400), 0.184, 0.120) * 0.62, p[..., 2] - 0.505, 0.05)
    h.lower = lambda p: smax(seg(squash(p, -0.085, 0.42), V(0, -0.085, -0.02), V(0, -0.082, 0.385), 0.176, 0.108) * 0.42, p[..., 2] - 0.488, 0.04)
    h.mouth_y = lambda p: -0.070 + 0.5 * np.clip(0.05 - p[..., 2], 0, 1) ** 2
    h.eye, h.eye_r, h.eye_n = V(0.170, 0.038, 0.205), 0.058, nrm([0.90, 0.12, 0.40])
    def extra(p):
        m = mirror(p)
        jaw = sph(m, V(0.140, -0.030, -0.06), 0.100)                                  # 턱 뒤 — 목보다 조금 넓다
        brow = seg(m, V(0.126, 0.070, 0.110), V(0.062, 0.026, 0.430), 0.032, 0.022)   # 눈 위에서 주둥이로 서는 모서리
        return smin(jaw, brow, 0.03)
    h.extra = extra
    seat_eye(h, 0.30, exact=True)
    h.spikes, h.spike_col = far, h.skin
    if tone == 'kingrat':   # 뒤통수부터 온몸에 밑동이 검은 노란 비늘 — 납작하게 누워 윤곽은 매끈하고, 겹친 자리가 검은 그물로 읽힌다
        skin0 = lambda p: smin(smin(h.cran(p), h.extra(p), 0.05), h.upper(p), 0.05)
        lv = head_leaves(h, skin0, 0.075, -0.300, 0.070, 0.096, 0.95, 0.05, 0.08)
        h.scaly = dict(lift=0.28, dark=mark, tip=skin, ramp=(0.12, 0.42))
        h.spikes, h.spike_col = lv.dist, lv.tint(mark, skin, 0.12, 0.42)
    h.pits = [(V(0.066, 0.022, 0.492), 0.012)]
    h.jaw_w = 0.205; h.reach = 0.76
    ez, ey, er = float(h.eye[2]), float(h.eye[1]), h.eye_r
    def mix(col, k, c): return col * (1 - k[..., None]) + np.asarray(c, np.float32) * k[..., None]
    def tears(p, w=0.009):   # 눈에서 입술로 내려오는 줄 둘 + 눈 뒤로 비껴 내려가는 줄 하나
        z, y = p[..., 2], p[..., 1]; my = h.mouth_y(p)
        d = np.minimum(line2(z, y, ez + 0.030, ey - er * 0.8, ez + 0.062, -0.068), line2(z, y, ez - 0.030, ey - er * 0.8, ez - 0.075, -0.066))
        d = np.minimum(d, line2(z, y, ez - er * 0.9, ey - 0.012, ez - 0.215, -0.050))
        return sstep(w, w * 0.6, d) * sstep(0.07, 0.10, np.abs(p[..., 0])) * (y > my - 0.004)
    def lipbars(p, lo, hi, k=62.0, th=0.45):   # 입술 띠(입선에서 lo~hi)의 세로 줄
        yv = p[..., 1] - h.mouth_y(p)
        band = sstep(lo - 0.012, lo, yv) * sstep(hi + 0.012, hi, yv) * sstep(-0.02, 0.04, p[..., 2]) * sstep(0.08, 0.12, np.abs(p[..., 0])) * sstep(0.45, 0.41, p[..., 2])
        return sstep(th - 0.15, th + 0.15, np.sin(p[..., 2] * k + 0.6)) * band, band
    def paint(p, col):
        x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]; yv = y - h.mouth_y(p)
        if tone == 'boelen':      # 윗입술 — 검은 바탕에 노란 줄 · 아랫입술 — 노란 바탕에 검은 줄
            up, _ = lipbars(p, 0.0, 0.052); col = mix(col, up, mark)
            lo, band = lipbars(p, -0.060, 0.0, th=-0.35); col = mix(col, band, mark); col = mix(col, band * (1 - lo), S)
        elif tone == 'kingrat':   # 눈 밑 줄 · 입술 줄(비늘의 검은 테는 빚은 비늘이 낸다)
            col = mix(col, tears(p), mark)
            lb, _ = lipbars(p, -0.045, 0.045, k=58.0, th=0.88); col = mix(col, lb, mark)
        elif tone == 'indigo':    # 턱 · 입술이 녹슨 붉은색 · 그 위에 검은 줄
            chin = sstep(0.050, 0.020, yv) * sstep(-0.10, 0.02, z)
            col = mix(col, chin, mark)
            lb, _ = lipbars(p, -0.060, 0.045, k=58.0, th=0.55); col = mix(col, lb * chin, S * 0.8)
        else:                     # red — 눈 밑 · 눈 뒤 검은 줄
            col = mix(col, tears(p, 0.013), mark)
            lb, _ = lipbars(p, -0.040, 0.040, k=58.0, th=0.86); col = mix(col, lb * sstep(0.30, 0.20, z), mark)
        return col
    h.paint = paint
    def body_paint(p, col):
        s_, tailw = along(p); x = np.abs(p[..., 0])
        lum = col.sum(-1); back = (lum < 0.6) if tone in ('boelen', 'indigo') else (np.abs(col - S).sum(-1) < 0.25)   # 배 색은 안 건드린다
        if tone in ('boelen', 'indigo'):   # 검푸른 살의 옅은 무지갯빛
            w = 0.035 * np.sin(p[..., 1] * 11.0 + p[..., 2] * 8.0) * back
            col = np.clip(col + np.stack([-0.5 * w, 0.8 * w, 1.4 * w], -1), 0, 1)
        if tone == 'boelen':               # 옆구리 · 꼬리의 노란 빗금
            side = sstep(0.07, 0.15, x)
            flank = sstep(0.86, 0.95, np.sin(p[..., 1] * 24.0 + x * 16.0 - p[..., 2] * 9.0)) * side * (1 - tailw)
            tailb = sstep(0.86, 0.95, np.sin(p[..., 2] * 26.0 + p[..., 1] * 12.0)) * tailw
            col = mix(col, np.maximum(flank, tailb) * back, mark)
        elif tone == 'red':                # 붉은 머리에서 꼬리로 갈수록 검붉은 잿빛
            k = sstep(TOP + 0.05, D['lift'] - 0.35, s_) * back
            col = mix(col, k * 0.82, (0.23, 0.15, 0.18))
        return col
    h.body_paint = body_paint
    return h

# 숲살모사 — 색 넷(사용자 그림: 풀빛 · 청회색 · 숯빛 · 주황). 살은 비늘 사이로 어둡게 비치고 잎 끝이 밝다.
#            살(잎 밑동)          잎 끝               배 · 턱             눈                  눈 종류
VIPER = {'green': ((0.30, 0.50, 0.17), (0.80, 0.86, 0.40), (0.86, 0.88, 0.52), (0.46, 0.74, 0.66), 'bigslit'),
         'slate': ((0.24, 0.36, 0.45), (0.66, 0.80, 0.80), (0.78, 0.84, 0.62), (0.84, 0.88, 0.62), 'bigslit'),
         'coal':  ((0.13, 0.15, 0.19), (0.40, 0.45, 0.52), (0.36, 0.39, 0.44), BLACK,              'gecko'),
         'amber': ((0.80, 0.44, 0.10), (0.99, 0.82, 0.30), (0.97, 0.86, 0.46), (0.74, 0.86, 0.40), 'bigslit')}
def viper_head(crest=None, tone=None):
    """숲살모사 — 다른 뱀과 윤곽이 다르다(사용자 11-12). 위에서 보면 **넓은 세모**: 좁고 뭉툭한 주둥이에서 턱 모서리까지 곧게 벌어졌다가
    목에서 확 잘록해진다. 옆에서 보면 **윗면이 평평하고 주둥이가 깊은 채로 뚝 잘린다**(끝으로 가늘어지지 않는다) · 윗면과 옆면이 모서리(눈썹 줄)로
    만나고 큰 눈이 그 바로 밑에 있다 · 입선은 눈 뒤 턱 모서리까지 가며 끝이 올라간다. 비늘은 뒤로 누워 겹친 잎."""
    tone = tone or 'green'; skin, tip, belly, eyec, eyek = VIPER[tone]
    h = Head('viper', skin, belly, eyek); h.eye_col = eyec; h.tone = tone; h.tip = tip
    ZW = np.array([-0.33, -0.24, -0.10, 0.14, 0.325], np.float32); WW = np.array([0.105, 0.160, 0.262, 0.152, 0.092], np.float32)   # 볼 높이의 반폭 — 턱 모서리(z −0.10)가 가장 넓다
    ZT = np.array([-0.33, -0.18, 0.15, 0.325], np.float32); TT = np.array([0.070, 0.136, 0.138, 0.084], np.float32)                    # 윗면 높이 — 눈 앞에서 주둥이로 조금 내려간다
    def wedge(p, zf, k_w=1.0, top=True):
        x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
        w = np.interp(z, ZW, WW) * k_w - 0.050 * sstep(-0.02, 0.12, y) * sstep(0.30, -0.05, z)   # 윗면은 볼보다 좁다(뒤로 갈수록 더)
        d = smax(x - w, z - zf - 0.10 * np.minimum(y, 0.0), 0.075)                                                           # 뭉툭하게 잘린 주둥이
        if top: d = smax(d, y - (np.interp(z, ZT, TT) - 0.6 * x * x), 0.045)                                     # 평평한 윗면 · 눈썹 모서리
        return smax(d, -0.31 - z, 0.09) * 0.74
    h.upper = lambda p: smax(wedge(p, 0.330), -0.085 - p[..., 1], 0.02)   # 아래는 입선이 자른다 — 그 밑으로 조금만 남긴다(입 안 조각이 기둥이 되지 않게)
    h.lower = lambda p: smax(smax(wedge(p, 0.322, 0.97, top=False), (-0.150 + 0.085 * np.clip(p[..., 2], -0.1, 0.33) + 1.3 * p[..., 0] ** 2) - p[..., 1], 0.05), p[..., 1] - 0.0, 0.03)
    h.mouth_y = lambda p: -0.058 + 0.75 * np.clip(0.06 - p[..., 2], 0, 1) ** 2
    h.cran = lambda p: ell(p, V(0, -0.010, -0.200), V(0.160, 0.135, 0.150))                  # 뒤통수 밑 — 목이 여기 꽂힌다
    h.extra = lambda p: sph(mirror(p), V(0.170, -0.030, -0.135), 0.088)                        # 턱 모서리(독샘) — 입선이 여기서 끝난다
    h.eye, h.eye_r, h.eye_n = V(0.150, 0.060, 0.150), 0.060, nrm([0.92, 0.10, 0.38])
    h.chin_z = (-0.16, -0.08)                                                                  # 아래턱의 밝은 색이 턱 모서리까지
    seat_eye(h, 0.36, exact=True)
    skin0 = lambda p: smin(smin(h.cran(p), h.extra(p), 0.05), h.upper(p), 0.05)
    lv = head_leaves(h, skin0, 0.275, -0.330, 0.050, 0.104, 0.92, 0.10, 0.26, brow_lift=0.24)
    h.leaves = lv
    h.scaly = dict(lift=1.0, dark=np.asarray(skin, np.float32) * 0.92, tip=tip, ramp=(0.25, 0.95))
    h.spikes, h.spike_col = lv.dist, lv.tint(h.scaly['dark'], tip)
    h.pits = [(V(0.078, 0.012, 0.318), 0.011)]
    h.jaw_w = 0.27; h.reach = 0.72
    if tone == 'amber':   # 주황 — 등을 따라 녹슨 얼룩 띠
        def body_paint(p, col):
            s_, _ = along(p); k = sstep(0.55, 0.85, np.sin(s_ * 30.0 + 0.4 * np.sin(np.abs(p[..., 0]) * 30.0))) * (np.abs(col - np.asarray(skin, np.float32)).sum(-1) < 0.2)
            return col * (1 - 0.45 * k[..., None]) + np.asarray((0.62, 0.24, 0.08), np.float32) * 0.45 * k[..., None]
        h.body_paint = body_paint
    return h

def croc_head(crest=None):
    """덩치 큰 악어 — 넓은 주둥이(가늘면 약해 보인다) · 깊은 아래턱 · 눈 위에서 주둥이로 내려오는 굵은 턱 · 눈은 작고 깊다."""
    h = Head('croc', (0.23, 0.32, 0.25), (0.74, 0.72, 0.54), 'glare')
    h.eye_col = YELLOW
    h.cran = lambda p: smax(ell(p, V(0, 0.0, -0.07), V(0.265, 0.225, 0.27)), p[..., 1] - 0.190, 0.05)   # 윗면이 납작하다
    SW = lambda z: 0.215 + (0.150 - 0.215) * np.clip((z - 0.05) / 0.50, 0, 1)   # 위턱 반폭
    def upper(p):
        d = seg(squash(p, -0.03, 0.60), V(0, -0.01, 0.05), V(0, -0.03, 0.55), 0.215, 0.150) * 0.60
        mound = ell(p, V(0, 0.022, 0.590), V(0.100, 0.052, 0.085))   # 콧등 둔덕 — 콧구멍이 이 위에 뚫린다
        return smin(d, mound, 0.035)
    h.upper = upper
    h.lower = lambda p: seg(squash(p, -0.135, 0.64), V(0, -0.135, 0.0), V(0, -0.13, 0.545), 0.205, 0.140) * 0.64
    h.mouth_y = lambda p: -0.095 + 0.011 * np.sin(p[..., 2] * 24.0) + 0.7 * np.clip(0.10 - p[..., 2], 0, 1) ** 2   # 물결치는 입선
    h.eye, h.eye_r, h.eye_n = V(0.175, 0.125, 0.105), 0.046, nrm([0.78, 0.18, 0.60])
    def base(p):
        m = mirror(p)
        mound = ell(m, V(0.130, 0.118, 0.050), V(0.092, 0.072, 0.108))                  # 눈 둔덕 — 머리 윗면보다 솟지 않는다
        snout = seg(m, V(0.058, 0.050, 0.500), V(0.070, 0.095, 0.290), 0.020, 0.030)    # 주둥이 위 두 줄 능선
        hinge = sph(m, V(0.190, -0.090, -0.04), 0.112)                                  # 턱 근육
        return smin(mound, smin(snout, hinge, 0.02), 0.03)
    h.extra = base
    seat_eye(h)
    t = nrm(np.cross(UP, h.eye_n))
    b0 = h.eye + UP * (h.eye_r * 0.95) - t * 0.060 + h.eye_n * 0.012 - UP * 0.022   # 앞(안쪽)이 낮다 — 노려본다. 살에 붙여 짧게(길면 끝이 귀처럼 솟는다)
    b1 = h.eye + UP * (h.eye_r * 0.95) + t * 0.050 - h.eye_n * 0.015 + UP * 0.010
    h.extra = lambda p: smin(base(p), seg(mirror(p), b0, b1, 0.026, 0.032), 0.03)
    def spikes(p):   # 뒤통수 골판 — 안쪽 줄은 크게, 바깥 줄은 작게
        m = mirror(p); d = far(p)
        for z in (-0.07, -0.17, -0.27):
            y = min(0.190, 0.225 * np.sqrt(max(0.0, 1 - ((z + 0.07) / 0.27) ** 2)))
            d = np.minimum(d, seg(m, V(0.080, y - 0.02, z), V(0.095, y + 0.075, z - 0.055), 0.050, 0.012))
            d = np.minimum(d, seg(m, V(0.180, y - 0.05, z - 0.02), V(0.215, y + 0.025, z - 0.065), 0.040, 0.010))
        return d
    h.spikes, h.spike_col = spikes, (0.14, 0.21, 0.16)
    h.nostril, h.nostril_r = V(0.036, 0.066, 0.605), 0.017
    def teeth(p):   # 굵기와 길이가 들쭉날쭉한 송곳니 — 위는 아래로, 아래 큰 것 둘은 윗입술 밖으로 솟는다
        m = mirror(p); d = far(p)
        for z, ln, r in ((0.16, 0.070, 0.022), (0.25, 0.118, 0.031), (0.34, 0.072, 0.022), (0.43, 0.108, 0.029), (0.52, 0.078, 0.023)):
            w = SW(z) * 0.975
            d = np.minimum(d, seg(m, V(w, -0.082, z), V(w + 0.006, -0.082 - ln, z + 0.004), r, 0.003))
        for z, ln, r in ((0.295, 0.105, 0.027), (0.475, 0.090, 0.025)):
            w = SW(z) + 0.004
            d = np.minimum(d, seg(m, V(w, -0.118, z), V(w + 0.010, -0.118 + ln, z), r, 0.003))
        for x in (0.045, 0.115):
            z = 0.690 - x * x * 3.2
            d = np.minimum(d, seg(m, V(x, -0.082, z), V(x, -0.160, z + 0.006), 0.022, 0.003))
        return d
    h.teeth = teeth
    h.jaw_w = 0.225; h.reach = 0.90
    return h

def brute_head(crest=None):
    """공룡 악어 — **짧고 깊은 턱**(v5는 주둥이가 길고 납작했다 — 사용자: *"주둥이가 너무 길고 · 공룡은 저 정도로 납작하지 않다 · 좀 더 두꺼워도"*):
    폭보다 높이가 큰 머리 · 위아래로 두꺼운 주둥이 · 깊은 아래턱 · 눈 위 뼈 능선 밑의 눈 · 능선 끝에서 뒤로 누운 뿔 · 드러난 이빨 ·
    정수리에서 뒤로 누운 골판. 턱 옆은 **납작한 면**이다(둥근 턱 근육 공은 *"두꺼비 같다"* — 걷었다)."""
    h = Head('brute', (0.28, 0.40, 0.50), (0.80, 0.76, 0.62), 'glare')
    h.eye_col = (0.95, 0.60, 0.14)
    h.cran = lambda p: smax(ell(p, V(0, 0.03, -0.08), V(0.238, 0.225, 0.25)), p[..., 1] - 0.200, 0.05)   # 좌우로 넓다(v8 — 사용자: *"얼굴도 살짝 더 늘어나면 — 좌우로"*)
    SW = lambda z: 0.196 + (0.146 - 0.196) * np.clip((z - 0.04) / 0.34, 0, 1)   # 위턱 반폭
    h.upper = lambda p: seg(squash(p, 0.01, 0.90), V(0, 0.01, 0.04), V(0, -0.005, 0.38), 0.196, 0.146) * 0.90   # 넓고 두껍다
    h.lower = lambda p: seg(squash(p, -0.135, 0.74), V(0, -0.135, -0.01), V(0, -0.130, 0.372), 0.186, 0.128) * 0.74
    h.mouth_y = lambda p: -0.070 + 0.008 * np.sin(p[..., 2] * 22.0) + 0.6 * np.clip(0.08 - p[..., 2], 0, 1) ** 2
    h.eye, h.eye_r, h.eye_n = V(0.178, 0.122, 0.095), 0.040, nrm([0.86, 0.12, 0.50])
    def base(p):
        m = mirror(p)
        ramus = ell(m, V(0.152, -0.085, -0.055), V(0.060, 0.170, 0.115))               # 턱뼈 뒤 — 옆으로 납작한 면(공이 아니다)
        snout = seg(m, V(0.060, 0.110, 0.400), V(0.106, 0.150, 0.180), 0.020, 0.032)   # 콧등에서 눈으로 오르는 능선
        return smin(ramus, snout, 0.03)
    h.extra = base
    seat_eye(h)
    t = nrm(np.cross(UP, h.eye_n))
    b0 = h.eye + UP * (h.eye_r * 1.05) - t * 0.072 + h.eye_n * 0.012 - UP * 0.020   # 앞(안쪽)이 낮다 — 노려본다
    b1 = h.eye + UP * (h.eye_r * 1.05) + t * 0.064 - h.eye_n * 0.018 + UP * 0.012
    h.extra = lambda p: smin(base(p), seg(mirror(p), b0, b1, 0.027, 0.036), 0.03)
    def spikes(p):
        m = mirror(p); d = far(p)
        d = np.minimum(d, seg(m, b1 - UP * 0.010, b1 + nrm([0.22, 0.50, -0.84]) * 0.110, 0.030, 0.004))          # 능선 끝의 뿔 — 뒤로 눕는다
        q = thin(p, 0.36)                                                                                        # 정수리 골판 — 뒤로 누워 뒤로 갈수록 크다
        for i, (z, y) in enumerate(((0.02, 0.190), (-0.10, 0.196), (-0.21, 0.170))):
            ln = 0.055 + 0.028 * i
            d = np.minimum(d, seg(q, V(0, y - 0.03, z), V(0, y + ln * 0.80, z - 0.050 - 0.020 * i), 0.046, 0.008) * 0.36)
        return d
    h.spikes, h.spike_col = spikes, (0.80, 0.76, 0.62)
    h.nostril, h.nostril_r = V(0.060, 0.086, 0.462), 0.015
    def teeth(p):   # 위는 아래로 · 아래 큰 것들은 윗입술 밖으로 솟는다(입을 다물어도 보인다)
        m = mirror(p); d = far(p)
        for z, ln, r in ((0.08, 0.050, 0.018), (0.15, 0.086, 0.025), (0.22, 0.058, 0.019), (0.29, 0.088, 0.025), (0.36, 0.056, 0.018)):
            w = SW(z) * 0.975
            d = np.minimum(d, seg(m, V(w, -0.058, z), V(w + 0.005, -0.058 - ln, z + 0.004), r, 0.003))
        for z, ln, r in ((0.115, 0.066, 0.021), (0.185, 0.094, 0.026), (0.255, 0.074, 0.022), (0.325, 0.060, 0.019)):
            w = SW(z) + 0.003
            d = np.minimum(d, seg(m, V(w, -0.092, z), V(w + 0.008, -0.092 + ln, z), r, 0.003))
        for x in (0.040, 0.105):
            z = 0.516 - x * x * 3.0
            d = np.minimum(d, seg(m, V(x, -0.058, z), V(x, -0.124, z + 0.005), 0.019, 0.003))
        return d
    h.teeth = teeth
    h.jaw_w = 0.20; h.reach = 0.95
    return h

HEADS = {'lizard': lizard_head, 'python': python_head, 'viper': viper_head, 'croc': croc_head, 'brute': brute_head}

# ── 눈 — 고르기 축. 날카로운 쪽에서 둥근 쪽으로 ────────────────────────────────────────────────────────────
#   이름       눈동자   크기   윗꺼풀 높이 · 앞으로 기운 정도   아랫꺼풀(없으면 None)   색
EYE = {
    'sharp':   ('slit',  0.95, 0.02, 0.80, 0.50, AMBER),    # 가늘게 찢어진 눈 — 위아래 꺼풀이 다 덮는다
    'glare':   ('slit',  1.00, 0.28, 0.55, None, AMBER),    # 노려보는 눈
    'almond':  ('slit',  1.08, 0.52, 0.30, 0.80, GREEN),    # 중간 — 꺼풀이 살짝
    'bigslit': ('slit',  1.24, 0.90, 0.08, None, GREY),     # 크고 둥근 눈에 세로 눈동자(볏도마뱀붙이)
    'round':   ('round', 1.14, 0.70, 0.15, None, AMBER),    # 둥근 눈동자
    'gecko':   (None,    1.24, 0.90, 0.08, None, BLACK),    # 크고 검은 눈 + 반짝임
    'blank':   (None,    1.00, 0.28, 0.55, None, PALE),     # 눈동자 없는 흐린 눈
    'bead':    (None,    1.00, 0.92, 0.05, None, BLACK),    # 작고 검은 구슬 눈
    'ring':    ('ring',  1.00, 0.86, 0.06, None, AMBER),    # 큰 둥근 눈동자에 가는 테 + 반짝임(매끈한 뱀)
}
EYE_ORDER = ('sharp', 'glare', 'almond', 'bigslit', 'round', 'gecko', 'blank')

def pits_of(h): return getattr(h, 'pits', None) or [(h.nostril, h.nostril_r)]
def eye_frame(h):
    t = nrm(np.cross(UP, h.eye_n)); u = np.cross(h.eye_n, t)   # t = 눈의 바깥 · 뒤쪽 · u = 위
    return t, u
def head_skin(h, e):
    _, k, lid0, slant, low, _ = e; r = h.eye_r * k; t, _ = eye_frame(h)
    def f(p):
        yv = p[..., 1] - h.mouth_y(p)
        base = smin(h.cran(p), h.extra(p), 0.05)
        up = smin(base, np.maximum(h.upper(p), -(yv - G)), 0.05)
        lo = smin(base, np.maximum(h.lower(p), yv + G), 0.05)
        d = np.minimum(up, lo)
        m = mirror(p)
        for c, pr in pits_of(h): d = smax(d, -(length(m - c) - pr), 0.004)   # 콧구멍 · 열 구멍은 판다 — 튀어나온 점이 아니다
        q = m - h.eye; ball = length(q) - (r + 0.009); a = (q * t).sum(-1)
        d = np.minimum(d, np.maximum(ball, (r * lid0 + slant * a) - q[..., 1]))
        if low is not None: d = np.minimum(d, np.maximum(ball, q[..., 1] - (-r * low + 0.25 * a)))
        return d
    return f
def head_inner(h): return lambda p: np.minimum(h.upper(p), h.lower(p)) + 0.011   # 입 안 — 입선 틈으로만 보인다
def eyeball(h, e): return lambda p: sph(mirror(p), h.eye, h.eye_r * e[1])
def pupil(h, e):
    kind, k = e[0], e[1]; r = h.eye_r * k; t, u = eye_frame(h)
    def f(p):
        q = mirror(p) - h.eye
        ball = length(q) - (r + 0.004); fwd = r * 0.35 - (q * h.eye_n).sum(-1)
        a, b = (q * t).sum(-1), (q * u).sum(-1)
        shape = np.maximum(np.abs(a) - r * 0.16, np.abs(b) - r * 0.74) if kind == 'slit' else np.sqrt(a * a + b * b) - r * (0.70 if kind == 'ring' else 0.48)
        return np.maximum(np.maximum(ball, fwd), shape)
    return f
def glint(h, e):
    r = h.eye_r * e[1]; t, u = eye_frame(h)
    c = h.eye + (h.eye_n * 0.80 + u * 0.40 - t * 0.28) * r
    return lambda p: sph(mirror(p), c, r * 0.21)
def head_col(h):   # 아래턱은 배 색(턱 관절 앞, 아래턱 폭 안에서만) · 콧구멍 안은 검다
    S, B, K = np.asarray(h.skin, np.float32), np.asarray(h.belly, np.float32), np.asarray(DARK, np.float32)
    def c(p):
        yv = p[..., 1] - h.mouth_y(p)
        z0, z1 = getattr(h, 'chin_z', (0.03, 0.13))
        k = sstep(0.0, -0.03, yv) * sstep(z0, z1, p[..., 2]) * sstep(h.jaw_w + 0.02, h.jaw_w - 0.05, np.abs(p[..., 0]))
        col = S * (1 - k[..., None]) + B * k[..., None]
        if hasattr(h, 'paint'): col = h.paint(p, col)
        m = mirror(p)
        for cc, pr in pits_of(h):
            n = sstep(pr * 1.25, pr * 0.95, length(m - cc))
            col = col * (1 - n[..., None]) + K * n[..., None]
        return col
    return c

def pieces(kind, body=True, eye=None, crest=None, eye_col=None):
    """kind는 `종류` 또는 `종류:색`(뱀 둘은 색이 여럿 — `viper:slate` · `python:kingrat`)."""
    kind, _, tone = kind.partition(':')
    set_body(kind)
    h = HEADS[kind](crest, tone or None) if kind in ('python', 'viper') else (HEADS[kind](crest) if crest else HEADS[kind]())
    e = list(EYE[eye or h.eye_default])
    if eye_col is not None: e[5] = eye_col
    elif eye is None and hasattr(h, 'eye_col'): e[5] = h.eye_col
    def hd(f): return lambda p: bounded(lambda q: f((q - HO) / HK) * HK, p, HO + V(0, 0, 0.12 * HK), h.reach * HK)
    def hcol(c): return (lambda p: c((p - HO) / HK)) if callable(c) else c
    hc = head_col(h)
    P = [(hd(head_skin(h, e)), hcol(hc)), (hd(head_inner(h)), MOUTH), (hd(eyeball(h, e)), e[5]), (hd(h.spikes), hcol(h.spike_col))]
    if e[0]: P.append((hd(pupil(h, e)), DARK))
    if e[5] == BLACK or e[0] == 'ring': P.append((hd(glint(h, e)), WHITE))
    if h.teeth: P.append((hd(h.teeth), BONE))
    if h.tongue: P.append((hd(h.tongue), TONGUE))
    bc = body_col(h.skin, h.belly)
    if kind == 'brute':   # 근육질 몸 — 몸통에 근육 덩이를 얕게 녹이고 팔 · 손 · 다리 · 발을 따로 빚는다
        def skin_body(p):
            tn = smin(smin(torso(p), neck(p), 0.05), brute_muscles(p), 0.09)
            return union([smin(tn, brute_arms(p), 0.03), brute_hands(p), brute_legs(p), brute_feet(p), smin(torso(p), tail(p), 0.06)])
    else:
        def skin_body(p): return union([smin(torso(p), neck(p), 0.05), arms(p), hands(p), legs(p), feet(p), smin(torso(p), tail(p), 0.06)])
    if body:
        bcol = (lambda p: h.body_paint(p, bc(p))) if hasattr(h, 'body_paint') else bc
        claws = (brute_hand_claws, brute_foot_claws) if kind == 'brute' else (hand_claws, foot_claws)
        P += [(skin_body, bcol), (claws[0], BONE), (claws[1], BONE), (belt, LEATHER), (flap, CLOTH)]
        sc = getattr(h, 'scaly', None)
        if sc:
            for lv in body_leaves(sc['lift']): P.append((lv.dist, lv.tint(sc['dark'], sc['tip'], *sc['ramp'])))
        elif kind == 'lizard' and (crest or 'fringe') == 'fringe': P.append((fringe_rows_f, h.spike_col))   # 가시 테 — 등 양쪽 두 줄(한가운데 줄 대신)
        elif kind == 'lizard' and crest == 'bare': pass                                                        # 뿔 없는 가시 테 머리 — 등도 매끈
        elif kind == 'brute': P.append((brute_plates_f, h.spike_col))   # 골판
        elif kind != 'python': P.append((ridge, (0.24, 0.36, 0.20) if kind == 'lizard' else h.spike_col))
    else:
        cut = HO[1] - 0.52 * HK
        P += [(lambda p: np.maximum(smin(torso(p), neck(p), 0.05), cut - p[..., 1]), bc)]
    if kind == 'brute':   # 몸 전체를 키운다 — 크기는 메시가 든다
        S = BRUTE_S
        P = [((lambda f: lambda p: f(p / S) * S)(f), ((lambda g: lambda p: g(p / S))(col) if callable(col) else col)) for f, col in P]
    return P

KINDS = ('lizard', 'python', 'viper', 'croc')
SNAKES = ('viper', 'viper:slate', 'viper:coal', 'viper:amber', 'python', 'python:kingrat', 'python:indigo', 'python:red')
FULL = ((0, 380), (40, 500), (90, 760), (150, 560))                  # 보기 각도 · 폭
CLOSE = ((0, 360, 0), (35, 440, 0), (90, 600, 0), (25, 480, 50))    # 각도 · 폭 · 내려다보기(게임 카메라 쪽)
CRESTS = ('fringe', 'comb', 'none', 'bare')
BRUTE_VIEW = (1.50, 3.40)   # 공룡 악어 판의 보기 — 가운데 높이 · 세로 범위(m). 도마뱀도 같은 눈금으로 그려 크기를 견준다
def job(a):
    mode, kind, i = a
    if mode == 'brute':
        if i < len(FULL):
            yaw, w = FULL[i]; return np.asarray(hb.render(pieces('brute'), yaw, (700, 760, 1000, 820)[i], 760, *BRUTE_VIEW))
        if i == len(FULL): return np.asarray(hb.render(pieces('lizard'), 40, 420, 760, *BRUTE_VIEW))
        yaw, w, pitch = CLOSE[i - len(FULL) - 1]; P = pieces('brute', body=False)
        return np.asarray(shifted(P, yaw, w, 400, 1.30 * HK * BRUTE_S, pitch, BRUTE_S))
    if mode == 'full':
        yaw, w = FULL[i]
        return np.asarray(hb.render(pieces(kind), yaw, w, 640, 1.08, 2.62))
    if mode == 'close':
        yaw, w, pitch = CLOSE[i]; P = pieces(kind, body=False)
        return np.asarray(shifted(P, yaw, w, 360, 1.20 * HK, pitch))
    if mode == 'eyes':
        P = pieces(kind, body=False, eye=EYE_ORDER[i])
        return np.asarray(shifted(P, 32, 300, 280, 0.95 * HK, 0))
    yaw, w, pitch = ((35, 440, 0), (90, 560, 0), (25, 480, 50))[i]   # crest — kind 자리에 볏 이름
    P = pieces('lizard', body=False, crest=kind)
    return np.asarray(shifted(P, yaw, w, 360, 1.15, pitch))
def shifted(P, yaw, w, h, span, pitch, k=1.0):
    """머리가 굽은 어깨 앞에 있어 원점 둘레로 돌리면 그림 밖으로 나간다 — 머리 자리를 원점으로 옮겨 그린다. `k` — 키운 몸의 배수."""
    c = (HO + V(0, 0.0, 0.10 * HK)) * k
    Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in P]
    return hb.render(Q, yaw, w, h, 0.0, span, pitch=pitch)

def sheet(mode, rows, ws, hgt, ims, out, labels=None):
    im = Image.new('RGB', (sum(ws), hgt * len(rows)), (40, 40, 44)); dr = ImageDraw.Draw(im)
    for r, kind in enumerate(rows):
        x = 0
        for i in range(len(ws)):
            im.paste(Image.fromarray(ims[(mode, kind, i)]), (x, r * hgt))
            if labels: dr.text((x + 6, r * hgt + 18), labels[i], fill=(200, 200, 210))
            x += ws[i]
        dr.text((6, r * hgt + 4), kind, fill=(255, 220, 120))
    im.save(out); print('saved', out)

if __name__ == '__main__':
    from multiprocessing import Pool
    stem = sys.argv[1]
    only = sys.argv[2] if len(sys.argv) > 2 else 'all'
    on = lambda m: only in ('all', m)
    for k in KINDS:
        set_body(k); print('%-6s 머리통 꼭대기 %.2f m · 어깨 %.2f m' % (k, HO[1] + 0.24 * HK, TOP))
    jobs = []
    if on('full'):  jobs += [('full', k, i) for k in KINDS for i in range(len(FULL))]
    if on('heads'): jobs += [('close', k, i) for k in KINDS for i in range(len(CLOSE))]
    if on('eyes'):  jobs += [('eyes', k, i) for k in KINDS for i in range(len(EYE_ORDER))]
    if on('crest'): jobs += [('crest', c, i) for c in CRESTS for i in range(3)]
    if on('snakes'): jobs += [(m, k, i) for m, V_ in (('full', FULL), ('close', CLOSE)) for k in SNAKES for i in range(len(V_))]
    if only == 'brute': jobs += [('brute', 'brute', i) for i in range(len(FULL) + 1 + len(CLOSE))]
    jobs = list(dict.fromkeys(jobs))
    with Pool(min(14, len(jobs))) as pool: res = pool.map(job, jobs)
    ims = dict(zip(jobs, res))
    if on('full'):  sheet('full', KINDS, [w for _, w in FULL], 640, ims, stem + '_full.png')
    if on('heads'): sheet('close', KINDS, [v[1] for v in CLOSE], 360, ims, stem + '_heads.png')
    if on('eyes'):  sheet('eyes', KINDS, [300] * len(EYE_ORDER), 280, ims, stem + '_eyes.png', EYE_ORDER)
    if on('crest'): sheet('crest', CRESTS, [440, 560, 480], 360, ims, stem + '_crest.png')
    if only == 'brute':   # 윗줄 — 온몸 넷 + 같은 눈금의 도마뱀 · 아랫줄 — 머리 넷
        top = [ims[('brute', 'brute', i)] for i in range(len(FULL) + 1)]; low = [ims[('brute', 'brute', i)] for i in range(len(FULL) + 1, len(FULL) + 1 + len(CLOSE))]
        wd = max(sum(t.shape[1] for t in top), sum(t.shape[1] for t in low))
        im = Image.new('RGB', (wd, 760 + 400), (40, 40, 44)); dr = ImageDraw.Draw(im); x = 0
        for i, t in enumerate(top):
            im.paste(Image.fromarray(t), (x, 0))
            if i == len(FULL): dr.text((x + 6, 6), 'lizard (same scale)', fill=(200, 200, 210))
            x += t.shape[1]
        x = 0
        for t in low: im.paste(Image.fromarray(t), (x, 760)); x += t.shape[1]
        dr.text((6, 4), 'brute x%.2f' % BRUTE_S, fill=(255, 220, 120))
        im.save(stem + '_brute.png'); print('saved', stem + '_brute.png')
        ref = _os.path.join(_here, '..', '_refs', '2026-11-14_lizard_Croc', 'user_markup_v7.png')
        if _os.path.exists(ref):   # 사용자가 고쳐 그린 그림(왼쪽 · 깃 밖)과 정면을 나란히 — 보낸 그림이 목표다
            r = Image.open(ref).convert('RGB'); r = r.resize((int(r.width * 760 / r.height), 760), Image.LANCZOS)
            cm = Image.new('RGB', (r.width + top[0].shape[1], 760), (40, 40, 44)); cm.paste(r, (0, 0)); cm.paste(Image.fromarray(top[0]), (r.width, 0))
            cm.save(stem + '_cmp.png'); print('saved', stem + '_cmp.png')
    if on('snakes'):
        sheet('full', SNAKES, [w for _, w in FULL], 640, ims, stem + '_snakes_full.png')
        sheet('close', SNAKES, [v[1] for v in CLOSE], 360, ims, stem + '_snakes_heads.png')
