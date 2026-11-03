# Skull SDF sketch — raymarched toon render (front / side / 3-4 / game view).
import numpy as np, sys
from PIL import Image, ImageDraw
L = np.linalg.norm
def length(p): return np.sqrt((p*p).sum(-1))
def smin(a,b,k):
    h=np.clip(0.5+0.5*(b-a)/k,0,1); return b*(1-h)+a*h-k*h*(1-h)
def smax(a,b,k): return -smin(-a,-b,k)
def ell(p,c,r):
    q=(p-c)/r; k0=length(q); k1=length(q/r); return k0*(k0-1)/np.maximum(k1,1e-6)
def rbox(p,c,h,r):
    q=np.abs(p-c)-(np.array(h)-r); return length(np.maximum(q,0))+np.minimum(q.max(-1),0)-r
def sph(p,c,r): return length(p-c)-r
A=np.array
def mirror(p): q=p.copy(); q[...,0]=np.abs(q[...,0]); return q

P = dict(sock_r=A([0.148,0.155,0.13]), sock_c=A([0.20,0.06,0.46]))
N=dict(nt=-0.11, nb=-0.25, nw=0.062, notch=-0.205)
def nose2d(p):
    x=np.abs(p[...,0]); y=p[...,1]; h=N['nt']-N['nb']
    k=np.clip((N['nt']-y)/h,0,1); rx=N['nw']*np.sqrt(np.clip(k*1.6,0,1))*(0.75+0.25*k)
    d=np.maximum(x-rx, np.maximum(y-N['nt'], N['nb']-y))
    inV=(x < (N['notch']-y)*0.55) & (y < N['notch'])
    return np.where(inV, np.maximum(d,0.03), d)
def capsule(p,a,b,r):
    pa=p-a; ba=b-a; h=np.clip((pa*ba).sum(-1)/(ba*ba).sum(),0,1)
    return length(pa-ba*h[...,None])-r
def arch(m):
    # zygomatic arch: thin bar from under the socket's outer rim back to the ear, bowing out slightly
    a=A([0.24,-0.075,0.31]); mid=A([0.33,-0.075,0.13]); b=A([0.31,-0.02,-0.17])
    d=np.minimum(capsule(m,a,mid,0.034),capsule(m,mid,b,0.03))
    return d
def socket(m):
    # rounded-square opening at the front, tapering as it goes back
    c=P['sock_c']; z=m[...,2]; k=np.clip((c[2]-z)/0.45,0,1); sc=1.0-0.45*k
    q=(m[...,:2]-c[:2])/(P['sock_r'][:2]*sc[...,None])
    q4=(np.abs(q)**3.2).sum(-1)**(1/3.2)        # superellipse: rounded square
    d2=(q4-1)*P['sock_r'][:2].min()*sc
    return np.maximum(d2, np.maximum(z-(c[2]+0.2), (c[2]-0.45)-z))
def cranium(p):
    m=mirror(p)
    d=ell(p,A([0,0.16,-0.04]),A([0.50,0.47,0.52]))
    d=smax(d,-(p[...,1]+0.16),0.05)                          # skull base: nothing below but the face
    face=rbox(p,A([0,-0.17,0.24]),[0.21,0.16,0.16],0.10)   # narrow maxilla, short in depth
    face=smax(face,-rbox(p,A([0,-0.24,0.04]),[0.13,0.12,0.13],0.05),0.03)   # hollow behind the teeth (U)
    d=smin(d,face,0.14)
    d=smax(d,-sph(m,A([0.66,-0.10,0.02]),0.26),0.10)          # temples pinch in
    d=smin(d,arch(m),0.07)
    d=smin(d,ell(m,A([0.17,0.19,0.38]),A([0.12,0.04,0.06])),0.06)    # brow ridge
    d=smax(d,-socket(m),0.03)                                  # eye sockets: deep tunnels
    # nasal cavity: inverted heart = two small ellipsoids leaning in
    d=smax(d,-np.maximum(nose2d(p)-0.004,-0.05-p[...,2]),0.012)   # nasal hole, carved straight in
    d=smax(d,-(p[...,1]+0.30),0.02)                           # flat underside of maxilla
    return d
