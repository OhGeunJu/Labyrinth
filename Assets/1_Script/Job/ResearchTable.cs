using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ResearchTable : MonoBehaviour
{
    [SerializeField] List<ResearchData> availableResearch = new();

    public IReadOnlyList<ResearchData> AllResearch => availableResearch;

    public static event Action<ResearchTable> OnSelected;

    Camera _cam;

    void Awake() => _cam = Camera.main;

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject)
            OnSelected?.Invoke(this);
    }

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
