using System;
using System.Collections.Generic;
using UnityEngine;

// 폰 하나의 작업 허용 설정 — "이 작업을 한다 / 안 한다"만 담는다.
// 무엇을 먼저 할지는 폰별 숫자가 아니라 JobCategoryInfo.QueueOrder(전역 고정)가 결정한다.
//
// 부착: UnitBrain.Awake가 플레이어 세력 폰(방랑자·하수인)에만 자동으로 붙인다.
// 하수인은 MinionData.AllowedJobs를 **초기값**으로만 쓰고, 이후엔 개체별로 바뀐다
// (같은 MinionData를 공유하는 다른 하수인에 영향을 주지 않는다).
public class WorkSettings : MonoBehaviour
{
    public event Action OnChanged;

    // 기본값은 "전부 켬" — 끈 것만 저장한다.
    readonly HashSet<JobCategory> _disabled = new();

    public bool IsEnabled(JobCategory category)
        => JobCategoryInfo.IsQueued(category) && !_disabled.Contains(category);

    public void SetEnabled(JobCategory category, bool enabled)
    {
        if (!JobCategoryInfo.IsQueued(category)) return;

        bool changed = enabled ? _disabled.Remove(category) : _disabled.Add(category);
        if (changed) OnChanged?.Invoke();
    }

    public void Toggle(JobCategory category) => SetEnabled(category, !IsEnabled(category));

    // 하수인 초기값. 목록이 비어 있으면 "전부 허용"(MinionData의 기존 규칙 그대로).
    public void InitFromAllowed(IReadOnlyList<JobCategory> allowed)
    {
        _disabled.Clear();
        if (allowed == null || allowed.Count == 0) { OnChanged?.Invoke(); return; }

        foreach (var category in JobCategoryInfo.QueueOrder)
            if (!Contains(allowed, category))
                _disabled.Add(category);

        OnChanged?.Invoke();
    }

    static bool Contains(IReadOnlyList<JobCategory> list, JobCategory category)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == category) return true;
        return false;
    }
}
