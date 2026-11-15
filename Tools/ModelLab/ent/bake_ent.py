"""엔트를 게임 메시로 뽑는다(11-14 사용자: *"유니티로 옮겨봐"* — 사람 머리카락 `bake_hair.py`와 같은 길: 미리보기 수식을 C#으로 손으로 옮기지 않는다).

가지 · 잎은 난수로 자라 C#으로 옮기면 미리보기와 똑같이 못 맞춘다 — 그래서 여기서 메시를 뽑고, 유니티 `DungeonColony/Pawn/17`이 읽어
스킨 메시로 뼈에 붙인다. **종류마다 한 벌**(`ent.ORDER`) · 장식과 잎 모양 넷(`ent.LEAF_SHAPES`)은 다 지은 채 뽑는다(게임에서 고르기 축).
**계절 색은 메시에 안 굽는다**(11-14 사용자: *"색만 다른데 왜 프리팹으로 나뉘어"* → 모양 축 × 색 조합) — 잎 · 이끼 · 덩굴 · 겨우살이 · 버들가지는
`ent.PAL['code']`(색 자리에 칸 번호)로 뽑아 정점 색이 곧 "칸마다 몫"이 되고, 게임이 개체마다 팔레트(목록의 `palettes` ← `ent.PALETTES`)를 골라 칠한다
(`PaletteLook` · 셰이더 `_VertexPal`). 껍질의 이끼 얼룩 색(`_AltColor`)도 팔레트가 든다.

- **뼈 가중치는 정점마다**(`weights`) — `ent.build`가 내놓는 뼈대 선(`skel` — 줄기 · 다리 · 팔 · 손가락 · 곁가지)의 캡슐 거리로 가장 가까운 뼈 둘을
  고르고 경계에서 부드럽게 나눈다. 관 · 얼굴 · 수염 · 겨우살이는 등뼈에 통째로(`meta`의 뼈). **조각마다 고를 수 있는 뼈가 있고**(`meta`의 `allow` —
  줄기는 다리까지 · 팔은 제 팔만 · 나머지는 손을 뺀 전부) **둘째 뼈는 첫째 뼈의 이웃만**(`ADJ`) — 거리만 보던 때는 다리 옆까지 늘어진 손가락이
  다리 뼈를 받아 걸을 때 손과 다리가 붙어 늘어났다(11-14 사용자: *"다리하고 손이 붙어서 늘어나"*).
- **관절 자리도 같이 낸다**(목록의 `joints`) — 게임이 뼈를 거기로 옮긴 뒤 바인드 자세를 잡는다. 엔트의 다리 · 팔은 바깥으로 벌어져 있어
  사람 뼈 자리(곧게 아래)에서 돌리면 엉뚱한 축으로 돌아 늘어난다.
- **법선은 거리장의 기울기**(표면 그물이 낸 것) · 대 · 잎은 면 법선의 평균 — 게임이 줄인 메시에 옮겨 쓴다(줄인 면에서 다시 재면 울퉁불퉁해 외곽선이 갈라졌다).
- **색은 정점마다** — 껍질 조각(`bark_cols`)은 (무늬 몫 rgb, 이끼 몫 a) — 게임이 껍질 색(개체마다 `SkinLook`)과 이끼 색(재질 `_AltColor`)을 얹는다.
  나머지(잎 · 눈 · 이끼 · 덩굴 …)는 색 그대로(알파 0).
- **가는 것은 도형으로 직접 짓는다** — 잔가지 · 버들가지 · 수염(마디 → 여섯 모 대) · 잎(납작한 물방울). 표면 그물로는 종이처럼 얇은 잎과
  8mm 대가 뭉개진다. 굵은 것(줄기 · 팔 · 잎 뭉치 · 이끼 · 옹이 · 눈)은 표면 그물(`common/surface_nets.py`).
  **가는 것은 메시를 따로 묶는다**(`line` = 'Fine') — 게임이 가는 선을 건다(몸 선을 1cm 잔가지에 걸면 검은 띠가 된다).
- 조각을 (장식 갈래, 재질, 선)마다 한 메시로 묶는다 — 몸 `Spine.Ent.{Bark|Paint}{|Fine}`, 장식 `Spine.Pick.{갈래}.On.{Bark|Paint}{|Fine}@{갈래}.!Off`.

파일: `baked/{메시}.bin.gz` — 점 수 · 세모 수(int32) · 점(f32×3) · 법선(f32×3) · 세모(i32×3) · 색(f32×4) · 뼈 둘(i32×2) · 가중치 둘(f32×2). 목록 `baked/manifest.json`.
실행: python bake_ent.py [종류 oak,slim]   (준 종류만 다시 뽑는다 · 목록의 나머지는 그대로)"""
import os, sys, json, gzip, time
import numpy as np
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, '..', 'common'))
from surface_nets import surface_nets
import ent

