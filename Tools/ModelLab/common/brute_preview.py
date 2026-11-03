"""Preview of ChibiPawnCreator.Brutes SDFs (same formulas, numpy raymarch). Units = head height (Hh), origin at feet, +z front."""
import sys
import numpy as np
from PIL import Image, ImageDraw

def V(x, y, z): return np.array([x, y, z], dtype=np.float32)

def length(q): return np.sqrt((q * q).sum(-1))
def mirror(p): q = p.copy(); q[..., 0] = np.abs(q[..., 0]); return q
def side(p, s): q = p.copy(); q[..., 0] = q[..., 0] * s; return q

def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)
def smax(a, b, k): return -smin(-a, -b, k)

def ell(p, c, r):
    q = (p - c) / r
    k0 = length(q); k1 = length(q / r)
    return k0 * (k0 - 1) / np.maximum(k1, 1e-6)
def rbox(p, c, h, r):
    q = np.abs(p - c) - (h - r)
    return length(np.maximum(q, 0)) + np.minimum(q.max(-1), 0) - r
def sph(p, c, r): return length(p - c) - r
def capsule(p, a, b, r):
    pa = p - a; ba = b - a
    h = np.clip((pa * ba).sum(-1) / (ba * ba).sum(), 0, 1)
    return length(pa - ba * h[..., None]) - r
def seg(p, a, b, ra, rb):
    pa = p - a; ba = b - a
    h = np.clip((pa * ba).sum(-1) / (ba * ba).sum(), 0, 1)
    return length(pa - ba * h[..., None]) - (ra + (rb - ra) * h)
def torus(p, c, big, small):
    q = p - c
    a = np.sqrt(q[..., 0] ** 2 + q[..., 2] ** 2) - big
    return np.sqrt(a * a + q[..., 1] ** 2) - small

# ── joints (BodyDims, hh = 1; ShoulderDrop 0.03 m ≈ 0.06 Hh) ──
def joints(armK, legK, ax, lx):
    lift = 0.22 + 0.40 * legK; top = lift + 0.96
    hip = lift + 0.20; foot = 0.11; knee = (hip + foot) / 2
    shoulder = top - 0.10 - 0.06; hand = top - 0.10 - 0.56 * armK
    return dict(AX=ax, LX=lx, ArmTop=shoulder + 0.035, Elbow=(shoulder + hand) / 2, Wrist=hand,
                Hip=hip, Knee=knee, Ankle=foot, Top=top)

OGRE = joints(1.15, 0.70, 0.62, 0.24)
TROLL = joints(1.55, 0.65, 0.60, 0.22)

# ── shapes (transcribed from ChibiPawnCreator.Brutes.cs) ──
def ogre_torso(p, j):
    t = j['Top']
    belly = ell(p, V(0, j['Hip'] + 0.30, 0.08), V(0.50, 0.42, 0.44))
    chest = ell(p, V(0, t - 0.20, 0.0), V(0.54, 0.28, 0.36))
    traps = capsule(mirror(p), V(0.02, t - 0.04, -0.04), V(0.50, t - 0.10, -0.03), 0.17)
    hips = ell(p, V(0, j['Hip'] + 0.02, -0.02), V(0.40, 0.22, 0.32))
    d = smin(smin(belly, chest, 0.14), smin(traps, hips, 0.12), 0.12)
    return smax(d, -sph(p, V(0, j['Hip'] + 0.26, 0.53), 0.035), 0.02)

def troll_torso(p, j):
    t = j['Top']
    chest = ell(p, V(0, t - 0.24, 0.04), V(0.46, 0.30, 0.34))
    hump = ell(p, V(0, t - 0.04, -0.16), V(0.44, 0.26, 0.30))
    belly = ell(p, V(0, j['Hip'] + 0.32, 0.06), V(0.38, 0.32, 0.33))
    hips = ell(p, V(0, j['Hip'] + 0.02, -0.02), V(0.34, 0.20, 0.28))
    traps = capsule(mirror(p), V(0.02, t - 0.10, -0.06), V(0.48, t - 0.14, -0.04), 0.15)
    knobs = np.full(p.shape[:-1], 9.0, np.float32)
    for i in range(4):
        knobs = np.minimum(knobs, sph(p, V(0, t - 0.02 - i * 0.13, -0.42 + i * 0.04), 0.07 - i * 0.008))
    d = smin(smin(chest, hump, 0.16), smin(belly, hips, 0.12), 0.12)
    return smin(smin(d, traps, 0.10), knobs, 0.05)

