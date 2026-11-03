import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import troll_preview as T
from brute_preview import V
from PIL import Image
angs = (0, 40, 90)
rows = []
for neck in ('Collar', 'Off'):
    T.PICK['Neck'] = neck
    P = T.pieces(); sh = V(0, 0, T.HEAD_SHIFT * 0.85)
    PF = [((lambda p, f=f: f(p + sh)), c) for f, c in P]
    rows.append([T.render(PF, a, 240, 240, T.J['Top'] - 0.20, 0.85) for a in angs])
im = Image.new('RGB', (240 * len(angs), 480))
for r, row in enumerate(rows):
    for i, t in enumerate(row): im.paste(t, (i * 240, r * 240))
im.save(sys.argv[1]); print('saved')
