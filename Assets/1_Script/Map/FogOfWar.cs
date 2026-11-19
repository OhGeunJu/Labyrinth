using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// **전장의 안개 — 안 가 본 곳은 검고, 가 본 곳은 보이되 그 안의 움직임은 안 보인다**
// (2026-09-15, 사용자 결정 — RTS의 그 안개).
//
// *"안 가 본 길 = 검은색. 갔던 길은 원래 보이던 대로 보이는데 그 안에서 움직이는 것들이
//  안 보이는 느낌 — 다른 게임들도 그렇잖아."* · *"내 폰의 시야만 열어야지."*
//
// **처음에는 상태를 둘로 지었다**(보임 ↔ 안 보임 — *"떠나면 다시 흐려진다"*를 땅에 걸었다).
// 사용자가 뜻한 것은 **땅이 아니라 움직이는 것**이었다. 그래서 상태가 셋이다:
//
//     안 가 본 곳   검다             땅도 물건도 폰도 안 보인다
//     가 본 곳      땅은 그대로 보인다  **남의 폰만 안 보인다**
//     지금 보는 곳  다 보인다
//
// 루프가 **탐험이 본류**라 *어디에 무엇이 있나*가 처음부터 다 보이면 굴을 파고 나갈 이유가
// 반쪽이 된다. 막힌 굴(`TerrainCeiling`의 봉한 큰 굴)은 **찾는 것**이어야 하고, 벽 광맥은
// **가 본 곳에서** 읽혀야 캐러 갈 이유가 생긴다.
//
// ── 무엇을 정했나 ──────────────────────────────────────────────────────────
//
// - **땅은 기억하고 폰은 기억하지 않는다** — 가 본 곳의 벽·바닥·건물은 남고, 거기 선 적은
//   지금 보고 있을 때만 보인다(마지막으로 본 자리에 유령을 남기지 않는다).
// - **여는 것은 내 폰의 눈뿐이다** — 횃불·화톳불·용병·동맹은 안 연다(사용자).
// - **바위가 시야를 막는다** — 안 막으면 반경 안의 **벽 너머 굴이 들여다보여** 봉한 굴이 봉한
//   것이 아니게 된다. 판정은 `RockMap.IsRock` 하나다(길찾기가 막히는 곳이 곧 눈이 막히는 곳).
//   드러나는 것은 **첫 벽까지**다(벽면은 보인다 · 그 속은 안 보인다).
// - **반경은 전투 감지 범위와 따로 둔다** — 감지 범위는 10m 남짓이라 그대로 쓰면 화면이 바늘
//   구멍이다. 이건 *"플레이어가 무엇을 보나"*이고 저건 *"폰이 저 혼자 무엇에 덤비나"*다.
//
// ── 어떻게 가리나 ──────────────────────────────────────────────────────────
//
// **화면을 한 번 덮는다**(`FogOfWar.shader`) — 셰이더마다 넣으면 하나가 반드시 빠진다. 다만
// 덮는 것은 *어둡게*일 뿐이라 **남의 폰은 렌더러를 끈다**(안 끄면 어두운 실루엣으로 적의 수가
// 읽힌다) · **커서도 그 폰을 못 짚는다**(`SelectionManager.PickVisible`이 `Hides`를 묻는다).
//
// ── 어기면 조용히 깨지는 것 ────────────────────────────────────────────────
//
// - **텍스처는 세계 전체가 아니라 카메라 둘레 창이다**(512m) — 흐려지는 동안 매번 올려야
//   하는데 세계 전체(1MB)를 초당 스무 번 올리면 그것이 곧 버스 대역이다. 창 밖은 셰이더가 가린다.
// - **렌더러는 `enabled`가 아니라 `forceRenderingOff`로 끈다** — 켜고 끄는 다른 코드(무기·도구
//   바꿔 들기)와 같은 스위치를 두고 싸우지 않는다.
// - **가렸던 폰이 시체가 되면 풀어 준다** — 시체는 `UnitBrain.All`에서 빠지므로 안 풀면 **영영
//   안 보이는 시체**가 남는다.
// - **게임 카메라에만 건다** — 초상화 카메라에 걸면 초상화가 안개에 먹힌다.
public class FogOfWar : MonoBehaviour
{
    [Tooltip("내 폰이 보는 반경(m). **이 값이 안개의 인상을 거의 다 정한다** — 작으면 손전등, " +
             "크면 안개가 있는 줄 모른다.")]
    [SerializeField, Range(6f, 90f)] float sightRadius = 44f;   // 22 → 44(2026-09-15, 사용자: *"2배로"*)

