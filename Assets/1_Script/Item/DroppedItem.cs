using UnityEngine;

// 씬에 존재하는 물리 아이템.
// DroppedItem.Spawn()으로 생성 — 프리팹 불필요.
// HaulJob 또는 유닛이 PickUp() 호출 시 인벤토리에 추가 후 자신 제거.
[RequireComponent(typeof(SphereCollider))]
public class DroppedItem : MonoBehaviour
{
    public ItemData    Item    { get; private set; }
    public int         Amount  { get; private set; }
    public ItemQuality Quality { get; private set; }

    // 어디서든 호출 가능한 팩토리 메서드. quality를 보존해 등급 있는 장비가 드롭을 거쳐도 소실되지 않게 한다.
    public static DroppedItem Spawn(ItemData item, int amount, Vector3 position,
                                    ItemQuality quality = ItemQuality.Common)
    {
        var go = new GameObject($"Drop_{item.ItemName}");
        go.transform.position = position;

        var col = go.AddComponent<SphereCollider>();
        col.radius    = 0.3f;
        col.isTrigger = true;

        // 시각 표현: ItemData.WorldModel 우선, 없으면 작은 큐브 폴백.
        GameObject visual;
        if (item.WorldModel != null)
        {
            visual = Object.Instantiate(item.WorldModel);
        }
        else
        {
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.localScale = Vector3.one * 0.25f;
        }
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;
        // 픽업은 부모 SphereCollider 트리거가 담당 → 모델 콜라이더는 제거(레이캐스트/이동 간섭 방지)
        foreach (var c in visual.GetComponentsInChildren<Collider>())
            Object.Destroy(c);

        var dropped = go.AddComponent<DroppedItem>();
        dropped.Item    = item;
        dropped.Amount  = amount;
        dropped.Quality = quality;

        // 수용 가능한 창고가 있으면 운반 작업 자동 등록
        var storage = Storage.FindNearest(position, item);
        if (storage != null && JobManager.Instance != null)
            JobManager.Instance.RegisterJob(new GroundHaulJob(dropped, storage));

        return dropped;
    }

    public void PickUp(Inventory inventory)
    {
        int added    = inventory.TryAdd(Item, Amount, Quality);
        int leftover = Amount - added;
        if (leftover > 0)
            DroppedItem.Spawn(Item, leftover, transform.position, Quality);
        Destroy(gameObject);
    }
}
