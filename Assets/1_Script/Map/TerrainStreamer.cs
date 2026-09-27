using System.Collections.Generic;
using UnityEngine;

// **보는 곳 둘레의 청크만 세워 둔다**(2026-08-26).
//
// 세계가 29,440m라 지형을 통째로 올릴 수 없다. 정점만 2억 1,700만 개이고, 그보다 무거운 것은
// **콜라이더**다. 그래서 뷰어 둘레 `radius` 칸만 `Terrain`을 만들고 나머지는 **오브젝트가
// 아예 없다**.
//
// **`Terrain`을 데이터가 아니라 렌더러로 쓴다** — 높이의 원본은 `TerrainHeight`(노이즈 + 델타)이고,
// 여기서 만드는 `TerrainData`는 **그 값을 그리기 위한 그릇**이다. 청크를 버리면 같이 버린다.
// 덕분에 저장되는 것은 손댄 델타뿐이고, `SampleGroundHeight`는 이 스트리머가 무엇을 올렸든
// **상관없이 답한다**.
//
// ── 높이 정규화는 세계 전체가 한 벌을 쓴다 ──────────────────────────────────
//
// `TerrainData`의 높이는 `size.y`에 대한 0~1 값이다. 청크마다 제 최소/최대로 정규화하면
// **이웃과 눈금이 달라져 경계에서 지형이 어긋난다.** 그래서 `MinHeight`/`HeightRange`를
// 세계 상수로 두고 모든 청크가 같은 자를 쓴다. 16비트 높이맵이라 300m 범위에서 **4.6mm**
// 해상도다 — 넉넉하다.
[DefaultExecutionOrder(-60)]   // MapManager·길찾기가 지면을 묻기 전에 선다
public class TerrainStreamer : MonoBehaviour
{
    public const float MinHeight   = -100f;
    public const float HeightRange = 300f;

    // **프로필은 씬이 든다** — `TerrainHeight.Profile`이 static이라 어디선가 한 번 넣어 줘야
    // 하는데, 그 자리를 코드에 두면 **저작할 수가 없다.** 저작 도구도 여기서 읽어 가므로
    // 에디터에서 조각한 지형과 게임에서 도는 지형이 **같은 값**을 쓴다.
    [Header("지형 성격")]
    [SerializeField] TerrainProfile profile = TerrainProfile.Default;

    [Tooltip("저작한 산·분지. 노이즈 위에 얹힌다 — 좌표로 적으므로 저장할 것이 없다.")]
    [SerializeField] TerrainFeature[] features = System.Array.Empty<TerrainFeature>();

    [Tooltip("바이옴마다 무엇을 깔 것인가. 비우면 지형이 머티리얼 한 장으로만 보인다 — " +
             "**걸어 다녀도 사막과 설원이 구별되지 않는다.**")]
    [SerializeField] TerrainPalette palette;

    [Tooltip("저작한 높이맵. 비우면 큰 모양을 펄린 한 층이 만든다 — **물길도 능선도 없다.** " +
             "`Terrain → B. Bake Eroded Heightmap`이 굽는다.")]
    [SerializeField] TerrainHeightmap heightmap;

    // ── 천장 — 지하로 내려가는 축 ───────────────────────────────────────────
    //
    // **프로필을 여기 두는 이유는 지형과 같다** — `TerrainCeiling`이 static이라 어디선가 한 번
    // 넣어 줘야 하고, 그 자리를 코드에 두면 저작할 수가 없다. **바닥과 천장을 한 컴포넌트가
    // 드는 것이 요점이다**: 둘은 여유(천장 − 바닥)로 맞물려 있어서 따로 저작하면 어느 씬은
    // 바닥만 있고 어느 씬은 천장만 있는 상태가 생긴다.
    [Header("천장 (지하)")]
    [Tooltip("켜면 세계에 천장이 덮이고 **머리 위 여유가 없는 칸은 못 지나간다.** " +
             "끄면 지상 그대로다 — 판정이 한 줄도 안 바뀐다.\n\n" +
             "**높이맵을 같이 떼는 것을 잊지 말 것**: 저작된 높이맵은 지상 침식이라 " +
             "200m 산이 서 있고, 거기에 천장을 덮으면 굴이 아니라 **산을 감싼 비닐**이 된다.")]
    [SerializeField] bool ceiling = true;

