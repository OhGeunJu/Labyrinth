"""머리 하나 vs 머리 둘 크기 — 현재(A) · 키운 B · C."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import ogre_v3 as O, ogre_preview as G
from PIL import Image, ImageDraw
VARS = [('A now 0.74/0.88 x.26', 0.74, 0.88, 0.26), ('B 0.82/0.92 x.30', 0.82, 0.92, 0.30), ('C 0.88/0.95 x.335', 0.88, 0.95, 0.335)]

def twin(sex, s, kx, x):
    O.TWIN_S, O.TWIN_KX, O.TWIN_X = s, kx, x
    return O.build(sex, 'one', (True, False), True)

W, H = 300, 420
jobs = [(None, 0, 'single')] + [(v, 0, v[0]) for v in VARS] + [(v, 30, v[0] + ' 3/4') for v in VARS[1:]]
sheet = Image.new('RGB', (W * len(jobs), H), (40, 40, 44)); d = ImageDraw.Draw(sheet)
for i, (v, yaw, lab) in enumerate(jobs):   # 머리 둘 좌표는 그릴 때 전역 값을 읽는다 — 값을 정한 직후 그린다
    P = O.build('M', 'one') if v is None else twin('M', *v[1:])
    sheet.paste(G.render(P, yaw, W, H, 1.35, 3.1), (i * W, 0)); d.text((i * W + 6, 4), lab, fill=(230, 230, 230))
sheet.save(sys.argv[1]); print('saved')
