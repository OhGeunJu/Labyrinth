using UnityEngine;

// **폰을 바닥 위에 세우는 일 한 곳.**
//
// 같은 계산이 다섯 군데에 흩어져 있었다 — 스폰(`EventManager`), 건물이 깔고 앉은 칸에서
// 밀어내기(`PlacedBuilding`), 업고 내려놓기(`PawnCarry`), 갇힌 자리 붙들기(`Imprisonment`),
// 낙하 복구(`PawnMover`). 셋은 주석으로 **같은 함정을 각자 설명**하고 있었는데, 설명이
// 세 벌이면 값이 어긋나는 것은 시간문제다(적재 한도가 3.3배 어긋나 있던 것과 같은 모양).
//
// ── 함정 둘 ────────────────────────────────────────────────────────────────
//
// **① 표면 Y는 발밑이지 중심이 아니다.** `MapManager.TileToGround`가 주는 것은 **바닥 윗면**인데
// `CharacterController`는 중심이 0이고 높이가 2다 — 그대로 찍으면 **아래 절반이 파묻힌다.**
// 유니티가 겹침을 풀며 옆으로 밀어내고, 그 자리가 바닥 가장자리면 그대로 떨어진다
// ("바닥보다 살짝 밖에서 나와서 계속 떨어진다", 2026-08-08).
//
// **② 켠 채로 옮기면 되민다.** `CharacterController`가 켜져 있으면 순간이동한 자리에서
// 콜라이더가 제자리로 밀어낸다. 껐다 옮기고 다시 켜야 한다.
//
// 핵 소환이 이 함정을 안 밟았던 건 `GetSpawnPosition`이 **핵의 부양 높이**를 써서 공중에서
// 떨어져 착지하기 때문이다 — 운이 좋았던 것이지 다른 규칙이었던 게 아니다.
public static class PawnPlacement
{
    // 발이 바닥에 닿게 하려면 중심을 이만큼 올려야 한다. 컨트롤러가 없으면 1m로 친다.
    public const float GroundClearance = 0.05f;   // 살짝 띄워 시작 — 겹침 판정을 피한다

    public static float Lift(GameObject pawn)
    {
        var cc = pawn != null ? pawn.GetComponent<CharacterController>() : null;
        return Lift(cc);
    }

    public static float Lift(CharacterController cc) =>
        cc != null ? cc.height * 0.5f - cc.center.y : 1f;

    // **바닥 윗면 좌표**를 받아 그 위에 세운다(`TileToGround`가 주는 값을 그대로 넘기면 된다).
    public static Vector3 StandingPos(GameObject pawn, Vector3 groundPos) =>
        new Vector3(groundPos.x, groundPos.y + Lift(pawn) + GroundClearance, groundPos.z);

    // 옮긴다 — 컨트롤러를 껐다 켜는 것까지. `rotation`을 주면 같이 맞춘다.
    public static void PlaceOnGround(GameObject pawn, Vector3 groundPos, Quaternion? rotation = null)
    {
        if (pawn == null) return;

        var cc = pawn.GetComponent<CharacterController>();
        bool was = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        pawn.transform.position = new Vector3(groundPos.x, groundPos.y + Lift(cc) + GroundClearance,
                                              groundPos.z);
        if (rotation.HasValue) pawn.transform.rotation = rotation.Value;

        // **원래 켜져 있었을 때만 되켠다.** 업혀 있는 폰은 꺼진 채로 있어야 한다 —
        // 무턱대고 켜면 부모에 붙은 콜라이더가 제자리로 되밀어 어깨에서 흘러내린다.
        if (cc != null && was) cc.enabled = true;
    }

    // 이미 컨트롤러가 꺼져 있는 폰을 **켜면서** 내려놓는다(업기 해제·스폰 직후).
    public static void PlaceAndEnable(GameObject pawn, Vector3 groundPos, Quaternion? rotation = null)
    {
        if (pawn == null) return;

        var cc = pawn.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        pawn.transform.position = new Vector3(groundPos.x, groundPos.y + Lift(cc) + GroundClearance,
                                              groundPos.z);
        if (rotation.HasValue) pawn.transform.rotation = rotation.Value;

        if (cc != null) cc.enabled = true;
    }
}
