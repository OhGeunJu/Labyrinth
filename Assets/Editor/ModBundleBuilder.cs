using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 모드가 **모델을 들고 올 수 있게** 하는 도구.
//
// 왜 필요한가: 그림(PNG/JPG)은 런타임에 바로 해독되지만 **모델은 그렇지 않다.**
// 유니티는 실행 중에 .fbx/.obj를 해석하지 못한다 — 에디터가 미리 구운 에셋 번들만 읽을 수 있다.
// 그래서 모드 제작자에게 "번들을 구우세요"라고 말하려면 구울 도구가 있어야 한다.
// 읽는 쪽은 `ModAssets`.
//
// 쓰는 법: 프로젝트 창에서 모델·프리팹·텍스처를 고르고 → `DungeonColony → Build Mod AssetBundle`
//          → 번들을 넣을 모드 폴더 선택. 번들 이름은 **그 폴더 이름**이라 모드마다 하나씩 생긴다.
public static class ModBundleBuilder
{
    const string MenuPath = "DungeonColony/Build Mod AssetBundle";

    [MenuItem(MenuPath)]
    static void Build()
    {
        // 프로젝트 창의 선택만 본다(씬 오브젝트는 번들에 못 넣는다).
        var paths = Selection.GetFiltered<Object>(SelectionMode.Assets)
                             .Select(o => AssetDatabase.GetAssetPath(o))
                             .Where(p => !string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
                             .Distinct()
                             .ToArray();

        if (paths.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "선택된 에셋이 없습니다",
                "프로젝트 창에서 번들에 넣을 모델·프리팹·텍스처를 먼저 고르세요.\n" +
                "(폴더는 안 됩니다 — 안의 파일을 고르세요.)", "확인");
            return;
        }

        string modsRoot = Path.Combine(Application.streamingAssetsPath, "Mods");
        Directory.CreateDirectory(modsRoot);

        string outDir = EditorUtility.SaveFolderPanel("번들을 넣을 모드 폴더", modsRoot, "");
        if (string.IsNullOrEmpty(outDir)) return;
        Directory.CreateDirectory(outDir);

        // 번들 이름 = 모드 폴더 이름. 모드마다 하나씩이라 이름이 겹치지 않고,
        // 다시 구우면 같은 파일을 덮어써 번들이 쌓이지 않는다.
        string bundleName = new DirectoryInfo(outDir).Name.ToLowerInvariant() + ".bundle";

        var build = new AssetBundleBuild
        {
            assetBundleName = bundleName,
            assetNames      = paths,
        };

        // `LoadFromFile`로 읽으므로 **청크 압축(LZ4)** 이 맞다 — 기본 LZMA는 통째로 풀어야 해서
        // 로딩이 눈에 띄게 느려진다.
        var manifest = BuildPipeline.BuildAssetBundles(
            outDir,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget);

        if (manifest == null)
        {
            Debug.LogError($"[ModBundleBuilder] 번들 굽기 실패 — 콘솔의 앞선 오류를 보세요. (출력: {outDir})");
            return;
        }

        AssetDatabase.Refresh();

        var names = new List<string>();
        foreach (var p in paths) names.Add(Path.GetFileNameWithoutExtension(p));

        Debug.Log(
            $"[ModBundleBuilder] {bundleName} 완성 ({paths.Length}개) → {outDir}\n" +
            $"  JSON의 modelPath/iconPath에 적을 이름: {string.Join(", ", names)}\n" +
            $"  대상 플랫폼: {EditorUserBuildSettings.activeBuildTarget} · 유니티 {Application.unityVersion}\n" +
            "  번들은 유니티 버전·플랫폼마다 다릅니다 — 다른 버전에서 구운 번들은 열리지 않습니다.");
    }
}
