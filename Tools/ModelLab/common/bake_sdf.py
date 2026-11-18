"""거리장 조각 목록 → 게임 스킨 메시(11-15 — 공룡 악어 · 늑대인간을 유니티로. 엔트 `ent/bake_ent.py`와 같은 길: 수식을 C#으로 손으로 옮기지 않는다).

미리보기 스크립트의 `pieces()`(거리장 f, 색) 목록을 조각마다 표면 그물로 떠서, 정점마다 뼈 둘과 가중치를 적어 파일로 낸다. 유니티 쪽은
`ChibiPawnCreator.Baked`가 읽어 스킨 메시로 뼈에 붙인다(엔트와 같은 파일 꼴 — `ChibiPawnCreator.ImportBakedMesh`).

- **조각마다 메타**(`Spec`) — 뼈 하나에 통째로(`bone` — 머리 조각 · 꼬리) · 고를 뼈(`allow` — 손톱은 손만 · 바지는 엉덩이 · 다리만) · 선(`line`):
  `''` 몸 선 · `'Fine'` 가는 선(이 · 손톱 — 몸 선을 걸면 검은 덩이가 된다) · `'None'` 선 없음(눈동자 · 반짝임 · 입 속 · 눈 테 — 둘레가 살에 묻힌다).
- **뼈대 선**(`skel` — (뼈, a, b, 반지름 a, 반지름 b)) — 정점마다 캡슐 거리의 부드러운 최소로 뼈 둘을 고른다 · 둘째 뼈는 첫째 뼈의 이웃만(`ADJ` — 손 ↔ 다리처럼
  계층이 먼 뼈가 섞이면 걸을 때 그 사이가 늘어난다 · 엔트 11-14).
- **관절 자리**(`joints`) — 게임이 뼈를 거기로 옮긴 뒤 바인드 자세를 잡는다(역관절 다리 · 굽은 등은 사람 뼈 자리에서 돌리면 엉뚱한 축으로 돈다).
- **조각의 자리는 성긴 격자로 찾는다**(3cm) — 미리보기 조각은 자리 정보가 없다(엔트는 조각마다 중심 · 반지름을 들었다).
- **메시는 (머리냐 몸이냐, 선)마다 하나로 묶는다** — 머리(`bone == 'Head'`)는 따로 묶어 줄일 몫을 따로 받는다(몸과 한 메시면 눈 · 코가 먼저 뭉개진다).

파일: `{out}/{메시}.bin.gz` — 점 수 · 세모 수(int32) · 점(f32×3) · 법선(f32×3) · 세모(i32×3) · 색(f32×4 · sRGB · 알파 0) · 뼈 둘(i32×2) · 가중치 둘(f32×2).
목록 `{out}/manifest.json` — items(mesh · part · file · mat · line · verts) · joints(bone · x · y · z)."""
import os, json, gzip, time
from functools import partial
import numpy as np
from surface_nets import surface_nets

BONES = ('Hips', 'Spine', 'ArmL', 'LowerArmL', 'HandL', 'ArmR', 'LowerArmR', 'HandR', 'LegL', 'LowerLegL', 'FootL', 'LegR', 'LowerLegR', 'FootR', 'Head')   # C# `ChibiPawnCreator.B` 순서
SIGMA = 0.03   # 뼈 경계의 부드러움(m)

# 이웃 뼈 — 둘째 뼈는 여기서만.
ADJ = {'Hips': ('Spine', 'LegL', 'LegR'), 'Spine': ('Hips', 'ArmL', 'ArmR', 'Head'), 'Head': ('Spine',)}
for _sd in 'LR':
    ADJ.update({'Arm' + _sd: ('Spine', 'LowerArm' + _sd), 'LowerArm' + _sd: ('Arm' + _sd, 'Hand' + _sd), 'Hand' + _sd: ('LowerArm' + _sd,),
                'Leg' + _sd: ('Hips', 'LowerLeg' + _sd), 'LowerLeg' + _sd: ('Leg' + _sd, 'Foot' + _sd), 'Foot' + _sd: ('LowerLeg' + _sd,)})
