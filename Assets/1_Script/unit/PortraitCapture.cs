using System.Collections.Generic;
using UnityEngine;

// 유닛의 3D 모델을 1회 렌더해 초상화 Sprite로 캐싱하는 런타임 헬퍼.
// 전용 "Portrait" 레이어로 대상 렌더러를 임시 이동시켜 배경/타 오브젝트를 격리한 뒤,
// 단색(투명) 배경 카메라로 RenderTexture에 그려 Texture2D → Sprite로 변환한다.
//
// 주의:
// - "Portrait" 레이어가 프로젝트에 없으면 캡처를 건너뛰고 null 반환(호출측이 Icon 폴백).
// - ReadPixels는 GPU 동기 스톨을 유발하므로 유닛당 1회만, 필요 시점(로스터 표시)에 호출.
// - 씬의 디렉셔널 라이트가 대상도 비추므로 별도 조명은 두지 않음(필요 시 추가).
public static class PortraitCapture
{
    const string LayerName = "Portrait";
    const int    DefaultSize = 128;
    const float  Fov = 30f;
    static readonly Color BgColor = new Color(0.12f, 0.12f, 0.14f, 0f); // 투명 배경

    static Camera _cam;
    static int    _layer = -2; // -2=미확인, -1=레이어 없음

    static int ResolveLayer()
    {
        if (_layer == -2) _layer = LayerMask.NameToLayer(LayerName);
        return _layer;
    }

    static void EnsureRig()
    {
        if (_cam != null) return;

        var go = new GameObject("~PortraitCaptureCam");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);

        _cam = go.AddComponent<Camera>();
        _cam.enabled       = false; // 수동 Render만 사용
        _cam.clearFlags     = CameraClearFlags.SolidColor;
        _cam.backgroundColor = BgColor;
        _cam.orthographic    = false;
        _cam.fieldOfView     = Fov;
        _cam.nearClipPlane   = 0.05f;
        _cam.farClipPlane    = 100f;
        _cam.cullingMask     = 1 << ResolveLayer();
    }

    // 대상 GameObject의 모델을 렌더해 초상화 Sprite를 만든다. 실패 시 null.
    public static Sprite Capture(GameObject target, int size = DefaultSize)
    {
        if (target == null) return null;
        if (ResolveLayer() < 0)
        {
            Debug.LogWarning($"[PortraitCapture] '{LayerName}' 레이어가 없어 초상화를 건너뜁니다. PawnRoster 에디터 스크립트로 레이어를 생성하세요.");
            return null;
        }

        var renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0) return null;

        EnsureRig();

        // 1) 대상 렌더러 레이어 임시 변경 + 월드 바운드 계산
        var saved = new Dictionary<GameObject, int>(renderers.Length);
        Bounds bounds = default;
        bool hasBounds = false;
        foreach (var r in renderers)
        {
            saved[r.gameObject] = r.gameObject.layer;
            r.gameObject.layer  = _layer;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else            bounds.Encapsulate(r.bounds);
        }

        // 2) 카메라 배치 — 머리 쪽(상단)을 향하도록 포커스를 위로 편향
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        Vector3 focus  = new Vector3(center.x, bounds.max.y - extents.y * 0.45f, center.z);

        float fitExtent = Mathf.Max(extents.y, extents.x) * 1.1f; // 약간의 여백
        float dist = fitExtent / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);

        Vector3 fwd = target.transform.forward;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        _cam.transform.position = focus + fwd * dist;
        _cam.transform.LookAt(focus);

        // 3) 렌더 → 픽셀 읽기
        var rt = RenderTexture.GetTemporary(size, size, 16, RenderTextureFormat.ARGB32);
        var prevActive = RenderTexture.active;
        var prevTarget = _cam.targetTexture;

        _cam.targetTexture = rt;
        _cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();

        _cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);

        // 4) 레이어 복원
        foreach (var kv in saved)
            if (kv.Key != null) kv.Key.layer = kv.Value;

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
