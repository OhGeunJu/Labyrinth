"""엘프 몸 비례 — `ChibiPawnCreator.Proto`의 `BodyDims` 식(머리 높이 배수)으로 정면 개략도를 그려 사람 남성과 견준다.
실행: python elf_body.py renders/elf_body_v1.png"""
import sys
import numpy as np
from PIL import Image, ImageDraw

HH, HW = 0.688, 0.628 * 0.88          # 머리 높이 · 폭(m) — 엘프는 얼굴을 좁힌다(`elf.NAR`)
PTorsoH, PLegVis, PShoeH, PArmLen, PShoulder, PArmW, PLegW = 0.96, 0.40, 0.22, 0.56, 0.10, 0.20, 0.28
ST = [0.0, 0.30, 0.55, 0.80, 1.0]
# 이름: (반폭 밑·엉덩이·허리·가슴·어깨, 손, ArmK, LegK, TorsoK, 귀)
SH = {
    'Human Male':   ((0.21, 0.24, 0.26, 0.30, 0.31), 0.30, 1.00, 1.00, 1.00, False),
    'Elf A':        ((0.19, 0.21, 0.20, 0.25, 0.27), 0.26, 1.20, 1.70, 1.08, True),
    'Elf B':        ((0.19, 0.21, 0.19, 0.25, 0.27), 0.25, 1.32, 2.20, 1.14, True),
    'Elf C':        ((0.18, 0.20, 0.18, 0.24, 0.26), 0.24, 1.45, 2.70, 1.20, True),
}

PX = 150; Wc = 300; Hpx = 620; ground = Hpx - 40
im = Image.new('RGB', (Wc * len(SH), Hpx), (40, 40, 44)); d = ImageDraw.Draw(im)
SKIN = (237, 204, 178); TUNIC = (72, 97, 66); PANTS = (70, 58, 44); SHOE = (50, 40, 32); HAIR = (234, 224, 188)
def Y(m): return ground - m * PX
for c, (name, (half, hand, ak, lk, tk, ears)) in enumerate(SH.items()):
    cx = c * Wc + Wc // 2
    shoe = PShoeH * HH; lift = shoe + PLegVis * lk * HH; torso = PTorsoH * tk * HH; top = lift + torso
    legx = half[1] * 0.16 / 0.24; legr = half[1] * (PLegW * 0.5 / 0.24)
    for sx in (-1, 1):   # 다리 · 신발
        x0 = cx + sx * legx * PX
        d.rounded_rectangle((x0 - legr * PX, Y(lift + 0.10), x0 + legr * PX, Y(shoe * 0.6)), 8, fill=PANTS, outline=(10, 10, 10), width=2)
        d.rounded_rectangle((x0 - legr * 1.18 * PX, Y(shoe), x0 + legr * 1.18 * PX, Y(0)), 8, fill=SHOE, outline=(10, 10, 10), width=2)
    armr = PArmW * 0.5 * (half[4] / 0.31) ** 0.5 * HH * 0.9
    sh_y = top - PShoulder * HH; hand_y = sh_y - PArmLen * ak * HH
    for sx in (-1, 1):   # 팔 · 손
        x0 = cx + sx * (half[4] + armr * 0.4) * PX
        d.rounded_rectangle((x0 - armr * PX, Y(sh_y + 0.04), x0 + armr * PX, Y(hand_y)), int(armr * PX), fill=TUNIC, outline=(10, 10, 10), width=2)
        hr = hand * 0.5 * HH * 0.55
        d.ellipse((x0 - hr * PX, Y(hand_y + hr), x0 + hr * PX, Y(hand_y - hr)), fill=SKIN, outline=(10, 10, 10), width=2)
    ys = np.linspace(0, 1, 40); hs = np.interp(ys, ST, half)   # 몸통 옆선
    pts = [(cx + h * PX, Y(lift + y * torso)) for y, h in zip(ys, hs)] + [(cx - h * PX, Y(lift + y * torso)) for y, h in zip(ys[::-1], hs[::-1])]
    d.polygon(pts, fill=TUNIC, outline=(10, 10, 10))
    hw = HW if ears else 0.628
    hb = top - 0.06; ht = hb + HH
    if ears:
        for sx in (-1, 1):   # 귀(Long) 개략
            ex = cx + sx * hw * 0.42 * PX; ey = Y(hb + HH * 0.45)
            d.polygon([(ex, ey - 8), (ex + sx * 0.22 * PX, ey - 0.14 * PX), (ex, ey + 10)], fill=SKIN, outline=(10, 10, 10))
    d.ellipse((cx - hw * 0.5 * PX, Y(ht), cx + hw * 0.5 * PX, Y(hb)), fill=SKIN, outline=(10, 10, 10), width=2)
    total = ht
    d.text((c * Wc + 8, 6), name, fill=(255, 220, 120))
    d.text((c * Wc + 8, 22), f'legK {lk:.2f}  torsoK {tk:.2f}  armK {ak:.2f}', fill=(220, 220, 220))
    d.text((c * Wc + 8, 38), f'height {total / HH:.2f} heads ({total:.2f} m)', fill=(220, 220, 220))
d.line((0, ground, im.width, ground), fill=(90, 90, 96))
im.save(sys.argv[1]); print('saved', sys.argv[1])
