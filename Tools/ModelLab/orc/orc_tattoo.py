"""Orc tattoos v10 — shape and colour are separate; layers stack (few or many).
Strokes are drawn in a wrapped face space (u = arc around the head, v = height) so they follow the cheeks and temples."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import sys
import numpy as np
from PIL import Image, ImageDraw
import orc_parts as O
from orc_preview import render, EY, MY, NY
from brute_preview import smax

R0 = 0.30
EU = float(np.arctan2(O.EX, O.front(O.EYE_T)) * R0)   # eye centre u

def uv(p):
    return np.arctan2(p[..., 0], p[..., 2]) * R0, p[..., 1]

def stroke(u, v, pts, w0, w1=None, taper=(0.15, 0.85), n=20):
    """Tapered brush stroke along a quadratic/cubic bezier — thick in the body, thin at both ends."""
    P = [np.array(q, np.float32) for q in pts]
    def at(t):
        if len(P) == 3: return (1 - t) ** 2 * P[0] + 2 * (1 - t) * t * P[1] + t * t * P[2]
        return (1 - t) ** 3 * P[0] + 3 * (1 - t) ** 2 * t * P[1] + 3 * (1 - t) * t * t * P[2] + t ** 3 * P[3]
    w1 = w0 if w1 is None else w1
    def width(t):
        a, b = taper
        k = min(t / a, 1.0) if a > 0 else 1.0
        k = min(k, (1 - t) / (1 - b)) if b < 1 else k
        return (w0 + (w1 - w0) * t) * (k ** 0.6)
    d = np.full(u.shape, 9.0, np.float32)
    ts = np.linspace(0, 1, n + 1)
    for i in range(n):
        a, b = at(ts[i]), at(ts[i + 1]); wa, wb = width(ts[i]), width(ts[i + 1])
        ab = b - a; L = float((ab * ab).sum()) + 1e-9
        k = np.clip(((u - a[0]) * ab[0] + (v - a[1]) * ab[1]) / L, 0, 1)
        dd = np.sqrt((u - a[0] - ab[0] * k) ** 2 + (v - a[1] - ab[1] * k) ** 2) - (wa + (wb - wa) * k)
        d = np.minimum(d, dd)
    return d

def both(f):   # mirror a one-side design
    return lambda u, v: np.minimum(f(u, v), f(-u, v))

# ── shapes (u > 0 = the pawn's left, viewer's right) ──
def chin(u, v):   # three lines from the lower lip down under the chin
    d = np.full(u.shape, 9.0, np.float32)
    for x, w in ((-0.05, 0.010), (0.0, 0.013), (0.05, 0.010)):
        d = np.minimum(d, stroke(u, v, [(x, MY - 0.002), (x * 1.05, -0.30), (x * 1.15, -0.37)], w, w * 0.7, (0.05, 0.75)))
    return d
def claw(u, v):   # three claw rakes across one eye
    d = np.full(u.shape, 9.0, np.float32)
    for k in (-1, 0, 1):
        o = k * 0.035
        d = np.minimum(d, stroke(u, v, [(-EU - 0.09 + o, EY + 0.15), (-EU - 0.01 + o, EY + 0.02), (-EU + 0.05 + o, EY - 0.15)], 0.011))
    return d
def brow(u, v):   # hooks rising off the brow, joined by a centre spike
    side = lambda u, v: np.minimum(
        stroke(u, v, [(0.025, EY + 0.075), (0.12, EY + 0.10), (0.17, EY + 0.20), (0.24, EY + 0.15)], 0.014, 0.008, (0.1, 0.8)),
        stroke(u, v, [(0.07, EY + 0.13), (0.10, EY + 0.17), (0.09, EY + 0.24)], 0.009, 0.004, (0.1, 0.8)))
    centre = stroke(u, v, [(0.0, EY + 0.06), (0.0, EY + 0.16), (0.0, EY + 0.27)], 0.016, 0.004, (0.05, 0.7))
    return np.minimum(both(side)(u, v), centre)
def cheek(u, v):   # swoops along the cheekbone from beside the nose toward the ear, a spur hanging off each
    side = lambda u, v: np.minimum(np.minimum(
        stroke(u, v, [(0.06, NY + 0.055 + 0.005), (0.17, NY + 0.055 - 0.07), (0.33, NY + 0.055 + 0.04)], 0.016, 0.007, (0.08, 0.8)),
        stroke(u, v, [(0.09, NY + 0.055 - 0.045), (0.18, NY + 0.055 - 0.10), (0.30, NY + 0.055 - 0.04)], 0.010, 0.005, (0.1, 0.8))),
        stroke(u, v, [(0.20, NY + 0.055 - 0.05), (0.22, NY + 0.055 - 0.10), (0.21, NY + 0.055 - 0.15)], 0.009, 0.003, (0.05, 0.8)))
    return both(side)(u, v)
def fangs(u, v):   # tear-drop fangs under both eyes
    side = lambda u, v: np.minimum(
        stroke(u, v, [(EU - 0.015, EY - 0.05), (EU - 0.02, EY - 0.10), (EU - 0.01, EY - 0.17)], 0.017, 0.0, (0.02, 1.0)),
        stroke(u, v, [(EU + 0.035, EY - 0.05), (EU + 0.04, EY - 0.09), (EU + 0.05, EY - 0.13)], 0.012, 0.0, (0.02, 1.0)))
    return both(side)(u, v)
def temple(u, v):   # from the outer brow back over the temple, forked
    side = lambda u, v: np.minimum(
        stroke(u, v, [(0.19, EY + 0.06), (0.30, EY + 0.10), (0.42, EY + 0.06), (0.52, EY + 0.12)], 0.015, 0.006, (0.08, 0.85)),
        stroke(u, v, [(0.30, EY + 0.10), (0.36, EY + 0.18), (0.44, EY + 0.22)], 0.009, 0.004, (0.05, 0.8)))
    return both(side)(u, v)
SHAPES = [('Chin', chin), ('Claw', claw), ('Brow', brow), ('Cheek', cheek), ('Fangs', fangs), ('Temple', temple)]

def layers(*fs):
    def mask(p):
        u, v = uv(p); d = np.full(u.shape, 9.0, np.float32)
        for f in fs: d = np.minimum(d, f(u, v))
        return d
    return mask

def tattoo(mask):
    return lambda p: smax(np.maximum(O.orc_head(p) - 0.0025, mask(p)), -p[..., 2] - 0.15, 0.002)

INK = {'black': (0.09, 0.09, 0.11), 'red': (0.60, 0.12, 0.10), 'bone': (0.86, 0.84, 0.76), 'blue': (0.16, 0.28, 0.58)}

def pieces(mask, ink):
    P = O.pieces(O.h_bald, O.b_none)
    return [(tattoo(mask), ink)] + P

if __name__ == '__main__':
    out = sys.argv[1]; S = 250
    singles = [render(pieces(layers(f), INK['black']), 20, S, S, -0.06, 0.85) for _, f in SHAPES]
    combos = [('few: Chin', layers(chin), 'black'), ('Chin+Fangs', layers(chin, fangs), 'red'),
              ('Brow+Cheek', layers(brow, cheek), 'blue'), ('Claw+Chin+Temple', layers(claw, chin, temple), 'black'),
              ('many: Brow+Cheek+Chin+Temple', layers(brow, cheek, chin, temple), 'bone'),
              ('all', layers(*[f for _, f in SHAPES]), 'black')]
    combo_ims = [render(pieces(m, INK[c]), 0, S, S, -0.06, 0.85) for _, m, c in combos]
    im = Image.new('RGB', (S * 6, S * 2), (40, 40, 44))
    for i, t in enumerate(singles): im.paste(t, (i * S, 0))
    for i, t in enumerate(combo_ims): im.paste(t, (i * S, S))
    d = ImageDraw.Draw(im)
    for i, (n, _) in enumerate(SHAPES): d.text((i * S + 6, 4), n, fill=(230, 230, 230))
    for i, (n, _, c) in enumerate(combos): d.text((i * S + 6, S + 4), f'{n} ({c})', fill=(230, 230, 230))
    im.save(out); print('saved', out)