ADJ_M = np.zeros((len(BONES), len(BONES)), bool)
for _a, _bs in ADJ.items():
    for _b in _bs: ADJ_M[BONES.index(_a), BONES.index(_b)] = ADJ_M[BONES.index(_b), BONES.index(_a)] = True
NO_HAND = tuple(b for b in BONES if not b.startswith('Hand'))   # 고를 뼈를 안 정한 조각의 기본 — 손가락은 팔 조각만 든다
ARMS = tuple(b for b in BONES if 'Arm' in b or b.startswith('Hand'))
LEGS = tuple(b for b in BONES if 'Leg' in b or b.startswith('Foot'))

def nrm(v): return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-9)

def adjacency(bones, adj):
    """이웃 뼈 표(`ADJ` 꼴 사전) → 행렬."""
    M = np.zeros((len(bones), len(bones)), bool)
    for a, bs in adj.items():
        for b in bs: M[bones.index(a), bones.index(b)] = M[bones.index(b), bones.index(a)] = True
    return M

def weights(V, skel, bone, allow=None, bones=None, adjm=None):
    """정점마다 뼈 둘과 가중치 — 뼈대 선(캡슐) 거리의 부드러운 최소. `bone`을 주면 그 뼈에 통째로 · `allow` 밖의 뼈는 안 고른다 · 둘째 뼈는 이웃만.
    `bones` · `adjm` — 뼈 목록 · 이웃 행렬(안 주면 사람 뼈 열다섯 — 네 발 짐승은 `quad_bones`)."""
    BONES = bones or globals()['BONES']; ADJ_M = adjm if adjm is not None else globals()['ADJ_M']
    n = len(V)
    if bone is not None: return np.full((n, 2), [BONES.index(bone), 0], np.int32), np.tile(np.array([1, 0], np.float32), (n, 1))
    allow = set(allow or (NO_HAND if bones is None else BONES))
    d = np.full((n, len(BONES)), 9.0, np.float32)
    for name, a, b, ra, rb in skel:
        if name not in allow: continue
        a, b = np.asarray(a, np.float32), np.asarray(b, np.float32); ab = b - a
        h = np.clip(((V - a) @ ab) / max(float(ab @ ab), 1e-9), 0, 1)
        dist = np.linalg.norm(V - (a + ab * h[:, None]), axis=1) - (ra + (rb - ra) * h)
        j = BONES.index(name); d[:, j] = np.minimum(d[:, j], dist)
    order = np.argsort(d, 1)[:, :2]; d2 = np.take_along_axis(d, order, 1)
    w = np.exp(-(d2 - d2[:, :1]) / SIGMA); w[w[:, 1] < 0.05, 1] = 0
    w[~ADJ_M[order[:, 0], order[:, 1]], 1] = 0
    w /= w.sum(1, keepdims=True)
    return order.astype(np.int32), w.astype(np.float32)

def color_at(col, V):
    if not callable(col): rgb = np.tile(np.asarray(col, np.float32), (len(V), 1))
    else: rgb = np.asarray(col(V.astype(np.float32)), np.float32).reshape(len(V), -1)[:, :3]
    return np.concatenate([rgb, np.zeros((len(V), 1), np.float32)], 1)

class Spec:
    """조각 하나의 메타 — 이름(로그용) · 선 · 뼈 · 고를 뼈 · 칸.
    고르기(11-15 공룡 악어): `part` — 게임의 렌더러 이름 앞머리(`Spine.Pick.{축}.{벌}` — `PawnLook`이 축으로 읽는다) · `when` — 조건(`@Head.Long@Fins.!On`) ·
    `mat` — 재질(`Paint` 정점 색 그대로 · 그 밖은 팔레트 재질 — 정점 색이 칸 몫이다). `part`를 안 주면 머리 · 몸 한 덩이(`Spine.Baked.{Head|Body}`)."""
    def __init__(self, name, line='', bone=None, allow=None, step=None, part=None, when='', mat='Paint'):
        self.name, self.line, self.bone, self.allow, self.step = name, line, bone, allow, step   # step — 칸(m)을 정해 줄 때(반짝임처럼 상자에 비해 아주 작은 것)
        self.part, self.when, self.mat = part, when, mat

