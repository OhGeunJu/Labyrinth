import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
import troll_preview as T
from brute_preview import V, mirror
from PIL import Image
def ear_w(p): q = T.head_space(p); return T.ear_raw(mirror(q), T.J['Top']) - 0.0015
P = [(T.head, T.SKIN), (ear_w, (0.85, 0.45, 0.40))]
c = V(0.206 * T.WIDE, 0, 0.285 + T.HEAD_SHIFT)
sh = V(c[0], 0, c[2])
PF = [((lambda p, f=f: f(p + sh)), col) for f, col in P]
tiles = [T.render(PF, a, 260, 260, T.J['Top'] + 0.035 - T.HEAD_DROP, 0.16) for a in (40, 70, 95)]
im = Image.new('RGB', (780, 260))
for i, t in enumerate(tiles): im.paste(t, (i * 260, 0))
im.save(sys.argv[1]); print('saved')
