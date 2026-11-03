"""Ogre v2 preview — same formulas as Assets/Editor/ChibiPawnCreator.Ogre.cs."""
import sys
import numpy as np
from PIL import Image
from brute_preview import V, length, mirror, side, smin, smax, ell, rbox, sph, capsule, seg, joints

J = joints(1.0, 0.80, 0.52, 0.21)

def torso(p, j=J):
    t, h = j['Top'], j['Hip']; m = mirror(p)
    chest = ell(p, V(0, t - 0.20, 0.0), V(0.40, 0.26, 0.30))
    delt = ell(m, V(0.34, t - 0.10, -0.03), V(0.16, 0.14, 0.17))
    traps = capsule(m, V(0.05, t + 0.04, -0.05), V(0.28, t - 0.04, -0.04), 0.10)
    gut = ell(p, V(0, h + 0.28, 0.10), V(0.46, 0.36, 0.40))
    hips = ell(p, V(0, h + 0.02, -0.02), V(0.38, 0.18, 0.28))
    upper = smin(smin(chest, delt, 0.08), traps, 0.08)
    return smin(upper, smin(gut, hips, 0.12), 0.14)

def ear(m, j=J):
    t = j['Top']; c = V(0.31, t + 0.42, -0.02)
    q = m - c; a = np.radians(52)  # 머리 옆에 붙어 앞-바깥을 본다
    q = np.stack([q[..., 0] * np.cos(a) - q[..., 2] * np.sin(a), q[..., 1], q[..., 0] * np.sin(a) + q[..., 2] * np.cos(a)], -1)
    shell = ell(q, V(0.025, 0.0, 0.0), V(0.075, 0.105, 0.03))
    cup = ell(q, V(0.025, -0.005, 0.03), V(0.040, 0.060, 0.022))
    return smax(shell, -cup, 0.012)
def nose(p, j=J):
    t = j['Top']; m = mirror(p)
    bridge = seg(p, V(0, t + 0.43, 0.29), V(0, t + 0.375, 0.37), 0.035, 0.045)
    base = ell(p, V(0, t + 0.355, 0.35), V(0.085, 0.048, 0.06))
    wing = sph(m, V(0.055, t + 0.35, 0.33), 0.042)
    d = smin(smin(bridge, base, 0.03), wing, 0.02)
    return smax(d, -sph(m, V(0.04, t + 0.325, 0.37), 0.018), 0.008)   # 콧구멍
def bend(p, t): return np.stack([p[..., 0], p[..., 1] - (0.255 - 0.6 * p[..., 0] ** 2), p[..., 2]], -1)
def slit(p, j=J): return rbox(bend(p, j['Top']), V(0, j['Top'], 0.34), V(0.15, 0.032, 0.08), 0.028)
def head(p, j=J):
    t = j['Top']; m = mirror(p)
    cran = ell(p, V(0, t + 0.44, 0.02), V(0.32, 0.33, 0.31))
    jaw = rbox(p, V(0, t + 0.16, 0.16), V(0.27, 0.10, 0.20), 0.10)   # 아래턱이 윗입술보다 앞으로
    muzzle = ell(p, V(0, t + 0.31, 0.21), V(0.22, 0.08, 0.12))
    d = smin(smin(cran, jaw, 0.10), muzzle, 0.06)
    d = smin(d, nose(p, j), 0.03)
    d = smin(d, ear(m, j), 0.02)
    lip = seg(m, V(0.0, t + 0.235 , 0.345), V(0.15, t + 0.215, 0.32), 0.03, 0.025)   # 내민 아랫입술
    d = smin(d, lip, 0.03)
    return smax(d, -slit(p, j), 0.02)
