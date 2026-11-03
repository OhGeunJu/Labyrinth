# Outline width options on the v5 skeleton. Lines are drawn outside each part's silhouette (like the hull), seams 1.5cm.
import sys, numpy as np
from PIL import Image, ImageDraw
_me=__file__; __file__=_me.replace('lines.py','body.py')
exec(open(__file__,encoding='utf-8').read().split("if __name__ == '__main__':")[0])
__file__=_me
PX_PER_M = None
def raw(yaw,pitch,W,H,span,cy):
    global PX_PER_M
    PX_PER_M = (H/2)/span/0.69
    yaw,pitch=np.radians(yaw),np.radians(pitch)
    fwd=A([-np.sin(yaw)*np.cos(pitch),-np.sin(pitch),-np.cos(yaw)*np.cos(pitch)])
    right=A([np.cos(yaw),0,-np.sin(yaw)]); up=np.cross(right,fwd)
    xs=np.linspace(-span*W/H,span*W/H,W); ys=np.linspace(span,-span,H); X,Y=np.meshgrid(xs,ys)
    o=A([0,cy,0])[None,None]+X[...,None]*right+Y[...,None]*up-fwd*6
    t=np.zeros(X.shape); hit=np.zeros(X.shape,bool); pid=np.full(X.shape,-1)
    for _ in range(170):
        act=~hit&(t<12)
        if not act.any(): break
        p=o[act]+fwd*t[act][...,None]; ds=scene(p); d=ds.min(-1); nh=d<1e-3
        idx=np.argwhere(act)[nh]; hit[idx[:,0],idx[:,1]]=True; pid[idx[:,0],idx[:,1]]=ds.argmin(-1)[nh]
        tt=t[act]; t[act]=np.where(nh,tt,tt+d*0.9)
    P=o+fwd*t[...,None]; hp=P[hit]; f=lambda q: scene(q).min(-1); e=1e-3
    n=np.stack([f(hp+A([e,0,0]))-f(hp-A([e,0,0])),f(hp+A([0,e,0]))-f(hp-A([0,e,0])),f(hp+A([0,0,e]))-f(hp-A([0,0,e]))],-1)
    n/=np.maximum(length(n)[...,None],1e-9)
    ld=A([0.45,0.75,0.5]); ld/=L(ld); lit=np.clip(((n*ld).sum(-1)-0.15)/0.08,0,1)
    ao=np.ones(len(hp))
    for i in range(1,9):
        h=0.03*i; ao-=np.maximum(h-f(hp+n*h),0)*(0.8**i)*5.5
    ao=np.clip(ao,0.08,1)
    img=np.ones(X.shape+(3,))*A([0.30,0.30,0.32]); img[hit]=(A([226,217,194])/255)*(0.72+0.28*lit[...,None])*ao[...,None]
    return img,hit,pid,np.where(hit,t,99)
def dilate(mask,r):
    out=mask.copy(); r=int(round(r))
    for dy in range(-r,r+1):
        for dx in range(-r,r+1):
            if dx*dx+dy*dy<=r*r and (dx or dy): out|=np.roll(np.roll(mask,dy,0),dx,1)
    return out
SKULL={0,1,2,3}; TORSO={4,5,6,7}; LIMB={8,9,11,12}; DIGIT={10,13}
def compose(img,hit,pid,tt,width_of,seam=0.015):
    img=img.copy(); ring=np.zeros(hit.shape,bool)
    for k in range(14):
        m=pid==k
        if m.any(): ring|=dilate(m,width_of(k)*PX_PER_M)&~hit
    # seams between parts / depth steps
    edge=np.zeros(hit.shape,bool)
    for dy,dx in [(1,0),(0,1),(1,1),(-1,1)]:
        i2=np.roll(np.roll(pid,dy,0),dx,1); t2=np.roll(np.roll(tt,dy,0),dx,1)
        edge|=hit&((i2!=pid)|(np.abs(t2-tt)>0.06))
    edge=dilate(edge,max(seam*PX_PER_M/2,0.5))&hit
    img[ring|edge]=0.05
    return img
opts=[('A now: skull 6 / bones 1.5', lambda k: 0.06 if k in SKULL else 0.015),
      ('B all 6cm', lambda k: 0.06),
      ('C graded 6/4.5/3.5/2.5', lambda k: 0.06 if k in SKULL else 0.045 if k in TORSO else 0.035 if k in LIMB else 0.025)]
W,H=240,400; views=[(0,5),(25,55)]
sh=Image.new('RGB',(W*len(views)*len(opts)+10*(len(opts)-1),H),(20,20,20))
base=[raw(yw,pt,W,H,1.55,1.38) for yw,pt in views]
for k,(lab,fn) in enumerate(opts):
    for i,(img,hit,pid,tt) in enumerate(base):
        im=Image.fromarray((np.clip(compose(img,hit,pid,tt,fn),0,1)*255).astype(np.uint8))
        if i==0: ImageDraw.Draw(im).text((6,6),lab,fill=(255,255,255))
        sh.paste(im,(k*(W*len(views)+10)+W*i,0))
sh.save(sys.argv[1])
