using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **건물에 얼굴을 입힌다**(2026-08-28).
//
// 이 저장소의 건물은 대부분 **기본 도형**이다 — 상자에 뚜껑을 얹으면 창고, 납작한 판이면
// 침대. 배선을 막지 않으려고 그렇게 시작한 것이고, 그 판단은 옳았다(모양을 다듬는 것은
// 나중에 모델을 갈아 끼우는 일이다). **지금이 그 나중이다.**
//
// ── 왜 모듈 벽이 아니라 소품인가 ───────────────────────────────────────────
//
// `Fantastic Dungeon Pack`의 **모듈 벽·바닥은 2m 격자에 벽 높이 4m**다(데모 씬의 좌표가
// 전부 2·4의 배수다). 이 게임의 칸은 1m라 **모듈 하나가 두 칸**이고, 그대로 쓰려면 껍데기의
// 벽 길이·문 폭·마스크를 전부 짝수로 다시 저작해야 한다 — 그건 이 팩을 쓰는 일이 아니라
// **격자를 팩에 맞추는 일**이다.
//
// **소품은 그 문제가 없다.** 궤짝도 광맥도 격자를 모르고, 제 발자국 안에 들어가기만 하면
// 된다. 그래서 값이 가장 싸고 화면은 가장 크게 바뀐다.
//
// ── 무엇을 안 건드리나 ─────────────────────────────────────────────────────
//
// **루트를 다시 짓지 않는다** — 프리팹을 새로 만들면 루트 fileID가 바뀌어 씬 인스턴스가
// 오버라이드를 잃는다. **컴포넌트·콜라이더·저작된 자식도 안 건드린다**(`WorkSpot`·눕는
// 자리·트리거 콜라이더가 거기 저작돼 있고, 하나만 빠뜨려도 폰이 비스듬히 눕거나 밭을
// 눌러도 안 열린다).
//
// 하는 일은 둘뿐이다: **옛 메시를 끄고**(지우지 않는다 — 콜라이더가 같은 오브젝트에 붙어
// 있다) **`[Art]` 자식을 하나 붙인다.** 그래서 되돌리기가 `[Art]`를 지우고 렌더러를 켜는
// 것으로 끝나고, 다시 돌려도 같은 결과가 나온다.
public static class PropDresser
{
    // **팩이 셋이고 결이 같다.** 던전 팩만으로는 침대도 버섯도 우물도 없었다 —
    // 형제 팩(`Fantastic Interior Pack`)과 **동굴 전용 팩**(`3DForge/Cave Adventure kit`)이
    // 그 자리를 메운다. 접두사로 가른다: `I/`는 인테리어, `C/`는 동굴, 나머지는 던전.
    //
    // **`ithappy/Fantasy`는 안 쓴다** — 성·집·동물이 있지만 로우폴리 판타지라 **결이 통째로
    // 다르다.** 모양이 맞아도 톤이 어긋나면 *들어맞는 것*이 아니다.
    const string Pack         = "Assets/assetsss/Fantastic Dungeon Pack/prefabs/PROPS";
    const string PackInterior = "Assets/assetsss/Fantastic Interior Pack/prefabs/PROPS";
    const string PackCave     = "Assets/assetsss/3DForge/Cave Adventure kit/Prefabs";

    const string ArtName = "[Art]";

    // 발자국을 꽉 채우지 않는다 — 옆 칸 건물과 모서리가 닿으면 서로 파고든 것처럼 보인다.
    const float FitMargin = 0.85f;

    // 건물 프리팹 → 입힐 소품. **여러 개를 적으면 그중 첫째를 쓴다**(팩에서 이름이
    // 바뀌었을 때 조용히 아무것도 안 하는 대신 다음 것을 잡게).
    struct Dress
    {
        public string Building;    // 프리팹 이름
        public string Folder;      // `3_Prefab/` 아래 폴더. 비우면 `Building`
        public string[] Props;     // 소품 경로(확장자 없이) — 접두사로 팩을 가른다
        public bool    KeepHeight; // 세로는 안 늘린다 — 광맥·버섯처럼 제 비례가 중요한 것

        // **`Props`를 폴백이 아니라 변형 목록으로 읽는다**(2026-09-18) — 전부 굽고
        // 런타임이 **자리 해시로 하나만 켠다**(`PropVariant`). 버섯처럼 *한 종류인데
        // 여러 모습으로 나야 하는 것*의 자리다.
        public bool    Variants;

        // 얹은 소품이 스스로 빛나야 하면 그 색. **원래 프리팹의 발광 재질은 `[Art]`에
        // 덮이면서 같이 꺼지므로**, 안 주면 *빛만 나고 물건은 어두운* 야광 버섯이 된다.
        public Color   Glow;

        // 색을 갈아입힐 때. 팩 크리스탈은 다섯 빛깔뿐인데 **우리 보석은 일곱**이라,
        // 모델 하나를 색만 바꿔 돌려 쓴다(`Glow`와 같이 재질을 복제한다).
        public Color   Tint;

