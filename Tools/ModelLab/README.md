# ModelLab — 모델 미리보기 한곳

치비 · 큰 종족 모델은 **파이썬 미리보기(같은 거리장 수식 · numpy 레이마칭)로 먼저 맞추고 C#(`Assets/Editor/ChibiPawnCreator.*.cs`)으로 옮긴다.**
그 스크립트와 그림은 전부 여기 둔다 — 세션 임시 폴더(scratchpad)에 두면 세션이 끝날 때 흩어지고 사라진다.

## 폴더

| 폴더 | 무엇 | C# |
|---|---|---|
| `common/` | 공용 — 거리장 도구 · 큰 짐승 관절(`brute_preview.py`) · 큰 짐승 렌더러(`ogre_preview.py`) · **구운 메시 보기**(`mesh_view.py` — 게임에 든 메시 에셋을 읽어 살 + 껍데기 외곽선 · 왼쪽 정점 법선 / 오른쪽 선 법선) · **거리장 → 게임 스킨 메시 뽑기**(`bake_sdf.py` — 조각마다 표면 그물 · 정점마다 뼈 둘 · 축 이름 · 팔레트 몫) · 뽑은 것 보기(`baked_view.py` — 색 · 뼈 나눔 · 굽힌 자세 · 고르기 축 · 팔레트를 골라 보기 `baked_pick_view.py`) · **폰마다 점 수**(`pawn_verts.py` — 프리팹의 켜진 렌더러 · 머리형은 한 벌만 셈 · 1초) · **메시 거칠기 재기**(`mesh_audit.py` — 폰 메시 에셋 전부 · 또는 `--baked <폴더>`를 잔물결 · 꺾임으로 재서 나쁜 순 CSV · 1분) | `ChibiPawnCreator.Brutes` · `BakeOutlineNormals` · `ChibiPawnCreator.Baked` |
| `skull/` | 해골 머리 | `ChibiPawnCreator.Skull` |
| `skeleton/` | 해골 몸(뼈) · 방어구 · 선 | `ChibiPawnCreator.Bones` |
| `troll/` | 트롤(`troll_preview.py` = v52 · 목걸이는 `necklace56.py`가 덮는다) | `ChibiPawnCreator.Troll` |
| `ogre/` | 오우거(`ogre_v3.py`) · 여성 오우거(`ogre_female.py`) | `ChibiPawnCreator.Ogre` · `.OgreFemale` |
| `orc/` | 남성 오크 머리 · 귀 · 머리카락 · 수염 · 문신 · 오크 머리카락 전부 한 장(`all_hair_sheet.py` — 남녀 · 귀 처리 B) · 게임 메시 뽑기(`bake_orc_hair.py` → `baked/` → 유니티 `Pawn/14`) | `ChibiPawnCreator.Orc` |
| `orc_female/` | 여성 오크 머리 · 얼굴 · 머리 열(`hair_v2.py`) · 체형(`body_cmp.py`) | `ChibiPawnCreator.OrcFemale` |
| `dwarf/` | 드워프 머리 · 수염 · 머리카락 · 눈썹(`dwarf.py` — `brows` 인자로 눈썹 보기) · 몸 비례(`body.py` — 사람 몸 식 그대로) | `ChibiPawnCreator.Dwarf` |
| `dwarf/` | 드워프 머리 · 수염 · 머리카락(바탕 머리 `head_base.py`) | (아직) |
| `elf/` | 엘프 머리 · 귀 셋 · 머리카락 여섯(`elf.py` — 바탕은 `dwarf/` · 한 판 약 4분 · `python elf.py out.png 5,7 0,30,70` = 고른 줄 · 보기 각도) · 몸 비례(`elf_body.py`) | `ChibiPawnCreator.Elf` |
| `human/` | 사람 치비 — 머리 열 벌(`ChibiHeadProfiles` 표 · 넓은 날카로운 뺌 · 좁은 머리 세로 ×0.88) 위 빚은 얼굴(`human.py` — 여자 이목구비 = 같은 모양의 남자 배치) · **머리카락은 확인받은 원본을 사람 머리에 다시 읽는다**(`port.py` — 엘프 · 드워프 · 여성 오크 · 오크 올백 + 사람용 `_human_family`) · 판 `port_sheet.py`(`CLOSE=1`이면 얼굴 가까이) · `head_sheet.py` · `hair_grid.py` · 뱀파이어 로드 머리 넷 + 더듬이도 뽑는다(`bake_hair.py` `VAMP` — `vampire/lord_hair.py`를 다시 읽는다) | (아직) |
| `fungal/` | 감염된 사람(기생림) — 사람 치비 몸(`dwarf/body.py` `dims(MALE)`) + 민머리(`dwarf/head_base`)에 버섯 · 얼굴 셋 · 말굽 자리 · 틈 · 대표 버섯(`fungal.py` · 2분 · `python fungal.py out.png 시드 collar,ink,...`) | `ChibiPawnCreator.Fungal` |
| `lizard/` | 리자드맨(지저늪) — 엘프 비례 몸을 굽히고(`set_body`) 짐승 다리 · 꼬리 · 종류 넷(도마뱀 · 매끈한 뱀 `python` · 숲살모사 `viper` · 악어 — 악어는 몸도 굵다) · 눈 아홉 · 도마뱀 볏 셋. **뱀 둘은 색이 넷씩**(`종류:색` — `viper:slate` · `python:kingrat`) · 비늘은 뒤로 누워 겹친 잎(`Leaves` — 가시 아님). `python lizard.py renders/lizard_v9 [full|heads|eyes|crest|snakes]`(전부 3분 · snakes 2분) · 사진과 나란히 `python ref_sheet.py renders/lizard_v9` · **공룡 악어**(`brute` — 따로인 큰 종족 · 생김새 축 `BRUTE_LOOK` · 색 `BRUTE_PAL`) 후보 판 `python lizard.py renders/brute_vN looks`(2분 30초) · **게임 메시 뽑기** `python bake_croc.py`(30초 · 축마다 조각) → 유니티 `Pawn/18` · 도마뱀 볏(`fins`)은 C#으로 옮겼다 | `ChibiPawnCreator.Lizard`(`Pawn/16`) · `ChibiPawnCreator.Baked`(`Pawn/18`) |
| `ent/` | 엔트 — **나무 느낌의 사람이 아니라 나무에 어거지로 사람 느낌만**(사용자 그림 둘 `_refs/2026-11-12_ent/`) · 줄기가 밑에서 갈라져 다리 · 옆 가지가 팔(좌우가 다르다) · **줄기 꼭대기가 굵은 가지로 갈라져 관이 된다**(머리와 한 살) · 눈만 사람 눈(어두운 바탕 · 조금 밝은 홍채) · 종류 넷(`oak` · `slim` · `willow` · `snag` 부러진 고목 — 얼굴도 다르다 `FACES`) · **넷 다 봄 판 · 가을 판**(`PAL` — 잎 · 이끼 · 덩굴 · 겨우살이 색이 계절을 탄다) · 잎은 가지 끝의 뭉치 + 낱잎 · 장식(`DECOR` — 이끼 · 이끼 수염 · 옹이 여섯 자리(자리마다 따로) · 덩굴 · 겨우살이 — **버섯은 없다**: 엔트는 살아 있는 나무다). 껍질 색 여섯(`BARKS` — 잿빛 · 짙은 밤색 · 붉은 갈색 · 자작 · 서리 · 흑단). `python ent.py renders/ent_v14 [full|heads|decor|bark|cmp]`(전부 3분 — 조각마다 성긴 격자로 미리 재 둔다 `accel` · `cmp`는 보낸 그림과 나란히) · **게임 메시 뽑기** `python bake_ent.py`(여덟 벌 · 1분 — 정점마다 색 · 뼈 가중치) → 유니티 `Pawn/17` | `ChibiPawnCreator.Ent`(`Pawn/17`) |
| `werewolf/` | 늑대인간 — 사람 치비 몸(`dwarf/body.py` `dims`) + 역관절 다리 + 늑대 머리(주둥이 · 솟은 귀 · 볼 갈기 · 호박색 눈) · 털 술 팔뚝 · 꼬리. `python werewolf.py renders/werewolf_vN`(50초) · **게임 메시 뽑기** `python bake_werewolf.py`(10초 · 사람의 1.1배) → 유니티 `Pawn/19` | `ChibiPawnCreator.Baked`(`Pawn/19`) |
| `vampire/` | 뱀파이어(11-18 확정) — **사람 치비 몸 그대로**(`body.figure`의 셈 · 남 `MALE` / 여 C# `Female` 줄 · 머리가 떠 있다) + 사람 머리 표(`human/human.py` `Head`). **로드**(순혈 — 사람 얼굴 그대로 · 창백한 회색 · 붉은 눈 · 송곳니 · 높은 깃 망토 · 남 연미 코트 / 여 코르셋 + 긴 치마) · 머리 일곱 벌은 `lord_hair.py`(매끈한 한 덩어리 + 낮게 묻은 다발 · `curtain` · `curtain_plain` · `slick` · `slick_bangs` · `long`(주름 · 물결 · 들쭉날쭉한 끝) · `lowtail` · `lowtail_bangs` — **더듬이 앞머리는 따로 켜고 끄는 조각**(`Ribbon` — 얇고 납작한 띠 · 뾰족한 끝)). **노예**(물린 하급 — 보낸 그림 `_refs/2026-11-15_vampire/` · 민머리 · 푸른 잿빛 · 탈 · 빛나는 눈 · 미간 주름 · 역삼각형 코 `nose_pts` · 뾰족 귀 · 해진 옷 · 족쇄). **사람 머리 거리장은 정수리 위에서 열 배 넘게 크게 답한다** — `safe_crown`으로 걸음을 묶는다. `python vampire.py renders/vampire_vN [lord,lordF,thrall,thrallF\|cmp\|hairs]`(3분 · `cmp` 노예 얼굴을 그림과 나란히 · `hairs` 로드 머리 일곱 벌) | `ChibiPawnCreator.Vampire`(`Pawn/22` · 뽑기 `python bake_vampire.py` — 몸 넷 → `baked/`(깃 밖)) |
| `mole/` | 두더지(네 발 짐승) — 소만 한 거대 두더지(단위 몸 × `SIZE` 1.6 · 어깨 약 1.4m · 몸길이 약 2.8m): 목 없는 술통 몸 · 원뿔 머리 · 분홍 코끝 · 동그란 눈 + 반짝임 · 네 발 다 땅을 딛는 넓적한 발(앞발이 조금 크다 · 상아 발톱) · 몽당 꼬리. 공용 틀 `common/quad_preview.py`(네 발 도우미 + 한 장 판 — 조각 = (거리 함수, 색, 부위) · 부위는 네발 관절 `Body` · `Head` · `Tail` · `LegF/B`). `python mole.py renders/mole_vN`(40초) · **게임 메시 뽑기** `python bake_mole.py`(6초 · `common/bake_quad.py` — 네발 뼈) → 유니티 `Pawn/20` | `ChibiPawnCreator.Quad`(`Pawn/20`) |
| `rat/` | 괴물쥐(네 발 짐승) — 큰 개만 한 쥐: 솟은 엉덩이 · 등줄기 털 가시 · 누런 앞니 · 붉은 구슬눈 · 크고 얇은 귀(가장자리로 얇아지는 오목한 판 · 왼귀 찢김) · 수염 · 고리 무늬 맨살 꼬리(쉬는 자세는 좌우 대칭 · 마디 점이 꼬리 뼈 사슬 `TAIL_BONES`) · 등 가시는 몸 표면에서 뽑는다(`surface_y`). 틀은 두더지와 같다. `python rat.py renders/rat_vN`(40초 · 생김새 세 벌 `RAT_LOOK=A|B|C` — 표 `LOOKS`) · **게임 메시 뽑기** `python bake_rat.py`(꼬리 마디 여섯) → 유니티 `Pawn/21` | `ChibiPawnCreator.Quad`(`Pawn/21`) |
| `toad/` | 포자두꺼비(네 발 짐승 · 균사림이라 푸르다) — 등이 높이 솟은 혹 몸 · 머리 꼭대기가 사람 키쯤(단위 몸 × `SIZE` 1.85) · 낮게 숙인 넓적한 쐐기 머리 · 두꺼운 눈두덩 밑의 빛나는 작은 눈(눈은 머리 겉에서 자리를 찾고 눈두덩 · 볼을 눈에 맞춰 붙인다 — 덮으면 사라진다) · 처진 입 홈 · 아래턱 엄니 · 사마귀 · 짧은 다리. **등에 포자 주머니 무리**(낮은 돔 + 숨구멍 굴뚝 · 시안 가루 색 — 넓은 구멍은 눈알 같아 뺐다 · 머리 위 포자샘도 눈처럼 보여 뺐다). 틀은 두더지와 같다. `python toad.py renders/toad_vN`(1분) · **게임 메시 뽑기** `python bake_toad.py`(15초 · 꼬리 없음) → 유니티 `Pawn/23` | `ChibiPawnCreator.Quad`(`Pawn/23`) |
| `kobold/` | 코볼트 — **하이에나 몬스터**(사용자: *"개 얼굴 느낌의 하이에나"*) · 늑대인간 스크립트를 갈래 냈다(사람 치비 몸 + 역관절 다리 + 짐승 머리 — 몸 · 손 · 발은 같은 식): 짧고 굵은 주둥이 · 검은 주둥이 앞 · 짧고 둥근 귀 · 목덜미 털 덩이가 허리까지 가늘어지며 등 살에 녹는다(`fur_shell` — 등 중간에서 끊기면 턱이 진다) · 그 위 정수리 → 등의 뻣뻣한 볏(`_crest_tufts` · 늑대인간 갈기는 안 쓴다) · 모래색 + 점박이(`spots`) · 짧고 끝이 검은 꼬리 · 목둘레 술(collar) 없음 · **옷은 웃옷 × 아랫도리 × 붕대를 따로 고른다**(`TOPS` 없음 · 가죽 조끼 · 웃옷 · 넝마 웃옷 · 어깨 쇠 조각 / `BOTTOMS` 반바지 · 긴 바지 · 넝마 띠 / 팔 · 정강이 붕대 — 넝마 느낌: 꿰맨 조각 · 징 · 해진 밑단 · 구멍 · 덧댄 조각 · 참고 그림 `_refs/2026-11-19_kobold/`) · 몸에 닿는 옷은 전부 등 털 덩이까지 감싼다(`_wear_base`) · 바지를 입으면 넓적다리 털 술을 뺀다(`PANTS_ON`) · **털 벌 여섯**(`COATS` — 점박이 · 붉은 · 줄무늬 · 갈색(목 망토) · 바랜 · 검은 — 게임에서는 프리팹 하나 + 팔레트: 바탕 칸 = 바탕 · 짙은 · 밝은 · 주둥이, 덧칠 칸 = 점 · 줄 · 망토) 판 `python kobold.py renders/kobold_vN coats`(1분) · 조합 판 `python kobold.py renders/kobold_vN outfits`(`COMBOS` 여덟 · 1분) · **게임 메시 뽑기** `python bake_kobold.py`(10초 · 옷 축 넷 · 털 팔레트 여섯 — 털 메시는 무늬 셋마다) → 유니티 `Pawn/24` · 확인 `python ../common/baked_pick_view.py baked out.png "Palette=Spotted Top=Vest Bottom=Rags WrapArm=On WrapShin=Off" …` | `ChibiPawnCreator.Baked`(`Pawn/24`) |

- **작업 공간(`.claude/worktrees/`)에 만들지 않는다** — 이 폴더는 본 체크아웃에만 있다(작업 공간에는 따라오지 않는다).

- 모델 폴더마다 `renders/` — 그 모델로 뽑은 그림(판 번호를 이름에). **깃에는 안 올린다**(`.gitignore`).
- `_refs/` — 사용자가 보낸 그림(참고 그림 · 유니티 화면) · 날짜_세션별. **깃에 안 올린다** — 남의 그림은 저장소에 넣지 않는다.
- 고블린 미리보기 스크립트는 남아 있지 않았다(그 세션 폴더가 비어 있었다) — C#(`ChibiPawnCreator.Goblin`)이 원본이다.

## 규칙

- **새 모델은 `Tools/ModelLab/<모델>/`에 폴더를 만든다** — 세션 scratchpad에 만들지 않는다.
- **판마다 사본(`_v23.py`)을 쌓지 않는다** — 스크립트는 하나를 고쳐 가고(이력은 git), 그림만 판 번호로 남긴다(`renders/troll_full50.png`).
- 공용(`common/`)을 쓰는 스크립트는 맨 위 한 줄이 `common`을 경로에 넣는다(아래). 새 스크립트에도 붙일 것.
  ```python
  import os as _os, sys as _sys; _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'common'))
  ```
- 실행은 그 모델 폴더에서 — 그림은 `renders/`로: `cd Tools/ModelLab/troll && python troll_preview.py renders/troll_full57.png`
- **수치를 고치면 C#에도 같이 옮긴다** — 미리보기와 C#이 어긋나면 그림으로 맞춘 것이 거짓이 된다.
- 렌더러(`dwarf/head_base.render`)는 안 끝난 광선만 재고 다 끝나면 멈춘다.
- 한 번 쓰고 끝난 진단(확대 · 단면)은 다 쓰면 지운다. 다시 쓸 만한 보기 도구(`face_sheet.py` · `neck_pair.py` · `section.py`)만 남긴다.
