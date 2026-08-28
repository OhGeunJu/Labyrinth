using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

// **세계 16,384m를 한 장으로 띄운다**(2026-08-27).
//
// 저작을 막고 있던 것은 *"한 화면에 0.2%밖에 안 보인다"*였다. `Open Chunk`가 256m,
// `Show Terrain Here`가 768m인데 세계는 16,384m라, **"저기에 산맥, 여기서 저기까지 사막"을
// 잡을 눈이 아예 없었다.**
//
// ── 왜 전부 올리는 것은 안 되고 이것은 되나 ─────────────────────────────────
//
// 진짜 해상도(정점 간격 2m)로 세계를 다 세우면 **정점 6,712만 개 · 4,096청크**다. 메모리
// 550MB에, 콜라이더 굽기가 무거워 `TerrainStreamer.buildsPerFrame`이 **1**인 것을 생각하면
// 세우는 데만 1분이 넘고 그동안 계속 끊긴다.
//
// **보기만 할 것이면 그 0.4%면 된다.** 정점 간격을 32m로 낮추면 513×513 = 26만 정점이고,
// 산·분지는 반경이 수백 m~수 km라 **그대로 보인다.** 사라지는 것은 잔기복(주기 80m)뿐인데
// 그건 어차피 게임 지도(`TerrainHeight.MapHeight`)가 같은 이유로 이미 빼고 있다.
//
// ── 왜 `Terrain`이 아니라 메시인가 ──────────────────────────────────────────
//
// `Terrain`으로 만들면 딸려 오는 것이 전부 짐이다 — 콜라이더 · 패치 LOD · 이웃 자동 연결 ·
// 지형 레이어. 그리고 **진짜 지면으로 오인될 수 있다**: 콜라이더가 `Ground` 레이어에 서면
// 건설 커서와 이동 클릭이 이 저해상도 판을 짚어, 지형은 멀쩡한데 건물이 엉뚱한 높이에 놓인다.
//
// 메시 한 장은 그 어느 것도 아니다 — **콜라이더가 없어 레이캐스트에 안 걸리고**, UV를 내가
// 정하므로 바이옴 색을 그대로 입힐 수 있다.
//
// ── 높이 과장은 트랜스폼이 한다 ─────────────────────────────────────────────
//
// 16km 폭에 기복이 300m면 **1.8%**라 화면에서는 판판한 종이로 보인다. 그래서 세로로 부풀리는데,
// **정점에 곱해 굽지 않고 `localScale.y`로 준다** — 그래야 슬라이더가 즉시 먹고(다시 굽지 않는다),
// **1로 두면 진짜 지면과 정확히 겹친다**(비교할 수 있다).
public class WorldPreview : EditorWindow
{
    const string RootName = "__WorldPreview";
    const int    TexSize  = 512;    // 바이옴 색 텍스처 한 변 — 한 픽셀이 32m

    // 정점 간격(m). **세계 한 변(16,384m)을 나눠떨어지게** — 안 떨어지면 마지막 줄이 세계
    // 밖으로 삐져나가 가장자리가 접힌다. 8m(420만 정점)는 안 둔다 — 굽는 데 몇십 초가 걸리고,
    // **한눈에 보자는 도구가 그만큼 기다리게 하면 뜻이 없다.**
    static readonly int[] Spacings = { 64, 32, 16 };

    int   _spacing      = 32;
    float _exaggeration = 6f;

    [MenuItem("DungeonColony/Terrain/E. World Preview (전체 보기)")]
    static void Open()
    {
        var w = GetWindow<WorldPreview>("World Preview");
        w.minSize = new Vector2(340f, 230f);
        w.maxSize = new Vector2(520f, 300f);
    }

    // ── 창 ──────────────────────────────────────────────────────────────────

