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
def h_warknot(p):
    root = seg(p, V(0, 0.30, -0.10), V(0, 0.40, -0.13), 0.06, 0.05)
    pts = [V(0, 0.40, -0.14), V(0, 0.36, -0.30), V(0, 0.24, -0.40), V(0, 0.10, -0.44), V(0, -0.04, -0.46), V(0, -0.18, -0.47), V(0, -0.32, -0.47)]
    return smin(root, chain(p, pts, 0.045, 0.032), 0.02), np.minimum(cuff(p, V(0, 0.345, -0.115), V(0, 0.385, -0.125), 0.058),
                                                                    cuff(p, V(0, -0.20, -0.47), V(0, -0.23, -0.47), 0.038))
def h_braids(p):   # 관자놀이에서 늘인 땋은 머리 두 가닥 + 뒤로 넘긴 머리
    m = mirror(p); c = seg(m, V(0.20, 0.17, -0.18), V(0.25, 0.08, -0.17), 0.045, 0.042)
    pts = [V(0.25, 0.08, -0.17), V(0.29, -0.02, -0.14), V(0.31, -0.12, -0.08), V(0.32, -0.22, -0.02),
           V(0.32, -0.32, 0.04), V(0.31, -0.42, 0.10), V(0.30, -0.52, 0.15)]
    br = chain(m, pts, 0.035, 0.026)
    deco = np.minimum(cuff(m, V(0.31, -0.32, 0.095), V(0.31, -0.345, 0.105), 0.036), cuff(m, V(0.30, -0.52, 0.175), V(0.30, -0.545, 0.185), 0.031))
    return smin(c, br, 0.02), deco
HAIRS = [('Bald', h_bald), ('Topknot', h_topknot), ('WarKnot', h_warknot), ('Ponytail', h_ponytail), ('Mohawk', h_mohawk), ('Braids', h_braids)]

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
    br = chain(p, pts, 0.055, 0.035)
    tuft = seg(p, V(0, CHIN_Y - 0.31, CHIN_Z + 0.09), V(0, CHIN_Y - 0.41, CHIN_Z + 0.12), 0.04, 0.012)
    return smin(smin(root, br, 0.025), tuft, 0.02), cuff(p, V(0, CHIN_Y - 0.26, CHIN_Z + 0.07), V(0, CHIN_Y - 0.285, CHIN_Z + 0.078), 0.045)
def b_twin(p):
    m = mirror(p)
    root = patch(p, 0.12)
    pts = [V(0.06, CHIN_Y, CHIN_Z), V(0.065, CHIN_Y - 0.08, CHIN_Z + 0.01), V(0.07, CHIN_Y - 0.16, CHIN_Z + 0.03),
           V(0.07, CHIN_Y - 0.24, CHIN_Z + 0.06)]
    br = chain(m, pts, 0.042, 0.032)
    return smin(root, br, 0.025), cuff(m, V(0.07, CHIN_Y - 0.19, CHIN_Z + 0.04), V(0.07, CHIN_Y - 0.21, CHIN_Z + 0.048), 0.038)
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
BEARDS = [('None', b_none), ('Braid', b_braid), ('TwinBraid', b_twin), ('Shaggy', b_shaggy), ('Goatee', b_goatee), ('Mutton', b_mutton)]

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
    if which == 'beard':
        sheet([[render(pieces(h_bald, b), 0, S, S, -0.25, 1.25) for _, b in BEARDS],
               [render(pieces(h_bald, b), 50, S, S, -0.25, 1.25) for _, b in BEARDS]], [n for n, _ in BEARDS], '_beard.png')