        // **긴 축에 맞춘다 — 짧은 쪽으로 삐져나와도 둔다**(2026-09-18, 사용자: *"비율에
        // 맞춰서 키워줘 콜라이더보다 모델이 그냥 더 나와도 괜찮을 거 같아"*).
        //
        // `Fit`은 본래 **빡빡한 쪽**에 맞춘다 — 발자국 밖으로 안 나가게. 그런데 비율이 다른
        // 모델(3×1 칸에 든 관)은 **짧은 쪽에 걸려 통째로 작아진다.** 켜면 긴 쪽을 채우고,
        // 짧은 쪽은 넘치게 둔다. **비율은 그대로다** — 넘치는 것과 찌그러지는 것은 다르다.
        public bool    FillLong;

        // **가로만 맞추고 높이는 그대로 둔다.** 울타리처럼 *가로로 여러 개가 붙은* 소품을
        // 한 칸에 욱여넣으면 **균등 스케일이라 높이까지 같이 줄어** 납작한 턱이 된다.
        // 켜면 기둥이 **가늘고 촘촘해지는데**, 목책은 그게 맞는 모습이다.
        public bool    KeepTall;

        // 발자국 대비 크기. **0이면 기본값(`FitMargin`)** — 잇대어 쓰는 것(울타리·벽)은
        // **1을 줘야 이어진다**: 0.85로 줄이면 칸마다 15%씩 틈이 벌어져 **하나씩 박힌 것처럼**
        // 보인다(2026-09-18, 사용자: *"목책이 안끊키게 이어져야지 지금은 하나씩 박히잖아"*).
        public float   Margin;
    }