    [Tooltip("시야 가장자리가 흐려지는 폭(m). 0이면 칼같이 끊긴다.")]
    [SerializeField, Range(0f, 15f)] float edgeSoftness = 5f;

    [Tooltip("**안 가 본 곳**의 밝기(0 = 검정). 사용자: *\"안 가 본 길 = 검은색\"*.")]
    [SerializeField, Range(0f, 1f)] float unexploredBrightness = 0f;

    [Tooltip("**가 봤지만 지금은 안 보는 곳**의 밝기(1 = 원래대로). 사용자: *\"원래 보이던 대로\"* — " +
             "지금 보는 곳과 갈라 보고 싶으면 조금 내린다.")]
    [SerializeField, Range(0f, 1f)] float rememberedBrightness = 1f;

    [Tooltip("보이게/안 보이게 바뀌는 데 걸리는 시간(초).")]
    [SerializeField, Range(0.05f, 2f)] float fadeSeconds = 0.35f;

    [Tooltip("시야를 다시 재는 간격(초). 폰은 이 사이에 1m도 못 간다.")]
    [SerializeField, Range(0.05f, 0.5f)] float sightInterval = 0.1f;

    public static FogOfWar Instance { get; private set; }

    // 남의 폰이 이만큼 안 보이면 가린다. 가장자리 흐림 안에 반쯤 들어오면 보인다.
    const float HideBelow = 0.35f;

    const int Window = 512;      // 텍스처 창 한 변(칸 = m)
    const int WindowSnap = 32;   // 창이 이만큼씩만 움직인다 — 매 프레임 따라가면 창 전체를 매번 다시 옮긴다

    // **층마다 한 묶음**(2026-10-01, `Floors`). 세계가 4,096m가 되면서 칸마다 네 배열을 세계 전체로 잡으면 67MB였다 —
    // 층 사이는 기반암이라 시야가 안 넘어간다. 그래서 묶음은 **층 안 칸**으로 색인하고, **가 본 곳**은 그 층을 처음
    // 물을 때(지도가 볼 때도) · **시야 배열**은 그 층에 내 폰이 처음 설 때 잡는다. 아무도 안 간 층은 가 본 곳 한 장(4MB)뿐이다.
    class Layer
    {
        public int OX, OZ;               // 층의 남서 모서리(세계 칸)
        public byte[] Target;            // 이번에 잰 보임(0~255) — 시야가 처음 닿을 때 잡는다
        public byte[] Current;           // 흐려지며 따라가는 보임
        public bool[] Active;            // Current나 Target이 0이 아닌 칸
        public byte[] Explored;          // 가 본 곳 — **줄지 않는다**(Current의 최댓값)
        public readonly List<int> ActiveList = new();
        public readonly List<int> LitNow = new();
        public RectInt Dirty;            // 가 본 곳이 늘어난 범위(층 안 칸)
        public bool HasDirty;
    }

    int _world;                          // 층 한 변(칸)
    readonly Layer[] _layers = new Layer[Floors.Count + 1];

    Layer LayerOf(int floor, bool create)
    {
        floor = Mathf.Clamp(floor, 1, Floors.Count);
        var l = _layers[floor];
        if (l != null || !create) return l;
        var o = Floors.TileOrigin(floor);
        l = new Layer { OX = o.x, OZ = o.y, Explored = new byte[_world * _world] };
        _layers[floor] = l;
        return l;
    }

