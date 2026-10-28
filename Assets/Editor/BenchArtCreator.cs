using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// **등급마다 얼굴을 한 벌씩 굽는다**
// (2026-09-17, 사용자: *"작업대나 연구대 등급에 따라 모델링이 달라지면 좋을 거 같긴해
//  업그레이드하는 느낌이 들어야지"* · *"부품이 붙은걸 모델을 통째로 갈아끼우는게 맞겠지?
//  그리고 크기도 변화가 되도 괜찮을 거 같"*).
//
// ## 부품은 여기서 누적되고, 런타임은 한 벌을 켤 뿐이다
//
// `[Art 3]`은 `[Art 2]`에 소품 하나가 더 얹힌 모습이다 — **그 누적을 굽는 것이 이 도구의
// 일**이고, 런타임(`BenchVisual`)이 하는 것은 **해당 등급 자식 하나만 켜는 것**뿐이다.
// 런타임에 붙이면 *무엇이 붙어 있나*가 상태가 되어 저장·복원·다시 짓기마다 따라다닌다.
//
// ## 얼굴의 답은 한 벌이다
//
// `PropDresser`가 `[Art]`를 얹는데, 그것과 여기를 **둘 다 돌리면 얼굴이 두 겹으로 선다** —
// 화톳불이 `CampfireUpgrader`로 옮겨 가면서 `PropDresser.Table`에서 빠진 것과 **정확히
// 같은 자리**다. 그래서 등급을 가진 작업대 열둘은 저쪽 표에서 뺐고, 여기가 든다.
//
// ## 크기도 등급을 따라간다
//
// 발자국 대비 **0.78 → 0.94**. 꽉 채우지 않는 이유는 옆 칸 건물과 모서리가 닿으면 서로
// 파고든 것처럼 보이기 때문이다(`PropDresser.FitMargin`이 같은 값을 든다).
//
// **맞추는 것은 바탕 소품뿐이다** — 세트 전체에 맞추면 뒤에 책장을 세우는 순간 그 높이에
// 눌려 **탁자가 등급이 오를수록 작아진다.** 얹는 것은 제 크기로 두되 상한만 건다.
//
// **다시 돌려도 된다** — 먼저 `[Art *]`를 걷고 새로 굽는다(guid는 프리팹 것이라 유지된다).
public static class BenchArtCreator
{
    // **팩이 둘이고 결이 같다** — 같은 제작자의 `Fantastic ~ Pack` 형제라 섞어도 어울린다.
    // 던전 팩에는 **용광로도 모루도 없어서**(불은 화로·벽난로·모닥불뿐) 작업대의 정체를
    // 바탕이 말할 수가 없었다 — 제련소가 화로라 사용자에게 *"조리대 같다"*로 읽혔다.
    const string PackDungeon  = "Assets/assetsss/djf/prefabs/PROPS";   // 팩 폴더가 `djf`로 바뀌었다(2026-09-30 확인)
    const string PackInterior = "Assets/assetsss/Fantastic Interior Pack/prefabs/PROPS";

    const string PrefabDir = "Assets/3_Prefab/Building";
    const string Prefix    = "[Art ";

    // 등급별 발자국 대비 크기. **I이 제일 작고 V가 제일 크다.**
    //
    // **폭이 좁으면 나란히 놓고 봐야 겨우 보인다**(2026-09-17, 사용자: *"업그레이드마다
    // 차이가 좀 컸으면 좋겠는데"* — 처음엔 0.78~0.94, 등급당 5%였다). 지금은 I과 V가
    // **36% 차이**라 한 대만 봐도 읽힌다.
    //
    // **0.98을 안 넘는다** — 꽉 채우면 옆 칸 건물과 모서리가 닿아 서로 파고든 것처럼 보인다.
    // 더 키우고 싶으면 **여기가 아니라 뒤에 세우는 것**을 늘릴 것(실루엣은 그쪽이 만든다).
    static readonly float[] FitAt = { 0.72f, 0.79f, 0.86f, 0.92f, 0.98f };

    enum Put { Top, Behind, Side }   // 바탕 위 / 뒤 / 옆

