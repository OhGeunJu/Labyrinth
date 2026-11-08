"""Orc v3 — v1 brow/eyes · v2 tusks · middle lip · pointed nose · hair six · beard six."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg
from orc_preview import head, front, y_of, W, H, T_, R_, EYE_T, MOUTH_T, NOSE_T, EY, MY, NY, EX, ER, render

# ── 오크 부품 ──
def brow(p):   # 눈 위 선반 — 안쪽 끝이 내려와 노려본다(v1)
    m = mirror(p); z = front(EYE_T + 0.12)
    a = V(0.025, EY + ER * 1.35 + 0.012, z + 0.010)
    b = V(EX + ER * 1.25, EY + ER * 1.55 + 0.03, z - 0.03)
    return seg(m, a, b, 0.030, 0.024)
def jaw(p):    # 넓은 아래턱 — 치비 턱이 모이는 자리를 네모로 채운다
    zc = front(0.12) - 0.13
    return rbox(p, V(0, y_of(0.13), zc), V(0.215, 0.085, 0.13), 0.075)
def lip_bend(p): return np.stack([p[..., 0], p[..., 1] + 0.9 * p[..., 0] ** 2, p[..., 2]], -1)   # 끝이 처진다
LIP_Z = front(MOUTH_T) + 0.040
def lower_lip(p):   # 내민 아랫입술 — v1과 v2의 가운데
    q = lip_bend(p)
    return capsule(q, V(-0.132, MY - 0.025, LIP_Z), V(0.132, MY - 0.025, LIP_Z), 0.038)
def mouth(p):
    q = lip_bend(p); z = front(MOUTH_T) + 0.012
    return rbox(q, V(0, MY + 0.010, z), V(0.12, 0.012, 0.02), 0.01)
def teeth(p):
    q = lip_bend(p); z = LIP_Z + 0.006; d = np.full(p.shape[:-1], 9.0, np.float32)
    for x in (-0.07, -0.025, 0.025, 0.07):
        d = np.minimum(d, seg(q, V(x, MY + 0.004, z), V(x * 1.02, MY + 0.026, z - 0.004), 0.010, 0.004))
    return d
def tusks(p):  # 아랫입술 **안에서** 솟는다(v2 길이)
    m = mirror(p); z = LIP_Z
    a = V(0.11, MY - 0.03, z); b = V(0.128, MY + 0.04, z + 0.014); c = V(0.142, MY + 0.085, z + 0.006)
    return np.minimum(seg(m, a, b, 0.026, 0.017), seg(m, b, c, 0.017, 0.003))
def nose(p):   # 눈썹 사이에서 내려와 끝이 서는 코 — 콧방울은 작게, 콧구멍은 끝 밑
    m = mirror(p); z = front(NOSE_T)
    bridge = seg(p, V(0, NY + 0.09, z - 0.030), V(0, NY - 0.005, z + 0.040), 0.018, 0.024)
    tip = seg(p, V(0, NY + 0.000, z + 0.042), V(0, NY - 0.035, z + 0.046), 0.026, 0.012)   # 끝이 아래로 선다
    wing = ell(m, V(0.035, NY + 0.010, z + 0.010), V(0.016, 0.015, 0.017))               # 콧방울은 위에 — V자
    d = smin(smin(bridge, tip, 0.015), wing, 0.020)
    return smax(d, -ell(m, V(0.019, NY - 0.008, z + 0.031), V(0.009, 0.007, 0.010)), 0.004)
def rot(q, ax, deg):
    a = np.radians(deg); c, s_ = np.cos(a), np.sin(a); x, y, z = q[..., 0], q[..., 1], q[..., 2]
    if ax == 'x': return np.stack([x, c * y - s_ * z, s_ * y + c * z], -1)
    if ax == 'y': return np.stack([c * x + s_ * z, y, -s_ * x + c * z], -1)
    return np.stack([c * x - s_ * y, s_ * x + c * y, z], -1)
def taper(px, py, a, b, ra, rb):   # 2D segment with radius going ra -> rb
    ab = np.array(b) - np.array(a); L = float((ab * ab).sum())
    k = np.clip(((px - a[0]) * ab[0] + (py - a[1]) * ab[1]) / L, 0, 1)
    return np.sqrt((px - a[0] - ab[0] * k) ** 2 + (py - a[1] - ab[1] * k) ** 2) - (ra + (rb - ra) * k)

def outline(u, v, s, tip):
    """Ear silhouette in its own plane. u = back from the joined edge, v = up. Broad upper body, small lobe, pointed tip."""
    body = taper(u, v, (0.045 * s, 0.00), tip, 0.050 * s, 0.006)
    upper = taper(u, v, (0.04 * s, 0.03 * s), (tip[0] * 0.75, tip[1] * 0.6), 0.055 * s, 0.03 * s)
    lobe = np.sqrt(((u - 0.035 * s) / 0.040 / s) ** 2 + ((v + 0.045 * s) / 0.040 / s) ** 2) * 0.040 * s - 0.040 * s
    return np.minimum(np.minimum(body, upper), lobe)

def make(flare, s, tip, thk=0.022, z0=-0.035, y0=None):
    y0 = EY - 0.03 if y0 is None else y0
    f = np.radians(flare)
    ua = np.array([np.sin(f), 0, -np.cos(f)], np.float32)   # back, swung out
    na = np.array([np.cos(f), 0, np.sin(f)], np.float32)    # plate normal (outer face)
    def ear(p):
        m = mirror(p)
        r0 = float(np.interp(EYE_T, T_, R_) * W)
        q = m - V(r0 - 0.01, y0, z0)
        u = (q * ua).sum(-1); v = q[..., 1]; n = (q * na).sum(-1)
        d2 = outline(u, v, s, tip)
        d2 = np.maximum(d2, -u - 0.03)                            # front edge runs into the head — the only joint
        plate = np.maximum(d2, np.abs(n) - thk)
        cup = np.maximum(outline(u - 0.006, v - 0.004, s * 0.78, (tip[0] * 0.8, tip[1] * 0.8)) + 0.004, -(n - thk * 0.3))
        return smax(plate, -cup, 0.004)
    return ear

EARS = {'Big': make(30, 1.0, (0.12, 0.20)), 'Mid': make(30, 0.88, (0.11, 0.18)), 'Small': make(30, 0.76, (0.10, 0.16))}
EAR_SIZE = 'Big'
def ear(p): return EARS[EAR_SIZE](p)

def orc_head(p):
    d = head(p)
    d = smin(d, jaw(p), 0.07)
    d = np.maximum(d, -H * 0.5 - p[..., 1])   # 턱 블렌드가 머리 바닥 밑으로 새지 않게
    d = smin(d, brow(p), 0.035)
    d = smin(d, nose(p), 0.015)
    d = smin(d, ear(p), 0.02)
    lip = lower_lip(p)
    tight, fill = smin(d, lip, 0.02), smin(d, lip, 0.08)   # 입술 밑만 넓게 메운다 — 위(입 · 이)는 그대로
    w = np.clip((MY - 0.02 - p[..., 1]) / 0.04, 0, 1); w = w * w * (3 - 2 * w)
    return tight + (fill - tight) * w

def eyes(p):
    m = mirror(p); z = front(EYE_T)
    return ell(m, V(EX, EY, z - 0.012), V(ER, ER * 1.1, 0.02))
def glint(p):
    m = mirror(p); z = front(EYE_T)
    return ell(m, V(EX - ER * 0.3, EY + ER * 0.3, z - 0.002), V(ER * 0.28, ER * 0.28, 0.02))

def NONE(p): return np.full(p.shape[:-1], 9.0, np.float32)
def chain(p, pts, r0, r1, k=0.012):   # 땋은 가닥 — 마디 타원을 줄줄이
    d = NONE(p); n = len(pts) - 1
    for i in range(n):
        a, b = pts[i], pts[i + 1]; r = r0 + (r1 - r0) * i / max(n - 1, 1)
        c = (a + b) * 0.5
        d = smin(d, ell(p, c, V(r, float(np.linalg.norm(b - a)) * 0.62, r)), k)
    return d
def cuff(p, a, b, r):   # 가닥을 감는 얇은 뼈 띠 — 가닥보다 살짝만 굵고 짧다, 가장자리는 둥글게
    ab = b - a; c = (a + b) * 0.5; L = float(np.linalg.norm(ab)); ax = ab / L
    q = p - c; h = (q * ax).sum(-1); rr = np.sqrt(np.maximum((q * q).sum(-1) - h * h, 0))
    return smax(rr - (r * 0.80 + 0.006), np.abs(h) - L * 0.5, 0.004)

# ── 머리카락 ── (머리카락, 장식)
def hairline(p, front_y, back_y):   # 이마 선 — 앞은 높고 뒤는 낮다
    k = np.clip(-p[..., 2] / 0.30 * 0.5 + 0.5, 0, 1) ** 0.7
    return (front_y + (back_y - front_y) * k) - p[..., 1]
HL_A = np.radians([0, 40, 80, 110, 145, 180]); HL_Y = np.array([0.24, 0.20, 0.13, 0.05, -0.14, -0.18])   # 이마는 높이 · 옆은 민다 · 뒤는 목덜미까지
def cap(p, th=0.014, fy=0.0, by=0.0):
    ang = np.abs(np.arctan2(p[..., 0], p[..., 2]))
    line = np.interp(ang, HL_A, HL_Y + fy)          # 각도마다의 이마 선 높이
    above = p[..., 1] - line
    k = np.clip(above / 0.07, 0, 1); k = k * k * (3 - 2 * k)   # 선에서 0 → 7cm 위에서 다 두께
    return smax(head(p) - th * k - 0.0005, -above, 0.004)
def h_bald(p): return NONE(p), NONE(p)
def h_topknot(p):
    root = seg(p, V(0, 0.30, -0.10), V(0, 0.40, -0.13), 0.06, 0.05)
    tail = seg(p, V(0, 0.40, -0.13), V(0, 0.46, -0.22), 0.05, 0.05)
    fall = seg(p, V(0, 0.46, -0.22), V(0, 0.12, -0.42), 0.05, 0.02)
    return smin(smin(root, tail, 0.03), fall, 0.03), cuff(p, V(0, 0.345, -0.115), V(0, 0.385, -0.125), 0.058)
def h_ponytail(p):
    knot = seg(p, V(0, 0.16, -0.24), V(0, 0.09, -0.33), 0.055, 0.055)
    tail = seg(p, V(0, 0.08, -0.34), V(0, -0.40, -0.42), 0.058, 0.025)
    return smin(knot, tail, 0.03), cuff(p, V(0, 0.095, -0.325), V(0, 0.065, -0.335), 0.064)
def h_mohawk(p):
    crest = NONE(p)
    for a in np.linspace(np.radians(35), np.radians(200), 14):
        c = V(0, 0.30 * np.sin(a) + 0.02, 0.30 * np.cos(a) - 0.03)
        crest = smin(crest, ell(p, c * 1.12, V(0.045, 0.085, 0.06)), 0.03)
    return crest, NONE(p)
def braid3(p, pts, r0, r1, n=16, side=(1.0, 0.0, 0.0)):   # 세 가닥 땋은 머리 — 마디가 좌우로 엇갈려 겹친다 · 붙어 있고 이음매만 잘록(엘프 `plait` + 엇갈림)
    P = np.array(pts, np.float32); seglen = np.linalg.norm(np.diff(P, axis=0), axis=1)
    s_ = np.concatenate([[0], np.cumsum(seglen)]); u = np.linspace(0, s_[-1], n * 2 + 1)
    Q = np.stack([np.interp(u, s_, P[:, i]) for i in range(3)], -1).astype(np.float32)
    sd = np.array(side, np.float32); d = NONE(p)
    for i in range(n):
        a, c, b = Q[2 * i], Q[2 * i + 1], Q[2 * i + 2]; r = r0 + (r1 - r0) * i / max(n - 1, 1)
        c = c + sd * r * 0.32 * (1 if i % 2 == 0 else -1)
        d = smin(d, smin(seg(p, a, c, r * 0.58, r), seg(p, c, b, r, r * 0.58), 0.004), 0.008)
    return d
def braid_ring(p, pts, r0, r1, frac, w=0.032):   # 땋은 가닥 위 고리 — 길을 따라 frac 자리 · 그 자리 굵기의 1.45배(엇갈린 마디까지 감싼다)
    # 옛 사슬 굵기에 맞춘 고리는 세 가닥 땋기의 엇갈린 마디 속에 묻혀 흰 조각만 삐져나왔다(11-08 사용자 "수염 장신구가 이상해졌어")
    P = np.array(pts, np.float32); L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))])
    at = lambda u: np.array([np.interp(u, L, P[:, k]) for k in range(3)], np.float32)
    u = frac * L[-1]; c = at(u); ax = at(min(u + 0.006, L[-1])) - at(max(u - 0.006, 0)); ax /= np.linalg.norm(ax)
    r = r0 + (r1 - r0) * frac
    return cuff(p, c - ax * w * 0.5, c + ax * w * 0.5, (1.45 * r - 0.006) / 0.8)
def h_warknot(p):   # 정수리 상투에서 등으로 땋아 내린 한 가닥 — v52까지는 마디(타원)가 떨어져 보였다(사용자) → 세 가닥 땋기 결로 촘촘히
    root = seg(p, V(0, 0.30, -0.10), V(0, 0.40, -0.13), 0.06, 0.05)
    pts = [V(0, 0.40, -0.14), V(0, 0.36, -0.30), V(0, 0.24, -0.40), V(0, 0.10, -0.44), V(0, -0.04, -0.46), V(0, -0.18, -0.47), V(0, -0.32, -0.47)]
    d = smin(root, braid3(p, pts, 0.046, 0.032, 18), 0.02)
    d = smin(d, seg(p, V(0, -0.32, -0.47), V(0, -0.40, -0.47), 0.026, 0.008), 0.01)               # 끝 술
    return d, np.minimum(cuff(p, V(0, 0.345, -0.115), V(0, 0.385, -0.125), 0.058), braid_ring(p, pts, 0.046, 0.032, 0.82))
def h_warknot_old(p):
    root = seg(p, V(0, 0.30, -0.10), V(0, 0.40, -0.13), 0.06, 0.05)
    pts = [V(0, 0.40, -0.14), V(0, 0.36, -0.30), V(0, 0.24, -0.40), V(0, 0.10, -0.44), V(0, -0.04, -0.46), V(0, -0.18, -0.47), V(0, -0.32, -0.47)]
    return smin(root, chain(p, pts, 0.045, 0.032), 0.02), np.minimum(cuff(p, V(0, 0.345, -0.115), V(0, 0.385, -0.125), 0.058),
                                                                    cuff(p, V(0, -0.20, -0.47), V(0, -0.23, -0.47), 0.038))
def h_braids(p):   # 관자놀이 뒤에서 땋은 두 가닥 — 귀 뒤로 내려가 귀 밑을 크게 돌아 어깨 앞으로
    # 귀 위를 지나 앞으로 내리면 귀 처리에 지워졌고(11-08 사용자 게임 화면), 타원 사슬은 마디가 끊겨 보였다 → 세 가닥 땋기 · 귀 처리(귀 밑 12cm)보다 아래로 돈다
    m = mirror(p)
    pts = [V(0.20, 0.18, -0.20), V(0.25, 0.08, -0.23), V(0.28, -0.05, -0.24), V(0.29, -0.20, -0.23), V(0.30, -0.34, -0.18),
           V(0.31, -0.42, -0.06), V(0.30, -0.50, 0.06), V(0.29, -0.58, 0.13)]
    br = braid3(m, pts, 0.036, 0.027, 24, side=(0.0, 0.0, 1.0))
    br = smin(br, seg(m, V(0.29, -0.58, 0.13), V(0.285, -0.66, 0.16), 0.024, 0.008), 0.01)   # 끝 술
    deco = np.minimum(braid_ring(m, pts, 0.036, 0.027, 0.62), braid_ring(m, pts, 0.036, 0.027, 0.93))
    return br, deco

# ── 숱이 있는 머리 셋(2026-11-06 — 사용자 그림 `_refs/2026-11-06_orc/`) ──
# 위의 다섯은 민머리에 가닥만 얹었다. 이 셋은 **머리 전체를 덮는 숱 껍데기**(`cap`)에서 시작한다.
def lock(p, a_deg, y0, y1, r0, r1, out0=0.27, out1=0.36, sway=0.0):   # 귀 뒤에서 흘러내리는 다발 하나(각도는 앞 0° · 뒤 180°)
    a = np.radians(a_deg); d = np.array([np.sin(a), 0.0, np.cos(a)], np.float32)
    s = d * out0 + V(0, y0, 0); m = d * (out0 + out1) * 0.5 + V(sway, (y0 + y1) * 0.5, 0); e = d * out1 + V(sway * 1.5, y1, 0)
    return smin(seg(p, s, m, r0, (r0 + r1) * 0.5), seg(p, m, e, (r0 + r1) * 0.5, r1), 0.02)
def ease(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)
def angle(p): return np.degrees(np.abs(np.arctan2(p[..., 0], p[..., 2])))
def oscalp(p, th, low, k=0.02):   # 숱 = 머리를 고르게 키운 껍데기(엘프 `scalp`와 같은 셈 — 그냥 부풀리면 정수리만 솟는다) · 아랫선 위만
    sc = 1 + np.asarray(th, np.float32) / 0.30
    s3 = sc[..., None] if np.ndim(sc) else sc
    return smax(head(p / s3) * sc, low - p[..., 1], k)
CAM = np.array([0.0, 0.82, 0.57], np.float32)   # 게임 카메라 쪽(앞 위 55°)
def ear_gap(p, d, ear_fn):   # **귀는 머리카락에 안 가린다**(사용자 · B) — 귀 모양을 바깥 · 아래 · 카메라 쪽으로 밀어 낸 자국까지 숱에서 덜어 낸다.
    # 옆으로만 덜면 옆에서는 보여도 게임 카메라(앞 위)에서는 관자놀이 숱이 귀 앞을 가렸다(PonyA 51% · SpikyBob 89% — 메시로 잰 값).
    sx = np.sign(p[..., 0]); cut = NONE(p)
    for k in (-0.05, -0.025, 0.0, 0.01):
        for dn in (0.0, 0.06, 0.12):                       # 바깥 × 아래(귀 밑에 깔리지 않게)
            q = p.copy(); q[..., 0] = p[..., 0] + sx * k; q[..., 1] = p[..., 1] + dn
            cut = np.minimum(cut, ear_fn(q))
        for t in (0.03, 0.06):                             # 바깥 × 앞(수평) — 위로는 안 민다: 귀 위에 쐐기 홈이 팼다(11-07 사용자)
            q = p.copy(); q[..., 0] = p[..., 0] + sx * k; q[..., 2] = p[..., 2] - t
            cut = np.minimum(cut, ear_fn(q))
    return smax(d, -(cut - 0.004), 0.004)
def ear_cut(p, d): return ear_gap(p, d, ear)
def with_ear_gap(h):   # (머리카락, 장식) 함수의 머리카락만 귀 자리를 덜어 낸다 · 민 층(`stubble`)도 같이
    def f(p):
        r = h(p); return (ear_cut(p, r[0]),) + tuple(r[1:])
    f.__name__ = h.__name__
    st = getattr(h, 'stubble', None)
    if st is not None: f.stubble = lambda p, st=st: ear_cut(p, st(p))
    return f
def poly(p, pts, radii, k=0.02):
    d = NONE(p)
    for i in range(len(pts) - 1): d = smin(d, seg(p, pts[i], pts[i + 1], radii[i], radii[i + 1]), k)
    return d
def flatx(p, F): return p * np.array([F, 1, 1], np.float32)
def panel(p, pts, radii, F=1.8):   # 넓적한 머리 판 — 얼굴 옆(x)으로 얇다(엘프 앞 판)
    return poly(flatx(p, F), [flatx(t, F) for t in pts], [r * F for r in radii]) / F

# 갈기(올백) — 이마선은 높고 앞이 들려 뒤로 넘어간다 · 결은 앞에서 뒤로(위) · 귀 위는 수평으로 뒤로(옆) · 뒤는 어깨까지 한 덩어리
def h_swept(p):   # v30 올백(평평한 이마선) — **사람 쪽으로 옮길 머리**(2026-11-06 사용자: "원래 사용된 헤어는 인간쪽에") · 오크는 M자
    return h_mane(p, flat=True)
def h_mane(p, flat=False):
    ang = angle(p)
    if flat:   # 앞에서 평평하게 가로지르고 관자놀이에서만 내려간다 — 가운데부터 내려가면 ∧로 꺾여 5:5 가르마로 보였다(v29 사용자)
        low = np.interp(ang, [0, 40, 70, 100, 140, 180], [EY + 0.25, EY + 0.25, EY + 0.14, EY + 0.06, -0.20, -0.30])
    else:
        # **M자 + 얼굴 테**(사용자 그림 `_refs/2026-11-06_orc/want_mane_v33_target.png` — 거의 그대로) — 이마는 넓게 드러나고
        # M은 이마 양 끝 두 군데만 고양이 귀처럼 뾰족하게 올라간다(가운데는 조금만 내려온다 — v32는 가운데가 이마를 덮었다).
        # 꼭지 바깥에서 가파르게 내려와 관자놀이부터 **얼굴 옆을 타고 수염 높이까지** — 머리와 수염이 얼굴 둘레를 한 테로 감싼다.
        # 그림에서 잰 자리(머리 폭 기준) — 가운데 y 0.10 · 꼭지 x ±0.17(36°) y 0.21 · 테는 x 0.25 밖(64° 밖)만 — v33은 테가 볼을 덮어 얼굴이 좁았다
        # 꼭지 사이는 거의 수평인 띠(U로 처지거나 V로 찌르면 안 된다 · v35 · v36) · 꼭지 직전에서만 솟고 바깥은 거의 수직으로 떨어져 얼굴 옆 테가 된다
        # v38 — 꺾은선으로 이으니 각져 "자연스럽지 않다"(사용자) → 수평 띠 + 둥근 꼭지(종 모양) + 부드럽게 떨어지는 옆선.
        # 얼굴 옆 테는 **입 높이에서 끝난다** — 수염은 그보다 안쪽 · 아래에서 시작해 이음매가 안으로 들어간다(일자로 붙으면 이어 붙인 띠 같았다).
        # 앞은 **두 경계**로 매긴다 — 위는 이마 띠(x마다 높이) · 옆은 높이마다 x(얼굴 옆 선). 옆선을 x 한 값으로 두면 일자로 떨어져
        # 그림과 다를 수밖에 없다(v40 사용자) → 꼭지에서 바깥으로 완만하게 휘며 내려가 코밑 높이에서 둥글게 끝난다.
        side_end = MY - 0.01                                                                    # 얼굴 옆 띠가 수염 높이까지 내려와 이어진다(v50 사용자)
        ax, y = np.abs(p[..., 0]), p[..., 1]
        top = (EY + 0.23) + 0.08 * ease((ax - 0.10) / 0.085)                                     # 올백 시작을 올렸다(v44 사용자)                                     # 수평 띠 → 꼭지(x 0.185 · y 0.21 — v40은 너무 높았다)
        # 옆선은 **머리 둘레 각도**로 — x로 두면 머리 옆면은 다 x 0.25를 넘어 귀 앞 볼 전체가 판처럼 덮였다(v41 사용자 그림
        # `bad_mane_v41_side_slab.png`). 관자놀이 위는 넓게, 볼로 내려갈수록 귀 쪽으로 물러나는 좁은 띠.
        # 옆모습 그림 `want_mane_side.png` — 이마 위에서 대각선으로 뒤 · 아래로: 눈 높이는 머리 앞뒤의 중간쯤 · 볼은 귀 바로 앞에서 수염과 만난다
        aedge = np.interp(-y, [-0.24, -0.12, 0.08, 0.155], [40.0, 50.0, 62.0, 76.0])   # 귀 둘레 · 귀 앞 볼 아래는 머리카락(v45는 귀까지 살이었다)
        m_front = np.minimum(top - y, smax((aedge - ang) * 0.0052, side_end - y, 0.03))
        m_back = np.interp(ang, [95, 140, 180], [side_end, -0.28, -0.30]) - y
        low = None
    # 앞 정수리는 조금만 들린다 — 머리 둥근 선을 따라 덮는다(6cm 들리면 이마 꼭대기가 불룩 솟았다 · v49 사용자) · |x|로 줄이면 가운데에 능선이 선다
    lift = 0.026 + (0.034 if flat else 0.008) * np.clip(1 - (p[..., 0] / 0.34) ** 2, 0, 1) * ease((p[..., 2] + 0.05) / 0.30)
    if low is None:   # M자 — 앞 두 경계 · 뒤는 높이
        # 두께는 경계에서 0으로 살에 붙어 시작해 안쪽(뒤)으로 갈수록 차오른다 — 경계에서 바로 꽉 차면 직각 턱이 서서 투구 테처럼 보였다(v43 사용자)
        mask = np.where(ang < 95, m_front, m_back)
        grow = 0.15 + 0.85 * ease(-mask / 0.09)
        sc = 1 + lift * grow / 0.30
        # 껍데기는 오크 얼굴 겉면(광대 · 턱)도 덮는다 — 둥근 바탕 머리만 키우면 옆으로 튀어나온 광대 · 턱이 얇은 숱을 뚫고 나와
        # 옆선을 어디 두든 귀 앞이 살로 보였다(v45~v47). 코 · 입은 경계 밖(앞)이라 안 덮인다.
        face = smax(orc_head(p) - lift * grow * 0.45, -(ear(p) - 0.035), 0.01)                 # 귀 둘레는 빼고(귀가 묻혔다) · 얇게(턱 옆이 네모 판처럼 튀었다)
        shell = np.minimum(head(p / sc[..., None]) * sc, face)
        d = smax(shell, mask, 0.012)
    else:
        d = oscalp(p, lift, low, k=0.02)
    w = ease((np.abs(p[..., 0]) - 0.16) / 0.12)                                           # 위 → 옆
    comb = (1 - w) * np.sin(p[..., 0] * 70) + w * np.sin((p[..., 1] - 0.25 * p[..., 2]) * 60)
    # 결 골은 이마선에서 0 → 안쪽으로 깊어진다 — 얇은 숱 끝까지 파면 이마선이 톱니가 되고 한가운데가 V로 파였다(11-08 사용자 '구멍')
    fade = ease(-mask / 0.06) if low is None else ease((p[..., 1] - low) / 0.06)
    d = d + 0.0035 * comb * fade
    back = ell(p, V(0, -0.08, -0.10), V(0.36, 0.40, 0.25))
    back = smax(back, (105 - ang) * 0.004, 0.03)
    back = smax(back, -(p[..., 1] + 0.36), 0.03)
    d = smin(d, back + 0.003 * np.sin(p[..., 0] * 60), 0.05)
    for a_, y1, r in ((112, -0.42, 0.072), (138, -0.48, 0.078), (160, -0.52, 0.080), (180, -0.53, 0.080),
                      (200, -0.52, 0.080), (222, -0.48, 0.078), (248, -0.42, 0.072)):
        d = smin(d, lock(p, a_, -0.12, y1, r, r * 0.35, 0.31, 0.35), 0.05)
    return ear_cut(p, d), NONE(p)

# 상투 꽁지 — 숱이 사방에서 상투로 빗겨 모이고, 상투 끝에서 꽁지가 나와 뒤로 휘어 **다리께까지** 내려오는 긴 장발(사용자 그림 · v38:
# "꽁지 부분을 만들어줘 · 거의 다리까지 · 너무 짧아"). 꽁지는 규칙대로 — 끈 자리 잘록 · 가늘게 나와 불룩했다가 가늘게 · 끝은 말꼬리로 갈라진다.
KNOT = V(0, 0.31, -0.05)
TOPK = V(0, 0.45, -0.08)
LONG_R = [0.030, 0.048, 0.064, 0.074, 0.078, 0.076, 0.072, 0.066, 0.058, 0.048, 0.036, 0.024, 0.012]   # TAIL_R을 길이에 맞춰 늘린 것
def h_knottail(p):
    d, deco = h_hookknot(p, hook=False)
    tail = NONE(p)
    for dx, dz, sc in ((-0.05, 0.00, 0.90), (-0.025, -0.03, 1.0), (0.0, 0.02, 1.05), (0.025, -0.02, 1.0), (0.05, 0.01, 0.90), (0.0, -0.05, 0.96)):
        pts = bez(TOPK, TOPK + V(dx * 0.3, 0.09, -0.10), TOPK + V(dx * 1.0, -0.02, -0.46), TOPK + V(dx * 2.4, -1.65 * sc, -0.42 + dz), 12)
        tail = smin(tail, poly(p, pts, LONG_R, 0.006), 0.03)
    d = smin(d, tail + 0.002 * np.sin(p[..., 0] * 60), 0.03)
    return d, deco
def h_hookknot(p, hook=True):
    ang = angle(p)
    low = np.interp(ang, [0, 40, 75, 100, 140, 180], [EY + 0.24, EY + 0.24, EY + 0.11, EY + 0.04, -0.18, -0.26])   # 앞은 평평하게(가르마 금지)
    d = oscalp(p, 0.022, low)
    phi = np.arctan2(p[..., 0], p[..., 2] - KNOT[2])
    d = d + 0.0035 * np.sin(phi * 22) * ease((0.36 - np.linalg.norm(p - KNOT, axis=-1)) / 0.25 + 0.3) * ease((p[..., 1] - low) / 0.06)   # 상투로 모이는 결 — 이마선에서는 0(얇은 숱을 뚫어 V로 파였다)
    d = smin(d, ell(p, V(0, 0.33, -0.05), V(0.10, 0.07, 0.10)), 0.05)                     # 모인 숱이 부풀어 오른 밑동
    d = smin(d, seg(p, V(0, 0.34, -0.05), V(0, 0.44, -0.07), 0.080, 0.072), 0.03)
    if hook:
        pts = [V(0, 0.44, -0.07), V(0, 0.51, -0.12), V(0, 0.53, -0.20), V(0, 0.49, -0.27), V(0, 0.42, -0.30)]
        d = smin(d, chain(p, pts, 0.072, 0.036, 0.025), 0.03)
    deco = np.minimum(cuff(p, V(0, 0.385, -0.060), V(0, 0.420, -0.064), 0.086), cuff(p, V(0, 0.360, -0.057), V(0, 0.375, -0.058), 0.090))
    return ear_cut(p, d), deco

# 높은 꽁지 — 앞은 엘프 생머리처럼 가운데 가르마에서 이마 양쪽으로 둥글게 흘러내려 관자놀이에서 꺾여 얼굴 옆으로 떨어진다 · 정수리 뒤에서 묶어 솟았다 등까지
def bez(a, b, c, e, n):
    return [a * (1 - t) ** 3 + b * 3 * (1 - t) ** 2 * t + c * 3 * (1 - t) * t * t + e * t ** 3 for t in np.linspace(0, 1, n + 1)]
TAIL_R = [0.030, 0.050, 0.074, 0.080, 0.070, 0.056, 0.042, 0.026, 0.012]   # 엘프 · 여성 오크와 같은 꽁지 굵기
TIE = V(0, 0.33, -0.17)
def part_groove(p): return 0.012 * np.exp(-(p[..., 0] / 0.013) ** 2) * (p[..., 2] > -0.05) * (p[..., 1] > 0.12)
def h_hightail(p):
    ang = angle(p)
    top, temple, burn = EY + 0.28, EY + 0.06, EY - 0.14
    fr = np.where(ang < 50, top + (temple - top) * ease(ang / 50) ** 0.8, temple + (burn - temple) * ease((ang - 50) / 38))
    low = np.where(ang < 90, fr, burn + (-0.30 - burn) * ease((ang - 90) / 70))
    d = oscalp(p, 0.030, low) + part_groove(p) + 0.003 * np.sin(p[..., 0] * 55 + 0.6 * np.sin(p[..., 1] * 20))
    m = mirror(p)
    side = panel(m, [V(0.17, EY + 0.20, 0.23), V(0.25, EY + 0.08, 0.17), V(0.29, EY - 0.08, 0.10), V(0.31, MY - 0.10, 0.05), V(0.33, -0.56, 0.02)],
                 [0.030, 0.044, 0.050, 0.046, 0.024])
    d = smin(d, side, 0.03)
    back = smax(ell(p, V(0, -0.10, -0.10), V(0.35, 0.36, 0.24)), (110 - ang) * 0.004, 0.03)
    d = smin(d, smax(back, -(p[..., 1] + 0.40), 0.03), 0.05)                              # 묶고 남은 뒷머리
    # 꽁지 — **정해 둔 규칙 그대로**(엘프 `h_tail` · 여성 오크): 끈 자리 잘록 · 가늘게 나와 불룩했다가 가늘게(`TAIL_R`) ·
    # 끈에서 위로 솟았다가 휘어 내리고 끝은 말꼬리처럼 가닥이 갈라진다. 굵기가 일정한 밧줄 하나로 빚었다가 지적받았다(v30).
    tail = NONE(p)
    for dx, dz, sc in ((-0.05, 0.00, 0.88), (-0.025, -0.03, 1.0), (0.0, 0.02, 1.06), (0.025, -0.02, 1.0), (0.05, 0.01, 0.88), (0.0, -0.05, 0.95)):
        pts = bez(TIE, TIE + V(dx * 0.3, 0.14, -0.05), TIE + V(dx * 0.9, 0.11, -0.36), TIE + V(dx * 2.0, -0.95 * sc, -0.22 + dz), 8)
        tail = smin(tail, poly(p, pts, [r * 1.08 for r in TAIL_R], 0.006), 0.03)
    d = smin(d, tail + 0.002 * np.sin(p[..., 0] * 60), 0.03)
    tie = cuff(p, TIE + V(0, 0.010, 0.005), TIE + V(0, 0.048, -0.010), 0.046)   # 끈 — 잘록한 목보다 살짝만 굵다
    return ear_cut(p, d), tie

# ── 남자 새 머리 여섯(2026-11-08 사용자: "오크 남자 머리 6개 정도 더 — 밸런스") ──
# 규칙: 꽁지는 끈 잘록 · TAIL_R · 말꼬리 / 풀어 내린 뒷머리는 겉면에 붙은 납작 가닥 / 숱은 고르게 키운 껍데기 · 결 골은 이마선에서 0 / 귀 처리.
def surf_pt(a_deg, y, out=0.0):   # 방위각 a(앞 0 · 뒤 180) · 높이 y의 머리 겉면 점 + 바깥 out
    a = np.radians(a_deg); dx, dz = np.sin(a), np.cos(a); lo, hi = 0.0, 0.6
    for _ in range(40):
        r = (lo + hi) * 0.5
        if float(head(np.array([[dx * r, y, dz * r]], np.float32))[0]) < 0: lo = r
        else: hi = r
    return V(dx * (lo + out), y, dz * (lo + out))
def surf_yz(x0, th_deg, out=0.0):   # x = x0 평면에서 각도 th(앞 0 · 위 90 · 뒤 180)의 머리 겉면 점 + 바깥 out
    t = np.radians(th_deg); dy, dz = np.sin(t), np.cos(t); lo, hi = 0.0, 0.6
    for _ in range(40):
        r = (lo + hi) * 0.5
        if float(head(np.array([[x0, dy * r, dz * r]], np.float32))[0]) < 0: lo = r
        else: hi = r
    n = np.array([x0 * 0.5, dy, dz], np.float32); n /= np.linalg.norm(n)
    return V(x0, dy * lo, dz * lo) + n * out
def nflat(p, pts, radii, n, F=2.2):   # 납작한 가닥 — 바깥 방향(n)으로 얇다(엘프 `nlock`)
    n = np.asarray(n, np.float32)
    def sq(q): return q + (F - 1) * (q @ n)[..., None] * n
    return poly(sq(p), [sq(t) for t in pts], [r * F for r in radii], 0.02) / F
M_LINE = ([0, 40, 75, 100, 140, 180], [EY + 0.24, EY + 0.24, EY + 0.10, EY + 0.04, -0.18, -0.26])   # 남자 기본 이마선(평평한 앞)
def m_low(p): return np.interp(angle(p), *M_LINE)
def shell(p, th, low, k=0.012, grow=True):   # 고르게 키운 숱 — 두께는 경계에서 차오른다(투구 턱 방지)
    g = (0.25 + 0.75 * ease((p[..., 1] - low) / 0.07)) if grow else 1.0
    sc = 1 + th * g / 0.30
    sc3 = sc[..., None] if np.ndim(sc) else sc
    return smax(head(p / sc3) * sc, low - p[..., 1], k)

def buzz_layer(p):   # 짧게 민 층 — 살 위 얇은 층(색은 살과 머리색 사이 — 머리색이면 투구 같았다 · v71)
    low = m_low(p)
    return shell(p, 0.007, low, 0.006, grow=False)
def h_buzz(p):   # 짧게 민 머리 — 민 층 하나뿐
    return NONE(p), NONE(p)
h_buzz.stubble = buzz_layer

def h_undercut(p):   # 옆 · 뒤는 밀고(얇은 층) 윗머리만 길게 남겨 뒤로 넘긴다 — 정수리 뒤에서 목덜미 쪽으로 살짝 뻗친다
    low = m_low(p)
    ax = np.abs(p[..., 0])
    band = 0.17 + 0.03 * ease(-p[..., 2] / 0.25)                                  # 위 띠 반폭(뒤로 조금 넓다)
    top_low = np.where(p[..., 2] > -0.12, EY + 0.24, EY + 0.24 - 0.9 * (-0.12 - p[..., 2]))   # 뒤로 갈수록 내려간다
    top = shell(p, 0.050, np.maximum(top_low, low), 0.012)
    top = smax(top, ax - band, 0.02)
    w = ease((p[..., 1] - (EY + 0.24)) / 0.06)
    top = top + 0.003 * np.sin(p[..., 0] * 70) * w                                 # 뒤로 빗은 결(이마선에서 0)
    flick = poly(p, [V(0, 0.10, -0.30), V(0, 0.0, -0.34), V(0, -0.07, -0.31)], [0.075, 0.06, 0.02], 0.02)   # 목덜미 쪽 뻗친 끝
    return smin(top, flick, 0.03), NONE(p)
h_undercut.stubble = buzz_layer   # 민 옆 · 뒤는 민 층 색으로(윗머리와 갈린다)

def h_loose_dreads(p):   # 굵은 레게 다발을 묶지 않고 어깨까지 풀어 내린다 — 다발은 따로따로(울퉁불퉁한 줄)
    hair = shell(p, 0.016, m_low(p), 0.01)
    k = 0
    # 내려오는 자리는 다발마다 따로 — 위 줄 열은 귀 뒤 128°~232°에 고르게, 아래 줄 여섯은 그 사이사이 조금 바깥(같은 길로 몰면 둘이 겹쳤다 · v74 사용자)
    top_a = (100, 118, 135, 155, 170, 190, 205, 225, 242, 260); cols = np.linspace(128, 232, 10)
    low_a = (135, 155, 170, 190, 205, 225); mids = ((cols[:-1] + cols[1:]) * 0.5)[[1, 2, 3, 5, 6, 7]]
    for a, ac, y0, out in [(a, c, 0.18, 0.035) for a, c in zip(top_a, cols)] + [(a, c, 0.04, 0.060) for a, c in zip(low_a, mids)]:
            s = surf_pt(a, y0, 0.010); c = surf_pt(ac, -0.06, out)
            e = V(c[0] * 1.08, -0.44 + 0.04 * ((k * 5) % 3) / 2, c[2] * 1.08)
            pts = [s, V((s[0] + c[0]) * 0.5, (y0 - 0.06) * 0.5, (s[2] + c[2]) * 0.5) * 1.04, c, e]
            rr = [0.026 * (1 + 0.12 * np.sin(k * 1.7 + i)) for i in range(4)]
            hair = smin(hair, poly(p, pts, rr, 0.006), 0.012); k += 1
    return hair, NONE(p)

def h_cornrows(p):   # 이마에서 목덜미까지 두피를 따라 줄지어 땋고(다섯 줄), 끝은 짧은 땋은 가닥으로 늘어뜨린다
    hair = shell(p, 0.008, m_low(p), 0.006, grow=False)
    for x0 in (-0.13, -0.065, 0.0, 0.065, 0.13):
        path = [surf_yz(x0, th, 0.018) for th in np.linspace(38, 205, 14)]
        end = path[-1]
        path += [end + V(0, -0.10, -0.02), end + V(0, -0.20, -0.02)]
        hair = smin(hair, braid3(p, path, 0.026, 0.020, 26, side=(1.0, 0.0, 0.0)), 0.006)
    return hair, NONE(p)

def h_spikes(p):   # 짧은 머리가 사방으로 뾰족하게 뻗친다 — 숱 위에 짧은 뿔 가닥
    hair = shell(p, 0.022, m_low(p), 0.01)
    k = 0
    for a in range(0, 360, 40):
        for y0 in (0.30, 0.20, 0.08):
            if y0 == 0.08 and (a < 60 or a > 300): continue
            if y0 == 0.30 and a % 80: continue
            s = surf_pt(a, y0, 0.0); n = np.array([s[0], s[1] * 1.6, s[2]], np.float32); n /= np.linalg.norm(n)
            L = 0.08 + 0.03 * ((k * 7) % 3) / 2
            tip = s + n * L + V(0, 0.02, -0.03)
            hair = smin(hair, seg(p, s, tip, 0.032, 0.003), 0.014); k += 1
    return hair, NONE(p)

def h_halfup(p):   # 반묶음 — 윗머리만 정수리 뒤에서 묶어 짧은 꽁지 · 아랫머리는 어깨까지 풀어 내린다(겉면에 붙은 납작 가닥)
    hair = shell(p, 0.024, m_low(p), 0.012)
    R = surf_pt(180, 0.24, 0.04)
    for dx, dz, sc in ((-0.03, 0.0, 0.9), (0.0, 0.02, 1.0), (0.03, -0.01, 0.92), (0.0, -0.03, 0.95)):
        pts = bez(R, R + V(dx * 0.3, 0.08, -0.05), R + V(dx * 0.8, 0.02, -0.17), R + V(dx * 1.6, -0.30 * sc, -0.18 + dz), 8)
        hair = smin(hair, poly(p, pts, [r * 0.8 for r in TAIL_R], 0.006), 0.03)        # 짧은 꽁지(규칙대로)
    for a in np.linspace(125, 235, 9):   # 귀 뒤에서만 — 귀 쪽 가닥(105° · 120°)은 귀에 걸려 끊겼다(11-08 덩어리 세기)
        n = np.array([np.sin(np.radians(a)), 0.0, np.cos(np.radians(a))], np.float32)
        top = surf_pt(a, 0.10, -0.004); mid = surf_pt(a, -0.06, 0.012)
        y_end = -0.46 + 0.14 * ((a - 180) / 75) ** 2                                   # 가운데가 길고 옆으로 짧다(네모 판처럼 뭉뚝했다 · v71)
        pts = [top, mid, V(mid[0], (y_end - 0.06) * 0.5, mid[2]) + n * 0.006, V(mid[0], y_end, mid[2]) + n * 0.014]
        hair = smin(hair, nflat(p, pts, [0.050, 0.052, 0.042, 0.014], n), 0.02)
    tie = cuff(p, R + V(0, 0.012, 0.004), R + V(0, -0.025, -0.012), 0.050)
    return hair, tie

h_topknot, h_warknot, h_ponytail, h_mohawk, h_braids = (with_ear_gap(h) for h in (h_topknot, h_warknot, h_ponytail, h_mohawk, h_braids))   # 귀 B
h_buzz, h_undercut, h_loose_dreads, h_cornrows, h_spikes, h_halfup = (with_ear_gap(h) for h in (h_buzz, h_undercut, h_loose_dreads, h_cornrows, h_spikes, h_halfup))
HAIRS = [('Bald', h_bald), ('Topknot', h_topknot), ('WarKnot', h_warknot), ('Ponytail', h_ponytail), ('Mohawk', h_mohawk), ('Braids', h_braids),
         ('Mane', h_mane), ('KnotTail', h_knottail), ('HighTail', h_hightail),
         ('Buzz', h_buzz), ('Undercut', h_undercut), ('LooseDreads', h_loose_dreads), ('Cornrows', h_cornrows), ('Spikes', h_spikes), ('HalfUp', h_halfup)]

# ── 수염 ── (수염, 장식) — 턱 앞(아랫입술 밑)에서 시작한다
CHIN_Y = MY - 0.10; CHIN_Z = LIP_Z - 0.03
def patch(p, half=0.10):   # 입술 밑 턱을 덮는 수염 밑동
    d = smax(orc_head(p) - 0.022, p[..., 1] - (MY - 0.055), 0.015)
    return smax(smax(d, np.abs(p[..., 0]) - half, 0.02), -(p[..., 2] - 0.15), 0.02)
def b_none(p): return NONE(p), NONE(p)
def b_braid(p):
    root = patch(p)
    pts = [V(0, CHIN_Y, CHIN_Z), V(0, CHIN_Y - 0.08, CHIN_Z + 0.01), V(0, CHIN_Y - 0.16, CHIN_Z + 0.03),
           V(0, CHIN_Y - 0.24, CHIN_Z + 0.06), V(0, CHIN_Y - 0.31, CHIN_Z + 0.09)]
    br = braid3(p, pts, 0.055, 0.036, 14, side=(1.0, 0.0, 0.0))   # 타원 사슬은 마디가 끊겨 보였다(11-08) → 세 가닥 땋기
    tuft = seg(p, V(0, CHIN_Y - 0.31, CHIN_Z + 0.09), V(0, CHIN_Y - 0.41, CHIN_Z + 0.12), 0.04, 0.012)
    return smin(smin(root, br, 0.025), tuft, 0.02), braid_ring(p, pts, 0.055, 0.036, 0.80)
def b_twin(p):
    m = mirror(p)
    root = patch(p, 0.12)
    pts = [V(0.06, CHIN_Y, CHIN_Z), V(0.065, CHIN_Y - 0.08, CHIN_Z + 0.01), V(0.07, CHIN_Y - 0.16, CHIN_Z + 0.03),
           V(0.07, CHIN_Y - 0.24, CHIN_Z + 0.06)]
    br = braid3(m, pts, 0.042, 0.032, 10, side=(1.0, 0.0, 0.0))   # 세 가닥 땋기(11-08)
    return smin(root, br, 0.025), braid_ring(m, pts, 0.042, 0.032, 0.78)
def b_shaggy(p):   # 입술 바로 밑에서 턱을 덮는 짧은 수염(v25 L)
    d = smax(jaw(p) - 0.03, p[..., 1] - (MY - 0.05), 0.02)
    d = smax(d, -p[..., 2], 0.03)
    yb = p[..., 1] + 1.4 * p[..., 0] ** 2
    up = smax(smax(orc_head(p) - 0.022, yb - (MY - 0.045), 0.012), -p[..., 2], 0.03)
    d = smin(d, up, 0.02)
    return smin(d, ell(p, V(0, CHIN_Y - 0.005, LIP_Z - 0.005), V(0.14, 0.055, 0.05)), 0.04), NONE(p)
def b_goatee(p):
    return smin(patch(p, 0.05), seg(p, V(0, CHIN_Y, CHIN_Z), V(0, CHIN_Y - 0.14, CHIN_Z + 0.04), 0.045, 0.012), 0.02), NONE(p)
def b_mutton(p):   # 귀 앞 구레나룻이 턱선을 따라 내려와 턱 끝에서 만난다 — 볼과 입 둘레는 맨살
    m = mirror(p)
    path = [(V(0.285, EY - 0.04, -0.03), 0.040), (V(0.272, MY - 0.01, 0.04), 0.046), (V(0.21, MY - 0.085, 0.12), 0.052),
            (V(0.08, CHIN_Y - 0.03, CHIN_Z), 0.056), (V(0.0, CHIN_Y - 0.04, CHIN_Z + 0.005), 0.058)]
    vol = NONE(p)
    for (a, ra), (b, rb) in zip(path, path[1:]): vol = smin(vol, seg(m, a, b, ra, rb), 0.02)
    return smax(orc_head(p) - 0.020, vol, 0.012), NONE(p)
def full_beard(p, length, width, depth, inset=False):   # 구레나룻 → 턱선 → 턱 전체를 덮고 가슴께로 늘어지는 덩어리
    m = mirror(p)
    if inset:   # 머리카락 테가 입 높이에서 끝나는 머리(Mane)용 — 그보다 안쪽 · 아래에서 시작해 이음매가 오목하게 들어간다
        side = poly(m, [V(0.255, MY + 0.050, 0.060), V(0.245, MY - 0.020, 0.100), V(0.20, CHIN_Y - 0.01, 0.18)], [0.028, 0.045, 0.075], 0.03)   # 머리카락 띠 끝과 맞닿게 올렸다
    else:
        side = poly(m, [V(0.29, EY + 0.04, -0.01), V(0.285, EY - 0.06, 0.03), V(0.27, MY - 0.01, 0.08), V(0.20, CHIN_Y - 0.01, 0.18)], [0.040, 0.050, 0.060, 0.080], 0.03)
    jawm = smax(orc_head(p) - 0.032, p[..., 1] - (MY - 0.04 + 1.2 * p[..., 0] ** 2), 0.02)
    jawm = smax(jawm, -(p[..., 2] + 0.02), 0.03)
    hang = ell(p, V(0, CHIN_Y - length * 0.45, CHIN_Z - 0.01), V(width, length * 0.55 + 0.04, depth))
    hang = smax(hang, -(p[..., 2] - 0.10), 0.04)                                           # 목 쪽(뒤)은 안 튀어나오게
    d = smin(smin(side, jawm, 0.04), hang, 0.06)
    d = smax(orc_head(p) - 0.018, d, 0.012)                                               # 살 밖에만
    return d + 0.004 * np.sin(p[..., 0] * 60 + 0.8 * np.sin(p[..., 1] * 18))              # 세로 결
def moustache(p, droop, r=1.0):
    m = mirror(p)
    return poly(m, [V(0.03, NY - 0.065, LIP_Z + 0.02), V(0.13, MY + 0.005, LIP_Z - 0.005), V(0.17, MY - droop, LIP_Z - 0.035)], [0.030 * r, 0.028 * r, 0.014 * r], 0.015)
def b_braidfull(p):   # 덥수룩한 수염(구레나룻 → 턱 → 목께) + 콧수염 + 밑에서 가슴 앞으로 곧게 내린 땋은 가닥 둘(고리 셋씩) — 사용자 그림 그대로(v33)
    d = smin(full_beard(p, 0.20, 0.22, 0.10, inset=True), moustache(p, 0.07), 0.02)
    m = mirror(p)
    pts = [V(0.13, CHIN_Y - 0.02, CHIN_Z + 0.01), V(0.14, CHIN_Y - 0.12, CHIN_Z + 0.03), V(0.145, CHIN_Y - 0.21, CHIN_Z + 0.05),
           V(0.15, CHIN_Y - 0.30, CHIN_Z + 0.06), V(0.15, CHIN_Y - 0.39, CHIN_Z + 0.07), V(0.15, CHIN_Y - 0.47, CHIN_Z + 0.075)]
    d = smin(d, braid3(m, pts, 0.050, 0.038, 18, side=(1.0, 0.0, 0.0)), 0.025)   # 세 가닥 땋기(11-08)
    d = smin(d, seg(m, V(0.15, CHIN_Y - 0.47, CHIN_Z + 0.075), V(0.15, CHIN_Y - 0.56, CHIN_Z + 0.08), 0.038, 0.012), 0.015)
    deco = np.minimum(np.minimum(braid_ring(m, pts, 0.050, 0.038, 0.50), braid_ring(m, pts, 0.050, 0.038, 0.68)), braid_ring(m, pts, 0.050, 0.038, 0.86))
    return d, deco
def b_tied(p):   # 묶은 세 묶음 — 덥수룩한 수염 아래가 세 묶음으로(가운데가 길다 · 고리 하나씩) · v32는 너무 굵었다
    d = smin(full_beard(p, 0.18, 0.22, 0.10), moustache(p, 0.07), 0.02)
    deco = NONE(p)
    for x, L, r0 in ((0.0, 0.40, 0.058), (-0.14, 0.33, 0.048), (0.14, 0.33, 0.048)):   # 묶음 사이를 조금 벌렸다(v40)
        top = V(x, CHIN_Y - 0.09, CHIN_Z + 0.02)
        mid = V(x * 1.10, CHIN_Y - L * 0.55, CHIN_Z + 0.04)
        end = V(x * 1.15, CHIN_Y - L, CHIN_Z + 0.06)
        b = smin(seg(p, top, mid, r0, r0 * 0.72), seg(p, mid, end, r0 * 0.72, 0.012), 0.02)
        d = smin(d, b + 0.003 * np.sin(p[..., 0] * 70), 0.025)
        c = mid + (end - mid) * 0.30                                                              # 고리는 수염 덩어리 밑 드러난 자리에(v40 — 덩어리에 가렸다)
        deco = np.minimum(deco, cuff(p, c + V(0, 0.014, -0.002), c - V(0, 0.014, -0.004), r0 * 0.70))
    return d, deco
def b_bushy(p):   # 안 묶은 아주 덥수룩한 긴 수염 — **한 덩어리**: 넓은 구레나룻에서 턱을 두툼하게 덮고 가슴께에서 뭉툭하게 모인다 ·
    # 아랫단은 물결로만 나뉜다(덩이 다섯을 붙이면 꽁지 다섯처럼 보였다 · v40) · 굵게 처진 콧수염
    d = full_beard(p, 0.30, 0.27, 0.12)
    y = p[..., 1]
    for i in range(7):   # 타원 덩이를 줄줄이 녹여 한 덩어리로 — 위는 넓고 아래로 좁아진다(상자로 깎으면 날개 · 판자처럼 보였다 · v41)
        t = i / 6
        c = V(0, CHIN_Y - 0.05 - 0.50 * t, CHIN_Z + 0.03 + 0.08 * t)
        d = smin(d, ell(p, c, V(0.24 * (1 - t) ** 0.9 + 0.05, 0.11, 0.105 - 0.04 * t)), 0.05)
    d = d + 0.020 * np.cos(p[..., 0] * 34) * ease((CHIN_Y - 0.40 - y) / 0.12)                     # 아랫단 물결
    d = d + 0.005 * np.sin(p[..., 0] * 50 + 1.1 * np.sin(y * 14))                                  # 굵은 세로 결
    return smin(d, moustache(p, 0.16, 1.35), 0.025), NONE(p)
BEARDS = [('None', b_none), ('Braid', b_braid), ('TwinBraid', b_twin), ('Shaggy', b_shaggy), ('Goatee', b_goatee), ('Mutton', b_mutton),
          ('BraidFull', b_braidfull), ('Tied', b_tied), ('Bushy', b_bushy)]

def seg2(p, a, b, w):
    q = p[..., :2]; a = np.array(a, np.float32); b = np.array(b, np.float32); ab = b - a
    k = np.clip(((q - a) * ab).sum(-1) / (ab * ab).sum(), 0, 1)
    return np.sqrt(((q - (a + ab * k[..., None])) ** 2).sum(-1)) - w
def tri2(p, a, b, c):
    q = p[..., :2]; P = [np.array(v, np.float32) for v in (a, b, c)]
    cen = (P[0] + P[1] + P[2]) / 3
    d = np.full(q.shape[:-1], -9.0, np.float32)
    for i in range(3):
        e0, e1 = P[i], P[(i + 1) % 3]; e = e1 - e0; n = np.array([e[1], -e[0]], np.float32); n /= np.linalg.norm(n)
        if ((cen - e0) * n).sum() > 0: n = -n
        d = np.maximum(d, ((q - e0) * n).sum(-1))
    return d
def tat_chin(p):
    d = NONE(p)
    for x in (-0.055, 0.0, 0.055): d = np.minimum(d, seg2(p, (x, MY - 0.05), (x * 1.1, -0.34), 0.012))
    return d
def tat_slash(p):
    return np.minimum(seg2(p, (-EX - 0.07, EY + 0.15), (-EX + 0.05, EY - 0.14), 0.010),
                      seg2(p, (-EX - 0.03, EY + 0.16), (-EX + 0.09, EY - 0.12), 0.010))
def tat_chevron(p):
    m = mirror(p); d = NONE(p)
    for dy in (0.0, 0.045): d = np.minimum(d, seg2(m, (0.0, 0.02 + dy), (0.11, 0.10 + dy), 0.010))
    return d
def tat_cheek(p):
    m = mirror(p); d = NONE(p)
    for i, y in enumerate((NY + 0.01, NY - 0.025, NY - 0.06)): d = np.minimum(d, seg2(m, (0.13, y), (0.22 - i * 0.01, y + 0.01), 0.009))
    return d
def tat_fangs(p):
    m = mirror(p)
    return np.minimum(tri2(m, (EX - 0.035, EY - 0.045), (EX + 0.035, EY - 0.045), (EX, EY - 0.15)),
                      tri2(m, (EX + 0.05, EY - 0.045), (EX + 0.09, EY - 0.04), (EX + 0.08, EY - 0.12)))
def tat_jaw(p):
    return np.maximum(np.abs(p[..., 1] - (MY - 0.085 + 0.6 * p[..., 0] ** 2)) - 0.022, np.abs(p[..., 0]) - 0.22)
def tattoo(mask):
    return lambda p: smax(np.maximum(orc_head(p) - 0.0025, mask(p)), 0.05 - p[..., 2], 0.002)
TATTOOS = [('ChinBars', tat_chin, (0.10, 0.10, 0.12)), ('EyeSlash', tat_slash, (0.62, 0.12, 0.10)),
           ('Chevron', tat_chevron, (0.88, 0.86, 0.80)), ('CheekBars', tat_cheek, (0.15, 0.30, 0.60)),
           ('Fangs', tat_fangs, (0.80, 0.58, 0.15)), ('JawBand', tat_jaw, (0.30, 0.12, 0.40))]

def body(p): return ell(p, V(0, -0.82, -0.02), V(0.46, 0.42, 0.33))

SKIN = (0.43, 0.54, 0.32); BONE = (0.90, 0.86, 0.74); DARK = (0.07, 0.05, 0.05); HAIR = (0.13, 0.12, 0.14)
SHIRT = (0.35, 0.27, 0.18); STUBBLE = (0.28, 0.34, 0.21); WHITE = (0.95, 0.95, 0.95); RED = (0.45, 0.12, 0.10)

def pieces(hair=h_topknot, bd=b_braid, tat=None):
    extra = [(tattoo(tat[0]), tat[1])] if tat else []
    st = getattr(hair, 'stubble', None)
    if st is not None: extra.append((st, STUBBLE))   # 민 층 — 살과 머리색 사이
    return extra + [(orc_head, SKIN), (mouth, RED), (teeth, BONE), (tusks, BONE), (eyes, DARK), (glint, WHITE), (body, SHIRT),
            (lambda p: hair(p)[0], HAIR), (lambda p: hair(p)[1], BONE),
            (lambda p: bd(p)[0], HAIR), (lambda p: bd(p)[1], BONE)]

if __name__ == '__main__':
    out = sys.argv[1]; which = sys.argv[2]; S = 260
    def sheet(rows, labels, name):
        im = Image.new('RGB', (S * len(rows[0]), S * len(rows)), (40, 40, 44))
        for r, row in enumerate(rows):
            for c, t in enumerate(row): im.paste(t, (c * S, r * S))
        d = ImageDraw.Draw(im)
        for c, l in enumerate(labels): d.text((c * S + 6, 4), l, fill=(230, 230, 230))
        im.save(out + name); print('saved', out + name)
    if which == 'tat':
        sheet([[render(pieces(h_bald, b_none, (f, c)), 0, S, S, -0.10, 0.80) for _, f, c in TATTOOS]], [n for n, _, _ in TATTOOS], '_tattoo.png')
    if which == 'face':
        f = pieces(h_bald, b_none)
        sheet([[render(f, a, S, S, -0.02, 0.90) for a in (0, 30, 90, 150)]], ['front', '3/4', 'side', 'back'], '_face.png')
    if which == 'hair':
        sheet([[render(pieces(h, b_none), 35, S, S, -0.05, 1.25) for _, h in HAIRS],
               [render(pieces(h, b_none), 150, S, S, -0.05, 1.25) for _, h in HAIRS]], [n for n, _ in HAIRS], '_hair.png')
    if which == 'new':   # 숱이 있는 머리 셋 — 짝 수염과 같이(앞 3/4 · 옆 · 뒤)
        combos = [('Mane + BraidFull', h_mane, b_braidfull, -0.14, 1.50), ('KnotTail + Bushy', h_knottail, b_bushy, -0.42, 2.10),
                  ('HighTail', h_hightail, b_none, -0.14, 1.50), ('Mane + Tied', h_mane, b_tied, -0.14, 1.50)]
        if len(sys.argv) > 3: combos = [combos[int(i)] for i in sys.argv[3].split(',')]   # 고를 줄(예: 0,2)
        sheet([[render(pieces(h, b), a, S, S, cy, sp) for a in (0, 30, 90, 160)] for _, h, b, cy, sp in combos],
              ['front', '3/4', 'side', 'back'], '_new.png')
    if which == 'beard':
        sheet([[render(pieces(h_bald, b), 0, S, S, -0.25, 1.25) for _, b in BEARDS],
               [render(pieces(h_bald, b), 50, S, S, -0.25, 1.25) for _, b in BEARDS]], [n for n, _ in BEARDS], '_beard.png')