def troll_belly(p, j):
    d = ell(p, V(0, j['Hip'] + 0.34, 0.11), V(0.35, 0.42, 0.31))
    return smax(d, 0.14 - p[..., 2], 0.04)

def ogre_head(p, j):
    t = j['Top']; m = mirror(p)
    cran = ell(p, V(0, t + 0.30, 0.10), V(0.28, 0.27, 0.28))
    jaw = rbox(p, V(0, t + 0.14, 0.22), V(0.24, 0.10, 0.16), 0.08)
    brow = capsule(m, V(0.03, t + 0.40, 0.33), V(0.19, t + 0.38, 0.29), 0.055)
    nose = ell(p, V(0, t + 0.30, 0.40), V(0.075, 0.065, 0.06))
    ear = ell(m, V(0.28, t + 0.30, 0.06), V(0.04, 0.075, 0.055))
    d = smin(smin(cran, jaw, 0.10), smin(brow, nose, 0.04), 0.05)
    d = smin(d, ear, 0.03)
    d = smax(d, -sph(m, V(0.11, t + 0.33, 0.37), 0.055), 0.015)
    return smax(d, -capsule(m, V(0, t + 0.19, 0.39), V(0.15, t + 0.20, 0.34), 0.018), 0.01)
def ogre_eyes(p, j): return sph(mirror(p), V(0.11, j['Top'] + 0.33, 0.355), 0.035)
def ogre_tusks(p, j):
    t = j['Top']; return seg(mirror(p), V(0.14, t + 0.17, 0.36), V(0.17, t + 0.33, 0.40), 0.034, 0.008)
def ogre_horn(p, j):
    t = j['Top']; a = V(0, t + 0.52, 0.08); mid = V(0, t + 0.68, 0.06); b = V(0, t + 0.84, -0.03)
    return np.minimum(seg(p, a, mid, 0.07, 0.04), seg(p, mid, b, 0.04, 0.008))

def troll_head(p, j):
    t = j['Top']; m = mirror(p)
    cran = ell(p, V(0, t + 0.12, 0.30), V(0.21, 0.20, 0.23))
    muzzle = rbox(p, V(0, t - 0.02, 0.44), V(0.20, 0.08, 0.12), 0.07)
    brow = capsule(m, V(0.02, t + 0.19, 0.50), V(0.16, t + 0.17, 0.45), 0.05)
    nose = ell(p, V(0, t + 0.10, 0.56), V(0.07, 0.05, 0.05))
    ear = ell(m, V(0.21, t + 0.12, 0.24), V(0.035, 0.06, 0.05))
    d = smin(smin(cran, muzzle, 0.09), smin(brow, nose, 0.04), 0.05)
    d = smin(d, ear, 0.03)
    d = smax(d, -sph(m, V(0.025, t + 0.10, 0.61), 0.018), 0.008)
    d = smax(d, -sph(m, V(0.09, t + 0.13, 0.52), 0.045), 0.012)
    return smax(d, -rbox(p, V(0, t - 0.035, 0.55), V(0.15, 0.022, 0.05), 0.015), 0.01)
def troll_eyes(p, j): return sph(mirror(p), V(0.09, j['Top'] + 0.13, 0.51), 0.028)
def troll_teeth(p, j):
    t = j['Top']; d = np.full(p.shape[:-1], 9.0, np.float32)
    for x, h in zip([-0.11, -0.04, 0.035, 0.10], [0.05, 0.03, 0.04, 0.055]):
        d = np.minimum(d, seg(p, V(x, t - 0.06, 0.57), V(x * 1.05, t - 0.06 + h, 0.575), 0.018, 0.006))
    return d

def upper(m, j, o):
    r0, r1 = (0.17, 0.14) if o else (0.13, 0.11)
    arm = seg(m, V(j['AX'], j['ArmTop'], 0), V(j['AX'], j['Elbow'], 0.02), r0, r1)
    return smin(arm, sph(m, V(j['AX'] - 0.02, j['ArmTop'] + 0.02, -0.01), 0.20 if o else 0.17), 0.08)
def fore(m, j, o):
    r0, r1 = (0.14, 0.13) if o else (0.12, 0.17)
    return seg(m, V(j['AX'], j['Elbow'], 0.02), V(j['AX'], j['Wrist'] + 0.03, 0.04), r0, r1)