    [SerializeField] TerrainCeilingProfile ceilingProfile = TerrainCeilingProfile.Default;

    [Tooltip("저작한 방(+)과 기둥(−). 지형 특징과 **같은 타입**이라 같은 도구로 놓는다 — " +
             "다만 여기서 얹는 값은 높이가 아니라 **여유**다.")]
    [SerializeField] TerrainFeature[] ceilingFeatures = System.Array.Empty<TerrainFeature>();

    public bool                   CeilingOn       => ceiling;
    public TerrainCeilingProfile  CeilingProfile  => ceilingProfile;
    public TerrainFeature[]       CeilingFeatures => ceilingFeatures;

    public TerrainProfile   Profile  => profile;
    public TerrainFeature[] Features => features;
    public TerrainPalette   Palette  => palette;
    public TerrainHeightmap Heightmap => heightmap;

    void Awake()
    {
        // ── 판이 열릴 때 런타임 상태를 비운다 (2026-09-24) ───────────────────
        //
        // 캔 칸(`RockMap._mined`)·고른 땅(`TerrainHeight._leveled`)·찍어 둔 표시는 **static이라
        // 플레이를 나갔다 들어와도 살아남는다**(도메인 리로드를 끈 설정에서 실제로 남고,
        // 에디터가 컴파일하지 않은 채 다시 들어가면 기본 설정에서도 남는다).
        //
        // **`RockMap.Clear`는 *"판을 새로 열 때"*라고 적어 두고 부르는 자리가 없었다** —
        // 도메인 리로드가 대신 해 주고 있었을 뿐이라, 그 전제가 바뀌는 날 지난 판에서 판
        // 굴이 새 판에 남는다. 고른 땅을 여기서만 비우면 **캔 칸은 남고 땅은 돌아오는**
        // 반쪽 상태가 되므로 셋을 같이 비운다.
        //
        // **`Apply`가 아니라 `Awake`다** — `Apply`는 `OnValidate`도 부르므로, 거기 두면
        // 인스펙터를 만질 때마다 고른 땅이 사라진다.
        RockMap.Clear();
        TerrainHeight.ForgetLeveled();
        GroundLevel.Clear();
        Chasm.ClearRuntime();

        if (Application.isPlaying) DropEditorPreview();

        Apply();
    }

    // ── 에디터가 띄운 미리보기 지형은 판에 들이지 않는다 (2026-09-26) ─────────
    //
    // `Authoring → Show Terrain Here`가 씬 뷰에 3×3 지형을 띄우는데(**콜라이더까지 단다** — 프리팹을 딛고 놓으려고),
    // 그것이 씬에 저장된 채 Play에 들어오면 **런타임 청크와 겹쳐 선다.** 게다가 띄운 그때의 높이로 구워져 있어
    // 코드가 높이를 바꾸면(이날 계단 이완) 그대로 뒤처진다 — 폰이 청크 경계(z = 192)에서 **함수는 0m인데 콜라이더는
    // 2m인 벽**에 막혀 섰다(`Chunk_2_2` ↔ `Preview_2_3`, 이동진단의 *"발밑 지형"* 줄이 갈랐다).
    // 이제 판이 열릴 때 치우고 알린다. 띄우는 쪽도 씬에 안 남게 한다(`WorldSiteAuthoring.ShowHere`).
    public const string EditorPreviewRoot = "__TerrainPreview";

    static void DropEditorPreview()
    {
        var root = GameObject.Find(EditorPreviewRoot);
        if (root == null) return;
        Debug.LogWarning($"[지형] 에디터 미리보기 지형(`{EditorPreviewRoot}`)이 씬에 남아 있어 판에서 치웠다 — " +
                         "런타임 청크와 겹쳐 옛 높이의 벽이 된다. 씬에서 `Authoring → Hide Terrain`으로 지우고 저장할 것.");
        Destroy(root);
    }

    void OnDestroy()
    {
        // **끄면 예전으로 돌아간다** — 남겨 두면 절차 지형을 안 쓰는 씬에서 폰이 이유 없이 언다.
        SimLocality.Active = false;
    }

    // 인스펙터에서 값을 만지면 즉시 반영 — 조각하며 보는 지형이 안 뒤처지게.
    void OnValidate() => Apply();

