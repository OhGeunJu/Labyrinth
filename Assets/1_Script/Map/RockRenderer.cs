using System.Collections.Generic;
using UnityEngine;

// **바위를 그린다 — 칸 데이터에서 청크 메시로**(2026-08-29).
//
// `FloorRenderer`와 **같은 물건**이다: 칸마다 오브젝트를 세우지 않고, 16×16 청크 하나를
// 메시 한 장으로 굽는다. 바닥에서 이미 겪었다 — 20×20 마당 하나가 GameObject 800개였고,
// 바위는 그보다 훨씬 많다(파내기 전에는 **세계가 통째로 바위**다).
//
// ── 보이는 것만 굽는다 ─────────────────────────────────────────────────────
//
// 세계 전체가 바위라도 **그릴 것은 파낸 공간의 테두리뿐**이다. 바위 속의 바위는 어느
// 면에서도 안 보인다. 그래서:
//
//   · **윗면** — 위에서 내려다보는 화면이라 이건 늘 보인다.
//   · **옆면** — **뚫린 이웃 쪽만.** 바위끼리 맞닿은 면은 두 장이 겹쳐 z-파이팅이 나고,
//     무엇보다 안 보인다.
//
// 파내면 그 칸과 **맞닿은 넷**을 다시 굽는다(옆면이 새로 드러난다). 바닥이 쓰는 규칙 그대로다.
//
// ── 지정된 칸은 서브메시로 가른다 ──────────────────────────────────────────
//
// "캘 것"으로 찍은 칸은 다르게 보여야 한다. 칸마다 표식을 세우면 다시 오브젝트가 늘어나므로,
// **같은 메시의 두 번째 서브메시**로 굽고 머티리얼만 달리 준다 — 지정이 바뀌면 그 청크만
// 다시 구우면 된다.
[DefaultExecutionOrder(-50)]
public class RockRenderer : MonoBehaviour
{
    // 청크 한 변(타일). **`FloorRenderer`와 같은 값**이어야 다시 굽는 단위가 어긋나지 않는다.
    public const int ChunkTiles = 16;

    // ── 윗면과 옆면을 가른다 (2026-08-29) ──────────────────────────────────
    //
    // 참고로 삼은 화면의 규칙이 하나다: **파낸 곳만 보이고 그 밖은 새까맣다.** 위에서
    // 내려다보면 안 판 자리는 **바위 윗면**이므로, 그 면을 검게 칠하면 그대로 그 그림이 된다.
    // 옆면만 밝게 두면 **파낸 방의 테두리가 빛을 받아** 방의 모양이 또렷해진다.
    //
    // **조명으로는 못 가른다** — 주변광을 낮추면 옆면도 같이 죽고, 방향광을 눕히면 한쪽
    // 옆면만 밝다. 면의 역할이 다르므로 **재질이 달라야 한다.**
    [Tooltip("바위 **윗면**. 비우면 벽 색의 절반(`TopShade`)인 단색으로 만든다.")]
    [SerializeField] Material rockMaterial;

    [Tooltip("바위 **옆면**(파낸 방의 벽). 비우면 밝은 청회색으로 만든다.")]
    [SerializeField] Material wallMaterial;

    [Tooltip("캘 것으로 찍은 칸. 비우면 바위 재질을 복제해 노랗게 물들인다.")]
    [SerializeField] Material markedMaterial;

    [Tooltip("한 프레임에 다시 굽는 청크 수. 드래그로 넓게 찍으면 여러 장이 한꺼번에 더러워진다.")]
    [SerializeField, Range(1, 8)] int rebuildsPerFrame = 2;

    [Tooltip("한 프레임에 버리는 청크 수. **세우는 쪽과 짝이다** — 지형 하나가 바위 열여섯 " +
             "장이라 예산이 없으면 서른두 장이 한 프레임에 몰린다. 평소 churn(달릴 때 초당 " +
             "150장 안팎)을 따라잡을 만큼은 되어야 밀리지 않는다.")]
    [SerializeField, Range(1, 32)] int dropsPerFrame = 4;

    // ── 벽 자르기 (`2_Material/RockShader/RockCut.hlsl`) ─────────────────────
    //
    // **벽이 24m가 된 값이 여기서 치러진다.** 깊이는 벌었는데 비스듬한 카메라에서 앞쪽
    // 벽이 뒤를 통째로 가리므로, **수평면 하나로 위를 잘라 낸다.**
    //
    // **반경이 아니라 평면이다**(2026-09-07, 두 번 고쳐서 여기 왔다). 카메라에서 가까울수록
    // 낮추는 쪽을 먼저 만들었는데 **어지러웠다** — 트인 자리가 카메라를 따라다니는 웅덩이라
    // 조금만 움직여도 눈앞의 벽이 오르내린다. 띠를 여섯 배로 넓혀도 그대로였다:
    // **움직이는 것 자체가 문제**였지 넓이가 아니었다.
    //
    // 평면이면 **패닝으로는 아무것도 안 변한다.** 변하는 것은 줌뿐이고 그건 일부러 하는
    // 조작이다 — 그래서 **깊이가 줌에 붙는다**: 당기면 내려다보이고 물리면 협곡이 온다.
    //
    // **값을 여기서 미는 이유**: 셰이더의 `static const`로 두면 만질 때마다 다시 굽고
    // 플레이 중에 못 본다. 전역 유니폼이면 **인스펙터에서 보면서 맞춘다**.
    [Header("벽 자르기")]
    [Tooltip("끄면 벽이 어디서나 제 높이 그대로다(가리는 대신 깊어 보인다).")]
    [SerializeField] bool cutNearWalls = true;

    // **주 손잡이 — 눈높이의 비율이다**(2026-09-07에 고정 오프셋에서 바꿨다).
    //
    // *"눈에서 몇 미터 아래"*로 두었더니 **줌에 따라 뜻이 달라졌다**: 가까이서는 바닥에
    // 눌려 아무리 키워도 더 안 내려가고, 물러나면 40을 빼도 벽이 여전히 눈앞을 막았다
    // (사용자: *"전혀 안 낮아지는데"*). 화면에서 벽이 얼마나 가리는지는 **절대 높이가
    // 아니라 내 눈높이와의 비**가 정한다 — 50m 위에서 14m 벽은 여전히 벽이다.
    //
    // 비율이면 **어느 줌에서나 같은 만큼 트인다.** 내리면 더 트이고, 올리면 벽이 남는다.
    [Range(0.02f, 0.6f)]
    [Tooltip("눈높이의 이만큼 높이에서 자른다. 내릴수록 벽이 낮아지고 멀리까지 보인다.")]
    [SerializeField] float cutFrac = 0.4f;

    // **비율로 바꾼 뒤로는 거의 안 걸린다** — 아주 가까이 당겼을 때만이다. 경계선을
    // 내리는 것은 위의 `cutFrac`이고, 이 값은 *벽이 아예 사라지지는 않게* 하는 바닥이다.
    [Tooltip("아무리 당겨도 벽은 이만큼 남는다(m). 경계선 높이와는 다른 축이다.")]
    [SerializeField] float cutLow = 2.5f;

    // 자르기가 뚜껑을 내리는 만큼 **경계를 아래로 늘린다**(m) — 근거는 `Rebuild`에 있다.
    // 자르는 면은 카메라 발밑에서 재므로 청크 바닥이 그보다 높으면 그 차이만큼 더 내려간다.
    // 지하 바닥의 기복이 그 폭이라 한 뼘이면 족하다.
    const float CutBoundsSlack = 4f;

    static readonly int IdOn       = Shader.PropertyToID("_RockCutOn");
    static readonly int IdLow      = Shader.PropertyToID("_RockCutLow");
    static readonly int IdFrac     = Shader.PropertyToID("_RockCutFrac");
    static readonly int IdFloorY   = Shader.PropertyToID("_RockCutFloorY");

    // **매 프레임 민다** — 카메라가 움직이면 바닥 높이도 따라 움직인다. 지하 바닥은 거의
    // 평지라 **한 값이면 족하다**(정점마다 제 기둥의 바닥을 들려면 스트림이 하나 더 는다).
    //
    // **끄면 `_RockCutOn`에 0을 민다** — 셰이더가 그 하나로 기능 전체를 건너뛴다.
    // 0이 곧 꺼짐이라 **아무도 안 밀었을 때도 안전하다**(에디터 씬 뷰가 그렇다).
    // ── 보이는 것을 짚는다 ────────────────────────────────────────────────────
    //
    // **벽은 잘려 그려지는데 콜라이더는 24m 그대로다**(2026-09-19, 사용자: *"안개가 생긴후부터 채굴 지정하는
    // 판정이 안좋아진 거 같아"*). 커서 레이를 그냥 쏘면 **자르고 남은 윗부분 — 화면에 없는 벽**에 먼저 맞아,
    // 눈으로 겨눈 칸보다 **카메라 쪽 칸**이 찍힌다(내려다보는 각이 60°면 몇 m씩). 콜라이더를 낮추는 길은
    // 안 된다 — 자르는 높이는 카메라를 따라 매 프레임 바뀌고, 막는 판정이 화면을 따라가면 안 된다.
    //
    // 그래서 **짚는 쪽이 자르는 면을 안다**(`VisiblePick` — 커서로 월드를 짚는 자리가 전부 거기를 지난다).
    // 높이의 답은 셰이더와 **같은 식**이다(`RockCut.hlsl` — 바닥 + max(눈높이 × `cutFrac`, `cutLow`)).
    // 여기서 다른 식을 쓰면 짚는 것과 보이는 것이 갈린다.
    public static bool CutActive { get; private set; }
    public static float CutY { get; private set; }

    void PushCutUniforms()
    {
        CutActive = false;
        // **1인칭에서는 안 자른다**(2026-10-28) — 자르기는 위에서 내려다보는 눈을 위한 것이다. 눈높이에서
        // 자르면 바닥 언저리(`cutLow`)에서 벽이 끊겨 굴 밖 허공이 보인다. 그 위는 천장이 덮는다.
        if (!cutNearWalls || FirstPersonView.On) { Shader.SetGlobalFloat(IdOn, 0f); return; }

        var cam = Camera.main;
        var map = MapManager.Instance;

        // **바닥을 모르면 자르지 않는다**(2026-09-07). 여기서 0으로 떨어뜨리면 자르는 면이
        // **실제 바닥보다 30m 아래**에 놓여 **바위가 통째로 사라진다** — 화면에는
        // *"밑으로 내려가지도 않았는데 다 투명했어"*로만 보이고, 원인이 바닥값이라는
        // 표시가 아무 데도 없다. **모르면 끄는 편이 짐작해서 자르는 것보다 낫다.**
        if (cam == null || map == null) { Shader.SetGlobalFloat(IdOn, 0f); return; }

        var camPos = cam.transform.position;
        float floorY = map.SampleGroundHeight(camPos.x, camPos.z);

        Shader.SetGlobalFloat(IdOn,       1f);
        Shader.SetGlobalFloat(IdLow,      cutLow);
        Shader.SetGlobalFloat(IdFrac,     cutFrac);
        Shader.SetGlobalFloat(IdFloorY,   floorY);

        // 짚는 쪽(`PickVisible`)이 쓸 같은 값 — 셰이더의 `RockCutY`와 한 식이다.
        CutActive = true;
        CutY      = floorY + Mathf.Max((camPos.y - floorY) * cutFrac, cutLow);

        // **숫자를 안 보면 손잡이를 짐작으로 돌리게 된다**(2026-09-07에 네 번 헛돌렸다).
        // 화면만 보고는 *벽이 안 낮아지는 것*인지 *카메라가 낮은 것*인지 가릴 수가 없다.
        if (!cutDebug) return;

        _debugAt += Time.unscaledDeltaTime;
        if (_debugAt < 1f) return;
        _debugAt = 0f;

        float eye  = camPos.y - floorY;
        float wall = Mathf.Max(eye * cutFrac, cutLow);

        // **둘은 다른 물음이라 따로 찍는다**(2026-09-07에 한 낱말로 뭉뚱그렸다가 틀렸다).
        // *"바위 속인가"*라고 라벨을 달아 놓고 실제로는 **자르고 남은 벽**과 견주고 있었고,
        // 그래서 24m 기둥 한복판에 있는 카메라를 **`바깥`**이라고 답했다 — 그 거짓말 하나를
        // 근거로 등진 면 버리기를 지웠다(사용자: *"아니 지금 바위 속에 있는거야"*).
        //
        //   · **바위 속**   = 지도가 아는 진짜 바위. 통행·채굴이 보는 것과 같은 답이다.
        //   · **그린 벽 속** = 지금 그려지는 것 속. **등진 면이 보일 수 있는 조건**이라
        //                     셰이더 이야기를 할 때 봐야 하는 쪽은 이것이다.
        //
        // **이름이 묻는 것과 재는 것이 같아야 한다.**
        bool column   = RockMap.IsRock(camPos);
        bool inRock   = column && eye < RockMap.Height;
        bool inDrawn  = column && eye < wall;

        Debug.Log($"[바위 컷] 눈높이 {eye:F1}m · 벽 {wall:F1}m · " +
                  $"{(inRock ? "바위 속" : "바위 밖")} · " +
                  $"{(inDrawn ? "그린 벽 속" : "그린 벽 위")} · " +
                  $"카메라 Y {camPos.y:F1} · 바닥 Y {floorY:F1} · " +
                  $"시점각 {cam.transform.eulerAngles.x:F0}도");
    }

    [Tooltip("1초마다 눈높이·벽 높이·카메라가 바위 속인지를 로그로 찍는다. 진단·값 맞춤용.")]
    [SerializeField] bool cutDebug;

    float _debugAt;

    [Layer]
    [Tooltip("바위가 놓일 레이어. **`Ground`여야 한다** — 커서 레이캐스트가 바위를 짚어야 " +
             "캘 자리를 고를 수 있다.")]
    [SerializeField] int rockLayer = 3;

    class Chunk
    {
        public GameObject   Go;
        public Mesh         Mesh;
        public MeshRenderer Renderer;

        // **박스로 막는다 — 메시가 아니다**(2026-08-30). 근거는 `FitBoxes` 위에 적었다.
        public readonly List<BoxCollider> Boxes = new();

        // 이 청크 벽에 붙은 조명(칸 → 선 것). **청크의 자식이다** — 청크를 버리면 같이 간다.
        public readonly Dictionary<Vector2Int, PlacedLight> Lights = new();

        // 천장(`BuildCeiling`) — 판 안에서만 선다. 1인칭일 때만 그린다.
        public Mesh         CeilMesh;
        public MeshRenderer CeilRenderer;
    }

    struct PlacedLight
    {
        public GameObject    Go;
        public int           Face;
        public WallLightData Data;
    }

    struct WantedLight
    {
        public Vector2Int    Tile;
        public int           Face;
        public WallLightData Data;
        public GameObject    Prefab;
        public Vector3       Pos;
        public Quaternion    Rot;
    }

    readonly Dictionary<Vector2Int, Chunk> _chunks = new();
    readonly HashSet<Vector2Int>           _dirty  = new();

