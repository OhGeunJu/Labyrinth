"""사람 얼굴 — 코 다섯 · 입 여섯(가까이 · 정면 / 40° / 옆). 실행: python face_sheet.py renders/face_v1.png"""
import sys
import numpy as np
from PIL import Image, ImageDraw
import human as H

S, Hh = 230, 230
NOSES = list(H.NOSES)
def cell(job):
    nose, mouth, yaw = job
    h = H.Head('Male_BroadSoft')
    return np.asarray(H.render(H.pieces('Male_BroadSoft', nose, 'Soft', None, H.BROWN, mouth), yaw, S, Hh, (h.NY + h.MY) * 0.5 + 0.03, 0.50))   # 코 · 입 가까이(옆에서도 코가 판 안에)

def side(n):
    h = H.Head('Male_BroadSoft')
    return np.asarray(H.render(H.pieces('Male_BroadSoft', n, 'Soft', None, H.BROWN), 90, S, Hh, h.NY + 0.03, 0.80))

if __name__ == '__main__':
    from multiprocessing import Pool
    if len(sys.argv) > 2 and sys.argv[2] == 'side':   # 코 옆모습만
        with Pool(7) as pool: ims = pool.map(side, NOSES)
        sheet = Image.new('RGB', (S * len(NOSES), Hh)); d = ImageDraw.Draw(sheet)
        for i, im in enumerate(ims): sheet.paste(Image.fromarray(im), (S * i, 0)); d.text((S * i + 4, 4), NOSES[i], fill=(255, 220, 120))
        sheet.save(sys.argv[1]); print('saved', sys.argv[1]); sys.exit()
    jobs = [(n, 'Line', y) for n in NOSES for y in (0, 40, 90)] + [(NOSES[0], m, y) for m in H.MOUTHS for y in (0, 30)]
    with Pool(12) as pool: ims = pool.map(cell, jobs)
    cols = 6
    rows_n = (len(NOSES) * 3 + cols - 1) // cols + (len(H.MOUTHS) * 2 + cols - 1) // cols
    sheet = Image.new('RGB', (S * cols, Hh * rows_n + 40), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    try:
        from PIL import ImageFont; font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
    except Exception: font = None
    for i, im in enumerate(ims):
        if i < len(NOSES) * 3: r, c = divmod(i, cols); y0 = 0
        else:
            k = i - len(NOSES) * 3; r, c = divmod(k, cols); r += (len(NOSES) * 3 + cols - 1) // cols; y0 = 40
        sheet.paste(Image.fromarray(im), (c * S, r * Hh + y0))
        n, m, yaw = jobs[i]
        d.text((c * S + 4, r * Hh + y0 + 3), f'코 {n} · {yaw}°' if i < len(NOSES) * 3 else f'입 {m} · {yaw}°', fill=(255, 220, 120), font=font)
    sheet.save(sys.argv[1]); print('saved', sys.argv[1])
