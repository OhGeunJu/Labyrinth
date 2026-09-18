using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **몇 칸을 차지하나 — 저작이다**
// (2026-09-18, 사용자: *"무덤을 크기를 그냥 3*1로 키워주고 침대도 3*1로"*).
//
// `BuildingData.Size`는 **인스펙터에서 한 줄**이지만, 밖에서 `.asset`을 고치면 유니티가
// 저장 한 번에 되돌린다(이 저장소의 규칙). 그래서 **도구가 쓴다.**
//
// **비용 도구(`F`)와 가르는 이유**: 저쪽은 *무엇으로 짓나*이고 여기는 *얼마나 넓나*다.
// 한 표에 섞으면 크기를 고치려고 비용 표를 열게 되고, 비용을 손보다 크기가 딸려 간다.
//
// **크기를 바꾸면 얼굴도 다시 입혀야 한다** — `PropDresser.Fit`이 발자국에 맞춰 줄이므로,
// 이 도구를 돌린 뒤에는 `1. Dress Buildings`를 한 번 더 돌린다(로그가 그렇게 말한다).
public static class BuildingSizeFix
{
    const string Dir = "Assets/Resources/Data/Buildings";

    // 에셋 이름 → 칸 수. **표에 없는 것은 안 건드린다.**
    static readonly Dictionary<string, Vector2Int> Table = new()
    {
        // 무덤과 침대는 **사람이 눕는 자리**다 — 1×2로는 관도 침대도 칸 안에 우겨넣은
        // 꼴이라, 모델을 키우면 발자국을 넘고 안 키우면 조그맣게 놓인다.
        ["Grave"]      = new Vector2Int(3, 1),
        ["Bed"]        = new Vector2Int(3, 1),
        ["QualityBed"] = new Vector2Int(3, 1),
        ["PrisonBed"]  = new Vector2Int(3, 1),
    };

    // **콜라이더는 발자국을 따라간다**(2026-09-18, 사용자가 화면을 보내 줬다: 침대를 3×1로
    // 키웠는데 **집히는 데는 가운데 한 줌**이었다).
    //
    // 크기와 점유는 **다른 축이다** — 콜라이더는 *어디를 눌러야 잡히나*이고 `OccupiesTile`은
    // *길을 막나*다. 그래서 칸 수를 고치면 **콜라이더만 뒤처진다**: 데이터는 3칸인데 손은
    // 1칸에만 닿는다.
    //
    // **`Size − 0.2`가 이 저장소의 규칙이다** — 꽉 채우면 옆 칸 건물과 모서리가 겹쳐 엉뚱한
    // 것이 잡힌다. **`OccupiesTile`이 꺼진 것은 트리거로 둔다**(침대가 그렇다 — 지도는 빈
    // 칸이라 하는데 몸이 부딪히면 폰이 처박힌다).
    static void FitCollider(BuildingData data, Vector2Int size, List<string> notes)
    {
        if (data.Prefab == null) return;

        string path = AssetDatabase.GetAssetPath(data.Prefab);
        if (string.IsNullOrEmpty(path)) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var box = root.GetComponent<BoxCollider>();
            if (box == null) { notes.Add($"{data.BuildingName}: 루트에 BoxCollider가 없어 건너뜀"); return; }

            float x = Mathf.Max(0.2f, size.x - 0.2f);
            float z = Mathf.Max(0.2f, size.y - 0.2f);

            box.size   = new Vector3(x, box.size.y, z);
            box.center = new Vector3(0f, box.center.y, 0f);
            if (!data.OccupiesTile) box.isTrigger = true;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            notes.Add($"{data.BuildingName}: 콜라이더 {x:0.#}×{z:0.#}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("DungeonColony/Building/9. Fix Building Sizes (칸 수)")]
    static void Run()
    {
        int n = 0;
        var notes = new List<string>();

        foreach (var kv in Table)
        {
            var b = AssetDatabase.LoadAssetAtPath<BuildingData>($"{Dir}/{kv.Key}.asset");
            if (b == null) { Debug.LogWarning($"[칸 수] 건물 에셋이 없습니다 — {kv.Key}"); continue; }

            if (b.Size != kv.Value)
            {
                Undo.RecordObject(b, "칸 수");
                notes.Add($"{b.BuildingName} {b.Size.x}×{b.Size.y} → {kv.Value.x}×{kv.Value.y}");
                b.Size = kv.Value;
                EditorUtility.SetDirty(b);
                n++;
            }

            // **크기가 이미 맞아도 콜라이더는 맞춘다** — 여기서 같이 건너뛰면, 칸 수를
            // 먼저 고쳐 둔 다음 콜라이더 손질을 넣은 날 **영영 안 고쳐진다**(실제로 그랬다).
            FitCollider(b, kv.Value, notes);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(notes.Count == 0
            ? "[칸 수] 이미 다 맞습니다."
            : $"[칸 수] 칸 {n}개 · 콜라이더 포함: {string.Join(" · ", notes)}\n"
            + "**`1. Dress Buildings`를 다시 돌리세요** — 얼굴이 발자국에 맞춰 줄어 있습니다.");
    }
}
