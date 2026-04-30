using System;
using System.Collections.Generic;
using UnityEngine;

public class PriorityManager : MonoBehaviour
{
    public static PriorityManager Instance { get; private set; }
    public static event Action OnChanged;

    // 0 = 비활성, 1~5 = 우선순위 (기본 3)
    readonly Dictionary<JobCategory, int> _priorities = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        foreach (JobCategory cat in Enum.GetValues(typeof(JobCategory)))
            _priorities[cat] = cat == JobCategory.None ? 0 : 3;
    }

    public int Get(JobCategory category) =>
        _priorities.TryGetValue(category, out int p) ? p : 3;

    public void Set(JobCategory category, int priority)
    {
        if (category == JobCategory.None) return;
        _priorities[category] = Mathf.Clamp(priority, 0, 5);
        OnChanged?.Invoke();
    }

    public void Increase(JobCategory category) => Set(category, Get(category) + 1);
    public void Decrease(JobCategory category) => Set(category, Get(category) - 1);

    public bool IsEnabled(JobCategory category) => Get(category) > 0;
}
