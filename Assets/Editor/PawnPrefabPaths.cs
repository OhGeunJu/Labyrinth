using UnityEditor;

// **폰 프리팹은 종족마다 폴더가 따로다**(2026-11-02, 사용자: *"종족별로 따로 폴더 나눠서 정리해줘"*).
//
// `3_Prefab/Pawn/{종족}/{이름}.prefab` — 사람 · 고블린 · 오크 · 오우거 · 엘프 · 트롤 · 드워프 · 해골 · 골렘 · 짐승. 역할의 체형 사본은 `Human/Body/`.
// **경로를 묻는 문은 여기 하나다** — 도구마다 `3_Prefab/Pawn/…`을 손으로 적어 두면 폴더를 옮기는 날 한 곳만 빠뜨려도 그 도구가
// *"프리팹이 없다"*로 조용히 건너뛴다. 종족은 이름이 답한다(새 종족을 들이면 `RaceOf`에 한 줄).
// 메시(`Mesh/`)는 체형 · 종족이 같이 쓰므로 그대로 둔다.
public static class PawnPrefabPaths
{
    public const string Root = "Assets/3_Prefab/Pawn";

    public static string RaceOf(string name)
    {
        if (name.Contains("Goblin"))   return "Goblin";
        if (name.Contains("Orc"))      return "Orc";
        if (name.Contains("Ogre"))     return "Ogre";
        if (name.Contains("Elf"))      return "Elf";
        if (name.Contains("Troll"))    return "Troll";
        if (name.Contains("Dwarf"))    return "Dwarf";
        if (name.Contains("Fungal"))   return "Fungal";
        if (name.Contains("Skeleton")) return "Skeleton";
        if (name.Contains("Golem"))    return "Golem";
        if (name.EndsWith("_Wolf") || name.EndsWith("_Bear") || name.EndsWith("_Ant")) return "Beast";
        return "Human";
    }

    public static string FolderOf(string name) => $"{Root}/{RaceOf(name)}";
    public static string PathOf(string name)   => $"{FolderOf(name)}/{name}.prefab";

    // 역할의 체형 사본(`ChibiPawnCreator.DressBodies`) — 사람 역할만 있다.
    public const string BodyDir = Root + "/Human/Body";

    // 새 프리팹을 놓기 전에 — 폴더가 없으면 위에서부터 만든다.
    public static void Ensure(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        int cut = folder.LastIndexOf('/');
        string parent = folder.Substring(0, cut);
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, folder.Substring(cut + 1));
    }
}
