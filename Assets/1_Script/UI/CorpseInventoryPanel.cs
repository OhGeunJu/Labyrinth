using UnityEngine;

public class CorpseInventoryPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RectTransform gridContainer;

    InventoryGridUI _gridUI;

    void Awake()
    {
        if (gridContainer != null)
        {
            _gridUI = gridContainer.GetComponent<InventoryGridUI>();
            if (_gridUI == null) _gridUI = gridContainer.gameObject.AddComponent<InventoryGridUI>();
        }
        if (panel != null) panel.SetActive(false);
        SelectionManager.OnCorpseSelected += OnCorpseSelected;
    }

    void OnDestroy() => SelectionManager.OnCorpseSelected -= OnCorpseSelected;

    void OnCorpseSelected(Corpse corpse)
    {
        if (panel == null || _gridUI == null) return;
        _gridUI.Bind(corpse.Inventory);
        panel.SetActive(true);
    }
}
