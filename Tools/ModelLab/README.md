# ModelLab — 모델 미리보기 한곳

치비 · 큰 종족 모델은 **파이썬 미리보기(같은 거리장 수식 · numpy 레이마칭)로 먼저 맞추고 C#(`Assets/Editor/ChibiPawnCreator.*.cs`)으로 옮긴다.**
그 스크립트와 그림은 전부 여기 둔다 — 세션 임시 폴더(scratchpad)에 두면 세션이 끝날 때 흩어지고 사라진다.

## 폴더

| 폴더 | 무엇 | C# |
|---|---|---|
| `common/` | 공용 — 거리장 도구 · 큰 짐승 관절(`brute_preview.py`) · 큰 짐승 렌더러(`ogre_preview.py`) | `ChibiPawnCreator.Brutes` |
| `skull/` | 해골 머리 | `ChibiPawnCreator.Skull` |
| `skeleton/` | 해골 몸(뼈) · 방어구 · 선 | `ChibiPawnCreator.Bones` |
| `troll/` | 트롤(`troll_preview.py` = v52 · 목걸이는 `necklace56.py`가 덮는다) | `ChibiPawnCreator.Troll` |
| `ogre/` | 오우거(`ogre_v3.py`) · 여성 오우거(`ogre_female.py`) | `ChibiPawnCreator.Ogre` · `.OgreFemale` |
| `orc/` | 남성 오크 머리 · 귀 · 머리카락 · 수염 · 문신 | `ChibiPawnCreator.Orc` |
| `orc_female/` | 여성 오크 머리 · 얼굴 · 머리 열(`hair_v2.py`) · 체형(`body_cmp.py`) | `ChibiPawnCreator.OrcFemale` |
| `dwarf/` | 드워프 머리 · 수염 · 머리카락 · 눈썹(`dwarf.py` — `brows` 인자로 눈썹 보기) · 몸 비례(`body.py` — 사람 몸 식 그대로) | `ChibiPawnCreator.Dwarf` |
| `dwarf/` | 드워프 머리 · 수염 · 머리카락(바탕 머리 `head_base.py`) | (아직) |
| `elf/` | 엘프 머리 · 귀 · 머리카락(`elf.py` — 바탕은 `dwarf/`) · 몸 비례(`elf_body.py`) | (아직) |
| `fungal/` | 감염된 사람(기생림) — 사람 치비 몸(`dwarf/body.py` `dims(MALE)`) + 민머리(`dwarf/head_base`)에 버섯 · 얼굴 셋(`fungal.py` · 2분) | (아직) |

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
- 한 번 쓰고 끝난 진단(확대 · 단면)은 다 쓰면 지운다. 다시 쓸 만한 보기 도구(`face_sheet.py` · `neck_pair.py` · `section.py`)만 남긴다.
