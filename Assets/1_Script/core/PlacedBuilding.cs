using System.Collections.Generic;
using UnityEngine;

// 런타임 배치된 건물의 메타(원본 BuildingData)를 보관하고 철거를 담당.
// 배치 시 BuildingDormancy.Ensure처럼 자동 부착(BuildModeManager.Place / BuildingBlueprint.Complete).
// 철거: 점유 해제 + 메소 재베이크 + 건설비 75% 환불(또는 코어 아이템 드랍, step C) + 파괴.
[DisallowMultipleComponent]
public class PlacedBuilding : MonoBehaviour
{
    public static readonly List<PlacedBuilding> All = new();

    [SerializeField] BuildingData data;
    [SerializeField, Range(0f, 1f)] float refundRatio = 0.75f;

    public BuildingData Data => data;

    // 철거 예정 빨간 틴트용
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    static readonly int ColorId     = Shader.PropertyToID("_Color");     // Built-in
    static readonly Color PendingColor = new Color(1f, 0.3f, 0.3f, 1f);

    Renderer[] _renderers;
    bool _highlighted;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // 배치 시 부착 + BuildingData 연결.
    public static PlacedBuilding Attach(GameObject go, BuildingData data)
    {
        if (go == null) return null;
        var pb = go.GetComponent<PlacedBuilding>() ?? go.AddComponent<PlacedBuilding>();
        pb.data = data;
        return pb;
    }

    // 마우스 위치의 건물 PlacedBuilding 반환 (없으면 null).
    public static PlacedBuilding PickUnderCursor(Camera cam, Vector2 screenPos)
    {
        if (cam == null) return null;
        var ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit, 500f))
            return hit.collider.GetComponentInParent<PlacedBuilding>();
        return null;
    }

    // ── 철거 ────────────────────────────────────────────────────────────────
    public void Demolish()
    {
        var pos = transform.position;

        Refund();

        // 점유 타일 해제 (자원노드 등이 막아둔 게 아닌 한 건물 자기 타일)
        if (MapManager.Instance != null)
            MapManager.Instance.SetTile(MapManager.Instance.WorldToTile(pos), TileData.MakePassable());

        Destroy(gameObject);

        // 벽 등 철거 후 메소 격자 복구
        HierarchicalPathfinder.Instance?.RebakeObstacleAt(pos);
    }

    void Refund()
    {
        if (data == null || data.CostAmount <= 0) return;
        int refund = Mathf.FloorToInt(data.CostAmount * refundRatio);
        if (refund <= 0) return;

        var item = DataManager.GetResourceItem(data.CostType);
        if (item == null) return; // 해당 자원 ItemData 없음 → 환불 생략

        // 철거 위치에 드랍 → 자동으로 가장 가까운 창고로 운반.
        DroppedItem.Spawn(item, refund, transform.position);
    }

    // ── 철거 예정 빨간 표시 ─────────────────────────────────────────────────
    public void SetPendingHighlight(bool on)
    {
        if (on == _highlighted) return;
        _highlighted = on;

        _renderers ??= GetComponentsInChildren<Renderer>();
        var mpb = new MaterialPropertyBlock();
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            if (on)
            {
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, PendingColor);
                mpb.SetColor(ColorId,     PendingColor);
                r.SetPropertyBlock(mpb);
            }
            else
            {
                r.SetPropertyBlock(null);
            }
        }
    }
}