OUT = os.path.join(_here, 'baked')
BONES = ('Hips', 'Spine', 'ArmL', 'LowerArmL', 'HandL', 'ArmR', 'LowerArmR', 'HandR', 'LegL', 'LowerLegL', 'FootL', 'LegR', 'LowerLegR', 'FootR', 'Head')   # C# `ChibiPawnCreator.B` 순서
AXIS = {'moss': 'Moss', 'beard': 'Beard', 'vine': 'Vine', 'mistle': 'Mistle', **{'knot%d' % i: 'Knot%d' % i for i in range(1, ent.KNOTS + 1)}}
PAL_MAT = {'leaf': 'Leaf', 'moss': 'Moss', 'vine': 'Vine', 'mistle': 'Mistle'}   # 팔레트 재질 — 칸은 `ent.PAL['code']`의 머리말
SIGMA = 0.03   # 뼈 경계의 부드러움(m)
# 크기 — 미리보기 그대로 뽑아 이만큼 키운다(점 · 관절 · 가중치는 키우기 전에 잰다). 11-14 사용자: *"scale 키우지 말고 처음부터 크기를 키워줘 —
# 폰 하나가 다리 길이 정도로"* — 엔트 다리(엉덩이 관절 → 땅) 1.07m × 1.7 ≈ 사람 폰 키 1.84m.
SCALE = 1.7

# 이웃 뼈 — 둘째 뼈는 여기서만(손 ↔ 다리처럼 계층으로 먼 뼈끼리 섞이면 걸을 때 그 사이가 늘어난다).
ADJ = {'Hips': ('Spine', 'LegL', 'LegR'), 'Spine': ('Hips', 'ArmL', 'ArmR', 'Head'), 'Head': ('Spine',)}
for sd in 'LR':
    ADJ.update({'Arm' + sd: ('Spine', 'LowerArm' + sd), 'LowerArm' + sd: ('Arm' + sd, 'Hand' + sd), 'Hand' + sd: ('LowerArm' + sd,),
                'Leg' + sd: ('Hips', 'LowerLeg' + sd), 'LowerLeg' + sd: ('Leg' + sd, 'Foot' + sd), 'Foot' + sd: ('LowerLeg' + sd,)})
ADJ_M = np.zeros((len(BONES), len(BONES)), bool)
for _a, _bs in ADJ.items():
    for _b in _bs: ADJ_M[BONES.index(_a), BONES.index(_b)] = ADJ_M[BONES.index(_b), BONES.index(_a)] = True
NO_HAND = tuple(b for b in BONES if not b.startswith('Hand'))   # 고를 뼈를 안 정한 조각 — 손가락은 팔 조각만 든다

def nrm(v): return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-9)

def orient(V, T, out_of):
    """세모의 앞면이 바깥(`out_of(무게중심) → 바깥 방향`)을 보게 감김을 맞춘다."""
    T = np.asarray(T, np.int32).reshape(-1, 3); a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    flip = (np.cross(b - a, c - a) * out_of((a + b + c) / 3, T)).sum(-1) < 0
    T[flip] = T[flip][:, [0, 2, 1]]
    return T

def tubes(segs, sides=6):
    """마디마다 여섯 모 대 + 양 끝 꼭지(이음매가 비지 않게 반지름만큼 내민다)."""
    V, T, AX = [], [], []
    for a, b, ra, rb in segs:
        a, b = np.asarray(a, np.float32), np.asarray(b, np.float32); d = b - a; L = np.linalg.norm(d)
        if L < 1e-5: continue
        d /= L; e1 = np.cross(d, [0.3, 0.9, 0.1]); e1 /= np.linalg.norm(e1); e2 = np.cross(d, e1)
        o = len(V)
        for c, r in ((a, ra), (b, rb)):
            for k in range(sides):
                th = 2 * np.pi * k / sides; V.append(c + (e1 * np.cos(th) + e2 * np.sin(th)) * r)
        V += [a - d * ra * 0.8, b + d * rb * 0.8]
        for k in range(sides):
            k1 = (k + 1) % sides
            T += [(o + k, o + k1, o + sides + k1), (o + k, o + sides + k1, o + sides + k), (o + 2 * sides, o + k1, o + k), (o + 2 * sides + 1, o + sides + k, o + sides + k1)]
        AX += [(a, b)] * (sides * 4)
    if not V: return None
    V = np.array(V, np.float32); AXA = np.array(AX, np.float32)
    def out_of(cen, _T):
        a, b = AXA[:, 0], AXA[:, 1]; ab = b - a; h = np.clip(((cen - a) * ab).sum(-1) / np.maximum((ab * ab).sum(-1), 1e-9), 0, 1)
        return cen - (a + ab * h[:, None])
    return V, orient(V, T, out_of)

