"""옮겨 온 머리카락 판 — 줄마다 한 벌 · 칸은 큰 머리(정면 · 40° · 뒤)와 작은 머리(정면).
실행: python port_sheet.py renders/port_v1.png [종류 elf,dwarf,orcF,orcM,human] [human 성별 Male|Female] [이 머리카락만 TwinTails,Braid]"""
import sys
import numpy as np
from PIL import Image, ImageDraw
import port
from head_base import render

ROWS = {'elf': ['Straight', 'HalfUp', 'Tail', 'BangsM', 'BangsArch'],
        'orcF': ['HighPony', 'TwinTails', 'LongBraids', 'SpikyBob', 'CrestTail', 'Mohawk'],
        'dwarf': ['Fringe', 'Long', 'Braid', 'Crest', 'Mane', 'TwinBraids', 'LongFringe', 'Topknot'],
        'orcM': ['Swept'],
        'human': ['Long', 'LongFringe', 'Mane', 'Braid', 'TwinBraids', 'Topknot', 'TwinTails', 'TwinTailsV', 'LongBraids', 'Slick', 'SidePart', 'TwoBlock', 'ShortFringe', 'Messy', 'Spiky', 'Dandy', 'Comma', 'Dreads', 'FauxHawk', 'Pixie', 'Crop', 'BaldWings', 'SlickMullet', 'BuzzCut', 'LowTail', 'HalfBun', 'Military', 'Pomp', 'ManBun', 'Shoulder', 'Curtain', 'BangsMShort']}
GENDER = {'elf': 'Female', 'orcF': 'Female', 'dwarf': 'Male', 'orcM': 'Male', 'human': 'Male'}
if len(sys.argv) > 3: GENDER['human'] = sys.argv[3]
import os
CLOSE = os.environ.get('CLOSE') == '1'   # 얼굴 가까이(앞머리 · 옆 가닥 보기)
CELLS = [('BroadSoft', 0), ('BroadSoft', 30), ('BroadSoft', 70), ('SlimSoft', 0)] if CLOSE else [('BroadSoft', 0), ('BroadSoft', 40), ('BroadSoft', 180), ('SlimSoft', 0)]
if os.environ.get('SIDE') == '1': CELLS = [('BroadSoft', 0), ('BroadSoft', 90), ('BroadSoft', 140), ('BroadSoft', 180)]   # 옆 · 뒤(번 · 꽁지 보기)
S, Hh = 220, 280

def cell(job):
    kind, hair, shape, yaw = job
    return np.asarray(render(port.pieces(kind, f'{GENDER[kind]}_{shape}', hair), yaw, S, Hh, *((0.0, 0.95) if CLOSE else (-0.22, 1.6))))

if __name__ == '__main__':
    from multiprocessing import Pool
    kinds = sys.argv[2].split(',') if len(sys.argv) > 2 else list(ROWS)
    only = sys.argv[4].split(',') if len(sys.argv) > 4 else None    # 이 머리카락만(예: TwinTails)
    rows = [(k, hr) for k in kinds for hr in ROWS[k] if only is None or hr in only]
    jobs = [(k, hr, s, y) for k, hr in rows for s, y in CELLS]
    with Pool(12) as pool: ims = pool.map(cell, jobs)
    sheet = Image.new('RGB', (S * len(CELLS), Hh * len(rows)), (40, 40, 44)); d = ImageDraw.Draw(sheet)
    for i, im in enumerate(ims):
        r, c = divmod(i, len(CELLS))
        sheet.paste(Image.fromarray(im), (c * S, r * Hh))
        if c == 0: d.text((4, r * Hh + 3), f'{jobs[i][0]} / {jobs[i][1]}', fill=(255, 220, 120))
    sheet.save(sys.argv[1]); print('saved', sys.argv[1])
