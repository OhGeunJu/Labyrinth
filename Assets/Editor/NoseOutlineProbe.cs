using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// **코의 바깥 선이 옆에서 안 보이는 까닭을 재는 진단**(2026-09-19, 사용자: *"옆에서 봤을 때는 외곽선이 보여야지"*).
//
// 추측으로 두 번 틀렸다 — 그래서 유니티에게 묻는다. 폰 하나를 미리보기 씬에 세우고 코를 **옆에서** 찍어,
// 바깥 선 재질을 뺀 그림(기준)과 **달라진 픽셀 수**를 조건마다 센다. 0이면 그 조건에서 선이 **아예 안 그려진** 것이다.
// 같은 장치를 쓰는 몸통 선을 같이 재서, 재는 틀 자체가 맞는지도 확인한다.
public static class NoseOutlineProbe
{
    const string Prefab = "Assets/3_Prefab/Pawn/Chibi_Male.prefab";
    const string Head   = "Male_BroadSoft";
    const int    Res    = 256;

    [MenuItem("DungeonColony/Pawn/Debug. Probe Nose Outline (코 바깥 선 진단)")]
    static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (prefab == null) { Debug.LogError($"[코 선 진단] 프리팹 없음: {Prefab}"); return; }

        var scene = EditorSceneManager.NewPreviewScene();
        var go    = Object.Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        var camGo = new GameObject("ProbeCam");
        SceneManager.MoveGameObjectToScene(camGo, scene);
        var cam = camGo.AddComponent<Camera>();
        cam.scene            = scene;
        cam.orthographic     = true;
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = Color.white;
        cam.nearClipPlane    = 0.01f;
        cam.farClipPlane     = 10f;
        cam.enabled          = false;

        var lightGo = new GameObject("ProbeLight");
        SceneManager.MoveGameObjectToScene(lightGo, scene);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.5f;
        lightGo.transform.rotation = Quaternion.Euler(30f, -90f, 0f);

        var rt  = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
        var log = new StringBuilder("[코 선 진단]\n");
        var temp = new List<Object> { rt, tex };

        try
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var nose  = rs.FirstOrDefault(r => r.name == $"Head.Nose.Pointy.{Head}");
            var torso = rs.FirstOrDefault(r => r.name == "Torso");
            var skin  = rs.FirstOrDefault(r => r.name == $"Head.Shape.{Head}");
            if (nose == null || torso == null || skin == null)
            { Debug.LogError($"[코 선 진단] 렌더러를 못 찾음 nose={nose} torso={torso} head={skin}"); return; }

            // 머리 · 코 · 몸통만 남긴다(머리카락·귀가 코 둘레를 가리면 재는 것이 흐려진다).
            foreach (var r in rs) r.enabled = r == nose || r == torso || r == skin;

            var noseMats  = nose.sharedMaterials;
            var torsoMats = torso.sharedMaterials;
            var skinMats  = skin.sharedMaterials;
            log.AppendLine($"코 재질: {string.Join(", ", noseMats.Select(m => m ? m.name : "null"))} · 메시 서브메시 {nose.GetComponent<MeshFilter>().sharedMesh.subMeshCount}");
            log.AppendLine($"몸통 재질: {string.Join(", ", torsoMats.Select(m => m ? m.name : "null"))}");

            Material Variant(Material src, System.Action<Material> tweak)
            { var m = new Material(src); tweak(m); temp.Add(m); return m; }

            // ── 코를 옆에서 ──
            var nb = nose.bounds;
            Frame(cam, nb.center, Vector3.right, Mathf.Max(nb.extents.magnitude * 2.2f, 0.08f));
            var baseNose = Shot(cam, rt, tex, () => nose.sharedMaterials = new[] { noseMats[0] });

            Save(tex, cam, rt, "0_base", () => nose.sharedMaterials = new[] { noseMats[0] });
            Save(tex, cam, rt, "1_asis", () => nose.sharedMaterials = noseMats);
            Save(tex, cam, rt, "2_nohead", () => { skin.enabled = false; nose.sharedMaterials = noseMats; });
            skin.enabled = true;
            // 씬 뷰처럼 원근으로, 비스듬히
            cam.orthographic = false; cam.fieldOfView = 30f;
            cam.transform.position = nb.center + new Vector3(0.9f, 0.15f, 0.25f);
            cam.transform.LookAt(nb.center);
            Save(tex, cam, rt, "3_persp", () => nose.sharedMaterials = noseMats);
            cam.orthographic = true;
            Frame(cam, nb.center, Vector3.right, Mathf.Max(nb.extents.magnitude * 2.2f, 0.08f));
            log.AppendLine($"코 bounds 중심 {nb.center} 크기 {nb.size} · 찍은 폭 {cam.orthographicSize * 2f:F3}m");

