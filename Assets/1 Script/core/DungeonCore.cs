using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class DungeonCore : MonoBehaviour
{
    public static DungeonCore Instance { get; private set; }

    [SerializeField] DungeonCoreData data;
    [SerializeField] Color rangeColor = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] int rangeSegments = 64;

    public DungeonCoreData Data => data;
    public Vector2Int GridPos { get; private set; }
    public bool IsSelected { get; private set; }

    [SerializeField] Color selectedColor = new Color(1f, 0.9f, 0.2f, 0.8f);

    Material _mat;

    void Awake()
    {
        Instance = this;
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",    (int)CullMode.Off);
        _mat.SetInt("_ZWrite", 0);
    }

    void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    void OnDestroy() => Destroy(_mat);

    void Start()
    {
        var map = MapManager.Instance;
        GridPos = new Vector2Int(map.Width / 2, map.Height / 2);
        map.SetTile(GridPos, TileData.MakeImpassable());
        SnapToGrid();
    }

    public void Select()   { IsSelected = true; }
    public void Deselect() { IsSelected = false; }

    void Update()
    {
        if (!IsSelected) return;
        if (Mouse.current.leftButton.wasPressedThisFrame)
            TryMoveToMouse();
    }

    void TryMoveToMouse()
    {
        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float dist)) return;

        var map     = MapManager.Instance;
        var tilePos = map.WorldToTile(ray.GetPoint(dist));

        if (!map.IsInBounds(tilePos)) return;
        if (!map.GetTile(tilePos).IsPassable) return;

        map.SetTile(GridPos, TileData.MakePassable());
        GridPos = tilePos;
        map.SetTile(GridPos, TileData.MakeImpassable());
        SnapToGrid();
        Deselect();
    }

    [System.Serializable]
    struct MinionEntry
    {
        public MinionData Data;
        public GameObject Prefab;
    }

    [Header("소환 가능한 하수인 목록")]
    [SerializeField] MinionEntry[] minionEntries;

    // 하수인 소환 — 자원 소비 후 프리팹 스폰
    public bool Summon(MinionData minionData, Vector3 spawnPosition)
    {
        if (minionData == null) return false;

        GameObject prefab = null;
        foreach (var entry in minionEntries)
            if (entry.Data == minionData) { prefab = entry.Prefab; break; }

        if (prefab == null)
        {
            Debug.LogWarning($"[DungeonCore] {minionData.RaceName}에 연결된 프리팹이 없습니다.");
            return false;
        }

        if (!StorageBuilding.Consume(minionData.SummonCostType, minionData.SummonCostAmount))
        {
            Debug.Log($"[DungeonCore] 자원 부족 — {minionData.SummonCostType} ×{minionData.SummonCostAmount} 필요");
            return false;
        }

        Instantiate(prefab, spawnPosition, Quaternion.identity);
        Debug.Log($"[DungeonCore] {minionData.RaceName} 소환 완료");
        return true;
    }

    public bool IsWithinRange(Vector2Int tile)
    {
        if (data == null) return true;
        var wp = MapManager.Instance.TileToWorld(tile);
        var dx = wp.x - transform.position.x;
        var dz = wp.z - transform.position.z;
        return dx * dx + dz * dz <= data.BaseRadius * data.BaseRadius;
    }

    void SnapToGrid()
    {
        var wp = MapManager.Instance.TileToWorld(GridPos);
        transform.position = new Vector3(wp.x, wp.y + 0.6f, wp.z);
    }

    void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != Camera.main) return;
        DrawRange();
    }

    void OnRenderObject()
    {
        if (GraphicsSettings.currentRenderPipeline != null) return;
        DrawRange();
    }

    void DrawRange()
    {
        if (data == null) return;

        float radius = data.BaseRadius;
        Vector3 center = new Vector3(transform.position.x, 0.02f, transform.position.z);

        _mat.SetPass(0);
        GL.PushMatrix();
        GL.Begin(GL.LINE_STRIP);
        GL.Color(IsSelected ? selectedColor : rangeColor);

        for (int i = 0; i <= rangeSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / rangeSegments;
            GL.Vertex3(center.x + Mathf.Cos(angle) * radius,
                       center.y,
                       center.z + Mathf.Sin(angle) * radius);
        }

        GL.End();
        GL.PopMatrix();
    }
}
