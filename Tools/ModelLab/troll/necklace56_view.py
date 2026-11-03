import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
from PIL import Image
import troll_preview as T
import necklace56 as S
T.PICK.update(Wrist='Off', Ankle='Off')
P = S.pieces()
tiles = [T.render(P, a, 300, 380, 0.95, 2.35) for a in (0, 60, 90)]
im = Image.new('RGB', (900, 380))
for i, t in enumerate(tiles): im.paste(t, (i * 300, 0))
im.save(sys.argv[1]); print('saved', flush=True)