    void Apply()
    {
        TerrainHeight.Profile  = profile;
        TerrainHeight.Features = features ?? System.Array.Empty<TerrainFeature>();
        TerrainHeight.Heightmap = heightmap;
        TerrainSplat.Palette   = palette;
        if (palette != null) palette.PushToShader();
        TerrainHeight.ForgetDeltas();
        TerrainHeight.ForgetBiomes();   // 지역 격자도 다시 읽는다

        TerrainCeiling.Enabled  = ceiling;
        TerrainCeiling.Profile  = ceilingProfile;
        TerrainCeiling.Features = ceilingFeatures ?? System.Array.Empty<TerrainFeature>();
        TerrainCeiling.ForgetDeltas();

        SimLocality.MaxJobDistance = maxJobDistance;
    }

    [Header("범위")]
    [Tooltip("뷰어를 중심으로 이 칸 수만큼 둘레를 세운다. 2면 5×5 = 25청크(1,280m).")]
    [SerializeField, Range(1, 6)] int radius = 2;

    [Tooltip("한 프레임에 새로 세우는 청크 수. **콜라이더 굽기가 제일 무겁다** — " +
             "여러 개를 한꺼번에 세우면 걸어갈 때마다 뚝뚝 끊긴다.")]
    [SerializeField, Range(1, 4)] int buildsPerFrame = 1;

    [Tooltip("한 프레임에 버리는 청크 수. **버리는 것도 공짜가 아니다** — 청크마다 뿌린 " +
             "나무·바위가 딸려 있어서, 카메라를 멀리 옮기면 스물다섯 청크 × 수십 개가 " +
             "**한 프레임에 통째로 파괴된다**. 걸어 다닐 때는 한 프레임에 한둘이라 " +
             "이 값이 뭐든 티가 안 나고, 순간이동할 때만 갈린다.")]
    [SerializeField, Range(1, 8)] int unloadsPerFrame = 2;

    [Header("시뮬 지역성")]
    [Tooltip("이 반경(m) 안의 **남의 폰**만 BT를 돌린다. 플레이어 세력은 거리를 안 본다 — " +
             "원정 나간 팀이 얼어붙으면 안 된다. 지형 반경보다 작으면 지형 반경을 쓴다.")]
    [SerializeField] float simRadius = 1200f;

    [Tooltip("일감이 폰에게서 이만큼(m)보다 멀면 안 준다. 0이면 무제한.")]
    [SerializeField] float maxJobDistance = 600f;

    [Header("보는 곳")]
    [Tooltip("비우면 `Camera.main`. 카메라 자리의 XZ를 쓴다 — 기울여 봐도 반경이 넉넉해 덮인다.")]
    [SerializeField] Transform viewer;

    [Header("그리기")]
    [Tooltip("비우면 URP 지형 셰이더로 하나 만든다.")]
    [SerializeField] Material terrainMaterial;

    // **레이어는 인스펙터에서 고른다.** `BuildModeManager.groundMask`가 이 프로젝트에서는
    // `Ground`로 좁혀져 있어서, 청크가 다른 레이어면 **건설 커서가 땅을 못 찾는다** —
    // 증상이 "지형은 보이는데 건물을 못 놓는다"라 원인을 짚기 어렵다.
    //
    // 숫자를 그냥 두면 9가 뭔지 외워야 하고, **문자열로 두면 오타와 이름 변경에 조용히
    // 깨진다.** 드롭다운으로 고르고 인덱스를 저장하는 것이 셋 중 유일하게 안 깨지는 길이다.
    [Layer]
    [Tooltip("청크가 놓일 레이어. 땅 클릭(건설·이동)의 groundMask가 이 레이어를 받아야 한다.")]
    [SerializeField] int terrainLayer = 3;   // Ground

    readonly Dictionary<Vector2Int, Terrain> _live = new();
    readonly List<Vector2Int> _wanted = new();
    readonly List<Vector2Int> _stale  = new();

    // **비교자를 미리 만들어 둔다.** 람다를 그 자리에 적으면 `center`를 잡아채므로 **프레임마다
    // 클로저와 델리게이트가 새로 생긴다** — 프로파일러에서 GC 할당 1위가 `TerrainStreamer.Update`로
    // 찍힌 자리가 여기다(하는 일은 스물다섯 개를 정렬하는 것뿐인데).
    Vector2Int _sortCenter;
    System.Comparison<Vector2Int> _byDistance;

