# Bone body sketch — chibi skeleton (ThinMale dims, head height = 1). Skull from skull23 (v39 normal teeth).
import numpy as np, sys
from PIL import Image, ImageDraw
exec(open(__file__.replace('body.py','skull_lib.py'), encoding='utf-8').read())

SK_O = A([0.0, 2.12, 0.0]); SK_U = 1.2          # skull SDF origin in body space, SDF units per head height
def skull_parts(p):
    q = (p - SK_O) * SK_U
    return [x / SK_U for x in (cranium(q), teeth(q,-0.345,0.058,-0.385,True,0.19,0.395,0.16), jaw(q),
                               teeth(q,-0.43,0.052,-0.389,False,0.175,0.378,0.15))]

def seg(p,a,b,ra,rb=None):
    rb = ra if rb is None else rb
    pa=p-a; ba=b-a; h=np.clip((pa*ba).sum(-1)/(ba*ba).sum(),0,1)
    return length(pa-ba*h[...,None])-(ra+(rb-ra)*h)

def long_bone(p,a,b,r,knob):
    # cartoon bone: shaft pinched in the middle, two round lobes at each end (side by side, across x)
    d = seg(p,a,(a+b)/2,r*1.0,r*0.85); d = np.minimum(d, seg(p,(a+b)/2,b,r*0.85,r*1.0))
    for e in (a,b):
        d = smin(d, sph(p,e,knob), 0.03)
    return d

V = dict(ribs=4, gap=0.5)
# ── torso ──
def ribcage(p):
    m = mirror(p)
    c = A([0,1.30,0.01]); r = A([0.27,0.28,0.22])
    e = ell(p,c,r)
    shell = np.abs(e) - 0.035
    # ribs: bands that slope down toward the front
    yy = p[...,1] + 0.16*np.clip((p[...,2]+0.05)/0.25,0,1)**1.3
    top, bot = 1.53, 1.04
    pitch = (top-bot)/V['ribs']
    t = (yy-bot)/pitch
    band = (np.abs(t-np.floor(t)-0.5) - V['gap']*0.5)*pitch     # >0 in the slot between ribs
    d = smax(shell, band, 0.012)
    d = np.maximum(d, bot-0.02-yy)                               # bottom open
    d = smax(d, p[...,1]-(c[1]+0.20), 0.03)                     # top open (neck goes through)
    # front: open V under the sternum (costal arch)
    front = (p[...,2] > 0.05)
    arch = (np.abs(p[...,0]) - (1.27-p[...,1])*0.75)
    d = np.where(front & (p[...,1] < 1.27), smax(d, -arch, 0.02), d)
    sternum = rbox(p, A([0,1.39,0.225]), [0.045,0.13,0.03], 0.028)
    d = smin(d, sternum, 0.02)
    return d

def spine(p):
    d = np.full(p.shape[:-1], 9.0)
    ys = [0.98, 1.10, 1.62, 1.75, 1.88]
    for y in ys:
        z = -0.14 + 0.08*np.clip((y-1.55)/0.3,0,1)               # neck leans forward to meet the skull
        d = np.minimum(d, rbox(p, A([0,y,z]), [0.065,0.04,0.055], 0.03))
        
    return d

def clavicles(p):
    m = mirror(p)
    return seg(m, A([0.04,1.55,0.16]), A([0.35,1.51,-0.01]), 0.045, 0.05)

def pelvis(p):
    m = mirror(p)
    # iliac wings: tilted flat ellipsoids flaring out and up
    q = m - A([0.13,0.86,-0.01])
    ca, sa = np.cos(0.5), np.sin(0.5)
    q = np.stack([q[...,0]*ca + q[...,1]*sa, -q[...,0]*sa + q[...,1]*ca, q[...,2]],-1)
    wing = np.abs(ell(q, A([0,0,0]), A([0.105,0.08,0.10]))) - 0.022
    wing = smax(wing, -(m[...,1]-0.80), 0.02)
    sacrum = rbox(p, A([0,0.85,-0.11]), [0.05,0.06,0.03], 0.022)
    pubis = seg(m, A([0.0,0.76,0.09]), A([0.10,0.79,0.05]), 0.03)
    socket = sph(m, A([0.12,0.80,0.0]), 0.05)
    d = smin(smin(wing, sacrum, 0.04), smin(pubis, socket, 0.04), 0.04)
    return d