            void Try(string label, Material[] mats)
            {
                var img = Shot(cam, rt, tex, () => nose.sharedMaterials = mats);
                log.AppendLine($"  {label,-38} 달라진 픽셀 {Diff(baseNose, img)}");
            }
            log.AppendLine("코(옆모습) — 기준은 살 재질 하나:");
            Try("지금 그대로(바깥 선 가는 재질)", noseMats);
            if (noseMats.Length > 1 && noseMats[1] != null)
            {
                var o = noseMats[1];
                Try("굵기 6cm", new[] { noseMats[0], Variant(o, m => { m.SetFloat("_OutlineWidth", 0.06f); m.SetFloat("_OutlineMin", 0.008f); }) });
                Try("땅 이기기 1.3", new[] { noseMats[0], Variant(o, m => m.SetFloat("_OutlineGroundPush", 1.3f)) });
                Try("스텐실 번호 200(살과 다르게)", new[] { noseMats[0], Variant(o, m => m.SetFloat("_StencilRef", 200f)) });
            }
            Try("몸통 바깥 선 재질을 코에", new[] { noseMats[0], torsoMats.Length > 1 ? torsoMats[1] : null });
            Try("살 재질 둘(고전 선 6cm 비교)", new[] { skinMats[0] });

            // 살이 스텐실을 안 적으면?
            var skinNoWrite = Variant(skinMats[0], m => m.SetFloat("_StencilWriteMask", 0f));
            var noseNoWrite = Variant(noseMats[0], m => m.SetFloat("_StencilWriteMask", 0f));
            skin.sharedMaterials = new[] { skinNoWrite };
            var baseNoWrite = Shot(cam, rt, tex, () => nose.sharedMaterials = new[] { noseNoWrite });
            var imgNoWrite  = Shot(cam, rt, tex, () => nose.sharedMaterials = new[] { noseNoWrite, noseMats.Length > 1 ? noseMats[1] : null });
            log.AppendLine($"  {"머리·코 살이 스텐실을 안 적을 때",-38} 달라진 픽셀 {Diff(baseNoWrite, imgNoWrite)}");
            skin.sharedMaterials = skinMats;
            nose.sharedMaterials = noseMats;

            // ── 코를 앞에서 — 굵은 바깥 선이 얼굴 위에 검은 덩어리로 뜨나 ──
            Frame(cam, nb.center, Vector3.forward, Mathf.Max(nb.extents.magnitude * 2.2f, 0.08f));
            var baseFront = Shot(cam, rt, tex, () => nose.sharedMaterials = new[] { noseMats[0] });
            var imgFront  = Shot(cam, rt, tex, () => nose.sharedMaterials = noseMats);
            System.IO.File.WriteAllBytes("Temp/NoseProbe/4_front.png", tex.EncodeToPNG());
            log.AppendLine($"코(앞모습) 얼굴 위 — 달라진 픽셀 {Diff(baseFront, imgFront)} (0이어야 한다: 제 얼굴 위에는 선이 안 선다)");

            // 같은 자리가 얼굴이 아니었다면(= 제 손·몸통) 선이 서야 한다 — 머리 살을 "얼굴 아님"으로 바꿔 흉내 낸다.
            var notFace = Variant(skinMats[0], m => { m.SetFloat("_StencilFace", 0f); m.SetFloat("_StencilRef", 1f); });
            skin.sharedMaterials = new[] { notFace };
            var baseOther = Shot(cam, rt, tex, () => nose.sharedMaterials = new[] { noseMats[0] });
            var imgOther  = Shot(cam, rt, tex, () => nose.sharedMaterials = noseMats);
            System.IO.File.WriteAllBytes("Temp/NoseProbe/5_front_otherpart.png", tex.EncodeToPNG());
            log.AppendLine($"코(앞모습) 다른 부위 위 — 달라진 픽셀 {Diff(baseOther, imgOther)} (0보다 커야 한다: 손·몸통 위에서는 선이 선다)");
            skin.sharedMaterials = skinMats;
            nose.sharedMaterials = noseMats;

            // ── 몸통을 옆에서(재는 틀이 맞는지) ──
            var tb = torso.bounds;
            Frame(cam, tb.center, Vector3.right, tb.extents.magnitude * 2.4f);
            var baseTorso = Shot(cam, rt, tex, () => torso.sharedMaterials = new[] { torsoMats[0] });
            var imgTorso  = Shot(cam, rt, tex, () => torso.sharedMaterials = torsoMats);
            log.AppendLine($"몸통(옆모습) 지금 그대로 — 달라진 픽셀 {Diff(baseTorso, imgTorso)}");
            torso.sharedMaterials = torsoMats;
        }
        finally
        {
            cam.targetTexture = null;
            foreach (var o in temp) if (o) Object.DestroyImmediate(o);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Debug.Log(log.ToString());
    }

    static void Save(Texture2D tex, Camera cam, RenderTexture rt, string name, System.Action setup)
    {
        Shot(cam, rt, tex, setup);
        System.IO.Directory.CreateDirectory("Temp/NoseProbe");
        System.IO.File.WriteAllBytes($"Temp/NoseProbe/{name}.png", tex.EncodeToPNG());
    }

    static void Frame(Camera cam, Vector3 center, Vector3 side, float size)
    {
        cam.transform.position = center + side * 2f;
        cam.transform.rotation = Quaternion.LookRotation(-side, Vector3.up);
        cam.orthographicSize   = size * 0.5f;
    }

    static Color32[] Shot(Camera cam, RenderTexture rt, Texture2D tex, System.Action setup)
    {
        setup();
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        return tex.GetPixels32();
    }

    static int Diff(Color32[] a, Color32[] b)
    {
        int n = 0;
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 40) n++;
        return n;
    }
}
