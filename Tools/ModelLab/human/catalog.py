"""사람 머리카락 후보 한 장 — 번호를 붙여 고르기 좋게(정면 · 40°). 남자 후보는 남자 머리, 여자 후보는 여자 머리(넓은 부드러운)에 씌운다.
실행: python catalog.py renders/catalog_v1.png"""
import sys
import numpy as np
from PIL import Image, ImageDraw
import port
from head_base import render

# (번호 붙일 이름, 원본 종류, 원본 이름, 그릴 성별)
ITEMS = [
    ('올려 넘긴 머리', 'human', 'Pomp', 'Male'),
    ('올백', 'human', 'Slick', 'Male'),
    ('M자 보통 기장', 'human', 'BangsMShort', 'Male'),
    ('짧은 앞머리', 'human', 'ShortFringe', 'Male'),
    ('덥수룩한 짧은 머리', 'human', 'Messy', 'Male'),
    ('뾰족 머리', 'human', 'Spiky', 'Male'),
    ('바짝 민 머리', 'human', 'BuzzCut', 'Male'),
    ('댄디', 'human', 'Dandy', 'Male'),
    ('쉼표 머리', 'human', 'Comma', 'Male'),
    ('가운데 세운 머리', 'human', 'FauxHawk', 'Male'),
    ('숏컷', 'human', 'Pixie', 'Male'),
    ('언더컷 + 레게', 'human', 'Dreads', 'Male'),
    ('짧게 깎은 머리', 'human', 'Crop', 'Male'),
    ('벗겨진 머리 · 옆 뻗친(남)', 'human', 'BaldWings', 'Male'),
    ('볏 머리', 'human', 'Crest', 'Male'),
    ('벗겨진 머리(남)', 'human', 'Fringe', 'Male'),
    ('벗겨진 긴 머리(남)', 'human', 'LongFringe', 'Male'),
    ('맨번', 'human', 'ManBun', 'Male'),
    ('낮게 묶은 꽁지', 'human', 'LowTail', 'Male'),
    ('반묶음', 'human', 'HalfBun', 'Male'),
    ('상투', 'human', 'Topknot', 'Male'),
    ('땋은 머리', 'human', 'Braid', 'Male'),
    ('엘프 반묶음', 'elf', 'HalfUp', 'Male'),
    ('볏 꽁지', 'orcF', 'CrestTail', 'Male'),
    ('쌍꼬리 A', 'human', 'TwinTails', 'Male'),
    ('쌍꼬리 B', 'human', 'TwinTailsV', 'Male'),
    ('쌍갈래', 'human', 'TwinBraids', 'Male'),
    ('긴 땋은 머리', 'human', 'LongBraids', 'Male'),
    ('뒤로 넘긴 긴 머리', 'human', 'Long', 'Male'),
    ('갈기', 'human', 'Mane', 'Male'),
    ('어깨 기장', 'human', 'Shoulder', 'Male'),
    ('가운데 가르마 커튼', 'human', 'Curtain', 'Male'),
    ('넘긴 장발', 'human', 'SlickMullet', 'Male'),
    ('엘프 생머리', 'elf', 'Straight', 'Male'),
    ('엘프 M자 앞머리', 'elf', 'BangsM', 'Male'),
    ('엘프 아치 앞머리', 'elf', 'BangsArch', 'Male'),
    ('뾰족 단발', 'orcF', 'SpikyBob', 'Male'),
    ('모히칸', 'orcF', 'Mohawk', 'Male'),
]
S, Hh, COLS = 170, 210, 5   # 칸 하나 = 정면 + 40° 두 장

def cell(job):
    kind, hair, g, yaw = job
    return np.asarray(render(port.pieces(kind, f'{g}_BroadSoft', hair), yaw, S, Hh, -0.20, 1.55))

if __name__ == '__main__':
    from multiprocessing import Pool
    jobs = [(k, h, g, y) for _, k, h, g in ITEMS for y in (0, 40)]
    with Pool(12) as pool: ims = pool.map(cell, jobs)
    rows = (len(ITEMS) + COLS - 1) // COLS
    W = S * 2 + 8
    sheet = Image.new('RGB', (W * COLS, (Hh + 22) * rows), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    try:
        from PIL import ImageFont
        font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 15)
    except Exception:
        font = None
    for i, (name, *_ ) in enumerate(ITEMS):
        r, c = divmod(i, COLS); x0, y0 = c * W, r * (Hh + 22)
        sheet.paste(Image.fromarray(ims[2 * i]), (x0, y0 + 22)); sheet.paste(Image.fromarray(ims[2 * i + 1]), (x0 + S, y0 + 22))
        d.text((x0 + 4, y0 + 2), f'{i + 1}. {name}', fill=(255, 220, 120), font=font)
    sheet.save(sys.argv[1]); print('saved', sys.argv[1])