    static readonly Dress[] Table =
    {
        // **정확히 맞는 것만 갈아 끼운다**(2026-09-17, 사용자: *"모델이 정확히 들어맞거나
        // 들어맞는다고 생각되는 것만 바꿔주고 비슷한 모델이 없는건 직접 만든 걸로 넣어 임시로"*).
        //
        // 그래서 **큰 구조물은 손대지 않는다** — 오두막 7×6 · 홀 13×10 · 작업장 11×8 ·
        // 곳간 9×9 · 감방 5×5 · 복도 3×9는 소품 하나로 덮을 수 있는 크기가 아니고, 덮으면
        // 발자국 안이 텅 빈다. 벽·바닥·밭도 그대로다(1m 칸에는 프리미티브가 오히려 정확하다).

        // ── 광맥 — **이름까지 맞는다.** 이 팩을 쓸 이유의 절반이 여기 있다.
        // **광맥은 여기서 뺐다**(2026-09-18, 사용자: *"구리 광석은 왜케 작아진거야 텍스쳐만
        // 가져와 모델링을 바꾸지말고"*).
        //
        // 광맥 모양은 `VeinArt`가 굽는다 — *깎아 만든 바위에 광석이 찔끔 박힌 것*이고 **크기도
        // 거기서 정해진다.** 팩 소품으로 덮으면 그 메시가 통째로 갈리고, `KeepHeight` 때문에
        // 원래 크기로 줄어들어 **작아진 것처럼 보였다.**
        //
        // **가져올 것은 텍스처뿐이다** — `VeinArt.RockMat`/`OreMat`이 팩의
        // `T_PROP_orevein_dungeon_*`을 입는다(`D. Rebuild Vein Art`).

        // ── 보관
        // **큰 창고는 큰 궤짝이다**(2026-09-17, 사용자: *"창고가 상자인 건 이해가 가는데
        // 큰 창고가 바구니인건 좀 그래 … 내가 원하는 건 이런 큰 철제 상자 같은 느낌"*).
        // `crate_dungeon_01`은 **엮은 상자**라 바구니로 읽혔다 — 쇠테를 두른 궤짝으로 옮긴다.
        // (던전 팩은 판타지라 **순수 철제 상자는 없다** — 쇠테 궤짝이 가장 가깝다.)
        new Dress { Building = "Storage",      Props = new[]{ "containers/chests/P_PROP_chest_dungeon_01" } },
        new Dress { Building = "LargeStorage", Props = new[]{ "containers/chests/P_PROP_chest_dungeon_03",
                                                              "containers/chests/P_PROP_chest_dungeon_02" } },

        // ── 거주·장례
        //
        // **화톳불은 여기서 뺐다**(2026-09-05) — `CampfireUpgrader`가 그 생김새를 든다.
        // 둘 다 두면 **같은 물건의 얼굴에 답이 둘**이고, 둘 다 돌리면 `[Art]`가 두 겹으로
        // 선다. 무엇보다 여기 방식(*"옛 메시를 끄고 소품을 얹는다"*)은 **불꽃까지 같이
        // 끈다** — 화톳불에서 불꽃은 장식이 아니라 *켜졌는지를 보여 주는 유일한 표시*다
        // (`LightSource.flames`). 그래서 저쪽은 불꽃을 다시 짓고 배선까지 물린다.
        // **무덤은 돌 석관이다**(2026-09-18, 사용자: *"무덤도 casket_stone_base 이걸로
        // 바꿔주면 좋겠는데"*). 뚜껑은 **따로 얹고**(`casket_stone_lid`) `Grave`가 여닫는다 —
        // 그래서 `Child`로 그 자식을 짚는다: 뚜껑이 `[Art]` 안에 섞여 들어가면 **본체와 같이
        // 움직여** 여닫을 수가 없다.
        // **`KeepHeight`를 안 켠다** — 켜면 *키우지 않는다*라, 관이 원래 작아서 2×1 발자국
        // 한구석에 조그맣게 놓인다(사용자: *"크기도 너무 작아"*). 무덤은 **그 칸을 채우는
        // 물건**이라 키워야 맞다.
        new Dress { Building = "Grave", Margin = 0.95f, FillLong = true,
            Props = new[]{ "C/Props/Grave/casket_stone_base" } },

        // **화장터는 뺐다**(2026-09-17, 사용자: *"화장대 도축대 전혀 맞지 않는 모델링"* ·
        // *"무덤도 내가 원하는건 위에 흙까지 연결된 무덤이야"*).
        //
        // **직접 지은 것이 팩 소품보다 정확했다.** `Grave`는 이미 `Mound`(봉분) + `Kerb`(연석)
        // + `Headstone`(묘비)로 짜여 있었는데 **묘비 하나로 덮어서 흙이 사라졌고**, `Pyre`는
        // `Log0~3` + `Ash`로 쌓은 **장작단**인데 화로(brazier)로 덮으니 *불 피우는 접시*가 됐다.
        //
        // 팩에 **화장단도 봉분도 없다**(흙더미와 묘비가 따로 있을 뿐이라 겹쳐 놓아야 하는데,
        // 그건 *덮는 것*이 아니라 *다시 짓는 것*이다). **없는 것을 비슷한 것으로 덮으면
        // 원래 있던 뜻까지 지운다** — 사용자의 규칙이 그것이고, 여기가 그 예다.

        // **침대 셋 — 1×2라 소품 비례가 그대로 맞는다.** 좋은 침대는 천개가 달린 것으로,
        // 죄수 침대는 가장 초라한 것으로 갈라 **값이 화면에 보이게** 한다.
        new Dress { Building = "Bed",        FillLong = true, Margin = 0.95f, Props = new[]{ "I/Beds/P_PROP_bed_interior_01" } },
        // **2층 침대는 안 쓴다**(2026-09-18, 사용자). 좋은 침대는 *한 사람이 잘 자는 자리*라
        // 이층으로 두면 **한 칸에 둘이 자는 물건**으로 읽히는데, 침대는 폰 하나가 배정된다.
        new Dress { Building = "QualityBed", FillLong = true, Margin = 0.95f, Props = new[]{ "I/Beds/P_PROP_bed_interior_02" } },
        new Dress { Building = "PrisonBed",  FillLong = true, Margin = 0.95f, Props = new[]{ "I/Beds/P_PROP_bed_interior_04" } },

        // ── 수용 — **쇠창살이 그대로 있다.**
        new Dress { Building = "PrisonCage", Props = new[]{ "comps/COMP_PROP_cage_dungeon_01",
                                                            "construction/P_PROP_bars_wall_dungeon_01" } },

        // ── 울타리
        //
        // **`1X1`은 기둥 하나였다**(2026-09-18, 사용자가 화면을 보내 줬다: *"이 여러개로
        // 되어있는 걸 쓰라는 말이 이해가 안되나?"*). 칸마다 하나씩 놓으니 **굵은 말뚝이
        // 띄엄띄엄** 선 그림이었다 — 마진을 1로 채워도 *이어진 울타리*가 아니라 *굵어진 말뚝*이다.
        //
        // `3X5`는 **기둥 여럿이 한 덩어리**라, 한 칸에 넣으면 그 자체로 울타리 한 마디다.
        // **`KeepTall`을 켠다** — 안 켜면 가로를 한 칸에 맞추느라 **키까지 같이 줄어** 납작해진다.
        new Dress { Building = "Palisade", Margin = 1f, KeepTall = true,
            Props = new[]{ "C/Wooden/Fences/cave_fence3X5" } },

        // **바리케이드는 `BarricadeBuilder`가 조립한다** — 팩에 그 물건이 없어서
        // 말뚝을 X자로 엇갈려 세워야 한다. 소품 하나를 얹는 이 방식으로는 못 만든다.

        // ── 물 — 돌을 쌓은 우물. 1×1이라 그대로 맞는다.
        // **프리팹 이름은 `WaterSource`다** — 건물 에셋은 `Water.asset`이라 이름이 갈린다.
        new Dress { Building = "WaterSource", Props = new[]{ "construction/P_PROP_well_dungeon_01" }, KeepHeight = true },

        // ── 뼈 제단 · 마력 충전대
        //
        // 충전대는 **마나 결정**이다 — 충전이라는 일에 결정만큼 맞는 그림이 없고,
        // 동굴 팩의 `Special/Crystals`가 정확히 그것이다(빛나는 재질까지 들어 있다).
        new Dress { Building = "BoneAltar", Props = new[]{ "furniture/P_PROP_altar_dungeon_02",
                                                           "furniture/P_PROP_altar_dungeon_01" }, KeepHeight = true },
        new Dress { Building = "Charger",   Props = new[]{ "C/Special/Crystals/mana/mana_crystal2" }, KeepHeight = true },

        // ── 보석·마법 광물 — **팩 크리스탈을 우리 색으로 입힌다**
        //
        // (2026-09-18, 사용자: *"크리스탈도 있는데 이것도 우리가쓰는 보석으로 쓰기 좋은데"*.)
        //
        // 팩에는 **다섯 빛깔**(fire·ice·life·mana·obsidian)뿐이고 **우리 보석은 일곱**이라,
        // **모델 하나를 색만 바꿔 돌려 쓴다**(`Tint`). 색은 `ResourceNodeData`가 이미 저작한
        // 그 색과 나란히 둔다 — **벽에서와 바닥에서 다르게 읽히면 화면에서 다른 물건이다.**
        //
        // **광맥은 `VeinArt`가 굽는 것이 원칙인데**(구리에서 배웠다) 여기만 예외인 이유:
        // 보석은 `Shape.Crystal`이라 **기둥 다발**이고, 팩 크리스탈이 바로 그 모양이다.
        // 금속 광석처럼 *바위에 박힌 알갱이*가 아니라서 크기도 어긋나지 않는다.
        new Dress { Building = "Node_RubyVein",     Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.86f, 0.16f, 0.22f), Glow = new Color(0.30f, 0.05f, 0.07f),
            Props = new[]{ "C/Special/Crystals/fire/fire_crystal1", "C/Special/Crystals/fire/fire_crystal3" } },

