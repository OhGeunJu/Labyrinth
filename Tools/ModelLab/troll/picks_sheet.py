import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import troll_preview as T
from PIL import Image
combos = [dict(Neck='Collar', Wrist='Shackle', Ankle='Shackle'), dict(Neck='Bones', Wrist='Wrap', Ankle='Off'), dict(Neck='Off', Wrist='Off', Ankle='Off')]
tiles = []
for c in combos:
    T.PICK.update(c); tiles.append(T.render(T.pieces(), 30, 300, 380, 0.95, 2.35))
im = Image.new('RGB', (900, 380))
for i, t in enumerate(tiles): im.paste(t, (i * 300, 0))
im.save(sys.argv[1]); print('saved')
