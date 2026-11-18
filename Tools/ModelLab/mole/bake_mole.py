"""두더지를 게임 메시로 뽑는다(11-18 사용자: *"넣어"* — 미리보기 v10 확정 뒤).

미리보기 `mole._pieces()`(단위 몸)를 그대로 떠서(`common/bake_quad.py` → `bake_sdf.py`) `baked/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/20`.
**모양을 고치면 미리보기를 고치고 → `python bake_mole.py` → `Pawn/20`.**
- 크기는 뽑을 때 `mole.SIZE`(1.6)배 — 소만 하다. 뼈대 선 · 관절은 단위 몸에서 적는다.
- 머리 + 몸이 한 털 살(`fur`)이라 머리 · 몸 사이에서 가중치로 나뉜다 · 코 · 눈 · 이는 머리 뼈에 통째로.
- 꼬리는 몽당이라 마디 하나(`Tail`).
실행: python bake_mole.py"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common')); sys.path.insert(0, _here)
import numpy as np
import mole as m
from bake_quad import specs_for, leg_caps, bake

OUT = os.path.join(_here, 'baked')
NAMES = ['snout', 'nose', 'mouth', 'teeth', 'eyes', 'fur', 'arms', 'hands', 'claws_front', 'legs', 'feet', 'claws_back', 'tail']
TAILS = ('Tail',)

def skeleton():
    V = lambda x, y, z: np.array([x, y, z], np.float32)
    sk = [('Body', V(0, 0.50, -0.42), V(0, 0.50, 0.16), 0.30, 0.30),
          ('Head', V(0, 0.50, 0.36), V(0, 0.46, 0.70), 0.22, 0.14), ('Head', V(0, 0.46, 0.70), m.NOSE_C, 0.10, 0.06)]
    sk += leg_caps('LegFR', 'LegFL', [m.SH, m.EL, m.WR, m.PAW_F, m.PAW_F + V(0, 0, 0.17)], [0.11, 0.10, 0.08, 0.07, 0.04])
    sk += leg_caps('LegBR', 'LegBL', [m.HIP, m.KN, m.HEEL, m.PAW_B, m.PAW_B + V(0, 0, 0.15)], [0.12, 0.09, 0.07, 0.06, 0.04])
    sk.append(('Tail', m.TAIL[0], m.TAIL[-1], 0.06, 0.03))
    joints = {'Body': V(0, 0.50, -0.10), 'Head': V(0, 0.50, 0.30), 'LegFR': m.SH, 'LegFL': np.array([-m.SH[0], m.SH[1], m.SH[2]], np.float32),
              'LegBR': m.HIP, 'LegBL': np.array([-m.HIP[0], m.HIP[1], m.HIP[2]], np.float32), 'Tail': m.TAIL[0]}
    return sk, joints

def build():
    P = m._pieces()
    assert len(P) == len(NAMES), '조각 %d · 이름 %d — 미리보기 `_pieces()`가 바뀌었다' % (len(P), len(NAMES))
    specs = specs_for(P, NAMES, TAILS, fine=('teeth', 'claws_front', 'claws_back'), none=('mouth', 'eyes'),
                      blend=('fur',), legs_only=('hands', 'claws_front', 'feet', 'claws_back'), steps={'eyes': 0.0018, 'teeth': 0.0018, 'mouth': 0.002})
    sk, joints = skeleton()
    return [(f, c) for f, c, _ in P], specs, sk, joints

if __name__ == '__main__':
    bake(build, OUT, 'Chibi_Mole', lo=(-0.65, -0.05, -0.95), hi=(0.65, 1.0, 1.05), tail_links=len(TAILS), scale=m.SIZE)
