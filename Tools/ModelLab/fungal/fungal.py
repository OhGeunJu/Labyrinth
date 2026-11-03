"""감염된 사람(기생림) — 사람 치비 몸(`dwarf/body.py` `dims(MALE)` = C# `ChibiPawnCreator.Proto`)과
민머리 치비 머리(`dwarf/head_base.head`)를 그대로 쓰고 감염 흔적만 얹는다(사용자 11-03: 따로 빚지 말고 치비 몸에서).
얼굴 셋 — 버섯이 눈을 덮고 입만 드러난다:
  A 가면  — 두 눈 위에 부채가 나비 날개처럼
  B 터짐  — 눈구멍 · 이마에서 겹겹이 위 · 밖으로
  C 한쪽  — 한 눈만 무더기, 다른 눈은 탁하게 빛난다
몸 — 왼 어깨 · 등 · 오른 팔뚝에서 터진 부채 · 찢어진 셔츠 사이로 가슴 주름살.
단위 m · 발이 원점 · +z 앞. 실행: python fungal.py renders/infected_v6.png"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.join(_here, '..', 'common')); _sys.path.insert(0, _os.path.join(_here, '..', 'dwarf'))
import sys
import numpy as np
from PIL import Image
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg
import head_base as hb
import body as bd

D = bd.dims(bd.MALE)
HY = D['top'] + bd.HeadGap + hb.H * 0.5          # 머리 중심
HO = V(0, HY, bd.HeadZ)
def at(f): return lambda p: f(p - HO)             # 머리 좌표(머리 중심 원점)로

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def union(ds):
    out = ds[0]
    for d in ds[1:]: out = np.minimum(out, d)
    return out
def bounded(f, p, c, r):
    """c 둘레 r 밖은 경계까지의 거리만 — 비싼 조각을 그 근처에서만 돈다."""
    b = length(p - c) - r
    m = b < 0.03
    if not m.any(): return b
    out = b.copy(); out[m] = f(p[m]); return out

def frill(p, c, n, R, th=0.009, k=4, amp=0.02, cup=0.045, sq=1.0, gill=0.0, lobe=0.15):
    """부채 버섯 한 장 — c 중심, n 펼친 면의 법선, R 반지름. 큰 물결 k개 · gill이면 결."""
    n = nrm(n); up = np.array([0, 1, 0], np.float32)
    if abs(n[1]) > 0.95: up = np.array([1, 0, 0], np.float32)
    u = nrm(np.cross(up, n)); v = np.cross(n, u)
    q = p - c
    a = (q * u).sum(-1); b = (q * v).sum(-1) / sq; w = (q * n).sum(-1)
    r = np.sqrt(a * a + b * b); ang = np.arctan2(b, a); rr = np.clip(r / R, 0, 1.3)
    w2 = w - amp * np.sin(k * ang) * rr ** 1.5 + cup * rr ** 2
    w2 = w2 - gill * np.abs(np.sin(ang * 26)) * rr
    Re = R * (1 + lobe * np.sin(k * ang + 0.6))
    d = smax(r - Re, np.abs(w2) - th * (1.3 - 0.7 * rr), 0.008)
    return d * 0.6

# ── 머리(머리 좌표) ──
ZE = hb.front(hb.EYE_T); ZM = hb.front(hb.MOUTH_T)
MC = V(0, hb.MY - 0.005, ZM)                      # 입 중심
MR = V(0.062, 0.085, 0.09)
def head(h): return smax(hb.head(h), -ell(h, MC + V(0, 0, 0.02), MR), 0.012)   # 아래위로 벌어진 입
def mouth(h): return ell(h, MC - V(0, 0, 0.035), MR * V(1, 1, 0.8))
def teeth(h):   # 윗니는 아래로 · 아랫니는 위로 · 길이가 들쭉날쭉
    d = np.full(h.shape[:-1], 9.0, np.float32)
    for i, x in enumerate((-0.04, -0.02, 0.0, 0.02, 0.04)):
        ry = MR[1] * np.sqrt(max(0.0, 1 - (x / MR[0]) ** 2)) - 0.008
        zz = ZM + 0.035 - 2.0 * x * x
        l1 = (0.032, 0.022, 0.038, 0.020, 0.030)[i]; l2 = (0.022, 0.032, 0.018, 0.034, 0.025)[i]
        d = np.minimum(d, seg(h, V(x, MC[1] + ry, zz), V(x * 0.9, MC[1] + ry - l1, zz - 0.006), 0.009, 0.002))
        d = np.minimum(d, seg(h, V(x, MC[1] - ry, zz), V(x * 0.9, MC[1] - ry + l2, zz - 0.006), 0.009, 0.002))
    return d
def eye(h, s): return ell(h, V(s * hb.EX, hb.EY, ZE - 0.012), V(hb.ER, hb.ER * 1.1, 0.02))   # 사람 먹 눈 그대로
def e_at(s): return V(s * hb.EX, hb.EY, ZE)

def eyedisc(h, s):   # 눈 자리 — 결 진 둥근 원판
    return frill(h, e_at(s) + V(s * 0.01, 0.0, 0.045), (s * 0.15, 0.05, 1), 0.055, th=0.007, k=6, amp=0.004, cup=0.02, gill=0.003, lobe=0.05)

def face_A(h):
    a, b, inner = [], [], []
    for s in (1, -1):
        e = e_at(s)
        a.append(frill(h, e + V(s * 0.04, 0.0, 0.025), (s * 0.30, 0.0, 1), 0.12, sq=1.15))
        b.append(frill(h, e + V(s * 0.10, 0.12, -0.01), (s * 0.45, 0.5, 0.8), 0.115, k=3))
        inner.append(eyedisc(h, s))
    return union(a), union(b), union(inner)
def face_B(h):
    a, b, inner = [], [], []
    for s in (1, -1):
        e = e_at(s)
        a += [frill(h, e + V(s * 0.04, 0.0, 0.035), (s * 0.5, 0.0, 1), 0.10),
              frill(h, e + V(s * 0.12, 0.20, -0.05), (s * 0.8, 0.8, 0.3), 0.12, k=3)]
        b += [frill(h, e + V(s * 0.10, 0.08, 0.0), (s * 0.9, 0.3, 0.6), 0.11),
              frill(h, e + V(s * 0.02, -0.06, 0.04), (s * 0.3, -0.7, 1), 0.065)]
        inner.append(eyedisc(h, s))
    a.append(frill(h, V(0, 0.23, 0.25), (0, 0.8, 1), 0.13, k=3))
    b.append(frill(h, V(0, 0.10, 0.31), (0, 0.3, 1), 0.10))
    return union(a), union(b), union(inner)
def face_C(h):
    e = e_at(1); a, b, inner = [], [], []
    a += [frill(h, e + V(0.035, 0.0, 0.03), (0.4, 0.0, 1), 0.11),
          frill(h, e + V(0.11, 0.17, -0.04), (0.8, 0.8, 0.4), 0.12, k=3)]
    b += [frill(h, e + V(0.11, 0.06, -0.01), (1, 0.3, 0.5), 0.10),
          frill(h, e + V(0.015, -0.07, 0.035), (0.3, -0.7, 1), 0.07),
          frill(h, e + V(-0.05, 0.13, 0.02), (-0.3, 0.8, 0.8), 0.08)]
    inner.append(eyedisc(h, 1))
    return union(a), union(b), union(inner)
FACES = {'A': face_A, 'B': face_B, 'C': face_C}

# ── 몸 ──
def torso(p):
    y = p[..., 1] - D['lift']; t = y / D['torsoH']
    r = bd.torso_r(D['half'], t, D['torsoH'], D['topR'])
    q = np.sqrt(p[..., 0] ** 2 + (p[..., 2] / D['depth']) ** 2)
    return np.maximum((q - r) * D['depth'], np.maximum(-y, y - D['torsoH']))
TEAR = V(0.0, D['top'] - 0.30, 0.25)              # 셔츠가 찢어진 자리(가슴 가운데 · 세로로 긴 구멍)
def tear(p): return ell(p, TEAR, V(0.10, 0.20, 0.25)) + 0.012 * np.sin(p[..., 1] * 90)
def shirt(p): return smax(np.maximum(torso(p), D['hem'] - p[..., 1]), -tear(p), 0.005)
def chest(p):   # 찢어진 틈으로 보이는 결 — 살갗 아래가 주름살이 됐다
    ridge = 0.5 + 0.5 * np.sin(p[..., 0] * 160)
    d = torso(p) + 0.006 - 0.012 * ridge
    return smax(d, tear(p) - 0.01, 0.005) * 0.7
def pants(p): return np.maximum(torso(p), p[..., 1] - D['hem'])
def mir(p): return np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
def legs(p):
    y0 = max(D['footY'], D['legR'] + 0.01)
    return capsule(mir(p), V(D['legX'], y0, 0), V(D['legX'], D['hipY'], 0), D['legR'])
def shoes(p):
    sw, sh, sl = D['shoe']
    return rbox(mir(p), V(D['legX'], sh * 0.5, bd.PShoeFwd * bd.HH), V(sw * 0.5, sh * 0.5, sl * 0.5), min(sw, sh) * 0.4)
def arms(p): return capsule(mir(p), V(D['armX'], D['handY'], 0), V(D['armX'], D['shY'], 0), D['armR'])
def hands(p): return sph(mir(p), V(D['armX'], D['handY'] - D['hand'] * 0.25, 0), D['hand'] * 0.5)

T = D['top']; AX = float(D['armX'])
def growth(p):   # 몸에서 터진 부채 — 왼 어깨(+x) · 등 · 오른 팔뚝(-x)
    a = [frill(p, V(0.20, T + 0.02, 0.0), (0.5, 1, 0.1), 0.11, k=3),
         frill(p, V(0.27, T - 0.06, -0.06), (1, 0.4, -0.3), 0.09),
         frill(p, V(-0.10, T - 0.22, -0.20), (-0.2, 0.3, -1), 0.10),
         frill(p, V(-AX - 0.05, D['handY'] + 0.17, 0.02), (-1, 0.2, 0.3), 0.09)]
    b = [frill(p, V(0.13, T + 0.03, -0.08), (0.2, 1, -0.5), 0.085),
         frill(p, V(0.05, T - 0.36, -0.20), (0.3, -0.2, -1), 0.08),
         frill(p, V(-AX - 0.04, D['handY'] + 0.07, 0.05), (-0.8, -0.3, 0.6), 0.07)]
    return union(a), union(b)

SKIN = (0.70, 0.71, 0.60); SHIRT = (0.36, 0.31, 0.24); PANTS = (0.24, 0.24, 0.30); SHOE = (0.22, 0.16, 0.12)
FRILL = (0.96, 0.60, 0.48); FRILL_B = (0.98, 0.72, 0.58); FRILL_IN = (0.80, 0.48, 0.72); GILL = (0.82, 0.74, 0.56)
CLOUD = (0.80, 0.92, 0.70); MOUTH = (0.30, 0.06, 0.08); TOOTH = (0.92, 0.88, 0.74)

def pieces(face):
    f = FACES[face]; c = {}
    hc = V(0, 0.05, 0.12)
    def fp(i):
        def g(p):
            if i == 0:
                h = p - HO
                c['v'] = [bounded(lambda q, k=k: f(q)[k], h, hc, 0.36) for k in range(3)]
            return c['v'][i]
        return g
    def gp(i):
        def g(p):
            if i == 0:
                c['g'] = [bounded(lambda q, k=k: growth(q)[k], p, V(0, T - 0.15, 0), 0.50) for k in range(2)]
            return c['g'][i]
        return g
    P = [(at(head), SKIN), (at(mouth), MOUTH), (at(teeth), TOOTH),
         (fp(0), FRILL), (fp(1), FRILL_B), (fp(2), FRILL_IN),
         (shirt, SHIRT), (chest, GILL), (pants, PANTS), (legs, PANTS), (shoes, SHOE), (arms, SHIRT), (hands, SKIN),
         (gp(0), FRILL), (gp(1), FRILL_B)]
    if face == 'C':   # 덮이지 않은 눈 — 탁하게 빛난다
        P.append((at(lambda h: eye(h, -1)), CLOUD))
    return P

def job(a):
    face, yaw, w, hgt, cy, span = a
    return np.asarray(hb.render(pieces(face), yaw, w, hgt, cy, span))

if __name__ == '__main__':
    from multiprocessing import Pool
    out = sys.argv[1]
    full = [('A', 0), ('A', 40), ('A', 90), ('A', 180), ('B', 0), ('C', 0)]
    face = [('A', 0), ('A', 35), ('B', 0), ('B', 35), ('C', 0), ('C', 35)]
    jobs = [(k, y, 260, 460, 0.95, 2.05) for k, y in full] + [(k, y, 260, 260, HY - 0.02, 0.85) for k, y in face]
    with Pool(len(jobs)) as pool: ims = pool.map(job, jobs)
    sheet = Image.new('RGB', (260 * 6, 460 + 260), (40, 40, 44))
    for i, im in enumerate(ims[:6]): sheet.paste(Image.fromarray(im), (i * 260, 0))
    for i, im in enumerate(ims[6:]): sheet.paste(Image.fromarray(im), (i * 260, 460))
    sheet.save(out); print('saved', out)
