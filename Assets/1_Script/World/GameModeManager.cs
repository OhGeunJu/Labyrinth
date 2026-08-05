using System;
using System.Collections.Generic;
using UnityEngine;

// 게임 모드 — 필드맵(거점 시뮬)과 월드맵(탐험)은 **화면·입력만 배타적이고, 시뮬은 둘 다 돈다**.
//   Field → World : 필드 UI off + 필드 전용 입력·카메라 조작 off + 월드 UI/루트/카메라 on
//   World → Field : 정확히 반대.
//
// UI 캔버스는 **하나**다(2026-07-30). 필드 UI와 월드 UI가 같은 캔버스 아래 형제로 살고,
// 전환할 때 캔버스가 아니라 **그 자식들**을 다룬다 — 캔버스째 끄면 월드 UI까지 같이 꺼진다.
// 캔버스를 둘로 나누면 배칭이 갈리고 "이 UI는 어느 캔버스 소속인가"를 계속 판단하게 된다.
// 필드 UI는 **끄기 전 상태를 기억했다가 되돌린다**(`ApplyFieldUi`) — 일괄로 켜면 닫아 뒀던
// 패널까지 따라 열린다.
// **거점은 계속 돌아간다** — 폰이 일하고, 습격이 떨어지고, 시간이 간다.
//
// 2026-07-30 결정 — 07-29의 "월드맵을 켜면 필드를 통째로 끈다"를 되돌렸다. 근거:
//   ① 월드맵 횡단은 대각선으로도 약 18초(맵 한 변 64유닛 ÷ partySpeed 5)인데 하루는 3600초다.
//      "이동에 시간이 드니 그동안은 추상 생산"이라는 명분이 수치상 성립하지 않았다 —
//      하루 경계를 넘으려면 지도를 200번 왕복해야 한다.
//   ② 필드를 끄면 원정 중 습격을 **추상 방어 판정**으로 처리해야 한다. 안 끄면 M키로 돌아가 싸우면 된다.
//   ③ 원정대(`ExpeditionParty`)는 "일부는 나가고 일부는 남는다"가 전제인데,
//      필드가 멈추면 남은 폰도 같이 멈춰 던전 코어 운영과 탐험이 배타가 된다.
// 재검토 시점: 월드맵 이동이 실제로 며칠 걸리게 되거나(속도↓/맵↑), 폰 수가 늘어 성능이 문제될 때.
// 그때도 완전 정지 말고 **월드 모드에서 필드는 저속 틱**이라는 중간 지점이 있다.
//
// 오프라인 추상 생산(`SettlementManager`)은 그대로 남는다 — 겨냥 대상은 원래
// **지금 안 보고 있는 다른 거점**이지, 눈앞의 활성 거점이 아니다.
//
// 왜 Unity 씬 분리가 아닌가: 씬 Single 로드는 싱글톤(MapManager/JobManager/…)을 전부 파괴해
// 세이브 시스템이 전제가 된다(설계 `docs/design_world_field.md` 2026-06-20 "씬 전략 = A" 확정).
[DefaultExecutionOrder(-30)]   // WorldMap(-60) / SettlementManager(-58) / WorldMapView(-40) 뒤
public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }

    public static GameMode Current { get; private set; } = GameMode.Field;
    public static bool IsField => Current == GameMode.Field;
    public static bool IsWorld => Current == GameMode.World;

    public static event Action<GameMode> OnModeChanged;

    [Header("비우면 시작 시 자동 탐색")]
    [SerializeField] Camera    fieldCamera;   // 없으면 Camera.main
    [SerializeField] Canvas    uiCanvas;      // 없으면 씬의 첫 Canvas — 필드/월드 UI가 **같이** 산다
    [Tooltip("UI 캔버스 아래 월드 전용 UI 묶음. 이것만 필드와 반대로 켜진다. 비우면 자동 탐색.")]
    [SerializeField] Transform worldUiRoot;
    [SerializeField] Camera    worldCamera;   // 없으면 WorldMapView에서
    [SerializeField] Transform worldRoot;     // 없으면 WorldMapView.MapRoot

    // 한쪽 모드에서만 **입력을 받아야 하는** 컴포넌트들(카메라 조작·선택·이동 명령·건설).
    // 시뮬은 양쪽 다 돌지만 조작은 보고 있는 쪽에만 가야 하므로 enabled를 서로 반대로 뒤집는다.
    readonly List<Behaviour> _fieldOnly = new();
    readonly List<Behaviour> _worldOnly = new();

    bool _applied;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        Resolve();
        Current = GameMode.Field;   // 시작은 항상 필드 — 월드 루트/카메라를 확실히 꺼둔다
        Apply();
    }

    // ── 전환 ──────────────────────────────────────────────────────────────────

    public void SwitchTo(GameMode mode)
    {
        if (_applied && mode == Current) return;

        // 월드 측이 없으면 필드만 끄고 아무것도 안 보이는 상태가 된다 → 전환 거부.
        if (mode == GameMode.World && worldRoot == null && worldCamera == null)
        {
            Debug.LogWarning("[GameModeManager] 월드맵 뷰가 없어 전환하지 않습니다 — " +
                             "`DungeonColony → Setup World Map Scene`을 먼저 실행하세요.");
            return;
        }

        // 필드를 떠나기 전에 필드 조작 모드를 정리한다 —
        // 고스트/미리보기가 월드맵 위로 따라 넘어가지 않게.
        if (mode == GameMode.World) LeaveField();

        Current = mode;
        Apply();
        OnModeChanged?.Invoke(Current);
    }

    public void Toggle() => SwitchTo(IsField ? GameMode.World : GameMode.Field);

    static void LeaveField()
    {
        if (CoreRelocator.IsActive) CoreRelocator.Cancel();
        if (BuildModeManager.Instance != null) BuildModeManager.Instance.Deactivate();
    }

    void Apply()
    {
        bool field = IsField;

        // 정착지 루트는 건드리지 않는다 — 필드 시뮬은 월드맵을 보는 동안에도 계속 돈다.
        // 뒤집는 건 **보고 조작하는 쪽**뿐이다.

        // 1) 루트 밖 필드/월드 전용 컴포넌트(입력·카메라 조작·건설). 서로 반대로 켜진다.
        for (int i = 0; i < _fieldOnly.Count; i++)
            if (_fieldOnly[i] != null) _fieldOnly[i].enabled = field;
        for (int i = 0; i < _worldOnly.Count; i++)
            if (_worldOnly[i] != null) _worldOnly[i].enabled = !field;

        // 2) UI — **캔버스는 하나**다. 캔버스째 끄면 그 안의 월드 UI까지 같이 꺼지므로
        //    캔버스 아래 자식들을 다루되, 월드 UI 묶음만 반대로 켠다.
        ApplyFieldUi(field);
        if (worldUiRoot != null) worldUiRoot.gameObject.SetActive(!field);

        // 3) 카메라 / 월드 루트
        if (fieldCamera != null) fieldCamera.enabled = field;
        if (worldCamera != null) worldCamera.enabled = !field;
        if (worldRoot   != null) worldRoot.gameObject.SetActive(!field);

        _applied = true;
    }

    // ── 필드 UI 켜고 끄기 ─────────────────────────────────────────────────────

    // 떠날 때 **열려 있던 것을 기억**했다가 돌아올 때 그대로 되돌린다.
    //
    // 캔버스가 둘이던 시절엔 캔버스째 SetActive라 각 패널의 개별 상태가 저절로 보존됐다.
    // 캔버스를 하나로 합치면서 자식을 일괄 `SetActive(field)` 했더니 **돌아올 때 전부 켜져서**
    // 닫아 뒀던 인벤토리·상점 패널까지 따라 열렸다. 그래서 상태를 직접 들고 있는다.
    readonly List<Transform> _fieldUi        = new();
    readonly List<bool>      _fieldUiWasOpen = new();
    bool _fieldUiStashed;

    void ApplyFieldUi(bool field)
    {
        if (uiCanvas == null) return;

        if (!field)
        {
            _fieldUi.Clear();
            _fieldUiWasOpen.Clear();
            foreach (Transform child in uiCanvas.transform)
            {
                if (child == worldUiRoot) continue;
                _fieldUi.Add(child);
                _fieldUiWasOpen.Add(child.gameObject.activeSelf);
                child.gameObject.SetActive(false);
            }
            _fieldUiStashed = true;
            return;
        }

        // 기억해 둔 게 없으면(= 시작 시 첫 Apply) 씬 상태를 그대로 둔다 — 여기서 전부 켜면 안 된다.
        if (!_fieldUiStashed) return;

        for (int i = 0; i < _fieldUi.Count; i++)
            if (_fieldUi[i] != null) _fieldUi[i].gameObject.SetActive(_fieldUiWasOpen[i]);

        _fieldUi.Clear();
        _fieldUiWasOpen.Clear();
        _fieldUiStashed = false;
    }

    // ── 참조 해결 ─────────────────────────────────────────────────────────────

    void Resolve()
    {
        if (fieldCamera == null) fieldCamera = Camera.main;

        // 월드 측을 **먼저** 정한다 — 아래 필드 캔버스 탐색과 Collect가 둘 다 이 값에 기댄다.
        var view = First<WorldMapView>();
        if (view != null)
        {
            if (worldCamera == null) worldCamera = view.WorldCamera;
            if (worldRoot   == null) worldRoot   = view.MapRoot;
        }

        if (uiCanvas == null) uiCanvas = First<Canvas>();
        if (worldUiRoot == null) worldUiRoot = FindWorldUiRoot();

        // worldCamera가 정해진 뒤에 수집해야 필드/월드를 가를 수 있다(아래 Collect 규칙).
        _fieldOnly.Clear();
        _worldOnly.Clear();
        Collect<CameraController>();
        Collect<SelectionManager>();
        Collect<SelectionBox>();
        Collect<MoveController>();
        Collect<BuildModeManager>();
        // `EventManager`는 일부러 뺐다 — 원정 나가 있는 동안에도 거점에 습격이 떨어져야 한다.
        // M키로 뛰어 돌아가 싸우면 되므로 "추상 방어 판정"을 만들 필요가 없어진다.
        // **잔여**: `EventNotificationUI`가 필드 캔버스 아래라 월드 모드에선 안 보인다 —
        // 습격이 떨어져도 지도에선 모른다. 월드 측 알림이 따로 필요하다.
    }

    static T First<T>() where T : Component
    {
        var found = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return found.Length > 0 ? found[0] : null;
    }

    // 월드 UI 묶음 = 캔버스 **직속 자식** 중 월드 전용 UI를 품은 것.
    // 이름 규칙에 기대지 않고 컴포넌트로 찾아 올라간다 — 씬에서 오브젝트 이름을 바꿔도 안 깨진다.
    Transform FindWorldUiRoot()
    {
        if (uiCanvas == null) return null;

        var menu = FindFirstObjectByType<WorldMenuUI>(FindObjectsInactive.Include);
        if (menu == null) return null;

        // 캔버스 직속 자식이 될 때까지 부모를 타고 올라간다.
        var t = menu.transform;
        while (t != null && t.parent != uiCanvas.transform) t = t.parent;
        return t;
    }

    // **월드 카메라 오브젝트에 붙어 있으면 월드 측**, 나머지는 전부 필드 측.
    // 월드맵에서도 필드처럼 카메라를 조작하려면 같은 `CameraController`를 월드 카메라에 붙이는데,
    // 이 규칙이 없으면 모드 전환이 그걸 꺼버린다.
    void Collect<T>() where T : Behaviour
    {
        foreach (var b in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null) continue;
            bool isWorldSide = worldCamera != null && b.gameObject == worldCamera.gameObject;
            (isWorldSide ? _worldOnly : _fieldOnly).Add(b);
        }
    }
}