    // **버리는 것도 나눠서 한다**(2026-09-12). 지형 청크(64m) 하나가 바위 청크(16m) **열여섯
    // 장**이라, 지형이 둘 내려가는 프레임에 **서른두 장이 한꺼번에** 파괴됐다 — 세우는 쪽은
    // `rebuildsPerFrame`이 나눠 주는데(측정: 최악 프레임에도 **두 장**) **버리는 쪽만 예산이
    // 없었다.** 좋은 기계에서는 안 보이지만 **몇 배 느린 기계에서는 그 프레임만 통째로 튄다.**
    //
    // **미뤄도 되는 이유**: 내려가는 청크는 이미 **화면 밖**이다(지형이 먼저 내려갔다).
    // 몇 프레임 더 서 있어도 아무도 안 본다.
    readonly HashSet<Vector2Int> _doomed = new();

    readonly List<Vector3> _verts = new();
    readonly List<Vector3> _norms = new();
    readonly List<Vector2> _uvs   = new();
    readonly List<int>     _top   = new();   // 윗면 — 벽과 같은 무늬, 밝기는 절반(`TopShade`)
    readonly List<int>     _mark  = new();   // 캘 것으로 찍힌 칸

    readonly List<int>     _side  = new();   // 옆면 — 밝다

    // ── 벽에 박힌 광맥 (2026-09-14) ────────────────────────────────────────
    //
    // 광맥은 **여태 안 보였다.** `RockMap.VeinAt`이 바이옴별로 덩어리째 심어 두는데 그리는
    // 쪽이 한 번도 안 물어서, 어디에 무엇이 박혔는지는 **벽을 하나씩 눌러 살펴봐야만** 알 수
    // 있었다 — 그러면 *어디를 팔까*가 판단이 아니라 뽑기다.
    //
    // **드러난 옆면에만 그린다.** 윗면은 *안 판 자리 = 어둠*이라 거기 알갱이를 얹으면
    // 세계의 경계가 흐려지고, 무엇보다 **아직 안 판 바위 속이 통째로 들여다보인다**(광맥을
    // 따라 파는 일이 사라진다). 파낸 방의 벽에만 보이면 **판 만큼 드러난다.**
    //
    // **서브메시를 광맥 종류마다 하나씩** 붙인다. 색이 종류마다 다른데
    // `MaterialPropertyBlock`은 **렌더러 단위**라(바이옴 색조가 그걸 쓴다) 한 청크에 두
    // 종류가 박히면 한 색으로 눌린다. 청크가 16m이고 광맥 덩어리가 22m라 **한 청크에
    // 대개 한 종류**이므로 늘어나는 드로콜은 한둘이다.
    readonly List<ResourceNodeData> _oreKinds = new();
    readonly List<List<int>>        _oreTris  = new();
    static   readonly List<Material> _mats    = new();

    // **천장을 둔다** — 넘치면 그 종류는 안 그린다(`WideGraph`의 덩어리 천장과 같은 규칙:
    // 이어 붙이면 *없는 것을 있다고* 하게 되므로 버리는 쪽이 낫다). 22m 덩어리가 16m 청크에
    // 넷이나 겹치는 일은 없다.
    const int MaxOreKinds = 4;

    // 알갱이를 어디에 몇 개 박나.
    //
    // **높이는 벽이 보이는 만큼 퍼뜨린다**(2026-09-15, 사용자: *"너무 밑에만 딸랑 있다"*).
    // 전에는 바닥에서 2.2m까지만 박았다 — `RockCut`의 **바닥값**(`cutLow` 2.5m)에 맞춘 것인데,
    // 그 값은 *끝까지 당겼을 때*이고 **평소에는 벽이 눈높이 × 0.4(8~16m)까지 보인다.** 가장
    // 드문 경우에 맞춰서 흔한 경우에 밑바닥에만 몰렸다. 이제 위로 퍼뜨린다(위쪽 것은
    // 가까이 당겼을 때 벽과 같이 잘릴 뿐 틀린 것이 아니다).
    //
    // **덩어리 수도 면마다 굴린다** — 전부 같으면 벽을 따라 같은 간격으로 늘어선다.
    //
    // **절반으로 줄이고 그만큼 위로 폈다**(2026-09-15, 사용자: *"너무 오밀조밀 붙어 있다 —
    // 절반은 줄어도 알아보는 데 문제없다, 줄인 걸 위로 올려라"*). 면당 2~3 → **1~2** 덩어리,
    // 높이 6 → **10m**, 아래로 몰림 1.5 → **1.15**. 광맥은 *여기에 있다*만 읽히면 되고, 빽빽하면
    // 벽이 아니라 **광석 판**으로 보인다. 값도 그만큼 줄었다(면당 꼭짓점 42~63 → 21~42).
    //
    // **높이를 칸으로 나눠 고르게 편다**(2026-09-29, 사용자: *"너무 밑에만 붙어 있어서 위는 조금 비어 보이지 않아?"*).
    // 높이를 덩어리마다 따로 굴리면 우연히 둘 다 아래에 떨어지는 면이 많다 — 아래로 몰림(1.15)까지 얹혀
    // 벽의 윗부분이 비었다. 이제 띠를 덩어리 수만큼 **층으로 나눠 층마다 하나씩** 둔다(층 안의 자리만 굴린다).
    // 덩어리는 면당 1~2 → **1~3**(평균 1.5 → 2)만 늘렸다 — 09-15의 *"오밀조밀하다"*를 다시 밟지 않게,
    // 개수보다 **퍼짐**으로 채운다.
    //
    // **벽 꼭대기까지 박는다**(같은 날, 사용자: *"그냥 맨 위까지 광맥이 박히게 하면 용량에 뭐 문제가 생기나?"*).
    // 14m로 두면 물러나 벽이 통째로 보일 때 위 3분의 1이 비었다. 촘촘함은 14m일 때(1~3 = 1m에 0.14덩어리)
    // 그대로 두고 높이만 편다 → 2~5. 값은 광맥 면 하나에 꼭짓점 70개 남짓이라 청크 한 장(수천)에 비하면 작다.
    // 자르는 면 위의 것은 그려지지 않을 뿐이다(`RockCut`).
    const int   OreClustersMin = 2;    // 면 하나에 덩어리 몇 — 이 사이에서
    const int   OreClustersMax = 5;
    const int   OreNuggets  = 3;       // 덩어리 하나에 알갱이 몇
    const int   OreRim      = 6;       // 알갱이 둘레 꼭짓점
    const float OreBand     = RockMap.Height - 0.6f;   // 바닥에서 이 높이까지 — 꼭대기 한 뼘 아래
    const float OreOut      = 0.03f;   // 벽면에서 이만큼 띄운다(z-파이팅)

    Transform _root;
    Material  _topMat, _sideMat, _markMat;

    [Header("캐면 나오는 것")]
    [Tooltip("광맥이 없는 **맨바위**를 캤을 때. `5_Data/Resource/Stone.asset`을 물릴 것 — " +
             "나오는 것·개수·걸리는 시간이 다 그 안에 있다. **비우면 아무것도 안 나온다.**")]
    [SerializeField] ResourceNodeData plainRock;

    [Header("시작 자리")]
    [Tooltip("판이 열릴 때 내 폰 둘레를 이만큼(m) 파 둔다. **0으로 두면 폰이 바위 속에 갇힌다** — " +
             "걸을 수도, 핵을 놓을 수도, 캘 자리를 고를 수도 없다.")]
    [SerializeField, Range(0f, 40f)] float startClearing = 12f;

    [Tooltip("시작 공터에서 **가장 가까운 자연 굴까지** 길을 뚫는다(m). 굴이 그물로 나 있어도 " +
             "공터가 바위 한복판에 떨어지면 첫 화면이 여전히 웅덩이 하나라, 어느 쪽에 길이 " +
             "있는지를 알 방법이 없다. **0이면 안 뚫는다.**")]
    [SerializeField, Range(0f, 200f)] float pathToCave = 90f;

    [Tooltip("그 길의 폭(m). **좁게 둔다** — 넓으면 저작한 굴과 구별이 안 되고, 시작부터 " +
             "파낼 것이 줄어든다.")]
    [SerializeField, Range(1f, 8f)] float pathWidth = 3f;

    // **판이 열릴 때 내 폰 둘레를 연다.** `Start`에서 한다 — `OnEnable`은 폰이 아직 다 서기
    // 전일 수 있고(실행 순서), 그러면 아무도 못 찾아 **아무 데도 안 열린다.**
    void Start()
    {
        // **바위를 그리는 것은 한 벌이어야 한다**(2026-09-18). 둘이면 각자 `_chunks`와
        // `RockChunks` 루트를 들고 **같은 이름의 청크를 따로 세운다** — 한쪽이 파낸 것을
        // 모르면 그쪽 콜라이더가 그대로 남아 **보이지 않는 벽**이 된다.
        var renderers = FindObjectsByType<RockRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (renderers.Length > 1)
            Debug.LogError($"[바위] 씬에 `RockRenderer`가 {renderers.Length}개다 — 청크가 두 벌로 서고 "
                         + "한쪽의 낡은 콜라이더가 길을 막는다. 하나만 남길 것.");

        // **세계의 끝을 보여 주는 막**(2026-09-15). 바위를 그리는 쪽이 세운다 — 기반암이
        // 바위의 일부라 *"바위가 있는 씬"*과 *"막이 있는 씬"*이 갈리면 안 된다. **없으면
        // 만든다**(씬 배선을 늘리면 언젠가 한 씬만 빠진다).
        if (RockMap.Enabled && Application.isPlaying && GetComponent<WorldBorderVeil>() == null)
            gameObject.AddComponent<WorldBorderVeil>();

        // **전장의 안개도 같은 자리에서**(2026-09-15) — 굴이 있는 씬이 곧 안개가 있는 씬이다.
        // 값을 인스펙터에서 맞추려면 `0. Setup World Streaming`이 씬에 붙여 둔다(그러면 여기서는 안 만든다).
        if (RockMap.Enabled && Application.isPlaying && GetComponent<FogOfWar>() == null)
            gameObject.AddComponent<FogOfWar>();

        // **층 통로도 판이 열릴 때 선다**(2026-10-01, `FloorPassage`) — 둘레 바위를 여는 문이 시작 공터와 같다.
        if (RockMap.Enabled && Application.isPlaying) FloorPassage.Build();

        if (!RockMap.Enabled || startClearing <= 0f) return;

        int opened = 0;

        // **시작 자리 표식이 먼저다**(`StartSpot`, 2026-09-20) — 캐릭터를 만드는 판에서는 판이 열릴 때 씬에
        // 폰이 없다(만든 폰은 화면을 닫은 뒤에 선다). 폰을 기다리면 첫 화면이 통짜 바위다.
        var spot = StartSpot.Current != null ? StartSpot.Current
                 : FindFirstObjectByType<StartSpot>();
        if (spot != null)
        {
            RockMap.OpenAround(spot.transform.position, startClearing);
            if (pathToCave > 0f) RockMap.OpenPathToCave(spot.transform.position, pathToCave, pathWidth);
            opened++;
        }

        var all = UnitBrain.All;
        for (int i = 0; i < all.Count; i++)
        {
            var brain = all[i];
            var f = brain.GetComponent<Faction>();
            if (f == null || !f.IsPlayer) continue;

            RockMap.OpenAround(brain.transform.position, startClearing);

            // **첫 한 자리만 굴에 잇는다** — 폰마다 이으면 진입로가 여럿이라 어디로 가야
            // 할지가 다시 흐려지고, 무엇보다 시작 공터끼리 이어져 마당이 통째로 열린다.
            if (opened == 0 && pathToCave > 0f)
                RockMap.OpenPathToCave(brain.transform.position, pathToCave, pathWidth);

            opened++;
        }

        // **아무도 못 찾으면 카메라 앞을 연다** — 폰이 나중에 오는 판(이벤트로 합류)에서도
        // 최소한 볼 것과 누를 것이 있어야 한다.
        if (opened == 0)
        {
            var p = ViewerXZ();
            RockMap.OpenAround(p, startClearing);
            if (pathToCave > 0f) RockMap.OpenPathToCave(p, pathToCave, pathWidth);
        }
    }

    Vector3 ViewerXZ()
    {
        var t = Camera.main != null ? Camera.main.transform : transform;
        var p = t.position;

        // 카메라는 비스듬히 내려다보므로 **제 자리가 아니라 보고 있는 지면**을 연다.
        var map = MapManager.Instance;
        float y = map != null ? map.SampleGroundHeight(p) : 0f;
        float drop = Mathf.Max(0f, p.y - y);
        return p + t.forward * drop;
    }

    void OnEnable()
    {
        // **씬이 든 것을 static으로 밀어 넣는다** — `RockMap`은 정적이라 스스로 저작할 수
        // 없고, `MineJob`은 컴포넌트가 아니라 씬을 못 본다(`TerrainStreamer`가 지형 프로필을
        // 넘기는 것과 같은 자리).
        if (plainRock != null) RockMap.PlainRock = plainRock;
        else Debug.LogWarning("[바위] `Plain Rock`이 비어 있다 — 맨바위를 캐도 아무것도 안 나온다. " +
                              "`DungeonColony → World → 0. Create Rock Materials`가 물려 준다.");

        RockMap.OnChanged        += OnRockChanged;
        // **바닥이 바뀌면 벽도 다시 굽는다**(2026-09-24) — 벽은 바닥 위에 서고(`SurfaceOf`)
        // 옆면은 **이웃 바닥이 낮으면 거기까지 내려 세운다.** 안 들으면 땅을 고른 자리에서
        // **벽 밑이 뚫려 보인다.** 처리는 `OnRockChanged`와 같다(칸과 이웃 넷의 청크).
        TerrainHeight.OnHeightChanged += OnRockChanged;
        TerrainStreamer.ChunkLoaded   += OnTerrainChunk;
        TerrainStreamer.ChunkUnloaded += OnTerrainGone;
        FirstPersonView.Changed       += OnFirstPerson;
        HoleChanged                   += OnHoleChanged;

        foreach (var c in TerrainStreamer.LiveChunks) OnTerrainChunk(c);
    }

