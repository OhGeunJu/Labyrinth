using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// **소환 창 — 소환진을 누르면 열린다**(2026-09-29 — 전에는 핵을 누르면 열렸다. 핵은 이제 배치 창을 연다: `WorkPostsUI`).
// 목록 · 한도 · 비용은 **그 소환진을 덮는 핵**이 든다(`SummonCircle`). 하수인은 **소환진 곁**에 선다.
// 인스펙터에서 panel, minionContainer, minionEntryPrefab 연결.
public class DungeonCoreUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject    panel;
    [SerializeField] Transform     minionContainer;   // ScrollView Content
    [SerializeField] MinionEntryUI minionEntryPrefab;

    [Header("코어 정보")]
    [SerializeField] TextMeshProUGUI coreInfoText;     // 범위, 등급 등
    [SerializeField] TextMeshProUGUI minionCountText;  // 활성 하수인 수

    [Header("액션")]
    [SerializeField] Button relocateButton;   // 핵 이전 (씬에서 연결)

    DungeonCore _core;
    Vector3?    _spawnAt;   // 소환진 곁 — 없으면 핵 곁(옛 길)

    static DungeonCoreUI _instance;

    // **소환진이 부른다** — 어느 핵의 하수인을 어디에 세울지를 넘긴다.
    public static void OpenFor(DungeonCore core, Vector3 spawnAt)
    {
        if (_instance == null) { Debug.LogWarning("[소환] 씬에 소환 창(`DungeonCoreUI`)이 없다."); return; }
        _instance._spawnAt = spawnAt;
        _instance.Open(core);
    }

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (relocateButton != null) relocateButton.onClick.AddListener(OnRelocateClicked);
        _instance = this;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
        // 연구가 끝나면 새로 열린 하수인이 **열려 있는 목록에도** 나타나야 한다
        // (`MinionData.RequiredResearch`). 안 붙이면 창을 닫았다 열어야 보인다.
        ResearchManager.OnResearchCompleted += OnResearchDone;
    }

    void OnResearchDone(ResearchData _)
    {
        if (panel != null && panel.activeSelf) RebuildList();
    }

    void OnRelocateClicked()
    {
        var core = _core;
        if (core == null) return;
        Close();                     // 배치 지점을 가리지 않게 패널을 먼저 닫는다
        CoreRelocator.Begin(core);   // 좌클릭 확정 / 우클릭·Esc 취소
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        BuildModeManager.OnActiveChanged    -= OnBuildModeChanged;
        ResearchManager.OnResearchCompleted -= OnResearchDone;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(DungeonCore core)
    {
        _core = core;
        WindowRaiser.Open(panel);
        RefreshInfo();
        RebuildList();
    }

    public void Close()
    {
        _core = null;
        _spawnAt = null;
        WindowRaiser.Close(panel);
    }

    void RefreshInfo()
    {
        if (_core == null) return;

        if (coreInfoText != null)
        {
            float radius = _core.Data != null ? _core.Data.BaseRadius : 0f;
            coreInfoText.text = $"범위: {radius:F0}";
        }

        if (minionCountText != null)
        {
            int count = _core.CurrentMinionCount;   // 한도를 재는 것과 같은 수
            minionCountText.text = $"하수인: {count}명 활성";
        }
    }

    void RebuildList()
    {
        if (minionContainer == null || minionEntryPrefab == null || _core == null) return;

        foreach (Transform child in minionContainer)
            Destroy(child.gameObject);

        // 목록의 출처는 `DungeonCore.Minions` — 씬 배열이 비면 레지스트리다.
        foreach (var data in _core.Minions)
        {
            if (data == null) continue;
            var ui = Instantiate(minionEntryPrefab, minionContainer);
            ui.Setup(data);
            ui.OnSummon += OnSummonClicked;
        }
    }

    void OnSummonClicked(MinionData data)
    {
        if (_core == null) return;
        _core.Summon(data, _spawnAt ?? _core.GetSpawnPosition());
        RefreshList(); // 자원 현황 갱신
        RefreshInfo();
    }

    void RefreshList()
    {
        if (minionContainer == null) return;
        foreach (Transform child in minionContainer)
            if (child.TryGetComponent(out MinionEntryUI entry)) entry.Refresh();
    }

}
