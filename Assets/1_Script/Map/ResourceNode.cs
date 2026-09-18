using System;
using System.Collections.Generic;
using UnityEngine;

// **자연에 있는 것 ↔ 지은 것**(2026-09-06, 사용자 결정: *"미궁핵에 건설된 자원들만 배정을
// 받는 거로 할까? 미궁핵에서 에너지를 써서 계속 생산되게 하는거라고 하면 말이 되잖아"*).
//
// | | 자연에 있는 것 | 미궁핵에 지은 것 |
// |---|---|---|
// | 무엇 | 야생 광맥·거대버섯 | `Node_*Vein` 건물(이식 광맥) |
// | 어떻게 | **찍어서 가져온다**(`H`) | **배정한다** |
// | 끝 | **캐다 보면 사라진다**(`TotalYields`) | 계속 내놓는다 |
//
// **한 문장으로: 자연은 찍고, 기지는 배정한다.** 그전에는 둘이 안 갈려서 야생 광맥에도 폰을
// 붙일 수 있었고, 그러면 **굴에서 캐 온 것으로 기지에 광맥을 이식할 이유가 없었다**(밖에서
// 무한히 캐면 되니까). 이식이라는 축이 게임에 있는데 그 축을 쓸 이유를 규칙이 안 만들고 있었다.
//
// **가르는 것은 `PlacedBuilding`이 있느냐 하나다**(`IsBuilt`) — 지은 것에만 그것이 붙는다.
// 저작으로 가르면 노드가 늘 때마다 *"이건 야생인가"*를 사람이 적어야 하고, 하나 빠뜨리면
// **밖에서 무한히 캐지는 광맥**이 조용히 하나 생긴다.
public class ResourceNode : MonoBehaviour, IHaulSource, IGatherTarget
{
    // ── 야생으로 세운다 ────────────────────────────────────────────────────
    //
    // **노드 프리팹은 *이식 광맥*이다** — `Node_*Vein`에는 `PlacedBuilding`과 `PowerUser`가
    // 붙어 있고, 그게 `IsBuilt`의 답이다(윗글의 표). 그대로 굴에 뿌리면 **자연에 있는 것이
    // 지은 것 행세를 한다**: 배정을 받고 · 마르지 않고(`TotalYields`를 안 본다) · 핵 영역
    // 밖이라 **전력을 못 받아 돌지도 않는다.** 걷어낸 자원 지대가 정확히 그랬다.
    //
    // **프리팹을 한 벌 더 만들지 않는다** — 광맥이 열다섯이라 야생판을 따로 두면 서른이
    // 되고, `VeinArt`가 모양을 다시 구울 때마다 **그중 하나는 반드시 뒤처진다.**
    // 가르는 것은 *무엇이냐*가 아니라 **어떻게 세웠느냐**이므로, 세우는 쪽이 답한다.
    //
    // ## 비활성 요람을 거치지 않는다 (2026-09-15에 되돌렸다)
    //
    // 처음에는 **꺼진 부모 아래에서 만들고 `DestroyImmediate`로 떼어 낸 뒤** 옮겼다 —
    // `OnEnable`이 돌기 전에 떼려는 것이었다. 그런데 그렇게 세운 광맥이 **하이어라키에는
    // 서 있는데 화면에 아무것도 안 그려졌다**(콜라이더 와이어만 보였다). 프리팹도 메시도
    // 재질도 멀쩡했으니 **원인이 프리팹이 아니라 세우는 길에 있었다.**
    //
    // `DestroyImmediate`는 런타임에서 **쓰지 말라고 적혀 있는 함수**이고, 거기에 *꺼진
    // 부모 아래의 갓 만든 인스턴스*라는 드문 상태를 겹쳤다. **드문 것 둘을 겹치면 무엇이
    // 범인인지 가릴 수가 없다** — 그래서 둘 다 걷어냈다.
    //
    // 이제 **평범하게 세우고 `MakeWild`로 말해 준다.** 딸려 오는 것 둘은 감당된다:
    //
    //   · `PlacedBuilding.Start`가 제 칸을 막는데, **`ResourceNode.Awake`가 같은 칸을
    //     이미 막는다** — 겹쳐 막는 것이라 결과가 같다.
    //   · `PowerUser`는 `OnEnable`에서 등록하고 `OnDisable`(파괴)에서 빠진다 — 한 프레임
    //     전력 목록에 있다가 나간다. 그 사이에 힘을 먹지는 않는다(`PowerGrid`가 다시 나눌 뿐).
    //
    // **`MakeWild`를 `Destroy`보다 먼저 부르는 것이 요점이다.** `Destroy`는 프레임 끝으로
    // 미뤄지므로, 그 전에 도는 `ResourceNode.Start`가 `IsBuilt`를 물으면 **아직 붙어 있는
    // `PlacedBuilding`을 보고 참으로 캐싱한다** — 그러면 야생 광맥이 영영 *지은 것*이 된다.
    public static GameObject SpawnWild(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent)
    {
        if (prefab == null) return null;

        var go = Instantiate(prefab, pos, rot, parent);

        // **먼저 말해 준다** — 아래 `Destroy`는 프레임 끝이라 `Start`가 그보다 먼저 돈다.
        var node = go.GetComponent<ResourceNode>();
        if (node != null) node.MakeWild();

        foreach (var pu in go.GetComponentsInChildren<PowerUser>(true))      Destroy(pu);
        foreach (var pb in go.GetComponentsInChildren<PlacedBuilding>(true)) Destroy(pb);

        return go;
    }

