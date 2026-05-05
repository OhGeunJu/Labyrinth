using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class PatrolZoneUI : MonoBehaviour
{
    [SerializeField] SelectionManager   selectionManager;
    [SerializeField] GameObject         panel;
    [SerializeField] TextMeshProUGUI    titleText;
    [SerializeField] Button             modeWaypoint;
    [SerializeField] Button             modeArea;
    [SerializeField] Button             modeIdle;
    [SerializeField] Transform          unitListContainer;
    [SerializeField] Button             assignButton;

    [Header("배정 유닛 항목 프리팹 (코드 생성)")]
    Color _colSelected   = new(0.20f, 0.48f, 1.00f, 1f);
    Color _colNormal     = new(0.22f, 0.22f, 0.22f, 1f);

    PatrolZone _current;
    readonly List<GameObject> _unitRows = new();

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        PatrolZone.OnSelected            += Open;
        BuildModeManager.OnActiveChanged += b => { if (b) Close(); };

        modeWaypoint?.onClick.AddListener(() => SetMode(PatrolMode.Waypoint));
        modeArea?.onClick.AddListener(()     => SetMode(PatrolMode.Area));
        modeIdle?.onClick.AddListener(()     => SetMode(PatrolMode.Idle));
        assignButton?.onClick.AddListener(AssignSelected);
    }

    void OnDestroy() => PatrolZone.OnSelected -= Open;

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
        RefreshModeButtons();
    }

    void Open(PatrolZone zone)
    {
        _current = zone;
        panel.SetActive(true);
        if (titleText != null) titleText.text = zone.ZoneName;
        RefreshUnitList();
        RefreshModeButtons();
    }

    public void Close()
    {
        _current = null;
        if (panel != null) panel.SetActive(false);
    }

    void SetMode(PatrolMode mode)
    {
        if (_current == null) return;
        _current.Mode = mode;
        RefreshModeButtons();
    }

    void RefreshModeButtons()
    {
        if (_current == null) return;
        SetButtonColor(modeWaypoint, _current.Mode == PatrolMode.Waypoint);
        SetButtonColor(modeArea,     _current.Mode == PatrolMode.Area);
        SetButtonColor(modeIdle,     _current.Mode == PatrolMode.Idle);
    }

    void SetButtonColor(Button btn, bool selected)
    {
        if (btn == null) return;
        btn.GetComponent<Image>().color = selected ? _colSelected : _colNormal;
    }

    void AssignSelected()
    {
        if (_current == null || selectionManager == null) return;
        if (selectionManager.Selected.Count == 0) return;
        var brain = selectionManager.Selected[0].GetComponent<UnitBrain>();
        if (brain == null) return;
        _current.Assign(brain);
        RefreshUnitList();
    }

    void RefreshUnitList()
    {
        foreach (var row in _unitRows) Destroy(row);
        _unitRows.Clear();
        if (_current == null || unitListContainer == null) return;

        foreach (var unit in _current.Assigned)
        {
            if (unit == null) continue;
            var row = MakeUnitRow(unit);
            _unitRows.Add(row);
        }
    }

    GameObject MakeUnitRow(UnitBrain unit)
    {
        var row = new GameObject("UnitRow");
        row.transform.SetParent(unitListContainer, false);
        var rt = row.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 28);

        var img = row.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var le = row.AddComponent<UnityEngine.UI.LayoutElement>();
        le.preferredHeight = 28;

        // 유닛 이름
        var nameGo = new GameObject("Name");
        nameGo.transform.SetParent(row.transform, false);
        var nameRt = nameGo.AddComponent<RectTransform>();
        nameRt.anchorMin        = new Vector2(0, 0);
        nameRt.anchorMax        = new Vector2(1, 1);
        nameRt.offsetMin        = new Vector2(8,  0);
        nameRt.offsetMax        = new Vector2(-50, 0);
        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text      = unit.gameObject.name;
        nameTmp.fontSize  = 12;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.color     = Color.white;

        // 해제 버튼
        var btnGo = new GameObject("RemoveBtn");
        btnGo.transform.SetParent(row.transform, false);
        var btnRt = btnGo.AddComponent<RectTransform>();
        btnRt.anchorMin        = new Vector2(1, 0.5f);
        btnRt.anchorMax        = new Vector2(1, 0.5f);
        btnRt.pivot            = new Vector2(1, 0.5f);
        btnRt.anchoredPosition = new Vector2(-4, 0);
        btnRt.sizeDelta        = new Vector2(40, 22);
        btnGo.AddComponent<Image>().color = new Color(0.5f, 0.15f, 0.15f, 1f);
        var btn = btnGo.AddComponent<Button>();

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(btnGo.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.text = "해제"; txt.fontSize = 10;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        var captured = unit;
        btn.onClick.AddListener(() =>
        {
            _current?.Unassign(captured);
            RefreshUnitList();
        });

        return row;
    }
}
