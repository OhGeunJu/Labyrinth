// 월드 좌클릭으로 선택 가능한 건물/오브젝트.
// SelectionManager의 단일 레이캐스트가 hit collider에서 이 인터페이스를 찾아 OnWorldClick을 호출한다.
// 이전엔 Storage/Bed/FarmPlot/Merchant/CraftingTable/ResearchTable/PatrolZone이 각자 Update에서
// 개별 레이캐스트 → 건물 수만큼 중복 + UI 관통 + 빌드모드 미게이트 + 선택 우선순위 미조정 문제.
// 이 인터페이스로 라우팅을 일원화(클릭당 레이캐스트 1회, UI 블로킹·빌드모드 게이트 공유).
public interface IWorldClickable
{
    void OnWorldClick();
}