    void OnGUI()
    {
        EditorGUILayout.LabelField("세계 전체 보기", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"세계 {TerrainGrid.WorldSize:N0}m · 지역 {TerrainGrid.RegionsPerAxis}×{TerrainGrid.RegionsPerAxis}" +
            $" ({TerrainGrid.RegionSize:N0}m)", EditorStyles.miniLabel);

        EditorGUILayout.Space(4f);

        int idx = Mathf.Max(0, System.Array.IndexOf(Spacings, _spacing));
        var labels = new string[Spacings.Length];
        for (int i = 0; i < Spacings.Length; i++)
        {
            int n = TerrainGrid.WorldSize / Spacings[i] + 1;
            labels[i] = $"{Spacings[i]}m — {n}² = {n * (long)n / 1000:N0}k 정점";
        }
        _spacing = Spacings[EditorGUILayout.Popup("정점 간격", idx, labels)];
        EditorGUILayout.LabelField(Cost(_spacing), EditorStyles.miniLabel);

        EditorGUILayout.Space(4f);

        // **띄워 둔 채로 움직인다** — 다시 굽지 않으므로 슬라이더가 즉시 먹는다.
        EditorGUI.BeginChangeCheck();
        _exaggeration = EditorGUILayout.Slider("높이 과장", _exaggeration, 1f, 20f);
        if (EditorGUI.EndChangeCheck()) Exaggerate(_exaggeration);

        EditorGUILayout.LabelField(
            _exaggeration <= 1.001f
                ? "1 = 실제 높이. 진짜 지면과 정확히 겹친다."
                : $"기복 300m가 {300f * _exaggeration:N0}m로 보인다 " +
                  $"(16km 폭 기준 {300f * _exaggeration / TerrainGrid.WorldSize * 100f:0.#}%).",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(6f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("띄우기", GUILayout.Height(24f)))
            {
                Build(_spacing, _exaggeration);
                Frame();
            }

            using (new EditorGUI.DisabledScope(!Exists))
            {
                if (GUILayout.Button("전체 보기", GUILayout.Height(24f))) Frame();
                if (GUILayout.Button("치우기",   GUILayout.Height(24f))) Clear();
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "콜라이더가 없어 클릭·건설 레이캐스트에 안 걸리고, 씬에 저장되지도 않는다.\n" +
            "산·분지를 `D. Edit Mountains`로 옮기면 손을 뗄 때 여기도 같이 다시 그려진다.",
            MessageType.None);
    }

    static string Cost(int spacing)
    {
        int  n     = TerrainGrid.WorldSize / spacing + 1;
        long verts = (long)n * n;
        long tris  = (long)(n - 1) * (n - 1) * 2;
        long bytes = verts * 32 + tris * 12 + TexSize * TexSize * 4;
        return $"삼각형 {tris / 1000:N0}k · 메모리 약 {bytes / (1024f * 1024f):0.#} MB" +
               (verts > 900_000 ? "   (굽는 데 1초쯤 걸린다)" : "");
    }

    // ── 짓기 ────────────────────────────────────────────────────────────────

    // **정적이다** — 산 손잡이를 끄는 도중에도 불리므로, 여기서 창을 띄우면 드래그 중에
    // 창이 튀어나온다.
    public static void Build(int spacing, float exaggeration)
    {
        Clear();

        // **씬의 `TerrainStreamer`가 값의 주인이다** — 안 읽어 오면 기본 프로필로 그려서
        // 저작해 둔 산이 하나도 안 나온다.
        TerrainAuthoring.SyncFromScene();

        var sw = Stopwatch.StartNew();
        int n  = TerrainGrid.WorldSize / Mathf.Max(1, spacing) + 1;

        try
        {
            var go = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            mf.sharedMesh        = BuildMesh(n);
            mr.sharedMaterial    = BuildMaterial();
            mr.shadowCastingMode = ShadowCastingMode.Off;   // 미리보기가 진짜 지형에 그림자를 던지면 안 된다
            mr.receiveShadows    = false;
        }
        finally { EditorUtility.ClearProgressBar(); }

        Exaggerate(exaggeration);
        sw.Stop();

        Debug.Log($"[전체 보기] {TerrainGrid.WorldSize:N0}m를 한 장으로 띄웠다 — " +
                  $"{n}² = {n * (long)n:N0} 정점 · {sw.ElapsedMilliseconds}ms. " +
                  "**잔기복은 빠져 있다**(게임 지도와 같은 `MapHeight`) — 손댄 델타도 안 보인다. " +
                  "정밀 조각은 `1. Open Chunk`.");
    }

    // **높이는 `MapHeight`로 뽑는다 — 게임 지도와 같은 답이다.**
    //
    // 한 정점이 32m를 대표하는데 잔기복의 주기가 80m라, 그냥 `Procedural`을 쓰면 **표본
    // 간격이 주기와 비슷해져 모아레**가 생긴다(지도 텍스처가 먼저 겪은 그것). 애초에 이
    // 배율에 2m짜리 요철은 그릴 것이 아니다.
    static Mesh BuildMesh(int n)
    {
        var verts = new Vector3[n * n];
        var uvs   = new Vector2[n * n];

        float span = TerrainGrid.WorldSize;

        for (int z = 0; z < n; z++)
        {
            if ((z & 63) == 0)
                EditorUtility.DisplayProgressBar("전체 보기", "높이를 뽑는다…", z / (float)n * 0.7f);

            float wz = z * span / (n - 1);
            for (int x = 0; x < n; x++)
            {
                float wx = x * span / (n - 1);
                int   i  = z * n + x;
                verts[i] = new Vector3(wx, TerrainHeight.MapHeight(wx, wz), wz);
                uvs[i]   = new Vector2(wx / span, wz / span);
            }
        }

        EditorUtility.DisplayProgressBar("전체 보기", "면을 잇는다…", 0.75f);

        var tris = new int[(n - 1) * (n - 1) * 6];
        int t = 0;
        for (int z = 0; z < n - 1; z++)
        for (int x = 0; x < n - 1; x++)
        {
            int i = z * n + x;
            tris[t++] = i;     tris[t++] = i + n; tris[t++] = i + 1;
            tris[t++] = i + 1; tris[t++] = i + n; tris[t++] = i + n + 1;
        }

        var mesh = new Mesh
        {
            name      = "WorldPreview",
            hideFlags = HideFlags.DontSave,
            // **26만 정점은 16비트 인덱스(65,535)를 넘는다** — 안 바꾸면 메시가 조용히 잘린다.
            indexFormat = IndexFormat.UInt32,
        };
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.triangles = tris;

        EditorUtility.DisplayProgressBar("전체 보기", "법선을 잡는다…", 0.9f);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // **색은 게임 지도가 쓰는 그것 그대로다**(`TerrainHeight.MapColor`) — 바이옴이 색을 들고
    // 높이가 명암을 준다. 여기서 따로 칠하면 **M키로 본 지도와 씬이 다른 세계**가 된다.
    static Material BuildMaterial()
    {
        EditorUtility.DisplayProgressBar("전체 보기", "바이옴 색을 굽는다…", 0.95f);

        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
        {
            name       = "WorldPreviewColor",
            hideFlags  = HideFlags.DontSave,
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var px = new Color32[TexSize * TexSize];
        float step = TerrainGrid.WorldSize / (float)TexSize;
        for (int y = 0; y < TexSize; y++)
        for (int x = 0; x < TexSize; x++)
        {
            // **알파는 손으로 채운다** — `MapColor`의 명암은 색에 실수를 곱한 것이라
            // 알파까지 같이 어두워진다(0.7). 불투명 셰이더는 안 보지만, 반투명으로 한 번
            // 바꾸는 순간 낮은 땅이 비쳐 보인다.
            Color c = TerrainHeight.MapColor((x + 0.5f) * step, (y + 0.5f) * step);
            c.a = 1f;
            px[y * TexSize + x] = c;
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);

        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Texture");
        var mat = new Material(sh) { name = "WorldPreview", hideFlags = HideFlags.DontSave };
        mat.mainTexture = tex;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0f);
        return mat;
    }

    // ── 손질 ────────────────────────────────────────────────────────────────

    static void Exaggerate(float ex)
    {
        var go = Find();
        if (go == null) return;
        go.transform.localScale = new Vector3(1f, Mathf.Max(0.01f, ex), 1f);
        SceneView.RepaintAll();
    }

    static void Frame()
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return;
        float half = TerrainGrid.WorldSize * 0.5f;
        view.LookAt(new Vector3(half, 0f, half), view.rotation, TerrainGrid.WorldSize * 0.6f);
    }

    static GameObject Find() => GameObject.Find(RootName);

    // ── 밖에서 부르는 문 ────────────────────────────────────────────────────

    public static bool Exists => Find() != null;

    public static void Clear()
    {
        var go = Find();
        if (go == null) return;

        // **메시·텍스처·머티리얼도 같이 버린다** — 오브젝트만 지우면 `DontSave`라 아무도 안
        // 거두고, 다시 띄울 때마다 26만 정점짜리 메시가 하나씩 쌓인다(`TerrainStreamer.Unload`가
        // `TerrainData`를 같이 버리는 것과 같은 자리).
        var mf = go.GetComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>();
        if (mf != null && mf.sharedMesh != null) DestroyImmediate(mf.sharedMesh);
        if (mr != null && mr.sharedMaterial != null)
        {
            if (mr.sharedMaterial.mainTexture != null) DestroyImmediate(mr.sharedMaterial.mainTexture);
            DestroyImmediate(mr.sharedMaterial);
        }
        DestroyImmediate(go);
    }

    // 산·분지를 옮긴 뒤 `TerrainAuthoring.RefreshPreview`가 부른다 — **띄워 둔 것이 있을 때만.**
    // **지금 떠 있는 해상도와 과장을 그대로 이어 간다** — 다시 그릴 때 눈금이 바뀌면 무엇이
    // 달라진 것인지 알 수 없다.
    public static bool Rebuild()
    {
        var go = Find();
        if (go == null) return false;

        var mf = go.GetComponent<MeshFilter>();
        int verts = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0;
        if (verts < 4) return false;

        int   n  = Mathf.RoundToInt(Mathf.Sqrt(verts));
        float ex = go.transform.localScale.y;

        Build(Mathf.Max(1, TerrainGrid.WorldSize / (n - 1)), ex);
        return true;
    }
}
