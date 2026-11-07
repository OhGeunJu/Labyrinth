"""오크 머리카락 전부를 한 장에(남자 `orc_parts` · 여성 `../orc_female/hair_v2`) — 머리마다 게임 카메라(앞 위)와 뒤 3/4.

    python all_hair_sheet.py renders/all_hair.png [S]
"""
import os as _os, sys as _sys
_here = _os.path.dirname(_os.path.abspath(__file__))
_sys.path.insert(0, _os.path.join(_here, '..', 'common'))
import sys
from multiprocessing import Pool
from PIL import Image, ImageDraw

def male():
    import orc_parts as o
    return [('M Topknot', o.h_topknot, o.b_none, 1.45), ('M WarKnot', o.h_warknot, o.b_none, 1.45), ('M Ponytail', o.h_ponytail, o.b_none, 1.45),
            ('M Mohawk', o.h_mohawk, o.b_none, 1.45), ('M Braids', o.h_braids, o.b_none, 1.45), ('M Mane', o.h_mane, o.b_braidfull, 1.6),
            ('M KnotTail', o.h_knottail, o.b_bushy, 2.2), ('M HighTail', o.h_hightail, o.b_none, 1.6)]

FEMALE = ['h_high_pony', 'h_pony_no_temple', 'h_pony_a_back', 'h_pony_b_back', 'h_pony_back_up', 'h_mohawk', 'h_twin_tails', 'h_long_braids', 'h_spiky_bob', 'h_crest_tail']
FNAMES = ['F PonyA', 'F PonyB', 'F PonyA2', 'F PonyB2', 'F PonyC', 'F Mohawk', 'F TwinTails', 'F LongBraids', 'F SpikyBob', 'F CrestTail']

def tile(job):
    kind, i, yaw, pitch, S = job
    if kind == 'M':
        _sys.path.insert(0, _here)
        import orc_parts as o
        name, h, b, span = male()[i]
        cy = -0.30 if span > 2 else -0.10
        return o.render(o.pieces(h, b), yaw, S, S, cy, span, pitch).tobytes()
    _sys.path.insert(0, _os.path.join(_here, '..', 'orc_female'))
    _os.chdir(_os.path.join(_here, '..', 'orc_female'))
    import hair_v2 as H
    from orc_preview import render
    return render(H.pieces(getattr(H, FEMALE[i])), yaw, S, S, -0.10, 1.45, pitch).tobytes()

if __name__ == '__main__':
    out = sys.argv[1]; S = int(sys.argv[2]) if len(sys.argv) > 2 else 200
    names = [n for n, *_ in male()] + FNAMES
    jobs = [('M', i, y, p, S) for i in range(8) for y, p in ((20, 35), (150, 25))] + [('F', i, y, p, S) for i in range(10) for y, p in ((20, 35), (150, 25))]
    with Pool(4) as pool: tiles = pool.map(tile, jobs)
    cols = 6   # 머리 셋씩 한 줄(머리마다 두 칸)
    rows = (len(names) + 2) // 3
    im = Image.new('RGB', (S * cols, S * rows), (40, 40, 44)); d = ImageDraw.Draw(im)
    for k, name in enumerate(names):
        r, c = divmod(k, 3)
        for j in range(2):
            im.paste(Image.frombytes('RGB', (S, S), tiles[k * 2 + j]), ((c * 2 + j) * S, r * S))
        d.text((c * 2 * S + 6, r * S + 4), name, fill=(235, 235, 235))
    im.save(out); print('saved', out)