COARSE = 0.03   # 조각의 자리를 찾는 격자(m)

def _piece(build, lo, hi, scale, bones, adj, i):
    """조각 i 하나 — 자리 찾기 → 표면 그물 → 색 · 가중치. 일꾼 프로세스에서 돈다(`build`는 미리보기 모듈을 다시 읽어 목록을 낸다)."""
    pieces, specs, skel, _ = build()
    (f, col), sp = pieces[i], specs[i]
    t0 = time.time()
    g = [np.arange(lo[k], hi[k] + COARSE, COARSE, dtype=np.float32) for k in range(3)]
    P = np.stack(np.meshgrid(*g, indexing='ij'), -1).reshape(-1, 3)
    vals = np.concatenate([f(P[j:j + 200_000]) for j in range(0, len(P), 200_000)])
    hit = P[vals < COARSE * 0.98]   # 미리보기의 `bounded`는 경계 구 밖 3cm부터 구까지의 거리를 낸다 — 문턱이 3cm를 넘으면 그 껍데기가 다 걸린다
    if len(hit) == 0: return None
    plo, phi = hit.min(0) - COARSE * 1.5, hit.max(0) + COARSE * 1.5
    step = sp.step or float(np.clip((phi - plo).max() / 90.0, 0.0025, 0.008))   # 게임이 메시마다 점 수 상한까지 다시 줄인다(`ChibiPawnCreator.Baked`)
    V, N, T = surface_nets(f, plo, phi, step)
    if len(V) == 0: return None
    V = V.astype(np.float32); N = nrm(np.asarray(N, np.float32)); C = color_at(col, V)
    BI, BW = weights(V, skel, sp.bone, sp.allow, bones, adjacency(bones, adj) if bones else None)
    V = V * scale   # 미리보기 눈금에서 재고 키운다(가중치 · 뼈대 선은 미리보기 눈금)
    print('  %-12s 점 %6d · 칸 %.1fmm · %.0f초' % (sp.name, len(V), step * 1000, time.time() - t0), flush=True)
    return V, N, np.asarray(T, np.int32).reshape(-1, 3), C, BI, BW

