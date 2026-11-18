"""게임에 든 폰 메시가 얼마나 울퉁불퉁한가 — 전부 재서 나쁜 순으로 늘어놓는다(2026-11-18, 사용자: *"거의 모든 모델을 한 번씩 확인해봐야해"*).

표면 그물 → 줄이기를 거친 살은 격자 방향 계단 띠 · 자잘한 돌기가 남는다(오크 머리 — 펴기 `Relax`를 안 탔다). 칠은 거리장 법선이 가려 매끈해 보여도
바깥 선 껍데기는 그 정점을 밀어 그리므로 톱니가 된다. 눈으로 천 개를 돌리기 전에 숫자로 고른다.

- **잔물결**(`ripple`) — 점마다 이웃 평균에서 법선 쪽으로 벗어난 거리 ÷ 둘레 변 길이(우산 라플라시안의 법선 성분). 매끈한 곡면은 곡률만큼만
  벗어나 작고 이웃끼리 비슷하다 — 계단 · 돌기는 부호가 번갈아 크다. **중앙값**을 쓴다(뿔 끝 · 손톱처럼 일부러 뾰족한 몇 점은 안 끌어올린다).
- **꺾임**(`fold`) — 이웃 면끼리 30°보다 꺾인 변의 몫(%). 낮은 해상도의 작은 조각(눈 판 · 이)은 꺾임만 높고 잔물결은 낮다.

    python mesh_audit.py [out.csv] [glob=Chibi_*]      # Assets/3_Prefab/Pawn/Mesh/*.asset
    python mesh_audit.py --baked <baked 폴더> [out.csv]  # 파이썬 굽기(.bin.gz)
"""
import os, sys, glob, gzip, csv
import numpy as np
from mesh_view import load

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..'))
MESH_DIR = os.path.join(ROOT, 'Assets', '3_Prefab', 'Pawn', 'Mesh')


def load_baked(path):
    b = gzip.open(path, 'rb').read()
    nv, nt = np.frombuffer(b[:8], np.int32); o = 8
    V = np.frombuffer(b[o:o + nv * 12], np.float32).reshape(-1, 3); o += nv * 24
    T = np.frombuffer(b[o:o + nt * 12], np.int32).reshape(-1, 3).astype(np.int64)
    return V, T


def measure(V, T):
    """(잔물결 중앙값 · 잔물결 90% · 꺾임 %) — 겹친 점(이음매)은 자리로 묶어서 잰다."""
    V = np.asarray(V, np.float64)
    key = np.round(V / 1e-5).astype(np.int64)
    _, uid, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    inv = inv.ravel(); P = V[uid]; F = inv[T]
    F = F[(F[:, 0] != F[:, 1]) & (F[:, 1] != F[:, 2]) & (F[:, 0] != F[:, 2])]
    if len(F) < 8: return None
    a, b, c = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
    fn = np.cross(b - a, c - a); area = np.linalg.norm(fn, axis=1); ok = area > 1e-14
    F, fn, area = F[ok], fn[ok], area[ok]
    fu = fn / area[:, None]
    n = len(P)
    vn = np.zeros((n, 3)); np.add.at(vn, F.ravel(), np.repeat(fn, 3, 0))
    vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-12)
    # 이웃 평균(우산) · 둘레 변 길이
    E = np.concatenate([F[:, [0, 1]], F[:, [1, 2]], F[:, [2, 0]]])
    E = np.unique(np.sort(E, 1), axis=0)
    deg = np.bincount(E.ravel(), minlength=n).astype(np.float64)
    nb = np.zeros((n, 3)); np.add.at(nb, E[:, 0], P[E[:, 1]]); np.add.at(nb, E[:, 1], P[E[:, 0]])
    L = np.linalg.norm(P[E[:, 0]] - P[E[:, 1]], axis=1)
    el = np.bincount(E[:, 0], L, n) + np.bincount(E[:, 1], L, n)
    live = deg > 2
    lap = nb[live] / deg[live, None] - P[live]
    ripple = np.abs((lap * vn[live]).sum(1)) / (el[live] / deg[live])
    # 꺾임 — 면 둘이 나눈 변
    fe = np.concatenate([np.sort(F[:, [0, 1]], 1), np.sort(F[:, [1, 2]], 1), np.sort(F[:, [2, 0]], 1)])
    fid = np.tile(np.arange(len(F)), 3)
    order = np.lexsort((fe[:, 1], fe[:, 0])); fe, fid = fe[order], fid[order]
    same = (fe[1:] == fe[:-1]).all(1)
    d = (fu[fid[1:][same]] * fu[fid[:-1][same]]).sum(1)
    fold = float((d < np.cos(np.radians(30))).mean() * 100) if same.any() else 0.0
    return float(np.median(ripple)), float(np.percentile(ripple, 90)), fold, int(len(V))


def model_of(name):
    """`Chibi_Orc_Head` → `Orc` — 모델(종족) 묶음 이름."""
    s = name.replace('Chibi_', '', 1)
    return s.split('_')[0]


def main():
    args = sys.argv[1:]
    rows = []
    if args and args[0] == '--baked':
        d = args[1]; out = args[2] if len(args) > 2 else 'mesh_audit_baked.csv'
        files = sorted(glob.glob(os.path.join(d, '*.bin.gz')))
        for p in files:
            r = measure(*load_baked(p))
            if r: rows.append((os.path.basename(p)[:-7],) + r)
    else:
        out = args[0] if args else 'mesh_audit.csv'
        pat = args[1] if len(args) > 1 else 'Chibi_*'
        files = sorted(glob.glob(os.path.join(MESH_DIR, pat + '.asset')))
        for i, p in enumerate(files):
            try:
                m = load(p)
            except Exception as e:
                print('  읽기 실패', os.path.basename(p), e); continue
            if m['V'] is None: continue
            r = measure(m['V'], m['T'])
            if r: rows.append((os.path.basename(p)[:-6],) + r)
            if i % 100 == 0: print('  %d / %d' % (i, len(files)), flush=True)
    rows.sort(key=lambda r: -r[1])
    with open(out, 'w', newline='', encoding='utf-8-sig') as fh:
        w = csv.writer(fh); w.writerow(['mesh', 'model', 'ripple_med', 'ripple_p90', 'fold_pct', 'verts'])
        for r in rows: w.writerow([r[0], model_of(r[0]), '%.4f' % r[1], '%.4f' % r[2], '%.2f' % r[3], r[4]])
    print('메시 %d개 → %s' % (len(rows), out))


if __name__ == '__main__':
    main()
