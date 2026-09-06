using UnityEditor;
using UnityEngine;

// **화톳불에 얼굴을 입힌다**(2026-09-05, 사용자: *"화톳불에비해 벽걸이횃불은 너무 잘만든 거
// 같아 화톳불도 업그레이드 해줘"*).
//
// 맞는 말이었다. 화톳불은 **기본 도형**이다 — 돌 여덟 개를 둥글게 놓고 통나무 넷을 얹은
// 것이라, 팩 모델을 쓴 횃불 옆에 서면 **같은 세계의 물건으로 안 보인다.** 불의 눈금(반경 8 ·
// 세기 1)은 그대로 두고 **보이는 것만** 갈아 끼운다.
//
// ## 프리팹을 새로 짓지 않는다 — 루트를 물려받는다
//
// `LoadPrefabContents` → 고치기 → 같은 경로에 저장. **`SaveAsPrefabAsset`으로 새로 만들면
// 루트의 fileID가 바뀌고**, 씬에 놓인 인스턴스가 제 오버라이드를 잃는다(*"Prefab mismatch …
// data will be lost"*). 자식은 없어져도 된다 — 평범한 편집이라 그 오버라이드만 조용히
// 버려진다. **위험한 것은 루트뿐이다.**
//
// ## `PropDresser`의 화톳불 줄을 걷어냈다
//
// 그쪽 표에도 `Campfire → light/P_PROP_campfire_dungeon_01`이 있었다(한 번도 안 돌린 채로).
// 남겨 두면 **같은 물건의 생김새에 답이 둘**이 되고, 둘 다 돌리면 `[Art]`가 두 겹으로 선다.
// 저쪽은 *"옛 메시를 끄고 소품을 얹는다"*라 불꽃까지 같이 꺼지는데, 화톳불에서 불꽃은
// 장식이 아니라 **켜졌는지를 보여 주는 유일한 표시**다(`LightSource.flames`).
//
// **다시 돌려도 안전하다** — 옛 자식을 지우고 다시 짓는다.
public static class CampfireUpgrader
{
    const string Path = "Assets/3_Prefab/Building/Campfire.prefab";
    const string Tag  = "[화톳불]";

    [MenuItem("DungeonColony/Building/5. Upgrade Campfire (화톳불 모델)")]
    static void Run()
    {
        var contents = PrefabUtility.LoadPrefabContents(Path);
        if (contents == null) { Debug.LogError(Tag + " Campfire.prefab 을 못 찾았습니다."); return; }

        // ── 옛 도형을 걷어낸다 ───────────────────────────────────────────
        //
        // 돌·통나무·잿더미·불꽃·등이 전부 자식이다. **끄지 않고 지운다** — `PropDresser`가
        // 끄기만 하는 것은 *콜라이더가 같은 오브젝트에 붙어 있어서*인데, 여기 콜라이더는
        // 루트에 있다(클릭 상자). 남겨 두면 새 모델 속에 옛 돌덩이가 박힌다.
        for (int i = contents.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(contents.transform.GetChild(i).gameObject);

        // ── 새 모델 ──────────────────────────────────────────────────────
        var art = FireArt.Prop(contents.transform, "[Art]", "light/P_PROP_campfire_dungeon_01", Tag);

        // **불꽃은 장작 위에서 탄다.** 소품 꼭대기를 재서 그 위에 앉힌다 — 상수로 박으면
        // 팩을 갈아 끼웠을 때 불이 장작 속에서 타거나 허공에 뜬다.
        float logTop = FireArt.TopOf(art, fallback: 0.3f);

        var head = new GameObject("Head").transform;
        head.SetParent(contents.transform, false);
        head.localPosition = new Vector3(0f, logTop * 0.75f, 0f);

        // **세 갈래다 — 횃불은 한 갈래다.** 그것이 두 불이 갈리는 자리다(넓은 불 ↔ 작은 불).
        var flames = FireArt.Flames(head, size: 0.42f, tongues: 3);

        // 등은 불꽃 언저리에서. **눈금은 그대로다**(세기 3.2 · 사거리 9) — 이 작업은 보이는
        // 것만 바꾸는 것이고, 밝기를 같이 건드리면 밤 밸런스가 조용히 따라 움직인다.
        var lamp = FireArt.Lamp(head, intensity: 3.2f, range: 9f);
        lamp.transform.localPosition = new Vector3(0f, 0.25f, 0f);

        // ── 다시 물린다 ──────────────────────────────────────────────────
        //
        // **옛 자식을 지웠으므로 `lamp`·`flames` 참조가 끊겼다.** 안 물리면 불이 **안 켜지고
        // 안 흔들린다** — 그런데 판정(`LightSource.BrightestAt`)은 멀쩡히 돌아서, 화면에는
        // *"불도 없는데 둘레가 밝다"*로만 보인다.
        var light = contents.GetComponent<LightSource>();
        if (light == null) Debug.LogError(Tag + " 루트에 LightSource가 없습니다 — 배선을 못 물렸습니다.");
        else FireArt.Set(light, so =>
        {
            so.FindProperty("lamp").objectReferenceValue   = lamp;
            so.FindProperty("flames").objectReferenceValue = flames;
        });

        PrefabUtility.SaveAsPrefabAsset(contents, Path);
        PrefabUtility.UnloadPrefabContents(contents);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"{Tag} 모델을 갈아 끼웠습니다 — 장작 꼭대기 {logTop:F2}m, 불꽃 세 갈래. "
                + "불의 눈금(반경 8 · 세기 1)과 클릭 상자는 그대로입니다.");
    }
}
