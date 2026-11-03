from PIL import Image, ImageDraw
import hair_v2 as Hv
S = 220
im = Image.new('RGB', (S * 6, S * 4), (40, 40, 44)); d = ImageDraw.Draw(im)
for i, (n, _) in enumerate(Hv.HAIRS):
    im.paste(Image.open(f'v2_{i}.png'), (i * S, 0)); d.text((i * S + 6, 4), n, fill=(255, 220, 120))
im.save('v2_hair.png'); print('saved')
