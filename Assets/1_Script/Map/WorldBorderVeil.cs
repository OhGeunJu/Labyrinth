using UnityEngine;

// **세계의 끝에 서는 투명한 막 — 마인크래프트의 월드보더**(2026-09-15).
//
// 사용자: *"카메라를 경계 밖으로 나가려고 했을 때 그 경계의 벽 위 허공에 약간 투명막 같은
// 느낌 — 마크에서 맵 끝에 가면 보이는 월드보더."*
//
// 처음에는 **기반암을 발광 재질로 칠했다** — *"너무 쨍하다"*였다. 세계 둘레 4km를 한 바퀴
// 두르는 줄이 늘 빛나면 화면의 주인공이 된다. **알릴 때만 보여야 한다** — 그래서 물건이
// 아니라 막이고, **다가갈 때만** 짙어진다.
//
// ── 어기면 조용히 깨지는 것 ────────────────────────────────────────────────
//
// - **카메라가 막 위에 서면 막이 안 보인다** — 수직면을 제 평면에서 보면 선 하나다.
//   카메라를 세계 끝에서 `CameraMargin`만큼 안쪽에 묶는 이유다(`CameraController.ClampToWorld`).
// - **변마다 따로 짙어진다** — 한 재질로 전부 올리면 서쪽에 붙었는데 동쪽 끝까지 막이 선다
//   (1km 밖이라 안 보이긴 하지만, 모서리에서는 두 변이 같이 보여야 한다).
// - **콜라이더를 안 단다** — 커서 레이캐스트가 막을 짚으면 그 뒤의 벽·폰을 못 고른다.
// - **무늬는 월드 눈금이다** — UV를 미터로 준다. 0~1로 주면 1km짜리 면에 줄무늬가 너덧 개라
//   막이 아니라 **색이 칠해진 판**이다.
// - **시간은 실제 시간이다** — 일시정지 중에도 카메라는 움직이고, 배속에서 막이 열 배로
//   흐르면 안 된다(`CameraController.Dt`와 같은 이유).
public class WorldBorderVeil : MonoBehaviour
{
    // 카메라가 세계 끝에서 이만큼 안쪽에 묶인다(m). 기반암(3칸)보다 조금 더 안쪽이다.
    public const float CameraMargin = 8f;

    const float Top        = 220f;   // 줌을 끝까지 당겨도 위가 안 보이게
    const float Bottom     = -4f;
    const float StripeM    = 6f;     // 줄무늬 한 벌이 몇 m인가
    const float FadeRange  = 70f;    // 이만큼 가까워지면서 짙어진다
    const float BaseAlpha  = 0.35f;  // 끝에 붙었을 때
    const float PushAlpha  = 0.65f;  // 끝을 밀고 있을 때 — "나가려 했다"
    const float PushFade   = 0.6f;   // 밀기를 멈추면 이만큼(초)에 걸쳐 가라앉는다
    const float ScrollSpeed = 0.08f; // 줄무늬가 위로 흐르는 속도(한 벌/초)

    static readonly Color Tint = new(0.45f, 0.72f, 1.0f);

    // **밀고 있다는 소식은 카메라가 준다** — 막이 입력을 읽으면 카메라 조작이 두 곳에 산다.
    // 서 · 동 · 남 · 북.
    static readonly float[] _pushedAt = { -99f, -99f, -99f, -99f };

    public static void Push(int side)
    {
        if ((uint)side < 4) _pushedAt[side] = Time.unscaledTime;
    }

    Transform _root;
    Texture2D _tex;
    readonly MeshRenderer[] _renderers = new MeshRenderer[4];
    readonly Material[]     _mats      = new Material[4];
    readonly Mesh[]         _meshes    = new Mesh[4];

    static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
    static readonly int MainTexSt = Shader.PropertyToID("_MainTex_ST");