def run(build, out_dir, prefix, lo, hi, procs=8, scale=1.0, palettes=None, bones=None, adj=None, extra=None):
    """`build()` → (pieces, specs, skel, joints) — 미리보기 모듈 꼭대기 함수여야 한다(일꾼이 다시 부른다). `lo` · `hi` — 온몸을 담는 상자(m · 미리보기 눈금).
    `scale` — 뽑은 메시 · 관절을 이만큼 키운다(크기는 메시가 든다 — 종족 몸집 `BodyScale`은 안 쓴다).
    `palettes` — 게임이 개체마다 고르는 색(`[{name, slots: [{mat, colors: [(r,g,b)…]}]}]` — 칸 순서는 정점 색 rgba).
    `bones` · `adj` — 사람 뼈가 아닌 몸의 뼈 목록 · 이웃(네 발 짐승 — `quad_bones`) · `extra` — 목록에 더 적을 것(뼈 차례 · 부모)."""
    t0 = time.time()
    pieces, specs, skel, joints = build()
    assert len(pieces) == len(specs), '조각 %d · 메타 %d — 미리보기 `pieces()`가 바뀌었다(메타 목록을 맞출 것)' % (len(pieces), len(specs))
    os.makedirs(out_dir, exist_ok=True)
    for fn in os.listdir(out_dir):
        if fn.endswith('.bin.gz'): os.remove(os.path.join(out_dir, fn))
    from multiprocessing import Pool
    with Pool(min(procs, len(pieces))) as pool:
        res = pool.map(partial(_piece, build, np.asarray(lo, np.float32), np.asarray(hi, np.float32), scale, bones, adj), range(len(pieces)))
    groups = {}
    for sp, r in zip(specs, res):
        if r is None: print('  %-12s 비었다' % sp.name); continue
        region = 'Head' if sp.bone == 'Head' else 'Body'
        key = (sp.part or 'Spine.Baked.' + region, sp.when, sp.mat, sp.line, region)
        groups.setdefault(key, []).append(r)
    items = []
    for (part, when, mat, line, region), parts in sorted(groups.items()):
        V, N, T, C, BI, BW, off = [], [], [], [], [], [], 0
        for v, n, t, c, bi, bw in parts: V.append(v); N.append(n); T.append(t + off); C.append(c); BI.append(bi); BW.append(bw); off += len(v)
        V, N, T, C, BI, BW = (np.concatenate(x) for x in (V, N, T, C, BI, BW))
        if part == 'Spine.Baked.' + region and mat == 'Paint' and not when: name = '%s_%s%s' % (prefix, region, line)   # 머리 · 몸 한 덩이(늑대인간 · 옛 이름 그대로)
        else:   # 고르기 조각 — 이름에서 메시 이름을 낸다(`Spine.Pick.Brow.Large` + `@Head.Long@Fins.!On` → `Brow_Large_HeadLong_FinsNotOn`)
            tail = part.replace('Spine.Pick.', '').replace('Spine.Baked.', '').replace('.', '_')
            cond = when.replace('@', '_').replace('.', '').replace('!', 'Not').replace('|', 'Or')
            name = '%s_%s%s_%s%s' % (prefix, tail, cond, mat, line)
        with gzip.open(os.path.join(out_dir, name + '.bin.gz'), 'wb', compresslevel=6) as fh:
            fh.write(np.array([len(V), len(T)], np.int32).tobytes())
            for x in (V, N): fh.write(x.astype(np.float32).tobytes())
            fh.write(T.astype(np.int32).tobytes()); fh.write(C.astype(np.float32).tobytes()); fh.write(BI.astype(np.int32).tobytes()); fh.write(BW.astype(np.float32).tobytes())
        pname = ('Spine.Baked.%s%s%s' % (region, '' if mat == 'Paint' else '.' + mat, line)) if part == 'Spine.Baked.' + region and not when else '%s.%s%s%s' % (part, mat, line, when)
        items.append(dict(mesh=name, part=pname, file=name + '.bin.gz', mat=mat, line=line, region=region, verts=int(len(V))))
    js = [dict(bone=b, x=float(p[0]) * scale, y=float(p[1]) * scale, z=float(p[2]) * scale) for b, p in joints.items()]
    pal = [dict(name=p['name'], slots=[dict(mat=sl['mat'], colors=[dict(r=float(c[0]), g=float(c[1]), b=float(c[2]), a=1.0) for c in sl['colors']]) for sl in p['slots']])
           for p in (palettes or [])]
    json.dump(dict({'items': items, 'joints': js, 'palettes': pal}, **(extra or {})), open(os.path.join(out_dir, 'manifest.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('%s: 메시 %d · 점 %d · 관절 %d · %.0f초' % (prefix, len(items), sum(i['verts'] for i in items), len(js), time.time() - t0))


def quad_bones(tail_links):
    """네 발 짐승 뼈(게임 `PawnRig` 네발 규격 + 꼬리 사슬) — (뼈 차례, 이웃, 부모).
    `Model/Body/{Head, LegFL, LegFR, LegBL, LegBR, Tail/Tail1/…}` — 꼬리 마디는 `TailSway`가 흔든다(네발 클립은 `Body/Tail`만 돌린다)."""
    tails = ['Tail'] + ['Tail%d' % i for i in range(1, tail_links)]
    bones = ('Body', 'Head', 'LegFL', 'LegFR', 'LegBL', 'LegBR') + tuple(tails)
    adj = {'Body': ('Head', 'LegFL', 'LegFR', 'LegBL', 'LegBR', 'Tail')}
    for a, b in zip(tails, tails[1:]): adj[a] = (b,)
    parents = {'Body': '', 'Head': 'Body', 'LegFL': 'Body', 'LegFR': 'Body', 'LegBL': 'Body', 'LegBR': 'Body', 'Tail': 'Body'}
    for a, b in zip(tails, tails[1:]): parents[b] = a
    return bones, adj, parents
