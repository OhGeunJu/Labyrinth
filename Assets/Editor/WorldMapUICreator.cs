using UnityEditor;
using UnityEngine;

// DungeonColony → Create World Map View
// 마운트앤블레이드식 실시간 월드맵(같은 씬, 카메라/루트 토글) + 월드 매니저 GameObject 구성.
// 노드 마커/파티 토큰은 런타임 생성 — 여기선 카메라/루트/컴포넌트만 만든다.
public static class WorldMapUICreator
{
    const float NodeSpacing = 4f;          // WorldMapView.nodeSpacing 기본과 일치
    static readonly Vector3 MapRootPos = new Vector3(5000f, 0f, 5000f); // 필드맵과 겹치지 않게 먼 곳

    [MenuItem("DungeonColony/Create World Map View")]
    static void Create()
    {
        int w = 16, h = 16;
        EnsureWorldManagers(ref w, ref h);

        // 기존 것 제거
        var old = Object.FindFirstObjectByType<WorldMapView>();
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var viewGO = new GameObject("WorldMapView");

        // 마커/토큰 부모 (먼 곳)
        var mapRoot = new GameObject("MapRoot").transform;
        mapRoot.SetParent(viewGO.transform, false);
        mapRoot.position = MapRootPos;

        // 탑다운 월드 카메라 (기본 비활성). 지도면 중심 = (w*spacing/2, h*spacing/2)
        float cx = w * NodeSpacing / 2f;
        float cz = h * NodeSpacing / 2f;
        var camGO = new GameObject("WorldCamera");
        camGO.transform.SetParent(viewGO.transform, false);
        camGO.transform.position = MapRootPos + new Vector3(cx, 100f, cz);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(cx, cz) + NodeSpacing * 2f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
        cam.enabled = false;

        // 컴포넌트 연결
        var view = viewGO.AddComponent<WorldMapView>();
        var so = new SerializedObject(view);
        so.FindProperty("worldCamera").objectReferenceValue = cam;
        so.FindProperty("mapRoot").objectReferenceValue = mapRoot;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = viewGO;
        Debug.Log("[WorldMapUICreator] 월드맵 뷰 생성 완료 — Play 후 M키 토글. " +
                  "마커/토큰은 런타임 생성. 노드 클릭=이동, 정착지 정박 중 Enter=진입.");
    }

    // WorldMap / SettlementManager GameObject 보장 + 월드맵 크기 반환(카메라 프레이밍용)
    static void EnsureWorldManagers(ref int width, ref int height)
    {
        var wm = Object.FindFirstObjectByType<WorldMap>();
        if (wm == null)
        {
            var go = new GameObject("WorldSystems");
            wm = go.AddComponent<WorldMap>();
            Debug.Log("[WorldMapUICreator] WorldSystems(WorldMap) 생성");
        }
        var wmSO = new SerializedObject(wm);
        var wp = wmSO.FindProperty("width");
        var hp = wmSO.FindProperty("height");
        if (wp != null) width = wp.intValue;
        if (hp != null) height = hp.intValue;

        if (Object.FindFirstObjectByType<SettlementManager>() == null)
        {
            var go = new GameObject("SettlementManager");
            go.AddComponent<SettlementManager>();
            Debug.Log("[WorldMapUICreator] SettlementManager 생성 — settlements[0]에 시작 정착지 루트 연결 필요");
        }
    }
}