def hand(m, j, o):
    s = 1.2 if o else 1.3
    q = (m - V(j['AX'], j['Wrist'], 0.04)) / s
    d = rbox(q, V(0, -0.10, 0.02), V(0.07, 0.09, 0.10), 0.06)
    for z in [0.07, 0.0, -0.07]:
        d = smin(d, capsule(q, V(0.01, -0.17, z + 0.02), V(0.03, -0.27, z + 0.04), 0.042), 0.03)
    d = smin(d, capsule(q, V(-0.04, -0.08, 0.11), V(-0.03, -0.17, 0.15), 0.04), 0.03)
    return d * s
def thigh(m, j, o): return seg(m, V(j['LX'], j['Hip'], 0), V(j['LX'], j['Knee'], 0.02), 0.20 if o else 0.17, 0.16 if o else 0.15)
def shin(m, j, o): return seg(m, V(j['LX'], j['Knee'], 0.02), V(j['LX'], j['Ankle'] + 0.05, 0.02), 0.15, 0.13 if o else 0.14)
def foot(m, j, o):
    w = 1.0 if o else 1.2
    d = rbox(m, V(j['LX'], 0.07, 0.06), V(0.12 * w, 0.065, 0.16), 0.06)
    for x in ([-0.07, 0, 0.07] if o else [-0.10, -0.035, 0.035, 0.10]):
        d = smin(d, capsule(m, V(j['LX'] + x, 0.04, 0.20), V(j['LX'] + x * 1.1, 0.035, 0.28), 0.042), 0.03)
    return d

def ogre_loin(p, j):
    belt = smax(np.abs(ell(p, V(0, j['Hip'] + 0.08, -0.01), V(0.44, 0.30, 0.36))) - 0.025, np.abs(p[..., 1] - (j['Hip'] + 0.08)) - 0.06, 0.01)
    front = rbox(p, V(0, j['Hip'] - 0.12, 0.33), V(0.15, 0.17, 0.018), 0.015)
    back = rbox(p, V(0, j['Hip'] - 0.10, -0.33), V(0.17, 0.15, 0.018), 0.015)
    return np.minimum(belt, np.minimum(front, back))
def ogre_band(m, j, o):
    return smax(np.abs(seg(m, V(j['AX'], j['Wrist'] + 0.16, 0.035), V(j['AX'], j['Wrist'] + 0.03, 0.04), 0.145, 0.14)) - 0.012,
                np.abs(m[..., 1] - (j['Wrist'] + 0.095)) - 0.06, 0.01)
def troll_loin(p, j):
    belt = smax(np.abs(ell(p, V(0, j['Hip'] + 0.06, -0.01), V(0.37, 0.28, 0.31))) - 0.022, np.abs(p[..., 1] - (j['Hip'] + 0.06)) - 0.05, 0.01)
    hem = j['Hip'] - 0.30 + 0.04 * np.sin(p[..., 0] * 40)
    front = smax(rbox(p, V(0, j['Hip'] - 0.10, 0.28), V(0.20, 0.22, 0.018), 0.015), hem - p[..., 1], 0.01)
    return np.minimum(belt, front)
def troll_collar(p, j):
    t = j['Top']; c = V(0, t - 0.13, 0.14)
    ring = torus(p, c, 0.29, 0.05)
    spikes = np.full(p.shape[:-1], 9.0, np.float32)
    for i in range(7):
        a = i / 7 * np.pi * 2; d_ = V(np.sin(a), 0, np.cos(a))
        spikes = np.minimum(spikes, seg(p, c + d_ * 0.32, c + d_ * 0.44 + V(0, 0.03, 0), 0.028, 0.005))
    chain = np.full(p.shape[:-1], 9.0, np.float32)
    for i in range(4):
        at = V(0, t - 0.22 - i * 0.085, 0.45 - i * 0.015); q = p - at
        q = np.stack([q[..., 0], q[..., 2], q[..., 1]], -1) if i % 2 == 0 else np.stack([q[..., 2], q[..., 0], q[..., 1]], -1)
        a2 = np.sqrt((q[..., 0] / 0.65) ** 2 + q[..., 1] ** 2) - 0.04
        chain = np.minimum(chain, np.sqrt(a2 * a2 + q[..., 2] ** 2) - 0.012)
    return np.minimum(np.minimum(ring, spikes), chain)

def limbs(p, j, o, f):
    return np.minimum(f(side(p, 1), j, o), f(side(p, -1), j, o))

