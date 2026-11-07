"""사람 치비 — 회전체 머리 열두 벌(`ChibiHeadProfiles`) 위에 빚은 얼굴(코 · 먹 눈 · 반짝임 · 눈썹 · 입 · 둥근 귀)과 빚은 머리카락.
2026-11-07 사용자: *"원래 치비도 지금 다른 캐릭들처럼 외형을 좀 바꾸자"* → 얼굴 + 머리카락 · 머리 모양 열두 벌은 살리고 이목구비는 고르기 축.
단위 m · 원점은 머리 한복판 · +z 앞. 머리 표는 C# 표(`Assets/Editor/ChibiHeadProfiles.cs`)와 얼굴 자리 표(`5_Data/Chibi/Chibi_FaceLayout.asset`)를 그대로 읽는다.
실행: python human.py renders/human_v1.png [줄 고르기 0,2] [보기 각도 0,40,90]

**여기 `HAIRS`(새로 지은 껍데기 머리 열셋)는 쓰지 않는다** — 사용자: *"머리카락을 너무 못만든 거 같아"*(11-07). 머리카락은 `port.py`(확인받은 원본을 옮긴다).
이 파일은 머리 표 · 얼굴 · 귀 · 꽁지 도구(`tail` · `TAIL_R` · `bez` · `lock_r`)를 낸다."""
import os as _os, sys as _sys, re
_here = _os.path.dirname(_os.path.abspath(__file__))
for _p in (_os.path.join(_here, '..', 'common'), _os.path.join(_here, '..', 'dwarf')):
    if _p not in _sys.path: _sys.path.append(_p)
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, seg
from head_base import render
from dwarf import NONE, lock, ease, angle, strands, band

ROOT = _os.path.join(_here, '..', '..', '..')
TORSO_H, BODY_PX, DEPTH = 0.885, 87.5, 0.95          # C# `TorsoH` · `ChibiHeadProfiles.BodyPx` · `Depth`
EYE_K, EYE_ASPECT = 0.7, 1.35                        # `ChibiFaceLayout` 전체 손잡이(눈 크기 · 세로 비)
SLIM_H = 0.88                                        # 좁은 머리는 세로를 줄인다(11-07 사용자: *"좁은 머리들은 세로 길이를 좀 줄이면"*) — 눈 · 입 높이는 비율이라 같이 따라온다
DROPPED = ('Male_BroadSharp', 'Female_BroadSharp')   # 뺀 머리(11-07 사용자: *"BroadSharp들이 실제 얼굴로 하기에는 너무 만화느낌"*)
EYE_SPREAD = 0.82                                  # 눈 사이를 좁힌다(11-07 사용자: 다른 종족보다 멀다 — 표의 간격은 그림 판 눈의 것)

def _read_tables():
    src = open(_os.path.join(ROOT, 'Assets/Editor/ChibiHeadProfiles.cs'), encoding='utf-8').read()
    heads = {}
    for m in re.finditer(r'new Ref\("(\w+)", ([\d.]+)f, ([\d.]+)f, [\d.]+f, [\d.]+f, [\d.]+f,\s*new\[\] \{([^}]*)\},\s*new\[\] \{([^}]*)\}', src):
        name, w, h, t, r = m.groups()
        heads[name] = dict(W=float(w), H=float(h), T=np.array([float(x.strip().rstrip('f')) for x in t.split(',')], np.float32),
                           R=np.array([float(x.strip().rstrip('f')) for x in r.split(',')], np.float32))
    lay = open(_os.path.join(ROOT, 'Assets/5_Data/Chibi/Chibi_FaceLayout.asset'), encoding='utf-8').read()
    for m in re.finditer(r'- Head: (\w+)\s+EyeHeight: ([\d.]+)\s+EyeSpacing: ([\d.]+)\s+EyeSize: ([\d.]+)\s+MouthHeight: ([\d.]+)\s+NoseHeight: ([\d.]+)', lay):
        n = m.group(1)
        if n in heads: heads[n].update(EyeT=float(m.group(2)), EyeDx=float(m.group(3)), EyeS=float(m.group(4)), MouthT=float(m.group(5)), NoseT=float(m.group(6)))
    return heads
