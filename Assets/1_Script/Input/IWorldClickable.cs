// 월드 좌클릭으로 선택 가능한 건물/오브젝트.
// SelectionManager의 단일 레이캐스트가 hit collider에서 이 인터페이스를 찾아 OnWorldClick을 호출한다.
// 이전엔 Storage/Bed/FarmPlot/Merchant/CraftingTable/ResearchTable/PatrolZone이 각자 Update에서
// 개별 레이캐스트 → 건물 수만큼 중복 + UI 관통 + 빌드모드 미게이트 + 선택 우선순위 미조정 문제.
// 이 인터페이스로 라우팅을 일원화(클릭당 레이캐스트 1회, UI 블로킹·빌드모드 게이트 공유).
public interface IWorldClickable
{
    void OnWorldClick();
}

// 지금 이 클릭을 받을 수 있는가. 안 붙이면 언제나 받는다.
//
// **못 받으면 클릭은 사라지지 않고 다음 처리로 넘어간다** — 쓰러진 상인을 누르면 상점 대신
// 폰 정보가 뜬다. 거절을 알림으로 띄우는 길도 있었지만, 그건 이벤트 알림 패널을 조작 실패에
// 쓰는 것이라 "무슨 사건이 일어났나"를 보는 자리에 사건이 아닌 것이 섞인다. 쓰러진 상인은
// 눈으로 보이므로 설명이 필요 없고, 정보 패널이 뜨는 편이 오히려 상태를 알려 준다.
public interface IClickGate
{
    bool CanClick { get; }
}
