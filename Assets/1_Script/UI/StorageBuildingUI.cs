using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class StorageBuildingUI : MonoBehaviour
{
    [Header("씬 참조")]
    [SerializeField] GameObject      panel;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] RectTransform   gridContainer;

    InventoryGridUI _gridUI;
    Storage         _current;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);

        if (gridContainer != null)
        {
            _gridUI = gridContainer.GetComponent<InventoryGridUI>();
            if (_gridUI == null) _gridUI = gridContainer.gameObject.AddComponent<InventoryGridUI>();
        }

        Storage.OnSelected               += Open;
        BuildModeManager.OnActiveChanged += OnBuildModeChanged;
    }

    void OnDestroy()
    {
        Storage.OnSelected               -= Open;
        BuildModeManager.OnActiveChanged -= OnBuildModeChanged;
    }

    void OnBuildModeChanged(bool active) { if (active) Close(); }

    void Update()
    {
        if (panel != null && panel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    void Open(Storage storage)
    {
        _current = storage;
        if (panel != null) panel.SetActive(true);
        if (titleText != null) titleText.text = storage.BuildingName;
        if (_gridUI != null)
        {
            _gridUI.ReceiveFilter = storage.CanAccept;
            _gridUI.Bind(storage.Inventory);
        }
    }

    public void Close()
    {
        _current = null;
        if (_gridUI != null)
        {
            _gridUI.ReceiveFilter = null;
            _gridUI.Bind(null);
        }
        if (panel != null) panel.SetActive(false);
    }
}
