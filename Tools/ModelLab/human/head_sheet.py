"""머리 모양 열두 벌을 같은 머리카락 · 얼굴로 나란히 — 어느 얼굴형을 남길지 고르는 판.
실행: python head_sheet.py renders/heads_v1.png"""
import sys
import numpy as np
from PIL import Image, ImageDraw
import human as H

ORDER = ['BroadSoft', 'BroadSquare', 'SlimSoft', 'SlimSharp', 'SlimSquare']   # BroadSharp는 뺐다(`human.DROPPED`)
S, Hh = 240, 300

def cell(job):
    name, yaw, hair, col = job
    return np.asarray(H.render(H.pieces(name, 'Button', 'Soft', hair, col), yaw, S, Hh, -0.12, 1.25))

if __name__ == '__main__':
    from multiprocessing import Pool
    jobs = []
    for g, hair, col in (('Male', 'Crop', H.BROWN), ('Female', 'Bob', H.AUBURN)):
        for yaw in (0, 40):
            for o in ORDER: jobs.append((f'{g}_{o}', yaw, hair, col))
    with Pool(12) as pool: ims = pool.map(cell, jobs)
    sheet = Image.new('RGB', (S * len(ORDER), Hh * 4), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for i, im in enumerate(ims):
        r, c = divmod(i, len(ORDER))
        sheet.paste(Image.fromarray(im), (c * S, r * Hh))
        d.text((c * S + 6, r * Hh + 4), f'{jobs[i][0]}  {jobs[i][1]}deg', fill=(255, 220, 120))
    sheet.save(sys.argv[1]); print('saved', sys.argv[1])
