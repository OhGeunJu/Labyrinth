"""여성 오크 머리카락 — 좌표를 박지 않고 머리 표면에서 뽑는다(머리 바탕이 바뀌어도 붙는다)."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import numpy as np
from brute_preview import V, smin, smax, ell, seg, sph
import orc_parts as O
from orc_preview import head, W, H, D, F, T_, R_

def on_head(a_deg, y, out=0.0):
    """방위각 a(0 = 정면, 90 = 오른쪽 옆, 180 = 뒤) · 높이 y의 머리 표면 점 — out만큼 바깥으로."""
    uy = y / H; r = float(np.interp(np.clip(uy + 0.5, 0, 1), T_, R_))
    a = np.radians(a_deg); sh = F * 2 * uy * uy if uy < 0 else 0.0
    x = r * np.sin(a) * W; z = (r * np.cos(a) + sh) * D
    n = np.array([x / W ** 2, 0, (z / D - sh) / D], np.float32); n /= max(np.linalg.norm(n), 1e-6)
    return V(x, y, z) + n * out

# 이마 선 — 앞은 이마 위, 옆은 귀 위, 귀 뒤에서 목덜미로 떨어진다
LINE_A = np.radians([0, 20, 45, 75, 100, 118, 140, 180])
LINE_Y = np.array([0.17, 0.16, 0.12, 0.10, 0.10, -0.10, -0.24, -0.27])   # 관자놀이에서 내려오고 · 귀 뒤에서 목덜미까지 덮는다
def cap(p, th=0.030, front=0.0):
    ang = np.abs(np.arctan2(p[..., 0], p[..., 2]))
    above = p[..., 1] - (np.interp(ang, LINE_A, LINE_Y) + front)
    k = np.clip(above / 0.025, 0, 1); k = k * k * (3 - 2 * k)
    hair = head(p) - th * k
    return smax(hair, -above, 0.006)

def chain(p, pts, r0, r1): return O.chain(p, pts, r0, r1)
def NONE(p): return O.NONE(p)
def down(a_deg, y0, y1, n, out0, out1, flare=0.0):
    """머리 표면을 따라 내려오다, 머리가 안으로 말려 들어가는 높이부터는 곧게 떨어지는 가닥 점들(목 · 몸 속으로 안 파고든다)."""
    pts = []; best = None
    for i in range(n):
        k = i / (n - 1); y = y0 + (y1 - y0) * k
        q = on_head(a_deg, max(y, -H * 0.45), out0 + (out1 - out0) * k)
        hr = float(np.hypot(q[0], q[2]))
        if best is None or hr >= best[0]: best = (hr, q[0], q[2])
        else: q = V(best[1], q[1], best[2])                # 가장 불룩한 자리에서 수직으로
        q = V(q[0] * (1 + flare * k), y, q[2] * (1 + flare * k))
        pts.append(q)
    return pts

def h_long_braid(p):   # 덮개 + 뒤통수 가운데서 등까지 땋아 내린 한 가닥
    pts = down(180, 0.16, -0.66, 9, 0.04, 0.05)
    tie = on_head(180, 0.16, 0.04)
    deco = np.minimum(O.cuff(p, pts[5] + V(0, 0.02, 0), pts[5] - V(0, 0.01, 0), 0.044), O.cuff(p, pts[-1] + V(0, 0.015, 0), pts[-1] - V(0, 0.015, 0), 0.036))
    return smin(smin(cap(p), sph(p, tie, 0.055), 0.03), chain(p, pts, 0.050, 0.032), 0.03), deco

def h_twin_tails(p):   # 덮개 + 귀 뒤 낮게 묶은 두 꼬리
    m = O.mirror(p)
    k = on_head(125, -0.02, 0.04)
    tail = seg(m, k, k + V(0.03, -0.42, 0.06), 0.055, 0.022)
    return smin(smin(cap(p), sph(m, k, 0.055), 0.03), tail, 0.03), O.cuff(m, k + V(0, 0.01, -0.01), k + V(0.004, -0.03, 0.0), 0.058)

def h_bun(p):          # 덮개 + 뒤통수 위에 틀어 올린 쪽 + 뼈 비녀
    c = on_head(180, 0.20, 0.07)
    bun = ell(p, c, V(0.11, 0.095, 0.085))
    pin = seg(p, c + V(-0.17, 0.05, 0.0), c + V(0.17, -0.03, -0.01), 0.012, 0.008)
    return smin(cap(p), bun, 0.04), pin

def h_ponytail(p):     # 덮개 + 뒤통수 높이 묶어 내린 꼬리
    c = on_head(180, 0.12, 0.05)
    tail = seg(p, c + V(0, -0.02, -0.03), c + V(0, -0.56, -0.08), 0.058, 0.025)
    return smin(smin(cap(p), sph(p, c, 0.06), 0.03), tail, 0.03), O.cuff(p, c + V(0, -0.07, -0.038), c + V(0, -0.11, -0.042), 0.062)

def h_braids(p):       # 덮개 + 관자놀이에서 귀 앞으로 내린 땋은 가닥 둘
    m = O.mirror(p)
    pts = down(88, 0.12, -0.46, 8, 0.035, 0.05, flare=0.10)
    deco = np.minimum(O.cuff(m, pts[4] + V(0, 0.015, 0), pts[4] - V(0, 0.012, 0), 0.036), O.cuff(m, pts[-1] + V(0, 0.012, 0), pts[-1] - V(0, 0.012, 0), 0.031))
    return smin(cap(p), chain(m, pts, 0.036, 0.026), 0.02), deco

def h_topknot(p):      # 덮개 + 정수리 상투에서 뒤로 늘인 꼬리
    top = on_head(180, 0.30, 0.02)
    root = seg(p, top, top + V(0, 0.09, -0.03), 0.065, 0.055)
    fall = seg(p, top + V(0, 0.09, -0.03), top + V(0, -0.30, -0.24), 0.05, 0.02)
    return smin(smin(cap(p), root, 0.03), fall, 0.03), O.cuff(p, top + V(0, 0.04, -0.012), top + V(0, 0.075, -0.024), 0.064)

HAIRS = [('LongBraid', h_long_braid), ('TwinTails', h_twin_tails), ('Bun', h_bun),
         ('Ponytail', h_ponytail), ('Braids', h_braids), ('Topknot', h_topknot)]

# ── 앞머리 — 이마 선 밑으로 내려오는 얇은 껍데기 · 밑단 모양만 다르다(θ = 정면에서 잰 부호 있는 방위각) ──
def _bang_hem_straight(th): return 0.105 + 0.012 * np.abs(np.sin(th * 14))                       # 일자 · 끝이 톱니
def _bang_hem_part(th):     return 0.165 - 0.115 * np.clip(np.abs(th) / 0.85, 0, 1) ** 0.8       # 가운데 가르마 · 양쪽으로 커튼
def _bang_hem_swept(th):    return 0.150 - 0.085 * np.clip((th + 0.7) / 1.5, 0, 1)                # 왼쪽에서 오른쪽 아래로 넘김
BANGS = {'Straight': _bang_hem_straight, 'Part': _bang_hem_part, 'Swept': _bang_hem_swept}
BANG = 'Part'
def bangs(p, style=None):
    hem = BANGS[style or BANG]
    th = np.arctan2(p[..., 0], p[..., 2])
    shell = head(p) - (0.018 + 0.012 * np.clip((p[..., 1] - 0.08) / 0.08, 0, 1))   # 위가 조금 두껍다
    d = smax(shell, hem(th) - p[..., 1], 0.006)                                     # 밑단 아래는 없다
    d = smax(d, np.abs(th) - 1.15, 0.03)                                            # 관자놀이까지만
    return smax(d, -p[..., 2], 0.02)

_cap0 = cap
def cap(p, th=0.030, front=0.0): return smin(_cap0(p, th, front), bangs(p), 0.012)

# ── 앞머리 — 이마를 덮는 얇은 판(덮개보다 조금 두껍다) · 끝은 들쭉날쭉(오크라 가지런하지 않다) ──
def _jag(x, n=34.0, a=0.016):   # 세모 물결 — 머리 끝 가닥
    f = x * n; return a * (np.abs(f - np.floor(f) - 0.5) * 2)
FRINGE_Y = {   # 앞머리 끝(아랫선) 높이 — x(오른쪽 +)의 함수
    'Blunt':   lambda x: 0.085 + 0.0 * x,                                   # 눈썹 바로 위 일자
    'Swept':   lambda x: np.clip(0.055 + 0.42 * (x + 0.16), 0.055, 0.17),   # 왼쪽 눈썹까지 덮고 오른쪽으로 걷어 올린다
    'Curtain': lambda x: np.clip(0.165 - 0.55 * np.abs(x), 0.06, 0.165),    # 가운데 가르마 · 양옆으로 갈라 내려온다
}
def fringe(p, kind, th=0.040):
    if kind is None: return NONE(p)
    x = p[..., 0]; ang = np.abs(np.arctan2(x, p[..., 2]))
    hem = FRINGE_Y[kind](x) - _jag(x)
    top = np.interp(ang, LINE_A, LINE_Y) + 0.03
    k = np.clip((p[..., 1] - hem) / 0.02, 0, 1); k = k * k * (3 - 2 * k)
    shell = head(p) - th * k
    band = smax(-(p[..., 1] - hem), p[..., 1] - top, 0.004)                  # 끝선과 이마 선 사이만
    side = ang - np.radians(70)                                              # 관자놀이에서 끝난다
    if kind == 'Curtain':                                                    # 가르마 — 가운데 좁은 틈
        band = smax(band, -(np.abs(x) - 0.010), 0.004)
    return smax(smax(shell, band, 0.004), side, 0.02)
def with_fringe(hair, kind):
    def h(p):
        a, b = hair(p); return smin(a, fringe(p, kind), 0.01), b
    return h
FRINGES = ['Blunt', 'Swept', 'Curtain']
