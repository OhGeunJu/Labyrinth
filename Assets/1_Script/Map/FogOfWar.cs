using System.Collections.Generic;
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

    int _world;
    byte[] _target;              // 이번에 잰 보임(0~255)
    byte[] _current;             // 흐려지며 따라가는 보임
    byte[] _explored;            // 가 본 곳 — **줄지 않는다**(_current의 최댓값)
    bool[] _active;              // _current나 _target이 0이 아닌 칸
    readonly List<int> _activeList = new();
    readonly List<int> _litNow = new();

    Texture2D _tex;
    Vector2Int _origin = new(int.MinValue, int.MinValue);
    bool _texDirty = true;

    float _sightAt, _fadeAcc;
    Material _mat;
    FogPass _pass;

    readonly HashSet<UnitBrain> _hidden = new();
    readonly List<UnitBrain> _unhide = new();
    static readonly List<Renderer> _renderers = new();

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
        if ((uint)x >= f._world || (uint)z >= f._world) return 0f;
        return f._current[z * f._world + x] / 255f;
    }

    // **가 본 적이 있나**(0~1, 줄지 않는다). 지도 창이 쓴다 — 화면은 검은데 지도에 길이
    // 그려져 있으면 안개가 지도에서 새어 나간다. 안개가 없으면 늘 1.
    public static float ExploredAt(int x, int z)
    {
        var f = Instance;
        if (f == null || !f.isActiveAndEnabled) return 1f;
        if ((uint)x >= f._world || (uint)z >= f._world) return 0f;
        return f._explored[z * f._world + x] / 255f;
    }

    public static float ExploredAt(Vector3 world) =>
        ExploredAt(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));

    // **가 본 곳이 늘어난 범위** — 지도가 가져가며 비운다. 지도가 1초마다 세계 전체(백만 칸)를
    // 다시 굽던 것을 **바뀐 사각형만** 굽게 하는 장치다(2026-09-15, 코드 리뷰).
    RectInt _exploredDirty;
    bool    _hasExploredDirty;

    void GrowExploredDirty(int i)
    {
        int x = i % _world, z = i / _world;
        if (!_hasExploredDirty) { _exploredDirty = new RectInt(x, z, 1, 1); _hasExploredDirty = true; return; }
        int xMin = Mathf.Min(_exploredDirty.xMin, x), zMin = Mathf.Min(_exploredDirty.yMin, z);
        int xMax = Mathf.Max(_exploredDirty.xMax, x + 1), zMax = Mathf.Max(_exploredDirty.yMax, z + 1);
        _exploredDirty = new RectInt(xMin, zMin, xMax - xMin, zMax - zMin);
    }

    public static bool TakeExploredDirty(out RectInt rect)
    {
        var f = Instance;
        rect = default;
        if (f == null || !f._hasExploredDirty) return false;
        rect = f._exploredDirty;
        f._hasExploredDirty = false;
        return true;
    }

    // **가 본 곳 배열을 그대로 준다** — 지도가 칸마다 `ExploredAt`을 부르면 백만 번의 속성 확인이다.
    // 안개가 없으면 `null`(= 전부 가 봤다). **읽기만 할 것.**
    public static byte[] ExploredRaw =>
        Instance != null && Instance.isActiveAndEnabled ? Instance._explored : null;

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
        _world   = TerrainGrid.WorldSize;
        _target  = new byte[_world * _world];
        _current = new byte[_world * _world];
        _active  = new bool[_world * _world];
        _explored = new byte[_world * _world];

        // **두 채널이다** — R = 가 봤나 · G = 지금 보나. 밝기 둘을 셰이더가 섞으므로
        // 인스펙터에서 바꾸면 바로 반영된다(CPU가 섞어 구우면 다시 구울 때까지 안 바뀐다).
        _tex = new Texture2D(Window, Window, TextureFormat.RG16, false, true)
        {
            name = "FogOfWar", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        _mat  = new Material(shader) { name = "FogOfWar (runtime)" };
        _pass = new FogPass(_mat);

        RenderPipelineManager.beginCameraRendering += OnBeginCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCamera;

        foreach (var b in _hidden) SetShown(b, true);
        _hidden.Clear();

        if (_tex != null) Destroy(_tex);
        if (_mat != null) Destroy(_mat);
        _tex = null; _mat = null; _pass = null;
        _activeList.Clear(); _litNow.Clear();
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
            MeasureSight();
            ApplyPawnHiding();
        }

        // **흐리기는 초당 스무 번** — 매 프레임 올리면 창(256KB)을 프레임마다 버스에 싣는다.
        _fadeAcc += dt;
        if (_fadeAcc >= 0.05f)
        {
            Fade(_fadeAcc);
            _fadeAcc = 0f;
        }

        UploadWindow();
    }

    // 지난번에 켠 칸의 목표를 끄고, 내 폰마다 다시 켠다.
    void MeasureSight()
    {
        foreach (int i in _litNow) _target[i] = 0;
        _litNow.Clear();

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
            Sight(c);
        }
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
        if ((uint)x >= _world || (uint)y >= _world) return;

        float soft = Mathf.Max(0.01f, edgeSoftness);
        float v = Mathf.Clamp01((sightRadius - dist) / soft);
        byte b = (byte)(v * 255f);
        if (b == 0) return;

        int i = y * _world + x;
        if (_target[i] == 0) _litNow.Add(i);
        if (b > _target[i]) _target[i] = b;

        if (!_active[i]) { _active[i] = true; _activeList.Add(i); }
    }

    // 켜진 칸들이 목표를 따라간다. 둘 다 0이 된 칸은 목록에서 뺀다.
    void Fade(float dt)
    {
        int step = Mathf.Max(1, Mathf.RoundToInt(255f * dt / Mathf.Max(0.01f, fadeSeconds)));

        for (int k = _activeList.Count - 1; k >= 0; k--)
        {
            int i = _activeList[k];
            int cur = _current[i], tgt = _target[i];

            if (cur < tgt)      cur = Mathf.Min(tgt, cur + step);
            else if (cur > tgt) cur = Mathf.Max(tgt, cur - step);

            if (cur != _current[i]) { _current[i] = (byte)cur; _texDirty = true; }
            if (cur > _explored[i]) { _explored[i] = (byte)cur; GrowExploredDirty(i); }

            if (cur == 0 && tgt == 0)
            {
                _active[i] = false;
                int last = _activeList.Count - 1;
                _activeList[k] = _activeList[last];
                _activeList.RemoveAt(last);
            }
        }
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

        if (origin != _origin) { _origin = origin; _texDirty = true; }
        if (!_texDirty) return;
        _texDirty = false;

        var data = _tex.GetPixelData<byte>(0);   // RG16 = 칸마다 두 바이트(R, G)
        for (int ty = 0; ty < Window; ty++)
        {
            int wy = _origin.y + ty;
            int row = ty * Window * 2;
            if ((uint)wy >= _world)
            {
                for (int tx = 0; tx < Window * 2; tx++) data[row + tx] = 0;
                continue;
            }
            int src = wy * _world;
            for (int tx = 0; tx < Window; tx++)
            {
                int wx = _origin.x + tx;
                bool inWorld = (uint)wx < _world;
                data[row + tx * 2]     = inWorld ? _explored[src + wx] : (byte)0;
                data[row + tx * 2 + 1] = inWorld ? _current[src + wx]  : (byte)0;
            }
        }
        _tex.Apply(false, false);

        Shader.SetGlobalTexture(IdTex, _tex);
        Shader.SetGlobalVector(IdRect, new Vector4(_origin.x, _origin.y, 1f / Window, 1f / Window));
    }

    // ── 남의 폰을 가린다 ────────────────────────────────────────────────────

    void ApplyPawnHiding()
    {
        var all = UnitBrain.All;
        for (int k = 0; k < all.Count; k++)
        {
            var brain = all[k];
            if (brain == null) continue;
            var bb = brain.Blackboard;
            bool mine = bb != null && bb.Faction != null && bb.Faction.IsPlayer;

            // **업힌 몸은 업은 사람을 따른다** — 내 폰이 업고 오는 적이 어깨 위에서 사라지면 안 된다.
            bool hide = !mine && VisibilityAt(brain.transform.position) < HideBelow
                        && (bb == null || bb.CarriedBy == null);

            if (hide)
            {
                _hidden.Add(brain);
                SetShown(brain, false);   // 매번 다시 건다 — 그사이 손에 든 도구가 생겼을 수 있다
            }
            else if (_hidden.Remove(brain))
                SetShown(brain, true);
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