def mouth(p, j=J): return ell(bend(p, j['Top']), V(0, j['Top'], 0.26), V(0.15, 0.045, 0.07))
def teeth(p, j=J):
    t = j['Top']; q = bend(p, t); d = np.full(p.shape[:-1], 9.0, np.float32)
    for x in (-0.105, -0.035, 0.035, 0.105):   # 윗니 넷 — 윗입술에 매달린다
        d = np.minimum(d, rbox(q, V(x, t + 0.029, 0.30), V(0.028, 0.012, 0.02), 0.008))
    for x in (-0.07, 0.0, 0.07):              # 아랫니 셋 — 송곳니 사이
        d = np.minimum(d, rbox(q, V(x, t - 0.029, 0.31), V(0.025, 0.011, 0.02), 0.008))
    return d
def tusks(p, j=J):   # 아랫입술 안에서 솟아 윗입술 앞으로
    m = mirror(p); t = j['Top']
    return seg(m, V(0.15, t + 0.22, 0.31), V(0.17, t + 0.37, 0.355), 0.042, 0.008)
ONE = np.array([0, 0.505, 0.27], np.float32); TWO = np.array([0.145, 0.49, 0.235], np.float32)
def one_white(p, j=J): return ell(p, ONE + V(0, j['Top'], 0), V(0.105, 0.10, 0.075))
def one_iris(p, j=J): return ell(p, ONE + V(0, j['Top'], 0.067), V(0.062, 0.062, 0.015))
def one_pupil(p, j=J): return ell(p, ONE + V(0, j['Top'], 0.081), V(0.026, 0.032, 0.008))
def two_white(p, j=J): return ell(mirror(p), TWO + V(0, j['Top'], 0), V(0.088, 0.088, 0.07))
def two_iris(p, j=J): return ell(mirror(p), TWO + V(0, j['Top'], 0.062), V(0.054, 0.054, 0.014))
def two_pupil(p, j=J): return ell(mirror(p), TWO + V(0, j['Top'], 0.075), V(0.023, 0.028, 0.007))
def horn(p, j=J):
    t = j['Top']; a, b, c = V(0, t + 0.72, 0.05), V(0, t + 0.90, 0.02), V(0, t + 1.02, -0.04)
    return np.minimum(seg(p, a, b, 0.095, 0.055), seg(p, b, c, 0.055, 0.01))
def upper(m, j=J):
    ax = j['AX']
    arm = seg(m, V(ax, j['ArmTop'], 0), V(ax, j['Elbow'], 0.01), 0.17, 0.15)
    bicep = ell(m, V(ax, (j['ArmTop'] + j['Elbow']) * 0.5, 0.06), V(0.15, 0.15, 0.14))
    return smin(smin(arm, bicep, 0.05), sph(m, V(ax - 0.03, j['ArmTop'] + 0.02, -0.01), 0.19), 0.06)
def fore(m, j=J):
    ax = j['AX']
    arm = seg(m, V(ax, j['Elbow'], 0.01), V(ax, j['Wrist'] + 0.04, 0.03), 0.15, 0.15)
    meat = ell(m, V(ax, j['Elbow'] - (j['Elbow'] - j['Wrist']) * 0.3, 0.03), V(0.17, 0.12, 0.16))
    return smin(arm, meat, 0.05)
def hand(m, j=J):
    ax, w = j['AX'], j['Wrist']
    d = rbox(m, V(ax, w - 0.13, 0.04), V(0.13, 0.14, 0.14), 0.11)
    for z in (0.09, 0.03, -0.03, -0.09):
        d = smin(d, sph(m, V(ax + 0.06, w - 0.24, z + 0.05), 0.05), 0.03)
    return smin(d, capsule(m, V(ax - 0.08, w - 0.09, 0.16), V(ax - 0.06, w - 0.19, 0.20), 0.055), 0.04)
