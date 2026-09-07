using UnityEngine;

// **한 번 따면 없어지는 것**(2026-09-06, 사용자: *"배정받아서 캐는 버섯이 아니라 그냥 한번
// 캐고 없어지는 작은 버섯"*).
//
// ## `ResourceNode`와 무엇이 다른가 — 축이 통째로 다르다
//
// | | 자원 노드(광맥·거대버섯) | 채집물(갓버섯) |
// |---|---|---|
// | 붙는 법 | **배정**(손으로 붙이거나 `ClaimFreeFor`가 잡는다) | **큐**(누구든 지나가다 집는다) |
// | 일하는 법 | 게이지를 채우고 **되풀이해서** 캔다 | **한 번**에 끝난다 |
// | 끝나면 | 그 자리에 남는다 | **없어진다** |
//
// 갓버섯을 노드로 두었을 때 어긋난 것이 그 셋이다 — 폰 하나가 버섯 한 무리에 *배정되어*
// 눌러앉았고, 다 따도 버섯이 그대로 서 있었다. **작은 것을 큰 것의 축소판으로 만들면
// 작아지지 않는다.**
//
// ## 일감은 하나만 걸어 둔다
//
// `FarmPlot`이 수확 일감을 하나만 걸어 두는 것과 같다(`_harvestJobPending`). 안 그러면 매
// 프레임 같은 일감이 큐에 쌓이고, 폰 열여섯이 **같은 버섯 하나**로 몰린다.
//
// ## 없어지는 것이 곧 다시 나는 것이다
//
// 파괴되면 `ScatterTag`가 순번을 남기고(`RegrowDays`), 그 날짜가 지나면 **뿌리기가 같은
// 자리에 다시 세운다.** 여기서 되살릴 것도, 자리를 저장할 것도 없다.
[DisallowMultipleComponent]
public class Forageable : MonoBehaviour, IGatherTarget
{
    [Tooltip("따면 나오는 것. **바닥에 떨어진다** — 배낭으로 바로 가지 않는다(수확과 같은 규칙).")]
    [SerializeField] ItemData item;

    [Tooltip("한 번에 나오는 개수 범위.")]
    [SerializeField] int minAmount = 1;
    [SerializeField] int maxAmount = 3;

    public ItemData Item => item;

    // 살펴보기 창이 *"무엇이 얼마나 나오나"*에 답하려면 밖에서 보여야 한다.
    public int MinAmount => minAmount;
    public int MaxAmount => maxAmount;

    // **일감을 손에 들고 있는다** — 무를 때 되찾아야 하는데, `JobManager`에 *"이 채집물의
    // 일감"*을 묻는 문이 없다(도축만 그런 문을 갖고 있고, 그건 그쪽 사정이다). 문을 새로
    // 내는 것보다 **낸 쪽이 들고 있는 편**이 짧다.
    ForageJob _job;
    bool _designated;

    // ── 찍어야 딴다 ─────────────────────────────────────────────────────────
    //
    // **기본은 꺼짐이다**(2026-09-06, 사용자: *"버섯을 처음에 자동채집이 켜져있으면 안돼
    // 내가 시켜야지 마치 운반을 시키는 것처럼"*). `DroppedItem.HaulAllowed`가 이미 그
    // 규칙이고(*"바닥의 물건은 아무도 안 건드린다"*), **채집물만 저 혼자 켜져 있으면 같은
    // 화면의 같은 조작이 물건마다 다르게 돈다.**
    //
    // 찍는 문은 **운반 지정 모드 하나**다(`HaulDesignator` — 모드를 새로 만들지 않는 이유는
    // `IGatherTarget`에 적어 두었다).
    public bool Designated => _designated;

    public void SetDesignated(bool on)
    {
        if (_designated == on) return;
        _designated = on;
        RefreshMark();

        if (on) TryRegister();
        else    DropJob();
    }

    Transform IGatherTarget.Where => transform;

    // **`Awake`가 아니라 `Start`다** — 뿌리기가 `Instantiate` 직후 스케일·표식을 붙이므로,
    // 그 전에 일감을 걸면 아직 자리도 안 잡힌 것을 큐에 올린다.
    void Start() { if (_designated) TryRegister(); }

    // 청크가 다시 올라오면 새 오브젝트라 여기서 다시 건다.
    void OnEnable() { if (_designated) TryRegister(); }

    void TryRegister()
    {
        if (_job != null || item == null || JobManager.Instance == null) return;

        _job = new ForageJob(this);
        JobManager.Instance.RegisterJob(_job);
    }

    // **무르면 일감도 거둔다** — 안 거두면 지정을 껐는데 폰이 계속 걸어온다.
    void DropJob()
    {
        if (_job == null) return;

        var job = _job;
        _job = null;                       // 먼저 놓는다 — `CancelJob`이 `OnJobCancelled`를 부른다
        JobManager.Instance?.CancelJob(job);
    }

    public void OnJobCancelled() => _job = null;

    // 딴다 — **떨구고 사라진다.** 부르는 쪽은 `ForageJob` 하나다.
    public void Harvest()
    {
        if (item != null)
        {
            int n = Random.Range(minAmount, maxAmount + 1);

            // **바닥에 떨군다**(`FarmHarvestJob`과 같은 규칙) — 배낭에 바로 넣으면 같은
            // 물건이 두 길로 창고에 들어간다. `HaulAllowed`를 켜 두므로 짐꾼이 알아서 나른다.
            if (n > 0)
                DroppedItem.Spawn(item, n, transform.position + Vector3.up * 0.2f)
                          ?.SetHaulAllowed(true);
        }

        // **`Destroy`가 곧 "다시 날 때까지 없다"이다** — `ScatterTag.OnDestroy`가 순번과
        // 날수를 남긴다. 여기서 그걸 다시 적으면 답이 두 벌이 된다.
        Destroy(gameObject);
    }

    // ── 표식 ────────────────────────────────────────────────────────────────
    //
    // **`DroppedItem`과 같은 초록 구슬이다** — 같은 모드가 찍는 것이니 같은 말을 해야 한다.
    // 표식을 따로 만들면 *"이건 찍힌 건가"*를 물건 종류마다 다시 배우게 된다.
    [SerializeField] GameObject mark;

    bool _pendingMark;

    public void SetPendingHighlight(bool on)
    {
        if (_pendingMark == on) return;
        _pendingMark = on;
        RefreshMark();
    }

    void RefreshMark()
    {
        if (mark == null) mark = BuildMark();
        if (mark == null) return;

        bool show = _designated || _pendingMark;
        mark.SetActive(show);
        if (!show) return;

        // **예정은 흐리게, 찍힌 것은 진하게** — 같은 구슬이라 알파가 유일한 차이다.
        var r = mark.GetComponent<Renderer>();
        if (r != null) FadeMaterial.SetAlpha(r.sharedMaterial, _designated ? 0.95f : 0.45f);
    }

    GameObject BuildMark()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Mark";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        go.transform.localScale    = Vector3.one * 0.16f;

        // **투명하게 만드는 문은 `FadeMaterial` 하나다** — 알파만 낮추면 URP에서는 조용히
        // 불투명하다(표면 종류·블렌드·깊이·키워드를 같이 바꿔야 한다).
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = FadeMaterial.Create(new Color(0.35f, 0.95f, 0.45f, 0.95f));

        go.SetActive(false);
        return go;
    }

    public void Configure(ItemData drop, int min, int max)
    {
        item      = drop;
        minAmount = min;
        maxAmount = max;
    }
}