    void OnEnable()
    {
        if (!RockMap.Enabled) { enabled = false; return; }

        _tex  = StripeTexture();
        _root = new GameObject("WorldBorderVeil").transform;
        _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        float w = TerrainGrid.WorldSize;
        // 서(x=0) · 동(x=W) · 남(z=0) · 북(z=W). 면은 변을 따라 뻗는다.
        Build(0, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, w));
        Build(1, new Vector3(w,  0f, 0f), new Vector3(w,  0f, w));
        Build(2, new Vector3(0f, 0f, 0f), new Vector3(w,  0f, 0f));
        Build(3, new Vector3(0f, 0f, w),  new Vector3(w,  0f, w));
    }

    void OnDisable()
    {
        for (int i = 0; i < 4; i++)
        {
            if (_mats[i]   != null) Destroy(_mats[i]);
            if (_meshes[i] != null) Destroy(_meshes[i]);
            _mats[i] = null; _meshes[i] = null; _renderers[i] = null;
        }
        if (_tex  != null) Destroy(_tex);
        if (_root != null) Destroy(_root.gameObject);
        _tex = null; _root = null;
    }

    void Build(int side, Vector3 a, Vector3 b)
    {
        float len = Vector3.Distance(a, b);
        var mesh = new Mesh { name = $"Veil_{side}" };
        mesh.SetVertices(new[]
        {
            new Vector3(a.x, Bottom, a.z), new Vector3(b.x, Bottom, b.z),
            new Vector3(b.x, Top,    b.z), new Vector3(a.x, Top,    a.z),
        });
        // **UV는 미터 ÷ 줄무늬 한 벌** — 월드 눈금이다.
        mesh.SetUVs(0, new[]
        {
            new Vector2(0f, Bottom / StripeM),       new Vector2(len / StripeM, Bottom / StripeM),
            new Vector2(len / StripeM, Top / StripeM), new Vector2(0f, Top / StripeM),
        });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(mesh.name);
        go.transform.SetParent(_root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;
        mr.enabled           = false;

        var mat = new Material(RuntimeShaders.Find(RuntimeShaders.Unlit, RuntimeShaders.UnlitTexture))
        { name = mesh.name };
        FadeMaterial.Setup(mat);
        mat.mainTexture = _tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", _tex);
        if (mat.HasProperty("_Cull"))    mat.SetFloat("_Cull", 0f);   // 양면 — 안에서도 밖에서도
        FadeMaterial.SetColor(mat, new Color(Tint.r, Tint.g, Tint.b, 0f));
        mr.sharedMaterial = mat;

        _meshes[side] = mesh; _mats[side] = mat; _renderers[side] = mr;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        var   p = cam.transform.position;
        float w = TerrainGrid.WorldSize;
        float now = Time.unscaledTime;

        float scroll = now * ScrollSpeed;
        for (int i = 0; i < 4; i++)
        {
            var mr = _renderers[i];
            if (mr == null) continue;

            // 막까지의 거리 — 카메라는 `CameraMargin` 안쪽에 묶이므로 그만큼을 빼고 잰다.
            float dist  = i == 0 ? p.x : i == 1 ? w - p.x : i == 2 ? p.z : w - p.z;
            float near  = Mathf.Clamp01(1f - (dist - CameraMargin) / FadeRange);
            float push  = Mathf.Clamp01(1f - (now - _pushedAt[i]) / PushFade);
            float alpha = Mathf.Max(near * near * BaseAlpha, push * PushAlpha);

            mr.enabled = alpha > 0.003f;   // 안 보일 때는 아예 안 그린다
            if (!mr.enabled) continue;

            FadeMaterial.SetAlpha(_mats[i], alpha);
            var st = new Vector4(1f, 1f, 0f, -scroll);
            _mats[i].SetVector(BaseMapSt, st);
            _mats[i].SetVector(MainTexSt, st);
        }
    }

    // 사선 줄무늬. **줄 사이도 아주 옅게 남긴다** — 완전히 비우면 막이 아니라 떠 있는 선들이다.
    static Texture2D StripeTexture()
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true)
        {
            name = "WorldBorderStripes", wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear, anisoLevel = 4,
        };
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            // 한 칸에 사선 두 줄. 가장자리를 부드럽게 — 딱 자르면 멀리서 계단이 진다.
            float t    = ((x + y) % (N / 2)) / (float)(N / 2);          // 0~1
            float band = Mathf.Abs(t - 0.5f) * 2f;                        // 0 가운데 · 1 가장자리
            float a    = Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.45f - band) / 0.2f + 0.5f)));
            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }
}
