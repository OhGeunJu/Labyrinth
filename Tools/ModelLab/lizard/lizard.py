"""리자드맨(2층 지저늪) — 사람 치비 몸(`dwarf/body.py` `dims` = C# `ChibiPawnCreator.Proto`)을 굽히고 꼬리 · 짐승 다리 · 빚은 머리를 얹는다.
종류 넷(사용자 11-11: *"머리는 세 개 다"* → 뱀은 실제 뱀 둘로 — *"도마뱀을 잘해서 뱀도 현실감 있게"*):
  lizard  도마뱀 — 납작한 쐐기 머리 · 눈 위에서 뒤통수로 도는 가시 테(볏도마뱀붙이 — 사용자 그림 6) · 큰 눈
  python  비단뱀 — 길고 낮은 머리 · 네모진 주둥이 · 구슬 눈 · 입술의 노란 줄 · 매끈한 검푸른 몸(사용자 그림 8)
  viper   숲살모사 — 짧고 넓은 세모 머리 · 가는 목 · 큰 눈 · 온몸에 일어선 비늘(사용자 그림 7 · 9)
  croc    악어   — 덩치가 크다(몸도 따로 굵다) · 넓고 무거운 턱 · 눈 위 굵은 턱 · 큰 송곳니 · 골판
눈은 고르기 축이다(사용자: *"뾰족한 눈 · 귀여운 눈 · 그 중간으로 넓게"*) — 아래 `EYE` 표.
몸 — 엘프 비례(키를 키워 머리와 비율을 맞춘다) · **굽은 등**(트롤과 같은 식 — 허리 위로 높이의 제곱만큼 앞으로) · 머리는 그 앞에 처져 달린다 ·
다리는 **짐승 다리**(무릎 앞 · 뒤꿈치 들림 · 발끝으로 선다). 뼈대는 사람 것 그대로 두고 모양만 그렇게 빚는다 — 정강이 뼈가 정강이 + 발등을 한 덩이로 든다.
단위 m · 발이 원점 · +z 앞. 머리 부품은 머리 중심 원점.
실행: python lizard.py renders/lizard_v9 [full|heads|eyes|crest|snakes] — snakes는 뱀 둘의 색 여덟 줄(2분)"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.join(_here, '..', 'common')); _sys.path.insert(0, _os.path.join(_here, '..', 'dwarf'))
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, mirror, smin, smax, ell, rbox, sph, capsule, seg
import head_base as hb
import body as bd

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
def squash(p, y0, sy):
    """y0을 기준으로 세로를 sy배 눌러 본다 — 돌려준 점으로 잰 거리에 sy를 곱해 쓴다."""
    q = p.copy(); q[..., 1] = y0 + (p[..., 1] - y0) / sy; return q
def thin(p, sx):
    q = p.copy(); q[..., 0] = p[..., 0] / sx; return q
def sstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def cone(p, a, b, r): return seg(p, V(*a), V(*b), r, 0.003)
def far(p): return np.full(p.shape[:-1], 9.0, np.float32)
UP = np.array([0, 1, 0], np.float32)

class Leaves:
    """뒤로 누워 겹친 잎 비늘 한 묶음(숲살모사 — 사용자 11-12: *"가시는 징그럽다 · 사진과 안 닮았다"*) — 곤두선 가시가 아니라
    살에 누운 납작한 잎이고, 끝만 살짝 들려 앞줄이 뒷줄 밑동을 덮는다(솔방울). 좌우 대칭 — 받는 점은 |x|로 접어 잰다.
    색은 밑동이 어둡고 끝이 밝다(`tint`) — 겹친 잎이 한 장씩 읽힌다."""
    def __init__(s, flat=0.36): s.L = []; s.flat = flat; s.c = None
    def add(s, base, back, n, ln, wd, lift=0.30):
        n = nrm(n); back = np.asarray(back, np.float32); back = nrm(back - n * float(np.dot(back, n)))
        d = nrm(back + n * lift); sd = nrm(np.cross(n, d)); up = np.cross(d, sd)
        s.L.append((np.asarray(base, np.float32) - d * (wd * 0.25), d, sd, up, float(ln), float(wd)))
    def bound(s):
        pts = np.array([b + d * (ln * 0.5) for b, d, _, _, ln, _ in s.L], np.float32)
        s.c = (pts.min(0) + pts.max(0)) * 0.5; s.r = float(length(pts - s.c).max()) + max(l[4] for l in s.L)
    def _eval(s, p):
        m = mir(p); d = far(p); t = np.zeros(p.shape[:-1], np.float32)
        for b, dr, sd, up, ln, wd in s.L:
            q = m - b
            bd_ = length(q - dr * (ln * 0.5)) - (ln * 0.5 + wd * 0.5)
            near = bd_ < 0.02
            if not near.any(): d = np.minimum(d, bd_); continue
            qn = q[near]; u = (qn * dr).sum(-1); v = (qn * sd).sum(-1); w = (qn * up).sum(-1) / s.flat
            hh = np.clip(u / ln, 0, 1); r = 0.005 + (wd * 0.5 - 0.005) * (1 - hh) ** 0.75   # 둥근 밑동 → 뾰족한 끝
            e = (np.sqrt((u - hh * ln) ** 2 + v * v + w * w) - r) * s.flat * 1.6
            dn = d[near]; better = e < dn
            tn = t[near]; tn[better] = hh[better]; t[near] = tn
            d[near] = np.minimum(dn, e)
            far_ = ~near; d[far_] = np.minimum(d[far_], bd_[far_])
        return d, t
    def dist(s, p):
        if s.c is None: s.bound()
        return bounded(lambda q: s._eval(q)[0], p, s.c, s.r)
    def tint(s, dark, light, e0=0.25, e1=0.95):
        """밑동(dark) → 끝(light) — e0~e1에서 넘어간다."""
        D, Lc = np.asarray(dark, np.float32), np.asarray(light, np.float32)
        def c(p):
            k = sstep(e0, e1, s._eval(p)[1])[..., None]
            return D * (1 - k) + Lc * k
        return c
def grad(f, c, e=0.004):
    c = np.asarray(c, np.float32)
    return nrm([float(f((c + np.array(v, np.float32) * e)[None, :])[0] - f((c - np.array(v, np.float32) * e)[None, :])[0]) for v in ([1, 0, 0], [0, 1, 0], [0, 0, 1])])
def surf(f, a, dr, hi=0.6):
    """a(살 속)에서 dr 쪽으로 나가 살 겉면을 만나는 점 — 살 밖에서 시작하면 None."""
    a = np.asarray(a, np.float32); dr = nrm(dr)
    if float(f(a[None, :])[0]) > 0: return None
    lo = 0.0
    for _ in range(22):
        mid = (lo + hi) * 0.5
        if float(f((a + dr * mid)[None, :])[0]) > 0: hi = mid
        else: lo = mid
    return a + dr * lo

AMBER = (0.95, 0.72, 0.16); RED = (0.86, 0.16, 0.12); GREEN = (0.62, 0.82, 0.25); PALE = (0.80, 0.87, 0.92); BLACK = (0.05, 0.05, 0.06)
GREY = (0.62, 0.58, 0.52); YELLOW = (0.93, 0.80, 0.20)
BONE = (0.90, 0.86, 0.72); DARK = (0.06, 0.05, 0.05); MOUTH = (0.30, 0.07, 0.08); TONGUE = (0.20, 0.05, 0.08); WHITE = (0.96, 0.96, 0.96)
LEATHER = (0.34, 0.22, 0.13); CLOTH = (0.42, 0.14, 0.11)

# ── 몸 — 종류마다 체형 한 줄. 악어만 덩치가 크다 ─────────────────────────────────────────────────────────
#          이름      반폭(밑 엉덩이 허리 가슴 어깨)          깊이  손    팔K   다리K  몸통K 팔굵기 다리굵기 머리 어깨둥글림
LIZARD = ('Lizard', [0.23, 0.26, 0.27, 0.33, 0.35], 0.88, 0.32, 1.12, 1.37, 1.05, 1.15, 1.12, 1.0, 0.42)
CROC   = ('Croc',   [0.29, 0.33, 0.36, 0.44, 0.47], 1.02, 0.39, 1.12, 1.30, 1.12, 1.45, 1.32, 1.0, 0.34)
# **공룡 악어**(`brute` — 11-14 사용자: *"악어 버전을 좀 더 공룡 느낌으로 · 덩치가 컸으면 · 일반 리자드맨이랑 따로 빼서"* — 그림은 깃 밖
# `_refs/2026-11-14_lizard_Croc/`). 리자드맨과 다른 큰 종족으로 갈 몸이다 — 더 굽은 등 · 넓은 어깨 · 굵은 팔뚝과 큰 손 · 굵고 짧은 다리 · 굵은 꼬리.
# 몸 전체를 `BRUTE_S`배로 키워 짓는다(크기는 메시가 든다 — 엔트와 같다).
BRUTE  = ('Brute',  [0.30, 0.33, 0.33, 0.50, 0.58], 0.90, 0.42, 1.15, 1.95, 1.10, 1.40, 1.05, 1.0, 0.30)   # 사용자가 고쳐 그린 비율 · 굵은 팔다리 · **다리를 더 길게**(v12 — 다리K 1.70 → 1.95 · 몸통은 그대로 — v11은 골반을 넓혀 허리가 굵어졌다)
BRUTE_S = 1.45
# **공룡 악어 생김새 — 색 × 모양을 고른다**(11-15 사용자: *"공룡 악어도 look 더 고를 수 있게 · 색 둘 다 · 코에 뿔 달린 것도 · 한 번에 10개 정도 후보"*).
# 색은 팔레트(게임에서 프리팹 하나 — 개체마다 고른다) · 모양은 고르기 축(눈 위 뿔 · 정수리 · 코뿔 · 등 골판 크기). 지금 고른 것은 `BRUTE_LOOK`.
#                 비늘                  배                    뿔 · 골판              눈                    볏 막 끝(v19 — 차분하게 다시 · 밑동은 비늘 색)
BRUTE_PAL = {
    'code':     dict(skin=(1, 0, 0), belly=(0, 1, 0), horn=(1, 0, 0), eye=(1, 0, 0), fin=(0, 0, 1)),   # 굽기 — 색 자리에 칸 몫(`bake_croc.py` · 게임이 팔레트로 칠한다)
    'slate':    dict(skin=(0.28, 0.40, 0.50), belly=(0.80, 0.76, 0.62), horn=(0.80, 0.76, 0.62), eye=(0.95, 0.60, 0.14), fin=(0.72, 0.44, 0.30)),   # 청회색(v16)
    'moss':     dict(skin=(0.27, 0.38, 0.22), belly=(0.78, 0.74, 0.50), horn=(0.70, 0.64, 0.46), eye=(0.95, 0.75, 0.20), fin=(0.74, 0.52, 0.22)),   # 이끼
    'mud':      dict(skin=(0.42, 0.32, 0.22), belly=(0.80, 0.70, 0.52), horn=(0.88, 0.82, 0.66), eye=(0.90, 0.45, 0.10), fin=(0.58, 0.24, 0.16)),   # 진흙 갈색
    'sand':     dict(skin=(0.66, 0.56, 0.36), belly=(0.90, 0.84, 0.66), horn=(0.48, 0.36, 0.24), eye=(0.40, 0.62, 0.22), fin=(0.74, 0.44, 0.26)),   # 모래
    'obsidian': dict(skin=(0.14, 0.14, 0.17), belly=(0.42, 0.38, 0.35), horn=(0.80, 0.32, 0.14), eye=(0.95, 0.25, 0.10), fin=(0.72, 0.20, 0.10)),   # 흑요 — 붉은 뿔
    'rust':     dict(skin=(0.52, 0.24, 0.16), belly=(0.86, 0.72, 0.52), horn=(0.22, 0.17, 0.14), eye=(0.98, 0.80, 0.25), fin=(0.86, 0.64, 0.34)),   # 녹슨 붉은빛 — 검은 뿔
    'ash':      dict(skin=(0.66, 0.66, 0.62), belly=(0.88, 0.86, 0.80), horn=(0.30, 0.28, 0.28), eye=(0.80, 0.16, 0.16), fin=(0.62, 0.26, 0.26)),   # 잿빛
    'teal':     dict(skin=(0.16, 0.36, 0.36), belly=(0.72, 0.78, 0.62), horn=(0.86, 0.80, 0.60), eye=(0.98, 0.85, 0.30), fin=(0.84, 0.68, 0.36)),   # 청록
    'olive':    dict(skin=(0.36, 0.38, 0.20), belly=(0.64, 0.60, 0.38), horn=(0.26, 0.22, 0.15), eye=(0.90, 0.70, 0.10), fin=(0.70, 0.42, 0.16)),   # 늪 올리브
    'indigo':   dict(skin=(0.20, 0.22, 0.36), belly=(0.62, 0.66, 0.78), horn=(0.86, 0.82, 0.70), eye=(0.55, 0.90, 0.95), fin=(0.58, 0.72, 0.86)),   # 밤 남빛 — 얼음 눈
}
# 눈 위 능선 끝의 뿔 — (쪽, 길이, 굵기). 대 · 중 · 소(11-15 사용자: *"눈 위에 뿔이 대중소 버전별로"*) · 위로 솟은 것 ·
# 부러진 것(큰 뿔 한쪽이 밑동만 남았다 — *"큰 뿔이 부서진 거나 그런 디테일"*).
# v19 — 대 · 중 · 소가 길이로 확 갈린다(사용자: *"중대가 중간 크기 큰 크기 느낌이 아니잖아 — 큰 크기는 진짜 큰 크기"*). 0.16m를 넘는 뿔은 휘어 뒤 · 아래로 감긴다.
BRUTE_BROW = {'none': None, 'small': ([0.22, 0.50, -0.84], 0.060, 0.024), 'mid': ([0.22, 0.50, -0.84], 0.130, 0.034),
              'large': ([0.25, 0.45, -0.86], 0.300, 0.050), 'up': ([0.30, 0.85, -0.40], 0.150, 0.036), 'broken': ([0.25, 0.45, -0.86], 0.300, 0.050)}
FIN_ANG = np.radians([-35.0, -8.0, 20.0, 48.0, 76.0]); FIN_LEN = np.array([0.15, 0.20, 0.24, 0.25, 0.19], np.float32)   # 볏 가시살 — 뒤에서 위로 · 위쪽이 길다
FIN_AT = {'brute': ((0.200, 0.000, -0.070), 1.6, True), 'lizard': ((0.205, 0.010, -0.060), 1.35, False)}   # v18 첫 판은 머리 뒤에 숨어 작았다 — 볼 쪽으로 · 1.6배   # 볏 밑동(머리 좌표) · 크기
LIZ_FINS = False; LIZ_FIN_COL = (0.86, 0.30, 0.15)   # 도마뱀 볏 — 미리보기 판에서만 켠다(게임은 아직)
def fins(o, k, sharp=False):
    """**볏**(11-15 사용자 그림 — 턱 뒤 양옆에 펼친 지느러미: 가시살 사이에 막). 머리 좌표의 밑동 `o`에서 뒤 · 바깥 · 위로 부채꼴로 편다.
    (막, 가시살, 막 색) — 막은 밑동이 비늘 색 · 끝으로 갈수록 볏 색(v19 — 몸과 이어진다), 가시살은 뿔 색. 막 가장자리는 가시살 사이에서 오목하게 파인다.
    `sharp`(공룡 악어 — 11-15 사용자: *"볏이 공룡은 좀 더 날카로워야"*) — 가시살이 막 밖으로 길게 · 끝이 뒤로 휜 바늘 · 막은 깊이 파인다."""
    o = V(*o); back = nrm([0.70, 0.05, -0.72]); up = nrm(UP - back * float(np.dot(UP, back))); n = np.cross(back, up)
    ang, ln = FIN_ANG, FIN_LEN * k
    def membrane(p):
        q = mirror(p) - o; h, u, v = (q * n).sum(-1), (q * back).sum(-1), (q * up).sum(-1)
        r = np.sqrt(u * u + v * v); a = np.arctan2(v, u)
        f = np.clip((a - ang[0]) / (ang[-1] - ang[0]), 0, 1) * (len(ang) - 1)
        R = np.interp(a, ang, ln) * ((0.64 + 0.36 * np.abs(np.cos(np.pi * f)) ** 2) if sharp else (0.74 + 0.26 * np.abs(np.cos(np.pi * f))))
        sector = np.maximum(ang[0] - a, a - ang[-1]) * np.maximum(r, 0.02)
        return np.maximum(np.maximum(np.abs(h) - 0.005 * k, r - R), sector)
    def spines(p):
        m = mirror(p); d = far(p)
        for a, L_ in zip(ang, ln):
            dr = back * np.cos(a) + up * np.sin(a)
            if sharp:   # 막 끝에서 30% 더 나가 뒤로 휜 바늘
                t0 = o + dr * L_ * 0.95; dr2 = nrm(dr + back * 0.45)
                d = np.minimum(d, smin(seg(m, o - dr * 0.02, t0, 0.011 * k, 0.006 * k), seg(m, t0, t0 + dr2 * L_ * 0.30, 0.006 * k, 0.0015), 0.004))   # v19 둘째 — 굵으면 막이 가려 가시 다발로 읽혔다
            else: d = np.minimum(d, seg(m, o - dr * 0.02, o + dr * L_ * 1.04, 0.016 * k, 0.003))
        return d
    def col(base, tip):
        B, T = np.asarray(base, np.float32), np.asarray(tip, np.float32)
        def c(p):
            q = mirror(p) - o; t = sstep(0.04 * k, ln.max() * 0.85, np.sqrt(((q * back).sum(-1)) ** 2 + ((q * up).sum(-1)) ** 2))[..., None]
            return B * (1 - t) + T * t
        return c
    return membrane, spines, col
BRUTE_NOSE = {'none': [],                                                                                         # 코뿔 — [((밑동, 꺾임), 끝, 굵기)]
              # 콧구멍(z 0.46) 바로 뒤 콧등에서 — v17 첫 판은 z 0.37로 이마 쪽에 붙어 뿔이 눈 앞에서 솟아 보였다
              'nub':   [(((0, 0.09, 0.44), (0, 0.145, 0.455)), (0, 0.18, 0.44), 0.034)],                          # 작은 혹뿔
              'rhino': [(((0, 0.08, 0.43), (0, 0.20, 0.465)), (0, 0.29, 0.41), 0.048)],                           # 크게 휘어 뒤로 — 코뿔소
              'twin':  [(((0, 0.08, 0.44), (0, 0.17, 0.465)), (0, 0.24, 0.43), 0.040),                            # 앞 큰 것 + 뒤 작은 것
                        (((0, 0.11, 0.31), (0, 0.17, 0.32)), (0, 0.21, 0.29), 0.030)],
              'broken': [(((0, 0.08, 0.43), (0, 0.20, 0.465)), (0, 0.29, 0.41), 0.048, 0.55)]}                   # 코뿔소 뿔 끝이 부러졌다(꺾임 위 55%에서 비스듬히 잘림)
BRUTE_BACK = {'tall': 1.35, 'mid': 1.0, 'low': 0.6}   # 등 · 목 · 꼬리 골판의 키
# **실루엣 축**(11-15 사용자: *"장식이 다 달라도 생긴 게 다 똑같아서 다른 유닛이라는 생각이 안 든다"*) — 멀리서 갈리는 것은 덩어리다. 머리 셋
# (v21에 체형 셋 · 투구 머리도 그렸다가 걷었다 — 사용자: *"투구 머리만 빼면 될 거 같아 · 체형은 바꾸지 말자"*).
#   머리 — 머리 좌표를 늘이고 줄인다(뿔 · 이 · 눈이 같이 따라간다)
BRUTE_HEADS = ('deep', 'long', 'broad')   # 짧고 깊은 턱(지금) · 긴 주둥이 · 넓적하고 뭉툭한
def head_warp(p, look=None):
    """머리 모양 축 — 머리 좌표 → 바탕 머리 좌표(그리고 거리 배수). 늘이면 거리가 줄어 안전하고, 줄이면 넘치니 배수로 누른다."""
    k = (look or BRUTE_LOOK).get('head', 'deep'); x, y, z = p[..., 0], p[..., 1], p[..., 2]
    if k == 'long':    # 주둥이가 앞으로 1.45배(눈 앞부터) · 조금 낮게
        z = np.where(z > 0.06, 0.06 + (z - 0.06) / 1.45, z); y = np.where(z > 0.06, y * 1.06, y)
        return np.stack([x, y, z], -1), 1.0
    if k == 'broad':   # 좌우로 1.22배 · 주둥이는 짧고 뭉툭 · 납작하게
        z = np.where(z > 0.10, 0.10 + (z - 0.10) * 1.18, z)
        return np.stack([x / 1.22, y * 1.08, z], -1), 1 / 1.18
    return p, 1.0
BRUTE_TAILS = ('plain', 'mace', 'horns')    # 꼬리 — 그대로 · 끝에 철퇴(둥근 뼈 공 + 큰 가시) · 양옆 낫 뿔(v22 사용자 그림 — v19의 꼬리 끝 큰 뿔 두 쌍을 바꿨다)
BRUTE_FINS = ('none', 'fins')               # 턱 뒤 볏(가시살 + 막)
BRUTE_LOOK = dict(pal='slate', brow='large', crown='plates', nose='none', back='mid', fins='none', tail='plain', head='deep')   # v16 그대로
BRUTE_LOOKS = [   # 후보 열 — 사용자가 고른다(색과 모양은 따로 고르는 축이라 섞어 보여 준다). v18 — 새 축(대중소 뿔 · 부러진 뿔 · 볏 · 꼬리 곤봉)이 다 한 번씩 나온다
    dict(pal='slate',    brow='none',   crown='plates', nose='none',   back='mid',  fins='fins', tail='plain', head='deep'),
    dict(pal='moss',     brow='small',  crown='plates', nose='nub',    back='mid',  fins='none', tail='mace', head='long'),
    dict(pal='mud',      brow='up',     crown='none',   nose='rhino',  back='tall', fins='none', tail='horns', head='broad'),
    dict(pal='sand',     brow='none',   crown='spikes', nose='broken', back='low',  fins='fins', tail='plain', head='deep'),
    dict(pal='obsidian', brow='broken', crown='frill',  nose='twin',   back='tall', fins='none', tail='horns', head='deep'),
    dict(pal='rust',     brow='small',  crown='spikes', nose='nub',    back='mid',  fins='none', tail='mace', head='long'),
    dict(pal='ash',      brow='mid',    crown='frill',  nose='none',   back='low',  fins='none', tail='plain', head='broad'),
    dict(pal='teal',     brow='large',  crown='plates', nose='twin',   back='mid',  fins='none', tail='mace', head='deep'),
    dict(pal='olive',    brow='broken', crown='none',   nose='broken', back='tall', fins='none', tail='mace', head='broad'),
    dict(pal='indigo',   brow='none',   crown='plates', nose='rhino',  back='low',  fins='fins', tail='horns', head='long'),
]
def set_look(look):
    """생김새를 세운다 — **볏과 눈 위 뿔은 같이 안 나온다**(11-15 사용자: *"공룡 악어는 뿔이랑 볏이 같이 나오면 안 될 거 같아"* — 둘 다 머리 뒤에 몰려
    엉킨다). 볏이 있으면 눈 위 뿔은 없다(게임의 고르기 축도 같은 규칙을 든다)."""
    global BRUTE_LOOK
    BRUTE_LOOK = dict(look)
    if BRUTE_LOOK.get('fins') == 'fins': BRUTE_LOOK['brow'] = 'none'
#           체형     머리 배수  굽힘    목 굵기  꼬리 굵기
BODY = {'brute':  (BRUTE,  0.95, 0.42, 0.250, 1.50),
        'lizard': (LIZARD, 1.00, 0.30, 0.150, 1.00),
        'python': (LIZARD, 1.00, 0.30, 0.160, 1.10),
        'viper':  (LIZARD, 1.00, 0.30, 0.122, 0.95),
        'croc':   (CROC,   1.16, 0.34, 0.215, 1.38)}
HEAD_FWD, HEAD_DROP = 0.17, 0.17   # 머리가 굽은 어깨보다 앞으로 · 아래로
HEAD_POSE = {'brute': (0.30, 0.12)}   # 종류마다 다른 머리 자리(앞으로 · 아래로) — 공룡 악어는 굵은 목 끝에 머리가 낮게 앞으로 나온다

NECK_TAPER = {'brute': 0.78}   # 목이 머리 쪽으로 가늘어지는 비(없으면 1) — 공룡 악어는 머리가 작아 목이 머리통보다 굵으면 얼굴이 목에 묻힌다(v6 · 사용자)
def set_body(kind):
    """그 종류의 몸 수치를 모듈 전역에 세운다 — 아래 몸 함수들이 읽는다(한 프로세스가 한 그림만 그린다)."""
    global D, HK, LEAN, NECK_R, TAIL_K, Y0, TOP, HO, TAIL, TAIL_R, _TZ, _TY, _TR, NECK_TIP, LIMB_IN
    NECK_TIP = NECK_TAPER.get(kind, 1.0); LIMB_IN = kind == 'brute'
    shape, HK, LEAN, NECK_R, TAIL_K = BODY[kind]
    D = bd.dims(shape); TOP = D['top']; Y0 = D['lift'] + 0.22 * D['torsoH']
    fwd, drop = HEAD_POSE.get(kind, (HEAD_FWD, HEAD_DROP))
    HO = V(0, TOP + bd.HeadGap + hb.H * 0.5 - drop, bd.HeadZ + float(zs(TOP)) + fwd)
    # 꼬리 — 엉덩이에서 뒤로 내려가 땅 가까이에서 눕는다. 3차 곡선 열두 마디 · 굵기는 뿌리에서 끝까지 한 번에 준다.
    tp = np.array([[D['lift'] + 0.115, -0.08], [D['lift'] - 0.02, -0.48 * TAIL_K ** 0.5], [0.11 * TAIL_K, -0.78 * TAIL_K ** 0.5], [0.10 * TAIL_K, -1.26 * TAIL_K ** 0.5]], np.float32)
    t = np.linspace(0, 1, 13)
    b = ((1 - t) ** 3)[:, None] * tp[0] + (3 * (1 - t) ** 2 * t)[:, None] * tp[1] + (3 * (1 - t) * t ** 2)[:, None] * tp[2] + (t ** 3)[:, None] * tp[3]
    TAIL = [V(0, y, z) for y, z in b]; TAIL_R = [0.168 * TAIL_K * (1 - u) ** 1.25 + 0.006 for u in t]
    _TZ = np.array([-z for _, z in b], np.float32); _TY = np.array([y for y, _ in b], np.float32); _TR = np.array(TAIL_R, np.float32)

def zs(y):   # 굽은 등 — 허리(Y0) 위로 올라갈수록 앞으로(높이의 제곱 · 트롤과 같은 식)
    return LEAN * np.maximum(y - Y0, 0) ** 2
def bend(p):   # 굽기 전 좌표로
    q = p.copy(); q[..., 2] = p[..., 2] - zs(p[..., 1]); return q

def mir(p): return np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
def torso(p):
    b = bend(p)
    y = b[..., 1] - D['lift']; t = y / D['torsoH']
    r = bd.torso_r(D['half'], t, D['torsoH'], D['topR'])
    q = np.sqrt(b[..., 0] ** 2 + (b[..., 2] / D['depth']) ** 2)
    return np.maximum((q - r) * D['depth'], np.maximum(-y, y - D['torsoH'])) * 0.9
def back_z(y):
    t = (y - D['lift']) / D['torsoH']; return -float(bd.torso_r(D['half'], t, D['torsoH'], D['topR'])) * D['depth'] + float(zs(y))
def neck(p):   # 굽은 어깨에서 앞 · 위로 뻗어 머리통 뒤 밑으로
    return seg(p, V(0, TOP - 0.08, float(zs(TOP)) - 0.05), HO + V(0, -0.07, -0.13) * HK, NECK_R * 1.18, NECK_R * NECK_TIP)
def arm_z(): return float(zs(D['shY']))
def arms(p): return capsule(mir(p), V(D['armX'], D['handY'], arm_z() + 0.04), V(D['armX'], D['shY'], arm_z()), D['armR'])
def hand_c(): return V(D['armX'], D['handY'] - D['hand'] * 0.25, arm_z() + 0.04)
def hands(p): return sph(mir(p), hand_c(), D['hand'] * 0.5)
def hand_claws(p):
    q = mir(p); c = hand_c(); r = D['hand'] * 0.5
    return union([cone(q, (c[0] + dx, c[1] - r * 0.55, c[2] + 0.045), (c[0] + dx * 1.3, c[1] - r * 1.35, c[2] + 0.085), 0.022 * r / 0.118) for dx in (-0.05, 0.0, 0.05)])

# 짐승 다리 — 엉덩이 → 무릎(앞) → 뒤꿈치(뒤 · 들림) → 발볼(땅). 허벅지는 굵고 아래로 가늘어진다.
def leg_pts():
    x, L, r = D['legX'], D['lift'], D['legR']
    return (V(x, D['hipY'], 0.0), V(x, L * 0.66, 0.135), V(x, L * 0.30, -0.105), V(x, 0.060, 0.055)), r
def legs(p):
    (h, k, a, b), r = leg_pts(); q = mir(p)
    d = seg(q, h, k, r * 1.22, r * 1.02)
    d = smin(d, seg(q, k, a, r * 0.98, r * 0.66), 0.035)
    return smin(d, seg(q, a, b, r * 0.66, r * 0.70), 0.03)
TOES = (-0.075, 0.0, 0.075)
def feet(p):   # 발끝으로 선다 — 발볼에서 벌어진 발가락 셋
    (_, _, _, b), r = leg_pts(); q = mir(p); k = r / 0.106
    pad = ell(q, b + V(0, -0.005, 0.02), V(0.105 * k, 0.055, 0.10 * k))
    toes = union([seg(q, b + V(dx * 0.6 * k, -0.012, 0.05), b + V(dx * 1.4 * k, -0.02, 0.215 * k), 0.042 * k, 0.032 * k) for dx in TOES])
    return smin(pad, toes, 0.03)
def foot_claws(p):
    (_, _, _, b), r = leg_pts(); q = mir(p); k = r / 0.106
    return union([cone(q, b + V(dx * 1.4 * k, -0.018, 0.21 * k), b + V(dx * 1.6 * k, -0.045, 0.30 * k), 0.026 * k) for dx in TOES])

def tail(p):
    d = seg(p, TAIL[0], TAIL[1], TAIL_R[0], TAIL_R[1])
    for i in range(1, len(TAIL) - 1): d = smin(d, seg(p, TAIL[i], TAIL[i + 1], TAIL_R[i], TAIL_R[i + 1]), 0.02)
    return d
SPINE_GAP = 0.030   # 가시 사이 — 머리 가시 테 · 목 · 등 · 꼬리가 다 같다(11-14 사용자: *"꼬리 · 등 · 머리 쪽 뿔 간격이 너무 다르다"*)
def fringe_rows():
    """가시 테가 이어지는 등의 가시 두 줄(볏도마뱀붙이 · 11-14 사용자: *"등이랑 이어지는 뿔들도 두 갈래짜리가 아니라 다른 한 줄"*) —
    머리 가시 테 끝(뒤통수 옆 아래)에서 목 뒤를 지나 등 양쪽으로, 꼬리 윗면 양옆을 따라 끝까지(*"꼬리까지 쭉"*). 오른쪽(+x)만 적고 거울로 쓴다.
    줄을 촘촘히 짚어 겉면 위의 길을 만든 뒤 그 길을 따라 `SPINE_GAP`마다 하나씩 놓는다. (밑동, 끝, 굵기) 목록."""
    body = lambda q: smin(torso(q), neck(q), 0.05)
    skin_tail = lambda q: smin(torso(q), tail(q), 0.06)
    na, nb = HO + V(0, -0.07, -0.13) * HK, V(0, TOP - 0.08, float(zs(TOP)) - 0.05)   # 목 축(머리 쪽 → 어깨)
    guide = []   # (속 점, 나가는 쪽, 크기, 눕는 쪽, 겉면 함수)
    for t in np.linspace(0.15, 1.0, 14):                                 # 목
        guide.append((na + (nb - na) * t, nrm([0.78, 0.15, -0.60]), 1.0, None, body))
    for y in np.linspace(TOP - 0.12, D['lift'] + 0.28, 24):             # 등 — 엉덩이로 갈수록 작다
        guide.append((V(0, y, float(zs(y))), nrm([0.62, 0.05, -0.78]), 0.55 + 0.45 * (y - D['lift']) / D['torsoH'], None, body))
    for i in range(1, len(TAIL) - 1):                                    # 꼬리 — 윗면 양옆 · 꼬리 끝 쪽으로 눕는다
        for u in np.linspace(0, 1, 6, endpoint=False):
            c = TAIL[i] + (TAIL[i + 1] - TAIL[i]) * u; r = TAIL_R[i] + (TAIL_R[i + 1] - TAIL_R[i]) * u
            ax = nrm(TAIL[min(i + 1, len(TAIL) - 1)] - TAIL[i - 1]); up = nrm(np.cross(np.cross(ax, UP), ax)); side = nrm(np.cross(up, ax))
            if side[0] < 0: side = -side
            guide.append((c, nrm(up * 0.70 + side * 0.72), 0.35 + 0.65 * r / TAIL_R[0], ax, skin_tail))
    path = []   # 겉면 위의 길 — (점, 나가는 쪽, 크기, 눕는 쪽)
    for a, dr, k, lay, f in guide:
        b = surf(f, a, dr)
        if b is not None: path.append((b, dr, k, lay))
    out, acc = [], SPINE_GAP * 0.5
    for (b0, d0, k0, l0), (b1, d1, k1, l1) in zip(path[:-1], path[1:]):
        L = float(np.linalg.norm(b1 - b0))
        while acc <= L:
            t = acc / max(L, 1e-6); b = b0 + (b1 - b0) * t; dr = nrm(d0 + (d1 - d0) * t); k = k0 + (k1 - k0) * t
            lay = l0 if l0 is not None else l1
            tip = nrm(dr * 0.70 + lay * 0.65) if lay is not None else nrm(dr * 0.75 + V(0, 0.15, -0.55))   # 꼬리는 꼬리 끝으로 · 몸은 뒤로 눕는다
            out.append((b - dr * 0.006, b + tip * (0.040 * k), 0.016 * k))
            acc += SPINE_GAP
        acc -= L
    return out
FROWS = None
def torso_rad(y):   # 그 높이의 몸통 반지름(굽기 전)
    return float(bd.torso_r(D['half'], (y - D['lift']) / D['torsoH'], D['torsoH'], D['topR']))
def brute_muscles(p):
    """몸통의 살 — **덩이를 붙이지 않는다**(v5는 가슴근 두 덩이가 여자 가슴 같았다 — 사용자: *"몸은 자연스럽게 · 억지로 근육 붙이니깐"*).
    가슴 전체가 하나의 두꺼운 통으로 부풀고(가운데 하나 · 넓게 녹인다) 목에서 어깨로 승모근이 완만하게 흐른다."""
    q = mir(p)
    y1 = TOP - 0.24; chest = ell(p, V(0, y1, float(zs(y1)) + torso_rad(y1) * D['depth'] * 0.30), V(torso_rad(y1) * 1.02, 0.24, torso_rad(y1) * D['depth'] * 0.92))
    sh = brute_arm_pts()[0]   # 승모근 — 목 옆에서 높이 솟아 올라간 어깨로 흐른다(트롤처럼 — 사용자: *"승모근을 더 올리고 어깨 위치를 더 올려도"*). 머리보다 뒤에 선다
    trap = seg(q, V(0.11, TOP + 0.14, float(zs(TOP)) - 0.08), V(float(sh[0]) - 0.04, float(sh[1]) + 0.07, float(sh[2]) - 0.02), 0.165, 0.115)   # v10 — 더 크고 높게(사용자: *"승모근 더 키워봐"*)
    return smin(chest, trap, 0.08)
BRUTE_SH_UP = 0.09   # 어깨 관절을 이만큼 올린다(트롤처럼 솟은 어깨)
def brute_arm_pts():
    az = arm_z(); r = D['armR']
    sh = V(D['armX'], D['shY'] + BRUTE_SH_UP, az); wr = V(D['armX'] + 0.025, D['hem'] + 0.03, az + 0.15)   # 손목이 허리띠 높이 · 조금 앞
    el = sh + (wr - sh) * 0.50 + V(0.050, 0.0, -0.075)   # 팔꿈치 — 뒤 · 바깥(힘을 뺀 팔이 살짝 굽어 늘어진다)
    return sh, el, wr, r
def brute_arms(p):
    """굵은 팔 — **한 줄기로 흐른다**(v9는 삼각근 · 이두 · 삼두 · 팔뚝 덩이가 따로 불거져 울퉁불퉁했다 — 사용자: *"팔 좀 더 자연스럽게"*):
    둥근 어깨 → 팔꿈치로 가늘어지는 위팔 → 다시 굵어지는 팔뚝 → 손목. 살 덩이는 둘만(위팔 앞 · 팔뚝) 넓게 녹인다."""
    q = mir(p); sh, el, wr, r = brute_arm_pts(); lu = float(np.linalg.norm(el - sh)); ll = float(np.linalg.norm(wr - el))
    d = seg(q, sh, el, r * 1.04, r * 0.84)
    d = smin(d, seg(q, el, wr, r * 0.84, r * 0.66), 0.06)                                                # 아래팔 뼈대
    d = smin(d, ell(q, sh + V(0.02, 0.015, 0.0), V(r * 1.26, r * 1.12, r * 1.26)), 0.10)                # 둥근 어깨
    d = smin(d, ell(q, sh + (el - sh) * 0.50 + V(0, 0, r * 0.20), V(r * 0.88, lu * 0.40, r * 0.92)), 0.09)   # 위팔 앞의 살
    return smin(d, ell(q, el + (wr - el) * 0.34 + V(r * 0.05, 0, r * 0.04), V(r * 1.02, ll * 0.40, r * 1.02)), 0.09)   # 팔뚝
BRUTE_HAND_TURN = -35.0   # 손을 세로축으로 돌리는 각(도) — 0이면 손바닥이 몸 쪽을 곧게 본다 · 음수면 손바닥이 몸 쪽 · 뒤로 비스듬히(힘을 뺀 손 · 정면에서 손가락이 어긋나 보인다 · v16)
BRUTE_PALM_T = 0.48      # 손바닥 두께(반폭 × 손 반지름) — v15 0.34는 너무 얇았다
def _hand_rot():
    a = np.radians(BRUTE_HAND_TURN); ca, sa = np.cos(a), np.sin(a)
    return np.array([[ca, 0, sa], [0, 1, 0], [-sa, 0, ca]], np.float32)   # 손 좌표 → 몸 좌표(세로축 둘레)
def brute_fingers():
    """손가락 셋 + 엄지(오른손 · 거울로 쓴다 · +x가 바깥) — 저마다 (밑동, 첫 마디, 둘째 마디, 끝). **손바닥이 몸 쪽(허벅지)을 본다**(v15 — 사용자:
    *"그냥 손바닥이 몸 쪽을 보게 해 놓으면 안 됐던 거야?"*): 손가락은 앞뒤로 나란히 서고 몸 쪽(손바닥 쪽)으로 살짝 말린다 · 가운데가 조금 길다 ·
    엄지는 손 앞 가장자리에서 손바닥 쪽으로 붙어 내려온다(앞으로 안 튀어나온다)."""
    _, _, wr, _ = brute_arm_pts(); h = D['hand'] * 0.5; c = wr + V(0, -h * 0.60, 0.0)
    R = _hand_rot(); T = lambda v: c + R @ (np.asarray(v, np.float32) - c)   # 손 둘레로 돌린다
    fs = []
    for dz, k in ((-0.60, 0.92), (0.0, 1.06), (0.60, 0.94)):
        a = c + V(0, -h * 0.50, dz * h * 0.60)
        m1 = a + V(-h * 0.10, -h * 0.58 * k, dz * h * 0.10)
        m2 = m1 + V(-h * 0.18, -h * 0.40 * k, dz * h * 0.04)
        fs.append((a, m1, m2, m2 + V(-h * 0.22, -h * 0.26 * k, 0)))
    a = c + V(-h * 0.22, -h * 0.02, h * 0.70); m1 = a + V(-h * 0.22, -h * 0.36, h * 0.04); m2 = m1 + V(-h * 0.10, -h * 0.28, -h * 0.04)
    fs.append((a, m1, m2, m2 + V(-h * 0.04, -h * 0.16, -h * 0.06)))   # 엄지 — 앞 가장자리에서 손바닥 쪽으로
    return c, h, [tuple(T(v) for v in f) for f in fs]
def brute_hands(p):
    q = mir(p); c, h, fs = brute_fingers(); _, _, wr, r = brute_arm_pts()
    ql = c + (q - c) @ _hand_rot()                                                                       # 손 좌표로(돌림을 되돌린다)
    d = ell(ql, c, V(h * BRUTE_PALM_T, h * 0.74, h * 0.84))                                               # 손바닥 — 넓적한 판(몸 쪽을 본다)
    d = smin(d, ell(q, (wr + c) * 0.5, V(r * 0.52, h * 0.55, r * 0.62)), 0.05)                          # 손목 → 손바닥으로 넓어진다
    for a, m1, m2, t in fs:
        f = np.minimum(np.minimum(seg(q, a, m1, h * 0.25, h * 0.22), seg(q, m1, m2, h * 0.22, h * 0.19)), seg(q, m2, t, h * 0.19, h * 0.15))
        d = smin(d, f, 0.03)
    return d
def brute_hand_claws(p):   # 손톱 — 손가락이 말리는 쪽으로 뻗고 끝이 굽는다
    q = mir(p); _, h, fs = brute_fingers(); d = far(p)
    for a, m1, m2, t in fs:
        dr = nrm(t - m2); c1 = t + dr * h * 0.28 + V(0, -h * 0.06, 0)
        d = np.minimum(d, np.minimum(seg(q, t - dr * h * 0.04, c1, h * 0.15, h * 0.09), seg(q, c1, c1 + nrm(dr + V(0, -0.8, 0)) * h * 0.24, h * 0.09, 0.004)))
    return d
BRUTE_LEG_X = 0.215   # 다리 자리 — 엉덩이 폭에서 안 낸다(좁은 허리에 다리가 가운데로 붙어 두 발이 겹쳤다)
def brute_leg_pts():
    """**역관절**(v13 — 사용자: *"다리 역관절을 더 확실하게"*) — 엉덩이(몸통 밑) → 무릎(앞으로 크게) → 뒤꿈치(뒤 · 위로 높이 — 땅에 안 닿는다) →
    발볼(발가락으로 선다). 발등뼈가 뒤꿈치에서 비스듬히 길게 내려온다. v8~12는 앞에서 곧은 기둥으로 보이게 꺾임을 줄였었다."""
    L_, r = D['lift'], D['legR']; x = BRUTE_LEG_X
    return (V(x, L_ - 0.01, 0.0), V(x, L_ * 0.64, 0.240), V(x, L_ * 0.25, -0.150), V(x, 0.062, 0.060)), r   # v14 — 무릎을 더 앞 · 위로, 뒤꿈치를 조금 내려 종아리가 길다
def brute_legs(p):
    (h, k, a, b), r = brute_leg_pts(); q = mir(p); lt = float(np.linalg.norm(k - h)); ls = float(np.linalg.norm(a - k))
    d = seg(q, h, k, r * 1.12, r * 0.94)
    d = smin(d, ell(q, h + (k - h) * 0.50 + V(r * 0.06, 0, r * 0.26), V(r * 0.96, lt * 0.44, r * 1.06)), 0.06)   # 넓적다리 근육(앞으로 — 옆으로 부풀면 두 다리가 붙는다)
    d = smin(d, seg(q, k, a, r * 0.86, r * 0.56), 0.04)                                                # 정강이 — 뒤꿈치로 가늘어진다
    d = smin(d, ell(q, k + (a - k) * 0.32 + V(0, r * 0.10, -r * 0.30), V(r * 0.78, ls * 0.30, r * 0.80)), 0.05)   # 장딴지 — 정강이 뒤
    d = smin(d, sph(q, a, r * 0.60), 0.03)                                                             # 뒤꿈치 마디
    return smin(d, seg(q, a, b, r * 0.56, r * 0.62), 0.03)                                             # 발등뼈 — 비스듬히 길게
# 공룡 발 — 발가락 셋이 **앞을 향해 조금만 벌어진다**(앞에서 보면 발톱 셋이 W로 — 사용자가 고쳐 그린 그림).
# **마디가 있다**(v9는 곧은 막대였다 — 사용자: *"발가락 좀 더 자연스럽게"*): 발등에서 부채꼴로 갈라져 → 가운데 마디가 솟고 → 끝이 땅으로 내려간다.
# 발톱은 발가락 끝 위에서 나와 갈고리처럼 굽어 땅에 닿는다. (밑동 x, 끝 x, 길이 z)
BTOES = ((-0.075, -0.150, 0.225), (0.0, 0.0, 0.265), (0.075, 0.150, 0.225))
def brute_toe_pts():
    (_, _, a, b), r = brute_leg_pts(); out = []
    for x0, x1, z in BTOES:
        base = b + V(x0, 0.004, 0.035); tip = b + V(x1, -0.020, z)
        knuck = base + (tip - base) * 0.52 + V(0, 0.034, 0)   # 가운데 마디 — 솟는다
        out.append((base, knuck, tip))
    return a, b, r, out
def brute_feet(p):
    a, b, r, toes = brute_toe_pts(); q = mir(p)
    d = ell(q, b + V(0, 0.000, -0.005), V(0.120, 0.058, 0.105))                                  # 발볼
    d = smin(d, seg(q, b + V(0, 0.030, -0.03), b + V(0, 0.012, 0.075), 0.085, 0.095), 0.04)      # 발등 — 발가락으로 넓어진다
    for base, kn, tip in toes:
        d = smin(d, np.minimum(seg(q, base, kn, 0.054, 0.048), seg(q, kn, tip, 0.048, 0.038)), 0.035)
    return d
def brute_foot_claws(p):   # 갈고리 발톱 — 발가락 끝 위에서 나와 앞으로 뻗다 굽어 내려온다
    a, b, r, toes = brute_toe_pts(); q = mir(p); d = far(p)
    for base, kn, tip in toes:
        dr = nrm(V(float(tip[0] - kn[0]), 0, float(tip[2] - kn[2])))
        c0 = tip + V(0, 0.016, 0) - dr * 0.015; c1 = c0 + dr * 0.085 + V(0, 0.012, 0); c2 = c1 + dr * 0.070 + V(0, -0.050, 0)
        d = np.minimum(d, np.minimum(seg(q, c0, c1, 0.042, 0.030), seg(q, c1, c2, 0.030, 0.005)))
    return d
def club_frame():
    """꼬리 끝 곤봉의 자리 — 꼬리 끝 두 마디 사이(중심 · 꼬리 쪽 축)."""
    c = TAIL[11] + (TAIL[12] - TAIL[11]) * 0.55; ax = nrm(TAIL[12] - TAIL[10])
    return c, ax
def tail_club(p, look=None):
    """**꼬리 끝 철퇴**(11-15 사용자: *"안킬로사우루스처럼 꼬리 끝에 둔기"* → v19 *"엉덩이 같은 느낌이 아니라 그냥 철퇴 · 아니면 뿔만 크게"*) —
    둥근 뼈 공 하나(살과 녹는 목 · 공은 뿔 색 조각 `club_bony`가 덮는다). 뿔만 단 꼬리(`horns`) · 그대로인 꼬리면 없다."""
    if (look or BRUTE_LOOK)['tail'] != 'mace': return far(p)
    c, ax = club_frame()
    return seg(p, c - ax * 0.22, c, 0.040, 0.085)   # 꼬리 끝에서 공으로 굵어지는 목
MACE_DIRS = [nrm(v) for v in ((1, 0.2, 0.1), (0.5, 1, 0.2), (0.5, 0.15, -1), (0.6, -0.55, 0.3), (0.45, 0.6, 0.8))]   # 철퇴 가시 — 오른쪽 반(거울)
def club_bony(p, look=None):
    """철퇴 — 둥근 뼈 공 + 사방으로 박힌 큰 가시(뿔 색). 뿔 — 꼬리 끝 양옆 · 위로 뻗은 큰 뿔 두 쌍(스테고사우루스 꼬리처럼)."""
    t = (look or BRUTE_LOOK)['tail']
    if t == 'plain': return far(p)
    c, ax = club_frame(); m = mir(p); d = far(p)
    if t == 'mace':
        d = sph(p, c, 0.120)
        for dr in MACE_DIRS: d = np.minimum(d, seg(m, c + dr * 0.08, c + dr * 0.235, 0.042, 0.004))
        d = np.minimum(d, seg(p, c + ax * 0.08, c + ax * 0.25, 0.040, 0.004))   # 끝 가시
    else:   # horns — 꼬리 양옆을 따라 넓적한 낫 뿔 넷씩(v22 — 사용자 그림 `_refs/2026-11-15_lizard_Croc/user_tail_horns.png`).
        # 밑동이 넓고 바깥 · 조금 뒤(엉덩이 쪽)로 휘며 위아래로 납작한 칼날이다 · 꼬리 끝으로 갈수록 작다.
        for i, ln in ((4, 0.25), (6, 0.22), (8, 0.18), (10, 0.13)):
            c0, r0 = TAIL[i], TAIL_R[i]; tax = nrm(TAIL[i + 1] - TAIL[i - 1])
            b = c0 + V(1, 0, 0) * r0 * 0.65; out = nrm(V(1, 0.08, 0) - tax * 0.30)
            mid = b + out * ln * 0.55 - tax * ln * 0.10; tip = mid + nrm(out - tax * 0.75) * ln * 0.55
            q = m.copy(); q[..., 1] = b[1] + (m[..., 1] - b[1]) / 0.40   # 위아래로 납작
            w = 0.040 + 0.20 * ln
            d = np.minimum(d, smin(seg(q, b - out * 0.02, mid, w, w * 0.62), seg(q, mid, tip, w * 0.62, 0.004), 0.01) * 0.40)
    return d
def brute_plates(back='mid'):
    """공룡 악어의 골판 — 목 뒤 · 등 · 꼬리 한가운데로 큰 판 한 줄(어깨에서 가장 크고 꼬리 끝으로 작아진다) + 꼬리 양옆 작은 가시.
    **판은 다 등뼈에서 바깥으로 선다**(꼬리 쪽으로 조금만 쓸린다) — v3는 등의 판만 아래로 누워 목 · 꼬리의 판과 방향이 달랐다
    (사용자: *"등에 혼자 반대 방향으로 나온 뿔도 좀 걸려"*). 등 양옆 가시 줄 · 혹 비늘은 걷었다. (한가운데 판, 옆 가시) — 저마다 (밑동, 끝, 굵기)."""
    body = lambda q: smin(torso(q), neck(q), 0.05)
    skin_tail = lambda q: smin(torso(q), tail(q), 0.06)
    na, nb = HO + V(0, -0.07, -0.13) * HK, V(0, TOP - 0.08, float(zs(TOP)) - 0.05)
    mid, side = [], []
    K = BRUTE_BACK[back]   # 골판 키(고르기 축)
    def plate(b, n, lay, ln, r): ln = ln * K; mid.append((b - n * 0.03, b + n * ln + lay * ln * 0.18, r))
    for t in np.linspace(0.10, 0.95, 4):                                  # 목 뒤
        a = na + (nb - na) * t; n = nrm([0, 0.80, -0.60]); b = surf(body, a, n)
        if b is not None: plate(b, n, nrm([0, -0.5, -0.85]), 0.09 + 0.07 * t, 0.056)
    for i, y in enumerate(np.linspace(TOP - 0.06, D['lift'] + 0.22, 6)):  # 등 — 어깨에서 가장 크다
        a = V(0, y, float(zs(y))); n = nrm([0, 0.45 - 0.07 * i, -1.0]); b = surf(body, a, n)
        if b is not None: plate(b, n, V(0, -1, 0), 0.175 - 0.016 * i, 0.060)
    for i in range(1, len(TAIL) - 1):                                      # 꼬리 — 윗면 한가운데 · 끝으로 작아진다
        ax = nrm(TAIL[i + 1] - TAIL[i - 1]); up = nrm(np.cross(np.cross(ax, UP), ax)); k = TAIL_R[i] / TAIL_R[0]
        b = surf(skin_tail, TAIL[i], up)
        if b is not None: plate(b, up, ax, 0.05 + 0.10 * k, 0.020 + 0.042 * k)
        sd = nrm(np.cross(up, ax)); sd = sd if sd[0] > 0 else -sd
        if 2 <= i < 8:
            dr = nrm(up * 0.75 + sd * 0.66); bs = surf(skin_tail, TAIL[i], dr)
            if bs is not None: side.append((bs - dr * 0.01, bs + nrm(dr + ax * 0.3) * (0.03 + 0.05 * k), 0.012 + 0.022 * k))
    return mid, side
BPLATES = {}   # 골판 키마다 한 번 잰다
def brute_plates_f(p, back='mid'):
    if back not in BPLATES: BPLATES[back] = brute_plates(back)
    mid, side = BPLATES[back]; d = far(p)
    q = thin(p, 0.34)                                                      # 판은 옆으로 납작하다
    for a, b, r in mid: d = np.minimum(d, seg(q, a, b, r, 0.008) * 0.34)
    m = mir(p)
    for a, b, r in side: d = np.minimum(d, seg(m, a, b, r, 0.004))
    return d
def fringe_rows_f(p):
    global FROWS
    if FROWS is None: FROWS = fringe_rows()
    m = mir(p); d = far(p)
    for a, b, r in FROWS: d = np.minimum(d, seg(m, a, b, r, 0.003))
    return d
def ridge(p):   # 등줄기에서 꼬리로 낮은 돌기 한 줄 — 끝으로 갈수록 작아지다 사라진다
    d = far(p)
    for i in range(1, 9):
        c, r = TAIL[i], TAIL_R[i]; ax = nrm(TAIL[i + 1] - TAIL[i - 1]); up = nrm(np.cross(np.cross(ax, UP), ax))
        d = np.minimum(d, seg(p, c + up * (r - 0.02), c + up * (r + 0.015 + r * 0.42) + ax * 0.03, 0.022 + r * 0.14, 0.004))
    for y in np.linspace(D['lift'] + 0.22, TOP - 0.08, 5):
        zb = back_z(y); k = TAIL_K ** 0.5
        d = np.minimum(d, seg(p, V(0, y, zb + 0.03), V(0, y + 0.02, zb - 0.075 * k), 0.042 * k, 0.004))
    return d
def body_leaves(lift=1.0):
    """살모사 몸의 잎 비늘 — 등(옆구리까지) · 어깨 · 꼬리 윗면. 아래(꼬리에선 끝) 쪽으로 누워 겹친다. 배 · 팔다리 안쪽은 매끈하다."""
    back, tl = Leaves(), Leaves()
    ln, wd = 0.136, 0.106; y = TOP + 0.005; r = 0
    while y > D['lift'] + 0.06:
        t = (y - D['lift']) / D['torsoH']; rr = float(bd.torso_r(D['half'], min(t, 0.985), D['torsoH'], D['topR'])); z0 = float(zs(y))
        amax = 1.50 if y > D['hem'] + 0.07 else 1.05          # 허리끈 아래는 꼬리 뿌리 둘레만
        for c in range(9):
            a = (c + (0.5 if r % 2 else 0.0)) * wd * 0.90 / max(rr, 0.08)
            if a > amax: break
            pt = V(rr * np.sin(a), y, -rr * D['depth'] * np.cos(a) + z0)
            n = nrm([np.sin(a) * D['depth'], 0.10, -np.cos(a)])
            back.add(pt, [0, -1, -2 * LEAN * max(y - Y0, 0)], n, ln, wd, 0.24 * lift)
        y -= ln * 0.50; r += 1
    def wrap(lv, a, b, ra, rb, out, a0, a1, ln, wd, lift, s0=0.0, s1=1.0):
        """a → b로 가는 굵은 선(팔 · 허벅지 · 목)의 둘레에 줄 맞춰 — out 쪽이 각도 0, 잎은 b 쪽으로 눕는다."""
        a, b = np.asarray(a, np.float32), np.asarray(b, np.float32); L = float(np.linalg.norm(b - a)); ax_ = (b - a) / L
        o = np.asarray(out, np.float32); o = nrm(o - ax_ * float(np.dot(o, ax_))); sd = np.cross(ax_, o)
        u = s0 * L; r = 0
        while u < s1 * L:
            rad = ra + (rb - ra) * u / L; da = wd * 0.90 / rad
            ang = a0 + (0.5 * da if r % 2 else 0.0)
            while ang <= a1:
                n = o * np.cos(ang) + sd * np.sin(ang); pt = a + ax_ * u + n * rad
                fl = V(-1, 1, 1) if pt[0] < 0 else V(1, 1, 1)
                lv.add(pt * fl, ax_ * fl, n * fl, ln, wd, lift)
                ang += da
            u += ln * 0.50; r += 1
    ax, sy, az, ar = D['armX'], D['shY'], arm_z(), D['armR']
    limbs = Leaves()
    for a_ in (-1.9, -1.1, -0.3):   # 어깨 꼭지 한 줄
        n = nrm([np.cos(a_) * 0.75, 0.66, np.sin(a_) * 0.75]); limbs.add(V(ax, sy, az) + n * ar, [n[0], -1, n[2]], n, 0.108, 0.090, 0.20 * lift)
    wrap(limbs, V(ax, sy, az), V(ax, D['handY'] + 0.10, az + 0.04), ar, ar, [1, 0, 0], -2.1, 0.35, 0.108, 0.090, 0.20 * lift, 0.04, 1.0)
    (hp, kn, _, _), lr = leg_pts()
    wrap(limbs, hp, kn, lr * 1.22, lr * 1.02, [1, 0, -0.5], -1.5, 0.75, 0.112, 0.094, 0.20 * lift, 0.30, 0.92)
    nb = HO + V(0, -0.07, -0.13) * HK; na = V(0, TOP - 0.08, float(zs(TOP)) - 0.05)
    wrap(back, nb, na, NECK_R, NECK_R * 1.18, [0, 0.35, -1], 0.0, 1.45, 0.104, 0.086, 0.20 * lift, 0.30, 0.95)
    # 꼬리 — 곡선을 따라 고리마다 엇갈려
    s_ = 0.10
    P = np.array(TAIL, np.float32); seglen = np.linalg.norm(P[1:] - P[:-1], axis=1); cum = np.concatenate([[0], np.cumsum(seglen)]); r = 0
    while s_ < cum[-1] - 0.10:
        i = min(int(np.searchsorted(cum, s_)) - 1, len(P) - 2); f = (s_ - cum[i]) / seglen[i]
        c = P[i] + (P[i + 1] - P[i]) * f; rad = TAIL_R[i] + (TAIL_R[i + 1] - TAIL_R[i]) * f
        ax_ = nrm(P[i + 1] - P[i]); up = nrm(np.cross(np.cross(ax_, UP), ax_)); sd = np.array([1, 0, 0], np.float32)
        l2 = 0.050 + rad * 0.62; w2 = l2 * 0.80
        for cc in range(7):
            a = (cc + (0.5 if r % 2 else 0.0)) * w2 * 0.90 / max(rad, 0.02)
            if a > 1.95: break
            n = up * np.cos(a) + sd * np.sin(a)
            tl.add(c + n * rad, ax_, n, l2, w2, 0.24 * lift)
        s_ += l2 * 0.50; r += 1
    return back, limbs, tl
def belt(p): return np.maximum(torso(p) - 0.014, np.abs(p[..., 1] - (D['hem'] + 0.02)) - 0.042)
def flap(p):   # 앞 가리개 — 뒤는 꼬리가 나와 비운다
    t = (D['hem'] - D['lift']) / D['torsoH']; zf = float(bd.torso_r(D['half'], t, D['torsoH'], D['topR'])) * D['depth']
    return rbox(p, V(0, D['hem'] - 0.155, zf + 0.012), V(0.105 * D['half'][1] / 0.246, 0.185, 0.014), 0.012)
def body_col(skin, belly):   # 배 · 꼬리 밑은 배 색
    S, B = np.asarray(skin, np.float32), np.asarray(belly, np.float32)
    def c(p):
        zr = p[..., 2] - zs(p[..., 1])
        front = sstep(0.02, 0.10, zr) * sstep(0.19, 0.13, np.abs(p[..., 0]) * 0.246 / D['half'][1]) * sstep(D['lift'], D['lift'] + 0.06, p[..., 1])
        ay = np.interp(-p[..., 2], _TZ, _TY); ar = np.interp(-p[..., 2], _TZ, _TR)   # 공룡 악어는 반지름 바닥 4cm — 곤봉의 위아래를 가른다
        under = sstep(-0.22, -0.34, p[..., 2]) * sstep(-0.15, -0.55, (p[..., 1] - ay) / np.maximum(ar, 0.04 if LIMB_IN else 0.01)) * sstep(D['lift'] + 0.2, D['lift'] + 0.1, p[..., 1])
        k = np.maximum(front, under)
        if LIMB_IN: k = np.maximum(k, limb_inner(p))
        return S * (1 - k[..., None]) + B * k[..., None]
    return c
def limb_inner(p):
    """**팔다리 안쪽도 배 색**(11-15 사용자: *"비늘 안쪽 색이 다리에도 있어야 하지 않나"* — 악어 · 도마뱀은 배에서 이어져 팔다리 안쪽 · 아래가 밝다).
    허벅지 안쪽(두 다리가 마주 보는 쪽) · 정강이 앞 · 팔 안쪽(몸통을 보는 쪽 — 몸통 살 위는 안 칠한다). 공룡 악어 몸만(`LIMB_IN`)."""
    x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
    (h, k, a, b), r = brute_leg_pts()
    leg = sstep(h[0] - r * 0.15, h[0] - r * 0.80, x) * sstep(h[1] + 0.06, h[1] - 0.06, y) * sstep(b[1] - 0.02, b[1] + 0.06, y)
    t = np.clip((k[1] - y) / (k[1] - a[1]), 0, 1); axz = k[2] + (a[2] - k[2]) * t                     # 정강이 축의 앞뒤 자리
    shin = sstep(axz + r * 0.15, axz + r * 0.70, z) * sstep(k[1] + 0.02, k[1] - 0.06, y) * sstep(a[1] - 0.02, a[1] + 0.06, y)
    sh, el, wr, ra = brute_arm_pts()
    u = np.clip((sh[1] - y) / (sh[1] - wr[1]), 0, 1); axx = sh[0] + (wr[0] - sh[0]) * u
    arm = sstep(axx - ra * 0.10, axx - ra * 0.75, x) * sstep(0.01, 0.05, torso(p)) * sstep(sh[1] - 0.05, sh[1] - 0.20, y) * sstep(wr[1] - 0.12, wr[1] - 0.02, y)
    # 팔다리 둘레로만 — 높이 · 폭으로만 고르면 앞뒤로 끝없이 뻗어 땅 가까이 내려온 꼬리까지 칠했다(v18 첫 판 — 꼬리 · 곤봉이 통째로 배 색)
    m = mir(p)
    near_leg = sstep(r * 1.9, r * 1.3, np.minimum(np.minimum(seg(m, h, k, 0, 0), seg(m, k, a, 0, 0)), seg(m, a, b, 0, 0)))
    near_arm = sstep(ra * 2.0, ra * 1.4, np.minimum(seg(m, sh, el, 0, 0), seg(m, el, wr, 0, 0)))
    return np.maximum(leg * near_leg, arm * near_arm)   # v19 — 정강이 앞은 뺐다(사용자: *"정강이 쪽은 안쪽 색이 잘못 칠해져"* — 안쪽만)

# ── 머리 — 저마다 (머리통 · 위턱 · 아래턱 · 입선 높이 · 눈 자리 · 덧붙이 · 콧구멍)를 낸다 ─────────────────────
G = 0.007   # 입선의 반 틈

class Head:
    def __init__(s, name, skin, belly, eye):
        s.name, s.skin, s.belly, s.eye_default = name, skin, belly, eye
        s.teeth = s.tongue = None

# 도마뱀의 가시 테 — 볏도마뱀붙이처럼(11-14 사용자 사진: *"뿔이 눈을 따라 쭉 내려가잖아 — 지금은 머리 위에 있는 뿔 같다"*).
#   ① 속눈썹 — 눈알 윗선을 따라 앞에서 뒤로 감싼다(가시는 눈 중심에서 바깥으로 · 둑 없음)
#   ② 머리 옆 가장자리 — 눈 뒤에서 뒤통수 옆까지(살은 테두리 둑)
#   ③ 뒤통수 옆에서 아래로 꺾여 목으로 내려간다 — 그대로 등 양쪽 두 줄로 이어진다(`fringe_rows` — 몸)
EYE_C = V(0.172, 0.085, 0.135)                                                                         # 머리의 눈 자리(`lizard_head`)
LASH = [V(0.118, 0.142, 0.212), V(0.168, 0.166, 0.160), V(0.206, 0.160, 0.100), V(0.226, 0.140, 0.052)]   # ① 눈알 윗선
FRINGE = [V(0.226, 0.150, 0.052), V(0.238, 0.178, -0.040), V(0.232, 0.180, -0.140),                      # ② 가장자리
          V(0.212, 0.128, -0.200), V(0.188, 0.050, -0.236), V(0.160, -0.040, -0.262)]                     # ③ 뒤통수 옆에서 아래로
def fringe_bank(p):
    m = mirror(p); d = far(p)
    for a, b in zip(FRINGE[:-1], FRINGE[1:]): d = np.minimum(d, capsule(m, a, b, 0.030))
    return d
def fringe_spines(p):
    m = mirror(p); d = far(p)
    for (a, b), n in zip(zip(LASH[:-1], LASH[1:]), (3, 3, 2)):          # ① 속눈썹 — 짧고 가늘다
        for i in range(n):
            # 바깥(머리 옆)으로 젖혀진다(11-14 사용자: *"속눈썹은 안쪽 말고 바깥쪽으로"*) — 눈에서 나가는 쪽 + 옆 + 조금 위
            c = a + (b - a) * ((i + 0.5) / n); out = nrm(nrm(c - EYE_C) * 0.55 + V(1, 0, 0) * 0.80 + UP * 0.15)
            d = np.minimum(d, seg(m, c - out * 0.004, c + out * 0.030, 0.010, 0.002))
    for a, b in zip(FRINGE[:-1], FRINGE[1:]):                                 # ② · ③ 가장자리 — 바깥 · 뒤로(몸과 같은 간격 `SPINE_GAP`)
        n = max(1, int(round(float(np.linalg.norm(b - a)) * HK / SPINE_GAP)))
        for i in range(n):
            c = a + (b - a) * ((i + 0.5) / n)
            down = sstep(0.17, 0.05, float(c[1]))                               # 내려가는 자리에선 뒤 · 바깥으로 눕는다
            out = nrm(nrm([c[0], 0, (c[2] + 0.05) * 0.5]) * 0.92 + UP * (0.34 - 0.40 * down) + V(0, 0, -0.30 * down))
            ln = 0.034 + 0.014 * np.sin(np.pi * (i + 0.5) / n)
            d = np.minimum(d, seg(m, c + out * 0.012, c + out * (0.026 + ln), 0.015, 0.003))
    return d
def seat_eye(h, depth=0.42, exact=False):
    """눈알을 살 겉면에 앉힌다 — 눈 방향으로 밀어 눈알 중심이 살 속 `depth × 반지름`에 오게(손으로 맞춘 자리가 살에 묻히거나 떴다)."""
    def skin0(p): return smin(smin(h.cran(p), h.extra(p), 0.05), np.minimum(h.upper(p), h.lower(p)), 0.05)
    if exact:   # 눌린 조각(주둥이)은 거리가 작게 나와 눈이 더 깊이 묻힌다 — 눈 방향 선 위에서 겉면을 직접 찾는다
        a = h.eye - h.eye_n * 0.12; pt = surf(skin0, a, h.eye_n)
        if pt is not None: h.eye = (pt - h.eye_n * (depth * h.eye_r)).astype(np.float32); return
    c = h.eye.copy()
    for _ in range(8): c = c - h.eye_n * (float(skin0(c[None, :])[0]) + depth * h.eye_r)   # 살 속이면(음수) 밖으로 민다
    h.eye = c.astype(np.float32)
def comb(c, r):   # 이구아나 볏 — 정수리에서 목 뒤로, 옆으로 납작한 가시
    def f(p):
        q = thin(p, 0.42); d = far(p)
        for i, th in enumerate(np.linspace(8, 128, 9)):
            a = np.radians(th); dr = np.array([0, np.cos(a), -np.sin(a)], np.float32)
            base = np.asarray(c, np.float32) + dr * np.array([0, r[1], r[2]], np.float32)
            ln = 0.050 + 0.065 * np.sin(np.pi * i / 8)
            d = np.minimum(d, seg(q, base - dr * 0.03, base + dr * ln + V(0, 0, -0.035), 0.050, 0.006))
        return d * 0.42
    return f

def lizard_head(crest='fringe'):
    h = Head('lizard', (0.36, 0.50, 0.27), (0.80, 0.76, 0.52), 'bigslit')
    C, R = (0, 0.04, -0.05), (0.245, 0.255, 0.28)
    flat = crest in ('fringe', 'bare')   # 가시 테 머리는 윗면이 납작한 쐐기다 · `bare` — 같은 머리통에 가시만 없다(11-14 사용자: *"저 두개골 그대로 뿔 없는 모습으로도"*)
    h.cran = lambda p: smax(ell(p, V(*C), V(*R)), p[..., 1] - 0.238, 0.05) if flat else ell(p, V(*C), V(*R))
    h.upper = lambda p: seg(squash(p, -0.01, 0.72), V(0, 0.0, 0.08), V(0, -0.035, 0.42), 0.190, 0.105) * 0.72
    h.lower = lambda p: seg(squash(p, -0.10, 0.60), V(0, -0.10, 0.02), V(0, -0.105, 0.39), 0.165, 0.088) * 0.60
    h.mouth_y = lambda p: -0.082 - 0.03 * p[..., 2] + 0.9 * np.clip(0.10 - p[..., 2], 0, 1) ** 2   # 뒤쪽이 올라간다
    h.eye, h.eye_r, h.eye_n = V(0.172, 0.085, 0.135), 0.058, nrm([0.80, 0.12, 0.58])
    def extra(p):
        m = mirror(p)
        cheek = sph(m, V(0.150, -0.065, 0.0), 0.098)
        if flat: return smin(fringe_bank(p), cheek, 0.03)
        return smin(seg(m, V(0.075, 0.120, 0.220), V(0.228, 0.172, 0.01), 0.038, 0.040), cheek, 0.03)
    h.extra = extra
    h.spikes = {'fringe': fringe_spines, 'comb': comb(C, R), 'none': far, 'bare': far}[crest]
    h.spike_col = (0.74, 0.76, 0.50) if flat else (0.24, 0.36, 0.20)
    h.nostril, h.nostril_r = V(0.045, 0.028, 0.452), 0.016
    h.jaw_w = 0.19; h.reach = 0.66
    return h

def top_y(skin0, x, z, lo=-0.06, hi=0.45):
    """(x, z)에서 살 윗면의 높이 — 위에서 아래로 좁혀 찾는다. 비늘을 살 위에 앉힐 때 쓴다."""
    for _ in range(22):
        mid = (lo + hi) * 0.5
        if float(skin0(np.array([[x, mid, z]], np.float32))[0]) > 0: hi = mid
        else: lo = mid
    return (lo + hi) * 0.5

def head_leaves(h, skin0, z0, z1, ln0, ln1, ratio, lift0, lift1, brow_lift=None):
    """머리 윗면 · 옆면에 뒤로 누운 잎 비늘을 줄 맞춰 깐다 — z0(앞)에서 z1(뒤)로 가며 커지고 더 들린다. 눈 둘레 · 입술은 비운다."""
    lv = Leaves(); z = z0; r = 0
    while z > z1:
        t = np.clip((z0 - z) / (z0 - z1), 0, 1); ln = ln0 + (ln1 - ln0) * t; wd = ln * ratio
        top = surf(skin0, V(0, -0.03, z), UP)
        if top is None: break
        rad = max(float(top[1]) + 0.03, 0.06)
        for c in range(12):
            phi = (c + (0.5 if r % 2 else 0.0)) * wd * 0.92 / rad
            if phi > 1.85: break
            pt = surf(skin0, V(0, -0.03, z), [np.sin(phi), np.cos(phi), 0])
            if pt is None or pt[1] < h.mouth_y(pt[None, :])[0] + 0.045: continue
            de = float(np.linalg.norm(pt - h.eye))
            if de < h.eye_r * 1.45: continue
            brow = brow_lift is not None and de < h.eye_r * 2.3 and pt[1] > h.eye[1]   # 눈 위 줄 — 눈썹
            lv.add(pt, [0, 0, -1], grad(skin0, pt), ln * (1.10 if brow else 1.0), wd, brow_lift if brow else lift0 + (lift1 - lift0) * t ** 2)
        z -= ln * 0.46; r += 1
    return lv

# 매끈한 뱀 — 머리는 하나, 색이 넷(사용자 그림: 보엘렌비단뱀 · 누런 구렁이 · 인디고 · 붉은 뱀). 눈은 둥근 눈동자에 테(`ring`).
#            살                  배                  눈 테               무늬 색
SNAKE = {'boelen':  ((0.07, 0.08, 0.11), (0.92, 0.82, 0.42), (0.80, 0.66, 0.26), (0.95, 0.80, 0.16)),
         'kingrat': ((0.90, 0.74, 0.18), (0.96, 0.90, 0.58), (0.56, 0.22, 0.10), (0.06, 0.06, 0.07)),
         'indigo':  ((0.08, 0.09, 0.14), (0.24, 0.27, 0.35), (0.84, 0.46, 0.20), (0.74, 0.30, 0.13)),
         'red':     ((0.80, 0.25, 0.16), (0.95, 0.66, 0.38), (0.10, 0.08, 0.08), (0.07, 0.05, 0.06))}
def line2(a, b, a0, b0, a1, b1):
    """(a, b) 평면에서 선분 (a0, b0)–(a1, b1)까지의 거리 — 칠하는 줄에 쓴다."""
    da, db = a1 - a0, b1 - b0
    t = np.clip(((a - a0) * da + (b - b0) * db) / (da * da + db * db), 0, 1)
    return np.sqrt((a - a0 - t * da) ** 2 + (b - b0 - t * db) ** 2)
def along(p):
    """몸을 따라 내려가는 자 — 몸통에선 높이, 꼬리에선 뒤로 간 거리를 이어 붙인다(띠 무늬가 꼬리에서 꺾이지 않게)."""
    w = sstep(-0.18, -0.36, p[..., 2]) * sstep(D['lift'] + 0.28, D['lift'] + 0.10, p[..., 1])
    return p[..., 1] * (1 - w) + (D['lift'] + 0.14 + p[..., 2]) * w, w

def python_head(crest=None, tone=None):
    """매끈한 뱀 머리 — 길고 낮다 · 윗면이 평평하고 눈 위에서 주둥이로 모서리(눈썹 판)가 선다 · 둥글게 좁아지는 주둥이 · 큰 둥근 눈.
    비늘은 빚지 않는다(매끈한 큰 판) — 종류는 색과 줄이 가른다: 눈 밑으로 내려오는 검은 줄 · 입술 세로 줄 · 정수리 판의 금."""
    tone = tone or 'boelen'; skin, belly, ring, mark = SNAKE[tone]
    h = Head('python', skin, belly, 'ring'); h.eye_col = ring; h.tone = tone
    S = np.asarray(skin, np.float32)
    h.cran = lambda p: smax(ell(p, V(0, 0.0, -0.07), V(0.200, 0.146, 0.27)), p[..., 1] - 0.108, 0.045)
    h.upper = lambda p: smax(seg(squash(p, -0.01, 0.62), V(0, -0.005, 0.03), V(0, -0.022, 0.400), 0.184, 0.120) * 0.62, p[..., 2] - 0.505, 0.05)
    h.lower = lambda p: smax(seg(squash(p, -0.085, 0.42), V(0, -0.085, -0.02), V(0, -0.082, 0.385), 0.176, 0.108) * 0.42, p[..., 2] - 0.488, 0.04)
    h.mouth_y = lambda p: -0.070 + 0.5 * np.clip(0.05 - p[..., 2], 0, 1) ** 2
    h.eye, h.eye_r, h.eye_n = V(0.170, 0.038, 0.205), 0.058, nrm([0.90, 0.12, 0.40])
    def extra(p):
        m = mirror(p)
        jaw = sph(m, V(0.140, -0.030, -0.06), 0.100)                                  # 턱 뒤 — 목보다 조금 넓다
        brow = seg(m, V(0.126, 0.070, 0.110), V(0.062, 0.026, 0.430), 0.032, 0.022)   # 눈 위에서 주둥이로 서는 모서리
        return smin(jaw, brow, 0.03)
    h.extra = extra
    seat_eye(h, 0.30, exact=True)
    h.spikes, h.spike_col = far, h.skin
    if tone == 'kingrat':   # 뒤통수부터 온몸에 밑동이 검은 노란 비늘 — 납작하게 누워 윤곽은 매끈하고, 겹친 자리가 검은 그물로 읽힌다
        skin0 = lambda p: smin(smin(h.cran(p), h.extra(p), 0.05), h.upper(p), 0.05)
        lv = head_leaves(h, skin0, 0.075, -0.300, 0.070, 0.096, 0.95, 0.05, 0.08)
        h.scaly = dict(lift=0.28, dark=mark, tip=skin, ramp=(0.12, 0.42))
        h.spikes, h.spike_col = lv.dist, lv.tint(mark, skin, 0.12, 0.42)
    h.pits = [(V(0.066, 0.022, 0.492), 0.012)]
    h.jaw_w = 0.205; h.reach = 0.76
    ez, ey, er = float(h.eye[2]), float(h.eye[1]), h.eye_r
    def mix(col, k, c): return col * (1 - k[..., None]) + np.asarray(c, np.float32) * k[..., None]
    def tears(p, w=0.009):   # 눈에서 입술로 내려오는 줄 둘 + 눈 뒤로 비껴 내려가는 줄 하나
        z, y = p[..., 2], p[..., 1]; my = h.mouth_y(p)
        d = np.minimum(line2(z, y, ez + 0.030, ey - er * 0.8, ez + 0.062, -0.068), line2(z, y, ez - 0.030, ey - er * 0.8, ez - 0.075, -0.066))
        d = np.minimum(d, line2(z, y, ez - er * 0.9, ey - 0.012, ez - 0.215, -0.050))
        return sstep(w, w * 0.6, d) * sstep(0.07, 0.10, np.abs(p[..., 0])) * (y > my - 0.004)
    def lipbars(p, lo, hi, k=62.0, th=0.45):   # 입술 띠(입선에서 lo~hi)의 세로 줄
        yv = p[..., 1] - h.mouth_y(p)
        band = sstep(lo - 0.012, lo, yv) * sstep(hi + 0.012, hi, yv) * sstep(-0.02, 0.04, p[..., 2]) * sstep(0.08, 0.12, np.abs(p[..., 0])) * sstep(0.45, 0.41, p[..., 2])
        return sstep(th - 0.15, th + 0.15, np.sin(p[..., 2] * k + 0.6)) * band, band
    def paint(p, col):
        x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]; yv = y - h.mouth_y(p)
        if tone == 'boelen':      # 윗입술 — 검은 바탕에 노란 줄 · 아랫입술 — 노란 바탕에 검은 줄
            up, _ = lipbars(p, 0.0, 0.052); col = mix(col, up, mark)
            lo, band = lipbars(p, -0.060, 0.0, th=-0.35); col = mix(col, band, mark); col = mix(col, band * (1 - lo), S)
        elif tone == 'kingrat':   # 눈 밑 줄 · 입술 줄(비늘의 검은 테는 빚은 비늘이 낸다)
            col = mix(col, tears(p), mark)
            lb, _ = lipbars(p, -0.045, 0.045, k=58.0, th=0.88); col = mix(col, lb, mark)
        elif tone == 'indigo':    # 턱 · 입술이 녹슨 붉은색 · 그 위에 검은 줄
            chin = sstep(0.050, 0.020, yv) * sstep(-0.10, 0.02, z)
            col = mix(col, chin, mark)
            lb, _ = lipbars(p, -0.060, 0.045, k=58.0, th=0.55); col = mix(col, lb * chin, S * 0.8)
        else:                     # red — 눈 밑 · 눈 뒤 검은 줄
            col = mix(col, tears(p, 0.013), mark)
            lb, _ = lipbars(p, -0.040, 0.040, k=58.0, th=0.86); col = mix(col, lb * sstep(0.30, 0.20, z), mark)
        return col
    h.paint = paint
    def body_paint(p, col):
        s_, tailw = along(p); x = np.abs(p[..., 0])
        lum = col.sum(-1); back = (lum < 0.6) if tone in ('boelen', 'indigo') else (np.abs(col - S).sum(-1) < 0.25)   # 배 색은 안 건드린다
        if tone in ('boelen', 'indigo'):   # 검푸른 살의 옅은 무지갯빛
            w = 0.035 * np.sin(p[..., 1] * 11.0 + p[..., 2] * 8.0) * back
            col = np.clip(col + np.stack([-0.5 * w, 0.8 * w, 1.4 * w], -1), 0, 1)
        if tone == 'boelen':               # 옆구리 · 꼬리의 노란 빗금
            side = sstep(0.07, 0.15, x)
            flank = sstep(0.86, 0.95, np.sin(p[..., 1] * 24.0 + x * 16.0 - p[..., 2] * 9.0)) * side * (1 - tailw)
            tailb = sstep(0.86, 0.95, np.sin(p[..., 2] * 26.0 + p[..., 1] * 12.0)) * tailw
            col = mix(col, np.maximum(flank, tailb) * back, mark)
        elif tone == 'red':                # 붉은 머리에서 꼬리로 갈수록 검붉은 잿빛
            k = sstep(TOP + 0.05, D['lift'] - 0.35, s_) * back
            col = mix(col, k * 0.82, (0.23, 0.15, 0.18))
        return col
    h.body_paint = body_paint
    return h

# 숲살모사 — 색 넷(사용자 그림: 풀빛 · 청회색 · 숯빛 · 주황). 살은 비늘 사이로 어둡게 비치고 잎 끝이 밝다.
#            살(잎 밑동)          잎 끝               배 · 턱             눈                  눈 종류
VIPER = {'green': ((0.30, 0.50, 0.17), (0.80, 0.86, 0.40), (0.86, 0.88, 0.52), (0.46, 0.74, 0.66), 'bigslit'),
         'slate': ((0.24, 0.36, 0.45), (0.66, 0.80, 0.80), (0.78, 0.84, 0.62), (0.84, 0.88, 0.62), 'bigslit'),
         'coal':  ((0.13, 0.15, 0.19), (0.40, 0.45, 0.52), (0.36, 0.39, 0.44), BLACK,              'gecko'),
         'amber': ((0.80, 0.44, 0.10), (0.99, 0.82, 0.30), (0.97, 0.86, 0.46), (0.74, 0.86, 0.40), 'bigslit')}
def viper_head(crest=None, tone=None):
    """숲살모사 — 다른 뱀과 윤곽이 다르다(사용자 11-12). 위에서 보면 **넓은 세모**: 좁고 뭉툭한 주둥이에서 턱 모서리까지 곧게 벌어졌다가
    목에서 확 잘록해진다. 옆에서 보면 **윗면이 평평하고 주둥이가 깊은 채로 뚝 잘린다**(끝으로 가늘어지지 않는다) · 윗면과 옆면이 모서리(눈썹 줄)로
    만나고 큰 눈이 그 바로 밑에 있다 · 입선은 눈 뒤 턱 모서리까지 가며 끝이 올라간다. 비늘은 뒤로 누워 겹친 잎."""
    tone = tone or 'green'; skin, tip, belly, eyec, eyek = VIPER[tone]
    h = Head('viper', skin, belly, eyek); h.eye_col = eyec; h.tone = tone; h.tip = tip
    ZW = np.array([-0.33, -0.24, -0.10, 0.14, 0.325], np.float32); WW = np.array([0.105, 0.160, 0.262, 0.152, 0.092], np.float32)   # 볼 높이의 반폭 — 턱 모서리(z −0.10)가 가장 넓다
    ZT = np.array([-0.33, -0.18, 0.15, 0.325], np.float32); TT = np.array([0.070, 0.136, 0.138, 0.084], np.float32)                    # 윗면 높이 — 눈 앞에서 주둥이로 조금 내려간다
    def wedge(p, zf, k_w=1.0, top=True):
        x, y, z = np.abs(p[..., 0]), p[..., 1], p[..., 2]
        w = np.interp(z, ZW, WW) * k_w - 0.050 * sstep(-0.02, 0.12, y) * sstep(0.30, -0.05, z)   # 윗면은 볼보다 좁다(뒤로 갈수록 더)
        d = smax(x - w, z - zf - 0.10 * np.minimum(y, 0.0), 0.075)                                                           # 뭉툭하게 잘린 주둥이
        if top: d = smax(d, y - (np.interp(z, ZT, TT) - 0.6 * x * x), 0.045)                                     # 평평한 윗면 · 눈썹 모서리
        return smax(d, -0.31 - z, 0.09) * 0.74
    h.upper = lambda p: smax(wedge(p, 0.330), -0.085 - p[..., 1], 0.02)   # 아래는 입선이 자른다 — 그 밑으로 조금만 남긴다(입 안 조각이 기둥이 되지 않게)
    h.lower = lambda p: smax(smax(wedge(p, 0.322, 0.97, top=False), (-0.150 + 0.085 * np.clip(p[..., 2], -0.1, 0.33) + 1.3 * p[..., 0] ** 2) - p[..., 1], 0.05), p[..., 1] - 0.0, 0.03)
    h.mouth_y = lambda p: -0.058 + 0.75 * np.clip(0.06 - p[..., 2], 0, 1) ** 2
    h.cran = lambda p: ell(p, V(0, -0.010, -0.200), V(0.160, 0.135, 0.150))                  # 뒤통수 밑 — 목이 여기 꽂힌다
    h.extra = lambda p: sph(mirror(p), V(0.170, -0.030, -0.135), 0.088)                        # 턱 모서리(독샘) — 입선이 여기서 끝난다
    h.eye, h.eye_r, h.eye_n = V(0.150, 0.060, 0.150), 0.060, nrm([0.92, 0.10, 0.38])
    h.chin_z = (-0.16, -0.08)                                                                  # 아래턱의 밝은 색이 턱 모서리까지
    seat_eye(h, 0.36, exact=True)
    skin0 = lambda p: smin(smin(h.cran(p), h.extra(p), 0.05), h.upper(p), 0.05)
    lv = head_leaves(h, skin0, 0.275, -0.330, 0.050, 0.104, 0.92, 0.10, 0.26, brow_lift=0.24)
    h.leaves = lv
    h.scaly = dict(lift=1.0, dark=np.asarray(skin, np.float32) * 0.92, tip=tip, ramp=(0.25, 0.95))
    h.spikes, h.spike_col = lv.dist, lv.tint(h.scaly['dark'], tip)
    h.pits = [(V(0.078, 0.012, 0.318), 0.011)]
    h.jaw_w = 0.27; h.reach = 0.72
    if tone == 'amber':   # 주황 — 등을 따라 녹슨 얼룩 띠
        def body_paint(p, col):
            s_, _ = along(p); k = sstep(0.55, 0.85, np.sin(s_ * 30.0 + 0.4 * np.sin(np.abs(p[..., 0]) * 30.0))) * (np.abs(col - np.asarray(skin, np.float32)).sum(-1) < 0.2)
            return col * (1 - 0.45 * k[..., None]) + np.asarray((0.62, 0.24, 0.08), np.float32) * 0.45 * k[..., None]
        h.body_paint = body_paint
    return h

def croc_head(crest=None):
    """덩치 큰 악어 — 넓은 주둥이(가늘면 약해 보인다) · 깊은 아래턱 · 눈 위에서 주둥이로 내려오는 굵은 턱 · 눈은 작고 깊다."""
    h = Head('croc', (0.23, 0.32, 0.25), (0.74, 0.72, 0.54), 'glare')
    h.eye_col = YELLOW
    h.cran = lambda p: smax(ell(p, V(0, 0.0, -0.07), V(0.265, 0.225, 0.27)), p[..., 1] - 0.190, 0.05)   # 윗면이 납작하다
    SW = lambda z: 0.215 + (0.150 - 0.215) * np.clip((z - 0.05) / 0.50, 0, 1)   # 위턱 반폭
    def upper(p):
        d = seg(squash(p, -0.03, 0.60), V(0, -0.01, 0.05), V(0, -0.03, 0.55), 0.215, 0.150) * 0.60
        mound = ell(p, V(0, 0.022, 0.590), V(0.100, 0.052, 0.085))   # 콧등 둔덕 — 콧구멍이 이 위에 뚫린다
        return smin(d, mound, 0.035)
    h.upper = upper
    h.lower = lambda p: seg(squash(p, -0.135, 0.64), V(0, -0.135, 0.0), V(0, -0.13, 0.545), 0.205, 0.140) * 0.64
    h.mouth_y = lambda p: -0.095 + 0.011 * np.sin(p[..., 2] * 24.0) + 0.7 * np.clip(0.10 - p[..., 2], 0, 1) ** 2   # 물결치는 입선
    h.eye, h.eye_r, h.eye_n = V(0.175, 0.125, 0.105), 0.046, nrm([0.78, 0.18, 0.60])
    def base(p):
        m = mirror(p)
        mound = ell(m, V(0.130, 0.118, 0.050), V(0.092, 0.072, 0.108))                  # 눈 둔덕 — 머리 윗면보다 솟지 않는다
        snout = seg(m, V(0.058, 0.050, 0.500), V(0.070, 0.095, 0.290), 0.020, 0.030)    # 주둥이 위 두 줄 능선
        hinge = sph(m, V(0.190, -0.090, -0.04), 0.112)                                  # 턱 근육
        return smin(mound, smin(snout, hinge, 0.02), 0.03)
    h.extra = base
    seat_eye(h)
    t = nrm(np.cross(UP, h.eye_n))
    b0 = h.eye + UP * (h.eye_r * 0.95) - t * 0.060 + h.eye_n * 0.012 - UP * 0.022   # 앞(안쪽)이 낮다 — 노려본다. 살에 붙여 짧게(길면 끝이 귀처럼 솟는다)
    b1 = h.eye + UP * (h.eye_r * 0.95) + t * 0.050 - h.eye_n * 0.015 + UP * 0.010
    h.extra = lambda p: smin(base(p), seg(mirror(p), b0, b1, 0.026, 0.032), 0.03)
    def spikes(p):   # 뒤통수 골판 — 안쪽 줄은 크게, 바깥 줄은 작게
        m = mirror(p); d = far(p)
        for z in (-0.07, -0.17, -0.27):
            y = min(0.190, 0.225 * np.sqrt(max(0.0, 1 - ((z + 0.07) / 0.27) ** 2)))
            d = np.minimum(d, seg(m, V(0.080, y - 0.02, z), V(0.095, y + 0.075, z - 0.055), 0.050, 0.012))
            d = np.minimum(d, seg(m, V(0.180, y - 0.05, z - 0.02), V(0.215, y + 0.025, z - 0.065), 0.040, 0.010))
        return d
    h.spikes, h.spike_col = spikes, (0.14, 0.21, 0.16)
    h.nostril, h.nostril_r = V(0.036, 0.066, 0.605), 0.017
    def teeth(p):   # 굵기와 길이가 들쭉날쭉한 송곳니 — 위는 아래로, 아래 큰 것 둘은 윗입술 밖으로 솟는다
        m = mirror(p); d = far(p)
        for z, ln, r in ((0.16, 0.070, 0.022), (0.25, 0.118, 0.031), (0.34, 0.072, 0.022), (0.43, 0.108, 0.029), (0.52, 0.078, 0.023)):
            w = SW(z) * 0.975
            d = np.minimum(d, seg(m, V(w, -0.082, z), V(w + 0.006, -0.082 - ln, z + 0.004), r, 0.003))
        for z, ln, r in ((0.295, 0.105, 0.027), (0.475, 0.090, 0.025)):
            w = SW(z) + 0.004
            d = np.minimum(d, seg(m, V(w, -0.118, z), V(w + 0.010, -0.118 + ln, z), r, 0.003))
        for x in (0.045, 0.115):
            z = 0.690 - x * x * 3.2
            d = np.minimum(d, seg(m, V(x, -0.082, z), V(x, -0.160, z + 0.006), 0.022, 0.003))
        return d
    h.teeth = teeth
    h.jaw_w = 0.225; h.reach = 0.90
    return h

def brute_head(crest=None):
    """공룡 악어 — **짧고 깊은 턱**(v5는 주둥이가 길고 납작했다 — 사용자: *"주둥이가 너무 길고 · 공룡은 저 정도로 납작하지 않다 · 좀 더 두꺼워도"*):
    폭보다 높이가 큰 머리 · 위아래로 두꺼운 주둥이 · 깊은 아래턱 · 눈 위 뼈 능선 밑의 눈 · 능선 끝에서 뒤로 누운 뿔 · 드러난 이빨 ·
    정수리에서 뒤로 누운 골판. 턱 옆은 **납작한 면**이다(둥근 턱 근육 공은 *"두꺼비 같다"* — 걷었다)."""
    L = BRUTE_LOOK
    h = Head('brute', BRUTE_PAL[L['pal']]['skin'], BRUTE_PAL[L['pal']]['belly'], 'glare')
    h.eye_col = BRUTE_PAL[L['pal']]['eye']
    h.cran = lambda p: smax(ell(p, V(0, 0.03, -0.08), V(0.238, 0.225, 0.25)), p[..., 1] - 0.200, 0.05)   # 좌우로 넓다(v8 — 사용자: *"얼굴도 살짝 더 늘어나면 — 좌우로"*)
    SW = lambda z: 0.196 + (0.146 - 0.196) * np.clip((z - 0.04) / 0.34, 0, 1)   # 위턱 반폭
    h.upper = lambda p: seg(squash(p, 0.01, 0.90), V(0, 0.01, 0.04), V(0, -0.005, 0.38), 0.196, 0.146) * 0.90   # 넓고 두껍다
    h.lower = lambda p: seg(squash(p, -0.135, 0.74), V(0, -0.135, -0.01), V(0, -0.130, 0.372), 0.186, 0.128) * 0.74
    h.mouth_y = lambda p: -0.070 + 0.008 * np.sin(p[..., 2] * 22.0) + 0.6 * np.clip(0.08 - p[..., 2], 0, 1) ** 2
    h.eye, h.eye_r, h.eye_n = V(0.178, 0.122, 0.095), 0.040, nrm([0.86, 0.12, 0.50])
    def base(p):
        m = mirror(p)
        ramus = ell(m, V(0.152, -0.085, -0.055), V(0.060, 0.170, 0.115))               # 턱뼈 뒤 — 옆으로 납작한 면(공이 아니다)
        snout = seg(m, V(0.060, 0.110, 0.400), V(0.106, 0.150, 0.180), 0.020, 0.032)   # 콧등에서 눈으로 오르는 능선
        return smin(ramus, snout, 0.03)
    h.extra = base
    seat_eye(h)
    t = nrm(np.cross(UP, h.eye_n))
    b0 = h.eye + UP * (h.eye_r * 1.05) - t * 0.072 + h.eye_n * 0.012 - UP * 0.020   # 앞(안쪽)이 낮다 — 노려본다
    b1 = h.eye + UP * (h.eye_r * 1.05) + t * 0.064 - h.eye_n * 0.018 + UP * 0.012
    h.extra = lambda p: smin(base(p), seg(mirror(p), b0, b1, 0.027, 0.036), 0.03)
    def spikes(p):
        m = mirror(p); d = far(p)
        bh = BRUTE_BROW[L['brow']]                                                                               # 능선 끝의 뿔
        if bh:
            dr = nrm(bh[0]); a0 = b1 - UP * 0.010
            if bh[1] > 0.16:   # 큰 뿔 — 밑동에서 뒤 · 위로 솟다 뒤 · 아래로 휜다(두 마디)
                a0 = a0 + V(0.0, 0.012, -0.055)   # 밑동을 원래 뿔 자리(능선 끝 뒤)로 — 굵은 밑동이 눈 위로 덮여 앞으로 나와 보였다(11-15 사용자)
                k1 = a0 + dr * bh[1] * 0.55; dr2 = nrm(dr + V(0.10, -0.55, -0.45))
                horn = smin(seg(m, a0, k1, bh[2], bh[2] * 0.66), seg(m, k1, k1 + dr2 * bh[1] * 0.50, bh[2] * 0.66, 0.004), 0.015)
            else: horn = seg(m, a0, a0 + dr * bh[1], bh[2], 0.004)
            if L['brow'] == 'broken':   # 오른쪽(+x)은 밑동만 남고 비스듬히 부러졌다 — 굵은 동강 + 기운 단면 + 단면 가장자리의 깨진 조각
                stub = seg(m, a0, a0 + dr * bh[1] * 0.40, bh[2], bh[2] * 0.82)
                cut = ((m - (a0 + dr * bh[1] * 0.24)) * nrm(dr + V(0.0, 0.5, 0.3))).sum(-1)
                stub = smax(stub, cut, 0.004)
                stub = np.minimum(stub, seg(m, a0 + dr * bh[1] * 0.18 + UP * 0.022, a0 + dr * bh[1] * 0.30 + UP * 0.034, 0.016, 0.003))
                horn = np.where(p[..., 0] > 0, stub, horn)
            d = np.minimum(d, horn)
        cr = L['crown']                                                                                          # 정수리
        if cr == 'plates':                                                                                       # 골판 — 뒤로 누워 뒤로 갈수록 크다
            q = thin(p, 0.36)
            for i, (z, y) in enumerate(((0.02, 0.190), (-0.10, 0.196), (-0.21, 0.170))):
                ln = 0.055 + 0.028 * i
                d = np.minimum(d, seg(q, V(0, y - 0.03, z), V(0, y + ln * 0.80, z - 0.050 - 0.020 * i), 0.046, 0.008) * 0.36)
        elif cr == 'spikes':                                                                                     # 가시 두 줄 — 정수리 양옆에서 뒤로
            for i, (z, y) in enumerate(((0.00, 0.182), (-0.09, 0.188), (-0.18, 0.168), (-0.26, 0.135))):
                ln = 0.050 + 0.020 * i
                d = np.minimum(d, seg(m, V(0.080, y - 0.03, z), V(0.095, y + ln * 0.75, z - 0.060), 0.026, 0.004))
        elif cr == 'frill':                                                                                      # 뒤통수 테 — 뒤 · 옆으로 뻗은 뿔 다섯(목 위를 두른다)
            for a in (0.0, 0.55, 1.10):
                o = V(np.sin(a), np.cos(a) * 0.8, -0.55); base = V(0.20 * np.sin(a), 0.14 * np.cos(a) + 0.02, -0.24)
                d = np.minimum(d, seg(m, base, base + nrm(o) * (0.13 - 0.02 * a), 0.034, 0.004))
        for c, tip, r0, *cut in BRUTE_NOSE[L['nose']]:                                                           # 코뿔 — 콧등 위에서 솟는다(밑동 → 꺾임 → 끝)
            a0, a1, a2 = V(*c[0]), V(*c[1]), V(*tip)
            hn = smin(seg(p, a0, a1, r0, r0 * 0.62), seg(p, a1, a2, r0 * 0.62, 0.004), 0.01)
            if cut:   # 부러졌다 — 꺾임 위에서 비스듬히 잘린 단면 + 단면에 남은 깨진 조각
                at = a1 + (a2 - a1) * (cut[0] - 0.5) if cut[0] > 0.5 else a0 + (a1 - a0) * cut[0] * 2
                hn = smax(hn, ((p - at) * nrm(V(0.35, 1.0, 0.45))).sum(-1), 0.004)
                hn = np.minimum(hn, seg(p, at + V(-0.012, -0.004, -0.006), at + V(-0.010, 0.018, -0.010), 0.009, 0.003))
            d = np.minimum(d, hn)
        return d
    h.spikes, h.spike_col = spikes, BRUTE_PAL[L['pal']]['horn']
    h.nostril, h.nostril_r = V(0.060, 0.086, 0.462), 0.015
    def teeth(p):   # 위는 아래로 · 아래 큰 것들은 윗입술 밖으로 솟는다(입을 다물어도 보인다)
        m = mirror(p); d = far(p)
        for z, ln, r in ((0.08, 0.050, 0.018), (0.15, 0.086, 0.025), (0.22, 0.058, 0.019), (0.29, 0.088, 0.025), (0.36, 0.056, 0.018)):
            w = SW(z) * 0.975
            d = np.minimum(d, seg(m, V(w, -0.058, z), V(w + 0.005, -0.058 - ln, z + 0.004), r, 0.003))
        for z, ln, r in ((0.115, 0.066, 0.021), (0.185, 0.094, 0.026), (0.255, 0.074, 0.022), (0.325, 0.060, 0.019)):
            w = SW(z) + 0.003
            d = np.minimum(d, seg(m, V(w, -0.092, z), V(w + 0.008, -0.092 + ln, z), r, 0.003))
        for x in (0.040, 0.105):
            z = 0.516 - x * x * 3.0
            d = np.minimum(d, seg(m, V(x, -0.058, z), V(x, -0.124, z + 0.005), 0.019, 0.003))
        return d
    h.teeth = teeth
    h.jaw_w = 0.20; h.reach = 0.95
    return h

HEADS = {'lizard': lizard_head, 'python': python_head, 'viper': viper_head, 'croc': croc_head, 'brute': brute_head}

# ── 눈 — 고르기 축. 날카로운 쪽에서 둥근 쪽으로 ────────────────────────────────────────────────────────────
#   이름       눈동자   크기   윗꺼풀 높이 · 앞으로 기운 정도   아랫꺼풀(없으면 None)   색
EYE = {
    'sharp':   ('slit',  0.95, 0.02, 0.80, 0.50, AMBER),    # 가늘게 찢어진 눈 — 위아래 꺼풀이 다 덮는다
    'glare':   ('slit',  1.00, 0.28, 0.55, None, AMBER),    # 노려보는 눈
    'almond':  ('slit',  1.08, 0.52, 0.30, 0.80, GREEN),    # 중간 — 꺼풀이 살짝
    'bigslit': ('slit',  1.24, 0.90, 0.08, None, GREY),     # 크고 둥근 눈에 세로 눈동자(볏도마뱀붙이)
    'round':   ('round', 1.14, 0.70, 0.15, None, AMBER),    # 둥근 눈동자
    'gecko':   (None,    1.24, 0.90, 0.08, None, BLACK),    # 크고 검은 눈 + 반짝임
    'blank':   (None,    1.00, 0.28, 0.55, None, PALE),     # 눈동자 없는 흐린 눈
    'bead':    (None,    1.00, 0.92, 0.05, None, BLACK),    # 작고 검은 구슬 눈
    'ring':    ('ring',  1.00, 0.86, 0.06, None, AMBER),    # 큰 둥근 눈동자에 가는 테 + 반짝임(매끈한 뱀)
}
EYE_ORDER = ('sharp', 'glare', 'almond', 'bigslit', 'round', 'gecko', 'blank')

def pits_of(h): return getattr(h, 'pits', None) or [(h.nostril, h.nostril_r)]
def eye_frame(h):
    t = nrm(np.cross(UP, h.eye_n)); u = np.cross(h.eye_n, t)   # t = 눈의 바깥 · 뒤쪽 · u = 위
    return t, u
def head_skin(h, e):
    _, k, lid0, slant, low, _ = e; r = h.eye_r * k; t, _ = eye_frame(h)
    def f(p):
        yv = p[..., 1] - h.mouth_y(p)
        base = smin(h.cran(p), h.extra(p), 0.05)
        up = smin(base, np.maximum(h.upper(p), -(yv - G)), 0.05)
        lo = smin(base, np.maximum(h.lower(p), yv + G), 0.05)
        d = np.minimum(up, lo)
        m = mirror(p)
        for c, pr in pits_of(h): d = smax(d, -(length(m - c) - pr), 0.004)   # 콧구멍 · 열 구멍은 판다 — 튀어나온 점이 아니다
        q = m - h.eye; ball = length(q) - (r + 0.009); a = (q * t).sum(-1)
        d = np.minimum(d, np.maximum(ball, (r * lid0 + slant * a) - q[..., 1]))
        if low is not None: d = np.minimum(d, np.maximum(ball, q[..., 1] - (-r * low + 0.25 * a)))
        return d
    return f
def head_inner(h): return lambda p: np.minimum(h.upper(p), h.lower(p)) + 0.011   # 입 안 — 입선 틈으로만 보인다
def eyeball(h, e): return lambda p: sph(mirror(p), h.eye, h.eye_r * e[1])
def pupil(h, e):
    kind, k = e[0], e[1]; r = h.eye_r * k; t, u = eye_frame(h)
    def f(p):
        q = mirror(p) - h.eye
        ball = length(q) - (r + 0.004); fwd = r * 0.35 - (q * h.eye_n).sum(-1)
        a, b = (q * t).sum(-1), (q * u).sum(-1)
        shape = np.maximum(np.abs(a) - r * 0.16, np.abs(b) - r * 0.74) if kind == 'slit' else np.sqrt(a * a + b * b) - r * (0.70 if kind == 'ring' else 0.48)
        return np.maximum(np.maximum(ball, fwd), shape)
    return f
def glint(h, e):   # 반짝임은 눈 면에 붙은 얇은 판(11-18 — 튀어나오지 않게 · C#과 같은 값) — 눈동자(+4mm) 바로 위(+5mm) 껍데기의 둥근 자리
    r = h.eye_r * e[1]; t, u = eye_frame(h)
    d = nrm(h.eye_n * 0.80 + u * 0.40 - t * 0.28)
    def f(p):
        q = mirror(p) - h.eye; ln = np.maximum(length(q), 1e-6)
        return np.maximum(np.maximum(ln - (r + 0.005), (r - 0.002) - ln), (np.cos(0.24) - (q * d).sum(-1) / ln) * r)
    return f
def head_col(h):   # 아래턱은 배 색(턱 관절 앞, 아래턱 폭 안에서만) · 콧구멍 안은 검다
    S, B, K = np.asarray(h.skin, np.float32), np.asarray(h.belly, np.float32), np.asarray(DARK, np.float32)
    def c(p):
        yv = p[..., 1] - h.mouth_y(p)
        z0, z1 = getattr(h, 'chin_z', (0.03, 0.13))
        k = sstep(0.0, -0.03, yv) * sstep(z0, z1, p[..., 2]) * sstep(h.jaw_w + 0.02, h.jaw_w - 0.05, np.abs(p[..., 0]))
        col = S * (1 - k[..., None]) + B * k[..., None]
        if hasattr(h, 'paint'): col = h.paint(p, col)
        m = mirror(p)
        for cc, pr in pits_of(h):
            n = sstep(pr * 1.25, pr * 0.95, length(m - cc))
            col = col * (1 - n[..., None]) + K * n[..., None]
        return col
    return c

def pieces(kind, body=True, eye=None, crest=None, eye_col=None):
    """kind는 `종류` 또는 `종류:색`(뱀 둘은 색이 여럿 — `viper:slate` · `python:kingrat`)."""
    kind, _, tone = kind.partition(':')
    set_body(kind)
    h = HEADS[kind](crest, tone or None) if kind in ('python', 'viper') else (HEADS[kind](crest) if crest else HEADS[kind]())
    e = list(EYE[eye or h.eye_default])
    if eye_col is not None: e[5] = eye_col
    elif eye is None and hasattr(h, 'eye_col'): e[5] = h.eye_col
    LOOK = dict(BRUTE_LOOK)   # 만들 때의 생김새를 붙잡는다 — 조각이 그려질 때 전역을 읽으면 여러 생김새를 한 목록에 담지 못한다(`bake_croc.py`)
    def warp(q): return head_warp(q, LOOK) if kind == 'brute' else (q, 1.0)   # 공룡 악어의 머리 모양 축(`BRUTE_HEADS`)
    def hd(f):
        def g(q):
            w, k = warp((q - HO) / HK); return f(w) * HK * k
        return lambda p: bounded(g, p, HO + V(0, 0, 0.12 * HK), h.reach * HK * (1.45 if kind == 'brute' else 1.0))
    def hcol(c): return (lambda p: c(warp((p - HO) / HK)[0])) if callable(c) else c
    hc = head_col(h)
    P = [(hd(head_skin(h, e)), hcol(hc)), (hd(head_inner(h)), MOUTH), (hd(eyeball(h, e)), e[5]), (hd(h.spikes), hcol(h.spike_col))]
    if e[0]: P.append((hd(pupil(h, e)), DARK))
    if e[5] == BLACK or e[0] == 'ring': P.append((hd(glint(h, e)), WHITE))
    if h.teeth: P.append((hd(h.teeth), BONE))
    if h.tongue: P.append((hd(h.tongue), TONGUE))
    if kind == 'brute' or LIZ_FINS:   # 볏(가시살 + 막) — 공룡 악어는 고르기 축(`BRUTE_LOOK['fins']`) · 도마뱀은 미리보기 판만(`LIZ_FINS`)
        mem, spn, fcol = fins(*FIN_AT[kind])
        on = LOOK['fins'] == 'fins' if kind == 'brute' else True
        fin_col = hcol(fcol(h.skin, BRUTE_PAL[LOOK['pal']]['fin'] if kind == 'brute' else LIZ_FIN_COL))
        P += [(hd(mem if on else far), fin_col), (hd(spn if on else far), hcol(h.spike_col))]
    bc = body_col(h.skin, h.belly)
    if kind == 'brute':   # 근육질 몸 — 몸통에 근육 덩이를 얕게 녹이고 팔 · 손 · 다리 · 발을 따로 빚는다
        def skin_body(p):
            tn = smin(smin(torso(p), neck(p), 0.05), brute_muscles(p), 0.09)
            return union([smin(tn, brute_arms(p), 0.03), brute_hands(p), brute_legs(p), brute_feet(p), smin(smin(torso(p), tail(p), 0.06), tail_club(p, LOOK), 0.03)])
    else:
        def skin_body(p): return union([smin(torso(p), neck(p), 0.05), arms(p), hands(p), legs(p), feet(p), smin(torso(p), tail(p), 0.06)])
    if body:
        bcol = (lambda p: h.body_paint(p, bc(p))) if hasattr(h, 'body_paint') else bc
        claws = (brute_hand_claws, brute_foot_claws) if kind == 'brute' else (hand_claws, foot_claws)
        P += [(skin_body, bcol), (claws[0], BONE), (claws[1], BONE), (belt, LEATHER), (flap, CLOTH)]
        sc = getattr(h, 'scaly', None)
        if sc:
            for lv in body_leaves(sc['lift']): P.append((lv.dist, lv.tint(sc['dark'], sc['tip'], *sc['ramp'])))
        elif kind == 'lizard' and (crest or 'fringe') == 'fringe': P.append((fringe_rows_f, h.spike_col))   # 가시 테 — 등 양쪽 두 줄(한가운데 줄 대신)
        elif kind == 'lizard' and crest == 'bare': pass                                                        # 뿔 없는 가시 테 머리 — 등도 매끈
        elif kind == 'brute': P += [(lambda p: brute_plates_f(p, LOOK['back']), h.spike_col), (lambda p: club_bony(p, LOOK), h.spike_col)]   # 골판 · 꼬리 곤봉의 뼈(그대로인 꼬리면 비었다)
        elif kind != 'python': P.append((ridge, (0.24, 0.36, 0.20) if kind == 'lizard' else h.spike_col))
    else:
        cut = HO[1] - 0.52 * HK
        P += [(lambda p: np.maximum(smin(torso(p), neck(p), 0.05), cut - p[..., 1]), bc)]
    if kind == 'brute':   # 몸 전체를 키운다 — 크기는 메시가 든다
        S = BRUTE_S
        P = [((lambda f: lambda p: f(p / S) * S)(f), ((lambda g: lambda p: g(p / S))(col) if callable(col) else col)) for f, col in P]
    return P

KINDS = ('lizard', 'python', 'viper', 'croc')
SNAKES = ('viper', 'viper:slate', 'viper:coal', 'viper:amber', 'python', 'python:kingrat', 'python:indigo', 'python:red')
FULL = ((0, 380), (40, 500), (90, 760), (150, 560))                  # 보기 각도 · 폭
CLOSE = ((0, 360, 0), (35, 440, 0), (90, 600, 0), (25, 480, 50))    # 각도 · 폭 · 내려다보기(게임 카메라 쪽)
CRESTS = ('fringe', 'comb', 'none', 'bare')
BRUTE_VIEW = (1.50, 3.40)   # 공룡 악어 판의 보기 — 가운데 높이 · 세로 범위(m). 도마뱀도 같은 눈금으로 그려 크기를 견준다
def job(a):
    mode, kind, i = a
    if mode == 'lizfins':   # 도마뱀 볏 — 볏 넷(가시 테 · 이구아나 · 없음 · 뿔 없음) 머리 + 온몸 하나
        global LIZ_FINS
        LIZ_FINS = True
        if i < len(CRESTS):
            P = pieces('lizard', body=False, crest=CRESTS[i]); return np.asarray(shifted(P, 30, 600, 420, 1.20, 15))
        return np.asarray(hb.render(pieces('lizard'), 40, 600, 640, 1.08, 2.62))
    if mode == 'tails':   # 꼬리 끝 셋(그대로 · 곤봉 · 가시 곤봉) — 꼬리 끝을 가운데에 두고 비스듬히 위에서 · 색은 첫 후보로 고정(일꾼마다 앞 후보의 색이 남는다)
        set_look(dict(BRUTE_LOOKS[0], tail=BRUTE_TAILS[i])); P = pieces('brute'); c = (TAIL[7] + TAIL[11]) * 0.5 * BRUTE_S   # 꼬리 뒤 절반을 가운데에(양옆 뿔이 다 들어오게)
        Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in P]
        return np.asarray(hb.render(Q, 140, 600, 420, 0.0, 1.70, pitch=55))   # 위에서 비스듬히(사용자 그림처럼)
    if mode == 'looks':   # 공룡 악어 후보 — 앞 열은 온몸(40°), 뒤 열은 머리(30° · 조금 내려다보기)
        set_look(BRUTE_LOOKS[i % len(BRUTE_LOOKS)])
        if i < len(BRUTE_LOOKS): return np.asarray(hb.render(pieces('brute'), 40, 600, 640, *BRUTE_VIEW))
        P = pieces('brute', body=False)
        return np.asarray(shifted(P, 30, 600, 420, 1.25 * HK * BRUTE_S, 15, BRUTE_S))
    if mode == 'brute':
        if i < len(FULL):
            yaw, w = FULL[i]; return np.asarray(hb.render(pieces('brute'), yaw, (700, 760, 1000, 820)[i], 760, *BRUTE_VIEW))
        if i == len(FULL): return np.asarray(hb.render(pieces('lizard'), 40, 420, 760, *BRUTE_VIEW))
        yaw, w, pitch = CLOSE[i - len(FULL) - 1]; P = pieces('brute', body=False)
        return np.asarray(shifted(P, yaw, w, 400, 1.30 * HK * BRUTE_S, pitch, BRUTE_S))
    if mode == 'full':
        yaw, w = FULL[i]
        return np.asarray(hb.render(pieces(kind), yaw, w, 640, 1.08, 2.62))
    if mode == 'close':
        yaw, w, pitch = CLOSE[i]; P = pieces(kind, body=False)
        return np.asarray(shifted(P, yaw, w, 360, 1.20 * HK, pitch))
    if mode == 'eyes':
        P = pieces(kind, body=False, eye=EYE_ORDER[i])
        return np.asarray(shifted(P, 32, 300, 280, 0.95 * HK, 0))
    yaw, w, pitch = ((35, 440, 0), (90, 560, 0), (25, 480, 50))[i]   # crest — kind 자리에 볏 이름
    P = pieces('lizard', body=False, crest=kind)
    return np.asarray(shifted(P, yaw, w, 360, 1.15, pitch))
def shifted(P, yaw, w, h, span, pitch, k=1.0):
    """머리가 굽은 어깨 앞에 있어 원점 둘레로 돌리면 그림 밖으로 나간다 — 머리 자리를 원점으로 옮겨 그린다. `k` — 키운 몸의 배수."""
    c = (HO + V(0, 0.0, 0.10 * HK)) * k
    Q = [((lambda f: lambda p: f(p + c))(f), ((lambda g: lambda p: g(p + c))(col) if callable(col) else col)) for f, col in P]
    return hb.render(Q, yaw, w, h, 0.0, span, pitch=pitch)

def sheet(mode, rows, ws, hgt, ims, out, labels=None):
    im = Image.new('RGB', (sum(ws), hgt * len(rows)), (40, 40, 44)); dr = ImageDraw.Draw(im)
    for r, kind in enumerate(rows):
        x = 0
        for i in range(len(ws)):
            im.paste(Image.fromarray(ims[(mode, kind, i)]), (x, r * hgt))
            if labels: dr.text((x + 6, r * hgt + 18), labels[i], fill=(200, 200, 210))
            x += ws[i]
        dr.text((6, r * hgt + 4), kind, fill=(255, 220, 120))
    im.save(out); print('saved', out)

if __name__ == '__main__':
    from multiprocessing import Pool
    stem = sys.argv[1]
    only = sys.argv[2] if len(sys.argv) > 2 else 'all'
    on = lambda m: only in ('all', m)
    for k in KINDS:
        set_body(k); print('%-6s 머리통 꼭대기 %.2f m · 어깨 %.2f m' % (k, HO[1] + 0.24 * HK, TOP))
    jobs = []
    if on('full'):  jobs += [('full', k, i) for k in KINDS for i in range(len(FULL))]
    if on('heads'): jobs += [('close', k, i) for k in KINDS for i in range(len(CLOSE))]
    if on('eyes'):  jobs += [('eyes', k, i) for k in KINDS for i in range(len(EYE_ORDER))]
    if on('crest'): jobs += [('crest', c, i) for c in CRESTS for i in range(3)]
    if on('snakes'): jobs += [(m, k, i) for m, V_ in (('full', FULL), ('close', CLOSE)) for k in SNAKES for i in range(len(V_))]
    if only == 'brute': jobs += [('brute', 'brute', i) for i in range(len(FULL) + 1 + len(CLOSE))]
    if only == 'looks': jobs += [('looks', 'brute', i) for i in range(2 * len(BRUTE_LOOKS))] + [('lizfins', 'lizard', i) for i in range(len(CRESTS) + 1)] + [('tails', 'brute', i) for i in range(len(BRUTE_TAILS))]
    jobs = list(dict.fromkeys(jobs))
    with Pool(min(14, len(jobs))) as pool: res = pool.map(job, jobs)
    ims = dict(zip(jobs, res))
    if on('full'):  sheet('full', KINDS, [w for _, w in FULL], 640, ims, stem + '_full.png')
    if on('heads'): sheet('close', KINDS, [v[1] for v in CLOSE], 360, ims, stem + '_heads.png')
    if on('eyes'):  sheet('eyes', KINDS, [300] * len(EYE_ORDER), 280, ims, stem + '_eyes.png', EYE_ORDER)
    if on('crest'): sheet('crest', CRESTS, [440, 560, 480], 360, ims, stem + '_crest.png')
    if only == 'looks':   # 후보 열 — 온몸 5×2 · 머리 5×2(번호 · 색 · 눈 위 뿔 · 정수리 · 코뿔 · 등 골판)
        n = len(BRUTE_LOOKS)
        for tag, off, hgt in (('full', 0, 640), ('heads', n, 420)):
            im = Image.new('RGB', (600 * 5, hgt * 2), (40, 40, 44)); dr = ImageDraw.Draw(im)
            for k in range(n):
                x, y = (k % 5) * 600, (k // 5) * hgt; im.paste(Image.fromarray(ims[('looks', 'brute', off + k)]), (x, y))
                L_ = BRUTE_LOOKS[k]
                dr.text((x + 8, y + 6), '%d  %s' % (k + 1, L_['pal']), fill=(255, 220, 120))
                dr.text((x + 8, y + 20), 'head %s / brow %s / crown %s / nose %s / back %s / fins %s / tail %s' % (L_['head'], L_['brow'], L_['crown'], L_['nose'], L_['back'], L_['fins'], L_['tail']), fill=(200, 200, 210))
            im.save('%s_looks_%s.png' % (stem, tag)); print('saved', '%s_looks_%s.png' % (stem, tag))
        im = Image.new('RGB', (600 * (len(CRESTS) + 1), 640), (40, 40, 44)); dr = ImageDraw.Draw(im)
        for k in range(len(CRESTS) + 1):
            im.paste(Image.fromarray(ims[('lizfins', 'lizard', k)]), (600 * k, 0)); dr.text((600 * k + 8, 6), CRESTS[k] + ' + fins' if k < len(CRESTS) else 'lizard + fins', fill=(255, 220, 120))
        im.save('%s_looks_lizard_fins.png' % stem); print('saved', '%s_looks_lizard_fins.png' % stem)
        im = Image.new('RGB', (600 * len(BRUTE_TAILS), 420), (40, 40, 44)); dr = ImageDraw.Draw(im)
        for k, t in enumerate(BRUTE_TAILS): im.paste(Image.fromarray(ims[('tails', 'brute', k)]), (600 * k, 0)); dr.text((600 * k + 8, 6), 'tail ' + t, fill=(255, 220, 120))
        im.save('%s_looks_tails.png' % stem); print('saved', '%s_looks_tails.png' % stem)
    if only == 'brute':   # 윗줄 — 온몸 넷 + 같은 눈금의 도마뱀 · 아랫줄 — 머리 넷
        top = [ims[('brute', 'brute', i)] for i in range(len(FULL) + 1)]; low = [ims[('brute', 'brute', i)] for i in range(len(FULL) + 1, len(FULL) + 1 + len(CLOSE))]
        wd = max(sum(t.shape[1] for t in top), sum(t.shape[1] for t in low))
        im = Image.new('RGB', (wd, 760 + 400), (40, 40, 44)); dr = ImageDraw.Draw(im); x = 0
        for i, t in enumerate(top):
            im.paste(Image.fromarray(t), (x, 0))
            if i == len(FULL): dr.text((x + 6, 6), 'lizard (same scale)', fill=(200, 200, 210))
            x += t.shape[1]
        x = 0
        for t in low: im.paste(Image.fromarray(t), (x, 760)); x += t.shape[1]
        dr.text((6, 4), 'brute x%.2f' % BRUTE_S, fill=(255, 220, 120))
        im.save(stem + '_brute.png'); print('saved', stem + '_brute.png')
        ref = _os.path.join(_here, '..', '_refs', '2026-11-14_lizard_Croc', 'user_markup_v7.png')
        if _os.path.exists(ref):   # 사용자가 고쳐 그린 그림(왼쪽 · 깃 밖)과 정면을 나란히 — 보낸 그림이 목표다
            r = Image.open(ref).convert('RGB'); r = r.resize((int(r.width * 760 / r.height), 760), Image.LANCZOS)
            cm = Image.new('RGB', (r.width + top[0].shape[1], 760), (40, 40, 44)); cm.paste(r, (0, 0)); cm.paste(Image.fromarray(top[0]), (r.width, 0))
            cm.save(stem + '_cmp.png'); print('saved', stem + '_cmp.png')
    if on('snakes'):
        sheet('full', SNAKES, [w for _, w in FULL], 640, ims, stem + '_snakes_full.png')
        sheet('close', SNAKES, [v[1] for v in CLOSE], 360, ims, stem + '_snakes_heads.png')
