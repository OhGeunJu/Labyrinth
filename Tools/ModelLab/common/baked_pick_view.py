"""뽑은 메시를 **고르기 축 · 팔레트를 골라** 본다(11-19 코볼트 — 옷 축 넷 · 털 팔레트 여섯). `baked_view.py`는 모든 조각을 한꺼번에 그리고
팔레트 재질의 정점 색(칸 몫)을 색으로 그려 알아볼 수 없다 — 여기서는 게임 `PawnLook`과 같은 규칙으로 켜질 조각만 고르고(`Spine.Pick.{축}.{벌}@조건`),
팔레트 재질 조각은 칸 몫 × 팔레트 색으로 칠한다. 줄마다 한 벌: 정면 · 40° · 옆 · 뒤 · 걷는 자세.

    python baked_pick_view.py <baked 폴더> out.png "Palette=Spotted Top=Vest Bottom=Rags WrapArm=On WrapShin=Off" "Palette=Brown Top=None …" …"""
import sys, json, os
import numpy as np
from PIL import Image, ImageDraw
import baked_view as bv

def active(part, pick):
    """`PawnLook.AddPick`과 같은 읽기 — 고르기 조각이 아니면 늘 켜진다."""
    segs = part.split('@'); head = segs[0].split('.')
    if '.Pick.' not in segs[0] or len(head) < 4: return True
    axis, opt = head[2], head[3]
    if axis in pick and pick[axis] != opt: return False
    for c in segs[1:]:
        a, o = c.split('.', 1); neg = o.startswith('!'); o = o.lstrip('!')
        hit = pick.get(a) in o.split('|')
        if hit == neg: return False
    return True

def main(d, out, combos):
    man = json.load(open(os.path.join(d, 'manifest.json'), encoding='utf-8'))
    meshes, J = bv.load(d)
    pals = {p['name']: {s['mat']: [np.array([c['r'], c['g'], c['b']], np.float32) for c in s['colors']] for s in p['slots']} for p in man.get('palettes', [])}
    import gzip
    rows = []
    for combo in combos:
        pick = dict(kv.split('=') for kv in combo.split())
        sel = []
        for it, m in zip(man['items'], meshes):
            if not active(it['part'], pick): continue
            m = dict(m)
            if it['mat'] != 'Paint' and pick.get('Palette') in pals:
                with gzip.open(os.path.join(d, it['file']), 'rb') as fh: b = fh.read()
                nv, nt = np.frombuffer(b[:8], np.int32); o = 8 + nv * 24 + nt * 12
                C4 = np.frombuffer(b[o:o + nv * 16], np.float32).reshape(nv, 4)
                cols = pals[pick['Palette']][it['mat']]
                m['C'] = sum(C4[:, i:i + 1] * cols[i] for i in range(min(4, len(cols))))
            sel.append(m)
        allv = np.concatenate([m['V'] for m in meshes]); lo, hi = allv.min(0), allv.max(0)
        span = float(max(hi[1] - lo[1], (hi[2] - lo[2]) * 0.9)) * 1.08; cy = float(lo[1] + hi[1]) / 2; H = 560; W = int(H * 0.75)
        walk = {'LegL': bv.rot('x', -35), 'LowerLegL': bv.rot('x', 30), 'LegR': bv.rot('x', 25), 'ArmR': bv.rot('x', -70), 'LowerArmR': bv.rot('x', -40), 'ArmL': bv.rot('z', -50), 'Head': bv.rot('y', 30)}
        views = [bv.render(sel, a, W, H, cy, span) for a in (0, 40, 90, 160)] + [bv.render(bv.pose(sel, J, walk), 40, W, H, cy, span)]
        row = Image.new('RGB', (W * len(views), H), (44, 44, 44)); dr = ImageDraw.Draw(row)
        for i, v in enumerate(views): row.paste(v, (i * W, 0))
        dr.text((6, 6), combo, fill=(255, 220, 120)); rows.append(row)
    im = Image.new('RGB', (rows[0].width, sum(r.height for r in rows)), (44, 44, 44)); y = 0
    for r in rows: im.paste(r, (0, y)); y += r.height
    im.save(out); print('saved', out)

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2], sys.argv[3:])
