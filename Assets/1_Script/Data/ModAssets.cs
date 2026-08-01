using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 모드가 **자기 폴더의 그림·모델**을 쓸 수 있게 하는 자산 해석기.
//
// 무엇이 나빴나: `iconPath`가 `Resources.Load` 하나뿐이었다. 그건 **빌드에 이미 들어 있는 것**만
// 가리킨다 — 모드가 새 PNG를 넣을 자리가 아예 없었고, 모델은 경로 필드조차 없었다.
// 그래서 지금 구조는 "숫자만 바꾸는 모드"까지만 되고 "내용물을 추가하는 모드"는 막혀 있었다.
//
// 해석 순서 — 경로 하나를 세 곳에서 차례로 시도한다:
//   1. **모드 폴더의 파일**  그림은 `.png/.jpg`, 모델은 `.obj`(+`.mtl`) — 유니티 없이 그냥 넣으면 된다
//   2. **에셋 번들**         모드 폴더의 `*.bundle` 안 에셋. 프리팹·애니메이션 같은 고급 자산용
//   3. **Resources**         예전 동작(빌드에 들어 있는 기본 콘텐츠) — 기존 콘텐츠 호환
//
// **1번이 모드 구조의 본체다.** 번들만 있으면 모드 제작자가 유니티를 설치하고 정확히 같은 버전으로
// 구워야 하니 "폴더를 넣으면 된다"가 모델에서만 깨진다. `.fbx`는 런타임에 해석할 방법이 없지만
// `.obj`는 순수 텍스트라 읽을 수 있다(`ObjModel`). 그래서 `.fbx`를 넣은 흔한 실수는 아래에서
// 이름으로 알아채 "obj로 내보내라"고 알려 준다.
public static class ModAssets
{
    // 확장자를 생략해도 찾아 준다 — `"icons/pickaxe"`와 `"icons/pickaxe.png"` 둘 다 통한다.
    // 빈 문자열이 먼저인 이유: 이미 확장자가 붙어 있으면 그대로 맞아떨어져야 한다.
    static readonly string[] ImageExtensions = { "", ".png", ".jpg", ".jpeg" };
    static readonly string[] ObjExtensions   = { "", ".obj" };

