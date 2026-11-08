"""오크 머리카락 · 수염을 게임 메시로 뽑는다(11-08 사용자: "13 ㄱㄱ" — 사람 머리카락 `human/bake_hair.py`와 같은 길).

미리보기 수식(남자 `orc/orc_parts.py` · 여자 `orc_female/hair_v2.py`)을 C#으로 손으로 옮기지 않는다 — 확인받은 모양 그대로 뽑는다.
좌표는 C# 오크 머리와 같은 거리장 좌표(머리 한복판 원점 · m · +z 앞)라 그대로 놓인다(`ChibiPawnCreator.OrcSculpt` — 변환 없음).
뽑은 것은 `baked/`(깃 밖) · 목록 `baked/manifest.json` → 유니티 `DungeonColony/Pawn/14`가 읽어 다듬고(펴기 · 줄이기 · 선 법선) 메시 에셋으로 굽고,
`Pawn/3`이 오크 머리를 지을 때 이 목록으로 머리카락 · 수염 부위를 붙인다(목록이 없으면 옛 C# 수식).

이름(렌더러 = 고르기 축 · 메시)은 옛 C#과 같다:
- 남자 머리카락 `Head.Pick.Hair.{이름}@Hair.!Bald`(`Chibi_Orc_Hair_{이름}`) · 장식 `.Cuff`(뼈) · 민 층 `.Buzz`
- 남자 수염 `Head.Pick.Beard.{이름}@Beard.!None`(`Chibi_Orc_Beard_{이름}`) · 장식 `.Cuff`
- 여자 머리카락 `Head.Pick.Hair.{이름}`(`Chibi_OrcF_Hair_{이름}`) · `.Leather` · `.Metal` · 반삭 `.Buzz`(반삭 자리를 머리색으로면 `.Short`)

실행: python bake_orc_hair.py male|female [이 머리만 Mane,Bob] [--force]   (남자 · 여자를 따로 — 미리보기 모듈 이름이 같다) · 목록은 늘 다시 쓴다
      python bake_orc_hair.py manifest   (목록만)"""
import os, sys, json, gzip, time
import numpy as np
_here = os.path.dirname(os.path.abspath(__file__))
_common = os.path.join(_here, '..', 'common')
_female = os.path.join(_here, '..', 'orc_female')
OUT = os.path.join(_here, 'baked')

M_HAIRS = ['Topknot', 'WarKnot', 'Ponytail', 'Mohawk', 'Braids', 'Mane', 'KnotTail', 'HighTail',
           'Buzz', 'Undercut', 'LooseDreads', 'Cornrows', 'Spikes', 'HalfUp']
M_BEARDS = ['Braid', 'TwinBraid', 'Shaggy', 'Goatee', 'Mutton', 'BraidFull', 'Tied', 'Bushy']
F_HAIRS = {'PonyA': 'h_high_pony', 'PonyB': 'h_pony_no_temple', 'PonyA2': 'h_pony_a_back', 'PonyB2': 'h_pony_b_back', 'PonyC': 'h_pony_back_up',
           'Mohawk': 'h_mohawk', 'TwinTails': 'h_twin_tails', 'LongBraids': 'h_long_braids', 'SpikyBob': 'h_spiky_bob', 'CrestTail': 'h_crest_tail',
           'SideSweep': 'h_side_sweep', 'Dreads': 'h_dreads', 'CrownBraid': 'h_crown_braid', 'TopBun': 'h_top_bun', 'Shag': 'h_shag',
           'LongStraight': 'h_long_straight', 'LowPony': 'h_low_pony', 'Bob': 'h_bob', 'BackBraid': 'h_back_braid', 'LowBun': 'h_low_bun'}
BOX = {'mhair': ((-0.45, -1.30, -0.66), (0.45, 0.66, 0.46)), 'mbeard': ((-0.42, -1.06, -0.16), (0.42, 0.10, 0.56)),
       'fhair': ((-0.56, -0.86, -0.72), (0.56, 0.66, 0.48))}
STEP = {'hair': 0.0060, 'beard': 0.0050, 'deco': 0.0042, 'leather': 0.0042, 'metal': 0.0042, 'buzz': 0.0042}


def jobs(sex):
    out = []
    if sex == 'male':
        for n in M_HAIRS:
            out.append(dict(sex='Male', kind='mhair', name=n, ch='hair', part=f'Head.Pick.Hair.{n}@Hair.!Bald', mesh=f'Chibi_Orc_Hair_{n}', mat='HairFine'))
            out.append(dict(sex='Male', kind='mhair', name=n, ch='deco', part=f'Head.Pick.Hair.{n}.Cuff@Hair.!Bald', mesh=f'Chibi_Orc_Hair_{n}_Cuff', mat='Bone'))
            out.append(dict(sex='Male', kind='mhair', name=n, ch='buzz', part=f'Head.Pick.Hair.{n}.Buzz@Hair.!Bald', mesh=f'Chibi_Orc_Hair_{n}_Buzz', mat='HairFine'))
        for n in M_BEARDS:
            out.append(dict(sex='Male', kind='mbeard', name=n, ch='beard', part=f'Head.Pick.Beard.{n}@Beard.!None', mesh=f'Chibi_Orc_Beard_{n}', mat='HairFine'))
            out.append(dict(sex='Male', kind='mbeard', name=n, ch='deco', part=f'Head.Pick.Beard.{n}.Cuff@Beard.!None', mesh=f'Chibi_Orc_Beard_{n}_Cuff', mat='Bone'))
    else:
        for n in F_HAIRS:
            b = f'Head.Pick.Hair.{n}'
            out += [dict(sex='Female', kind='fhair', name=n, ch='hair', part=b, mesh=f'Chibi_OrcF_Hair_{n}', mat='HairFine'),
                    dict(sex='Female', kind='fhair', name=n, ch='leather', part=b + '.Leather', mesh=f'Chibi_OrcF_Hair_{n}_Leather', mat='Leather'),
                    dict(sex='Female', kind='fhair', name=n, ch='metal', part=b + '.Metal', mesh=f'Chibi_OrcF_Hair_{n}_Metal', mat='Metal'),
                    dict(sex='Female', kind='fhair', name=n, ch='buzz', part=b + '.Buzz', mesh=f'Chibi_OrcF_Hair_{n}_Buzz', mat='HairFine')]
    for j in out:
        j['file'] = j['mesh'] + '.bin.gz'
        j['relax'] = j['ch'] in ('hair', 'beard', 'buzz')
        j['keep'] = 0.35 if j['ch'] in ('hair', 'beard', 'buzz') else 0.5
    return out