    // **청크 목록의 주인은 여기 하나다.** 나무·바위·마을을 스트리밍하는 쪽이 반경 계산을
    // 따로 하면 **둘의 답이 어긋나** 지형은 있는데 나무가 없는(또는 그 반대) 칸이 생긴다.
    // 그쪽은 이 소식만 듣는다.
    public static event System.Action<Vector2Int> ChunkLoaded;
    public static event System.Action<Vector2Int> ChunkUnloaded;

    // 이미 서 있는 청크 — 스트리머가 나중에 켜진 쪽도 따라잡을 수 있게.
    public static IEnumerable<Vector2Int> LiveChunks =>
        _instance != null ? _instance._live.Keys : System.Linq.Enumerable.Empty<Vector2Int>();

    // **그 자리에 지형이 실제로 서 있나**(2026-08-30). 콜라이더가 있느냐를 묻는 것과 같다 —
    // 스트리밍은 **카메라 둘레만** 세우므로, 카메라에서 먼 폰의 발밑에는 아무것도 없다.
    // 그걸 모르면 폰이 중력에 떨어지다 낙하 그물에 걸려 **제자리로 되돌려진다.**
    //
    // **높이·바위와는 다른 물음이다** — 그쪽은 함수라 로드와 무관하게 답이 나온다. 여기서
    // 묻는 것은 오직 *"물리가 받쳐 주나"*다.
    // **스트리머가 없으면 참이다** — 절차 지형을 안 쓰는 씬에서는 지형이 늘 서 있다.
    // 여기서 거짓을 돌려주면 그런 씬의 폰이 전부 추상 이동으로 떨어진다.
    public static bool HasGroundAt(Vector3 world) =>
        _instance == null || _instance._live.ContainsKey(TerrainGrid.ChunkAt(world));

    static TerrainStreamer _instance;

    // **청크를 이 컴포넌트의 자식으로 붙이지 않는다.** 붙이면 이 오브젝트의 회전·스케일이
    // 청크에 그대로 곱해져 **세계가 통째로 기울거나 커진다** — 그러면 "아무 오브젝트에나
    // 붙이면 된다"가 거짓이 되고, 어긋난 이유가 지형 코드가 아니라 **부모의 트랜스폼**이라
    // 찾기도 어렵다. 자기 루트를 하나 세워 쓴다.
    Transform _root;

    Transform Root
    {
        get
        {
            if (_root == null)
            {
                _root = new GameObject("TerrainChunks").transform;
                _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _root.localScale = Vector3.one;
            }
            return _root;
        }
    }

    void Update()
    {
        var viewer = ViewerPos();

        // **폰이 도는 곳과 지형이 서는 곳을 같은 축에 둔다**(`SimLocality`). 따로 재면
        // *"지형은 있는데 적이 얼어 있는"* 칸이나 그 반대가 생긴다.
        SimLocality.Active = true;
        SimLocality.Center = viewer;
        SimLocality.Radius = Mathf.Max(simRadius, (radius + 0.5f) * TerrainGrid.ChunkSize);

        var center = TerrainGrid.ChunkAt(viewer);

        // ① 있어야 할 것 모으기
        _wanted.Clear();
        for (int dz = -radius; dz <= radius; dz++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            var c = new Vector2Int(center.x + dx, center.y + dz);
            if (TerrainGrid.InWorld(c)) _wanted.Add(c);
        }

#if UNITY_EDITOR
        AddSceneViewChunks(center);
#endif

        // ② 벗어난 것 버리기 — **먼저 버린다.** 나중에 버리면 잠깐 둘 다 올라가 있다.
        //
        // **여기도 한 프레임에 몇 개만이다.** 걸어 다닐 때는 한둘씩이라 티가 안 나지만,
        // 카메라를 멀리 옮기면 **스물다섯이 한꺼번에 벗어난다** — 청크마다 뿌린 나무·바위가
        // 딸려 있어서 수백 개가 한 프레임에 파괴되고, 그게 순간이동할 때의 그 끊김이다.
        //
        // 남은 것은 다음 프레임에 다시 걸린다(`_live`에 그대로 있으므로). 그동안 몇 청크가
        // 더 올라가 있지만, 이미 화면 밖이라 그리는 비용은 없고 메모리만 잠깐 는다.
        _stale.Clear();
        foreach (var kv in _live)
        {
            if (_wanted.Contains(kv.Key)) continue;
            _stale.Add(kv.Key);
            if (_stale.Count >= unloadsPerFrame) break;
        }
        foreach (var c in _stale) Unload(c);

        // ③ 없는 것 세우기 — **한 프레임에 몇 개만.** 가운데부터.
        int budget = buildsPerFrame;
        _sortCenter = center;
        _byDistance ??= (a, b) => Sqr(a - _sortCenter).CompareTo(Sqr(b - _sortCenter));
        _wanted.Sort(_byDistance);
        foreach (var c in _wanted)
        {
            if (budget <= 0) break;
            if (_live.ContainsKey(c)) continue;
            Build(c);
            budget--;
        }

        if (_stale.Count > 0 || budget < buildsPerFrame) Reconnect();
    }

