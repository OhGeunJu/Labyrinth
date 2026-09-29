using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **저절로 나는 무리의 초안을 바이옴 표에 넣는다**(2026-09-28, `WildSpawner`).
//
// **무엇이 어디 사는가는 사용자가 정한다** — 여기 적은 것은 *보고 고칠 수 있는 첫 줄*이다. 그래서
// **이미 적힌 줄은 안 건드린다**(같은 무리가 있으면 건너뛴다) — 다시 돌려도 손본 값이 안 날아간다.
//
// 초안의 뜻: 얕은 굴(1층)에는 **짐승**(늑대·곰)이 흔하고 고블린·산적이 드물다 · 늪·빙하에도 짐승 ·
// 납골당·심연에는 **망자** · 깊은 곳(수정·용암·심연)에는 **교단**. 지상 바이옴(숲·평원…)은 안 건드린다.
public static class WildSpawnCreator
{
    const string BiomeDir = "Assets/Resources/Data/Biomes";
    const string BandDir  = "Assets/5_Data/World/Band";

    [MenuItem("DungeonColony/World/L. Wire Wild Spawns (저절로 나는 무리 · 늑대 · 곰)")]
    static void Run()
    {
        // **짐승 자리에는 개미 떼가 먼저 선다**(2026-09-29, 사용자: *"개미만 넣어봐"*) — 있으면 개미, 없으면 옛 짐승.
        var beasts  = AssetDatabase.LoadAssetAtPath<WorldBandData>($"{BandDir}/Band_Ants.asset") ?? Band("Band_Beasts");
        var goblins = Band("Band_Goblin_Raiders");
        var bandits = Band("Band_Bandits");
        var undead  = Band("Band_Undead");
        var cult    = Band("Band_Cult");

        var plan = new Dictionary<Biome, (WorldBandData band, float chance)[]>
        {
            { Biome.Cavern,     new[] { (beasts, 0.10f), (goblins, 0.05f) } },
            { Biome.Limestone,  new[] { (beasts, 0.10f) } },
            { Biome.Mossgrotto, new[] { (beasts, 0.12f) } },
            { Biome.Fungal,     new[] { (beasts, 0.08f), (goblins, 0.05f) } },
            { Biome.Mine,       new[] { (bandits, 0.08f), (goblins, 0.04f) } },
            { Biome.Swamp,      new[] { (beasts, 0.10f), (goblins, 0.06f) } },
            { Biome.Desert,     new[] { (beasts, 0.06f), (bandits, 0.06f) } },
            { Biome.Glacier,    new[] { (beasts, 0.08f) } },
            { Biome.Ossuary,    new[] { (undead, 0.12f) } },
            { Biome.Crystal,    new[] { (cult, 0.06f) } },
            { Biome.Lava,       new[] { (cult, 0.06f) } },
            { Biome.Abyss,      new[] { (undead, 0.10f), (cult, 0.06f) } },
        };

        int added = 0, biomes = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:BiomeContentData", new[] { BiomeDir }))
        {
            var b = AssetDatabase.LoadAssetAtPath<BiomeContentData>(AssetDatabase.GUIDToAssetPath(guid));
            if (b == null || !plan.TryGetValue(b.Biome, out var rows)) continue;

            b.WildSpawns ??= new List<BiomeContentData.WildSpawnEntry>();
            bool dirty = false;
            foreach (var (band, chance) in rows)
            {
                if (band == null) continue;
                if (b.WildSpawns.Exists(e => e != null && e.Band == band)) continue;   // 손본 줄은 그대로

                b.WildSpawns.Add(new BiomeContentData.WildSpawnEntry
                    { Band = band, ChancePerChunk = chance, RespawnDays = 3f });
                added++;
                dirty = true;
            }
            if (dirty) { EditorUtility.SetDirty(b); biomes++; }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[야생] 바이옴 {biomes}곳에 저절로 나는 무리 {added}줄을 넣었습니다. **이미 적힌 줄은 안 건드렸습니다.**\n" +
                  $"무엇이 어디 사는가는 `{BiomeDir}`의 각 바이옴 → **저절로 나는 무리** 목록에서 고칩니다 " +
                  "(청크당 확률 · 다시 나는 날수). 무리 구성은 `" + BandDir + "`의 무리 에셋이 든다.");
    }

    static WorldBandData Band(string name)
    {
        var d = AssetDatabase.LoadAssetAtPath<WorldBandData>($"{BandDir}/{name}.asset");
        if (d == null) Debug.LogWarning($"[야생] 무리 에셋 `{name}`을 못 찾았습니다 — 그 줄은 건너뜁니다.");
        return d;
    }
}
