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
    [Tooltip("바위 **윗면**. 비우면 거의 검게 만든다 — 안 판 자리가 곧 어둠이다.")]
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
    void PushCutUniforms()
    {
        if (!cutNearWalls) { Shader.SetGlobalFloat(IdOn, 0f); return; }

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

    // ── 청크가 들고 나는 값을 잰다 ──────────────────────────────────────────
    //
    // **손질이 값을 하는지는 숫자가 답한다**(`WideGraph`가 덩어리 수·간선 수·ms를 찍는 것과
    // 같은 자리). 2026-09-12에 풀링을 **안 재고 넣었다가** 되돌린 적이 있다 — 그때 알고
    // 싶었던 것이 정확히 이 넷이다: **몇 장이 생기고 몇 장이 사라지나 · 만드는 데 얼마나
    // 걸리나 · 굽는 데 얼마나 걸리나.**
    //
    // 풀링이 줄이는 것은 **만드는 값**뿐이다(`new GameObject` + `AddComponent` + `new Mesh`).
    // **굽는 값은 그대로다** — 정점을 다시 채우는 일은 풀이 있든 없든 돈다. 그래서 굽는 값이
    // 만드는 값을 압도하면 **풀은 잡음이다.**
    [Header("진단")]
    [Tooltip("청크가 들고 나는 값을 1초마다 찍는다. 풀링 같은 손질을 재려면 이것부터 켠다.")]
    [SerializeField] bool churnDebug;

    static readonly System.Diagnostics.Stopwatch _churnSw = new();
    int    _made, _dropped, _frameMade, _frameDropped, _peakMade, _peakDropped;
    double _makeMs, _bakeMs;
    float  _churnAt;

    readonly List<Vector3> _verts = new();
    readonly List<Vector3> _norms = new();
    readonly List<Vector2> _uvs   = new();
    readonly List<int>     _top   = new();   // 윗면 — 검다
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

    // 알갱이를 어디에 몇 개 박나. **눈높이 아래에 둔다** — 벽은 24m인데 `RockCut`이
    // 눈높이의 비율로 뚜껑을 내리므로(`cutLow` 2.5m가 바닥) 위쪽에 박으면 **잘려서 안 보인다.**
    const int   OreSpecks = 5;
    const float OreBand   = 2.2f;    // 바닥에서 이 높이까지
    const float OreOut    = 0.03f;   // 벽면에서 이만큼 띄운다(z-파이팅)

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
        if (!RockMap.Enabled || startClearing <= 0f) return;

        int opened = 0;
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
        TerrainStreamer.ChunkLoaded   += OnTerrainChunk;
        TerrainStreamer.ChunkUnloaded += OnTerrainGone;

        foreach (var c in TerrainStreamer.LiveChunks) OnTerrainChunk(c);
    }

    void OnDisable()
    {
        RockMap.OnChanged        -= OnRockChanged;
        TerrainStreamer.ChunkLoaded   -= OnTerrainChunk;
        TerrainStreamer.ChunkUnloaded -= OnTerrainGone;

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

        if (churnDebug) ReportChurn();
    }

    // **한 프레임에 몰리는 것이 값이다** — 누계만 보면 *"초당 서른 장"*이 고르게 퍼진 것처럼
    // 읽히는데, 실제로는 지형 청크가 내려가는 프레임에 **열여섯 장씩** 몰린다.
    void ReportChurn()
    {
        if (_frameMade    > _peakMade)    _peakMade    = _frameMade;
        if (_frameDropped > _peakDropped) _peakDropped = _frameDropped;
        _frameMade = _frameDropped = 0;

        _churnAt += Time.unscaledDeltaTime;
        if (_churnAt < 1f) return;
        _churnAt = 0f;

        if (_made == 0 && _dropped == 0) return;   // 가만히 서 있으면 조용하다

        Debug.Log($"[바위] 초당 만듦 {_made}장({_makeMs:F1}ms) · 버림 {_dropped}장 · " +
                  $"구움 {_bakeMs:F1}ms · 최악 프레임 만듦 {_peakMade}/버림 {_peakDropped} · " +
                  $"살아 있는 청크 {_chunks.Count}\n" +
                  $"풀링이 줄이는 것은 **만드는 값**뿐이다 — 굽는 값({_bakeMs:F1}ms)과 견줄 것.");

        _made = _dropped = _peakMade = _peakDropped = 0;
        _makeMs = _bakeMs = 0d;
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    void Rebuild(Vector2Int chunk)
    {
        double bakeAt = churnDebug ? Now() : 0d;

        _verts.Clear(); _norms.Clear(); _uvs.Clear(); _top.Clear(); _side.Clear(); _mark.Clear();
        _oreKinds.Clear();

        var map = MapManager.Instance;
        int x0 = chunk.x * ChunkTiles, y0 = chunk.y * ChunkTiles;

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

            float floor = RockMap.SurfaceOf(tile);   // **`MapManager` 없이도 답한다**(씬 뷰)
            float top   = floor + RockMap.Height;

            if (floor < lowest)  lowest  = floor;
            if (floor > highest) highest = floor;

            // 칸의 네 모서리(월드). **격자 위상을 얹는다** — 안 얹으면 벽이 반 칸 어긋난다.
            Vector3 c = map != null ? map.TileToWorld(tile)
                                    : new Vector3(tile.x + 0.5f, 0f, tile.y + 0.5f);
            float xa = c.x - 0.5f, xb = c.x + 0.5f;
            float za = c.z - 0.5f, zb = c.z + 0.5f;

            // 윗면 — 늘 그린다.
            Quad(marked ? _mark : _top,
                 new Vector3(xa, top, za), new Vector3(xa, top, zb),
                 new Vector3(xb, top, zb), new Vector3(xb, top, za), Vector3.up);

            // 옆면 — **뚫린 이웃 쪽만.**
            var sideList = marked ? _mark : _side;
            bool wL = Side(sideList, tile + Vector2Int.left,  xa, za, xa, zb, floor, top, Vector3.left);
            bool wR = Side(sideList, tile + Vector2Int.right, xb, zb, xb, za, floor, top, Vector3.right);
            bool wD = Side(sideList, tile + Vector2Int.down,  xb, za, xa, za, floor, top, Vector3.back);
            bool wU = Side(sideList, tile + Vector2Int.up,    xa, zb, xb, zb, floor, top, Vector3.forward);

            // **광맥은 드러난 옆면에만 박힌다.** 찍힌 칸에는 안 그린다 — *"여기를 판다"*가
            // 그 칸의 전부라, 거기서 또 갈라 봐야 색만 늘고 읽히지는 않는다(윗면·옆면을
            // 한 벌로 보내는 것과 같은 규칙).
            if (marked || !(wL || wR || wD || wU)) continue;

            var vein = RockMap.VeinAt(tile);
            if (vein == null || !vein.IsVein) continue;

            var ore = OreList(vein);
            if (ore == null) continue;                      // 종류가 천장을 넘었다

            if (wL) Specks(ore, tile, 0, xa, za, xa, zb, floor, Vector3.left);
            if (wR) Specks(ore, tile, 1, xb, zb, xb, za, floor, Vector3.right);
            if (wD) Specks(ore, tile, 2, xb, za, xa, za, floor, Vector3.back);
            if (wU) Specks(ore, tile, 3, xa, zb, xb, zb, floor, Vector3.forward);
        }

        if (_top.Count == 0 && _side.Count == 0 && _mark.Count == 0) { Drop(chunk); return; }

        var ch = Ensure(chunk);
        var mesh = ch.Mesh;
        mesh.Clear();
        mesh.indexFormat = _verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(_verts);
        mesh.SetNormals(_norms);
        mesh.SetUVs(0, _uvs);
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
        var b = mesh.bounds;
        b.SetMinMax(new Vector3(b.min.x, lowest - CutBoundsSlack, b.min.z), b.max);
        mesh.bounds = b;

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

        // **굽는 값에는 만드는 값이 들어 있다**(`Ensure`가 그 안이다) — 빼서 재면 풀이
        // 줄일 몫과 그대로 남을 몫이 갈린다.
        if (churnDebug) _bakeMs += Now() - bakeAt;
    }

    static double Now() => System.Diagnostics.Stopwatch.GetTimestamp()
                         * 1000d / System.Diagnostics.Stopwatch.Frequency;

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
    bool Side(List<int> list, Vector2Int neighbor,
              float x1, float z1, float x2, float z2, float floor, float top, Vector3 n)
    {
        if (RockMap.IsRock(neighbor)) return false;   // 맞닿은 바위 — 안 보인다

        // 이웃 바닥이 더 낮으면 거기까지 내려 세운다. 안 내리면 **벽 밑이 뚫려 보인다.**
        float bottom = Mathf.Min(floor, RockMap.SurfaceOf(neighbor));

        Quad(list,
             new Vector3(x1, bottom, z1), new Vector3(x1, top, z1),
             new Vector3(x2, top, z2),    new Vector3(x2, bottom, z2), n);
        return true;
    }

    // ── 벽면에 광석 알갱이 ──────────────────────────────────────────────────
    //
    // 면을 통째로 물들이지 않는다 — **박힌 것**으로 보여야 *"이 벽에 구리가 있다"*로 읽히고,
    // 면을 칠하면 *바이옴이 바뀐 것*처럼 보인다(벽 색조가 이미 그 축을 쓰고 있다).
    //
    // **자리는 좌표 해시다** — 저장하는 것이 없고, 청크를 다시 구워도 알갱이가 안 움직인다
    // (뿌리기·광맥 심기와 같은 규칙). 움직이면 한 칸을 팔 때마다 옆 벽의 무늬가 흔들린다.
    void Specks(List<int> list, Vector2Int tile, int dir,
                float x1, float z1, float x2, float z2, float floor, Vector3 n)
    {
        float tx = x2 - x1, tz = z2 - z1;           // 면을 따라가는 방향(길이 1m)
        float baseY = floor + 0.12f;

        for (int k = 0; k < OreSpecks; k++)
        {
            float u = 0.14f + Hash(tile, dir, k * 3 + 0) * 0.72f;
            float y = baseY + Hash(tile, dir, k * 3 + 1) * OreBand;
            float h = 0.10f + Hash(tile, dir, k * 3 + 2) * 0.16f;   // 반쪽 크기

            float cx = x1 + tx * u + n.x * OreOut;
            float cz = z1 + tz * u + n.z * OreOut;

            float ax = tx * h, az = tz * h;         // 면을 따라 +-h
            Quad(list,
                 new Vector3(cx - ax, y - h, cz - az), new Vector3(cx - ax, y + h, cz - az),
                 new Vector3(cx + ax, y + h, cz + az), new Vector3(cx + ax, y - h, cz + az), n);
        }
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

    static Material OreMat(ResourceNodeData vein)
    {
        if (_oreMats.TryGetValue(vein, out var m) && m != null) return m;

        m = Lit("Vein " + vein.name + " (runtime)", vein.VeinColor);
        m.SetFloat("_Metallic",   vein.VeinMetallic);
        m.SetFloat("_Smoothness", vein.VeinSmoothness);

        // **어두운 굴에서는 색만으로는 안 읽힌다** — 빛 배율이 어느 때에도 1이 아니라
        // (0.30~0.62) 알갱이가 벽과 함께 눌린다. 아주 약하게 스스로 빛나게 두면 *박힌 것*이
        // 그대로 보이고, **빛나는 광물은 더 세게** 빛난다(미스릴·마법 결정).
        //
        // **키워드를 같이 켜야 한다** — 색만 넣으면 URP가 조용히 그 패스를 안 돈다.
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", vein.VeinColor * (vein.VeinGlow ? 0.9f : 0.22f));
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        _oreMats[vein] = m;
        return m;
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
        _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
        _norms.Add(n); _norms.Add(n); _norms.Add(n); _norms.Add(n);
        Uv(a, n); Uv(b, n); Uv(c, n); Uv(d, n);
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

    public static bool IsRockCollider(Component c) =>
        c != null && ChunkRoot != null && c.transform.parent == ChunkRoot;

    Chunk Ensure(Vector2Int chunk)
    {
        if (_chunks.TryGetValue(chunk, out var c) && c.Go != null) return c;

        if (_root == null)
        {
            _root = new GameObject("RockChunks").transform;
            _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            ChunkRoot = _root;
        }

        if (churnDebug) _churnSw.Restart();

        var go = new GameObject($"Rock_{chunk.x}_{chunk.y}");
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
        _chunks[chunk] = c;

        if (churnDebug)
        {
            _churnSw.Stop();
            _makeMs += _churnSw.Elapsed.TotalMilliseconds;
            _made++; _frameMade++;
        }
        return c;
    }

    // **벽도 바이옴을 따라간다**(2026-09-06). 굴에서 화면의 절반 이상은 벽이라, 바닥만
    // 바꾸면 *"바닥 색이 좀 다른 같은 굴"*로 보인다.
    //
    // **재질을 바이옴마다 만들지 않는다** — `MaterialPropertyBlock`으로 **색만** 얹는다.
    // 재질을 늘리면 여덟 벌을 손으로 맞추게 되고, 그중 하나는 반드시 뒤처진다.
    //
    // **옆면(서브메시 1)에만 건다**(2026-09-06에 바로잡았다).
    //
    // 윗면은 *안 판 자리 = 어둠*이라 거의 검어야 **세계의 경계가 어디인지**가 읽힌다
    // (기본색이 0.05~0.07인 이유). 거기까지 물들이면 **안 판 바위가 밝아져** 판 곳과
    // 안 판 곳이 한 덩어리로 보인다.
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
        mr.SetPropertyBlock(_mpb, 1);        // 옆면만
    }

    void Drop(Vector2Int chunk)
    {
        if (!_chunks.TryGetValue(chunk, out var c)) return;
        _chunks.Remove(chunk);
        if (churnDebug) { _dropped++; _frameDropped++; }

        if (c.Go != null) Toss(c.Go);
        if (c.Mesh != null) Toss(c.Mesh);   // **메시도 같이 버린다** — 안 버리면 샌다
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

        _topMat = Lit("Rock top (runtime)", new Color(0.05f, 0.06f, 0.07f));
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

    Material MarkMat()
    {
        if (markedMaterial != null) return markedMaterial;
        if (_markMat != null) return _markMat;

        _markMat = Lit("Rock marked (runtime)", new Color(1.00f, 0.74f, 0.24f));

        // **스스로 빛나게 한다** — 어두운 굴에서 색만 바꾸면 주변광에 묻혀 티가 안 난다.
        // **키워드를 같이 켜야** URP가 그 패스를 돈다(색만 넣으면 아무 일도 안 일어난다).
        _markMat.EnableKeyword("_EMISSION");
        _markMat.SetColor("_EmissionColor", new Color(0.95f, 0.55f, 0.08f));
        _markMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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
