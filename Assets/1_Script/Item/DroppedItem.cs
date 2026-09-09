using UnityEngine;

// 씬에 존재하는 물리 아이템.
// DroppedItem.Spawn()으로 생성 — 프리팹 불필요.
// HaulJob 또는 유닛이 PickUp() 호출 시 인벤토리에 추가 후 자신 제거.
[RequireComponent(typeof(SphereCollider))]
// **찍을 수 있는 것 둘 중 하나다**(`IGatherTarget`) — 나머지 하나는 `Forageable`이다.
// 여기서 *찍혔나*는 곧 `HaulAllowed`다(이름을 하나 더 만들지 않는다).
public class DroppedItem : MonoBehaviour, IGatherTarget
{
    public ItemData    Item    { get; private set; }
    public int         Amount  { get; private set; }
    public ItemQuality Quality { get; private set; }

    // ── 바닥의 물건은 **아무도 안 건드린다. 체크한 것만 실려 간다.** ────────────
    //
    // 예전엔 "누가 놓았나"로 갈랐다 — 손으로 내려놓은 것은 두고, 사건으로 떨어진 것(발견·
    // 철거 환불·작업대/수확 넘침)은 날랐다. 규칙 자체는 말이 됐지만 **화면에서 구별이 안 되고
    // 되돌리는 스위치도 한 방향뿐**이라(켤 수는 있는데 끌 수는 없었다), "왜 이건 나르고
    // 저건 안 나르지"가 고칠 수 없는 것으로 보였다.
    //
    // 지금은 **기본이 전부 꺼짐**이고 켜는 것만 나른다(림월드의 금지 표시와 반대 방향).
    // 규칙이 한 줄이 되고, 켜진 것은 **표식이 떠서 눈에 보인다.**
    //
    // 설비가 내놓는 것(자원 노드·작업대 산출물)은 이 축과 무관하다 — 그쪽은 바닥을 거치지
    // 않고 `HaulDispatcher`가 직접 일감을 낸다. **생산 라인은 그대로 자동이다.**
    // **이 한 개가 찍혔나.** 품목 규칙(`HaulPolicy`)과 **합집합**이다 — 아래 `Hauls` 참조.
    public bool HaulAllowed { get; private set; }

    // ── 실제로 나르나 ───────────────────────────────────────────────────────
    //
    // **이 한 개가 찍혔거나, 그 품목이 켜졌거나**(`HaulPolicy`). 둘 다 *"나른다"*만 말하므로
    // **합집합이고 우선순위 규칙이 없다** — 그래서 "어느 쪽이 이기나"를 정할 일이 없다.
    //
    // 다만 **품목이 켜져 있으면 한 개만 빼 둘 수는 없다.** 그건 *품목 규칙*이라는 말의
    // 뜻 그대로다(빼려면 품목을 끄고 하나씩 찍는다).
    public bool Hauls => HaulAllowed || HaulPolicy.IsAuto(Item);

    // **정책이 바뀌면 이미 바닥에 있는 것도 따라온다** — 켜 놓고 *"왜 저건 안 나르지"*가
    // 되면 스위치가 거짓말이 된다. 훑는 쪽을 정책이 알면(`FindObjectsByType`) 정책이 화면을
    // 아는 꼴이 되므로, **듣는 쪽이 제 것만 다시 맞춘다.**
    void OnEnable()  => HaulPolicy.OnChanged += OnPolicyChanged;
    void OnDisable() => HaulPolicy.OnChanged -= OnPolicyChanged;

    void OnPolicyChanged()
    {
        RefreshMark();
        if (Hauls) TryRegisterHaul();
        else       DropHaulJob();
    }

    // ── `IGatherTarget` — 지정 모드가 보는 얼굴 ────────────────────────────
    //
    // **찍혔나는 이 한 개를 묻는다** — 품목이 켜져 있어도 *이것을 찍었나*는 따로다
    // (안 그러면 품목을 켠 순간 지정 모드가 **무를 것이 하나도 없다**고 답한다).
    bool      IGatherTarget.Designated => HaulAllowed;
    void      IGatherTarget.SetDesignated(bool on) => SetHaulAllowed(on);
    Transform IGatherTarget.Where => transform;

    public void SetHaulAllowed(bool on)
    {
        if (HaulAllowed == on) return;
        HaulAllowed = on;

        RefreshMark();

        if (Hauls) { TryRegisterHaul(); return; }
        DropHaulJob();
    }

    // **껐으면 가던 폰도 돌려세운다.** 헛걸음을 아끼려고 하던 것은 끝내게 두는 규약이
    // 여기엔 안 맞는다 — "여기 둔다"는 **지금 그러지 마라**는 뜻이고, 걸어오는 것을 못
    // 멈추면 껐는데도 물건이 실려 가서 스위치가 거짓말이 된다.
    // (`JobBase.Cancel`은 `WorkNode`가 다음 틱에 알아채고 놓는다 — 밖에서 취소된 일감을
    //  놓는 길은 이미 있다.)
    //
    // **부르는 곳이 둘이다** — 개별 지정을 끌 때와 품목 규칙이 꺼질 때. 각자 적으면
    // 한쪽만 고치는 날이 온다.
    void DropHaulJob()
    {
        if (_haulJob != null)
        {
            JobManager.Instance?.RemoveJob(_haulJob);
            _haulJob.Cancel();
        }
        _haulJob = null;
    }

