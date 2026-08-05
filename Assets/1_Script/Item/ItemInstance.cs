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

    // **"이건 이 폰 것이다" 표시.** 게임이 알아서 처분하지 못하게 못 박는다.
    //
    // 종류로 거르지 않고 **뭉치 단위로** 잠근다 — "무기는 안 내린다" 같은 규칙은 반드시
    // 어긋난다(만들어 쌓아 둔 검은 창고로 가야 하고, 쥐어 준 예비 검은 남아야 한다).
    // 같은 종류라도 이 뭉치를 남길지 말지는 플레이어만 안다.
    //
    // 잠기면 이렇게 된다:
    //   · 자동 운반이 안 가져간다 (`UnloadJob`)
    //   · **재료로 안 센다** (`MaterialPool`) — 제작이 조용히 먹어 버리면 잠근 뜻이 없다
    //   · 팔 수 없고 버릴 수 없다 (`MerchantUI` · `DraggableItem`의 월드 드롭)
    //
    // 잠긴 뭉치도 **새 물건은 받는다**(`Inventory.TryAdd`). 잠금은 자리를 비워 두지 말라는
    // 뜻이지 봉인이 아니다 — 안 받으면 잠글 때마다 칸이 쪼개져 배낭이 금세 조각난다.
    // 합쳐진 것은 같이 잠긴 채로 남는다.
    //
    // 드래그로 옮기고 장착하는 것은 그대로 된다 — 손으로 하는 일까지 막으면 풀었다 잠그기를
    // 반복하게 된다. 막는 것은 **되돌릴 수 없는 처분**(판매·버리기)과 **자동**뿐이다.
    public bool         Locked;

    public int EffectiveWidth  => Rotated ? Data.Height : Data.Width;
    public int EffectiveHeight => Rotated ? Data.Width  : Data.Height;

    // 인벤토리 표시용 이름 (등급 접두사 포함)
    public string DisplayName => $"[{Quality.DisplayName()}] {Data.ItemName}";

    public ItemInstance(ItemData data, int quantity, int x, int y,
                        bool rotated = false, ItemQuality quality = ItemQuality.Common)
    {
        Data     = data;
        Quantity = quantity;
        GridX    = x;
        GridY    = y;
        Rotated  = rotated;
        Quality  = quality;
    }
}
