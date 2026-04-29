using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CraftingTable : MonoBehaviour
{
    [SerializeField] List<RecipeData> availableRecipes = new();

    public IReadOnlyList<RecipeData> Recipes => availableRecipes;

    public static event Action<CraftingTable> OnSelected;

    Camera _cam;

    void Awake() => _cam = Camera.main;

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
        var ray = _cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit) && hit.collider.gameObject == gameObject)
            OnSelected?.Invoke(this);
    }

    public void QueueOrder(RecipeData recipe)
    {
        if (recipe == null || JobManager.Instance == null) return;
        JobManager.Instance.RegisterJob(new CraftJob(this, recipe));
    }

    public bool CanCraft(RecipeData recipe)
    {
        if (recipe == null) return false;
        foreach (var ing in recipe.Ingredients)
            if (StorageBuilding.GetTotalByItem(ing.Item) < ing.Amount)
                return false;
        return true;
    }
}
