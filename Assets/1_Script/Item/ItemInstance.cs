using UnityEngine;

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

    // ── 남은 양 — 뭉치마다 "열린 하나" (2026-10-16) ─────────────────────────────
    //
    // 사용자: *"B가 나을 거 같다"*(아이템마다 남은 양) · *"회복 아이템도 치료한 만큼만 소모되는게 좋잖아"*.
    // 반쯤 탄 횃불·반쯤 쓴 붕대는 **물건이 든다** — 폰이 들면 다른 폰이 집었을 때 새것이 된다.
    //
    // **뭉치 하나에 열린 것은 하나뿐이다.** `Quantity`개 중 하나가 `Left`(0~1)만큼 남았고 나머지는 새것이다.
    // 쓰다 만 것이 칸을 따로 먹으면 붕대를 쓸 때마다 배낭이 조각나고, 꽉 찬 배낭에서는 쓰지도 못한다.
    //
    // 지키는 규칙 둘(`Inventory`):
    //   · **개수로 세고 빼는 길은 새것만 본다**(`FullUnits`) — 창고에서 붕대 다섯을 챙기는 길이 열린 것을
    //     새것으로 쳐서 옮기면 반쯤 쓴 것이 거기서 새것이 된다.
    //   · **열린 것을 넣으면 같은 물건의 열린 것과 합친다**(`TryAddOpened`) — 양이 보존된다(0.3 + 0.8 → 새것 하나 + 0.1).
    // 남은 양을 갖는 것은 닳는 물건뿐이다(`ItemData.Depletes`) — 나머지는 늘 1이다.
    public float Left = 1f;

    public const float Epsilon = 0.0001f;
    public bool  IsPartial => Left < 1f - Epsilon;
    public int   FullUnits => IsPartial ? Mathf.Max(0, Quantity - 1) : Quantity;
    public float Amount    => IsPartial ? Quantity - 1 + Left : Quantity;   // 새것으로 치면 몇 개인가

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