    void OnDisable()
    {
        RockMap.OnChanged        -= OnRockChanged;
        TerrainHeight.OnHeightChanged -= OnRockChanged;
        TerrainStreamer.ChunkLoaded   -= OnTerrainChunk;
        TerrainStreamer.ChunkUnloaded -= OnTerrainGone;
        FirstPersonView.Changed       -= OnFirstPerson;
        HoleChanged                   -= OnHoleChanged;

        _doomed.Clear();   // 떠날 때는 예산이 없다 — 아래에서 전부 버린다
        foreach (var c in new List<Vector2Int>(_chunks.Keys)) Drop(c);
        if (_root != null) Toss(_root.gameObject);

        // **런타임에 만든 재질을 버린다** — 인스펙터에 물린 것은 에셋이라 안 건드리고,
        // 비어서 여기가 만든 것만 거둔다. 안 버리면 씬을 다시 열 때마다 쌓인다
        // (`TerrainStreamer`가 `TerrainData`에 대해 지키는 규칙과 같다).
        if (_topMat  != null) { Toss(_topMat);  _topMat  = null; }
        if (_sideMat != null) { Toss(_sideMat); _sideMat = null; }
        if (_markMat != null) { Toss(_markMat); _markMat = null; }


    }

#if UNITY_EDITOR
    // **씬 뷰에서도 벽을 세운다**(2026-09-12) — 에디터 지형(`WorldSceneRock`)이 청크를
    // 세울 때 여기를 두드린다. **런타임과 같은 문이다** — 따로 그리면 *에디터에서 보이는
    // 벽*과 *게임에서 막는 벽*이 갈린다.
    //
    // 그릴 것을 밀어 넣는 일까지만 한다 — 실제로 굽는 것은 `LateUpdate`가 아니라
    // 부르는 쪽이 `EditorFlush`로 돌린다(에디터에서는 프레임이 안 돈다).
    public void EditorShow(Vector2Int terrainChunk) => OnTerrainChunk(terrainChunk);
    public void EditorHide(Vector2Int terrainChunk) => OnTerrainGone(terrainChunk);

    // 예약된 청크를 실제로 굽는다 — **에디터에는 프레임이 없어서** `LateUpdate`가 안 돈다.
    // 돌려주는 것은 *남은 것*이라 부르는 쪽이 계속 부를지 정한다.
    //
    // **자르기 유니폼은 안 민다**(`PushCutUniforms`) — 그건 게임 카메라 높이로 벽 윗부분을
    // 깎는 장치인데, 씬 뷰는 제 카메라로 보므로 엉뚱한 높이에서 잘린다. 저작할 때는
    // **안 잘린 24m 벽**을 보는 것이 맞다(무엇이 막혀 있는지가 그것이다).
    public int EditorFlush(int budget)
    {
        while (budget-- > 0 && _dirty.Count > 0)
        {
            Vector2Int chunk = default;
            foreach (var c in _dirty) { chunk = c; break; }
            _dirty.Remove(chunk);
            Rebuild(chunk);
        }
        return _dirty.Count;
    }

    public void EditorClear()
    {
        foreach (var c in new List<Vector2Int>(_chunks.Keys)) Drop(c);
        ReapStrayRoots(null);   // **`_chunks`가 비어 있어도 씬에 남은 것을 거둔다**(아래 참고)
        _dirty.Clear();
        _doomed.Clear();
        if (_root != null) { Toss(_root.gameObject); _root = null; ChunkRoot = null; }
    }
#endif

    // **지형 청크를 따라간다.** 바위는 세계 전체에 있으므로 반경을 여기서 다시 재면
    // `TerrainStreamer`와 답이 어긋나 **땅은 있는데 벽이 없는** 칸이 생긴다.
    void OnTerrainChunk(Vector2Int terrainChunk)
    {
        if (!RockMap.Enabled) return;

        // 지형 청크(256m)는 바위 청크(16m) 열여섯 장 × 열여섯 줄이다.
        int per = TerrainGrid.ChunkSize / ChunkTiles;
        var o   = TerrainGrid.OriginOf(terrainChunk);
        var map = MapManager.Instance;
        var baseTile = map != null ? map.WorldToTile(o) : new Vector2Int((int)o.x, (int)o.z);

        for (int y = 0; y < per; y++)
        for (int x = 0; x < per; x++)
        {
            var c = new Vector2Int(baseTile.x / ChunkTiles + x, baseTile.y / ChunkTiles + y);
            _live.Add(c);
            _dirty.Add(c);
            _doomed.Remove(c);   // **되돌아왔으면 안 버린다** — 아직 서 있으니 굽기만 하면 된다
        }
    }

    void OnTerrainGone(Vector2Int terrainChunk)
    {
        int per = TerrainGrid.ChunkSize / ChunkTiles;
        var o   = TerrainGrid.OriginOf(terrainChunk);
        var map = MapManager.Instance;
        var baseTile = map != null ? map.WorldToTile(o) : new Vector2Int((int)o.x, (int)o.z);

        for (int y = 0; y < per; y++)
        for (int x = 0; x < per; x++)
        {
            var c = new Vector2Int(baseTile.x / ChunkTiles + x, baseTile.y / ChunkTiles + y);
            _live.Remove(c);
            _dirty.Remove(c);      // **예약도 거둔다** — 안 거두면 내린 청크가 다음 프레임에 되살아난다
            _doomed.Add(c);        // 버리는 것도 예산을 탄다(`LateUpdate`)
        }
    }

    // **지형이 서 있는 자리만 그린다**(2026-08-30). 바위 청크가 있는 자리를 정하는 것은
    // `OnTerrainChunk` 하나여야 하는데, 아래 `OnRockChanged`가 그걸 건너뛰고 **아무 칸이나
    // 더럽힐 수 있었다.**
    //
    // 증상: *"처음에는 안 보이는데 땅 없는 곳에 채굴을 대 보면 벽이 갑자기 계속 생긴다"*
    // (사용자). 채굴 지정은 미리보기로 칸을 **찍었다 무르며** `OnChanged`를 터뜨리는데,
    // 그 칸이 **아직 지형이 안 선 자리**여도 그대로 구워졌다 — 그래서 **땅도 없는 허공에
    // 벽만 서고**, 커서를 쓸면 그 벽이 계속 늘어난다.
    //
    // 드래그 박스는 콜라이더가 아니라 **평면**과 교차해 칸을 모으므로(귀퉁이가 허공이어도
    // 답이 나와야 한다) 지형 밖까지 닿는다 — 거기서 막지 말고 **그리는 쪽에서** 막는다.
    readonly HashSet<Vector2Int> _live = new();

    // **맞닿은 넷까지 더럽힌다** — 파내면 이웃 바위의 옆면이 새로 드러난다.
    void OnRockChanged(Vector2Int tile)
    {
        Dirty(ChunkOf(tile));
        Dirty(ChunkOf(tile + Vector2Int.right));
        Dirty(ChunkOf(tile + Vector2Int.left));
        Dirty(ChunkOf(tile + Vector2Int.up));
        Dirty(ChunkOf(tile + Vector2Int.down));
    }

    void Dirty(Vector2Int chunk)
    {
        if (_live.Contains(chunk)) _dirty.Add(chunk);
    }

    static Vector2Int ChunkOf(Vector2Int tile) => new(
        Mathf.FloorToInt(tile.x / (float)ChunkTiles),
        Mathf.FloorToInt(tile.y / (float)ChunkTiles));

    void LateUpdate()
    {
        PushCutUniforms();
        ShadowBudget.Tick(Camera.main);

        // **버리는 것이 먼저다** — 굽는 것보다 싸고, 안 쓰는 것을 들고 있을 이유가 없다.
        int reap = dropsPerFrame;
        while (reap-- > 0 && _doomed.Count > 0)
        {
            Vector2Int gone = default;
            foreach (var c in _doomed) { gone = c; break; }
            _doomed.Remove(gone);
            Drop(gone);
        }

        int budget = rebuildsPerFrame;
        while (budget-- > 0 && _dirty.Count > 0)
        {
            Vector2Int chunk = default;
            foreach (var c in _dirty) { chunk = c; break; }
            _dirty.Remove(chunk);
            Rebuild(chunk);
        }

    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    void Rebuild(Vector2Int chunk)
    {
        _verts.Clear(); _norms.Clear(); _uvs.Clear(); _top.Clear(); _side.Clear(); _mark.Clear();
        _oreKinds.Clear();
        _wantLights.Clear();
        _liftAt.Clear();

        var map = MapManager.Instance;
        int x0 = chunk.x * ChunkTiles, y0 = chunk.y * ChunkTiles;
        BeginWobble(map);

        // **도는 김에 콜라이더 쪽이 쓸 것까지 채운다**(2026-09-02). `FitBoxes`가 같은 256칸을
        // 한 번 더 돌며 `IsRock`과 `SurfaceHeight`를 **다시** 물었었다 — 답이 안 바뀌는 물음을
        // 청크 한 장마다 두 벌 낸 셈이다. 여기서 채워 넘기면 그 한 바퀴가 통째로 사라진다.
        float lowest = float.MaxValue, highest = float.MinValue;

        for (int ty = 0; ty < ChunkTiles; ty++)
        for (int tx = 0; tx < ChunkTiles; tx++)
        {
            var tile = new Vector2Int(x0 + tx, y0 + ty);

            bool solid = RockMap.IsRock(tile);
            _solid[tx, ty] = solid;
            _taken[tx, ty] = false;
            if (!solid) continue;

            // **찍힌 칸은 윗면·옆면을 가리지 않고 한 벌로 간다** — 찍혔다는 것이 그 칸의
            // 전부이므로, 거기서 또 갈라 봐야 색만 늘고 읽히지는 않는다.
            bool marked = RockMap.IsMarked(tile);

            // **기반암은 보통 바위와 똑같이 그린다**(2026-09-15에 따로 칠했다가 되돌렸다 — 사용자:
            // *"너무 쨍하다"*). 세계의 끝을 알리는 것은 벽의 색이 아니라 그 위 허공의 막이다
            // (`WorldBorderVeil`). **광맥은 안 박는다** — 못 캐는 벽에 광석이 보이면 거짓말이다.
            bool border = RockMap.IsBedrock(tile);

            float floor = RockMap.SurfaceOf(tile);   // **`MapManager` 없이도 답한다**(씬 뷰)
            float top   = floor + RockMap.Height;

            if (floor < lowest)  lowest  = floor;
            if (floor > highest) highest = floor;

            // 칸의 네 모서리(월드). **격자 위상을 얹는다** — 안 얹으면 벽이 반 칸 어긋난다.
            Vector3 c = map != null ? map.TileToWorld(tile)
                                    : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            float xa = c.x - 0.5f, xb = c.x + 0.5f;
            float za = c.z - 0.5f, zb = c.z + 0.5f;

            // 윗면 — 늘 그린다. **벽선에 닿은 칸은 넷으로 쪼갠다** — 벽선의 가운데 점이 흔들리므로(`Wobble`)
            // 뚜껑 가장자리도 그 점을 가져야 벽과 틈 없이 맞는다. 사방이 바위인 칸은 한 장이다.
            var capL = marked ? _mark : _top;
            if (TouchesOpen(tile))
            {
                float xm = c.x, zm = c.z;
                Quad(capL, new Vector3(xa, top, za), new Vector3(xa, top, zm), new Vector3(xm, top, zm), new Vector3(xm, top, za), Vector3.up);
                Quad(capL, new Vector3(xm, top, za), new Vector3(xm, top, zm), new Vector3(xb, top, zm), new Vector3(xb, top, za), Vector3.up);
                Quad(capL, new Vector3(xa, top, zm), new Vector3(xa, top, zb), new Vector3(xm, top, zb), new Vector3(xm, top, zm), Vector3.up);
                Quad(capL, new Vector3(xm, top, zm), new Vector3(xm, top, zb), new Vector3(xb, top, zb), new Vector3(xb, top, zm), Vector3.up);
            }
            else
                Quad(capL,
                     new Vector3(xa, top, za), new Vector3(xa, top, zb),
                     new Vector3(xb, top, zb), new Vector3(xb, top, za), Vector3.up);

            // 옆면 — **뚫린 이웃 쪽만.** 바위끼리 물린 사이의 **단차 띠는 뚜껑 쪽에 넣는다**
            // (아래 `Side` 참고 — 재질이 갈려 있어서 목록을 잘못 고르면 그 띠만 밝게 뜬다).
            var sideList = marked ? _mark : _side;
            var capList  = marked ? _mark : _top;
            bool wL = Side(sideList, capList, tile + Vector2Int.left,  xa, za, xa, zb, floor, top, Vector3.left);
            bool wR = Side(sideList, capList, tile + Vector2Int.right, xb, zb, xb, za, floor, top, Vector3.right);
            bool wD = Side(sideList, capList, tile + Vector2Int.down,  xb, za, xa, za, floor, top, Vector3.back);
            bool wU = Side(sideList, capList, tile + Vector2Int.up,    xa, zb, xb, zb, floor, top, Vector3.forward);

            // **조명은 찍힌 칸에도 선다** — 광맥 알갱이와 다르다: 알갱이는 *무엇이 박혔나*를 알리는
            // 표시라 *"여기를 판다"*와 겹치면 읽히지 않지만, 불빛은 **표시가 아니라 물건**이다.
            // 찍었다고 꺼지면 굴이 한 칸씩 어두워진다.
            if (!border && (wL || wR || wD || wU))
                CollectWallLight(map, tile, wL, wR, wD, wU, xa, xb, za, zb, floor);

            // **광맥은 드러난 칸에만 박힌다** — 옆면과, 그 칸의 **윗면**(2026-09-28, 아래 `TopSpecks`).
            // 찍힌 칸에는 안 그린다 — *"여기를 판다"*가
            // 그 칸의 전부라, 거기서 또 갈라 봐야 색만 늘고 읽히지는 않는다(윗면·옆면을
            // 한 벌로 보내는 것과 같은 규칙).
            if (border || marked || !(wL || wR || wD || wU)) continue;

            var vein = RockMap.VeinAt(tile);
            if (vein == null || !vein.IsVein) continue;

            var ore = OreList(vein);
            if (ore == null) continue;                      // 종류가 천장을 넘었다

            if (wL) Specks(ore, tile, 0, xa, za, xa, zb, floor, Vector3.left);
            if (wR) Specks(ore, tile, 1, xb, zb, xb, za, floor, Vector3.right);
            if (wD) Specks(ore, tile, 2, xb, za, xa, za, floor, Vector3.back);
            if (wU) Specks(ore, tile, 3, xa, zb, xb, zb, floor, Vector3.forward);
            TopSpecks(ore, tile, xa, za, top);
        }

        // **판 안에서는 다 판 청크도 남긴다** — 바위는 없어도 천장은 있다(`BuildCeiling`). 큰 방을 파면 흔하다.
        if (_top.Count == 0 && _side.Count == 0 && _mark.Count == 0 && !Application.isPlaying) { Drop(chunk); return; }

        var ch = Ensure(chunk);
        var mesh = ch.Mesh;
        mesh.Clear();
        mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetUVs(0, _uvs);
        SetLift(mesh);
        mesh.subMeshCount = 3 + _oreKinds.Count;
        mesh.SetTriangles(_top,  0);
        mesh.SetTriangles(_side, 1);
        mesh.SetTriangles(_mark, 2);
        for (int k = 0; k < _oreKinds.Count; k++) mesh.SetTriangles(_oreTris[k], 3 + k);
        mesh.RecalculateBounds();

        // **경계는 자르기 전이 아니라 자른 뒤를 덮어야 한다**(2026-09-08).
        //
        // `RecalculateBounds`는 **굽힌 정점**을 재는데, 화면에 그려지는 자리는 `RockCut`이
        // **뚜껑을 눈높이까지 내린 뒤**다. 통짜 바위 청크는 **드러난 면이 윗면뿐**이라
        // (옆면은 뚫린 이웃 쪽만 굽는데 사방이 바위다) 경계가 24m 높이의 **납작한 판**인데
        // 실제로 그려지는 것은 2.5~10m 언저리다 — **가까이 당기면 그 판이 절두체 위로
        // 빠져 유니티가 청크를 통째로 컬링한다.**
        //
        // 화면에는 *"가까이 가면 가운데가 뻥 비어 보인다"*로만 나타난다. **옆면이 하나라도
        // 있는 청크는 경계가 이미 바닥까지 닿아 멀쩡하므로** 통짜 바위만 사라지고, 그래서
        // 원인이 셰이더나 메시가 아니라 **경계**에 있다는 표시가 아무 데도 없다.
        //
        // **바닥까지 늘린다** — 옆면이 있는 청크가 이미 갖는 모양이고, 자르기는 **내리기만
        // 하므로** 그 아래로는 안 간다. 자르는 면의 기준은 **카메라 발밑**이라 이 청크가
        // 그보다 높으면 조금 더 내려갈 수 있어 여유를 한 뼘 둔다.
        if (lowest <= highest)   // 바위가 하나도 없으면 늘릴 바닥도 없다(천장만 있는 청크)
        {
            var b = mesh.bounds;
            b.SetMinMax(new Vector3(b.min.x, lowest - CutBoundsSlack, b.min.z), b.max);
            mesh.bounds = b;
        }

        // **재질은 청크마다 다시 맞춘다** — 광맥 서브메시가 늘고 줄기 때문이다(한 칸을
        // 파내면 드러나는 벽이 바뀌어 굽기마다 종류가 달라질 수 있다). 개수가 어긋나면
        // 유니티가 남는 서브메시를 **첫 재질로** 그려 **광맥이 바위색 알갱이**가 된다.
        _mats.Clear();
        _mats.Add(TopMat()); _mats.Add(SideMat()); _mats.Add(MarkMat());
        for (int k = 0; k < _oreKinds.Count; k++) _mats.Add(OreMat(_oreKinds[k]));
        ch.Renderer.SetSharedMaterials(_mats);

        // **색조는 재질을 갈아 끼운 뒤에 얹는다** — `MaterialPropertyBlock`은 슬롯에 붙는데
        // 슬롯 수가 방금 바뀌었다.
        TintForBiome(ch.Renderer, chunk);

        // **콜라이더를 단다** — 커서가 바위를 짚어야 캘 자리를 고를 수 있고, 몸도 막아야 한다.
        // (천장에 안 다는 것과 정반대다 — 저쪽은 위에 있어서 짚으면 안 되고, 이쪽은 짚어야 한다.)
        FitBoxes(ch, chunk, lowest, highest);

        SyncWallLights(ch);

        BuildCeiling(ch, chunk, map);
    }

    // ── 천장 (2026-10-28) ───────────────────────────────────────────────────
    //
    // 사용자: *"천장을 보이게 하자는 건데 1인칭 카메라를 만들어서"*. 굴은 위에서 내려다보는 화면을 위해 지어서
    // 천장이 없었다 — 벽 위가 그냥 뚫려 있다. 눈높이(`FirstPersonView`)에서 고개를 들면 그 허공이 보인다.
    //
    // **따로 굽고 따로 그린다 — 바위 메시에 안 넣는다.** 바위 셰이더는 *뚜껑*(수평면)을 자르는 높이까지
    // 끌어내린다(`RockCutWS`). 천장을 같은 메시에 넣으면 위에서 볼 때 눈앞까지 내려와 화면을 덮는다.
    // 그래서 청크의 자식 렌더러로 두고 **1인칭일 때만 켠다**(그때는 자르기도 꺼진다). 위에서 볼 때 값은 0이다.
    //
    // **높이는 빈칸마다 하나** — *제 바닥 + 벽 높이*와 *둘레 여덟 바위의 꼭대기* 중 낮은 쪽. 바닥이 계단이라
    // 이웃 벽 꼭대기가 1m씩 다른데, 천장이 그보다 높으면 벽과 천장 사이로 허공이 샌다. 낮게 두면 벽이 천장을
    // 뚫고 올라갈 뿐이고 그 윗부분은 아래에서 안 보인다. 천장끼리 높이가 다른 자리는 세로 띠로 잇는다.
    //
    // **벽선의 흔들림을 같이 탄다** — 칸을 넷으로 쪼개 반 칸 격자 점을 쓰고 `Quad`로 굽는다. 벽 꼭대기의
    // 점과 같은 자리라 같은 만큼 움직인다(`Wobble`은 자리의 함수다).
    readonly List<int> _ceil = new();

    void BuildCeiling(Chunk ch, Vector2Int chunk, MapManager map)
    {
        if (ch.CeilMesh == null) return;   // 판 밖(씬 뷰)에서는 안 세운다 — `Ensure`

        _verts.Clear(); _norms.Clear(); _uvs.Clear(); _ceil.Clear(); _liftAt.Clear();

        int x0 = chunk.x * ChunkTiles, y0 = chunk.y * ChunkTiles;
        for (int ty = 0; ty < ChunkTiles; ty++)
        for (int tx = 0; tx < ChunkTiles; tx++)
        {
            if (_solid[tx, ty]) continue;
            var tile = new Vector2Int(x0 + tx, y0 + ty);
            if (SkyHoles.Contains(tile)) continue;   // 볕 구멍 — 위가 뚫려 있다(`Skylight`)

            float y = CeilingOf(tile);
            Vector3 c = map != null ? map.TileToWorld(tile) : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            float xa = c.x - 0.5f, xb = c.x + 0.5f, za = c.z - 0.5f, zb = c.z + 0.5f, xm = c.x, zm = c.z;

            var down = Vector3.down;
            Quad(_ceil, new Vector3(xa, y, za), new Vector3(xm, y, za), new Vector3(xm, y, zm), new Vector3(xa, y, zm), down);
            Quad(_ceil, new Vector3(xm, y, za), new Vector3(xb, y, za), new Vector3(xb, y, zm), new Vector3(xm, y, zm), down);
            Quad(_ceil, new Vector3(xa, y, zm), new Vector3(xm, y, zm), new Vector3(xm, y, zb), new Vector3(xa, y, zb), down);
            Quad(_ceil, new Vector3(xm, y, zm), new Vector3(xb, y, zm), new Vector3(xb, y, zb), new Vector3(xm, y, zb), down);

            // 이웃 천장이 더 높으면 그 차이를 세로로 막는다 — **낮은 쪽이 그린다**(벽의 단차 띠와 같은 규칙, 겹침 없음).
            Riser(tile + Vector2Int.left,  y, xa, za, xa, zb, Vector3.right);
            Riser(tile + Vector2Int.right, y, xb, za, xb, zb, Vector3.left);
            Riser(tile + Vector2Int.down,  y, xa, za, xb, za, Vector3.forward);
            Riser(tile + Vector2Int.up,    y, xa, zb, xb, zb, Vector3.back);
        }

        var mesh = ch.CeilMesh;
        mesh.Clear();
        mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetUVs(0, _uvs);
        SetLift(mesh);   // **셰이더가 읽는 채널은 늘 든다** — 0이어도
        mesh.SetTriangles(_ceil, 0);
        mesh.RecalculateBounds();

        ch.CeilRenderer.sharedMaterial = SideMat();
        TintCeiling(ch.CeilRenderer, chunk);
        ch.CeilRenderer.enabled = FirstPersonView.On && _ceil.Count > 0;
    }

    void Riser(Vector2Int neighbor, float y, float x1, float z1, float x2, float z2, Vector3 n)
    {
        if (RockMap.IsRock(neighbor) || SkyHoles.Contains(neighbor)) return;
        float ny = CeilingOf(neighbor);
        if (ny <= y + 0.001f) return;

        float xm = (x1 + x2) * 0.5f, zm = (z1 + z2) * 0.5f;
        Quad(_ceil, new Vector3(x1, y, z1), new Vector3(x1, ny, z1), new Vector3(xm, ny, zm), new Vector3(xm, y, zm), n);
        Quad(_ceil, new Vector3(xm, y, zm), new Vector3(xm, ny, zm), new Vector3(x2, ny, z2), new Vector3(x2, y, z2), n);
    }

    // 빈칸의 천장 높이 — 위의 규칙. **자리만의 함수다** — 청크 이음매 너머 이웃도 같은 답을 낸다.
    static float CeilingOf(Vector2Int t)
    {
        float y = RockMap.SurfaceOf(t) + RockMap.Height;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var n = new Vector2Int(t.x + dx, t.y + dy);
            if (RockMap.IsRock(n)) y = Mathf.Min(y, RockMap.TopOf(n));
        }
        return y;
    }