def leaf_mesh(data):
    """잎 — 미리보기 `leaves`와 같은 꼴(밑동 0 → 40%에서 가장 넓다 → 끝 0 · 두께는 폭의 0.22배)을 고리 일곱 × 여섯 모로."""
    A, Dr, Up, Sd, LN, WD, CI = data
    hs = np.array([0.0, 0.1, 0.25, 0.4, 0.6, 0.8, 1.0], np.float32); sides = 6
    V, T, C = [], [], []
    for i in range(len(A)):
        o = len(V); r = WD[i] * np.sin(np.pi * hs ** 0.75) + 0.004
        for h, rr in zip(hs, r):
            c = A[i] + Dr[i] * (h * LN[i])
            for k in range(sides):
                th = 2 * np.pi * k / sides; V.append(c + Sd[i] * (rr * np.cos(th)) + Up[i] * (rr * 0.22 * np.sin(th)))
        V += [A[i] - Dr[i] * 0.004, A[i] + Dr[i] * (LN[i] + 0.004)]; C += [CI[i]] * (len(hs) * sides + 2)
        n = len(hs)
        for j in range(n - 1):
            for k in range(sides):
                k1 = (k + 1) % sides; p, q = o + j * sides, o + (j + 1) * sides
                T += [(p + k, p + k1, q + k1), (p + k, q + k1, q + k)]
        for k in range(sides):
            k1 = (k + 1) % sides; T += [(o + n * sides, o + k1, o + k), (o + n * sides + 1, o + (n - 1) * sides + k, o + (n - 1) * sides + k1)]
    V = np.array(V, np.float32)
    leaf_of = np.repeat(np.arange(len(A)), (len(hs) - 1) * sides * 2 + 2 * sides)
    def out_of(cen, _T):
        a, d, ln = A[leaf_of], Dr[leaf_of], LN[leaf_of]; h = np.clip(((cen - a) * d).sum(-1), 0, ln)
        return cen - (a + d * h[:, None])
    return V, orient(V, T, out_of), np.array(C, np.float32)

def face_normals(V, T):
    """넓이 가중 면 법선의 평균(대 · 잎 — 감김은 `orient`가 바깥으로 맞췄다)."""
    N = np.zeros_like(V); f = np.cross(V[T[:, 1]] - V[T[:, 0]], V[T[:, 2]] - V[T[:, 0]])
    for k in range(3): np.add.at(N, T[:, k], f)
    return nrm(N).astype(np.float32)

def step_of(rad): return float(np.clip(rad / 100.0, 0.004, 0.014))   # 유니티가 다시 줄인다(`Pawn/17` — 메시마다 점 수 상한)

def weights(V, skel, bone, allow=None):
    """정점마다 뼈 둘과 가중치 — 뼈대 선(캡슐) 거리의 부드러운 최소. `bone`을 주면 그 뼈에 통째로 · `allow` 밖의 뼈는 안 고른다 · 둘째 뼈는 이웃만."""
    n = len(V)
    if bone is not None: return np.full((n, 2), [BONES.index(bone), 0], np.int32), np.tile(np.array([1, 0], np.float32), (n, 1))
    allow = set(allow or NO_HAND)
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

def color_at(col, V, bark):
    if not callable(col): rgb = np.tile(np.asarray(col, np.float32), (len(V), 1)); return np.concatenate([rgb, np.zeros((len(V), 1), np.float32)], 1)
    c = col(V.astype(np.float32))
    return c if c.shape[1] == 4 else np.concatenate([c, np.zeros((len(V), 1), np.float32)], 1)

def palettes():
    """게임이 고르는 팔레트 — 재질마다 칸 색(sRGB). 칸 순서는 `ent.PAL['code']`와 같다."""
    out = []
    for name in ent.PALETTES:
        P = ent.PAL[name]; c = lambda x: dict(r=float(x[0]), g=float(x[1]), b=float(x[2]), a=1.0)
        out.append(dict(name=name, slots=[dict(mat='Leaf', colors=[c(x) for x in P['leaf']]),
                                          dict(mat='Moss', colors=[c(P['moss'][0]), c(P['moss'][1]), c(P['lichen'])]),
                                          dict(mat='Vine', colors=[c(P['strand'][0]), c(P['strand'][1]), c(P['vine'])]),
                                          dict(mat='Mistle', colors=[c(P['mistle'])]),
                                          dict(mat='BarkAlt', colors=[c(P['moss'][0])])]))
    return out

