"""뱀파이어를 게임 메시로 뽑는다(11-18 사용자: 미리보기 확정 뒤 *"ㄱㄱ"* — 늑대인간 · 공룡 악어와 같은 길: 미리보기 수식을 C#으로 손으로 옮기지 않는다).

미리보기 `vampire.py` · `lord_hair.py`의 조각을 그대로 떠서(`common/bake_sdf.py`) `baked/{몸}/`(깃 밖)에 낸다 → 유니티 `DungeonColony/Pawn/22`가 읽어 스킨 메시로 뼈에 붙인다.
**모양을 고치면 미리보기를 고치고 → `python bake_vampire.py` → `Pawn/22`.** 미리보기 판 — 노예 v15 · 로드 머리 v48.
몸 넷 — 로드 남 · 여(`lord` · `lordF`) · 노예 남 · 여(`thrall` · `thrallF`). 몸은 사람 치비 몸 그대로(머리가 뜬다).
- **로드 머리는 고르기 축**: `Spine.Pick.Hair.{Curtain|Slick|Long|LowTail}` × `Spine.Pick.Bangs.{On|Off}`(더듬이 — 장발에는 안 붙는다 `@Hair.!Long`).
  `Off`는 렌더러가 없다(머리 조각의 조건 `@Bangs.On|Off`에만 나온다 — `PawnLook`은 조건에만 나오는 벌도 축에 넣는다).
- **머리색은 팔레트**(`mat='HairPal'` — `Hair`는 사람 머리카락 재질 이름과 겹쳐 팔레트 재질이 안 생겼다(11-19) — 정점 색 빨강이 칸 0 몫 · 명암은 그 몫의 크기) · 색은 개체마다 `PaletteLook`이 고른다(먹 · 은 · 갈색 · 적갈 · 포도주).
- 얼굴은 3mm 칸 · 머리카락 4mm · 더듬이 3mm(기본 칸 8mm는 코 · 얼굴이 뭉개진다) · 머리 · 얼굴 조각은 머리 뼈에 통째로 · 망토 · 깃은 등뼈 · 엉덩이 · 치마는 엉덩이에 굳는다(다리를 따라 늘어나지 않게).
실행: python bake_vampire.py [lord,lordF,thrall,thrallF]"""
import os, sys
_here = os.path.dirname(os.path.abspath(__file__))
for _p in (os.path.join(_here, '..', 'common'), os.path.join(_here, '..', 'dwarf'), os.path.join(_here, '..', 'human'), _here):
    if _p not in sys.path: sys.path.insert(0, _p)
import numpy as np
import vampire as vp
import lord_hair as lh
from bake_sdf import Spec, run, ARMS, LEGS

OUT = os.path.join(_here, 'baked')
HANDS = tuple(b for b in ARMS if b.startswith('Hand'))
TRUNK = ('Hips', 'Spine')
STYLES = (('curtain_plain', 'Curtain'), ('slick', 'Slick'), ('long', 'Long'), ('lowtail', 'LowTail'))
HAIR_PALETTES = [('Ink', vp.INK), ('Silver', (0.70, 0.70, 0.73)), ('Brown', (0.36, 0.25, 0.16)), ('Auburn', (0.30, 0.17, 0.11)), ('Wine', vp.WINE)]

# 머리카락은 부풀린 머리 통째(머리 속은 얼굴 살이 가린다 — 사람 머리카락 굽기와 같다). 11-18에 살갗 바깥 껍데기만 떴더니 얇은 껍데기 사이로
# 몸 외곽선이 새어 나와 더 나빴다 — 되돌렸다. 머리카락 선은 `HangLine`(`ChibiPawnCreator.Vampire`가 갈아 끼운다).
def L(v): return np.array([-v[0], v[1], v[2]], np.float32)

def wrap(v, f, r=1.2):     # 머리 좌표 → 몸 좌표(미리보기 `hd`와 같다)
    return lambda p: vp.bounded(lambda q: f(q - v.HO), p, v.HO, r)
def shade(v, k_fn):        # 팔레트 몫 — 빨강(칸 0)에 명암을 싣는다
    return lambda p: np.stack([k_fn(p - v.HO), np.zeros(len(p), np.float32), np.zeros(len(p), np.float32)], -1)

