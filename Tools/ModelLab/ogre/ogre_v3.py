"""오우거 v3 — 뿔(없음 · 하나 · 부러진, 키움) · 여성 다리 가늘게 · 머리 둘."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image, ImageDraw
import ogre_preview as G
import ogre_female as F
from brute_preview import V, side, smax, seg
J = G.J; T = J['Top']

def horn(p, j=J):        # 키운 뿔 — 더 굵고 더 높이 솟아 뒤로 휜다
    t = j['Top']; a, b, c = V(0, t + 0.70, 0.05), V(0, t + 0.94, 0.01), V(0, t + 1.12, -0.08)
    return np.minimum(seg(p, a, b, 0.125, 0.072), seg(p, b, c, 0.072, 0.012))
def horn_broken(p, j=J):
    t = j['Top']
    stub = seg(p, V(0, t + 0.70, 0.05), V(0, t + 0.94, 0.01), 0.125, 0.072)
    return smax(stub, p[..., 1] - (t + 0.86 + 0.06 * p[..., 0]), 0.01)
HORNS = {'none': [], 'one': [(horn, G.HORN)], 'broken': [(horn_broken, G.HORN)]}

def thigh_f(m, j=J): return F.thigh(m, j) + 0.045        # 허벅지 · 정강이 한 단 가늘게
def shin_f(m, j=J): return G.shin(m, j) + 0.045
def foot_f(m, j=J):                                       # 남성 발의 0.9배
    k = 0.9; lx = j['LX']; q = m.copy(); q[..., 0] = lx + (m[..., 0] - lx) / k
    return G.foot(q, j) * k

def head_parts(sex, hrn, one_eye):
    if sex == 'M':
        eyes = [(G.one_white, G.WHITE), (G.one_iris, G.IRIS), (G.one_pupil, G.DARK)] if one_eye else \
               [(G.two_white, G.WHITE), (G.two_iris, G.IRIS), (G.two_pupil, G.DARK)]
        return [(G.head, G.SKIN), (G.mouth, G.DARK), (G.teeth, G.TOOTH), (G.tusks, G.TOOTH)] + HORNS[hrn] + eyes
    eyes = [(G.one_white, G.WHITE), (G.one_iris, G.IRIS), (G.one_pupil, G.DARK), (F.lid, G.DARK)] if one_eye else \
           [(G.two_white, G.WHITE), (G.two_iris, G.IRIS), (G.two_pupil, G.DARK), (F.lid2, G.DARK)]
    return [(F.head, G.SKIN), (G.mouth, G.DARK), (G.teeth, G.TOOTH), (F.tusks, G.TOOTH)] + HORNS[hrn] + eyes + \
           [(F.hair, F.HAIR), (F.hair_ring, G.METAL), (F.earring, G.METAL)]
def body_parts(sex):
    b = lambda f: F.both(f)
    if sex == 'M':
        return [(G.torso, G.SKIN), (G.sash, G.CLOTH), (G.loin, G.CLOTH), (G.buckle, G.METAL),
                (b(G.upper), G.SKIN), (b(G.fore), G.SKIN), (b(G.hand), G.SKIN), (b(G.band), G.CLOTH), (b(G.spike), G.METAL),
                (b(G.thigh), G.SKIN), (b(G.shin), G.SKIN), (b(G.foot), G.SKIN)]
    return [(F.torso, G.SKIN), (F.wrap, F.CLOTH), (F.skirt, F.CLOTH), (F.buckle, G.METAL),
            (b(F.upper), G.SKIN), (b(F.fore), G.SKIN), (b(F.hand), G.SKIN), (b(F.band), F.CLOTH),
            (b(thigh_f), G.SKIN), (b(shin_f), G.SKIN), (b(foot_f), G.SKIN)]

# 머리 둘 — 목 밑동(0, Top, 0)을 축으로 줄이고 · 옆으로 옮기고 · 바깥으로 기울이고 돌린다
TWIN_S, TWIN_X, TWIN_DROP, TWIN_ROLL, TWIN_YAW = 0.74, 0.26, -0.08, 0.0, 0.0   # 똑바로 · 안쪽 귀가 안 닿을 만큼만 띄운다
TWIN_KX = 0.88   # 머리 가로만 더 줄인다
def to_local(p, sgn):
    piv = V(0, T, 0); q = p - piv - V(sgn * TWIN_X, TWIN_DROP, 0)
    th = np.radians(sgn * TWIN_ROLL); c, s = np.cos(th), np.sin(th)        # 기울임을 되돌린다
    x, y = q[..., 0] * c + q[..., 1] * s, -q[..., 0] * s + q[..., 1] * c
    ph = np.radians(sgn * TWIN_YAW); c2, s2 = np.cos(ph), np.sin(ph)       # 돌림을 되돌린다
    z = q[..., 2]; x, z = x * c2 - z * s2, x * s2 + z * c2
    return np.stack([x / TWIN_KX, y, z], -1) / TWIN_S + piv
def on_head(f, sgn): return lambda p: f(to_local(p, sgn)) * TWIN_S * min(1.0, TWIN_KX)

SADDLE_Y, SADDLE_K, SADDLE_R = 0.03, 1.0, 0.12   # A — 얕고 부드럽게(사용자 고름)(-0.06 · 0.07은 파여 보였다)
def saddle(f):       # 머리 둘 — 두 얼굴 사이 몸통 위를 안장처럼 낮춘다(가운데가 턱 밑 · 머리 밑에서 원래 높이로)
    def g(p):
        cut = p[..., 1] - (T + SADDLE_Y + SADDLE_K * p[..., 0] ** 2)
        return smax(f(p), cut, SADDLE_R)
    return g
SADDLED = {G.torso, G.sash, F.torso, F.wrap}

def build(sex, hrn='one', eyes=(True,), twin=False):
    if not twin: return head_parts(sex, hrn, eyes[0]) + body_parts(sex)
    parts = []
    hrns = hrn if isinstance(hrn, tuple) else (hrn, hrn)   # 뿔도 머리마다 따로
    for sgn, one, h in zip((-1, 1), eyes, hrns):
        parts += [(on_head(f, sgn), c) for f, c in head_parts(sex, h, one)]
    return parts + [(saddle(f) if f in SADDLED else f, c) for f, c in body_parts(sex)]

if __name__ == '__main__':
    out = sys.argv[1]; R = G.render
    row1 = [(build('M', 'none'), 0, 'M no horn'), (build('M', 'one'), 0, 'M horn'), (build('M', 'broken'), 30, 'M broken'),
            (build('F', 'none'), 0, 'F no horn'), (build('F', 'one'), 30, 'F horn'), (build('F', 'broken'), 0, 'F broken')]
    row2 = [(build('M', 'one', (True, True), True), 0, 'M twin horn+horn'), (build('M', ('one', 'broken'), (True, False), True), 30, 'M twin horn+broken 3/4'),
            (build('M', ('none', 'one'), (False, False), True), 0, 'M twin none+horn'),
            (build('F', 'one', (True, True), True), 0, 'F twin horn+horn'), (build('F', ('broken', 'one'), (True, False), True), 30, 'F twin broken+horn 3/4'),
            (build('F', ('one', 'none'), (False, True), True), 180, 'F twin back')]
    W, H = 300, 420
    sheet = Image.new('RGB', (W * 6, H * 2), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    rows = (row1, row2) if len(sys.argv) < 3 else (row2,)
    sheet = Image.new('RGB', (W * 6, H * len(rows)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for r, row in enumerate(rows):
        for i, (P, yaw, lab) in enumerate(row):
            sheet.paste(R(P, yaw, W, H, 1.35 if row is row2 else 1.25, 3.1 if row is row2 else 2.85), (i * W, r * H))
            d.text((i * W + 6, r * H + 4), lab, fill=(230, 230, 230))
    sheet.save(out); print('saved', out)
