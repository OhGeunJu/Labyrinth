"""감염된 사람(기생림) — 사람 치비 몸(`dwarf/body.py` `dims(MALE)` = C# `ChibiPawnCreator.Proto`)과
민머리 치비 머리(`dwarf/head_base.head`)를 그대로 쓰고 감염 흔적만 얹는다(사용자 11-03: 따로 빚지 말고 치비 몸에서).
얼굴 셋 — 버섯이 눈을 덮고 입만 드러난다:
  A 가면  — 두 눈 위에 부채가 나비 날개처럼
  B 터짐  — 눈구멍 · 이마에서 겹겹이 위 · 밖으로
  C 한쪽  — 한 눈만 무더기, 다른 눈은 탁하게 빛난다
몸 — 몸 자체가 버섯이 됐다(11-04): 옷은 삭아 허리 넝마만 · 몸통은 세로 결 살 + 앞가슴은 목 밑에서 퍼지는 주름살 부채 ·
목둘레 · 팔꿈치 · 손목 · 무릎 · 발목에 겹겹이 처진 해진 고리 · 발은 뿌리 발가락 · 어깨 · 등에 터진 부채.
색은 죽은 버섯 — 잿빛 · 썩은 갈색 · 멍든 자주.
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
def head(h): return smax(hb.head(h), -ell(h, MC + V(0, 0, 0.02), MR), 0.025)   # 아래위로 벌어진 입
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

HC = V(0, 0.02, 0.0)
def surf(d):   # 머리 중심에서 d 방향으로 나가 머리 겉에 닿는 점
    d = nrm(d); t = 0.05
    while float(hb.head(np.asarray(HC + d * t, np.float32)[None])[0]) < 0: t += 0.002
    return V(*(HC + d * t))
SPREAD = [(surf((0.25, 0.35, 0.9)), V(0.12, 0.09, 0.10)),    # 왼 눈 위 이마
          (surf((0.6, 0.6, 0.4)), V(0.10, 0.12, 0.12)),      # 왼 관자 · 정수리 쪽
          (surf((0.15, 1.0, 0.0)), V(0.11, 0.07, 0.12)),     # 정수리
          (surf((-0.45, 0.1, 0.9)), V(0.06, 0.05, 0.05)),    # 오른 눈 옆 작은 얼룩
          (surf((0.7, -0.4, 0.55)), V(0.05, 0.09, 0.06)),    # 왼 뺨 아래로 흘러내림
          ]
SPREAD_BACK = [(surf((-0.35, 0.45, -0.85)), V(0.16, 0.14, 0.12)),  # 뒤통수 오른 위 — 크게
               (surf((-0.9, 0.2, -0.3)), V(0.10, 0.12, 0.14)),    # 오른 옆머리
               (surf((0.4, 0.1, -0.9)), V(0.09, 0.10, 0.08)),     # 뒤통수 왼 아래
               (surf((0.0, -0.5, -0.85)), V(0.12, 0.07, 0.10)),   # 뒷목 쪽으로 흘러내림
               (surf((0.55, 0.5, -0.65)), V(0.13, 0.12, 0.11)),   # 뒤통수 왼 위
               (surf((0.9, 0.1, -0.35)), V(0.09, 0.11, 0.12))]    # 왼 옆머리
# 뒤통수 말굽 무더기 후보 — (방향, 크기) 셋씩 위로 쌓는다
HEAD_STACKS = [[((-0.30, -0.05, -0.95), 0.20), ((-0.22, 0.22, -0.95), 0.16), ((-0.15, 0.45, -0.88), 0.12)],   # 뒤 오른
               [((0.35, -0.10, -0.93), 0.18), ((0.30, 0.15, -0.94), 0.14), ((0.22, 0.38, -0.90), 0.10)],      # 뒤 왼
               [((0.0, 0.55, -0.83), 0.15), ((0.05, 0.75, -0.6), 0.11)],                                    # 정수리 뒤
               [((-0.80, -0.15, -0.55), 0.15), ((-0.75, 0.10, -0.62), 0.11)],                               # 오른 귀 뒤
               [((0.15, -0.35, -0.92), 0.13), ((-0.12, -0.30, -0.94), 0.10)]]                               # 뒷목 위
HEAD = {'back': [0, 2, 3], 'stacks': [0]}
def rot(h):   # 머리에 번진 균사 — 살 위로 얇게 · 결진 얼룩
    d = hb.head(h) - 0.011 - 0.003 * np.abs(np.sin(h[..., 0] * 70 + h[..., 1] * 40))   # 1cm 넘게 띄운다(C# — 머리 메시와 겹쳐 비쳤다)
    blob = union([ell(h, c, r) for c, r in SPREAD + [SPREAD_BACK[i] for i in HEAD['back']]])
    blob = blob + 0.012 * np.sin(h[..., 0] * 60) * np.sin(h[..., 1] * 55)        # 들쭉날쭉한 테
    return smax(d, blob, 0.01)
NUBS = [(surf(d), r) for d, r in (((0.35, 0.55, 0.75), 0.022), ((0.6, 0.35, 0.7), 0.017), ((0.3, 0.95, 0.15), 0.026),
                                  ((0.6, 0.75, 0.05), 0.019), ((0.35, 0.9, -0.25), 0.015), ((-0.45, 0.15, 0.88), 0.014),
                                  ((0.7, -0.3, 0.6), 0.013), ((0.5, 0.6, 0.45), 0.012),
                                  ((-0.2, 0.55, -0.8), 0.020), ((-0.55, 0.3, -0.75), 0.016), ((-0.85, 0.35, -0.2), 0.018),
                                  ((0.35, 0.3, -0.88), 0.014), ((-0.1, 0.0, -1.0), 0.015))]
def nubs(h):  # 얼룩 위로 돋은 작은 혹 버섯 — 반쯤 묻힌다 · 뒤통수엔 말굽 버섯 무더기
    out = [sph(h, c, r) for c, r in NUBS]
    for d, r in (x for i in HEAD['stacks'] for x in HEAD_STACKS[i]):
        c = surf(d); a = float(np.arctan2(c[2], c[0]))
        out.append(bracket(h, c - V(np.cos(a), 0, np.sin(a)) * r * 0.15, a, r, wide=0.72))
    return union(out)
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
def ring(p, cx, cz, y0, rin, R, th=0.012, droop=0.7, k=7, rag=0.18, amp=0.035, face=0.0, arc=np.pi, ph=0.0):
    """팔다리 · 목에 번진 주름 버섯 — 세로축(cx, cz) 둘레, face 방향(라디안)을 가운데로 arc만큼만 덮는다.
    테는 둥근 꽃잎(톱니 X) · 위아래로 물결치는 주름 · 바깥으로 처진다."""
    x = p[..., 0] - cx; z = p[..., 2] - cz
    r = np.sqrt(x * x + z * z); ang = np.arctan2(z, x)
    da = np.angle(np.exp(1j * (ang - face)))                       # face에서 벗어난 각
    span = np.clip(1 - (np.abs(da) / arc) ** 2, 0, 1)              # 가장자리로 갈수록 줄어든다
    f = np.clip((r - rin) / (R - rin), 0, 1.3)
    yp = y0 - droop * (r - rin).clip(0) - amp * np.sin(k * ang + ph) * f ** 1.3
    lob = np.abs(np.cos(0.5 * k * ang + ph)) ** 0.5                # 둥근 꽃잎
    Ro = rin + (R - rin) * span * (1 - rag + rag * lob) * (1 + 0.08 * np.sin(2.7 * ang + ph))
    d = smax(np.abs(p[..., 1] - yp) - th * (1.25 - 0.45 * f), r - Ro, 0.010)
    return d * 0.55
NECK = V(0.0, D['top'] + 0.02, 0.02)        # 주름살이 모이는 자리(목 밑)
WOUNDS = [((0.07, D['top'] - 0.36), (0.085, 0.14), 1),    # 왼 배(앞)
          ((-0.09, D['top'] - 0.24), (0.075, 0.12), -1)]  # 오른 날개뼈(뒤)
def wound(p, w):   # 터진 틈의 테두리까지의 비율(1 = 가장자리) — 들쭉날쭉 · 그 면(앞/뒤)에만
    (cx, cy), (rx, ry), side = w
    x = (p[..., 0] - cx) / rx; y = (p[..., 1] - cy) / ry
    a = np.arctan2(y, x)
    r = np.sqrt(x * x + y * y) * (1 + 0.12 * np.sin(5 * a + 1.0 + side) + 0.06 * np.sin(11 * a))
    return np.where(p[..., 2] * side > 0.02, r, 9.0)
def flesh(p):   # 옷은 삭아 없어지고 몸통이 버섯 살이 됐다 — 결이 세로로 선다 · 터진 틈 둘레는 입술처럼 젖혀진다
    fib = np.abs(np.sin(np.arctan2(p[..., 0], p[..., 2]) * 22 + p[..., 1] * 9))
    lip = sum(np.exp(-((wound(p, w) - 1.05) / 0.16) ** 2) for w in WOUNDS)
    return torso(p) - 0.004 * fib - 0.018 * lip
def chest(p):   # 틈 안 — 안쪽 한 점에서 퍼지는 주름살(갓 밑면)이 살짝 비어져 나온다
    out = []
    for w in WOUNDS:
        (cx, cy), _, _ = w
        a = np.arctan2(p[..., 1] - cy - 0.05, p[..., 0] - cx)
        d = torso(p) - 0.006 - 0.010 * np.abs(np.sin(a * 13)) ** 0.6
        out.append(smax(d, (wound(p, w) - 0.95) * 0.08, 0.004) * 0.7)
    return union(out)
def pants(p):   # 삭은 바지 — 허리에 걸린 넝마
    bot = D['hem'] - 0.10 + 0.03 * np.sin(p[..., 0] * 55 + 1.0) + 0.02 * np.sin(p[..., 2] * 70)   # 해진 아랫단
    return np.maximum(torso(p) - 0.006, np.maximum(p[..., 1] - D['hem'], bot - p[..., 1]))
def mir(p): return np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
def legs(p):
    y0 = max(D['footY'], D['legR'] + 0.01)
    return capsule(mir(p), V(D['legX'], y0, 0), V(D['legX'], D['hipY'], 0), D['legR'])
def arms(p): return capsule(mir(p), V(D['armX'], D['handY'], 0), V(D['armX'], D['shY'], 0), D['armR'])
def hands(p): return sph(mir(p), V(D['armX'], D['handY'] - D['hand'] * 0.25, 0), D['hand'] * 0.5)

T = D['top']; AX = float(D['armX']); LX = float(D['legX'])
def growth(p):   # (11-04) 얇은 부채 조각은 걷었다 — 몸 버섯은 전부 말굽 버섯(rings)
    far = np.full(p.shape[:-1], 9.0, np.float32)
    return far, far
def bracket(p, c, ang, r, wide=1.0):
    """말굽 버섯 하나 — 두툼한 반원 덩어리 · 윗면은 둥글고 테 무늬 · 바깥 끝은 아래로 살짝 처진다."""
    u = np.array([np.cos(ang), 0, np.sin(ang)], np.float32); w = np.array([-np.sin(ang), 0, np.cos(ang)], np.float32)
    q = p - c
    a_ = (q * u).sum(-1); b_ = q[..., 1]; c_ = (q * w).sum(-1)
    c_ = c_ / wide
    rad = np.sqrt(a_ * a_ + c_ * c_)
    b_ = b_ + 0.22 * r * np.clip(a_ / r, 0, 1.2) ** 2                 # 끝이 처진다
    th = np.arctan2(c_, a_)
    rr = r * (1 + 0.07 * np.sin(5 * th + ang * 3))                    # 물결진 테
    d = (np.sqrt((rad / rr) ** 2 + (b_ / (0.30 * r)) ** 2) - 1) * 0.30 * r
    return d
def shelf(p, cx, cz, y, ang, R, n=3, step=0.05, shrink=0.75, base=0.07):
    """말굽 버섯 무더기 — 죽은 나무에 붙듯 몸 한쪽(ang 방향)에 위로 겹겹이. 아래 것이 크다."""
    out = []
    for i in range(n):
        r = R * shrink ** i; a = ang + 0.35 * i * (-1) ** i
        o = base + r * 0.15
        out.append(bracket(p, V(cx + np.cos(a) * o, y + i * step, cz + np.sin(a) * o), a, r))
    return union(out)
BK = -np.pi / 2; FR = np.pi / 2                       # 뒤 · 앞 방향(z)
# 말굽 버섯이 붙을 수 있는 자리 — (이름, 그리기). 개체마다 시드로 몇 곳만 고른다(11-04: 다 같은 자리면 복제한 것 같다)
SITES = [
    ('허리 뒤',      lambda p: shelf(p, 0.04, 0.0, T - 0.50, BK + 0.2, 0.15, n=3, step=0.05, base=0.15)),
    ('왼 날개뼈',    lambda p: shelf(p, 0.13, 0.0, T - 0.18, BK + 0.6, 0.11, n=2, step=0.045, base=0.15)),
    ('오른 날개뼈',  lambda p: shelf(p, -0.13, 0.0, T - 0.16, BK - 0.6, 0.11, n=2, step=0.045, base=0.15)),
    ('목 왼',        lambda p: shelf(p, 0.04, 0.0, T - 0.06, 0.4, 0.15, n=3, step=0.05, base=0.18)),
    ('목 오른',      lambda p: shelf(p, -0.04, 0.0, T - 0.06, np.pi - 0.4, 0.14, n=3, step=0.05, base=0.18)),
    ('오른 가슴',    lambda p: shelf(p, -0.10, 0.0, T - 0.24, FR + 0.35, 0.08, n=2, step=0.035, base=0.16)),
    ('왼 가슴',      lambda p: shelf(p, 0.10, 0.0, T - 0.20, FR - 0.35, 0.08, n=2, step=0.035, base=0.16)),
    ('아랫배',       lambda p: shelf(p, -0.02, 0.0, D['hem'] + 0.06, FR - 0.25, 0.07, n=2, step=0.03, base=0.17)),
    ('왼 옆구리',    lambda p: shelf(p, 0.10, 0.0, T - 0.40, 0.1, 0.10, n=2, step=0.04, base=0.18)),
    ('오른 옆구리',  lambda p: shelf(p, -0.10, 0.0, T - 0.34, np.pi - 0.1, 0.10, n=2, step=0.04, base=0.18)),
    ('왼 팔뚝',      lambda p: shelf(p, AX, 0, D['handY'] + 0.10, 0.1, 0.12, n=3, base=0.06)),
    ('오른 팔뚝',    lambda p: shelf(p, -AX, 0, D['handY'] + 0.12, np.pi - 0.2, 0.11, n=3, base=0.06)),
    ('오른 손목',    lambda p: shelf(p, -AX, 0, D['handY'] + 0.04, 3.0, 0.10, n=2, base=0.06)),
    ('왼 손목',      lambda p: shelf(p, AX, 0, D['handY'] + 0.03, 0.2, 0.09, n=2, base=0.06)),
    ('오른 무릎',    lambda p: shelf(p, -LX, 0, 0.28, 1.9, 0.12, n=2, base=0.09)),
    ('왼 무릎',      lambda p: shelf(p, LX, 0, 0.30, 1.3, 0.11, n=2, base=0.09)),
    ('왼 정강이 뒤', lambda p: shelf(p, LX, 0, 0.14, -0.5, 0.12, n=2, step=0.045, base=0.09)),
    ('오른 종아리',  lambda p: shelf(p, -LX, 0, 0.16, -2.4, 0.11, n=2, step=0.045, base=0.09)),
]
WOUND_SITES = [((0.07, D['top'] - 0.36), (0.085, 0.14), 1),    # 왼 배
               ((-0.08, D['top'] - 0.22), (0.075, 0.12), 1),   # 오른 가슴
               ((-0.09, D['top'] - 0.24), (0.075, 0.12), -1),  # 오른 날개뼈
               ((0.08, D['top'] - 0.42), (0.08, 0.11), -1),    # 왼 허리 뒤
               ((0.10, D['top'] - 0.15), (0.06, 0.10), -1)]    # 왼 날개뼈
CFG = {'sites': [0, 1, 5, 7, 10, 12, 14, 16], 'mirror': False, 'nubs': list(range(6)), 'special': None, 'sside': 1}
def configure(seed):
    """개체 하나 — 시드에서 자리 · 틈 · 머리 번짐 방향을 굴린다(C#에서는 PawnSpec 시드 꼬리)."""
    global WOUNDS
    rng = np.random.default_rng(seed)
    CFG['sites'] = sorted(rng.choice(len(SITES), int(rng.integers(7, 11)), replace=False).tolist())
    WOUNDS = [WOUND_SITES[i] for i in rng.choice(len(WOUND_SITES), int(rng.integers(1, 3)), replace=False)]
    CFG['mirror'] = bool(rng.integers(0, 2))
    CFG['nubs'] = sorted(rng.choice(6, int(rng.integers(2, 6)), replace=False).tolist())
    HEAD['back'] = rng.choice(len(SPREAD_BACK), int(rng.integers(2, 5)), replace=False).tolist()
    HEAD['stacks'] = rng.choice(len(HEAD_STACKS), int(rng.integers(1, 3)), replace=False).tolist()
    CFG['special'] = SPECIALS[int(rng.integers(0, len(SPECIALS)))]
    CFG['sside'] = 1 if rng.integers(0, 2) else -1
def rings(p):   # 고른 자리의 말굽 버섯 — 두 색을 번갈아
    a, b = [], []
    for j, i in enumerate(CFG['sites']): (a if j % 2 == 0 else b).append(SITES[i][1](p))
    far = np.full(p.shape[:-1], 9.0, np.float32)
    return (union(a) if a else far), (union(b) if b else far)
def bodynubs(p):   # 앞가슴 · 배에 돋은 작은 혹
    out = []
    NB = ((-0.04, T - 0.12, 0.020), (-0.13, T - 0.33, 0.016), (0.16, T - 0.18, 0.014), (0.13, T - 0.46, 0.017),
          (-0.06, T - 0.40, 0.012), (0.00, T - 0.18, 0.011))
    for x, y, r in (NB[i] for i in CFG['nubs']):
        t = (y - D['lift']) / D['torsoH']; z = D['depth'] * np.sqrt(max(0.0, float(bd.torso_r(D['half'], np.float32(t), D['torsoH'], D['topR'])) ** 2 - x * x))
        out.append(sph(p, V(x, y, z), r))
    return union(out)
def roots(p):   # 신 대신 뿌리 발가락 — 앞으로 셋, 땅을 움켜쥔다
    q = mir(p); d = np.full(p.shape[:-1], 9.0, np.float32)
    for dx, l in ((-0.05, 0.16), (0.0, 0.19), (0.05, 0.15)):
        b0 = V(LX + dx * 0.6, 0.07, 0.02); b1 = V(LX + dx, 0.015, l)
        d = np.minimum(d, seg(q, b0, b1, 0.035, 0.010))
    return np.minimum(d, ell(q, V(LX, 0.07, 0.0), V(0.08, 0.07, 0.09)))
def limbs(p):   # 팔 살 — 세로 결(다리는 바지 — 11-04, 사용자: *"바지 정도는 입혀주던지"*)
    fib = np.abs(np.sin(np.arctan2(p[..., 2], np.abs(p[..., 0]) - LX) * 9))
    return arms(p) - 0.004 * fib

# ── 대표 버섯 — 개체마다 하나(눈 버섯처럼 한눈에 들어오는 것) ──
SPECIALS = ['collar', 'sprout', 'puff', 'ink']
def collar(p):   # 목도리 — 목 뒤에서 머리를 감싸며 머리보다 크게 펼쳐진 부채(결이 진하다) · 몸을 뚫지 않는다
    c = HO + V(0, -0.12, -0.17)
    d = frill(p, c, (0, 0.45, 1), 0.66, th=0.013, k=7, amp=0.05, cup=-0.14, sq=0.80, gill=0.014, lobe=0.14)
    d = smax(d, -(torso(p) - 0.004), 0.01)                                          # 몸 안은 잘라 낸다
    d = smax(d, -(hb.head(p - HO) - 0.004), 0.01)                                   # 머리 안도
    return smax(d, (T - 0.04) - p[..., 1] + 0.02 * np.sin(p[..., 0] * 40), 0.01)   # 어깨선 아래로는 안 내려온다
def sprout(p):   # 큰 버섯 하나 — 어깨에서 머리 옆으로 굽어 솟은 대 + 처진 갓
    sx = CFG['sside']
    b0 = V(sx * 0.17, T - 0.04, -0.03); b1 = V(sx * 0.33, T + 0.25, -0.05); b2 = V(sx * 0.40, T + 0.62, -0.02)
    stalk = smin(seg(p, b0, b1, 0.065, 0.048), seg(p, b1, b2, 0.048, 0.036), 0.03)
    cc = b2 + V(sx * 0.03, 0.05, 0)
    cap = ell(p, cc, V(0.27, 0.15, 0.27))
    cap = smax(cap, -(p[..., 1] - (cc[1] - 0.03)), 0.012)                         # 밑면을 잘라 갓으로
    ang = np.arctan2(p[..., 2] - cc[2], p[..., 0] - cc[0])
    cap = cap - 0.016 * np.sin(ang * 6) * np.clip((length(p - cc) - 0.16) / 0.1, 0, 1)   # 처진 물결 테
    under = smax(ell(p, cc - V(0, 0.035, 0), V(0.25, 0.05, 0.25)), (p[..., 1] - (cc[1] - 0.025)), 0.005)
    under = under - 0.004 * np.abs(np.sin(ang * 40))                               # 갓 밑 주름
    spots = union([sph(p, cc + V(dx, 0.145 - 0.9 * (dx * dx + dz * dz), dz), r)
                   for dx, dz, r in ((0.0, 0.0, 0.03), (0.12, 0.06, 0.022), (-0.10, -0.10, 0.025), (0.06, -0.15, 0.018), (-0.15, 0.07, 0.02), (0.03, 0.14, 0.017))])
    return stalk, cap, under, spots
PUFFS = [((0.10, -0.12), 0.12), ((-0.07, -0.06), 0.085), ((0.19, 0.02), 0.08), ((0.02, -0.30), 0.075), ((-0.13, -0.24), 0.06), ((0.21, -0.22), 0.055)]
def puff(p):   # 포자 주머니 — 등 · 어깨 살을 밀고 솟은 울퉁불퉁한 말불버섯 덩이 · 둘은 찢어져 속이 검다
    sx = CFG['sside']; out = []; holes = []; inner = []
    for i, ((x, y), r) in enumerate(PUFFS):
        t = (T + y - D['lift']) / D['torsoH']
        rz = D['depth'] * float(bd.torso_r(D['half'], np.float32(t), D['torsoH'], D['topR']))
        cz = -np.sqrt(max(0.0, rz * rz - (x * 0.8) ** 2)) + r * 0.25                # 몸에 반쯤 묻힌다
        c = V(sx * x, T + y, cz) if y < 0 else V(sx * x, T + y - 0.02, -0.02)
        n = nrm(c - V(0, T - 0.15, 0.0))
        e = ell(p, c + n * r * 0.1, V(r, r * 0.92, r * 0.85)) - 0.006 * np.sin(p[..., 0] * 90) * np.sin(p[..., 1] * 85) * np.sin(p[..., 2] * 80)
        out.append(e)
        if i in (0, 3):   # 찢어진 입구 — 들쭉날쭉한 테
            h = c + n * r * 1.0
            hole = sph(p, h, r * 0.45) + 0.012 * np.sin(np.arctan2(p[..., 1] - h[1], p[..., 0] - h[0]) * 7)
            holes.append(hole); inner.append(sph(p, c + n * r * 0.55, r * 0.5))
    ball = smax(union(out), -union(holes), 0.008)
    return ball, union(inner)
def ink(p):   # 먹물 버섯 다발 — 정수리에서 솟은 길쭉한 종 · 끝이 검게 녹아 흘러내린다
    st, caps, drip = [], [], []
    for dx, dz, h, r in ((0.0, -0.02, 0.30, 0.05), (0.09, 0.04, 0.22, 0.042), (-0.08, 0.05, 0.25, 0.045),
                         (0.05, -0.12, 0.20, 0.04), (-0.10, -0.08, 0.17, 0.036), (0.14, -0.06, 0.14, 0.032)):
        base = surf((dx, 1.0, dz)); tip = base + V(dx * 0.8, h, dz * 0.8)
        st.append(seg(p - HO, base, tip, 0.016, 0.012))
        cc = tip - V(0, r * 1.2, 0)
        bell = ell(p - HO, cc + V(0, r * 0.6, 0), V(r, r * 2.0, r))
        bell = smax(bell, -(p[..., 1] - HO[1] - (cc[1] - r * 0.9)), 0.006)
        caps.append(bell)
        rim = ell(p - HO, cc - V(0, r * 0.75, 0), V(r * 1.05, r * 0.35, r * 1.05))
        rim = smax(rim, -(p[..., 1] - HO[1] - (cc[1] - r * 1.25)), 0.004)
        rim = rim + 0.012 * np.sin(np.arctan2(p[..., 2] - HO[2] - cc[2], p[..., 0] - cc[0]) * 7) - 0.0
        drip.append(rim)
    return union(st), union(caps), union(drip)

def mouthshroom(p):   # 입에서 비어져 나온 버섯 무리 — 벌린 입 안쪽에서 앞 · 아래로 쏟아진다
    h = p - HO; st, caps = [], []
    for dx, dy, dz, r in ((0.0, -0.04, 0.20, 0.065), (0.09, -0.13, 0.16, 0.052), (-0.08, -0.12, 0.17, 0.055),
                          (0.05, 0.05, 0.15, 0.045), (-0.04, -0.22, 0.11, 0.042), (0.13, -0.03, 0.10, 0.038), (-0.12, 0.02, 0.11, 0.036),
                          (0.02, -0.18, 0.19, 0.040)):
        b0 = MC + V(dx * 0.4, dy * 0.3, -0.02); b1 = MC + V(dx, dy, dz)
        st.append(seg(h, b0, b1, 0.018, 0.014))
        n = nrm(b1 - b0)
        cap = ell(h, b1 + n * r * 0.2, V(r, r * 0.65, r))
        caps.append(smax(cap, -((h - b1) * n).sum(-1), 0.004))
    return union(st), union(caps)
CORDY = [((0.14, -0.06, -0.12), (0.30, 0.42, -0.30), 0.40), ((-0.10, -0.02, -0.15), (-0.20, 0.55, -0.35), 0.48),
         ((0.02, -0.18, -0.19), (0.05, 0.40, -0.55), 0.38), ((0.20, 0.0, -0.02), (0.55, 0.38, -0.05), 0.30),
         ((-0.18, -0.08, -0.08), (-0.50, 0.30, -0.25), 0.32), ((0.08, -0.30, -0.19), (0.25, 0.10, -0.60), 0.28)]
def cordy(p):   # 동충하초 — 등 · 어깨에서 뿔처럼 휘어 솟은 검은 대 · 끝에 황토색 곤봉
    st, club = [], []
    for (x, y, z), dirv, L in CORDY:
        b0 = V(x, T + y, z); d = nrm(dirv); m = b0 + d * L * 0.5 + V(0, L * 0.12, 0)
        b2 = b0 + d * L + V(0, L * 0.30, 0)
        st.append(smin(seg(p, b0, m, 0.020, 0.015), seg(p, m, b2, 0.015, 0.010), 0.01))
        cd = nrm(b2 - m)
        club.append(seg(p, b2 - cd * 0.01, b2 + cd * 0.09, 0.026, 0.016))
    return union(st), union(club)
SPECIALS += ['mouth', 'cordy']

SKIN = (0.40, 0.42, 0.38); FLESH = (0.34, 0.37, 0.34); PANTS = (0.68, 0.59, 0.45); RAG = (0.16, 0.15, 0.17); ROOT = (0.20, 0.17, 0.15)
FRILL = (0.38, 0.24, 0.16); FRILL_B = (0.48, 0.38, 0.27); FRILL_IN = (0.27, 0.13, 0.22); GILL = (0.68, 0.59, 0.45)
ROT = (0.31, 0.23, 0.18)
CAP = (0.32, 0.18, 0.11); STALK = (0.58, 0.55, 0.46); CORD = (0.13, 0.11, 0.10); CLUB = (0.56, 0.40, 0.22); SPOT = (0.62, 0.58, 0.48); PUFF = (0.52, 0.49, 0.42); INK = (0.06, 0.05, 0.06); INKCAP = (0.40, 0.38, 0.35)
CLOUD = (0.66, 0.70, 0.52); MOUTH = (0.12, 0.03, 0.04); TOOTH = (0.70, 0.66, 0.54)

def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def fleshness(p):   # 1 = 회녹 살 · 0 = 황갈(다리 · 틈 색) — 다리에서 배 위로 혀처럼 번져 오르고 경계가 들쭉날쭉 · 몸통엔 찌그러진 황갈 얼룩 · 허벅지엔 살 얼룩(11-04)
    x, y, z = p[..., 0], p[..., 1], p[..., 2]
    n = 0.5 * np.sin(x * 23 + y * 7) + 0.3 * np.sin(z * 31 - y * 17 + 1.3) + 0.2 * np.sin((x + z) * 47 + y * 29)
    tongue = 0.10 * np.sin(np.arctan2(x, z) * 3 + 0.7)                     # 둘레를 따라 높낮이가 다른 혀 셋
    f = sstep(D['hem'] + 0.04, D['hem'] + 0.30, y + 0.10 * n - tongue)
    for (cx, cy, cz), (rx, ry, rz) in TAN_BLOTS:
        q = (p - V(cx, cy, cz)) / V(rx, ry, rz)
        f = f * sstep(0.55, 1.0, length(q) + 0.35 * n)
    for (cx, cy, cz), (rx, ry, rz) in FLESH_BLOTS:
        q = (p - V(cx, cy, cz)) / V(rx, ry, rz)
        f = np.maximum(f, 1 - sstep(0.55, 1.0, length(q) + 0.35 * n))
    return f
TAN_BLOTS = [(V(0.12, T - 0.14, 0.15), V(0.07, 0.13, 0.10)), (V(-0.14, T - 0.30, -0.15), V(0.10, 0.07, 0.10)),
             (V(-AX, D['handY'] + 0.06, 0.0), V(0.08, 0.10, 0.08))]
FLESH_BLOTS = [(V(LX, 0.40, 0.08), V(0.07, 0.10, 0.07)), (V(-LX, 0.22, -0.08), V(0.07, 0.09, 0.07))]
def body_col(p):
    f = fleshness(p)[:, None]
    return np.asarray(PANTS, np.float32) * (1 - f) + np.asarray(FLESH, np.float32) * f

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
                c['g'] = [bounded(lambda q, k=k: growth(q)[k], p, V(0, T - 0.15, 0), 0.50) for k in range(2)] +                          [bounded(lambda q, k=k: rings(q)[k], p, V(0, 0.6, 0), 0.75) for k in range(2)]
            return c['g'][i]
        return g
    fx = (lambda h: h * np.array([-1, 1, 1], np.float32)) if CFG['mirror'] else (lambda h: h)
    P = [(at(head), SKIN), (at(lambda h: rot(fx(h))), ROT), (at(lambda h: nubs(fx(h))), FRILL_B), (at(mouth), MOUTH), (at(teeth), TOOTH),
         (fp(0), FRILL), (fp(1), FRILL_B), (fp(2), FRILL_IN),
         (flesh, body_col), (chest, GILL), (pants, RAG), (legs, body_col), (limbs, body_col), (roots, ROOT), (hands, FLESH),
         (gp(0), FRILL), (gp(1), FRILL_B), (gp(2), FRILL), (gp(3), FRILL_B), (bodynubs, FRILL_B)]
    sp = CFG['special']
    def sv(fn, i, n, c0, r0):   # 대표 버섯 조각 — 한 번 돌고 나눠 쓴다
        def g(p):
            if i == 0: c['s'] = [bounded(lambda q, k=k: (fn(q) if n > 1 else (fn(q),))[k], p, c0, r0) for k in range(n)]
            return c['s'][i]
        return g
    if sp == 'collar':
        P.append((sv(collar, 0, 1, HO + V(0, -0.16, -0.1), 0.75), FRILL_IN))
    elif sp == 'sprout':
        P += [(sv(sprout, 0, 4, V(0, T + 0.35, 0), 0.80), FLESH), (sv(sprout, 1, 4, 0, 0), CAP),
              (sv(sprout, 2, 4, 0, 0), GILL), (sv(sprout, 3, 4, 0, 0), SPOT)]
    elif sp == 'puff':
        P += [(sv(puff, 0, 2, V(0, T - 0.12, -0.1), 0.42), PUFF), (sv(puff, 1, 2, 0, 0), INK)]
    elif sp == 'ink':
        P += [(sv(ink, 0, 3, HO + V(0, 0.3, 0), 0.45), FLESH), (sv(ink, 1, 3, 0, 0), INKCAP), (sv(ink, 2, 3, 0, 0), INK)]
    elif sp == 'mouth':
        P += [(sv(mouthshroom, 0, 2, HO + MC + V(0, -0.06, 0.12), 0.36), STALK), (sv(mouthshroom, 1, 2, 0, 0), INKCAP)]
    elif sp == 'cordy':
        P += [(sv(cordy, 0, 2, V(0, T + 0.2, -0.2), 0.85), CORD), (sv(cordy, 1, 2, 0, 0), CLUB)]
    if face == 'C':   # 덮이지 않은 눈 — 탁하게 빛난다
        P.append((at(lambda h: eye(h, -1)), CLOUD))
    return P

def job(a):
    face, seed, yaw, w, hgt, cy, span = a[:7]
    if seed is not None: configure(seed)
    if len(a) > 7: CFG['special'] = a[7]
    return np.asarray(hb.render(pieces(face), yaw, w, hgt, cy, span))

if __name__ == '__main__':
    # python fungal.py out.png [첫 시드] — 개체 여섯(대표 버섯 넷 + 굴린 둘)을 앞(위) · 뒤(아래)로
    from multiprocessing import Pool
    out = sys.argv[1]; s0 = int(sys.argv[2]) if len(sys.argv) > 2 else 1
    show = sys.argv[3].split(',') if len(sys.argv) > 3 else (SPECIALS + [None, None])[:6]
    ids = [('ABC'[i % 3], s0 + i, sp if sp not in (None, '-') else None) for i, sp in enumerate(show)]
    def row(yaw): return [(f, sd, yaw, 260, 520, 1.12, 2.5) + ((sp,) if sp else ()) for f, sd, sp in ids]
    jobs = row(25) + row(205)
    with Pool(len(jobs)) as pool: ims = pool.map(job, jobs)
    sheet = Image.new('RGB', (260 * len(ids), 1040), (40, 40, 44))
    for i, im in enumerate(ims): sheet.paste(Image.fromarray(im), ((i % len(ids)) * 260, (i // len(ids)) * 520))
    sheet.save(out); print('saved', out)