def lord_list(female):
    v = vp.VARIANTS['lordF' if female else 'lord']; vp.set_body(female); h = v.h
    P = list(zip(vp.lord_head(v)[:6], [Spec('face', '', 'Head', step=0.003), Spec('eyes', 'None', 'Head', step=0.0015), Spec('glint', 'None', 'Head', step=0.0012),
                                        Spec('brows', 'None', 'Head', step=0.0015), Spec('mouth', 'None', 'Head', step=0.0015), Spec('fangs', 'Fine', 'Head', step=0.0015)]))
    P = [(f, c, s) for (f, c), s in P]
    for style, name in STYLES:                                                                 # 머리 네 벌
        f, deco, _ = lh.build(h, style)
        st = None if style == 'long' else lh.strands_of(h, style)[0]
        kc = lh.hair_col((1.0, 1.0, 1.0), st, folds=style == 'long')
        P.append((wrap(v, lambda p, f=f: vp.safe_crown(f(p), p)), shade(v, lambda q, kc=kc: kc(q)[..., 0]),
                  Spec('hair_' + name, '', 'Head', step=0.004, part='Spine.Pick.Hair.' + name, when='@Bangs.On|Off', mat='HairPal')))
        if deco is not None:
            P.append((wrap(v, deco), vp.DECO, Spec('tie', '', 'Head', part='Spine.Pick.Hair.' + name)))
    locks = lh.face_locks(h, lh.lift_of(h, 'curtain'))                                        # 더듬이 두 갈래(따로 된 조각)
    P.append((wrap(v, lambda p: np.minimum(locks[0].sdf(p), locks[1].sdf(p))), shade(v, lambda q: np.full(len(q), 0.97, np.float32)),
              Spec('bangs', '', 'Head', step=0.003, part='Spine.Pick.Bangs.On', when='@Hair.!Long', mat='HairPal')))
    body = [(vp.collar, vp.collar_col, Spec('collar', '', allow=('Spine',))), (vp.cape, vp.cape_col, Spec('cape', '', allow=TRUNK)),
            (vp.mantle, vp.COAT, Spec('mantle', '', allow=TRUNK)), (vp.sleeves, vp.COAT, Spec('sleeves', '', allow=('Spine',) + ARMS)),
            (vp.cuffs, vp.LACE, Spec('cuffs', '', allow=ARMS)), (vp.hands, vp.PALE, Spec('hands', '', allow=ARMS))]
    if not female:
        body += [(vp.torso, vp.VEST, Spec('vest', '', allow=TRUNK)), (vp.coat, vp.COAT, Spec('coat', '', allow=TRUNK)), (vp.lapels, vp.SATIN, Spec('lapels', '', allow=TRUNK)),
                 (vp.buttons, vp.GOLD, Spec('buttons', 'Fine', allow=TRUNK)), (vp.jabot, vp.LACE, Spec('jabot', '', allow=('Spine',))),
                 (vp.brooch, vp.GEM, Spec('brooch', 'Fine', allow=('Spine',))), (vp.brooch_rim, vp.GOLD, Spec('brooch_rim', 'Fine', allow=('Spine',))),
                 (vp.legs, vp.PANTS, Spec('legs', '', allow=('Hips',) + LEGS)), (vp.boots, vp.BOOT, Spec('boots', '', allow=LEGS))]
    else:
        body += [(vp.torso, vp.PALE, Spec('skin', '', allow=TRUNK)), (vp.corset, vp.VEST, Spec('corset', '', allow=TRUNK)), (vp.laces, vp.GOLD, Spec('laces', 'Fine', allow=TRUNK)),
                 (lambda p: vp.jabot(p, 0.75), vp.LACE, Spec('jabot', '', allow=('Spine',))), (vp.brooch, vp.GEM, Spec('brooch', 'Fine', allow=('Spine',))),
                 (vp.brooch_rim, vp.GOLD, Spec('brooch_rim', 'Fine', allow=('Spine',))), (vp.gown, vp.gown_col, Spec('gown', '', allow=('Hips',)))]
    return P + body

def thrall_list(female):
    v = vp.VARIANTS['thrallF' if female else 'thrall']; vp.set_body(female)
    head = vp.thrall_head(v)
    hs = [Spec('face', '', 'Head', step=0.003), Spec('eyes', 'None', 'Head', step=0.0015), Spec('eye_rim', 'None', 'Head', step=0.0012), Spec('eye_core', 'None', 'Head', step=0.0012),
          Spec('mouth', 'None', 'Head', step=0.0015)]
    P = [(f, c, s) for (f, c), s in zip(head, hs)]
    body = [(vp.torso, vp.BLUE, Spec('skin', '', allow=TRUNK)), (vp.tunic, vp.tunic_col, Spec('tunic', '', allow=TRUNK)), (vp.rope, vp.ROPE, Spec('rope', '', allow=TRUNK)),
            (vp.arms, vp.BLUE, Spec('arms', '', allow=('Spine',) + ARMS)), (vp.rags, vp.tunic_col, Spec('rags', '', allow=('Spine',) + ARMS)),
            (vp.hands, vp.BLUE, Spec('hands', '', allow=ARMS)), (vp.shackles, vp.IRON, Spec('shackles', '', allow=ARMS)),
            (vp.legs, vp.RAG_D if not female else vp.BLUE, Spec('legs', '', allow=('Hips',) + LEGS)), (vp.wraps, vp.WRAP, Spec('wraps', '', allow=LEGS))]
    if female: body.append((vp.rag_skirt, vp.tunic_col, Spec('skirt', '', allow=('Hips',))))
    return P + body

