"""체형 표(ChibiPawnCreator.Shapes)의 반폭으로 몸통 앞 실루엣을 그려 견준다 — 개략도(팔 · 다리는 손 · 엉덩이 비로만)."""
from PIL import Image, ImageDraw
import numpy as np
ST = [0.0, 0.30, 0.55, 0.80, 1.0]
SH = {  # 이름: (밑, 엉덩이, 허리, 가슴, 어깨, 깊이, 손)
    'Male':   (0.21, 0.24, 0.26, 0.30, 0.31, 0.82, 0.30),
    'Hulk':   (0.27, 0.31, 0.34, 0.41, 0.43, 0.90, 0.34),
    'Female': (0.23, 0.24, 0.21, 0.27, 0.22, 0.80, 0.27),
}
# 여성 헐크 v2 — 다리는 엉덩이 반폭이 정한다(DimsOf) → 엉덩이를 줄여 다리를 좁힌다 · 어깨는 여성답게 작게
SH['HulkFemale A'] = (0.27, 0.27, 0.25, 0.35, 0.28, 0.86, 0.30)
SH['HulkFemale B'] = (0.26, 0.26, 0.24, 0.34, 0.26, 0.85, 0.29)
del SH['Male']
for k, v in SH.items(): print(f'{k:12s}', v)

S = 300; Hpx = 420; im = Image.new('RGB', (S * len(SH), Hpx), (40, 40, 44)); d = ImageDraw.Draw(im)
male = (0.21, 0.24, 0.26, 0.30, 0.31, 0.82, 0.30)
for c, (name, s) in enumerate(SH.items()):
    cx = c * S + S // 2; base = 300; th = 170; k = 260
    half = [s[1] if i == 0 else s[i] for i in range(5)]
    half = [x * (0.50 / male[1]) * 0.5 for x in half]   # 개략 눈금
    ys = np.linspace(0, 1, 40); hs = np.interp(ys, ST, half)
    pts = [(cx + hx * k, base - y * th) for y, hx in zip(ys, hs)] + [(cx - hx * k, base - y * th) for y, hx in zip(ys[::-1], hs[::-1])]
    skin = (110, 150, 70)
    hip = half[1] * k; legw = hip * 0.45
    for sx in (-1, 1):   # 다리
        x0 = cx + sx * hip * 0.52
        d.rounded_rectangle((x0 - legw, base - 10, x0 + legw, base + 80), 12, fill=(70, 55, 40), outline=(10, 10, 10), width=2)
    sh = half[4] * k; arm = s[6] * 70
    for sx in (-1, 1):   # 팔
        x0 = cx + sx * (sh + arm * 0.6)
        d.rounded_rectangle((x0 - arm, base - th + 5, x0 + arm, base - 25), int(arm), fill=skin, outline=(10, 10, 10), width=2)
    d.polygon(pts, fill=skin, outline=(10, 10, 10))
    d.ellipse((cx - 62, base - th - 125, cx + 62, base - th + 5), fill=skin, outline=(10, 10, 10), width=2)
    d.text((c * S + 8, 6), name, fill=(255, 220, 120))
    d.text((c * S + 8, Hpx - 40), ' '.join(f'{x:.2f}' for x in s[1:5]), fill=(220, 220, 220))
    d.text((c * S + 8, Hpx - 24), f'depth {s[5]:.2f}  hand {s[6]:.2f}', fill=(220, 220, 220))
im.save('body_cmp2.png'); print('saved')
