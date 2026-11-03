"""드워프 몸 비례 — 사람 치비 몸(`ChibiPawnCreator.Proto` · `BodyDims` · `DimsOf`)을 그대로 옮겨 체형 줄 하나만 바꿔 본다.
남성 · 헐크 · 드워프를 나란히. 실행: python body.py renders/body_v1.png
수치는 C#(`Shape` 한 줄)과 같은 뜻이다 — 반폭(밑·엉덩이·허리·가슴·어깨) · 깊이 · 손 · 팔 · 다리 · 몸통 배수."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))
import sys
import numpy as np
from PIL import Image, ImageDraw
from brute_preview import V, length, smin, smax, sph, capsule, rbox
import dwarf as dw
from head_base import render, H

# ── C# 상수(ChibiPawnCreator.Proto) ──
PTorsoH, PLegVis, PLegW, PLegX, PHipIn, PPantsH = 0.96, 0.40, 0.28, 0.16, 0.20, 0.22
PShoeH, PShoeW, PShoeLen, PShoeFwd, PHand = 0.22, 0.33, 0.50, 0.08, 0.32
PArmLen, PArmW, PArmIn, PShoulder, PDepth, ShoulderDrop = 0.56, 0.20, 0.30, 0.10, 0.80, 0.03
PTorsoHalf = [0.32, 0.33, 0.32, 0.34, 0.30]; Stations = [0, 0.30, 0.55, 0.80, 1.0]
PBottomRound, TopRound, HeadGap, HeadZ = 0.35, 0.55, 0.065, 0.04
HH = 0.688   # 기준 머리 높이(Male_BroadSoft = 68px)

#           이름        반폭(밑 엉덩이 허리 가슴 어깨)        깊이  손    팔K  다리K 몸통K  팔굵기 다리굵기 머리 어깨둥글림
# 팔굵기 · 다리굵기 · 머리 · 어깨둥글림은 C# `Shape`에 아직 없는 칸 — 사람은 팔 굵기가 손에서, 다리 굵기가 엉덩이에서 나온다(그 비에 곱한다) ·
# 어깨둥글림은 몸통 꼭대기를 깎는 반지름(× 어깨 반폭 · 사람 `TopRound` 0.55) — 작을수록 승모근이 목까지 차오른다.
MALE  = ('Male',  [0.21, 0.24, 0.26, 0.30, 0.31], 0.82, 0.30, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.55)
HULK  = ('Hulk',  [0.27, 0.31, 0.34, 0.41, 0.43], 0.90, 0.34, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 0.55)
# v2(11-03 사용자: 키 1.65 · 머리 작게 · 팔 자체를 굵게 · 손 · 다리는 줄여도) — 다리는 짧게 두고 모자란 키는 몸통이 채운다(통짜 몸)
# v3(사용자: 승모근이 어깨보다 너무 낮다 · 다리가 너무 얇다 — 근육질) — 어깨둥글림 0.28 · 다리굵기 1.10 · 엉덩이를 넓혀 두 다리가 안 붙게
DWARF = ('Dwarf', [0.32, 0.36, 0.39, 0.43, 0.45], 0.96, 0.36, 0.95, 0.55, 1.03, 1.45, 1.10, 0.88, 0.28)

def dims(s):   # DimsOf + BodyDims
    _, half, dep, hand, armK, legK, torsoK, armW, legW, headK, topR = s
    m = MALE
    hf = [PTorsoHalf[i] * HH * (half[1 if i == 0 else i] / m[1][1 if i == 0 else i]) for i in range(5)]
    d = dict(half=hf, depth=PDepth * dep / m[2], topR=topR)
    d['hand'] = PHand * HH * hand / m[3]
    d['armR'] = d['hand'] * (PArmW / PHand) * 0.5 * armW   # 손에서 나온 굵기 × 팔굵기
    d['legX'] = hf[1] * PLegX / PTorsoHalf[1]; d['legR'] = hf[1] * PLegW * 0.5 / PTorsoHalf[1] * legW
    d['headK'] = headK
    d['torsoH'] = PTorsoH * torsoK * HH
    d['lift'] = (PShoeH + PLegVis * legK) * HH; d['top'] = d['lift'] + d['torsoH']
    d['hem'] = d['lift'] + PPantsH * HH
    d['hipY'] = d['lift'] + PHipIn * HH; d['footY'] = PShoeH * HH * 0.5
    d['shoe'] = (d['legR'] * 2 * PShoeW / PLegW, PShoeH * HH, PShoeLen * HH)
    # 위팔 관절 — 굵어진 반지름만큼 내린다(C# `BodyDims` — 팔 윗끝이 몸통 꼭대기 위로 솟지 않게 · 사람은 0)
    thick = d['armR'] - d['armR'] / armW
    d['shY'] = d['top'] - PShoulder * HH - ShoulderDrop - thick
    d['armX'] = torso_r(hf, 1 - PShoulder / PTorsoH, d['torsoH'], topR) + d['armR'] * 2 * PArmIn
    d['handY'] = d['shY'] + ShoulderDrop - PArmLen * armK * HH
    return d

def torso_r(half, t, th, topR=TopRound):
    t = np.clip(t, 0, 1); y = t * th; w = np.interp(t, Stations, half)
    rb = half[0] * PBottomRound; rt = half[-1] * topR
    lo = w - rb + np.sqrt(np.maximum(0, rb * rb - (rb - y) ** 2))
    dd = th - y; hi = w - rt + np.sqrt(np.maximum(0, rt * rt - (rt - dd) ** 2))
    return np.where(y < rb, lo, np.where(y > th - rt, hi, w))

def figure(s, beard, hair, col, brow):
    d = dims(s)
    def torso(p):
        y = p[..., 1] - d['lift']; t = y / d['torsoH']
        r = torso_r(d['half'], t, d['torsoH'], d['topR'])
        q = np.sqrt(p[..., 0] ** 2 + (p[..., 2] / d['depth']) ** 2)
        return np.maximum((q - r) * d['depth'], np.maximum(-y, y - d['torsoH']))
    def shirt(p): return np.maximum(torso(p), d['hem'] - p[..., 1])
    def pants(p): return np.maximum(torso(p), p[..., 1] - d['hem'])
    def legs(p):
        q = np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
        y0 = max(d['footY'], d['legR'] + 0.01)   # C# `LegBottom` — 아래 반구가 신발 바닥을 뚫지 않게
        return capsule(q, V(d['legX'], y0, 0), V(d['legX'], d['hipY'], 0), d['legR'])
    def shoes(p):
        q = np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1); sw, sh, sl = d['shoe']
        return rbox(q, V(d['legX'], sh * 0.5, PShoeFwd * HH), V(sw * 0.5, sh * 0.5, sl * 0.5), min(sw, sh) * 0.4)
    def arms(p):   # 늘어뜨린 팔(게임에서는 대기 동작) — 어깨에서 손목까지
        q = np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
        return capsule(q, V(d['armX'], d['handY'], 0), V(d['armX'], d['shY'], 0), d['armR'])
    def hands(p):
        q = np.stack([np.abs(p[..., 0]), p[..., 1], p[..., 2]], -1)
        return sph(q, V(d['armX'], d['handY'] - d['hand'] * 0.25, 0), d['hand'] * 0.5)
    # (팔 길이는 어깨에서 잰다 — 관절이 내려간 만큼 손도 내려간다)
    k = d['headK']; hy = d['top'] + HeadGap + H * 0.5 * k
    def at(f): return lambda p: f((p - V(0, hy, HeadZ)) / k) * k
    head = [(at(f), c) for f, c in dw.pieces(beard, hair, col, brow) if f is not dw.body]
    SHIRT, PANTS, SHOE = (0.36, 0.30, 0.24), (0.24, 0.24, 0.30), (0.22, 0.16, 0.12)
    return head + [(shirt, SHIRT), (pants, PANTS), (legs, PANTS), (shoes, SHOE), (arms, SHIRT), (hands, dw.SKIN)], d

LINEUP = [(MALE, dw.h_bald, dw.b_box, dw.BROWN), (HULK, dw.h_bald, dw.b_box, dw.BROWN), (DWARF, dw.h_braid, dw.b_braids, dw.GINGER)]
VIEWS = (0, 40)
def job(a):
    i, yaw = a; s, hair, beard, col = LINEUP[i]
    P, _ = figure(s, beard, hair, col, dw.e_bushy)
    return np.asarray(render(P, yaw, 300, 560, 1.02, 2.30))

# 긴 머리 보기(전신 — 어깨 · 가슴과 같이): python body.py out.png hairs
HAIR_SHOW = [('Long', 'Box', 'BROWN'), ('Mane', 'Bushy', 'GREY'), ('TwinBraids', 'Stache', 'GINGER'), ('LongFringe', 'Long', 'GREY'), ('Topknot', 'Forked', 'RED')]
HAIR_VIEWS = (0, 50, 150)
def hair_job(a):
    i, yaw = a; hn, bn, cn = HAIR_SHOW[i]
    P, _ = figure(DWARF, dw.BEARDS[bn], dw.HAIRS[hn], getattr(dw, cn), dw.e_bushy)
    return np.asarray(render(P, yaw, 300, 520, 1.00, 1.95))

if __name__ == '__main__' and len(sys.argv) > 2 and sys.argv[2] == 'hairs':
    from multiprocessing import Pool
    jobs = [(i, v) for i in range(len(HAIR_SHOW)) for v in HAIR_VIEWS]
    with Pool(len(jobs)) as pool: ims = pool.map(hair_job, jobs)
    sheet = Image.new('RGB', (300 * len(HAIR_VIEWS), 520 * len(HAIR_SHOW)), (40, 40, 44)); dr = ImageDraw.Draw(sheet)
    for (i, v), im in zip(jobs, ims):
        sheet.paste(Image.fromarray(im), (HAIR_VIEWS.index(v) * 300, i * 520))
    for i, (hn, bn, _) in enumerate(HAIR_SHOW): dr.text((6, i * 520 + 4), f'hair {hn} (beard {bn})', fill=(255, 220, 120))
    sheet.save(sys.argv[1]); print('saved', sys.argv[1]); sys.exit()

if __name__ == '__main__':
    from multiprocessing import Pool
    out = sys.argv[1]
    for s, *_ in LINEUP:
        d = dims(s); print(s[0], 'height %.2f m' % (d['top'] + HeadGap + H * d['headK']), 'hand %.3f' % d['hand'], 'arm r %.3f' % d['armR'], 'leg r %.3f' % d['legR'])
    jobs = [(i, v) for v in VIEWS for i in range(len(LINEUP))]
    with Pool(len(jobs)) as pool: ims = pool.map(job, jobs)
    sheet = Image.new('RGB', (300 * len(LINEUP), 560 * len(VIEWS)), (40, 40, 44)); dr = ImageDraw.Draw(sheet)
    for k, ((i, v), im) in enumerate(zip(jobs, ims)):
        sheet.paste(Image.fromarray(im), (i * 300, VIEWS.index(v) * 560))
        dr.text((i * 300 + 6, VIEWS.index(v) * 560 + 4), LINEUP[i][0][0], fill=(255, 220, 120))
    for y in range(0, 560 * len(VIEWS), 560): dr.line([(0, y + 560 * (1.02 + 1.15) / 2.30), (900, y + 560 * (1.02 + 1.15) / 2.30)], fill=(70, 70, 76))
    sheet.save(out); print('saved', out)
