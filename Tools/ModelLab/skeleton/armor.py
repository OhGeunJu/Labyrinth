# Armor shells over the v5 bone body — which pieces hide which bones.
import sys, numpy as np
from PIL import Image, ImageDraw
src = open(__file__.replace('armor.py','body.py'), encoding='utf-8').read().split("if __name__ == '__main__':")[0]
_me = __file__; __file__ = _me.replace('armor.py','body.py'); exec(src); __file__ = _me
FAR = 9.0
WEAR = set()
def chest(p):
    # breastplate: a closed shell a bit bigger than the ribcage, open at the neck and the waist
    e = ell(p, A([0,1.30,0.02]), A([0.31,0.31,0.26]))
    d = np.abs(e) - 0.025
    d = smax(d, p[...,1]-1.58, 0.03); d = smax(d, 1.03-p[...,1], 0.02)
    return d
def greaves(m):
    wr, el, hip, knee = lim()
    a = seg(m, A([LX,hip-0.02,0]), A([LX,knee+0.03,0.0]), 0.085, 0.075)
    b = seg(m, A([LX,knee-0.03,0.0]), A([LX,ANK+0.06,0.0]), 0.075, 0.068)
    return np.minimum(a, b)
def gauntlet(m):
    wr = lim()[0]
    cuff = seg(m, A([AX,wr+0.07,0]), A([AX,wr-0.02,0]), 0.06, 0.065)
    mitt = rbox(m, A([AX,wr-0.10,0.01]), [0.055,0.09,0.085], 0.05)
    return np.minimum(cuff, mitt)
def boot(m):
    heel = rbox(m, A([LX,0.10,-0.01]), [0.085,0.10,0.09], 0.06)
    toe  = rbox(m, A([LX,0.06,0.14]), [0.09,0.06,0.13], 0.05)
    return smin(heel, toe, 0.05)
BONE_N = 14
def scene(p):
    u = p - A([0, EXT_L, 0]); m = mirror(p); mu = mirror(u)
    parts = skull_parts(u) + [ribcage(u), spine(u), clavicles(u), pelvis(u),
             humerus(mu), forearm(mu), hand(mu), femur(m), tibia(m), foot(m)]
    full = lambda f: f if True else None
    parts += [chest(u) if 'chest' in WEAR else np.full(p.shape[:-1],FAR),
              greaves(m) if 'legs' in WEAR else np.full(p.shape[:-1],FAR),
              gauntlet(mu) if 'hands' in WEAR else np.full(p.shape[:-1],FAR),
              boot(m) if 'feet' in WEAR else np.full(p.shape[:-1],FAR)]
    return np.stack(parts, -1)

def render(yaw, pitch, W, H, span, cy):
    yaw, pitch = np.radians(yaw), np.radians(pitch)
    fwd = A([-np.sin(yaw)*np.cos(pitch), -np.sin(pitch), -np.cos(yaw)*np.cos(pitch)])
    right = A([np.cos(yaw),0,-np.sin(yaw)]); up = np.cross(right, fwd)
    asp = W/H
    xs = np.linspace(-span*asp, span*asp, W); ys = np.linspace(span, -span, H)
    X, Y = np.meshgrid(xs, ys)
    o = A([0,cy,0])[None,None] + X[...,None]*right + Y[...,None]*up - fwd*6
    t = np.zeros(X.shape); hit = np.zeros(X.shape,bool); pid = np.full(X.shape,-1)
    for _ in range(170):
        act = ~hit & (t < 12)
        if not act.any(): break
        p = o[act] + fwd*t[act][...,None]
        ds = scene(p); d = ds.min(-1)
        nh = d < 1e-3
        idx = np.argwhere(act)
        h_idx = idx[nh]
        hit[h_idx[:,0],h_idx[:,1]] = True; pid[h_idx[:,0],h_idx[:,1]] = ds.argmin(-1)[nh]
        tt = t[act]; tt = np.where(nh, tt, tt + d*0.9); t[act] = tt
    P = o + fwd*t[...,None]
    hp = P[hit]
    f = lambda q: scene(q).min(-1)
    e = 1e-3
    n = np.stack([f(hp+A([e,0,0]))-f(hp-A([e,0,0])), f(hp+A([0,e,0]))-f(hp-A([0,e,0])), f(hp+A([0,0,e]))-f(hp-A([0,0,e]))],-1)
    n /= np.maximum(length(n)[...,None],1e-9)
    ld = A([0.45,0.75,0.5]); ld = ld/L(ld)
    lit = np.clip(((n*ld).sum(-1)-0.15)/0.08,0,1)
    ao = np.ones(len(hp))
    for i in range(1,9):
        h = 0.03*i; ao -= np.maximum(h - f(hp+n*h),0)*(0.8**i)*5.5
    ao = np.clip(ao,0.08,1)
    bone = A([226,217,194])/255
    hid = pid[hit]; base = np.where((hid>=14)[...,None], A([0.55,0.58,0.62]), bone)
    col = base*(0.72+0.28*lit[...,None])*ao[...,None]
    img = np.ones(X.shape+(3,))*A([0.30,0.30,0.32])
    img[hit] = col
    tt = np.where(hit,t,99); ii = np.where(hit,pid,-1)
    edge = np.zeros(X.shape,bool)
    for dy,dx in [(1,0),(0,1),(1,1),(-1,1)]:
        t2 = np.roll(np.roll(tt,dy,0),dx,1); i2 = np.roll(np.roll(ii,dy,0),dx,1)
        edge |= (i2!=ii) | (np.abs(t2-tt) > 0.06)
    edge = edge | np.roll(edge,1,0) | np.roll(edge,1,1)
    img = np.where(edge[...,None], 0.05, img)
    return Image.fromarray((np.clip(img,0,1)*255).astype(np.uint8))


W, H = 230, 400
sets = [(set(), 'none'), ({'chest'}, 'chest'), ({'chest','legs','hands','feet'}, 'all but helmet')]
views = [(0,5), (25,55)]
sh = Image.new('RGB', (W*len(views)*len(sets) + 10*(len(sets)-1), H), (20,20,20))
for k,(w,lab) in enumerate(sets):
    WEAR = w
    for i,(yw,pt) in enumerate(views):
        im = render(yw, pt, W, H, span=1.55, cy=1.38)
        if i == 0: ImageDraw.Draw(im).text((6,6), lab, fill=(255,255,255))
        sh.paste(im, (k*(W*len(views)+10) + W*i, 0))
sh.save(sys.argv[1])
