using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **화톳불에 모닥불 모델을 입힌다**(2026-09-30, 사용자: *"화톳불? 모닥불 모델이 이상해 이것도 좋은 거 많던데 찾아봐"*).
//
// 화톳불은 **받침 없이 도형 불꽃 셋(`Tongue`)만 떠 있었다** — 한때 입혔던 얼굴은 도구(`CampfireUpgrader`)와 함께
// 사라졌다. 던전 팩(`djf`)의 모닥불(장작 + 둘레 돌)을 받침으로 얹고, 불꽃은 같은 팩의 불 이펙트로 바꾼다.
//
// ## 켜고 끄는 것은 그대로 `LightSource`가 한다
//
// 팩 모닥불은 **불 · 연기 · 깜빡이는 등을 제 안에 들고 있다**(하위 프리팹 셋). 그대로 두면 **늘 타고 있고**, 등이
// 둘이 되어 끈 불도 밝다(`LightSource`가 모르는 등이다 — *"화면이 안 밝아지면 시야만 도는 거짓말"*의 반대편).
// 그래서 **받침의 불·연기·등은 끄고**, 불과 연기는 **`Flames` 아래에 새로 세운다** — `LightSource.flames`가 그
// 오브젝트를 켜고 끄므로 불을 끄면 이펙트도 같이 꺼진다. 등은 원래 것(`Head/Light`) 하나를 불 자리로 옮긴다.
//
// **다시 돌려도 같다** — 지난번 `[Art]`·`[Fire]`·`[Smoke]`를 먼저 걷는다.
public static class CampfireDresser
{
    const string Prefab = "Assets/3_Prefab/Building/Campfire.prefab";
    const string Pack   = "Assets/assetsss/djf/prefabs";
    const string Base   = Pack + "/PROPS/light/P_PROP_campfire_dungeon_01.prefab";
    const string Fire   = Pack + "/FX/P_FX_fire_v1_dungeon.prefab";
    const string Smoke  = Pack + "/FX/P_FX_steam_dungeon.prefab";

    // **`[Art]`라는 이름을 쓰지 않는다** — `PropDresser`(`1. Dress Buildings`)가 **표에 없는 건물의 `[Art]`를 지우고**
    // 렌더러를 되살린다(`StripUnlisted`). 화톳불은 그 표에 없으므로 그 이름이면 `Dress Buildings` 한 번에 벗겨진다.
    const string ArtName = "[Campfire]", FireName = "[Fire]", SmokeName = "[Smoke]";

    // 받침의 폭(m) — 칸 하나에 들어가게. 팩 소품은 크기가 제각각이라 **재어서 맞춘다.**
    const float Width = 1.0f;

    [MenuItem("DungeonColony/Building/C. Dress Campfire (모닥불 모델)")]
    static void Run()
    {
        var basePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(Base);
        var firePrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(Fire);
        var smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Smoke);
        if (basePrefab == null || firePrefab == null)
        {
            Debug.LogError($"[화톳불] 팩 소품을 못 찾았습니다 — {Base} / {Fire}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var ls = root.GetComponentInChildren<LightSource>(true);
            if (ls == null) { Debug.LogError("[화톳불] `LightSource`가 없습니다."); return; }
            var lsSo   = new SerializedObject(ls);
            var flames = lsSo.FindProperty("flames").objectReferenceValue as GameObject;
            var lamp   = lsSo.FindProperty("lamp").objectReferenceValue as Light;
            if (flames == null) { Debug.LogError("[화톳불] `LightSource.flames`가 비어 있습니다."); return; }

            // ── 지난번 것을 걷는다 ──
            Remove(root.transform, ArtName);
            Remove(root.transform, "[Art]");   // 이름을 바꾸기 전에 입힌 것
            Remove(flames.transform, FireName);
            Remove(flames.transform, SmokeName);

            // ── 받침 ──
            var art = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab, root.transform);
            art.name = ArtName;
            art.transform.localPosition = Vector3.zero;
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale    = Vector3.one;
            foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // 고르기는 루트 콜라이더 하나

            // 받침이 들고 온 불·연기·등은 **끈다**(하위 프리팹 안이라 지울 수 없다). 불의 자리는 크기를 맞춘 뒤에 잰다.
            Transform packFire = null;
            foreach (Transform t in art.GetComponentsInChildren<Transform>(true))
            {
                if (t == art.transform) continue;
                string n = t.name.ToLowerInvariant();
                bool fx = n.Contains("fx_fire") || n.Contains("steam") || n.Contains("pointlight");
                if (!fx) continue;
                if (n.Contains("fx_fire") && packFire == null) packFire = t;
                t.gameObject.SetActive(false);
            }

            // 폭을 칸에 맞추고 **발밑을 피벗에**(피벗이 발밑이라는 규약 — `PawnPlacement`와 같은 규칙).
            var b = RenderBounds(art);
            float wide = Mathf.Max(b.size.x, b.size.z);
            float k = wide > 1e-4f ? Width / wide : 1f;
            art.transform.localScale = Vector3.one * k;
            b = RenderBounds(art);
            art.transform.position += new Vector3(root.transform.position.x - b.center.x,
                                                  root.transform.position.y - b.min.y,
                                                  root.transform.position.z - b.center.z);
            b = RenderBounds(art);

            // 불 자리 — 받침이 알려 준 곳(받침과 같이 줄고 옮겨졌다). 못 찾으면 장작 더미 한가운데 조금 위.
            Vector3 at = packFire != null
                ? packFire.position
                : new Vector3(b.center.x, b.min.y + b.size.y * 0.35f, b.center.z);

            // ── 옛 도형 불꽃은 지운다(우리 것이라 지울 수 있다) ──
            var tongues = new List<GameObject>();
            foreach (Transform t in flames.transform)
                if (t.name.StartsWith("Tongue")) tongues.Add(t.gameObject);
            foreach (var g in tongues) Object.DestroyImmediate(g);

            // ── 켜고 끄는 불 · 연기 ──
            Place(firePrefab, flames.transform, FireName, at, k);
            if (smokePrefab != null) Place(smokePrefab, flames.transform, SmokeName, at, k);

            // 등은 불 자리 조금 위로 — 장작 속에 묻히면 돌 둘레가 그림자로 먹힌다.
            if (lamp != null) lamp.transform.position = at + Vector3.up * 0.6f;

            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[화톳불] 모닥불 모델을 입혔습니다 — 받침 {System.IO.Path.GetFileNameWithoutExtension(Base)}(×{k:F2}) · "
                    + $"불 {System.IO.Path.GetFileNameWithoutExtension(Fire)} · 도형 불꽃 {tongues.Count}개 걷음.\n"
                    + "불을 끄면(우클릭) 불·연기가 같이 꺼지는지 봐 주세요 — `LightSource.flames` 아래에 섰습니다.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Remove(Transform parent, string name)
    {
        var old = parent.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
    }

    static void Place(GameObject prefab, Transform parent, string name, Vector3 at, float scale)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position   = at;
        go.transform.rotation   = prefab.transform.rotation;
        go.transform.localScale = prefab.transform.localScale * scale;
        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;   // 등은 하나 — `LightSource.lamp`
    }

    static Bounds RenderBounds(GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }
}