def bake(kind):
    t0 = time.time()
    m = ent.build(kind, 'code', ent.DECOR, split=True, leaf=None)
    bark_cols = {id(c) for c in m['bark_cols']}
    groups = {}
    for (f, col), (tag, bone, allow, pal) in zip(m['pieces'], m['meta']):
        bark = id(col) in bark_cols; fine = True
        if hasattr(f, 'leafdata'):
            V, T, C = leaf_mesh(f.leafdata); C = np.concatenate([C, np.zeros((len(C), 1), np.float32)], 1); N = face_normals(V, T)
        elif hasattr(f, 'segs'):
            r = tubes(f.segs)
            if r is None: continue
            V, T = r; C = color_at(col, V, bark); N = face_normals(V, T)
        else:
            lo, hi = f.c - f.rad, f.c + f.rad
            V, N, T = surface_nets(f, lo, hi, step_of(f.rad))
            if len(V) == 0: continue
            V = V.astype(np.float32); N = nrm(np.asarray(N, np.float32)); C = color_at(col, V, bark); fine = False
        BI, BW = weights(V, m['skel'], bone, allow)
        key = (tag, PAL_MAT[pal] if pal else 'Bark' if bark else 'Paint', 'Fine' if fine else '')
        groups.setdefault(key, []).append((V, N, np.asarray(T, np.int32).reshape(-1, 3), C, BI, BW))
    items = []
    for (tag, mat, line), parts in groups.items():
        V, N, T, C, BI, BW, off = [], [], [], [], [], [], 0
        for v, nn, t, c, bi, bw in parts: V.append(v); N.append(nn); T.append(t + off); C.append(c); BI.append(bi); BW.append(bw); off += len(v)
        V, N, T, C, BI, BW = (np.concatenate(x) for x in (V, N, T, C, BI, BW))
        V = V * SCALE
        name = 'Chibi_Ent_%s_%s_%s%s' % (kind.capitalize(), tag, mat, line)
        if tag == 'core': part = 'Spine.Ent.%s%s' % (mat, line)
        elif tag.startswith('leaf'): part = 'Spine.Pick.Leaf.%s.%s%s' % (tag[4:], mat, line)   # 잎 모양 축 — 넷 중 하나가 켜진다
        else: part = 'Spine.Pick.%s.On.%s%s@%s.!Off' % (AXIS[tag], mat, line, AXIS[tag])
        with gzip.open(os.path.join(OUT, name + '.bin.gz'), 'wb', compresslevel=6) as fh:
            fh.write(np.array([len(V), len(T)], np.int32).tobytes())
            for x in (V, N): fh.write(x.astype(np.float32).tobytes())
            fh.write(T.astype(np.int32).tobytes()); fh.write(C.astype(np.float32).tobytes()); fh.write(BI.astype(np.int32).tobytes()); fh.write(BW.astype(np.float32).tobytes())
        items.append(dict(mesh=name, part=part, file=name + '.bin.gz', mat=mat, line=line, kind=kind, verts=int(len(V))))
    joints = [dict(kind=kind, bone=b, x=float(p[0]) * SCALE, y=float(p[1]) * SCALE, z=float(p[2]) * SCALE) for b, p in m['joints'].items()]
    print('%s: 메시 %d · 점 %d · %.0f초' % (kind, len(items), sum(i['verts'] for i in items), time.time() - t0), flush=True)
    return items, joints

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    only = [a for a in sys.argv[1:] if not a.startswith('--')]
    kinds = only[0].split(',') if only else ent.ORDER
    man_path = os.path.join(OUT, 'manifest.json')
    old = json.load(open(man_path, encoding='utf-8')) if os.path.exists(man_path) else {}
    items = [i for i in old.get('items', []) if i['kind'] not in kinds]
    joints = [j for j in old.get('joints', []) if j['kind'] not in kinds]
    # 옛 파일 이름(계절 · 선 갈래 전)이 남지 않게 다시 뽑는 종류의 파일은 지운다
    for fn in os.listdir(OUT):
        if fn.endswith('.bin.gz') and any(fn.startswith('Chibi_Ent_%s' % k.capitalize()) for k in kinds): os.remove(os.path.join(OUT, fn))
    from multiprocessing import Pool
    with Pool(min(8, len(kinds))) as pool:
        for got, js in pool.map(bake, kinds): items += got; joints += js
    items.sort(key=lambda i: (ent.ORDER.index(i['kind']), i['mesh']))
    json.dump({'items': items, 'joints': joints, 'palettes': palettes()}, open(man_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print('목록 %d · 관절 %d' % (len(items), len(joints)))
