using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// DungeonColony → Setup World Map Scene
//
// 월드맵에 필요한 씬 오브젝트/컴포넌트를 **없는 것만 채우고, 참조만 잇는다.**
// 아무것도 지우지 않는다 — MapRoot 아래에 넣어 둔 지형이나 손으로 맞춘 카메라가 살아남아야 하므로
// "지우고 다시 만들기"(구 `Create World Map View`)는 폐기했다.
//
// 구성하는 것:
//   WorldSystems(WorldMap) · SettlementManager · GameModeManager
//   WorldMapView + MapRoot + WorldCamera
//   MapRoot 위의 WorldMapTerrain(지형 한 장) + WorldMapGrid(격자)
//   에셋 배선 — WorldMapData(저작 격자) / WorldTileVisualData(바이옴 데코·마커, 선택)
//
// 여러 번 실행해도 안전하다. 이미 맞게 연결돼 있으면 "확인"만 찍고 넘어간다.
public static class WorldMapSceneSetup
{
    static readonly Vector3 MapRootPos = new Vector3(5000f, 0f, 5000f); // 필드맵과 겹치지 않게 먼 곳
    const float CamPitch = 55f;   // 내려다보는 각도 — 지형 기복이 읽히도록 기울인다

    [MenuItem("DungeonColony/Setup World Map Scene")]
    static void Setup()
    {
        var log = new List<string>();

        // ── 매니저 ───────────────────────────────────────────────────────────
        var wm = Object.FindFirstObjectByType<WorldMap>();
        if (wm == null)
        {
            var go = new GameObject("WorldSystems");
            Undo.RegisterCreatedObjectUndo(go, "Setup World Map");
            wm = go.AddComponent<WorldMap>();
            log.Add("생성: WorldSystems (WorldMap)");
        }
        else log.Add($"확인: WorldMap ({wm.name})");

        var settlements = Object.FindFirstObjectByType<SettlementManager>();
        if (settlements == null)
        {
            var go = new GameObject("SettlementManager");
            Undo.RegisterCreatedObjectUndo(go, "Setup World Map");
            settlements = go.AddComponent<SettlementManager>();
            log.Add("생성: SettlementManager — 시작 정착지 루트는 `Setup Settlement Root`로");
        }
        else log.Add("확인: SettlementManager");

        if (Object.FindFirstObjectByType<GameModeManager>() == null)
        {
            Undo.AddComponent<GameModeManager>(settlements.gameObject);
            log.Add($"추가: GameModeManager → {settlements.name} (카메라/UI는 런타임 자동 탐색)");
        }
        else log.Add("확인: GameModeManager");

        // 채집기지(월드맵 추상 시설)의 소유자. 건설 비용·하루 채굴량을 인스펙터에서 조정하므로
        // 런타임 생성이 아니라 씬에 실물로 있어야 값이 보존된다.
        if (Object.FindFirstObjectByType<OutpostManager>() == null)
        {
            Undo.AddComponent<OutpostManager>(settlements.gameObject);
            log.Add($"추가: OutpostManager → {settlements.name} (건설 비용은 비어 있음 = 무료)");
        }
        else log.Add("확인: OutpostManager");

        // ── 뷰 / 루트 / 카메라 ────────────────────────────────────────────────
        var view = Object.FindFirstObjectByType<WorldMapView>();
        if (view == null)
        {
            var go = new GameObject("WorldMapView");
            Undo.RegisterCreatedObjectUndo(go, "Setup World Map");
            view = go.AddComponent<WorldMapView>();
            log.Add("생성: WorldMapView");
        }
        else log.Add("확인: WorldMapView");

        var viewSO  = new SerializedObject(view);
        var rootProp = viewSO.FindProperty("mapRoot");

        var mapRoot = rootProp.objectReferenceValue as Transform;
        if (mapRoot == null) mapRoot = FindChild(view.transform, "MapRoot");
        if (mapRoot == null)
        {
            var go = new GameObject("MapRoot");
            Undo.RegisterCreatedObjectUndo(go, "Setup World Map");
            go.transform.SetParent(view.transform, false);
            go.transform.position = MapRootPos;
            mapRoot = go.transform;
            log.Add($"생성: MapRoot @ {MapRootPos} — **저작한 지형을 이 아래에 넣으세요**");
        }
        else log.Add($"확인: MapRoot @ {mapRoot.position}");
        rootProp.objectReferenceValue = mapRoot;

        var camProp = viewSO.FindProperty("worldCamera");
        var cam = camProp.objectReferenceValue as Camera;
        if (cam == null) cam = FindChildComponent<Camera>(view.transform, "WorldCamera");
        if (cam == null)
        {
            cam = CreateCamera(view.transform, mapRoot, wm);
            log.Add("생성: WorldCamera (기울인 원근, 기본 비활성)");
        }
        else log.Add("확인: WorldCamera — 위치는 손대지 않았습니다(직접 맞춘 구도 보존)");
        camProp.objectReferenceValue = cam;

        // 월드맵에서도 필드처럼 카메라를 움직이게 — 같은 CameraController를 쓴다(조작감 동일).
        // 모드 전환은 "월드 카메라 오브젝트에 붙은 것 = 월드 측"으로 보고 반대로 켠다(GameModeManager).
        if (cam.GetComponent<CameraController>() == null)
        {
            var ctrl = Undo.AddComponent<CameraController>(cam.gameObject);
            TuneWorldCamera(ctrl);
            log.Add("추가: CameraController → WorldCamera (WASD 이동 / 중클릭 회전 / 휠 줌)");
        }
        else
        {
            // **이미 있으면 값을 건드리지 않는다** — 직접 맞춘 조작감이 우선이다.
            var cur = new SerializedObject(cam.GetComponent<CameraController>());
            log.Add($"확인: WorldCamera의 CameraController — 그대로 둡니다 " +
                    $"(이동 {cur.FindProperty("moveSpeed").floatValue} / " +
                    $"줌 {cur.FindProperty("zoomSpeed").floatValue} / " +
                    $"높이 {cur.FindProperty("minHeight").floatValue}~{cur.FindProperty("maxHeight").floatValue})");
        }

        // 맵 밖으로 못 나가게. 격자 참조는 아래에서 grid가 정해진 뒤 채운다.
        if (cam.GetComponent<WorldCameraBounds>() == null)
        {
            Undo.AddComponent<WorldCameraBounds>(cam.gameObject);
            log.Add("추가: WorldCameraBounds → WorldCamera (맵 범위 밖으로 못 나감)");
        }
        else log.Add("확인: WorldCameraBounds");

        // ── 격자 ─────────────────────────────────────────────────────────────
        var gridProp = viewSO.FindProperty("grid");
        var grid = gridProp.objectReferenceValue as WorldMapGrid;
        if (grid == null) grid = mapRoot.GetComponentInChildren<WorldMapGrid>(true);
        if (grid == null)
        {
            grid = Undo.AddComponent<WorldMapGrid>(mapRoot.gameObject);
            log.Add("추가: WorldMapGrid → MapRoot");
        }
        else log.Add("확인: WorldMapGrid");
        gridProp.objectReferenceValue = grid;
        viewSO.ApplyModifiedProperties();

        var bounds = cam.GetComponent<WorldCameraBounds>();
        if (bounds != null)
        {
            var bSO = new SerializedObject(bounds);
            bSO.FindProperty("grid").objectReferenceValue = grid;
            bSO.ApplyModifiedProperties();
        }

        // 지형 — 맵 전체를 메시 한 장으로 짓는다. 직접 만든 지형을 쓰려면 이 컴포넌트를 빼면 된다.
        if (mapRoot.GetComponent<WorldMapTerrain>() == null)
        {
            Undo.AddComponent<WorldMapTerrain>(mapRoot.gameObject);
            log.Add("추가: WorldMapTerrain → MapRoot (맵 전체 지형 한 장)");
        }
        else log.Add("확인: WorldMapTerrain");

        // ── 에셋 배선 ────────────────────────────────────────────────────────
        WireAsset<WorldMapData>(new SerializedObject(wm), "mapData",
                                "WorldMapData(저작 격자)", log,
                                "`World Map Editor`에서 [새로 만들기]로 만드세요");

        var gridSO = new SerializedObject(grid);
        WireAsset<WorldTileVisualData>(gridSO, "visuals",
                                       "WorldTileVisualData(데코·마커, 선택)", log,
                                       "없어도 지도는 뜹니다 — 나무/바위 프리팹을 얹고 싶을 때만 만드세요");
        gridSO.ApplyModifiedProperties();

        EditorUtility.SetDirty(view);
        EditorUtility.SetDirty(grid);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.activeGameObject = view.gameObject;

        var sb = new StringBuilder("[WorldMapSceneSetup] 월드맵 씬 배선 완료\n");
        foreach (var l in log) sb.Append("  · ").Append(l).Append('\n');
        sb.Append("  씬 저장 필요. Play 후 M키로 월드맵 전환.");
        Debug.Log(sb.ToString());
    }

