import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import troll_preview as T
from brute_preview import V
from PIL import Image
sh = V(0, 0, T.HEAD_SHIFT + 0.36)
P = [((lambda p, f=f: f(p + sh)), c) for f, c in T.pieces()]
tiles = [T.render(P, a, 360, 360, T.J['Top'] - 0.14, 0.62) for a in (0, 90, 40)]
im = Image.new('RGB', (1080, 360))
for i, t in enumerate(tiles): im.paste(t, (i * 360, 0))
im.save(sys.argv[1]); print('saved')
