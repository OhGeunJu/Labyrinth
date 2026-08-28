using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// **지도 — 별도 화면이 아니라 창 하나**(2026-08-26).
//
// 세계가 이어지면서 `WorldMapView`(모드를 갈아 끼우고 카메라를 통째로 넘기던 것)의 자리가
// 사라졌다. 그런데 **개관과 빠른 이동은 여전히 필요하다** — 29km를 걸어 다니면서 지금 어디쯤
// 있는지, 무리가 어디서 오는지를 알 길이 있어야 한다.
//
// **지형에서 그린다.** 높이 원본이 `TerrainHeight`(노이즈 + 델타)라 **로드 안 된 곳도
// 계산된다** — 그래서 가 본 적 없는 데까지 한 번에 그릴 수 있다. 안개는 없다(2026-06-20 결정).
//
// **한 번 굽고 캐싱한다** — 256×256이면 `Procedural()`을 65,536번 부르는데, 그건 열 때마다
// 할 일이 아니다. 지형 프로필이 바뀌면 `Rebake()`.
public class WorldMapPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RawImage   mapImage;
    [SerializeField] RectTransform markerRoot;
    [SerializeField] GameObject markerTemplate;

    [Header("지도")]
    [Tooltip("가로세로 몇 픽셀로 구울지. 클수록 곱지만 굽는 데 오래 걸린다(제곱으로).")]
    [SerializeField, Range(64, 512)] int resolution = 256;

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
    [SerializeField] Color resourceColor = new(0.95f, 0.65f, 0.20f);
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
    readonly List<GameObject> _markers = new();

    public bool IsOpen => panel != null && panel.activeSelf;

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
        if (kb != null && kb.mKey.wasPressedThisFrame && !IsTyping()) Toggle();

        if (IsOpen) RefreshMarkers();
    }

    // 글자를 치는 중이면 단축키를 안 먹는다 — 이름 칸에 'm'을 쓰다가 지도가 열리면 안 된다.
    static bool IsTyping()
    {
        var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (sel == null) return false;

        return sel.GetComponent<TMPro.TMP_InputField>() != null
            || sel.GetComponent<InputField>() != null;
    }

    public void Toggle()
    {
        if (panel == null) return;
        if (IsOpen) { panel.SetActive(false); return; }

        Bake();
        WindowRaiser.Open(panel);
    }

    // ── 굽기 ────────────────────────────────────────────────────────────────

    public void Rebake() { if (_tex != null) { Destroy(_tex); _tex = null; } Bake(); }

    void Bake()
    {
        if (_tex != null || mapImage == null) return;

        int n = resolution;
        _tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

        float step = TerrainGrid.WorldSize / (float)n;
        var px = new Color32[n * n];

        // **색은 바이옴이 든다**(`TerrainHeight.MapColor`) — 높이만으로 칠하면 저작해 둔
        // 지도가 화면에 안 나타나서 사막도 숲도 같은 초록이 된다. 그리고 **잔기복을 뺀 높이**로
        // 명암을 준다: 한 픽셀이 64m인데 잔기복 주기가 80m라 그대로 그리면 **모아레가 낀다.**
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
            px[y * n + x] = TerrainHeight.MapColor(x * step, y * step);

        _tex.SetPixels32(px);
        _tex.Apply(false, false);
        mapImage.texture = _tex;
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
                case WorldNodeContent.ResourceSite: col = resourceColor; break;
                default: continue;
            }

            // **친 소굴은 안 그린다.** 저작물에는 그대로 있고 없어진 것은 런타임 상태다
            // (`WorldLairs._gone`) — 전에는 저작 격자를 `Empty`로 고쳐서 지웠는데, 자리가
            // 굽힌 에셋으로 오면서 그럴 수가 없다. **표식만 남으면 친 소굴과 안 친 소굴이
            // 지도에서 같아 보인다.**
            if (site.Kind == WorldNodeContent.EnemyLair && WorldLairs.IsGone(site.Id)) continue;

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
            if (mine != null && mine.IsHostileTo(f))
                used = Mark(used, f.transform.position, hostileColor, span * hostileSize);
        }

        var mgr = WorldBandManager.Instance;
        if (mgr != null)
            foreach (var b in mgr.Bands)
                if (b != null && b.IsAlive) used = Mark(used, b.Position, bandColor, span * bandSize);

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

        if (panel != null) panel.SetActive(false);
    }

    void OnDestroy() { if (_tex != null) Destroy(_tex); }
}
