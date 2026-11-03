import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
from PIL import Image
import hair_v2 as Hv
from orc_preview import render
S = 240; which = sys.argv[1]
fn = {'A': Hv.h_high_pony, 'B': Hv.h_pony_no_temple, 'A2': Hv.h_pony_a_back, 'B2': Hv.h_pony_b_back, 'C': Hv.h_pony_back_up}[which]
P = Hv.pieces(fn)
im = Image.new('RGB', (S * 5, S))
for c, a in enumerate((0, 35, 90, 150, 200)): im.paste(render(P, a, S, S, -0.10, 1.40), (c * S, 0))
im.save(f'pony9_{which}.png'); print('done', which)
