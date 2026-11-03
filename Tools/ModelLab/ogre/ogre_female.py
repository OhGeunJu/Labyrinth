"""여성 오우거 — ogre_preview(남성 v15)의 식에 몸·머리 부위만 갈아 끼운다."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image, ImageDraw
import ogre_preview as G
from brute_preview import V, length, mirror, side, smin, smax, ell, rbox, sph, capsule, seg
J = G.J

def torso(p, j=J):   # 배를 줄이고 허리를 조이고 엉덩이를 넓힌다 · 가슴 둘
    t, h = j['Top'], j['Hip']; m = mirror(p)
    chest = ell(p, V(0, t - 0.20, 0.0), V(0.36, 0.25, 0.27))
    bust = ell(m, V(0.15, t - 0.31, 0.20), V(0.175, 0.16, 0.17))   # 앞으로 더 낸다
    delt = ell(m, V(0.31, t - 0.10, -0.03), V(0.14, 0.13, 0.15))
    traps = capsule(m, V(0.05, t + 0.04, -0.05), V(0.25, t - 0.04, -0.04), 0.085)
    waist = ell(p, V(0, h + 0.30, 0.02), V(0.33, 0.30, 0.28))
    hips = ell(p, V(0, h + 0.04, -0.03), V(0.44, 0.22, 0.32))
    upper = smin(smin(smin(chest, delt, 0.08), traps, 0.08), bust, 0.04)
    return smin(upper, smin(waist, hips, 0.14), 0.12)
def skin(p, gap, thick): return np.abs(torso(p) - gap) - thick

def wrap(p, j=J):    # 가슴을 감은 천 띠 — 어깨 끈 하나(사선)
    t = j['Top']
    band = smax(skin(p, 0.016, 0.022), np.abs(p[..., 1] - (t - 0.31)) - 0.13, 0.01)
    a = np.array([0.24, t - 0.22], np.float32); b = np.array([-0.18, t + 0.02], np.float32)
    q = p[..., :2]; ab = b - a
    k = np.clip(((q - a) * ab).sum(-1) / (ab * ab).sum(), 0, 1)
    line = np.sqrt(((q - (a + ab * k[..., None])) ** 2).sum(-1))
    strap = smax(smax(skin(p, 0.016, 0.02), line - 0.06, 0.01), -p[..., 2] - 0.0, 0.01)
    return np.minimum(band, strap)
def skirt(p, j=J):   # 허리띠 + 무릎까지 오는 천 — 앞뒤 두 장보다 길고 옆까지 두른다
    h = j['Hip']
    belt = smax(skin(p, 0.016, 0.026), np.abs(p[..., 1] - (h + 0.10)) - 0.06, 0.01)
    hem = h - 0.42 + 0.03 * np.sin(np.arctan2(p[..., 0], p[..., 2]) * 9)
    r = np.sqrt(p[..., 0] ** 2 / 1.0 + p[..., 2] ** 2 / 0.70)
    rad = 0.46 + 0.10 * np.clip((h - p[..., 1]) / 0.42, 0, 1)
    shell = np.abs(r - rad) - 0.018
    cloth = smax(smax(shell, p[..., 1] - (h + 0.08), 0.01), hem - p[..., 1], 0.01)
    return np.minimum(belt, cloth)
def buckle(p, j=J):
    disc = length(p[..., :2] - np.array([0, j['Hip'] + 0.10], np.float32)) - 0.08
    return smax(smax(skin(p, 0.046, 0.008), disc, 0.008), -p[..., 2], 0.01)

def head(p, j=J):    # 턱이 좁고 둥글다 · 아랫입술 덜 내밈
    t = j['Top']; m = mirror(p)
    cran = ell(p, V(0, t + 0.45, 0.02), V(0.30, 0.32, 0.30))
    jaw = rbox(p, V(0, t + 0.19, 0.13), V(0.22, 0.09, 0.18), 0.09)
    muzzle = ell(p, V(0, t + 0.31, 0.20), V(0.19, 0.075, 0.11))
    d = smin(smin(cran, jaw, 0.11), muzzle, 0.06)
    d = smin(d, G.nose(p, j) + 0.008, 0.03)
    d = smin(d, smin(G.ear(m, j), lobe(m, j), 0.03), 0.02)
    lip = seg(m, V(0.0, t + 0.245, 0.315), V(0.13, t + 0.225, 0.29), 0.026, 0.020)
    d = smin(d, lip, 0.03)
    return smax(d, -G.slit(p, j), 0.02)
def tusks(p, j=J):   # 짧은 엄니 — 윗입술 밑까지만
    m = mirror(p); t = j['Top']
    return seg(m, V(0.13, t + 0.23, 0.28), V(0.14, t + 0.32, 0.31), 0.030, 0.007)
def lid(p, j=J):     # 외눈 위 속눈썹 셋 — 눈꺼풀 위로 뻗친다
    e = G.ONE + V(0, j['Top'], 0); d = np.full(p.shape[:-1], 9.0, np.float32)
    for ang in (55, 90, 125):   # 가운데가 똑바로 위
        a = np.radians(ang)
        s = e + V(0.10 * np.cos(a) * 0.95, 0.095 * np.sin(a), 0.045)
        f = e + V(0.15 * np.cos(a), 0.14 * np.sin(a) + 0.01, 0.03)
        d = np.minimum(d, seg(p, s, f, 0.012, 0.004))
    return d
def lid2(p, j=J):
    e = G.TWO + V(0, j['Top'], 0); d = np.full(p.shape[:-1], 9.0, np.float32)
    for ang in (40, 75, 110):   # 눈마다 셋 · 바깥으로 살짝 (좌우 대칭)
        a = np.radians(ang)
        s = e + V(0.085 * np.cos(a), 0.085 * np.sin(a), 0.04)
        f = e + V(0.125 * np.cos(a), 0.125 * np.sin(a) + 0.01, 0.025)
        d = np.minimum(d, seg(mirror(p), s, f, 0.011, 0.004))
    return d
def hair(p, j=J):    # 뿔 둘레에서 묶어 등 뒤로 늘인 굵은 땋은 머리
    t = j['Top']; d = np.full(p.shape[:-1], 9.0, np.float32)
    knot = ell(p, V(0, t + 0.70, -0.14), V(0.13, 0.08, 0.12))
    pts = [V(0, t + 0.68, -0.22), V(0, t + 0.52, -0.33), V(0, t + 0.34, -0.36), V(0, t + 0.16, -0.40),
           V(0, t - 0.02, -0.44), V(0, t - 0.20, -0.46), V(0, t - 0.38, -0.46), V(0, t - 0.54, -0.45)]
    for i in range(len(pts) - 1):
        a, b = pts[i], pts[i + 1]; r = 0.085 - 0.004 * i
        d = smin(d, ell(p, (a + b) * 0.5, V(r, float(np.linalg.norm(b - a)) * 0.62, r)), 0.02)
    return smin(knot, d, 0.03)
def hair_ring(p, j=J):
    t = j['Top']; c = V(0, t - 0.44, -0.455)
    q = p - c; rr = np.sqrt(q[..., 0] ** 2 + q[..., 2] ** 2)
    return smax(rr - 0.075, np.abs(q[..., 1]) - 0.035, 0.01)
EAR_A = np.radians(52)
EAR_N = V(np.sin(EAR_A), 0, np.cos(EAR_A))     # 귀가 보는 쪽(앞-바깥)
EAR_T = V(np.cos(EAR_A), 0, -np.sin(EAR_A))    # 귀 면을 따라 가로
def earring(p, j=J):   # 귓불을 꿰고 매달린 고리 — 고리 면이 귀 면을 뚫고 지나간다
    m = mirror(p); t = j['Top']
    lobe = V(0.31, t + 0.42, -0.02) + EAR_T * 0.02 + V(0, -0.075, 0)
    q = m - (lobe + V(0, -0.055, 0))
    u = (q * EAR_N).sum(-1); w = (q * EAR_T).sum(-1)
    a = np.sqrt(u * u + q[..., 1] ** 2) - 0.06
    return np.sqrt(a * a + w * w) - 0.012
def lobe(m, j=J):      # 귀걸이가 걸릴 도톰한 귓불
    t = j['Top']
    return sph(m, V(0.31, t + 0.42, -0.02) + EAR_T * 0.02 + V(0, -0.075, 0), 0.035)

def upper(m, j=J):   # 팔은 두 단 가늘게
    return G.upper(m, j) + 0.05
def fore(m, j=J): return G.fore(m, j) + 0.045
def band(m, j=J): return smax(np.abs(fore(m) - 0.012) - 0.018, np.abs(m[..., 1] - (j['Wrist'] + 0.15)) - 0.08, 0.01)
def hand(m, j=J): return G.hand(m, j) + 0.012
def thigh(m, j=J): return G.thigh(side(m, 1) - V(0.02, 0, 0), j)
def foot(m, j=J):    # 폭을 줄이고 발가락도 작게
    lx = j['LX']
    d = rbox(m, V(lx, 0.085, 0.09), V(0.125, 0.085, 0.23), 0.08)
    for x, r in zip((-0.08, -0.025, 0.03, 0.08), (0.048, 0.042, 0.040, 0.036)):
        d = smin(d, sph(m, V(lx + x, 0.05, 0.30), r), 0.025)
    return d
def both(f): return lambda p: np.minimum(f(side(p, 1)), f(side(p, -1)))

SKIN = G.SKIN; CLOTH = (0.55, 0.30, 0.25); HAIR = (0.18, 0.16, 0.22)
def pieces(one_eye, skin=SKIN):
    eyes = ([(G.one_white, G.WHITE), (G.one_iris, G.IRIS), (G.one_pupil, G.DARK), (lid, G.DARK)] if one_eye else
            [(G.two_white, G.WHITE), (G.two_iris, G.IRIS), (G.two_pupil, G.DARK), (lid2, G.DARK)])
    return [(torso, skin), (head, skin), (G.mouth, G.DARK), (G.teeth, G.TOOTH), (tusks, G.TOOTH), (G.horn, G.HORN)] + eyes + [
            (hair, HAIR), (hair_ring, G.METAL), (earring, G.METAL),
            (wrap, CLOTH), (skirt, CLOTH), (buckle, G.METAL),
            (both(upper), skin), (both(fore), skin), (both(hand), skin), (both(band), CLOTH),
            (both(thigh), skin), (both(G.shin), skin), (both(foot), skin)]

if __name__ == '__main__':
    out = sys.argv[1]
    M = G.pieces(True); F = pieces(True)
    tiles = [G.render(M, 0), G.render(M, 35), G.render(F, 0), G.render(F, 35), G.render(F, 90), G.render(F, 180)]
    W, H = tiles[0].size
    sheet = Image.new('RGB', (W * 6, H + 480), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for i, tl in enumerate(tiles): sheet.paste(tl, (i * W, 0))
    face = [G.render(pieces(True), 0, 240, 240, J['Top'] + 0.40, 0.95), G.render(pieces(False), 0, 240, 240, J['Top'] + 0.40, 0.95),
            G.render(pieces(True), 35, 240, 240, J['Top'] + 0.40, 0.95)]
    ear = [G.render(pieces(True), 60, 240, 240, J['Top'] + 0.35, 0.55), G.render(pieces(True), 100, 240, 240, J['Top'] + 0.35, 0.55)]
    skins = [(0.48, 0.62, 0.85), (0.80, 0.42, 0.30), (0.55, 0.50, 0.62)]
    for i, tl in enumerate(face): sheet.paste(tl, (i * 240, H))
    for i, c in enumerate(skins): sheet.paste(G.render(pieces(False, c), 25, 240, 240, 1.25, 2.85), (720 + i * 240, H))
    for i, tl in enumerate(ear): sheet.paste(tl, (i * 240, H + 240))
    sheet.paste(G.render(pieces(True), 0, 240, 240, J['Hip'] - 0.1, 1.6), (480, H + 240)); sheet.paste(G.render(F, 90, 240, 240, J['Top'] - 0.2, 1.0), (720, H + 240))
    for i, l in enumerate(['남 앞', '남 3/4', '여 앞', '여 3/4', '여 옆', '여 뒤']): d.text((i * W + 6, 4), ['M front','M 3/4','F front','F 3/4','F side','F back'][i], fill=(230, 230, 230))
    sheet.save(out); print('saved', out)
