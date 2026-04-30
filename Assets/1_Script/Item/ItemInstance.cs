// 인벤토리 셀에 실제로 존재하는 아이템 인스턴스 (런타임 객체).
// ItemData(템플릿) + 위치 + 회전 + 스택 수량 + 등급.
public class ItemInstance
{
    public ItemData     Data;
    public int          Quantity;
    public int          GridX;
    public int          GridY;
    public bool         Rotated;
    public ItemQuality  Quality;

    public int EffectiveWidth  => Rotated ? Data.Height : Data.Width;
    public int EffectiveHeight => Rotated ? Data.Width  : Data.Height;

    // 인벤토리 표시용 이름 (등급 접두사 포함)
    public string DisplayName
    {
        get
        {
            string prefix = Quality.DisplayName();
            return string.IsNullOrEmpty(prefix) ? Data.ItemName : $"[{prefix}] {Data.ItemName}";
        }
    }

    public ItemInstance(ItemData data, int quantity, int x, int y,
                        bool rotated = false, ItemQuality quality = ItemQuality.Normal)
    {
        Data     = data;
        Quantity = quantity;
        GridX    = x;
        GridY    = y;
        Rotated  = rotated;
        Quality  = quality;
    }
}
