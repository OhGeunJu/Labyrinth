using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 씬에 **손으로 놓은 건물**을 격자에 맞추고, 콜라이더를 건물 크기에 맞춘다.
//
// 왜 필요한가: 점유 발자국은 정수 칸인데(`BuildModeManager.GetFootprint`) 에디터에서 끌어다
// 놓은 건물은 아무 좌표에나 선다. 그러면 모델이 반 칸씩 밀려 **폰이 서야 할 작업 자리를
// 먹고**, 화면에는 길이 열려 있는데 폰이 모서리에 처박힌다(2026-08-08 연구대 사건).
//
// 콜라이더도 같은 사고의 다른 쪽이다 — 옛 리사이즈 도구가 모델만 키우고 콜라이더는 두고 가서
// 연구대가 모델 3×2에 콜라이더 1×1이었다. 길찾기가 아는 건 콜라이더뿐이라(씬 건물은 점유
// 등록도 안 됐다) 책상 한복판을 가로지르는 경로가 나왔다.
//
// **순서가 있다: 먼저 정렬, 그다음 콜라이더.** 어긋난 채로 콜라이더를 키우면 오히려 작업
// 자리를 물어 버린다.
public static class BuildingGridFixer
{
    // 발자국 규약: 홀수 크기 축은 칸 중앙(`.5`), 짝수 축은 칸 경계(`.0`).
    //   size 3 → tiles [a-1, a+1] → 중심 a+0.5
    //   size 2 → tiles [a,   a+1] → 중심 a+1.0
    static float SnapAxis(float value, int size)
    {
        float offset = (size % 2 == 1) ? 0.5f : 0f;
        return Mathf.Round(value - offset) + offset;
    }

    // 90°/270° 회전이면 축이 바뀐다(`BuildModeManager.FootprintSize`와 같은 규칙).
    static Vector2Int OrientedSize(BuildingData data, Transform t)
    {
        var s = new Vector2Int(Mathf.Max(1, data.Size.x), Mathf.Max(1, data.Size.y));
        int quarter = Mathf.RoundToInt(t.eulerAngles.y / 90f) & 3;
        return (quarter % 2 == 1) ? new Vector2Int(s.y, s.x) : s;
    }

    [MenuItem("DungeonColony/Snap Placed Buildings To Grid")]
    public static void SnapScene()
    {
        var buildings = Object.FindObjectsByType<PlacedBuilding>(FindObjectsSortMode.None);
        if (buildings.Length == 0)
        {
            Debug.LogWarning("[격자 정렬] 씬에서 `PlacedBuilding`을 찾지 못했습니다 — " +
                             "건물 프리팹에 `PlacedBuilding` + `data`가 붙어 있어야 합니다.");
            return;
        }

        int moved = 0, skipped = 0;
        foreach (var pb in buildings)
        {
            if (pb.Data == null) { skipped++; continue; }

            var t    = pb.transform;
            var size = OrientedSize(pb.Data, t);
            var p    = t.position;

            var snapped = new Vector3(SnapAxis(p.x, size.x), p.y, SnapAxis(p.z, size.y));
            if ((snapped - p).sqrMagnitude < 0.0001f) continue;

            Undo.RecordObject(t, "Snap Building To Grid");
            t.position = snapped;
            EditorUtility.SetDirty(t);
            moved++;

            Debug.Log($"[격자 정렬] {pb.name} ({size.x}×{size.y}) " +
                      $"({p.x:F3}, {p.z:F3}) → ({snapped.x:F1}, {snapped.z:F1})");
        }

        if (moved > 0)
        {
            // 씬에 사는 것은 `SaveAssets`로 안 남는다 — 씬을 직접 더티로 표시하고 저장한다.
            EditorSceneManager.MarkSceneDirty(buildings[0].gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
        }

        Debug.Log($"[격자 정렬] 완료 — {moved}개 이동, {skipped}개 건너뜀(BuildingData 없음), " +
                  $"검사 {buildings.Length}개.");
    }

    [MenuItem("DungeonColony/Fit Building Colliders To Size")]
    public static void FitColliders()
    {
        int fixedCount = 0;

        // `DataManager`는 `RuntimeInitializeOnLoadMethod`라 **에디터에서는 비어 있다** —
        // 에셋을 직접 훑는다.
        var guids = AssetDatabase.FindAssets("t:BuildingData");
        foreach (var guid in guids)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingData>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (data == null || data.Prefab == null) continue;

            // **바닥만 뺀다.** 바닥은 드래그로 칠하는 substrate라 콜라이더가 서로 맞닿아야
            // 한다 — 0.2를 깎으면 타일 사이에 틈이 생기고, 배치 미리보기의 지면 레이캐스트가
            // 그 틈으로 빠져 커서가 헛돈다.
            if (data.IsFloor) continue;

            // **`OccupiesTile`로 거르지 않는다.**
            //
            // 전에는 걸렀는데, 그건 다른 물음이다: `OccupiesTile`은 "길을 막나"이고 콜라이더
            // 크기는 "어디를 눌러야 잡히나"다(길찾기는 `PlacedBuilding`이 등록한 타일 점유를
            // 보지 콜라이더를 안 본다 — `obstacleLayer`는 Wall뿐이다).
            //
            // 그 게이트 때문에 **창고가 2×2인데 콜라이더가 1×1로 남아**, 눈에 보이는 상자의
            // 바깥 절반이 클릭에 안 잡혔다. 문서가 "옛 리사이즈 도구가 모델만 키우고 콜라이더는
            // 두고 갔다"고 적어 둔 그 사고인데, 정작 고치는 도구가 그것만 건너뛰고 있었다.

            string path = AssetDatabase.GetAssetPath(data.Prefab);
            if (string.IsNullOrEmpty(path)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var box = root.GetComponent<BoxCollider>();
                if (box == null) continue;

                // **발자국보다 조금 작게.** 딱 맞추면 이웃 칸 중앙에 선 폰(반경 0.5)이 경계에
                // 정확히 닿아 밀려난다. 모델도 대개 이 정도 여백을 두고 만들어져 있다.
                var want = new Vector3(Mathf.Max(1, data.Size.x) - 0.2f,
                                       box.size.y,
                                       Mathf.Max(1, data.Size.y) - 0.2f);

                if ((box.size - want).sqrMagnitude < 0.0001f) continue;

                Debug.Log($"[콜라이더] {data.BuildingName}: {box.size} → {want} " +
                          $"(크기 {data.Size.x}×{data.Size.y})");
                box.size = want;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                fixedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[콜라이더] 완료 — {fixedCount}개 프리팹 수정. " +
                  "**먼저 `Snap Placed Buildings To Grid`를 돌렸는지 확인할 것** — " +
                  "어긋난 채로 키우면 작업 자리를 뭅니다.");
    }
}