    // **야생으로 세웠다** — `PlacedBuilding`을 떼기로 했으니 `IsBuilt`를 먼저 못 박는다.
    // 안 박으면 `Start`가 **아직 안 지워진 컴포넌트**를 보고 참으로 캐싱한다.
    public void MakeWild()
    {
        _built      = false;
        _builtKnown = true;
    }

    // 배정/해제 통보 — 노드 패널·무리 관리 패널이 즉시 갱신하도록.
    // 해제는 패널 밖(이동 명령·사망)에서도 일어나므로, 폴링만으로는 목록이 낡은 채로 남는다.
    public static event Action<ResourceNode> OnAssignmentChanged;

    [SerializeField] ResourceNodeData data;

    [Tooltip("이 수량 이상 쌓이면 운반 작업 등록. 한 번에 몇 개나 실려 가는지는 여기가 아니라 " +
             "폰의 적재 한도(CarryCapacity)가 정한다.")]
    [SerializeField] int haulThreshold = 10;

    public Inventory Inventory { get; private set; }

    public int HaulThreshold
    {
        get => haulThreshold;
        set => haulThreshold = Mathf.Max(1, value);
    }

    public ResourceNodeData Data => data;
    public float GaugeProgress => data != null ? _gauge / data.GaugeRequired : 0f;

    readonly List<UnitBrain> _assigned = new();
    public IReadOnlyList<UnitBrain> AssignedPawns => _assigned;

    // **레지스트리** — 자율 배정(`ClaimFreeFor`)이 매 틱 `FindObjectsByType`을 돌 수는 없다.
    // 휴면 노드는 물어볼 것도 없다: `BuildingDormancy`가 컴포넌트를 끄면 `OnDisable`이
    // 목록에서 빼 준다(`Bed._all`과 같은 자리).
    static readonly List<ResourceNode> _all = new();
    public static IReadOnlyList<ResourceNode> All => _all;

    void Start()
    {
        // **지은 노드는 힘을 먹는다** — 배선을 빠뜨려도 여기서 붙으므로 **공짜로 도는
        // 건물이 생기지 않는다**(`PowerUser.Ensure`). 야생에는 안 붙인다: 자연은 핵과
        // 무관하고, 붙이면 **핵이 없을 때 세계의 나무를 아무도 못 베게 된다.**
        if (IsBuilt) PowerUser.Ensure(this, DefaultPowerCost);
    }

