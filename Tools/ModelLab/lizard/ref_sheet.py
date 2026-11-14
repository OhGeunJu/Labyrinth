"""뱀 머리 줄을 사용자가 보낸 사진과 나란히 — 왼쪽 사진 · 오른쪽 그 색의 머리(비스듬히 · 옆 · 내려다보기).
먼저 `python lizard.py renders/lizard_v9 snakes`로 머리 판을 뽑는다. 사진은 깃 밖 `../_refs/2026-11-11_lizard/`.
실행: python ref_sheet.py renders/lizard_v9"""
import os, sys
from PIL import Image, ImageDraw
import lizard as lz

REFS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '_refs', '2026-11-11_lizard')
PHOTOS = {'viper': ('8_snake.png', 'images.jpg', '9_snake.png'),
          'viper:slate': ('5bacb3c72930434e3c57a835a9e797ce.jpg',),
          'viper:coal': ('f85ace36a072c80e27b3683f1867ea68.jpg',),
          'viper:amber': ('f85ace36a072c80e27b3683f1867ea68.jpg', '세상에서_가장_아름다운_뱀_(6).jpg'),
          'python': ('7_snake.png',),
          'python:kingrat': ('14e81f2b2cc2635ed82105c2375c9769.webp', 'afc1e7e3ba9594407acfcaf4b2a3d440.webp', 'e2040bef9226afc18b3965ea65e19f86.webp'),
          'python:indigo': ('427aab62c0715193e3c5163cded959ad.webp', '7b64d637763ab6ecb659586fbe9e2189.webp'),
          'python:red': ('dc844640159d16d85551eb2f5169210c.webp', '인디고 스네이크.webp')}
H, LEFT = 360, 900

if __name__ == '__main__':
    stem = sys.argv[1]
    heads = Image.open(stem + '_snakes_heads.png')
    x0 = lz.CLOSE[0][1]; w = heads.width - x0
    out = Image.new('RGB', (LEFT + w, H * len(lz.SNAKES)), (40, 40, 44)); dr = ImageDraw.Draw(out)
    for r, kind in enumerate(lz.SNAKES):
        x = 4
        for name in PHOTOS[kind]:
            path = os.path.join(REFS, name)
            if not os.path.exists(path): continue
            im = Image.open(path).convert('RGB'); k = min((H - 8) / im.height, (LEFT / len(PHOTOS[kind]) - 8) / im.width)
            im = im.resize((int(im.width * k), int(im.height * k)), Image.LANCZOS)
            out.paste(im, (x, r * H + (H - im.height) // 2)); x += im.width + 6
        out.paste(heads.crop((x0, r * H, heads.width, (r + 1) * H)), (LEFT, r * H))
        dr.text((LEFT + 6, r * H + 4), kind, fill=(255, 220, 120))
    out.save(stem + '_snakes_cmp.png'); print('saved', stem + '_snakes_cmp.png')