    // 천장은 벽과 같은 돌이고 조금 어둡다 — 횃불이 멀리서 비춘다.
    const float CeilShade = 0.8f;

    void TintCeiling(MeshRenderer mr, Vector2Int chunk)
    {
        if (MapManager.Instance == null) return;
        _mpb ??= new MaterialPropertyBlock();

        float half = ChunkTiles * 0.5f;
        var at = MapManager.Instance.TileToWorld(new Vector2Int(Mathf.RoundToInt(chunk.x * ChunkTiles + half),
                                                                 Mathf.RoundToInt(chunk.y * ChunkTiles + half)));
        var tint = WorldMap.BiomeAtWorld(new Vector2(at.x, at.z)).RockTint() * CeilShade;
        tint.a = 1f;

        _mpb.Clear();
        _mpb.SetColor("_BaseColor", tint);
        _mpb.SetColor("_Color",     tint);
        mr.SetPropertyBlock(_mpb);
    }

    void OnFirstPerson(bool on)
    {
        foreach (var c in _chunks.Values)
            if (c.CeilRenderer != null) c.CeilRenderer.enabled = on && c.CeilMesh.vertexCount > 0;
    }

    // ── 볕 구멍 — 천장을 비운다 ────────────────────────────────────────────
    //
    // 볕(`Skylight`)은 *천장에 뚫린 구멍*으로 지은 물건이다. 천장이 생기면 그 자리를 비워야 볕이 거짓말이 안 된다.
    // 볕은 뿌리기가 청크를 따라 세우고 거두므로 **선 것이 스스로 알린다**(겹칠 수 있어 칸마다 센다).
    static readonly Dictionary<Vector2Int, int> _holeCount = new();
    static readonly HashSet<Vector2Int> SkyHoles = new();
    static event System.Action<Vector2Int> HoleChanged;

    public static void AddSkyHole(Vector3 center, float radius)  => EditSkyHole(center, radius, +1);
    public static void RemoveSkyHole(Vector3 center, float radius) => EditSkyHole(center, radius, -1);

    static void EditSkyHole(Vector3 center, float radius, int delta)
    {
        var map = MapManager.Instance;
        if (map == null) return;

        var mid = map.WorldToTile(center);
        int r = Mathf.CeilToInt(radius);
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
        {
            var t = new Vector2Int(mid.x + dx, mid.y + dy);
            var p = map.TileToWorld(t);
            float ex = p.x - center.x, ez = p.z - center.z;
            if (ex * ex + ez * ez > radius * radius) continue;

            _holeCount.TryGetValue(t, out int n);
            n += delta;
            if (n > 0) { _holeCount[t] = n; SkyHoles.Add(t); }
            else       { _holeCount.Remove(t); SkyHoles.Remove(t); }
        }
        // 구멍이 청크 경계에 걸치면 이웃 청크도 — 네 귀퉁이가 든 청크를 다 더럽힌다(같은 청크면 한 번으로 접힌다).
        HoleChanged?.Invoke(mid + new Vector2Int(r, r));
        HoleChanged?.Invoke(mid - new Vector2Int(r, r));
        HoleChanged?.Invoke(mid + new Vector2Int(r, -r));
        HoleChanged?.Invoke(mid + new Vector2Int(-r, r));
    }

    void OnHoleChanged(Vector2Int tile) => Dirty(ChunkOf(tile));

    // ── 벽에 박힌 조명 (2026-09-28) ─────────────────────────────────────────
    //
    // 무엇이 어디에 박혔나는 `RockMap.WallLightAt`이 답하고, 여기는 **드러난 면에 세우기만** 한다.
    // 근거는 `WallLightData` 머리에 적었다.
    //
    // **판 안에서만 세운다** — 씬 뷰에서도 벽을 굽는데(`EditorShow`) 거기서 프리팹을 세우면
    // `LightSource` 레지스트리가 편집 중에 차고 씬 저장을 막는 표시를 따로 챙겨야 한다. 저작할
    // 때 볼 것은 *벽이 어디 있나*이고 불빛은 판에서 본다.
    readonly List<WantedLight> _wantLights = new();
    readonly HashSet<Vector2Int> _keepLights = new();
    static readonly List<Vector2Int> _dropLights = new();

    void CollectWallLight(MapManager map, Vector2Int tile, bool wL, bool wR, bool wD, bool wU,
                          float xa, float xb, float za, float zb, float floor)
    {
        if (!Application.isPlaying) return;

        var data   = RockMap.WallLightAt(tile);
        var prefab = data != null ? data.PrefabFor(tile) : null;
        if (prefab == null) return;

        // **면은 해시가 고른다** — 늘 첫 면(왼쪽)부터 보면 모서리 칸의 조명이 전부 같은 쪽을 본다.
        int start = (int)(RockMap.Hash01(tile, 0x85EBCA6Bu) * 4f) & 3;
        for (int i = 0; i < 4; i++)
        {
            int f = (start + i) & 3;
            bool open = f switch { 0 => wL, 1 => wR, 2 => wD, _ => wU };
            if (!open) continue;

            Vector2Int d; Vector3 n, along, center;
            float mx = (xa + xb) * 0.5f, mz = (za + zb) * 0.5f;
            switch (f)
            {
                case 0:  d = Vector2Int.left;  n = Vector3.left;    along = Vector3.forward; center = new Vector3(xa, 0f, mz); break;
                case 1:  d = Vector2Int.right; n = Vector3.right;   along = Vector3.forward; center = new Vector3(xb, 0f, mz); break;
                case 2:  d = Vector2Int.down;  n = Vector3.back;    along = Vector3.right;   center = new Vector3(mx, 0f, za); break;
                default: d = Vector2Int.up;    n = Vector3.forward; along = Vector3.right;   center = new Vector3(mx, 0f, zb); break;
            }

            // **높이는 벽 앞 바닥에서 잰다** — 벽 칸의 바닥(`floor`)은 이웃이 파여 낮아지면 그만큼
            // 떠 보인다(옆면을 이웃 바닥까지 내려 세우는 것과 같은 이유).
            float ground = map != null ? map.SurfaceHeight(tile + d) : floor;
            float h      = Mathf.Lerp(data.HeightMin, data.HeightMax, RockMap.Hash01(tile, 0xC2B2AE35u));
            float slide  = (RockMap.Hash01(tile, 0x27D4EB2Fu) * 2f - 1f) * data.Jitter;

            _wantLights.Add(new WantedLight
            {
                Tile = tile, Face = f, Data = data, Prefab = prefab,
                Pos  = center + along * slide + Wobble(center + along * slide) + Vector3.up * (ground + h),
                Rot  = Quaternion.LookRotation(n, Vector3.up) * data.TwistFor(tile),
            });
            return;
        }
    }

