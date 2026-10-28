using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// **거점 바 — 핵마다 칸 하나, 누르면 그 거점으로 간다**(2026-10-01, 사용자: *"UI에서 1,2,3,4 거점 시점 변환
// 할 수 있게"*).
//
// 세계가 이어진 뒤로(08-26) 거점은 **동시에 살아 있다**(`DungeonCore.All`) — 옛 `SettlementManager`처럼 하나를
// 끄고 하나를 켜는 전환이 아니다. 그래서 *시점 변환*은 둘이다: **카메라를 그 핵으로 옮기고**(`FocusOn`) ·
// **조작 중인 거점을 그 핵으로 바꾼다**(`DungeonCore.SetAsActive` — 재료 집계·건설 기준이 따라간다).
// 둘이 갈리면 *"화면은 2번 거점인데 창고 수는 1번 것"*이 된다.
//
// **번호는 핵이 선 차례다**(`DungeonCore.All`의 순서) — 이름순으로 두면 새 거점을 세울 때마다 번호가 밀려
// 손에 익은 자리가 바뀐다.
//
// **지휘소·상단 버튼은 여기 없다** — 처음엔 여기 붙였는데(10-01) 2026-10-09에 하단 모드 바로 옮겼다(사용자: *"지휘소랑
// 상단은 왜 왼쪽 상단에 있는거야? 차라리 가운데 하단에 버튼들이랑 같이"* — `ModeBarUI.Mode.Posts`·`Caravan`). 여기는
// **거점 사이를 옮겨 다니는 칸**만 든다. 그래서 거점이 하나뿐이면 **판째 숨는다**(옮겨 갈 곳이 없다).
//
// **모양은 씬이 든다**(`UI → Create Outpost Bar`) — 칸은 씬의 템플릿을 복제하고 여기서는 글자·색·클릭만.
public class OutpostBarUI : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] Transform  slotParent;
    [SerializeField] Button     slotTemplate;

    [SerializeField] Color normalColor = new(0.16f, 0.16f, 0.19f, 0.95f);
    [SerializeField] Color activeColor = new(0.30f, 0.45f, 0.62f, 0.95f);

    readonly List<Button> _slots = new();
    readonly List<DungeonCore> _bound = new();
    int _beat = -1;

    void Awake()
    {
        if (slotTemplate != null) slotTemplate.gameObject.SetActive(false);
    }

    void Update()
    {
        // **보이나는 박자에 안 태운다**(`UiRefresh` — 그것은 *언제 다시 그리나*의 답이다).
        bool show = DungeonCore.All.Count > 1;
        if (panel != null && panel.activeSelf != show) panel.SetActive(show);
        if (!show || !UiRefresh.Due(ref _beat, 2)) return;
        Refresh();
    }

    void Refresh()
    {
        var cores = DungeonCore.All;

        // 거점이 하나뿐이면 옮겨 갈 곳이 없다 — 판째 숨는다(`Update`).
        int n = cores.Count > 1 ? cores.Count : 0;

        while (_slots.Count < n)
        {
            var b = Instantiate(slotTemplate, slotParent);
            int idx = _slots.Count;
            b.onClick.AddListener(() => Go(idx));
            _slots.Add(b);
            _bound.Add(null);
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            bool on = i < n;
            if (_slots[i].gameObject.activeSelf != on) _slots[i].gameObject.SetActive(on);
            if (!on) { _bound[i] = null; continue; }

            var core = cores[i];
            _bound[i] = core;

            var label = _slots[i].GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = $"{i + 1} 거점 - {Floors.At(core.transform.position)}층";   // 층이 넷이라 어느 층인지 같이

            var img = _slots[i].targetGraphic as Image;
            if (img != null) img.color = core == DungeonCore.Instance ? activeColor : normalColor;
        }

    }

    void Go(int index)
    {
        if (index < 0 || index >= _bound.Count) return;
        var core = _bound[index];
        if (core == null) return;

        core.SetAsActive();

        var cam = CameraController.Active;
        if (cam != null) cam.FocusOn(core.transform.position);

        Refresh();   // 색을 바로 옮긴다 — 다음 박까지 기다리면 누른 칸이 한 박 늦게 켜진다
    }
}