def tooth_row(hw, zf, zb, n=5):
    # tooth centres along a parabola, spaced by arc length; returns (x,z,tx,tz,k) per tooth, k 0=incisor..1=molar
    t=np.linspace(0,1,400); x=hw*t; z=zf-(zf-zb)*t*t
    seg=np.sqrt(np.diff(x)**2+np.diff(z)**2); L=np.concatenate([[0],np.cumsum(seg)])
    out=[]
    widths=[0.026,0.022,0.024,0.028,0.034]                # half-gap-free widths: incisor, lateral, canine, premolar, molar
    pos=0.0
    for i in range(n):
        w=widths[i]; c=pos+w; pos=c+w+0.004
        j=np.searchsorted(L,c); j=min(j,len(t)-2)
        tx,tz=x[j+1]-x[j],z[j+1]-z[j]; nn=np.hypot(tx,tz); out.append((x[j],z[j],tx/nn,tz/nn,w,i/(n-1)))
    return out
TEETH='normal'
def tooth_spec(i, up):
    # (length x, pointy 0..1, past the bite line?)
    if TEETH=='vampire' and up and i==2: return (1.7,1.0,True,0.03,0.0)
    if TEETH=='orc' and (not up) and i==2: return None
    if TEETH=='monster': return (1.5 if i>=1 else 1.25, 1.0, True,0.012 if up else 0.0,0.0)
    return (1.0,0.0,False,0.0,0.0)
def tusk(p):
    # a horn from the outer corner of the lower jaw: out, forward, then up; thick base, sharp tip
    m=mirror(p)
    x,z=tooth_row(0.175,0.378,0.15)[2][:2]
    P0=A([x,-0.41,z]); P1=P0+A([0.075,0.05,0.08]); P2=P0+A([0.14,0.27,0.06])
    n=12; d=np.full(p.shape[:-1],1e9)
    pts=[(1-t)**2*P0+2*(1-t)*t*P1+t*t*P2 for t in np.linspace(0,1,n+1)]
    rs=[0.05*(1-t)**0.8+0.004 for t in np.linspace(0,1,n+1)]
    for a,b,ra,rb in zip(pts[:-1],pts[1:],rs[:-1],rs[1:]):
        pa=m-a; ba=b-a; h=np.clip((pa*ba).sum(-1)/(ba*ba).sum(),0,1)
        d=np.minimum(d,length(pa-ba*h[...,None])-(ra+(rb-ra)*h))
    return d
def teeth(p,yc,h,bite,up,hw,zf,zb):
    d=np.full(p.shape[:-1],1e9); free=np.full(p.shape[:-1],1e9)
    rows=tooth_row(hw,zf,zb)
    for sgn in (1,-1):
        for i,(x,z,tx,tz,w,k) in enumerate(rows):
            spec=tooth_spec(i,up)
            if spec is None: continue
            ln,pt,past,fw,out=spec
            cx=(x+out)*sgn; ux=tx*sgn
            dx=p[...,0]-cx; dz=p[...,2]-(z+fw)
            u=dx*ux+dz*tz; v=-dx*tz+dz*ux
            hh=h*(1.0-0.15*k)*(1.0+0.06*np.sin(7.3*x+3*sgn))*ln
            root=0.5*h                                          # how far the root sits inside the bone
            yy=p[...,1]-yc
            cy=(-(hh-h)) if up else (hh-h)                     # root end stays put (same as a normal tooth), only the tip grows
            yl=yy-cy*(ln>1)
            # taper toward the tip: tip is down for upper teeth, up for lower
            tipward=np.clip((-yl if up else yl)/hh,0,1)
            if TEETH=='orc' and past: 
                bend=0.05*np.clip((yl+hh)/(2*hh),0,1)**2
                u=u-bend*1.0; v=v-bend*0.4
            sc=1.0-pt*0.85*tipward**1.2
            q=np.stack([u/sc, yl, v/sc],-1)
            dd=ell(q,A([0,0,0]),A([w*(1.15 if ln>1.6 else 1.0),hh,0.020+0.014*k]))*np.minimum(sc,1)*0.8
            if past: free=np.minimum(free,dd)
            else:    d=np.minimum(d,dd)
    d=smax(d,(bite-p[...,1]) if up else (p[...,1]-bite),0.012)
    if not up: free=np.maximum(free,-0.43-p[...,1])
    return np.minimum(d,free)
