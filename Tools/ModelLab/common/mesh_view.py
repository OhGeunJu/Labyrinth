"""구운 메시 에셋(`Assets/3_Prefab/Pawn/Mesh/*.asset` — 텍스트 YAML)을 읽어 살 + 뒤집힌 껍데기 외곽선을 그린다.

거리장 미리보기(레이마칭)는 매끈하다 — 게임에 들어가는 것은 표면 그물 → 줄이기 → 펴기를 거친 메시다. 메시에서 생긴 탈
(각진 면 · 껍데기 가시)은 이걸로 본다. 왼쪽은 정점 법선으로 민 껍데기(옛 셰이더), 오른쪽은 선 법선(탄젠트 칸 `w = 2` —
`ChibiPawnCreator.BakeOutlineNormals`)으로 민 껍데기다. 아직 안 구운 메시는 오른쪽을 같은 셈으로 흉내 낸다.

    python mesh_view.py out.png <yaw> <pitch> <선 굵기(메시 단위)> <에셋> [에셋 ...]

선 굵기는 월드 6cm ÷ 그 메시의 배율이다(큰 짐승 몸 좌표는 약 0.03 · 빚은 머리는 0.04~0.05).
"""
import re, sys
import numpy as np
from PIL import Image


def load(path):
    t = open(path, encoding='utf-8').read()
    vc = int(re.search(r'm_VertexCount: (\d+)', t).group(1))
    ch = [tuple(map(int, c)) for c in re.findall(r'- stream: (\d+)\s+offset: (\d+)\s+format: (\d+)\s+dimension: (\d+)', t)]
    data = bytes.fromhex(re.search(r'_typelessdata: ([0-9a-f]*)', t).group(1))
    ib = bytes.fromhex(re.search(r'm_IndexBuffer: ([0-9a-f]*)', t).group(1))
    wide = re.search(r'm_IndexFormat: (\d)', t).group(1) == '1'
    tris = np.frombuffer(ib, dtype=np.uint32 if wide else np.uint16).astype(np.int64).reshape(-1, 3)
    s0 = [c for c in ch if c[0] == 0 and c[3] > 0]
    stride = max(o + d * (4 if f == 0 else 1) for s, o, f, d in s0)
    raw = np.frombuffer(data[:vc * stride], dtype=np.uint8).reshape(vc, stride)

    def chan(i):   # 0 자리 · 1 법선 · 2 탄젠트 · 3 색
        if i >= len(ch): return None
        s, o, f, d = ch[i]
        if d == 0 or s != 0 or f != 0: return None
        return raw[:, o:o + d * 4].copy().view(np.float32).reshape(vc, d)

    return dict(V=chan(0), N=chan(1), T=tris, tan=chan(2), C=chan(3))


def line_normals(V, N, T, R):
    """`ChibiPawnCreator.OutlineNormals`와 같은 셈 — 반지름 R 안의 넓이 가중 면 법선, 서로 지운 곳은 제 법선."""
    a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]
    fn = np.cross(b - a, c - a)
    fn *= np.sign((fn * N[T].sum(1)).sum(1))[:, None]
    cen, fl = (a + b + c) / 3, np.linalg.norm(fn, axis=1)
    out, tot = np.zeros_like(V), np.zeros(len(V))
    for i in range(0, len(V), 512):
        w = np.clip(1 - ((V[i:i + 512, None] - cen[None]) ** 2).sum(-1) / (R * R), 0, None)
        out[i:i + 512], tot[i:i + 512] = w @ fn, w @ fl
    agree = np.linalg.norm(out, axis=1) / (tot + 1e-12)
    out /= np.linalg.norm(out, axis=1, keepdims=True) + 1e-12
    t = np.clip((agree - 0.3) / 0.3, 0, 1)[:, None]
    out = t * out + (1 - t) * N
    return out / (np.linalg.norm(out, axis=1, keepdims=True) + 1e-12)