    struct Add
    {
        public string Prop;
        public Put    Where;
        public Add(string prop, Put where) { Prop = prop; Where = where; }
    }

    struct Bench
    {
        public string Building;
        public string Base;        // 바탕 — 등급이 올라도 이것은 그대로다(무슨 작업대인지가 흐려진다)
        public Add[][] Steps;      // II·III·IV·V에서 **여럿씩** 더해진다
    }

    // 표를 짧게 쓰려고 — `T("...")`는 위에, `B("...")`는 뒤에, `S("...")`는 옆에.
    static Add T(string p) => new Add(p, Put.Top);
    static Add B(string p) => new Add(p, Put.Behind);
    static Add S(string p) => new Add(p, Put.Side);
    static Add[] Step(params Add[] a) => a;

    // **작업대의 성격이 무엇이 붙는지를 정한다** — 연금술대에 뼈가 쌓이면 도축대와 구별이
    // 안 되고, 그러면 얼굴을 바꾼 뜻이 없다(등급은 읽히는데 *무슨 작업대인지*가 흐려진다).
    static readonly Bench[] Table =
    {
        // **바탕을 비우면 직접 지은 도형을 쓴다**(2026-09-17, 사용자: *"석재 가동대 등등
        // 테이블이 진짜 테이블이면 안돼 연구를 할 수 있는 작업대 느낌이지 그냥 책상 의자가
        // 아니야"*).
        //
        // 직접 지은 도형이 **이미 작업대다** — 연구대는 `Screen`·`Apparatus`·`Backboard`까지
        // 짜여 있고 도축대는 고기 거는 `Rail`이 있다. 가구 소품(탁자·책상)으로 덮으면
        // **그 구조가 통째로 사라지고 식탁이 된다.** 소품은 *위에 쌓는 것*이지 *대신하는
        // 것*이 아니다.
        //
        // **바탕을 두는 것은 요리대 하나뿐이다** — 화덕은 *빵 굽는 물건 자체*가 정체이고,
        // 직접 지은 도형에는 그 화구가 없다. 나머지는 제 도형이 이미 작업대다.

        // 연구대 — 책상. 독서대는 작아서 1칸을 못 채웠다.
        new Bench { Building = "ResearchTable", Base = null,
            Steps = new[] {
                Step(T("I/Deco/P_PROP_book_interior_01"),         T("I/Deco/P_PROP_featherpen_interior")),
                Step(T("I/Deco/P_PROP_hourglass_interior"),       T("I/Deco/P_PROP_ink_interior")),
                Step(B("I/Storage/P_PROP_bookshelf_interior_01"), S("I/Deco/P_PROP_globe_interior")),
                Step(B("light/P_PROP_brazier_dungeon_01"),        S("I/Misc/P_PROP_telescope_interior")),
            }},

        // 일반 작업대 — 아무 일이나 하는 곳이라 바탕도 그냥 탁자다.
        new Bench { Building = "CraftingTable_Labor", Base = null,
            Steps = new[] {
                Step(T("I/Deco/P_PROP_brush_interior"),          S("furniture/P_PROP_stool_01_dungeon")),
                Step(S("I/Container/P_PROP_barrel_interior_01"), T("I/Container/P_PROP_bucket_interior_01")),
                Step(B("I/Storage/P_PROP_shelf_interior_01"),    S("I/Container/P_PROP_basket_interior_01")),
                Step(B("light/P_PROP_brazier_dungeon_02"),       T("I/Lights/P_PROP_lantern_interior_01")),
            }},

        // 요리대 — **화덕**(2026-09-18, 사용자: *"Smelt는 모델이 cook쪽이 더 맞는 거 같아
        // … cook은 지금 smelt걸로 교체해줘"*). 빵을 굽는 자리라 화덕이 맞고, 제련소는
        // 직접 지은 용광로로 돌아간다.
        new Bench { Building = "CraftingTable_Cook", Base = "I/Misc/P_PROP_oven_interior",
            Steps = new[] {
                Step(T("I/Container/P_PROP_cookingpot_interior_01"), T("I/Deco/P_PROP_cuttingboard_interior")),
                Step(T("I/Food/P_PROP_bread_interior_01"),           S("I/Misc/P_PROP_breadpaddle_interior")),
                Step(B("I/Storage/P_PROP_cabinet_interior_01"),      S("I/Container/P_PROP_barrel_interior_02")),
                Step(B("I/Misc/P_PROP_fireplace_interior"),          T("I/Container/P_PROP_kettle_interior")),
            }},

        // 무기 제작대 — **모루가 바탕이다.** 탁자였을 때는 일반 작업대와 구별이 안 됐다.
        // (모루는 `Kevin Iglesias` 애니메이션 데모의 소품이라 **결이 조금 다를 수 있다** —
        //  튀면 이 한 줄만 바꾸면 된다.)
        new Bench { Building = "CraftingTable_Weapon", Base = null,
            Steps = new[] {
                Step(S("I/Tables/P_PROP_table_interior_02"),     T("tools_weapons/P_PROP_knife_dungeon_01")),
                Step(B("construction/P_PROP_planks_dungeon_01"), S("I/Container/P_PROP_bucket_interior_02")),
                Step(B("furniture/P_PROP_wallshelf_dungeon_03"), S("I/Deco/P_PROP_weapon_sword_interior")),
                Step(B("light/P_PROP_brazier_dungeon_01"),       B("construction/P_PROP_chain_dungeon_02")),
            }},

        // 갑주 제작대 — 판금을 펴는 자리. 방패와 흉갑이 정체를 말한다.
        new Bench { Building = "CraftingTable_Armor", Base = null,
            Steps = new[] {
                Step(T("construction/P_PROP_planks_dungeon_02"), S("I/Deco/P_PROP_weapon_shield_interior_01")),
                Step(S("I/Container/P_PROP_barrel_interior_01"), T("I/Deco/P_PROP_horseshoe_interior")),
                Step(B("furniture/P_PROP_wallshelf_dungeon_04"), S("I/Deco/P_PROP_trophy_interior_01")),
                Step(B("light/P_PROP_brazier_dungeon_02"),       B("construction/P_PROP_chain_dungeon_03")),
            }},

        // 재봉대 — 옷걸이가 정체다.
        new Bench { Building = "CraftingTable_Tailor", Base = null,
            Steps = new[] {
                Step(T("fabrics/P_PROP_rug_dungeon_01"),         S("furniture/P_PROP_stool_01_dungeon")),
                Step(S("I/Misc/P_PROP_clothesrack_interior_01"), T("I/Deco/P_PROP_soap_interior")),
                Step(B("I/Storage/P_PROP_wardrobe_interior_01"), S("I/Container/P_PROP_basket_interior_02")),
                Step(B("I/Fabrics/P_PROP_curtain_interior_01"),  S("I/Deco/P_PROP_mirror_interior_01")),
            }},

        // 제련소 — **직접 지은 것으로 되돌렸다**(2026-09-18, 사용자: *"거기는 그냥 예전걸로
        // 돌려줘"*). 화덕은 **빵 굽는 물건**으로 읽혀 요리대로 넘겼다 — 팩에 용광로가 없고,
        // 직접 지은 도형이 그 자리를 이미 채우고 있다.
        new Bench { Building = "CraftingTable_Smelt", Base = null,
            Steps = new[] {
                Step(S("natural/P_PROP_debris_stones_dungeon_01"),  T("I/Container/P_PROP_bucket_interior_03")),
                Step(B("construction/P_PROP_beam_wood_dungeon_01"), S("I/Misc/P_PROP_woodlogs_interior")),
                Step(B("light/P_PROP_brazier_dungeon_02"),          S("I/Container/P_PROP_barrel_interior_02")),
                Step(B("construction/P_PROP_chain_dungeon_01"),     T("misc/P_PROP_goldpile_dungeon_02")),
            }},

        new Bench { Building = "CraftingTable_Stonecut", Base = null,
            Steps = new[] {
                Step(T("natural/P_PROP_debris_stones_dungeon_02"),     T("tools_weapons/P_PROP_shovel_dungeon")),
                Step(S("natural/P_PROP_debris_stones_dungeon_04"),     B("construction/P_PROP_planks_dungeon_04")),
                Step(B("furniture/P_PROP_gravestone_base_dungeon_01"), S("I/Container/P_PROP_bucket_interior_04")),
                Step(B("light/P_PROP_brazier_dungeon_01"),             S("furniture/P_PROP_stool_01_dungeon")),
            }},

        // 목공대 — 장작과 널판.
        new Bench { Building = "CraftingTable_Woodwork", Base = null,
            Steps = new[] {
                Step(T("construction/P_PROP_planks_dungeon_03"),    T("tools_weapons/P_PROP_knife_dungeon_02")),
                Step(S("I/Misc/P_PROP_woodlogs_interior"),          B("furniture/P_PROP_logpile_dungeon")),
                Step(B("I/Storage/P_PROP_shelf_interior_02"),       S("I/Misc/P_PROP_woodlog_interior")),
                Step(B("construction/P_PROP_beam_wood_dungeon_03"), S("furniture/P_PROP_ladder_dungeon_01")),
            }},

        // 보석 연마대 — 돋보기와 금고.
        new Bench { Building = "CraftingTable_Gemcut", Base = null,
            Steps = new[] {
                Step(T("I/Deco/P_PROP_magnifyingglass_interior"), T("light/P_PROP_candle_dungeon_01")),
                Step(T("I/Deco/P_PROP_coinpouch_interior"),       S("furniture/P_PROP_stool_01_dungeon")),
                Step(B("I/Misc/P_PROP_safe_interior"),            T("misc/P_PROP_goldpile_dungeon_01")),
                Step(B("light/P_PROP_brazier_dungeon_01"),        S("I/Deco/P_PROP_trophy_interior_02")),
            }},

        // 연금술대 — 병과 솥. **할로윈 팩의 가마솥은 안 쓴다**(결이 통째로 다르다).
        new Bench { Building = "CraftingTable_Alchemy", Base = null,
            Steps = new[] {
                Step(T("containers/bottles/P_PROP_bottle_dungeon_02"), T("I/Food/P_PROP_jar_interior_01")),
                Step(S("I/Container/P_PROP_cookingpot_interior_02"),   T("light/P_PROP_candle_dungeon_07")),
                Step(B("I/Storage/P_PROP_bookshelf_interior_02"),      S("I/Plants/P_PROP_plantpot_interior_01")),
                Step(B("light/P_PROP_brazier_dungeon_02"),             T("paper/P_PROP_scroll_dungeon_03")),
            }},

        // 도축대 — **바탕을 비운다.** 직접 지은 도형이 고기 거는 **레일과 기둥**까지 갖춘
        // 도축대인데, 팩에는 도축대가 없어서 탁자로 덮으면 그 구조가 통째로 사라진다.
        new Bench { Building = "ButcherTable", Base = null,
            Steps = new[] {
                Step(T("tools_weapons/P_PROP_knife_dungeon_02"),             T("I/Food/P_PROP_meat_interior")),
                Step(B("natural/bones/P_PROP_skeleton_bonepile_dungeon_01"), S("I/Container/P_PROP_bucket_interior_01")),
                Step(B("construction/P_PROP_chain_dungeon_01"),              S("natural/bones/P_PROP_skeleton_bone_pile_dungeon")),
                Step(B("construction/P_PROP_chain_dungeon_04"),              S("I/Container/P_PROP_barrel_interior_01")),
            }},
    };

