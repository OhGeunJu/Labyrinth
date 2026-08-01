using System;
using System.Collections.Generic;
using UnityEngine;

// 연구 완료 상태를 관리하는 싱글턴.
// ResearchJob이 Complete()를 호출하면 관련 레시피가 해금됨.
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }
    public static event Action<ResearchData> OnResearchCompleted;

    readonly HashSet<ResearchData> _completed = new();

    // 레시피 → 그것을 해금하는 연구. `ResearchData.UnlockedRecipes`를 뒤집어 만든다.
    // 잠금은 두 방향 어느 쪽으로 적어도 걸린다:
    //   ResearchData.UnlockedRecipes  ← 큐레이션(연구 하나가 여러 레시피를 연다)
    //   RecipeData.RequiredResearch   ← 레시피 개별 지정
    // 둘 다 없으면 처음부터 열린 레시피다.
    readonly Dictionary<RecipeData, ResearchData> _unlockedBy = new();

    // ── 부분 진행도 (초) ──────────────────────────────────────────────────
    // **중단해도 그동안 한 만큼이 남는다.** 전에는 `ResearchJob`이 `Time.time + 총시간`으로
    // 끝날 시각만 들고 있어서, 폰이 밥 먹으러 가거나 이동 명령을 받는 순간 진행이 0으로 돌아갔다.
    // 긴 연구일수록 끝낼 수 없었다 — 중간에 한 번도 안 끊길 수가 없으니까.
    readonly Dictionary<ResearchData, float> _progress = new();

    public float GetProgressSeconds(ResearchData research) =>
        research != null && _progress.TryGetValue(research, out var s) ? s : 0f;

    public void SetProgressSeconds(ResearchData research, float seconds)
    {
        if (research == null) return;
        _progress[research] = Mathf.Max(0f, seconds);
    }

    public void ClearProgress(ResearchData research)
    {
        if (research != null) _progress.Remove(research);
    }

    // 0~1. UI가 "이 연구가 어디까지 왔나"를 폰이 붙어 있지 않을 때도 보여줄 수 있게.
    public float GetProgress01(ResearchData research)
    {
        if (research == null) return 0f;
        return Mathf.Clamp01(GetProgressSeconds(research) / Mathf.Max(0.01f, research.ResearchTime));
    }

    // 예전엔 `Instance`(프로퍼티)와 `_instance`(필드)가 따로 있었고 Awake가 필드에만
    // 대입했다 — 그래서 `ResearchManager.Instance`가 **항상 null**이었고, 그걸 읽는 6곳이
    // 전부 "매니저 없음" 분기로 빠졌다(레시피 전부 해금 / 연구 완료 무시 / 선행 검사 통과).
    // 통로를 하나로 합쳐 다시 갈라지지 않게 한다.
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUnlockIndex();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // DataManager의 연구 전체를 훑어 역인덱스를 만든다.
    // 이걸 안 하면 `UnlockedRecipes`는 아무도 안 읽는 죽은 데이터가 되고,
    // `RequiredResearch`가 비어 있는 레시피는 연구 없이도 전부 제작 가능해진다.
    void BuildUnlockIndex()
    {
        _unlockedBy.Clear();
        foreach (var research in DataManager.AllResearch.Values)
        {
            if (research == null) continue;
            foreach (var recipe in research.UnlockedRecipes)
            {
                if (recipe == null || _unlockedBy.ContainsKey(recipe)) continue;
                _unlockedBy[recipe] = research;
            }
        }
    }

    // 연구 완료 (ResearchJob에서 호출)
    public void Complete(ResearchData research)
    {
        if (research == null || _completed.Contains(research)) return;
        _completed.Add(research);
        Debug.Log($"[Research] 연구 완료 — {research.ResearchName}");
        OnResearchCompleted?.Invoke(research);
    }

    // 레시피 해금 여부 확인. 두 방향 중 하나라도 연구를 가리키면 그 연구가 끝나야 한다.
    public bool IsUnlocked(RecipeData recipe)
    {
        if (recipe == null) return false;

        if (recipe.RequiredResearch != null)
            return _completed.Contains(recipe.RequiredResearch);

        if (_unlockedBy.TryGetValue(recipe, out var research))
            return _completed.Contains(research);

        return true;   // 어느 연구도 걸어두지 않음 = 처음부터 열림
    }

    // 이 레시피를 여는 연구(없으면 null). UI가 "○○ 연구 필요"를 띄울 때 쓴다.
    public ResearchData GetRequiredResearch(RecipeData recipe)
    {
        if (recipe == null) return null;
        if (recipe.RequiredResearch != null) return recipe.RequiredResearch;
        return _unlockedBy.TryGetValue(recipe, out var research) ? research : null;
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

    // 이 연구가 여는 레시피 — **양방향을 모두 모은다.**
    // 잠금은 `ResearchData.UnlockedRecipes`(큐레이션)로도, `RecipeData.RequiredResearch`
    // (JSON의 `requiredResearchId` 포함)로도 걸린다. 한쪽만 읽으면 반대로 적힌 것이
    // 화면에서 통째로 사라진다 — 연구를 고르는 유일한 근거인데.
    public List<RecipeData> GetUnlockedRecipes(ResearchData research)
    {
        var list = new List<RecipeData>();
        if (research == null) return list;

        if (research.UnlockedRecipes != null)
            foreach (var r in research.UnlockedRecipes)
                if (r != null && !list.Contains(r)) list.Add(r);

        foreach (var kv in DataManager.AllRecipes)
        {
            var r = kv.Value;
            if (r != null && r.RequiredResearch == research && !list.Contains(r)) list.Add(r);
        }
        return list;
    }
}