    // 이식 광맥이 먹는 힘. 저작하고 싶으면 프리팹의 `PowerUser.cost`를 고친다 —
    // 여기 값은 **아무도 안 적어 뒀을 때의 답**이다.
    const int DefaultPowerCost = 2;

    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    // 이 노드를 캐려면 어떤 작업이 켜져 있어야 하나. **여기 하나가 답한다** —
    // `Assign`·`ClaimFreeFor`·`WorkNode`가 각자 `IsTree`를 물으면 하나가 어긋난다.
    // **채집은 여기 안 온다**(2026-09-06). 한 번 따고 없어지는 것은 노드가 아니라
    // `Forageable`이고 **큐 작업**이다 — 노드로 두면 폰이 배정되어 눌러앉는다.
    public JobCategory NeededCategory =>
        data != null && data.IsTree ? JobCategory.Logging : JobCategory.Mining;

    // 동시에 붙을 수 있는 인원. 0이면 제한 없음(`ResourceNodeData`에 그렇게 적은 경우).
    public int MaxWorkers => data != null ? Mathf.Max(0, data.MaxWorkers) : 0;

    public int  AssignedCount => _assigned.Count;
    public bool IsFull        => MaxWorkers > 0 && _assigned.Count >= MaxWorkers;

    // 배정 성공 여부를 **돌려준다** — 자리가 없으면 부른 쪽이 그 사실을 말해 줘야 한다.
    // 조용히 실패하면 플레이어는 명령이 먹은 줄 알고 폰이 안 움직이는 것만 본다.
    public bool Assign(UnitBrain brain)
    {
        if (brain == null) return false;
        if (_assigned.Contains(brain)) return true;   // 이미 여기 있다 = 원하는 상태
        if (IsFull) return false;

        // **야생도 배정을 받는다**(2026-09-17, 사용자: *"이미 캐라고 지정을 해놨는데 조금
        // 움직였다고 아예 안 캐는건 말이 안되잖아"*).
        //
        // 2026-09-06에 막았던 이유는 *"붙일 수 있으면 밖에서 무한히 캘 수 있어 기지에 광맥을
        // 이식할 이유가 사라진다"*였는데, **그 전제가 그 사이에 없어졌다** — 야생 노드는
        // `TotalYields`만큼 내고 사라진다(`9. Set Wild Yields`). 유한한 것에 사람을 붙이는
        // 일은 *무한 채굴*이 아니라 **그냥 시키는 일**이다.
        //
        // 이식 광맥이 사는 이유도 그대로다: 마르지 않고, 기지 안에 있고, 힘을 먹는다.

        // 다른 노드에 배정돼 있었으면 **거기서 먼저 뗀다.** 폰이 들고 있는
        // `Blackboard.AssignedNode`는 하나뿐이라, 안 떼면 이전 노드의 목록에만
        // 유령 항목이 남는다 — 그 폰은 실제로는 여기서 일하는데 저쪽 패널엔 계속 보인다.
        //
        // **자리 확인을 먼저 한 뒤에 뗀다** — 순서가 반대면 꽉 찬 노드에 보내려다 실패했을 때
        // 폰이 원래 캐던 곳에서도 떨어져 나와 아무 데도 배정되지 않은 채로 남는다.
        // **못 하는 일이면 배정 자체를 안 받는다**(성장 배경). 채굴·벌목은 큐를 안 타므로
        // `WorkSettings.IsEnabled`가 걸러 주지 않는다 — 여기서 물어야 *"무능력인데 바위는
        // 캔다"*가 안 된다.
        if (brain.Work != null && brain.Work.IsBlocked(NeededCategory)) return false;

        var prev = brain.Blackboard.AssignedNode;
        if (prev != null && prev != this) prev.Unassign(brain);

        _assigned.Add(brain);
        brain.Blackboard.AssignedNode      = this;
        brain.Blackboard.NodeAutoClaimed   = false;   // 손으로 붙인 것 — `ClaimFreeFor`만 참으로 되돌린다
        OnAssignmentChanged?.Invoke(this);
        return true;
    }

