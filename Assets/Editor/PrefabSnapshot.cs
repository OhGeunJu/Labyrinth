using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// **고른 프리팹을 찍어 한 장으로 — 화면을 못 보는 쪽이 모델을 고를 때**(2026-09-29).
//
// 치비 몸 작업(09-23)에서 배운 것: *"화면을 못 보는 채로 추측하면 다섯 번 헛돈다 · 다시 할 때는 찍어서 보는
// 장치부터"*. 팩 모델은 이름이 번호뿐이라(`Fantasy_animal_7`) 무엇인지 글로는 알 수 없다 — 그래서 **고른 것을
// 빈 미리보기 씬에 하나씩 세우고 비스듬히 찍어 격자 한 장**(`Temp/Snapshots/sheet.png`)으로 남긴다.
//
// 프로젝트 창에서 **프리팹 여럿이나 폴더**를 고르고 돌린다. 칸 순서는 이름의 **숫자 순**(1, 2 … 10)이고
// 콘솔에 칸 번호 → 이름이 찍힌다. 씬은 안 건드린다(미리보기 씬은 저장되지 않는다).
public static class PrefabSnapshot
{
    const int Cell = 256, Cols = 5;
    const string OutDir = "Temp/Snapshots";

    [MenuItem("DungeonColony/Debug/Snap Selected Prefabs (고른 프리팹 한 장으로)")]
    static void Run()
    {
        var paths = new List<string>();
        foreach (var o in Selection.objects)
        {
            string p = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(p)) continue;
            if (AssetDatabase.IsValidFolder(p))
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { p })) paths.Add(AssetDatabase.GUIDToAssetPath(g));
            else if (p.EndsWith(".prefab") || p.EndsWith(".fbx")) paths.Add(p);
        }
        if (paths.Count == 0) { Debug.LogWarning("[찍기] 프로젝트 창에서 프리팹이나 폴더를 고르세요."); return; }
        paths.Sort(NaturalCompare);

        int rows = (paths.Count + Cols - 1) / Cols;
        var sheet = new Texture2D(Cols * Cell, rows * Cell, TextureFormat.RGB24, false);
        var bg = new Color(0.32f, 0.33f, 0.36f);
        var fill = new Color[sheet.width * sheet.height];
        for (int i = 0; i < fill.Length; i++) fill[i] = bg;
        sheet.SetPixels(fill);

        var scene = EditorSceneManager.NewPreviewScene();
        var rt    = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var shot  = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
        try
        {
            var camGo = new GameObject("SnapCam");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene           = scene;
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.fieldOfView     = 30f;
            cam.targetTexture   = rt;

            var lightGo = new GameObject("SnapLight");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var log = new System.Text.StringBuilder("[찍기] 칸 → 이름 (왼쪽 위부터 오른쪽으로)\n");
            for (int i = 0; i < paths.Count; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (asset == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                var b = new Bounds(Vector3.zero, Vector3.zero);
                bool any = false;
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }

                // **비스듬히 앞 45°에서** — 옆모습으로는 네발짐승과 새가 안 갈린다.
                float radius = any ? Mathf.Max(0.2f, b.extents.magnitude) : 1f;
                float dist   = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                var dir = Quaternion.Euler(20f, 35f, 0f) * Vector3.back;
                cam.transform.position = b.center - dir * dist;
                cam.transform.LookAt(b.center);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane  = dist * 4f;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                shot.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                shot.Apply();
                RenderTexture.active = prev;

                int col = i % Cols, row = rows - 1 - i / Cols;   // 텍스처는 아래가 0이다
                sheet.SetPixels(col * Cell, row * Cell, Cell, Cell, shot.GetPixels());

                log.AppendLine($"  {i + 1,2}. {Path.GetFileNameWithoutExtension(paths[i])}" +
                               (any ? $"  (크기 {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} m)" : "  (그릴 것이 없다)"));
                Object.DestroyImmediate(inst);
            }

            Directory.CreateDirectory(OutDir);
            string outPath = Path.Combine(OutDir, "sheet.png");
            File.WriteAllBytes(outPath, sheet.EncodeToPNG());
            log.Append($"→ {Path.GetFullPath(outPath)}");
            Debug.Log(log.ToString());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(shot);
            Object.DestroyImmediate(sheet);
        }
    }

    // `animal_2`가 `animal_10`보다 앞에 오게.
    static int NaturalCompare(string a, string b)
    {
        string Pad(string s) => Regex.Replace(s, @"\d+", m => m.Value.PadLeft(6, '0'));
        return string.CompareOrdinal(Pad(a), Pad(b));
    }
}