def _raster(P, T, mask, zbuf, img, color):
    H, W = zbuf.shape
    for ti in np.nonzero(mask)[0]:
        pa, pb, pc = P[T[ti, 0]], P[T[ti, 1]], P[T[ti, 2]]
        x0, x1 = int(max(0, min(pa[0], pb[0], pc[0]))), int(min(W - 1, np.ceil(max(pa[0], pb[0], pc[0]))))
        y0, y1 = int(max(0, min(pa[1], pb[1], pc[1]))), int(min(H - 1, np.ceil(max(pa[1], pb[1], pc[1]))))
        if x1 < x0 or y1 < y0: continue
        d = (pb[0] - pa[0]) * (pc[1] - pa[1]) - (pc[0] - pa[0]) * (pb[1] - pa[1])
        if abs(d) < 1e-9: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + .5, np.arange(y0, y1 + 1) + .5)
        w1 = ((xs - pa[0]) * (pc[1] - pa[1]) - (pc[0] - pa[0]) * (ys - pa[1])) / d
        w2 = ((pb[0] - pa[0]) * (ys - pa[1]) - (xs - pa[0]) * (pb[1] - pa[1])) / d
        w0 = 1 - w1 - w2
        z = w0 * pa[2] + w1 * pb[2] + w2 * pc[2]
        sub = zbuf[y0:y1 + 1, x0:x1 + 1]
        m = (w0 >= 0) & (w1 >= 0) & (w2 >= 0) & (z < sub)
        if not m.any(): continue
        sub[m] = z[m]
        img[y0:y1 + 1, x0:x1 + 1][m] = color(ti, w0[m], w1[m], w2[m])


def render(meshes, yaw, pitch, width, hull_dir, W=700):
    y, p = np.radians(yaw), np.radians(pitch)
    R = np.array([[1, 0, 0], [0, np.cos(p), -np.sin(p)], [0, np.sin(p), np.cos(p)]]) @ \
        np.array([[np.cos(y), 0, np.sin(y)], [0, 1, 0], [-np.sin(y), 0, np.cos(y)]])
    allv = np.concatenate([m['V'] for m in meshes])
    center, s = (allv.min(0) + allv.max(0)) / 2, W / (2 * (allv.max(0) - allv.min(0)).max() * 0.6)
    proj = lambda V: np.stack([W / 2 + ((V - center) @ R.T)[:, 0] * s, W / 2 - ((V - center) @ R.T)[:, 1] * s, -((V - center) @ R.T)[:, 2]], 1)
    view, light = R.T @ np.array([0, 0, 1.0]), R.T @ np.array([-.4, .6, .7]) / np.linalg.norm([-.4, .6, .7])
    zbuf, img = np.full((W, W), np.inf), np.full((W, W, 3), 200, np.uint8)
    for m in meshes:   # 껍데기 — 앞면을 버린다
        D = hull_dir(m)
        _raster(proj(m['V'] + D * width), m['T'], D[m['T']].mean(1) @ view < 0, zbuf, img, lambda *_: np.array([10, 10, 14], np.uint8))
    for m in meshes:   # 살 — 계단 음영(문턱 0.42) × 정점 색 그늘
        V, N, T = m['V'], m['N'], m['T']
        lam = np.where((N @ light) * .5 + .5 > .42, 1.0, .62) * (m['C'][:, 0] if m['C'] is not None else 1)
        col = np.stack([lam * 235, lam * 190, lam * 160], 1)
        _raster(proj(V), T, N[T].mean(1) @ view > 0, zbuf, img,
                lambda ti, a, b, c: (a[:, None] * col[T[ti, 0]] + b[:, None] * col[T[ti, 1]] + c[:, None] * col[T[ti, 2]]).clip(0, 255).astype(np.uint8))
    return Image.fromarray(img)


def baked_or_simulated(width):
    def f(m):
        t = m['tan']
        if t is not None and (t[:, 3] > 1.5).all(): return t[:, :3]
        return line_normals(m['V'], m['N'], m['T'], width)
    return f


if __name__ == '__main__':
    out, yaw, pitch, width = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4])
    ms = [load(p) for p in sys.argv[5:]]
    for p, m in zip(sys.argv[5:], ms):
        baked = m['tan'] is not None and (m['tan'][:, 3] > 1.5).all()
        print(f"{p}: 정점 {len(m['V'])} · 세모 {len(m['T'])} · 선 법선 {'구워짐' if baked else '없음(흉내)'}")
    a = render(ms, yaw, pitch, width, lambda m: m['N'])
    b = render(ms, yaw, pitch, width, baked_or_simulated(width))
    sheet = Image.new('RGB', (a.width * 2, a.height)); sheet.paste(a, (0, 0)); sheet.paste(b, (a.width, 0)); sheet.save(out)