    static int Sqr(Vector2Int v) => v.x * v.x + v.y * v.y;

#if UNITY_EDITOR
    // ── 판 중에 씬 뷰로 둘러볼 때 (2026-09-28) ──────────────────────────────
    //
    // 사용자: *"게임을 실행한 상태에서 … 씬에서 뭔가를 조절해보고 싶어서 카메라를 옮기는거야 메인 카메라말고
    // 그냥 씬 카메라"* — **씬 뷰로 가면 땅이 없었다.** 여기는 메인 카메라 둘레만 세우고, 세계가 1,024m ·
    // 청크 64m가 된 뒤로 그 둘레가 세계의 일부뿐이다(청크가 256m였을 때는 반경만으로 세계가 거의 다 덮여
    // *"원래는 됐던"* 것처럼 보였다).
    //
    // **씬 뷰 둘레도 세운다 — 에디터에서만.** 땅이 서면 벽·안개·뿌린 것이 `ChunkLoaded`를 듣고 따라온다.
    // **판정 축은 안 옮긴다** — `SimLocality`(누가 도나)는 여전히 메인 카메라다. 둘러보려고 씬 뷰를 옮겼다고
    // 거기 적이 깨어나면 안 된다.
    //
    // **씬 뷰를 만지는 동안만** — 게임 뷰를 누르면 내려놓는다. 씬 뷰의 초점만 보면 인스펙터에서 값을 고치는
    // 순간 땅이 사라진다(그게 사용자가 씬 뷰로 간 이유다).
    [Tooltip("에디터에서 판이 도는 동안 씬 뷰가 보는 곳 둘레에도 땅을 세운다(판정은 여전히 메인 카메라).")]
    [SerializeField] bool followSceneView = true;

    bool _sceneViewActive;

    void AddSceneViewChunks(Vector2Int mainCenter)
    {
        if (!followSceneView) return;

        var focused = UnityEditor.EditorWindow.focusedWindow;
        if (focused is UnityEditor.SceneView)             _sceneViewActive = true;
        else if (focused != null && focused.GetType().Name == "GameView") _sceneViewActive = false;
        if (!_sceneViewActive) return;

        var view = UnityEditor.SceneView.lastActiveSceneView;
        if (view == null) return;

        var sc = TerrainGrid.ChunkAt(view.pivot);
        if (sc == mainCenter) return;   // 같은 자리 — 더할 것이 없다

        for (int dz = -radius; dz <= radius; dz++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            var c = new Vector2Int(sc.x + dx, sc.y + dz);
            if (TerrainGrid.InWorld(c) && !_wanted.Contains(c)) _wanted.Add(c);
        }
    }
#endif

    int GroundLayer => Mathf.Clamp(terrainLayer, 0, 31);

    Vector3 ViewerPos()
    {
        if (viewer != null) return viewer.position;
        var cam = Camera.main;
        return cam != null ? cam.transform.position : transform.position;
    }

    // ── 세우기 ──────────────────────────────────────────────────────────────

