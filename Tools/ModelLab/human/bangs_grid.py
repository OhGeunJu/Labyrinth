"""앞머리 판 — 줄 = 여자 뒷머리, 칸 = 앞머리 없음 · 짧은 · 눈썹 · 긴 · 옆으로 넘긴(정면 · 넓은 부드러운 여자 머리).
실행: python bangs_grid.py renders/bangs_v1.png [각도 0]"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import port
from head_base import render

BACKS = sys.argv[4].split(',') if len(sys.argv) > 4 else ['Straight', 'Shoulder', 'Tail', 'HalfUp']
KINDS = sys.argv[3].split(',') if len(sys.argv) > 3 else ['None', 'Clumps', 'Sweep', 'Wispy']
NAMES = {'None': '앞머리 없음', 'Clumps': '다발', 'Sweep': '옆으로 쓸린', 'Wispy': '가늘게 갈라진', 'Parted': '갈라 넘긴', 'SideOpen': '한쪽 깐', 'Open': '깐 머리'}
S, Hh = 170, 190

def cell(job):
    b, k, yaw = job
    return np.asarray(render(port.pieces('human', 'Female_BroadSoft', f'{b}+{k}'), yaw, S, Hh, 0.0, 1.05))

if __name__ == '__main__':
    from multiprocessing import Pool
    yaw = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    jobs = [(b, k, yaw) for b in BACKS for k in KINDS]
    with Pool(12) as pool: ims = pool.map(cell, jobs)
    font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 13)
    sheet = Image.new('RGB', (S * len(KINDS), (Hh + 18) * len(BACKS) + 18), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for c, k in enumerate(KINDS): d.text((c * S + 4, 2), NAMES[k], fill=(255, 220, 120), font=font)
    for i, im in enumerate(ims):
        r, c = divmod(i, len(KINDS))
        sheet.paste(Image.fromarray(im), (c * S, 18 + r * (Hh + 18) + 18))
        if c == 0: d.text((4, 18 + r * (Hh + 18) + 2), BACKS[r], fill=(200, 200, 200), font=font)
    sheet.save(sys.argv[1]); print('saved', sys.argv[1])