# ── limbs (rest pose: arms hang straight down) — each bone its own piece so the outline splits them ──
EXT_A = 0.0; EXT_L = 0.0          # extra arm / leg length (head heights)
AX, SH = 0.43, 1.47
LX, ANK = 0.133, 0.12
def lim():
    wr = 0.97 - EXT_A; el = (SH + wr) / 2
    hip = 0.80 + EXT_L; knee = (hip + ANK) / 2
    return wr, el, hip, knee

def humerus(m):
    wr, el, hip, knee = lim()
    return long_bone(m, A([AX,SH,0]), A([AX,el,0]), 0.042, 0.06)
def forearm(m):
    wr, el, hip, knee = lim()
    return long_bone(m, A([AX,el-0.01,0]), A([AX,wr+0.02,0]), 0.036, 0.05)

def hand(m):
    # v1 bony hand (four fingers + thumb), 1.25x, hung from the wrist
    wr = lim()[0]; S = 1.25
    q = (m - A([AX,wr,0])) / S + A([0.31,0.97,0])
    palm = rbox(q, A([0.31,0.905,0.01]), [0.03,0.05,0.07], 0.028)
    d = palm
    for z, ln in [(0.065,0.10),(0.022,0.115),(-0.022,0.11),(-0.062,0.09)]:
        a = A([0.31,0.86,z]); b = A([0.30,0.86-ln,z+0.01])
        d = np.minimum(d, smin(seg(q,a,(a+b)/2,0.019), seg(q,(a+b)/2,b,0.017), 0.01))
    d = np.minimum(d, seg(q, A([0.29,0.90,0.07]), A([0.26,0.82,0.10]), 0.02))
    return d * S

def femur(m):
    wr, el, hip, knee = lim()
    return long_bone(m, A([LX,hip,0]), A([LX,knee,0]), 0.05, 0.068)
def tibia(m):
    wr, el, hip, knee = lim()
    return long_bone(m, A([LX,knee-0.04,0]), A([LX,ANK+0.02,0]), 0.042, 0.058)

def foot(m):
    # v1 bony foot (four toes), 1.2x about the ankle on the floor
    S = 1.2
    q = (m - A([LX,0,0])) / S + A([LX,0,0])
    heel = rbox(q, A([LX,0.055,-0.02]), [0.05,0.05,0.06], 0.04)
    mid  = rbox(q, A([LX,0.045,0.09]), [0.065,0.035,0.07], 0.03)
    d = smin(heel, mid, 0.04)
    for x in (-0.045, -0.015, 0.015, 0.045):
        d = np.minimum(d, seg(q, A([LX+x,0.03,0.15]), A([LX+x*1.15,0.022,0.24]), 0.018))
    return d * S

def scene(p):
    u = p - A([0, EXT_L, 0])           # everything above the hips rides up with longer legs
    m = mirror(p); mu = mirror(u)
    return np.stack(skull_parts(u) + [ribcage(u), spine(u), clavicles(u), pelvis(u),
                     humerus(mu), forearm(mu), hand(mu), femur(m), tibia(m), foot(m)], -1)

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
    col = bone*(0.72+0.28*lit[...,None])*ao[...,None]
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

if __name__ == '__main__':
    W, H = 230, 400
    views = [(0,5,'front'), (35,8,'3/4'), (90,5,'side'), (25,55,'game')]
    ext = float(sys.argv[2]) if len(sys.argv) > 2 else 0.0
    rows = [(0.0, 'same length'), (ext, f'+{ext} arms/legs')] if ext else [(0.0,'')]
    sh = Image.new('RGB', (W*len(views)*len(rows) + 10*(len(rows)-1), H), (20,20,20))
    for r,(e,lab) in enumerate(rows):
        EXT_A = EXT_L = e
        for i,(yw,pt,name) in enumerate(views):
            im = render(yw, pt, W, H, span=1.55, cy=1.38)
            ImageDraw.Draw(im).text((6,6), name + ('  ' + lab if i == 0 else ''), fill=(255,255,255))
            sh.paste(im, (r*(W*len(views)+10) + W*i, 0))
    sh.save(sys.argv[1])
