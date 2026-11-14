# ModelLab — 모델 미리보기 한곳

치비 · 큰 종족 모델은 **파이썬 미리보기(같은 거리장 수식 · numpy 레이마칭)로 먼저 맞추고 C#(`Assets/Editor/ChibiPawnCreator.*.cs`)으로 옮긴다.**
그 스크립트와 그림은 전부 여기 둔다 — 세션 임시 폴더(scratchpad)에 두면 세션이 끝날 때 흩어지고 사라진다.

## 폴더

| 폴더 | 무엇 | C# |
|---|---|---|
| `common/` | 공용 — 거리장 도구 · 큰 짐승 관절(`brute_preview.py`) · 큰 짐승 렌더러(`ogre_preview.py`) · **구운 메시 보기**(`mesh_view.py` — 게임에 든 메시 에셋을 읽어 살 + 껍데기 외곽선 · 왼쪽 정점 법선 / 오른쪽 선 법선) | `ChibiPawnCreator.Brutes` · `BakeOutlineNormals` |
| `skull/` | 해골 머리 | `ChibiPawnCreator.Skull` |
| `skeleton/` | 해골 몸(뼈) · 방어구 · 선 | `ChibiPawnCreator.Bones` |
| `troll/` | 트롤(`troll_preview.py` = v52 · 목걸이는 `necklace56.py`가 덮는다) | `ChibiPawnCreator.Troll` |
| `ogre/` | 오우거(`ogre_v3.py`) · 여성 오우거(`ogre_female.py`) | `ChibiPawnCreator.Ogre` · `.OgreFemale` |
| `orc/` | 남성 오크 머리 · 귀 · 머리카락 · 수염 · 문신 · 오크 머리카락 전부 한 장(`all_hair_sheet.py` — 남녀 · 귀 처리 B) · 게임 메시 뽑기(`bake_orc_hair.py` → `baked/` → 유니티 `Pawn/14`) | `ChibiPawnCreator.Orc` |
| `orc_female/` | 여성 오크 머리 · 얼굴 · 머리 열(`hair_v2.py`) · 체형(`body_cmp.py`) | `ChibiPawnCreator.OrcFemale` |
| `dwarf/` | 드워프 머리 · 수염 · 머리카락 · 눈썹(`dwarf.py` — `brows` 인자로 눈썹 보기) · 몸 비례(`body.py` — 사람 몸 식 그대로) | `ChibiPawnCreator.Dwarf` |
| `dwarf/` | 드워프 머리 · 수염 · 머리카락(바탕 머리 `head_base.py`) | (아직) |
| `elf/` | 엘프 머리 · 귀 셋 · 머리카락 여섯(`elf.py` — 바탕은 `dwarf/` · 한 판 약 4분 · `python elf.py out.png 5,7 0,30,70` = 고른 줄 · 보기 각도) · 몸 비례(`elf_body.py`) | `ChibiPawnCreator.Elf` |
| `human/` | 사람 치비 — 머리 열 벌(`ChibiHeadProfiles` 표 · 넓은 날카로운 뺌 · 좁은 머리 세로 ×0.88) 위 빚은 얼굴(`human.py` — 여자 이목구비 = 같은 모양의 남자 배치) · **머리카락은 확인받은 원본을 사람 머리에 다시 읽는다**(`port.py` — 엘프 · 드워프 · 여성 오크 · 오크 올백 + 사람용 `_human_family`) · 판 `port_sheet.py`(`CLOSE=1`이면 얼굴 가까이) · `head_sheet.py` · `hair_grid.py` | (아직) |
| `fungal/` | 감염된 사람(기생림) — 사람 치비 몸(`dwarf/body.py` `dims(MALE)`) + 민머리(`dwarf/head_base`)에 버섯 · 얼굴 셋 · 말굽 자리 · 틈 · 대표 버섯(`fungal.py` · 2분 · `python fungal.py out.png 시드 collar,ink,...`) | `ChibiPawnCreator.Fungal` |
| `lizard/` | 리자드맨(지저늪) — 엘프 비례 몸을 굽히고(`set_body`) 짐승 다리 · 꼬리 · 종류 넷(도마뱀 · 매끈한 뱀 `python` · 숲살모사 `viper` · 악어 — 악어는 몸도 굵다) · 눈 아홉 · 도마뱀 볏 셋. **뱀 둘은 색이 넷씩**(`종류:색` — `viper:slate` · `python:kingrat`) · 비늘은 뒤로 누워 겹친 잎(`Leaves` — 가시 아님). `python lizard.py renders/lizard_v9 [full|heads|eyes|crest|snakes]`(전부 3분 · snakes 2분) · 사진과 나란히 `python ref_sheet.py renders/lizard_v9` | `ChibiPawnCreator.Lizard`(`Pawn/16`) |
| `ent/` | 엔트 — **나무 느낌의 사람이 아니라 나무에 어거지로 사람 느낌만**(사용자 그림 둘 `_refs/2026-11-12_ent/`) · 줄기가 밑에서 갈라져 다리 · 옆 가지가 팔(좌우가 다르다) · **줄기 꼭대기가 굵은 가지로 갈라져 관이 된다**(머리와 한 살) · 눈만 사람 눈(어두운 바탕 · 조금 밝은 홍채) · 종류 넷(`oak` · `slim` · `willow` · `snag` 부러진 고목 — 얼굴도 다르다 `FACES`) · **넷 다 봄 판 · 가을 판**(`PAL` — 잎 · 이끼 · 덩굴 · 겨우살이 색이 계절을 탄다) · 잎은 가지 끝의 뭉치 + 낱잎 · 장식(`DECOR` — 이끼 · 이끼 수염 · 옹이 여섯 자리(자리마다 따로) · 덩굴 · 겨우살이 — **버섯은 없다**: 엔트는 살아 있는 나무다). 껍질 색 여섯(`BARKS` — 잿빛 · 짙은 밤색 · 붉은 갈색 · 자작 · 서리 · 흑단). `python ent.py renders/ent_v14 [full|heads|decor|bark|cmp]`(전부 3분 — 조각마다 성긴 격자로 미리 재 둔다 `accel` · `cmp`는 보낸 그림과 나란히) | (아직) |

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
