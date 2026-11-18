"""뽑은 메시(`bake_sdf.py`가 낸 `baked/`)를 유니티에 넣기 전에 본다 — 색 · 뼈 나눔 · 관절을 굽힌 자세(스킨이 늘어나나).

정점을 점으로 뿌려 깊이로 가린다(메시가 촘촘해 세모를 안 칠해도 겉이 읽힌다 · 빠르다). 칸: 정면 · 40° · 옆 · 뒤 · 뼈 색 · 굽힌 자세 둘.
굽힌 자세는 유니티와 같은 셈(관절 자리 = 바인드 · 회전 없음 → 뼈마다 돌려 가중치로 섞는다).

    python baked_view.py <baked 폴더> out.png"""
import sys, json, gzip, os
import numpy as np
from PIL import Image, ImageDraw
from bake_sdf import BONES

PARENT = {'Hips': None, 'Spine': 'Hips', 'Head': 'Spine', 'ArmL': 'Spine', 'LowerArmL': 'ArmL', 'HandL': 'LowerArmL', 'ArmR': 'Spine', 'LowerArmR': 'ArmR',
          'HandR': 'LowerArmR', 'LegL': 'Hips', 'LowerLegL': 'LegL', 'FootL': 'LowerLegL', 'LegR': 'Hips', 'LowerLegR': 'LegR', 'FootR': 'LowerLegR'}
BONE_COL = np.array([(200, 60, 60), (230, 160, 60), (60, 120, 230), (90, 170, 240), (150, 210, 250), (60, 200, 90), (110, 230, 130), (170, 250, 180),
                     (170, 70, 200), (210, 120, 230), (240, 180, 250), (200, 200, 60), (230, 230, 120), (250, 250, 190), (240, 240, 240)], np.float32)

def load(d):
    man = json.load(open(os.path.join(d, 'manifest.json'), encoding='utf-8'))
    out = []
    for it in man['items']:
        with gzip.open(os.path.join(d, it['file']), 'rb') as fh: b = fh.read()
        nv, nt = np.frombuffer(b[:8], np.int32); o = 8
        def take(n, dt, k):
            nonlocal o; a = np.frombuffer(b[o:o + n * k * 4], dt).reshape(n, k); o += n * k * 4; return a
        V = take(nv, np.float32, 3); N = take(nv, np.float32, 3); take(nt, np.int32, 3); C = take(nv, np.float32, 4); BI = take(nv, np.int32, 2); BW = take(nv, np.float32, 2)
        out.append(dict(V=V, N=N, C=C[:, :3], BI=BI, BW=BW, line=it['line']))
    J = {j['bone']: np.array([j['x'], j['y'], j['z']], np.float32) for j in man['joints']}
    # 네 발 짐승(11-18 · `bake_quad.py`) — 목록이 뼈 차례 · 부모를 든다(사람 뼈 열다섯이 아니다).
    if man.get('bones'):
        global BONES, PARENT
        BONES = tuple(man['bones']); PARENT = {d['bone']: (d['parent'] or None) for d in man['parents']}
    return out, J

def rot(ax, deg):
    a = np.radians(deg); c, s = np.cos(a), np.sin(a)
    return {'x': np.array([[1, 0, 0], [0, c, -s], [0, s, c]]), 'y': np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]]), 'z': np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])}[ax]

def pose(meshes, J, R):
    """뼈마다 돌림(R — 이름 → 3×3) → 정점 · 법선을 섞어 옮긴다(선형 스킨)."""
    G = {}
    def glob(b):
        if b in G: return G[b]
        p = PARENT[b]; Rb = R.get(b, np.eye(3))
        if p is None: G[b] = (Rb, J[b])
        else:
            Rp, tp = glob(p); G[b] = (Rp @ Rb, tp + Rp @ (J[b] - J[p]))
        return G[b]
    M = []
    for b in BONES:
        Rg, t = glob(b); M.append((Rg, t - Rg @ J[b]))
    out = []
    for m in meshes:
        V, N = np.zeros_like(m['V']), np.zeros_like(m['N'])
        for k in range(2):
            w = m['BW'][:, k:k + 1]
            for bi in np.unique(m['BI'][:, k]):
                s = m['BI'][:, k] == bi; Rg, t = M[bi]
                V[s] += w[s] * (m['V'][s] @ Rg.T + t); N[s] += w[s] * (m['N'][s] @ Rg.T)
        out.append(dict(m, V=V, N=N))
    return out

