# 렌더링 — 셰이더가 빌드에 들어가는 길 · 런타임 재질

**한 줄:** 이 축의 사고는 **에디터에서는 멀쩡하고 빌드에서만 틀리거나**, **알파만 낮추고
조용히 불투명하거나**, **클래스가 통째로 죽어 "안 보인다"로만 보인다** — 셋 다 화면에
원인이 안 적힌다.

## 셰이더가 빌드에 들어가는 길은 둘뿐이고 무게로 갈린다

**재질 에셋이 그것을 가리키는 것**이 유일한 정상 경로다. **코드가 이름으로 찾는 것
(`Shader.Find`)은 참조가 아니다** — 그래서 에디터에서는 끝까지 재현이 안 된다.

- **작은 것은 Always Included**(`RuntimeShaders.AlwaysInclude`)
- **무거운 것은 재질 에셋**(지형처럼 변형이 수만 개인 것)

**새로 `Shader.Find`를 쓰면 이름을 `RuntimeShaders`에 추가할 것.**

## 런타임 재질을 만질 때

- **투명은 색이 아니라 상태다** — `FadeMaterial` 하나가 답한다.
- **`MaterialPropertyBlock`을 필드 초기화자에서 만들지 말 것** — 정적 초기화자는 **정적
  생성자**에서 도는데 그 시점이 `MonoBehaviour` 생성 도중일 수 있어 유니티가 막고
  (`CreateImpl is not allowed…`), **정적 생성자가 던지면 그 클래스가 통째로 죽는다.**
  화면에는 *"벽이 안 뜬다"*로만 보인다(원인이 색칠 한 줄이라는 표시가 없다).
  **처음 쓸 때 만든다** — 순수 관리 객체처럼 보이지만 **네이티브 자원을 잡는다**
  (`Mesh`·`Material`과 같은 취급).
- **재질은 복제한다** — 공유 재질을 건드리면 **세상의 모든 지붕이 같이 흐려진다**
  (`BuildingRoof`).
- **종류마다 재질을 만들지 않는다** — 바이옴 여덟, 표식 종류마다 한 벌씩 만들면
  **그중 하나는 반드시 뒤처진다.** 재질도 메시도 한 벌이고 **색만
  `MaterialPropertyBlock`으로 얹는다**(`RockRenderer`·`PawnMark`).

## 그림자를 던지는 조명은 몫이 있다 (2026-10-11)

점 조명 하나는 그림자 지도 여섯 장이다. 판(2048²)을 넘기면 URP가 버릴 것을 제멋대로 고른다 — 그래서 고르는 것을 `ShadowBudget`이 한다(시선 축에 가까운 `MaxCasters`개 · 경계에서 세기를 줄여 끈다). **그림자를 켠 조명을 새로 세우면 `ShadowBudget.Track`을 부를 것**(지금은 벽 조명 `RockRenderer`와 `Skylight`). 수백 개씩 서는 것(야광석·버섯·광맥)은 처음부터 그림자가 없다.

## 다른 곳에 있는 것

- **바위 자르기(`RockCut.hlsl`)와 그리는 패스 목록** → `design_underworld.md` "바위"
- **지형 사본 셰이더(매크로 변주·거리 블렌드·triplanar)** → `world.md` "지형 그리기"
- **천장 셰이더와 폴백을 안 두는 이유** → `design_underworld.md` "걷히는 규칙"
  (**죽은 절이다** — `CeilingRenderer`를 지웠다)

---

- **런타임에 `Shader.Find`로 찾는 셰이더는 빌드에서 잘려 나간다 — 목록은 `RuntimeShaders` 하나다**(2026-09-02). 셰이더가 빌드에 들어가는 길은 **재질 에셋이 그것을 가리키는 것** 하나뿐이고, **코드가 이름으로 찾는 것은 참조가 아니다.** 그래서 **에디터에서는 멀쩡하고 빌드에서만 검게 나온다** — 지하 지형이 통째로 검은 판이었고 원인은 `DungeonColony/Terrain/Lit`이 아무 재질도 안 가리켜 통째로 잘린 것이었다(화면에는 *"땅바닥이 검다"*로만 보인다). **넣는 문이 둘이고 무게로 갈린다** — **작은 것은 Always Included**(`RuntimeShaders.AlwaysInclude` 넷), **무거운 것은 재질 에셋**이다. **Always Included에 아무거나 넣으면 빌드가 죽고, 죽는 길이 둘이다**: **에디터 전용 셰이더**(`Hidden/Internal-Colored` — `Library/unity default resources`에 `HideFlags.DontSave`로 들어 있어 **애초에 빌드에 못 들어간다.** `Hidden/`으로 시작하는 것은 넣지 말 것)와 **무거운 셰이더**(*"모든 변형을 굽고 넣어라"*라는 뜻이라 **지형 셰이더에 쓰면 굽다가 죽는다**(2026-09-02에 겪었다 — 아홉을 넣었더니 유니티가 버그 리포터를 띄웠다. URP 지형은 패스 넷 × 스플랫·인스턴싱 옵션이라 변형이 수만 개다)). **크래시의 원인은 `%LOCALAPPDATA%/Unity/Editor/Editor-prev.log`의 스택 직전 몇 줄이 답한다** — 추측으로는 두 번 틀렸다. 지형은 `0. Setup World Streaming`이 `TerrainChunk.mat`을 만들어 `TerrainStreamer.terrainMaterial`에 물린다 — 그러면 **런타임에 찾을 일 자체가 없어진다.** 잘못 넣은 것을 **빼 주는 것도 같은 버튼이 한다.** **새로 `Shader.Find`를 쓰면 이름을 `RuntimeShaders`에 추가할 것** — 안 하면 그 물건만 빌드에서 검게 나오고 에디터에서는 끝까지 재현이 안 된다.

- **런타임에 투명하게 만드는 문은 `FadeMaterial` 하나다**(2026-09-02에 합쳤다 — 전에는 `BuildingRoof.MakeFadeable`과 `BuildingBlueprint.CreateBlueprintMaterial` 둘이었고 서로 달랐다). **URP는 "투명"을 색이 아니라 상태로 든다** — 표면 종류·블렌드 인자(**알파 채널 쪽까지**)·깊이 쓰기·키워드·`RenderType` 태그·렌더 큐를 같이 안 바꾸면 **알파만 낮추고 조용히 불투명**이다(식은 URP의 `BaseShaderGUI.SetupMaterialBlendModeInternal` 그대로 옮겼다). **`Material.color`를 쓰지 말 것** — 그건 `_Color`를 짚는데 **URP Lit이 읽는 것은 `_BaseColor`**라, 청사진이 알파 0.4를 넣어 놓고도 기본값(흰색·알파 1)으로 떴다. `FadeMaterial.SetColor`/`SetAlpha`가 둘 다 넣는다.