    // ── 청크는 만들고 버리는 대신 **돌려 쓴다** ─────────────────────────────
    //
    // 세울 때마다 `new TerrainData()`를 하면 유니티가 **높이맵·알파맵·베이스맵 텍스처를 새로
    // 만들어 GPU에 올린다**(프로파일러의 `CreateCommittedResourceWithTag`가 그것이다). 버릴
    // 때는 그걸 다시 거둔다. 걸어 다니는 내내 그 왕복이 반복되는데, **개수는 늘 같다** —
    // 화면에 있는 청크는 `(2r+1)²`로 정해져 있고 자리만 바뀐다.
    //
    // 그러니 **그릇은 그대로 두고 내용만 갈아 끼운다.** `SetHeights`·`SetAlphamaps`는 이미
    // 있는 텍스처에 덮어쓰므로 새로 만들 것이 없고, 오브젝트·컴포넌트·머티리얼도 그대로다.
    // 남는 비용은 **높이·스플랫을 올리는 것과 콜라이더 굽기**뿐인데 그건 어차피 치러야 한다.
    //
    // **`ChunkLoaded`/`ChunkUnloaded`를 듣는 쪽은 안 바뀐다** — 그쪽이 받는 것은 칸 좌표뿐이고
    // `Terrain` 참조를 들고 있지 않다(`RockRenderer`·`WorldContentStreamer` 둘 다 그렇다).
    readonly Stack<Terrain> _pool = new();

    // **화면에 설 수 있는 만큼만 든다** — 그 이상은 다시 쓸 일이 없으니 진짜로 버린다.
    int PoolCap => (2 * radius + 1) * (2 * radius + 1);

    // 높이 배열도 다시 쓴다 — 129×129 = 66KB짜리 쓰레기가 청크마다 나온다.
    float[,] _heights;

    bool _loggedFirst;

    // 청크 하나의 높이를 지금 함수 값으로 채운다 — **`Build`와 `Refill`이 같은 식을 쓴다.**
    // 갈라 두면 런타임에 고친 땅이 새로 서는 청크와 이미 선 청크에서 다르게 나온다.
    float[,] FillHeights(Vector2Int chunk)
    {
        int res = TerrainGrid.VertsPerAxis;   // 129 = 2^7 + 1 — 유니티가 요구하는 형태

        // **[z, x] 순서다** — 뒤집으면 지형이 전치되어 이웃과 안 맞는다.
        if (_heights == null || _heights.GetLength(0) != res) _heights = new float[res, res];
        var h = _heights;

        for (int vz = 0; vz < res; vz++)
        for (int vx = 0; vx < res; vx++)
            h[vz, vx] = Mathf.Clamp01(
                (TerrainHeight.VertexHeight(chunk, vx, vz) - MinHeight) / HeightRange);

        return h;
    }

    // ── 이미 선 청크의 높이를 갈아 끼운다 (2026-09-24) ───────────────────────
    //
    // **땅 고르기가 필요해서 생긴 문이다.** 전에는 세우거나 버리는 길뿐이라, 높이가 런타임에
    // 바뀌면 그 청크를 **`Unload` 후 다시 `Build`** 하는 수밖에 없었다 — 그러면 청크에 딸린
    // 나무·바위·광맥이 통째로 파괴되고 다시 뿌려져 **한 칸을 고를 때마다 화면이 깜빡인다.**
    //
    // `TerrainData`는 그릇이고 `SetHeights`는 덮어쓰므로 **내용만 갈면 된다**(풀링이 이미
    // 그 전제로 돌고 있다). `TerrainCollider`가 같은 `TerrainData`를 가리키므로 **콜라이더도
    // 따라온다** — 따로 굽지 않는다.
    //
    // **없는 청크는 아무 일도 안 한다** — 화면 밖이면 다음에 설 때 새 높이로 선다.
    public static void Refill(Vector2Int chunk)
    {
        if (_instance != null) _instance.RefillChunk(chunk);
    }

    void RefillChunk(Vector2Int chunk)
    {
        if (!_live.TryGetValue(chunk, out var terrain) || terrain == null) return;

        var data = terrain.terrainData;
        if (data == null) return;

        var h = FillHeights(chunk);
        data.SetHeights(0, 0, h);
        Chasm.ApplyHoles(data, chunk);

        // **텍스처도 같이 깐다** — 경사가 바뀌었으므로(고른 땅은 평평해진다) 안 다시 깔면
        // 암반 무늬가 **없어진 비탈에 그대로 남는다.**
        TerrainSplat.Apply(data, chunk, h);
    }