def band(m, j=J): return smax(np.abs(fore(m) - 0.014) - 0.022, np.abs(m[..., 1] - (j['Wrist'] + 0.15)) - 0.09, 0.01)
def spike(m, j=J): return seg(m, V(j['AX'] + 0.12, j['Wrist'] + 0.15, 0.03), V(j['AX'] + 0.30, j['Wrist'] + 0.16, 0.03), 0.06, 0.006)
def thigh(m, j=J):
    lx = j['LX']
    leg = seg(m, V(lx, j['Hip'], 0), V(lx, j['Knee'] + 0.02, 0.02), 0.21, 0.19)
    quad = seg(m, V(lx, j['Hip'] - 0.02, 0.03), V(lx - 0.01, j['Knee'] + 0.09, 0.07), 0.18, 0.19)   # 위는 가늘고 무릎 위가 굵다
    inner = ell(m, V(lx - 0.07, j['Knee'] + 0.10, 0.08), V(0.10, 0.10, 0.12))                        # 무릎 안쪽 위 물방울
    return smin(smin(leg, quad, 0.08), inner, 0.05)
def shin(m, j=J): return seg(m, V(j['LX'], j['Knee'], 0.02), V(j['LX'], j['Ankle'] + 0.08, 0.02), 0.19, 0.18)
def foot(m, j=J):
    lx = j['LX']
    d = rbox(m, V(lx, 0.095, 0.09), V(0.18, 0.095, 0.25), 0.09)
    for x, r in zip((-0.12, -0.05, 0.02, 0.09), (0.060, 0.052, 0.050, 0.046)):
        d = smin(d, sph(m, V(lx + x, 0.055, 0.32), r), 0.03)
    return d
def skin(p, gap, thick): return np.abs(torso(p) - gap) - thick
def sash(p, j=J):
    a = np.array([0.42, j['Hip'] + 0.18], np.float32); b = np.array([-0.30, j['Top'] + 0.02], np.float32)
    q = p[..., :2]; ab = b - a
    k = np.clip(((q - a) * ab).sum(-1) / (ab * ab).sum(), 0, 1)
    line = np.sqrt(((q - (a + ab * k[..., None])) ** 2).sum(-1))
    return smax(skin(p, 0.016, 0.02), line - 0.15, 0.02)
def loin(p, j=J):
    h = j['Hip']
    belt = smax(skin(p, 0.016, 0.026), np.abs(p[..., 1] - (h + 0.07)) - 0.075, 0.01)
    hem = h - 0.28 + 0.035 * np.sin(p[..., 0] * 38)
    front = smax(rbox(p, V(0, h - 0.10, 0.35), V(0.24, 0.17, 0.02), 0.015), hem - p[..., 1], 0.01)
    back = smax(rbox(p, V(0, h - 0.10, -0.30), V(0.26, 0.17, 0.02), 0.015), hem - p[..., 1], 0.01)
    return np.minimum(belt, np.minimum(front, back))
def buckle(p, j=J):   # 허리띠 위에 겹쳐 붙은 쇠 판 — 배 곡면을 그대로 탄다
    disc = length(p[..., :2] - np.array([0, j['Hip'] + 0.07], np.float32)) - 0.10
    return smax(smax(skin(p, 0.046, 0.008), disc, 0.008), -p[..., 2], 0.01)

def both(f): return lambda p: np.minimum(f(side(p, 1)), f(side(p, -1)))

SKIN = (0.48, 0.62, 0.85); CLOTH = (0.45, 0.40, 0.25); HORN = (0.62, 0.62, 0.60); WHITE = (0.95, 0.94, 0.90)
IRIS = (0.88, 0.62, 0.15); DARK = (0.06, 0.04, 0.04); TOOTH = (0.92, 0.89, 0.80); METAL = (0.62, 0.64, 0.68)

def pieces(one_eye):
    eyes = [(one_white, WHITE), (one_iris, IRIS), (one_pupil, DARK)] if one_eye else [(two_white, WHITE), (two_iris, IRIS), (two_pupil, DARK)]
    return [(torso, SKIN), (head, SKIN), (mouth, DARK), (teeth, TOOTH), (tusks, TOOTH), (horn, HORN)] + eyes + [
            (sash, CLOTH), (loin, CLOTH), (buckle, METAL),
            (both(upper), SKIN), (both(fore), SKIN), (both(hand), SKIN), (both(band), CLOTH), (both(spike), METAL),
            (both(thigh), SKIN), (both(shin), SKIN), (both(foot), SKIN)]