HEADS = _read_tables()

class Head:
    """머리 한 벌 — C# `HeadShape`와 같은 셈(픽셀 → m · 깊이 0.95 · 아래 반이 앞으로 쏠린다 `Shear`)."""
    def __init__(self, name):
        h = HEADS[name]; k = TORSO_H / BODY_PX
        # **여자 얼굴의 이목구비 자리 · 크기는 같은 모양의 남자 얼굴 값을 쓴다**(11-07 사용자: *"남자 이목구비랑 맞춰서 이목구비를 조절해달라"*) —
        # 표의 여자 값은 그림 판 시절에 따로 재어 머리마다 눈 높이 · 간격 · 크기가 들쭉날쭉했다(넓은 부드러운은 눈이 0.496으로 높았다).
        f = HEADS[name.replace('Female_', 'Male_')] if name.startswith('Female_') else h
        self.name = name; self.female = name.startswith('Female')
        self.W, self.H = h['W'] * k, h['H'] * k * (SLIM_H if 'Slim' in name else 1.0); self.D = self.W * DEPTH
        self.F = 0.26 if name.endswith('Sharp') else 0.20
        self.T, self.R = h['T'], h['R']
        et, mt, nt = f['EyeT'], f['MouthT'], f['NoseT']
        self.EY = self.y_of(et); self.MY = self.y_of(mt); self.NY = self.y_of(nt)
        self.EX = f['EyeDx'] * self.W * EYE_SPREAD; self.ER = f['EyeS'] * EYE_K * self.W * 0.5
        self.ZE = self.front(et); self.ZN = self.front(nt); self.ZM = self.front(mt)
        self.R0 = float(np.interp(et, self.T, self.R)) * self.W   # 귀 높이의 머리 옆 반폭
    def y_of(self, t): return (t - 0.5) * self.H
    def front(self, t):
        yn = t - 0.5
        return (float(np.interp(t, self.T, self.R)) + (self.F * 2 * yn * yn if yn < 0 else 0)) * self.D
    def sdf(self, p):
        ux, uy, uz = p[..., 0] / self.W, p[..., 1] / self.H, p[..., 2] / self.D
        uz = uz - np.where(uy < 0, self.F * 2 * uy * uy, 0)
        r = np.interp(np.clip(uy + 0.5, 0, 1), self.T, self.R)
        d = np.sqrt(ux * ux + uz * uz) - r
        return np.maximum(d, np.abs(uy) - 0.5) * 0.6

# ── 얼굴 ── 코 · 귀 · 눈 · 눈썹 · 입은 고르기 축(벌 이름)
def nose(h, kind, p):
    m = mirror(p); z = h.ZN; NY = h.NY
    if kind == 'Button':     # 작고 둥근 코끝 — 콧대는 거의 없다
        return smin(sph(p, V(0, NY + 0.004, z + 0.016), 0.024), sph(m, V(0.020, NY - 0.006, z + 0.002), 0.016), 0.010)
    if kind == 'Straight':   # 곧은 콧대 · 작은 끝
        bridge = seg(p, V(0, h.EY - 0.005, h.ZE + 0.000), V(0, NY + 0.004, z + 0.026), 0.011, 0.018)
        return smin(bridge, ell(p, V(0, NY - 0.002, z + 0.024), V(0.024, 0.019, 0.020)), 0.012)
    if kind == 'Broad':      # 넓고 둥근 코 — 콧방울이 벌어진다
        base = ell(p, V(0, NY - 0.002, z + 0.014), V(0.040, 0.026, 0.028))
        return smin(base, sph(m, V(0.032, NY - 0.010, z + 0.002), 0.020), 0.012)
    return NONE(p)
def ear(h, p):               # 둥근 사람 귀 — 머리 옆에 세운 납작한 콩 · 안쪽은 오목
    m = mirror(p)
    c = V(h.R0 - 0.004, (h.EY + h.NY) * 0.5 - 0.005, -0.03)
    q = m - c
    shell = ell(q, V(0, 0, 0), V(0.030, 0.062, 0.046))
    cup = ell(q, V(0.020, 0.004, 0.006), V(0.016, 0.042, 0.030))
    return smax(shell, -cup, 0.006)