    static void EnsureSight(Layer l, int n)
    {
        if (l.Target != null) return;
        l.Target  = new byte[n];
        l.Current = new byte[n];
        l.Active  = new bool[n];
    }

    // 세계 칸 → (층, 층 안 색인). 세계 밖이면 거짓.
    bool Index(int x, int z, bool create, out Layer layer, out int i) =>
        Index(x, z, create, out layer, out i, out _);

    bool Index(int x, int z, bool create, out Layer layer, out int i, out int floor)
    {
        layer = null; i = 0; floor = 0;
        if ((uint)x >= TerrainGrid.WorldSize || (uint)z >= TerrainGrid.WorldSize) return false;
        floor = Mathf.Clamp(Floors.At(new Vector2Int(x, z)), 1, Floors.Count);
        layer = LayerOf(floor, create);
        if (layer == null) return false;
        int lx = x - layer.OX, lz = z - layer.OZ;
        if ((uint)lx >= _world || (uint)lz >= _world) return false;
        i = lz * _world + lx;
        return true;
    }

    Texture2D _tex;
    byte[] _texBuf;                      // 창을 여기 채워 한 번에 올린다 — `GetPixelData` 인덱서는 에디터에서 칸마다 안전 검사를 탄다
    Layer _lastUploaded;
    Vector2Int _origin = new(int.MinValue, int.MinValue);
    bool _texDirty = true;

    float _sightAt, _fadeAcc;
    Material _mat;
    FogPass _pass;

    readonly HashSet<UnitBrain> _hidden = new();
    readonly List<UnitBrain> _unhide = new();
    static readonly List<Renderer> _renderers = new();
    int _hideTick;

    // ── 시야는 **선 칸마다 기억한다**(2026-11-19, 프로파일러 — `FogOfWar.Update` 자기 시간 10.5ms) ──
    //
    // 0.1초마다 내 폰 전원의 그림자 던지기(반경 44m = 칸 6천 개 · 칸마다 `IsRock`)를 처음부터 다시 했다.
    // 서 있는 폰도, 한 칸에 몰린 폰도 매번 따로 쟀다. 이제 **칸 → 그 칸에서 보이는 칸들**을 들고,
    // 새 칸에 선 폰만 잰다. 모인 칸이 지난번과 같으면 목표를 다시 쓰지도 않는다.
    // 바위·절벽·천장·반경이 바뀌면 통째로 버린다(`_rockVer` ← `RockMap.OnChanged`).
    sealed class SightRecord
    {
        public readonly List<int>  Cells = new();   // (층 안 색인 << 3) | 층
        public readonly List<byte> Vals  = new();
        public int Used;
    }

    readonly Dictionary<Vector2Int, SightRecord> _sightCache = new();
    readonly Stack<SightRecord> _sightPool = new();
    readonly List<Vector2Int> _evict = new();
    SightRecord _rec;                    // `Light`가 적는 곳
    int _measureId, _rockVer;
    int _keyRock = -1, _keyChasm = -1, _keyCeil = -1;
    float _keyRadius = -1f, _keySoft = -1f;
    bool _fadeIdle;                      // 지난 흐리기가 아무것도 안 바꿨다 — 시야가 다시 써질 때까지 쉰다

    static readonly ProfilerMarker MSight  = new("FogOfWar.Sight");
    static readonly ProfilerMarker MHide   = new("FogOfWar.Hide");
    static readonly ProfilerMarker MFade   = new("FogOfWar.Fade");
    static readonly ProfilerMarker MUpload = new("FogOfWar.Upload");

    void OnRockChanged(Vector2Int _) => _rockVer++;

    void ForgetSight()
    {
        foreach (var r in _sightCache.Values) Recycle(r);
        _sightCache.Clear();
    }