    // ── 도우미 ───────────────────────────────────────────────────────────────

    // 씬에 그 타입 에셋이 **딱 하나**일 때만 자동 연결한다. 여러 개면 고르지 않고 알려만 준다
    // — 어느 것이 맞는지는 저작자만 안다.
    static void WireAsset<T>(SerializedObject so, string field, string label,
                             List<string> log, string howToMake) where T : Object
    {
        var prop = so.FindProperty(field);
        if (prop == null) return;

        if (prop.objectReferenceValue != null)
        {
            log.Add($"확인: {label} = {prop.objectReferenceValue.name}");
            return;
        }

        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids.Length == 0)
        {
            log.Add($"없음: {label} — {howToMake}");
            return;
        }
        if (guids.Length > 1)
        {
            log.Add($"여러 개: {label}이 {guids.Length}개 — 어느 것을 쓸지 인스펙터에서 직접 고르세요");
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        prop.objectReferenceValue = asset;
        so.ApplyModifiedProperties();
        log.Add($"연결: {label} = {asset.name}");
    }

    // 월드 카메라 조작감의 기준값 — **씬에서 직접 맞춰본 값**을 그대로 박아둔다.
    // 맵 크기로 계산하던 공식은 폐기했다(실제로 굴려보고 고른 값이 계산식보다 낫다).
    // 감이 달라지면 인스펙터에서 고친 뒤 이 표도 같이 갱신할 것 — 카메라를 새로 만들 때 여기서 나온다.
    static void TuneWorldCamera(CameraController ctrl)
    {
        var so = new SerializedObject(ctrl);
        SetFloat(so, "moveSpeed",   22.4f);
        SetFloat(so, "rotateSpeed", 10f);
        SetFloat(so, "minPitch",    30f);
        SetFloat(so, "maxPitch",    90f);
        SetFloat(so, "zoomSpeed",   300f);
        SetFloat(so, "minHeight",   30f);
        SetFloat(so, "maxHeight",   90f);
        so.ApplyModifiedProperties();
    }