def face(h, nose_kind):
    return lambda p: smin(smin(h.sdf(p), nose(h, nose_kind, p), 0.010), ear(h, p), 0.012)

def on_face(h, p, region, out, inner=0.010):   # 얼굴 살 위 `out` 두께의 껍데기 중 `region` 안(드워프 눈과 같은 길 — 옆에서 봐도 면에 붙는다)
    fd = h.sdf(p)
    shell = np.maximum(fd - out, -(fd + inner))
    return smax(np.maximum(shell, region), (h.ZE - 0.12) - p[..., 2], 0.002)
def eyes(h, p):
    m = mirror(p)
    return on_face(h, p, ell(m, V(h.EX, h.EY - 0.004, h.ZE), V(h.ER * 0.80, h.ER * 0.92, 0.3)), 0.004)
def glint(h, p):
    m = mirror(p)
    return on_face(h, p, ell(m, V(h.EX - h.ER * 0.25, h.EY + h.ER * 0.25, h.ZE), V(h.ER * 0.25, h.ER * 0.25, 0.3)), 0.007)
BROWS = {   # 이름: (굵기 안 · 밖, 아치 높이, 기울기)
    'Soft':  (0.010, 0.004, 0.016, -0.004),
    'Thick': (0.016, 0.008, 0.008, -0.002),
    'Arch':  (0.009, 0.003, 0.026, -0.010),
}
def brows(h, kind, p):       # 살갗에 칠한 눈썹 띠(엘프와 같은 길)
    th0, th1, arch, tilt = BROWS[kind]
    m = mirror(p)
    x0, x1 = h.EX - h.ER * 1.3, h.EX + h.ER * 1.4
    t = np.clip((m[..., 0] - x0) / (x1 - x0), 0, 1)
    yc = h.EY + h.ER / EYE_ASPECT ** 0.5 + 0.040 + arch * np.sin(t * np.pi * 0.9) + tilt * t
    th = th0 * (1 - t) + th1 * t
    band = np.maximum(np.abs(m[..., 1] - yc) - th, np.maximum(x0 - m[..., 0], m[..., 0] - x1))
    return on_face(h, p, band, 0.003)
def mouth(h, p):
    return on_face(h, p, np.maximum(np.abs(p[..., 1] - (h.MY + 0.004)) - 0.004, np.abs(p[..., 0]) - 0.040), 0.003)

# ── 머리카락 ── 숱 = 머리를 고르게 키운 껍데기 + 아랫선(엘프 `scalp`와 같은 길 — `머리 - 두께`면 정수리가 바늘처럼 솟는다)
def scalp(h, p, th, low):
    s = 1 + np.asarray(th, np.float32) / 0.30
    s3 = s[..., None] if np.ndim(s) else s
    return smax(h.sdf(p / s3) * s, low - p[..., 1], 0.02)
def hairline(h, ang, top, side, nape, burn=None):   # 이마(0°) → 관자놀이 → 귀 위(90°) → 목덜미(180°)
    front = np.where(ang < 85, top + (side - top) * ease((ang - 18) / 67), side)
    return np.where(ang < 95, front, side + (nape - side) * ease((ang - 100) / 60))
def ear_cut(h, p, d):        # 귀가 숱을 뚫고 나온다 — 귀 둘레만 덜어 낸다
    return smax(d, -(ear(h, p) - 0.006), 0.004)

def male_line(h, ang, top, recess, side, burn, nape):
    """남자 이마선 — 가운데(top) → 관자놀이 모서리가 살짝 들어가고(recess) → 귀 위(side)에서 짧은 구레나룻(burn)으로 → 목덜미(nape)."""
    a = np.asarray(ang)
    f = np.interp(a, [0, 22, 38, 60, 80, 88, 96, 112, 140, 180], [top, top - 0.005, recess, side + 0.03, side, burn, side + 0.01, side - 0.02, nape, nape])   # 목덜미는 평평하게(한 점으로 모으면 V자 · v4)
    return f.astype(np.float32)