    void Build(Vector2Int chunk)
    {
        int res = TerrainGrid.VertsPerAxis;
        var h = FillHeights(chunk);

        Terrain terrain = null;
        while (_pool.Count > 0 && terrain == null) terrain = _pool.Pop();   // 파괴된 것은 버린다

        TerrainData data;
        if (terrain != null)
        {
            data = terrain.terrainData;
        }
        else
        {
            data = new TerrainData
            {
                heightmapResolution = res,
                size = new Vector3(TerrainGrid.ChunkSize, HeightRange, TerrainGrid.ChunkSize),
            };

            var go = new GameObject("Chunk");
            go.transform.SetParent(Root, false);
            go.layer = GroundLayer;

            terrain = go.AddComponent<Terrain>();
            terrain.terrainData   = data;
            terrain.materialTemplate = Material();
            // **인스턴싱을 안 쓴다**(2026-09-02). 켜면 정점 변위와 법선이 **인스턴싱 전용
            // 변형**을 타는데, 우리 지형은 `TerrainData`도 키워드도 **전부 런타임 생성**이라
            // 빌드 수집기가 그 변형이 쓰인다는 것을 알 방법이 없다 — 잘려 나가면 빌드에서만
            // 이상하게 그려진다(에디터는 변형을 그때그때 굽는다).
            //
            // **잃는 것이 거의 없다** — 화면에 서는 청크가 25장뿐이고 CPU는 예산의 5%다.
            terrain.drawInstanced = false;
            terrain.allowAutoConnect = true;   // 자리로 이웃을 찾아 LOD 이음매를 맞춘다
            terrain.groupingID    = 0;

            go.AddComponent<TerrainCollider>().terrainData = data;
        }

        data.SetHeights(0, 0, h);
        Chasm.ApplyHoles(data, chunk);   // 절벽 — 그릇을 돌려 쓰므로 없는 청크도 한 번 묻는다
        Chasm.PrepareMaterial(terrain.materialTemplate);

        // **텍스처를 깐다** — 높이·경사·바이옴에서 결정적으로. 저장되는 것은 없다.
        TerrainSplat.Apply(data, chunk, h);

        var t = terrain.transform;
        t.position = TerrainGrid.OriginOf(chunk) + Vector3.up * MinHeight;
        terrain.gameObject.name = $"Chunk_{TerrainGrid.KeyOf(chunk)}";

        // **자리를 옮긴 다음에 켠다** — 켜 놓고 옮기면 그 한 프레임 동안 옛 자리에 서 있다.
        if (!terrain.gameObject.activeSelf) terrain.gameObject.SetActive(true);

        // **첫 청크만 무엇으로 그리는지 적는다**(2026-09-02). 지형이 검게 나오는 길은 여럿이고
        // (재질 없음 · 셰이더가 빌드에서 잘림 · 레이어 0장) **화면으로는 셋이 똑같이 보인다.**
        // 한 줄이면 갈린다 — 그리고 빌드에서만 나는 증상이라 로그 말고는 볼 방법이 없다.
        if (!_loggedFirst)
        {
            _loggedFirst = true;
            var mat = terrain.materialTemplate;
            Debug.Log($"[지형] 첫 청크 — 재질 {(mat != null ? mat.name : "**없음**")} · " +
                      $"셰이더 {(mat != null && mat.shader != null ? mat.shader.name : "**없음**")} · " +
                      $"레이어 {(data.terrainLayers != null ? data.terrainLayers.Length : 0)}장 · " +
                      $"알파맵 {data.alphamapLayers}채널 {data.alphamapResolution}px · " +
                      $"팔레트 {(TerrainSplat.Palette != null ? TerrainSplat.Palette.name : "**없음**")}");
        }

        _live[chunk] = terrain;
        ChunkLoaded?.Invoke(chunk);
    }