_mod = {}
def field(j):
    """이 조각의 거리장 — 남자 · 여자 모듈을 따로 불러 쓴다(한 프로세스에는 한쪽만)."""
    if j['kind'] in ('mhair', 'mbeard'):
        if 'm' not in _mod:
            sys.path[:0] = [_here, _common]
            import orc_parts as o
            _mod['m'] = o
        o = _mod['m']
        if j['kind'] == 'mbeard':
            fn = dict(o.BEARDS)[j['name']]; i = 0 if j['ch'] == 'beard' else 1
            return lambda p: fn(p)[i]
        fn = dict(o.HAIRS)[j['name']]
        if j['ch'] == 'buzz':
            st = getattr(fn, 'stubble', None)
            return (lambda p: st(p)) if st else None
        i = 0 if j['ch'] == 'hair' else 1
        return lambda p: fn(p)[i]
    if 'f' not in _mod:
        sys.path[:0] = [_female, _common]; os.chdir(_female)
        import hair_v2 as H
        _mod['f'] = H
    H = _mod['f']; fn = getattr(H, F_HAIRS[j['name']])
    if j['ch'] == 'buzz':
        st = getattr(fn, 'stubble', None)
        return (lambda p: st(p)) if st else None
    i = {'hair': 0, 'leather': 1, 'metal': 2}[j['ch']]
    return lambda p: fn(p)[i]


def bake(j):
    sys.path.insert(0, _common)
    from surface_nets import surface_nets
    t0 = time.time()
    f = field(j)
    path = os.path.join(OUT, j['file'])
    if f is None:
        v = np.zeros((0, 3), np.float32); n = v; t = np.zeros((0, 3), np.int32)
    else:
        lo, hi = BOX[j['kind']]
        v, n, t = surface_nets(f, lo, hi, STEP[j['ch']])
    with gzip.open(path, 'wb', compresslevel=6) as fh:
        fh.write(np.array([len(v), len(t)], np.int32).tobytes())
        fh.write(v.astype(np.float32).tobytes()); fh.write(n.astype(np.float32).tobytes()); fh.write(t.astype(np.int32).tobytes())
    return f"{j['mesh']}: 점 {len(v)} · 세모 {len(t)} · {time.time() - t0:.0f}초"


def write_manifest():
    """뽑힌 파일만(빈 조각 — 그 머리에 없는 장식 · 반삭 — 은 뺀다). 반삭을 머리색으로 채운 여자 머리는 `.Short`로 적는다."""
    keep = ('mesh', 'part', 'file', 'mat', 'relax', 'keep', 'sex')
    items = []; skipped = 0; short = set()
    try:
        sys.path[:0] = [_female, _common]; cwd = os.getcwd(); os.chdir(_female)
        import hair_v2 as H
        short = {n for n, fn in F_HAIRS.items() if getattr(getattr(H, fn), 'stubble_full', False)}
        os.chdir(cwd)
    except Exception as e:
        print('여자 미리보기를 못 불렀다(.Short 판정 생략):', e)
    for j in jobs('male') + jobs('female'):
        path = os.path.join(OUT, j['file'])
        if not os.path.exists(path): skipped += 1; continue
        with gzip.open(path, 'rb') as fh:
            if np.frombuffer(fh.read(8), np.int32)[0] == 0: skipped += 1; continue
        it = {k: j[k] for k in keep}
        if j['ch'] == 'buzz' and j['name'] in short: it['part'] = it['part'].replace('.Buzz', '.Short')
        items.append(it)
    with open(os.path.join(OUT, 'manifest.json'), 'w', encoding='utf-8') as fh:
        json.dump({'items': items}, fh, ensure_ascii=False, indent=1)
    print(f'목록 {len(items)}(빈 조각 · 안 뽑은 것 {skipped} 뺌)')


if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    mode = sys.argv[1] if len(sys.argv) > 1 else 'manifest'
    if mode == 'manifest':
        write_manifest(); sys.exit()
    rest = [a for a in sys.argv[2:] if not a.startswith('--')]
    only = set(rest[0].split(',')) if rest else None
    force = '--force' in sys.argv
    js = jobs(mode)
    todo = [j for j in js if (only is None or j['name'] in only) and (force or not os.path.exists(os.path.join(OUT, j['file'])))]
    print(f'{mode}: 목록 {len(js)} · 뽑을 것 {len(todo)}', flush=True)
    from multiprocessing import Pool
    t0 = time.time()
    with Pool(12) as pool:
        for k, msg in enumerate(pool.imap_unordered(bake, todo)):
            print(f'[{k + 1}/{len(todo)}] {msg}', flush=True)
    write_manifest()
    print(f'끝 · {time.time() - t0:.0f}초')
