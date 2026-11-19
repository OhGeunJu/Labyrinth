"""코볼트 — **하이에나 몬스터**(11-19 사용자: *"내가 생각하는 코볼트는 개 얼굴 느낌의 하이에나 몬스터"*).

늑대인간(`werewolf/werewolf.py`)을 갈래 냈다 — 사람 치비 몸 + 역관절 다리 + 짐승 머리는 같은 틀이고, 하이에나로 바꾼 것:
  - 머리 — 짧고 굵은 주둥이 · 검은 주둥이와 코 · 크고 둥근 귀(앞을 본다) · 고개를 더 앞으로 숙인다.
  - 털 — 모래색 바탕에 짙은 점박이 · 정수리에서 목덜미 · 등으로 서는 뻣뻣한 볏(사자 갈기 대신).
  - 꼬리 — 짧고 끝이 검은 술.
실행: python kobold.py renders/kobold_v1"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.join(_here, '..', 'common')); _sys.path.insert(0, _os.path.join(_here, '..', 'dwarf'))
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg
import head_base as hb
import body as bd
from quad_preview import fluff, frame

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def union(ds):
    out = ds[0]
    for d in ds[1:]: out = np.minimum(out, d)
    return out
def bounded(f, p, c, r):
    b = length(p - c) - r; m = b < 0.03
    if not m.any(): return b
    out = b.copy(); out[m] = f(p[m]); return out
def squash(p, y0, sy):
    q = p.copy(); q[..., 1] = y0 + (p[..., 1] - y0) / sy; return q
def flat(p, n, k):
    """법선 n 쪽으로 k배 납작하게 본 좌표 — 돌려준 점으로 잰 거리에 k를 곱해 쓴다(귀 · 털 술)."""
    n = nrm(n); h = (p * n).sum(-1); return p + n * (h / k - h)[..., None]
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
def mixc(a, b, k): return np.asarray(a, np.float32) * (1 - k[..., None]) + np.asarray(b, np.float32) * k[..., None]
UP = np.array([0, 1, 0], np.float32)

# ── 색 ──
# ── 털 벌(v19 — 사용자: *"하이에나의 색이나 무늬를 다양성 있게"*) — 게임에서는 프리팹 하나 + 팔레트(바탕 칸 = 바탕 · 짙은 · 밝은 · 주둥이 /
#    덧칠 칸 = 점 · 줄 · 망토 — 그 무늬가 없는 벌은 칸 알파를 끈다). 실제 하이에나 갈래에서: 점박이 · 줄무늬 · 갈색(목 망토) + 붉은 · 바랜 · 검은.
COATS = {
    'spotted':   dict(fur=(0.64, 0.53, 0.36), dark=(0.24, 0.18, 0.13), pale=(0.80, 0.74, 0.60), spot=(0.30, 0.22, 0.15), muz=(0.13, 0.11, 0.10), spots=1.0, stripes=0.0, body_stripes=0.0, mantle=0.0, mantle_c=(0.8, 0.74, 0.6)),
    'red':       dict(fur=(0.58, 0.38, 0.24), dark=(0.22, 0.12, 0.08), pale=(0.78, 0.64, 0.50), spot=(0.24, 0.13, 0.09), muz=(0.12, 0.08, 0.07), spots=1.0, stripes=0.0, body_stripes=0.0, mantle=0.0, mantle_c=(0.8, 0.74, 0.6)),
    'striped':   dict(fur=(0.70, 0.66, 0.56), dark=(0.16, 0.14, 0.13), pale=(0.86, 0.83, 0.75), spot=(0.18, 0.16, 0.14), muz=(0.10, 0.09, 0.09), spots=0.0, stripes=1.0, body_stripes=1.0, mantle=0.0, mantle_c=(0.8, 0.74, 0.6)),
    'brown':     dict(fur=(0.32, 0.25, 0.19), dark=(0.16, 0.12, 0.09), pale=(0.66, 0.58, 0.46), spot=(0.18, 0.14, 0.11), muz=(0.10, 0.08, 0.07), spots=0.0, stripes=0.9, body_stripes=0.0, mantle=1.0, mantle_c=(0.78, 0.70, 0.55)),
    'faded':     dict(fur=(0.80, 0.74, 0.60), dark=(0.46, 0.40, 0.33), pale=(0.90, 0.86, 0.76), spot=(0.58, 0.50, 0.40), muz=(0.28, 0.24, 0.21), spots=0.6, stripes=0.0, body_stripes=0.0, mantle=0.0, mantle_c=(0.8, 0.74, 0.6)),
    'black':     dict(fur=(0.25, 0.23, 0.23), dark=(0.10, 0.09, 0.09), pale=(0.42, 0.40, 0.38), spot=(0.12, 0.11, 0.11), muz=(0.06, 0.06, 0.06), spots=0.7, stripes=0.0, body_stripes=0.0, mantle=0.0, mantle_c=(0.8, 0.74, 0.6)),
}
def set_coat(name):
    global COAT, FUR, FUR_D, PALE, SPOT, MUZ, MANTLE
    COAT = COATS[name]; FUR, FUR_D, PALE, SPOT, MUZ, MANTLE = COAT['fur'], COAT['dark'], COAT['pale'], COAT['spot'], COAT['muz'], COAT['mantle_c']
set_coat('spotted')   # 짙은 잿빛 · 더 짙은(귀 끝 · 등) · 밝은(주둥이 밑 · 가슴)
NOSE = (0.06, 0.05, 0.06); EYE = (0.92, 0.66, 0.16); DARK = (0.05, 0.04, 0.05); BONE = (0.93, 0.90, 0.80); MOUTH = (0.32, 0.08, 0.09)
EAR_IN = (0.36, 0.26, 0.22);   # v11 — 분홍을 걷고 밝은 잿빛 속털(사용자: *"귀가 너무 핑크색인 것도 좀 짜쳐"*)
CLOTH = (0.27, 0.22, 0.26); CLAW = (0.10, 0.09, 0.10)

# ── 몸 치수 — 사람 치비 식 · 체형 한 줄 ──
#        이름    반폭(밑 엉덩이 허리 가슴 어깨)          깊이  손    팔K   다리K 몸통K 팔굵기 다리굵기 머리 어깨둥글림
WOLF = ('Wolf', [0.21, 0.23, 0.24, 0.33, 0.35], 0.84, 0.32, 1.12, 1.22, 1.02, 1.15, 1.10, 1.0, 0.42)
D = bd.dims(WOLF); TOP = D['top']
HEAD_Y = TOP + bd.HeadGap + hb.H * 0.5 - 0.115; HEAD_Z = bd.HeadZ + 0.195   # 코볼트 — 더 숙인다   # v12 — 더 앞 · 아래(사용자: *"약간 굽은 몸처럼 얼굴이 앞으로 좀 나와 있는 게"*)   # 머리가 조금 앞 · 아래(늑대는 고개를 내민다)
HO = V(0, HEAD_Y, HEAD_Z)
def mir(p): return np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)

# ── 머리(머리 중심 원점) ─────────────────────────────────────────────────────
# **v8 — 진짜 늑대 얼굴**(사용자: *"지금 만들어진 건 너무 납작하고 · 내가 원하는 건 진짜 이런 늑대 같은 얼굴 · 늑대인간은 좀 멋있어야 해"* +
# 그림 다섯 — 깃 밖 `_refs/2026-11-15_werewolf/`). 그림에서 뽑은 것:
#   - 머리가 **깊다** — 주둥이는 길되 위아래로 두툼한 상자꼴(콧등이 비스듬히 내려가고 아래턱이 단단하다) · 이마에서 콧등으로 완만하게 꺾인다(stop).
#   - 눈은 **아몬드꼴로 치켜 올라간다** — 호박색 홍채 · 검은 눈동자 · 검은 테 · 무거운 눈썹뼈가 위를 덮는다.
#   - 귀는 크고 뾰족하며 머리 위 양옆에 선다.
#   - **볼 털이 얼굴을 액자처럼 감싼다** — 광대 뒤에서 턱 밑까지 겹겹이 바깥 · 뒤 · 아래로.
#   - **으르렁** — 윗입술이 송곳니 자리에서 들려 이가 드러나고 콧등에 주름이 진다.
#   - 털 색 — 콧등 · 이마 · 눈 둘레는 짙고, 볼 · 윗입술 가장자리 · 턱 밑 · 눈썹 위 점은 희다(회색 늑대).
MZ0, MZ1 = 0.085, 0.370   # 코볼트 — 짧고 굵은 주둥이   # v12 — 0.40 → 0.44(v8 옆선의 날렵함 — 사용자)   # v10 — 0.48 → 0.40(사용자: *"늑대인간이지 늑대가 아니니깐 주둥이 길이를 좀 더 좁혀도"*)                                               # 주둥이 뿌리 · 끝(z)
def muzzle_top(z): return 0.090 - 0.115 * (z - MZ0)                     # v12 — v8처럼 곧게 내려간다(끝에서 꺾지 않는다)   # 콧등 — 비스듬히 내려가고 끝에서 코로 둥글게 떨어진다
def muzzle_w(z): return 0.140 - 0.090 * np.clip(z - MZ0, 0, None)     # 위턱 반폭 — 코 쪽으로 가늘어진다(v8은 끝이 상자처럼 뭉툭했다)
MY0 = -0.095                                                          # 입선 높이
def mouth_y(p):     # 입선 — 송곳니 자리(z 0.30 ~ 0.42)에서 들린다(으르렁) · 뒤쪽은 볼로 올라간다
    z = p[..., 2]
    snarl = 0.020 * np.exp(-((z - 0.320) / 0.055) ** 2)
    return MY0 + snarl + 0.9 * np.clip(MZ0 + 0.03 - z, 0, 1) ** 2
EYE_C = V(0.104, 0.060, 0.160); EYE_R = 0.047; EYE_N = nrm([0.46, 0.12, 0.88])   # v9 — 크게 · 앞으로(v8은 작고 꺼풀에 묻혔다)
FACE = 'C'
def set_face(v): pass   # 옛 비교(A · B)는 걷었다 — v8은 그림을 따른 한 얼굴
def cran(p):
    m = mirror(p)
    d = ell(p, V(0, 0.020, -0.060), V(0.222, 0.212, 0.238))                         # 머리통
    d = smin(d, ell(m, V(0.140, -0.030, 0.030), V(0.080, 0.075, 0.120)), 0.05)      # 광대
    # **날렵한 볼**(v14 — 사용자: *"볼이 동그래 — 날렵한 느낌이 들어야"*): 옆면을 평면으로 깎아 광대에서 주둥이 쪽 · 턱 쪽으로 각지게 좁힌다
    x, y, z = m[..., 0], p[..., 1], p[..., 2]
    side = x - (0.200 - 0.42 * np.clip(z - 0.00, 0, None) - 0.55 * np.clip(-0.04 - y, 0, None))
    return smax(d, side, 0.022)
def muzzle_up(p):   # 위턱 — 위아래로 두툼한 상자(모서리를 둥글린다)
    x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
    d = smax(x - muzzle_w(z), y - muzzle_top(z), 0.035)
    d = smax(d, (MY0 - 0.020) - y, 0.025)
    return smax(smax(d, z - MZ1, 0.070), MZ0 - 0.05 - z, 0.03)
def muzzle_low(p):  # 아래턱 — 단단하다 · 위턱보다 조금 짧고 좁다
    x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
    w = 0.112 - 0.150 * np.clip(z - 0.02, 0, None); bot = -0.180 + 0.100 * np.clip(z - 0.02, 0, None)
    d = smax(x - w, y - (MY0 + 0.010), 0.025)
    d = smax(d, bot - y, 0.030)
    return smax(smax(d, z - (MZ1 - 0.035), 0.040), -0.02 - z, 0.03)
def wrinkles(p):    # 콧등 주름 — 으르렁댈 때 콧등을 가로지르는 짧은 골 셋
    m = mirror(p); d = far(p)
    for i, z in enumerate((0.255, 0.290, 0.325)):
        y = muzzle_top(z) - 0.006; d = np.minimum(d, seg(m, V(0.018, y + 0.008, z), V(muzzle_w(z) * 0.95, y - 0.030, z - 0.010), 0.012, 0.008))
    return d
def brow(p):        # 눈썹뼈 — 무겁게 눈 위를 덮고 안쪽 끝이 내려와 노려본다
    m = mirror(p); t = nrm(np.cross(UP, EYE_N))
    a = EYE_C + UP * 0.034 - t * 0.048 + EYE_N * 0.008 - UP * 0.010; b = EYE_C + UP * 0.054 + t * 0.060 - EYE_N * 0.008 + UP * 0.010
    return seg(m, a, b, 0.017, 0.021)   # v16 — 얇게 · 덜 튀어나오게(사용자: *"눈썹뼈가 너무 두꺼워 보인다"* — v15까지 0.030~0.036)
def skull0(p):
    d = smin(cran(p), muzzle_up(p), 0.075)                                         # 이마에서 콧등으로 완만하게 꺾인다
    return smin(smin(d, brow(p), 0.03), wrinkles(p), 0.012)
def skin(p):
    """머리 살 — 입선이 위아래를 가른다 · 눈구멍을 판다."""
    yv = p[..., 1] - mouth_y(p); G = 0.008
    up = np.maximum(skull0(p), -(yv - G) - 9.0 * (p[..., 2] < MZ0))
    low = np.maximum(smin(muzzle_low(p), cran(p), 0.05), yv + G)
    d = np.minimum(up, low)
    return smax(d, -(sph(mirror(p), EYE_C + EYE_N * 0.010, EYE_R * 1.02)), 0.006)
def inner(p): return np.minimum(muzzle_up(p), muzzle_low(p)) + 0.014             # 입 안 · 잇몸 — 입선 틈으로 보인다
NOSE_C = None
def nose(p):
    """**개 코**(v10 — v9는 주둥이 끝에 공을 붙여 *"삐에로처럼"* 보였다): 주둥이 끝을 덮는 사다리꼴 — 위는 평평하고 앞면이 납작하다 ·
    아래가 좁다 · 콧구멍 둘(쉼표꼴로 옆으로 벌어진다)을 파고 · 가운데 밑으로 인중 골이 입까지 내려간다."""
    m = mirror(p); top = muzzle_top(MZ1 - 0.02)
    c = V(0, top - 0.026, MZ1 - 0.010)
    q = p - c
    w = 0.046 - 0.30 * np.clip(-q[..., 1], 0, None)                                  # 아래로 좁아진다
    d = smax(np.abs(q[..., 0]) - w, np.abs(q[..., 1]) - 0.028, 0.014)
    d = smax(d, np.abs(q[..., 2] + 0.006) - 0.030, 0.014)
    d = smax(d, p[..., 1] - (muzzle_top(p[..., 2]) + 0.006), 0.010)                   # 윗면은 콧등 선을 넘지 않는다
    holes = ell(m, c + V(0.022, -0.006, 0.026), V(0.012, 0.009, 0.014))               # 콧구멍
    d = smax(d, -holes, 0.004)
    groove = np.maximum(np.abs(p[..., 0]) - 0.004, np.maximum(p[..., 1] - (c[1] - 0.012), (c[2] + 0.034) - p[..., 2]))   # 인중 — 앞면 아래 가운데
    return smax(d, -groove, 0.003)
def eyeball(p): return sph(mirror(p), EYE_C, EYE_R)
def pupil(p):       # 둥근 검은 눈동자
    m = mirror(p); t = nrm(np.cross(UP, EYE_N)); u = np.cross(EYE_N, t); q = m - EYE_C
    a, b = (q * t).sum(-1), (q * u).sum(-1)
    return np.maximum(np.maximum(length(q) - (EYE_R + 0.003), EYE_R * 0.45 - (q * EYE_N).sum(-1)), np.sqrt(a * a + b * b) - EYE_R * 0.40)
def lid(p):         # 아몬드 눈매 — 윗꺼풀은 바깥이 높고(치켜 올라간다) 아랫꺼풀도 바깥이 높다
    m = mirror(p); t = nrm(np.cross(UP, EYE_N)); u = np.cross(EYE_N, t); q = m - EYE_C
    a, b = (q * t).sum(-1), (q * u).sum(-1)
    up = np.maximum(length(q) - (EYE_R + 0.007), (EYE_R * 0.42 + 0.40 * a) - b)
    low = np.maximum(length(q) - (EYE_R + 0.006), b - (-EYE_R * 0.58 + 0.30 * a))
    return np.minimum(up, low)
def glint(p):   # 반짝임은 눈 면에 붙은 얇은 판(11-18 — 튀어나오지 않게 · C#과 같은 값) — 눈동자(+3mm) 바로 위(+4mm) 껍데기의 둥근 자리
    t = nrm(np.cross(UP, EYE_N)); u = np.cross(EYE_N, t); q = mirror(p) - EYE_C; ln = np.maximum(length(q), 1e-6)
    d = nrm(EYE_N * 0.86 + u * 0.10 + t * 0.30)
    return np.maximum(np.maximum(ln - (EYE_R + 0.004), (EYE_R - 0.002) - ln), (np.cos(0.22) - (q * d).sum(-1) / ln) * EYE_R)
def liner(p):       # 눈 둘레의 검은 테(눈알 뒤에 깔린 공 — 가장자리만 테로 보인다) · 눈 앞꼬리에서 코 쪽으로 내려가는 검은 줄
    m = mirror(p); t = nrm(np.cross(UP, EYE_N))
    ring = sph(m, EYE_C - EYE_N * 0.014, EYE_R * 1.14)
    tear = seg(m, EYE_C - t * EYE_R * 0.95 + EYE_N * 0.004, EYE_C - t * EYE_R * 1.75 - UP * 0.030 + EYE_N * 0.010, 0.008, 0.004)
    return np.minimum(ring, tear)
# 귀 — 크고 뾰족하며 머리 위 양옆에 선다 · 앞뒤로 납작
EAR_A, EAR_B = V(0.125, 0.150, -0.030), V(0.200, 0.285, -0.080)   # v2 — 짧게(v1은 토끼 귀 같았다) · 코볼트 — 크고 둥근 하이에나 귀 · 곧게 선다   # v15 — 자리는 눈썹뼈 위 옆 · 끝은 옆 · 뒤 · 위로 눕는다(v14는 앞을 봐 곧추선 귀였다 — 사용자가 v10 · v11 · v12에서 거듭 말한 방향)   # v14 — 눈썹뼈 위 옆 · 밖으로 벌어진다(사용자: *"좀 더 옆으로 눈썹뼈 위치 정도"* + 늑대 사진)   # v13 — 뿌리가 털 껍데기 안에서 나온다(v12는 뿌리가 드러났다)   # v12 — 뒤 · 위로 길게 눕는다(사용자가 고쳐 그린 그림 `_refs/2026-11-15_werewolf/user_markup_v11.png`)   # v10 — 옆으로 약 37° 눕힌 A(사용자: *"너무 곧추서 있어 · 위로 안 서 있고 약간 옆으로"*)
_ax = nrm(EAR_B - EAR_A); EAR_N = nrm(V(0.40, 0.05, 0.90) - _ax * float(np.dot(V(0.40, 0.05, 0.90), _ax)))   # 귓속은 바깥 · 조금 앞을 본다

def ear(p):
    m = mirror(p); return seg(flat(m - EAR_A, EAR_N, 0.40) + EAR_A, EAR_A - nrm(EAR_B - EAR_A) * 0.05, EAR_B, 0.086, 0.066) * 0.40
def ear_in(p):
    m = mirror(p); c = EAR_A + (EAR_B - EAR_A) * 0.06 + EAR_N * 0.026
    d = seg(flat(m - c, EAR_N, 0.30) + c, c, EAR_A + (EAR_B - EAR_A) * 0.80 + EAR_N * 0.022, 0.062, 0.046) * 0.30
    return np.maximum(d, 0.012 - ((m - EAR_A) * EAR_N).sum(-1))
def tuft(m, a, dr, ln, r, n=None, k=0.55):
    """털 술 — 밑동이 굵고 끝이 뾰족한 납작한 뿔."""
    dr = nrm(dr); n = nrm(np.cross(dr, UP) if n is None else n)
    return seg(flat(m - a, n, k) + a, a - dr * r * 0.6, a + dr * ln, r, 0.004) * k
# **사자 갈기 한 덩어리**(v10 — 사용자: *"볼 털 별로야 · 정수리랑 볼 그리고 뒷머리 털을 한 번에 뽑는 거 어때 — 사자 갈기처럼 털을 더 많게"*).
# 얼굴 둘레(정수리 → 볼 → 턱 밑)를 따라 고리를 돌며 여러 겹으로 털 가닥을 꽂는다 — 가닥은 뒤 · 바깥으로 누워 흐르고, 겹이 뒤로 갈수록 길다.
# 가닥끼리 녹여 한 덩이로 읽힌다(한 줄로 서면 가시 · 뿔로 보였다).
def _mane_tufts():
    out = []
    LAYERS = ((-0.060, 0.215, 0.110, 0.066), (-0.130, 0.245, 0.140, 0.076), (-0.205, 0.255, 0.155, 0.082), (-0.275, 0.235, 0.150, 0.080))   # v12 — 더 길고 굵게   # v11 — 짧고 굵게(v10은 뒤로 뻗은 가시가 고슴도치 같았다)   # (z, 둘레 반지름, 길이, 굵기)
    for li, (z, rad, ln, r) in enumerate(LAYERS):
        n = 9 + li
        for i in range(n):
            th = np.radians(8 + (178 - 8) * (i + (0.5 if li % 2 else 0.0)) / (n - 0.5))   # 0 = 정수리 · 180 = 턱 밑
            if th > np.radians(180): continue
            ry = rad * (0.88 if th < np.radians(45) else 1.04 if th < np.radians(90) else 0.96)   # 정수리 쪽은 낮게(귀가 갈기 위로 드러난다)
            a = V(rad * np.sin(th), 0.030 + ry * np.cos(th), z)
            out_dir = V(np.sin(th), np.cos(th) * 0.85, 0)                              # 얼굴 둘레 바깥
            dr = nrm(out_dir * 0.70 + V(0, -0.55 - 0.20 * (th > np.radians(110)), -0.50))   # 바깥 · 아래로 흘러내린다   # 뒤로 눕고 · 아래쪽은 더 처진다
            k = 1.0 + 0.18 * np.sin(np.radians(37) * i)                                 # 가닥마다 길이를 조금씩 흔든다
            out.append((a, dr, ln * k * (0.70 if th < np.radians(45) else 1.0), r * (0.85 if th < np.radians(45) else 1.0)))
    return out
MANE_T = _mane_tufts()
def mane(p):
    m = mirror(p)
    d = ell(p, V(0, 0.030, -0.215), V(0.215, 0.235, 0.120))   # 뒤통수를 메우는 털 덩이(v11 뒤에서 고리 가운데가 비어 머리통이 보였다)
    for a, dr, ln, r in MANE_T:
        n = np.cross(dr, nrm(V(a[0], a[1] - 0.030, 0) + 1e-6))                          # 가닥은 둘레 방향으로 납작하다
        d = smin(d, tuft(m, a, dr, ln, r, n=n, k=0.70), 0.040)
    return d
# **망토 갈기**(v12 — 사용자가 고쳐 그린 그림: 털이 뒤통수에서 목덜미 · 어깨까지 흘러내린다). 몸 좌표 — 머리 둘레 갈기 아래를 잇는다.
def _cloak_tufts():
    out = []; top = TOP
    for row, (y, rad, ln, r) in enumerate(((HO[1] - 0.08, 0.215, 0.24, 0.074), (HO[1] - 0.18, 0.230, 0.26, 0.078), (top + 0.06, 0.240, 0.24, 0.080), (top - 0.04, 0.250, 0.20, 0.076))):
        zc = HO[2] - 0.22 - 0.05 * row
        n = 7 + row
        for i in range(n):
            th = np.radians(-75 + 150 * (i + (0.5 if row % 2 else 0.0)) / (n - 0.5))      # 0 = 바로 뒤 · ±75° = 옆
            if abs(th) > np.radians(75): continue
            x, z = rad * np.sin(th), zc - rad * 0.70 * np.cos(th) + 0.10 * (1 - np.cos(th))
            o = V(np.sin(th), 0, -np.cos(th) * 0.8)
            dr = nrm(o * 0.32 + V(0, -1.0, -0.20))                                         # 아래로 흘러내린다
            k = 1.0 + 0.15 * np.sin(1.7 * i + row)
            out.append((V(x, y, z), dr, ln * k, r))
    return out
CLOAK_T = None
def cloak(p):
    global CLOAK_T
    if CLOAK_T is None: CLOAK_T = _cloak_tufts()
    d = seg(p, HO + V(0, -0.04, -0.24), V(0, TOP - 0.12, -0.17), 0.175, 0.195)              # 속을 메우는 덩이(뒤 · 목덜미)
    d = smin(d, ell(mir(p), V(0.17, TOP - 0.03, -0.08), V(0.12, 0.10, 0.14)), 0.06)         # 어깨에 얹힌 털
    for a, dr, ln, r in CLOAK_T:
        n = np.cross(dr, nrm(V(a[0], 0, a[2] - (HO[2] - 0.15)) + 1e-6))
        d = smin(d, tuft(p, a, dr, ln, r, n=n, k=0.70), 0.045)
    return np.maximum(d, (HO[2] + 0.02) - p[..., 2] if False else d)
def cloak_col(p):   # 앞 · 옆(턱 밑에서 이어지는 볼 쪽)은 희고 · 뒤는 짙다
    z = p[..., 2] - (HO[2] - 0.15)
    return mixc(FUR, FUR_D, sstep(-0.05, -0.25, z) * 0.6)                              # 볼 · 턱 밑의 흰 털은 머리 갈기가 든다
# ── **털 한 덩어리**(v13 — 사용자: *"윗머리카락과 아랫머리카락의 어색한 간격 · 윗머리의 두 개로 나뉜 가르마 — 보낸 사진처럼 원래 머리랑
# 자연스럽게 이어지도록"*). v12는 머리 둘레 갈기(머리 좌표)와 망토(몸 좌표)가 따로 붙어 사이에 턱이 졌고, 좌우 거울 가닥 사이가 가르마로 갈라졌다.
# 이제 털은 **껍데기 하나**다 — 머리통을 감싼 살 위 한 겹이 이마 뒤에서 시작해 정수리 · 뒤통수를 덮고 목덜미 · 어깨까지 끊김 없이 흘러내린다.
# 가닥은 그 껍데기 위에 얹는 결일 뿐이다(넓게 녹인다 — 따로 서는 술이 아니다). 가운데 줄에도 가닥을 둬 가르마가 안 생긴다.
def fur_shell(p):
    """털 껍데기 — 머리통 위 얇은 한 겹 · **이마를 덮고 눈썹 사이까지 내려온다**(v14 — 사용자: *"정수리 털도 눈썹 사이까지 내려오고 — 지금은
    올백 머리 같은데 몸에서 자연스레 나오는 털처럼"*) · 볼 뒤에서 **볼 털 덩이**가 옆 · 뒤로 부푼다(v8의 광대 → 볼 털 선 — 사용자가 동그라미 친 그림
    `_refs/2026-11-15_werewolf/user_markup_v13_cheek.png`) · 목덜미 · 등 위 · 어깨로 끊김 없이 이어진다."""
    q = p - HO; x = np.abs(q[..., 0]); m = mirror(q)
    back = q[..., 2] - (0.055 + 0.20 * q[..., 1])                                    # 옆 · 뒤 — 볼 뒤까지
    brow_line = (0.078 + 0.55 * x) - q[..., 1]                                       # 이마 — 가운데는 눈썹 사이까지 V로 내려온다
    front = np.maximum(brow_line, q[..., 2] - 0.215)                                 # 콧등으로는 안 넘어간다
    # 두께가 끝에서 0으로 잦아든다 — 털이 살에서 자라 나온다(끊어 내면 모자 · 가발처럼 턱이 졌다)
    reg = np.minimum(back, front)
    h = np.maximum(skull0(q) - 0.026 * sstep(0.0, 0.07, -reg), reg)                   # 털 자리 밖(주둥이 · 입선)은 비운다 — 경계에선 두께가 0이라 턱이 없다
    return smin(h, _back_fur(p), 0.10)   # 목덜미 → 등 덩이(v3에서 덩이만 빼 봤다가 사용자: *"털 갈기가 있는 게 더 자연스럽다"* — v4에 되돌림)
def _back_fur(p):
    """목덜미 → 등 털 덩이 — 조끼 · 웃옷도 이것을 감싼다."""
    b = smin(seg(p, HO + V(0, -0.03, -0.17), V(0, TOP - 0.13, -0.16), 0.160, 0.175),   # 목덜미 → 등 위
             seg(p, V(0, TOP - 0.13, -0.16), V(0, D['lift'] + 0.12, -0.10), 0.175, 0.105), 0.08)   # 코볼트 v5 — 허리까지 가늘어지며 등 살에 녹는다(v2는 등 중간에서 끊겨 턱이 졌다)
    b = smin(b, ell(mir(p), V(0.165, TOP - 0.03, -0.08), V(0.125, 0.100, 0.140)), 0.07)   # 어깨에 얹힌 털
    b = smax(b, (p[..., 2] - (HO[2] - 0.02)), 0.06)                                   # 목 앞(목젖 · 가슴)은 덮지 않는다
    return b
def _on_back(a):
    """점 a 높이에서 몸통 · 목 살의 등 쪽 표면을 찾는다(안 → 뒤 · 조금 위)."""
    f = lambda q: smin(torso(q), neck(q), 0.05)
    o = V(0, float(a[1]), float(a[2]) + 0.15); dr = nrm(V(0, 0.15, -1)); lo, hi = 0.0, 0.6
    for _ in range(40):
        c = 0.5 * (lo + hi)
        if f((o + dr * c)[None, :])[0] < 0: lo = c
        else: hi = c
    return o + dr * (0.5 * (lo + hi))
def _crest_tufts():
    """하이에나 볏 — 정수리에서 목덜미 · 등으로 서는 뻣뻣한 털(뒤 · 위로) · 목덜미에서 가장 길다."""
    out = []
    path = [HO + V(0, 0.205, 0.010), HO + V(0, 0.170, -0.190), V(0, TOP + 0.03, -0.215), V(0, TOP - 0.30, -0.255)]
    L = [float(np.linalg.norm(path[i + 1] - path[i])) for i in range(3)]; tot = sum(L)
    for k in range(18):
        t = k / 17 * tot; i = 0
        while i < 2 and t > L[i]: t -= L[i]; i += 1
        a = path[i] + (path[i + 1] - path[i]) * (t / L[i])
        u = k / 17; ln = 0.07 + 0.10 * np.sin(np.pi * min(u * 1.6, 1.0)) * (1.0 - 0.5 * u)
        if a[2] < HO[2] - 0.20 or a[1] < HO[1] - 0.05:                  # 목 · 등은 살 표면에 심는다(머리 쪽은 털 껍데기가 받는다)
            a = _on_back(a) + V(0, 0, 0.012)
        a = a + V(0.022 * (1 if k % 2 else -1), 0, 0)
        out.append((a, nrm(V(0.20 * (1 if k % 2 else -1), 0.80, -0.60)), ln, 0.040))
    return out
def _mane_tufts_wolf():
    out = []
    # 머리 — 이마에서 정수리 · 뒤통수로 **누운** 결(가운데 줄 포함 · 좌우가 엇갈린다 · 살갗을 따라 눕는다)
    for row, (y, z, ln, r) in enumerate(((0.170, 0.120, 0.11, 0.046), (0.215, 0.040, 0.13, 0.052), (0.222, -0.050, 0.15, 0.056),
                                         (0.200, -0.140, 0.16, 0.060), (0.140, -0.225, 0.18, 0.064))):
        if row == 0: continue                                                       # 이마는 껍데기만(가닥이 서면 앞머리가 된다)
        for x in ((0.0, 0.075, 0.145) if row % 2 == 0 else (0.038, 0.110)):
            a = HO + V(x, y - 0.8 * x * x - 0.010, z)
            out.append((a, nrm(V(0.30 * x / 0.15, 0.10 - 0.30 * row, -1.0)), ln * 1.15, r * 0.85))   # 살갗을 따라 납작하게 눕는다
    # 목덜미 · 등 위 · 어깨 — 아래로 흘러내린다(옆 75°까지)
    for row, (y, rad, ln, r) in enumerate(((HO[1] - 0.05, 0.190, 0.26, 0.062), (HO[1] - 0.15, 0.200, 0.28, 0.066), (TOP + 0.08, 0.205, 0.27, 0.068), (TOP - 0.03, 0.215, 0.23, 0.066))):
        n = 7 + row
        for i in range(n):
            th = np.radians(-75 + 150 * (i + (0.5 if row % 2 else 0.0)) / (n - 0.5))
            if abs(th) > np.radians(75): continue
            zc = HO[2] - 0.17 - 0.03 * row
            a = V(rad * np.sin(th), y, zc - rad * 0.75 * np.cos(th))
            o = V(np.sin(th), 0, -np.cos(th) * 0.8)
            out.append((a, nrm(o * 0.30 + V(0, -1.0, -0.15)), ln * (1.0 + 0.15 * np.sin(1.7 * i + row)), r))
    return out
def _fur_tufts(): return _crest_tufts()   # v5 — 볏만(사용자: *"v2 갈기가 좋다 · 늑대인간 갈기 말고 등까지 틈만 없애면"*) · `_mane_tufts_wolf`는 v4에서 써 본 것
FUR_T = None
CREST_CUT = None   # 이 높이 아래의 볏 털은 등에 선다(`crest_back`) — 웃옷이 덮는다
def _tufts_d(p, T, d):
    m = mir(p)
    for a, dr, ln, r in T:
        n = nrm(np.cross(dr, nrm(V(a[0] + 1e-3, a[1] - HO[1] + 0.05, 0.3 * (a[2] - HO[2])))))
        d = smin(d, tuft(m, a, dr, ln, r, n=n, k=0.50), 0.034)                       # 밑동을 녹이고 끝은 따로 선다(결이 보인다)
    return d
def _tufts():
    global FUR_T
    if FUR_T is None: FUR_T = _fur_tufts()
    cut = TOP - 0.02
    return [t for t in FUR_T if t[0][1] >= cut], [t for t in FUR_T if t[0][1] < cut]
def fur(p):
    """털 껍데기 + 정수리 · 목덜미 볏(어깨선 위) — 등에 선 볏은 `crest_back`(웃옷이 없을 때만 · 게임 `Top.None`)."""
    return _tufts_d(p, _tufts()[0], fur_shell(p))
def crest_back(p): return _tufts_d(p, _tufts()[1], far(p))
def spots(p):
    """하이에나 점박이 — 세 사인 곱의 양의 봉우리만 짙은 점으로."""
    x, y, z = p[..., 0], p[..., 1], p[..., 2]
    a = np.sin(x * 26 + 1.1 + 2 * np.sin(z * 13)) * np.sin(y * 22 + 0.6 + 1.5 * np.sin(x * 11)) * np.sin(z * 24 + 0.3 + 1.5 * np.sin(y * 9))
    return sstep(0.20, 0.36, a)
def stripes(p):
    """줄무늬 — 몸통 옆구리는 세로줄(앞뒤로 늘어선다) · 다리(엉덩이 아래)는 가로줄."""
    x, y, z = p[..., 0], p[..., 1], p[..., 2]
    body = sstep(0.55, 0.80, np.sin(z * 46 + 2.0 * np.sin(y * 9) + 1.0 + 0.8 * np.sin(x * 7)))   # v20 — 가늘고 촘촘하게
    leg = sstep(0.55, 0.80, np.sin(y * 52 + 3.0 * z + 0.5))
    k = sstep(D['lift'] + 0.02, D['lift'] - 0.06, y)
    return body * (1 - k) * COAT['body_stripes'] + leg * k
def pattern(p):
    """털 무늬 한 벌 — 털 벌(`COAT`)이 점 · 줄 중 무엇을 얼마나 쓰는지 정한다(게임에서는 팔레트 덧칠 칸 둘)."""
    return np.maximum(spots(p) * COAT['spots'], stripes(p) * COAT['stripes'])
def mantle(p):
    """목 · 어깨를 두른 밝은 망토 털(갈색 하이에나)."""
    y = p[..., 1]
    return sstep(TOP - 0.24, TOP - 0.10, y) * sstep(HO[1] - 0.06, HO[1] - 0.16, y) * COAT['mantle']   # 머리는 짙게 둔다(v20 — 망토가 머리까지 덮었다)
def fur_col(p):     # 볏은 짙고 · 나머지는 바탕 + 무늬
    x = np.abs(p[..., 0])
    c = mixc(mixc(FUR, SPOT, pattern(p) * 0.85), MANTLE, mantle(p))
    c = mixc(c, FUR_D, sstep(0.07, 0.02, x) * sstep(TOP - 0.35, TOP - 0.25, p[..., 1]) * 0.85)
    return c
def _wolf_fur_col(p):
    q = p - HO; x, y, z = np.abs(q[..., 0]), q[..., 1], q[..., 2]
    face = sstep(-0.05, 0.03, z + 0.10 * (y < 0)) * sstep(0.02, -0.10, y) * sstep(-0.42, -0.20, y)   # 볼 · 턱 밑 · 앞쪽
    c = mixc(FUR, PALE, face * 0.9)
    return mixc(c, FUR_D, np.clip(sstep(-0.05, -0.30, z) * 0.55 + sstep(0.12, 0.26, y) * 0.35, 0, 0.7))
def mane_col(p):    # 아래 · 앞(턱 밑 · 볼)은 희고 · 위 · 뒤로 갈수록 짙다
    y, z = p[..., 1], p[..., 2]
    c = mixc(PALE, FUR, sstep(-0.12, 0.06, y))
    return mixc(c, FUR_D, sstep(0.10, 0.26, y) * 0.6 + sstep(-0.18, -0.38, z) * 0.35)
def ruff(p): return far(p)
def crown(p): return far(p)
def ruff_col(p): return PALE
def teeth(p):       # 송곳니 — 위(크다 · 아래로) · 아래(위로) · 그 사이 작은 이들 · 앞니
    m = mirror(p); d = far(p)
    for z, ln, r in ((0.325, 0.076, 0.020), (0.270, 0.028, 0.012), (0.225, 0.026, 0.011), (0.185, 0.024, 0.011)):
        x = muzzle_w(z) * 0.86; d = np.minimum(d, seg(m, V(x, MY0 + 0.012, z), V(x * 0.98, MY0 - ln, z + 0.004), r, 0.002))
    for z, ln, r in ((0.292, 0.056, 0.016), (0.240, 0.024, 0.011)):
        x = muzzle_w(z) * 0.80; d = np.minimum(d, seg(m, V(x, MY0 - 0.010, z), V(x, MY0 + ln, z - 0.002), r, 0.002))
    for x in (0.016, 0.040):
        z = MZ1 - 0.030 - x * x * 6.0; d = np.minimum(d, seg(m, V(x, MY0 + 0.006, z), V(x, MY0 - 0.020, z + 0.002), 0.010, 0.003))
    return d
def head_col(p):
    """하이에나 — 모래색 머리 · 정수리에 점박이 · 주둥이와 눈 둘레는 검다 · 턱 밑은 밝다."""
    x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
    c = mixc(FUR, SPOT, spots(p + HO) * COAT['spots'] * 0.75 * sstep(MZ0 + 0.02, MZ0 - 0.06, z))
    c = mixc(c, PALE, sstep(MY0 - 0.010, MY0 - 0.050, y) * sstep(MZ0 + 0.10, MZ0 - 0.02, z) * 0.8)
    eye = sstep(0.030, 0.008, length(np.stack([x - EYE_C[0], y - EYE_C[1], z - EYE_C[2]], -1)) - EYE_R)
    return mixc(c, MUZ, np.maximum(sstep(MZ0 + 0.09, MZ0 + 0.17, z), eye * 0.45))   # v2 — 검은 것은 주둥이 앞쪽만(v1은 얼굴이 검은 상자)
def ear_col(p): return mixc(FUR, FUR_D, sstep(0.27, 0.33, p[..., 1]))

def head_pieces():
    def hd(f, r=0.72): return lambda p: bounded(lambda q: f(q - HO), p, HO + V(0, 0.06, 0.10), r)
    def hc(c): return (lambda p: c(p - HO)) if callable(c) else c
    return [(hd(skin), hc(head_col)), (hd(inner), MOUTH), (hd(nose), NOSE), (hd(eyeball), EYE), (hd(pupil), DARK), (hd(glint), (0.98, 0.98, 0.95)),
            (hd(liner), DARK), (hd(lid), hc(head_col)), (hd(ear), hc(ear_col)), (hd(ear_in), EAR_IN), (hd(teeth), BONE)]

# ── 몸 ───────────────────────────────────────────────────────────────────
def torso(p):
    y = p[..., 1] - D['lift']; t = y / D['torsoH']
    r = bd.torso_r(D['half'], t, D['torsoH'], D['topR'])
    q = np.sqrt(p[..., 0] ** 2 + (p[..., 2] / D['depth']) ** 2)
    return np.maximum((q - r) * D['depth'], np.maximum(-y, y - D['torsoH']))
def neck(p): return seg(p, V(0, TOP - 0.06, 0.0), HO + V(0, -0.14, -0.06), 0.150, 0.135)
def arm_pts():
    r = D['armR']; sh = V(D['armX'], D['shY'], 0.0); wr = V(D['armX'] + 0.015, D['handY'] + 0.05, 0.075)
    el = sh + (wr - sh) * 0.50 + V(0.025, 0.0, -0.050)
    return sh, el, wr, r
def arms(p):
    q = mir(p); sh, el, wr, r = arm_pts(); ll = float(np.linalg.norm(wr - el))
    d = smin(seg(q, sh, el, r * 1.02, r * 0.86), seg(q, el, wr, r * 0.88, r * 0.66), 0.04)
    d = smin(d, sph(q, sh + V(0.010, 0.010, 0), r * 1.18), 0.05)                                        # 어깨
    return smin(d, ell(q, el + (wr - el) * 0.32, V(r * 1.00, ll * 0.36, r * 1.02)), 0.05)              # 팔뚝
def arm_fur(p): return np.minimum(arm_fur_upper(p), arm_fur_lower(p))
def arm_fur_upper(p): return _arm_tufts(p, ((0.02, 0.105), (0.26, 0.095)))   # 팔꿈치 쪽 — 늘 보인다
def arm_fur_lower(p): return _arm_tufts(p, ((0.50, 0.080),))                    # 팔뚝 가운데 — 팔 붕대가 덮는다(게임 `WrapArm.Off`일 때만)
def _arm_tufts(p, specs):     # 팔뚝 바깥 · 팔꿈치의 털 술 — 뒤 · 아래로
    q = mir(p); sh, el, wr, r = arm_pts(); d = far(p)
    for t, ln in specs:
        a = el + (wr - el) * t + V(r * 0.30, 0, -r * 0.62); d = np.minimum(d, tuft(q, a, (0.25, -0.60, -0.76), ln, r * 0.58, n=(1, 0, 0), k=0.62))
    return d
def fingers():
    """손가락 셋 + 엄지(오른손 · 거울 · +x 바깥) — 손바닥이 몸 쪽을 보고 약간 뒤로 돌아 있다(공룡 악어 v16과 같은 손)."""
    _, _, wr, r = arm_pts(); h = D['hand'] * 0.5; c = wr + V(0, -h * 0.56, 0.0)
    a_ = np.radians(-30.0); ca, sa = np.cos(a_), np.sin(a_); R = np.array([[ca, 0, sa], [0, 1, 0], [-sa, 0, ca]], np.float32)
    T = lambda v: c + R @ (np.asarray(v, np.float32) - c); fs = []
    for dz, k in ((-0.58, 0.92), (0.0, 1.06), (0.58, 0.94)):
        a = c + V(0, -h * 0.46, dz * h * 0.58); m1 = a + V(-h * 0.08, -h * 0.52 * k, dz * h * 0.10); m2 = m1 + V(-h * 0.16, -h * 0.36 * k, dz * h * 0.04)
        fs.append((a, m1, m2, m2 + V(-h * 0.18, -h * 0.22 * k, 0)))
    a = c + V(-h * 0.20, -h * 0.02, h * 0.66); m1 = a + V(-h * 0.20, -h * 0.32, h * 0.04); m2 = m1 + V(-h * 0.10, -h * 0.24, -h * 0.04)
    fs.append((a, m1, m2, m2 + V(-h * 0.04, -h * 0.14, -h * 0.06)))
    return c, h, R, [tuple(T(v) for v in f) for f in fs]
def hands(p):
    q = mir(p); c, h, R, fs = fingers(); _, _, wr, r = arm_pts()
    ql = c + (q - c) @ R
    d = smin(ell(ql, c, V(h * 0.46, h * 0.70, h * 0.80)), ell(q, (wr + c) * 0.5, V(r * 0.56, h * 0.50, r * 0.62)), 0.05)
    for a, m1, m2, t in fs:
        d = smin(d, union([seg(q, a, m1, h * 0.24, h * 0.21), seg(q, m1, m2, h * 0.21, h * 0.18), seg(q, m2, t, h * 0.18, h * 0.14)]), 0.03)
    return d
def hand_claws(p):
    q = mir(p); _, h, _, fs = fingers(); d = far(p)
    for a, m1, m2, t in fs:
        dr = nrm(t - m2); c1 = t + dr * h * 0.26 + V(0, -h * 0.05, 0)
        d = np.minimum(d, np.minimum(seg(q, t - dr * h * 0.04, c1, h * 0.13, h * 0.08), seg(q, c1, c1 + nrm(dr + V(0, -0.8, 0)) * h * 0.22, h * 0.08, 0.004)))
    return d
# 역관절 다리 — 엉덩이 → 무릎(앞) → 뒤꿈치(뒤 · 위) → 발볼. 사람 치비 다리 자리(`legX`) 그대로.
def leg_pts():
    L, r, x = D['lift'], D['legR'], D['legX']
    return (V(x, D['hipY'], 0.0), V(x, L * 0.60, 0.135), V(x, L * 0.27, -0.105), V(x, 0.050, 0.045)), r
def legs(p):
    (h, k, a, b), r = leg_pts(); q = mir(p); lt = float(np.linalg.norm(k - h)); ls = float(np.linalg.norm(a - k))
    d = seg(q, h, k, r * 1.16, r * 0.92)
    d = smin(d, ell(q, h + (k - h) * 0.48 + V(0, 0, r * 0.22), V(r * 0.98, lt * 0.44, r * 1.06)), 0.05)   # 넓적다리
    d = smin(d, seg(q, k, a, r * 0.84, r * 0.50), 0.04)                                                # 정강이 — 뒤꿈치로 가늘어진다
    d = smin(d, ell(q, k + (a - k) * 0.30 + V(0, r * 0.08, -r * 0.26), V(r * 0.70, ls * 0.30, r * 0.74)), 0.04)   # 장딴지
    d = smin(d, sph(q, a, r * 0.54), 0.03)                                                             # 뒤꿈치 마디
    return smin(d, seg(q, a, b, r * 0.48, r * 0.54), 0.03)                                             # 발등뼈
def thigh_fur(p):   # 넓적다리 뒤 털 술 — 바지를 입으면 뺀다(v12 — 바지를 뚫고 검은 가시처럼 나왔다)
    (h, k, a, b), r = leg_pts(); q = mir(p)
    return tuft(q, h + (k - h) * 0.55 + V(0, 0, -r * 0.95), (0.0, -0.55, -0.83), 0.110, r * 0.60, n=(1, 0, 0), k=0.62)
def heel_fur(p):    # 뒤꿈치 털 술
    (h, k, a, b), r = leg_pts(); q = mir(p)
    return tuft(q, a + V(0, 0.01, -r * 0.40), (0.0, -0.30, -0.95), 0.085, r * 0.46, n=(1, 0, 0), k=0.62)
def leg_fur(p): return np.minimum(thigh_fur(p), heel_fur(p))
TOES = ((-0.050, -0.088, 0.150), (0.0, 0.0, 0.180), (0.050, 0.088, 0.150))
def toe_pts():
    (_, _, a, b), r = leg_pts(); out = []
    for x0, x1, z in TOES:
        base = b + V(x0, 0.004, 0.025); tip = b + V(x1, -0.014, z); out.append((base, base + (tip - base) * 0.52 + V(0, 0.026, 0), tip))
    return b, out
def feet(p):
    b, toes = toe_pts(); q = mir(p)
    d = ell(q, b + V(0, 0.0, -0.005), V(0.086, 0.046, 0.078))
    for base, kn, tip in toes: d = smin(d, np.minimum(seg(q, base, kn, 0.040, 0.036), seg(q, kn, tip, 0.036, 0.028)), 0.028)
    return d
def foot_claws(p):
    b, toes = toe_pts(); q = mir(p); d = far(p)
    for base, kn, tip in toes:
        dr = nrm(V(float(tip[0] - kn[0]), 0, float(tip[2] - kn[2]))); c0 = tip + V(0, 0.012, 0) - dr * 0.010; c1 = c0 + dr * 0.055 + V(0, 0.008, 0)
        d = np.minimum(d, np.minimum(seg(q, c0, c1, 0.030, 0.020), seg(q, c1, c1 + dr * 0.045 + V(0, -0.034, 0), 0.020, 0.004)))
    return d
# 꼬리 — 풍성하다: 엉덩이 뒤에서 뒤 · 아래로 휘어 내려와 끝이 살짝 들린다
TAIL = [V(0, D['lift'] + 0.10, -0.13), V(0, D['lift'] + 0.04, -0.26), V(0, D['lift'] - 0.08, -0.35), V(0, D['lift'] - 0.20, -0.40), V(0, D['lift'] - 0.30, -0.42)]   # 코볼트 — 짧다
TAIL_R = [0.045, 0.045, 0.060, 0.072, 0.020]
def tail(p):
    d = far(p)
    for i in range(len(TAIL) - 1): d = smin(d, seg(p, TAIL[i], TAIL[i + 1], TAIL_R[i], TAIL_R[i + 1]), 0.04)
    return d
def tail_col(p): return mixc(FUR, FUR_D, sstep(D['lift'] - 0.04, D['lift'] - 0.14, p[..., 1]))   # 끝이 검은 술
# 목 둘레 갈기 — 어깨 · 가슴 위로 늘어진 털 술 고리(머리와 몸의 이음을 덮는다)
def collar(p):
    d = far(p); m = mir(p); y = TOP - 0.02
    for ang, ln in ((0.0, 0.150), (0.55, 0.135), (1.10, 0.125), (1.75, 0.130), (2.45, 0.150), (3.14, 0.170)):
        o = V(np.sin(ang), 0, np.cos(ang)); a = V(0, y, -0.02) + o * 0.135
        d = np.minimum(d, tuft(m, a, nrm(o * 0.55 + V(0, -0.83, 0)), ln, 0.074, n=np.cross(o, UP), k=0.66))
    return d
def collar_col(p): return mixc(FUR, PALE, sstep(0.02, 0.10, p[..., 2]) * 0.6)
def body_col(p):
    """몸 털 색 — 가슴 · 배 · 팔다리 안쪽은 밝다."""
    x, z = np.abs(p[..., 0]), p[..., 2]
    chest = sstep(0.01, 0.07, z) * sstep(0.20, 0.12, x) * sstep(D['lift'] - 0.05, D['lift'] + 0.10, p[..., 1])
    c = mixc(mixc(FUR, SPOT, pattern(p) * 0.85 * (1 - chest)), PALE, chest * 0.7)
    return mixc(c, MANTLE, mantle(p))
def _flat_top(p, d):
    """바지 윗단을 일자로(v14 — 사용자: *"바지 위쪽이 U자 · 일자였으면"*): 허리에 곧은 띠를 둘러 골반 통과 잇고 허리선에서 수평으로 자른다."""
    # v15 — 띠는 몸에 붙게 얇게(0.034는 몸보다 두꺼워 부자연스러웠다) · 윗단은 앞이 아주 살짝 내려가는 완만한 허리선(완전 일자도 어색했다)
    y = p[..., 1]
    edge = D['hem'] - 0.016 * sstep(0.0, 0.18, p[..., 2]) + 0.006 * sstep(0.10, 0.22, np.abs(p[..., 0]))
    band = np.maximum(_wear_base(p) - 0.017, np.maximum(y - edge, (D['hem'] - 0.10) - y))
    return smax(smin(d, band, 0.03), y - edge, 0.006)
def pants(p):       # 찢어진 반바지 — 허리띠 아래 몸통 + 골반 + 넓적다리(무릎 위까지) 한 벌. v2는 넓적다리 통만 있어 속옷 같았다(사용자: *"팬티 같네"*)
    (h, k, a, b), r = leg_pts(); q = mir(p)
    thigh = seg(q, h, k, r * 1.16, r * 0.92) - 0.034   # 넓적다리 살(근육 덩이)이 뚫고 나오지 않게 넉넉히
    cut = (q - (h + (k - h) * 0.84)) @ nrm(k - h) + 0.020 * np.sin(q[..., 0] * 55.0 + q[..., 2] * 43.0)   # 밑단이 들쭉날쭉
    hips = ell(p, V(0, D['lift'] + 0.03, 0.015), V(D['legX'] + r * 1.18, 0.125, D['half'][1] * D['depth'] + 0.008))   # v12 — 몸에 맞게(엉덩이가 뒤로 부풀었다)   # 골반 — 두 통 사이를 메운다
    top = np.maximum(_wear_base(p) - 0.016, p[..., 1] - D['hem'])   # v11 — 등 털 덩이까지 감싼다(몸통만 감싸면 덩이가 허리 뒤를 뚫고 나왔다)
    return _flat_top(p, smin(np.minimum(top, np.maximum(hips, p[..., 1] - D['hem'])), np.maximum(thigh, cut), 0.03))

# ── 옷 — 개체마다 고르는 축(v7 — 사용자: *"천 옷은 별로 · 반바지만 · 가죽 조끼 · 일반 옷 — 다양하게"*) ──
# v9 — **넝마 조각**(사용자: *"옷이 너무 디테일이 없어 · 네모 조각 느낌"* + 보낸 그림 넷 `_refs/2026-11-19_kobold/`):
# 감은 붕대(팔뚝 · 정강이) · 허리 띠천 + 고리 버클 · 길이와 색이 제각각인 넝마 띠 · 조각을 꿰매고 징을 박은 가죽 조끼(해진 밑단) ·
# 한쪽 어깨의 겹친 쇠 조각 + 가슴을 가로지르는 끈. 게임에서는 조각마다 고르기 축(`Spine.Pick.Outfit.*`)이 될 자리.
VEST = (0.42, 0.26, 0.15); VEST_D = (0.27, 0.16, 0.09); SHIRT = (0.60, 0.54, 0.42); PANTS2 = (0.30, 0.30, 0.36)
LINEN = (0.78, 0.72, 0.57); LINEN_D = (0.50, 0.44, 0.33); SASH = (0.24, 0.20, 0.17); BRASS = (0.76, 0.60, 0.30)
IRON = (0.34, 0.34, 0.36); IRON_D = (0.20, 0.19, 0.20); STRAP = (0.30, 0.19, 0.12)
RAGS = [(0.52, 0.17, 0.14), (0.40, 0.40, 0.24), (0.44, 0.31, 0.20), (0.66, 0.60, 0.46), (0.36, 0.13, 0.12)]
def _wear_base(p): return smin(torso(p), _back_fur(p), 0.04)
def _hash(c): return np.modf(np.abs(np.sin(c @ np.array([12.9898, 78.233, 37.719], np.float32)) * 43758.5453))[0]

# 붕대 — 팔다리 마디에 비스듬히 감긴 띠(띠 가장자리에 골) · 모양 · 색이 같은 위상을 쓴다
def _wrap_frame(a, b):
    ax = nrm(b - a); u = nrm(np.cross(ax, V(1, 0, 0) if abs(ax[0]) < 0.9 else UP)); return ax, u, np.cross(ax, u), float(np.linalg.norm(b - a))
def _wrap_phase(q, a, b, pitch):
    ax, u, v, L = _wrap_frame(a, b); pa = q - a; s = pa @ ax
    ph = np.modf(np.abs(s / pitch + np.arctan2(pa @ v, pa @ u) / (2 * np.pi) * 0.7 + 50))[0]
    return s, L, 1 - sstep(0.0, 0.16, np.minimum(ph, 1 - ph))
def _wrap(q, a, b, ra, rb, t0, t1, pitch=0.034):
    s, L, g = _wrap_phase(q, a, b, pitch)
    d = np.maximum(seg(q, a, b, ra, rb), np.maximum(t0 * L - s, s - t1 * L))
    return d + 0.005 * g
def arm_wraps(p):
    q = mir(p); sh, el, wr, r = arm_pts()
    return _wrap(q, el, wr + (wr - el) * 0.08, r * 1.00 + 0.016, r * 0.70 + 0.014, 0.40, 1.0)
def arm_wraps_col(p):
    q = mir(p); sh, el, wr, r = arm_pts(); return mixc(LINEN, LINEN_D, _wrap_phase(q, el, wr + (wr - el) * 0.08, 0.034)[2] * 0.8 + 0.2 * _hash(np.floor(p * 40)))
def shin_wraps(p):
    q = mir(p); (h, k, a, b), r = leg_pts()
    return _wrap(q, k, a, r * 0.72 + 0.018, r * 0.52 + 0.016, 0.38, 0.92)
def shin_wraps_col(p):
    q = mir(p); (h, k, a, b), r = leg_pts(); return mixc(LINEN, LINEN_D, _wrap_phase(q, k, a, 0.034)[2] * 0.8 + 0.2 * _hash(np.floor(p * 40)))

# 허리 띠천 + 고리 버클
BELT_Y = D['hem'] - 0.030
def sash(p):
    sag = 0.020 * np.clip(p[..., 2] / 0.2, 0, 1)                                       # 앞이 조금 처진다
    return np.maximum(_wear_base(p) - 0.026, np.abs(p[..., 1] - BELT_Y + sag) - 0.034)
def _front_z(y):
    lo, hi = 0.0, 0.5
    for _ in range(30):
        c = 0.5 * (lo + hi)
        if torso(np.array([[0, y, c]], np.float32))[0] < 0: lo = c
        else: hi = c
    return lo
RING_C = V(0, BELT_Y - 0.020, _front_z(BELT_Y - 0.020) + 0.034)
def ring(p):
    q = p - RING_C; a = length(q[..., :2]) - 0.034
    return np.sqrt(a * a + q[..., 2] ** 2) - 0.009

# 넝마 띠 — 허리 둘레에서 길이 · 너비 · 색이 제각각 · 끝은 찢겨 뾰족하다
def _strips():
    rng = np.random.default_rng(3); out = []
    for th in np.radians(np.linspace(-150, 150, 17)) + rng.uniform(-0.08, 0.08, 17):
        if abs(th) > np.radians(100) and rng.uniform() < 0.4: continue              # 뒤는 듬성듬성(꼬리 자리)
        T = V(np.sin(th) * 0.245, BELT_Y - 0.035, np.cos(th) * (0.215 if np.cos(th) > 0 else 0.24))
        Dn = nrm(V(np.sin(th) * 0.22, -1, np.cos(th) * 0.22)); W = nrm(V(np.cos(th), 0, -np.sin(th)))
        L = float(rng.uniform(0.17, 0.36)) * (1.15 if abs(th) < 0.5 else 1.0)
        out.append((T, Dn, W, nrm(np.cross(W, Dn)), L, float(rng.uniform(0.030, 0.050)), int(rng.integers(len(RAGS)))))
    return out
STRIPS = _strips()
def _strip_d(p, T, Dn, W, N, L, wd):
    q = p - T; s = q @ Dn; w = q @ W; n = q @ N - 0.6 * w * w + 0.010 * np.sin(s * 30 + w * 20)
    jag = L + 0.035 * np.sin(w * 95 + L * 40) - 0.02
    wd_s = wd * (1 - 0.55 * sstep(0.55 * L, jag, s))
    return np.maximum(np.maximum(np.abs(n) - 0.006, np.abs(w) - wd_s), np.maximum(-s, s - jag))
def rags(p):
    d = far(p)
    for T, Dn, W, N, L, wd, ci in STRIPS: d = np.minimum(d, _strip_d(p, T, Dn, W, N, L, wd))
    return d
def rags_col(p):
    best = far(p); col = np.zeros(p.shape, np.float32)
    for T, Dn, W, N, L, wd, ci in STRIPS:
        d = _strip_d(p, T, Dn, W, N, L, wd); m = d < best; best = np.where(m, d, best); col[m] = RAGS[ci]
    s = sstep(BELT_Y - 0.05, BELT_Y - 0.40, p[..., 1])                                  # 끝으로 갈수록 때가 탄다
    return col * (1 - 0.25 * s[..., None])

# 가죽 조끼 — 조각을 꿰매 붙였다(조각마다 톤) · 꿰맨 선 · 징 · 해진 밑단 · V 앞섶
def vest(p):
    y = p[..., 1]; base = _wear_base(p)
    hem = (D['hem'] - 0.05) + 0.022 * np.sin(p[..., 0] * 47 + p[..., 2] * 31) + 0.012 * np.sin(p[..., 0] * 113)
    d = smax(np.maximum(base - 0.024, hem - y), y - (TOP + 0.05) - 0.5 * np.abs(p[..., 0]) ** 2, 0.05)
    w = 0.030 + 0.11 * np.clip((y - D['hem']) / (TOP - D['hem']), 0, 1)
    d = np.maximum(d, -np.maximum(np.abs(p[..., 0]) - w, 0.04 - p[..., 2]))
    return np.maximum(d, -(neck(p) - 0.022))
def vest_col(p):
    g = np.stack([p[..., 0] * 7.0 + 0.3 * np.sin(p[..., 1] * 13), p[..., 1] * 6.0, p[..., 2] * 7.0], -1)
    cell = np.floor(g); f = g - cell
    c = mixc(VEST, VEST_D, _hash(cell) * 0.75)
    e = np.minimum(np.minimum(f[..., 0], 1 - f[..., 0]), np.minimum(f[..., 1], 1 - f[..., 1]))
    c = mixc(c, (0.12, 0.08, 0.05), sstep(0.07, 0.03, e) * (np.sin(g[..., 0] * 40 + g[..., 1] * 40) > -0.2))   # 꿰맨 선(끊긴 땀)
    st = length(np.stack([np.minimum(f[..., 0], 1 - f[..., 0]) - 0.16, np.minimum(f[..., 1], 1 - f[..., 1]) - 0.16], -1))
    return mixc(c, BRASS, sstep(0.07, 0.04, st))                                          # 조각 모서리마다 징

# 어깨 쇠 조각(한쪽 · +x) + 가슴을 가로지르는 끈
def _rbox(q, b, r):
    k = np.abs(q) - b; return length(np.maximum(k, 0)) + np.minimum(np.max(k, -1), 0) - r
def _plates():
    sh, el, wr, r = arm_pts(); R = r * 1.18 + 0.030; out = []
    for row, elev in enumerate((8, 34, 58, 82)):                                        # v10 — 크게 · 네 줄 · 위팔까지(v9는 작은 회색 덩이)
        for az in ((-50, -5, 40) if row % 2 == 0 else (-28, 18)):
            e, a = np.radians(elev), np.radians(az + 9 * row)
            n = nrm(V(np.sin(e), np.cos(e) * np.cos(a), np.sin(a) * 0.9))
            c = sh + n * (R + 0.010 * row) + V(0.012 * row, -0.020 * row, 0)
            fwd = nrm(np.cross(n, V(0, 0, 1)))
            tilt = nrm(fwd * 0.97 + n * 0.25)                                            # 아래 줄이 위 줄 밑으로 겹친다
            out.append((c, frame(tilt, n), row))
    return out
PLATES = _plates()
PL_B = V(0.064, 0.008, 0.058)
def pauldron(p):
    d = far(p)
    for c, Rm, row in PLATES: d = np.minimum(d, _rbox((p - c) @ Rm, PL_B, 0.004))
    return d
def pad_base(p):
    """쇠 조각 밑 가죽 덮개 — 어깨를 감싸 조각 사이 틈을 메운다."""
    sh, el, wr, r = arm_pts(); R = r * 1.18 + 0.022
    d = ell(p, sh + V(0.02, -0.03, 0), V(R * 1.05, R * 1.15, R * 1.10)) - 0.004
    return np.maximum(d, -(sph(p, sh + V(0.02, -0.03, 0), R * 0.98)))
def pauldron_col(p):
    best = far(p); k = np.zeros(p.shape[:-1], np.float32)
    for i, (c, Rm, row) in enumerate(PLATES):
        d = _rbox((p - c) @ Rm, PL_B, 0.004); m = d < best; best = np.where(m, d, best); k = np.where(m, (i * 0.37) % 1.0, k)
    c = mixc(IRON, IRON_D, k * 0.7)
    return mixc(c, (0.46, 0.26, 0.14), sstep(0.55, 0.85, fluff(p * 6.0)) * 0.6)         # 녹
def _on_wear(y, dx, dz):
    o = V(0, y, 0); dr = nrm(V(dx, 0, dz)); lo, hi = 0.0, 0.6
    for _ in range(30):
        c = 0.5 * (lo + hi)
        if _wear_base((o + dr * c)[None, :])[0] < 0: lo = c
        else: hi = c
    return o + dr * (lo + 0.012)
def _strap_pts():
    pts = [((TOP - 0.02) + (BELT_Y + 0.03 - (TOP - 0.02)) * t, 0.18 * (1 - 2 * t)) for t in np.linspace(0, 1, 9)]   # +x 어깨 → -x 옆구리
    return [_on_wear(y, x, 0.20) for y, x in pts], [_on_wear(y, x, -0.20) for y, x in pts]
STRAP_F, STRAP_B = _strap_pts()
def strap(p):
    d = far(p)
    for P in (STRAP_F, STRAP_B):
        for a, b in zip(P, P[1:]): d = np.minimum(d, seg(p, a, b, 0.014, 0.014))
    return d

# 웃옷 · 긴 바지 — 밑단을 해지게 · 얼룩
def shirt(p):
    sh, el, wr, r = arm_pts(); q = mir(p)
    sleeve = seg(q, sh, sh + (el - sh) * 0.55, r * 1.02 + 0.026, r * 0.90 + 0.022)
    cap = sph(q, sh + V(0.010, 0.010, 0), r * 1.18 + 0.022)                            # v13 — 소매 머리가 어깨 공을 덮는다(어깨가 뚫려 보였다)
    d = smin(smin(_wear_base(p) - 0.020, sleeve, 0.04), cap, 0.04)
    hem = D['hem'] + 0.010 + 0.016 * np.sin(p[..., 0] * 53 + p[..., 2] * 29)            # v13 — 바지 허리선 바로 위까지(길어서 바지를 뚫고 나왔다)
    d = np.maximum(d, hem - p[..., 1])
    d = smax(d, p[..., 1] - (TOP + 0.035) - 0.4 * p[..., 0] ** 2, 0.03)                  # v13 — 등판은 어깨선에서 끊는다(목덜미까지 올라가 뒷목이 안 보였다)
    return np.maximum(d, -(neck(p) - 0.018))
def shirt_col(p): return mixc(SHIRT, (0.42, 0.37, 0.28), np.clip(_hash(np.floor(p * 9)) * 0.5 + sstep(0.5, 0.9, fluff(p * 4)) * 0.4, 0, 1))
def pants_long(p):
    (h, k, a, b), r = leg_pts(); q = mir(p); ls = float(np.linalg.norm(a - k))
    thigh = seg(q, h, k, r * 1.16, r * 0.92) - 0.034
    shin = smin(seg(q, k, a, r * 0.84 + 0.014, r * 0.50 + 0.014),                       # v12 — 장딴지 통을 몸에 맞게(3~5cm 부풀려 뒤로 튀어나왔다)
                ell(q, k + (a - k) * 0.30 + V(0, r * 0.08, -r * 0.26), V(r * 0.70 + 0.012, ls * 0.30 + 0.008, r * 0.74 + 0.012)), 0.02)
    cut = (q - (k + (a - k) * 0.45)) @ nrm(a - k) + 0.020 * np.sin(q[..., 0] * 61.0 + q[..., 2] * 37.0)
    hips = ell(p, V(0, D['lift'] + 0.03, 0.015), V(D['legX'] + r * 1.18, 0.125, D['half'][1] * D['depth'] + 0.008))   # v12 — 몸에 맞게(엉덩이가 뒤로 부풀었다)
    top = np.maximum(_wear_base(p) - 0.016, p[..., 1] - D['hem'])   # v11 — 등 털 덩이까지 감싼다(몸통만 감싸면 덩이가 허리 뒤를 뚫고 나왔다)
    return _flat_top(p, smin(np.minimum(top, np.maximum(hips, p[..., 1] - D['hem'])), np.maximum(smin(thigh, shin, 0.04), cut), 0.03))
def pants_col(p): return mixc(PANTS2, (0.20, 0.20, 0.24), sstep(0.5, 0.9, fluff(p * 4)) * 0.5)

# 넝마 웃옷(v13 — 사용자: *"웃옷도 반바지 느낌에 맞춰 넝마 스타일도 하나"*) — 한쪽 소매는 뜯겨 나갔고(+x만 남는다 · 끝이 찢겼다) ·
# 밑단은 비스듬하고 들쭉날쭉 · 구멍 몇 개(속 털이 보인다) · 덧댄 조각 · 때
def _rag_holes():
    out = []
    for y, dx, dz, rad in ((TOP - 0.16, 0.10, 1.0, 0.030), (D['hem'] + 0.10, -0.55, 1.0, 0.024), (TOP - 0.24, 0.60, -1.0, 0.034), (D['hem'] + 0.06, -0.20, -1.0, 0.022)):
        out.append((_on_wear(y, dx, dz) - nrm(V(dx, 0, dz)) * 0.004, rad))
    return out
RAG_HOLES = _rag_holes()
def rag_shirt(p):
    sh, el, wr, r = arm_pts()
    ax = nrm(el - sh); s_ = (p - sh) @ ax
    sleeve = seg(p, sh, sh + (el - sh) * 0.48, r * 1.02 + 0.024, r * 0.92 + 0.020)       # +x 소매만
    sleeve = np.maximum(sleeve, s_ - (0.36 * float(np.linalg.norm(el - sh)) + 0.025 * np.sin(p[..., 2] * 90 + p[..., 0] * 40)))
    cap = sph(p, sh + V(0.010, 0.010, 0), r * 1.18 + 0.020)
    d = smin(smin(_wear_base(p) - 0.018, sleeve, 0.04), cap, 0.04)
    x, z = p[..., 0], p[..., 2]
    hem = D['hem'] + 0.030 + 0.045 * x + 0.030 * np.abs(np.sin(x * 29 + z * 17)) + 0.015 * np.sin(x * 83 - z * 41)
    d = np.maximum(d, hem - p[..., 1])
    d = smax(d, p[..., 1] - (TOP + 0.035) - 0.4 * x ** 2, 0.03)
    for c, rad in RAG_HOLES: d = np.maximum(d, -(sph(p, c, rad) + 0.006 * np.sin(p[..., 1] * 140)))
    return np.maximum(d, -(neck(p) - 0.018))
RAG_SHIRT = (0.56, 0.50, 0.38)
def rag_shirt_col(p):
    g = np.floor(np.stack([p[..., 0] * 6 + 0.4 * np.sin(p[..., 1] * 11), p[..., 1] * 5, p[..., 2] * 6], -1)); h = _hash(g)
    c = mixc(RAG_SHIRT, (0.40, 0.35, 0.26), sstep(0.5, 0.9, fluff(p * 4)) * 0.6)
    c = mixc(c, (0.46, 0.30, 0.22), (h > 0.86).astype(np.float32) * 0.8)                   # 덧댄 조각
    return mixc(c, (0.30, 0.25, 0.18), sstep(D['hem'] + 0.10, D['hem'] + 0.02, p[..., 1]) * 0.5)   # 밑단의 때

# 웃옷 × 아랫도리 × 붕대 — 따로 고른다(게임에서는 조각마다 고르기 축 — `Spine.Pick.Top.*` · `.Bottom.*` · `.WrapArm` · `.WrapShin`)
TOPS = {'none': lambda: [],
        'vest': lambda: [(vest, vest_col)],
        'shirt': lambda: [(shirt, shirt_col)],
        'rag_shirt': lambda: [(rag_shirt, rag_shirt_col)],
        'pauldron': lambda: [(pauldron, pauldron_col), (pad_base, STRAP), (strap, STRAP)]}
# 긴 바지 허리띠(v16 — 사용자: *"허리띠가 바지에 가려진다"* — 넝마 띠천은 바지 허리 띠와 같은 자리라 묻혔다): 바지보다 바깥 · 허리선을 따라 앞이 살짝 내려간다 · 놋쇠 네모 버클
def _belt_y(p): return D['hem'] - 0.034 - 0.016 * sstep(0.0, 0.18, p[..., 2])
def pants_belt(p): return np.maximum(pants_long(p) - 0.011, np.abs(p[..., 1] - _belt_y(p)) - 0.019)   # v17 — 바지 겉면을 따라 두른다(몸통 기준이면 양옆 골반 · 넓적다리가 덮었다)
BELT_L = (0.36, 0.23, 0.14); BELT_D = (0.20, 0.13, 0.08)
def pants_belt_col(p):
    """닳은 가죽(v18 — 사용자: *"색이나 무늬만 조절"*) — 긁힌 얼룩 · 위아래 가장자리 꿰맨 땀 · 군데군데 징."""
    v = p[..., 1] - _belt_y(p); ang = np.arctan2(p[..., 0], p[..., 2])
    c = mixc(BELT_L, BELT_D, np.clip(sstep(0.45, 0.85, fluff(p * 5.0)) * 0.7 + 0.15 * _hash(np.floor(p * 60)), 0, 1))
    stitch = sstep(0.004, 0.0015, np.abs(np.abs(v) - 0.012)) * (np.sin(ang * 70) > 0.1)
    c = mixc(c, (0.12, 0.08, 0.05), stitch * 0.9)
    stud = sstep(0.010, 0.006, np.sqrt((np.abs(v) * 1.0) ** 2 + (np.abs(np.sin(ang * 5.5)) * 0.10) ** 2))
    return mixc(c, (0.52, 0.48, 0.40), stud * (np.cos(ang) < 0.92))                     # 버클 둘레는 비운다
IRON_BUCKLE = (0.40, 0.38, 0.34)
def buckle_col(p): return mixc(IRON_BUCKLE, (0.45, 0.25, 0.13), sstep(0.45, 0.80, fluff(p * 9.0)) * 0.7)   # 녹슨 쇠
BUCKLE_C = V(0, D['hem'] - 0.050, _front_z(D['hem'] - 0.050) + 0.032)
def buckle(p):
    q = p - BUCKLE_C; outer = _rbox(q, V(0.030, 0.024, 0.005), 0.003)
    return np.maximum(outer, -_rbox(q, V(0.018, 0.013, 0.02), 0.002))
BOTTOMS = {'shorts': lambda: [(pants, CLOTH)],
           'pants': lambda: [(pants_long, pants_col), (pants_belt, pants_belt_col), (buckle, buckle_col)],
           'rags': lambda: [(sash, SASH), (ring, BRASS), (rags, rags_col)]}
PANTS_ON = ('shorts', 'pants')   # 바지를 입는 아랫도리 — 넓적다리 털 술을 뺀다
OUTFIT = ('none', 'rags', True, True)   # (웃옷, 아랫도리, 팔 붕대, 정강이 붕대)
def outfit_pieces():
    top, bottom, wa, ws = OUTFIT
    P = TOPS[top]() + BOTTOMS[bottom]()
    if wa: P += [(arm_wraps, arm_wraps_col)]
    if ws: P += [(shin_wraps, shin_wraps_col)]
    return P
COMBOS = [('none', 'shorts', False, True), ('none', 'rags', True, True), ('vest', 'rags', True, False), ('pauldron', 'rags', True, True),
          ('rag_shirt', 'shorts', False, True), ('rag_shirt', 'pants', True, False), ('shirt', 'pants', False, True), ('vest', 'pants', True, True)]

def pieces(body=True):
    P = head_pieces()
    if not body:
        cut = HO[1] - 0.55
        return P + [(lambda p: np.maximum(smin(torso(p), neck(p), 0.05), cut - p[..., 1]), body_col), 
                    (lambda p: np.maximum(fur(p), cut - p[..., 1]), fur_col)]
    def flesh(p): return union([smin(smin(torso(p), neck(p), 0.05), arms(p), 0.03), hands(p), legs(p), feet(p)])
    top, bottom, wa, ws = OUTFIT
    # 털 조각 중 옷 · 붕대에 매인 것 — 게임에서도 같은 조건(`bake_kobold.py`): 등 볏은 웃옷이 없을 때 · 팔뚝 · 발목 털 술은 붕대가 없을 때 · 넓적다리 털 술은 넝마 띠일 때
    C = [(fur, fur_col), (arm_fur_upper, FUR_D), (tail, tail_col)]
    if top == 'none': C.append((crest_back, fur_col))
    if not wa: C.append((arm_fur_lower, FUR_D))
    if not ws: C.append((heel_fur, FUR_D))
    if bottom not in PANTS_ON: C.append((thigh_fur, FUR_D))
    return P + [(flesh, body_col)] + C + [(hand_claws, CLAW), (foot_claws, CLAW)] + outfit_pieces()

FULL = ((0, 420), (40, 480), (90, 560), (150, 500)); CLOSE = ((0, 420, 0), (35, 460, 0), (90, 520, 0), (25, 460, 45))
VIEW = (0.98, 2.25)
def job(a):
    global OUTFIT
    mode, i = a
    if mode == 'coat':
        c, yaw = i; set_coat(c); OUTFIT = ('none', 'shorts', False, False)
        return np.asarray(hb.render(pieces(), yaw, 480, 760, *VIEW))
    if mode == 'outfit':
        OUTFIT, yaw = i; return np.asarray(hb.render(pieces(), yaw, 480, 760, *VIEW))
    if mode == 'faces':
        v, (yaw, w, pitch) = i; set_face(v); c = HO + V(0, 0.04, 0.10)
        Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in pieces(body=False)]
        return np.asarray(hb.render(Q, yaw, w, 520, 0.0, 1.10, pitch=pitch))
    if mode == 'full':
        yaw, w = FULL[i]; return np.asarray(hb.render(pieces(), yaw, w, 760, *VIEW))
    if mode == 'human':
        P, _ = bd.figure(bd.MALE, bd.dw.b_box, bd.dw.h_bald, bd.dw.BROWN, bd.dw.e_bushy); return np.asarray(hb.render(P, 40, 380, 760, *VIEW))
    yaw, w, pitch = CLOSE[i]; c = HO + V(0, 0.04, 0.10)
    Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in pieces(body=False)]
    return np.asarray(hb.render(Q, yaw, w, 460, 0.0, 1.25, pitch=pitch))

if __name__ == '__main__':
    from multiprocessing import Pool
    stem = sys.argv[1]
    if len(sys.argv) > 2 and sys.argv[2] == 'faces':   # 얼굴 안 A · B를 나란히(정면 · 비스듬히 · 옆 · 내려다보기)
        views = ((0, 520, 0), (35, 540, 0), (90, 600, 0), (25, 540, 45)); jobs = [('faces', (v, vw)) for v in 'AB' for vw in views]
        with Pool(len(jobs)) as pool: res = pool.map(job, jobs)
        im = Image.new('RGB', (sum(v[1] for v in views), 520 * 2), (40, 40, 44)); dr = ImageDraw.Draw(im)
        for k, t in enumerate(res):
            r, c = divmod(k, len(views)); im.paste(Image.fromarray(t), (sum(v[1] for v in views[:c]), r * 520))
        dr.text((6, 4), 'A', fill=(255, 220, 120)); dr.text((6, 524), 'B', fill=(255, 220, 120))
        im.save(stem + '_faces.png'); print('saved', stem + '_faces.png'); sys.exit()
    if len(sys.argv) > 2 and sys.argv[2] == 'coats':   # 털 벌 여섯 — 비스듬히 앞 · 뒤(옷은 반바지)
        jobs = [('coat', (c, y)) for c in COATS for y in (40, 150)]
        with Pool(len(jobs)) as pool: res = pool.map(job, jobs)
        im = Image.new('RGB', (480 * 4, 760 * 3), (40, 40, 44)); dr = ImageDraw.Draw(im)
        for k, t in enumerate(res): im.paste(Image.fromarray(t), (480 * (k % 4), 760 * (k // 4)))
        for k, c in enumerate(COATS): dr.text((960 * (k % 2) + 6, 760 * (k // 2) + 6), c, fill=(255, 220, 120))
        im.save(stem + '_coats.png'); print('saved', stem + '_coats.png'); sys.exit()
    if len(sys.argv) > 2 and sys.argv[2] == 'outfits':   # 옷 조합(웃옷 × 아랫도리 × 붕대) — 비스듬히 앞 · 뒤
        jobs = [('outfit', (o, y)) for o in COMBOS for y in (40, 150)]
        with Pool(len(jobs)) as pool: res = pool.map(job, jobs)
        im = Image.new('RGB', (480 * len(res), 760), (40, 40, 44)); dr = ImageDraw.Draw(im)
        for k, t in enumerate(res): im.paste(Image.fromarray(t), (480 * k, 0))
        for k, o in enumerate(COMBOS): dr.text((960 * k + 6, 6), '%s + %s%s%s' % (o[0], o[1], ' +arm' if o[2] else '', ' +shin' if o[3] else ''), fill=(255, 220, 120))
        im.save(stem + '_outfits.png'); print('saved', stem + '_outfits.png'); sys.exit()
    print('키(귀 끝) %.2f m · 어깨 %.2f m · 다리 관절 %.2f m' % (HO[1] + float(EAR_B[1]), TOP, D['hipY']))
    jobs = [('full', i) for i in range(len(FULL))] + [('human', 0)] + [('close', i) for i in range(len(CLOSE))]
    with Pool(len(jobs)) as pool: res = pool.map(job, jobs)
    top = res[:len(FULL) + 1]; low = res[len(FULL) + 1:]
    wd = max(sum(t.shape[1] for t in top), sum(t.shape[1] for t in low))
    im = Image.new('RGB', (wd, 760 + 460), (40, 40, 44)); dr = ImageDraw.Draw(im); x = 0
    for i, t in enumerate(top):
        im.paste(Image.fromarray(t), (x, 0))
        if i == len(FULL): dr.text((x + 6, 6), 'human chibi (same scale)', fill=(200, 200, 210))
        x += t.shape[1]
    x = 0
    for t in low: im.paste(Image.fromarray(t), (x, 760)); x += t.shape[1]
    dr.text((6, 4), 'kobold', fill=(255, 220, 120))
    _os.makedirs(_os.path.dirname(_os.path.abspath(stem)), exist_ok=True)
    im.save(stem + '.png'); print('saved', stem + '.png')
