using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **프리팹 루트에 박힌 씬 좌표를 0으로 옮긴다** — 자식은 그대로 둔 채 루트만.
//
// 씬에서 오브젝트를 끌어내 프리팹으로 만들면 그때의 **씬 좌표가 루트에 그대로 박힌다**
// (작업대 셋과 연구대가 `(26.94, 0, 23.62)`, 침대가 `(0, 0, 0.5)`였다).
//
// 게임 안에서는 대개 무해하다 — `Instantiate(prefab, pos, rot)`가 루트를 덮어쓰기 때문이다.
// 문제는 **덮어쓰지 않는 길**이 섞여 있다는 것: `SetParent(transform, false)`는 그 값을 그대로
// 물려받아, 청사진 고스트가 완성품과 어긋나 보였다(2026-08-11). 코드는 고쳤지만 값이 남아 있으면
// **프리팹 편집 화면에서 엉뚱한 자리에 보이고**, 다음에 또 누가 같은 함정에 빠진다.
//
// **`localPosition.y`가 양수면 안 건드린다** — 그건 `BuildModeManager.AuthoredHover`가 읽는
// **저작 부양 높이**다(서브코어 `y = 2`). 지워 버리면 배치 높이가 통째로 무너진다.
// XZ만 0으로 밀고 Y는 그대로 둔다.
//
// **자식을 반대로 밀지 않는다.** 루트를 옮기면 자식의 월드 위치도 같이 옮겨지지만, 프리팹 안에서
// **서로의 상대 위치는 그대로**다 — 게임에서는 어차피 루트가 덮어써지므로 보이는 결과가 같다.
// 자식을 보정하면 오히려 실제 배치 모양이 바뀐다.
public static class PrefabRootZeroer
{
    const string Dir = "Assets/3_Prefab";

    [MenuItem("DungeonColony/Fix: Zero Prefab Root Offsets")]
    static void Run()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { Dir });
        var fixedOnes  = new List<string>();
        var hoverKept  = new List<string>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // **제자리 수정** — 같은 경로에 새로 저장하면 루트 fileID가 바뀌어 그 프리팹을
            // 물고 있는 에셋(`BuildingData.Prefab`·이벤트·무리 데이터)의 참조가 끊긴다.
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) continue;

            try
            {
                Vector3 lp = root.transform.localPosition;
                float   keepY = Mathf.Max(0f, lp.y);   // 저작 부양 높이는 남긴다

                if (Mathf.Approximately(lp.x, 0f) && Mathf.Approximately(lp.z, 0f)
                    && Mathf.Approximately(lp.y, keepY))
                    continue;   // 이미 정리됨

                root.transform.localPosition = new Vector3(0f, keepY, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, path);

                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                fixedOnes.Add($"{name} {Fmt(lp)} → {Fmt(root.transform.localPosition)}");
                if (keepY > 0f) hoverKept.Add($"{name} (부양 {keepY:F2} 유지)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();

        if (fixedOnes.Count == 0) { Debug.Log("[PrefabRoot] 정리할 것이 없습니다."); return; }

        Debug.Log($"[PrefabRoot] {fixedOnes.Count}개 정리:\n  " + string.Join("\n  ", fixedOnes));
        if (hoverKept.Count > 0)
            Debug.Log("[PrefabRoot] 저작 부양 높이는 그대로 뒀습니다:\n  " + string.Join("\n  ", hoverKept));
    }

    static string Fmt(Vector3 v) => $"({v.x:0.##}, {v.y:0.##}, {v.z:0.##})";
}
