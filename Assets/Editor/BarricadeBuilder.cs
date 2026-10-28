using UnityEditor;
using UnityEngine;

// **바리케이드는 소품 하나가 아니라 조립이다**
// (2026-09-18, 사용자: *"내가 원하건 네가 그 원래 바리게이트의 모양으로 제작하는거야
//  지금 모델을 써서"* · 그 전 시도들에 *"내가 말한게 아니잖아"*).
//
// ## 왜 얹는 것으로는 안 됐나
//
// 팩에는 **바리케이드가 없다.** `spikepole_wall2`는 말뚝 함정이고 `cave_supports_small_up`은
// 천장 버팀목이며 `cross1`은 **장대 하나**다 — 셋 다 *길을 막는 물건*으로 안 읽혔다.
// 소품 하나를 얹는 방식(`PropDresser`)은 **없는 물건을 만들어 내지는 못한다.**
//
// ## 원래 프리팹이 설계도다
//
// 직접 지은 `Barricade`는 `BaseF`/`BaseB`(앞뒤 받침)에 `StakeF`/`StakeB`를 **X자로 엇갈려**
// 박고 `Tie`로 묶은 것 — 대전차 방벽의 그 모양이다. **그 배치를 그대로 두고 막대만 팩
// 것으로 갈아 끼운다**: 모양은 지키고 결만 바뀐다.
//
// 그래서 여기서 하는 일은 *새로 디자인하는 것*이 아니라 **좌표를 베끼는 것**이다 —
// 원래 자식들의 `localPosition`/`localRotation`/길이를 읽어, 같은 자리에 팩 장대를 세운다.
//
// **다시 돌려도 된다** — 먼저 지난번 `[Art]`를 걷는다.
public static class BarricadeBuilder
{
    const string Cave    = "Assets/assetsss/3DForge/Cave Adventure kit/Prefabs";
    const string Prefab  = "Assets/3_Prefab/Building/Barricade.prefab";
    const string ArtName = "[Art]";

    // 막대 하나. **끝이 뾰족한 것**이라 방벽의 결이 산다.
    const string PoleProp = Cave + "/Wooden/Poles & posts/pole_spike.prefab";

    // 받침으로 쓰는 통나무(가로로 눕힌다).
    const string BeamProp = Cave + "/Wooden/Poles & posts/pole.prefab";