def pieces(kind):
    o = kind == 'ogre'; j = OGRE if o else TROLL
    skin = (0.43, 0.56, 0.78) if o else (0.37, 0.42, 0.48)
    P = [(lambda p: (ogre_torso if o else troll_torso)(p, j), skin)]
    for f in (upper, fore, hand, thigh, shin, foot):
        P.append((lambda p, f=f: limbs(p, j, o, f), skin))
    if o:
        P += [(lambda p: ogre_head(p, j), skin), (lambda p: ogre_eyes(p, j), (0.1, 0.08, 0.06)),
              (lambda p: ogre_tusks(p, j), (0.88, 0.85, 0.75)), (lambda p: ogre_horn(p, j), (0.6, 0.59, 0.55)),
              (lambda p: ogre_loin(p, j), (0.31, 0.42, 0.23)), (lambda p: limbs(p, j, o, ogre_band), (0.31, 0.42, 0.23))]
    else:
        P += [(lambda p: troll_head(p, j), skin), (lambda p: troll_eyes(p, j), (0.1, 0.08, 0.06)),
              (lambda p: troll_teeth(p, j), (0.85, 0.80, 0.62)), (lambda p: troll_belly(p, j), (0.78, 0.75, 0.66)),
              (lambda p: troll_collar(p, j), (0.30, 0.31, 0.33)), (lambda p: troll_loin(p, j), (0.29, 0.23, 0.16))]
    return P

def render(kind, yaw_deg, W=340, H=440):
    P = pieces(kind)
    def scene(p):
        ds = np.stack([f(p) for f, _ in P], 0)
        return ds.min(0), ds.argmin(0)
    yaw = np.radians(yaw_deg)
    fwd = np.array([-np.sin(yaw), 0, -np.cos(yaw)], np.float32)       # camera looks toward -z when yaw 0 (front view)
    right = np.array([np.cos(yaw), 0, -np.sin(yaw)], np.float32)
    up = np.array([0, 1, 0], np.float32)
    center = np.array([0, 1.25, 0], np.float32)
    span = 2.9
    xs = (np.arange(W) / W - 0.5) * span * W / H
    ys = (0.5 - np.arange(H) / H) * span
    X, Y = np.meshgrid(xs, ys)
    org = center - fwd * 6 + X[..., None] * right + Y[..., None] * up
    org = org.astype(np.float32)
    t = np.zeros((H, W), np.float32); hit = np.zeros((H, W), bool)
    for _ in range(140):
        p = org + fwd * t[..., None]
        d, _i = scene(p)
        hit |= d < 0.003
        t = np.where(hit, t, t + np.maximum(d * 0.9, 0.002))
    p = org + fwd * t[..., None]
    d, idx = scene(p)
    e = 0.004
    n = np.stack([scene(p + np.array([e, 0, 0], np.float32))[0] - scene(p - np.array([e, 0, 0], np.float32))[0],
                  scene(p + np.array([0, e, 0], np.float32))[0] - scene(p - np.array([0, e, 0], np.float32))[0],
                  scene(p + np.array([0, 0, e], np.float32))[0] - scene(p - np.array([0, 0, e], np.float32))[0]], -1)
    n /= np.maximum(length(n), 1e-6)[..., None]
    L = np.array([0.4, 0.8, 0.5], np.float32); L /= np.linalg.norm(L)
    lam = np.clip((n * L).sum(-1), 0, 1)
    shade = np.where(lam > 0.35, 1.0, 0.72)                            # toon: two bands
    cols = np.array([c for _, c in P], np.float32)
    img = cols[idx] * shade[..., None]
    img = np.where(hit[..., None], img, np.array([0.17, 0.17, 0.19], np.float32))
    t = np.where(hit, t, 50.0)
    # outline: silhouette edge + piece boundaries where depth jumps
    sil = hit.astype(np.float32)
    edge = np.zeros_like(sil)
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2)]:
        edge = np.maximum(edge, np.abs(np.roll(np.roll(sil, dx, 1), dy, 0) - sil))
        tj = np.roll(np.roll(t, dx, 1), dy, 0)
        edge = np.maximum(edge, ((np.abs(tj - t) > 0.12) & hit).astype(np.float32))
    img = np.where(edge[..., None] > 0, 0.05, img)
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))

if __name__ == '__main__':
    out = sys.argv[1]
    tiles = []
    for kind in ('ogre', 'troll'):
        for yaw in (0, 35, 90):
            tiles.append(render(kind, yaw))
    W, H = tiles[0].size
    sheet = Image.new('RGB', (W * 3, H * 2), (40, 40, 44))
    for i, tl in enumerate(tiles):
        sheet.paste(tl, ((i % 3) * W, (i // 3) * H))
    sheet.save(out)
    print('saved', out)
