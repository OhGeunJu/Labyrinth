"""코볼트(하이에나 몬스터)를 게임 메시로 뽑는다(11-19 사용자: *"ㄱㄱ"* — 미리보기 v20 확정 뒤).

미리보기 `kobold.py`의 조각을 그대로 떠서(`common/bake_sdf.py`) `baked/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/24`.
**모양을 고치면 미리보기를 고치고 → `python bake_kobold.py` → `Pawn/24`.** 뼈대 · 관절은 늑대인간과 같은 셈(`werewolf/bake_werewolf.py`).

**옷은 고르기 축 넷**(`PawnLook`이 개체마다 벌을 고른다 — 미리보기 `TOPS` · `BOTTOMS` · 붕대):
  `Spine.Pick.Top.{None|Vest|Shirt|RagShirt|Pauldron}` · `Bottom.{Shorts|Pants|Rags}` · `WrapArm.{On|Off}` · `WrapShin.{On|Off}`.
  '없음' 벌도 진짜 조각이다 — 옷 · 붕대가 덮을 털이 그 벌에 든다: 등 볏(`Top.None`) · 팔뚝 털 술(`WrapArm.Off`) · 발목 털 술(`WrapShin.Off`) ·
  넓적다리 털 술(`Bottom.Rags` — 바지가 덮는다). 그래서 옷이 털을 뚫지 않는다.
**털 색은 팔레트**(`COATS` 여섯 — 프리팹 하나 · 개체마다 `PaletteLook`): 털 재질(`Fur`)의 정점 색은 칸 몫이다 — r 바탕 · g 무늬 · b 밝은(가슴 · 턱 밑 · 망토) ·
  a 짙은(볏 · 귀 끝 · 꼬리 끝 · 주둥이). **무늬가 셋**(점 · 줄 · 갈색 망토)이라 털 조각은 무늬마다 한 벌씩 뜨고 `@Palette.{이름}`이 맞는 벌만 켠다
  (칸 넷으로는 점과 줄을 한 메시에 못 담는다 — 색에 딸린 모양은 팔레트 조건 · CLAUDE.md). 털 조각은 축 `Fur.On` · 머리 털은 `FurHead.On`(벌 하나 — 늘 켜지고 조건만 거른다).
실행: python bake_kobold.py"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common')); sys.path.insert(0, _here)
import numpy as np
import kobold as k
from bake_sdf import Spec, run, ARMS, LEGS

OUT = os.path.join(_here, 'baked')
HANDS = tuple(b for b in ARMS if b.startswith('Hand'))
FEET = tuple(b for b in LEGS if b.startswith('Foot'))
LOWER_ARMS = ('LowerArmL', 'LowerArmR', 'HandL', 'HandR')
LOWER_LEGS = ('LowerLegL', 'LowerLegR', 'FootL', 'FootR')
TORSO = ('Hips', 'Spine')

# ── 무늬 묶음 — 팔레트 이름(`COATS`의 키를 대문자로) · 무늬 값 ──
GROUPS = {'Spots': (('spotted', 'red', 'faded', 'black'), dict(spots=1.0, stripes=0.0, body_stripes=0.0, mantle=0.0)),
          'Stripes': (('striped',), dict(spots=0.0, stripes=1.0, body_stripes=1.0, mantle=0.0)),
          'Brown': (('brown',), dict(spots=0.0, stripes=0.9, body_stripes=0.0, mantle=1.0))}
def pal_name(c): return c[:1].upper() + c[1:]

def basis(group):
    """칸 몫을 재려고 털 색을 바탕 벡터로 둔다 — 색 셈이 선형이라 섞인 값이 곧 몫이다(r 바탕 · g 무늬 · b 밝은 · 나머지 짙은)."""
    k.COAT = dict(GROUPS[group][1]); k.FUR, k.SPOT, k.PALE, k.FUR_D, k.MUZ, k.MANTLE = (1, 0, 0), (0, 1, 0), (0, 0, 1), (0, 0, 0), (0, 0, 0), (0, 0, 1)
def shares(group, col):
    def f(p):
        basis(group)
        c = np.asarray(col(p) if callable(col) else np.broadcast_to(np.asarray(col, np.float32), p.shape), np.float32).reshape(len(p), 3)
        c = np.clip(c, 0, 1)
        return np.concatenate([c, np.clip(1 - c.sum(1, keepdims=True), 0, 1)], 1)
    return f

def palettes():
    out = []
    for name, c in k.COATS.items():
        # 무늬 세기(바랜 0.6 · 검은 0.7)는 무늬 칸 색을 바탕 쪽으로 당겨 낸다 · 짙은 칸은 짙은 털과 주둥이의 가운데
        s = c['spots'] if c['spots'] > 0 else 1.0
        spot = tuple(np.asarray(c['fur']) + (np.asarray(c['spot']) - np.asarray(c['fur'])) * s)
        pale = c['mantle_c'] if c['mantle'] > 0 else c['pale']
        dark = tuple((np.asarray(c['dark']) + np.asarray(c['muz'])) * 0.5)
        out.append(dict(name=pal_name(name), slots=[dict(mat='Fur', colors=[c['fur'], spot, pale, dark])]))
    return out

def build():
    k.set_coat('spotted')
    H = k.head_pieces()   # skin · inner · nose · eyeball · pupil · glint · liner · lid · ear · ear_in · teeth
    head_names = ['skin', 'inner', 'nose', 'eyeball', 'pupil', 'glint', 'liner', 'lid', 'ear', 'ear_in', 'teeth']
    head_meta = {'skin': ('', None), 'inner': ('None', None), 'nose': ('Fine', None), 'eyeball': ('None', None), 'pupil': ('None', 0.003),
                 'glint': ('None', 0.0012), 'liner': ('None', None), 'lid': ('', None), 'ear': ('', None), 'ear_in': ('None', None), 'teeth': ('Fine', None)}
    FUR_HEAD = ('skin', 'lid', 'ear')
    pieces, specs = [], []
    def add(f, col, sp): pieces.append((f, col)); specs.append(sp)

    # 털 아닌 머리 조각 — 정점 색 그대로
    for (f, col), nm in zip(H, head_names):
        if nm in FUR_HEAD: continue
        line, step = head_meta[nm]; add(f, col, Spec(nm, line, 'Head', step=step))
    add(k.hand_claws, k.CLAW, Spec('hand_claws', 'Fine', allow=HANDS))
    add(k.foot_claws, k.CLAW, Spec('foot_claws', 'Fine', allow=FEET))

    # 털 조각 — 무늬 묶음마다 한 벌(`@Palette.…`)
    trunk = lambda p: k.smin(k.torso(p), k.neck(p), 0.05)
    arms = lambda p: np.minimum(k.arms(p), k.hands(p))
    legs = lambda p: np.minimum(k.legs(p), k.feet(p))
    for g, (names, _) in GROUPS.items():
        when = '@Palette.' + '|'.join(pal_name(n) for n in names)
        fur_on = dict(part='Spine.Pick.Fur.On', when=when, mat='Fur')
        for (f, col), nm in zip(H, head_names):
            if nm in FUR_HEAD: add(f, shares(g, col), Spec('%s %s' % (nm, g), '', 'Head', part='Spine.Pick.FurHead.On', when=when, mat='Fur'))   # 머리 털은 축을 가른다 — 몸 털과 이름이 같으면 메시 파일이 덮어써진다
        add(trunk, shares(g, k.body_col), Spec('trunk ' + g, '', allow=('Hips', 'Spine', 'Head'), **fur_on))
        add(arms, shares(g, k.body_col), Spec('arms ' + g, '', allow=('Spine',) + ARMS, **fur_on))
        add(legs, shares(g, k.body_col), Spec('legs ' + g, '', allow=('Hips',) + LEGS, **fur_on))
        add(k.fur, shares(g, k.fur_col), Spec('fur ' + g, '', allow=('Spine', 'Head', 'ArmL', 'ArmR'), **fur_on))
        add(k.arm_fur_upper, shares(g, lambda p: np.zeros(p.shape, np.float32)), Spec('arm_fur ' + g, '', allow=ARMS, **fur_on))
        add(k.tail, shares(g, k.tail_col), Spec('tail ' + g, '', 'Hips', **fur_on))
        # 옷 · 붕대가 덮을 털 — 그 '없음' 벌에 든다
        dark = lambda p: np.zeros(p.shape, np.float32)
        add(k.crest_back, shares(g, k.fur_col), Spec('crest_back ' + g, '', allow=('Spine', 'Hips'), part='Spine.Pick.Top.None', when=when, mat='Fur'))
        add(k.arm_fur_lower, shares(g, dark), Spec('arm_fur_lower ' + g, '', allow=ARMS, part='Spine.Pick.WrapArm.Off', when=when, mat='Fur'))
        add(k.heel_fur, shares(g, dark), Spec('heel_fur ' + g, '', allow=LEGS, part='Spine.Pick.WrapShin.Off', when=when, mat='Fur'))
        add(k.thigh_fur, shares(g, dark), Spec('thigh_fur ' + g, '', allow=LEGS, part='Spine.Pick.Bottom.Rags', when=when, mat='Fur'))

    # 옷 — 고르기 축(정점 색 그대로)
    def pick(axis, opt): return dict(part='Spine.Pick.%s.%s' % (axis, opt))
    add(k.vest, k.vest_col, Spec('vest', '', allow=TORSO + ('ArmL', 'ArmR'), **pick('Top', 'Vest')))
    add(k.shirt, k.shirt_col, Spec('shirt', '', allow=TORSO + ('ArmL', 'ArmR'), **pick('Top', 'Shirt')))
    add(k.rag_shirt, k.rag_shirt_col, Spec('rag_shirt', '', allow=TORSO + ('ArmL', 'ArmR'), **pick('Top', 'RagShirt')))
    add(k.pauldron, k.pauldron_col, Spec('pauldron', '', allow=('ArmR', 'Spine'), **pick('Top', 'Pauldron')))
    add(k.pad_base, k.STRAP, Spec('pad_base', '', allow=('ArmR', 'Spine'), **pick('Top', 'Pauldron')))
    add(k.strap, k.STRAP, Spec('strap', '', allow=TORSO, **pick('Top', 'Pauldron')))
    add(k.pants, k.CLOTH, Spec('shorts', '', allow=('Hips', 'Spine') + LEGS, **pick('Bottom', 'Shorts')))
    add(k.pants_long, k.pants_col, Spec('pants', '', allow=('Hips', 'Spine') + LEGS, **pick('Bottom', 'Pants')))
    add(k.pants_belt, k.pants_belt_col, Spec('pants_belt', '', allow=TORSO, **pick('Bottom', 'Pants')))
    add(k.buckle, k.buckle_col, Spec('buckle', 'Fine', 'Hips', **pick('Bottom', 'Pants')))
    add(k.sash, k.SASH, Spec('sash', '', allow=TORSO, **pick('Bottom', 'Rags')))
    add(k.ring, k.BRASS, Spec('ring', 'Fine', 'Hips', **pick('Bottom', 'Rags')))
    add(k.rags, k.rags_col, Spec('rags', '', allow=('Hips', 'LegL', 'LegR'), **pick('Bottom', 'Rags')))
    add(k.arm_wraps, k.arm_wraps_col, Spec('arm_wraps', '', allow=LOWER_ARMS, **pick('WrapArm', 'On')))
    add(k.shin_wraps, k.shin_wraps_col, Spec('shin_wraps', '', allow=LOWER_LEGS, **pick('WrapShin', 'On')))
    sk, joints = skeleton()
    return pieces, specs, sk, joints

def L(v): return np.array([-v[0], v[1], v[2]], np.float32)

def skeleton():
    """늑대인간과 같은 셈(`bake_werewolf.skeleton`) — 몸 · 팔 · 역관절 다리가 같은 식이다."""
    D, TOP, HO = k.D, k.TOP, k.HO
    sk = []
    yh = D['hipY'] + 0.05
    sk.append(('Hips', np.array([0, D['lift'], 0.0], np.float32), np.array([0, yh, 0.0], np.float32), D['half'][1], D['half'][2]))
    sk.append(('Spine', np.array([0, yh, 0.0], np.float32), np.array([0, TOP - 0.05, 0.0], np.float32), D['half'][2], D['half'][4] * 0.9))
    neck = np.array([0, TOP + 0.02, 0.03], np.float32)
    sk.append(('Spine', np.array([0, TOP - 0.05, 0.0], np.float32), neck, 0.17, 0.15))
    sk.append(('Head', neck + np.array([0, 0.04, 0.02], np.float32), HO, 0.14, 0.22))
    sh, el, wr, r = k.arm_pts(); c, h, R, fs = k.fingers()
    arm = [('Arm', sh, el, r * 1.1, r * 0.9), ('LowerArm', el, wr, r * 0.9, r * 0.7), ('Hand', wr, c, r * 0.6, h * 0.5)] + [('Hand', a, t, h * 0.24, h * 0.14) for a, m1, m2, t in fs]
    (hp, kn, an, ba), lr = k.leg_pts(); b, toes = k.toe_pts()
    leg = [('Leg', hp, kn, lr * 1.16, lr * 0.92), ('LowerLeg', kn, an, lr * 0.84, lr * 0.54), ('Foot', an, ba, lr * 0.5, lr * 0.54)] + [('Foot', ba, tip, 0.04, 0.03) for base, kk, tip in toes]
    for name, a, bb, ra, rb in arm + leg:
        sk.append((name + 'R', np.asarray(a, np.float32), np.asarray(bb, np.float32), ra, rb))
        sk.append((name + 'L', L(a), L(bb), ra, rb))
    joints = {'Hips': np.array([0, D['hipY'], 0.0]), 'Spine': np.array([0, yh, 0.0]), 'Head': neck}
    for sd, f in (('R', lambda v: np.asarray(v, np.float32)), ('L', L)):
        joints.update({'Arm' + sd: f(sh), 'LowerArm' + sd: f(el), 'Hand' + sd: f(wr), 'Leg' + sd: f(hp), 'LowerLeg' + sd: f(kn), 'Foot' + sd: f(an)})
    return sk, joints

if __name__ == '__main__':
    run(build, OUT, 'Chibi_Kobold', lo=(-0.85, -0.05, -0.85), hi=(0.85, 2.30, 0.95), palettes=palettes())
