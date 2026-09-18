using UnityEditor;

// **얼굴 그림 폴더를 지켜본다 — 넣고 빼면 아틀라스를 저절로 다시 굽는다**
// (2026-09-18, 사용자: *"이미지 다시 받아왔는데 자동으로 보이는 거 아니었어? 안보이네"*).
//
// 아틀라스는 **구운 것**이라 그림을 폴더에 넣는 것만으로는 안 바뀐다 — 메뉴를 눌러야 했고,
// 누르는 줄을 모르면 *"넣었는데 안 보인다"*가 된다. 폴더를 지켜보다 PNG가 들어오거나·빠지거나·
// 옮겨지면 다음 틱에 다시 굽는다.
//
// **다음 틱에 굽는다**(`delayCall`) — 가져오기 콜백 안에서 에셋을 또 만들면 가져오기가 다시 돌아
// 되부름이 된다. 한 번에 여러 장이 들어와도 **한 번만** 굽는다(`_pending`).
// 아틀라스 자신은 `2_Material/Chibi`에 있어 여기 안 걸린다(안 그러면 굽기가 굽기를 부른다).
class ChibiFaceAtlasWatcher : AssetPostprocessor
{
    const string Watched = "Assets/assetsss/face/";
    static bool _pending;

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (_pending) return;
        if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom)) return;

        _pending = true;
        EditorApplication.delayCall += () =>
        {
            _pending = false;
            ChibiPawnCreator.BuildFaceAtlas();
        };
    }

    static bool Touches(string[] paths)
    {
        foreach (var p in paths)
            if (p.StartsWith(Watched) && p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
