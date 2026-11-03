"""각 조각이 차지하는 상자(머리 높이 단위) — C# 조각의 굽는 상자로 쓴다. 오른쪽(+x) 팔다리만 잰다."""
import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))  # 공용: brute_preview · ogre_preview
import numpy as np
import troll_preview as T

S = 0.012
xs = np.arange(-1.0, 1.0, S, dtype=np.float32)
ys = np.arange(0.0, 2.0, S, dtype=np.float32)
zs = np.arange(-0.8, 1.3, S, dtype=np.float32)

def box(f, xr=(-1.0, 1.0)):
    lo = np.array([9, 9, 9.]); hi = -lo
    X = xs[(xs >= xr[0]) & (xs <= xr[1])]
    for y in ys:
        Xg, Zg = np.meshgrid(X, zs, indexing='ij')
        p = np.stack([Xg, np.full_like(Xg, y), Zg], -1)
        d = f(p)
        m = d < 0
        if m.any():
            pts = p[m]
            lo = np.minimum(lo, pts.min(0)); hi = np.maximum(hi, pts.max(0))
    pad = 0.04
    c = (lo + hi) / 2; s = hi - lo + 2 * pad
    return c, s

R = (0.0, 1.0)
items = [
    ('Torso', T.torso, None), ('Belly', T.belly, None), ('Loin', T.loin, None), ('Head', T.head, None),
    ('Eyes', T.eyes, None), ('Mouth', T.mouth, None), ('Teeth', T.teeth, None), ('Collar', T.collar, None),
    ('BoneCord', T.bone_cord, None), ('BoneTeeth', T.bone_teeth, None),
    ('Upper', T.upper, R), ('Fore', T.fore, R), ('Hand', T.hand, R), ('Thigh', T.thigh, R), ('Shin', T.shin, R), ('Foot', T.foot, R),
    ('Wrap', T.wrap, R), ('Anklet', T.anklet, R),
]
for name, f, xr in items:
    c, s = box(f, xr or (-1.0, 1.0))
    print(f"{name:10s} c=({c[0]:.3f}, {c[1]:.3f}, {c[2]:.3f}) s=({s[0]:.3f}, {s[1]:.3f}, {s[2]:.3f})")
print('HEAD_SHIFT', T.HEAD_SHIFT, 'ARM_SHIFT', T.ARM_SHIFT, 'LEAN', T.LEAN, 'CF', T.CF)
print('TEETH', T.TEETH)
print('chest_front at teeth', [T.chest_front(y - 0.04, x=x) + 0.018 for x, y, z in T.TEETH])