    void Recycle(SightRecord r)
    {
        r.Cells.Clear(); r.Vals.Clear(); r.Used = 0;
        _sightPool.Push(r);
    }

    static readonly int IdTex  = Shader.PropertyToID("_FogVisTex");
    static readonly int IdRect = Shader.PropertyToID("_FogVisRect");
    static readonly int IdDim  = Shader.PropertyToID("_FogDim");

    // ── 묻기 ────────────────────────────────────────────────────────────────

    // 그 자리가 지금 얼마나 보이나(0~1). 안개가 없으면 늘 1.
    public static float VisibilityAt(Vector3 world)
    {
        var f = Instance;
        if (f == null || !f.isActiveAndEnabled) return 1f;

        int x = Mathf.FloorToInt(world.x), z = Mathf.FloorToInt(world.z);
        if (!f.Index(x, z, false, out var l, out int i) || l.Current == null) return 0f;
        return l.Current[i] / 255f;
    }

    // **가 본 적이 있나**(0~1, 줄지 않는다). 지도 창이 쓴다 — 화면은 검은데 지도에 길이
    // 그려져 있으면 안개가 지도에서 새어 나간다. 안개가 없으면 늘 1.
    public static float ExploredAt(int x, int z)
    {
        var f = Instance;
        if (f == null || !f.isActiveAndEnabled) return 1f;
        if (!f.Index(x, z, false, out var l, out int i)) return 0f;
        return l.Explored[i] / 255f;
    }

