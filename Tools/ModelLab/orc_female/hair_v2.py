"""여성 오크 머리카락 v2 — 옆은 밀고 위에만 남긴다 · 굵은 다발(가닥)로 나뉜 큰 머리 · 귀에는 고리 여럿.
조각마다 (머리카락, 가죽, 쇠) 셋을 돌려준다."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import numpy as np
from brute_preview import V, smin, smax, ell, seg, sph, mirror
import orc_parts as O
from orc_preview import head, W, H, D, F, T_, R_, EYE_T, EY
from hair_f import on_head, cap, down

def NONE(p): return O.NONE(p)
def nrm(v): return v / max(float(np.linalg.norm(v)), 1e-6)
def bez(a, b, c, d, n):
    out = []
    for i in range(n + 1):
        t = i / n; s = 1 - t
        out.append((a * s ** 3 + b * 3 * s * s * t + c * 3 * s * t * t + d * t ** 3).astype(np.float32))
    return out
def lock(p, pts, r0, r1, k=0.006):   # 끝으로 갈수록 가늘어지는 다발 한 가닥
    d = NONE(p); n = len(pts) - 1
    for i in range(n):
        ra = r0 + (r1 - r0) * i / n; rb = r0 + (r1 - r0) * (i + 1) / n
        d = smin(d, seg(p, pts[i], pts[i + 1], ra, rb), k)
    return d
def strip(p, half, front_y=0.12, back_y=0.0, th=0.022):   # 가운데 띠만 남긴 머리(옆은 민다)
    ang = np.abs(np.arctan2(p[..., 0], p[..., 2])); k = ang / np.pi
    line = front_y + (back_y - front_y) * k + 0.8 * p[..., 0] ** 2   # 앞선이 둥글다
    above = p[..., 1] - line
    w = np.clip(above / 0.02, 0, 1)
    d = head(p) - th * w
    return smax(smax(d, -above, 0.005), np.abs(p[..., 0]) - half, 0.02)
def ring(p, c, axis, R, r):   # 고리(축 axis)
    q = p - c; h = (q * axis).sum(-1)
    rad = np.sqrt(np.maximum((q * q).sum(-1) - h * h, 0)) - R
    return np.sqrt(rad * rad + h * h) - r

# ── 귀고리 — 귓바퀴 뒷선을 따라 셋 ──
_f = np.radians(30)
UA = np.array([np.sin(_f), 0, -np.cos(_f)], np.float32); NA = np.array([np.cos(_f), 0, np.sin(_f)], np.float32)
_R0 = float(np.interp(EYE_T, T_, R_) * W); EAR0 = V(_R0 - 0.01, EY - 0.03, -0.035)
def earrings(p):
    m = mirror(p); d = NONE(p)
    for u, v in ((0.040, -0.035), (0.075, 0.035), (0.098, 0.105)):
        c = EAR0 + UA * (u + 0.012) + V(0, v, 0)
        d = np.minimum(d, ring(m, c, NA, 0.034, 0.009))
    return d

# ── 머리 여섯 ──
def h_high_pony(p):   # 옆은 밀고 정수리 띠 → 높이 묶어 크게 휘어 내리는 꼬리
    base = strip(p, 0.11, 0.14, 0.24)
    R = on_head(180, 0.30, 0.03)
    hair = base
    for dx, dz, s in ((-0.06, 0.00, 0.85), (-0.03, -0.03, 1.0), (0.0, 0.02, 1.08), (0.03, -0.02, 1.0), (0.06, 0.01, 0.85), (0.0, -0.06, 0.95)):
        pts = bez(R, R + V(dx * 0.3, 0.27, -0.05), R + V(dx * 0.9, 0.24, -0.44), R + V(dx * 2.6, -0.55 * s, -0.52 + dz), 7)
        hair = smin(hair, lock(p, pts, 0.080, 0.014), 0.03)
    tie = O.cuff(p, R + V(0, 0.04, -0.01), R + V(0, 0.10, -0.025), 0.085)
    return hair, tie, NONE(p)
h_high_pony_old = h_high_pony

# ── 끌어모은 머리 — 이마 · 관자놀이에서 묶는 자리로 당겨 모인 다발(두피에서 떠서 둥글게 부푼다) ──
def proj(q, out=0.0):
    """머리 가운데에서 q 쪽으로 쏴 닿는 표면 점 + out."""
    d = nrm(np.asarray(q, np.float32)); lo, hi = 0.02, 0.8
    for _ in range(40):
        mid = (lo + hi) * 0.5
        if float(head((d * mid)[None])[0]) < 0: lo = mid
        else: hi = mid
    return (d * (lo + out)).astype(np.float32)
def gathered(p, R, starts, puff=0.055, r0=0.050, r1=0.058, n=8):
    """starts(표면 점)마다 R로 당긴 다발 — 가운데가 두피에서 puff만큼 떠 둥근 덩어리가 된다."""
    hair = NONE(p)
    for s in starts:
        pts = []
        for i in range(n + 1):
            t = i / n
            q = s * (1 - t) + R * t
            pts.append(proj(q, 0.012 + puff * np.sin(np.pi * min(t * 1.25, 1.0)) * (1 - 0.35 * t)))
        hair = smin(hair, lock(p, pts, r0, r1), 0.028)
    return hair

def lock_r(p, pts, radii, k=0.006):   # 마디마다 굵기를 주는 다발
    d = NONE(p)
    for i in range(len(pts) - 1):
        d = smin(d, seg(p, pts[i], pts[i + 1], radii[i], radii[i + 1]), k)
    return d

# 끈으로 묶은 자리는 잘록하다 — 모인 가닥은 묶는 자리로 가늘어지고(r1), 꼬리는 끈 밑에서 가늘게 나와 부풀었다가 끝으로 준다.
TAIL_R = [0.030, 0.050, 0.074, 0.080, 0.070, 0.056, 0.042, 0.026, 0.012]
def gathered_via(p, R, starts, puff=0.050, r0=0.050, r1=0.026, n=10):
    """구레나룻 — 귀 앞에서 먼저 위로 올라갔다(via) 묶는 자리로 간다(귀를 가로지르지 않게)."""
    hair = NONE(p)
    for s, via in starts:
        pts = []
        for i in range(n + 1):
            t = i / n
            q = s + (via - s) * (t / 0.4) if t < 0.4 else via + (R - via) * ((t - 0.4) / 0.6)
            pts.append(proj(q, 0.010 + puff * np.sin(np.pi * min(t * 1.2, 1.0)) * (1 - 0.4 * t)))
        hair = smin(hair, lock(p, pts, r0 * 0.85, r1), 0.028)
    return hair

def under(p, line_a, line_y, th=0.052):
    """다발 밑을 받치는 머리 층 — 다발 사이로 두피가 비쳐 보이지 않게. 선(방위각 → 높이) 위만 덮는다."""
    ang = np.degrees(np.abs(np.arctan2(p[..., 0], p[..., 2])))
    above = p[..., 1] - np.interp(ang, line_a, line_y)
    k = np.clip(above / 0.03, 0, 1); k = k * k * (3 - 2 * k)
    return smax(head(p) - th * k, -above, 0.006)

PONY_FRONT = [(a, 0.15 + 0.00001 * a * a) for a in (-60, -40, -20, 0, 20, 40, 60)]
PONY_TEMPLE = [(a, 0.19) for a in (-88, 88, -115, 115)]
PONY_BACK = [(a, -0.13) for a in (125, 140, 155, 170, 190, 205, 220, 235)] + [(a, -0.04) for a in (-118, 118)]
# 받침 층의 선 — (방위각들, 높이들). 묶는 자리 아래 뒤통수는 민다.
LINE_A_TEMPLE = ([0, 60, 88, 118, 140, 180], [0.15, 0.186, 0.19, 0.19, 0.30, 0.32])
LINE_B_SHAVED = ([0, 60, 78, 120, 180], [0.15, 0.186, 0.25, 0.31, 0.32])
LINE_A_BACK = ([0, 60, 88, 112, 128, 180], [0.15, 0.186, 0.19, 0.12, -0.13, -0.13])
LINE_B_BACK = ([0, 60, 78, 104, 126, 180], [0.15, 0.186, 0.25, 0.08, -0.13, -0.13])
LINE_C = ([0, 60, 70, 80, 88, 96, 116, 128, 180], [0.15, 0.17, 0.04, 0.0, 0.20, 0.21, 0.10, -0.13, -0.13])
# 구레나룻 — 귀(90°) 앞 74°~80°에서 내려와, 귀 위(95°, 0.25)로 올라갔다 묶는 자리로
SIDEBURN = [(on, via) for on, via in (((74, 0.03), (92, 0.26)), ((80, 0.0), (98, 0.25)))]
def pony(starts_ab, line, burns=()):
    def h(p):   # 옆은 밀고 · 머리를 끌어모아 정수리 뒤에서 끈으로 묶어 크게 휘어 내리는 꼬리
        R = on_head(180, 0.30, 0.05)
        starts = [on_head(a, y, 0.0) for a, y in starts_ab]
        hair = smin(under(p, *line), gathered(p, R, starts, r1=0.026), 0.02)
        # 속 다발 — 겉 다발보다 낮게 · 굵게 같은 길을 지나 다발 사이 골 밑을 막는다(겉의 결은 그대로, 사이로 뒤가 비치지 않게)
        hair = smin(hair, gathered(p, R, starts, puff=0.030, r0=0.060, r1=0.030), 0.01)
        if burns:
            pairs = []
            for (a, y), (va, vy) in burns:
                for sgn in (1, -1):
                    pairs.append((on_head(a * sgn, y, 0.0), on_head(va * sgn, vy, 0.03)))
            # 따로 묶인 한 가닥 — 받침 층에 녹이지 않는다(작은 k) · 조금 가늘게
            hair = smin(hair, gathered_via(p, R, pairs, puff=0.045, r0=0.048, r1=0.020), 0.004)
        for dx, dz, s in ((-0.06, 0.00, 0.85), (-0.03, -0.03, 1.0), (0.0, 0.02, 1.08), (0.03, -0.02, 1.0), (0.06, 0.01, 0.85), (0.0, -0.06, 0.95)):
            pts = bez(R, R + V(dx * 0.3, 0.27, -0.05), R + V(dx * 0.9, 0.24, -0.44), R + V(dx * 2.6, -0.55 * s, -0.52 + dz), 8)
            hair = smin(hair, lock_r(p, pts, TAIL_R), 0.03)
        tie = O.cuff(p, R + V(0, 0.012, -0.002), R + V(0, 0.05, -0.010), 0.050)   # 끈 — 잘록한 목보다 살짝만 굵다
        return hair, tie, NONE(p)
    return h
# 반삭이 덮는 아랫선 — 관자놀이는 눈썹 위에서 시작해 · 귀 위를 넘고 · 귀 뒤에서 목덜미 쪽으로 내려간다(얼굴은 안 덮는다)
BUZZ_LO = ([56, 62, 72, 86, 93, 112, 124, 180], [0.30, 0.12, 0.00, -0.07, 0.19, 0.19, -0.08, -0.12])   # 귀 앞 구레나룻은 귓불 높이까지
def buzz(line):
    """반삭 — 민 자리를 살 위 얇은 층으로 덮어 짧은 머리가 비치게 한다."""
    def f(p):
        ang = np.degrees(np.abs(np.arctan2(p[..., 0], p[..., 2])))
        top = np.interp(ang, *line) + 0.02
        lo = np.interp(ang, *BUZZ_LO)
        band = smax(lo - p[..., 1], p[..., 1] - top, 0.008)
        return smax(smax(head(p) - 0.004, band, 0.004), (56 - ang) * 0.004, 0.01)
    return f
def with_buzz(h, line, full=False):
    h.stubble = buzz(line); h.stubble_full = full; return h   # full — 반삭을 머리색 그대로(짧은 머리)

h_high_pony = with_buzz(pony(PONY_FRONT + PONY_TEMPLE, LINE_A_TEMPLE), LINE_A_TEMPLE)   # A  이마 + 관자놀이 · 반삭
h_pony_no_temple = with_buzz(pony(PONY_FRONT, LINE_B_SHAVED), LINE_B_SHAVED)  # B  관자놀이 반삭
h_pony_a_back = pony(PONY_FRONT + PONY_TEMPLE + PONY_BACK, LINE_A_BACK)       # A2 A + 뒷머리(구레나룻 없음)
h_pony_b_back = with_buzz(pony(PONY_FRONT + PONY_BACK, LINE_B_BACK), LINE_B_BACK)   # B2 B + 뒷머리 · 관자놀이 반삭
h_pony_back_up = with_buzz(pony(PONY_FRONT + PONY_TEMPLE + PONY_BACK, LINE_A_BACK), LINE_A_BACK, full=True)  # C  A2 + 반삭 자리를 머리색으로(구레나룻 대신)

def _crest(p, n, length, front_y, back_y, half):
    base = strip(p, half, front_y, back_y - 0.06)
    pts = [on_head(0, front_y + 0.03)] + [on_head(0, y) for y in (0.27, 0.32)] + [V(0, H * 0.5 - 0.004, 0.0)] + \
          [on_head(180, y) for y in (0.31, 0.26, 0.19, 0.11, back_y)]
    pts = np.array(pts, np.float32)
    seglen = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]
    hair = base
    for i in range(n):
        s = seglen[-1] * (i + 0.5) / n
        b = np.array([np.interp(s, seglen, pts[:, k]) for k in range(3)], np.float32)
        out = nrm(b - V(0, -0.05, 0)); L = length * (0.75 + 0.45 * np.sin(np.pi * (i + 0.5) / n))
        for side, sh in ((-1, 0.0), (1, 0.5)):
            Ls = L * (1.0 if side < 0 else 0.85)
            off = V(0.028 * side, 0, 0)
            tip = b + off * 1.6 + out * Ls + V(0, 0.0, -0.10)
            hair = smin(hair, seg(p, b + off - out * 0.02, tip, 0.058, 0.004), 0.018)
    return hair
def h_mohawk(p):      # 이마에서 목덜미까지 뒤로 누운 뾰족한 볏
    return _crest(p, 9, 0.20, 0.18, 0.02, 0.08), NONE(p), NONE(p)

def h_twin_tails(p):  # 머리를 덮고(앞 가르마) 귀 뒤 높이 묶어 양옆으로 퍼지는 꼬리 둘 · 고리
    m = mirror(p)
    full = cap(p, 0.026)
    parted = smax(full, -(np.abs(p[..., 0]) - 0.008), 0.006)
    c = np.where(p[..., 2] > 0.05, parted, full)
    T = on_head(112, 0.22, 0.06); hair = c
    for dx, dz, s in ((0.00, 0.03, 1.0), (0.04, -0.03, 0.92), (0.07, 0.02, 0.85), (0.02, -0.06, 0.95), (-0.02, 0.0, 0.9)):
        pts = bez(T, T + V(0.12, 0.08, -0.03), T + V(0.22, -0.10, -0.04 + dz), T + V(0.15 + dx, -0.58 * s, 0.02 + dz), 6)
        hair = smin(hair, lock(m, pts, 0.070, 0.012), 0.025)
    tie = ring(m, T + V(0.045, 0.012, -0.008), nrm(V(1, 0.3, -0.1)), 0.058, 0.014)
    return hair, NONE(p), tie

def h_long_braids(p): # 머리를 덮고 등까지 풀어 내린 다발 + 관자놀이에서 어깨 앞으로 땋은 둘(가죽 감개 · 쇠 고리)
    m = mirror(p); hair = cap(p, 0.026)
    for a in (138, 152, 166, 180, 194, 208, 222):
        pts = down(a, 0.26, -0.64 - 0.05 * np.cos(np.radians(a - 180) * 4), 7, 0.03, 0.06)
        hair = smin(hair, lock(p, pts, 0.065, 0.018), 0.03)
    bp = [on_head(76, 0.13, 0.03), on_head(84, 0.03, 0.05), on_head(92, -0.10, 0.06)]
    bp += [bp[-1] + V(0.015, -0.12, 0.07), bp[-1] + V(0.02, -0.24, 0.12), bp[-1] + V(0.02, -0.34, 0.15)]
    hair = smin(hair, O.chain(m, bp, 0.040, 0.030), 0.012)
    leather = np.minimum(O.cuff(m, bp[2] + V(0, 0.02, 0), bp[2] + V(0.004, -0.05, 0.02), 0.046),
                         O.cuff(m, bp[4] + V(0, 0.03, -0.01), bp[4] + V(0, -0.03, 0.02), 0.040))
    metal = O.cuff(m, bp[3] + V(0, 0.018, -0.006), bp[3] + V(0, -0.018, 0.006), 0.044)
    return hair, leather, metal

def h_spiky_bob(p):   # 머리를 덮고 이마를 덮는 뾰족 앞머리 · 턱까지 오는 옆 다발 · 위로 뻗친 끝
    hair = cap(p, 0.030)
    for a in (-48, -30, -12, 6, 24, 42):
        s = on_head(a, 0.25, 0.025); e = on_head(a * 1.1, 0.07 + 0.012 * np.cos(a), 0.04)
        hair = smin(hair, lock(p, [s, (s + e) * 0.5 + nrm(s) * 0.03, e], 0.045, 0.005), 0.012)
    for a in (72, 100, 128, 155, 180, 205, 232, 260, 288):
        s = on_head(a, 0.14, 0.03); e = on_head(a, -0.13 - 0.03 * np.sin(np.radians(a)) ** 2, 0.07)
        mid = (s + e) * 0.5 + nrm(V(s[0], 0, s[2])) * 0.03
        hair = smin(hair, lock(p, [s, mid, e], 0.052, 0.006), 0.012)
    for a in (150, 180, 210):
        b = on_head(a, 0.29, 0.0)
        hair = smin(hair, seg(p, b, b + V(0.03 * np.sin(np.radians(a)), 0.10, -0.07), 0.04, 0.004), 0.012)
    return hair, NONE(p), NONE(p)

def h_crest_tail(p):  # 짧은 볏 + 목덜미에서 가죽으로 감아 내린 꼬리
    hair = _crest(p, 6, 0.12, 0.20, 0.0, 0.07)
    R = on_head(180, -0.02, 0.04)
    hair = smin(hair, lock(p, [R, R + V(0, -0.15, -0.04), R + V(0, -0.32, -0.05), R + V(0, -0.46, -0.03)], 0.050, 0.010), 0.015)
    leather = O.cuff(p, R + V(0, -0.04, -0.012), R + V(0, -0.12, -0.035), 0.054)
    return hair, leather, NONE(p)

HAIRS = [('HighPony', h_high_pony), ('Mohawk', h_mohawk), ('TwinTails', h_twin_tails),
         ('LongBraids', h_long_braids), ('SpikyBob', h_spiky_bob), ('CrestTail', h_crest_tail)]

HAIR = (0.22, 0.19, 0.42); LEATHER = (0.42, 0.26, 0.15); METAL = (0.58, 0.60, 0.64); COPPER = (0.80, 0.45, 0.20)
def pieces(hair_fn, skin=None):
    import orc_female as Fm
    skin = skin or (0.50, 0.66, 0.28)
    P = [(Fm.head_f, skin), (O.mouth, O.RED), (O.teeth, O.BONE), (Fm.tusks_f, O.BONE), (O.eyes, O.DARK), (O.glint, O.WHITE),
         (Fm.lash, O.DARK), (Fm.lips_f, tuple(c * 0.72 for c in skin)), (Fm.body_f, LEATHER), (earrings, COPPER)]
    cache = {}
    def part(i):   # 머리 함수 한 번 돌린 결과를 셋이 나눠 쓴다(같은 p면 다시 안 돈다)
        def f(p):
            if cache.get('k') is not p: cache['k'] = p; cache['v'] = hair_fn(p)
            return cache['v'][i]
        return f
    P = P + [(part(0), HAIR), (part(1), LEATHER), (part(2), METAL)]
    st = getattr(hair_fn, 'stubble', None)
    if st is not None:
        col = HAIR if getattr(hair_fn, 'stubble_full', False) else tuple(skin[i] * 0.55 + HAIR[i] * 0.45 for i in range(3))   # 반삭 — 살과 머리색 사이
        P.append((st, col))
    return P