    // **있던 것은 그대로 둔다** — 한 칸을 팔 때마다 청크 전체가 다시 구워지는데, 그때마다 불을
    // 세웠다 부수면 `LightSource`가 들고 나며 불꽃 박자가 새로 굴러 **깜빡인다.** 면이나 종류가
    // 바뀐 것만 새로 세우고, 자리는 늘 다시 맞춘다(땅을 고르면 바닥 높이가 바뀐다).
    void SyncWallLights(Chunk ch)
    {
        _keepLights.Clear();

        foreach (var w in _wantLights)
        {
            _keepLights.Add(w.Tile);

            if (ch.Lights.TryGetValue(w.Tile, out var have) && have.Go != null
                && have.Face == w.Face && have.Data == w.Data)
            {
                have.Go.transform.SetPositionAndRotation(w.Pos, w.Rot);
                continue;
            }

            if (have.Go != null) Toss(have.Go);

            var go = Instantiate(w.Prefab, w.Pos, w.Rot, ch.Go.transform);
            w.Data.AddMembers(go, w.Tile);   // 곁가지 — 모양만(불빛은 무리에 하나)
            w.Data.Dress(go, w.Tile);        // 크기·빛깔 — 칸마다 다르다, 곁가지까지 같이
            ShadowBudget.Track(go);          // 그림자는 화면 가운데 몇 개만 던진다
            ch.Lights[w.Tile] = new PlacedLight { Go = go, Face = w.Face, Data = w.Data };
        }

        if (ch.Lights.Count == _keepLights.Count) return;

        _dropLights.Clear();
        foreach (var kv in ch.Lights)
            if (!_keepLights.Contains(kv.Key)) _dropLights.Add(kv.Key);

        foreach (var t in _dropLights)
        {
            if (ch.Lights[t].Go != null) Toss(ch.Lights[t].Go);
            ch.Lights.Remove(t);
        }
    }

    // ── 막는 것은 박스다 ────────────────────────────────────────────────────
    //
    // **콜라이더로 이 메시를 쓰면 안 된다**(2026-08-30). 이 메시는 *드러난 면만* 굽는다 —
    // 즉 **속이 빈 껍데기**다. 캡슐이 어쩌다 한 번 안으로 새면 **밀어낼 면이 안에 없어서**
    // 그대로 벽 속에 선다. ㄷ자 안쪽처럼 두 면에 동시에 눌리는 자리, 순간이동으로 자리를
    // 옮긴 직후, 프레임이 튄 순간 — 새어 들어가는 길이 여럿이고 **하나씩 막는 방식으로는
    // 안 끝난다.**
    //
    // **박스는 속이 찼다.** 유니티가 겹침을 풀 때 언제나 바깥으로 밀어내므로 *"들어간 채로
    // 서 있는"* 상태 자체가 안 생긴다.
    //
    // **칸마다 하나씩 달면 안 된다** — 청크 하나가 256개다. 이어진 바위를 **직사각형으로
    // 묶는다**(탐욕적으로 오른쪽 → 아래로 넓힌다). 통짜 바위 청크는 박스 **하나**가 되고,
    // 굴이 난 곳도 대개 열 몇 개다. `FloorRenderer`가 면을 묶는 것과 같은 결의 최적화다.
    //
    // **높이는 넉넉히 잡는다** — 바닥이 칸마다 조금씩 다르므로 가장 낮은 데서 가장 높은
    // 데까지 덮는다. 넘치는 쪽은 땅속이라 아무도 안 만난다.
    static readonly bool[,] _solid = new bool[ChunkTiles, ChunkTiles];
    static readonly bool[,] _taken = new bool[ChunkTiles, ChunkTiles];

    // `_solid`·`_taken`·최저/최고는 **`Rebuild`가 채워 넘긴다** — 같은 256칸을 두 번 돌지
    // 않으려는 것이고, 그래서 **`Rebuild` 말고 다른 데서 부르면 안 된다**(빈 배열로 돈다).
    void FitBoxes(Chunk ch, Vector2Int chunk, float lowest, float highest)
    {
        var map = MapManager.Instance;
        int x0 = chunk.x * ChunkTiles, y0 = chunk.y * ChunkTiles;

        // **박스의 주인은 여기 하나다**(2026-09-18). `SetBoxCount`는 **목록에 든 것만** 끌 수
        // 있으므로, 그 오브젝트에 목록 밖의 `BoxCollider`가 하나라도 붙어 있으면 **영영 켜진
        // 채로 남는다** — 그게 곧 *"파냈는데 안 보이는 벽에 막힌다"*다(2026-09-18 진단:
        // 같은 범위의 박스가 열두 개 겹쳐 있었고 그중 열하나가 낡은 것이었다).
        //
        // 그래서 굽기 전에 **실제로 붙어 있는 것을 세어 목록을 맞춘다.** 개수가 같으면
        // 아무 일도 안 한다(대개 그렇다).
        ch.Go.GetComponents(_boxBuf);
        if (_boxBuf.Count != ch.Boxes.Count)
        {
            if (!_warnedStrayBox)
            {
                _warnedStrayBox = true;
                Debug.LogWarning($"[바위] {ch.Go.name}에 목록 밖 콜라이더가 있다 — "
                               + $"실제 {_boxBuf.Count}개 / 목록 {ch.Boxes.Count}개. 거둬서 끈다.");
            }
            ch.Boxes.Clear();
            ch.Boxes.AddRange(_boxBuf);
        }

        if (lowest > highest) { SetBoxCount(ch, 0); return; }   // 바위가 없다

        float bottom = lowest - 2f;                 // 조금 파묻는다 — 발밑으로 새지 않게

        float top    = highest + RockMap.Height;
        float cy     = (bottom + top) * 0.5f;
        float sy     = top - bottom;

        int used = 0;
        for (int ty = 0; ty < ChunkTiles; ty++)
        for (int tx = 0; tx < ChunkTiles; tx++)
        {
            if (!_solid[tx, ty] || _taken[tx, ty]) continue;

            // 오른쪽으로 최대한.
            int w = 1;
            while (tx + w < ChunkTiles && _solid[tx + w, ty] && !_taken[tx + w, ty]) w++;

            // 그 폭 그대로 아래로 최대한.
            int h = 1;
            while (ty + h < ChunkTiles)
            {
                bool ok = true;
                for (int k = 0; k < w; k++)
                    if (!_solid[tx + k, ty + h] || _taken[tx + k, ty + h]) { ok = false; break; }
                if (!ok) break;
                h++;
            }

            for (int j = 0; j < h; j++)
            for (int k = 0; k < w; k++) _taken[tx + k, ty + j] = true;

            // 칸 범위 → 월드. **격자 위상을 얹는다**(그리는 쪽과 같은 식이어야 한다).
            Vector3 a = map != null ? map.TileToWorld(new Vector2Int(x0 + tx, y0 + ty))
                                    : new Vector3(x0 + tx + 0.5f, 0f, y0 + ty + 0.5f);

            var box = BoxAt(ch, used++);
            box.center = new Vector3(a.x - 0.5f + w * 0.5f, cy, a.z - 0.5f + h * 0.5f);
            box.size   = new Vector3(w, sy, h);
        }

        SetBoxCount(ch, used);
    }

    // **박스를 지웠다 다시 만들지 않는다** — 청크는 파낼 때마다 다시 구워지므로, 그때마다
    // 컴포넌트를 붙였다 떼면 물리 엔진이 매번 브로드페이즈를 다시 짠다. 남는 것은 꺼 둔다.
    static readonly List<BoxCollider> _boxBuf = new();
    static bool _warnedStrayBox;

    BoxCollider BoxAt(Chunk ch, int i)
    {
        while (ch.Boxes.Count <= i) ch.Boxes.Add(ch.Go.AddComponent<BoxCollider>());
        var b = ch.Boxes[i];
        b.enabled = true;
        return b;
    }

    static void SetBoxCount(Chunk ch, int used)
    {
        for (int i = used; i < ch.Boxes.Count; i++)
            if (ch.Boxes[i] != null) ch.Boxes[i].enabled = false;
    }

    // **그렸는지 돌려준다** — 광맥 알갱이는 *드러난* 면에만 박히는데, 그 판정을 부르는
    // 쪽에서 다시 하면 `IsRock`을 칸마다 두 번 묻게 되고 무엇보다 **답이 두 벌**이 된다.
    bool Side(List<int> list, List<int> capList, Vector2Int neighbor,
              float x1, float z1, float x2, float z2, float floor, float top, Vector3 n)
    {
        bool rock = RockMap.IsRock(neighbor);
        float bottom;

        if (rock)
        {
            // ── 맞닿은 바위라도 윗면이 다르면 그 차이가 틈으로 벌어진다 (2026-09-24) ──
            //
            // 여태는 *"맞닿은 바위 — 안 보인다"*로 통째로 건너뛰었고 **그게 맞았다**: 바닥이
            // 완전 평지라 이웃의 `top`이 언제나 같아서 두 기둥이 딱 맞물렸다.
            //
            // **바닥이 계단이 되면서 그 전제가 깨졌다.** `top = SurfaceOf(tile) + Height`라
            // 이웃 칸의 윗면이 **단만큼(1m) 낮고**, 겹치지 않는 그 구간에는 아무 면도 없어
            // **벽 위쪽이 뚫려 보인다**(사용자: *"제일 위에서는 벽 위에 틈이 좀 벌어져"*).
            // 벽이 24m라 아래쪽은 서로 가리므로 **언제나 위쪽에서만** 드러난다.
            //
            // **낮은 쪽이 아니라 높은 쪽이 그린다** — 양쪽이 다 그리면 같은 자리에 면이 두
            // 장 겹쳐 z-파이팅이 난다. 같은 높이면 아무도 안 그린다(예전 그대로).
            float ntop = RockMap.TopOf(neighbor);
            if (ntop >= top - 0.001f) return false;
            bottom = ntop;
        }
        else
        {
            // 이웃 바닥이 더 낮으면 거기까지 내려 세운다. 안 내리면 **벽 밑이 뚫려 보인다.**
            bottom = Mathf.Min(floor, RockMap.SurfaceOf(neighbor));
        }

        // ── 단차 띠는 뚜껑의 색으로 (2026-09-24, 사용자) ────────────────────
        //
        // 사용자: *"옆면이 보이는 거 같긴한데 그래도 통일성을 가지고 윗면이랑 똑같은 색으로
        // 보이면 좋겠다."* 재질이 **10배 갈려 있다** — 윗면은 `(0.05, 0.06, 0.07)`로 거의
        // 검고(*안 판 자리 = 어둠*이라는 규칙) 옆면은 `(0.52, 0.57, 0.60)`인 밝은 회색이다.
        // 그래서 이 띠를 옆면 목록에 넣으면 **검은 뚜껑들 사이에 밝은 선**이 줄줄이 선다.
        //
        // **뜻으로도 뚜껑이 맞다** — 이것은 *드러난 벽면*이 아니라 **위에서 본 뚜껑의 단차**다
        // (그래서 아래에서 알갱이도 안 박는다). 위에서 내려다보는 화면에서 뚜껑의 일부로
        // 읽혀야 한다.
        //
        // **법선은 수평으로 둔다.** 위로 기울이면 `RockCut`이 *뚜껑*으로 보고 `min(y, cutY)`로
        // **끌어내려** 띠가 납작해진다(벽 광석 알갱이가 2026-09-15에 그렇게 **몇 m짜리 가시**를
        // 세웠다). 자르기는 옆면처럼 걸려야 맞다 — 뚜껑보다 위로 삐져나오면 안 된다.
        // **가운데에서 둘로 나눈다** — 벽선이 칸마다 한 번이 아니라 반 칸마다 굽는다(`Wobble`).
        var l = rock ? capList : list;
        float xm = (x1 + x2) * 0.5f, zm = (z1 + z2) * 0.5f;

        // **드러난 벽은 세로로도 나눈다**(2026-10-12) — 셰이더가 높이마다 다르게 밀므로(`RockBulge`) 꺾일
        // 자리가 있어야 한다. 단차 띠(`rock`)는 벽 꼭대기에 있어 밀리는 높이 밖이다.
        float y0 = bottom;
        if (!rock)
        {
            float until = Mathf.Min(top, bottom + BulgeSpan);
            for (float y1 = (Mathf.Floor(y0 / BulgeRow) + 1f) * BulgeRow; y1 < until - 0.01f; y1 += BulgeRow)
            {
                WallBand(l, x1, z1, xm, zm, x2, z2, y0, y1, n);
                y0 = y1;
            }
            if (until > y0 + 0.01f && until < top - 0.01f)
            {
                WallBand(l, x1, z1, xm, zm, x2, z2, y0, until, n);
                y0 = until;
            }
        }
        WallBand(l, x1, z1, xm, zm, x2, z2, y0, top, n);

        // **알갱이는 드러난 면에만.** 바위끼리 물린 사이의 얇은 띠는 *벽면*이 아니라 이음매라,
        // 거기에 광석을 박으면 **못 캐는 자리에 광맥이 보인다**(기반암에 안 박는 것과 같은 이유).
        return !rock;
    }

    // **줄은 세계 높이에 맞춘다**(바닥에서 재지 않는다) — 셰이더가 `y / BulgeRow`로 줄을 세므로, 벽마다
    // 바닥이 달라도 꺾이는 높이가 같아야 이웃 벽과 뚜껑이 맞물린다. 값은 `RockCut.hlsl`의 `kRockBulgeRow`와 같다.
    const float BulgeRow  = 0.75f;
    // 이 높이까지만 잘게 나눈다 — 벽은 24m인데 다 나누면 정점이 서른 배가 된다. 셰이더는 자른 벽이 7.5m를
    // 넘으면 밀기를 멈추고(`kRockBulgeUntil`), 나머지는 바닥이 층층이라 생기는 차이의 여유다.
    const float BulgeSpan = 10f;

    // **가운데에서 둘로 나눈다** — 벽선이 칸마다 한 번이 아니라 반 칸마다 굽는다(`Wobble`).
    void WallBand(List<int> l, float x1, float z1, float xm, float zm, float x2, float z2,
                  float y0, float y1, Vector3 n)
    {
        Quad(l, new Vector3(x1, y0, z1), new Vector3(x1, y1, z1),
                new Vector3(xm, y1, zm), new Vector3(xm, y0, zm), n);
        Quad(l, new Vector3(xm, y0, zm), new Vector3(xm, y1, zm),
                new Vector3(x2, y1, z2), new Vector3(x2, y0, z2), n);
    }

