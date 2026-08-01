using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

// 런타임 OBJ/MTL 로더 — **모드가 유니티 없이 모델을 들고 오는 길**.
//
// 왜 이게 필요한가: 에셋 번들만으로는 모드 구조가 성립하지 않는다. 번들을 구우려면
// 모드 제작자가 유니티를 설치하고 **정확히 같은 버전**으로 구워야 한다 — "폴더를 넣으면 된다"가
// 모델에서만 깨진다. `.fbx`는 런타임에 해석할 방법이 없지만 `.obj`는 순수 텍스트라 읽을 수 있고,
// 모든 3D 툴이 내보낸다. 텍스처는 `ModAssets`의 PNG 로더가 받는다.
//
// 지원 범위 — 소품 모델에 필요한 만큼:
//   v / vt / vn / f (n각형은 부채꼴 삼각화) / usemtl(서브메시) / mtllib
//   MTL: newmtl · Kd(색) · d·Tr(투명) · map_Kd(텍스처)
// 안 하는 것: 뼈대·애니메이션·PBR 맵. 그건 에셋 번들 쪽 몫이다.
public static class ObjModel
{
    // OBJ는 오른손 좌표계(Y 위, -Z 앞), 유니티는 왼손(Y 위, +Z 앞).
    // **Z를 뒤집고 삼각형 감는 방향도 뒤집는다** — 축만 뒤집으면 모델이 거울상이 되고,
    // 감는 방향만 뒤집으면 앞뒤가 뒤집혀 안쪽만 보인다. 둘 다 해야 제대로 선다.
    static Vector3 ToUnity(Vector3 v) => new(v.x, v.y, -v.z);

    // `texture`는 텍스처 이름을 받아 실제 텍스처를 돌려주는 해석기(`ModAssets`가 넘긴다).
    // OBJ 파일이 자기 폴더 기준으로 텍스처를 가리키므로 해석은 부르는 쪽이 한다.
    public static GameObject Load(string file, Func<string, Texture2D> texture)
    {
        try { return Parse(file, texture); }
        catch (Exception e)
        {
            Debug.LogWarning($"[ObjModel] 모델을 읽지 못했습니다: {file}\n{e}");
            return null;
        }
    }

    static GameObject Parse(string file, Func<string, Texture2D> texture)
    {
        var positions = new List<Vector3>();
        var uvs       = new List<Vector2>();
        var normals   = new List<Vector3>();

        // 유니티 메시는 정점 하나에 위치·UV·법선이 함께 붙는다. OBJ는 셋을 **따로** 색인하므로
        // `v/vt/vn` 조합마다 정점을 하나씩 펼쳐야 한다 — 그 조합을 기억하는 표.
        var vertexOf = new Dictionary<string, int>();
        var vPos = new List<Vector3>();
        var vUv  = new List<Vector2>();
        var vNrm = new List<Vector3>();
        bool missingNormal = false;

        // usemtl 구간마다 서브메시 하나.
        var groups   = new List<KeyValuePair<string, List<int>>>();
        List<int> tris = null;

        var mtlNames = new List<string>();

        foreach (var raw in File.ReadLines(file))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;

            var t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            switch (t[0])
            {
                case "v":
                    positions.Add(ToUnity(new Vector3(F(t, 1), F(t, 2), F(t, 3))));
                    break;

                case "vt":
                    uvs.Add(new Vector2(F(t, 1), F(t, 2)));   // OBJ도 유니티도 좌하단 원점 — 그대로
                    break;

                case "vn":
                    normals.Add(ToUnity(new Vector3(F(t, 1), F(t, 2), F(t, 3))));
                    break;

                case "mtllib":
                    if (t.Length > 1) mtlNames.Add(string.Join(" ", t, 1, t.Length - 1));
                    break;

                case "usemtl":
                    tris = new List<int>();
                    groups.Add(new KeyValuePair<string, List<int>>(t.Length > 1 ? t[1] : "", tris));
                    break;

                case "f":
                {
                    if (t.Length < 4) break;   // 삼각형이 못 되는 면은 버린다

                    // `usemtl` 없이 면이 먼저 나오는 파일도 있다 — 이름 없는 구간을 만들어 받는다.
                    if (tris == null)
                    {
                        tris = new List<int>();
                        groups.Add(new KeyValuePair<string, List<int>>("", tris));
                    }

                    // n각형은 부채꼴로 삼각화. 감는 방향은 뒤집는다(위 ToUnity 주석).
                    int first = Vertex(t[1]);
                    for (int i = 3; i < t.Length; i++)
                    {
                        int a = Vertex(t[i - 1]);
                        int b = Vertex(t[i]);
                        tris.Add(first); tris.Add(b); tris.Add(a);
                    }
                    break;
                }
            }
        }

        if (vPos.Count == 0)
        {
            Debug.LogWarning($"[ObjModel] 정점이 없습니다: {file}");
            return null;
        }

