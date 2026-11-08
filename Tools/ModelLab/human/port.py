"""확인받은 머리카락(엘프 · 드워프 · 여성 오크 · 오크 올백)을 **원본 스크립트 그대로** 사람 머리 열 벌에 씌운다.

11-07 사용자: *"머리카락을 너무 못만든 거 같아 엘프나 다른 머리카락 많이 했는데 왜 이렇게 만들지"* — 새로 지은 껍데기(human.py의 옛 HAIRS)는
버리고, 확인받은 원본을 가져온다. 원본은 모두 같은 꼴의 바탕 머리(폭 · 높이 · 깊이 · 쏠림 · 윤곽 표 · 눈/입/코 높이 — `dwarf/head_base.py` ·
`orc_female/orc_preview.py`)에 기대어 이마선 · 숱 · 가닥 자리를 머리에서 뽑으므로, **바탕 머리 자리에 사람 머리의 값을 끼워 원본을 다시 읽으면**
같은 디자인이 그 머리에 맞춰 빚어진다(수식을 손으로 옮기지 않는다 — 옮기면 또 어긋난다).
- 귀를 파내는 자리(엘프 `ear_blade` · 오크 `ear`)는 사람 귀(`human.ear`)로 바꿔 끼운다.
- 엘프 원본은 얼굴 폭을 좁혔다(`NAR = 0.88`) — 사람 머리는 좁히지 않는다(1).
"""
import os, sys, re, types
import numpy as np
import human as HU

LAB = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
ORC_WT = os.path.join(LAB, 'orc')   # 오크 올백(`orc_parts.h_swept` — 작업 공간 `orc-hair-styles`가 메인에 합쳐졌다)
FAMILY_MODS = ('prof', 'head_base', 'dwarf', 'elf', 'orc_preview', 'orc_parts', 'hair_f', 'hair_v2')

def _base_text(path, h, f):
    """바탕 머리 스크립트의 치수 줄만 사람 머리 값으로 바꾼다."""
    s = open(path, encoding='utf-8').read()
    s = s.replace('from prof import T, R', f'T = {list(map(float, h.T))}; R = {list(map(float, h.R))}')
    s = re.sub(r'^W, H, D, F = .*$', f'W, H, D, F = {h.W}, {h.H}, {h.D}, {h.F}', s, flags=re.M)
    s = re.sub(r'^EYE_T, EYE_DX, EYE_S, MOUTH_T, NOSE_T = .*$',
               f"EYE_T, EYE_DX, EYE_S, MOUTH_T, NOSE_T = {f['EyeT']}, {f['EyeDx']}, {f['EyeS']}, {f['MouthT']}, {f['NoseT']}", s, flags=re.M)
    return s

def _load(name, path, text=None):
    m = types.ModuleType(name); m.__file__ = path
    sys.modules[name] = m
    exec(compile(text if text is not None else open(path, encoding='utf-8').read(), path, 'exec'), m.__dict__)
    return m

def _fresh(dirs):
    for n in FAMILY_MODS: sys.modules.pop(n, None)
    for d in reversed(dirs):
        if d in sys.path: sys.path.remove(d)
        sys.path.insert(0, d)

def _layout(h):   # `human.Head`가 쓴 얼굴 표(여자는 같은 모양의 남자 값)
    n = h.name.replace('Female_', 'Male_') if h.female else h.name
    return HU.HEADS[n]

def family(kind, head_name):
    """(머리카락 이름 → p를 받아 [(거리장, 'hair' | 'deco')]를 돌려주는 함수)."""
    h = HU.Head(head_name); f = _layout(h)
    human_ear = lambda p, *_: HU.ear(h, p)
    out = {}
    if kind == 'human':
        return h, _human_family(h, human_ear)
    if kind in ('elf', 'dwarf'):
        dd = os.path.join(LAB, 'dwarf'); ed = os.path.join(LAB, 'elf')
        _fresh([dd, ed, os.path.join(LAB, 'common')])
        _load('prof', os.path.join(dd, 'prof.py'))
        _load('head_base', os.path.join(dd, 'head_base.py'), _base_text(os.path.join(dd, 'head_base.py'), h, f))
        dw = _load('dwarf', os.path.join(dd, 'dwarf.py'))
        if kind == 'dwarf':
            for k, fn in dw.HAIRS.items():
                if k == 'Bald': continue
                out[k] = (lambda fn: lambda p: list(zip(fn(p), ('hair', 'deco'))))(fn)
        else:
            src = open(os.path.join(ed, 'elf.py'), encoding='utf-8').read().replace('NAR = 0.88', 'NAR = 1.0')
            el = _load('elf', os.path.join(ed, 'elf.py'), src)
            el.ear_blade = human_ear                                   # 귀 둘레 파내기는 사람 귀로
            el.front_locks = _flat_front_locks(el, HU)                 # 앞 가닥은 얇고 넓은 띠로
            spec = el.EARS['Leaf']
            ear_y = (el.EY + el.NY) * 0.5
            def under_ear(p, d):   # 귀 밑 숱을 비운다(11-07 사용자: *"엘프 M자 머리 귀 밑에 머리카락이 있는게 이상함"* — 엘프 구레나룻이 사람 귀 밑까지 내려왔다) · 머리 가까이(6cm)만 — 뒷머리 기둥은 그대로
                ang = el.angle(p)
                r = np.maximum(np.maximum(np.maximum(80 - ang, ang - 100) * np.pi / 180 * 0.30, p[..., 1] - ear_y), el.nhead(p) - 0.06)
                return HU.smax(d, -r, 0.01)
            for k, fn in el.HAIRS.items():
                out[k] = (lambda fn: lambda p: [(under_ear(p, v) if i == 0 else v, t) for i, (v, t) in enumerate(zip(fn(p, spec), ('hair', 'deco', 'plait')))])(fn)
    elif kind in ('orcF', 'orcM'):
        d = os.path.join(LAB, 'orc_female') if kind == 'orcF' else ORC_WT
        _fresh([d, os.path.join(LAB, 'common')])
        _load('prof', os.path.join(d, 'prof.py'))
        _load('orc_preview', os.path.join(d, 'orc_preview.py'), _base_text(os.path.join(d, 'orc_preview.py'), h, f))
        op = _load('orc_parts', os.path.join(d, 'orc_parts.py'))
        op.ear = human_ear
        if kind == 'orcF':
            _load('hair_f', os.path.join(d, 'hair_f.py'))
            hv = _load('hair_v2', os.path.join(d, 'hair_v2.py'))
            for k, fn in hv.HAIRS:
                out[k] = (lambda fn: lambda p: [(r, 'hair' if i == 0 else 'deco') for i, r in enumerate(fn(p) if isinstance(fn(p), tuple) else (fn(p),))])(fn)
        else:
            out['Swept'] = lambda p: list(zip(op.h_swept(p), ('hair', 'deco')))
    return h, out


def _flat_front_locks(el, HU):
    """엘프 생머리 앞 가닥을 **얇고 넓은 띠**로(11-07 사용자: *"더듬이 머리가 너무 원으로 두꺼워 … 머리카락은 보통 얇고 넓은데"*) — 원본 판(`elf.panel` F 1.8 ·
    반지름 0.048)은 두께가 9cm 가까운 관이었다. 같은 길로 폭(얼굴 앞뒤)은 그대로, 얼굴 옆으로의 두께만 1/5로."""
    F = 2.75   # 폭 r·F = 원본 폭의 55%(11-07 사용자: *"더듬이 머리가 너무 넓어서 다른 머리카락이랑 겹치면 내려오는 부분이 좀 잘리네 그 잘린 크기에 맞춰서"*) · 두께는 그대로 얇게
    def front_locks(p, L=-0.62):
        m = HU.mirror(p); x1, _, z1 = (float(v) for v in el.CURL[1])
        pts = [el.CURL[0], el.CURL[1], HU.V(x1 + 0.020, el.EY - 0.08, z1 - 0.04), HU.V(x1 + 0.030, el.MY - 0.12, z1 - 0.07), HU.V(x1 + 0.030, L, z1 - 0.10)]
        radii = [r * 1.8 / 5.0 for r in (0.030, 0.042, 0.048, 0.044, 0.026)]                  # 두께(얼굴 옆) — 원본의 1/5
        sc = np.array([F, 1, 1], np.float32)
        q = m * sc
        return HU.lock_r(q, [t * sc for t in pts], [r * F for r in radii], 0.02) / F
    return front_locks