def render(meshes, yaw, W, H, cy, span, bones=False):
    R = rot('y', -yaw)
    V = np.concatenate([m['V'] for m in meshes]) @ R.T; N = np.concatenate([m['N'] for m in meshes]) @ R.T
    C = np.concatenate([BONE_COL[m['BI'][:, 0]] / 255 if bones else m['C'] for m in meshes])
    s = H / span; x = (W / 2 + V[:, 0] * s).astype(int); y = (H / 2 - (V[:, 1] - cy) * s).astype(int)
    lit = np.where(N @ np.array([-0.4, 0.6, 0.7]) / 1.02 > -0.15, 1.0, 0.62)[:, None]
    col = (C * lit * 255).clip(0, 255)
    img = np.full((H, W, 3), 44, np.float32); z = np.full((H, W), -9.0)
    order = np.argsort(V[:, 2])   # 먼 것부터 — 가까운 것이 덮는다
    for dx in (0, 1):
        for dy in (0, 1):
            xx, yy = x[order] + dx, y[order] + dy; ok = (xx >= 0) & (xx < W) & (yy >= 0) & (yy < H) & (N[order] @ np.array([0, 0, 1]) > -0.2)
            img[yy[ok], xx[ok]] = col[order][ok]
    return Image.fromarray(img.astype(np.uint8))

if __name__ == '__main__':
    d, out = sys.argv[1], sys.argv[2]
    meshes, J = load(d)
    allv = np.concatenate([m['V'] for m in meshes]); lo, hi = allv.min(0), allv.max(0)
    span = float(max(hi[1] - lo[1], (hi[2] - lo[2]) * 0.9)) * 1.08; cy = float(lo[1] + hi[1]) / 2; H = 640
    W = int(H * 0.75)
    if 'Body' in BONES: span = float(max(hi[1] - lo[1], hi[2] - lo[2])) * 1.05; W = H
    quad = 'Body' in BONES
    if quad:   # 네 발 — 대각 걸음(앞왼 · 뒤오른 한 쌍) · 머리 돌림 · 꼬리 마디마다 휜다
        tails = [b for b in BONES if b.startswith('Tail')]
        walk = {'LegFL': rot('x', 30), 'LegBR': rot('x', 30), 'LegFR': rot('x', -30), 'LegBL': rot('x', -30), 'Head': rot('y', 25)}
        walk.update({b: rot('y', 18) for b in tails})
        crouch = {'Head': rot('x', 25), 'Body': rot('x', -8), 'LegFL': rot('z', 25), 'LegFR': rot('z', -25)}
        crouch.update({b: rot('y', -22) for b in tails})
    else: walk = {'LegL': rot('x', -35), 'LowerLegL': rot('x', 30), 'LegR': rot('x', 25), 'ArmR': rot('x', -70), 'LowerArmR': rot('x', -40), 'ArmL': rot('z', -50), 'Head': rot('y', 30)}
    if not quad: crouch = {'LegL': rot('x', -60), 'LowerLegL': rot('x', 70), 'FootL': rot('x', -30), 'LegR': rot('x', -60), 'LowerLegR': rot('x', 70), 'FootR': rot('x', -30),
              'Spine': rot('x', 20), 'ArmL': rot('x', -90), 'ArmR': rot('z', 80), 'LowerArmR': rot('y', 60)}
    views = [render(meshes, a, W, H, cy, span) for a in (0, 40, 90, 160)] + [render(meshes, 40, W, H, cy, span, bones=True),
             render(pose(meshes, J, walk), 40, W, H, cy, span), render(pose(meshes, J, crouch), 60, W, H, cy, span)]
    im = Image.new('RGB', (W * len(views), H), (44, 44, 44)); dr = ImageDraw.Draw(im)
    for i, (v, t) in enumerate(zip(views, ('0', '40', '90', '160', 'bones', 'walk', 'crouch'))):
        im.paste(v, (i * W, 0)); dr.text((i * W + 6, 6), t, fill=(255, 220, 120))
    im.save(out); print('saved', out)
