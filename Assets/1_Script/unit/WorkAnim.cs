// **지금 어떤 동작으로 일하고 있나.** 일감(`JobBase.Anim`)이 답하고 블랙보드(`UnitBlackboard.Working`)에
// 실려 애니메이터(`Work` 매개변수)와 손에 드는 도구(`PawnProps`)가 읽는다.
//
// `JobCategory`보다 잘다 — 농사 하나에도 씨 뿌리기·물 주기·거두기가 다른 동작이고, 건설과 제작은
// 같은 망치질인데 벽이냐 작업대냐가 다르다. **값은 뒤에 붙일 것**(애니메이터 매개변수가 정수다).
public enum WorkAnim
{
    None,
    Mining,        // 곡괭이로 벽을 찍는다
    Chopping,      // 도끼로 찍는다
    HammerWall,    // 벽에 망치질 — 건설
    HammerTable,   // 작업대 망치질 — 제작
    Seeding,       // 씨 뿌리기
    Watering,      // 물 주기
    Gathering,     // 쭈그려 줍기 — 채집
    Skinning,      // 작업대에서 가죽 벗기기 — 도축대가 있을 때

    // **같은 일도 자리가 다르면 동작이 다르다**(2026-09-11, 사용자). 여기서 갈라 두면
    // 클립도 손에 드는 것도 따라온다 — 일감이 *어느 자리에서 하는지*를 이미 알고 있어서,
    // 아는 쪽이 적어 주는 것이 화면이 판정을 다시 짐작하는 것보다 낫다.
    SkinningGround,  // 바닥에 쭈그려 가죽 벗기기 — 도축대가 없을 때
    HammerGround,    // 바닥에 망치질 — 바닥·기단·계단을 깐다
    Harvesting,      // 밭에서 거두기 — 채집(`Gathering`)과 다른 손놀림이다
}
