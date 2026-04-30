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
    public string           ResearchName;
    [TextArea]
    public string           Description;

    [Header("연구 비용")]
    public List<Ingredient> Ingredients  = new();
    public float            ResearchTime = 30f;   // 초

    [Header("선행 연구")]
    public ResearchData     Prerequisite;          // null이면 기본 개방

    [Header("해금 레시피")]
    public List<RecipeData> UnlockedRecipes = new();
}
