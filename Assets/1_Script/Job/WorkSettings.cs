using System;
using System.Collections.Generic;
using UnityEngine;   // Debug

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
    //
    // **직접 배정 카테고리(채굴·벌목·경비)는 여기서 아무 뜻이 없다.** 그것들은 큐를 타지
    // 않으므로 켜고 끌 대상이 아니다 — 그런데 예전엔 목록에 그것들만 적혀 있어도
    // "목록이 비지 않았다"고 보고 **큐 작업을 전부 껐다.** `Goblin.AllowedJobs`가
    // [채굴, 벌목]이었던 탓에 하수인은 하나같이 운반·건설·제작을 안 했고, 남은 방랑자
    // 한 명이 온 짐을 날랐다("운반을 한 명만 한다"의 정체).
    //
    // 큐 카테고리가 하나도 없는 목록은 큐 작업에 대해 **아무 말도 하지 않은 것**이다.
    // 그럴 땐 빈 목록과 똑같이 전부 허용으로 본다.
    public void InitFromAllowed(IReadOnlyList<JobCategory> allowed)
    {
        _disabled.Clear();
        if (allowed == null || allowed.Count == 0) { OnChanged?.Invoke(); return; }

        bool mentionsQueue = false;
        for (int i = 0; i < allowed.Count; i++)
            if (JobCategoryInfo.IsQueued(allowed[i])) { mentionsQueue = true; break; }

        if (!mentionsQueue)
        {
            Debug.LogWarning($"[WorkSettings] {name}: 허용 작업 목록에 큐 작업이 하나도 없습니다 " +
                             "(채굴·벌목·경비는 직접 배정이라 여기서 켜고 끄는 대상이 아닙니다). " +
                             "전부 허용으로 둡니다 — 의도한 제한이 있다면 MinionData에 큐 작업으로 적으세요.");
            OnChanged?.Invoke();
            return;
        }

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