    // ── 벽면에 광석 알갱이 ──────────────────────────────────────────────────
    //
    // 면을 통째로 물들이지 않는다 — **박힌 것**으로 보여야 *"이 벽에 구리가 있다"*로 읽히고,
    // 면을 칠하면 *바이옴이 바뀐 것*처럼 보인다(벽 색조가 이미 그 축을 쓰고 있다).
    //
    // **자리는 좌표 해시다** — 저장하는 것이 없고, 청크를 다시 구워도 알갱이가 안 움직인다
    // (뿌리기·광맥 심기와 같은 규칙). 움직이면 한 칸을 팔 때마다 옆 벽의 무늬가 흔들린다.
    //
    // **납작한 사각형이 아니라 솟은 알갱이다**(2026-09-15, 사용자: *"너무 단색이야 — 어색해,
    // 주변이랑 안 어울려"*). 전에는 면에 붙인 **평행사변형 한 장**이었는데, 법선이 벽과 같아
    // 빛을 받는 방식이 벽과 똑같고 가장자리가 칼같아서 **스티커**로 보였다. 셋을 바꿨다:
    //   ① **가운데를 벽 밖으로 밀고 둘레 법선을 바깥으로 기울인다** — 빛이 한쪽을 밝히고
    //      반대쪽을 어둡게 해 **한 알갱이 안에서 톤이 갈린다**(단색이 풀린다).
    //   ② **둘레를 흔든 여섯 모**로 — 사각형은 사람이 붙인 모양이다.
    //   ③ **흩뿌리지 않고 덩어리로** — 광맥은 줄기로 난다. 고르게 흩으면 무늬가 된다.
    void Specks(List<int> list, Vector2Int tile, int dir,
                float x1, float z1, float x2, float z2, float floor, Vector3 n)
    {
        var t = new Vector3(x2 - x1, 0f, z2 - z1);   // 면을 따라가는 방향(길이 1m)
        float baseY = floor + 0.15f;

        int clusters = OreClustersMin +
                       Mathf.FloorToInt(Hash(tile, dir, 99) * (OreClustersMax - OreClustersMin + 1));

        for (int c = 0; c < clusters; c++)
        {
            float cu = 0.18f + Hash(tile, dir, 100 + c * 7) * 0.64f;
            // 층 c 안에서만 굴린다 — 층마다 하나씩이라 위아래로 고르게 퍼진다(위 상수 설명).
            float cy = baseY + (c + 0.15f + Hash(tile, dir, 101 + c * 7) * 0.7f) / clusters * OreBand;

            for (int k = 0; k < OreNuggets; k++)
            {
                int salt = 200 + c * 31 + k * 5;

                // 덩어리 안에서 조금씩 비껴 선다 — 첫 알갱이가 가장 크다.
                float du = (Hash(tile, dir, salt)     - 0.5f) * 0.30f;
                float dy = (Hash(tile, dir, salt + 1) - 0.5f) * 0.26f;
                float r  = (k == 0 ? 0.10f : 0.045f) + Hash(tile, dir, salt + 2) * 0.06f;

                float u = Mathf.Clamp(cu + du, 0.08f, 0.92f);
                var center = new Vector3(x1, cy + dy, z1) + t * u + n * OreOut;

                Nugget(list, center, t, n, r, Hash(tile, dir, salt + 3));
            }
        }
    }

    // 여섯 모 알갱이 하나. 가운데를 **반지름의 절반만큼** 벽 밖으로 민다 — 더 밀면 가시가 된다.
    void Nugget(List<int> list, Vector3 center, Vector3 t, Vector3 n, float r, float seed)
    {
        int i0 = _verts.Count;
        center += Wobble(center - n * OreOut);   // 굽은 벽을 따라 옮긴다 — 벽선 위 자리로 잰다

        var tip = center + n * (r * 0.5f);
        _verts.Add(tip); _norms.Add(n); Uv(tip, n);

        for (int k = 0; k < OreRim; k++)
        {
            float a  = (k + (Frac(seed * 13.7f + k * 0.37f) - 0.5f) * 0.5f) / OreRim * Mathf.PI * 2f;
            float rr = r * (0.65f + Frac(seed * 7.3f + k * 0.61f) * 0.5f);

            var radial = t * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
            var p      = center + radial * rr;

            // **둘레는 바깥을 보되 위아래로는 덜 기운다**(2026-09-15에 잡았다). `RockCut`이
            // **법선으로 윗면을 가른다**(`|n.y| > 0.5` = 뚜껑) — 뚜껑은 자르지 않고 **자르는 높이로
            // 끌어내린다.** 처음엔 둘레를 사방으로 똑같이(0.85) 기울였더니 **위쪽 둘레 꼭짓점이
            // 뚜껑으로 읽혀** 카메라를 내려 자르는 면이 낮아질 때 그 꼭짓점만 끌려 내려가고,
            // 사이의 삼각형이 **몇 m짜리 가시**로 늘어났다(사용자: *"화면 내릴 때 이렇게 된다"*).
            // 위아래 몫을 0.4로 두면 `n.y`가 최대 0.45 — 보간해도 넘지 않는다. **바위 메시에
            // 새 면을 더할 때는 법선의 y를 0.5 밑에 둘 것.**
            var tilt = t * (Mathf.Cos(a) * 0.6f) + Vector3.up * (Mathf.Sin(a) * 0.4f);
            _verts.Add(p);
            _norms.Add((n * 0.8f + tilt).normalized);
            Uv(p, n);
        }

        for (int k = 0; k < OreRim; k++)
        {
            list.Add(i0);
            list.Add(i0 + 1 + k);
            list.Add(i0 + 1 + (k + 1) % OreRim);
        }
    }

    static float Frac(float v) => v - Mathf.Floor(v);

    // ── 윗면에도 박는다 (2026-09-28) ────────────────────────────────────────
    //
    // 사용자: *"광물이 박힌 벽 있잖아? 그 벽위에도 광물이 박혀서 직관적으로 볼 수 있게"*. 카메라는 내려다보므로
    // 화면에서 벽은 **대부분 윗면**이다 — 옆면에만 박으면 광맥이 벽 가장자리의 가느다란 띠로만 보인다.
    //
    // **드러난 칸의 윗면에만** 박는다(옆면이 하나라도 트인 칸 — 옆면 알갱이와 같은 칸). 09-14에 윗면을 뺀
    // 이유는 *안 판 바위 속이 들여다보인다*였고, 그것은 **아직 안 드러난 칸**의 이야기다. 드러난 칸의
    // 윗면은 이미 보이는 벽의 머리라 새로 알려 주는 것이 없다 — *판 만큼 드러난다*는 그대로다.
    //
    // **자르기와 싸우지 않게** — 윗면은 자르기가 **끌어내리는 면**(`RockCut`)이다. 알갱이 정점도 법선을
    // 위로 둬 뚜껑으로 읽히게 하고, **뚜껑에서 뜬 만큼**(`lift`, uv3)을 넘겨 같이 내려오게 한다. 안 넘기면
    // 전부 한 평면으로 눌려 뚜껑과 z-파이팅한다.
    const float TopOreOut = 0.02f;   // 뚜껑에서 이만큼 띄운다

    // 정점 번호 → 뚜껑에서 뜬 높이. 나머지 정점은 0이다(`SetLift`가 채운다).
    readonly List<(int index, float lift)> _liftAt = new();
    readonly List<Vector2> _lift = new();

    // **옆면 것보다 크고 많다**(2026-09-28, 사용자: *"벽위에 딱히 광물이 박혀있지도 않아"*). 윗면은 늘 **멀리서
    // 내려다보는** 면이라 옆면 크기(큰 알 12cm)로는 화면에서 두세 픽셀이었고, 뚜껑이 거의 검어 묻혔다.
    const int   TopClustersMin = 2;
    const int   TopClustersMax = 3;
    const float TopBigR        = 0.20f;   // 덩어리의 첫 알
    const float TopSmallR      = 0.09f;

    void TopSpecks(List<int> list, Vector2Int tile, float xa, float za, float top)
    {
        int clusters = TopClustersMin +
                       Mathf.FloorToInt(Hash(tile, 4, 99) * (TopClustersMax - TopClustersMin + 1));

        for (int c = 0; c < clusters; c++)
        {
            float cx = 0.2f + Hash(tile, 4, 100 + c * 7) * 0.6f;
            float cz = 0.2f + Hash(tile, 4, 101 + c * 7) * 0.6f;

            for (int k = 0; k < OreNuggets; k++)
            {
                int salt = 200 + c * 31 + k * 5;
                float dx = (Hash(tile, 4, salt)     - 0.5f) * 0.30f;
                float dz = (Hash(tile, 4, salt + 1) - 0.5f) * 0.30f;
                float r  = (k == 0 ? TopBigR : TopSmallR) + Hash(tile, 4, salt + 2) * 0.07f;

                var center = new Vector3(xa + Mathf.Clamp(cx + dx, 0.08f, 0.92f), top + TopOreOut,
                                         za + Mathf.Clamp(cz + dz, 0.08f, 0.92f));
                TopNugget(list, center, top, r, Hash(tile, 4, salt + 3));
            }
        }
    }

    // `Nugget`을 눕힌 것. **법선의 y를 0.5 위에 둔다** — 뚜껑으로 읽혀야 자르기가 끌어내린다
    // (옆면으로 읽히면 자르는 높이 위에서 **잘려 사라진다**). 둘레는 바깥으로 기울되 위가 주인이다.
    void TopNugget(List<int> list, Vector3 center, float top, float r, float seed)
    {
        int i0 = _verts.Count;
        center += Wobble(center);   // 뚜껑 가장자리가 굽은 만큼 같이

        var tip = center + Vector3.up * (r * 0.5f);
        _verts.Add(tip); _norms.Add(Vector3.up); Uv(tip, Vector3.up);
        _liftAt.Add((i0, tip.y - top));

        float spin = seed * Mathf.PI * 2f;
        for (int k = 0; k < OreRim; k++)
        {
            float a  = spin + (k + (Frac(seed * 13.7f + k * 0.37f) - 0.5f) * 0.5f) / OreRim * Mathf.PI * 2f;
            float rr = r * (0.65f + Frac(seed * 7.3f + k * 0.61f) * 0.5f);

            var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var p      = center + radial * rr;

            _liftAt.Add((_verts.Count, p.y - top));
            _verts.Add(p);
            _norms.Add((Vector3.up * 0.8f + radial * 0.6f).normalized);   // y 0.8 — 뚜껑
            Uv(p, Vector3.up);
        }

        for (int k = 0; k < OreRim; k++)
        {
            list.Add(i0);
            list.Add(i0 + 1 + (k + 1) % OreRim);
            list.Add(i0 + 1 + k);
        }
    }

    // uv3.x = 뚜껑에서 뜬 높이. **알갱이가 없어도 채널을 반드시 채운다**(2026-09-28에 겪었다).
    //
    // 처음엔 *"없는 채널은 셰이더에서 0으로 읽힌다"*고 보고 알갱이 없는 청크는 건너뛰었다. **아니었다** — 0이 아닌
    // 값이 들어와 `min(y - lift, 자르는 높이) + lift`가 뚜껑을 안 내렸고, 화면에는 **광맥이 박힌 청크만 벽과 같이
    // 내려가고 나머지는 윗면이 벽에서 떨어져 떠 있는 것**으로 보였다(사용자: *"박혀있는 쪽만 벽이랑 같이 내려가는
    // 거 같아"*). **셰이더가 읽는 채널은 메시가 늘 들고 있어야 한다.**
    void SetLift(Mesh mesh)
    {
        _lift.Clear();
        for (int i = 0; i < _verts.Count; i++) _lift.Add(Vector2.zero);
        foreach (var (index, lift) in _liftAt) _lift[index] = new Vector2(lift, 0f);
        mesh.SetUVs(3, _lift);
    }

