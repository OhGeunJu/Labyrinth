import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import troll_preview as T
from PIL import Image, ImageDraw
angs = (0, 30, 90, 180)
im = Image.new('RGB', (300 * len(angs), 380 * 2), (40, 40, 44))
for r, neck in enumerate(('Collar', 'Off')):
    T.PICK['Neck'] = neck
    P = T.pieces()
    for i, a in enumerate(angs): im.paste(T.render(P, a, 300, 380, 0.95, 2.35), (i * 300, r * 380))
im.save(sys.argv[1]); print('saved')