    void Unload(Vector2Int chunk)
    {
        if (!_live.TryGetValue(chunk, out var t)) return;
        _live.Remove(chunk);
        if (t == null) return;

        if (_pool.Count < PoolCap)
        {
            t.gameObject.SetActive(false);
            _pool.Push(t);
        }
        else
        {
            // **`TerrainData`도 같이 버린다** — 오브젝트만 지우면 그 데이터가 메모리에 남아
            // 걸어 다닐수록 샌다(에셋이 아니라 런타임에 만든 것이라 아무도 안 거둔다).
            var data = t.terrainData;
            Destroy(t.gameObject);
            if (data != null) Destroy(data);
        }

        ChunkUnloaded?.Invoke(chunk);
    }

    // `allowAutoConnect`가 자리로 이웃을 찾지만, **붙이고 뗀 직후에는 다시 알려 줘야 한다** —
    // 안 그러면 새로 선 청크의 가장자리 LOD가 이웃과 안 맞아 **틈이 한 줄 보인다.**
    void Reconnect() => Terrain.SetConnectivityDirty();

    Material _made;

    // **청크 머티리얼의 답은 여기 하나다** — 에디터에서 지형을 세우는 도구도 같은 것을 써야
    // 저작 중에 보는 땅과 게임에서 보는 땅이 같은 색이다.
    public Material ChunkMaterial() => Material();

    Material Material()
    {
        if (terrainMaterial != null) return terrainMaterial;
        if (_made != null) return _made;

        // **우리 사본을 먼저 찾는다** — URP 지형 셰이더에 매크로 변주를 얹은 것
        // (`2_Material/TerrainShader/`). 없으면 URP 원본으로 떨어진다: 사본이 컴파일에
        // 실패해도 **세상이 분홍색이 되지는 않는다.**
        // **이름 목록은 `RuntimeShaders`가 든다** — `Shader.Find`는 **빌드에 포함된 것만**
        // 찾는데, 이름으로 찾는 것은 참조가 아니라서 아무 재질도 안 가리키면 **통째로 잘려
        // 나간다.** 그러면 재질이 `null`인 채로 지형이 서고 화면은 **검은 판**이다
        // (2026-09-02에 실제로 그렇게 나왔다 — 에디터에서는 끝까지 멀쩡했다).
        var sh = RuntimeShaders.Find(RuntimeShaders.TerrainLit,
                                     RuntimeShaders.UrpTerrainLit,
                                     RuntimeShaders.NatureTerrain);
        if (sh == null) return null;
        _made = new Material(sh) { name = "TerrainChunk (runtime)" };
        return _made;
    }

    void OnDisable()
    {
        foreach (var c in new List<Vector2Int>(_live.Keys)) Unload(c);

        // **넣어 둔 것도 거둔다.** `_root`를 지우면 오브젝트는 같이 사라지지만 런타임에 만든
        // `TerrainData`는 **아무도 안 거둔다**(에셋이 아니다). 위 `Unload`가 살아 있는 것에
        // 대해 하는 일을, 여기서는 풀에 대해 한다.
        while (_pool.Count > 0)
        {
            var t = _pool.Pop();
            if (t == null) continue;
            var data = t.terrainData;
            if (data != null) Destroy(data);
        }

        if (_root != null) Destroy(_root.gameObject);
        if (_instance == this) _instance = null;
    }

    // **둘이면 서로 다툰다** — 같은 청크를 각자 세우고 각자 버려서, 지형이 깜빡이거나
    // `TerrainData`가 두 배로 쌓인다. 씬에 하나만 둘 것.
    void OnEnable()
    {
        _instance = this;
        var all = FindObjectsByType<TerrainStreamer>(FindObjectsSortMode.None);
        if (all.Length > 1)
            Debug.LogWarning($"[TerrainStreamer] 씬에 {all.Length}개 있다 — 하나만 두어야 한다.");

        // **천장을 켰는데 그리는 쪽이 없으면 지하가 안 보인다** — 통행은 막히는데 화면은
        // 지상 그대로라, 증상이 *"천장이 왜 안 뜨지"*로만 나타나고 원인은 씬 배선에 있다.
        if (ceiling && FindFirstObjectByType<RockRenderer>(FindObjectsInactive.Include) == null)
            Debug.LogWarning("[TerrainStreamer] 지하가 켜져 있는데 씬에 `RockRenderer`가 없다 — " +
                             "통행은 막히는데 **화면에는 벽이 안 보인다.** " +
                             "`DungeonColony → Terrain → 0. Setup World Streaming`을 돌릴 것.");
    }
}
