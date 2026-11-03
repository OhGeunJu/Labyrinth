import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys, ogre_v3 as O, ogre_preview as G
from PIL import Image, ImageDraw
T = O.T
def make(sex, y, k, r):
    O.SADDLE_Y, O.SADDLE_K, O.SADDLE_R = y, k, r
    P = []
    for sgn in (-1, 1): P += [(O.on_head(f, sgn), c) for f, c in O.head_parts(sex, 'one', True)]
    if y is None: return P + O.body_parts(sex)
    sad = O.saddle  # 값을 지금 묶어 둔다
    def bind(f, yy=y, kk=k, rr=r):
        def g(p):
            return O.smax(f(p), p[..., 1] - (T + yy + kk * p[..., 0] ** 2), rr)
        return g
    return P + [(bind(f) if f in O.SADDLED else f, c) for f, c in O.body_parts(sex)]
vs = [(None, 0, 0, 'before'), (0.03, 1.0, 0.12, 'A shallow'), (0.0, 1.2, 0.14, 'B a bit lower')]
tiles = [(make(s, y, k, r), s + ' ' + lab) for s in ('M', 'F') for y, k, r, lab in vs]
sheet = Image.new('RGB', (300 * 6, 300), (40, 40, 44)); d = ImageDraw.Draw(sheet)
for i, (P, lab) in enumerate(tiles):
    sheet.paste(G.render(P, 0, 300, 300, T + 0.05, 1.1), (i * 300, 0)); d.text((i * 300 + 6, 4), lab, fill=(230, 230, 230))
sheet.save(sys.argv[1]); print('saved')
