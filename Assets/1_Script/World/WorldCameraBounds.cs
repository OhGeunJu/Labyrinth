using UnityEngine;

// 월드맵 카메라를 맵 밖으로 못 나가게 가둔다 — **카메라 위치(XZ)를 직접 클램프한다.**
//
// 처음엔 "내려다보는 지점"(전방 레이 ∩ 바닥 평면)만 가뒀는데 그게 틀렸다:
// 기울어진 카메라는 초점보다 `높이 ÷ tan(피치)`만큼 뒤에 있어서, 피치 30°·높이 90이면
// 초점이 가장자리에 붙어 있어도 **카메라는 맵 밖 156유닛 바깥**에 선다. 시야만 갇히고 카메라는 뚫렸다.
// 위치를 막는 게 "맵 밖으로 못 나간다"는 말 그대로의 동작이고, 예측도 된다.
//
// 기울어진 시점이라 먼 쪽 가장자리 너머가 화면에 들어오는 건 정상이다(RTS 카메라가 다 그렇다).
// 가장자리에서 답답하면 `edgeMargin`을 키우면 된다.
//
// `CameraController`가 Update에서 움직인 **뒤**에 보정해야 하므로 LateUpdate에서 돈다.
[DefaultExecutionOrder(50)]
public class WorldCameraBounds : MonoBehaviour
{
    [Tooltip("맵 범위를 읽어올 격자. 비우면 씬에서 찾는다.")]
    [SerializeField] WorldMapGrid grid;

    [Tooltip("맵 경계 바깥으로 카메라가 나갈 수 있는 여유(월드 유닛). " +
             "기울어진 카메라는 맵을 정면으로 담으려면 가장자리 바깥에 서야 하므로 어느 정도 필요하다.")]
    [SerializeField] float edgeMargin = 24f;

    void LateUpdate()
    {
        // 필드 모드에선 월드 카메라가 놀고 있으므로 계산할 이유가 없다.
        if (!GameModeManager.IsWorld) return;

        if (grid == null) grid = FindFirstObjectByType<WorldMapGrid>();
        var wm = WorldMap.Instance;
        if (grid == null || wm == null) return;

        var min = grid.Origin.position;
        var max = min + new Vector3(wm.Width * grid.NodeSpacing, 0f, wm.Height * grid.NodeSpacing);

        var pos = transform.position;
        var clamped = new Vector3(
            Mathf.Clamp(pos.x, min.x - edgeMargin, max.x + edgeMargin),
            pos.y,                                   // 높이는 CameraController의 줌 한계가 담당
            Mathf.Clamp(pos.z, min.z - edgeMargin, max.z + edgeMargin));

        if (clamped != pos) transform.position = clamped;
    }
}
