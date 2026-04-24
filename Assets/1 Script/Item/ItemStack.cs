using System;

[Serializable]
public struct ItemStack
{
    public ItemData Item;
    public int Quantity;

    public ItemStack(ItemData item, int quantity)
    {
        Item = item;
        Quantity = quantity;
    }
}
