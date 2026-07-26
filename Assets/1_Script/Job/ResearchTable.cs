using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ResearchTable : MonoBehaviour, IWorldClickable
{
    [SerializeField] List<ResearchData> availableResearch = new();

    public IReadOnlyList<ResearchData> AllResearch => availableResearch;

    public static event Action<ResearchTable> OnSelected;

    // SelectionManager의 단일 레이캐스트가 이 연구대를 클릭 판정 시 호출.
    public void OnWorldClick() => OnSelected?.Invoke(this);

    public void QueueResearch(ResearchData data)
    {
        if (data == null || JobManager.Instance == null) return;
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
