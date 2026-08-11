using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 씬에서 `Bed_UI`를 지운다. `DungeonColony → Fix: Remove Bed UI`
//
// 침대 전용 패널은 없앴다 — 하던 일([선택 유닛 배정]/[해제])이 전부 우클릭으로 갔고,
// "이게 어떤 침대이고 누가 누웠나"는 `UnitHP_Panel`이 건물 정보로 띄운다.
//
// **남겨 두면 나쁜 쪽으로 갈렸다**: 죄수 침대에 [선택 유닛 배정]을 누르면 우리 폰이 들어앉고
// 갇힌 사람은 자리에서 밀려나 **갇힘만 남았다**(침대 밖에 선 채로 BT가 멎는다).
// 문이 둘인데 한쪽만 규칙을 알고 있었다.
//
// **이름으로 찾는다** — `BedUI` 스크립트는 이미 지웠으므로 타입으로 물을 수가 없다.
// 스크립트가 빠진 컴포넌트가 남아 있어 씬을 열 때마다 경고가 뜨는데, 이걸 돌리면 사라진다.
//
// 다 쓰면 지운다 — `docs/editor_creators.md` "생성기의 수명".
public static class BedUIRemover
{
    const string ObjectName = "Bed_UI";

    [MenuItem("DungeonColony/Fix: Remove Bed UI")]
    static void Run()
    {
        GameObject found = null;
        foreach (var go in Object.FindObjectsByType<GameObject>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name != ObjectName) continue;
            found = go;
            break;
        }

        if (found == null)
        {
            Debug.Log($"[BedUIRemover] 씬에 '{ObjectName}'이(가) 없습니다 — 이미 지웠거나 이름이 다릅니다.");
            return;
        }

        var scene = found.scene;
        Undo.DestroyObjectImmediate(found);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[BedUIRemover] '{ObjectName}'을(를) 지우고 씬을 저장했습니다.");
    }
}
