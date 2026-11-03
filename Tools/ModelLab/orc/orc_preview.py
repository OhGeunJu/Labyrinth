"""Orc parts on the chibi head (Male_BroadSquare) — preview. Meters, head center = origin, +z front."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg, torus
from prof import T, R

W, H, D, F = 0.628, 0.688, 0.597, 0.20
EYE_T, EYE_DX, EYE_S, MOUTH_T, NOSE_T = 0.3839, 0.1973, 0.1578, 0.1728, 0.2889
T_ = np.array(T, np.float32); R_ = np.array(R, np.float32)

def y_of(t): return (t - 0.5) * H
def front(t):   # 얼굴 앞면 z — Shear와 같은 식
    yn = t - 0.5
    return (np.interp(t, T_, R_) + (F * 2 * yn * yn if yn < 0 else 0)) * D

def head(p):
    ux, uy, uz = p[..., 0] / W, p[..., 1] / H, p[..., 2] / D
    uz = uz - np.where(uy < 0, F * 2 * uy * uy, 0)
    r = np.interp(np.clip(uy + 0.5, 0, 1), T_, R_)
    d = np.sqrt(ux * ux + uz * uz) - r
    d = np.maximum(d, np.abs(uy) - 0.5)
    return d * 0.6

EY = y_of(EYE_T); MY = y_of(MOUTH_T); NY = y_of(NOSE_T)
EX = EYE_DX * W; ER = EYE_S * 0.7 * W * 0.5

# ── 오크 부품 ──
def brow(p):   # 눈 위 선반 — 안쪽 끝이 내려와 노려본다
    m = mirror(p); z = front(EYE_T + 0.12)
    a = V(0.02, EY + ER * 0.9 + 0.03, z + 0.022)
    b = V(EX + ER * 1.3, EY + ER * 1.2 + 0.05, z - 0.035)
    return seg(m, a, b, 0.040, 0.030)
def jaw(p):    # 넓은 아래턱 — 치비 턱이 모이는 자리를 네모로 채운다
    zc = front(0.12) - 0.13
    return rbox(p, V(0, y_of(0.13), zc), V(0.215, 0.085, 0.13), 0.075)
def lip_bend(p): return np.stack([p[..., 0], p[..., 1] + 0.9 * p[..., 0] ** 2, p[..., 2]], -1)   # 끝이 처진다
def lower_lip(p):   # 내민 아랫입술 — 두툼한 띠
    q = lip_bend(p); z = front(MOUTH_T) + 0.045
    return capsule(q, V(-0.13, MY - 0.028, z), V(0.13, MY - 0.028, z), 0.042)
def mouth(p):  # 윗입술 아래의 어두운 틈
    q = lip_bend(p); z = front(MOUTH_T) + 0.012
    return rbox(q, V(0, MY + 0.010, z), V(0.12, 0.012, 0.02), 0.01)
def teeth(p):  # 아랫입술 위로 보이는 작은 아랫니
    q = lip_bend(p); z = front(MOUTH_T) + 0.052; d = np.full(p.shape[:-1], 9.0, np.float32)
    for x in (-0.07, -0.025, 0.025, 0.07):
        d = np.minimum(d, seg(q, V(x, MY + 0.004, z), V(x * 1.02, MY + 0.026, z - 0.004), 0.010, 0.004))
    return d
def tusks(p):  # 아랫입술 **안에서** 솟는다 — 밑동은 입술이 덮는다
    m = mirror(p); z = front(MOUTH_T) + 0.03
    z = z + 0.012
    a = V(0.11, MY - 0.03, z); b = V(0.128, MY + 0.04, z + 0.014); c = V(0.142, MY + 0.085, z + 0.006)
    return np.minimum(seg(m, a, b, 0.026, 0.017), seg(m, b, c, 0.017, 0.003))
def nose(p):   # 넓고 납작한 코 — 짧은 콧대 · 벌어진 콧방울
    m = mirror(p); z = front(NOSE_T)
    bridge = seg(p, V(0, NY + 0.07, z - 0.02), V(0, NY + 0.005, z + 0.022), 0.020, 0.030)
    base = ell(p, V(0, NY - 0.005, z + 0.016), V(0.062, 0.032, 0.036))
    wing = sph(m, V(0.050, NY - 0.012, z + 0.004), 0.030)
    d = smin(smin(bridge, base, 0.02), wing, 0.015)
    return smax(d, -sph(m, V(0.026, NY - 0.034, z + 0.030), 0.012), 0.005)
def ear(p):
    m = mirror(p); r0 = np.interp(EYE_T, T_, R_) * W
    a = V(r0 - 0.02, EY + 0.01, -0.03); b = V(r0 + 0.13, EY + 0.17, -0.10)
    return seg(m, a, b, 0.05, 0.006) / 1.0

def orc_head(p):
    d = head(p)
    d = smin(d, jaw(p), 0.07)
    d = smin(d, brow(p), 0.035)
    d = smin(d, nose(p), 0.02)
    d = smin(d, ear(p), 0.02)
    d = smin(d, lower_lip(p), 0.02)
    return d

def eyes(p):   # 먹 눈 — 얼굴 곡면에 얹은 얇은 판(눈썹뼈가 위를 덮는다)
    m = mirror(p); z = front(EYE_T)
    e = ell(m, V(EX, EY, z - 0.012), V(ER, ER * 1.1, 0.02))
    lid = (m[..., 1] - (EY + ER * 0.25 + 0.55 * (m[..., 0] - EX)))   # 안쪽이 내려온 눈꺼풀 — 노려본다
    return np.maximum(e, lid)
def glint(p):
    m = mirror(p); z = front(EYE_T)
    return ell(m, V(EX - ER * 0.3, EY + ER * 0.3, z - 0.002), V(ER * 0.28, ER * 0.28, 0.02))

# ── 머리카락 · 수염 ──
def topknot(p):
    band = torus(p - V(0, 0, 0), V(0, 0.36, -0.12), 0.045, 0.018)
    root = seg(p, V(0, 0.30, -0.10), V(0, 0.40, -0.13), 0.06, 0.05)
    tail = seg(p, V(0, 0.40, -0.13), V(0, 0.46, -0.22), 0.05, 0.05)
    fall = seg(p, V(0, 0.46, -0.22), V(0, 0.12, -0.42), 0.05, 0.02)
    return smin(smin(root, tail, 0.03), fall, 0.03), band
def beard(p):   # 턱 밑에서 땋아 내린 수염 — 고리로 묶었다
    top = y_of(0.02)
    root = ell(p, V(0, top + 0.01, front(0.06) - 0.03), V(0.13, 0.07, 0.08))
    braid = seg(p, V(0, top - 0.04, front(0.03) - 0.03), V(0, top - 0.30, front(0.03) - 0.01), 0.065, 0.025)
    knots = np.full(p.shape[:-1], 9.0, np.float32)
    for i in range(4):
        y = top - 0.07 - i * 0.055
        knots = np.minimum(knots, ell(p, V(0, y, front(0.03) - 0.03 + i * 0.004), V(0.07 - i * 0.008, 0.032, 0.05 - i * 0.004)))
    hair = smin(smin(root, braid, 0.03), knots, 0.015)
    ring = smax(np.abs(length((p - V(0, 0, front(0.03) - 0.025))[..., [0, 2]]) - 0.052) - 0.012,
                np.abs(p[..., 1] - (top - 0.25)) - 0.018, 0.005)
    return hair, ring
def body(p):
    return ell(p, V(0, -0.82, -0.02), V(0.46, 0.42, 0.33))

SKIN = (0.43, 0.54, 0.32); BONE = (0.90, 0.86, 0.74); DARK = (0.07, 0.05, 0.05); HAIR = (0.13, 0.12, 0.14)
LEATHER = (0.40, 0.27, 0.16); SHIRT = (0.35, 0.27, 0.18); WHITE = (0.95, 0.95, 0.95); RED = (0.45, 0.12, 0.10)

def pieces(hair=True, bd=True):
    P = [(orc_head, SKIN), (mouth, RED), (teeth, BONE), (tusks, BONE), (eyes, DARK), (glint, WHITE), (body, SHIRT)]
    if hair:
        P += [(lambda p: topknot(p)[0], HAIR), (lambda p: topknot(p)[1], LEATHER)]
    if bd:
        P += [(lambda p: beard(p)[0], HAIR), (lambda p: beard(p)[1], BONE)]
    return P

def render(P, yaw_deg, Wpx=320, Hpx=320, center_y=0.0, span=1.1, pitch=0.0):
    def scene(p):
        ds = np.stack([f(p) for f, _ in P], 0)
        return ds.min(0), ds.argmin(0)
    yaw = np.radians(yaw_deg); pt = np.radians(pitch)
    fwd = np.array([-np.sin(yaw) * np.cos(pt), -np.sin(pt), -np.cos(yaw) * np.cos(pt)], np.float32)
    right = np.array([np.cos(yaw), 0, -np.sin(yaw)], np.float32)
    up = np.cross(right, fwd); up /= np.linalg.norm(up)
    xs = (np.arange(Wpx) / Wpx - 0.5) * span * Wpx / Hpx; ys = (0.5 - np.arange(Hpx) / Hpx) * span
    X, Y = np.meshgrid(xs, ys)
    org = (np.array([0, center_y, 0], np.float32) - fwd * 4 + X[..., None] * right + Y[..., None] * up).astype(np.float32)
    t = np.zeros((Hpx, Wpx), np.float32); hit = np.zeros((Hpx, Wpx), bool)
    for _ in range(220):
        d, _ = scene(org + fwd * t[..., None])
        hit |= d < 0.0015
        t = np.where(hit, t, np.minimum(t + np.maximum(d * 0.8, 0.001), 30))
    p = org + fwd * t[..., None]
    _, idx = scene(p)
    e = 0.002
    n = np.stack([scene(p + np.array(v, np.float32) * e)[0] - scene(p - np.array(v, np.float32) * e)[0]
                  for v in ([1, 0, 0], [0, 1, 0], [0, 0, 1])], -1)
    n /= np.maximum(length(n), 1e-6)[..., None]
    L = np.array([0.35, 0.75, 0.55], np.float32); L /= np.linalg.norm(L)
    lam = np.clip((n * L).sum(-1), 0, 1)
    shade = np.where(lam > 0.55, 1.0, np.where(lam > 0.2, 0.80, 0.60))
    cols = np.array([c for _, c in P], np.float32)
    img = np.where(hit[..., None], cols[idx] * shade[..., None], np.array([0.16, 0.16, 0.18], np.float32))
    t = np.where(hit, t, 30.0)
    edge = np.zeros(hit.shape, bool)
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2)]:
        tj = np.roll(np.roll(t, dx, 1), dy, 0); ij = np.roll(np.roll(idx, dx, 1), dy, 0)
        edge |= (np.abs(tj - t) > 0.15)       # 바깥 선(외곽선 셰이더)
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        tj = np.roll(np.roll(t, dx, 1), dy, 0); ij = np.roll(np.roll(idx, dx, 1), dy, 0)
        edge |= hit & (ij != idx) & (np.abs(tj - t) > 0.012)
    img = np.where(edge[..., None], 0.04, img)
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))

if __name__ == '__main__':
    out = sys.argv[1]
    P = pieces()
    row1 = [render(P, a, 320, 380, -0.12, 1.45) for a in (0, 35, 90, 160)]
    row2 = [render(pieces(False, False), a, 320, 320, -0.12, 0.70) for a in (0, 30, 90)] + [render(P, 20, 320, 320, -0.10, 0.75, pitch=-25)]
    sheet = Image.new('RGB', (1280, 700), (40, 40, 44))
    for i, im in enumerate(row1): sheet.paste(im, (i * 320, 0))
    for i, im in enumerate(row2): sheet.paste(im, (i * 320, 380))
    sheet.save(out); print('saved', out)