def _human_family(h, human_ear):
    """사람 머리 — 드워프 · 엘프 원본을 이 머리에 읽어 와 묶는다(11-07 사용자: *"드워프의 그 굵은 밧줄같은 뒷머리들이 이상한 거 같아 인간한테는"* ·
    *"인간머리니깐 드워프 장신구는 제외"* · 쌍꼬리 · 갈기는 다시).
    - 숱 · 이마선은 드워프 원본(`scalp` · `hairline` · 벗겨진 띠 · 볏) 그대로.
    - **뒷머리는 엘프 생머리의 납작한 가닥 기둥**(`back_fall` — 숱 겉면에 붙어 귀 뒤부터 정뒤까지 곧게 떨어진다) — 드워프의 둥근 밧줄 대신.
    - 땋은 가닥은 엘프 땋은 장식 굵기(`plait`) · 꽁지는 꽁지 규칙(`human.tail` — 끈 잘록 · TAIL_R · 끝이 갈라진다) · 쇠고리 · 감개는 없다."""
    dd = os.path.join(LAB, 'dwarf'); ed = os.path.join(LAB, 'elf')
    _fresh([dd, ed, os.path.join(LAB, 'common')])
    _load('prof', os.path.join(dd, 'prof.py'))
    _load('head_base', os.path.join(dd, 'head_base.py'), _base_text(os.path.join(dd, 'head_base.py'), h, _layout(h)))
    dw = _load('dwarf', os.path.join(dd, 'dwarf.py'))
    el = _load('elf', os.path.join(ed, 'elf.py'), open(os.path.join(ed, 'elf.py'), encoding='utf-8').read().replace('NAR = 0.88', 'NAR = 1.0'))
    el.ear_blade = human_ear
    el.front_locks = _flat_front_locks(el, HU)
    V, smin, smax, ell, mirror = HU.V, HU.smin, HU.smax, HU.ell, HU.mirror
    EY = dw.EY
    def cut(p, d, gap=0.006): return smax(d, -(human_ear(p) - gap), 0.004)   # 귀는 숱을 뚫고 나온다 · `gap` — 귀 둘레를 얼마나 비우나

    def flared_fall(p, L, flare, a0=95):   # 엘프 뒷머리 기둥 — 아래로 갈수록 바깥으로 퍼진다(갈기)
        m = mirror(p); d = HU.NONE(p)
        for a in np.linspace(a0, 180, 10):
            dx, dz = np.sin(np.radians(a)), np.cos(np.radians(a)); nrm = V(dx, 0, dz)
            side = p if a >= 179.9 else m
            c = el._column(a)
            y_end = L + 0.12 * ((180 - a) / (180 - a0)) ** 2
            top = el.on_cap(dx * 0.3, 0.17, dz * 0.3, -0.004)
            pts = [top, V(c[0], 0.04, c[2]), V(c[0], -0.28, c[2]) + nrm * (0.004 + flare * 0.4), V(c[0], y_end, c[2]) + nrm * (0.012 + flare)]
            d = smin(d, el.nlock(side, pts, 0.060, 0.040, nrm), 0.02)
        return d

    def h_long(p):          # 뒤로 넘긴 긴 머리 — 드워프 숱 · 이마선 + 엘프 뒷머리(어깨 아래까지)
        cap = dw.scalp(p, 0.035, dw.hairline(dw.angle(p), EY + 0.20))
        return [(cut(p, smin(cap, el.back_fall(p, -0.66), 0.04) + dw.strands(p, 0.0015, 55)), 'hair')]
    def h_longfringe(p):    # 정수리는 벗어지고 옆뒤만 어깨까지 — 드워프 띠 + 엘프 뒷머리
        ang = dw.angle(p)
        ring_ = dw.scalp(p, 0.032, -0.10)
        ring_ = smax(ring_, p[..., 1] - (0.03 + 0.08 * dw.ease((ang - 110) / 60) + 0.015 * np.sin(np.radians(ang) * 12)), 0.02)
        ring_ = smax(ring_, (95 - ang) * 0.004, 0.02)
        fall = smax(el.back_fall(p, -0.60), p[..., 1] - 0.04, 0.03)              # 기둥은 띠 아래에서만
        return [(cut(p, smin(ring_, fall, 0.04) + dw.strands(p, 0.0015, 55)), 'hair')]
    def h_mane(p):          # 갈기(다시) — 숱이 많은 뒤로 넘긴 머리 · 정수리가 둥글게 들리고 가닥이 어깨께에서 바깥으로 퍼진다
        ang = dw.angle(p)
        cap = el.scalp(p, 0.040 + 0.020 * dw.ease((p[..., 1] - EY) / 0.3), dw.hairline(ang, EY + 0.18))
        return [(cut(p, smin(cap, flared_fall(p, -0.62, 0.11), 0.05) + dw.strands(p, 0.002, 50)), 'hair')]
    def h_braid(p):         # 뒤로 빗어 넘겨 한 갈래로 땋았다 — 엘프 땋은 장식 굵기 · 끝은 꽁지처럼 갈라진다(고리 없다)
        cap = natural_cap(p, 0.012)                                                          # 둥근 이마선 · 얇게 시작(드워프 이마선은 직각이었다)
        top = el.on_cap(0.0, 0.02, -0.3, 0.0)
        pts = [top, top + V(0, -0.14, -0.04), top + V(0, -0.32, -0.05), top + V(0, -0.50, -0.04), top + V(0, -0.68, -0.02)]   # 등 중간까지(목께에서 끝나면 안 보였다 · v3)
        d = smin(cap, el.plait(p, pts, 0.044, 0.030, 13), 0.03)
        brush = HU.tail(p, pts[-1], -0.02, 0.0, 0.12, 0.40)
        return [(cut(p, smin(d, brush, 0.01)), 'hair')]
    def h_twinbraids(p):    # 두 갈래 땋아 어깨 너머 가슴으로 — 가늘게 · 고리 없이
        cap = dw.scalp(p, 0.034, dw.hairline(dw.angle(p), EY + 0.19)) + dw.strands(p, 0.003, 90)
        m = mirror(p); pts = dw._twin_pts()
        d = smin(cap, el.plait(m, pts, 0.042, 0.030, 12), 0.03)
        return [(cut(p, smin(d, HU.tail(m, pts[-1], -0.02, 0.0, 0.10, 0.35), 0.01)), 'hair')]
    def h_topknot(p):       # 정수리 뒤 상투 + 꽁지(꽁지 규칙) — 고리 대신 끈 자리만 잘록
        cap = natural_cap(p, 0.012)                                                          # 둥근 이마선 · 얇게 시작(드워프 이마선은 직각이었다)
        tie = el.on_cap(0.0, 0.30, -0.18, 0.03)
        knot = ell(p, tie + V(0, 0.03, 0.01), V(0.075, 0.065, 0.075))
        d = smin(cap, knot, 0.04)
        d = smin(d, HU.tail(p, tie + V(0, 0.0, -0.04), 0.06, 0.24, 0.62), 0.02)
        return [(cut(p, d), 'hair')]
    def h_fringe(p):        # 벗겨진 머리 — 드워프 그대로
        return [(dw.h_fringe(p)[0], 'hair')]
    def h_crest(p):         # 볏 — 드워프 숱 그대로 · 고리는 뺀다
        return [(dw.h_crest(p)[0], 'hair')]
    def h_twintails(p):     # 쌍꼬리(다시) — 가운데 가르마로 숱을 덮고 귀 뒤에 낮게 묶어 아래로 늘어뜨린다 · 꼬리마다 꽁지 규칙
        ang = dw.angle(p)
        cap = el.scalp(p, 0.028, el.front_line(ang)) + el.part_line(p)
        m = mirror(p)
        # **좌우로 뻗는다**(11-07 사용자: *"뒤로 되어있는 트윈테일이 아니라 좌우로 빠져있는게 더 트윈테일 같긴"*) — 정수리 옆(귀 위)에서 묶어
        # 바깥으로 솟았다가 휘어 내린다. 굵기 · 끈 · 끝 갈라짐은 꽁지 규칙(`human.TAIL_R` · 가닥 여섯) 그대로, 가닥이 나가는 방향만 옆(+x)이다.
        tie = el.on_cap(0.30, EY + 0.17, -0.10, 0.03)
        R = [r * 0.85 for r in HU.TAIL_R]; tails = HU.NONE(p)
        for dy, dz, s_ in ((0.00, 0.00, 1.0), (0.025, -0.03, 0.92), (-0.02, 0.02, 0.96), (0.01, 0.04, 0.88), (-0.025, -0.02, 1.04), (0.0, -0.05, 0.94)):
            pts = HU.bez(tie, tie + V(0.10, 0.07 + dy, -0.02), tie + V(0.24, 0.02 + dy, -0.04 + dz), tie + V(0.26 + 0.03 * dy / 0.025, -0.55 * s_, -0.06 + dz), 8)
            tails = smin(tails, HU.lock_r(m, pts, R), 0.03)
        d = smin(cap, tails + dw.strands(p, 0.002, 60), 0.03)
        band = HU.band(m, tie + V(-0.008, -0.004, 0.0), tie + V(0.022, 0.010, -0.004), 0.034)   # 끈 — 잘록한 목보다 살짝만 굵다
        return [(cut(p, d), 'hair'), (band, 'hair')]
    def fringe(p, hem, reach):   # 앞머리 — 이마를 감싸는 한 장(엘프 앞머리 기법)에 아랫단이 다발로 갈라진다(11-07 사용자: *"앞머리랑 더듬이 머리쪽을 더 자연스럽게"*)
        ang = dw.angle(p)
        ph = ang / 10.5 + 0.5
        w = 1 - np.abs(2 * (ph - np.floor(ph)) - 1)                                          # 다발 가운데 1 · 다발 사이 0
        h_ = hem(ang) + 0.022 * (1 - w) ** 2.2                                               # 다발 끝(v13 — 사용자가 v14보다 좋다고 함)
        th = 0.008 + 0.024 * dw.ease((p[..., 1] - h_) / 0.10)                                # 아랫단에서 얇고 위로 숱 두께
        d = el.scalp(p, th, h_)
        d = d + 0.003 * (1 - w) * dw.ease((p[..., 1] - h_) / 0.06)                           # 다발 사이의 얕은 골 — 위로 잦아든다
        return smax(d, (ang - reach) * np.pi / 180 * 0.28, 0.01)
    def side_locks(p, vertical=False):   # 더듬이 — 가닥 둘을 맞붙여 **사이 틈 없이**(11-07 사용자: *"머리카락 틈을 채우라는 말"*) · 위는 숱 속에서 시작
        # vertical=False: v15식 — 앞머리 끝 관자놀이에서 볼을 따라 살짝 바깥으로 휘며 턱 아래로
        # vertical=True : v14식 — 관자놀이 · 귀 앞에서 거의 곧게 떨어진다(사용자: *"14처럼 더듬이가 직각으로 내려간 것도"*)
        m = mirror(p); d = HU.NONE(p)
        specs = ((0.21, 0.15, 0.15, 1.0), (0.225, 0.115, 0.11, 0.9)) if vertical else ((0.17, 0.22, 0.10, 1.0), (0.185, 0.19, 0.07, 0.9))
        for (x0, z0, L, k) in specs:
            a = el.on_cap(x0, EY + 0.12, z0, -0.004)                                         # 숱 속에서 — 갈라지는 자리에 틈이 안 나게
            if vertical:
                b = el.on_cap(x0 + 0.01, EY - 0.04, z0 - 0.005, 0.016)
                c = HU.V(float(b[0]) + 0.012, dw.MY + 0.02, float(b[2]) - 0.01)              # 눈 높이 아래는 곧게 — 턱으로 감겨 들지 않는다
                e = HU.V(float(c[0]) + 0.012, dw.MY - L, float(c[2]) - 0.01)
            else:
                b = el.on_cap(x0 + 0.04, EY - 0.04, z0 - 0.05, 0.016)
                c = HU.V(float(b[0]) + 0.025, dw.MY + 0.02, float(b[2]) - 0.05)              # 눈 높이 아래는 곧게 — 턱으로 감겨 들지 않는다
                e = HU.V(float(c[0]) + 0.01, dw.MY - L, float(c[2]) - 0.02)
            d = smin(d, el.panel(m, [a, b, c, e], [0.020 * k, 0.022 * k, 0.018 * k, 0.005]), 0.03)   # 맞붙은 두 가닥을 넉넉히 녹인다
        return d
    def h_twinhigh(p, vertical=False):      # 높이 묶은 긴 쌍꼬리(11-07 사용자 그림 `_refs/2026-11-07_human/want_twintails_*` · 깃 밖) — 공통 특징만:
        # 정수리 뒤 양옆에 높이 묶어 살짝 솟았다가 어깨 뒤로 허리께까지 떨어지는 넓은 꼬리 · 눈썹까지 덮는 앞머리(끝이 가늘게 갈라진다) ·
        # 귀 앞으로 얼굴을 감싸 턱 아래까지 내려오는 옆 가닥 · 뒤통수는 가운데 가르마에서 묶은 자리로 빗어 올렸다.
        ang = dw.angle(p); m = mirror(p)
        hem = lambda a: np.interp(a, [0, 14, 28, 42, 50, 58, 66, 72], [EY + 0.072, EY + 0.070, EY + 0.076, EY + 0.072, EY + 0.060, EY + 0.000, dw.MY - 0.02, dw.MY - 0.08])   # 눈썹에 걸친다 · 바깥 끝은 아래로 흘러내려 더듬이로 잇는다(v18 — 사용자 그림 `user_19.png`)
        # v14식은 귀 위로 끝나는 이마선 · v15식은 구레나룻 — 단 귀 근처(72° 뒤)는 둘 다 귀 위로(구레나룻이 귀를 덮었다 · v24)
        line = el.front_line if vertical else (lambda a: np.where(a < 72, el.curtain_line(a), el.front_line(a)))
        low = np.where(ang < 90, np.maximum(line(ang), hem(np.minimum(ang, 72))), line(ang))
        # 귀 앞은 더듬이 뒤부터 귀까지 숱으로 채워 턱께까지(11-07 사용자 그림 `_refs/2026-11-07_human/want_side_filled_to_ear_v22.png` ·
        # *"왜 머리카락을 자꾸 파고들라고 하는거야"* — 귀 둘레를 넓게 파던 것 · 귀 앞 살 틈은 틀렸다)
        low = np.minimum(low, np.interp(ang, [50, 57, 79, 85], [9.0, dw.MY + 0.03, dw.MY + 0.03, 9.0]))   # 귀 앞 가장자리는 덮지 않는다(v23) · 입 높이 위까지만 — 아래로 내리면 턱선에 감겨 볼에 붙었다(사용자 그림 `want_gap_beside_jaw_v25.png`)
        cap = el.scalp(p, 0.030, low)
        back_part = 0.010 * np.exp(-(p[..., 0] / 0.010) ** 2) * (p[..., 2] < -0.06) * (p[..., 1] > -0.18) * (p[..., 1] < 0.20)   # 뒤 가르마 골
        d = smin(cap + back_part, fringe(p, hem, 70), 0.012)                                  # 앞머리 — 이마를 감싸는 한 장 + 끝이 갈라진 다발(v9)
        # 귀 아래 — 귀 높이 아래로 귀 앞뒤 폭(각도 68~108°)은 숱이 없다(11-07 사용자: *"상식상 귀 밑에 머리카락이 남아 있을 수 없겠지"* — 귀 둘레를 파면 그 밑 숱이 떠서 남았다) · 숱에만 — 더듬이까지 자르면 귀 앞에 곧은 세로 경계가 섰다(v21)
        ear_y = (EY + dw.NY) * 0.5
        below = np.maximum(np.maximum(82 - ang, ang - 110) * np.pi / 180 * 0.30, p[..., 1] - ear_y)   # 귀 앞 경계(82°)부터 뒤로만
        d = smax(d, -below, 0.012)
        # **볼에 붙지 않는다**(11-07 사용자: *"아예 안 붙을 수는 없어?"* → *"볼에 붙은걸 때라고 했지 머리카락을 볼에서 멀리 치우라는 말은 안했어"*) —
        # 눈 높이 아래 앞 · 옆(88° 안)은 얼굴에서 8mm 안의 숱만 비운다(자리는 그대로 · 2.2cm는 멀리 치운 것이었다 · v28).
        near_face = np.maximum(np.maximum(el.nhead(p) - 0.008, p[..., 1] - (EY - 0.01)), (ang - 88) * np.pi / 180 * 0.30)
        d = smax(d, -near_face, 0.008)
        d = smin(d, side_locks(p, vertical), 0.03)                                           # 더듬이(v17 — 얼굴 옆을 덮는 한 장은 뺐다: 사용자 *"얼굴 옆을 채울 필요는 없지"*)
        tie = el.on_cap(0.22, 0.29, -0.17, 0.02)                                             # 정수리 뒤 양옆 — 높고 바깥(정면에서 머리 위 양옆으로 솟아 보이게 · v6)
        R = [r * 1.05 for r in HU.TAIL_R]; tails = HU.NONE(p)
        for dx, dz, s_ in ((0.00, 0.00, 1.0), (0.03, -0.03, 0.94), (-0.02, 0.02, 0.97), (0.04, 0.02, 0.90), (-0.01, -0.05, 1.03), (0.02, -0.07, 0.96), (0.05, -0.01, 0.86)):
            pts = HU.bez(tie, tie + V(0.13, 0.12, -0.02), tie + V(0.30 + dx, -0.06, -0.10 + dz), tie + V(0.24 + dx * 1.6, -0.98 * s_, -0.12 + dz), 8)   # 위 · 바깥으로 튀었다가 머리와 떨어져 내려온다(v7은 머리 옆에 붙어 귀마개 같았다)
            tails = smin(tails, HU.lock_r(m, pts, R), 0.03)
        d = smin(d, tails + dw.strands(p, 0.002, 55), 0.03)
        band = HU.band(m, tie + V(-0.006, 0.006, 0.010), tie + V(0.020, 0.014, -0.012), 0.040)   # 끈 — 잘록한 목보다 살짝만 굵다
        return [(cut(p, d, 0.012), 'hair'), (band, 'hair')]   # 귀는 숱 사이로 나온다(테두리만 1.2cm)(둘레를 넓게 파지 않는다 — v20~22는 3.5cm 팠다)
    # ── 11-07 두 번째 묶음(사용자: 7 · 12 · 14 · 16 고치기 · 엘프 M 귀 밑 · 남자 짧은 머리 · 사진 넷 느낌 · M 보통 기장) ──
    ear_y = (EY + dw.NY) * 0.5
    def no_under_ear(p, d, a0=80, a1=100):   # 귀 밑 — 귀 앞뒤 폭에서 귀 높이 아래 · 머리 가까이(6cm 안)의 숱은 없다(뒷머리 기둥은 그대로)
        ang = dw.angle(p)
        r = np.maximum(np.maximum(np.maximum(a0 - ang, ang - a1) * np.pi / 180 * 0.30, p[..., 1] - ear_y), el.nhead(p) - 0.06)
        return smax(d, -r, 0.01)
    def off_cheek(p, d):                       # 눈 높이 아래 앞 · 옆은 얼굴에서 8mm 안 숱을 비운다(볼에 붙지 않게 — 쌍꼬리 v29와 같은 규칙)
        ang = dw.angle(p)
        r = np.maximum(np.maximum(el.nhead(p) - 0.008, p[..., 1] - (EY - 0.01)), (ang - 88) * np.pi / 180 * 0.30)
        return smax(d, -r, 0.008)
    def swept_cap(p, th0=0.0):                 # 올백 숱 — 오크 v30(`orc_parts.h_mane(flat=True)`)의 평평한 이마선 · 정수리 앞이 둥글게 들린다 · 빗질 골은 앞뒤로
        ang = dw.angle(p)
        low = np.interp(ang, [0, 40, 70, 100, 140, 180], [EY + 0.25, EY + 0.25, EY + 0.14, EY + 0.06, -0.20, -0.30])
        lift = th0 + 0.026 + 0.034 * np.clip(1 - (p[..., 0] / 0.34) ** 2, 0, 1) * dw.ease((p[..., 2] + 0.05) / 0.30)
        return el.scalp(p, lift, low) + 0.003 * np.sin(p[..., 0] * 90) * dw.ease((p[..., 1] - low - 0.03) / 0.05)
    def on_dir(dirn, off):                     # 머리 겉면(+ off) 위의 점 — 방향 그대로 밀어 맞춘다(`el.on_cap`은 y를 높이로 받는다 — 방향을 넘기면 허공에 떴다 · v1)
        lo, hi = 0.05, 2.0
        for _ in range(30):
            t = (lo + hi) * 0.5
            if el.nhead(np.array([dirn * t], np.float32))[0] < off: lo = t
            else: hi = t
        return dirn * lo
    def natural_cap(p, crown=0.010, nape=-0.20):   # **빗어 넘긴 숱 한 벌**(11-07 사용자: 맨번 v5 *"다른 머리들도 저렇게 자연스럽게"*) — 둥근 남자 이마선(관자놀이 쪽이 살짝 들어간다) ·
        # 이마선 바로 위는 얇게 시작해 머리에 붙고 정수리 뒤로 조금 부푼다 · 빗질 골은 앞뒤로. 올백(가로로 평평한 이마선 · 앞이 들린 숱)과 드워프 이마선은 직각으로 보였다.
        ang = dw.angle(p)
        low = HU.male_line(None, ang, EY + 0.21, EY + 0.195, EY + 0.08, EY + 0.01, nape)
        th = (0.016 + crown * dw.ease((p[..., 1] - (EY + 0.25)) / 0.12)) * (0.2 + 0.8 * dw.ease((p[..., 1] - low) / 0.06))
        return el.scalp(p, th, low) + 0.0025 * np.sin(p[..., 0] * 90) * dw.ease((p[..., 1] - low - 0.02) / 0.05)
    def short_sides(p, top_y, top_th, front_k=0.4):   # 옆 · 뒤는 바짝(4mm) · 위만 숱(군인 · 올린 머리의 바탕)
        ang = dw.angle(p)
        low = HU.male_line(None, ang, EY + 0.20, EY + 0.205, EY + 0.08, EY + 0.02, -0.15)
        top = dw.ease((p[..., 1] - top_y) / 0.07)
        th = 0.004 + top_th * top * (1 - front_k + front_k * dw.ease((p[..., 2] + 0.05) / 0.25))
        th = th * (0.25 + 0.75 * dw.ease((p[..., 1] - low) / 0.05))                           # 이마선 바로 위는 얇게 — 두꺼우면 바가지 챙이었다(v1)
        return el.scalp(p, th, low), top
    def buzz_split(p, cap, top_y):             # 위 숱(머리색)과 바짝 깎은 옆 · 뒤(`.Buzz` — 살과 머리색 사이 · 여성 오크 반삭과 같은 칠)로 가른다 — 한 색이면 옆이 짧아 보이지 않았다(v3)
        top = smax(cap, (top_y - 0.01) - p[..., 1], 0.012)
        side = smax(cap, p[..., 1] - (top_y + 0.01), 0.012)
        return top, side

    def h_long12(p):        # 12 뒤로 넘긴 긴 머리(다시) — 앞은 올백 숱(드워프 이마선은 모자 같았다) + 엘프 뒷머리 기둥(어깨 아래)
        d = smin(natural_cap(p, 0.016, -0.24), el.back_fall(p, -0.62), 0.04)                # 앞은 빗어 넘긴 숱 한 벌(올백 숱은 직각이었다)
        return [(cut(p, no_under_ear(p, d)) + dw.strands(p, 0.0015, 55), 'hair')]
    def h_mane14(p):        # 14 갈기(다시) — 앞은 엘프 생머리의 가르마 · 관자놀이 꺾임(드워프 이마선은 모자 같았다) · 숱 많고 어깨께에서 퍼진다
        ang = dw.angle(p)
        cap = el.scalp(p, 0.040 + 0.016 * dw.ease((p[..., 1] - EY) / 0.3), np.maximum(el.curtain_line(ang), EY + 0.02 * (ang < 90))) + el.part_line(p)
        d = smin(cap, flared_fall(p, -0.60, 0.10), 0.05)
        return [(cut(p, off_cheek(p, no_under_ear(p, d))) + dw.strands(p, 0.002, 50), 'hair')]
    def h_longbraids7(p):   # 7 긴 땋은 머리(다시) — 가운데 가르마로 덮고 귀 뒤에서 땋은 두 가닥이 어깨 앞으로 · 뒤는 목덜미까지 · 장신구 없다
        ang = dw.angle(p); m = mirror(p)
        cap = el.scalp(p, 0.030, el.front_line(ang)) + el.part_line(p)
        pts = [el.on_cap(0.24, EY - 0.02, -0.14, 0.01), V(0.27, -0.24, -0.08), V(0.27, -0.38, 0.03), V(0.25, -0.54, 0.10), V(0.23, -0.72, 0.12)]
        d = smin(cap, el.plait(m, pts, 0.046, 0.032, 13), 0.03)
        d = smin(d, HU.tail(m, pts[-1], -0.02, 0.0, 0.11, 0.40), 0.01)
        return [(cut(p, no_under_ear(p, d)) + dw.strands(p, 0.002, 80), 'hair')]
    def h_twinbraids16(p):  # 16 쌍갈래(다시) — 귀 뒤에서 땋아 어깨 위로 얹혀 가슴께로(가슴 앞으로 띄워 늘어뜨리면 떴다)
        ang = dw.angle(p); m = mirror(p)
        cap = natural_cap(p, 0.012)                                                          # 둥근 이마선 · 얇게 시작
        pts = [el.on_cap(0.23, EY + 0.02, -0.16, 0.01), V(0.27, -0.16, -0.10), V(0.28, -0.32, -0.02), V(0.26, -0.46, 0.06), V(0.24, -0.62, 0.09)]
        d = smin(cap, el.plait(m, pts, 0.042, 0.030, 12), 0.03)
        d = smin(d, HU.tail(m, pts[-1], -0.02, 0.0, 0.10, 0.35), 0.01)
        return [(cut(p, no_under_ear(p, d)), 'hair')]
    def h_military(p):      # 군인 머리(사용자 사진 `want_military_short` · 공통 특징만) — 옆 · 뒤 바짝 · 위는 짧게 세운 결
        cap, top = short_sides(p, EY + 0.13, 0.030, 0.6)
        # 위는 짧게 세운 결 — 숱 겉면의 잔 들쭉날쭉으로만(머리 위로 따로 세운 다발은 어색했다 · 사용자 v2)
        tuft = 0.006 * np.sin(p[..., 0] * 55 + 1.3 * np.sin(p[..., 2] * 40)) * np.sin(p[..., 2] * 48 + 0.7) * top
        d = cap + dw.strands(p, 0.003, 120) * top - np.abs(tuft)
        d, side = buzz_split(p, d, EY + 0.13)
        return [(cut(p, d), 'hair'), (cut(p, side), 'buzz')]
    def h_pomp(p):          # 올려 넘긴 머리(사진 `want_swept_volume`) — 옆은 짧고 위는 숱 많게 앞에서 들려 뒤로 · 왼쪽 가르마
        # 앞 정수리 숱만 두껍게 — 앞에서 들려 뒤로 갈수록 잦아든다(머리 위에 따로 얹은 덩어리는 어색했다 · 사용자 v2)
        cap, top = short_sides(p, EY + 0.12, 0.030, 0.6)
        front = dw.ease((p[..., 2] + 0.02) / 0.22) * dw.ease((p[..., 1] - (EY + 0.17)) / 0.10) * np.clip(1 - (p[..., 0] / 0.26) ** 2, 0, 1)
        d = smin(cap, el.scalp(p, 0.030 + 0.034 * front, HU.male_line(None, dw.angle(p), EY + 0.20, EY + 0.205, EY + 0.08, EY + 0.02, -0.15)) * (front > 0.02) + 9 * (front <= 0.02), 0.02)
        part = 0.010 * np.exp(-((p[..., 0] + 0.10) / 0.010) ** 2) * (p[..., 1] > EY + 0.12) * (p[..., 2] > -0.10)
        d, side = buzz_split(p, d + part + 0.003 * np.sin(p[..., 0] * 80) * top, EY + 0.12)
        return [(cut(p, d), 'hair'), (cut(p, side), 'buzz')]
    def h_manbun(p):        # 묶어 올린 남자 머리(사진 `want_manbun`) — 올백으로 빗어 정수리 뒤에 둥글게 틀어 묶었다 · 끈 자리 잘록
        d = natural_cap(p)                                                                   # 둥근 이마선 · 얇게 시작하는 숱(맨번 v5 — 공용으로 뺐다)
        u = np.array([0.0, 0.62, -0.78], np.float32); u /= np.linalg.norm(u)
        c = on_dir(u, 0.045)                                                                  # 정수리 뒤 겉면(`el.on_cap`에 방향을 넘기면 허공에 떠 안 보였다 · v2)
        bun = smin(HU.sph(p, c + V(0, 0.045, -0.035), 0.085), HU.sph(p, c + V(0, 0.095, -0.065), 0.066), 0.035)   # 숱 밖으로 둥글게 — 작으면 올백 숱에 묻혀 혹 같았다(v3)
        bun = bun + 0.003 * np.sin(np.arctan2(p[..., 0], p[..., 2] - c[2]) * 8 + p[..., 1] * 50)
        tie = HU.band(p, c + V(0, -0.012, 0.010), c + V(0, 0.012, -0.004), 0.046)
        return [(cut(p, smin(d, bun, 0.02)), 'hair'), (tie, 'hair')]
    def h_shoulder(p):      # 어깨 기장(다시) — 엘프 생머리 방식 그대로 길이만 어깨께로(사용자: *"투구 쓴 거 같아"* — 숱 껍데기가 귀 아래까지 한 덩어리로 내려와 끝만 벌어졌다)
        # 가르마에서 흘러내리는 숱 · 뒤는 겉면을 따라 떨어지는 얇은 가닥 기둥(`back_fall`) · 얼굴 옆은 얇은 앞 가닥(`front_locks` — 사람용 얇은 띠)
        ang = dw.angle(p)
        cap = el.scalp(p, 0.030, el.curtain_line(ang)) + el.part_line(p)
        d = smin(cap, el.back_fall(p, -0.36), 0.04)
        d = smin(d, el.front_locks(p, -0.36), 0.03)
        return [(cut(p, off_cheek(p, no_under_ear(p, d))) + dw.strands(p, 0.0015, 55), 'hair')]
    def h_curtain(p):       # 가운데 가르마 커튼 머리(사진 `want_center_curtain`) — 가르마에서 이마 양쪽으로 눈께까지 흘러내리고 귀를 덮어 목덜미까지
        ang = dw.angle(p)
        hem = lambda a: np.interp(a, [0, 6, 16, 30, 46, 60, 72], [EY + 0.22, EY + 0.15, EY + 0.06, EY + 0.02, EY - 0.02, EY - 0.06, EY - 0.10])
        low = np.where(ang < 90, np.maximum(el.curtain_line(ang), hem(np.minimum(ang, 72))), np.interp(ang, [90, 120, 180], [dw.NY - 0.02, -0.20, -0.24]))
        cap = el.scalp(p, 0.032, low) + el.part_line(p)
        d = smin(cap, el.bangs(p, hem, 72, 0.006), 0.015)
        return [(off_cheek(p, d) + dw.strands(p, 0.0015, 55), 'hair')]
    # ── 짧은 머리 더(11-07 사용자: *"남자 머리 더 넣긴 해야지 짧은 머리가 많이 들어가면 좋겠어"*) — 남녀 구분 없이 쓴다 ──
    def part_groove(p, x0, depth=0.010):       # 가르마 골(정수리 앞 · x0 자리)
        return depth * np.exp(-((p[..., 0] - x0) / 0.010) ** 2) * (p[..., 1] > EY + 0.12) * (p[..., 2] > -0.10)
    def h_sidepart(p):      # 짧게 다듬은 가르마 머리 — 왼쪽 가르마 · 넘긴 쪽(오른쪽)만 숱이 조금 많다
        side = dw.ease((p[..., 0] + 0.06) / 0.25) * dw.ease((p[..., 1] - (EY + 0.15)) / 0.10)
        d = natural_cap(p, 0.010) - 0.012 * side
        return [(cut(p, d + part_groove(p, -0.09)), 'hair')]
    def h_twoblock(p):      # 투블럭 — 옆 · 뒤는 바짝(반삭) · 위는 길게 남겨 오른쪽으로 넘겨 덮는다
        cap, top = short_sides(p, EY + 0.14, 0.030, 0.4)
        over = dw.ease((p[..., 1] - (EY + 0.14)) / 0.08) * (0.6 + 0.4 * dw.ease((p[..., 0] + 0.05) / 0.25))
        d = cap - 0.010 * over + part_groove(p, -0.10)
        d, side = buzz_split(p, d, EY + 0.14)
        return [(cut(p, d), 'hair'), (cut(p, side), 'buzz')]
    SHORT_FRINGE = [(0, 2, EY + 0.150, 0.048), (12, 16, EY + 0.165, 0.046), (-12, -16, EY + 0.165, 0.046), (24, 32, EY + 0.150, 0.044),
                    (-24, -32, EY + 0.150, 0.044), (36, 46, EY + 0.120, 0.040), (-36, -46, EY + 0.120, 0.040)]
    def h_shortfringe(p):   # 짧은 앞머리 머리 — 위는 짧고 이마에 짧은 다발 앞머리(다발 앞머리 기법 · 덮는 앞머리처럼 숱 두께만큼 띄운다)
        d = natural_cap(p, 0.008)
        for az0, az1, y1, r0 in SHORT_FRINGE:
            d = smin(d, clump(p, az0, az1, TOP, y1, r0), 0.006)
        return [(cut(p, d), 'hair')]
    MESSY = [(0, -6, EY + 0.100, 0.050), (14, 22, EY + 0.130, 0.048), (-14, -24, EY + 0.090, 0.048), (28, 40, EY + 0.080, 0.046),
             (-28, -38, EY + 0.120, 0.046), (44, 58, EY + 0.020, 0.044), (-44, -56, EY + 0.050, 0.044), (60, 72, EY - 0.030, 0.040), (-60, -72, EY - 0.010, 0.040)]
    def h_messy(p):         # 덥수룩한 짧은 머리 — 길이가 들쭉날쭉한 다발이 이마와 귀 위를 살짝 덮는다 · 뒤는 목덜미까지
        ang = dw.angle(p)
        d = el.scalp(p, 0.034, HU.male_line(None, ang, EY + 0.20, EY + 0.19, EY + 0.06, EY - 0.01, -0.24))
        for az0, az1, y1, r0 in MESSY:
            d = smin(d, clump(p, az0, az1, TOP, y1, r0), 0.006)
        return [(cut(p, off_cheek(p, d)), 'hair')]
    def h_spiky(p):         # 뾰족뾰족한 머리(곱슬은 덥수룩한 머리와 겹쳐 뺐다 · 사용자 *"뾰족뾰족하게 만들어보던지"*) — 정수리에서 사방으로 뻗는 다발이
        # 숱에서 이어져 나와 끝이 뾰족하게 머리 밖으로 들린다(머리 위에 따로 세운 다발은 어색했다 — 군인 머리 v1 · 다발 앞머리 기법으로 숱에 녹인다)
        # v3 — v1의 갈라지는 끝(사용자: *"갈라지는게 더 좋아보였는데"*) + 정수리 한가운데를 채우는 다발(v2는 둘레에서만 시작해 가운데가 갓파처럼 비었다) ·
        # 굵기는 v1보다 조금 넓게(가늘면 실 같았다)
        # v6 + 정수리 부피(사용자 그림 `_refs/2026-11-07_human/want_spiky_smooth_top.png` — v6 위에 정수리 선 하나만 그었다 · v7은 전체를 다시 빚어 틀렸다)
        ang = dw.angle(p)
        low = HU.male_line(None, ang, EY + 0.15, EY + 0.155, EY + 0.07, EY + 0.00, -0.20)
        th = (0.020 + 0.014 * dw.ease((p[..., 1] - (EY + 0.12)) / 0.20) + 0.040 * dw.ease((p[..., 1] - (EY + 0.24)) / 0.14)) * (0.3 + 0.7 * dw.ease((p[..., 1] - low) / 0.05))
        d = el.scalp(p, th, low)
        for az, y1, lift in ((0, EY + 0.10, 0.040), (12, EY + 0.12, 0.040), (-12, EY + 0.12, 0.040), (38, EY + 0.11, 0.045), (-38, EY + 0.11, 0.045),
                             (25, EY + 0.15, 0.045), (-25, EY + 0.15, 0.045), (50, EY + 0.12, 0.050), (-50, EY + 0.12, 0.050),
                             (75, EY + 0.10, 0.055), (-75, EY + 0.10, 0.055), (100, EY + 0.06, 0.055), (-100, EY + 0.06, 0.055), (130, EY + 0.02, 0.050),
                             (-130, EY + 0.02, 0.050), (155, EY - 0.02, 0.045), (-155, EY - 0.02, 0.045), (180, EY - 0.04, 0.040),
                             (12, EY + 0.25, 0.050), (-12, EY + 0.25, 0.050), (60, EY + 0.24, 0.055), (-60, EY + 0.24, 0.055), (120, EY + 0.20, 0.055), (-120, EY + 0.20, 0.055),
                             (160, EY + 0.18, 0.050), (-160, EY + 0.18, 0.050), (35, EY + 0.30, 0.045), (-35, EY + 0.30, 0.045), (90, EY + 0.28, 0.050), (-90, EY + 0.28, 0.050)):
            pts, rad = [], []
            y0 = min(EY + 0.33, y1 + 0.16)
            for t in np.linspace(0, 1, 12):
                a_ = np.radians(az); y = y0 + (y1 - y0) * t
                pts.append(el.on_cap(np.sin(a_) * 0.3, y, np.cos(a_) * 0.3, 0.012 + lift * 0.45 * t ** 1.6))
                rad.append(max(0.058 * (1 - t ** 1.8) ** 0.8, 0.003))
            d = smin(d, HU.lock_r(p, pts, rad, 0.012), 0.024)
        return [(cut(p, d), 'hair')]
    # ── 짧은 머리 셋째 묶음(11-07 사용자: *"짧은 머리 더 만들어봐"*) ──
    DANDY = [(0, 0, EY + 0.075, 0.052), (12, 14, EY + 0.085, 0.052), (-12, -14, EY + 0.085, 0.052), (25, 28, EY + 0.075, 0.050),
             (-25, -28, EY + 0.075, 0.050), (38, 44, EY + 0.050, 0.046), (-38, -44, EY + 0.050, 0.046), (52, 60, EY + 0.020, 0.042), (-52, -60, EY + 0.020, 0.042)]
    def h_dandy(p):         # 댄디 — 둥글게 덮은 숱 · 앞머리가 눈썹 위까지 곧게 내려온다 · 귀 위를 살짝 덮고 목덜미는 짧다
        ang = dw.angle(p)
        d = el.scalp(p, 0.030, HU.male_line(None, ang, EY + 0.20, EY + 0.19, EY + 0.05, EY - 0.01, -0.20))
        for az0, az1, y1, r0 in DANDY:
            d = smin(d, clump(p, az0, az1, TOP, y1, r0), 0.006)
        return [(cut(p, off_cheek(p, d)), 'hair')]
    def h_comma(p):         # 쉼표 머리 — 가르마 머리 + 앞머리 한 다발이 이마 한쪽으로 내려와 끝이 안으로 말린다
        d = natural_cap(p, 0.012) - 0.010 * dw.ease((p[..., 0] + 0.06) / 0.25) * dw.ease((p[..., 1] - (EY + 0.15)) / 0.10)
        d = d + part_groove(p, -0.09)
        for az0, az1, y1, r0 in ((-4, 14, EY + 0.090, 0.050), (8, 24, EY + 0.070, 0.050), (20, 36, EY + 0.080, 0.046)):
            d = smin(d, clump(p, az0, az1, TOP, y1, r0), 0.008)
        a = np.radians(16)                                                                    # 말린 끝 — 다발 끝에서 안(가운데)으로 감겨 올라간다
        c0 = el.on_cap(np.sin(a) * 0.3, EY + 0.075, np.cos(a) * 0.3, 0.030)
        curl = HU.bez(c0, c0 + V(-0.012, -0.022, 0.010), c0 + V(-0.040, -0.020, 0.012), c0 + V(-0.038, 0.004, 0.006), 6)
        d = smin(d, HU.lock_r(p, curl, [0.020, 0.018, 0.016, 0.013, 0.010, 0.007, 0.004]), 0.010)
        return [(cut(p, d), 'hair')]
    def h_dreads(p):        # 언더컷 + 레게 가닥(사용자 그림 `_refs/2026-11-07_human/want_undercut_dreads.png`) — 옆 · 뒤는 높이까지 바짝 · 위는 판처럼 남기고
        # **뒤쪽 윗머리 아래 경계에서만** 가는 꼰 가닥이 나와 반삭한 뒤통수를 따라 목덜미께까지 내려온다
        ang = dw.angle(p)
        low = np.interp(ang, [0, 25, 45, 70, 110, 180], [EY + 0.20, EY + 0.20, EY + 0.20, EY + 0.21, EY + 0.22, EY + 0.22])
        front = dw.ease((p[..., 2] + 0.02) / 0.22) * np.clip(1 - (p[..., 0] / 0.22) ** 2, 0, 1)
        ease_w = np.interp(ang, [0, 40, 70], [0.015, 0.015, 0.06])
        th = (0.016 + 0.020 * dw.ease((p[..., 1] - (EY + 0.24)) / 0.10) + 0.012 * front * dw.ease((p[..., 1] - (EY + 0.26)) / 0.06)) * (0.3 + 0.7 * dw.ease((p[..., 1] - low) / ease_w))
        top = el.scalp(p, th, low) + 0.003 * np.sin(p[..., 0] * 80 + 0.8 * np.sin(p[..., 2] * 12)) * dw.ease((p[..., 1] - low - 0.02) / 0.05)
        side = el.scalp(p, 0.004, HU.male_line(None, ang, EY + 0.20, EY + 0.205, EY + 0.08, EY + 0.02, -0.15))
        side = smax(side, p[..., 1] - (low + 0.03), 0.006)
        side = smax(side, 50 - ang, 2.0)
        n = 10
        bead = [0.015 if i % 2 == 0 else 0.012 for i in range(n + 1)]
        ropes = HU.NONE(p)
        for k, az in enumerate((180, 171, -171, 162, -162, 153, -153, 144, -144)):   # 가닥 사이를 붙인다(사용자: *"간격이 좀 붙어 있어야"* — v3은 귀 뒤까지 벌려 빗살 같았다)          # 뒤쪽 경계에만 — 귀 위(옆)에서는 안 나온다
            a_ = np.radians(az); sx, sz = np.sin(a_) * 0.3, np.cos(a_) * 0.3
            yb = EY + 0.22 - 0.005
            L = -0.30 - 0.06 * ((k * 5 + 1) % 4) / 3                                            # 목덜미께 · 길이 들쭉날쭉
            # v2 — 고르지 않게(그림은 빗살이 아니라 흔들리며 몇 가닥씩 모인다) · 아래로 갈수록 가운데 쪽으로 휜다 · 가늘게
            jit = (0.020, -0.015, 0.012, -0.022, 0.016, -0.010, 0.024, -0.018, 0.008)[k]
            pts = [el.on_cap(sx, yb, sz, 0.010), el.on_cap(sx + jit * 0.5, yb - 0.10, sz, 0.014), el.on_cap(sx * 0.90 + jit, -0.08, sz, 0.016),
                   el.on_cap(sx * 0.84 - jit * 0.6, -0.20, sz, 0.016)]
            pts.append(pts[-1] + V(-np.sign(sx) * 0.015 + jit * 0.5, L + 0.20, -0.02))
            pts = HU.bez(pts[0], pts[1], pts[3], pts[4], n)
            rad = [(0.015 if i % 2 == 0 else 0.012) for i in range(len(pts) - 2)] + [0.010, 0.006]
            ropes = np.minimum(ropes, HU.lock_r(p, pts, rad, 0.004))
        return [(cut(p, smin(top, ropes, 0.010)), 'hair'), (cut(p, side), 'buzz')]
    def h_fauxhawk(p):      # 가운데 세운 머리 — 옆은 짧고 가운데 줄만 숱이 두껍게 앞에서 뒤로 솟는다 · 위 끝은 톱니처럼 뾰족
        # v3 — 가운데 줄의 숱 두께를 키운다(v1 숱에서 빼기 · v2 따로 세운 다발은 정수리 점 하나로 뭉쳤다)
        ang = dw.angle(p)
        low = HU.male_line(None, ang, EY + 0.20, EY + 0.205, EY + 0.08, EY + 0.02, -0.15)
        mid = np.exp(-(p[..., 0] / 0.075) ** 2) * dw.ease((p[..., 1] - (EY + 0.12)) / 0.10) * dw.ease((p[..., 2] + 0.24) / 0.20)
        teeth = 0.5 + 0.5 * np.abs(np.sin(p[..., 2] * 32))                                    # 앞뒤로 톱니
        ridge = el.scalp(p, 0.010 + 0.075 * mid * teeth, low)
        cap, top = short_sides(p, EY + 0.13, 0.008, 0.3)
        d = smin(cap, ridge, 0.01)
        d, side = buzz_split(p, d, EY + 0.13)
        return [(cut(p, d), 'hair'), (cut(p, side), 'buzz')]
    PIXIE = [(-24, -6, EY + 0.110, 0.046), (-12, 10, EY + 0.080, 0.050), (0, 24, EY + 0.050, 0.052), (12, 36, EY + 0.030, 0.050), (26, 48, EY + 0.020, 0.046),
             (-38, -48, EY + 0.070, 0.040), (60, 72, EY - 0.040, 0.036), (-60, -70, EY - 0.030, 0.036)]
    def h_pixie(p):         # 숏컷 — 짧게 붙은 숱 · 옆으로 길게 쓸린 앞머리 · 귀 앞에 짧은 가닥 · 목덜미는 가늘게 깎아 짧다
        ang = dw.angle(p)
        d = el.scalp(p, 0.022 + 0.010 * dw.ease((p[..., 1] - (EY + 0.20)) / 0.12), HU.male_line(None, ang, EY + 0.20, EY + 0.19, EY + 0.06, EY - 0.04, -0.22))
        for az0, az1, y1, r0 in PIXIE:
            d = smin(d, clump(p, az0, az1, TOP, y1, r0), 0.006)
        return [(cut(p, off_cheek(p, d)), 'hair')]
    # ── 넷째 묶음(11-07 사용자) ──
    def h_crop(p):          # 짧게 깎은 머리 — 바짝 민 머리보다 조금 길고 머리색 그대로(사용자: *"바짝 민머리보다 좀 더 색이 있는 머리"*)
        ang = dw.angle(p)
        d = el.scalp(p, 0.009, HU.male_line(None, ang, EY + 0.20, EY + 0.19, EY + 0.08, EY + 0.02, -0.17))
        return [(cut(p, d + 0.0012 * np.sin(p[..., 0] * 150) * np.sin(p[..., 2] * 150)), 'hair')]
    def h_baldwings(p):     # 벗겨진 머리 · 옆 뻗친 — 정수리는 맨살 · 귀 위 옆머리가 위 · 바깥 · 뒤로 크게 뻗친 뾰족한 다발 셋씩 · 뒤는 띠
        ang = dw.angle(p)
        band = el.scalp(p, 0.016, HU.male_line(None, ang, EY + 0.20, EY + 0.19, EY + 0.06, EY + 0.00, -0.17))
        band = smax(band, p[..., 1] - (EY + 0.13), 0.01)                                     # 정수리는 비운다(맨살)
        band = smax(band, 62 - ang, 3.0)                                                     # 이마 · 관자놀이 앞은 비운다
        m = mirror(p); d = band
        # v2 — 다발을 크고 많게 · 위로 크게 솟아 정수리 높이까지(v1은 작은 뿔 셋이 옆으로 튀었다) · 뿌리끼리 녹여 한 덩어리 날개
        for k, (az, y0, tip) in enumerate(((84, EY + 0.08, V(0.10, 0.22, 0.00)), (98, EY + 0.11, V(0.13, 0.20, -0.05)), (114, EY + 0.11, V(0.15, 0.15, -0.10)),
                                           (130, EY + 0.09, V(0.14, 0.10, -0.15)), (146, EY + 0.07, V(0.10, 0.05, -0.18)))):
            a_ = np.radians(az); root = el.on_cap(np.sin(a_) * 0.3, y0, np.cos(a_) * 0.3, 0.004)
            pts = HU.bez(root, root + V(0.05, 0.01, 0.0), root + tip * 0.65 + V(0.01, -0.03, 0), root + tip, 8)
            d = smin(d, HU.lock_r(m, pts, [0.068, 0.066, 0.060, 0.052, 0.042, 0.030, 0.018, 0.008, 0.002]), 0.03)
        return [(cut(p, d + dw.strands(p, 0.0015, 80)), 'hair')]
    def h_slickmullet(p):   # 넘긴 장발 — 빗어 넘긴 젖은 숱 · 옆은 붙여 넘기고 관자놀이에 민 줄 둘 · 뒷머리는 목덜미 아래까지 늘어져 끝이 바깥으로 뻗친다
        # (사용자 사진 `_refs/2026-11-07_human/want_slick_mullet_*.png` · 공통 특징만)
        # 뒷머리 기둥은 귀 뒤(125°)부터만 — 엘프 기둥(100°부터)은 정면에서 얼굴 옆으로 늘어져 단발처럼 보였다(v1)
        d = natural_cap(p, 0.014, -0.24)
        m = mirror(p)
        for az in (125, 137, 149, 161, 173, 180):
            dx, dz = np.sin(np.radians(az)), np.cos(np.radians(az)); nrm = V(dx, 0, dz)
            side = p if az >= 179.9 else m
            c = el.on_cap(dx * 0.3, -0.02, dz * 0.3, 0.014)
            y_end = -0.46 + 0.10 * ((180 - az) / 55) ** 2
            pts = [el.on_cap(dx * 0.3, 0.12, dz * 0.3, -0.004), V(c[0], -0.02, c[2]), V(c[0], -0.30, c[2]) + nrm * 0.006, V(c[0], y_end, c[2]) + nrm * 0.014]
            d = smin(d, el.nlock(side, pts, 0.050, 0.034, nrm), 0.02)
        for az, y1 in ((128, -0.38), (144, -0.42), (160, -0.45), (174, -0.46)):   # 끝이 바깥으로 뻗친 가닥
            for sg in ((1, -1) if az < 180 else (1,)):
                a_ = np.radians(az * sg); n_ = V(np.sin(a_), 0, np.cos(a_))
                c = el.on_cap(np.sin(a_) * 0.3, -0.10, np.cos(a_) * 0.3, 0.020)
                pts = HU.bez(V(c[0], -0.20, c[2]), V(c[0], y1 + 0.06, c[2]) + n_ * 0.01, V(c[0], y1, c[2]) + n_ * 0.03, V(c[0], y1 + 0.03, c[2]) + n_ * 0.07, 6)
                d = smin(d, HU.lock_r(p, pts, [0.030, 0.028, 0.026, 0.022, 0.016, 0.010, 0.004]), 0.012)
        for dy in (0.00, 0.035):                                                              # 관자놀이 민 줄 둘 — 숱을 파내 살이 비친다(뒤로 비스듬히)
            a0 = el.on_cap(0.28, EY + 0.10 + dy, 0.10, 0.022); a1 = el.on_cap(0.27, EY + 0.16 + dy, -0.10, 0.022)   # 숱 겉면 높이에서 깊게 파낸다(얕으면 점으로만 비쳤다)
            d = smax(d, -HU.seg(m, a0, a1, 0.014, 0.014), 0.002)
        return [(cut(p, no_under_ear(p, d)) + dw.strands(p, 0.0015, 60), 'hair')]
    def h_buzzcut(p):       # 바짝 민 머리 — 전체가 반삭(살과 머리색 사이)
        ang = dw.angle(p)
        d = el.scalp(p, 0.004, HU.male_line(None, ang, EY + 0.20, EY + 0.19, EY + 0.08, EY + 0.02, -0.16))
        return [(cut(p, d), 'buzz')]
    def h_lowtail(p):       # 낮게 묶은 꽁지 — 빗어 넘긴 숱 · 목덜미에서 묶어 꽁지 규칙대로 내린다
        d = natural_cap(p, 0.010, -0.24)
        u = np.array([0.0, -0.50, -0.87], np.float32); u /= np.linalg.norm(u)
        tie = on_dir(u, 0.02)
        # 목덜미에서 등을 따라 꺾임 없이 흘러내린다(묶은 자리 바로 밑이 불룩했다 꺾이면 관절 같았다 — 사용자 그림 `bad_lowtail_joint.png`) ·
        # 꽁지 규칙은 지킨다 — 끈에서 가늘게 나와 완만히 부풀고 끝은 갈라진다
        R = [0.022, 0.030, 0.036, 0.038, 0.036, 0.031, 0.024, 0.016, 0.008]; tl = HU.NONE(p)
        for dx, dz, s_ in ((-0.03, 0.00, 0.95), (-0.012, -0.02, 1.0), (0.0, 0.01, 1.04), (0.012, -0.02, 1.0), (0.03, 0.0, 0.94), (0.0, -0.03, 0.98)):
            pts = HU.bez(tie, tie + V(dx * 0.3, -0.10, -0.030), tie + V(dx * 0.8, -0.30, -0.035 + dz), tie + V(dx * 1.6, -0.60 * s_, -0.005 + dz), 8)
            tl = smin(tl, HU.lock_r(p, pts, R), 0.02)
        d = smin(d, tl + dw.strands(p, 0.0015, 60), 0.02)
        knot = HU.band(p, tie + V(0, 0.012, 0.004), tie + V(0, -0.016, -0.006), 0.032)
        return [(cut(p, d), 'hair'), (knot, 'hair')]
    def h_halfup(p):        # 반묶음 — 윗머리만 뒤통수에서 작게 묶고 나머지는 목덜미 아래까지(엘프 뒷머리 기둥 · 짧게)
        d = smin(natural_cap(p, 0.010, -0.26), el.back_fall(p, -0.36), 0.04)
        u = np.array([0.0, 0.40, -0.92], np.float32); u /= np.linalg.norm(u)
        c = on_dir(u, 0.045)                                                                 # 뒤통수(사용자: *"반묶음 위치는 좋았는데"* — v1 자리)
        # 묶음 — 끈에서 나온 다발이 위로 한 번 접혀 고리를 이루고 끝 몇 가닥이 삐져나온다(동그란 혹은 *"살짝 나온 돌기"* 같았다)
        out = np.array([0.0, 0.35, -0.94], np.float32); out /= np.linalg.norm(out)
        up = np.array([0.0, 0.94, 0.35], np.float32)
        loop = HU.bez(c, c + out * 0.10 + up * 0.02, c + out * 0.11 + up * 0.10, c + out * 0.02 + up * 0.08, 8)
        bun = HU.lock_r(p, loop, [0.040, 0.046, 0.048, 0.046, 0.042, 0.038, 0.034, 0.030, 0.026], 0.02)
        for dx, k in ((-0.03, 0.9), (0.025, 1.0), (0.0, 1.1)):
            ends = HU.bez(c + out * 0.04, c + out * 0.08 + V(dx, -0.02, 0), c + out * 0.10 + V(dx * 1.5, -0.08 * k, 0), c + out * 0.09 + V(dx * 2, -0.13 * k, 0), 6)
            bun = smin(bun, HU.lock_r(p, ends, [0.024, 0.022, 0.018, 0.014, 0.010, 0.006, 0.003]), 0.012)
        tie = HU.band(p, c + out * 0.010, c + out * 0.040, 0.034)
        return [(cut(p, no_under_ear(p, smin(d, bun, 0.02))) + dw.strands(p, 0.0015, 55), 'hair'), (tie, 'hair')]
    def h_slick(p):         # 11 올백(사람용 다시) — 빗어 넘긴 숱 한 벌 · 정수리 뒤로 좀 더 부푼다 · 목덜미에서 끝
        return [(cut(p, natural_cap(p, 0.022, -0.22)), 'hair')]
    def h_mshort(p):        # 엘프 M자 앞머리 · 보통 기장(사용자: *"뒷머리를 좀 짧게 만든 일반적인 기장"* → *"뒷머리를 없애라고 일반적인 남자 머리처럼"*) — 뒤는 목덜미에서 끝난다
        ang = dw.angle(p)
        line = lambda a: np.maximum(el.curtain_line(a), el.hem_M(a))
        low = np.where(ang < 90, line(ang), el.curtain_line(ang))
        cap = el.scalp(p, 0.030, low) + el.part_line(p)
        d = smin(cap, el.bangs(p, el.hem_M, 72, 0.0), 0.015)                              # 뒷머리 기둥 없이 목덜미에서 끝나는 보통 남자 머리(사용자: *"11번 뒷머리를 없애라고"*)
        return [(cut(p, off_cheek(p, no_under_ear(p, d))) + dw.strands(p, 0.0012, 55) * (ang > 70), 'hair')]

    # 벗겨진 머리 둘(`Fringe` · `LongFringe`)은 남자만(11-07 사용자) — C#에서 성별 축으로 거른다(`MALE_ONLY`).
    # ── 앞머리 축(11-07 사용자: *"여자 머리는 앞머리가 없는 경우는 별로 없는데 대부분 길거나 짧은 정도는 있어도"* → *"앞머리가 있는 버전도 만들어줘"*) ──
    # 뒷머리는 원래 머리 그대로 두고 앞머리 한 장(`fringe` — 이마를 감싸는 한 장 + 다발 끝)을 얹는다 — 같은 앞머리가 어느 머리에든 붙는다.
    # **앞머리는 다발로 빚는다**(사용자 그림 `_refs/2026-11-07_human/want_bangs_*` · `want_hair_sheet` · 깃 밖 · 공통 특징만) — 이마를 덮는 한 장에
    # 끝만 깎은 앞머리는 *"바보 같은 앞머리"*(바가지)였다. 정수리 앞 · 가르마에서 난 **납작한 다발**(뿌리 넓고 끝 뾰족 · 머리 겉면에 눕는다)이
    # 이마 곡면을 타고 내려와 조금씩 겹치고 벌어지며 끝 길이가 들쭉날쭉하다.
    def clump(p, az0, az1, y0, y1, r0, off0=0.006, off1=0.018, n=12, F=2.4, lift=1.0):
        # 매끄러운 다발 — 마디 열둘(여섯이면 이음매가 꺾였다) · 굵기는 대부분 유지하다 끝에서만 가늘어진다(곧게 줄이면 세모꼴로 각졌다) ·
        # 겉면 법선 쪽으로 얇게 누른다(둥근 관이면 볼록볼록 떴다 — 엘프 v17) · 결무늬는 안 더한다(잔물결이 섰다 · 사용자 *"다발 자체가 매끄럽지 않아서"*)
        pts, rad = [], []
        for t in np.linspace(0, 1, n):
            az = np.radians(az0 + (az1 - az0) * dw.ease(t)); y = y0 + (y1 - y0) * t
            off = lift * (0.028 + 0.022 * np.sin(np.pi * min(t / 0.85, 1.0)) - 0.010 * dw.ease((t - 0.8) / 0.2)) + (1 - lift) * 0.008   # 앞머리 볼륨 — 뿌리는 숱 두께 · 가운데가 볼록 · 끝은 이마 쪽으로 살짝 말린다(붙여 두면 숱보다 들어가 보였다)
            pts.append(el.on_cap(np.sin(az) * 0.3, y, np.cos(az) * 0.3, off))
            rad.append(max(r0 * (1 - t ** 2.4) ** 0.7, 0.003))
        azm = np.radians((az0 + az1) * 0.5)
        nrm = np.array([np.sin(azm), 0.35, np.cos(azm)], np.float32); nrm /= np.linalg.norm(nrm)
        sq = lambda q: q + (F - 1) * (q @ nrm)[..., None] * nrm
        return HU.lock_r(sq(p), [sq(t) for t in pts], rad, 0.012) / F                          # 면 위 폭은 rad 그대로 · 두께만 1/F(× F로 넣으면 2.4배 넓어 한 장으로 녹았다 · v3)
    TOP = EY + 0.30
    BANG_SETS = {   # (뿌리 각도, 끝 각도, 끝 높이, 뿌리 굵기) — 각도 0 = 가운데 · +는 오른쪽(+x)
        'Clumps': [(0, 0, EY + 0.045, 0.050), (11, 13, EY + 0.070, 0.050), (-11, -13, EY + 0.070, 0.050), (24, 28, EY + 0.050, 0.048), (-24, -28, EY + 0.050, 0.048),
                   (37, 44, EY + 0.030, 0.046), (-37, -44, EY + 0.030, 0.046), (50, 60, EY - 0.030, 0.044), (-50, -60, EY - 0.030, 0.044)],
        'Sweep':  [(-30, -10, EY + 0.100, 0.048), (-18, 4, EY + 0.080, 0.050), (-6, 18, EY + 0.040, 0.052), (6, 26, EY - 0.030, 0.050), (18, 36, EY + 0.020, 0.046),
                   (30, 48, EY - 0.010, 0.044), (44, 60, EY - 0.050, 0.042), (-42, -52, EY + 0.040, 0.044), (-54, -62, EY - 0.030, 0.042)],
        'Wispy':  [(4, 16, EY + 0.020, 0.030), (-4, -16, EY + 0.020, 0.030), (8, 26, EY + 0.060, 0.028), (-8, -26, EY + 0.060, 0.028), (14, 36, EY + 0.000, 0.028),
                   (-14, -36, EY + 0.000, 0.028), (22, 46, EY + 0.040, 0.026), (-22, -46, EY + 0.040, 0.026), (30, 56, EY - 0.040, 0.026), (-30, -56, EY - 0.040, 0.026),
                   (40, 64, EY - 0.080, 0.024), (-40, -64, EY - 0.080, 0.024)],
        # 이마를 드러내는 앞머리(11-07 사용자: *"앞머리가 약간 깐 머리 같은 스타일도 더"*) — 다발이 가르마에서 관자놀이 쪽으로 넘어간다
        'Parted':   [(3, 34, EY + 0.060, 0.044), (-3, -34, EY + 0.060, 0.044), (8, 44, EY + 0.020, 0.044), (-8, -44, EY + 0.020, 0.044),
                     (14, 54, EY - 0.030, 0.042), (-14, -54, EY - 0.030, 0.042), (22, 62, EY - 0.060, 0.040), (-22, -62, EY - 0.060, 0.040)],
        'SideOpen': [(-28, -4, EY + 0.140, 0.050), (-26, 8, EY + 0.115, 0.050), (-24, 20, EY + 0.090, 0.048), (-20, 36, EY + 0.150, 0.050), (-16, 50, EY + 0.100, 0.048),   # 위쪽 · 오른쪽 관자놀이까지 채운다 — 넘긴 다발과 이마선 사이가 살 틈으로 남았다(v1)
                     (-22, 10, EY + 0.070, 0.046), (-18, 24, EY + 0.040, 0.046), (-14, 38, EY + 0.010, 0.044), (-10, 50, EY - 0.030, 0.042),
                     (-6, 60, EY - 0.060, 0.040), (-30, -48, EY - 0.020, 0.040), (-34, -58, EY - 0.060, 0.038)],
        'Open':     [(26, 56, EY + 0.000, 0.034), (-26, -56, EY + 0.000, 0.034), (32, 64, EY - 0.060, 0.032), (-32, -64, EY - 0.060, 0.032)],   # 옆머리 위에 겹쳐 얹는다 — 옆머리와 나란히 붙으면 가는 살 틈이 길게 남았다(사용자 v4)
    }
    OPEN_BANGS = ('Parted', 'SideOpen', 'Open')   # 이마가 보이는 앞머리 — 원래 머리의 이마선을 비우지 않는다(비우면 드러난 이마 위로 살이 보인다)
    def bangs_piece(p, kind):
        d = HU.NONE(p)
        for az0, az1, y1, r0 in BANG_SETS[kind]:
            d = smin(d, clump(p, az0, az1, TOP, y1, r0, lift=0.3 if kind in OPEN_BANGS else 1.0), 0.005)                                    # 다발끼리 덜 녹인다 — 끝이 갈라져 보이게
        return d
    spec = el.EARS['Leaf']
    BACKS = {   # 앞머리를 얹을 여자 뒷머리(앞머리가 없던 것)
        'Straight': lambda p: el.h_straight(p, spec)[0], 'HalfUp': lambda p: el.h_halfup(p, spec)[0], 'Tail': lambda p: el.h_tail(p, spec)[0],
        'LongBraids': lambda p: h_longbraids7(p)[0][0], 'TwinBraids': lambda p: h_twinbraids16(p)[0][0], 'Braid': lambda p: h_braid(p)[0][0],
        'Shoulder': lambda p: h_shoulder(p)[0][0], 'Mane': lambda p: h_mane14(p)[0][0], 'Long': lambda p: h_long12(p)[0][0],
        # 묶은 머리도 앞머리를 얹는다(사용자: *"꽁지랑 번머리 앞머리가 좀 아쉽네"* — 맨 이마선뿐이었다) · 끈 · 고리 조각은 뒤에 따로 붙는다
        'LowTail': lambda p: h_lowtail(p)[0][0], 'HalfBun': lambda p: h_halfup(p)[0][0], 'ManBun': lambda p: h_manbun(p)[0][0],
    }
    def with_bangs(back, kind):
        def f(p):
            if kind is None: d = BACKS[back](p)
            elif kind in OPEN_BANGS: d = smin(BACKS[back](p), bangs_piece(p, kind), 0.045)   # 넉넉히 녹인다 — 이마선이 높은 머리(꽁지 · 긴 생머리)에서 다발과 이마선 사이에 가는 살 틈이 길게 남았다(사용자)
            else:
                # 앞머리 자리(이마 앞 · 눈 높이 위)는 원래 머리를 비운다 — 남겨 두면 원래 이마선(가르마의 A자)이 앞머리 뒤로 비쳤다(사용자)
                ang = dw.angle(p)
                front = np.maximum(np.maximum((ang - 46) * np.pi / 180 * 0.30, (EY - 0.03) - p[..., 1]), p[..., 1] - (TOP - 0.04))   # 앞머리가 실제로 덮는 곳만(넓게 비우면 관자놀이 위 살이 드러났다 · v6)
                d = smin(smax(BACKS[back](p), -front, 0.02), bangs_piece(p, kind), 0.015)
            return [(cut(p, off_cheek(p, no_under_ear(p, d))), 'hair')]
        return f
    banged = {f'{b}+{k or "None"}': with_bangs(b, k) for b in BACKS for k in (None, 'Clumps', 'Sweep', 'Wispy', 'Parted', 'SideOpen', 'Open')}
    # 앞머리 없음과 차이가 없는 짝은 만들지 않는다(11-07 사용자: *"Straight Shoulder는 앞머리 없음이랑 깐머리랑 차이가 없어"* — 차이를 만들라는 게 아니라 안 만들어도 된다는 말) —
    # 가르마에서 이마를 덮으며 흘러내리는 앞이라 깐 머리의 가는 가닥이 그 숱에 묻힌다.
    SAME_AS_NONE = ('Straight+Open', 'Shoulder+Open')
    for k in SAME_AS_NONE: banged.pop(k, None)
    # 게임용 조각(`bake_hair.py`) — 앞머리는 따로 고르는 축이라 앞머리 한 장과 '앞을 덜어 낸 뒷머리'를 따로 뽑는다(11-08 사용자: A안).
    # 다듬기(귀 · 볼 · 귀 밑)는 위 `with_bangs`와 같다.
    def trimmed(back):
        def f(p):
            ang = dw.angle(p)
            front = np.maximum(np.maximum((ang - 46) * np.pi / 180 * 0.30, (EY - 0.03) - p[..., 1]), p[..., 1] - (TOP - 0.04))
            return [(cut(p, off_cheek(p, no_under_ear(p, smax(BACKS[back](p), -front, 0.02)))), 'hair')]
        return f
    def bangs_only(kind):
        return lambda p: [(cut(p, off_cheek(p, no_under_ear(p, bangs_piece(p, kind)))), 'hair')]
    banged.update({f'Trim:{b}': trimmed(b) for b in BACKS})
    banged.update({f'Bangs:{k}': bangs_only(k) for k in BANG_SETS})
    return {'Long': h_long12, 'LongOld': h_long, 'Mane': h_mane14, 'ManeOld': h_mane, 'LongBraids': h_longbraids7, 'TwinBraids': h_twinbraids16, 'TwinBraidsOld': h_twinbraids,
            'Slick': h_slick, 'SidePart': h_sidepart, 'TwoBlock': h_twoblock, 'ShortFringe': h_shortfringe, 'Messy': h_messy, 'Spiky': h_spiky, 'Dandy': h_dandy, 'Comma': h_comma, 'Dreads': h_dreads, 'FauxHawk': h_fauxhawk, 'Pixie': h_pixie, 'Crop': h_crop, 'BaldWings': h_baldwings, 'SlickMullet': h_slickmullet,
            'BuzzCut': h_buzzcut, 'LowTail': h_lowtail, 'HalfBun': h_halfup, 'Military': h_military, 'Pomp': h_pomp, 'ManBun': h_manbun, 'Shoulder': h_shoulder, 'Curtain': h_curtain, 'BangsMShort': h_mshort,
            'Braid': h_braid, 'LongFringe': h_longfringe, **banged,
            'Topknot': h_topknot, 'Fringe': h_fringe, 'Crest': h_crest, 'TwinTails': h_twinhigh, 'TwinTailsV': lambda p: h_twinhigh(p, True), 'TwinTailsSide': h_twintails}

