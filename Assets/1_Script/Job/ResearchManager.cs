using System;
using System.Collections.Generic;
using UnityEngine;

// 연구 완료 상태를 관리하는 싱글턴.
// ResearchJob(미구현)이 Complete()를 호출하면 관련 레시피가 해금됨.
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }
    public static event Action<ResearchData> OnResearchCompleted;

    readonly HashSet<ResearchData> _completed = new();

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;
    }
    static ResearchManager _instance;

    // 연구 완료 (ResearchJob에서 호출)
    public void Complete(ResearchData research)
    {
        if (research == null || _completed.Contains(research)) return;
        _completed.Add(research);
        Debug.Log($"[Research] 연구 완료 — {research.ResearchName}");
        OnResearchCompleted?.Invoke(research);
    }

    // 레시피 해금 여부 확인
    public bool IsUnlocked(RecipeData recipe)
    {
        if (recipe == null) return false;
        if (recipe.RequiredResearch == null) return true;          // 연구 불필요
        return _completed.Contains(recipe.RequiredResearch);
    }

    // 선행 연구가 완료됐는지 확인 (연구 가능 여부)
    public bool CanResearch(ResearchData research)
    {
        if (research == null) return false;
        if (_completed.Contains(research)) return false;           // 이미 완료
        if (research.Prerequisite == null) return true;            // 선행 없음
        return _completed.Contains(research.Prerequisite);
    }

    public bool IsCompleted(ResearchData research) => _completed.Contains(research);
}
