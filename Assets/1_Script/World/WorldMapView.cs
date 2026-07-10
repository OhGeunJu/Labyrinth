using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 마운트앤블레이드식 실시간 월드맵 뷰 (같은 씬, 카메라/루트 토글).
// - 지도면: 노드별 바이옴 색을 Texture2D에 칠해 한 장의 평면에 입힘(Bilinear로 블렌딩 → 지도 느낌, 바둑판 X).
//   안개=어두움. 파티가 지나며 노드 발견 → 해당 텍셀 갱신으로 지도가 드러남.
// - 정착지/POI만 별도 마커로 지도 위에 표시. 나머지 노드는 마커 없이 지형으로만.
// - 파티 토큰이 노드 그래프를 따라 실시간 이동(지도 클릭 → 그 노드까지 BFS).
// - M키로 필드↔월드 카메라 토글. 정착지 정박 중 Enter → 그 정착지 필드맵 진입.
// 지도/마커/토큰은 런타임 생성. 에디터는 worldCamera/mapRoot/컴포넌트만 구성.
[DefaultExecutionOrder(-40)]
public class WorldMapView : MonoBehaviour
{
    [SerializeField] Camera worldCamera;     // 월드맵 전용 카메라 (기본 비활성)
    [SerializeField] Transform mapRoot;      // 지도/마커/토큰 부모 (필드와 겹치지 않게 먼 곳)
    [SerializeField] float nodeSpacing = 4f;
    [SerializeField] float partySpeed = 5f;  // 월드유닛/초
    [SerializeField] Key toggleKey = Key.M;
    [SerializeField] Key formKey   = Key.G;   // 필드에서 선택 유닛을 원정대에 합류

    Camera _fieldCamera;
    SelectionManager _selection;
    bool _active;

    Texture2D _mapTex;
    int _w, _h;
    readonly Dictionary<Vector2Int, GameObject> _poiMarkers = new();

    GameObject _partyGo;
    ExpeditionParty _party;
    readonly Queue<Vector2Int> _path = new();

    void Start()
    {
        _fieldCamera = Camera.main;
        _selection = FindFirstObjectByType<SelectionManager>();
        if (mapRoot == null || WorldMap.Instance == null)
        {
            Debug.LogWarning("[WorldMapView] mapRoot / WorldMap.Instance 미설정");
            return;
        }
        BuildMap();
        SpawnPlayerParty();
        WorldMap.OnNodeChanged += OnNodeChanged;
        SetActiveView(false);
    }

