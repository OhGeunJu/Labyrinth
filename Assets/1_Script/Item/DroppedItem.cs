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

    // ── 공용 드롭 모델 ────────────────────────────────────────────────────
    // **왜 3_Prefab이 아니라 Resources인가** (프로젝트 기본은 3_Prefab + 인스펙터 참조):
    //   ① `Spawn`이 static 팩토리라 인스펙터에서 프리팹을 꽂아줄 자리가 없다.
    //      `docs/data.md`의 기준 — "코드가 이름만 알고 실물을 몰라도 찾아와야 하는가?" — 에 해당한다.
    //   ② 이건 **절대 실패하면 안 되는 폴백**이다. 씬 참조로 두면 배선을 안 한 새 씬에서
    //      정확히 폴백이 필요한 순간에 없다. 정착지가 여럿 생기면 맵마다 배선을 반복해야 한다.
    // 대신 값은 프리팹 하나 — Resources가 빌드에 강제로 싣는 비용이 무시할 만하다.
    //
    // 한 번만 찾고 캐싱 — 드롭은 자주 생긴다.
    const string BundlePath = "Prefabs/DropBundle";   // Assets/Resources/Prefabs/DropBundle.prefab
    static GameObject _bundle;
    static bool       _bundleLoaded;

    static GameObject BundlePrefab
    {
        get
        {
            if (_bundleLoaded) return _bundle;
            _bundleLoaded = true;
            _bundle = Resources.Load<GameObject>(BundlePath);
            if (_bundle == null)
                Debug.LogWarning($"[DroppedItem] Resources/{BundlePath} 없음 — 큐브로 대체합니다. " +
                                 "DungeonColony → Create Drop Bundle Model 을 실행하세요.");
            return _bundle;
        }
    }

    // 머티리얼을 복제하지 않고 색만 바꾼다 — 드롭마다 머티리얼 인스턴스가 새로 생기면
    // 배칭이 깨지고 누수가 된다(`BuildingDormancy`의 회색 틴트와 같은 방식).
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");

    static void Tint(GameObject visual, Color color)
    {
        var block = new MaterialPropertyBlock();
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
        {
            r.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);   // URP Lit
            block.SetColor(ColorId,     color);   // 빌트인 폴백
            r.SetPropertyBlock(block);
        }
    }

    // 어디서든 호출 가능한 팩토리 메서드. quality를 보존해 등급 있는 장비가 드롭을 거쳐도 소실되지 않게 한다.
    public static DroppedItem Spawn(ItemData item, int amount, Vector3 position,
                                    ItemQuality quality = ItemQuality.Common)
    {
        var go = new GameObject($"Drop_{item.ItemName}");
        go.transform.position = position;

        var col = go.AddComponent<SphereCollider>();
        col.radius    = 0.3f;
        col.isTrigger = true;

        // 시각 표현: ItemData.WorldModel 우선, 없으면 **공용 꾸러미 모델**.
        // 아이템마다 모델을 만들 수는 없으므로 폴백이 대부분의 드롭을 담당한다.
        bool usedFallback = item.WorldModel == null;
        var  source       = usedFallback ? BundlePrefab : item.WorldModel;

        GameObject visual;
        if (source != null)
        {
            visual = Object.Instantiate(source);
        }
        else
        {
            // 꾸러미 프리팹조차 없을 때(생성기 미실행)의 최후 폴백.
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.localScale = Vector3.one * 0.25f;
        }
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;

        // 공용 모델은 무엇이 담겼는지 알려주지 못하므로 **등급 색으로 물들인다** —
        // 툴팁 글자색과 같은 출처(`ItemQualityExt.Color`)라 "저 파란 게 아까 그 희귀템"이 성립한다.
        // 아이템 전용 모델은 제 색이 있으니 건드리지 않는다.
        if (usedFallback) Tint(visual, quality.Color());
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