MALE_ONLY = ('Fringe', 'LongFringe', 'BaldWings')
HAIR_COL = (0.33, 0.22, 0.15); DECO_COL = (0.55, 0.50, 0.45); PLAIT_COL = (0.26, 0.17, 0.12)

def pieces(kind, head_name, hair, col=HAIR_COL):
    h, fams = family(kind, head_name)
    fn = fams[hair]
    cache = {}
    def part(i):
        def g(p):
            if cache.get('k') is not p: cache['k'] = p; cache['v'] = fn(p)
            v = cache['v']
            return v[i][0] if i < len(v) else HU.NONE(p)
        return g
    n = len(fn(np.zeros((1, 3), np.float32)))
    P = [(HU.face(h, 'Button'), HU.SKIN), (lambda p: HU.eyes(h, p), HU.DARK), (lambda p: HU.glint(h, p), HU.WHITE),
         (lambda p: HU.brows(h, 'Soft', p), tuple(c * 0.85 for c in col)), (lambda p: HU.mouth(h, p), HU.LIP), (HU.body, HU.TUNIC)]
    sample = fn(np.zeros((1, 3), np.float32))
    for i in range(n):
        tag = sample[i][1]
        buzz = tuple(HU.SKIN[j] * 0.55 + col[j] * 0.45 for j in range(3))   # 반삭 — 살과 머리색 사이(`MarkingLook` `.Buzz`)
        P.append((part(i), col if tag == 'hair' else PLAIT_COL if tag == 'plait' else buzz if tag == 'buzz' else DECO_COL))
    return P