def render(P, yaw_deg, W=300, H=420, center_y=1.25, span=2.85):
    def scene(p):
        ds = np.stack([f(p) for f, _ in P], 0)
        return ds.min(0), ds.argmin(0)
    yaw = np.radians(yaw_deg)
    fwd = np.array([-np.sin(yaw), 0, -np.cos(yaw)], np.float32)
    right = np.array([np.cos(yaw), 0, -np.sin(yaw)], np.float32)
    up = np.array([0, 1, 0], np.float32)
    xs = (np.arange(W) / W - 0.5) * span * W / H; ys = (0.5 - np.arange(H) / H) * span
    X, Y = np.meshgrid(xs, ys)
    org = (np.array([0, center_y, 0], np.float32) - fwd * 6 + X[..., None] * right + Y[..., None] * up).astype(np.float32)
    t = np.zeros((H, W), np.float32); hit = np.zeros((H, W), bool)
    for _ in range(150):
        d, _ = scene(org + fwd * t[..., None])
        hit |= d < 0.003
        t = np.where(hit, t, np.minimum(t + np.maximum(d * 0.9, 0.002), 50))
    p = org + fwd * t[..., None]
    _, idx = scene(p)
    e = 0.004
    n = np.stack([scene(p + np.array(v, np.float32) * e)[0] - scene(p - np.array(v, np.float32) * e)[0]
                  for v in ([1, 0, 0], [0, 1, 0], [0, 0, 1])], -1)
    n /= np.maximum(length(n), 1e-6)[..., None]
    L = np.array([0.35, 0.75, 0.55], np.float32); L /= np.linalg.norm(L)
    lam = np.clip((n * L).sum(-1), 0, 1)
    shade = np.where(lam > 0.55, 1.0, np.where(lam > 0.2, 0.82, 0.62))
    cols = np.array([c for _, c in P], np.float32)
    img = np.where(hit[..., None], cols[idx] * shade[..., None], np.array([0.16, 0.16, 0.18], np.float32))
    t = np.where(hit, t, 50.0)
    edge = np.zeros(hit.shape, bool)
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        tj = np.roll(np.roll(t, dx, 1), dy, 0); ij = np.roll(np.roll(idx, dx, 1), dy, 0)
        edge |= (np.abs(tj - t) > 0.08)
        edge |= hit & (ij != idx) & (np.abs(tj - t) > 0.02)
    img = np.where(edge[..., None], 0.05, img)
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))

if __name__ == '__main__':
    out = sys.argv[1]
    tiles = [render(pieces(True), 0), render(pieces(True), 30), render(pieces(True), 90),
             render(pieces(True), 180), render(pieces(False), 0)]
    legs = [render(pieces(True), 0, 240, 240, J['Hip'], 1.0), render(pieces(True), 60, 240, 240, J['Hip'], 1.0)]
    face = [render(pieces(True), 0, 240, 240, J['Top'] + 0.40, 0.95), render(pieces(False), 0, 240, 240, J['Top'] + 0.40, 0.95),
            render(pieces(True), 35, 240, 240, J['Top'] + 0.40, 0.95)]
    W, H = tiles[0].size
    sheet = Image.new('RGB', (W * 5, H + 240), (40, 40, 44))
    for i, tl in enumerate(tiles): sheet.paste(tl, (i * W, 0))
    for i, tl in enumerate(face): sheet.paste(tl, (i * 240 + 30, H))
    for i, tl in enumerate(legs): sheet.paste(tl, ((3 + i) * 240 + 60, H))
    sheet.save(out); print('saved', out)