        var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(file) };
        // 유니티 기본 색인은 16비트라 정점 65535개가 한계다. 넘으면 조용히 잘리므로 미리 올린다.
        if (vPos.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;

        mesh.SetVertices(vPos);
        if (uvs.Count > 0) mesh.SetUVs(0, vUv);

        mesh.subMeshCount = groups.Count;
        for (int i = 0; i < groups.Count; i++)
            mesh.SetTriangles(groups[i].Value, i);

        // 법선이 하나라도 비면 통째로 다시 계산한다 — 섞으면 그 면만 새까맣게 나온다.
        if (normals.Count > 0 && !missingNormal) mesh.SetNormals(vNrm);
        else                                     mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var materials = LoadMaterials(file, mtlNames, texture);
        var assigned  = new Material[groups.Count];
        for (int i = 0; i < groups.Count; i++)
            assigned[i] = materials.TryGetValue(groups[i].Key, out var m) ? m : Fallback();

        var go = new GameObject(mesh.name);
        go.AddComponent<MeshFilter>().sharedMesh        = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = assigned;
        return go;

        // ── 지역 함수 ─────────────────────────────────────────────────────
        int Vertex(string token)
        {
            if (vertexOf.TryGetValue(token, out int existing)) return existing;

            var parts = token.Split('/');
            int pi = Index(parts[0], positions.Count);
            int ti = parts.Length > 1 && parts[1].Length > 0 ? Index(parts[1], uvs.Count)     : -1;
            int ni = parts.Length > 2 && parts[2].Length > 0 ? Index(parts[2], normals.Count) : -1;

            vPos.Add(pi >= 0 && pi < positions.Count ? positions[pi] : Vector3.zero);
            vUv .Add(ti >= 0 && ti < uvs.Count       ? uvs[ti]       : Vector2.zero);
            if (ni >= 0 && ni < normals.Count) vNrm.Add(normals[ni]);
            else { vNrm.Add(Vector3.zero); missingNormal = true; }

            vertexOf[token] = vPos.Count - 1;
            return vPos.Count - 1;
        }
    }

    // OBJ 색인은 **1부터**이고 **음수는 끝에서 센다**(-1 = 마지막).
    // 음수 색인을 빠뜨리면 그걸 쓰는 도구가 뽑은 파일이 통째로 깨진다.
    static int Index(string s, int count) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)
            ? (i > 0 ? i - 1 : count + i)
            : -1;

    // **반드시 InvariantCulture로 파싱한다.** 한국어 윈도우의 기본 문화권으로 읽으면
    // 소수점 해석이 달라져 좌표가 통째로 어긋난다.
    static float F(string[] t, int i) =>
        i < t.Length && float.TryParse(t[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? v : 0f;

    // ── MTL ───────────────────────────────────────────────────────────────

    static Dictionary<string, Material> LoadMaterials(
        string objFile, List<string> mtlNames, Func<string, Texture2D> texture)
    {
        var result = new Dictionary<string, Material>();
        string dir = Path.GetDirectoryName(objFile) ?? "";

        foreach (var name in mtlNames)
        {
            string path = Path.Combine(dir, name);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[ObjModel] MTL을 찾지 못했습니다: {name} (기준: {dir})");
                continue;
            }

            Material current = null;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var t = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                switch (t[0])
                {
                    case "newmtl":
                        current = Fallback();
                        current.name = t.Length > 1 ? t[1] : "material";
                        result[current.name] = current;
                        break;

                    case "Kd":
                        // 알파는 건드리지 않는다 — `d`/`Tr`이 Kd보다 먼저 나올 수도 있다.
                        if (current != null)
                            SetColor(current, new Color(F(t, 1), F(t, 2), F(t, 3), CurrentColor(current).a));
                        break;

                    case "d":   // 1 = 불투명
                        if (current != null) SetAlpha(current, F(t, 1));
                        break;

                    case "Tr":  // Tr은 d의 반대다 — 0이 불투명
                        if (current != null) SetAlpha(current, 1f - F(t, 1));
                        break;

                    case "map_Kd":
                    {
                        // 옵션이 앞에 붙을 수 있다(`map_Kd -s 1 1 1 wood.png`) — 파일 이름은 맨 뒤.
                        if (current == null || t.Length < 2) break;
                        var tex = texture?.Invoke(t[t.Length - 1]);
                        if (tex != null) SetTexture(current, tex);
                        break;
                    }
                }
            }
        }
        return result;
    }

    // ── 머티리얼 ──────────────────────────────────────────────────────────
    // URP·빌트인 어느 쪽이든 서게 프로퍼티를 **둘 다** 쓴다. 없는 프로퍼티에 쓰는 건 무해하다.

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId     = Shader.PropertyToID("_Color");
    static readonly int BaseMapId   = Shader.PropertyToID("_BaseMap");
    static readonly int MainTexId   = Shader.PropertyToID("_MainTex");

    static void SetColor(Material m, Color c)   { m.SetColor(BaseColorId, c); m.SetColor(ColorId, c); }
    static void SetTexture(Material m, Texture2D t) { m.SetTexture(BaseMapId, t); m.SetTexture(MainTexId, t); }

    // 지금 색을 그대로 읽는다. **알파만 바꾸거나 색만 바꿀 때** 나머지 한쪽을 보존하려고 쓴다.
    static Color CurrentColor(Material m) =>
        m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;

    static void SetAlpha(Material m, float a)
    {
        var c = CurrentColor(m);
        c.a = a;
        SetColor(m, c);
    }

    static Shader _shader;

    static Material Fallback()
    {
        if (_shader == null)
        {
            _shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // 마지막 수단: 기본 도형의 셰이더를 빌려 온다. `Shader.Find`는 **빌드에 포함된 것만**
            // 찾으므로 이름을 믿고 null을 그대로 두면 분홍 덩어리가 된다.
            if (_shader == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _shader = probe.GetComponent<MeshRenderer>().sharedMaterial.shader;
                UnityEngine.Object.Destroy(probe);
            }
        }
        var mat = new Material(_shader);
        SetColor(mat, Color.white);
        return mat;
    }
}