    static void SetFloat(SerializedObject so, string field, float value)
    {
        var p = so.FindProperty(field);
        if (p != null) p.floatValue = value;
    }

    static Camera CreateCamera(Transform parent, Transform mapRoot, WorldMap wm)
    {
        var wmSO = new SerializedObject(wm);
        int w = wmSO.FindProperty("width").intValue;
        int h = wmSO.FindProperty("height").intValue;
        float spacing = 4f;   // WorldMapGrid.nodeSpacing 기본값

        float span = Mathf.Max(w, h) * spacing;
        float dist = span * 0.95f;
        var center = mapRoot.position + new Vector3(w * spacing / 2f, 0f, h * spacing / 2f);

        var go = new GameObject("WorldCamera");
        Undo.RegisterCreatedObjectUndo(go, "Setup World Map");
        go.transform.SetParent(parent, false);
        go.transform.position = center + new Vector3(
            0f,
             dist * Mathf.Sin(CamPitch * Mathf.Deg2Rad),
            -dist * Mathf.Cos(CamPitch * Mathf.Deg2Rad));
        go.transform.rotation = Quaternion.Euler(CamPitch, 0f, 0f);

        var cam = go.AddComponent<Camera>();
        cam.orthographic    = false;
        cam.fieldOfView     = 45f;
        cam.nearClipPlane   = 0.3f;
        cam.farClipPlane    = dist * 4f;
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
        cam.enabled         = false;   // 필드 모드로 시작하므로 꺼둔다
        return cam;
    }

    static Transform FindChild(Transform parent, string name)
    {
        foreach (Transform t in parent)
            if (t.name == name) return t;
        return null;
    }

    static T FindChildComponent<T>(Transform parent, string name) where T : Component
    {
        var t = FindChild(parent, name);
        return t != null ? t.GetComponent<T>() : null;
    }
}
