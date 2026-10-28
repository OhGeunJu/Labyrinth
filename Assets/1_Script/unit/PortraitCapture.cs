using System.Collections.Generic;
using UnityEngine;

// 유닛의 3D 모델을 1회 렌더해 초상화 Sprite로 캐싱하는 런타임 헬퍼.
// 전용 "Portrait" 레이어로 대상 렌더러를 임시 이동시켜 배경/타 오브젝트를 격리한 뒤,
// 단색(투명) 배경 카메라로 RenderTexture에 그려 Texture2D → Sprite로 변환한다.
//
// **초상화는 제 빛을 든다** — 세상의 조명을 안 탄다(2026-08-19).
//
// 전에는 씬의 디렉셔널 라이트가 대상을 비추게 두었다(*"필요 시 추가"*라고 적혀 있던 자리다).
// 낮밤이 시계 글자 말고 아무것도 안 바꾸던 동안에는 그래도 됐지만, 조명이 붙는 순간 **초상화가
// 찍힌 시각을 그대로 물고 있게 된다** — 그리고 이건 한 번 찍고 **영영 캐싱**되므로
// (`UnitBrain.EnsurePortrait`) 어둡게 나오는 것으로 끝나지 않는다:
//
// - 새벽에 처음 로스터를 연 폰은 **그 뒤로 하루 종일 어둡다.**
// - 낮에 합류한 폰은 밝다 → **같은 창에 두 사람이 다른 조명으로 나란히 뜬다.**
//
// 초상화는 *지금 몇 시인지*가 아니라 **그 사람이 누구인지**를 알려 주는 그림이라, 세상의 시각과
// 무관해야 한다(장비 아이콘이 밤에 어두워지지 않는 것과 같은 자리다).
//
// 그래서 캡처하는 동안 **세상의 등을 전부 끄고**(방향광만이 아니다 — 폰은 제자리에서 찍히므로
// 곁의 횃불도 닿는다) **초상화 등 하나와 고정 주변광**으로만 그린다. `_cam.Render()`는 동기라
// 그 사이에 다른 것이 그려지지 않고, 돌려놓는 것도 같은 프레임 안이라 화면에는 안 나타난다.
//
// 주의:
// - "Portrait" 레이어가 프로젝트에 없으면 캡처를 건너뛰고 null 반환(호출측이 Icon 폴백).
// - ReadPixels는 GPU 동기 스톨을 유발하므로 유닛당 1회만, 필요 시점(로스터 표시)에 호출.
public static class PortraitCapture
{
    const string LayerName = "Portrait";
    const int    DefaultSize = 128;
    const float  Fov = 30f;

    // 몸 높이에서 **위에서부터 이만큼만** 담는다 — 머리·어깨·가슴.
    // 키우면 전신 쪽으로, 줄이면 얼굴 쪽으로 간다.
    const float  BustPortion = 0.42f;
    static readonly Color BgColor = new Color(0.12f, 0.12f, 0.14f, 0f); // 투명 배경

    // 초상화 조명 — **여기 적힌 값이 곧 모든 초상화의 조명**이다(세상의 시각과 무관하게 늘 같다).
    // 카메라의 자식이라 폰이 어느 쪽을 보고 있든 **얼굴에 같은 각도로** 떨어진다.
    static readonly Color   KeyColor    = new Color(1f, 0.97f, 0.92f);
    const           float   KeyIntensity = 1.15f;
    static readonly Vector3 KeyAngles   = new Vector3(28f, -32f, 0f);   // 카메라 기준 좌상단
    static readonly Color   FillAmbient = new Color(0.38f, 0.39f, 0.43f);

    static Camera _cam;
    static Light  _key;
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
        go.hideFlags = SelfStanding.HiddenRuntimeObject;
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

        // **카메라의 자식**이라 매 캡처마다 각도를 다시 잡을 필요가 없다 — 카메라를 폰 앞에
        // 세우면 조명이 따라온다.
        var lightGo = new GameObject("~PortraitKeyLight") { hideFlags = SelfStanding.HiddenRuntimeObject };
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localEulerAngles = KeyAngles;

        _key = lightGo.AddComponent<Light>();
        _key.type        = LightType.Directional;
        _key.color       = KeyColor;
        _key.intensity   = KeyIntensity;
        _key.shadows     = LightShadows.None;   // 128px에서 그림자는 얼굴을 더럽히기만 한다
        _key.cullingMask = 1 << ResolveLayer();

        // **캡처하는 동안에만 켠다** — `cullingMask`에 기대지 않는다. URP는 **가장 밝은
        // 방향광을 메인 라이트로 고르는데** 그 선택은 컬링 마스크를 안 본다. 켜 둔 채로 두면
        // 이 등이 세상의 해를 밀어내고 낮밤이 통째로 멎을 수 있다 — 꺼 두면 그 물음 자체가 없다.
        _key.enabled = false;
    }

    // 캡처하는 동안의 세상 — 되돌리려고 들고 있는 것.
    struct WorldLighting
    {
        public List<Light>                        Off;
        public UnityEngine.Rendering.AmbientMode  Mode;
        public Color                              Ambient;
    }

    // **세상의 등을 전부 끈다.** 방향광만 끄면 곁의 점광원(횃불 등)이 남아, 폰이 어디에 서
    // 있었느냐로 초상화가 갈린다 — 그건 시각에 물드는 것과 똑같은 종류의 사고다.
    static WorldLighting BeginPortraitLighting()
    {
        var saved = new WorldLighting
        {
            Off     = new List<Light>(),
            Mode    = RenderSettings.ambientMode,
            Ambient = RenderSettings.ambientLight,
        };

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l == _key || !l.enabled) continue;
            l.enabled = false;
            saved.Off.Add(l);
        }

        // 주변광은 전역이라 카메라별로 못 나눈다 — 잠시 갈아 끼웠다 되돌린다.
        // `DayNightLighting`이 매 프레임 다시 쓰므로 한 프레임을 놓쳐도 저절로 맞춰진다.
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = FillAmbient;

        _key.enabled = true;
        return saved;
    }

    static void EndPortraitLighting(WorldLighting saved)
    {
        _key.enabled = false;

        foreach (var l in saved.Off)
            if (l != null) l.enabled = true;

        RenderSettings.ambientMode  = saved.Mode;
        RenderSettings.ambientLight = saved.Ambient;
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

        // **세상의 조명을 잠시 걷어낸다** — 초상화가 찍힌 시각을 물고 캐싱되지 않게(머리말).
        var world = BeginPortraitLighting();
        _cam.Render();
        EndPortraitLighting(world);

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