    [MenuItem("DungeonColony/Building/8. Build Barricade (나무 방벽 조립)")]
    static void Run()
    {
        var pole = AssetDatabase.LoadAssetAtPath<GameObject>(PoleProp);
        var beam = AssetDatabase.LoadAssetAtPath<GameObject>(BeamProp);
        if (pole == null || beam == null)
        {
            Debug.LogError($"[방벽] 소품을 못 찾았습니다 — {PoleProp} / {BeamProp}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var old = root.transform.Find(ArtName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // **옛 메시는 끄기만 한다** — 지우면 같은 오브젝트의 콜라이더까지 사라진다.
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;

            var art = new GameObject(ArtName);
            art.transform.SetParent(root.transform, false);

            // ── 원래 프리팹에서 읽은 좌표 (2026-09-18) ──────────────────────
            //
            // 앞서 두 번은 **값을 짐작해서** 틀렸다. 실제로 읽어 보니 이렇다:
            //
            //   StakeF{i} pos=(x, 0.62, 0) rot=(+28°, 0, 0) scale=(0.11, 0.7, 0.11)
            //   StakeB{i} pos=(x, 0.62, 0) rot=(-28°, 0, 0)      x = -0.76 … +0.76, 0.38 간격
            //   BaseF/B   pos=(0, 0.09, ∓0.34) 눕힌 통나무
            //
            // **한 쌍이 *같은 자리*에 있다**(z가 둘 다 0) — 가운데서 교차하고 **위아래로**
            // 벌어지는 것이 X자의 정체다. 지난번에는 앞뒤로 0.68m 벌려 놓아서 *V자 둘*이
            // 됐고, 가운데가 비어 **하늘에 뜬 기둥**으로 보였다.
            //
            // **쌍은 다섯이다**(셋이 아니라) — 촘촘해야 *막는 것*으로 읽힌다.
            const float Lean     = 28f;    // 원래 rot.x = 0.241922 → 28°
            const float StakeY   = 0.62f;  // 교차점 높이
            const float StakeLen = 1.40f;  // scale.y 0.7 × 기본 높이 2
            const float BaseY    = 0.09f;
            const float BaseZ    = 0.34f;
            const float BaseLen  = 1.92f;  // scale.y 0.96 × 2

            float[] xs = { -0.76f, -0.38f, 0f, 0.38f, 0.76f };

            foreach (float x in xs)
            {
                // **같은 자리에 둘, 각도만 반대** — 그래야 교차한다.
                Stake(art.transform, pole, new Vector3(x, StakeY, 0f),  Lean, StakeLen);
                Stake(art.transform, pole, new Vector3(x, StakeY, 0f), -Lean, StakeLen);
            }

            // 바닥에 눕힌 받침 통나무 둘 — **이것이 있어야 말뚝이 땅에 박힌 것으로 보인다**
            // (없으면 창 다섯 쌍이 공중에 교차해 있는 그림이다).
            Beam(art.transform, beam, new Vector3(0f, BaseY, -BaseZ), BaseLen);
            Beam(art.transform, beam, new Vector3(0f, BaseY,  BaseZ), BaseLen);

            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[방벽] 말뚝 {xs.Length * 2}개와 받침 둘로 조립했습니다.\n"
                    + "**좌표는 원래 프리팹(`StakeF`/`StakeB`/`BaseF`)에서 읽어 베꼈다** — "
                    + "모양이 안 맞으면 `Lean`·`StakeY`·`xs`를 고칠 것.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 막대 하나를 **길이에 맞춰** 세운다. 원래 좌표가 이미 바닥·교차점을 맞춰 두었으므로
    // **자리는 손대지 않는다** — 지난번에 밑동을 따로 보정했다가 가운데 것만 떴다.
    static void Stake(Transform parent, GameObject prop, Vector3 at, float lean, float length)
    {
        var go = Put(parent, prop, at, Quaternion.Euler(lean, 0f, 0f), length);
        if (go != null) go.name = "Stake";
    }

    // 바닥에 눕힌 받침 통나무. **Z축 90도로 눕혀 가로지른다**(원래 `BaseF`/`BaseB`가 그렇다).
    static void Beam(Transform parent, GameObject prop, Vector3 at, float length)
    {
        var go = Put(parent, prop, at, Quaternion.Euler(0f, 0f, 90f), length);
        if (go != null) go.name = "Base";
    }

    // **길이를 맞춰 놓는다** — 팩 막대는 제 크기가 있어서, 안 맞추면 방벽보다 길고 굵다.
    // 긴 축(회전 전 기준 Y)을 재서 `length`가 되도록 **균등하게** 줄인다.
    static GameObject Put(Transform parent, GameObject prop, Vector3 at, Quaternion rot, float length)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prop, parent);
        if (go == null) return null;

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // 안 돌린 상태에서 재야 **막대의 제 길이**가 나온다(돌린 뒤 재면 축이 섞인다).
        if (Bounds(go, out var b))
        {
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.0001f) go.transform.localScale = Vector3.one * (length / longest);
        }

        go.transform.localRotation = rot;
        go.transform.localPosition = at;

        // 피벗이 가운데가 아닐 수 있다 — 재서 **`at`이 한복판이 되게** 민다.
        if (Bounds(go, out b))
            go.transform.localPosition += at - b.center;

        return go;
    }

    // 조립한 덩어리를 발자국에 맞춘다 — **균등하게**(축마다 따로 늘이면 장대가 찌그러진다).
    static void Fit(GameObject art, float width)
    {
        if (!Bounds(art, out var b) || b.size.x <= 0.0001f) return;

        // 원래 배치가 폭 1.52(= -0.76…0.76)라 2칸(폭 2)에 그대로 들어간다 — 그래도
        // 발자국이 달라질 수 있으니 재서 맞춘다.
        float s = width * 0.92f / b.size.x;
        art.transform.localScale = Vector3.one * s;

        if (!Bounds(art, out b)) return;
        art.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    static bool Bounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
