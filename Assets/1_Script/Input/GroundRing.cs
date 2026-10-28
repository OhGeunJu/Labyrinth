using UnityEngine;

// **발밑 고리 — 골렸다는 표시의 모양은 한 벌이다**(2026-09-09).
//
// 그전에는 납작한 원통(`Cylinder` 프리미티브)에 초록을 칠했다 — 빛을 타는 **불투명 원판**이라
// 두껍고 촌스러웠다(사용자: *"초록원이 너무 촌스러"*). 폰(`SelectionIndicator`)과 물건
// (`DroppedItem`)이 각자 그 원통을 만들고 있어 한쪽만 고치면 *골렸다*가 두 모양이 된다.
//
// 이제는 **얇은 고리**다: 안쪽·바깥쪽 가장자리로 갈수록 알파가 0으로 죽는 세 겹 띠라 테두리가
// 부드럽게 번진다. 빛을 안 타고(`Sprites/Default` — 정점 색·알파·깊이 판정) 지면에 눌러 그린다.
// **깊이 판정은 켠다** — 벽 너머로 보이면 표시가 아니라 투시다(`PawnMark`와 같은 규칙).
//
// 색은 부르는 쪽이 정하되 **정점 색에 굽는다** — 재질은 세상에 하나다(고리마다 재질을 만들면
// 색을 손보는 날 하나는 반드시 뒤처진다).
public static class GroundRing
{
    const int Segments = 48;

    static Material _mat;

    public static GameObject Create(Transform parent, float localY, float radius, Color color,
                                    string name = "Ring")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, localY, 0f);
        go.layer = parent.gameObject.layer;

        go.AddComponent<MeshFilter>().sharedMesh = Build(radius, color);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial      = Mat();
        mr.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows      = false;
        mr.lightProbeUsage     = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go;
    }

    static Material Mat()
    {
        if (_mat != null) return _mat;
        _mat = new Material(RuntimeShaders.Find(RuntimeShaders.SpritesDefault))
        {
            hideFlags   = HideFlags.HideAndDontSave,
            renderQueue = 3000,
        };
        return _mat;
    }

    // 세 겹 띠: 안쪽(알파 0) · 가운데(알파 1) · 바깥(알파 0). 지면 위 XZ 평면.
    static Mesh Build(float radius, Color color)
    {
        float[] rings = { radius * 0.74f, radius * 0.88f, radius };
        float[] alpha = { 0f, 1f, 0f };

        var verts  = new Vector3[Segments * 3];
        var colors = new Color[Segments * 3];
        for (int r = 0; r < 3; r++)
        for (int i = 0; i < Segments; i++)
        {
            float a = i / (float)Segments * Mathf.PI * 2f;
            verts [r * Segments + i] = new Vector3(Mathf.Cos(a) * rings[r], 0f, Mathf.Sin(a) * rings[r]);
            colors[r * Segments + i] = new Color(color.r, color.g, color.b, color.a * alpha[r]);
        }

        var tris = new int[Segments * 2 * 6];
        int t = 0;
        for (int r = 0; r < 2; r++)
        for (int i = 0; i < Segments; i++)
        {
            int a0 = r * Segments + i, a1 = r * Segments + (i + 1) % Segments;
            int b0 = a0 + Segments,   b1 = a1 + Segments;
            // 위에서 봤을 때 앞면이 되게 감는다 — 뒷면이면 컬링돼 안 보인다.
            tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
            tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
        }

        var mesh = new Mesh { name = "GroundRing" };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
