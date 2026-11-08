"""사람 머리카락을 게임 메시로 뽑는다(11-08 사용자: *"파이썬 메시 뽑기"* — 미리보기 수식을 C#으로 손으로 옮기지 않는다).

머리카락 · 앞머리 · 앞을 덜어 낸 뒷머리를 **성별 둘 × 윗머리 모양 둘**(둥근 = 넓은 부드러운 머리 · 네모 = 넓은 네모 머리)로 뽑는다 —
나머지 머리 모양에는 게임이 늘려 씌운다(`PawnLook` — 굽기 B안). 머리 한복판이 원점 · 단위 m · +z 앞.
뽑은 것은 `baked/`(깃 밖)에 · 목록은 `baked/manifest.json` → 유니티 `DungeonColony/Pawn/13`이 읽어 다듬고(펴기 · 줄이기) 메시 에셋으로 굽는다.

이름 규칙(유니티 렌더러 이름 = `PawnLook` 고르기 축):
- 머리카락 `Head.Pick.Hair.{머리}@Fit.{Round|Box}@Hair.!Bald` (앞머리를 얹는 머리는 `@Bangs.None|Parted|SideOpen|Open` — 덮는 앞머리일 때는
  앞을 덜어 낸 판 `…@Bangs.Clumps|Sweep|Wispy`이 대신 켜진다) · 조각은 `.Buzz`(반삭) · `.Plait`(땋은 장식) · `.Metal`(쇠) · `.Tie`(끈 · 묶음)
- 앞머리 `Head.Pick.Bangs.{앞머리}@Fit.{…}@Hair.{얹는 머리들}`

실행: python bake_hair.py [이 머리만 Long,Spiky] [--force]   (이미 뽑은 파일은 건너뛴다 · 목록은 늘 다시 쓴다)"""
import os, sys, json, gzip, time
import numpy as np
_here = os.path.dirname(os.path.abspath(__file__))
sys.path.append(os.path.join(_here, '..', 'common'))
from surface_nets import surface_nets
import port

OUT = os.path.join(_here, 'baked')
FITS = {'Round': 'BroadSoft', 'Box': 'BroadSquare'}
GENDERS = ('Male', 'Female')
LO, HI = (-0.62, -0.95, -0.80), (0.62, 0.62, 0.52)
STEP = {'hair': 0.0055, 'buzz': 0.0055, 'tie': 0.0040, 'plait': 0.0040, 'deco': 0.0040}

# 확정한 머리 서른여덟(11-08 · `catalog.py`) — (게임 이름, 미리보기 종류, 미리보기 이름)
HAIRS = [('Pomp', 'human', 'Pomp'), ('Slick', 'human', 'Slick'), ('BangsMShort', 'human', 'BangsMShort'), ('ShortFringe', 'human', 'ShortFringe'),
         ('Messy', 'human', 'Messy'), ('Spiky', 'human', 'Spiky'), ('BuzzCut', 'human', 'BuzzCut'), ('Dandy', 'human', 'Dandy'), ('Comma', 'human', 'Comma'),
         ('FauxHawk', 'human', 'FauxHawk'), ('Pixie', 'human', 'Pixie'), ('Dreads', 'human', 'Dreads'), ('Crop', 'human', 'Crop'), ('BaldWings', 'human', 'BaldWings'),
         ('Crest', 'human', 'Crest'), ('Fringe', 'human', 'Fringe'), ('LongFringe', 'human', 'LongFringe'),
         ('ManBun', 'human', 'ManBun'), ('LowTail', 'human', 'LowTail'), ('HalfBun', 'human', 'HalfBun'), ('Topknot', 'human', 'Topknot'), ('Braid', 'human', 'Braid'),
         ('HalfUp', 'elf', 'HalfUp'), ('CrestTail', 'orcF', 'CrestTail'), ('TwinTails', 'human', 'TwinTails'), ('TwinTailsV', 'human', 'TwinTailsV'),
         ('TwinBraids', 'human', 'TwinBraids'), ('LongBraids', 'human', 'LongBraids'),
         ('Long', 'human', 'Long'), ('Mane', 'human', 'Mane'), ('Shoulder', 'human', 'Shoulder'), ('Curtain', 'human', 'Curtain'), ('SlickMullet', 'human', 'SlickMullet'),
         ('Straight', 'elf', 'Straight'), ('BangsM', 'elf', 'BangsM'), ('BangsArch', 'elf', 'BangsArch'), ('SpikyBob', 'orcF', 'SpikyBob'), ('Mohawk', 'orcF', 'Mohawk')]
MALE_ONLY = port.MALE_ONLY
# 성별마다 못 고르는 머리(11-08 사용자: *"너무 여자 머리 같은 것은 남캐가, 너무 남자 머리 같은 것은 여캐가 못 고르게"*).
# 목록에서 빠지면 `Pawn/3`이 그 성별 몸에 안 붙인다(캐릭터 창 · 씨앗 둘 다 못 고른다). 굽지 않고 목록만 다시 쓴다: `python bake_hair.py`.
NOT_FOR = {
    'Male':   ('Pixie', 'TwinTails', 'TwinTailsV', 'TwinBraids', 'LongBraids', 'SpikyBob', 'HalfBun'),
    'Female': MALE_ONLY + ('Pomp', 'Slick', 'SlickMullet', 'BangsMShort', 'BuzzCut', 'Dandy', 'ManBun', 'FauxHawk'),
}
COVER, OPEN = ('Clumps', 'Sweep', 'Wispy'), ('Parted', 'SideOpen', 'Open')
BANGABLE = ('Straight', 'HalfUp', 'LongBraids', 'TwinBraids', 'Braid', 'Shoulder', 'Mane', 'Long', 'LowTail', 'HalfBun', 'ManBun')   # 앞머리를 얹는 머리(`port.BACKS` · 엘프 꽁지는 뺐다)
NO_OPEN = ('Straight', 'Shoulder')   # 깐 머리가 앞머리 없음과 같다(`port.SAME_AS_NONE`)
PIECE = {'buzz': 'Buzz', 'plait': 'Plait', 'deco': 'Metal', 'hair': 'Tie'}
MAT = {'deco': 'Metal'}

