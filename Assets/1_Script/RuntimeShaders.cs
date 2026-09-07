using UnityEngine;

// **런타임에 `Shader.Find`로 찾는 셰이더의 목록 — 답은 여기 하나다**(2026-09-02).
//
// ── 왜 이 파일이 있나 ──────────────────────────────────────────────────────
//
// **`Shader.Find`는 빌드에 포함된 셰이더만 찾는다.** 그리고 셰이더가 빌드에 들어가는 길은
// 하나뿐이다 — **어떤 재질 에셋이 그것을 가리키고, 그 재질이 씬·프리팹·`Resources`에서
// 쓰이는 것.** 코드가 이름으로 찾는 것은 **참조가 아니다.**
//
// 그래서 에디터에서는 멀쩡하고(에디터는 프로젝트의 모든 셰이더를 들고 있다) **빌드에서만
// 조용히 검게 나온다.** 실제로 그렇게 나왔다: 지하 지형이 통째로 검은 판이었고, 원인은
// `DungeonColony/Terrain/Lit`이 **아무 재질도 안 가리켜서 통째로 잘려 나간 것**이었다.
// 화면에는 *"땅바닥이 검다"*로만 보이고, 코드에는 아무 잘못이 없다.
//
// ── 그래서 두 가지를 한다 ─────────────────────────────────────────────────
//
//  ① **이름을 여기 모은다.** 에디터의 `0. Setup World Streaming`이 이 목록을 읽어
//     **Graphics Settings의 Always Included Shaders**에 넣는다 — 그것이 *"재질이 안 가리켜도
//     빌드에 넣어라"*라고 말하는 유일한 문이다.
//  ② **못 찾으면 시끄럽게 군다**(`Find`). 조용히 `null`을 돌려주면 재질이 없는 채로 물건이
//     서고, 그 화면은 *검은 판*이라 **왜 검은지가 화면에 안 적혀 있다.**
//
// **새로 `Shader.Find`를 쓰는 자리를 만들면 이름을 여기 추가할 것.** 안 하면 그 물건만
// 빌드에서 검게 나오고, 에디터에서는 끝까지 재현이 안 된다.
public static class RuntimeShaders
{
    // 지형 — 우리 사본이 먼저, 없으면 URP 원본, 그다음 빌트인.
    public const string TerrainLit    = "DungeonColony/Terrain/Lit";
    public const string UrpTerrainLit = "Universal Render Pipeline/Terrain/Lit";
    public const string NatureTerrain = "Nature/Terrain/Standard";

    // 바위 — URP Lit의 사본(가까운 벽을 낮춰 그린다). **Always Included에는 안 넣는다** —
    // Lit의 사본이라 변형이 폭발한다. 지형과 같은 문으로 들어간다: **재질 에셋이 가리킨다**
    // (`World → 0. Create Rock Materials`가 만드는 `Rock_Top/Wall/Mark.mat`).
    public const string RockLit = "DungeonColony/Rock/Lit";

    // 일반 표면
    public const string Lit      = "Universal Render Pipeline/Lit";
    public const string Unlit    = "Universal Render Pipeline/Unlit";
    public const string Standard = "Standard";

    // 선·오버레이·토큰
    //
    // **`Hidden/Internal-Colored`는 빌드에 넣을 수 없다**(2026-09-02에 겪었다). 에디터 전용
    // 에셋(`Library/unity default resources`)이고 `HideFlags.DontSave`가 붙어 있어서,
    // Always Included에 넣으면 유니티가 빌드 중에 **죽는다**(로그: *"An asset is marked with
    // HideFlags.DontSave but is included in the build"* 바로 뒤에 네이티브 크래시).
    //
    // 그래서 **`AlwaysInclude`에 없다.** 빌드에서는 `Shader.Find`가 어차피 `null`을 돌려주고
    // `Sprites/Default`로 떨어진다 — 격자선이 깊이 판정을 못 꺼서 바닥에 묻힐 뿐 사고는 아니다
    // (에디터에서만 `_ZTest`가 먹는다. 원래도 그랬다).
    public const string InternalColored = "Hidden/Internal-Colored";
    public const string SpritesDefault  = "Sprites/Default";
    public const string UnlitColor      = "Unlit/Color";
    public const string UnlitTexture    = "Unlit/Texture";

    // 코드가 런타임에 찾는 이름 **전부**. 도구가 *"우리가 넣었던 것"*을 알아보는 데 쓴다.
    public static readonly string[] All =
    {
        TerrainLit, UrpTerrainLit, NatureTerrain,
        RockLit,
        Lit, Unlit, Standard,
        InternalColored, SpritesDefault, UnlitColor, UnlitTexture,
    };

    // ── Always Included에 넣어도 되는 것만 ─────────────────────────────────
    //
    // **아무거나 넣으면 빌드가 죽는다**(2026-09-02에 두 번 겪었다 — 유니티가 버그 리포터를
    // 띄웠다). 죽는 길이 **둘**이고 서로 다르다:
    //
    //   ① **무거운 셰이더**(지형) — 변형을 전부 굽다가 죽는다.
    //   ② **에디터 전용 셰이더**(`Hidden/Internal-Colored`) — 애초에 빌드에 못 들어간다.
    //      `Library/unity default resources`에 `HideFlags.DontSave`로 들어 있어서, 넣으면
    //      *"An asset is marked with HideFlags.DontSave but is included in the build"* 뒤에
    //      **바로 네이티브 크래시**다.
    //
    // Always Included는 *"쓰이는지 안 쓰이는지 모르니 **모든 변형**을 굽고 넣어라"*라는 뜻이다.
    // 작은 셰이더에는 공짜지만 **지형 셰이더에는 재앙이다** — URP 지형은 패스가 넷이고
    // 스플랫 개수·인스턴싱·법선 옵션이 곱해져 **변형이 수만 개**다. 그걸 통째로 굽다가 죽는다.
    //
    // **지형은 다른 문으로 넣는다 — 재질 에셋이다.** 재질이 셰이더를 가리키면 그것은
    // *참조*라, 유니티가 **실제로 쓰이는 변형만** 굽는다. `0. Setup World Streaming`이
    // `TerrainChunk.mat`을 만들어 `TerrainStreamer.terrainMaterial`에 물린다 — 그러면
    // 런타임에 `Shader.Find`를 부를 일 자체가 없어진다.
    //
    // **여기 남는 것은 "작고, 아무 재질도 안 가리키는 것"뿐이다.** `URP/Lit`·`URP/Unlit`은
    // 이미 재질 에셋이 가리키고 있어 넣을 이유가 없고(넣으면 변형만 폭발한다), `Standard`는
    // URP에서 안 쓰는 폴백이다.
    // **`InternalColored`는 여기 없다** — 위 설명 참고(넣으면 빌드가 죽는다).
    public static readonly string[] AlwaysInclude =
    {
        SpritesDefault, UnlitColor, UnlitTexture,
    };

    // 앞에서부터 찾아 처음 걸리는 것. **전부 없으면 한 번 알린다** — 매 프레임 부르는
    // 자리가 아니라 재질을 만들 때 한 번 부르는 자리들이라 그대로 찍어도 된다.
    public static Shader Find(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            var sh = Shader.Find(names[i]);
            if (sh != null) return sh;
        }

        Debug.LogError($"[셰이더] {string.Join(" · ", names)} 를 하나도 못 찾았다 — " +
                       "빌드에서 잘려 나간 것이다(에디터에서는 멀쩡하다). " +
                       "`World → 0. Setup World Streaming`을 한 번 돌려 " +
                       "Always Included Shaders에 등록할 것.");
        return null;
    }
}
