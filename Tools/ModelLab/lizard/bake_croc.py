"""공룡 악어(`lizard.py`의 `brute`)를 게임 메시로 뽑는다(11-15 사용자: *"리자드 악어 옮겨봐 유니티로"* — 엔트와 같은 길: 미리보기 수식을 C#으로 손으로 옮기지 않는다).

리자드맨(`Pawn/16` — C#으로 옮긴 수식)과 **따로인 큰 종족**이다(11-14 사용자: *"일반 리자드맨이랑 따로 빼서"*). 미리보기 `pieces('brute')`를 그대로 떠서
(`common/bake_sdf.py`) `baked/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/18`이 읽어 스킨 메시로 뼈에 붙인다.
**모양을 고치면 미리보기를 고치고 → `python bake_croc.py` → `Pawn/18`.** 미리보기 판 v22 기준(생김새 축 · 팔레트 — 아래 `build`).
- 크기는 미리보기 그대로(`BRUTE_S` 1.45배가 이미 들어 있다 — 뼈대 · 관절도 그 배수로 낸다).
- 머리 조각은 머리 뼈에 통째로 · 꼬리는 몸과 한 살이라 엉덩이 뼈의 캡슐이 꼬리를 따라간다 · 골판은 붙은 살의 뼈를 따른다.
- 관절 — 역관절 다리는 `brute_leg_pts` · 팔은 `brute_arm_pts`(솟은 어깨) · 머리 뼈는 굵은 목의 가운데(머리가 목 끝에 앞으로 나와 있다).
실행: python bake_croc.py"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common')); sys.path.insert(0, _here)
import numpy as np
import lizard as lz
from bake_sdf import Spec, run, ARMS, LEGS

OUT = os.path.join(_here, 'baked_croc')
HANDS = tuple(b for b in ARMS if b.startswith('Hand'))
FEET = tuple(b for b in LEGS if b.startswith('Foot'))
TRUNK = ('Hips', 'Spine', 'Head')

def L(v): return np.array([-v[0], v[1], v[2]], np.float32)
def P(*v): return np.array(v, np.float32)

def skeleton():
    lz.set_body('brute'); S = lz.BRUTE_S; D, TOP, HO, HK = lz.D, lz.TOP, lz.HO, lz.HK
    sk = []
    (hp, kn, an, ba), lr = lz.brute_leg_pts()
    hipY = float(hp[1]); yh = hipY + 0.05
    # 몸통 — 굽은 등을 따라(`zs`) 엉덩이 → 등뼈. 반지름은 그 높이의 몸통 반지름.
    ys = np.linspace(D['lift'], TOP - 0.05, 9)
    for y0, y1 in zip(ys[:-1], ys[1:]):
        sk.append(('Hips' if y1 <= yh + 0.08 else 'Spine', P(0, y0, lz.zs(y0)), P(0, y1, lz.zs(y1)), lz.torso_rad(y0) * 0.9, lz.torso_rad(y1) * 0.9))
    # 꼬리 — 엉덩이 뼈가 끝까지 든다
    for a, b, ra, rb in zip(lz.TAIL[:-1], lz.TAIL[1:], lz.TAIL_R[:-1], lz.TAIL_R[1:]): sk.append(('Hips', a, b, ra, rb))
    # 목 → 머리 — 목 가운데가 머리 관절
    nb = P(0, TOP - 0.08, float(lz.zs(TOP)) - 0.05); na = HO + P(0, -0.07, -0.13) * HK
    neck = nb + (na - nb) * 0.45
    sk.append(('Spine', nb, neck, lz.NECK_R * 1.18, lz.NECK_R))
    sk.append(('Head', neck, HO, lz.NECK_R, 0.24 * HK))
    # 팔 · 손
    sh, el, wr, r = lz.brute_arm_pts(); c, h, fs = lz.brute_fingers()
    arm = [('Arm', sh, el, r * 1.2, r * 0.9), ('LowerArm', el, wr, r * 0.95, r * 0.7), ('Hand', wr, c, r * 0.6, h * 0.6)] + [('Hand', a, t, h * 0.25, h * 0.15) for a, m1, m2, t in fs]
    # 다리 · 발
    _, _, _, toes = lz.brute_toe_pts()
    leg = [('Leg', hp, kn, lr * 1.12, lr * 0.94), ('LowerLeg', kn, an, lr * 0.86, lr * 0.56), ('Foot', an, ba, lr * 0.56, lr * 0.62)] + [('Foot', base, tip, 0.054, 0.038) for base, k, tip in toes]
    for name, a, b, ra, rb in arm + leg:
        sk.append((name + 'R', np.asarray(a, np.float32), np.asarray(b, np.float32), ra, rb))
        sk.append((name + 'L', L(a), L(b), ra, rb))
    joints = {'Hips': P(0, hipY, 0), 'Spine': P(0, yh, 0), 'Head': neck}
    for sd, f in (('R', lambda v: np.asarray(v, np.float32)), ('L', L)):
        joints.update({'Arm' + sd: f(sh), 'LowerArm' + sd: f(el), 'Hand' + sd: f(wr), 'Leg' + sd: f(hp), 'LowerLeg' + sd: f(kn), 'Foot' + sd: f(an)})
    # 미리보기는 몸 전체를 `BRUTE_S`배로 키워 그린다 — 뼈대 · 관절도 같이
    sk = [(n, np.asarray(a, np.float32) * S, np.asarray(b, np.float32) * S, ra * S, rb * S) for n, a, b, ra, rb in sk]
    joints = {k: np.asarray(v, np.float32) * S for k, v in joints.items()}
    return sk, joints

# **살은 세 조각으로 뜬다**(11-15 사용자: *"손이 몸에 붙는 오류"*) — 미리보기의 살(`skin_body`)은 몸통 · 팔 · 손 · 다리 · 꼬리를 합친 거리장 하나라,
# 허벅지 옆에 닿은 손이 표면 그물에서 허벅지와 한 겉면으로 이어졌다(그 사이 세모가 팔을 들면 끈처럼 늘어났다). 조각마다 따로 뜨고 고를 뼈도 가른다.
def split_flesh(P):
    S = lz.BRUTE_S
    def trunk(p):
        q = p / S; tn = lz.smin(lz.smin(lz.torso(q), lz.neck(q), 0.05), lz.brute_muscles(q), 0.09)
        return np.minimum(tn, lz.smin(lz.torso(q), lz.tail(q), 0.06)) * S
    arms = lambda p: np.minimum(lz.brute_arms(p / S), lz.brute_hands(p / S)) * S
    legs = lambda p: np.minimum(lz.brute_legs(p / S), lz.brute_feet(p / S)) * S
    col = P[6][1]
    return P[:6] + [(trunk, col), (arms, col), (legs, col)] + P[7:]

# **생김새는 고르기 축 · 색은 팔레트**(11-15 사용자: *"공룡 악어도 look 더 고를 수 있게"* — 미리보기 v22에서 확정). 축마다 조각을 따로 떠서
# 게임의 렌더러 이름(`Spine.Pick.{축}.{벌}@조건`)으로 낸다 — `PawnLook`이 개체마다 벌을 고른다. 머리 모양이 머리 좌표를 휘므로 머리에 붙는 것은 머리마다 한 벌.
# - 볏이 있으면 눈 위 뿔은 없다(`@Fins.!On` — 미리보기 `set_look`과 같은 규칙) · 없는 벌(`None` · `Off` · `Plain`)은 조건에만 적어 축에 모인다.
# - 색은 칸 몫으로 뽑는다(미리보기 팔레트 `code` — 비늘 r · 배 g · 볏 막 b · 뿔 · 눈은 제 재질의 r). 칠은 게임이 팔레트로(`BRUTE_PAL`의 열 벌).
HEADS = (('Deep', 'deep'), ('Long', 'long'), ('Broad', 'broad'))
BROWS = (('Small', 'small'), ('Mid', 'mid'), ('Large', 'large'), ('Up', 'up'), ('Broken', 'broken'))
CROWNS = (('Plates', 'plates'), ('Spikes', 'spikes'), ('Frill', 'frill'))
NOSES = (('Nub', 'nub'), ('Rhino', 'rhino'), ('Twin', 'twin'), ('Broken', 'broken'))
BACKS = (('Tall', 'tall'), ('Mid', 'mid'), ('Low', 'low'))
HORN_STEP = 0.004

def look(**kw):
    d = dict(pal='code', head='deep', brow='none', crown='none', nose='none', back='mid', fins='none', tail='plain'); d.update(kw); return d

def pieces_of(**kw):
    lz.set_look(look(**kw)); return lz.pieces('brute')

def build():
    sk, joints = skeleton()
    P, SP = [], []
    def add(f, col, spec): P.append((f, col)); SP.append(spec)
    # 머리 — 머리 모양마다: 살 · 입 속 · 눈알 · 눈동자 · 이 + 눈 위 뿔 · 정수리 · 코뿔 · 볏(벌마다)
    for H, h in HEADS:
        Q = pieces_of(head=h); part = 'Spine.Pick.Head.' + H
        add(*Q[0], Spec('skin ' + H, '', 'Head', part=part, mat='Hide'))
        add(*Q[1], Spec('inner ' + H, 'None', 'Head', part=part))
        add(*Q[2], Spec('eye ' + H, '', 'Head', step=0.004, part=part, mat='Eye'))
        add(*Q[4], Spec('pupil ' + H, 'None', 'Head', step=0.003, part=part))
        add(*Q[5], Spec('teeth ' + H, 'Fine', 'Head', step=0.0035, part=part))
        for B, b in BROWS:
            add(*pieces_of(head=h, brow=b)[3], Spec('brow %s %s' % (B, H), '', 'Head', step=HORN_STEP, part='Spine.Pick.Brow.' + B, when='@Head.%s@Fins.!On@Brow.!None' % H, mat='Horn'))
        for C, c in CROWNS:
            add(*pieces_of(head=h, crown=c)[3], Spec('crown %s %s' % (C, H), '', 'Head', step=HORN_STEP, part='Spine.Pick.Crown.' + C, when='@Head.%s@Crown.!None' % H, mat='Horn'))
        for N, n in NOSES:
            add(*pieces_of(head=h, nose=n)[3], Spec('nose %s %s' % (N, H), '', 'Head', step=HORN_STEP, part='Spine.Pick.Nose.' + N, when='@Head.%s@Nose.!None' % H, mat='Horn'))
        Q = pieces_of(head=h, fins='fins')
        add(*Q[6], Spec('fin ' + H, '', 'Head', step=HORN_STEP, part='Spine.Pick.Fins.On', when='@Head.%s@Fins.!Off' % H, mat='Hide'))
        add(*Q[7], Spec('fin spines ' + H, 'Fine', 'Head', step=0.003, part='Spine.Pick.Fins.On', when='@Head.%s@Fins.!Off' % H, mat='Horn'))
    # 몸 — 살 세 조각(한 덩이 · 칸 몫) · 손발톱 · 허리띠 · 가리개
    Q = pieces_of(); S = lz.BRUTE_S; col = Q[8][1]
    def trunk(p):
        q = p / S; tn = lz.smin(lz.smin(lz.torso(q), lz.neck(q), 0.05), lz.brute_muscles(q), 0.09)
        return np.minimum(tn, lz.smin(lz.torso(q), lz.tail(q), 0.06)) * S
    add(trunk, col, Spec('trunk', '', allow=TRUNK, step=0.012, mat='Hide'))
    add(lambda p: np.minimum(lz.brute_arms(p / S), lz.brute_hands(p / S)) * S, col, Spec('arms', '', allow=('Spine',) + ARMS, step=0.009, mat='Hide'))
    add(lambda p: np.minimum(lz.brute_legs(p / S), lz.brute_feet(p / S)) * S, col, Spec('legs', '', allow=('Hips',) + LEGS, step=0.009, mat='Hide'))
    add(*Q[9], Spec('hand_claws', 'Fine', allow=HANDS, step=0.004))
    add(*Q[10], Spec('foot_claws', 'Fine', allow=FEET, step=0.004))
    add(*Q[11], Spec('belt', '', allow=('Hips', 'Spine')))
    add(*Q[12], Spec('flap', '', allow=('Hips',)))
    # 등 골판 키 셋 · 꼬리 끝(철퇴 — 살 목 + 뼈 공과 가시 · 양옆 낫 뿔)
    for K, k in BACKS:
        add(*pieces_of(back=k)[13], Spec('plates ' + K, '', allow=TRUNK, part='Spine.Pick.Back.' + K, mat='Horn'))
    lm = look(tail='mace')
    add(lambda p: lz.tail_club(p / S, lm) * S, col, Spec('mace neck', '', 'Hips', part='Spine.Pick.Tail.Mace', when='@Tail.!Plain', mat='Hide'))
    add(*pieces_of(tail='mace')[14], Spec('mace', '', 'Hips', step=0.006, part='Spine.Pick.Tail.Mace', when='@Tail.!Plain', mat='Horn'))
    add(*pieces_of(tail='horns')[14], Spec('tail horns', '', 'Hips', step=0.006, part='Spine.Pick.Tail.Horns', when='@Tail.!Plain', mat='Horn'))
    return P, SP, sk, joints

def palettes():
    """게임이 고르는 색 — 미리보기 `BRUTE_PAL`의 열 벌(칸: 비늘 재질 = 비늘 · 배 · 볏 막 / 뿔 / 눈)."""
    return [dict(name=n.capitalize(), slots=[dict(mat='Hide', colors=[P_['skin'], P_['belly'], P_['fin']]), dict(mat='Horn', colors=[P_['horn']]), dict(mat='Eye', colors=[P_['eye']])])
            for n, P_ in lz.BRUTE_PAL.items() if n != 'code']

# 상자 앞쪽은 긴 주둥이까지(z 1.65m — 처음 1.6m 상자에 주둥이 끝이 잘려 입 속이 구멍으로 보였다 · 11-15 사용자)
if __name__ == '__main__':
    run(build, OUT, 'Chibi_DinoCroc', lo=(-1.3, -0.05, -2.6), hi=(1.3, 3.6, 2.4), palettes=palettes())