def jobs():
    out = []; fams = {}
    for g in GENDERS:
        for fit, shape in FITS.items():
            head = f'{g}_{shape}'
            for name, kind, src in HAIRS:
                if name in NOT_FOR[g]: continue
                if (kind, head) not in fams: fams[(kind, head)] = port.family(kind, head)[1]
                fam = fams[(kind, head)]
                tags = [t for _, t in fam[src](np.zeros((1, 3), np.float32))]
                for i, tag in enumerate(tags):
                    cond = f'@Fit.{fit}'
                    if i == 0:
                        cond += '@Hair.!Bald'
                        if name in BANGABLE: cond += '@Bangs.' + '|'.join(('None',) + OPEN)
                        part = f'Head.Pick.Hair.{name}{cond}'
                    else:
                        part = f'Head.Pick.Hair.{name}.{PIECE[tag]}{cond}'
                    out.append(dict(gender=g, fit=fit, head=head, kind=kind, src=src, piece=i, tag=tag, part=part,
                                    mesh=f'Chibi_HumanHair_{g}_{fit}_{name}' + (f'_{i}' if i else ''), mat=MAT.get(tag, 'HairFine')))
            for b in BANGABLE:
                if b in NOT_FOR[g]: continue
                out.append(dict(gender=g, fit=fit, head=head, kind='human', src=f'Trim:{b}', piece=0, tag='hair',
                                part=f'Head.Pick.Hair.{b}@Fit.{fit}@Bangs.' + '|'.join(COVER), mesh=f'Chibi_HumanHair_{g}_{fit}_{b}_Trim', mat='HairFine'))
            for k in COVER + OPEN:
                on = [b for b in BANGABLE if not (k == 'Open' and b in NO_OPEN) and b not in NOT_FOR[g]]
                out.append(dict(gender=g, fit=fit, head=head, kind='human', src=f'Bangs:{k}', piece=0, tag='hair',
                                part=f'Head.Pick.Bangs.{k}@Fit.{fit}@Hair.' + '|'.join(on), mesh=f'Chibi_HumanHair_{g}_{fit}_Bangs_{k}', mat='HairFine'))
    for j in out:
        j['file'] = j['mesh'] + '.bin.gz'
        j['relax'] = j['tag'] in ('hair', 'buzz', 'plait')
        j['keep'] = 0.35 if j['tag'] in ('hair', 'buzz') else 0.5
    return out

def bake(j):
    t0 = time.time()
    _, fam = port.family(j['kind'], j['head'])
    fn = fam[j['src']]; i = j['piece']
    f = lambda p: fn(p)[i][0]
    v, n, t = surface_nets(f, LO, HI, STEP[j['tag']])
    with gzip.open(os.path.join(OUT, j['file']), 'wb', compresslevel=6) as fh:
        fh.write(np.array([len(v), len(t)], np.int32).tobytes())
        fh.write(v.astype(np.float32).tobytes()); fh.write(n.astype(np.float32).tobytes()); fh.write(t.astype(np.int32).tobytes())
    return f"{j['mesh']}: 점 {len(v)} · 세모 {len(t)} · {time.time() - t0:.0f}초"

def write_manifest(js):
    """목록 — 뽑은 파일이 빈 조각(점 0 — 엘프 생머리의 쇠 · 땋은 장식 자리처럼 그 머리에 없는 조각)은 뺀다."""
    keep = ('mesh', 'part', 'file', 'mat', 'relax', 'keep', 'gender', 'fit')
    items = []
    for j in js:
        path = os.path.join(OUT, j['file'])
        if not os.path.exists(path): continue
        with gzip.open(path, 'rb') as fh:
            if np.frombuffer(fh.read(8), np.int32)[0] == 0: continue
        items.append({k: j[k] for k in keep})
    with open(os.path.join(OUT, 'manifest.json'), 'w', encoding='utf-8') as fh:
        json.dump({'items': items}, fh, ensure_ascii=False, indent=1)
    print(f'목록 {len(items)}(빈 조각 · 안 뽑은 것 {len(js) - len(items)} 뺌)')

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    only = [a for a in sys.argv[1:] if not a.startswith('--')]
    only = set(only[0].split(',')) if only else None
    force = '--force' in sys.argv
    js = jobs()
    todo = [j for j in js if (only is None or any(x in j['mesh'].split('_') or j['src'].endswith(x) for x in only))
            and (force or not os.path.exists(os.path.join(OUT, j['file'])))]
    print(f'목록 {len(js)} · 뽑을 것 {len(todo)}', flush=True)
    from multiprocessing import Pool
    t0 = time.time()
    with Pool(14) as pool:
        for k, msg in enumerate(pool.imap_unordered(bake, todo)):
            print(f'[{k + 1}/{len(todo)}] {msg}', flush=True)
    write_manifest(js)
    print(f'끝 · {time.time() - t0:.0f}초')
