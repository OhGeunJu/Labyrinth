using UnityEngine;

// 고른 폰의 발밑 고리. 모양은 `GroundRing` 한 벌이다(물건과 같은 신호).
//
// **색·크기는 씬이 안 든다** — 골렸다는 표시는 게임의 규약이지 폰마다 저작할 것이 아니다.
// 전에는 `[SerializeField] Color`였고, 그러면 코드 기본값을 고쳐도 프리팹 서른 개가 옛 초록을
// 그대로 든다(*"코드 기본값은 씬이 값을 들면 아무 힘이 없다"*).
public class SelectionIndicator : MonoBehaviour
{
    // 진한 초록(사용자 선택). 운반 지정 구슬·선은 연한 연두(`DroppedItem.PendingColor` ·
    // `HaulDesignator.Line`)라 이쪽을 어둡고 짙게 두어야 *골렸다*와 *찍혔다*가 갈린다.
    static readonly Color RingColor = new(0.08f, 0.62f, 0.22f, 0.95f);

    GameObject _ring;

    void Awake()
    {
        // **발밑이 어디인지는 콜라이더가 답한다**(`PawnPlacement.Lift` = 루트에서 바닥까지).
        // 예전엔 `-0.9`로 못 박혀 있었는데, 그건 사람 꼴(높이 2·중심 0)만 맞는 값이다 —
        // 늑대·곰처럼 몸이 낮아 콜라이더를 줄인 폰은 **표식이 땅속에 그려진다.**
        // 살짝 띄우는 것은 지면과 z-파이팅을 피하려는 것.
        float foot = -PawnPlacement.Lift(gameObject) + 0.06f;

        // 고리는 몸보다 한 뼘 넓다 — 몸에 딱 붙으면 발에 가려 반쪽만 보인다.
        var cc = GetComponent<CharacterController>();
        float radius = (cc != null ? cc.radius : 0.35f) * 1.5f;

        _ring = GroundRing.Create(transform, foot, radius, RingColor, "SelectRing");
        _ring.SetActive(false);
    }

    public void SetSelected(bool selected) => _ring.SetActive(selected);
}