def fringe_hem(ang, amp, n=11):   # 앞머리 끝 물결 — 가르마 쪽은 잔잔하게
    return amp * (0.5 - 0.5 * np.cos(np.radians(ang) * n * 2)) * ease((50 - ang) / 20)

def hair_crop(h, p):         # 짧게 친 머리 — 옆 · 뒤는 바짝(얇게), 정수리만 숱 · 이마선 모서리 · 짧은 구레나룻 · 앞머리 끝은 물결(v4 — 사용자 "남자 머리카락만 잘 다듬어")
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.185, h.EY + 0.200, h.EY + 0.07, h.EY - 0.02, -0.17) - fringe_hem(ang, 0.018)
    top = ease((p[..., 1] - (h.EY + 0.06)) / 0.22)
    th = 0.010 + 0.026 * top * (0.6 + 0.4 * ease((p[..., 2] + 0.05) / 0.25))                 # 옆 · 뒤 1cm · 정수리 앞쪽이 가장 두껍다
    return ear_cut(h, p, scalp(h, p, th, low) + strands(p, 0.0025, 80) * top)
def hair_side(h, p):         # 옆 가르마 — -x 쪽 가르마에서 +x로 쓸어 넘긴 윗머리 · 옆 · 뒤는 바짝(두꺼우면 헬멧 · v1~v3)
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.17, h.EY + 0.18, h.EY + 0.07, h.EY - 0.01, -0.18)   # 모서리를 깊이 넣으면 정면에서 뿔처럼 선다(v4)
    top = ease((p[..., 1] - (h.EY + 0.08)) / 0.20)
    side = ease((p[..., 0] + 0.08) / 0.25)                                                  # 가르마(-x)에서 멀수록 부푼다
    th = 0.010 + top * (0.016 + 0.018 * side) * (0.5 + 0.5 * ease((p[..., 2] + 0.10) / 0.30))
    d = scalp(h, p, th, low)
    # 이마 위로 쓸어 넘긴 끝 — 가르마 쪽에서 비스듬히 +x 관자놀이로 흘러 내려간다
    sweep = seg(p, V(-0.07, h.EY + 0.235, h.ZE - 0.05), V(0.19, h.EY + 0.165, h.ZE - 0.12), 0.040, 0.026)
    d = smin(d, smax(sweep, h.sdf(p) - 0.048, 0.015), 0.025)
    part = 0.013 * np.exp(-((p[..., 0] + 0.085) / 0.010) ** 2) * (p[..., 1] > h.EY + 0.12) * (p[..., 2] > -0.12)   # 가르마 골
    return ear_cut(h, p, d + part + strands(p, 0.0025, 70) * top)

def frame_line(h, ang, jaw):   # 가르마에서 이마 양쪽으로 둥글게 흘러내려(C) 관자놀이에서 꺾여 볼 옆을 따라 `jaw`까지 — 앞은 얼굴을 비우고 옆은 덮는다
    top, temple = h.EY + 0.25, h.EY + 0.04
    fr = top + (temple - top) * ease(ang / 46) ** 0.8
    return np.where(ang < 46, fr, temple + (jaw - temple) * ease((ang - 46) / 14))
def hair_bob(h, p):          # 단발 — 가운데 가르마 · 턱선까지 둥글게
    ang = angle(p)
    jaw = h.MY - 0.03
    low = np.where(ang < 100, frame_line(h, ang, jaw), jaw + 0.01 * ease((ang - 100) / 60))
    th = 0.034 + 0.020 * ease((h.EY - p[..., 1]) / 0.25)                                     # 아래로 갈수록 부푼다(단발의 둥근 끝)
    d = scalp(h, p, th, low)
    part = 0.012 * np.exp(-(p[..., 0] / 0.012) ** 2) * (p[..., 2] > -0.05) * (p[..., 1] > 0.12)
    return d + part + strands(p, 0.003, 55)
