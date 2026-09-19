using UnityEngine;

// **폰 미리보기 무대 — 모습만 있는 사본을 세상 밖에 세우고 카메라로 찍는다**(2026-09-20, 캐릭터 생성 화면).
//
// 진짜 폰을 세우면 뇌가 돌고(`UnitBrain`) 내 폰 목록에 잡히고(`Faction`) 발밑에 땅이 없어 떨어진다. 그래서
// **스크립트를 떼어 낸 사본**을 쓴다 — 떼는 규칙은 건설 미리보기와 **같은 함수**다(`BuildModeManager.StripScripts`).
// 남기는 것은 모습을 정하는 것뿐이다: `PawnLook`(머리형·머리카락·귀·코) · `ChibiFacePart`(눈·입 판) ·
// `PawnOutlineId`(외곽선 번호).
//
// ## 어기면 조용히 깨지는 것
// - **꺼진 부모 아래에서 만든다** — 켠 채로 만들면 떼기 **전에** `Awake`가 돌아 그 한 번에 목록에 등록된다
//   (`ResourceNode.SpawnWild`가 먼저 겪은 함정이다).
// - **레이어를 통째로 옮긴다**(`Portrait`) — 카메라가 그 레이어만 보게 해야 세상의 바위·폰이 안 찍힌다.
//   초상화와 같은 레이어를 쓴다(`PortraitCapture`) — 둘 다 *"폰 하나만 격리해서 찍는다"*는 같은 물음이다.
// - **콜라이더는 끈다** — 남으면 커서가 세상 밖의 이 사본을 짚을 수 있다.
public class PawnPreviewStage : MonoBehaviour
{
    const string LayerName = "Portrait";
    static readonly Vector3 StagePos = new(0f, -2000f, 0f);   // 세상 밖 — 바위·지형이 안 닿는 곳

    Camera        _cam;
    Light         _light;
    RenderTexture _rt;
    GameObject    _pawn;
    // **앞모습이 기본이다** — 200도는 뒤통수였다(2026-09-20 스크린샷). 폰은 +Z를 보고 카메라도 +Z에서 보므로 0이 정면이고,
    // 살짝 틀어 3/4로 둔다. **끌면 돌아간다**(사용자: *"캐릭 화면 안돌아가"*).
    float         _yaw = 20f;

    // **카메라는 몸에 맞춘다**(2026-09-20, 사용자: *"캐릭이 좀 더 가운데 있어야해 지금은 좀 위야"* · *"카메라로 확대 축소"*).
    // 거리를 상수로 두었더니 칸이 좁고 길쭉해 **양손이 잘리고 머리가 위로 붙었다** — 칸의 비율이 바뀌면 상수는 늘 틀린다.
    // 켜진 렌더러의 경계를 재서 **한가운데를 겨누고 가로·세로 중 빡빡한 쪽이 들어오는 거리**를 낸다. 휠은 그 거리에 배율을 곱한다.
    Vector3 _center = new(0f, 0.9f, 0f);
    float   _fitDist = 4.2f;
    float   _zoom = 1f;
    const float ZoomMin = 0.45f, ZoomMax = 1.6f;

    public RenderTexture Texture => _rt;
    public PawnLook      Look    { get; private set; }

    public static PawnPreviewStage Create(int width, int height)
    {
        var go    = new GameObject("PawnPreviewStage");
        go.transform.position = StagePos;
        var stage = go.AddComponent<PawnPreviewStage>();
        stage.Build(width, height);
        return stage;
    }

