"""뱀파이어 — **사람 치비 몸 그대로 + 사람 머리 표** · 로드 · 노예 × 남 · 여(11-15 사용자).

v7(사용자: *"치비 몸을 그대로 써서 만들어도 되잖아 — 목이랑 몸이랑 떨어져 있게 · 다른 종족 느낌도 아닌데"* ·
*"종류를 두 개로 — 귀족 로드 뱀파이어랑, 순혈이 아니고 물려서 노예가 된 뱀파이어는 저 이미지처럼 · 순혈은 원래 휴먼 얼굴에 창백한 회색 피부톤에 빨간 눈 정도"*) ·
v9(*"여성도 넣고 · 머리카락 너무 별로야 — 이미 만들어 놓은 것들 써도 되잖아 · 로드에 송곳니 · 노예 코를 좀 더 이미지처럼"*):
  - 몸 — 사람 치비 몸 식 그대로(`dwarf/body.py` `figure`와 같은 셈 · 체형 `MALE` / C# `Shapes`의 `Female` 줄) · 목 없이 머리가 몸 위에 떠 있다(`HeadGap`).
  - **로드**(순혈 귀족) — 사람 얼굴 그대로(`human/human.py` — 코 · 둥근 귀 · 눈 판 · 눈썹 · 입) · 창백한 회색 살 · 붉은 눈 · 입선 밑 송곳니 둘.
    **머리카락은 확인받은 원본**(`human/port.py` `family('human')` — 사람 머리에 다시 읽은 것) · 기본 남 `Slick` · 여 `Long` · 후보 판 `hairs`.
    옷: 높이 선 깃(세 갈래) · 안이 붉은 망토 · 남 연미 코트 + 붉은 조끼 · 목 레이스 · 장화 / 여 붉은 코르셋 + 바닥까지 끌리는 치마.
  - **노예**(물려서 된 하급 — 사용자 그림 `_refs/2026-11-15_vampire/thrall_ref.png`, 깃 밖) — 민머리 · 푸른 잿빛 살 · 코와 눈 둘레의 짙은 갈색 탈 ·
    꺼먼 눈두덩 속 희게 빛나는 눈 · 무거운 눈썹뼈 · 이마 · 미간 · 콧등 · 눈 밑 · 팔자 주름 · **세모 박쥐 코**(미간에서 좁게 나와 아래로 넓어지고 ·
    처진 둥근 코끝 · 위로 솟은 큰 콧방울 · 앞에서 보이는 콧구멍 · 콧방울 둘레 골) · 바깥으로 뻗은 뾰족 귀 · 얇은 입술 사이 누런 이.
    옷: 해진 튜닉(여 — 무릎까지 해진 치마) · 새끼줄 허리끈 · 손목 족쇄 · 발싸개.
v1~v6(키 큰 체형 · 목 · 박쥐 얼굴 · 귀부인)은 걷었다 — 커밋 전이라 그림(`renders/vampire_v1~v6`)만 남는다.
단위 m · 발이 원점 · +z 앞. 머리 부품은 머리 중심 원점.
실행: python vampire.py renders/vampire_vN [lord,lordF,thrall,thrallF]   (온몸 넷 + 얼굴 가까이 넷씩 · 같은 눈금의 사람 치비 하나 · 3분 남짓)
      python vampire.py renders/vampire_vN cmp     (노예 얼굴 — 보낸 그림과 나란히)
      python vampire.py renders/vampire_vN hairs   (로드 남 · 여 머리카락 후보 — 정면 · 40°)"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
for _p in (_os.path.join(_here, '..', 'common'), _os.path.join(_here, '..', 'dwarf'), _os.path.join(_here, '..', 'human')):
    if _p not in _sys.path: _sys.path.insert(0, _p)
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, sph, capsule, seg, rbox
import head_base as hb
import body as bd
import human as hm

def nrm(v): v = np.asarray(v, np.float32); return v / np.linalg.norm(v)
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
def mixc(a, b, k): return np.asarray(a, np.float32) * (1 - k[..., None]) + np.asarray(b, np.float32) * k[..., None]
def mir(p): return np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
def flat(p, n, k):
    """법선 n 쪽으로 k배 납작하게 본 좌표 — 돌려준 점으로 잰 거리에 k를 곱해 쓴다(귀)."""
    n = nrm(n); h = (p * n).sum(-1); return p + n * (h / k - h)[..., None]
def bounded(f, p, c, r):
    b = length(p - c) - r; m = b < 0.03
    if not m.any(): return b
    out = b.copy(); out[m] = f(p[m]); return out
def gauss2(x, y, cx, cy, sx, sy): return np.exp(-(((x - cx) / sx) ** 2 + ((y - cy) / sy) ** 2))
def seg2(x, y, a, b):
    """(x, y) 평면에서 선분 a-b까지의 거리 — 얼굴 앞면에 그리는 주름."""
    ax, ay = x - a[0], y - a[1]; bx, by = b[0] - a[0], b[1] - a[1]
    t = np.clip((ax * bx + ay * by) / (bx * bx + by * by), 0, 1)
    return np.hypot(ax - bx * t, ay - by * t), t

# ── 색 ──
PALE = (0.80, 0.80, 0.83)                                           # 로드 — 핏기 없는 회색 살
EYE_RED = (0.84, 0.08, 0.11); DARK = (0.06, 0.04, 0.06); WHITE = (0.98, 0.96, 0.96); LIP_P = (0.56, 0.46, 0.50); FANG = (0.97, 0.95, 0.90)
INK = (0.10, 0.10, 0.14); WINE = (0.30, 0.06, 0.10); DECO = (0.62, 0.52, 0.30)
COAT = (0.12, 0.11, 0.15); VEST = (0.46, 0.07, 0.12); LINING = (0.58, 0.06, 0.10); LACE = (0.93, 0.91, 0.87); SATIN = (0.22, 0.20, 0.27)
GOLD = (0.82, 0.63, 0.24); GEM = (0.75, 0.05, 0.10); PANTS = (0.17, 0.16, 0.20); BOOT = (0.07, 0.06, 0.08)
BLUE = (0.55, 0.63, 0.69); MASK = (0.38, 0.36, 0.34); SOCKET = (0.13, 0.12, 0.15); CREASE = (0.26, 0.25, 0.25)   # 노예 — 푸른 잿빛 · 코 둘레 탈 · 눈두덩 · 주름 골
NOSE_T = (0.42, 0.38, 0.34)                                                                                      # 노예 코 — 탈보다 조금 더 갈색
LIP_T2 = (0.24, 0.20, 0.24); GLOW = (1.00, 0.93, 0.92); GLOW_RIM = (0.86, 0.46, 0.50); TEETH = (0.78, 0.69, 0.42); LIP_T = (0.38, 0.40, 0.44)
RAG = (0.42, 0.38, 0.32); RAG_D = (0.30, 0.27, 0.23); ROPE = (0.60, 0.50, 0.32); IRON = (0.30, 0.30, 0.33); WRAP = (0.50, 0.45, 0.37)

# ── 몸 — 사람 치비 몸 그대로(`body.figure`의 셈) ──
FEMALE = ('Female', [0.23, 0.24, 0.21, 0.27, 0.22], 0.80, 0.27, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.55)   # C# `ChibiPawnCreator.Shapes`의 여성 줄
D = TOP = HAND_C = HAND_R = KNEE = CHEST_Z = CAPE_TOP = None
def set_body(female):
    """몸 치수를 그 성별로 — 몸 조각은 이 전역을 읽는다(한 일감은 한 몸만 그린다)."""
    global D, TOP, HAND_C, HAND_R, KNEE, CHEST_Z, CAPE_TOP
    D = bd.dims(FEMALE if female else bd.MALE); TOP = D['top']
    HAND_C = V(D['armX'], D['handY'] - D['hand'] * 0.25, 0); HAND_R = D['hand'] * 0.5
    KNEE = D['lift'] * 0.72; CHEST_Z = D['half'][3] * D['depth']; CAPE_TOP = TOP - 0.01
set_body(False)
class Variant:
    def __init__(self, name, kind, head, female, hair=None, hair_col=INK):
        self.name, self.kind, self.female, self.hair, self.hair_col = name, kind, female, hair, hair_col
        self.head = head; self.h = hm.Head(head)
        top = bd.dims(FEMALE if female else bd.MALE)['top']
        self.HO = V(0, top + bd.HeadGap + self.h.H * 0.5, bd.HeadZ)
VARIANTS = {'lord': Variant('lord', 'lord', 'Male_SlimSharp', False, 'slick', INK),
            'lordF': Variant('lordF', 'lord', 'Female_SlimSharp', True, 'long', WINE),
            'thrall': Variant('thrall', 'thrall', 'Male_SlimSharp', False),
            'thrallF': Variant('thrallF', 'thrall', 'Female_SlimSharp', True)}

def torso(p):
    y = p[..., 1] - D['lift']; t = y / D['torsoH']
    r = bd.torso_r(D['half'], t, D['torsoH'], D['topR'])
    q = np.sqrt(p[..., 0] ** 2 + (p[..., 2] / D['depth']) ** 2)
    return np.maximum((q - r) * D['depth'], np.maximum(-y, y - D['torsoH']))
def legs(p):
    q = mir(p); y0 = max(D['footY'], D['legR'] + 0.01)
    return capsule(q, V(D['legX'], y0, 0), V(D['legX'], D['hipY'], 0), D['legR'])
def shoes(p):
    q = mir(p); sw, sh, sl = D['shoe']
    return rbox(q, V(D['legX'], sh * 0.5, bd.PShoeFwd * bd.HH), V(sw * 0.5, sh * 0.5, sl * 0.5), min(sw, sh) * 0.4)
def arms(p):
    q = mir(p); return capsule(q, V(D['armX'], D['handY'], 0), V(D['armX'], D['shY'], 0), D['armR'])
def hands(p): return sph(mir(p), HAND_C, HAND_R)

def safe_crown(d, p):
    """정수리 둘레에서 걸음을 묶는다 — 사람 머리 표의 거리장(`hm.Head.sdf`)은 정수리 위에서 실제보다 열 배 넘게 크게 답해(1cm 위를 12cm로)
    광선이 숱 · 머리를 건너뛰고 뒤가 비쳤다(v2 내려다보기). 공 밖에서는 공까지의 거리가 안전한 아래 한계다."""
    return np.minimum(d, np.maximum(sph(p, V(0, 0.20, -0.02), 0.22), 0.008))

# ── 로드 — 사람 얼굴 그대로 · 창백한 회색 · 붉은 눈 · 송곳니 ─────────────────────
def fangs(h, p):     # 입선 밑으로 드러난 위 송곳니 둘 — 얼굴 면에 붙어 아래로 뾰족하다
    m = mirror(p); x = 0.019; z = h.front(0.5 + h.MY / h.H) + 0.001
    return seg(m, V(x, h.MY + 0.002, z - 0.004), V(x - 0.002, h.MY - 0.020, z + 0.001), 0.0065, 0.0010)
def hair_pieces(v, HO, hd):
    """확인받은 원본 머리카락(`port.family('human')`) — 조각마다 (거리장, 'hair' | 'deco' | 'plait' | 'buzz')."""
    import port
    _, fams = port.family('human', v.head)
    fn = fams[v.hair]; cache = {}
    def part(i):
        def g(p):
            if cache.get('k') is not p: cache['k'] = p; cache['v'] = fn(p)
            vv = cache['v']; return safe_crown(vv[i][0], p) if i < len(vv) else far(p)
        return g
    sample = fn(np.zeros((1, 3), np.float32)); out = []
    for i, (_, tag) in enumerate(sample):
        col = v.hair_col if tag == 'hair' else tuple(c * 0.75 for c in v.hair_col) if tag == 'plait' else \
              tuple(PALE[j] * 0.55 + v.hair_col[j] * 0.45 for j in range(3)) if tag == 'buzz' else DECO
        out.append((hd(part(i), 1.2), col))
    return out
def own_hair_pieces(v, hd):
    """가닥이 흐르는 로드 머리(`lord_hair.py`) — 원본 머리카락(껍데기 + 결)은 방향이 안 보여 짧은 머리 같았다(v17 사용자)."""
    import lord_hair
    f, deco, parts = lord_hair.build(v.h, v.hair)
    gc = lord_hair.hair_col(v.hair_col, None if v.hair == 'long' else lord_hair.strands_of(v.h, v.hair)[0], folds=v.hair == 'long')   # 다발 명암 · 장발은 주름 골(v39)
    out = [(hd(lambda p: safe_crown(f(p), p), 1.2), (lambda p: gc(p - v.HO)))]
    if deco is not None: out.append((hd(deco, 1.2), DECO))
    for g in parts: out.append((hd(g, 1.2), (lambda p: gc(p - v.HO))))                      # 따로 된 조각(더듬이) — 머리와 경계선이 생긴다
    return out
def lord_head(v):
    h, HO = v.h, v.HO
    def hd(f, r=0.62): return lambda p: bounded(lambda q: f(q - HO), p, HO, r)
    return [(hd(hm.face(h, 'Straight' if not v.female else 'Button')), PALE), (hd(lambda p: hm.eyes(h, p)), EYE_RED), (hd(lambda p: hm.glint(h, p)), WHITE),
            (hd(lambda p: hm.brows(h, 'Arch' if v.female else 'Soft', p)), tuple(c * 0.85 for c in v.hair_col)), (hd(lambda p: hm.mouth(h, p, 'Line')), LIP_P),
            (hd(lambda p: fangs(h, p)), FANG)] + (own_hair_pieces(v, hd) if v.hair.split('_')[0] in ('slick', 'lowtail', 'long', 'curtain') else hair_pieces(v, HO, hd))

# ── 노예 — 보낸 그림의 얼굴 ────────────────────────────────────────────────
# 주름은 얼굴 면을 안으로 미는 골(`wrinkles` — 앞면 (x, y)에 그린 선 · 거리장에 더한다)이고 같은 값으로 색도 짙게 칠한다(골이 그늘로 읽힌다).
def T_lines(h):
    """주름 — 미간 세로 둘만(v10 사용자: *"주름을 딱히 많이 표현할 필요는 없고"*)."""
    EY, ER = h.EY, h.ER
    return [((0.011, EY + ER * 0.40), (0.014, EY + ER + 0.030), 0.0035, 0.0035)]
def wrinkles(h, p):
    m = mirror(p); x, y = m[..., 0], p[..., 1]
    d = np.zeros(x.shape, np.float32)
    for a, b, w, dep in T_lines(h):
        r, _ = seg2(x, y, a, b); d = np.maximum(d, dep * np.exp(-(r / w) ** 2))
    return d * (p[..., 2] > 0.05) * (0.55 if h.female else 1.0)
def fk(h): return 0.85 if h.female else 1.0   # 여 노예 — 코 · 눈썹뼈를 덜어 낸다(보낸 그림 `2.webp`)
def nose_pts(h):
    """코의 뼈대(그림 `thrall_ref.png` · `2.webp` 확대 — v11): 콧대 위 · 아래 · 가운데 코끝 · 날개(가운데 → 솟은 등 → 처진 바깥 끝) · V 끝."""
    k = fk(h); NY, ZN = h.NY, h.ZN
    top = V(0, h.EY + 0.004, h.ZE + 0.008 * k); low = V(0, NY + 0.020, ZN + 0.024 * k)
    c = V(0, NY + 0.004, ZN + 0.032 * k)                                                  # 날개가 만나는 가운데
    wing = [c, V(0.020 * k, NY + 0.010, ZN + 0.026 * k), V(0.036 * k, NY + 0.017, ZN + 0.012 * k), V(0.052 * k, NY + 0.022, ZN - 0.006)]   # v12 — 바깥으로 살짝 오르며 볼 속으로 사라진다(처지지 않는다)
    yv = NY - 0.017; vtip = V(0, yv, h.front(0.5 + yv / h.H) + 0.008)                     # 아래로 뾰족한 V 끝 — 콧방울 바로 밑에서 짧게 끝나 얼굴 면에 붙는다(v14~v15 사용자: *"원래 위치에서 떨어져 있으니 붙히라는 것"* → *"콧끝이 좀 길어보여"* + 덧그린 그림 `_refs/2026-11-15_vampire/user_markup_v14_nose.png` — 연 모양)
    return top, low, c, wing, vtip
def T_nose(h, p):
    """**갈매기 날개 코**(v11 사용자: *"저 뾰족한 코가 제일 포인트 — 그냥 똑같이 · 콧구멍을 점찍듯이는 어색"*):
    미간에서 곧게 내려오는 좁은 콧대 → 끝에서 양옆으로 아치를 그리며 뻗다 바깥 끝이 처지는 콧방울(날개) → 가운데가 아래로 뾰족하게 떨어지는 V.
    콧구멍은 날개 밑에 숨는다(앞에서 안 보인다)."""
    m = mirror(p); k = fk(h)
    top, low, c, wing, vtip = nose_pts(h)
    d = seg(flat(p - low, V(0, 0, 1), 0.50) + low, top, low, 0.024 * k, 0.040 * k) * 0.50   # 넓은 콧대 — 앞뒤로 납작 · 옆으로 넓다(v13~v15 사용자: *"콧대를 더 늘려 · 사진 느낌"* · *"콧대가 옆으로 좀 더 넓잖아"*)
    radii = (0.019, 0.015, 0.009, 0.002)                                                 # 바깥으로 가늘어져 없어진다
    wq = flat(m - c, V(0, 1, 0), 0.62) + c                                                 # 위아래로 납작(v12 사용자: *"콧방울이 위아래로 너무 넓고"*)
    for i in range(3):
        d = smin(d, seg(wq, wing[i], wing[i + 1], radii[i] * k, radii[i + 1] * k) * 0.62, 0.008)   # 날개 — 옆으로 뻗다 사라진다
    d = smin(d, seg(p, c + V(0, 0.004, 0), vtip, 0.019 * k, 0.006), 0.012)                  # 가운데 V — 아래로 뾰족
    under = ell(m, V(0.024 * k, h.NY - 0.002, h.ZN + 0.016 * k), V(0.014 * k, 0.006, 0.012 * k))   # 날개 밑 오목(콧구멍이 숨는 그늘)
    return smax(d, -under, 0.004)
def T_ear(h, p):
    """뾰족 귀 — 머리 옆에서 바깥 · 위로 뻗는다(그림: 정면에서 눈 높이에 날개처럼 벌어진다) · 앞을 반쯤 보는 오목."""
    m = mirror(p)
    c = V(h.R0 - 0.014, h.EY - 0.018, -0.030); tip = c + V(0.092, 0.120, -0.040)
    n = nrm([0.72, -0.05, 0.69])
    blade = seg(flat(m - c, n, 0.32) + c, c + V(0, -0.025, 0), tip, 0.052, 0.003) * 0.32
    ci = c + n * 0.018
    cup = seg(flat(m - ci, n, 0.32) + ci, ci + V(0.004, -0.010, 0), c + (tip - c) * 0.78 + n * 0.016, 0.036, 0.003) * 0.32
    return smax(blade, -np.maximum(cup, 0.008 - ((m - c) * n).sum(-1)), 0.005)
def T_face(h):
    def f(p):
        m = mirror(p); x, y = m[..., 0], p[..., 1]; EY, EX, ER = h.EY, h.EX, h.ER
        d = h.sdf(p)
        d = smin(d, seg(m, V(0.006, EY + ER + 0.002, h.ZE + 0.012 * fk(h)), V(EX + ER * 1.45, EY + ER * 1.45, h.ZE - 0.036), 0.024 * fk(h) ** 2, 0.014 * fk(h)), 0.018)   # 무거운 눈썹뼈 — 안쪽이 내려와 찌푸린다
        d = smin(d, ell(p, V(0, EY + ER + 0.014, h.ZE + 0.004), V(0.022, 0.016, 0.012)), 0.012)                                           # 미간이 부푼다
        d = smin(d, seg(m, V(EX + 0.006, EY - ER * 1.6, h.ZE - 0.020), V(h.R0 - 0.020, EY - ER * 1.30, 0.030), 0.013, 0.009), 0.018)      # 광대
        d = smin(d, T_nose(h, p), 0.010)
        d = d + 0.007 * gauss2(x, y, EX, EY - 0.002, ER * 1.25, ER * 0.95) * (p[..., 2] > 0)                                          # 꺼진 눈두덩
        d = d + 0.011 * gauss2(x, y, EX + 0.020, h.MY + 0.042, 0.030, 0.034) * (p[..., 2] > 0)                                       # 꺼진 볼
        d = d + wrinkles(h, p)
        return smin(d, T_ear(h, p), 0.012)
    return f
def on_face(F, h, p, region, out):
    """얼굴 살(F) 위 `out` 두께의 껍데기 중 `region` 안 — 꺼뜨린 눈두덩 · 주름진 면에 붙는다(`hm.on_face`는 바탕 머리 면에 붙어 뜬다)."""
    fd = F(p); shell = np.maximum(fd - out, -(fd + 0.010))
    return smax(np.maximum(shell, region), (h.ZE - 0.12) - p[..., 2], 0.002)
def T_eye_region(h, m):   # 가늘게 뜬 아몬드 눈 — 윗꺼풀은 안쪽이 내려온다
    r = ell(m, V(h.EX, h.EY - 0.006, h.ZE), V(h.ER * 0.78, h.ER * 0.52, 0.3))
    return np.maximum(r, m[..., 1] - (h.EY + h.ER * 0.18 + 0.30 * (m[..., 0] - h.EX)))
def T_col(h):
    def col(p):
        m = mirror(p); x, y = m[..., 0], p[..., 1]; EY, EX, ER, NY = h.EY, h.EX, h.ER, h.NY
        front = p[..., 2] > 0.05
        mask = np.clip(0.5 * gauss2(x, y, 0.0, NY + 0.015, 0.045, 0.055) + gauss2(x, y, EX - 0.010, EY - 0.010, ER * 2.0, ER * 1.6), 0, 1) * front
        c = mixc(BLUE, MASK, np.clip(mask * 1.15, 0, 1))                                                                 # 코 · 눈 둘레의 짙은 탈
        c = mixc(c, NOSE_T, sstep(0.006, 0.0, T_nose(h, p)) * front * 0.25)                                           # 코 살 — 살색과 거의 같게(v12 사용자: *"코랑 피부색이랑 차이를 많이 줄 필요는 없을 거 같아"*)
        groove, _ = seg2(x, y, (0.0, h.NY - 0.030), (0.0, h.MY + 0.006))
        c = mixc(c, CREASE, np.exp(-(groove / 0.0020) ** 2) * front * 0.30)                                         # 코끝 V에서 윗입술로 가는 골
        c = mixc(c, SOCKET, np.clip(1.25 * gauss2(x, y, EX, EY - 0.004, ER * 1.25, ER * 0.90), 0, 0.95) * front)          # 꺼먼 눈두덩
        c = mixc(c, LIP_T, gauss2(x, y, 0.0, h.MY - 0.004, 0.040, 0.012) * front * 0.8)                                  # 핏기 없는 입술
        return mixc(c, CREASE, np.clip(wrinkles(h, p) / (0.003 * (0.55 if h.female else 1.0)), 0, 1) * (0.35 if h.female else 0.50))                                            # 주름 골
    return col
def thrall_head(v):
    h, HO = v.h, v.HO; F = T_face(h)
    def hd(f, r=0.62): return lambda p: bounded(lambda q: f(q - HO), p, HO, r)
    def hc(c): return (lambda p: c(p - HO)) if callable(c) else c
    def mouth(p):    # 사람 치비 입선(`hm.mouth` 'Line'과 같은 띠 · v11 사용자: *"입을 똑같이 구현할 필요는 없었어"*) — 주름진 면(F)에 붙인다
        x = p[..., 0]; y = p[..., 1] - (h.MY + 0.004); th = 0.0040 * (1 - 0.45 * (np.abs(x) / 0.052) ** 2)
        return on_face(F, h, p, np.maximum(np.abs(y - 0.25 * x * x) - th, np.abs(x) - 0.052), 0.003)
    eye = lambda p: on_face(F, h, p, T_eye_region(h, mirror(p)), 0.003)
    rim = lambda p: on_face(F, h, p, ell(mirror(p), V(h.EX - 0.002, h.EY - 0.006, h.ZE), V(h.ER * 0.36, h.ER * 0.36, 0.3)), 0.0045)
    core = lambda p: on_face(F, h, p, ell(mirror(p), V(h.EX - 0.002, h.EY - 0.006, h.ZE), V(h.ER * 0.20, h.ER * 0.20, 0.3)), 0.006)
    return [(hd(lambda p: safe_crown(F(p), p)), hc(T_col(h))), (hd(eye), DARK), (hd(rim), GLOW_RIM), (hd(core), GLOW),
            (hd(mouth), LIP_T2)]

# ── 로드 옷 ──────────────────────────────────────────────────────────────
def coat(p):         # 연미 코트 — 몸통 껍데기(앞은 V로 열려 조끼가 보인다) + 엉덩이 뒤로 내려오는 자락
    y = p[..., 1]; x = np.abs(p[..., 0]); z = p[..., 2]
    w = 0.035 + 0.075 * sstep(D['lift'] + 0.22, TOP - 0.05, y)
    body = smax(torso(p) - 0.014, -np.maximum(w - x, -z), 0.008)
    yl = D['lift'] + 0.08; t = np.clip((yl - y) / 0.30, 0, 1)
    rr = np.sqrt(p[..., 0] ** 2 + (z / 0.86) ** 2)
    rad = D['half'][1] + 0.006 + 0.09 * t ** 1.2 + 0.008 * np.sin(np.arctan2(p[..., 0], -z) * 7) * t
    hem = y - (yl - 0.30 * sstep(0.10, -0.12, z / np.maximum(rr, 1e-3) * 0.25 + 0.02))
    skirt = np.maximum(np.maximum(np.abs(rr - rad) - 0.010, y - (yl + 0.03)), -hem)
    skirt = smax(skirt, np.maximum(0.06 - x, -z), 0.01)
    return smin(body, skirt, 0.02)
def lapels(p):
    y = p[..., 1]; x = np.abs(p[..., 0]); w = 0.035 + 0.075 * sstep(D['lift'] + 0.22, TOP - 0.05, y)
    return smax(np.maximum(torso(p) - 0.022, np.abs(x - (w + 0.020)) - 0.022), np.maximum((D['lift'] + 0.22) - y, y - (TOP - 0.03)), 0.01)
def jabot(p, k=1.0):
    c = V(0, TOP - 0.03, CHEST_Z); d = far(p)
    for i, (dy, s) in enumerate(((0.00, 0.055), (-0.050, 0.047), (-0.095, 0.038))):
        if k < 1 and i == 2: continue
        d = smin(d, ell(p, c + V(0, dy * k, 0.010 - 0.004 * i), V(s * k, 0.032 * k, 0.028)), 0.012)
    return smax(d, -(p[..., 2] - (c[2] - 0.05)), 0.01) + 0.003 * np.sin(p[..., 1] * 140)
def brooch(p): return ell(p, V(0, TOP - 0.040, CHEST_Z + 0.046), V(0.020, 0.024, 0.013))
def brooch_rim(p): return ell(p, V(0, TOP - 0.040, CHEST_Z + 0.038), V(0.029, 0.033, 0.011))
def buttons(p):
    d = far(p)
    for y in (D['lift'] + 0.11, D['lift'] + 0.19, D['lift'] + 0.27): d = np.minimum(d, sph(p, V(0, y, D['half'][2] * D['depth'] + 0.004), 0.015))
    return d
def sleeves(p): return capsule(mir(p), V(D['armX'], D['handY'] + 0.02, 0), V(D['armX'], D['shY'], 0), D['armR'] + 0.008)
def cuffs(p):        # 손목 레이스 — 소매 끝에서 손 위로 늘어진 주름
    q = mir(p); c = V(D['armX'], D['handY'] + 0.025, 0)
    d = smax(sph(q, c, D['armR'] * 1.45), np.abs(q[..., 1] - c[1]) - 0.018, 0.006)
    return d + 0.0035 * np.sin(np.arctan2(q[..., 0] - c[0], q[..., 2]) * 12)
def boots(p):        # 장화 — 정강이 위까지 · 접힌 윗단
    q = mir(p); x = D['legX']; r = D['legR']
    shaft = np.maximum(capsule(q, V(x, 0.06, 0.0), V(x, KNEE, 0.0), r + 0.014), q[..., 1] - KNEE)
    cuff = np.maximum(capsule(q, V(x, KNEE - 0.04, 0.0), V(x, KNEE + 0.02, 0.0), r + 0.026), np.abs(q[..., 1] - (KNEE - 0.005)) - 0.025)
    return smin(np.minimum(shaft, cuff), shoes(p), 0.03)
def corset(p): return torso(p) - 0.010                                                       # 여 로드 — 붉은 코르셋(몸통 위 얇은 껍데기)
def laces(p):        # 코르셋 앞의 엇갈린 끈
    z0 = D['half'][2] * D['depth'] + 0.012; d = far(p)
    for i in range(5):
        y = D['lift'] + 0.07 + i * 0.055; d = np.minimum(d, seg(mir(p), V(0.0, y, z0), V(0.028, y + 0.028, z0 - 0.004), 0.006, 0.006))
    return np.maximum(d, -(p[..., 2] - 0.05))
def gown(p):         # 바닥까지 끌리는 치마 — 허리에서 종 모양으로 벌어진다 · 세로 주름
    y = p[..., 1]; yl = D['lift'] + 0.10
    t = np.clip((yl - y) / yl, 0, 1)
    rad = D['half'][2] * 1.05 + 0.22 * t ** 1.4
    th = np.arctan2(p[..., 0], p[..., 2])
    rr = np.sqrt(p[..., 0] ** 2 + (p[..., 2] / 0.92) ** 2) - 0.016 * np.sin(th * 9) * t
    return np.maximum(rr - rad, np.maximum(y - yl, -y)) * 0.9
def gown_col(p): return mixc(COAT, VEST, 0.30 * sstep(0.10, 0.30, p[..., 2]) * sstep(0.30, 0.0, np.abs(p[..., 0])))
def cape_frame(p):
    y = p[..., 1]; t = np.clip((CAPE_TOP - y) / (CAPE_TOP - 0.15), 0, 1)
    z = p[..., 2] - (-0.03 - 0.10 * t)
    th = np.arctan2(p[..., 0], -z); rr = np.sqrt(p[..., 0] ** 2 + z ** 2)
    rad = 0.335 + 0.17 * t ** 0.8 + 0.022 * np.sin(th * 7) * t
    return rr, rad, th, t
def cape(p, with_mantle=False):
    y = p[..., 1]; rr, rad, th, t = cape_frame(p)
    d = np.maximum(np.abs(rr - rad) - 0.012, np.abs(th) - np.radians(108))
    d = np.maximum(d, np.maximum(y - CAPE_TOP, (0.15 + 0.03 * np.cos(th * 3)) - y))
    if not with_mantle: return d
    mantle = smax(ell(p, V(0, CAPE_TOP - 0.03, -0.03), V(0.345, 0.11, 0.27)), -(ell(p, V(0, CAPE_TOP - 0.03, -0.03), V(0.320, 0.09, 0.245))), 0.004)
    mantle = np.maximum(mantle, (CAPE_TOP - 0.05) - y)
    return smax(mantle, -(sph(mir(p), V(0, CAPE_TOP + 0.02, 0.0), 0.115)), 0.01)
def mantle(p): return cape(p, True)
def cape_col(p):
    rr, rad, th, t = cape_frame(p); return mixc(COAT, LINING, sstep(0.002, -0.004, rr - rad))
def collar_frame(p):
    q = p - V(0, 0, -0.05); y = p[..., 1]
    s = np.clip((y - (TOP - 0.03)) / 0.42, 0, 1)
    return np.arctan2(q[..., 0], -q[..., 2]), np.sqrt(q[..., 0] ** 2 + (q[..., 2] * 1.05) ** 2), 0.175 + 0.20 * s ** 1.3
def collar(p):       # 높이 선 깃 — 뒤에서 머리를 감싸며 벌어져 오른다 · 위 끝은 세 갈래로 뾰족하다
    y = p[..., 1]; th, rr, rad = collar_frame(p); lim = np.radians(112)
    d = np.maximum(np.abs(rr - rad) - 0.011, np.abs(th) - lim)
    a = np.abs(th) / lim
    crest = (TOP - 0.03) + 0.36 + 0.060 * np.cos(a * np.pi * 3) - 0.10 * a ** 3
    return np.maximum(d, np.maximum(y - crest, (TOP - 0.04) - y))
def collar_col(p):
    th, rr, rad = collar_frame(p); return mixc(COAT, LINING, sstep(0.002, -0.004, rr - rad))

# ── 노예 옷 ──────────────────────────────────────────────────────────────
def tunic(p):        # 해진 튜닉 — 몸통에서 바지 위까지 내려오고 밑단이 들쭉날쭉
    y = p[..., 1]; hem = D['hem'] - 0.05 + 0.030 * np.sin(p[..., 0] * 41 + p[..., 2] * 23) + 0.018 * np.sin(p[..., 0] * 97)
    return smax(torso(p) - 0.008, hem - y, 0.006)
def rag_skirt(p):    # 여 노예 — 무릎까지 내려오는 해진 치마(밑단이 찢겼다)
    y = p[..., 1]; yl = D['lift'] + 0.06; t = np.clip((yl - y) / 0.30, 0, 1)
    rr = np.sqrt(p[..., 0] ** 2 + (p[..., 2] / 0.90) ** 2); rad = D['half'][1] * 1.02 + 0.07 * t
    hem = (yl - 0.26) + 0.035 * np.sin(np.arctan2(p[..., 0], p[..., 2]) * 9) + 0.020 * np.sin(np.arctan2(p[..., 0], p[..., 2]) * 23)
    return np.maximum(rr - rad, np.maximum(y - (yl + 0.04), hem - y)) * 0.9
def tunic_col(p):    # 기운 자국 — 짙은 천 조각이 군데군데
    k = (np.sin(p[..., 0] * 13 + p[..., 1] * 9) * np.sin(p[..., 1] * 11 - p[..., 2] * 7) > 0.45).astype(np.float32)
    return mixc(RAG, RAG_D, k * 0.8)
def rope(p):
    y0 = D['hem'] + 0.035
    return smax(torso(p) - 0.022, np.abs(p[..., 1] - y0) - 0.014, 0.004) + 0.003 * np.sin(np.arctan2(p[..., 0], p[..., 2]) * 30 + p[..., 1] * 200)
def rags(p):         # 짧게 찢긴 소매 — 어깨에서 팔 위만
    q = mir(p); y0 = D['shY'] - 0.13 + 0.025 * np.sin(np.arctan2(q[..., 0] - D['armX'], q[..., 2]) * 5)
    return np.maximum(capsule(q, V(D['armX'], D['handY'], 0), V(D['armX'], D['shY'], 0), D['armR'] + 0.010), y0 - q[..., 1])
def shackles(p):     # 손목 족쇄 — 쇠고리 + 끊긴 사슬 한 마디
    q = mir(p); c = V(D['armX'], D['handY'] + 0.035, 0)
    d = smax(capsule(q, c - V(0, 0.022, 0), c + V(0, 0.022, 0), D['armR'] + 0.016), -capsule(q, c - V(0, 0.05, 0), c + V(0, 0.05, 0), D['armR'] - 0.002), 0.004)
    lk = q - (c + V(0, -0.045, D['armR'] + 0.018))
    link = np.hypot(np.hypot(lk[..., 0], lk[..., 1] * 0.7) - 0.022, lk[..., 2]) - 0.007
    return np.minimum(d, link)
def wraps(p):        # 발싸개 — 신발 자리를 천으로 감았다
    return shoes(p) + 0.004 * np.sin(p[..., 1] * 120 + p[..., 2] * 40)

def pieces(v, body=True):
    set_body(v.female)
    cut = v.HO[1] - 0.50
    clip = lambda f: (lambda p: np.maximum(f(p), cut - p[..., 1]))
    if v.kind == 'lord':
        P = lord_head(v)
        if not v.female:
            if not body:
                return P + [(clip(collar), collar_col), (clip(coat), COAT), (clip(torso), VEST), (jabot, LACE), (brooch, GEM), (brooch_rim, GOLD)]
            return P + [(collar, collar_col), (cape, cape_col), (mantle, COAT), (sleeves, COAT), (cuffs, LACE), (hands, PALE),
                        (torso, VEST), (coat, COAT), (lapels, SATIN), (buttons, GOLD), (jabot, LACE), (brooch, GEM), (brooch_rim, GOLD),
                        (legs, PANTS), (boots, BOOT)]
        small = lambda p: jabot(p, 0.75)
        if not body:
            return P + [(clip(collar), collar_col), (clip(corset), VEST), (small, LACE), (brooch, GEM), (brooch_rim, GOLD)]
        return P + [(collar, collar_col), (cape, cape_col), (mantle, COAT), (sleeves, COAT), (cuffs, LACE), (hands, PALE),
                    (torso, PALE), (corset, VEST), (laces, GOLD), (small, LACE), (brooch, GEM), (brooch_rim, GOLD), (gown, gown_col)]
    P = thrall_head(v)
    if not body:
        return P + [(clip(tunic), tunic_col), (clip(torso), BLUE)]
    P = P + [(torso, BLUE), (tunic, tunic_col), (rope, ROPE), (arms, BLUE), (rags, tunic_col), (hands, BLUE), (shackles, IRON), (legs, RAG_D if not v.female else BLUE), (wraps, WRAP)]
    return P + [(rag_skirt, tunic_col)] if v.female else P

FULL = ((0, 400), (35, 420), (90, 440), (160, 420)); CLOSE = ((0, 420, 0), (35, 440, 0), (90, 460, 0), (25, 440, 40))
VIEW = (0.98, 2.20)
# 로드 머리 넷(v16 사용자가 보낸 그림 넷 — 깃 밖 `_refs/2026-11-15_vampire/lord_hair_*.png`) — 확인받은 사람 머리카락(`port.family('human')`)에서 가장 가까운 것 ·
# 색은 그림 쪽(게임에서는 머리색 축이 따로 고른다)
LORD_HAIRS = [('curtain', 'lord_hair_curtain.png', INK),                       # 1 더듬이 머리
              ('curtain_plain', 'lord_hair_curtain.png', INK),                 # 1 더듬이 없음(v44)
              ('slick', 'lord_hair_slick.png', (0.70, 0.70, 0.73)),            # 2 깔끔한 올백 · 은빛
              ('slick_bangs', 'lord_hair_slick.png', (0.70, 0.70, 0.73)),      # 2 + 더듬이(v44)
              ('long', 'lord_hair_long.png', (0.36, 0.25, 0.16)),              # 3 장발 · 갈색
              ('lowtail', 'lord_hair_lowtail.png', (0.30, 0.17, 0.11)),        # 4 뒤로 넘겨 낮게 묶은 장발 · 적갈색
              ('lowtail_bangs', 'lord_hair_lowtail.png', (0.30, 0.17, 0.11))]  # 4 + 더듬이(v44)
# v17 — 확인받은 원본(`port`)이 아니라 가닥이 흐르는 머리(`lord_hair.py`)로 다시 지었다
HAIR_VIEWS = (('lord', 0), ('lord', 40), ('lord', 160), ('lordF', 0), ('lordF', 40))
def close(v, yaw, w, pitch, hpx=460, span=1.05):
    c = v.HO + V(0, 0.02, 0.06)
    Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in pieces(v, body=False)]
    return np.asarray(hb.render(Q, yaw, w, hpx, 0.0, span, pitch=pitch))
def job(a):
    mode, vn, i = a
    if mode == 'human':
        P, _ = bd.figure(bd.MALE, bd.dw.b_box, bd.dw.h_bald, bd.dw.BROWN, bd.dw.e_bushy); return np.asarray(hb.render(P, 35, 380, 760, *VIEW))
    if mode == 'hair':   # (변형, 머리카락) · 보기 각도
        k, (n, yaw) = vn, HAIR_VIEWS[i]; hair, _, col = LORD_HAIRS[k]; v0 = VARIANTS[n]
        v = Variant(n, 'lord', v0.head, v0.female, hair, col); return close(v, yaw, 300, 0, 340, 1.10)
    v = VARIANTS[vn]
    if mode == 'full':
        yaw, w = FULL[i]; return np.asarray(hb.render(pieces(v), yaw, w, 760, *VIEW))
    if mode == 'cmp':   # 보낸 그림과 같은 구도 — 정면 · 옆(얼굴 가까이)
        yaw = (0, 75)[i]; return close(v, yaw, 520, 0, 560, 0.80)
    yaw, w, pitch = CLOSE[i]; return close(v, yaw, w, pitch)

if __name__ == '__main__':
    from multiprocessing import Pool
    stem = sys.argv[1]
    _os.makedirs(_os.path.dirname(_os.path.abspath(stem)), exist_ok=True)
    arg = sys.argv[2] if len(sys.argv) > 2 else ''
    if arg == 'cmp':
        jobs = [('cmp', n, i) for n in ('thrall', 'thrallF') for i in range(2)]
        with Pool(4) as pool: res = pool.map(job, jobs)
        refs = []
        for fn in ('thrall_ref.png', '2.webp'):
            r = Image.open(_os.path.join(_here, '..', '_refs', '2026-11-15_vampire', fn)).convert('RGB'); refs.append(r.resize((int(r.width * 560 / r.height), 560)))
        im = Image.new('RGB', (max(r.width for r in refs) + 1040, 1120), (40, 40, 44))
        for row in range(2):
            im.paste(refs[row], (0, 560 * row))
            for k in range(2): im.paste(Image.fromarray(res[2 * row + k]), (refs[row].width + 520 * k, 560 * row))
        im.save(stem + '_cmp.png'); print('saved', stem + '_cmp.png'); sys.exit()
    if arg == 'hairs':
        # 줄마다 머리 하나 — 보낸 그림 · 남(정면 · 40° · 뒤) · 여(정면 · 40°)
        jobs = [('hair', k, i) for k in range(len(LORD_HAIRS)) for i in range(len(HAIR_VIEWS))]
        with Pool(min(len(jobs), _os.cpu_count() or 4)) as pool: res = pool.map(job, jobs)
        RW = 300; im = Image.new('RGB', (RW + 300 * len(HAIR_VIEWS), 340 * len(LORD_HAIRS)), (40, 40, 44)); dr = ImageDraw.Draw(im)
        for k, (hr, ref, _) in enumerate(LORD_HAIRS):
            r = Image.open(_os.path.join(_here, '..', '_refs', '2026-11-15_vampire', ref)).convert('RGB'); r.thumbnail((RW, 340)); im.paste(r, (0, 340 * k))
            for i in range(len(HAIR_VIEWS)): im.paste(Image.fromarray(res[k * len(HAIR_VIEWS) + i]), (RW + 300 * i, 340 * k))
            dr.text((RW + 6, 340 * k + 4), f'{k + 1}. {hr}', fill=(255, 220, 120))
        im.save(stem + '_hairs.png'); print('saved', stem + '_hairs.png'); sys.exit()
    names = arg.split(',') if arg else ['lord', 'lordF', 'thrall', 'thrallF']
    jobs = [('human', '', 0)] + [(m, n, i) for n in names for m, cnt in (('full', len(FULL)), ('close', len(CLOSE))) for i in range(cnt)]
    with Pool(min(len(jobs), _os.cpu_count() or 4)) as pool: res = pool.map(job, jobs)
    rows = []; k = 1
    for n in names:
        rows.append((n, res[k:k + len(FULL)], res[k + len(FULL):k + len(FULL) + len(CLOSE)])); k += len(FULL) + len(CLOSE)
    wd = max(res[0].shape[1] + sum(t.shape[1] for t in rows[0][1]), max(sum(t.shape[1] for t in r[2]) for r in rows))
    rh = 760 + 460
    im = Image.new('RGB', (wd, rh * len(rows)), (40, 40, 44)); dr = ImageDraw.Draw(im)
    for r, (n, full, cl) in enumerate(rows):
        y0 = r * rh
        im.paste(Image.fromarray(res[0]), (0, y0)); dr.text((6, y0 + 6), 'human chibi (same scale)', fill=(200, 200, 210))
        x = res[0].shape[1]
        for t in full: im.paste(Image.fromarray(t), (x, y0)); x += t.shape[1]
        x = 0
        for t in cl: im.paste(Image.fromarray(t), (x, y0 + 760)); x += t.shape[1]
        dr.text((res[0].shape[1] + 6, y0 + 4), 'vampire ' + n, fill=(255, 220, 120))
    im.save(stem + '.png'); print('saved', stem + '.png')
