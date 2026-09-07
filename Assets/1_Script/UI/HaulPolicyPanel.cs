using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

// **무엇을 자동으로 나를 것인가 — 한 화면에서 정한다**(`O`키, 2026-09-06).
//
// 규칙의 자리는 `HaulPolicy` 하나이고 이 창은 **그 자리를 보여 주는 창구**다. 살펴보기 창의
// 스위치와 **같은 곳에 쓴다** — 창이 둘이지 답이 둘인 것이 아니다.
//
// ## 왜 창이 따로 필요한가
//
// 살펴보기 창의 스위치는 *눈앞의 그 물건*에서만 열린다. 그런데 **아직 한 번도 못 본 물건**은
// 켤 방법이 없고(광맥을 만나기 전에 *"철광석은 자동으로 날라라"*를 말할 수 없다), 무엇을
// 켜 뒀는지 **한눈에 볼 자리**도 없다. 규칙은 세계 전체에 걸리는데 그것을 보는 곳이 없으면
// *"내가 뭘 켜 놨더라"*가 된다.
//
// ## 규격은 씬이, 내용은 여기가
//
// 창 크기·줄 원본·스크롤은 `HaulPolicyPanelSetup`이 씬에 세운다. 목록은 **레지스트리에서
// 훑으므로**(`DataManager.AllItems`) 모드가 넣은 자원도 저절로 딸려 온다 — 배열로 저작하면
// 런타임 생성물에서 반드시 빈다(이 저장소가 네 번 겪은 자리).
public class HaulPolicyPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RectTransform content;
    [SerializeField] Button rowTemplate;
    [SerializeField] TextMeshProUGUI headline;

    [Tooltip("이름으로 거르는 칸. 목록이 수십 줄이라 **찾는 것이 곧 일**이 되지 않게.")]
    [SerializeField] TMP_InputField search;

    readonly List<Button> _rows = new();

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || panel == null) return;

        if (kb.oKey.wasPressedThisFrame) Toggle();
        else if (kb.escapeKey.wasPressedThisFrame && panel.activeSelf) WindowRaiser.Close(panel);
    }

    public void Toggle()
    {
        if (panel == null) return;

        if (panel.activeSelf) { WindowRaiser.Close(panel); return; }

        Rebuild();
        WindowRaiser.Open(panel);   // **직접 `SetActive`하지 않는다** — 그러면 이 창만 안 올라온다
    }

    void OnEnable()
    {
        HaulPolicy.OnChanged += Rebuild;
        if (search != null) search.onValueChanged.AddListener(OnSearchChanged);
    }

    void OnDisable()
    {
        HaulPolicy.OnChanged -= Rebuild;
        if (search != null) search.onValueChanged.RemoveListener(OnSearchChanged);
    }

    void OnSearchChanged(string _) => Rebuild();

    // **레지스트리에서 훑고, 나를 만한 것만 남긴다.** 장비 한 자루까지 줄로 세우면 목록이
    // 수백이 되어 **찾는 것이 곧 일**이 된다 — 자동으로 날라 무더기로 쌓이는 것은
    // **자원과 음식**이다(그 둘이 바닥에 떨어지는 것들이다).
    void Rebuild()
    {
        if (content == null || rowTemplate == null) return;

        // **레지스트리는 `Id → 아이템` 사전이다** — 값만 본다(키는 식별자라 화면에 안 쓴다).
        // **거르는 것은 이름이다** — 식별자로 찾게 하면 `res_thickhide`를 외워야 한다.
        string q = search != null ? search.text : null;
        bool filtering = !string.IsNullOrWhiteSpace(q);
        string needle = filtering ? q.Trim() : null;

        var items = new List<ItemData>();
        var all   = new List<ItemData>();
        foreach (var kv in DataManager.AllItems)
        {
            var it = kv.Value;
            if (it is not ResourceItemData && it is not FoodData) continue;
            all.Add(it);

            if (filtering &&
                (it.ItemName == null ||
                 it.ItemName.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0))
                continue;

            items.Add(it);
        }

        // **켠 것을 위로 올리지 않는다**(2026-09-06, 사용자 결정: *"체크했다고 해서 순서를
        // 올리지는 마"*). 켤 때마다 줄이 뛰면 **방금 누른 것이 어디로 갔는지 눈으로 좇게
        // 되고**, 둘을 잇달아 켜려면 매번 다시 찾아야 한다. **목록이 가만히 있어야 손이
        // 기억한다** — 무엇이 켜졌나는 색과 글자가 이미 답한다.
        items.Sort((a, b) => string.CompareOrdinal(a.ItemName, b.ItemName));

        while (_rows.Count < items.Count)
        {
            var b = Instantiate(rowTemplate, content);
            b.name = $"Row{_rows.Count}";
            _rows.Add(b);
        }

        // **켠 개수는 거른 목록이 아니라 전부에서 센다** — 검색 중에 "3/12"가 뜨면
        // 그 숫자가 *지금 보이는 것*인지 *세계 전체*인지 알 수 없다.
        int on = 0;
        foreach (var it in all) if (HaulPolicy.IsAuto(it)) on++;

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (row == null) continue;

            if (i >= items.Count) { row.gameObject.SetActive(false); continue; }

            var item = items[i];
            bool auto = HaulPolicy.IsAuto(item);

            row.gameObject.SetActive(true);

            var label = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                // **상태를 글자로도 적는다** — 색만으로 가르면 무엇이 초록이었는지 외워야 한다.
                label.text  = auto ? $"<color=#7fe08a>V  {item.ItemName}</color>"
                                   : $"<color=#9a9a9a>·  {item.ItemName}</color>";
            }

            var img = row.GetComponent<Image>();
            if (img != null)
                img.color = auto ? new Color(0.18f, 0.42f, 0.22f) : new Color(0.18f, 0.18f, 0.20f);

            // **여기서는 토글이다**(살펴보기 창은 상태로 적는다) — 여럿을 빠르게 켜고 끄는
            // 자리라 한 번 누름 = 한 번 뒤집힘이 맞다.
            var captured = item;
            row.onClick.RemoveAllListeners();
            row.onClick.AddListener(() => HaulPolicy.Toggle(captured));
        }

        if (headline != null)
            // **제목은 헤더가 든다**(규격) — 여기는 *지금 몇 개가 켜져 있나*와 안내뿐이다.
            headline.text = $"<color=#c8c8cc>켜짐 {on}</color><color=#888> / {all.Count}</color>" +
                            (filtering ? $"  <color=#E8C566>· 검색 {items.Count}</color>" : "") +
                            "\n<size=80%><color=#888>줄을 누르면 켜고 끕니다. " +
                            "켜 두면 그 품목은 바닥에 떨어지는 즉시 실려 갑니다.</color></size>";
    }
}