    // 0~1. **`string.GetHashCode`를 안 쓴다** — 실행마다 달라지면 알갱이가 판마다 옮겨 다닌다.
    static float Hash(Vector2Int tile, int dir, int salt)
    {
        unchecked
        {
            int h = tile.x * 73856093 ^ tile.y * 19349663 ^ dir * 83492791 ^ salt * 1376312589;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    // ── 광맥 서브메시 ───────────────────────────────────────────────────────
    //
    // 종류마다 삼각형 목록 하나. **목록은 다시 쓴다** — 청크가 초당 백 장 넘게 구워지므로
    // 굽기마다 `new List`를 내면 그것이 곧 GC다.
    List<int> OreList(ResourceNodeData vein)
    {
        for (int i = 0; i < _oreKinds.Count; i++)
            if (_oreKinds[i] == vein) return _oreTris[i];

        if (_oreKinds.Count >= MaxOreKinds) return null;

        _oreKinds.Add(vein);
        while (_oreTris.Count < _oreKinds.Count) _oreTris.Add(new List<int>());

        var l = _oreTris[_oreKinds.Count - 1];
        l.Clear();
        return l;
    }

    // **재질은 광맥마다 한 벌이고 판 전체가 같이 쓴다** — 청크마다 만들면 세계를 걸어
    // 다니는 동안 재질이 수천 개 생긴다.
    //
    // **값은 `ResourceNodeData`가 든다**(`VeinArt`가 써 넣는다) — 여기서 색을 또 적으면
    // **같은 광맥이 벽에서와 바닥에서 다른 색**이 된다.
    static readonly Dictionary<ResourceNodeData, Material> _oreMats = new();

    // **판이 열릴 때 비운다** — 도메인 리로드를 끄면 지난 판에서 만든 재질(이미 파괴된
    // 것)을 들고 시작한다. 아래 `m != null`이 받아 주기는 하지만, 죽은 항목이 계속 쌓인다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOreMats() => _oreMats.Clear();

    // **벽의 재질에서 복제한다**(2026-09-15) — 전에는 텍스처 없는 단색 재질이었다. 벽은 사진
    // 기반 바위 무늬인데 알갱이만 **매끈한 한 색**이라 어두운 굴에서 *칠한 점*으로 튀었다.
    // 같은 텍스처·노멀맵을 쓰면 **벽의 결이 알갱이에도 이어져** 박힌 것으로 읽힌다.
    // 색은 곱해지므로 **광물 색 그대로 두면 한 칸 어두워진다** — 조금 밝게 올린다.
    //
    // **발광은 거의 끈다**(0.22 → 0.06). 발광이 알갱이를 *단색*으로 만든 주범이었다 — 조명과
    // 무관하게 같은 밝기를 더하므로 둘레 법선이 만든 톤 차이를 덮는다. **빛나는 광물은 그대로
    // 빛난다**(미스릴·마법 결정 — 그건 빛이 그 광물의 성격이다).
    // **빛나지 않는 광물의 발광 바닥값 — 벽 알갱이와 바닥 광맥(`VeinArt`)이 같이 읽는다.**
    // 둘이 따로 적혀 있었다(0.25 · 0.22). 2026-10-12에 굴을 더 어둡게 내린 뒤 사용자가 올렸다
    // (*"광석 발광은 좀 더 높혀야할 거 같은데"*). 야광 광물(0.8~0.9)의 절반이라 둘은 여전히 갈린다.
    public const float OreFloorGlow = 0.40f;

    Material OreMat(ResourceNodeData vein)
    {
        if (_oreMats.TryGetValue(vein, out var m) && m != null) return m;

        var wall = SideMat();
        m = wall != null ? new Material(wall) { name = "Vein " + vein.name + " (runtime)" }
                         : Lit("Vein " + vein.name + " (runtime)", vein.VeinColor);

        var col = vein.VeinColor * 1.35f;
        col.a = 1f;
        m.SetColor("_BaseColor", col);
        m.SetColor("_Color",     col);
        m.SetFloat("_Metallic",   vein.VeinMetallic);
        m.SetFloat("_Smoothness", vein.VeinSmoothness);

        m.EnableKeyword("_EMISSION");
        // **빛나지 않는 광맥도 은은하게는 뜬다**(2026-09-29, 사용자가 골랐다) — 0.06이면 불빛 없는 굴에서
        // 알갱이가 바위색에 묻혔다. 바닥값은 *여기 뭔가 박혀 있다*만 읽히는 정도고, 야광 광맥(0.9)과는 여전히 갈린다.
        m.SetColor("_EmissionColor", vein.VeinColor * (vein.VeinGlow ? 0.9f : OreFloorGlow));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        _oreMats[vein] = m;
        return m;
    }

    // ── 벽선을 흔든다 (2026-09-29) ──────────────────────────────────────────
    //
    // 사용자: *"벽들이 일렬로 너무 똑바로 서있는 거 같아 울퉁불퉁하면 좋겠어"*. 벽은 칸 경계라 위에서 보면
    // 자로 그은 선이다. **바위와 빈칸의 경계에 있는 점만** 수평으로 민다 — 반 칸 격자(모서리 · 변 가운데)
    // 위의 점마다 값이 하나다.
    //
    // **틈이 안 나는 이유는 셋이다.**
    //   · **값은 자리의 함수다** — 같은 점을 어느 칸이, 어느 청크가 그리든 같은 만큼 움직인다(청크 이음매 포함).
    //   · **경계가 아닌 점은 안 움직인다** — 사방이 바위인 칸의 뚜껑은 한 장인데, 그 가장자리에 옆 칸의
    //     쪼갠 뚜껑이 점을 하나 더 두어도 그 점이 안 움직이니 한 직선 위에 있다.
    //   · **높이를 안 탄다** — `RockCut`이 뚜껑을 *자르는 높이*로 끌어내리는데 그 높이가 줌마다 다르다.
    //     흔들기가 높이를 타면 그 높이에서 벽의 선과 뚜껑의 선이 어긋나 구멍이 난다.
    //
    // **콜라이더와 길찾기는 칸 그대로다** — 모양만 굽는다. 그래서 폭은 칸의 3분의 1 아래로 둔다(바닥으로
    // 삐져나온 만큼 폰이 벽 그림에 살짝 묻힐 수 있다). 이웃 점끼리는 매끈한 노이즈라 같이 움직여 면이 안 뒤집힌다.
    const float WobbleAmp    = 0.30f;   // 매끈한 굴곡의 폭(m)
    const float WobbleJitter = 0.07f;   // 점마다 얹는 잔굴곡(m)
    const float WobbleScale  = 0.42f;   // 노이즈 주파수(1/m) — 2.4m쯤에 한 번 굽는다

    float _latOx, _latOz;   // 반 칸 격자의 원점(격자 위상 포함)
    readonly Dictionary<long, Vector2> _latCache = new();

    void BeginWobble(MapManager map)
    {
        var c0 = map != null ? map.TileToWorld(Vector2Int.zero) : new Vector3(0.5f, 0f, 0.5f);
        _latOx = c0.x - 0.5f; _latOz = c0.z - 0.5f;
        _latCache.Clear();
    }

    // 사방 여덟 칸 중 하나라도 뚫렸나 — 뚜껑을 쪼갤지 가른다.
    static bool TouchesOpen(Vector2Int t)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if ((dx != 0 || dy != 0) && !RockMap.IsRock(new Vector2Int(t.x + dx, t.y + dy))) return true;
        return false;
    }

    // 그 자리를 얼마나 미나(수평). 반 칸 격자 점 넷을 쌍선형으로 섞는다 — 격자 점에서는 그 점의 값이고,
    // 벽선 위에서는 양끝 두 점 사이의 직선이라 **알갱이·조명도 굽은 벽면 위에 그대로 앉는다.**
    Vector3 Wobble(Vector3 p)
    {
        float fx = (p.x - _latOx) * 2f, fz = (p.z - _latOz) * 2f;
        int i = Mathf.FloorToInt(fx), j = Mathf.FloorToInt(fz);
        float u = fx - i, v = fz - j;
        if (u > 0.999f) { i++; u = 0f; }   // 격자 점 위의 부동소수점 흔들림을 접는다
        if (v > 0.999f) { j++; v = 0f; }
        if (u < 0.001f) u = 0f;
        if (v < 0.001f) v = 0f;

        Vector2 o = Lat(i, j) * ((1 - u) * (1 - v));
        if (u > 0f)           o += Lat(i + 1, j)     * (u * (1 - v));
        if (v > 0f)           o += Lat(i, j + 1)     * ((1 - u) * v);
        if (u > 0f && v > 0f) o += Lat(i + 1, j + 1) * (u * v);
        return new Vector3(o.x, 0f, o.y);
    }

    Vector2 Lat(int i, int j)
    {
        long key = ((long)i << 32) ^ (uint)j;
        if (_latCache.TryGetValue(key, out var v)) return v;

        v = OnBoundary(i, j) ? LatNoise(i, j) : Vector2.zero;
        _latCache[key] = v;
        return v;
    }

    // 반 칸 격자 점 (i, j)가 바위와 빈칸의 경계 위인가. 짝수 = 칸 모서리 줄, 홀수 = 칸 가운데 줄.
    static bool OnBoundary(int i, int j)
    {
        bool ei = (i & 1) == 0, ej = (j & 1) == 0;
        if (!ei && !ej) return false;                       // 칸 한가운데

        int tx = i >> 1, ty = j >> 1;                       // 산술 시프트 = 음수도 내림
        if (ei && ej)                                       // 모서리 — 둘레 네 칸이 다 같으면 경계가 아니다
        {
            bool a = RockMap.IsRock(new Vector2Int(tx - 1, ty - 1));
            return a != RockMap.IsRock(new Vector2Int(tx,     ty - 1))
                || a != RockMap.IsRock(new Vector2Int(tx - 1, ty))
                || a != RockMap.IsRock(new Vector2Int(tx,     ty));
        }
        if (ej)                                             // 가로 변 가운데 — 아래 칸 ↔ 위 칸
            return RockMap.IsRock(new Vector2Int(tx, ty - 1)) != RockMap.IsRock(new Vector2Int(tx, ty));

        return RockMap.IsRock(new Vector2Int(tx - 1, ty)) != RockMap.IsRock(new Vector2Int(tx, ty));   // 세로 변 가운데
    }

    Vector2 LatNoise(int i, int j)
    {
        float x = _latOx + i * 0.5f, z = _latOz + j * 0.5f;
        float nx = Mathf.PerlinNoise(x * WobbleScale + 1731.3f, z * WobbleScale + 911.7f) * 2f - 1f;
        float nz = Mathf.PerlinNoise(x * WobbleScale + 4417.9f, z * WobbleScale + 2203.1f) * 2f - 1f;
        var t = new Vector2Int(i, j);
        float jx = (RockMap.Hash01(t, 0x9E3779B9u) * 2f - 1f) * WobbleJitter;
        float jz = (RockMap.Hash01(t, 0x7F4A7C15u) * 2f - 1f) * WobbleJitter;
        return new Vector2(Mathf.Clamp(nx * WobbleAmp * 1.6f, -WobbleAmp, WobbleAmp) + jx,
                           Mathf.Clamp(nz * WobbleAmp * 1.6f, -WobbleAmp, WobbleAmp) + jz);
    }

    // 텍스처가 몇 미터에 한 번 반복하나. **칸 크기(1m)와 안 떨어지는 값**으로 둔다 —
    // 정수로 두면 칸마다 같은 자리에서 무늬가 시작해 **격자가 눈에 보인다.**
    const float UvScale = 1f / 2.7f;

    // **UV를 월드 좌표에서 뽑는다**(평면 투영). 면마다 로컬 UV를 매기면 칸 하나가 텍스처
    // 한 장을 통째로 쓰게 되어, 1m 칸에 2m짜리 바위 무늬가 **통째로 눌려 들어간다** —
    // 벽이 모자이크처럼 보인다. 월드로 뽑으면 무늬가 칸을 가로질러 이어진다.
    //
    // **면의 방향에 따라 축을 고른다** — 윗면은 XZ, 옆면은 (X 또는 Z)와 Y다. 안 고르면
    // 옆면의 UV가 한 축으로 눌려 **세로로 늘어난 줄무늬**가 된다.
    void Quad(List<int> list, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
    {
        int i = _verts.Count;
        Uv(a, n); Uv(b, n); Uv(c, n); Uv(d, n);   // 무늬는 흔들기 전 자리에서 — 굽은 만큼 늘어나지 않게

        var wa = a + Wobble(a); var wb = b + Wobble(b); var wc = c + Wobble(c); var wd = d + Wobble(d);

        // **옆면은 굽은 면의 법선을 다시 잰다** — 원래 법선이면 굽은 벽이 빛을 평평하게 받아 굴곡이 안 읽힌다.
        // 방향은 원래 법선 쪽으로 맞춘다(감김이 면마다 다르다 — `RockCutBackface`가 법선으로 등을 가른다).
        // 흔들기는 수평뿐이라 y가 0.5를 못 넘는다 — 뚜껑으로 읽히지 않는다(`RockIsCap`).
        if (Mathf.Abs(n.y) < 0.5f)
        {
            var m = Vector3.Cross(wc - wa, wd - wb);
            if (m.sqrMagnitude > 1e-8f)
            {
                m.Normalize();
                if (Vector3.Dot(m, n) < 0f) m = -m;
                n = m;
            }
        }

        _verts.Add(wa); _verts.Add(wb); _verts.Add(wc); _verts.Add(wd);
        _norms.Add(n); _norms.Add(n); _norms.Add(n); _norms.Add(n);
        list.Add(i); list.Add(i + 1); list.Add(i + 2);
        list.Add(i); list.Add(i + 2); list.Add(i + 3);
    }

    void Uv(Vector3 p, Vector3 n)
    {
        Vector2 uv;
        if (Mathf.Abs(n.y) > 0.5f)      uv = new Vector2(p.x, p.z);   // 윗면
        else if (Mathf.Abs(n.x) > 0.5f) uv = new Vector2(p.z, p.y);   // 동·서 벽
        else                            uv = new Vector2(p.x, p.y);   // 남·북 벽
        _uvs.Add(uv * UvScale);
    }

    // ── 그릇 ────────────────────────────────────────────────────────────────

    // **바위 청크의 부모.** 커서 레이가 *"이건 바위다"*를 물을 자리다 — 바위는 `Ground`
    // 레이어라(짚어야 캘 수 있다) 레이어로는 바닥과 못 가른다.
    public static Transform ChunkRoot { get; private set; }

    const string RootName = "RockChunks";

    // ── 도메인 리로드가 남긴 벌을 거둔다 (2026-09-18) ────────────────────────
    //
    // **원인**: 스크립트를 다시 컴파일하면 도메인이 리로드되어 `_chunks`와 `_root`(둘 다
    // 순수 C# 필드)가 **비워지는데, 에디터가 세운 청크는 `HideFlags.DontSave`라 살아남는다**
    // (`DontSave`는 *저장 안 함* + **씬을 새로 읽어도 안 죽음**이다).
    //
    // 그래서 **컴파일할 때마다 청크가 한 벌씩 쌓였다.** `EditorClear`는 `_chunks`가 비어
    // 있으니 아무것도 못 치우고, `_root`도 null이라 루트조차 못 지운다 — 플레이에 들어가도
    // `DontSave`라 안 죽는다.
    //
    // 화면에 나타난 모양: **그 벌이 굳은 시점의 지형을 콜라이더로 들고 있어**, 그 뒤에 판
    // 자리를 폰이 **보이지 않는 벽**으로 계속 막혔다(메시는 새 벌이 그리므로 **벽은 안
    // 보인다**). 진단에는 같은 범위의 낡은 박스가 **열한 개**로 찍혔다 — 그날 컴파일한
    // 횟수였다. *"원래 잘 됐는데 갑자기"*의 답이 이것이다.
    //
    // **`_chunks`로 찾으면 안 된다** — 잃어버린 것이 그 목록이다. **씬을 본다.**
    // **도메인 리로드가 남긴 것을 한 번에 쓴다**(2026-09-18).
    //
    // `_chunks`가 비어 있는데 씬에 `RockChunks`가 서 있다면 **그 안의 것은 전부 주인 없는
    // 것**이다 — 목록이 곧 주인이므로, 목록이 비었다는 것은 아무것도 안 들고 있다는 뜻이다.
    // 청크마다 만나서 거두면 **다시 구워지는 것만** 거둬지고 나머지는 그대로 남는다
    // (그 남은 것이 판 자리를 막는다).
    //
    // **한 번만 돈다** — 이 플래그도 리로드에 같이 날아가므로, 리로드마다 정확히 한 번이다.
    bool _swept;

    void SweepOrphans()
    {
        if (_swept) return;
        _swept = true;

        if (_chunks.Count > 0) return;   // 들고 있는 것이 있으면 내 것이다

        int reaped = 0;
        foreach (var go in AllRoots())
        {
            reaped += go.transform.childCount;
            Toss(go);
        }

        // `_root`가 살아남았을 수도 있다(그 아래 것이 전부 주인 없는 것이다).
        if (_root != null) { reaped += _root.childCount; Toss(_root.gameObject); }
        _root = null; ChunkRoot = null;

        if (reaped > 0)
            Debug.LogWarning($"[바위] 주인 없는 청크 {reaped}개를 거뒀다 — 스크립트를 다시 "
                           + "컴파일하면서 목록(`_chunks`)만 비워지고 오브젝트는 살아남은 것들이다. "
                           + "그 낡은 콜라이더가 판 자리를 막고 있었다.");
    }

    static void ReapStrayRoots(Transform mine)
    {
        int reaped = 0;
        foreach (var go in AllRoots())
        {
            if (mine != null && go.transform == mine) continue;
            Toss(go);
            reaped++;
        }

        if (reaped > 0)
            Debug.LogWarning($"[바위] 남아 있던 `{RootName}` {reaped}벌을 거뒀다 — "
                           + "도메인 리로드로 목록을 잃은 사이 살아남은 것들이다. "
                           + "그 낡은 콜라이더가 판 자리를 막고 있었다.");
    }

    // **메모리에 살아 있는 `RockChunks` 루트 전부** — 씬 목록으로 찾지 않는다(2026-09-19).
    //
    // 전에는 `GetActiveScene().GetRootGameObjects()`를 훑었는데, 에디터가 `DontSave`로 세운 것은
    // 플레이에 들어가는 사이 **활성 씬의 목록 밖**에 남을 수 있다 — 그러면 거두는 쪽이 **못 보고 조용히
    // 지나간다.** 2026-09-19에 실제로 그랬다: 그 판에는 *"거뒀다"*가 한 줄도 없었는데 파낸 자리를 막은
    // **낡은 박스가 두 벌씩** 찍혔다(사용자: *"저번에 돌렸던 벽이 남아서 채굴하러 들어갈 수 없는 버그가
    // 아직 남아있어"*). `FindObjectsOfTypeAll`은 숨김·`DontSave`·씬 밖을 가리지 않는다. **에셋은 뺀다**
    // (프리팹 안의 같은 이름을 부수면 안 된다).
    static readonly List<GameObject> _rootsBuf = new();
    static List<GameObject> AllRoots()
    {
        _rootsBuf.Clear();
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.parent != null || t.name != RootName) continue;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(t)) continue;
#endif
            _rootsBuf.Add(t.gameObject);
        }
        return _rootsBuf;
    }

    public static bool IsRockCollider(Component c) =>
        c != null && ChunkRoot != null && c.transform.parent == ChunkRoot;

    Chunk Ensure(Vector2Int chunk)
    {
        SweepOrphans();

        if (_chunks.TryGetValue(chunk, out var c) && c.Go != null) return c;

        if (_root == null)
        {
            _root = new GameObject(RootName).transform;
            _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ChunkRoot = _root;

            // **루트를 새로 세웠다는 것은 지난 벌을 통째로 잃었다는 뜻이다** — 그 벌이
            // 씬에 그대로 서 있을 수 있으므로 여기서 거둔다.
            ReapStrayRoots(_root);
        }

        // **같은 이름의 청크가 이미 서 있으면 거둔다** — 위의 한 번 쓸기가 놓친 것을 받는
        // 그물이다. **여기서는 안 알린다**(쓸기가 이미 개수로 말했다 — 청크마다 한 줄씩
        // 찍으면 콘솔이 그것으로 덮인다).
        string chunkName = $"Rock_{chunk.x}_{chunk.y}";
        var stray = _root.Find(chunkName);
        if (stray != null) Toss(stray.gameObject);

        var go = new GameObject(chunkName);
        go.transform.SetParent(_root, false);
        go.layer = Mathf.Clamp(rockLayer, 0, 31);

        // **에디터에서 세운 것은 씬에 저장되지 않는다**(2026-09-12). 씬 뷰에서도 벽을
        // 보여 주게 되면서(`WorldSceneRock`) 이 길이 편집 중에도 돈다 — 안 걸면 수백
        // 청크가 `Scene1`에 박히고, 판이 열리면 런타임이 세운 것과 **두 벌**이 된다.
        if (!Application.isPlaying)
        {
            go.hideFlags     = HideFlags.DontSave;
            _root.gameObject.hideFlags = HideFlags.DontSave;
        }

        var mesh = new Mesh { name = go.name };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { TopMat(), SideMat(), MarkMat() };

        // **그림자를 안 던진다**(2026-09-07) — 천장 메시가 그랬던 것과 같은 이유이고,
        // 이제는 하나가 더 있다: **깎는 것은 포워드 패스뿐**이라 그림자 패스는 **깎지 않은
        // 24m 벽**을 굽는다. 남겨 두면 **보이지 않는 벽이 그림자를 던진다.**
        //
        // 지하에는 해가 없어 방향광 그림자가 정보를 나르지 않고, 24m 벽 수천 개가
        // 그림자맵에 들어가는 값도 만만치 않다. **잃는 것이 없다.**
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // **색조는 여기서 안 얹는다** — 광맥 서브메시 때문에 재질 슬롯 수가 굽기마다
        // 달라지므로, 재질을 갈아 끼운 **뒤에** `Rebuild`가 얹는다.
        c = new Chunk { Go = go, Mesh = mesh, Renderer = mr };

        // **천장은 판 안에서만** — 씬 뷰는 저작하는 눈이라 위가 트여 있어야 한다.
        if (Application.isPlaying)
        {
            var ceil = new GameObject("Ceiling");
            ceil.transform.SetParent(go.transform, false);
            ceil.layer = go.layer;
            c.CeilMesh = new Mesh { name = chunkName + "_Ceiling" };
            ceil.AddComponent<MeshFilter>().sharedMesh = c.CeilMesh;
            c.CeilRenderer = ceil.AddComponent<MeshRenderer>();
            c.CeilRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 바위와 같은 이유
            c.CeilRenderer.enabled = false;
        }

        _chunks[chunk] = c;

        return c;
    }

    // **벽도 바이옴을 따라간다**(2026-09-06). 굴에서 화면의 절반 이상은 벽이라, 바닥만
    // 바꾸면 *"바닥 색이 좀 다른 같은 굴"*로 보인다.
    //
    // **재질을 바이옴마다 만들지 않는다** — `MaterialPropertyBlock`으로 **색만** 얹는다.
    // 재질을 늘리면 여덟 벌을 손으로 맞추게 되고, 그중 하나는 반드시 뒤처진다.
    //
    // **윗면(서브메시 0)에는 절반 밝기로 건다**(2026-10-15). 09-06에는 옆면에만 걸었다 — 윗면은
    // *안 판 자리 = 어둠*이라 거의 검어야 판 곳과 안 판 곳이 갈렸다. 그 뒤로 굴 전체가 어두워져
    // (`UnderworldLighting`) 불빛 밖의 뚜껑은 조명이 검게 누르고, 단색 0.05는 **불빛 아래서도 검어서**
    // 잘린 벽의 단면이 무늬 없는 구멍으로 보였다(사용자). 판 곳과 갈리는 것은 이제 밝기의 절반(`TopShade`)이 맡는다.
    //
    // 찍힌 칸(서브메시 2)도 안 건다 — 그건 *"여기를 판다"*는 표시라 어느 바이옴에서든 같은
    // 주황이어야 한다. `MaterialPropertyBlock`은 **머티리얼 인덱스별로** 걸 수 있다.
    //
    // **색조는 텍스처에 곱해진다** — 그래서 값이 원래 밝기(0.72~0.80) 근처여야 하고,
    // 어둡게 넣으면 색이 아니라 **밝기만 내려간다**(그렇게 한 번 만들었다).
    //
    // 청크가 16m라 한 청크는 대개 한 지역(64m) 안이다 — **한복판에서 한 번만 묻는다.**
    // **`new`를 필드 초기화자에 두면 안 된다**(2026-09-06에 겪었다).
    //
    // 정적 필드 초기화자는 **정적 생성자**에서 도는데, 그것이 처음 불리는 시점은 이 클래스가
    // 처음 쓰이는 때 — 즉 **`MonoBehaviour`가 생성되는 도중**일 수 있다. 유니티는 그 자리에서
    // 엔진 객체를 만드는 것을 막는다:
    //
    //   `CreateImpl is not allowed to be called from a MonoBehaviour constructor
    //    (or instance field initializer), call it in Awake or Start instead.`
    //
    // 그리고 정적 생성자가 던지면 `TypeInitializationException`이라 **이 클래스가 통째로
    // 죽는다** — 화면에는 *"벽이 안 뜬다"*로만 보였다(원인이 색칠 한 줄이라는 표시가 없다).
    //
    // **처음 쓸 때 만든다.** `MaterialPropertyBlock`은 순수 관리 객체처럼 보이지만
    // 네이티브 자원을 잡는다 — `Mesh`·`Material`과 같은 취급을 해야 한다.
    static MaterialPropertyBlock _mpb;

    void TintForBiome(MeshRenderer mr, Vector2Int chunk)
    {
        if (MapManager.Instance == null) return;

        _mpb ??= new MaterialPropertyBlock();

        float half = ChunkTiles * 0.5f;
        var   at   = MapManager.Instance.TileToWorld(
                         new Vector2Int(Mathf.RoundToInt(chunk.x * ChunkTiles + half),
                                        Mathf.RoundToInt(chunk.y * ChunkTiles + half)));

        var tint = WorldMap.BiomeAtWorld(new Vector2(at.x, at.z)).RockTint();

        _mpb.Clear();
        _mpb.SetColor("_BaseColor", tint);
        _mpb.SetColor("_Color",     tint);   // 빌트인으로 떨어졌을 때
        mr.SetPropertyBlock(_mpb, 1);        // 옆면

        // **윗면도 같은 색조를 절반 밝기로**(2026-10-15) — 윗면에 무늬를 깔면서(`Rock_Top.mat`) 옆면만
        // 물들이면 위에서 볼 때 뚜껑과 벽이 다른 돌로 보인다. 색조가 재질 색을 덮어쓰므로 어둡게 하는
        // 몫(`TopShade`)도 여기서 같이 곱한다.
        var topTint = tint * TopShade;
        topTint.a = 1f;
        _mpb.Clear();
        _mpb.SetColor("_BaseColor", topTint);
        _mpb.SetColor("_Color",     topTint);
        mr.SetPropertyBlock(_mpb, 0);        // 윗면
    }

    // **윗면은 벽의 몇 배 밝기인가.** 재질을 만드는 도구(`World → 0b`)와 바이옴 색조가 같은 값을 쓴다.
    public const float TopShade = 0.5f;

    void Drop(Vector2Int chunk)
    {
        if (!_chunks.TryGetValue(chunk, out var c)) return;
        _chunks.Remove(chunk);

        if (c.Go != null) Toss(c.Go);
        if (c.Mesh != null) Toss(c.Mesh);   // **메시도 같이 버린다** — 안 버리면 샌다
        if (c.CeilMesh != null) Toss(c.CeilMesh);
    }

    // **버리는 문은 하나다** — `Destroy`는 **편집 모드에서 에러**이고(씬 뷰에도 벽을 세우면서
    // 이 길이 편집 중에 돈다), `DestroyImmediate`는 플레이 중에 쓰면 안 된다. 부르는 쪽마다
    // 갈라 적으면 그중 하나가 반드시 틀린 쪽을 부른다.
    static void Toss(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else                       DestroyImmediate(o);
    }

    // **안 판 자리는 어둠이다.** 완전한 0으로는 안 내린다 — 그러면 벽 윗면과 화면 배경이
    // 구별이 안 가 **세계의 경계가 어디인지**를 못 읽는다. 아주 어둡게 두면 안개와 섞여
    // *"저기까지는 바위다"*가 보인다.
    // **안 판 자리는 어둠이다.** 완전한 0으로는 안 내린다 — 그러면 벽 윗면과 화면 배경이
    // 구별이 안 가 **세계의 경계가 어디인지**를 못 읽는다.
    Material TopMat()
    {
        if (rockMaterial != null) return rockMaterial;
        if (_topMat != null) return _topMat;

        _topMat = Lit("Rock top (runtime)", new Color(0.52f, 0.57f, 0.60f) * TopShade);
        return _topMat;
    }

    // **파낸 방의 벽.** 참고 화면의 그 밝은 청회색 — 빛을 받는 면이라 방의 모양을 그린다.
    Material SideMat()
    {
        if (wallMaterial != null) return wallMaterial;
        if (_sideMat != null) return _sideMat;

        _sideMat = Lit("Rock wall (runtime)", new Color(0.52f, 0.57f, 0.60f));
        return _sideMat;
    }

    // **찍은 표시는 안개를 안 탄다**(2026-09-18) — 렌더 큐를 반투명(3000)에 둔다.
    //
    // 안개는 **불투명을 다 그린 뒤 반투명 앞에** 덮으므로(`FogOfWar.FogPass`), 큐가 뒤면
    // 그 위에 안 덮인다 — 월드보더 막·청사진이 이미 그 자리에 있다.
    //
    // **왜 필요한가**: 시야는 **첫 벽 한 겹만** 연다(그래야 봉한 굴이 안 들여다보인다).
    // 그런데 벽 **뚜껑**의 XZ는 그 벽 칸 자신이라, **두 칸째부터의 뚜껑은 구조적으로 영영
    // 안 가 본 곳**이다 — 위에서 내려다보면 보이는 것이 대개 뚜껑이므로, 5칸짜리 채굴 구역을
    // 찍으면 **맨 앞줄만 주황색이고 나머지는 검다.**
    //
    // **안개를 밝혀서는 못 푼다** — 안개는 **곱하기**라 표시와 벽이 같은 비율로 밝아지고,
    // 표시가 읽힐 만큼 올리면 **안 가 본 굴의 모양이 통째로 드러난다**(사용자가 정한
    // *"안 가 본 길 = 검은색"*이 그것으로 끝난다). 가려야 할 것은 **세계의 사실**이지
    // **내가 그린 계획**이 아니다(`PawnMark`가 *"폰의 것이 아니라 화면의 것"*인 것과 같은 자리).
    const int MarkQueue = 3000;   // UnityEngine.Rendering.RenderQueue.Transparent

    Material MarkMat()
    {
        if (markedMaterial != null)
        {
            // **큐는 저작이 아니라 규격이라 코드가 쥔다** — 색·결은 재질이 정하지만 *안개 앞에
            // 서는가*는 고를 수 있는 값이 아니다. 경고만 하면 **밖에서 `.mat`을 고친 것이
            // 유니티 저장 한 번에 되돌아가** 같은 자리가 조용히 되살아난다(2026-09-18에 실제로
            // 그랬다 — 디스크에는 3000인데 화면에서는 2000이었다).
            if (markedMaterial.renderQueue != MarkQueue) markedMaterial.renderQueue = MarkQueue;
            return markedMaterial;
        }
        if (_markMat != null) return _markMat;

        _markMat = Lit("Rock marked (runtime)", new Color(1.00f, 0.74f, 0.24f));

        // **스스로 빛나게 한다** — 어두운 굴에서 색만 바꾸면 주변광에 묻혀 티가 안 난다.
        // **키워드를 같이 켜야** URP가 그 패스를 돈다(색만 넣으면 아무 일도 안 일어난다).
        _markMat.EnableKeyword("_EMISSION");
        _markMat.SetColor("_EmissionColor", new Color(0.95f, 0.55f, 0.08f));
        _markMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        _markMat.renderQueue = MarkQueue;
        return _markMat;
    }


    static Material Lit(string name, Color color)
    {
        // **사본 → URP → 빌트인 순이다**(지형과 같은 규칙) — 사본이 컴파일에 실패해도
        // 세상이 분홍색이 되지는 않는다. 깎기만 사라지고 벽은 제 높이로 선다.
        var sh = RuntimeShaders.Find(RuntimeShaders.RockLit, RuntimeShaders.Lit,
                                     RuntimeShaders.Standard);
        var m = new Material(sh) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color",     color);   // 빌트인으로 떨어졌을 때
        m.SetFloat("_Smoothness", 0.02f);  // 젖은 광택이 나면 저폴리 느낌이 깨진다
        m.SetFloat("_Metallic",   0f);

        // **양면으로 그린다**(2026-08-29). 벽이 **투명하게 보이던 원인**이다 — 면마다 네
        // 모서리를 손으로 적었는데 방향에 따라 감김이 뒤집혀서, 뒤집힌 면은 뒷면 컬링에
        // 걸려 **통째로 안 그려졌다.** 그 자리는 벽이 아니라 구멍으로 보인다.
        //
        // 감김을 하나씩 맞추는 길도 있지만, **면이 여섯 방향이고 새 면을 추가할 때마다 또
        // 틀린다.** 바위는 속이 꽉 찬 물건이라 양면으로 그려도 잃는 것이 없다.
        m.SetFloat("_Cull", 0f);                    // 0 = Off (양면)
        m.doubleSidedGI = true;
        return m;
    }
}
