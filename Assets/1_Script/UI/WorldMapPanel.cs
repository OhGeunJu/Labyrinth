using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// **지도 — 별도 화면이 아니라 창 하나**(2026-08-26).
//
// 세계가 이어지면서 `WorldMapView`(모드를 갈아 끼우고 카메라를 통째로 넘기던 것)의 자리가
// 사라졌다. 그런데 **개관과 빠른 이동은 여전히 필요하다** — 29km를 걸어 다니면서 지금 어디쯤
// 있는지, 무리가 어디서 오는지를 알 길이 있어야 한다.
//
// **굴을 그린다**(2026-09-15, 사용자: *"미니맵도 길이 보이게"*). 전에는 **바이옴 색만** 칠했다 —
// 지상 시절의 지도라 바위라는 개념이 없었고, 256픽셀(한 칸 4m)이라 6~10m 길은 그려도 뭉개졌다.
// 이제 **1m 한 칸 = 한 픽셀**이고 판정은 `RockMap.IsRock` 하나다(길찾기가 막히는 곳이 곧 지도의
// 벽 · 캔 굴도 따라온다). **색은 바이옴이 든다** — 길은 그 색을 밝게, 바위는 어둡게.
//
// **안개를 따른다** — 안 가 본 곳은 지도에서도 검다. 화면은 검은데 지도에 길이 다 그려져 있으면
// **안개가 지도에서 새어 나간다.** 같은 이유로 **지금 안 보이는 적과 무리는 안 그리고**, 마을·
// 소굴은 가 본 곳의 것만 그린다. (2026-06-20의 *"안개는 없다"*는 안개가 없던 때의 결정이다.)
//
// **바뀐 곳만 다시 굽는다**(2026-09-15, 코드 리뷰) — 처음엔 열려 있는 동안 1초마다 세계 전체
// (백만 칸)를 다시 구웠고, 일시정지 중에도 1초마다 프레임이 튀었다. 이제 **가 본 곳이 늘어난
// 사각형**(`FogOfWar.TakeExploredDirty`)과 **바위가 바뀐 칸**(`RockMap.OnChanged`)만 모아 두었다가
// 그 사각형만 굽는다. 닫혀 있는 동안에도 모은다(열었을 때 따라잡는다).
public class WorldMapPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RawImage   mapImage;
    [SerializeField] RectTransform markerRoot;
    [SerializeField] GameObject markerTemplate;

    [Header("지도")]
    [Tooltip("열려 있는 동안 바뀐 곳을 몇 초마다 모아 굽나. 바뀐 것이 없으면 아무 일도 안 한다.")]
    [SerializeField, Range(0.1f, 5f)] float rebakeSeconds = 0.5f;

    [Tooltip("길(지나갈 수 있는 곳)을 바이옴 색에서 얼마나 밝게 — 0이면 바이옴 색 그대로.")]
    [SerializeField, Range(0f, 1f)] float openLift = 0.45f;

    [Tooltip("바위의 밝기(바이옴 색에 곱한다). 길과 갈려야 한다.")]
    [SerializeField, Range(0f, 1f)] float rockShade = 0.22f;

    [SerializeField] Color unexploredColor = new(0.02f, 0.02f, 0.025f);

    [Header("표식")]
    [SerializeField] Color playerColor = new(0.45f, 0.85f, 1f);
    [SerializeField] Color bandColor   = new(1f, 0.45f, 0.35f);
    [SerializeField] Color coreColor   = new(1f, 0.9f, 0.3f);

    [Tooltip("실제로 서 있는 적 폰. 무리(토큰)와 색을 나눈다 — 하나는 지도를 도는 것이고 " +
             "하나는 거기 눌러앉은 것이라, 같은 색이면 '몇이 오고 있나'를 못 읽는다.")]
    [SerializeField] Color hostileColor = new(0.85f, 0.25f, 0.25f);

    [Tooltip("지금 보고 있는 곳. **이게 없으면 지도가 길을 안내하지 못한다** — " +
             "어디가 어딘지는 알겠는데 내가 어디 있는지를 모른다.")]
    [SerializeField] Color viewColor = Color.white;

    // **저작한 칸 내용물** — 마을·소굴·자원지대. `World Map Editor`에서 찍은 그것이다.
    [Header("저작한 장소")]
    [SerializeField] Color townColor     = new(1f, 0.95f, 0.75f);
    [SerializeField] Color lairColor     = new(0.75f, 0.10f, 0.10f);
    [SerializeField] Color outpostColor  = new(0.60f, 0.85f, 0.45f);

    // **표식 크기는 창에 비례한다** — 픽셀로 박으면 창을 키웠을 때 점이 그대로라 안 보인다.
    // 값은 지도 한 변에 대한 비율(0.02 = 850px 창에서 17px).
    [Header("표식 크기 (지도 한 변 대비)")]
    [SerializeField, Range(0.005f, 0.06f)] float viewSize    = 0.030f;
    [SerializeField, Range(0.005f, 0.06f)] float coreSize    = 0.026f;
    [SerializeField, Range(0.005f, 0.06f)] float pawnSize    = 0.016f;
    [SerializeField, Range(0.005f, 0.06f)] float bandSize    = 0.018f;
    [SerializeField, Range(0.005f, 0.06f)] float hostileSize = 0.014f;

    [Tooltip("저작한 마을·소굴 표식. 지역 하나가 지도에서 차지하는 폭과 비슷하게 두면 " +
             "편집 창에서 본 그림과 겹쳐 읽힌다.")]
    [SerializeField, Range(0.005f, 0.06f)] float siteSize = 0.020f;

    Texture2D _tex;
    bool      _fullBake = true;   // 처음 · 판을 새로 열었을 때
    RectInt   _dirty;
    bool      _hasDirty;
    Color[]   _biome;        // 바이옴 색 — 4m 눈금으로 한 번만 센다(판 도중에 안 바뀐다)
    const int BiomeStep = 4;
    float     _rebakeAt;
    readonly List<GameObject> _markers = new();

    public bool IsOpen => panel != null && panel.activeSelf;

    void OnEnable()  => RockMap.OnChanged += OnRockChanged;
    void OnDisable() => RockMap.OnChanged -= OnRockChanged;

    // 캐거나 찍을 때 불린다 — **찍은 것은 지도에 안 나오지만** 칸 하나라 굽는 값이 없다.
    // `Clear`는 `Vector2Int.zero`로 부르므로 그때는 통째로 굽는다.
    void OnRockChanged(Vector2Int t)
    {
        if (t == Vector2Int.zero) { _fullBake = true; return; }
        Grow(new RectInt(t.x, t.y, 1, 1));
    }

    void Grow(RectInt r)
    {
        if (!_hasDirty) { _dirty = r; _hasDirty = true; return; }
        int xMin = Mathf.Min(_dirty.xMin, r.xMin), yMin = Mathf.Min(_dirty.yMin, r.yMin);
        int xMax = Mathf.Max(_dirty.xMax, r.xMax), yMax = Mathf.Max(_dirty.yMax, r.yMax);
        _dirty = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    void Awake()
    {
        if (markerTemplate != null) markerTemplate.SetActive(false);
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        // **New Input System을 쓴다** — 이 프로젝트는 Player Settings에서 옛 `Input`을 껐다.
        // `UnityEngine.Input`을 부르면 예외가 난다(`DraftController`·`ColonyUI`와 같은 규약).
        var kb = Keyboard.current;
        if (kb != null && kb.mKey.wasPressedThisFrame && !InputFocus.KeysBlocked()) Toggle();

        // **닫혀 있어도 가 본 곳은 모은다** — 안 모으면 열었을 때 그동안 넓힌 곳이 안 그려진다.
        if (FogOfWar.TakeExploredDirty(out var explored)) Grow(explored);

        if (!IsOpen) return;

        RefreshMarkers();

        _rebakeAt -= Time.unscaledDeltaTime;   // 일시정지 중에도 지도는 본다
        if (_rebakeAt <= 0f) Bake();
    }

    public void Toggle()
    {
        if (panel == null) return;
        if (IsOpen) { WindowRaiser.Close(panel); return; }

        Bake();
        WindowRaiser.Open(panel);
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    void Bake()
    {
        if (mapImage == null) return;
        _rebakeAt = rebakeSeconds;

        int n = TerrainGrid.WorldSize;
        if (_tex == null)
        {
            _tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            mapImage.texture = _tex;
            _fullBake = true;
        }

        RectInt area;
        if (_fullBake) area = new RectInt(0, 0, n, n);
        else if (_hasDirty)
        {
            // 가장자리가 흐리게 번지는 몫(이중선형)으로 한 칸 넓힌다.
            int xMin = Mathf.Max(0, _dirty.xMin - 1), yMin = Mathf.Max(0, _dirty.yMin - 1);
            int xMax = Mathf.Min(n, _dirty.xMax + 1), yMax = Mathf.Min(n, _dirty.yMax + 1);
            area = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
        else return;   // 바뀐 것이 없다 — 아무것도 안 올린다

        _fullBake = false;
        _hasDirty = false;

        // **색은 바이옴이 든다**(`TerrainHeight.MapColor`) — 픽셀마다 물으면 백만 번이라 4m
        // 눈금으로 한 번만 센다. 경계가 4m 계단이 되지만 1,024m 지도에서 한 픽셀 남짓이다.
        int bn = n / BiomeStep;
        if (_biome == null)
        {
            _biome = new Color[bn * bn];
            for (int y = 0; y < bn; y++)
            for (int x = 0; x < bn; x++)
                _biome[y * bn + x] = TerrainHeight.MapColor((x + 0.5f) * BiomeStep, (y + 0.5f) * BiomeStep);
        }

        Color32 dark = unexploredColor;
        bool rockWorld = RockMap.Enabled;
        var explored = FogOfWar.ExploredRaw;          // null = 안개 없음(전부 가 봤다)
        var px = _tex.GetPixelData<Color32>(0);        // 텍스처 메모리에 바로 쓴다 — 4MB 사본을 안 만든다

        for (int y = area.yMin; y < area.yMax; y++)
        {
            int brow = Mathf.Min(bn - 1, y / BiomeStep) * bn;
            int row  = y * n;
            for (int x = area.xMin; x < area.xMax; x++)
            {
                int seenB = explored != null ? explored[row + x] : 255;
                if (seenB == 0) { px[row + x] = dark; continue; }

                Color b = _biome[brow + Mathf.Min(bn - 1, x / BiomeStep)];
                bool rock = rockWorld && RockMap.IsRock(new Vector2Int(x, y));

                Color c = rock ? b * rockShade : Color.Lerp(b, Color.white, openLift);
                c.a = 1f;
                px[row + x] = seenB >= 255 ? (Color32)c : Color32.Lerp(dark, c, seenB / 255f);
            }
        }

        _tex.Apply(false, false);
    }

    // ── 표식 ────────────────────────────────────────────────────────────────

    void RefreshMarkers()
    {
        if (markerRoot == null || markerTemplate == null) return;

        float span = markerRoot.rect.size.x;
        int used = 0;

        // ── 세계에 실제로 있는 장소 ─────────────────────────────────────────
        //
        // **저작한 격자가 아니라 구운 세계를 읽는다**(`WorldSites`, 2026-08-27).
        //
        // 전에는 `WorldMap.AuthoredContent`로 격자를 훑었다. 그때는 그것이 유일한 출처였지만,
        // 세계를 저작 씬에서 짓게 되면서 **손으로 놓은 마을이 게임 지도에 안 떴다** —
        // 격자에 안 찍혀 있으니까. **세계에 있는데 지도가 모르는 것**이 생긴 것이다.
        //
        // 색인은 굽힌 청크 에셋에서 온다. 그래서 **씬에서 옮기면 지도도 따라오고**, 한 지역에
        // 하나라는 격자의 제한도 없어진다.
        foreach (var site in WorldSites.All)
        {
            Color col;
            switch (site.Kind)
            {
                case WorldNodeContent.Town:         col = townColor;     break;
                case WorldNodeContent.EnemyLair:    col = lairColor;     break;
                default: continue;
            }

            // **친 소굴은 안 그린다.** 저작물에는 그대로 있고 없어진 것은 런타임 상태다
            // (`WorldLairs._gone`) — 전에는 저작 격자를 `Empty`로 고쳐서 지웠는데, 자리가
            // 굽힌 에셋으로 오면서 그럴 수가 없다. **표식만 남으면 친 소굴과 안 친 소굴이
            // 지도에서 같아 보인다.**
            if (site.Kind == WorldNodeContent.EnemyLair && WorldLairs.IsGone(site.Id)) continue;

            // **가 본 곳의 장소만** — 안 가 본 검은 땅 위에 마을 점이 떠 있으면 안개가 지도에서 샌다.
            var at = new Vector3(site.Position.x, 0f, site.Position.y);
            if (FogOfWar.ExploredAt(at) < 0.5f) continue;

            used = Mark(used, new Vector3(site.Position.x, 0f, site.Position.y), col, span * siteSize);
        }

        // ── 런타임에 생긴 것 — 레지스트리가 답한다 ──────────────────────────
        //
        // **저작 격자를 안 훑는다**(2026-08-27). 채집기지·정착지는 **플레이 중에 생기는 것**이라
        // 저작물에 있을 이유가 없었는데, `OutpostManager.Build`가 실제로 격자를 고치고 있었다.
        // 내 거점은 아래에서 `DungeonCore.All`로 이미 그리므로 여기서는 채집기지만 본다.
        var outposts = OutpostManager.Instance;
        if (outposts != null)
            foreach (var o in outposts.All)
                used = Mark(used, new Vector3(o.Position.x, 0f, o.Position.y), outpostColor, span * siteSize);

        // **지금 보는 곳이 맨 먼저다** — 표식이 겹치면 나중에 그린 것이 위로 오는데,
        // 그중 하나만 늘 보여야 한다면 그건 *내가 어디 있나*다. …라서 오히려 **맨 뒤에** 그린다.
        foreach (var c in DungeonCore.All)
            if (c != null) used = Mark(used, c.transform.position, coreColor, span * coreSize);

        // **적도 그린다.** 전에는 플레이어 세력만 그려서, `Site_Lair`가 세운 **실제 산적이
        // 지도에 안 떴다** — 무리(`WorldBand`)는 토큰이라 그렸는데, 실체가 된 뒤나 장소가
        // 세운 폰은 토큰이 아니다. 지도를 보는 이유가 *"어디가 위험한가"*인데 그게 빠져 있었다.
        var mine = Faction.Player;
        foreach (var f in Faction.All)
        {
            if (f == null) continue;
            if (f.IsPlayer) { used = Mark(used, f.transform.position, playerColor, span * pawnSize); continue; }
            // **지금 안 보이는 적은 안 그린다** — 화면에서 가린 폰을 지도가 알려 주면 안개가 아니다.
            if (mine != null && mine.IsHostileTo(f) && FogOfWar.IsVisible(f.transform.position))
                used = Mark(used, f.transform.position, hostileColor, span * hostileSize);
        }

        var mgr = WorldBandManager.Instance;
        if (mgr != null)
            foreach (var b in mgr.Bands)
                if (b != null && b.IsAlive && FogOfWar.IsVisible(b.Position))   // 무리도 보일 때만
                    used = Mark(used, b.Position, bandColor, span * bandSize);

        // **맨 마지막 = 맨 위.** 다른 표식과 겹쳐도 이건 보여야 한다.
        var cam = CameraController.Active;
        if (cam != null) used = Mark(used, cam.transform.position, viewColor, span * viewSize);

        for (int i = used; i < _markers.Count; i++) _markers[i].SetActive(false);
    }

    int Mark(int index, Vector3 world, Color color, float size)
    {
        while (_markers.Count <= index)
        {
            var m = Instantiate(markerTemplate, markerRoot);
            m.name = "Marker";
            _markers.Add(m);
        }

        var go = _markers[index];
        go.SetActive(true);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = ToMap(world);

        var img = go.GetComponent<Image>();
        if (img != null) img.color = color;

        return index + 1;
    }

    // 월드 → 지도 칸. **`markerRoot`의 크기를 쓴다** — 지도 이미지와 표식 판이 같은 크기여야
    // 표식이 지형 위에 맞는다(에디터 도구가 그렇게 세운다).
    Vector2 ToMap(Vector3 world)
    {
        var size = markerRoot.rect.size;
        float u = Mathf.Clamp01(world.x / TerrainGrid.WorldSize);
        float v = Mathf.Clamp01(world.z / TerrainGrid.WorldSize);
        return new Vector2((u - 0.5f) * size.x, (v - 0.5f) * size.y);
    }

    // ── 클릭: 그 자리를 본다 ────────────────────────────────────────────────

    // 지도의 한 점을 누르면 **카메라가 그리로 간다.** 폰을 보내지는 않는다 — 이동은 여전히
    // 세계에서 우클릭으로 한다(29km를 클릭 한 번에 건너뛰면 걸어가는 값이 사라진다).
    //
    // `WorldMapClickArea`가 지도 위 클릭을 정규화 좌표로 바꿔 이걸 부른다.
    public void LookAt(Vector2 normalized)
    {
        var cam = CameraController.Active;
        if (cam == null) return;

        float x = normalized.x * TerrainGrid.WorldSize;
        float z = normalized.y * TerrainGrid.WorldSize;
        cam.FocusOn(new Vector3(x, TerrainHeight.At(x, z), z));

        if (panel != null) WindowRaiser.Close(panel);
    }

    // **커서가 짚은 자리가 어디인가.** 지역 좌표와 **바이옴 이름**을 띄운다 — 지도가 색을
    // 칠해 놓고 이름을 아무 데도 안 적어서, *"균사림이 어디냐"*에 답할 길이 없었다.
    //
    // **`LookAt`과 같은 식으로 좌표를 낸다** — 갈리면 짚은 곳과 날아가는 곳이 어긋난다.
    // 이름과 색의 단일 출처는 `Biome`의 확장 메서드다(`DisplayName`·`Color`).
    public void Describe(Vector2 normalized)
    {
        float x = normalized.x * TerrainGrid.WorldSize;
        float z = normalized.y * TerrainGrid.WorldSize;

        // **안 가 본 곳은 이름도 안 알려 준다** — 검게 칠해 놓고 툴팁이 *"수정동"*이라고 말하면 새어 나간다.
        if (FogOfWar.ExploredAt(new Vector3(x, 0f, z)) < 0.5f)
        {
            HoverTooltipUI.Show("탐험하지 않은 곳", $"월드 ({x:F0}, {z:F0})");
            return;
        }

        var region = TerrainGrid.RegionAt(x, z);
        var biome  = WorldMap.AuthoredBiome(region.x, region.y);
        var c      = biome.Color();
        int tier   = BiomeTier.Of(biome);

        HoverTooltipUI.Show(
            $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{biome.DisplayName()}</color>",
            $"지역 ({region.x}, {region.y}) · 월드 ({x:F0}, {z:F0})\n" +
            $"티어 {tier} — {BiomeTier.Label(tier)}");
    }

    void OnDestroy() { if (_tex != null) Destroy(_tex); }
}
