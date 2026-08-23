using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/ResearchData")]
public class ResearchData : ScriptableObject
{
    [Serializable]
    public struct Ingredient
    {
        public ItemData Item;
        public int      Amount;
    }

    [Header("기본 정보")]
    [Tooltip("식별자. 비우면 에셋 파일명을 쓴다(권장). 표시명과 달리 번역되지 않는다.")]
    public string           Id;
    public string           ResearchName;
    [TextArea]
    public string           Description;

    [Header("연구 비용")]
    public List<Ingredient> Ingredients       = new();
    public float            ResearchTime      = 30f;   // 초
    public float            SkillGrowthAmount = 1f;    // Research 스킬 성장량

    // **선행은 여럿일 수 있다**(2026-08-23). 단일 참조였을 때는 사슬만 표현됐다 —
    // *"제련술과 목공술을 둘 다 끝내야 열리는 것"*을 적을 자리가 없었고, 그러면 연구가
    // 아무리 늘어도 화면은 일렬로만 뻗는다. 리스트로 두면 그래프가 곧 저작물이 된다.
    //
    // **비어 있으면 처음부터 열린 연구다.**
    [Header("선행 연구")]
    public List<ResearchData> Prerequisites = new();

    [Header("해금 레시피")]
    public List<RecipeData> UnlockedRecipes = new();
}
