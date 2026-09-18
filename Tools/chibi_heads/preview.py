import re, os, numpy as np
from PIL import Image, ImageDraw

S = os.path.dirname(os.path.abspath(__file__))
D = os.path.join(S, 'ref')
src = open(os.path.join(S, '..', '..', 'Assets', 'Editor', 'ChibiHeadProfiles.cs'), encoding='utf-8').read()
refs = re.findall(r'new Ref\("(\w+)", ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, ([\d.]+)f,\s*new\[\] \{([^}]*)\},\s*new\[\] \{([^}]*)\}', src)
BODY = 87.5
TORSO = 0.885
GAP = 0.065
m = TORSO / BODY
SC = 110  # px per meter in preview

def arr(s): return np.array([float(x.strip().rstrip('f')) for x in s.split(',')])

tiles = []
for i, (name, W, H, eyeT, eyeDx, eyeD, T, R) in enumerate(refs):
    W, H, eyeT, eyeDx, eyeD = map(float, (W, H, eyeT, eyeDx, eyeD))
    T, R = arr(T), arr(R)
    Wm, Hm, Dm = W * m, H * m, W * m * 0.95
    fwd = 0.26 if name.endswith('Pointy') else 0.20
    size = 200
    img = Image.new('RGB', (size * 3, 260), (30, 32, 36))
    dr = ImageDraw.Draw(img)
    base_y = 250
    # torso (rough male-ish capsule just for scale): height TORSO, width 0.62
    def P(x, y, ox): return (ox + x * SC, base_y - y * SC)
    for ox in (size * 0.5, size * 1.5):
        dr.rounded_rectangle([P(-0.31, TORSO, ox), P(0.31, 0, ox)], radius=int(0.25 * SC), fill=(150, 60, 55), outline=(0, 0, 0), width=4)
    cy = TORSO + GAP + Hm / 2
    ts = np.linspace(0, 1, 200)
    rs = np.interp(ts, T, R)
    # front
    ox = size * 0.5
    left = [P(-r * Wm, cy + (t - 0.5) * Hm, ox) for t, r in zip(ts, rs)]
    right = [P(r * Wm, cy + (t - 0.5) * Hm, ox) for t, r in zip(ts[::-1], rs[::-1])]
    dr.polygon(left + right, fill=(240, 205, 165), outline=(0, 0, 0), width=4)
    # eyes
    d = eyeD * m
    for sgn in (-1, 1):
        ex, ey = sgn * eyeDx * Wm, cy + (eyeT - 0.5) * Hm
        a, b = P(ex - d / 2, ey + d / 2, ox), P(ex + d / 2, ey - d / 2, ox)
        dr.ellipse([a, b], fill=(10, 10, 10))
    # side: z = r*D (+ shear on lower half), y
    ox = size * 1.5
    pts_f, pts_b = [], []
    for t, r in zip(ts, rs):
        y = t - 0.5
        sh = fwd * 2 * y * y if y < 0 else 0
        pts_f.append(P((r + sh) * Dm, cy + y * Hm, ox))
        pts_b.append(P((-r + sh) * Dm, cy + y * Hm, ox))
    dr.polygon(pts_f + pts_b[::-1], fill=(240, 205, 165), outline=(0, 0, 0), width=4)
    # reference image, scaled so its body height matches
    ref = Image.open(os.path.join(D, f'{name}.png')).convert('RGB')
    k = (TORSO * SC) / BODY
    ref = ref.resize((int(ref.width * k), int(ref.height * k)))
    img.paste(ref, (int(size * 2.5 - ref.width / 2), max(0, 250 - ref.height)))
    dr.text((4, 4), name, fill=(255, 255, 255))
    tiles.append(img)

cols = 2
rows = (len(tiles) + cols - 1) // cols
sheet = Image.new('RGB', (tiles[0].width * cols, tiles[0].height * rows), (0, 0, 0))
for i, t in enumerate(tiles):
    sheet.paste(t, ((i % cols) * t.width, (i // cols) * t.height))
out = os.path.join(S, 'preview.png')
sheet.save(out)
print(out, sheet.size)
