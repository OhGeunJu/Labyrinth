"""늑대인간을 게임 메시로 뽑는다(11-15 사용자: *"늑대 옮겨봐 유니티로"* — 엔트와 같은 길: 미리보기 수식을 C#으로 손으로 옮기지 않는다).

미리보기 `werewolf.pieces()`를 그대로 떠서(`common/bake_sdf.py`) `baked/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/19`가 읽어 스킨 메시로 뼈에 붙인다.
**모양을 고치면 미리보기를 고치고 → `python bake_werewolf.py` → `Pawn/19`.** 미리보기 판 v16 기준.
- 크기는 사람 치비의 1.1배(`SCALE` — 미리보기는 사람 눈금 그대로 두고 뽑을 때 키운다).
- 머리 조각은 머리 뼈에 통째로 · 꼬리는 엉덩이 뼈에 통째로 · 털 껍데기(정수리 → 목덜미 → 어깨)는 머리 · 등뼈 · 팔 사이에서 가중치로 나뉜다.
- 관절 — 역관절 다리(엉덩이 → 무릎 앞 → 뒤꿈치 뒤 · 위)는 `leg_pts` 자리 · 팔은 `arm_pts` 자리. 오른쪽이 +x(거울 `mir`), 게임의 L은 −x.
실행: python bake_werewolf.py"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common')); sys.path.insert(0, _here)
import numpy as np
import werewolf as w
from bake_sdf import Spec, run, ARMS, LEGS

OUT = os.path.join(_here, 'baked')
HANDS = tuple(b for b in ARMS if b.startswith('Hand'))
FEET = tuple(b for b in LEGS if b.startswith('Foot'))

# 미리보기 `pieces()`의 차례 그대로 — 머리 열하나(`head_pieces`) + 몸 아홉.
SPECS = [Spec('skin', '', 'Head'), Spec('inner', 'None', 'Head'), Spec('nose', 'Fine', 'Head'), Spec('eyeball', 'None', 'Head'),
         Spec('pupil', 'None', 'Head', step=0.003), Spec('glint', 'None', 'Head', step=0.0012), Spec('liner', 'None', 'Head'), Spec('lid', '', 'Head'),
         Spec('ear', '', 'Head'), Spec('ear_in', 'None', 'Head'), Spec('teeth', 'Fine', 'Head'),
         Spec('trunk', '', allow=('Hips', 'Spine', 'Head')), Spec('arms', '', allow=('Spine',) + ARMS), Spec('legs', '', allow=('Hips',) + LEGS),
         Spec('collar', '', allow=('Spine', 'Head', 'ArmL', 'ArmR')),
         Spec('fur', '', allow=('Spine', 'Head', 'ArmL', 'ArmR')), Spec('arm_fur', '', allow=ARMS), Spec('leg_fur', '', allow=LEGS),
         Spec('tail', '', 'Hips'), Spec('pants', '', allow=('Hips', 'Spine') + LEGS), Spec('hand_claws', 'Fine', allow=HANDS),
         Spec('foot_claws', 'Fine', allow=FEET)]
# **살은 세 조각으로 뜬다**(11-15 사용자: *"손이 몸에 붙는 오류"*) — 미리보기의 살은 몸통 · 팔 · 손 · 다리를 합친 거리장 하나라, 허벅지 옆에 닿은 손이
# 표면 그물에서 허벅지와 한 겉면으로 이어졌다(그 사이 세모가 팔을 들면 끈처럼 늘어났다). 조각마다 따로 뜨고 고를 뼈도 가른다(팔 조각은 팔 · 등뼈만).
def split_flesh(P):
    trunk = lambda p: w.smin(w.torso(p), w.neck(p), 0.05)
    arms = lambda p: np.minimum(w.arms(p), w.hands(p))
    legs = lambda p: np.minimum(w.legs(p), w.feet(p))
    col = P[11][1]
    return P[:11] + [(trunk, col), (arms, col), (legs, col)] + P[12:]

def L(v): return np.array([-v[0], v[1], v[2]], np.float32)

def skeleton():
    D, TOP, HO = w.D, w.TOP, w.HO
    sk = []
    # 몸통 — 엉덩이 · 등뼈. 반지름은 몸통 반폭(밑 · 가슴).
    yh = D['hipY'] + 0.05
    sk.append(('Hips', np.array([0, D['lift'], 0.0], np.float32), np.array([0, yh, 0.0], np.float32), D['half'][1], D['half'][2]))
    sk.append(('Spine', np.array([0, yh, 0.0], np.float32), np.array([0, TOP - 0.05, 0.0], np.float32), D['half'][2], D['half'][4] * 0.9))
    neck = np.array([0, TOP + 0.02, 0.03], np.float32)
    sk.append(('Spine', np.array([0, TOP - 0.05, 0.0], np.float32), neck, 0.17, 0.15))
    sk.append(('Head', neck + np.array([0, 0.04, 0.02], np.float32), HO, 0.14, 0.22))
    # 팔 — 어깨 → 팔꿈치 → 손목 → 손가락 끝
    sh, el, wr, r = w.arm_pts(); c, h, R, fs = w.fingers()
    arm = [('Arm', sh, el, r * 1.1, r * 0.9), ('LowerArm', el, wr, r * 0.9, r * 0.7), ('Hand', wr, c, r * 0.6, h * 0.5)] + [('Hand', a, t, h * 0.24, h * 0.14) for a, m1, m2, t in fs]
    # 다리 — 엉덩이 → 무릎 → 뒤꿈치 → 발볼 · 발가락
    (hp, kn, an, ba), lr = w.leg_pts(); b, toes = w.toe_pts()
    leg = [('Leg', hp, kn, lr * 1.16, lr * 0.92), ('LowerLeg', kn, an, lr * 0.84, lr * 0.54), ('Foot', an, ba, lr * 0.5, lr * 0.54)] + [('Foot', ba, tip, 0.04, 0.03) for base, k, tip in toes]
    for name, a, bb, ra, rb in arm + leg:
        sk.append((name + 'R', np.asarray(a, np.float32), np.asarray(bb, np.float32), ra, rb))
        sk.append((name + 'L', L(a), L(bb), ra, rb))
    joints = {'Hips': np.array([0, D['hipY'], 0.0]), 'Spine': np.array([0, yh, 0.0]), 'Head': neck}
    for sd, f in (('R', lambda v: np.asarray(v, np.float32)), ('L', L)):
        joints.update({'Arm' + sd: f(sh), 'LowerArm' + sd: f(el), 'Hand' + sd: f(wr), 'Leg' + sd: f(hp), 'LowerLeg' + sd: f(kn), 'Foot' + sd: f(an)})
    return sk, joints

def build():
    sk, joints = skeleton()
    return split_flesh(w.pieces()), SPECS, sk, joints

# 크기 — 사람 치비의 1.1배(11-15 사용자: *"늑대인간이 사람보다 1.1배 정도 크게"*). 미리보기는 사람 눈금 그대로 두고 뽑을 때 키운다.
SCALE = 1.1

if __name__ == '__main__':
    run(build, OUT, 'Chibi_Werewolf', lo=(-0.85, -0.05, -0.85), hi=(0.85, 2.45, 0.95), scale=SCALE)