    [MenuItem("DungeonColony/Building/3. Create Bench Tier Art (등급별 모델)")]
    static void Run()
    {
        int done = 0, missing = 0;
        var notes = new List<string>();

        foreach (var b in Table)
        {
            string path = $"{PrefabDir}/{b.Building}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                notes.Add($"건물 없음 — {b.Building}");
                missing++;
                continue;
            }

            int made = Build(path, b, notes);
            if (made > 0) { done++; notes.Add($"{b.Building} — 등급 {made}벌"); }
            else missing++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[등급 모델] {done}채에 등급별 얼굴을 구웠습니다(못 한 것 {missing}).\n  "
                + string.Join("\n  ", notes)
                + "\n런타임은 `BenchVisual`이 해당 등급 자식 하나만 켭니다. "
                + "되돌리려면 `[Art *]` 자식을 지우고 꺼진 렌더러를 켜면 됩니다.");
    }

    // ── 되돌리기 ────────────────────────────────────────────────────────────
    //
    // **되돌리는 길을 도구가 들고 있어야 한다**(`PropDresser`의 교훈 — git이 늘 안전망이
    // 되지는 않는다). 작업대 열둘이 저쪽 표에서 빠졌으므로 `2. Undress`가 이제 이들을
    // 못 되돌린다. 정확히 반대로 한다: `[Art *]`를 지우고 렌더러를 켠다.
    [MenuItem("DungeonColony/Building/4. Remove Bench Tier Art (등급별 모델 되돌리기)")]
    static void Undo()
    {
        int done = 0;
        foreach (var b in Table)
        {
            string path = $"{PrefabDir}/{b.Building}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool touched = false;
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var c = root.transform.GetChild(i);
                    if (!c.name.StartsWith(Prefix) && c.name != "[Art]") continue;
                    Object.DestroyImmediate(c.gameObject);
                    touched = true;
                }
                if (!touched) continue;

                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = true;

                // **컴포넌트도 뗀다** — 남겨 두면 세트가 없는데도 `Apply`가 돌고, 다음에
                // 다시 구울 때 *이미 있다*고 착각할 여지를 남긴다.
                var vis = root.GetComponent<BenchVisual>();
                if (vis != null) Object.DestroyImmediate(vis);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                done++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[등급 모델] {done}채를 원래 도형으로 되돌렸습니다.");
    }

    static int Build(string path, Bench b, List<string> notes)
    {
        // **바탕을 비우면 직접 지은 도형을 그대로 쓴다**(2026-09-17, 사용자: *"비슷한 모델이
        // 없는건 직접 만든 걸로 넣어 임시로"*). 도축대가 그 자리다 — 고기 거는 **레일과
        // 기둥**까지 짜 놓았는데 팩에는 도축대가 없어서, 그냥 탁자로 덮으니 *전혀 맞지 않는*
        // 물건이 됐다. 이제 옛 렌더러를 **안 끄고** 그 위에 소품만 쌓는다.
        bool keepOwn = string.IsNullOrEmpty(b.Base);

        var baseProp = keepOwn ? null : Load(b.Base);
        if (!keepOwn && baseProp == null) { notes.Add($"바탕 소품 없음 — {b.Building} ({b.Base})"); return 0; }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // **먼저 지난번 것을 걷는다** — 다시 돌려도 같은 결과가 나오게. `PropDresser`가
            // 얹은 `[Art]`도 같이 걷는다(얼굴은 한 벌이어야 한다).
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var c = root.transform.GetChild(i);
                if (c.name.StartsWith(Prefix) || c.name == "[Art]") Object.DestroyImmediate(c.gameObject);
            }

            // **옛 메시는 끄기만 한다** — 지우면 같은 오브젝트에 붙은 콜라이더까지 사라져
            // 클릭도 몸도 안 걸린다(`PropDresser`와 같은 규칙).
            // 바탕을 비운 작업대는 그 도형이 **곧 바탕**이므로 켜 둔다.
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = keepOwn;

            // **런타임이 켤 컴포넌트를 같이 붙인다** — 굽는 자리와 켜는 자리가 갈리면
            // *모델은 있는데 아무것도 안 바뀌는* 작업대가 생긴다.
            if (root.GetComponent<BenchVisual>() == null) root.AddComponent<BenchVisual>();

            var size = Footprint(root);
            int built = 0;

            for (int tier = BenchTier.Min; tier <= BenchTier.Max; tier++)
            {
                var set = new GameObject($"{Prefix}{tier}]");
                set.transform.SetParent(root.transform, false);

                // 바탕을 비웠으면 **직접 지은 도형이 바탕**이다 — 얹는 자리를 그 경계에서 잰다.
                // 크기는 안 건드린다(제 크기가 이미 발자국에 맞게 지어져 있다).
                var bottom = keepOwn ? root : Place(set, baseProp);
                if (bottom == null) { Object.DestroyImmediate(set); break; }

                if (!keepOwn) FitTo(bottom, size, FitAt[tier - BenchTier.Min]);

                // **그 등급까지 누적한다** — `[Art 3]`은 II와 III의 것을 둘 다 든다.
                int tops = 0, backs = 0, sides = 0;
                for (int s = 0; s < b.Steps.Length && s < tier - BenchTier.Min; s++)
                {
                    foreach (var add in b.Steps[s])
                    {
                        var prop = Load(add.Prop);
                        if (prop == null) { notes.Add($"소품 없음 — {b.Building} ({add.Prop})"); continue; }

                        var go = Place(set, prop);
                        if (go == null) continue;

                        // **자리마다 따로 센다** — 한 셈으로 흩으면 위에 둘, 뒤에 하나일 때
                        // 뒤엣것이 셋째 자리로 밀려 **아무것도 없는 데로 비켜난다.**
                        switch (add.Where)
                        {
                            case Put.Top:    PutOnTop(go, bottom, tops++);         break;
                            case Put.Behind: PutBehind(go, bottom, size, backs++); break;
                            default:         PutSide(go, bottom, size, sides++);   break;
                        }
                    }
                }

                // **I만 켜 둔다** — 프리팹이 다섯을 다 켠 채로 있으면 `BenchVisual`이 붙기
                // 전 한 프레임 동안 전부 겹쳐 보인다.
                set.SetActive(tier == BenchTier.Min);
                built++;
            }

            if (built == 0) return 0;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return built;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ── 놓기 ────────────────────────────────────────────────────────────────

    static GameObject Place(GameObject parent, GameObject prop)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prop, parent.transform);
        if (go == null) return null;

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        // **소품의 콜라이더는 뗀다** — 길찾기가 보는 것은 타일 점유지 콜라이더가 아니고,
        // 남겨 두면 지도는 "빈 칸"이라 하는데 몸이 부딪혀 폰이 처박힌다.
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        return go;
    }

    static Vector2 Footprint(GameObject root)
    {
        var pb = root.GetComponent<PlacedBuilding>();
        var s  = pb != null && pb.Data != null ? pb.Data.Size : Vector2Int.one;
        return new Vector2(Mathf.Max(1, s.x), Mathf.Max(1, s.y));
    }

    // 바탕을 발자국에 맞춘다 — **가로세로 중 빡빡한 쪽에 균등하게**(축마다 따로 늘이면
    // 모델이 찌그러지고, 그러면 모델을 바꾼 뜻이 없다).
    static void FitTo(GameObject go, Vector2 size, float margin)
    {
        if (!LocalBounds(go, out var b) || b.size.x <= 0.0001f || b.size.z <= 0.0001f) return;

        float s = Mathf.Min(size.x * margin / b.size.x, size.y * margin / b.size.z);
        go.transform.localScale = Vector3.one * s;

        if (!LocalBounds(go, out b)) return;
        go.transform.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
    }

    // 바탕 윗면에 올린다. **여럿이면 흩는다** — 같은 자리에 놓으면 겹쳐서 하나로 보인다.
    static void PutOnTop(GameObject go, GameObject bottom, int index)
    {
        if (!LocalBounds(bottom, out var bb)) return;
        if (!LocalBounds(go, out var gb) || gb.size.y <= 0.0001f) return;

        // **얹는 것은 바탕보다 작아야 한다** — 책 하나가 탁자만 하면 무엇이 바탕인지
        // 안 읽힌다. 바탕 너비의 0.4를 넘으면 줄인다.
        float cap = bb.size.x * 0.4f;
        if (gb.size.x > cap && gb.size.x > 0.0001f)
        {
            go.transform.localScale *= cap / gb.size.x;
            if (!LocalBounds(go, out gb)) return;
        }

        float[] slots = { 0.22f, -0.24f, 0.02f, -0.1f };
        float dx = bb.size.x * slots[index % slots.Length];
        float dz = bb.size.z * (index % 2 == 0 ? 0.12f : -0.14f);

        go.transform.localPosition += new Vector3(bb.center.x + dx - gb.center.x,
                                                  bb.max.y - gb.min.y,
                                                  bb.center.z + dz - gb.center.z);
    }

    // 발자국 **뒤쪽 바깥**에 세운다 — 바탕 위에 겹치지 않게. 발자국을 벗어나지만 그 칸은
    // 어차피 작업대가 막고 있고, 벽에 붙여 짓는 물건이라 화면에서는 배경으로 읽힌다.
    //
    // **실루엣을 만드는 것이 여기다**(2026-09-17) — 바탕은 칸에 묶여 크게 못 키우지만
    // 뒤엣것은 높이에 제약이 없다. *"차이가 컸으면 좋겠다"*의 답이 크기보다 이쪽이다.
    static void PutBehind(GameObject go, GameObject bottom, Vector2 size, int index)
    {
        if (!LocalBounds(go, out var gb) || gb.size.y <= 0.0001f) return;

        float cap = size.x * 0.6f;
        if (gb.size.x > cap && gb.size.x > 0.0001f)
        {
            go.transform.localScale *= cap / gb.size.x;
            if (!LocalBounds(go, out gb)) return;
        }

        // 여럿이면 좌우로 갈라 세운다 — 겹쳐 세우면 뒤엣것이 앞엣것에 가려 없는 것과 같다.
        float[] slots = { 0f, -0.34f, 0.34f, -0.17f, 0.17f };
        float dx = size.x * slots[index % slots.Length];

        float back = -(size.y * 0.5f) - gb.size.z * 0.5f - 0.05f;
        go.transform.localPosition += new Vector3(dx - gb.center.x, -gb.min.y, back - gb.center.z);
    }

    // 발자국 **옆 바깥**에 놓는다 — 좌우를 번갈아 쓴다. 뒤가 이미 붐빌 때의 자리다.
    static void PutSide(GameObject go, GameObject bottom, Vector2 size, int index)
    {
        if (!LocalBounds(go, out var gb) || gb.size.x <= 0.0001f) return;

        float cap = size.y * 0.6f;
        if (gb.size.z > cap && gb.size.z > 0.0001f)
        {
            go.transform.localScale *= cap / gb.size.z;
            if (!LocalBounds(go, out gb)) return;
        }

        float sign = index % 2 == 0 ? 1f : -1f;
        float row  = index / 2 * 0.45f;   // 같은 쪽에 둘 이상이면 앞뒤로 물린다
        float side = sign * ((size.x * 0.5f) + gb.size.x * 0.5f + 0.05f);

        go.transform.localPosition += new Vector3(side - gb.center.x, -gb.min.y,
                                                  size.y * (0.2f - row) - gb.center.z);
    }

    // **로컬 경계다** — `Renderer.bounds`는 월드 경계라 프리팹을 편집 중일 때 부모 변환이
    // 안 섞이지만, 자식에 스케일이 걸려 있으면 답이 갈린다. 메시 경계를 직접 변환한다.
    static bool LocalBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;

        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;

            var mb = mf.sharedMesh.bounds;
            var m  = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;

            // 여덟 꼭짓점을 옮겨 감싼다 — 회전이 걸려 있으면 중심+크기만으로는 틀린다.
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3(
                    (i & 1) == 0 ? mb.min.x : mb.max.x,
                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                var p = m.MultiplyPoint3x4(c);
                if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                else        bounds.Encapsulate(p);
            }
        }

        // 부모(`go`) 자신의 스케일을 얹는다 — 위에서 잰 것은 `go` 기준이라 부모 좌표로 올린다.
        if (found)
        {
            bounds.center = Vector3.Scale(bounds.center, go.transform.localScale) + go.transform.localPosition;
            bounds.size   = Vector3.Scale(bounds.size,   go.transform.localScale);
        }
        return found;
    }

    // 표를 짧게 유지하려고 접두사로 팩을 가른다: `I/`는 인테리어 팩, `Assets/`로 시작하면
    // 경로 그대로, 나머지는 던전 팩. **접두사를 안 두면 표의 절반이 경로로 덮인다.**
    static GameObject Load(string rel)
    {
        string path = rel.StartsWith("Assets/") ? rel
                    : rel.StartsWith("I/")      ? $"{PackInterior}/{rel.Substring(2)}.prefab"
                                                : $"{PackDungeon}/{rel}.prefab";
        if (!path.EndsWith(".prefab")) path += ".prefab";
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