def skeleton(v):
    """사람 치비 몸의 뼈대 선 · 관절 — 팔다리는 곧은 통(`vp.arms` · `vp.legs`)이라 가운데를 팔꿈치 · 무릎으로 친다."""
    D, TOP, HO = vp.D, vp.TOP, v.HO
    sk = []; yh = D['hipY'] + 0.05
    sk.append(('Hips', np.array([0, D['lift'], 0.0], np.float32), np.array([0, yh, 0.0], np.float32), D['half'][1], D['half'][2]))
    sk.append(('Spine', np.array([0, yh, 0.0], np.float32), np.array([0, TOP, 0.0], np.float32), D['half'][2], D['half'][4] * 0.9))
    neck = np.array([0, TOP + 0.02, 0.02], np.float32)
    sk.append(('Head', neck, HO, 0.14, 0.30))
    ax = D['armX']; sh = np.array([ax, D['shY'], 0], np.float32); wr = np.array([ax, D['handY'], 0], np.float32); el = (sh + wr) * 0.5
    hc = np.asarray(vp.HAND_C, np.float32); r = D['armR']
    lx = D['legX']; hp = np.array([lx, D['hipY'], 0], np.float32); ft = np.array([lx, D['footY'], 0], np.float32); kn = (hp + ft) * 0.5
    arm = [('Arm', sh, el, r * 1.1, r), ('LowerArm', el, wr, r, r), ('Hand', wr, hc, r, vp.HAND_R)]
    leg = [('Leg', hp, kn, D['legR'], D['legR']), ('LowerLeg', kn, ft, D['legR'], D['legR']), ('Foot', ft, ft + np.array([0, 0, 0.12], np.float32), D['legR'], D['legR'])]
    for name, a, b, ra, rb in arm + leg:
        sk.append((name + 'R', a, b, ra, rb)); sk.append((name + 'L', L(a), L(b), ra, rb))
    joints = {'Hips': np.array([0, D['hipY'], 0.0]), 'Spine': np.array([0, yh, 0.0]), 'Head': neck}
    for sd, f in (('R', lambda x: np.asarray(x, np.float32)), ('L', L)):
        joints.update({'Arm' + sd: f(sh), 'LowerArm' + sd: f(el), 'Hand' + sd: f(wr), 'Leg' + sd: f(hp), 'LowerLeg' + sd: f(kn), 'Foot' + sd: f(ft)})
    return sk, joints

def _build(kind):
    female = kind.endswith('F'); v = vp.VARIANTS[kind]
    lst = lord_list(female) if kind.startswith('lord') else thrall_list(female)
    vp.set_body(female)
    sk, joints = skeleton(v)
    return [(f, c) for f, c, _ in lst], [s for _, _, s in lst], sk, joints
# 일꾼이 다시 부르는 꼭대기 함수(몸마다 하나)
def build_lord(): return _build('lord')
def build_lordF(): return _build('lordF')
def build_thrall(): return _build('thrall')
def build_thrallF(): return _build('thrallF')

KINDS = {'lord': (build_lord, 'Chibi_VampireLord'), 'lordF': (build_lordF, 'Chibi_VampireLordF'),
         'thrall': (build_thrall, 'Chibi_VampireThrall'), 'thrallF': (build_thrallF, 'Chibi_VampireThrallF')}

if __name__ == '__main__':
    names = sys.argv[1].split(',') if len(sys.argv) > 1 else list(KINDS)
    pal = [dict(name=n, slots=[dict(mat='HairPal', colors=[c])]) for n, c in HAIR_PALETTES]
    for n in names:
        b, prefix = KINDS[n]
        run(b, os.path.join(OUT, n), prefix, lo=(-0.85, -0.05, -0.85), hi=(0.85, 2.30, 0.95), palettes=pal if n.startswith('lord') else None)
