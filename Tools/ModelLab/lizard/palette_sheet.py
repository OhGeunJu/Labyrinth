"""기본 도마뱀 팔레트 견본 — 사용자가 보낸 사진(깃 밖 `_refs/2026-11-14_lizard_colors/`) 옆에 팔레트 색을 놓아 맞춰 본다.
값은 `Assets/Editor/ChibiPawnCreator.Lizard.cs`의 `LizLizardTones`에서 읽는다(손으로 옮겨 적지 않는다).
실행: python palette_sheet.py → renders/lizard_palettes.png"""
import os, re
from PIL import Image, ImageDraw, ImageFont

here = os.path.dirname(os.path.abspath(__file__))
root = os.path.abspath(os.path.join(here, '..', '..', '..'))
refs = os.path.join(here, '..', '_refs', '2026-11-14_lizard_colors')
src = open(os.path.join(root, 'Assets', 'Editor', 'ChibiPawnCreator.Lizard.cs'), encoding='utf-8').read()
block = src[src.index('LizLizardTones ='):]; block = block[:block.index('};')]
COLS = ('살', '배', '가시 테', '볏 · 돌기', '눈알', '점', '띠', '등줄기', '눈테')
rows = []
for line in block.splitlines():
    m = re.match(r'\s*\("(\w+)",(.*)\),\s*(//.*)?$', line)
    if not m: continue
    vals = []
    for part in re.findall(r'new\(([^)]*)\)|default', m.group(2)):
        if not part: vals.append(None); continue
        f = [float(x.strip().rstrip('f')) for x in part.split(',')]
        vals.append(tuple(f) if len(f) == 4 else tuple(f) + (1.0,))
    # `default` 칸을 자리에 맞춰 채운다
    toks = re.findall(r'new\([^)]*\)|default', m.group(2)); vals = []
    for t in toks:
        if t == 'default': vals.append(None)
        else:
            f = [float(x.strip().rstrip('f')) for x in t[4:-1].split(',')]; vals.append(tuple(f) if len(f) == 4 else tuple(f) + (1.0,))
    rows.append((m.group(1), vals, (m.group(3) or '').lstrip('/ ').strip()))

REF = {'Crested': '9.png', 'Olive': '12.png', 'Skink': '11.png', 'Abronia': '14.png', 'Tokay': '15.png', 'Leachie': '16.png', 'FatTail': '17.png', 'Leopard': '18.png', 'Phelsuma': '18.png', 'LeafTail': '18.png'}
RH, RW, CW = 120, 160, 70
font = None
for f in ('C:/Windows/Fonts/malgun.ttf', 'C:/Windows/Fonts/gulim.ttc'):
    if os.path.exists(f): font = ImageFont.truetype(f, 14); break
im = Image.new('RGB', (RW + 120 + CW * len(COLS) + 10, 30 + RH * len(rows)), (40, 40, 44)); dr = ImageDraw.Draw(im)
for i, c in enumerate(COLS): dr.text((RW + 120 + CW * i + 4, 6), c, fill=(220, 220, 230), font=font)
for r, (name, vals, note) in enumerate(rows):
    y = 30 + RH * r
    fn = REF.get(name)
    if fn and os.path.exists(os.path.join(refs, fn)):
        ref = Image.open(os.path.join(refs, fn)).convert('RGB'); ref.thumbnail((RW, RH - 6)); im.paste(ref, (0, y + 3))
    dr.text((RW + 6, y + 6), name, fill=(255, 220, 120), font=font)
    dr.text((RW + 6, y + 26), note[:14], fill=(190, 190, 200), font=font)
    for i, v in enumerate(vals):
        x = RW + 120 + CW * i
        if v is None: dr.text((x + 20, y + 50), '-', fill=(120, 120, 130), font=font); continue
        c = tuple(int(255 * max(0, min(1, k))) for k in v[:3])
        dr.rectangle((x + 4, y + 8, x + CW - 6, y + RH - 10), fill=c, outline=(20, 20, 20))
        if v[3] < 1: dr.text((x + 8, y + RH - 30), 'a%.1f' % v[3], fill=(255, 255, 255), font=font)
out = os.path.join(here, 'renders', 'lizard_palettes.png'); os.makedirs(os.path.dirname(out), exist_ok=True)
im.save(out); print('saved', out, len(rows))

# ── 숲살모사 · 매끈한 뱀(`LizViperTones` · `LizSnakeTones`) — 칸: 살 · 끝/배 · 배/눈테 · 눈/무늬 ──
def dict_rows(name):
    b = src[src.index(name + ' = new()'):]; b = b[:b.index('};')]
    out = []
    for line in b.splitlines():
        m = re.match(r'\s*\["(\w+)"\]\s*=\s*\((.*)\),\s*(//.*)?$', line)
        if not m: continue
        cols = [tuple(float(x.strip().rstrip('f')) for x in t.split(',')) for t in re.findall(r'new Color\(([^)]*)\)', m.group(2))]
        out.append((m.group(1), cols, (m.group(3) or '').lstrip('/ ').strip()))
    return out
groups = [('LizViperTones', ('살', '잎 끝', '배', '눈')), ('LizSnakeTones', ('살', '배', '눈테', '무늬'))]
blocks = [(g, h, dict_rows(g)) for g, h in groups]
H2 = sum(30 + 60 * len(r) for _, _, r in blocks)
im2 = Image.new('RGB', (560, H2), (40, 40, 44)); dr2 = ImageDraw.Draw(im2); y = 0
for g, heads, rws in blocks:
    dr2.text((6, y + 6), '숲살모사' if 'Viper' in g else '매끈한 뱀', fill=(255, 220, 120), font=font)
    for i, c in enumerate(heads): dr2.text((150 + 90 * i, y + 6), c, fill=(220, 220, 230), font=font)
    y += 30
    for name, cols, note in rws:
        dr2.text((6, y + 8), name, fill=(240, 240, 240), font=font); dr2.text((6, y + 28), note[:12], fill=(170, 170, 180), font=font)
        for i, v in enumerate(cols[:4]):
            c = tuple(int(255 * max(0, min(1, k))) for k in v[:3]); dr2.rectangle((150 + 90 * i, y + 4, 150 + 90 * i + 80, y + 54), fill=c, outline=(20, 20, 20))
        y += 60
out2 = os.path.join(here, 'renders', 'lizard_palettes_snakes.png'); im2.save(out2); print('saved', out2)
