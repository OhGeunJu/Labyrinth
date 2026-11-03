"""여성 오크 — 남성 판(orc_parts)과 같은 머리 바탕에 부위만 갈아 끼운다."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image, ImageDraw
import orc_parts as O
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg
from orc_preview import head, front, y_of, W, H, EYE_T, MOUTH_T, NOSE_T, EY, MY, NY, EX, ER, render

NONE = O.NONE
def brow_f(p):   # 선반이 얇고 덜 노려본다 — 바깥 끝이 살짝 올라간다
    m = mirror(p); z = front(EYE_T + 0.12)
    a = V(0.035, EY + ER * 1.45 + 0.012, z + 0.002)
    b = V(EX + ER * 1.15, EY + ER * 1.75 + 0.03, z - 0.035)
    return seg(m, a, b, 0.020, 0.014)
JAW = (MOUTH_T - 0.10, 0.14, 0.055, 0.10)   # (높이 t, 반폭, 반높이, 반깊이) — None이면 턱을 안 덧댄다
def jaw_f(p):
    if JAW is None: return NONE(p)
    t, hw, hh, hd = JAW
    zc = front(0.12) - 0.14
    return rbox(p, V(0, y_of(t), zc), V(hw, hh, hd), min(0.07, hh * 0.95))
def lip_f(p):    # 아랫입술은 덜 내민다
    q = O.lip_bend(p)
    return capsule(q, V(-0.10, MY - 0.022, O.LIP_Z - 0.012), V(0.10, MY - 0.022, O.LIP_Z - 0.016), 0.024)
def head_f(p):
    d = head(p)
    d = smin(d, jaw_f(p), 0.07)
    d = np.maximum(d, -H * 0.5 - p[..., 1])
    d = smin(d, brow_f(p), 0.035)
    d = smin(d, O.nose(p) + 0.004, 0.015)   # 코는 조금 작게
    d = smin(d, O.ear(p), 0.02)
    lip = lip_f(p)
    tight, fill = smin(d, lip, 0.02), smin(d, lip, 0.06)
    w = np.clip((MY - 0.02 - p[..., 1]) / 0.04, 0, 1); w = w * w * (3 - 2 * w)
    return tight + (fill - tight) * w
def lips_f(p):   # 입술 색 — 아랫입술 겉만
    return smax(lip_f(p) - 0.003, -(p[..., 2] - (O.LIP_Z - 0.02)), 0.004)
def tusks_f(p):  # 짧은 엄니 — 입술 위로 조금만
    m = mirror(p); z = O.LIP_Z - 0.008
    a = V(0.095, MY - 0.025, z); b = V(0.105, MY + 0.025, z + 0.010); c = V(0.112, MY + 0.050, z + 0.004)
    return np.minimum(seg(m, a, b, 0.019, 0.013), seg(m, b, c, 0.013, 0.003))
def lash(p):     # 눈 바깥 위에 짧은 속눈썹 두 가닥
    m = mirror(p); z = front(EYE_T) + 0.004
    d = seg(m, V(EX + ER * 0.55, EY + ER * 0.85, z), V(EX + ER * 1.25, EY + ER * 1.25, z - 0.012), 0.009, 0.003)
    return np.minimum(d, seg(m, V(EX + ER * 0.85, EY + ER * 0.45, z - 0.004), V(EX + ER * 1.45, EY + ER * 0.70, z - 0.016), 0.008, 0.003))

def body_f(p):   # 어깨가 좁고 가슴이 있는 판
    d = ell(p, V(0, -0.86, -0.02), V(0.40, 0.40, 0.30))
    m = mirror(p)
    return smin(d, ell(m, V(0.13, -0.62, 0.16), V(0.13, 0.11, 0.11)), 0.06)

# 머리카락 — 남성 판 여섯 중 셋 + 여성 판 둘
def h_long_braid(p):   # 정수리에서 묶어 등까지 땋아 내린 한 가닥(+ 이마 위 덮개)
    capd = O.cap(p, 0.030, -0.03, 0.04)
    pts = [V(0, 0.28, -0.20), V(0, 0.18, -0.34), V(0, 0.04, -0.40), V(0, -0.10, -0.43), V(0, -0.24, -0.44), V(0, -0.38, -0.44), V(0, -0.52, -0.43)]
    return smin(capd, O.chain(p, pts, 0.050, 0.034), 0.03), np.minimum(O.cuff(p, V(0, -0.30, -0.44), V(0, -0.33, -0.44), 0.040),
                                                                     O.cuff(p, V(0, -0.52, -0.43), V(0, -0.55, -0.43), 0.036))
def h_twin_tails(p):   # 덮개 + 양 옆 낮게 묶은 두 꼬리
    m = mirror(p); capd = O.cap(p, 0.030, -0.03, 0.04)
    knot = sph(m, V(0.24, 0.02, -0.18), 0.055)
    tail = seg(m, V(0.26, -0.02, -0.19), V(0.30, -0.40, -0.12), 0.052, 0.022)
    return smin(smin(capd, knot, 0.03), tail, 0.03), O.cuff(m, V(0.255, -0.02, -0.19), V(0.262, -0.05, -0.185), 0.056)
def h_bun(p):   # 덮개 + 뒤통수에 틀어 올린 쪽 + 뼈 비녀
    capd = O.cap(p, 0.030, -0.03, 0.04)
    bun = ell(p, V(0, 0.22, -0.40), V(0.11, 0.10, 0.09))
    pin = seg(p, V(-0.17, 0.26, -0.41), V(0.17, 0.18, -0.42), 0.012, 0.008)
    return smin(capd, bun, 0.04), pin
import hair_f
HAIRS = hair_f.HAIRS
h_long_braid = hair_f.h_long_braid

def earring(p):   # 귓바퀴 아래 고리 — 뼈
    m = mirror(p); c = V(0.33, -0.04, -0.03)
    q = m - c; a = np.sqrt(q[..., 1] ** 2 + q[..., 2] ** 2) - 0.035
    return np.sqrt(a * a + q[..., 0] ** 2) - 0.009

SKIN = O.SKIN; LIP = (0.30, 0.36, 0.22); HAIR = O.HAIR; BONE = O.BONE; SHIRT = (0.40, 0.25, 0.20)
def pieces(hair, tat=None, skin=SKIN, hc=HAIR, ring=True):
    extra = [(O.tattoo(tat[0]), tat[1])] if tat else []
    P = extra + [(head_f, skin), (O.mouth, O.RED), (O.teeth, BONE), (tusks_f, BONE), (O.eyes, O.DARK), (O.glint, O.WHITE),
                 (lash, O.DARK), (lips_f, tuple(c * 0.72 for c in skin)), (body_f, SHIRT),
                 (lambda p: hair(p)[0], hc), (lambda p: hair(p)[1], BONE)]
    if ring: P.append((earring, BONE))
    return P

if __name__ == '__main__':
    out = sys.argv[1]; S = 260
    def sheet(rows, labels, name):
        im = Image.new('RGB', (S * len(rows[0]), S * len(rows)), (40, 40, 44))
        for r, row in enumerate(rows):
            for c, t in enumerate(row): im.paste(t, (c * S, r * S))
        d = ImageDraw.Draw(im)
        for c, l in enumerate(labels): d.text((c * S + 6, 4), l, fill=(230, 230, 230))
        im.save(out + name); print('saved', out + name)
    # 1) 남성 대 여성 — 대머리로 얼굴만
    male = O.pieces(O.h_bald, O.b_none)
    fem = pieces(O.h_bald, ring=False)
    sheet([[render(male, a, S, S, -0.05, 0.95) for a in (0, 35, 90)] + [render(fem, a, S, S, -0.05, 0.95) for a in (0, 35, 90)]],
          ['M front', 'M 3/4', 'M side', 'F front', 'F 3/4', 'F side'], '_compare.png')
    # 2) 머리 여섯 — 앞 3/4 · 뒤 3/4
    sheet([[render(pieces(h), 35, S, S, -0.12, 1.30) for _, h in HAIRS],
           [render(pieces(h), 150, S, S, -0.12, 1.30) for _, h in HAIRS]], [n for n, _ in HAIRS], '_hair.png')
    # 3) 부족 넷 살색
    tribes = [('Common', SKIN), ('Sand', (0.66, 0.54, 0.35)), ('Frost', (0.60, 0.35, 0.27)), ('Flame', (0.65, 0.64, 0.58))]
    sheet([[render(pieces(h_long_braid, skin=c), 25, S, S, -0.12, 1.30) for _, c in tribes]], [n for n, _ in tribes], '_tribes.png')
