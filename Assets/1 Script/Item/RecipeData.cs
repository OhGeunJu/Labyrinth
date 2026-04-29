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

    public string          RecipeName;
    public List<Ingredient> Ingredients = new();
    public ItemData        Result;
    public int             ResultAmount = 1;
    public float           CraftTime    = 5f;
    public JobCategory     Category     = JobCategory.Crafting;
}
