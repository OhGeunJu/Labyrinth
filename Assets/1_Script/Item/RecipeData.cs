using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonColony/RecipeData")]
public class RecipeData : ScriptableObject
{
    [Serializable]
    public struct Ingredient
    {
        public ItemData Item;
        public int      Amount;
    }

    public string           RecipeName;
    public List<Ingredient> Ingredients  = new();
    public ItemData         Result;
    public int              ResultAmount = 1;
    public float            CraftTime    = 5f;
    public JobCategory      Category     = JobCategory.Crafting;

    [Header("스킬")]
    public CraftSkill RequiredSkillType  = CraftSkill.Labor;
    [Tooltip("이 수치 미만이면 결과물 50% 패널티")]
    public float      RequiredSkillLevel = 0f;
    [Tooltip("제작 완료 시 스킬 성장량")]
    public float      SkillGrowthAmount  = 0.5f;

    [Header("연구 해금")]
    [Tooltip("null이면 처음부터 개방. 지정 시 해당 연구 완료 후 제작 가능.")]
    public ResearchData RequiredResearch;
}