    void OnDestroy() => WorldMap.OnNodeChanged -= OnNodeChanged;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb[toggleKey].wasPressedThisFrame) SetActiveView(!_active);

        if (!_active)
        {
            if (kb != null && kb[formKey].wasPressedThisFrame) FormFromSelection();
            return;
        }

        HandleClick();
        HandleEnter(kb);
        MoveParty();
    }

    // ── 뷰 토글 ───────────────────────────────────────────────────────────────
    void SetActiveView(bool on)
    {
        _active = on;
        if (mapRoot != null) mapRoot.gameObject.SetActive(on);
        if (worldCamera != null) worldCamera.enabled = on;
        if (_fieldCamera != null) _fieldCamera.enabled = !on;
    }

    // ── 좌표 변환 ─────────────────────────────────────────────────────────────
    Vector3 NodeWorld(Vector2Int n) =>
        mapRoot.position + new Vector3((n.x + 0.5f) * nodeSpacing, 0f, (n.y + 0.5f) * nodeSpacing);

    bool WorldToNode(Vector3 world, out Vector2Int node)
    {
        var local = world - mapRoot.position;
        int nx = Mathf.FloorToInt(local.x / nodeSpacing);
        int ny = Mathf.FloorToInt(local.z / nodeSpacing);
        node = new Vector2Int(nx, ny);
        return WorldMap.Instance.InBounds(node);
    }

    // ── 지도면 ────────────────────────────────────────────────────────────────
    void BuildMap()
    {
        var wm = WorldMap.Instance;
        _w = wm.Width; _h = wm.Height;

        _mapTex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,   // 블렌딩 → 지도 느낌
            wrapMode   = TextureWrapMode.Clamp,
        };
        for (int x = 0; x < _w; x++)
        for (int y = 0; y < _h; y++)
            _mapTex.SetPixel(x, y, TileColor(new Vector2Int(x, y)));
        _mapTex.Apply();

        // 바닥 평면 (Quad를 눕힘). 좌하단 모서리 = mapRoot.position.
        var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        plane.name = "MapSurface";
        plane.transform.SetParent(mapRoot, false);
        plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        plane.transform.localScale = new Vector3(_w * nodeSpacing, _h * nodeSpacing, 1f);
        plane.transform.localPosition = new Vector3(_w * nodeSpacing / 2f, 0f, _h * nodeSpacing / 2f);

        var mat = UnlitMat();
        SetTex(mat, _mapTex);
        SetColor(mat, Color.white);
        plane.GetComponent<Renderer>().material = mat;

        // POI 마커 (발견된 정착지/마을/소굴/자원)
        for (int x = 0; x < _w; x++)
        for (int y = 0; y < _h; y++)
            RefreshPoi(new Vector2Int(x, y));
    }

    Color TileColor(Vector2Int node)
    {
        var t = WorldMap.Instance.GetTile(node);
        return !t.Discovered ? new Color(0.08f, 0.08f, 0.11f) : BiomeColor(t.Biome);
    }

    void OnNodeChanged(Vector2Int node)
    {
        if (_mapTex != null && (uint)node.x < (uint)_w && (uint)node.y < (uint)_h)
        {
            _mapTex.SetPixel(node.x, node.y, TileColor(node));
            _mapTex.Apply();
        }
        RefreshPoi(node);
    }

    void RefreshPoi(Vector2Int node)
    {
        var t = WorldMap.Instance.GetTile(node);
        bool show = t.Discovered && t.Content != WorldNodeContent.Empty;

        if (!show)
        {
            if (_poiMarkers.TryGetValue(node, out var old) && old != null) Destroy(old);
            _poiMarkers.Remove(node);
            return;
        }
        if (_poiMarkers.ContainsKey(node)) return;

        var m = GameObject.CreatePrimitive(PrimitiveType.Cube);
        m.name = $"POI_{node.x}_{node.y}";
        m.transform.SetParent(mapRoot, false);
        m.transform.localPosition = NodeWorld(node) - mapRoot.position + new Vector3(0, 0.4f, 0);
        m.transform.localScale = Vector3.one * (nodeSpacing * 0.35f);
        Destroy(m.GetComponent<Collider>());   // 지도면 클릭 레이캐스트를 막지 않게
        var mat = UnlitMat(); SetColor(mat, ContentColor(t.Content));
        m.GetComponent<Renderer>().material = mat;
        _poiMarkers[node] = m;
    }

    // ── 파티 토큰 ─────────────────────────────────────────────────────────────
    void SpawnPlayerParty()
    {
        var wm = WorldMap.Instance;
        Vector2Int start = wm.Settlements.Count > 0 ? wm.Settlements[0] : wm.StartNode;
        _party = new ExpeditionParty(start);

        _partyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        _partyGo.name = "PartyToken";
        _partyGo.transform.SetParent(mapRoot, false);
        _partyGo.transform.localScale = Vector3.one * (nodeSpacing * 0.35f);
        _partyGo.transform.localPosition = NodeWorld(start) - mapRoot.position + new Vector3(0, 1f, 0);
        Destroy(_partyGo.GetComponent<Collider>());
        var mat = UnlitMat(); SetColor(mat, new Color(1f, 0.85f, 0.2f));
        _partyGo.GetComponent<Renderer>().material = mat;
    }

    void MoveParty()
    {
        if (_path.Count == 0 || _partyGo == null) return;

        var target = NodeWorld(_path.Peek());
        target.y = _partyGo.transform.position.y;
        _partyGo.transform.position = Vector3.MoveTowards(
            _partyGo.transform.position, target, partySpeed * Time.deltaTime);

        if ((_partyGo.transform.position - target).sqrMagnitude < 0.01f)
        {
            var reached = _path.Dequeue();
            _party.CurrentNode = reached;
            WorldMap.Instance.Discover(reached);
            foreach (var nb in WorldMap.Instance.Neighbors(reached))
                WorldMap.Instance.Discover(nb);
        }
    }

    // ── 입력 ──────────────────────────────────────────────────────────────────
    void HandleClick()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || worldCamera == null) return;

        var ray = worldCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 2000f) && WorldToNode(hit.point, out var node))
            CommandMove(node);
    }

    void HandleEnter(Keyboard kb)
    {
        if (kb == null || !kb.enterKey.wasPressedThisFrame || _party == null || _path.Count > 0) return;
        var t = WorldMap.Instance.GetTile(_party.CurrentNode);
        if (t.Content == WorldNodeContent.MySettlement)
            EnterSettlement(_party.CurrentNode);
    }

    // ── 파티 편성/복귀 ────────────────────────────────────────────────────────
    // 필드에서 선택된 플레이어 유닛을 원정대에 합류 → 필드에서 제거(비활성).
    void FormFromSelection()
    {
        if (_selection == null) _selection = FindFirstObjectByType<SelectionManager>();
        if (_selection == null || _party == null) return;

        var brains = new List<UnitBrain>();
        foreach (var pm in _selection.Selected)
        {
            if (pm == null) continue;
            var b = pm.GetComponent<UnitBrain>();
            if (b != null) brains.Add(b);
        }
        AddMembers(brains);
    }

    void AddMembers(IEnumerable<UnitBrain> units)
    {
        if (_party == null || units == null) return;
        int added = 0;
        foreach (var u in units)
        {
            if (u == null || _party.Members.Contains(u)) continue;
            _party.Members.Add(u);
            u.gameObject.SetActive(false);   // 거점 필드맵에서 빠짐(노동력 손실)
            added++;
        }
        if (added > 0)
        {
            UpdateTokenScale();
            Debug.Log($"[WorldMap] 원정대 +{added} (총 {_party.Members.Count}명)");
        }
    }

    // 정착지 진입: 뷰 전환 + 원정대 멤버를 그 거점에 복귀(재활성).
    void EnterSettlement(Vector2Int node)
    {
        SettlementManager.Instance?.SwitchToNode(node);

        var core = DungeonCore.Instance;   // SwitchTo가 SetAsActive로 갱신
        foreach (var m in _party.Members)
        {
            if (m == null) continue;
            m.gameObject.SetActive(true);
            if (core != null) m.transform.position = core.GetSpawnPosition();
        }
        _party.Members.Clear();
        UpdateTokenScale();
        SetActiveView(false);
    }

    void UpdateTokenScale()
    {
        if (_partyGo == null) return;
        float s = nodeSpacing * (0.35f + Mathf.Min(_party.Members.Count, 8) * 0.03f);
        _partyGo.transform.localScale = Vector3.one * s;
    }

    void CommandMove(Vector2Int target)
    {
        var path = FindNodePath(_party.CurrentNode, target);
        if (path == null) return;
        _path.Clear();
        foreach (var n in path) _path.Enqueue(n);
    }

    static List<Vector2Int> FindNodePath(Vector2Int from, Vector2Int to)
    {
        if (from == to) return new List<Vector2Int>();
        var wm = WorldMap.Instance;
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var visited = new HashSet<Vector2Int> { from };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nb in wm.Neighbors(cur))
            {
                if (!visited.Add(nb)) continue;
                prev[nb] = cur;
                if (nb == to)
                {
                    var path = new List<Vector2Int>();
                    var c = to;
                    while (c != from) { path.Add(c); c = prev[c]; }
                    path.Reverse();
                    return path;
                }
                q.Enqueue(nb);
            }
        }
        return null;
    }

    // ── 색/머티리얼 ───────────────────────────────────────────────────────────
    static Color BiomeColor(Biome b) => b switch
    {
        Biome.Plains => new Color(0.45f, 0.70f, 0.35f),
        Biome.Forest => new Color(0.18f, 0.42f, 0.20f),
        Biome.Desert => new Color(0.85f, 0.76f, 0.48f),
        Biome.Tundra => new Color(0.80f, 0.86f, 0.90f),
        Biome.Lava   => new Color(0.70f, 0.25f, 0.16f),
        Biome.Cavern => new Color(0.38f, 0.38f, 0.42f),
        Biome.Swamp  => new Color(0.36f, 0.40f, 0.26f),
        _            => new Color(0.28f, 0.28f, 0.30f),
    };

    static Color ContentColor(WorldNodeContent c) => c switch
    {
        WorldNodeContent.MySettlement => new Color(1f, 0.9f, 0.3f),
        WorldNodeContent.Town         => new Color(0.4f, 0.6f, 1f),
        WorldNodeContent.EnemyLair    => new Color(0.9f, 0.2f, 0.2f),
        WorldNodeContent.ResourceSite => new Color(0.3f, 0.85f, 0.85f),
        _                             => Color.white,
    };

    static Material UnlitMat()
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Unlit/Texture")
                 ?? Shader.Find("Sprites/Default");
        return new Material(sh);
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
    }

    static void SetTex(Material m, Texture t)
    {
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
        m.mainTexture = t;
    }
}
