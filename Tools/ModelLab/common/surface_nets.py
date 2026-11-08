"""거리장 → 삼각형 메시(표면 그물 · Surface Nets) — 유니티 `ChibiPawnCreator.SurfaceNets`와 같은 길을 numpy로.

먼저 성긴 격자(칸 4배)에서 재고, 표면 둘레(`band`) 칸만 촘촘히 잰다(나머지는 성긴 값으로 채운다 — 부호만 쓴다).
부호가 바뀌는 칸마다 점 하나(변 위 교점의 평균) · 부호가 바뀌는 격자 변마다 사각형 하나(짧은 대각선으로 가른다).
면 방향은 거리장 기울기(바깥)에 맞춘다. 법선은 기울기.

f(p): p (..., 3) float32 → (...) float32. 반환: verts (n, 3) · normals (n, 3) · tris (m, 3) int32."""
import numpy as np

COARSE = 4

def _eval(f, pts, chunk=150_000):
    out = np.empty(len(pts), np.float32)
    for i in range(0, len(pts), chunk):
        out[i:i + chunk] = f(pts[i:i + chunk].astype(np.float32))
    return out

def surface_nets(f, lo, hi, step, band_k=2.0):
    lo = np.asarray(lo, np.float32); hi = np.asarray(hi, np.float32)
    cstep = step * COARSE
    cn = np.ceil((hi - lo) / cstep).astype(int) + 1                    # 성긴 마디 수
    n = (cn - 1) * COARSE + 1                                            # 촘촘한 마디 수
    ci = np.stack(np.meshgrid(*[np.arange(c) for c in cn], indexing='ij'), -1).reshape(-1, 3)
    cval = _eval(f, lo + ci * cstep).reshape(tuple(cn))

    # 표면 둘레 칸 — 모서리 여덟 중 하나라도 띠 안이면 그 칸만 다 잰다
    band = band_k * cstep * 1.7320508
    a = np.abs(cval)
    m = np.minimum.reduce([a[x:x + cn[0] - 1, y:y + cn[1] - 1, z:z + cn[2] - 1] for x in (0, 1) for y in (0, 1) for z in (0, 1)])
    s0 = cval[:-1, :-1, :-1] < 0
    mixed = np.zeros_like(s0)
    for x in (0, 1):
        for y in (0, 1):
            for z in (0, 1):
                mixed |= (cval[x:x + cn[0] - 1, y:y + cn[1] - 1, z:z + cn[2] - 1] < 0) != s0
    act = np.argwhere((m < band) | mixed)

    # 촘촘한 값 — 바탕은 가장 가까운 성긴 값(부호만 맞으면 된다)
    idx = [np.minimum(np.round(np.arange(n[d]) / COARSE).astype(int), cn[d] - 1) for d in range(3)]
    val = cval[np.ix_(idx[0], idx[1], idx[2])].astype(np.float32)
    if len(act):
        off = np.stack(np.meshgrid(*[np.arange(COARSE + 1)] * 3, indexing='ij'), -1).reshape(-1, 3)
        fine = (act[:, None, :] * COARSE + off[None]).reshape(-1, 3)
        key = (fine[:, 0] * n[1] + fine[:, 1]) * n[2] + fine[:, 2]
        key = np.unique(key)
        fi = np.stack([key // (n[1] * n[2]), (key // n[2]) % n[1], key % n[2]], -1)
        val[fi[:, 0], fi[:, 1], fi[:, 2]] = _eval(f, lo + fi * step)

    # 부호가 바뀌는 칸 → 점(변 교점의 평균)
    s = val < 0
    cs = (n[0] - 1, n[1] - 1, n[2] - 1)
    c0 = s[:-1, :-1, :-1]
    cm = np.zeros(cs, bool)
    for x in (0, 1):
        for y in (0, 1):
            for z in (0, 1):
                cm |= s[x:x + cs[0], y:y + cs[1], z:z + cs[2]] != c0
    cells = np.argwhere(cm)
    cell_id = np.full(cs, -1, np.int32)
    cell_id[cells[:, 0], cells[:, 1], cells[:, 2]] = np.arange(len(cells), dtype=np.int32)
    corners = np.array([(x, y, z) for x in (0, 1) for y in (0, 1) for z in (0, 1)])
    edges = [(a_, b_) for a_ in range(8) for b_ in range(a_ + 1, 8) if np.abs(corners[a_] - corners[b_]).sum() == 1]
    cv = np.stack([val[cells[:, 0] + c[0], cells[:, 1] + c[1], cells[:, 2] + c[2]] for c in corners], 1)
    acc = np.zeros((len(cells), 3), np.float64); cnt = np.zeros(len(cells))
    for a_, b_ in edges:
        va, vb = cv[:, a_], cv[:, b_]
        hit = (va < 0) != (vb < 0)
        t = np.where(hit, va / np.where(hit, va - vb, 1), 0)
        acc += hit[:, None] * (corners[a_] + t[:, None] * (corners[b_] - corners[a_]))
        cnt += hit
    verts = (lo + (cells + acc / np.maximum(cnt, 1)[:, None]) * step).astype(np.float32)

    # 부호가 바뀌는 격자 변 → 사각형(둘레 네 칸)
    quads = []
    for ax in range(3):
        sl0 = [slice(1, n[d] - 1) for d in range(3)]
        sl1 = list(sl0); sl0[ax] = slice(0, n[ax] - 1); sl1[ax] = slice(1, n[ax])
        sl1 = [sl1[d] if d == ax else sl0[d] for d in range(3)]
        e = np.argwhere(s[tuple(sl0)] != s[tuple(sl1)])
        e[:, [d for d in range(3) if d != ax]] += 1
        u, v = [d for d in range(3) if d != ax]
        def cid(du, dv):
            c = e.copy(); c[:, u] += du; c[:, v] += dv
            return cell_id[c[:, 0], c[:, 1], c[:, 2]]
        q = np.stack([cid(-1, -1), cid(0, -1), cid(0, 0), cid(-1, 0)], 1)
        quads.append(q[(q >= 0).all(1)])
    q = np.concatenate(quads) if quads else np.zeros((0, 4), np.int32)
    d02 = ((verts[q[:, 0]] - verts[q[:, 2]]) ** 2).sum(1); d13 = ((verts[q[:, 1]] - verts[q[:, 3]]) ** 2).sum(1)
    use02 = d02 <= d13
    t1 = np.where(use02[:, None], q[:, [0, 1, 2]], q[:, [1, 2, 3]])
    t2 = np.where(use02[:, None], q[:, [0, 2, 3]], q[:, [1, 3, 0]])
    tris = np.concatenate([t1, t2]).astype(np.int32)

    # 법선 = 기울기 · 면 방향은 바깥(기울기 쪽)으로
    h = step * 0.5
    g = np.stack([_eval(f, verts + np.array(dv, np.float32) * h) - _eval(f, verts - np.array(dv, np.float32) * h)
                  for dv in ((1, 0, 0), (0, 1, 0), (0, 0, 1))], 1)
    normals = (g / np.maximum(np.linalg.norm(g, axis=1, keepdims=True), 1e-9)).astype(np.float32)
    fn = np.cross(verts[tris[:, 1]] - verts[tris[:, 0]], verts[tris[:, 2]] - verts[tris[:, 0]])
    flip = (fn * normals[tris].sum(1)).sum(1) < 0
    tris[flip] = tris[flip][:, ::-1]
    return verts, normals, tris
