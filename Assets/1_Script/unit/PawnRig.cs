using UnityEngine;

// **폰의 관절.** 애니메이션이 돌리는 것은 이 관절들이고, 보이는 살(큐브·구)은 관절의 자식이다.
//
// 그전에는 몸통·머리·팔·다리가 `Model` 아래에 **나란한 형제**였다 — 피벗이 저마다 제 한복판이라
// 팔을 돌리면 어깨가 아니라 팔꿈치 자리에서 돈다. 관절을 두고 살을 그 아래에 붙이면 **어깨에서
// 돌고**, 애니메이션 클립은 관절 이름(경로)만 알면 되므로 **사람·고블린·해골이 같은 클립을 쓴다.**
//
// **관절 이름과 계층은 규격이다** — 클립이 경로로 묶이므로 바꾸면 그 클립이 통째로 조용히 안 먹는다:
//
//   두발(`Biped`)     Model/Hips/{Spine/{Head, ArmL/LowerArmL/HandL, ArmR/LowerArmR/HandR},
//                                LegL/LowerLegL/FootL, LegR/LowerLegR/FootR}
//   네발(`Quadruped`) Model/Body/{Head, Tail, LegFL, LegFR, LegBL, LegBR}
//
// **두발은 사람 뼈 열다섯이다** — 그 위에 휴머노이드 아바타를 세워 **받은 휴머노이드 클립을 그대로**
// 쓴다(생김새는 블록 그대로, 뼈대만 사람 규격). 팔·다리 큐브는 반으로 갈라 팔꿈치·무릎에서 접힌다.
// 네발은 사람 뼈가 없으므로 관절 회전 곡선을 직접 굽는다.
//
// 짓는 도구는 `DungeonColony/Pawn/2. Build Pawn Rigs`(에디터). 손으로 만들지 말 것 — 서른 프리팹이
// 같은 규격이어야 하고, 관절 자리는 살의 크기에서 나온다(어깨 = 팔의 꼭대기).
//
// 골렘은 여기 안 온다 — 스킨 메시(FBX 휴머노이드)라 제 뼈가 있다. **치비 몸도 스킨 메시다**(2026-10-09) — 관절은 같은
// 규격이지만 `Pawn/3`(`ChibiPawnCreator.DressSkinned`)이 몸을 지으면서 세운다(`Pawn/2`는 건너뛴다).
public class PawnRig : MonoBehaviour
{
    public enum BodyKind { Biped, Quadruped }

    public BodyKind Kind;

    // 두발
    public Transform Hips, Spine, Head, ArmL, ArmR, LegL, LegR;
    public Transform LowerArmL, LowerArmR, LowerLegL, LowerLegR, FootL, FootR;
    // **무기가 붙을 자리** — 손 끝. 지금은 비어 있고, 장비 모델을 보여 주는 날 여기에 단다.
    public Transform HandL, HandR;

    // 네발
    public Transform Body, Tail, LegFL, LegFR, LegBL, LegBR;
}
