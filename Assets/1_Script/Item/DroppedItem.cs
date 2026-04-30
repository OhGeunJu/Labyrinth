using UnityEngine;

// 씬에 존재하는 물리 아이템.
// DroppedItem.Spawn()으로 생성 — 프리팹 불필요.
// HaulJob 또는 유닛이 PickUp() 호출 시 인벤토리에 추가 후 자신 제거.
[RequireComponent(typeof(SphereCollider))]
public class DroppedItem : MonoBehaviour
{
    public ItemData Item   { get; private set; }
    public int      Amount { get; private set; }

    // 어디서든 호출 가능한 팩토리 메서드
    public static DroppedItem Spawn(ItemData item, int amount, Vector3 position)
    {
        var go = new GameObject($"Drop_{item.ItemName}");
        go.transform.position = position;

        var col = go.AddComponent<SphereCollider>();
        col.radius    = 0.3f;
        col.isTrigger = true;

        // 작은 큐브로 시각 표현
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(go.transform);
        cube.transform.localScale   = Vector3.one * 0.25f;
        cube.transform.localPosition = Vector3.zero;
        Object.Destroy(cube.GetComponent<BoxCollider>());

        var dropped = go.AddComponent<DroppedItem>();
        dropped.Item   = item;
        dropped.Amount = amount;

        // 수용 가능한 창고가 있으면 운반 작업 자동 등록
        var storage = StorageBuilding.FindNearest(position, item);
        if (storage != null && JobManager.Instance != null)
            JobManager.Instance.RegisterJob(new GroundHaulJob(dropped, storage));

        return dropped;
    }

    public void PickUp(Inventory inventory)
    {
        inventory.TryAdd(Item, Amount);
        Destroy(gameObject);
    }
}