def hair_long(h, p):         # 긴 생머리 — 단발 위로 뒤 · 옆이 어깨 아래까지 곧게 떨어진다(머리 둘레를 따라 두른 한 장)
    ang = angle(p)
    d = hair_bob(h, p)
    th = 0.045
    rx, rz = h.W * 0.5 * 0.97 + th, h.D * 0.5 * 0.97 + th                                    # 머리 가장 넓은 둘레 + 숱
    rad = np.sqrt((p[..., 0] / rx) ** 2 + (p[..., 2] / rz) ** 2)
    ring = np.maximum(rad - 1, (1 - th / min(rx, rz) * 1.6) - rad) * min(rx, rz)               # 얇은 원통 껍데기
    fall = smax(ring, np.maximum(p[..., 1] - 0.02, -0.62 - p[..., 1]), 0.03)                  # 위는 숱에 묻히고 아래 끝은 등 중간
    fall = smax(fall, p[..., 2] - (-0.02 + 0.10 * ease((p[..., 1] + 0.05) / 0.25)), 0.04)          # 귀 뒤 평면보다 뒤만(각도로 자르면 턱 밑에 들쭉날쭉한 띠가 남았다 · v2) · 위는 볼 옆까지 앞으로
    return smin(d, fall + strands(p, 0.004, 50), 0.04)

# ── 다발 도구(엘프 미리보기와 같은 셈) ──
def lock_r(p, pts, radii, k=0.006):
    d = NONE(p)
    for i in range(len(pts) - 1): d = smin(d, seg(p, pts[i], pts[i + 1], radii[i], radii[i + 1]), k)
    return d
def bez(a, b, c, e, n):
    return [a * (1 - t) ** 3 + b * 3 * (1 - t) ** 2 * t + c * 3 * (1 - t) * t * t + e * t ** 3 for t in np.linspace(0, 1, n + 1)]
def plait(p, pts, r0, r1, n=14):   # 땋은 가닥 — 길을 따라 누운 마디
    P = np.array(pts, np.float32); seglen = np.linalg.norm(np.diff(P, axis=0), axis=1)
    s_ = np.concatenate([[0], np.cumsum(seglen)]); u = np.linspace(0, s_[-1], n * 2 + 1)
    Q = np.stack([np.interp(u, s_, P[:, i]) for i in range(3)], -1).astype(np.float32)
    d = NONE(p)
    for i in range(n):
        a_, c_, b_ = Q[2 * i], Q[2 * i + 1], Q[2 * i + 2]; r = r0 + (r1 - r0) * i / max(n - 1, 1)
        d = smin(d, np.minimum(seg(p, a_, c_, r * 0.62, r), seg(p, c_, b_, r, r * 0.62)), 0.004)
    return d
def on_head(h, x, y, z, off):      # 머리 겉면(+ off) 위의 점 — 방향 (x, y, z)로 밀어 맞춘다
    lo, hi = 0.2, 3.0
    for _ in range(30):
        s_ = (lo + hi) * 0.5
        if h.sdf(np.array([[x * s_, y * s_, z * s_]], np.float32))[0] < off: lo = s_
        else: hi = s_
    return V(x * lo, y * lo, z * lo)
TAIL_R = [0.030, 0.050, 0.074, 0.080, 0.070, 0.056, 0.042, 0.026, 0.012]   # 꽁지 굵기 — 끈에서 가늘게 나와 불룩했다가 가늘게(꽁지 규칙 · 엘프 `h_tail`)
def tail(p, tie, up, back, fall, k=1.0):   # 말꼬리처럼 끝이 갈라지는 꽁지 — 가닥 여섯이 끈에서 솟았다가 휘어 내린다(엘프 v6)
    d = NONE(p); R = [r * k for r in TAIL_R]
    for dx, dz, s_ in ((-0.05, 0.00, 0.88), (-0.025, -0.03, 1.0), (0.0, 0.02, 1.06), (0.025, -0.02, 1.0), (0.05, 0.01, 0.88), (0.0, -0.05, 0.95)):
        pts = bez(tie, tie + V(dx * 0.3, up, -0.05), tie + V(dx * 0.9, up * 0.75, -back), tie + V(dx * 2.0, -fall * s_, -back * 0.6 + dz), 8)
        d = smin(d, lock_r(p, pts, R), 0.03)
    return d

