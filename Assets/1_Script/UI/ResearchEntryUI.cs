using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(LayoutElement))]
public class ResearchEntryUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] float           preferredHeight = 40f;

    public event Action<ResearchData> OnSelected;

    // **구워 둔 노드가 제 연구를 기억한다**(2026-08-25). 런타임에만 들고 있으면 씬에 미리
    // 놓아 둘 수가 없어 **에디터의 연구 창이 텅 빈다** — 자리를 저작해 놓고 결과를 못 본다.
    [SerializeField] ResearchData _data;

    public ResearchData Data => _data;

    void Awake()
    {
        var le = GetComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight       = preferredHeight;
    }

    // **`Setup`은 이 노드를 처음부터 다시 묶는 일이다.**
    //
    // 구독을 여기서 지우는 이유: 구워 둔 노드는 **창을 닫아도 안 지워지므로**(2026-08-25)
    // 열 때마다 `RebuildGraph`가 `OnSelected +=` 를 한 번씩 더 한다 — 세 번 열면 노드를
    // 한 번 눌렀을 때 **셋이 한꺼번에 불린다.** 지우고 나서 찍던 시절엔 오브젝트가 사라져
    // 저절로 풀리던 것이라, 재사용으로 바꾸는 순간 딸려 오는 값이다.
    //
    // 버튼 쪽은 `Refresh`가 `RemoveAllListeners`로 이미 같은 일을 한다.
    public void Setup(ResearchData data, ResearchData activeResearch = null)
    {
        OnSelected = null;
        _data      = data;
        Refresh(activeResearch);
    }

    // `activeResearch` = 지금 연구대가 맡은 연구. **디테일 창의 게이지가 고른 연구에만 뜨게 되면서
    // 목록이 유일하게 "무엇이 도는 중인가"를 알려 주는 곳이 됐다** — 표식이 없으면 그 정보가
    // 화면 어디에도 남지 않는다.
    public void Refresh(ResearchData activeResearch)
    {
        var data = _data;
        if (data == null) return;

        var rm = ResearchManager.Instance;
        bool completed   = rm != null && rm.IsCompleted(data);
        bool canResearch = rm == null  || rm.CanResearch(data);
        bool researching = activeResearch != null && activeResearch == data;

        string label = data.ResearchName;
        Color  color;

        if (completed)
        {
            label = $"[완료] {label}";
            color = new Color(0.4f, 0.8f, 0.4f, 1f);
        }
        else if (researching)
        {
            // 진행률은 안 적는다 — 목록은 다시 그릴 때만 갱신되므로 숫자를 적으면 멈춘 채로 남는다.
            // 실시간 수치는 게이지가 맡는다.
            label = $"[연구 중] {label}";
            color = new Color(0.55f, 0.8f, 1f, 1f);
        }
        else if (!canResearch)
        {
            label = $"[잠김] {label}";
            color = new Color(0.4f, 0.4f, 0.4f, 1f);
        }
        else
        {
            color = StorageHasIngredients(data) ? Color.white : new Color(1f, 0.3f, 0.3f, 1f);
        }

        if (nameText != null) { nameText.text = label; nameText.color = color; }

        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();

            // **잠겨도 눌러진다.** 무엇이 열리는지 봐야 그 연구를 할지 정하는데, 버튼을 죽여 두면
            // 이름만 보고 짐작해야 한다(제작 레시피에 08-01에 한 것과 같은 처리).
            // 실제 착수만 막는다 — 디테일 창의 [연구] 버튼이 잠금을 다시 검사한다.
            btn.interactable = !completed;
            if (btn.interactable) btn.onClick.AddListener(() => OnSelected?.Invoke(_data));
        }
    }

    static bool StorageHasIngredients(ResearchData data)
    {
        // **에디터에는 창고가 없다** — 안 막으면 구워 둔 노드가 전부 빨갛게 뜬다(재료 부족).
        if (!Application.isPlaying) return true;

        foreach (var ing in data.Ingredients)
            // **`Resolve()`다 — `Item`을 직접 읽으면 JSON 재료가 조용히 null이 된다**
            // (2026-08-24에 읽는 곳 넷을 돌렸는데 **여기를 빠뜨렸다**). 천·약초·붕대·두꺼운
            // 가죽이 전부 JSON이라, 그것을 쓰는 연구는 창고에 쌓여 있어도 **늘 빨갛게** 떴다.
            if (MaterialPool.AvailableHere(ing.Resolve()) < ing.Amount) return false;
        return true;
    }
}
