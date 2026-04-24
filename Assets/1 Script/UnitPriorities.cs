using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitPriorities : MonoBehaviour
{
    public event Action OnChanged;

    readonly Dictionary<JobCategory, int> _priorities = new();

    void Awake()
    {
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
