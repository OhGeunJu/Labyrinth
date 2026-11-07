"""머리카락 × 머리 모양 — 같은 디자인이 큰 머리 · 작은 머리 모두에 맞나 보는 판(정면 · 한 각도).
11-07 사용자: *"단발 머리가 큰 머리에 있으면 작은 머리에도 그거랑 같은 디자인의 단발 머리가 있어야해"* — 머리카락은 머리 모양을 받아 빚는
함수라 모든 머리에 같은 벌이 있다. 줄 = 머리카락, 칸 = 머리 모양(남녀 각 다섯).
실행: python hair_grid.py renders/hair_grid_v1.png [각도 0]"""
import sys
import numpy as np
from PIL import Image, ImageDraw
import human as H

SHAPES = ['BroadSoft', 'BroadSquare', 'SlimSoft', 'SlimSharp', 'SlimSquare']
MALE = ['Crop', 'Side', 'Buzz', 'Slick', 'Spiky', 'Shaggy', 'ManBun']
FEMALE = ['Bob', 'Long', 'Pony', 'Bun', 'Braid', 'Pixie']
COL = {'Crop': H.BROWN, 'Side': H.BLACK, 'Buzz': H.BLACK, 'Slick': H.BROWN, 'Spiky': H.BLOND, 'Shaggy': H.AUBURN, 'ManBun': H.GREY,
       'Bob': H.AUBURN, 'Long': H.BLOND, 'Pony': H.BROWN, 'Bun': H.BLACK, 'Braid': H.PLATN, 'Pixie': H.BLACK}
S, Hh = 200, 240

def cell(job):
    head, hair, yaw = job
    return np.asarray(H.render(H.pieces(head, 'Button', 'Soft', hair, COL[hair]), yaw, S, Hh, -0.18, 1.45))

if __name__ == '__main__':
    from multiprocessing import Pool
    yaw = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    rows = [('Male', h) for h in MALE] + [('Female', h) for h in FEMALE]
    jobs = [(f'{g}_{s}', hair, yaw) for g, hair in rows for s in SHAPES]
    with Pool(12) as pool: ims = pool.map(cell, jobs)
    sheet = Image.new('RGB', (S * len(SHAPES), Hh * len(rows)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for i, im in enumerate(ims):
        r, c = divmod(i, len(SHAPES))
        sheet.paste(Image.fromarray(im), (c * S, r * Hh))
        d.text((c * S + 4, r * Hh + 3), f'{jobs[i][1]} / {jobs[i][0]}', fill=(255, 220, 120))
    sheet.save(sys.argv[1]); print('saved', sys.argv[1])