    public static float ExploredAt(Vector3 world) =>
        ExploredAt(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));

    // **가 본 곳이 늘어난 범위** — 지도가 가져가며 비운다. 지도가 1초마다 세계 전체(백만 칸)를
    // 다시 굽던 것을 **바뀐 사각형만** 굽게 하는 장치다(2026-09-15, 코드 리뷰).
    // **층마다 따로 모은다**(층 안 칸) — 지도는 보는 층의 것만 가져간다.
    void GrowExploredDirty(Layer l, int i)
    {
        int x = i % _world, z = i / _world;
        if (!l.HasDirty) { l.Dirty = new RectInt(x, z, 1, 1); l.HasDirty = true; return; }
        int xMin = Mathf.Min(l.Dirty.xMin, x), zMin = Mathf.Min(l.Dirty.yMin, z);
        int xMax = Mathf.Max(l.Dirty.xMax, x + 1), zMax = Mathf.Max(l.Dirty.yMax, z + 1);
        l.Dirty = new RectInt(xMin, zMin, xMax - xMin, zMax - zMin);
    }

    // 그 층의 가 본 곳이 늘어난 범위(**층 안 칸**)를 가져가며 비운다.
    public static bool TakeExploredDirty(int floor, out RectInt rect)
    {
        var f = Instance;
        rect = default;
        var l = f != null ? f.LayerOf(floor, false) : null;
        if (l == null || !l.HasDirty) return false;
        rect = l.Dirty;
        l.HasDirty = false;
        return true;
    }

    // **가 본 곳 배열을 그대로 준다** — 지도가 칸마다 `ExploredAt`을 부르면 백만 번의 속성 확인이다.
    // 안개가 없으면 `null`(= 전부 가 봤다). **읽기만 할 것.**
    // **층마다**(층 안 칸 · 한 변 `Floors.Size`) — 안개가 있으면 그 층을 처음 물어도 빈 배열을 잡아 준다(*"안 가 본
    // 층"*은 검게 그려져야 한다).
    public static byte[] ExploredRaw(int floor)
    {
        var f = Instance;
        if (f == null || !f.isActiveAndEnabled) return null;
        return f.LayerOf(floor, true).Explored;
    }

    // 남의 것이 지금 보이는 자리인가 — 폰을 가리는 문턱과 같은 값을 쓴다(지도와 화면이 갈리지 않게).
    public static bool IsVisible(Vector3 world) => VisibilityAt(world) >= HideBelow;

    // 안개가 이 폰을 가리고 있나. 커서가 짚지 못하게 하는 데 쓴다.
    public static bool Hides(UnitBrain brain) =>
        brain != null && Instance != null && Instance._hidden.Contains(brain);

    // ── 수명 ────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (!RockMap.Enabled) { enabled = false; return; }

        var shader = Resources.Load<Shader>("Shaders/FogOfWar");
        if (shader == null)
        {
            Debug.LogError("[안개] `Resources/Shaders/FogOfWar.shader`를 못 찾았다 — 안개를 안 건다.");
            enabled = false;
            return;
        }

        Instance = this;
        _world   = Floors.Size;
        System.Array.Clear(_layers, 0, _layers.Length);

        // **두 채널이다** — R = 가 봤나 · G = 지금 보나. 밝기 둘을 셰이더가 섞으므로
        // 인스펙터에서 바꾸면 바로 반영된다(CPU가 섞어 구우면 다시 구울 때까지 안 바뀐다).
        _tex = new Texture2D(Window, Window, TextureFormat.RG16, false, true)
        {
            name = "FogOfWar", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        _mat  = new Material(shader) { name = "FogOfWar (runtime)" };
        _pass = new FogPass(_mat);

        ForgetSight();
        _fadeIdle = false;
        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        RockMap.OnChanged += OnRockChanged;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        RockMap.OnChanged -= OnRockChanged;
        ForgetSight();

        foreach (var b in _hidden) SetShown(b, true);
        _hidden.Clear();

        if (_tex != null) Destroy(_tex);
        if (_mat != null) Destroy(_mat);
        _tex = null; _mat = null; _pass = null;
        System.Array.Clear(_layers, 0, _layers.Length);
        if (Instance == this) Instance = null;
    }

    // ── 한 틱 ───────────────────────────────────────────────────────────────

    void Update()
    {
        float dt = Time.unscaledDeltaTime;   // 일시정지 중에도 카메라는 움직이고 안개는 따라가야 한다

        _sightAt += dt;
        if (_sightAt >= sightInterval)
        {
            _sightAt = 0f;
            using (MSight.Auto()) MeasureSight();
            using (MHide.Auto())  ApplyPawnHiding();
        }

        // **흐리기는 초당 스무 번** — 매 프레임 올리면 창(256KB)을 프레임마다 버스에 싣는다.
        _fadeAcc += dt;
        if (_fadeAcc >= 0.05f)
        {
            if (!_fadeIdle)
                using (MFade.Auto()) _fadeIdle = !Fade(_fadeAcc);
            _fadeAcc = 0f;
        }

        using (MUpload.Auto()) UploadWindow();
    }

    // 내 폰이 선 칸마다 기억해 둔 시야를 모은다. 새 칸만 재고, 모인 칸이 지난번과 같으면 목표를 안 건드린다.
    void MeasureSight()
    {
        if (_keyRock != _rockVer || _keyChasm != Chasm.Version || _keyCeil != TerrainCeiling.Version
            || _keyRadius != sightRadius || _keySoft != edgeSoftness)
        {
            _keyRock = _rockVer; _keyChasm = Chasm.Version; _keyCeil = TerrainCeiling.Version;
            _keyRadius = sightRadius; _keySoft = edgeSoftness;
            ForgetSight();
        }

        _measureId++;
        bool changed = false;

        var map = MapManager.Instance;
        var all = UnitBrain.All;
        for (int k = 0; k < all.Count; k++)
        {
            var brain = all[k];
            var bb = brain != null ? brain.Blackboard : null;
            if (bb == null || bb.Faction == null || !bb.Faction.IsPlayer) continue;

            var p = brain.transform.position;
            var c = map != null ? map.WorldToTile(p)
                                : new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));

            if (!_sightCache.TryGetValue(c, out var rec))
            {
                rec = _sightPool.Count > 0 ? _sightPool.Pop() : new SightRecord();
                _rec = rec;
                Sight(c);
                _rec = null;
                _sightCache[c] = rec;
                changed = true;
            }
            rec.Used = _measureId;
        }

        // 이번에 아무도 안 선 칸은 버린다 — 남겨 두면 떠난 자리의 시야가 계속 켜져 있다.
        _evict.Clear();
        foreach (var kv in _sightCache)
            if (kv.Value.Used != _measureId) _evict.Add(kv.Key);
        foreach (var t in _evict)
        {
            Recycle(_sightCache[t]);
            _sightCache.Remove(t);
            changed = true;
        }

        if (!changed) return;   // 같은 칸들 그대로 — 목표도 그대로다

        foreach (var l in _layers)
        {
            if (l == null || l.Target == null) continue;
            foreach (int i in l.LitNow) l.Target[i] = 0;
            l.LitNow.Clear();
        }

        int n = _world * _world;
        foreach (var rec in _sightCache.Values)
        {
            var cells = rec.Cells; var vals = rec.Vals;
            for (int k = 0; k < cells.Count; k++)
            {
                int packed = cells[k];
                var l = _layers[packed & 7];
                if (l == null) continue;
                EnsureSight(l, n);

                int i = packed >> 3;
                byte b = vals[k];
                if (l.Target[i] == 0) l.LitNow.Add(i);
                if (b > l.Target[i]) l.Target[i] = b;
                if (!l.Active[i]) { l.Active[i] = true; l.ActiveList.Add(i); }
            }
        }
        _fadeIdle = false;
    }

    // ── 시야 — 그림자 던지기(recursive shadowcasting) ──────────────────────
    //
    // 여덟 팔분면을 따로 훑는다. 벽 칸 자신은 **보인다**(벽면을 봐야 캘 곳을 고른다) — 그 뒤가
    // 가려질 뿐이다.
    static readonly int[] OxX = { 1, 0, 0, -1, -1, 0, 0, 1 };
    static readonly int[] OxY = { 0, 1, -1, 0, 0, -1, 1, 0 };
    static readonly int[] OyX = { 0, 1, 1, 0, 0, -1, -1, 0 };
    static readonly int[] OyY = { 1, 0, 0, 1, -1, 0, 0, -1 };

    void Sight(Vector2Int c)
    {
        int r = Mathf.CeilToInt(sightRadius);
        Light(c.x, c.y, 0f);
        for (int o = 0; o < 8; o++)
            Cast(c, 1, 1f, 0f, r, OxX[o], OxY[o], OyX[o], OyY[o]);
    }

    void Cast(Vector2Int c, int row, float start, float end, int r, int xx, int xy, int yx, int yy)
    {
        if (start < end) return;
        float newStart = 0f;

        for (int j = row; j <= r; j++)
        {
            bool blocked = false;
            for (int dx = -j, dy = -j; dx <= 0; dx++)
            {
                float lSlope = (dx - 0.5f) / (dy + 0.5f);
                float rSlope = (dx + 0.5f) / (dy - 0.5f);
                if (start < rSlope) continue;
                if (end > lSlope) break;

                int x = c.x + dx * xx + dy * xy;
                int y = c.y + dx * yx + dy * yy;
                float d2 = dx * dx + dy * dy;
                if (d2 <= r * r) Light(x, y, Mathf.Sqrt(d2));

                bool opaque = RockMap.IsRock(new Vector2Int(x, y));
                if (blocked)
                {
                    if (opaque) { newStart = rSlope; continue; }
                    blocked = false;
                    start = newStart;
                }
                else if (opaque && j < r)
                {
                    blocked = true;
                    Cast(c, j + 1, start, lSlope, r, xx, xy, yx, yy);
                    newStart = rSlope;
                }
            }
            if (blocked) break;
        }
    }

    void Light(int x, int y, float dist)
    {
        float soft = Mathf.Max(0.01f, edgeSoftness);
        float v = Mathf.Clamp01((sightRadius - dist) / soft);
        byte b = (byte)(v * 255f);
        if (b == 0) return;

        if (!Index(x, y, true, out _, out int i, out int floor)) return;
        _rec.Cells.Add((i << 3) | floor);
        _rec.Vals.Add(b);
    }

    // 켜진 칸들이 목표를 따라간다. 둘 다 0이 된 칸은 목록에서 뺀다. 무엇이든 바뀌었으면 참.
    bool Fade(float dt)
    {
        bool any = false;
        int step = Mathf.Max(1, Mathf.RoundToInt(255f * dt / Mathf.Max(0.01f, fadeSeconds)));
        int viewing = Floors.Current;

        for (int f = 1; f < _layers.Length; f++)
        {
            var l = _layers[f];
            if (l == null || l.Target == null) continue;
            var list = l.ActiveList;

            for (int k = list.Count - 1; k >= 0; k--)
            {
                int i = list[k];
                int cur = l.Current[i], tgt = l.Target[i];

                if (cur < tgt)      cur = Mathf.Min(tgt, cur + step);
                else if (cur > tgt) cur = Mathf.Max(tgt, cur - step);

                if (cur != l.Current[i]) { l.Current[i] = (byte)cur; any = true; if (f == viewing) _texDirty = true; }
                if (cur > l.Explored[i]) { l.Explored[i] = (byte)cur; GrowExploredDirty(l, i); }

                if (cur == 0 && tgt == 0)
                {
                    any = true;
                    l.Active[i] = false;
                    int last = list.Count - 1;
                    list[k] = list[last];
                    list.RemoveAt(last);
                }
            }
        }
        return any;
    }

    // 카메라 둘레 창만 텍스처로 옮긴다.
    void UploadWindow()
    {
        var cam = Camera.main;
        if (cam == null || _tex == null) return;

        var cp = cam.transform.position;
        var origin = new Vector2Int(
            Mathf.FloorToInt((cp.x - Window * 0.5f) / WindowSnap) * WindowSnap,
            Mathf.FloorToInt((cp.z - Window * 0.5f) / WindowSnap) * WindowSnap);

        // **보는 층의 것만 올린다** — 창이 층 경계를 넘으면 그 너머는 기반암이고 검게 둔다.
        var layer = LayerOf(Floors.Current, true);
        if (layer != _lastUploaded) { _lastUploaded = layer; _texDirty = true; }

        if (origin != _origin) { _origin = origin; _texDirty = true; }
        if (!_texDirty) return;
        _texDirty = false;

        _texBuf ??= new byte[Window * Window * 2];   // RG16 = 칸마다 두 바이트(R, G)
        var data = _texBuf;
        var ex  = layer.Explored;
        var cur = layer.Current;
        for (int ty = 0; ty < Window; ty++)
        {
            int ly = _origin.y + ty - layer.OZ;
            int row = ty * Window * 2;
            if ((uint)ly >= _world)
            {
                System.Array.Clear(data, row, Window * 2);
                continue;
            }
            int src = ly * _world;
            for (int tx = 0; tx < Window; tx++)
            {
                int lx = _origin.x + tx - layer.OX;
                int o = row + tx * 2;
                if ((uint)lx < _world)
                {
                    data[o]     = ex[src + lx];
                    data[o + 1] = cur != null ? cur[src + lx] : (byte)0;
                }
                else { data[o] = 0; data[o + 1] = 0; }
            }
        }
        _tex.SetPixelData(data, 0);
        _tex.Apply(false, false);

        Shader.SetGlobalTexture(IdTex, _tex);
        Shader.SetGlobalVector(IdRect, new Vector4(_origin.x, _origin.y, 1f / Window, 1f / Window));
    }

    // ── 남의 폰을 가린다 ────────────────────────────────────────────────────

    void ApplyPawnHiding()
    {
        // **이미 가린 폰은 1초마다만 다시 건다**(2026-11-19) — 0.1초마다 가린 폰 전원(야생 · 다른 층)의
        // 렌더러를 `GetComponentsInChildren`으로 훑던 것이 프로파일러에 걸렸다. 새로 가릴 때는 바로 건다.
        bool refresh = (++_hideTick % 10) == 0;
        int viewing = Floors.Current;
        var all = UnitBrain.All;
        for (int k = 0; k < all.Count; k++)
        {
            var brain = all[k];
            if (brain == null) continue;
            var bb = brain.Blackboard;
            bool mine = bb != null && bb.Faction != null && bb.Faction.IsPlayer;

            // **업힌 몸은 업은 사람을 따른다** — 내 폰이 업고 오는 적이 어깨 위에서 사라지면 안 된다.
            // **보는 층 밖은 누구든 가린다**(2026-10-01, `Floors`) — 그 층의 땅은 안 서 있으므로(스트리밍이 보는 층만 세운다)
            // 층 경계 가까이서 옆 층의 폰이 허공에 떠 보인다. 내 폰도 — 다른 층에 둔 거점의 일꾼이다.
            bool otherFloor = Floors.At(brain.transform.position) != viewing;
            bool hide = otherFloor
                     || (!mine && VisibilityAt(brain.transform.position) < HideBelow
                         && (bb == null || bb.CarriedBy == null));

            if (hide)
            {
                // 가끔 다시 건다 — 그사이 손에 든 도구가 생겼을 수 있다
                if (_hidden.Add(brain) || refresh) SetShown(brain, false);
            }
            else
            {
                if (_hidden.Remove(brain)) SetShown(brain, true);

                // **보였으면 만난 것이다**(`FactionRelations.Meet` — 세력 창이 이것으로 찬다).
                // 업혀 온 몸은 안 센다 — 그건 내가 찾아간 것이 아니라 들고 온 것이다.
                if (!mine && bb != null && bb.CarriedBy == null && bb.Faction != null
                    && VisibilityAt(brain.transform.position) >= HideBelow)
                    FactionRelations.Meet(bb.Faction.Data);
            }
        }

        // 목록에서 빠진 폰(시체·파괴·원정)은 풀어 준다.
        _unhide.Clear();
        foreach (var b in _hidden)
            if (b == null || !b.isActiveAndEnabled) _unhide.Add(b);
        foreach (var b in _unhide)
        {
            _hidden.Remove(b);
            SetShown(b, true);
        }
    }

    static void SetShown(UnitBrain brain, bool shown)
    {
        if (brain == null) return;
        brain.GetComponentsInChildren(true, _renderers);
        foreach (var r in _renderers) if (r != null) r.forceRenderingOff = !shown;
        _renderers.Clear();
    }

    // ── 그리기 ──────────────────────────────────────────────────────────────

    void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
    {
        if (_pass == null || cam != Camera.main || cam.cameraType != CameraType.Game) return;

        _mat.SetVector(IdDim, new Vector4(unexploredBrightness, rememberedBrightness, 0f, 0f));
        cam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);
    }

    // **불투명을 다 그린 뒤, 반투명 앞에 덮는다** — 월드보더 막·청사진 같은 반투명은 안개에
    // 안 먹힌다(막은 세계의 끝을 알리는 것이라 안개보다 앞에 있어야 한다).
    sealed class FogPass : ScriptableRenderPass
    {
        readonly Material _mat;

        class PassData { public Material Mat; }

        public FogPass(Material mat)
        {
            _mat = mat;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var res = frameData.Get<UniversalResourceData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Fog Of War", out var data);
            data.Mat = _mat;

            builder.SetRenderAttachment(res.activeColorTexture, 0, AccessFlags.ReadWrite);
            if (res.cameraDepthTexture.IsValid()) builder.UseTexture(res.cameraDepthTexture);
            builder.UseAllGlobalTextures(true);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, new Vector4(1f, 1f, 0f, 0f), d.Mat, 0));
        }
    }
}