# dental arch: parabola in xz — narrow at the incisors, as wide as the cheekbone at the molars
ZF, ZB, HW = 0.40, 0.04, 0.27
def arch_pts(scale=1.0, zf=ZF, n=24):
    z=np.linspace(zf, ZB, n); x=np.sqrt(np.clip((zf-z)/(zf-ZB),0,1))*HW*scale
    half=np.stack([x,z],-1); return np.concatenate([half[::-1]*A([-1,1]), half[1:]])
def arch_dist(p, pts):
    xz=np.stack([p[...,0],p[...,2]],-1); d=np.full(p.shape[:-1],1e9)
    for a,b in zip(pts[:-1],pts[1:]):
        pa=xz-a; ba=b-a; h=np.clip((pa*ba).sum(-1)/(ba*ba).sum(),0,1)
        d=np.minimum(d,length(pa-ba*h[...,None]))
    return d
UP=arch_pts(); LO=arch_pts(0.93, ZF-0.03)
def tube(p, pts, r, ytop, ybot):
    dxz=arch_dist(p,pts)-r; yc=(ytop+ybot)/2; hy=np.abs(ytop-ybot)/2   # ytop/ybot may vary per point
    q=np.stack([np.maximum(dxz,0), np.maximum(np.abs(p[...,1]-yc)-hy,0)],-1)
    return length(q)+np.minimum(np.maximum(dxz,np.abs(p[...,1]-yc)-hy),0)
def jaw(p):
    m=mirror(p)
    sx=np.clip(arch_dist(p,LO)*0+ (ZF-p[...,2])/(ZF-ZB),0,1)          # 0 at the chin, 1 at the back
    bot=-0.61+0.19*sx**1.15                                            # V from the front, a soft curve
    body=tube(p,LO,0.004,-0.425,bot+0.03)-0.031                      # rounded bar following the dental arch
    body=smin(body,ell(p,A([0,-0.57,0.37]),A([0.06,0.035,0.035])),0.04)   # chin bump
    # ramus: a rounded plate rising from the jaw angle into the cheek void, leaning back
    a=A([-0.43,0.05]); b=A([-0.20,-0.05])                             # (y,z) of angle and condyle
    yz=np.stack([m[...,1],m[...,2]],-1); pa=yz-a; ba=b-a
    hh=np.clip((pa*ba).sum(-1)/(ba*ba).sum(),0,1)
    d2=length(pa-ba*hh[...,None])-(0.042+0.004*hh)
    dx=np.abs(m[...,0]-0.25)-0.012
    q=np.stack([np.maximum(d2,0),np.maximum(dx,0)],-1)
    ramus=length(q)+np.minimum(np.maximum(d2,dx),0)-0.012
    return smin(body,ramus,0.07)
JAW=True
def body(p):
    return rbox(p,A([0,-1.27,-0.02]),[0.33,0.55,0.22],0.14)