    // **할 일이 없는 폰이 빈 광맥을 잡는다**(`WorkNode`, 큐가 빈 다음).
    //
    // 큐 작업 여덟 중 **채굴·벌목만 큐를 안 탄다** — 건설·농사·운반은 폰이 알아서 집어가는데
    // 광맥은 열셋을 하나씩 손으로 붙여야 했고, 집결할 때 `AbandonWork`가 **배정까지 거둬**
    // 원정을 갔다 오면 다시 붙여 줘야 했다. 손으로 붙이는 길은 그대로 있고 **그쪽이 이긴다**
    // (이미 배정된 폰은 여기까지 오지 않는다).
    //
    // **핵 영역 안만 본다.** 야생 나무·바위도 `ResourceNode`라, 안 묶으면 폰이 맵 전체의
    // 나무를 베러 떠난다 — 그건 자동화가 아니라 통제를 잃는 것이다. 심은 광맥은 영역 안에만
    // 설 수 있으므로(건설이 핵 union을 요구한다) 이 한 줄이 *"내 거점의 것만"*을 뜻한다.
    // 밖의 것은 지금처럼 손으로 붙인다.
    //
    // **핵이 없으면 자율 배정도 없다**(2026-08-28). 전에는 `core != null &&`로 걸러서
    // **핵이 없을 때 조건이 통째로 사라졌다** — 08-27에 판이 열릴 때 핵을 안 주기로 한
    // 뒤로 그 상태가 판의 처음이 됐고, 그래서 거점을 세우기도 전에 폰들이 **세계 아무
    // 데서나 나무를 베기 시작한다.** 캔 것을 넣을 창고도 없는데.
    //
    // *"영역이 없으면 전부 허용"*은 게이트를 **없는 것으로** 만든다. 영역이 없다는 것은
    // 곧 **내 것이 하나도 없다**는 뜻이므로 아무것도 안 잡는 쪽이 맞다.
    // 핵이 없을 때 저 혼자 잡으러 갈 수 있는 거리. **일감 컷오프와 같은 눈금**이라
    // *찍어서 나가는 일*과 *저 혼자 잡는 일*이 같은 범위에서 돈다.
    const float FreeClaimRange = 200f;

    public static ResourceNode ClaimFreeFor(UnitBrain unit)
    {
        if (unit == null) return null;

        // `WorkSettings`는 플레이어 세력 일꾼에만 붙는다 — 없으면 자율 배정도 없다.
        var work = unit.Work;
        if (work == null) return null;

        var  core = DungeonCore.Instance;
        var  from = unit.transform.position;
        ResourceNode best = null;
        float bestDist = float.MaxValue;

        foreach (var node in _all)
        {
            if (node == null || node.data == null || node.IsFull) continue;

            // **저 혼자는 지은 것만 잡는다**(2026-09-18, 사용자: *"자동으로 게임시작했을 때
            // 자동으로 채굴이 잡히는 걸 원하는게 아니라 내가 자동 스폰된 자원들은 내가 폰을
            // 배정했을 때만 채굴을 해야하는거야"*).
            //
            // **손으로 붙이는 것과 다른 물음이다** — `Assign`은 야생도 받는다(사람이 시킨
            // 일이다). 여기는 *아무도 안 시켰을 때 알아서 가나*라, 야생까지 잡으면 **판이
            // 열리자마자 굴에 뿌려진 것을 전부 캐러 흩어진다.**
            if (!node.IsBuilt) continue;

            var need = node.NeededCategory;
            if (!work.IsEnabled(need) || work.IsBlocked(need)) continue;

            var at = node.transform.position;

            // **핵이 없으면 거리로 자른다**(2026-09-17). 전에는 `core == null`이면 그냥
            // `continue`라 **핵을 꽂기 전에는 자율 배정이 통째로 죽었다** — 판이 열리자마자
            // 나무 한 그루 저 혼자 안 베는 상태였고, 화면에는 *"폰이 논다"*로만 보인다.
            //
            // 핵이 있으면 **영역 안만** 본다(안 묶으면 맵 전체의 나무를 베러 떠난다).
            if (core != null) { if (!core.IsWithinAnyRangeWorld(at)) continue; }
            else if ((at - from).sqrMagnitude > FreeClaimRange * FreeClaimRange) continue;

            float d = (at - from).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = node; }
        }

        if (best == null || !best.Assign(unit)) return null;

