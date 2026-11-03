import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
# 목걸이 v54 — 끈은 직접 저작한 줄(캡슐 사슬): 뒤는 쇠목줄 고리를 따라 목을 두르고, 앞은 목 양옆에서 가슴으로 U자로 처진다.
# 장신구는 처진 아래쪽에 — 맨 구슬(뼈 구슬) · 굽은 송곳니가 번갈아.
import numpy as np
import troll_preview as T
from brute_preview import V
J = T.J; Top = J['Top']
CORD_R = 0.006
BEAD_R = 0.026
DROP = 0.52          # 앞 처짐 맨 아래(꼭대기 아래)
RING_A = 0.20        # 뒤 고리 옆 반폭(머리 좌표)
RING_RISE = 0.45     # v56 — 뒤 고리 높이를 쇠목줄의 0.45배로(그대로면 머리 꼭대기 위로 떠 보였다)
N = 7; SPACING = 0.078
GAP = 0.018         # 몸에서 띄우는 거리(v55 — 살에 붙어 보였다)

def to_body(h): return np.array([h[0] * T.WIDE, h[1] - T.HEAD_DROP, h[2] + T.HEAD_SHIFT], np.float32)

def torso_front(x, y):
    zs = np.linspace(1.2, -0.3, 1500, dtype=np.float32)
    p = np.stack([np.full_like(zs, x), np.full_like(zs, y), zs], -1)
    d = T.torso(p, J); k = np.argmax(d < 0)
    return float(zs[k]) if d[k] < 0 else None

# 뒤 고리(φ 0..π — 옆 → 목 뒤 위 → 옆)
back = []
for ph in np.linspace(0, np.pi, 17):
    h = T.COLLAR_C + T.CT * (T.COLLAR_B * RING_RISE * np.sin(ph)) + V(RING_A * np.cos(ph), 0, 0)
    back.append(to_body(h))
# v56 — 뒤 고리를 살에 붙인다(쇠목줄 고리 그대로면 가는 끈이 목 위로 떴다): 점마다 고리 가운데 쪽으로 당겨 살 위 끈 굵기 + .002에 둔다.
_RC = to_body(T.COLLAR_C)
def _body(q): return float(np.minimum(T.head(q[None], J), T.torso(q[None], J))[0])
def hug(pt):
    d = _RC - pt; L = float(np.linalg.norm(d)); d = d / L; want = CORD_R + 0.002
    if _body(pt) < want:   # 이미 살 속 — 같은 높이에서 수평으로(뒤 · 옆) 민다(고리 가운데에서 밀면 위로 올라가 다시 뜬다)
        hd = np.array([-d[0], 0, -d[2]], np.float32); hd /= max(float(np.linalg.norm(hd)), 1e-6)
        for t in np.linspace(0, 0.35, 700):
            q = pt + hd * t
            if _body(q) >= want: return q.astype(np.float32)
        return pt
    for t in np.linspace(0, L, 800):
        q = pt + d * t
        if _body(q) <= want: return q.astype(np.float32)
    return pt
# (살 위로 밀면 굵은 목이 머리보다 넓어 정면에서 얼굴 테가 됐다 — 뒤 고리는 살 속에 묻혀 가려지게 둔다)
side = back[0]; X0, YS = float(side[0]), float(side[1])
YB = Top - DROP

def front_xy(t):   # t 0(옆) → π/2(가운데 아래) — 옆에서는 곧게 떨어지고 아래에서는 눕는다
    return X0 * np.cos(t), YS - (YS - YB) * np.sin(t)

def front_pt(t, off):
    x, y = front_xy(t); z = torso_front(x, y)
    if z is None: z = float(side[2])
    return np.array([x, y, z + off], np.float32)

ts = np.linspace(0, np.pi / 2, 20)
front_r = [front_pt(t, CORD_R + 0.004 + GAP) for t in ts]          # 오른쪽(+x) 옆 → 가운데
# 옆 이음 — 고리 옆에서 앞 처짐 첫 점으로(머리 속을 지나 가려진다)
CORD = [p for p in back] + [np.array([-p[0], p[1], p[2]], np.float32) for p in front_r] + [p for p in reversed(front_r)]
# 정리: 뒤 고리는 +x → -x 순 · 이어서 -x 앞 → 가운데 → +x 앞 → (고리 처음으로 닫힘)
CORD = back + [np.array([-p[0], p[1], p[2]], np.float32) for p in front_r] + list(reversed(front_r))[1:] + [back[0]]

# 앞 호의 길이표 — 장신구를 아래 가운데부터 같은 간격으로
fine = np.linspace(np.pi / 2, 0, 400)
xy = np.array([front_xy(t) for t in fine]); seg = np.r_[0, np.cumsum(np.linalg.norm(np.diff(xy, axis=0), axis=1))]
def t_at(s): return float(np.interp(s, seg, fine))