    void Build(int width, int height)
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0) Debug.LogWarning($"[미리보기] `{LayerName}` 레이어가 없다 — 세상의 것이 같이 찍힐 수 있다.");

        _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "PawnPreview" };

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(transform, false);
        _cam = camGo.AddComponent<Camera>();
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
        _cam.fieldOfView     = 26f;
        _cam.nearClipPlane   = 0.05f;
        _cam.farClipPlane    = 20f;
        _cam.targetTexture   = _rt;
        if (layer >= 0) _cam.cullingMask = 1 << layer;
        PlaceCamera();

        var lightGo = new GameObject("PreviewLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localRotation = Quaternion.Euler(35f, 160f, 0f);
        _light = lightGo.AddComponent<Light>();
        _light.type      = LightType.Directional;
        _light.intensity = 1.1f;
        _light.shadows   = LightShadows.None;
        if (layer >= 0) _light.cullingMask = 1 << layer;
    }

    // 이 몸 프리팹으로 사본을 새로 세운다(몸을 갈면 프리팹이 바뀐다 — 관절이 몸마다 구워져 있다).
    public void Show(GameObject bodyPrefab)
    {
        if (_pawn != null) Destroy(_pawn);
        Look = null;
        if (bodyPrefab == null) return;

        // 꺼진 부모 아래에서 만든다 — `Awake`가 떼기 전에 돌지 않게.
        var hold = new GameObject("hold");
        hold.SetActive(false);
        _pawn = Instantiate(bodyPrefab, hold.transform);
        BuildModeManager.StripScripts(_pawn, b => b is PawnLook || b is ChibiFacePart || b is PawnOutlineId
                                                  || b is PawnRig || b is PawnBody);
        foreach (var c in _pawn.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        int layer = LayerMask.NameToLayer(LayerName);
        if (layer >= 0) foreach (var t in _pawn.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        _pawn.transform.SetParent(transform, false);
        _pawn.transform.localPosition = Vector3.zero;
        _pawn.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        Destroy(hold);

        Look = _pawn.GetComponent<PawnLook>();
    }

    // 몸을 잰다 — 얼굴·머리카락을 입힌 **뒤에** 부를 것(켜진 렌더러가 바뀌면 경계도 바뀐다).
    //
    // **체형·머리를 바꿀 때마다 부르지 않는다**(2026-09-20, 사용자: *"왜 체형마다 카메라 거리가 달라"*) — 몸마다 다시 재면
    // 뚱뚱한 몸일수록 카메라가 물러나 **모든 체형이 같은 크기로 보인다**(고르는 이유가 화면에서 사라진다). 부르는 쪽이
    // 종족이 바뀔 때만 부른다. 폭은 **키에서 낸 여유**와 잰 폭 중 큰 쪽이라 가장 넓은 체형도 안 잘린다.
    public void Fit()
    {
        if (_pawn == null) return;
        bool any = false;
        var b = new Bounds();
        foreach (var r in _pawn.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any) return;

        // 돌려도 잘리지 않게 가로는 넓은 쪽(x·z 중 큰 것)으로 잰다.
        _center = transform.InverseTransformPoint(b.center);
        _center.x = _center.z = 0f;   // 몸이 바뀌어도 한가운데는 발 위다 — 경계 중심을 따라가면 체형마다 옆으로 밀린다
        float halfH = b.extents.y;
        float halfW = Mathf.Max(Mathf.Max(b.extents.x, b.extents.z), halfH * 0.6f);
        float tanV  = Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float aspect = _rt != null && _rt.height > 0 ? (float)_rt.width / _rt.height : 1f;
        _fitDist = Mathf.Max(halfH / tanV, halfW / (tanV * aspect)) * 1.12f + halfW;
        PlaceCamera();
    }

    // 휠 — 양수면 당긴다. 한 칸에 15%.
    public void Zoom(float notches)
    {
        _zoom = Mathf.Clamp(_zoom * Mathf.Pow(0.85f, notches), ZoomMin, ZoomMax);
        PlaceCamera();
    }

    void PlaceCamera()
    {
        if (_cam == null) return;
        // 폰은 +Z를 본다 — 카메라는 +Z 쪽에서 한가운데를 수평으로 본다(내려다보면 머리가 커 보여 다시 위로 쏠린다).
        _cam.transform.localPosition = _center + new Vector3(0f, 0f, _fitDist * _zoom);
        _cam.transform.localRotation = Quaternion.LookRotation(Vector3.back);
    }

    // 끌어서 돌린다(화면이 부른다).
    public void Turn(float degrees)
    {
        _yaw += degrees;
        if (_pawn != null) _pawn.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
    }

    void OnDestroy()
    {
        if (_rt != null) { _rt.Release(); Destroy(_rt); }
    }
}
