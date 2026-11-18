"""폰 프리팹마다 점 수 — 켜진 렌더러가 그리는 것 · 프리팹에 든 것 전부(2026-11-18, 사용자: *"종족마다 실제 점 수 표 만들어줘"*).

프리팹 YAML을 읽어 렌더러(스킨 · 메시)마다 메시 에셋의 `m_VertexCount`를 더한다. "켜짐"은 프리팹에 저장된 상태(GameObject가 위까지
켜져 있고 렌더러가 켜짐)다 — 게임에서는 `PawnLook`이 개체마다 고르기 축을 다시 켜고 끄므로 대표 한 벌의 근사다. "전부"는 고르기
축의 모든 벌을 합친 것(메모리 · 굽기 크기). 외곽선 껍데기 패스가 같은 점을 한 번 더 돌린다(그리는 비용은 대략 두 배).

    python pawn_verts.py [out.csv]        # Assets/3_Prefab/Pawn/**/*.prefab
"""
import os, re, sys, glob, csv, statistics

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
PAWN = os.path.join(ROOT, 'Assets', '3_Prefab', 'Pawn')
DOC = re.compile(r'^--- !u!(\d+) &(-?\d+)', re.M)
SHAPE = re.compile(r'(?:^Head\.Shape\.|@Shape\.)([A-Za-z]+_[A-Za-z]+)')


def mesh_index():
    """guid → 점 수 — 폰 메시 폴더의 에셋(머리말만 읽는다)."""
    out = {}
    for meta in glob.glob(os.path.join(PAWN, '**', '*.asset.meta'), recursive=True):
        guid = re.search(r'guid: ([0-9a-f]+)', open(meta, encoding='utf-8').read()).group(1)
        # 인덱스 버퍼(16진수)가 앞에 길게 있어 `m_VertexCount`가 수 MB 뒤에 온다 — 조각씩 읽어 찾는다.
        m, tail = None, ''
        with open(meta[:-5], encoding='utf-8', errors='ignore') as fh:
            while m is None:
                chunk = fh.read(1 << 20)
                if not chunk: break
                buf = tail + chunk
                m = re.search(r'm_VertexCount: (\d+)', buf); tail = buf[-64:]
        if m: out[guid] = (int(m.group(1)), os.path.basename(meta[:-11]))
    return out


def prefab_verts(path, meshes):
    t = open(path, encoding='utf-8', errors='ignore').read()
    starts = [(m.start(), int(m.group(1)), m.group(2)) for m in DOC.finditer(t)]
    docs = {}
    for i, (s, cls, fid) in enumerate(starts):
        docs[fid] = (cls, t[s:starts[i + 1][0] if i + 1 < len(starts) else len(t)])
    def ref(body, key):
        m = re.search(key + r': \{fileID: (-?\d+)', body); return m.group(1) if m else None
    go_active, tr_of_go, father = {}, {}, {}
    for fid, (cls, body) in docs.items():
        if cls == 1: go_active[fid] = 'm_IsActive: 1' in body
        elif cls in (4, 224):
            go = ref(body, 'm_GameObject'); tr_of_go[go] = fid; father[fid] = ref(body, 'm_Father')
    go_of_tr = {v: k for k, v in tr_of_go.items()}
    def active(go):
        tr = tr_of_go.get(go)
        while tr and tr != '0':
            g = go_of_tr.get(tr)
            if g is not None and not go_active.get(g, True): return False
            tr = father.get(tr)
        return True
    filt = {}
    for fid, (cls, body) in docs.items():
        if cls == 33:
            m = re.search(r'm_Mesh: \{fileID: -?\d+, guid: ([0-9a-f]+)', body)
            if m: filt[ref(body, 'm_GameObject')] = m.group(1)
    names = {fid: (re.search(r'm_Name: (.*)', body) or [None, ''])[1].strip() for fid, (cls, body) in docs.items() if cls == 1}
    total = 0; lit = []   # (이름, 점 수) — 켜진 렌더러
    for fid, (cls, body) in docs.items():
        if cls not in (137, 23): continue
        go = ref(body, 'm_GameObject')
        if cls == 137:
            m = re.search(r'm_Mesh: \{fileID: -?\d+, guid: ([0-9a-f]+)', body); guid = m.group(1) if m else None
        else: guid = filt.get(go)
        if guid not in meshes: continue
        n = meshes[guid][0]; total += n
        if 'm_Enabled: 1' in body and active(go): lit.append((names.get(go, ''), n))
    # **머리형은 한 벌만 켜진다** — 사람 프리팹은 머리형 여섯(`Head.Shape.{머리}` · `@Shape.{머리}`)이 다 켜진 채 저장되고
    # `PawnLook`이 개체마다 하나를 고른다. 머리형마다 셈해 가운데 값을 쓴다.
    shape_of = lambda n: (SHAPE.search(n) or [None, None])[1]
    shapes = sorted({shape_of(n) for n, _ in lit} - {None})
    if len(shapes) > 1:
        sums = sorted(sum(v for n, v in lit if shape_of(n) in (None, s)) for s in shapes)
        on = sums[len(sums) // 2]
    else: on = sum(v for _, v in lit)
    return on, total, len(lit)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else 'pawn_verts.csv'
    meshes = mesh_index()
    rows = []
    for p in sorted(glob.glob(os.path.join(PAWN, '**', '*.prefab'), recursive=True)):
        if 'm_SourcePrefab' in open(p, encoding='utf-8', errors='ignore').read(4000): continue   # 변형 프리팹은 건너뛴다
        on, total, rends = prefab_verts(p, meshes)
        if total == 0: continue
        rel = os.path.relpath(p, PAWN).replace('\\', '/')
        rows.append((rel.split('/')[0], rel, on, total, rends))
    with open(out, 'w', newline='', encoding='utf-8-sig') as fh:
        w = csv.writer(fh); w.writerow(['race', 'prefab', 'verts_on', 'verts_all', 'renderers_on'])
        for r in rows: w.writerow(r)
    by = {}
    for r in rows: by.setdefault(r[0], []).append(r)
    print('%-10s %4s %9s %9s %9s %11s' % ('race', 'n', 'on_min', 'on_med', 'on_max', 'all_max'))
    for race, rs in sorted(by.items(), key=lambda kv: -statistics.median(r[2] for r in kv[1])):
        ons = [r[2] for r in rs]
        print('%-10s %4d %9d %9d %9d %11d' % (race, len(rs), min(ons), statistics.median(ons), max(ons), max(r[3] for r in rs)))
    print('→', out)


if __name__ == '__main__':
    main()