        unit.Blackboard.NodeAutoClaimed = true;   // 저 혼자 잡은 것 — 그 작업을 끄면 놓는다
        return best;
    }

    public void Unassign(UnitBrain brain)
    {
        if (!_assigned.Remove(brain)) return;
        if (brain.Blackboard.AssignedNode == this)
            brain.Blackboard.AssignedNode = null;
        OnAssignmentChanged?.Invoke(this);
    }

    void OnDestroy()
    {
        foreach (var brain in _assigned)
            if (brain != null && brain.Blackboard.AssignedNode == this)
                brain.Blackboard.AssignedNode = null;

        bool had = _assigned.Count > 0;
        _assigned.Clear();
        if (had) OnAssignmentChanged?.Invoke(this);

        // Awake에서 막아둔 점유 타일 해제 — 노드 소멸 자리가 영구 건설 불가로 남는 것 방지.
        // 막을 때와 **같은 목록**으로 푼다(그동안 노드가 움직였어도 어긋나지 않는다).
        if (MapManager.Instance != null)
            foreach (var tile in _blockedTiles)
                MapManager.Instance.SetOccupied(tile, false);
        _blockedTiles.Clear();
    }

    // 월드 위치에서 타일 좌표 자동 파생
    public Vector2Int TileCoord =>
        MapManager.Instance.WorldToTile(transform.position);

    [Tooltip("차지하는 타일 수 (가로×세로). 모델이 이 크기에 맞게 만들어져 있어야 한다.")]
    [SerializeField] Vector2Int size = Vector2Int.one;

    float _gauge;
    int _totalYielded;

    // **지은 것인가.** `Start`에서 한 번만 묻는다 — 매 틱 `GetComponentInParent`를 도는 것은
    // 이 클래스가 이미 피해 온 일이다(레지스트리를 둔 이유와 같다).
    bool _built;
    bool _builtKnown;

    public bool IsBuilt
    {
        get
        {
            if (!_builtKnown)
            {
                _built = GetComponentInParent<PlacedBuilding>() != null;
                _builtKnown = true;
            }
            return _built;
        }
    }

    // 야생 노드는 **다 캐면 없어진다.** 지은 것은 이 값을 안 본다.
    public bool Depleted =>
        !IsBuilt && data != null && data.TotalYields > 0 && _totalYielded >= data.TotalYields;

    // ── 찍어서 가져온다 (`IGatherTarget`) ──────────────────────────────────
    //
    // **야생만 찍힌다** — 지은 노드는 배정이라 찍을 것이 없다. 찍는 문은 바닥 물건·채집물과
    // 같은 하나다(`H` 지정 모드). **모드를 또 만들지 않는다**: 조작이 한 글자도 안 다르다.
    GatherNodeJob _job;
    bool _designated;
    bool _mark;

    public bool Designated   => _designated;
    public bool CanDesignate => !IsBuilt;

    public void SetDesignated(bool on)
    {
        if (IsBuilt) return;                    // 지은 노드는 배정이지 지정이 아니다
        if (_designated == on) return;

        _designated = on;
        RefreshDesignationMark();

        if (on)
        {
            if (_job == null && JobManager.Instance != null)
            {
                _job = new GatherNodeJob(this);
                JobManager.Instance.RegisterJob(_job);
            }
            return;
        }

        // **무르면 일감도 거둔다** — 안 거두면 껐는데 폰이 계속 걸어온다.
        var job = _job;
        _job = null;
        if (job != null) JobManager.Instance?.CancelJob(job);
    }

    public void OnJobCancelled() => _job = null;

    Transform IGatherTarget.Where => transform;

    public void SetPendingHighlight(bool on)
    {
        if (_mark == on) return;
        _mark = on;
        RefreshDesignationMark();
    }

    // **바닥 물건·채집물과 같은 초록 구슬** — 같은 모드가 찍는 것이니 같은 말을 해야 한다.
    GameObject _markGo;

    void RefreshDesignationMark()
    {
        bool show = _designated || _mark;

        if (_markGo == null)
        {
            if (!show) return;

            _markGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _markGo.name = "Designated";
            var col = _markGo.GetComponent<Collider>();
            if (col != null) Destroy(col);   // 클릭 판정은 노드 콜라이더 하나뿐이어야 한다

            _markGo.transform.SetParent(transform, false);
            _markGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            _markGo.transform.localScale    = Vector3.one * 0.22f;

            var r = _markGo.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = FadeMaterial.Create(new Color(0.35f, 0.95f, 0.45f, 0.95f));
        }

        _markGo.SetActive(show);
        if (!show) return;

        var rr = _markGo.GetComponent<Renderer>();
        if (rr != null) FadeMaterial.SetAlpha(rr.sharedMaterial, _designated ? 0.95f : 0.45f);
    }

    // 얼마나 남았나 — 살펴보기 창이 적는다(*"몇 번 더 캘 수 있나"*가 안 보이면 유한한 자원이
    // 무한한 것과 화면에서 구별되지 않는다).
    public int YieldsLeft =>
        IsBuilt || data == null || data.TotalYields <= 0
            ? -1 : Mathf.Max(0, data.TotalYields - _totalYielded);
    readonly List<Vector2Int> _blockedTiles = new();

    HaulDispatcher _haul;
    float _haulCheckTimer;
    const float HaulCheckInterval = 2f;

    // 차지하는 칸들.
    //
    // **월드 중심에서 되짚는다** — `TileCoord`(위치를 floor한 칸)에서 퍼뜨리면 안 된다.
    // 짝수 크기 건물은 배치할 때 칸 **경계**에 놓이므로(`BuildModeManager.FootprintCenter`),
    // floor한 칸은 실제 왼쪽 끝보다 한 칸 오른쪽이다. 그대로 퍼뜨리면 배치가 등록한 칸과
    // 한 칸 어긋나고, 둘의 합집합이 막혀 **2×1 광맥이 3칸을 막는다**(실제로 그랬다).
    //
    // 타일 중심은 `tile + 0.5`이므로(`MapManager.TileToWorld`), 맨 왼쪽 칸 중심은
    // `중심 - (w-1)/2`이고 그 칸 번호는 그것을 floor한 값이다.
    public IEnumerable<Vector2Int> Footprint => FootprintAt(transform.position, Size);

    // 칸 수. **1보다 작게 저작된 것을 여기서 누른다** — 읽는 쪽마다 `Max(1, ...)`을
    // 적으면 언젠가 한 곳이 빠지고, 그 자리만 0칸짜리 광맥을 본다.
    public Vector2Int Size => new(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

    // **세우기 *전에* 어느 칸을 덮을지 묻는 문**(2026-09-15).
    //
    // 뿌리기는 *놓아 보기 전에* 자리가 되는지 물어야 하는데, 위의 프로퍼티는 이미 선
    // 물건에만 답한다. 그래서 같은 셈을 뿌리기 쪽에 복사했다면 **답이 두 벌**이 되고,
    // 짝수 크기의 한 칸 어긋남(아래 주석)이 한쪽에만 고쳐진 채 남았을 것이다.
    public static IEnumerable<Vector2Int> FootprintAt(Vector3 pos, Vector2Int size)
    {
        int w = Mathf.Max(1, size.x), h = Mathf.Max(1, size.y);

        int minX = Mathf.FloorToInt(pos.x - (w - 1) * 0.5f);
        int minY = Mathf.FloorToInt(pos.z - (h - 1) * 0.5f);

        for (int x = 0; x < w; x++)
        for (int y = 0; y < h; y++)
            yield return new Vector2Int(minX + x, minY + y);
    }

    void Awake()
    {
        Inventory = GetComponent<Inventory>() ?? gameObject.AddComponent<Inventory>();
        _haul     = new HaulDispatcher(this);

        // 빌드존 안일 때만 타일 막힘 표시 (시각화용). 실제 이동 차단은 콜라이더+NavMesh 담당.
        // **차지한 칸 전부**를 막는다 — 한 칸만 막으면 큰 광맥의 나머지 칸에 건물이 겹쳐 지어진다.
        if (MapManager.Instance == null) return;
        foreach (var tile in Footprint)
        {
            if (!MapManager.Instance.IsInBuildZone(tile)) continue;
            _blockedTiles.Add(tile);
            MapManager.Instance.SetOccupied(tile, true);
        }
    }

    public void Init(ResourceNodeData nodeData) => data = nodeData;

    // 매 작업 틱마다 호출. 게이지가 차면 아이템 1회 드롭.
    //
    // `deltaTime`을 **호출자가 반드시 넘겨야 한다.** 예전엔 `Work(skill)`로 틱마다
    // 스킬 수치를 그대로 더했는데, BT는 tickInterval(0.1초)마다 도므로 실제 채굴 속도가
    // 초당 skill×10이 돼 버렸다(스킬 10이 돌 노드에서 초당 33개). 프레임/틱 수가
    // 속도를 좌우하면 배속·프레임레이트에 따라 산출이 달라진다.
    // 인자로 받는 이유: 호출부가 BT 노드(bb.TickDelta)와 Job(별도 틱)으로 갈려 있어
    // 여기서 Time.deltaTime을 쓸 수 없다 — 시그니처로 강제해 빠뜨리지 못하게 한다.
    public void Work(float power, float deltaTime)
    {
        if (data == null) return;

        if (Depleted) return;

        // **힘을 못 받으면 안 돈다**(2026-09-06). 이식 광맥은 *핵이 돌리는 것*이라 힘이 곧
        // 그 산출의 값이다.
        //
        // **`IsBuilt`를 먼저 묻는다 — `PowerUser`가 붙어 있나로 묻지 않는다.** 그렇게 뒀더니
        // 배선 도구가 이름(`Node_`)으로 골라 **야생 거대버섯 프리팹에도 붙였고**, 핵이 없으니
        // 힘을 못 받아 **폰이 베러 걸어가서는 아무것도 안 했다**(사용자가 본 그것).
        //
        // **컴포넌트가 붙어 있느냐는 배선 사고에 흔들린다. 지은 것이냐는 안 흔들린다.**
        if (IsBuilt && !PowerUser.Ok(this)) return;

        _gauge += power * deltaTime;

        if (_gauge >= data.GaugeRequired)
        {
            _gauge = 0f;
            DropYield();

            // **다 냈으면 없어진다.** 남은 재고는 이미 인벤토리에 있고 운반 일감도 나갔으므로
            // 여기서 바로 지우면 **그 짐이 같이 사라진다** — 비고 나서 지운다(`Update`가 본다).
            if (Depleted) _spent = true;
        }
    }

    // **다 캤는데 아직 짐이 남았다.** 지우는 것을 미룬다 — 자원 노드는 캔 것을 제 인벤토리에
    // 쌓고 짐꾼이 와서 가져가므로, 마지막 산출 직후에 지우면 **그 마지막 몫이 증발한다.**
    bool _spent;

    void LateUpdate()
    {
        if (!_spent) return;
        if (Inventory != null && Inventory.Items.Count > 0) return;   // 아직 실어 갈 것이 있다

        // **표식이 남지 않게 먼저 푼다** — 배정된 폰이 있으면 놓아 준다.
        for (int i = _assigned.Count - 1; i >= 0; i--)
            if (_assigned[i] != null) Unassign(_assigned[i]);

        Destroy(gameObject);
    }

    void DropYield()
    {
        if (data.DropItem == null)
        {
            Debug.LogWarning($"[ResourceNode] {name}: DropItem이 설정되지 않았습니다.");
            return;
        }

        int amount = UnityEngine.Random.Range(data.DropMin, data.DropMax + 1);
        _totalYielded++;

        // 노드 인벤토리에 쌓아둠
        Inventory.TryAdd(data.DropItem, amount);

        // 임계값 이상 쌓인 만큼 **몫으로 나눠** 등록 — 쌓인 양에 비례해 여러 폰이 붙는다.
        _haul?.Dispatch(data.DropItem, HaulThreshold);
    }

    // 쌓인 재고를 주기적으로 다시 본다.
    //
    // 산출(`DropYield`) 때만 등록하면, 폰 배낭이 차서 남긴 잔량은 **다음 산출이 있어야만**
    // 실려 간다 — 다 캔 노드에서는 그 다음이 없어 잔량이 영영 남았다.
    // 창고가 그때 없었거나 꽉 찼던 경우도 여기서 회복된다(작업대의 방식과 같다).
    void Update()
    {
        if (_haul == null || data == null || data.DropItem == null) return;
        if (Inventory == null || Inventory.Items.Count == 0) return;

        _haulCheckTimer -= Time.deltaTime;
        if (_haulCheckTimer > 0f) return;
        _haulCheckTimer = HaulCheckInterval;

        _haul.Dispatch(data.DropItem, HaulThreshold);
    }
}
