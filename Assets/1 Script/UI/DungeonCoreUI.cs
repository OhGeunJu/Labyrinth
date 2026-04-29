using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// 던전 코어 클릭 시 열리는 패널.
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

    DungeonCore _core;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        DungeonCore.OnSelected += Open;
    }

    void OnDestroy() => DungeonCore.OnSelected -= Open;

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(DungeonCore core)
    {
        _core = core;
        if (panel != null) panel.SetActive(true);
        RefreshInfo();
        RebuildList();
    }

    public void Close()
    {
        _core?.Deselect();
        _core = null;
        if (panel != null) panel.SetActive(false);
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
            int count = CountActiveMinions();
            minionCountText.text = $"하수인: {count}명 활성";
        }
    }

    void RebuildList()
    {
        if (minionContainer == null || minionEntryPrefab == null || _core == null) return;

        foreach (Transform child in minionContainer)
            Destroy(child.gameObject);

        foreach (var entry in _core.MinionEntries)
        {
            if (entry.Data == null) continue;
            var ui = Instantiate(minionEntryPrefab, minionContainer);
            ui.Setup(entry.Data);
            ui.OnSummon += OnSummonClicked;
        }
    }

    void OnSummonClicked(MinionData data)
    {
        if (_core == null) return;
        _core.Summon(data, _core.GetSpawnPosition());
        RefreshList(); // 자원 현황 갱신
        RefreshInfo();
    }

    void RefreshList()
    {
        if (minionContainer == null) return;
        foreach (Transform child in minionContainer)
            child.GetComponent<MinionEntryUI>()?.Refresh();
    }

    static int CountActiveMinions()
    {
        int count = 0;
        foreach (var brain in Object.FindObjectsByType<UnitBrain>(FindObjectsSortMode.None))
            if (brain.Data is MinionData) count++;
        return count;
    }
}