    // 런타임에 **해석할 방법이 없는** 모델 형식. 찾기용이 아니라 "이래서 안 된다"고 알려주려는 목록이다.
    // 확장자를 안 적었을 수도 있어 탐색용 배열을 따로 둔다(`""` = 경로에 이미 붙어 있는 경우).
    static readonly string[] OpaqueProbe = { "", ".fbx", ".blend", ".dae", ".gltf", ".glb", ".3ds", ".max" };
    static readonly HashSet<string> OpaqueExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".fbx", ".blend", ".dae", ".gltf", ".glb", ".3ds", ".max" };

    // 캐시 키는 `루트|경로` — "이 요청"의 메모다. 실패(null)도 캐시한다.
    // 100개 아이템이 같은 그림을 가리켜도 파일은 한 번만 읽고, 없는 경로를 매번 다시 훑지 않는다.
    static readonly Dictionary<string, Sprite>     _sprites  = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, GameObject> _prefabs  = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Texture2D>  _textures = new(StringComparer.OrdinalIgnoreCase);

    // **우리가 만든** 텍스처·스프라이트·메시·머티리얼. 에셋이 아니라 런타임 생성물이라 직접 치워야 한다.
    // (번들·Resources에서 온 것은 여기 넣지 않는다 — 우리 것이 아니라 지우면 안 된다.)
    static readonly List<UnityEngine.Object> _created = new();

    static readonly Dictionary<string, List<AssetBundle>> _bundlesByRoot = new(StringComparer.OrdinalIgnoreCase);

    static GameObject _templateRoot;

    // 에디터에서 Play를 재시작할 때 static 상태를 되돌린다(`DataManager.Reset`과 같은 시점).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        // **번들은 반드시 내려야 한다.** 안 내리면 다음 Play에서 같은 파일을 다시 열 때
        // "같은 파일의 번들이 이미 로드됨"으로 실패하고, 그 모드의 모델이 통째로 사라진다.
        foreach (var list in _bundlesByRoot.Values)
            foreach (var bundle in list)
                if (bundle != null) bundle.Unload(true);
        _bundlesByRoot.Clear();

        foreach (var obj in _created)
            if (obj != null) UnityEngine.Object.Destroy(obj);
        _created.Clear();

        _templateRoot = null;
        _sprites.Clear();
        _prefabs.Clear();
        _textures.Clear();
    }

    // ── 공개 API ──────────────────────────────────────────────────────────
    // `root`는 그 경로를 적어 낸 **콘텐츠 폴더**(모드 폴더 또는 StreamingAssets/Data).
    // null이면 Resources만 본다 — 즉 예전과 똑같이 동작한다.

    public static Sprite LoadSprite(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim();

        string key = (root ?? "") + "|" + path;
        if (_sprites.TryGetValue(key, out var cached)) return cached;

        var sprite = LoadSpriteUncached(path, root);
        _sprites[key] = sprite;
        return sprite;
    }

    public static GameObject LoadPrefab(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim();

        string key = (root ?? "") + "|" + path;
        if (_prefabs.TryGetValue(key, out var cached)) return cached;

        var prefab = LoadPrefabUncached(path, root);
        _prefabs[key] = prefab;
        return prefab;
    }

    // ── 그림 ──────────────────────────────────────────────────────────────

    static Sprite LoadSpriteUncached(string path, string root)
    {
        string file = ResolveFile(root, path, ImageExtensions);
        if (file != null)
        {
            var tex = LoadTextureFile(file);
            if (tex != null) return ToSprite(tex);
        }

        foreach (var bundle in Bundles(root))
        {
            var s = bundle.LoadAsset<Sprite>(path);
            if (s != null) return s;

            // 번들에 스프라이트가 아니라 텍스처로만 들어 있을 수 있다(임포트 설정에 달렸다).
            var t = bundle.LoadAsset<Texture2D>(path);
            if (t != null) return ToSprite(t);
        }

        var res = Resources.Load<Sprite>(path);
        if (res != null) return res;

        Debug.LogWarning($"[ModAssets] 그림 '{path}'을(를) 찾지 못했습니다{RootHint(root)}");
        return null;
    }

    // 파일 경로 → 텍스처. **파일 단위로 캐시한다** — 모델의 `map_Kd`와 아이콘이 같은 PNG를
    // 가리키는 경우가 흔하고, 그때 텍스처가 둘로 늘어날 이유가 없다.
    static Texture2D LoadTextureFile(string file)
    {
        if (_textures.TryGetValue(file, out var cached)) return cached;

        Texture2D tex = null;
        try
        {
            // 2×2는 자리표시자다 — `LoadImage`가 실제 크기로 다시 잡는다.
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (tex.LoadImage(File.ReadAllBytes(file)))
            {
                tex.name       = Path.GetFileNameWithoutExtension(file);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode   = TextureWrapMode.Repeat;   // 모델 UV가 1을 넘어갈 수 있다
                _created.Add(tex);
            }
            else
            {
                UnityEngine.Object.Destroy(tex);
                tex = null;
                Debug.LogWarning($"[ModAssets] 그림을 해독하지 못했습니다: {file} — PNG/JPG만 됩니다.");
            }
        }
        catch (Exception e)
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
            tex = null;
            Debug.LogWarning($"[ModAssets] 그림 읽기 실패: {file}\n{e.Message}");
        }

        _textures[file] = tex;
        return tex;
    }

    static Sprite ToSprite(Texture2D tex)
    {
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = tex.name;
        _created.Add(sprite);
        return sprite;
    }

    // ── 모델 ──────────────────────────────────────────────────────────────

    static GameObject LoadPrefabUncached(string path, string root)
    {
        // 1) 모드 폴더의 .obj — **유니티 없이 넣을 수 있는 유일한 모델 경로**라 제일 먼저 본다.
        string objFile = ResolveFile(root, path, ObjExtensions);
        if (objFile != null && objFile.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
        {
            string dir = Path.GetDirectoryName(objFile);
            var go = ObjModel.Load(objFile, texName => ResolveTexture(texName, dir, root));
            if (go != null) return AsTemplate(go);
        }

        // 2) 에셋 번들 — 프리팹·애니메이션 등 OBJ로 표현 못 하는 것들.
        foreach (var bundle in Bundles(root))
        {
            var asset = bundle.LoadAsset<GameObject>(path);
            if (asset != null) return asset;
        }

        // 3) Resources
        var res = Resources.Load<GameObject>(path);
        if (res != null) return res;

        // 흔한 실패를 이름으로 잡아 준다 — .fbx를 그냥 폴더에 넣은 경우.
        // 그냥 "못 찾음"이라고만 하면 파일이 눈앞에 있으니 원인을 못 찾는다.
        string opaque = ResolveFile(root, path, OpaqueProbe);
        if (opaque != null && OpaqueExtensions.Contains(Path.GetExtension(opaque)))
            Debug.LogWarning(
                $"[ModAssets] '{Path.GetFileName(opaque)}'은(는) 실행 중에 읽을 수 없습니다. " +
                "**.obj로 내보내면** 그대로 실립니다(같은 폴더의 .mtl·텍스처까지 같이 읽습니다). " +
                "프리팹·애니메이션이 필요하면 에디터에서 `DungeonColony → Build Mod AssetBundle`로 " +
                "번들을 구워 넣으세요.");
        else
            Debug.LogWarning($"[ModAssets] 모델 '{path}'을(를) 찾지 못했습니다{RootHint(root)}");
        return null;
    }

    // MTL의 `map_Kd`가 가리키는 텍스처. **OBJ 파일이 있는 폴더가 먼저** — 3D 툴은 모델 옆에
    // 텍스처를 두고 파일 이름만 적어 내보낸다. 없으면 모드 루트 기준으로 한 번 더 본다.
    static Texture2D ResolveTexture(string name, string objDir, string root)
    {
        // 경계는 모드 폴더(`root`) — 모델이 `../textures/wood.png`처럼 적어도 풀리되 모드 밖으로는 못 나간다.
        string file = ResolveFile(objDir, name, ImageExtensions, root)
                   ?? ResolveFile(root,   name, ImageExtensions);
        if (file != null) return LoadTextureFile(file);

        Debug.LogWarning($"[ModAssets] 모델 텍스처 '{name}'을(를) 찾지 못했습니다 (기준: {objDir})");
        return null;
    }

    // 런타임에 만든 모델을 **`Instantiate`의 원본**으로 쓸 수 있게 갈무리한다.
    //
    // 함정: 원본이 그냥 씬에 있으면 원점에 모델 하나가 떠 있게 되고, 원본을 꺼 버리면
    // `Instantiate` 사본도 꺼진 채로 나와 **바닥에 떨어진 아이템이 안 보인다.**
    // → **부모만 끄고 원본 자신은 켜 둔다.** 화면에는 안 나오고, 사본은 부모 없이 태어나 정상이다.
    static GameObject AsTemplate(GameObject go)
    {
        if (_templateRoot == null)
        {
            _templateRoot = new GameObject("[ModAssets Templates]");
            _templateRoot.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_templateRoot);
            _created.Add(_templateRoot);
        }
        go.transform.SetParent(_templateRoot.transform, false);

        // 메시·머티리얼도 런타임 생성물이라 Reset에서 같이 지운다.
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            if (mf.sharedMesh != null) _created.Add(mf.sharedMesh);
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var mat in mr.sharedMaterials)
                if (mat != null) _created.Add(mat);

        return go;
    }

    // ── 번들 ──────────────────────────────────────────────────────────────

    static IReadOnlyList<AssetBundle> Bundles(string root)
    {
        if (string.IsNullOrEmpty(root)) return Array.Empty<AssetBundle>();
        if (_bundlesByRoot.TryGetValue(root, out var list)) return list;

        list = new List<AssetBundle>();
        _bundlesByRoot[root] = list;   // 실패해도 등록 — 매번 폴더를 다시 훑지 않는다
        if (!Directory.Exists(root)) return list;

        foreach (var file in Directory.GetFiles(root, "*.bundle", SearchOption.AllDirectories))
        {
            var bundle = AssetBundle.LoadFromFile(file);
            if (bundle != null)
            {
                list.Add(bundle);
                Debug.Log($"[ModAssets] 에셋 번들 로드: {Path.GetFileName(file)} " +
                          $"(에셋 {bundle.GetAllAssetNames().Length}개)");
            }
            else
            {
                // 번들은 **유니티 버전·플랫폼마다 다르다.** 다른 버전에서 구운 번들은 열리지 않는다.
                Debug.LogWarning($"[ModAssets] 에셋 번들을 열지 못했습니다: {file} — " +
                                 "다른 유니티 버전이나 다른 플랫폼에서 구운 번들일 수 있습니다.");
            }
        }
        return list;
    }

    // ── 경로 ──────────────────────────────────────────────────────────────

    // `baseDir` 기준으로 경로를 풀되, **`boundary` 바깥으로는 절대 나가지 않는다**
    // (안 주면 `baseDir` 자신이 경계). 모드가 남의 파일을 읽을 이유가 없다.
    //
    // 둘을 나눈 이유는 모델 텍스처다. 3D 툴은 모델 옆에 텍스처를 두고 파일 이름만 적지만
    // `../textures/wood.png`처럼 적어 내보내기도 한다 — 기준은 **모델 폴더**여야 그게 풀리고,
    // 경계는 **모드 폴더**여야 그런 경로도 막히지 않는다. 하나로 묶으면 둘 중 하나가 깨진다.
    static string ResolveFile(string baseDir, string path, string[] extensions, string boundary = null)
    {
        if (string.IsNullOrEmpty(baseDir) || string.IsNullOrEmpty(path)) return null;

        string limit;
        try
        {
            // 끝에 구분자를 붙여야 `Mods/Foo`가 `Mods/Foobar`를 삼키지 않는다.
            limit = Path.GetFullPath(boundary ?? baseDir)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
        }
        catch { return null; }

        foreach (var ext in extensions)
        {
            string candidate;
            try { candidate = Path.GetFullPath(Path.Combine(baseDir, path + ext)); }
            catch { continue; }

            if (!candidate.StartsWith(limit, StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    static string RootHint(string root) =>
        string.IsNullOrEmpty(root) ? " (Resources에서만 찾았습니다)" : $" (찾은 곳: {root})";
}