    // ── 켜졌다는 표식 ─────────────────────────────────────────────────────────
    //
    // **눈에 안 보이면 체크가 아니다.** 우클릭 메뉴를 열어야만 알 수 있으면 예전(누가 놓았나로
    // 갈리던 시절)과 똑같이 "왜 이건 안 실려 가지"가 된다.
    //
    // 물건 위에 작은 구슬 하나를 띄운다. 자식이 하나 늘지만 이 오브젝트의 크기를 재는 곳이
    // 없어 안전하다(침대 표식이 `WorldBounds`를 키워 폰이 떠서 자던 것과 다른 자리다).
    // 색은 `Tint`가 `MaterialPropertyBlock`으로 입히므로 **머티리얼 인스턴스가 안 생긴다.**
    const float MarkHeight = 0.55f;
    static readonly Color MarkColor    = new(0.35f, 0.95f, 0.45f);
    static readonly Color PendingColor = new(0.65f, 0.85f, 0.60f);   // 지정 모드에서 "이번에 찍힐 것"

    GameObject _mark;
    bool       _pendingMark;

    // 운반 지정 모드(`HaulDesignator`)가 **놓기 전에** 무엇이 찍힐지 보여 준다 —
    // 철거 모드의 빨간 예정 표시와 같은 자리. 표식을 따로 만들지 않고 **같은 구슬을 흐린
    // 색으로** 띄운다: 미리보기와 결과가 다른 모양이면 무엇이 될지 알 수 없다.
    public void SetPendingHighlight(bool on)
    {
        if (_pendingMark == on) return;
        _pendingMark = on;
        RefreshMark();
    }

    // ── 선택 표식 ─────────────────────────────────────────────────────────────
    //
    // **여럿을 고를 수 있으면 무엇이 골렸는지 보여야 한다.** 패널이 "물건 7개"라고 적어도
    // 어느 일곱인지 모르면 [전부 창고로 옮긴다]를 누르기가 무섭다.
    //
    // 폰의 발밑 고리(`SelectionIndicator`)와 **같은 모양**을 쓴다(`GroundRing`). 고르는 것이
    // 폰이든 물건이든 "골렸다"는 신호가 같아야 한다. 다만 색은 다르다(폰=미색, 물건=하늘색):
    // 운반 지정 구슬과 같은 색이면 *골렸다*와 *찍혔다*를 구별할 수 없다.
    static readonly Color SelectColor = new(0.45f, 0.8f, 1f, 0.9f);

    GameObject _ring;

    public void SetSelected(bool on)
    {
        if (on)
        {
            if (_ring == null) _ring = GroundRing.Create(transform, 0.03f, 0.3f, SelectColor, "SelectRing");
            return;
        }

        if (_ring != null) { Destroy(_ring); _ring = null; }
    }

    void RefreshMark()
    {
        if (Hauls || _pendingMark)
        {
            if (_mark != null) { Tint(_mark, Hauls ? MarkColor : PendingColor); return; }

            _mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = _mark.GetComponent<Collider>();
            if (col != null) Destroy(col);   // 클릭 판정은 부모 트리거 하나뿐이어야 한다

            _mark.transform.SetParent(transform, false);
            _mark.transform.localPosition = new Vector3(0f, MarkHeight, 0f);
            _mark.transform.localScale    = Vector3.one * 0.12f;
            Tint(_mark, Hauls ? MarkColor : PendingColor);
            return;
        }

        if (_mark != null) { Destroy(_mark); _mark = null; }
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
    // **자동 운반은 안 켜진 채로 나온다** — 떨구는 쪽이 고를 수 없다. 고르는 것은 사람이고,
    // 그래야 "이건 왜 실려 가지"가 안 생긴다(예전엔 부르는 쪽마다 기본값을 정했다).
    public static DroppedItem Spawn(ItemData item, int amount, Vector3 position,
                                    ItemQuality quality = ItemQuality.Common)
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
        dropped.Item    = item;
        dropped.Amount  = amount;
        dropped.Quality = quality;
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
        if (!Hauls) return;   // 안 찍었고 품목도 안 켜졌다 — 시키기 전엔 아무도 안 건드린다
        if (JobManager.Instance == null || Item == null) return;

        // **쌓인 만큼·이 등급으로** 물어본다. 개수를 빼먹으면 한 개 자리만 확인하고 50을
        // 지고 가게 되고, 등급을 빼먹으면 `일반` 스택의 여유를 `희귀` 짐이 쓸 수 있다고 답한다.
        var storage = Storage.FindNearest(transform.position, Item, Amount, Quality);
        if (storage == null) return;      // 아직 받아 줄 곳이 없다 — 다음에 다시 본다

        _haulJob = new GroundHaulJob(this, storage);
        JobManager.Instance.RegisterJob(_haulJob);
    }

    // **켠 것만 다시 본다.** 창고가 꽉 차서 못 걸었을 수 있으므로, 켜 둔 물건은 자리가
    // 날 때까지 주기적으로 다시 시도한다("창고를 비우면 저절로 실려 간다"). 안 켠 물건은
    // 여기서 바로 빠지므로 바닥에 널린 것들이 매 프레임 값을 물리지 않는다.
    void Update()
    {
        if (!Hauls) return;

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

        // **못 실은 잔량은 체크를 물려받는다.** 나르라고 시킨 것의 나머지도 나르라고 시킨
        // 것이다 — 안 물려주면 배낭이 모자란 폰이 한 번 다녀간 것만으로 나머지가 조용히
        // 방치되고, 사람은 "분명 시켰는데"가 된다.
        int leftover = Amount - added;
        if (leftover > 0)
        {
            var rest = DroppedItem.Spawn(Item, leftover, transform.position, Quality);
            if (HaulAllowed) rest.SetHaulAllowed(true);
        }
        Destroy(gameObject);
        return added;
    }
}