# ── 남자 머리 더 ──
def hair_buzz(h, p):         # 바짝 민 머리 — 6mm 껍데기 · 거친 결
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.19, h.EY + 0.20, h.EY + 0.07, h.EY + 0.0, -0.16)
    return ear_cut(h, p, scalp(h, p, 0.006, low) + 0.0015 * np.sin(p[..., 0] * 160) * np.sin(p[..., 2] * 160))
def hair_slick(h, p):        # 올백 — 이마선이 곧고 숱은 뒤로 매끈하게 · 빗질 골은 앞뒤로
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.20, h.EY + 0.20, h.EY + 0.08, h.EY + 0.0, -0.20)
    top = ease((p[..., 1] - (h.EY + 0.08)) / 0.22)
    th = 0.010 + 0.030 * top * ease((0.10 - p[..., 2]) / 0.25)                              # 이마 위는 얇게 · 정수리 뒤로 부푼다
    comb = 0.003 * np.sin(p[..., 0] * 90) * ease((p[..., 1] - low - 0.03) / 0.05)          # 빗질 골 — 이마선 근처는 잦아든다(그대로면 톱니 · v6)
    return ear_cut(h, p, scalp(h, p, th, low) + comb * top)
def hair_spiky(h, p):        # 삐죽 세운 머리 — 짧은 머리 위로 뾰족한 다발
    d = hair_crop(h, p)
    # 다발은 머리 위 · 앞 반구에 고르게 — 머리 겉면 방향에서 위 · 앞으로 기울여 세운다(정수리 한 점에만 모으면 안 보인다 · v6)
    for el, az in ((80, 0), (60, 0), (60, 60), (60, -60), (55, 120), (55, -120), (55, 180), (35, 25), (35, -25), (35, 75), (35, -75),
                   (30, 130), (30, -130), (30, 180), (75, 120), (75, -120)):
        e, a = np.radians(el), np.radians(az)
        dirn = np.array([np.cos(e) * np.sin(a), np.sin(e), np.cos(e) * np.cos(a)], np.float32)
        base = on_head(h, *dirn, 0.015)
        tipd = dirn + np.array([0, 0.45, 0.25], np.float32); tipd /= np.linalg.norm(tipd)
        d = smin(d, seg(p, base, base + tipd * (0.075 + 0.02 * np.sin(el + az)), 0.040, 0.004), 0.018)
    return d
def hair_shaggy(h, p):       # 덥수룩한 머리 — 귀를 반쯤 덮고 앞머리가 눈썹께 · 끝이 들쭉날쭉
    ang = angle(p)
    hem = 0.022 * (0.5 - 0.5 * np.cos(np.radians(ang) * 26))
    low = np.interp(ang, [0, 30, 60, 85, 100, 140, 180], [h.EY + 0.10, h.EY + 0.09, h.EY + 0.03, h.EY - 0.04, h.EY - 0.05, -0.20, -0.22]) - hem
    th = 0.022 + 0.020 * ease((p[..., 1] - h.EY) / 0.25)
    d = scalp(h, p, th, low)
    d = smax(d, (p[..., 2] - (h.ZE + 0.02)) * (ang < 50) - 9 * (ang >= 50), 0.01)          # 앞머리가 얼굴 앞으로 나서지 않게
    return d + strands(p, 0.004, 60)
def hair_manbun(h, p):       # 묶어 올린 남자 머리 — 올백 + 정수리 뒤 상투 · 끈
    d = hair_slick(h, p)
    c = on_head(h, 0.0, 0.75, -0.6, 0.05)
    bun = smin(sph(p, c + V(0, 0.03, -0.01), 0.070), sph(p, c + V(0, 0.07, -0.03), 0.055), 0.03) + strands(p, 0.003, 70)
    tie = band(p, c + V(0, -0.025, 0.012), c + V(0, 0.005, 0.0), 0.050)
    return smin(d, np.minimum(bun, tie), 0.02)

