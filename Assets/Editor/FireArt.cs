using UnityEditor;
using UnityEngine;

// **불이 어떻게 생겼나 — 답은 여기 한 벌이다**(2026-09-05).
//
// 벽걸이 횃불을 만들면서 불꽃과 등을 짓는 코드를 `TorchCreator` 안에 적었는데, 화톳불을
// 같은 눈높이로 올리려니 **그 스무 줄을 통째로 베끼게 됐다.** 베끼면 갈린다 — 한쪽 불꽃만
// 색을 바꾸거나 흔들림을 손보면 **같은 세계의 두 불이 다르게 탄다.**
//
// 그래서 불을 쓰는 도구가 공유한다. 갈리는 것은 **크기와 갈래 수**뿐이다:
// 횃불은 자루 끝의 한 갈래, 화톳불은 장작 위의 세 갈래.
//
// ## 불꽃은 `Flames` 한 부모 아래에 둔다
//
// `LightSource`가 흔들 때 **그 오브젝트의 스케일을 만지기 때문**이다(`flicker`). 갈래마다
// 따로 물리면 흔드는 쪽이 하나만 잡아서 **한 갈래만 춤춘다.**
public static class FireArt
{
    const string MatDir = "Assets/2_Material";
    const string Pack   = "Assets/assetsss/Fantastic Dungeon Pack/prefabs/PROPS";

    // **불꽃 한 벌.** 돌려주는 것이 `LightSource.flames`에 물릴 부모다.
    //
    // `size`는 불꽃 하나의 키(m). 갈래가 여럿이면 **가운데가 크고 곁불은 작다** — 같은 크기로
    // 나란히 세우면 불이 아니라 **촛불 세 자루**로 보인다.
    public static GameObject Flames(Transform parent, float size, int tongues)
    {
        var root = new GameObject("Flames");
        root.transform.SetParent(parent, false);

        var hot  = Mat("Mdl_Flame");   // 노란 심지
        var warm = Mat("Mdl_Ember");   // 주황 겉불

        tongues = Mathf.Max(1, tongues);
        for (int i = 0; i < tongues; i++)
        {
            // 가운데(0)가 가장 크고 높다. 곁불은 옆으로 비껴 앉는다.
            bool center = i == 0;
            float scale = center ? 1f : 0.62f;
            float angle = center ? 0f : (i - 0.5f) * Mathf.PI;   // 곁불 둘을 마주 보게
            float off   = center ? 0f : size * 0.42f;

            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            t.name = center ? "Tongue" : $"Tongue_{i}";
            Object.DestroyImmediate(t.GetComponent<Collider>());
            t.transform.SetParent(root.transform, false);
            t.transform.localPosition = new Vector3(Mathf.Cos(angle) * off,
                                                    size * scale * 0.5f,
                                                    Mathf.Sin(angle) * off);
            // **위로 길쭉하다** — 정구면이면 불이 아니라 구슬이다.
            t.transform.localScale = new Vector3(size * scale * 0.62f,
                                                 size * scale,
                                                 size * scale * 0.62f);

            var mat = center ? hot : warm;
            if (mat != null) t.GetComponent<Renderer>().sharedMaterial = mat;
        }

        return root;
    }

    // **등 하나.** 색은 어디서나 같다 — 불빛 색이 물건마다 다르면 굴 안에서 **어느 불이
    // 어느 불인지**가 색으로 읽혀 버린다(그건 정보가 아니라 소음이다).
    public static Light Lamp(Transform parent, float intensity, float range)
    {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);

        var lamp = go.AddComponent<Light>();
        lamp.type      = LightType.Point;
        lamp.color     = new Color(1f, 0.62f, 0.28f);
        lamp.intensity = intensity;
        lamp.range     = range;
        // **그림자를 안 던진다** — 굴마다 수십 개가 설 물건이라 켜면 실시간 그림자가 그만큼 선다.
        lamp.shadows   = LightShadows.None;
        return lamp;
    }

    // 팩 소품 하나를 자식으로. **프리팹 인스턴스로 넣는다** — 팩이 갱신되면 따라온다.
    //
    // **콜라이더는 떼어 낸다** — 자식에 붙어 있으면 클릭 레이가 자식에 걸려 루트의
    // `IWorldClickable`이 빈손이 되고, 트리거가 아닌 것이 섞이면 폰이 걸린다.
    public static GameObject Prop(Transform parent, string name, string relPath, string tag)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "/" + relPath + ".prefab");
        if (src == null)
        {
            Debug.LogWarning($"{tag} 소품을 못 찾았습니다 — {relPath}. 그 자리가 빈 채로 나옵니다.");
            var empty = new GameObject(name);
            empty.transform.SetParent(parent, false);
            return empty;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        go.name = name;
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go;
    }

    // 모형 꼭대기 높이(m). **경계를 재는 것이지 피벗을 믿는 것이 아니다** — 팩마다 피벗이
    // 밑동일 수도 한복판일 수도 있다. 만들 때 모든 것이 원점에 있으므로 월드 y가 곧 로컬 y다.
    public static float TopOf(GameObject go, float fallback)
    {
        if (go == null) return fallback;

        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return fallback;

        float top = float.NegativeInfinity;
        foreach (var r in renderers) top = Mathf.Max(top, r.bounds.max.y);

        return top > 0.05f ? top : fallback;
    }

    // private [SerializeField]는 밖에서 못 만지므로 직렬화 창구로 넣는다.
    public static void Set(Component c, System.Action<SerializedObject> write)
    {
        var so = new SerializedObject(c);
        write(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material Mat(string name)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/" + name + ".mat");
        if (m == null) Debug.LogWarning("[불] 재질을 못 찾았습니다 — " + name);
        return m;
    }
}
