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

    // 몸 높이에서 **위에서부터 이만큼만** 담는다 — 머리·어깨·가슴.
    // 키우면 전신 쪽으로, 줄이면 얼굴 쪽으로 간다.
    const float  BustPortion = 0.42f;
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

        // 2) 카메라 배치 — **상반신만 담는다**(머리·어깨·가슴).
        //
        // 전에는 몸 전체가 들어가게 잡아서(`max(extents.y, extents.x)`) 128px 안에 사람이
        // 통째로 들어갔다 — 얼굴이 몇 픽셀이라 **누가 누구인지 구별이 안 됐다.** 초상화는
        // 개체를 알아보라고 있는 것이므로 얼굴이 커야 한다.
        //
        // 위에서 `BustPortion`만큼만 담고, 가로는 **어깨 폭**(`extents.x`)에 맞춘다 —
        // 둘 중 큰 쪽으로 맞춰야 어느 방향으로도 안 잘린다. 네발짐승은 앞을 보고 찍히므로
        // (카메라가 `forward`에 선다) 같은 계산이 그대로 얼굴 클로즈업이 된다.
        Vector3 center  = bounds.center;
        Vector3 extents = bounds.extents;

        float halfH = bounds.size.y * BustPortion * 0.5f;
        Vector3 focus = new Vector3(center.x, bounds.max.y - halfH, center.z);

        float fitExtent = Mathf.Max(halfH, extents.x) * 1.15f;   // 약간의 여백
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