# ── 여자 머리 더 ──
def hair_pony(h, p):         # 뒤로 빗어 묶은 꽁지(꽁지 규칙 — 엘프 `h_tail`)
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.22, h.EY + 0.21, h.EY + 0.08, h.EY + 0.02, -0.16)
    d = scalp(h, p, 0.026, low) + strands(p, 0.003, 90)
    tie = on_head(h, 0.0, 0.45, -0.9, 0.035)
    d = smin(d, tail(p, tie, 0.12, 0.30, 0.78) + strands(p, 0.002, 60), 0.03)
    knot = band(p, tie + V(0, 0.01, 0.005), tie + V(0, 0.045, -0.01), 0.038)
    return ear_cut(h, p, np.minimum(d, knot))
def hair_bun(h, p):          # 틀어 올린 머리 — 정수리 뒤에 둥근 쪽 · 앞은 가르마
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.22, h.EY + 0.21, h.EY + 0.08, h.EY + 0.02, -0.16)
    d = scalp(h, p, 0.028, low) + strands(p, 0.003, 90)
    c = on_head(h, 0.0, 0.85, -0.35, 0.05)
    swirl = 0.004 * np.sin(np.arctan2(p[..., 0], p[..., 2] - c[2]) * 9 + p[..., 1] * 40)   # 감아 올린 결
    bun = smin(sph(p, c + V(0, 0.04, -0.02), 0.095), sph(p, c + V(0, 0.09, -0.04), 0.075), 0.04) + swirl
    part = 0.010 * np.exp(-(p[..., 0] / 0.012) ** 2) * (p[..., 2] > -0.05) * (p[..., 1] > 0.12)
    return ear_cut(h, p, smin(d + part, bun, 0.03))
def hair_braid(h, p):        # 한 갈래로 땋아 등으로 내린 머리 — 끝은 끈으로 묶고 꽁지처럼 갈라진다
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.22, h.EY + 0.21, h.EY + 0.08, h.EY + 0.02, -0.18)
    d = scalp(h, p, 0.028, low) + strands(p, 0.003, 90)
    top = on_head(h, 0.0, 0.1, -1.0, 0.02)
    pts = [top + V(0, 0.02, 0.0), top + V(0, -0.10, -0.03), top + V(0, -0.25, -0.03), top + V(0, -0.40, 0.0), top + V(0, -0.52, 0.03)]
    d = smin(d, plait(p, pts, 0.050, 0.034, 9), 0.03)
    end = pts[-1]
    knot = band(p, end + V(0, 0.01, 0), end + V(0, -0.02, 0), 0.036)
    brush = tail(p, end + V(0, -0.02, 0), -0.02, 0.0, 0.14, 0.45)                           # 끝 술(꽁지 규칙 — 갈라진다)
    return ear_cut(h, p, np.minimum(smin(d, brush, 0.01), knot))
def hair_pixie(h, p):        # 짧은 여자 머리 — 옆 · 뒤는 짧고 앞머리를 한쪽으로 길게 쓸었다
    ang = angle(p)
    low = male_line(h, ang, h.EY + 0.18, h.EY + 0.19, h.EY + 0.04, h.EY - 0.03, -0.17)
    th = 0.014 + 0.024 * ease((p[..., 1] - h.EY) / 0.22)
    d = scalp(h, p, th, low)
    sweep = seg(p, V(-0.12, h.EY + 0.20, h.ZE - 0.03), V(0.16, h.EY + 0.09, h.ZE - 0.07), 0.034, 0.018)   # 이마를 비스듬히 가로지르는 앞머리
    d = smin(d, smax(sweep, h.sdf(p) - 0.040, 0.012), 0.025)
    return ear_cut(h, p, d + strands(p, 0.003, 70))

