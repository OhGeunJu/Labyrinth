"""네 발 짐승 미리보기 → 게임 스킨 메시(11-18 — 두더지 · 괴물쥐를 유니티로). `bake_sdf.run` 위의 얇은 판.

미리보기 조각은 `(거리 함수, 색, 부위)` — 부위(`Head` · `Body` · `LegF` · `LegB` · `Tail`)가 고를 뼈를 정한다:
  - `Head` 조각은 머리 뼈에 통째로(눈 · 코 · 이 · 귀) — 단, `blend`에 이름이 있으면 머리 · 몸 사이에서 가중치로 나뉜다(목이 이어진 살).
  - `Body` — 몸 뼈(목이 이어진 살이면 머리와 · 꼬리 뿌리와) · `LegF` / `LegB` — 그 다리 둘 + 몸(어깨 · 엉덩이 살) · 발 · 발톱은 다리만.
  - `Tail` — 꼬리 사슬(뿌리는 몸).
선은 이름으로 — `fine`(이 · 발톱 · 수염 — 가는 선) · `none`(눈 · 반짝임 · 입 속 — 둘레가 살에 묻힌다) · 그 밖은 몸 선.
목록에는 뼈 차례 · 부모(`bones` · `parents`)를 더 적는다 — 유니티 `ChibiPawnCreator.Quad`가 그대로 관절을 세운다."""
import numpy as np
from bake_sdf import Spec, run, quad_bones

FRONT = ('LegFL', 'LegFR'); BACK = ('LegBL', 'LegBR')

def specs_for(pieces, names, tails, fine=(), none=(), blend=(), legs_only=(), steps=None, thin=()):
    """조각마다 메타 — `names`는 조각 차례의 이름(미리보기 함수 이름) · `steps` — 칸(m)을 정해 줄 조각(반짝임처럼 아주 작은 것)."""
    out = []
    for (f, col, part), nm in zip(pieces, names):
        line = 'Fine' if nm in fine else 'None' if nm in none else 'Thin' if nm in thin else ''   # Thin — 얇은 판(쥐 귀): 가는 선을 살 뒤로 민다
        if part == 'Head':
            out.append(Spec(nm, line, allow=('Head', 'Body')) if nm in blend else Spec(nm, line, 'Head'))
        elif part == 'Body':
            out.append(Spec(nm, line, allow=('Body', 'Head', 'Tail') if nm in blend else ('Body', 'Tail')))
        elif part in ('LegF', 'LegB'):
            legs = FRONT if part == 'LegF' else BACK
            out.append(Spec(nm, line, allow=legs if nm in legs_only else legs + ('Body',)))
        elif part == 'Tail':
            out.append(Spec(nm, line, allow=('Body',) + tuple(tails)))
        else: raise ValueError('모르는 부위 %s (%s)' % (part, nm))
        out[-1].step = (steps or {}).get(nm)
    return out

def mirror_x(v): return np.array([-v[0], v[1], v[2]], np.float32)

def leg_caps(name_r, name_l, pts, radii):
    """다리 한 쌍의 뼈대 선 — 미리보기 +x 쪽 점들(게임의 오른쪽 · R) · 거울이 L."""
    out = []
    for i in range(len(pts) - 1):
        a, b = np.asarray(pts[i], np.float32), np.asarray(pts[i + 1], np.float32)
        out.append((name_r, a, b, radii[i], radii[i + 1])); out.append((name_l, mirror_x(a), mirror_x(b), radii[i], radii[i + 1]))
    return out

def bake(build, out, prefix, lo, hi, tail_links, scale=1.0):
    bones, adj, parents = quad_bones(tail_links)
    run(build, out, prefix, lo, hi, scale=scale, bones=bones, adj=adj,
        extra={'bones': list(bones), 'parents': [dict(bone=b, parent=parents[b]) for b in bones]})
