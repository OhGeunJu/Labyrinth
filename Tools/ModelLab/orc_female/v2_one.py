import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
from PIL import Image
import hair_v2 as Hv
from orc_preview import render
S = 220; i = int(sys.argv[1]); name, fn = Hv.HAIRS[i]
P = Hv.pieces(fn)
im = Image.new('RGB', (S, S * 4))
for r, a in enumerate((0, 35, 90, 150)): im.paste(render(P, a, S, S, -0.14, 1.40), (0, r * S))
im.save(f'v2_{i}.png'); print('done', name)