BEADS, FANGS = [], []
mid = N // 2
for i in range(N):
    k = i - mid; s = abs(k) * SPACING; t = t_at(s); sg = np.sign(k) if k else 1.0
    x, y = front_xy(t); x *= sg
    tx, ty = -X0 * np.sin(t) * sg, -(YS - YB) * np.cos(t)          # 호의 방향(바깥으로 갈 때)
    tn = np.array([tx, ty]); tn /= np.linalg.norm(tn)
    n = np.array([tn[1], -tn[0]]);
    if n[1] > 0: n = -n                                              # 아래 · 바깥
    if i % 2 == 1:   # 구슬 — 끈 바로 아래
        cx, cy = x + n[0] * 0.010, y + n[1] * 0.010
        BEADS.append(V(cx, cy, torso_front(cx, cy) + BEAD_R + 0.004 + GAP))
    else:            # 송곳니 — 바깥 아래로 뻗고 끝이 가운데로 휜다
        L = 0.12 + 0.05 * (1 - abs(k) / mid)
        side_ = np.sign(x) if abs(x) > 1e-4 else 0.0
        ang = np.radians(28) * side_; c, s_ = np.cos(ang), np.sin(ang)
        n2 = np.array([n[0] * c + n[1] * s_, n[1] * c - n[0] * s_]) if side_ else n
        p0 = np.array([x, y]) + n * 0.004; p1 = p0 + n * L * 0.55; p2 = p1 + n2 * L * 0.45
        z = [torso_front(*p) + r + GAP for p, r in ((p0, 0.025), (p1, 0.017), (p2, 0.006))]
        FANGS.append((V(p0[0], p0[1], z[0]), V(p1[0], p1[1], z[1]), V(p2[0], p2[1], z[2])))

def cord(p, j=J):
    d = np.full(p.shape[:-1], 9.0, np.float32)
    for a, b in zip(CORD[:-1], CORD[1:]):
        d = np.minimum(d, T.capsule(p, a, b, CORD_R))
    return d
def beads(p, j=J):
    d = np.full(p.shape[:-1], 9.0, np.float32)
    for c in BEADS: d = np.minimum(d, T.sph(p, c, BEAD_R))
    return d
def fangs(p, j=J):
    d = np.full(p.shape[:-1], 9.0, np.float32)
    for a, b, c in FANGS:
        d = np.minimum(d, T.smin(T.seg(p, a, b, 0.022, 0.015), T.seg(p, b, c, 0.015, 0.002), 0.004))
    return d
SINEW = (0.10, 0.08, 0.07)
def pieces():
    T.PICK['Neck'] = 'Off'
    return T.pieces() + [(cord, SINEW), (beads, T.BONE), (fangs, (0.93, 0.90, 0.80))]

# v56 — 뒤 고리 끝(머리 속)과 앞 처짐 첫 점(가슴 면) 사이가 옆에서 ㄱ자로 꺾였다 → 닫힌 줄 전체를 고르게 펴고(라플라스),
# 편 뒤 살 속으로 든 점은 거리장 기울기를 따라 살 위 끈 굵기 + .002까지 밀어낸다. 그다음 점 간격을 고르게 다시 뽑는다.
def _grad(q, e=0.002):
    g = np.array([_body(q + V(e, 0, 0)) - _body(q - V(e, 0, 0)),
                  _body(q + V(0, e, 0)) - _body(q - V(0, e, 0)),
                  _body(q + V(0, 0, e)) - _body(q - V(0, 0, e))], np.float32)
    return g / max(float(np.linalg.norm(g)), 1e-6)
def _push(q, want):
    for _ in range(60):
        b = _body(q)
        if b >= want: break
        q = q + _grad(q) * (want - b + 0.0005)
    return q.astype(np.float32)
def _resample(pts, n):
    P = np.array(pts + [pts[0]]); L = np.r_[0, np.cumsum(np.linalg.norm(np.diff(P, axis=0), axis=1))]
    ss = np.linspace(0, L[-1], n, endpoint=False)
    return [np.array([np.interp(s, L, P[:, k]) for k in range(3)], np.float32) for s in ss]
_loop = _resample(CORD[:-1], 64)
_pin = [float(q[1]) < Top - 0.30 for q in _loop]   # 장신구가 걸린 앞 아래쪽은 그대로(펴면 고리가 줄어 장신구가 끈 밑에 뜬다)
for _ in range(25):
    _loop = [_loop[i] if _pin[i] else (_loop[i - 1] + _loop[i] * 2 + _loop[(i + 1) % len(_loop)]) / 4 for i in range(len(_loop))]
CORD = _loop + [_loop[0]]