def scene(p):
    far=np.full(p.shape[:-1],9.0)
    parts=[cranium(p), teeth(p,-0.345,0.058,-0.385,True,0.19,0.395,0.16) if JAW else far, jaw(p) if JAW else far,
           np.minimum(teeth(p,-0.43,0.052,-0.389,False,0.175,0.378,0.15), tusk(p) if TEETH=='orc' else far) if JAW else far]
    return np.stack(parts,-1)

def cavity(p):
    m=mirror(p)
    s=ell(m,P['sock_c'],P['sock_r']*1.0)
    return (s<0.02)|((nose2d(p)<0.004)&(p[...,2]>0.15))

def render(yaw,pitch,W=300,Hh=300,span=1.35,cy=-0.35):
    yaw,pitch=np.radians(yaw),np.radians(pitch)
    fwd=A([-np.sin(yaw)*np.cos(pitch), -np.sin(pitch), -np.cos(yaw)*np.cos(pitch)])  # looks toward origin from +z
    right=A([np.cos(yaw),0,-np.sin(yaw)]); up=np.cross(right,fwd)
    xs=np.linspace(-span,span,W); ys=np.linspace(span,-span,Hh)
    X,Y=np.meshgrid(xs,ys)
    ctr=A([0,cy,0])
    o=ctr[None,None]+X[...,None]*right+Y[...,None]*up-fwd*4
    t=np.zeros(X.shape); hit=np.zeros(X.shape,bool); pid=np.full(X.shape,-1)
    for _ in range(140):
        p=o+fwd*t[...,None]; ds=scene(p); d=ds.min(-1)
        newhit=(d<1e-3)&~hit; pid[newhit]=ds.argmin(-1)[newhit]; hit|=d<1e-3
        t=np.where(hit,t,t+d*0.9); 
        if (hit|(t>9)).all(): break
    p=o+fwd*t[...,None]; e=1e-3
    def f(q): return scene(q).min(-1)
    n=np.stack([f(p+A([e,0,0]))-f(p-A([e,0,0])),f(p+A([0,e,0]))-f(p-A([0,e,0])),f(p+A([0,0,e]))-f(p-A([0,0,e]))],-1)
    n/=np.maximum(length(n)[...,None],1e-9)
    ldir=A([0.45,0.75,0.5]); ldir=ldir/L(ldir)
    ndl=(n*ldir).sum(-1); lit=np.clip((ndl-0.15)/0.08,0,1)
    bone=A([226,217,194])/255; img=np.ones(X.shape+(3,))*A([0.30,0.30,0.32])
    col=bone*(0.72+0.28*lit[...,None])
    cav=cavity(p)
    ao=np.ones(X.shape)
    for i in range(1,9):
        h=0.04*i; ao-=np.maximum(h-f(p+n*h),0)*(0.8**i)*4.5
    ao=np.clip(ao,0.08,1)
    col=col*ao[...,None]
    img=np.where(hit[...,None],col,img)
    # outline: id/depth discontinuities
    tt=np.where(hit,t,99); ii=np.where(hit,pid,-1)
    edge=np.zeros(X.shape,bool)
    for dy,dx in [(1,0),(0,1),(1,1),(-1,1)]:
        t2=np.roll(np.roll(tt,dy,0),dx,1); i2=np.roll(np.roll(ii,dy,0),dx,1)
        edge|=(i2!=ii)|(np.abs(t2-tt)>0.05)
    edge=edge|np.roll(edge,1,0)|np.roll(edge,1,1)
    img=np.where(edge[...,None],0.05,img)
    return Image.fromarray((np.clip(img,0,1)*255).astype(np.uint8))

ims=[]
for t in ('normal','vampire','orc','monster'):
    TEETH=t
    im=render(20,5,W=320,Hh=320,span=0.62,cy=-0.25); ImageDraw.Draw(im).text((6,6),t,fill=(255,255,255)); ims.append(im)
sh=Image.new("RGB",(320*4,320))
for i,im in enumerate(ims): sh.paste(im,(320*i,0))
sh.save(sys.argv[1])
