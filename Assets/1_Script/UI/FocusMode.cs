// **드래그의 뜻을 바꾸는 모드가 켜져 있나 — 켜져 있으면 무리·살펴보기 같은 창을 치운다**(2026-09-19).
//
// (사용자: *"명령이나 밭구역 눌렀을 때 그냥 다른 UI들 치워버려야 할 거 같아 무리나 상세정보 보여주는거나
//  그냥 불편하기만하네"*.) 건설 모드는 이미 그렇게 하고 있었다(창마다 `BuildModeManager.OnActiveChanged`를
// 들었다) — 명령·밭 구역만 빠져 있었다. **묻는 자리를 하나로 둔다** — 창마다 모드 넷을 각자 물으면 모드가
// 늘 때 한 창만 안 치워진다.
public static class FocusMode
{
    public static bool Active =>
        BuildModeManager.IsActive || CommandMode.IsActive || FarmZoneDesignator.IsActive;
}