        new Dress { Building = "Node_SapphireVein", Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.20f, 0.38f, 0.90f), Glow = new Color(0.06f, 0.12f, 0.30f),
            Props = new[]{ "C/Special/Crystals/ice/ice_crystal1", "C/Special/Crystals/ice/ice_crystal3" } },

        new Dress { Building = "Node_EmeraldVein",  Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.18f, 0.78f, 0.42f), Glow = new Color(0.05f, 0.24f, 0.13f),
            Props = new[]{ "C/Special/Crystals/life/life_crystal1", "C/Special/Crystals/life/life_crystal3" } },

        new Dress { Building = "Node_TopazVein",    Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.94f, 0.76f, 0.18f), Glow = new Color(0.30f, 0.24f, 0.05f),
            Props = new[]{ "C/Special/Crystals/fire/fire_crystal2", "C/Special/Crystals/fire/fire_crystal4" } },

        // 마법 광물 셋은 **원래도 빛나는 것**이라(`Glow`) 발광을 세게 준다.
        new Dress { Building = "Node_MagicCrystal", Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.55f, 0.35f, 0.95f), Glow = new Color(0.45f, 0.28f, 0.80f),
            Props = new[]{ "C/Special/Crystals/mana/mana_crystal1", "C/Special/Crystals/mana/mana_crystal3" } },

        new Dress { Building = "Node_EtherCrystal", Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.45f, 0.85f, 0.95f), Glow = new Color(0.32f, 0.70f, 0.80f),
            Props = new[]{ "C/Special/Crystals/ice/ice_crystal2", "C/Special/Crystals/ice/ice_crystal4" } },

        new Dress { Building = "Node_VoidCrystal",  Folder = "Node", Variants = true, KeepHeight = true,
            Tint = new Color(0.30f, 0.16f, 0.42f), Glow = new Color(0.34f, 0.10f, 0.52f),
            Props = new[]{ "C/Special/Crystals/obsidian/obsidian_crystal1",
                           "C/Special/Crystals/obsidian/obsidian_crystal3" } },

        // ── 굴에 나는 것 (`3_Prefab/Node`) ──────────────────────────────────
        //
        // **동굴 전용 팩이라 결이 맞는다** — 여기 버섯은 *동굴 버섯*이고, 이 게임의 버섯도
        // 그것이다. `MushroomCreator`가 만든 프리미티브(갓 + 기둥)를 덮는다.
        //
        // **`KeepHeight`를 켠다** — 버섯·풀은 제 비례가 곧 정체다. 발자국에 맞춰 늘리면
        // 갓버섯이 사람만 해진다.
        // **사용자가 모델을 직접 짚어 줬다**(2026-09-18): 양송이는 `1_04`·`1_05`·`2_04`·`2_05`,
        // 버섯 나무는 `4_08`·`4_large`. 빨강과 초록이 **같은 번호로 짝을 이룬다.**
        //
        // 그 전에는 `1_01`·`2_01`을 썼는데 **갓이 반원인 선반버섯**이라, 바닥에 놓으니
        // **반쪽이 땅에 묻힌 것처럼** 보였다(사용자: *"버섯이 잘려있어"*). 같은 폴더 안에서도
        // **번호마다 자라는 자리가 다르다** — 벽에 붙는 것과 바닥에 서는 것이 섞여 있다.

        // 갓버섯 — **균사림의 야광 버섯**(초록 + 발광).
        new Dress { Building = "Node_CapMushroom", Folder = "Node", Variants = true, KeepHeight = true,
            Glow = new Color(0.30f, 0.95f, 0.45f),
            Props = new[]{ "C/Plants/Fungus/fungus_green/cave_mushroom1_04_green",
                           "C/Plants/Fungus/fungus_green/cave_mushroom1_05_green",
                           "C/Plants/Fungus/fungus_green/cave_mushroom2_04_green",
                           "C/Plants/Fungus/fungus_green/cave_mushroom2_05_green" } },

        // 동굴버섯 — **그냥 먹는 양송이.** 빛나지 않는다.
        new Dress { Building = "Node_CaveMushroom", Folder = "Node", Variants = true, KeepHeight = true,
            Props = new[]{ "C/Plants/Fungus/fungus_red/cave_mushroom1_04_red",
                           "C/Plants/Fungus/fungus_red/cave_mushroom1_05_red",
                           "C/Plants/Fungus/fungus_red/cave_mushroom2_04_red",
                           "C/Plants/Fungus/fungus_red/cave_mushroom2_05_red" } },

        // 거대버섯 — **버섯 나무.** 벌목해서 목재를 얻는 것이라 이것만 크다.
        new Dress { Building = "Node_GiantMushroom", Folder = "Node", Variants = true, KeepHeight = true,
            Props = new[]{ "C/Plants/Fungus/fungus_green/cave_mushroom4_large_green",
                           "C/Plants/Fungus/fungus_green/cave_mushroom4_08_green" } },

        // 푸른 곰팡이 — **무리로 난 균사**가 그 그림이다(낱개가 아니라 덩어리).
        new Dress { Building = "Node_BlueMould", Folder = "Node", Variants = true, KeepHeight = true,
            Glow = new Color(0.22f, 0.44f, 0.92f),
            Props = new[]{ "C/Plants/Fungus/v1_fungus/fungus1_cluster",
                           "C/Plants/Fungus/v1_fungus/fungus2_cluster1",
                           "C/Plants/Fungus/v1_fungus/fungus3_cluster",
                           "C/Plants/Fungus/v1_fungus/fungus4_cluster" } },

        // 이끼 — 바닥에 난 풀. 동굴 팩의 작은 양치가 그 자리다.
        new Dress { Building = "Node_MossPatch", Folder = "Node", Props = new[]{ "C/Plants/Ferns/v1_ferns/swordfern1" }, KeepHeight = true },

        // 나무 — 굴에서 자라는 것은 **나무고사리**다(잎 달린 활엽수는 지하에 안 어울린다).
        new Dress { Building = "Node_MossTree", Folder = "Node", Props = new[]{ "C/Plants/Ferns/cave_treefern1_01" }, KeepHeight = true },
        new Dress { Building = "Tree",          Folder = "Node", Props = new[]{ "C/Plants/Ferns/cave_treefern1_02" }, KeepHeight = true },

        // 물먹은 둥치 — **이름부터 둥치(trunk)다.** 가지가 삭아 없어진 그 모습 그대로.
        new Dress { Building = "Node_HardwoodTrunk", Folder = "Node", Props = new[]{ "C/Plants/Ferns/v1_ferns/fern_trunk1" }, KeepHeight = true },

        // 바닥 돌 — 주워 담는 돌무더기.
        new Dress { Building = "Node_Pebbles", Folder = "Node", Props = new[]{ "natural/P_PROP_debris_stones_dungeon_01" }, KeepHeight = true },

        // ── 제작·연구는 여기서 뺐다 (2026-09-17)
        //
        // **등급이 얼굴을 바꾸므로** `BenchArtCreator`가 그 열둘을 든다(`[Art 1]`…`[Art 5]`).
        // 둘 다 두면 **같은 물건의 얼굴에 답이 둘**이고, 둘 다 돌리면 `[Art]`와 `[Art N]`이
        // **두 겹으로 선다** — 화톳불이 `CampfireUpgrader`로 옮겨 간 것과 같은 자리다.
        //
        // (저쪽이 `[Art]`도 같이 걷으므로, 이 표에 되돌려 놓으면 서로를 지우기만 한다.)
        //
        // ── 손 안 댄 것과 그 이유 ────────────────────────────────────────────
        //
        // **벽걸이 횃불**(`WallTorch`) — 놓인 자리에 따라 **모양이 둘**이고(선 것 / 걸린 것)
        // `IPlacementPose`가 그중 하나를 켠다. 여기 방식은 *옛 렌더러를 전부 끄고 소품 하나를
        // 얹는 것*이라 **그 두 모양이 통째로 사라진다.** 동굴 팩에 `torchholder_with_torch_small`이
        // 그대로 있으니 옮길 값은 충분하지만, **모양 둘을 다루는 규격이 먼저 필요하다.**
        //
        // **던전 핵·서브코어** — 이미 외부 메시를 쓰고 있다(프리미티브가 아니다).
        //
        // **밭·온실·수로** — 밭은 *데이터로 깔리는 평면*이라 얹을 것이 없고, 온실과 수로는
        // 팩에 대응하는 물건이 없다. **없는 것을 비슷한 것으로 덮으면 무엇인지 흐려진다.**
    };

    [MenuItem("DungeonColony/Building/1. Dress Buildings (모델 입히기)")]
    static void Run()
    {
        int done = 0, missing = 0;
        var notes = new List<string>();

        // **표에서 뺀 것은 여기서 되돌린다**(2026-09-18, 사용자: *"나무 바리게이트가 원래
        // 모델로 안바뀌었어"*).
        //
        // 표에서 한 줄을 지워도 **이미 입혀 둔 `[Art]`는 프리팹에 그대로 남는다** — 도구는
        // 그 프리팹을 아예 안 보기 때문이다. 그래서 *빼는 것*이 화면에서 아무 일도 안 하고,
        // 되돌리려면 `2. Undress`를 따로 눌러야 했다(그 사실을 아는 사람만).
        //
        // **표가 답이라면 표에 없는 것도 표가 답해야 한다.**
        int stripped = StripUnlisted(notes);

        foreach (var d in Table)
        {
            string path = PathOf(d);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"건물 없음 — {d.Building}");
                missing++;
                continue;
            }

            var prop = FindProp(d.Props);
            if (prop == null)
            {
                notes.Add($"소품 없음 — {d.Building} ({string.Join(" / ", d.Props)})");
                missing++;
                continue;
            }

            if (Dressing(path, d, prop)) { done++; notes.Add($"{d.Building} ← {prop.name}"); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // **못 찾은 것은 경고로 올린다**(2026-09-17). 전에는 `Debug.Log` 한 줄에 섞여
        // 들어가서, 광맥 넷이 **잘못된 폴더를 보느라 한 번도 안 입혀진 채로 늙었다** —
        // `VeinArt`가 따로 메시를 구워 화면에는 멀쩡해 보였기 때문에 더 안 보였다.
        // **세기만 하고 넘어가면 그런 줄이 조용히 늙는다.**
        string report = $"[모델] {done}채에 얼굴을 입혔다(못 찾은 것 {missing} · 되돌린 것 {stripped}).\n  "
                      + string.Join("\n  ", notes)
                      + "\n되돌리려면 프리팹에서 `[Art]` 자식을 지우고 꺼진 렌더러를 켜면 된다.";

        if (missing > 0) Debug.LogWarning(report);
        else             Debug.Log(report);
    }

    // ── 되돌리기 ────────────────────────────────────────────────────────────
    //
    // **git이 안전망이 못 된다** — 광맥 넷과 창고는 아직 미커밋이라(2026-08-28 확인)
    // 되돌릴 옛 판이 없다. 그래서 되돌리는 길을 도구가 들고 있어야 한다.
    //
    // 정확히 반대로 한다: `[Art]`를 지우고 렌더러를 켠다. **입힐 때 꺼진 렌더러가 하나도
    // 없었으므로**(그것을 `Run`이 확인하고 경고한다) *"전부 켠다"*가 곧 원래대로다.
    [MenuItem("DungeonColony/Building/2. Undress Buildings (원래대로)")]
    static void Undo()
    {
        // **표를 안 본다 — 폴더를 훑는다**(2026-09-17).
        //
        // 표에서 한 줄을 빼는 순간 그 프리팹은 **되돌릴 길이 없어진다** — 이미 입혀 둔
        // 얼굴이 그대로 굳는다(무덤과 화장터를 표에서 뺄 때 실제로 그럴 뻔했다).
        // 되돌리기가 묻는 것은 *"표에 있나"*가 아니라 **"`[Art]`가 붙어 있나"**다.
        int done = 0;
        foreach (var folder in new[] { "Building", "Node" })
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { $"Assets/3_Prefab/{folder}" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art = root.transform.Find(ArtName);
                if (art == null) continue;          // 안 입힌 것은 안 건드린다

                Object.DestroyImmediate(art.gameObject);
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    r.enabled = true;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[모델] {done}채를 원래 도형으로 되돌렸다.");
    }

    // 표에 없는데 얼굴이 붙어 있는 프리팹을 원래대로 되돌린다.
    static int StripUnlisted(List<string> notes)
    {
        var listed = new HashSet<string>();
        foreach (var d in Table) listed.Add(PathOf(d));

        int n = 0;
        foreach (var folder in new[] { "Building", "Node" })
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { $"Assets/3_Prefab/{folder}" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (listed.Contains(path)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool touched = false;
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var c = root.transform.GetChild(i);
                    if (c.name != ArtName && !c.name.StartsWith(PropVariant.Prefix)) continue;
                    Object.DestroyImmediate(c.gameObject);
                    touched = true;
                }
                if (!touched) continue;

                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;

                var pv = root.GetComponent<PropVariant>();
                if (pv != null) Object.DestroyImmediate(pv);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                notes.Add($"{root.name} — 표에 없어 원래대로 되돌림");
                n++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return n;
    }

    static GameObject FindProp(string[] rel)
    {
        foreach (var r in rel)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Resolve(r));
            if (go != null) return go;
        }
        return null;
    }

    static string Resolve(string rel)
    {
        string path = rel.StartsWith("Assets/") ? rel
                    : rel.StartsWith("I/")      ? $"{PackInterior}/{rel.Substring(2)}"
                    : rel.StartsWith("C/")      ? $"{PackCave}/{rel.Substring(2)}"
                                                : $"{Pack}/{rel}";
        return path.EndsWith(".prefab") ? path : path + ".prefab";
    }

    // 프리팹이 어느 폴더에 있나 — 노드도 얼굴을 입으므로 `Building` 하나로는 모자란다.
    static string PathOf(Dress d) =>
        $"Assets/3_Prefab/{(string.IsNullOrEmpty(d.Folder) ? "Building" : d.Folder)}/{d.Building}.prefab";

    // ── 한 채 입히기 ────────────────────────────────────────────────────────

    static bool Dressing(string path, Dress d, GameObject first)
    {
        bool keepHeight = d.KeepHeight;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // 다시 돌려도 같은 결과가 나오게 — 먼저 지난번 것을 걷는다.
            var old = root.transform.Find(ArtName);
            bool redress = old != null;
            if (redress) Object.DestroyImmediate(old.gameObject);

            // **되돌리기가 "전부 켠다"라서** 원래 꺼져 있던 렌더러가 있으면 그것까지
            // 켜진다. 조용히 그러지 않도록 여기서 알린다(처음 입힐 때만 — 다시 입히는
            // 것이면 꺼져 있는 게 정상이다).
            if (!redress)
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    if (!r.enabled)
                        Debug.LogWarning($"[모델] `{root.name}`의 `{r.name}` 렌더러가 원래 꺼져 있다 — " +
                                         "`3. Undress`로 되돌리면 그것도 같이 켜진다.");

            // **옛 메시는 끄기만 한다.** 지우면 같은 오브젝트에 붙은 콜라이더까지 사라져
            // 클릭도 몸도 안 걸린다(그 콜라이더가 발자국 판정과 짝을 이룬다).
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                r.enabled = false;

            // 지난번 변형도 걷는다(다시 돌려도 같은 결과가 나오게).
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var c0 = root.transform.GetChild(i);
                if (c0.name.StartsWith(PropVariant.Prefix)) Object.DestroyImmediate(c0.gameObject);
            }

            // **변형이면 `Props`를 전부 굽는다** — 런타임이 자리 해시로 하나만 켠다.
            // 아니면 첫째 하나를 `[Art]`로 얹는다(예전 그대로).
            if (d.Variants)
            {
                int n = 0;
                foreach (var rel in d.Props)
                {
                    var one = LoadProp(rel);
                    if (one == null) continue;

                    var v = Place(root, one, $"{PropVariant.Prefix}{n}]", keepHeight, d.Glow, d.Tint, d.Margin, d.KeepTall, d.FillLong);
                    if (v == null) continue;

                    // **0번만 켜 둔다** — `PropVariant`가 붙기 전 한 프레임 동안 전부
                    // 겹쳐 보이지 않게(그리고 프리팹을 열었을 때 하나만 보이게).
                    v.SetActive(n == 0);
                    n++;
                }
                if (n == 0) return false;

                if (root.GetComponent<PropVariant>() == null) root.AddComponent<PropVariant>();
            }
            else
            {
                if (Place(root, first, ArtName, keepHeight, d.Glow, d.Tint, d.Margin, d.KeepTall, d.FillLong) == null) return false;

                // 변형이 아니면 그 컴포넌트는 없어야 한다 — 표를 고쳐 변형을 끈 날
                // **켜진 것 하나만 남고 나머지는 사라졌는데 컴포넌트가 계속 도는** 꼴이 된다.
                var stale = root.GetComponent<PropVariant>();
                if (stale != null) Object.DestroyImmediate(stale);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // 소품 하나를 얹는다 — 이름만 다르고 나머지는 같다(`[Art]` 또는 `[Var n]`).
    static GameObject Place(GameObject root, GameObject prop, string name, bool keepHeight,
                            Color glow, Color tint, float margin, bool keepTall, bool fillLong)
    {
        if (prop == null) return null;

        var art = (GameObject)PrefabUtility.InstantiatePrefab(prop, root.transform);
        if (art == null) return null;

        art.name = name;
        art.transform.localPosition = Vector3.zero;
        art.transform.localRotation = Quaternion.identity;
        art.transform.localScale    = Vector3.one;

        // **소품의 콜라이더는 뗀다** — 길찾기가 보는 것은 타일 점유지 콜라이더가
        // 아니고(`obstacleLayer`는 `Wall`뿐), 남겨 두면 지도는 "빈 칸"이라 하는데
        // 몸이 부딪혀 폰이 처박힌다. 건물의 콜라이더는 이미 루트 쪽에 있다.
        foreach (var c in art.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        Fit(root, art, keepHeight, margin, keepTall, fillLong);
        if (glow.maxColorComponent > 0.001f || tint.maxColorComponent > 0.001f) Recolor(art, glow, tint);
        return art;
    }

    // **얹은 소품을 스스로 빛나게 한다.**
    //
    // 원래 프리팹의 발광 재질은 `[Art]`에 덮이면서 **렌더러째 꺼지므로**, 안 주면
    // *빛만 나고 물건은 어두운* 야광 버섯이 된다(`LightSource`는 남아 있다 — 그건 **둘레를
    // 밝히는 것**이고 여기는 **그 물건이 빛나 보이는 것**이라 다른 축이다).
    //
    // **재질을 복제한다** — 팩 재질을 그대로 건드리면 **그 소품을 쓰는 모든 것이 같이 빛난다.**
    static void Recolor(GameObject art, Color glow, Color tint)
    {
        foreach (var r in art.GetComponentsInChildren<MeshRenderer>(true))
        {
            var src = r.sharedMaterial;
            if (src == null) continue;

            string dir  = "Assets/2_Material/Glow";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/2_Material", "Glow");

            // **색을 이름에 넣는다** — 같은 소품을 다른 색으로 쓰는 날
            // **먼저 만든 쪽이 뒤엣것까지 물들인다**(`VeinArt.OreMat`이 겪은 그 자리).
            string key  = ColorUtility.ToHtmlStringRGB(tint) + "_" + ColorUtility.ToHtmlStringRGB(glow);
            string path = $"{dir}/{src.name}_{key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(src);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(src);

            if (tint.maxColorComponent > 0.001f)
            {
                m.SetColor("_BaseColor", tint);
                m.SetColor("_Color",     tint);
            }

            if (glow.maxColorComponent > 0.001f)
            {
                m.SetColor("_EmissionColor", glow);
                m.EnableKeyword("_EMISSION");   // **URP는 키워드를 같이 켜야 실제로 빛난다**
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);

            r.sharedMaterial = m;
        }
    }

    static GameObject LoadProp(string rel) =>
        AssetDatabase.LoadAssetAtPath<GameObject>(Resolve(rel));

    // 소품을 그 건물의 발자국에 맞춘다.
    //
    // **크기는 `BuildingData.Size`가 답한다** — 옛 큐브의 크기를 재서 맞추면 그 큐브가
    // 발자국과 어긋나 있었을 때 그 실수를 그대로 물려받는다. 데이터가 진짜다.
    static void Fit(GameObject root, GameObject art, bool keepHeight, float margin = 0f,
                    bool keepTall = false, bool fillLong = false)
    {
        var pb = root.GetComponent<PlacedBuilding>();
        var size = pb != null && pb.Data != null ? pb.Data.Size : Vector2Int.one;
        float m = margin > 0.001f ? margin : FitMargin;
        float wantX = Mathf.Max(1, size.x) * m;
        float wantZ = Mathf.Max(1, size.y) * m;

        if (!Bounds(art, out var b) || b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;

        // ── 긴 축을 맞춘다 (2026-09-18) ──────────────────────────────────────
        //
        // **모델이 어느 축으로 긴지 우리는 모른다.** 팩마다 다르고, 같은 팩 안에서도
        // 갈린다 — 관은 Z로 길고 울타리는 X로 길 수 있다. 그걸 모른 채 크기만 맞추면
        // **2×1 발자국에 관이 가로로 눕는다**(사용자: *"무덤 방향도 반대고"*).
        //
        // 그래서 **재서 돌린다**: 모델의 긴 축이 발자국의 긴 축과 어긋나면 90도.
        // 1×1이면 발자국에 긴 축이 없으므로 **모델을 X축이 길도록 정규화**한다 — 그래야
        // 드래그 회전(`BuildModeManager.DragYaw`: 가로 0 · 세로 90)이 말이 된다.
        bool propTall = b.size.z > b.size.x * 1.15f;   // 모델이 Z로 길다
        bool slotTall = size.y > size.x * 1.15f;       // 발자국이 세로로 길다

        // 발자국이 세로로 길면 **모델도 Z로 길어야** 하고, 그 밖(가로로 길거나 1×1)에서는
        // **X로 길어야** 한다. 어긋나면 돌린다.
        if (slotTall ? !propTall : propTall)
        {
            art.transform.localRotation *= Quaternion.Euler(0f, 90f, 0f);
            if (!Bounds(art, out b) || b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;
        }

        // **가로세로 중 더 빡빡한 쪽에 맞춰 균등하게 줄인다** — 축마다 따로 늘이면
        // 모델이 찌그러지고, 그러면 모델을 바꾼 뜻이 없다.
        // **긴 축을 채우거나(넘쳐도 둔다) 빡빡한 쪽에 맞추거나** — 비율은 어느 쪽이든 그대로다.
        float s = fillLong
                ? Mathf.Max(wantX / b.size.x, wantZ / b.size.z)
                : Mathf.Min(wantX / b.size.x, wantZ / b.size.z);

        // 세로를 지키는 것들(광맥·불)은 **키우지는 않는다** — 발자국보다 큰 모델을 줄이는
        // 것은 맞지만, 작은 모델을 늘리면 1×1 화톳불이 사람보다 커진다.
        if (keepHeight) s = Mathf.Min(s, 1f);

        // **높이를 지키면 세로만 1로 되돌린다** — 가로로 긴 소품(울타리)을 한 칸에 맞추면
        // 균등 스케일이라 **키까지 같이 줄어** 납작해진다. 찌그러지는 것을 감수하는 자리이고,
        // 울타리는 *가늘고 촘촘한 기둥*이 오히려 맞는 모습이다.
        art.transform.localScale = keepTall ? new Vector3(s, 1f, s) : Vector3.one * s;

        // 바닥에 발을 붙이고 발자국 한복판에 놓는다 — 소품의 피벗은 팩마다 다르다.
        if (!Bounds(art, out b)) return;
        var c = b.center;
        art.transform.localPosition += new Vector3(-c.x, -b.min.y, -c.z);
    }

    static bool Bounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!found) { bounds = r.bounds; found = true; }
            else        bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
