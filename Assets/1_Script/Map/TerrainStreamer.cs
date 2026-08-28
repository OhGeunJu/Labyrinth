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

    public TerrainProfile   Profile  => profile;
    public TerrainFeature[] Features => features;
    public TerrainPalette   Palette  => palette;
    public TerrainHeightmap Heightmap => heightmap;

    void Awake() => Apply();

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

    public int LiveCount => _live.Count;

    // **청크 목록의 주인은 여기 하나다.** 나무·바위·마을을 스트리밍하는 쪽이 반경 계산을
    // 따로 하면 **둘의 답이 어긋나** 지형은 있는데 나무가 없는(또는 그 반대) 칸이 생긴다.
    // 그쪽은 이 소식만 듣는다.
    public static event System.Action<Vector2Int> ChunkLoaded;
    public static event System.Action<Vector2Int> ChunkUnloaded;

    // 이미 서 있는 청크 — 스트리머가 나중에 켜진 쪽도 따라잡을 수 있게.
    public static IEnumerable<Vector2Int> LiveChunks =>
        _instance != null ? _instance._live.Keys : System.Linq.Enumerable.Empty<Vector2Int>();

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
        _wanted.Sort((a, b) => Sqr(a - center).CompareTo(Sqr(b - center)));
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

    int GroundLayer => Mathf.Clamp(terrainLayer, 0, 31);

    Vector3 ViewerPos()
    {
        if (viewer != null) return viewer.position;
        var cam = Camera.main;
        return cam != null ? cam.transform.position : transform.position;
    }

    // ── 세우기 ──────────────────────────────────────────────────────────────

    void Build(Vector2Int chunk)
    {
        int res = TerrainGrid.VertsPerAxis;   // 129 = 2^7 + 1 — 유니티가 요구하는 형태

        var data = new TerrainData
        {
            heightmapResolution = res,
            size = new Vector3(TerrainGrid.ChunkSize, HeightRange, TerrainGrid.ChunkSize),
        };

        // **[z, x] 순서다** — 뒤집으면 지형이 전치되어 이웃과 안 맞는다.
        var h = new float[res, res];
        for (int vz = 0; vz < res; vz++)
        for (int vx = 0; vx < res; vx++)
            h[vz, vx] = Mathf.Clamp01(
                (TerrainHeight.VertexHeight(chunk, vx, vz) - MinHeight) / HeightRange);

        data.SetHeights(0, 0, h);

        // **텍스처를 깐다** — 높이·경사·바이옴에서 결정적으로. 저장되는 것은 없다.
        TerrainSplat.Apply(data, chunk, h);

        var go = new GameObject($"Chunk_{TerrainGrid.KeyOf(chunk)}");
        go.transform.SetParent(Root, false);
        go.transform.position = TerrainGrid.OriginOf(chunk) + Vector3.up * MinHeight;
        go.layer = GroundLayer;

        var terrain = go.AddComponent<Terrain>();
        terrain.terrainData   = data;
        terrain.materialTemplate = Material();
        terrain.drawInstanced = true;
        terrain.allowAutoConnect = true;   // 자리로 이웃을 찾아 LOD 이음매를 맞춘다
        terrain.groupingID    = 0;

        go.AddComponent<TerrainCollider>().terrainData = data;

        _live[chunk] = terrain;
        ChunkLoaded?.Invoke(chunk);
    }

    void Unload(Vector2Int chunk)
    {
        if (!_live.TryGetValue(chunk, out var t)) return;
        _live.Remove(chunk);
        if (t == null) return;

        // **`TerrainData`도 같이 버린다** — 오브젝트만 지우면 그 데이터가 메모리에 남아
        // 걸어 다닐수록 샌다(에셋이 아니라 런타임에 만든 것이라 아무도 안 거둔다).
        var data = t.terrainData;
        Destroy(t.gameObject);
        if (data != null) Destroy(data);
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
        var sh = Shader.Find("DungeonColony/Terrain/Lit")
              ?? Shader.Find("Universal Render Pipeline/Terrain/Lit")
              ?? Shader.Find("Nature/Terrain/Standard");
        if (sh == null)
        {
            Debug.LogWarning("[TerrainStreamer] 지형 셰이더를 못 찾았다 — 인스펙터에 머티리얼을 물릴 것.");
            return null;
        }
        _made = new Material(sh) { name = "TerrainChunk (runtime)" };
        return _made;
    }

    void OnDisable()
    {
        foreach (var c in new List<Vector2Int>(_live.Keys)) Unload(c);
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
    }
}
