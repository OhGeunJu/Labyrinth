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

    // **플레이어가 손으로 내려놓은 것은 자동으로 치우지 않는다.**
    //
    // 일부러 내려놓은 물건을 폰이 도로 창고에 넣으면 "내려놓기"가 곧 "창고로 보내기"가 되어,
    // 어디에 두고 싶어도 둘 수가 없다 — 짐내림을 꺼 둔 폰의 짐도 바닥을 거쳐 창고로 흘러간다
    // (짐내림과 운반은 다른 축이라 끈 쪽만으로는 막히지 않는다).
    //
    // 사건으로 떨어진 것(발견·철거 환불·작업대 넘침·수확 넘침)은 그대로 운반된다 — 그건
    // **아무도 의도하지 않은 자리**라서 치우는 것이 맞다. 다시 나르게 하려면 우클릭 [창고로 옮긴다].
    public bool NoAutoHaul { get; private set; }

    // "이제 가져가라" — 손으로 내려놓은 것을 다시 운반 대상으로 되돌린다.
    public void AllowAutoHaul()
    {
        if (!NoAutoHaul) return;
        NoAutoHaul = false;
        TryRegisterHaul();
    }

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
                                    ItemQuality quality = ItemQuality.Common,
                                    bool noAutoHaul = false)
    {
        var go = new GameObject($"Drop_{item.ItemName}");
        go.transform.position = position;

        var col = go.AddComponent<SphereCollider>();
        // 우클릭으로 집으려면 **맞힐 수 있어야** 한다 — 0.3은 꾸러미 모델보다 작아서
        // 눌러도 자꾸 바닥이 잡혔다. 트리거라 이동을 막지는 않는다.
        col.radius    = 0.5f;
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
        dropped.Item       = item;
        dropped.Amount     = amount;
        dropped.Quality    = quality;
        dropped.NoAutoHaul = noAutoHaul;

        dropped.TryRegisterHaul();
        return dropped;
    }

    // ── 운반 일감 등록 ────────────────────────────────────────────────────────
    // **받아 줄 자리가 있을 때만** 건다. 만적인 창고를 가리키는 일감을 걸면 폰이 거기까지
    // 갔다가 못 넣고 짐을 떨구고, 그 짐이 다시 같은 일감이 되어 줍고 떨구기를 반복한다.
    //
    // 자리가 없어 못 걸었으면 짐은 그냥 바닥에 남는다 — 그래서 **주기적으로 다시 본다.**
    // 안 그러면 창고를 비워도 그 짐은 영영 방치된다(일감을 걸어 줄 계기가 없다).
    JobBase _haulJob;
    float   _retryTimer;
    const float RetryInterval = 5f;

    public void TryRegisterHaul()
    {
        if (NoAutoHaul) return;   // 손으로 내려놓은 것 — 시키기 전엔 아무도 안 건드린다
        if (JobManager.Instance == null || Item == null) return;

        // **쌓인 만큼·이 등급으로** 물어본다. 개수를 빼먹으면 한 개 자리만 확인하고 50을
        // 지고 가게 되고, 등급을 빼먹으면 `일반` 스택의 여유를 `희귀` 짐이 쓸 수 있다고 답한다.
        var storage = Storage.FindNearest(transform.position, Item, Amount, Quality);
        if (storage == null) return;      // 아직 받아 줄 곳이 없다 — 다음에 다시 본다

        _haulJob = new GroundHaulJob(this, storage);
        JobManager.Instance.RegisterJob(_haulJob);
    }

    void Update()
    {
        if (NoAutoHaul) return;

        // 일감이 살아 있으면 그만이다. 끝났거나 취소됐거나 애초에 못 걸었을 때만 다시 본다.
        if (_haulJob != null &&
            _haulJob.Status != JobStatus.Done &&
            _haulJob.Status != JobStatus.Cancelled) return;

        _retryTimer -= Time.deltaTime;
        if (_retryTimer > 0f) return;
        _retryTimer = RetryInterval;

        TryRegisterHaul();
    }

    public void PickUp(Inventory inventory) => PickUp(inventory, int.MaxValue);

    // `max`: 이만큼까지만 줍는다(적재 한도). 남는 건 **그 자리에 다시 떨어진다** —
    // 다시 떨어뜨리면 `Spawn`이 새 운반 일감을 걸어 주므로 방치되지 않는다.
    // 한 개도 못 실은 경우에도 같은 길을 탄다(그래야 다른 폰이 이어받을 일감이 생긴다).
    // 반환: 실제로 실은 수량.
    public int PickUp(Inventory inventory, int max)
    {
        int want  = Mathf.Min(Amount, Mathf.Max(0, max));
        int added = want > 0 ? inventory.TryAdd(Item, want, Quality) : 0;

        int leftover = Amount - added;
        if (leftover > 0)
            DroppedItem.Spawn(Item, leftover, transform.position, Quality, NoAutoHaul);
        Destroy(gameObject);
        return added;
    }
}
