using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CraftingTable : MonoBehaviour
{
    [SerializeField] CraftSkill tableType = CraftSkill.WeaponCrafting;

    public CraftSkill TableType => tableType;

    // DataManager에서 tableType에 맞는 레시피만 반환 (수동 등록 불필요)
    public IReadOnlyList<RecipeData> Recipes =>
        new List<RecipeData>(DataManager.GetRecipesBySkill(tableType));

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
            if (Storage.GetTotalByItem(ing.Item) < ing.Amount)
                return false;
        return true;
    }
}