HAIRS = {'Crop': hair_crop, 'Side': hair_side, 'Buzz': hair_buzz, 'Slick': hair_slick, 'Spiky': hair_spiky, 'Shaggy': hair_shaggy, 'ManBun': hair_manbun,
         'Bob': hair_bob, 'Long': hair_long, 'Pony': hair_pony, 'Bun': hair_bun, 'Braid': hair_braid, 'Pixie': hair_pixie}

SKIN = (0.93, 0.80, 0.68); DARK = (0.07, 0.05, 0.05); WHITE = (0.97, 0.97, 0.97); LIP = (0.55, 0.33, 0.30)
BROWN = (0.33, 0.22, 0.15); BLACK = (0.15, 0.13, 0.14); BLOND = (0.82, 0.66, 0.36); AUBURN = (0.48, 0.21, 0.13)
TUNIC = (0.30, 0.38, 0.28)
def body(p): return ell(p, V(0, -0.80, -0.04), V(0.36, 0.40, 0.21))   # 몸통 — 게임 몸(사람 남성 `BodyDims`)의 어깨 폭 · 깊이(전에는 깊이 0.33으로 두꺼워 뒤로 내린 가닥이 몸에 묻혔다)

def pieces(head, nose_kind, brow, hair, col):
    h = Head(head)
    P = [(face(h, nose_kind), SKIN), (lambda p: eyes(h, p), DARK), (lambda p: glint(h, p), WHITE),
         (lambda p: brows(h, brow, p), tuple(c * 0.85 for c in col)), (lambda p: mouth(h, p), LIP), (body, TUNIC)]
    if hair: P.append((lambda p: HAIRS[hair](h, p), col))
    return P

GREY = (0.55, 0.53, 0.52); PLATN = (0.88, 0.84, 0.72)
ROWS = [('Male_BroadSquare',  'Button',   'Thick', 'Crop',   BROWN),
        ('Male_SlimSoft',     'Straight', 'Soft',  'Side',   BLACK),
        ('Male_SlimSoft',     'Broad',    'Thick', 'Buzz',   BLACK),
        ('Male_SlimSquare',   'Straight', 'Soft',  'Slick',  BROWN),
        ('Male_BroadSoft',    'Button',   'Soft',  'Spiky',  BLOND),
        ('Male_SlimSharp',    'Straight', 'Thick', 'Shaggy', AUBURN),
        ('Male_BroadSquare',  'Broad',    'Thick', 'ManBun', GREY),
        ('Female_BroadSoft',  'Button',   'Arch',  'Bob',    AUBURN),
        ('Female_SlimSharp',  'Straight', 'Arch',  'Long',   BLOND),
        ('Female_SlimSoft',   'Button',   'Arch',  'Pony',   BROWN),
        ('Female_BroadSoft',  'Straight', 'Soft',  'Bun',    BLACK),
        ('Female_SlimSquare', 'Button',   'Arch',  'Braid',  PLATN),
        ('Female_BroadSquare','Straight', 'Soft',  'Pixie',  BLACK)]

S, Hh, VIEWS = 300, 360, (0, 40, 90)
def row(job):
    (hd, nk, br, hr, col), views = job
    return [np.asarray(render(pieces(hd, nk, br, hr, col), a, S, Hh, -0.12, 1.25)) for a in views]

if __name__ == '__main__':
    from multiprocessing import Pool
    out = sys.argv[1]
    if len(sys.argv) > 2 and sys.argv[2]: ROWS[:] = [ROWS[int(i)] for i in sys.argv[2].split(',')]
    views = tuple(int(a) for a in sys.argv[3].split(',')) if len(sys.argv) > 3 else VIEWS
    with Pool(len(ROWS)) as pool: rows = pool.map(row, [(r, views) for r in ROWS])
    sheet = Image.new('RGB', (S * len(views), Hh * len(ROWS)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for i, ims in enumerate(rows):
        for c, im in enumerate(ims): sheet.paste(Image.fromarray(im), (c * S, i * Hh))
        hd, nk, br, hr, _ = ROWS[i]; d.text((6, i * Hh + 4), f'{hd} / nose {nk} / brow {br} / hair {hr}', fill=(255, 220, 120))
    sheet.save(out); print('saved', out)
