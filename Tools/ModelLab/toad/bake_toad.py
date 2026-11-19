"""포자두꺼비를 게임 메시로 뽑는다(11-19 사용자: *"ㄱㄱ"* — 미리보기 v7 확정 뒤).

미리보기 `toad._pieces()`(단위 몸)를 그대로 떠서(`common/bake_quad.py` → `bake_sdf.py`) `baked/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/23`.
**모양을 고치면 미리보기를 고치고 → `python bake_toad.py` → `Pawn/23`.**
- 크기는 뽑을 때 `toad.SIZE`(1.85)배 — 머리 꼭대기가 사람 키쯤. 뼈대 선 · 관절은 단위 몸에서 적는다.
- 머리 + 몸이 한 살(`skin` — 포자 주머니 · 사마귀 · 입 홈까지)이라 머리 · 몸 사이에서 가중치로 나뉜다 · 눈 · 입 속은 머리 뼈에 통째로.
- 꼬리는 없다 — 네발 규격이 꼬리 관절 하나를 요구해 엉덩이 뒤에 둘 뿐(아무 살도 안 고른다).
실행: python bake_toad.py"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common')); sys.path.insert(0, _here)
import numpy as np
import toad as t
from bake_quad import specs_for, leg_caps, bake

OUT = os.path.join(_here, 'baked')
NAMES = ['eyes', 'mouth', 'tusks', 'skin', 'arms', 'legs']
TAILS = ('Tail',)

def skeleton():
    V = lambda x, y, z: np.array([x, y, z], np.float32)
    sk = [('Body', V(0, 0.38, -0.48), V(0, 0.46, 0.14), 0.40, 0.40),
          ('Head', V(0, 0.50, 0.28), V(0, 0.44, 0.66), 0.30, 0.18)]
    sk += leg_caps('LegFR', 'LegFL', [t.SH, t.EL, t.WR, t.HAND, t.HAND + V(0.03, 0, 0.12)], [0.09, 0.075, 0.06, 0.05, 0.03])
    sk += leg_caps('LegBR', 'LegBL', [t.HIP, t.KNEE, t.ANK, t.FOOT, t.FOOT + V(0.06, 0, 0.18)], [0.15, 0.09, 0.06, 0.05, 0.03])
    joints = {'Body': V(0, 0.45, -0.10), 'Head': V(0, 0.52, 0.26), 'LegFR': t.SH, 'LegFL': np.array([-t.SH[0], t.SH[1], t.SH[2]], np.float32),
              'LegBR': t.HIP, 'LegBL': np.array([-t.HIP[0], t.HIP[1], t.HIP[2]], np.float32), 'Tail': V(0, 0.32, -0.62)}
    return sk, joints

def build():
    P = t._pieces()
    assert len(P) == len(NAMES), '조각 %d · 이름 %d — 미리보기 `_pieces()`가 바뀌었다' % (len(P), len(NAMES))
    specs = specs_for(P, NAMES, TAILS, fine=('tusks',), none=('mouth', 'eyes'), blend=('skin',), steps={'eyes': 0.0018, 'mouth': 0.002, 'tusks': 0.0018})
    sk, joints = skeleton()
    return [(f, c) for f, c, _ in P], specs, sk, joints

if __name__ == '__main__':
    bake(build, OUT, 'Chibi_Toad', lo=(-0.72, -0.05, -0.80), hi=(0.72, 1.05, 0.90), tail_links=len(TAILS), scale=t.SIZE)
