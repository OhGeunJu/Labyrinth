using UnityEngine;

public class Corpse : MonoBehaviour
{
    public Inventory Inventory { get; private set; }
    public string UnitName    { get; private set; }

    public void Init(string unitName, Inventory inventory)
    {
        UnitName  = unitName;
        Inventory = inventory;
    }
}
