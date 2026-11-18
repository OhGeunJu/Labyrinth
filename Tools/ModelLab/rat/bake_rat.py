"""괴물쥐를 게임 메시로 뽑는다(11-18 사용자: *"넣어"* — 미리보기 v12 확정 뒤).

미리보기 `rat.pieces()`를 그대로 떠서(`common/bake_quad.py` → `bake_sdf.py`) `baked/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/21`.
**모양을 고치면 미리보기를 고치고 → `python bake_rat.py` → `Pawn/21`.**
- 머리 살(목 포함)은 머리 · 몸 사이에서 가중치로 나뉜다 · 코 · 눈 · 이 · 귀 · 수염은 머리 뼈에 통째로.
- 꼬리는 마디 사슬(`TAIL_BONES` — 여섯 마디: `Tail` · `Tail1` … `Tail5`) — 게임 `TailSway`가 마디마다 좌우로 흔든다.
실행: python bake_rat.py"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common')); sys.path.insert(0, _here)
import numpy as np
import rat as r
from bake_quad import specs_for, leg_caps, bake

OUT = os.path.join(_here, 'baked')
NAMES = ['head', 'nose', 'teeth', 'mouth', 'eyes', 'ears', 'whiskers', 'body', 'spines', 'front_legs', 'front_hands', 'front_claws',
         'back_legs', 'back_feet', 'back_claws', 'tail']
TAILS = ('Tail',) + tuple('Tail%d' % i for i in range(1, len(r.TAIL_BONES)))

def skeleton():
    V = lambda x, y, z: np.array([x, y, z], np.float32)
    sk = [('Body', V(0, 0.46, -0.36), V(0, 0.42, 0.18), 0.26, 0.22),
          ('Head', r.HEAD_C + V(0, -0.02, -0.08), r.TIP, 0.16, 0.05)]
    F_PAW = r.F_WR + V(0, -0.04, 0.12); B_TOE = r.B_HEEL + V(0, -0.04, 0.27)
    sk += leg_caps('LegFR', 'LegFL', [r.F_SH, r.F_EL, r.F_WR, F_PAW], [0.075, 0.055, 0.045, 0.035])
    sk += leg_caps('LegBR', 'LegBL', [r.B_HIP, r.B_KN, r.B_HEEL, B_TOE], [0.11, 0.07, 0.045, 0.035])
    T = r.TAIL
    for i, b in enumerate(TAILS): sk.append((b, T[i], T[i + 1], r.TAIL_R[i], r.TAIL_R[i + 1]))
    joints = {'Body': V(0, 0.44, -0.06), 'Head': V(0, 0.46, 0.30),
              'LegFR': r.F_SH, 'LegFL': np.array([-r.F_SH[0], r.F_SH[1], r.F_SH[2]], np.float32),
              'LegBR': r.B_HIP, 'LegBL': np.array([-r.B_HIP[0], r.B_HIP[1], r.B_HIP[2]], np.float32)}
    for i, b in enumerate(TAILS): joints[b] = T[i]
    return sk, joints

def build():
    P = r.pieces()
    assert len(P) == len(NAMES), '조각 %d · 이름 %d — 미리보기 `pieces()`가 바뀌었다' % (len(P), len(NAMES))
    specs = specs_for(P, NAMES, TAILS, fine=('teeth', 'whiskers', 'front_claws', 'back_claws'), none=('mouth', 'eyes'),
                      blend=('head',), legs_only=('front_hands', 'front_claws', 'back_feet', 'back_claws'), steps={'eyes': 0.0015, 'teeth': 0.002, 'whiskers': 0.002, 'mouth': 0.0018, 'ears': 0.0018}, thin=('ears',))
    sk, joints = skeleton()
    return [(f, c) for f, c, _ in P], specs, sk, joints

if __name__ == '__main__':
    bake(build, OUT, 'Chibi_Rat', lo=(-0.45, -0.05, -1.75), hi=(0.45, 0.95, 1.0), tail_links=len(TAILS))
