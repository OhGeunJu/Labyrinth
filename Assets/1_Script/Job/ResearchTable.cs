using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ResearchTable : MonoBehaviour, IWorldClickable
{
    [Tooltip("비우면 DataManager에 등록된 연구 **전부**를 쓴다. " +
             "특정 연구대만 일부를 다루게 하고 싶을 때만 채운다.")]
    [SerializeField] List<ResearchData> availableResearch = new();

    // **비어 있으면 레지스트리에서 끌어온다.**
    // 인스턴스마다 손으로 채우는 방식이면 런타임에 지은 연구대는 목록이 텅 빈 채로 나온다
    // — 씬에 미리 놓아둔 것에만 데이터가 들어가는 구조가 되기 때문이다.
    // `CraftingTable`이 `DataManager.GetRecipesBySkill`로 끌어오는 것과 같은 규칙으로 맞췄다.
    // 목록을 채우면 그게 이긴다(큐레이션용) — "데이터가 없으면 기본 동작"이라는 이 프로젝트의 관례.
    public IReadOnlyList<ResearchData> AllResearch
    {
        get
        {
            if (availableResearch.Count > 0) return availableResearch;

            _fallback.Clear();
            foreach (var kv in DataManager.AllResearch) _fallback.Add(kv.Value);
            return _fallback;
        }
    }

    // 매번 새 리스트를 만들면 UI가 열릴 때마다 쓰레기가 생긴다 — 한 벌만 돌려 쓴다.
    readonly List<ResearchData> _fallback = new();

    public static event Action<ResearchTable> OnSelected;

    // SelectionManager의 단일 레이캐스트가 이 연구대를 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

    public void QueueResearch(ResearchData data)
    {
        if (data == null || JobManager.Instance == null) return;
        // 휴면(핵 영역 밖) 연구대는 사용 불가 — UI 경로가 막혀도 직접 호출 방어.
        if (BuildingDormancy.IsDormantAt(this)) { Debug.Log("[ResearchTable] 휴면 중 — 연구 불가"); return; }
        JobManager.Instance.RegisterJob(new ResearchJob(this, data));
    }

    public bool CanResearch(ResearchData data)
    {
        if (data == null) return false;
        if (ResearchManager.Instance != null && !ResearchManager.Instance.CanResearch(data)) return false;
        foreach (var ing in data.Ingredients)
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount) return false;
        return true;
    }
}
