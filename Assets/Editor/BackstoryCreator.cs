using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// **성장 배경 첫 벌을 만든다.** `Resources/Data/Backstories/`에 `BackstoryData` 에셋을 찍는다.
//
// 왜 생성기인가: 새 스크립트라 **에셋이 가리킬 GUID가 아직 없어서** `.asset` 파일을 손으로
// 쓸 수가 없다(유니티가 스크립트를 임포트해야 생긴다). 한 번 돌리고 나면 이후 수정은
// 인스펙터에서 한다 — **다 쓰면 지운다**(`docs/editor_creators.md` "생성기의 수명").
//
// **이미 있는 것은 안 건드린다** — 손본 값을 덮어쓰면 생성기가 저작을 이기는 꼴이 된다.
public static class BackstoryCreator
{
    const string Dir = "Assets/Resources/Data/Backstories";

    struct Spec
    {
        public string Id, Title, Desc;
        public BackstorySlot Slot;
        public float Weight;
        public string Group;                 // 비우면 공용
        public (StatName stat, float delta)[] Offsets;
        public JobCategory[] Bans;
    }

    // **무능력은 드물어야 값이 있다.** 열일곱 중 넷만 무언가를 막는다 — 전부가 하나씩 막으면
    // 그건 개성이 아니라 세금이고, 폰을 뽑을 때마다 "이번엔 뭘 못 하나"를 확인하는 일이 된다.
    static readonly Spec[] Table =
    {
        // ── 유년기 ───────────────────────────────────────────────────────────
        new Spec { Id = "Back_Child_Street",  Slot = BackstorySlot.Childhood, Weight = 1.2f, Group = "civilian",
                   Title = "거리의 아이",
                   Desc  = "골목에서 자랐다. 뺏기지 않으려면 빨라야 했고, 얻으려면 손이 빨라야 했다.",
                   Offsets = new[] { (StatName.Evasion, 8f), (StatName.Dexterity, 5f), (StatName.Social, -4f) } },

        new Spec { Id = "Back_Child_Farm",    Slot = BackstorySlot.Childhood, Weight = 1.4f, Group = "civilian",
                   Title = "농가의 막내",
                   Desc  = "해 뜨면 밭에 나갔다. 흙이 무엇을 원하는지는 배우기 전에 알았다.",
                   Offsets = new[] { (StatName.Growing, 10f), (StatName.Strength, 5f), (StatName.Cognition, -4f) } },

        new Spec { Id = "Back_Child_Smithy",  Slot = BackstorySlot.Childhood, Weight = 1f, Group = "civilian",
                   Title = "대장간 도제",
                   Desc  = "풀무질부터 배웠다. 쇠가 언제 준비됐는지는 색으로 안다.",
                   Offsets = new[] { (StatName.WeaponCrafting, 8f), (StatName.Smelting, 6f), (StatName.Social, -3f) } },

        new Spec { Id = "Back_Child_Noble",   Slot = BackstorySlot.Childhood, Weight = 0.6f, Group = "civilian",
                   Title = "귀족가의 아이",
                   Desc  = "글과 예법을 배웠다. 짐을 지어 본 적은 한 번도 없다.",
                   Offsets = new[] { (StatName.Social, 10f), (StatName.Cognition, 6f), (StatName.Strength, -8f) },
                   Bans = new[] { JobCategory.Hauling } },

        new Spec { Id = "Back_Child_Mining",  Slot = BackstorySlot.Childhood, Weight = 1.1f, Group = "civilian",
                   Title = "광산 마을 출신",
                   Desc  = "갱도의 공기를 마시며 자랐다. 어두운 곳이 무섭지 않다.",
                   Offsets = new[] { (StatName.Mining, 12f), (StatName.Toughness, 5f) } },

        new Spec { Id = "Back_Child_Temple",  Slot = BackstorySlot.Childhood, Weight = 0.9f, Group = "civilian",
                   Title = "사원의 시동",
                   Desc  = "약초와 필사를 배웠다. 사람을 때리는 법은 배우지 않았다.",
                   Offsets = new[] { (StatName.Research, 8f), (StatName.Medicine, 5f), (StatName.MeleeAttack, -4f) } },

        new Spec { Id = "Back_Child_Forest",  Slot = BackstorySlot.Childhood, Weight = 1f, Group = "civilian",
                   Title = "숲에서 자람",
                   Desc  = "나무와 짐승 사이에서 컸다. 사람 사이가 오히려 어렵다.",
                   Offsets = new[] { (StatName.Cutting, 8f), (StatName.Bow, 6f), (StatName.Social, -5f) } },

        new Spec { Id = "Back_Child_Barrack", Slot = BackstorySlot.Childhood, Weight = 0.9f, Group = "mercenary",
                   Title = "병영에서 자람",
                   Desc  = "아버지의 부대를 따라다녔다. 검을 장난감으로 알고 컸다.",
                   Offsets = new[] { (StatName.MeleeAttack, 8f), (StatName.Toughness, 5f), (StatName.Research, -5f) } },

        // ── 성년기 ───────────────────────────────────────────────────────────
        new Spec { Id = "Back_Adult_Merc",    Slot = BackstorySlot.Adulthood, Weight = 1.3f, Group = "mercenary",
                   Title = "용병",
                   Desc  = "값을 받고 싸웠다. 누구 편이었는지는 그때마다 달랐다.",
                   Offsets = new[] { (StatName.MeleeAttack, 10f), (StatName.Toughness, 6f), (StatName.Social, -4f) } },

        new Spec { Id = "Back_Adult_Guard",   Slot = BackstorySlot.Adulthood, Weight = 1.1f, Group = "civilian",
                   Title = "상단 호위",
                   Desc  = "짐과 사람을 함께 지켰다. 길 위에서는 말이 칼보다 자주 쓰인다.",
                   Offsets = new[] { (StatName.Social, 8f), (StatName.MeleeDefense, 6f), (StatName.Athletics, 4f) } },

        new Spec { Id = "Back_Adult_Farmer",  Slot = BackstorySlot.Adulthood, Weight = 1.3f, Group = "civilian",
                   Title = "밭갈이꾼",
                   Desc  = "한 뙈기를 평생 갈았다. 그 땅이 남의 것이 되기 전까지는.",
                   Offsets = new[] { (StatName.Growing, 12f), (StatName.Strength, 6f) } },

        new Spec { Id = "Back_Adult_Medic",   Slot = BackstorySlot.Adulthood, Weight = 0.8f, Group = "civilian",
                   Title = "떠돌이 의원",
                   Desc  = "마을을 옮겨 다니며 상처를 꿰맸다. 값은 대개 밥이었다.",
                   Offsets = new[] { (StatName.Medicine, 14f), (StatName.Alchemy, 6f), (StatName.Strength, -4f) } },

        new Spec { Id = "Back_Adult_Robber",  Slot = BackstorySlot.Adulthood, Weight = 0.9f, Group = "bandit",
                   Title = "도굴꾼",
                   Desc  = "무덤을 열어 먹고살았다. 그 일로 잃은 것은 손이 아니라 이름이다.",
                   Offsets = new[] { (StatName.Dexterity, 10f), (StatName.Evasion, 6f), (StatName.Social, -6f) } },

        new Spec { Id = "Back_Adult_Scholar", Slot = BackstorySlot.Adulthood, Weight = 0.7f, Group = "civilian",
                   Title = "학자",
                   Desc  = "책과 도면 사이에서 늙었다. 창을 쥐어 줘도 어디를 봐야 할지 모른다.",
                   Offsets = new[] { (StatName.Research, 14f), (StatName.Cognition, 8f), (StatName.Strength, -6f) },
                   Bans = new[] { JobCategory.Guard } },

        new Spec { Id = "Back_Adult_Cook",    Slot = BackstorySlot.Adulthood, Weight = 1f, Group = "civilian",
                   Title = "요리사",
                   Desc  = "여관 부엌을 맡았다. 무엇이든 먹을 것으로 만드는 재주가 있다.",
                   Offsets = new[] { (StatName.Cooking, 14f), (StatName.Dexterity, 5f) } },

        new Spec { Id = "Back_Adult_Miner",   Slot = BackstorySlot.Adulthood, Weight = 1.1f, Group = "civilian",
                   Title = "광부",
                   Desc  = "곡괭이만 쥐고 살았다. 어깨가 그만큼 굽었다.",
                   Offsets = new[] { (StatName.Mining, 14f), (StatName.Strength, 8f), (StatName.Social, -4f) } },

        new Spec { Id = "Back_Adult_Convict", Slot = BackstorySlot.Adulthood, Weight = 0.6f, Group = "bandit",
                   Title = "옥살이",
                   Desc  = "몇 해를 갇혀 지냈다. 창살 안쪽이 어떤 곳인지 알아서, 남을 가둬 두지 못한다.",
                   Offsets = new[] { (StatName.Toughness, 10f), (StatName.Evasion, 4f), (StatName.Social, -8f) },
                   Bans = new[] { JobCategory.Warden } },

        // ── 무리별 이력 ──────────────────────────────────────────────────────
        //
        // **묶음이 있는 것은 그 무리에게만 나온다**(`BackstoryData.Group` ↔ `FactionData.BackstoryGroup`).
        // 공용은 여전히 누구에게나 나오므로 산적도 `농가의 막내`로 자랄 수 있다 — 처음부터
        // 산적으로 태어나는 사람은 없다.
        new Spec { Id = "Back_Adult_Bandit",  Slot = BackstorySlot.Adulthood, Weight = 1.4f, Group = "bandit",
                   Title = "산적",
                   Desc  = "길목에 앉아 지나가는 것을 받아먹었다. 처음엔 굶어서였다.",
                   Offsets = new[] { (StatName.MeleeAttack, 8f), (StatName.Evasion, 6f), (StatName.Social, -6f) } },

        new Spec { Id = "Back_Adult_Chief",   Slot = BackstorySlot.Adulthood, Weight = 0.5f, Group = "bandit",
                   Title = "패거리 두목",
                   Desc  = "열몇을 먹여 살렸다. 그 방법이 남의 것을 뺏는 것이었을 뿐이다.",
                   Offsets = new[] { (StatName.MeleeAttack, 10f), (StatName.Social, 8f), (StatName.Toughness, 6f) } },

        new Spec { Id = "Back_Child_Runaway", Slot = BackstorySlot.Childhood, Weight = 1.1f, Group = "bandit",
                   Title = "도망친 아이",
                   Desc  = "집을 나온 뒤로 돌아가지 않았다. 어디서 잤는지는 기억나지 않는다.",
                   Offsets = new[] { (StatName.Evasion, 8f), (StatName.Athletics, 6f), (StatName.Cognition, -4f) } },

        new Spec { Id = "Back_Adult_Cultist", Slot = BackstorySlot.Adulthood, Weight = 1.2f, Group = "cult",
                   Title = "교단의 신도",
                   Desc  = "밤마다 무언가를 불렀다. 대답이 있었는지는 말하지 않는다.",
                   Offsets = new[] { (StatName.DarkMagic, 12f), (StatName.Cognition, 6f), (StatName.Social, -8f) } },

        new Spec { Id = "Back_Child_Foundling", Slot = BackstorySlot.Childhood, Weight = 1f, Group = "cult",
                   Title = "제단에 놓인 아이",
                   Desc  = "누가 두고 갔는지 아무도 모른다. 교단이 거두어 길렀다.",
                   Offsets = new[] { (StatName.DarkMagic, 8f), (StatName.Toughness, 5f), (StatName.Growing, -5f) } },

        new Spec { Id = "Back_Adult_Company", Slot = BackstorySlot.Adulthood, Weight = 1.2f, Group = "mercenary",
                   Title = "용병단 소속",
                   Desc  = "계약서대로 싸웠고 계약서대로 물러났다. 그 이상도 이하도 아니다.",
                   Offsets = new[] { (StatName.MeleeAttack, 9f), (StatName.MeleeDefense, 7f), (StatName.Social, 4f) } },

        // **성년기는 무리가 정한다**(`BackstoryPool.Roll`) — 그러니 무리마다 **여럿**이어야 한다.
        // 산적이 둘뿐이면 잡아 온 산적이 하나같이 같은 사람이 되어, 이력이 개성이 아니라 딱지가 된다.
        new Spec { Id = "Back_Adult_Highway", Slot = BackstorySlot.Adulthood, Weight = 1.1f, Group = "bandit",
                   Title = "노상강도",
                   Desc  = "숲길 모퉁이에서 기다렸다. 활을 먼저 보이면 대개 짐만 두고 갔다.",
                   Offsets = new[] { (StatName.Bow, 10f), (StatName.Evasion, 5f), (StatName.Social, -5f) } },

        new Spec { Id = "Back_Adult_Smuggler", Slot = BackstorySlot.Adulthood, Weight = 0.9f, Group = "bandit",
                   Title = "밀수꾼",
                   Desc  = "검문을 피해 물건을 옮겼다. 값이 되는 것이라면 무엇이든.",
                   Offsets = new[] { (StatName.Social, 8f), (StatName.Dexterity, 6f), (StatName.Athletics, 4f) } },

        new Spec { Id = "Back_Adult_Deserter", Slot = BackstorySlot.Adulthood, Weight = 0.8f, Group = "bandit",
                   Title = "탈영병",
                   Desc  = "부대를 버리고 도망쳤다. 다루는 법만은 그대로 남았다.",
                   Offsets = new[] { (StatName.Polearm, 10f), (StatName.MeleeDefense, 6f), (StatName.Social, -4f) } },

        new Spec { Id = "Back_Adult_Graverob", Slot = BackstorySlot.Adulthood, Weight = 0.9f, Group = "cult",
                   Title = "시신을 나르던 자",
                   Desc  = "교단이 원하는 것을 구해다 주었다. 무엇에 쓰는지는 묻지 않았다.",
                   Offsets = new[] { (StatName.Medicine, 8f), (StatName.Strength, 6f), (StatName.Social, -6f) } },

        new Spec { Id = "Back_Adult_Scribe",  Slot = BackstorySlot.Adulthood, Weight = 0.8f, Group = "cult",
                   Title = "주문 필사자",
                   Desc  = "읽어서는 안 되는 것을 베껴 적었다. 손은 아직도 가끔 떨린다.",
                   Offsets = new[] { (StatName.DarkMagic, 10f), (StatName.Research, 8f), (StatName.Strength, -5f) } },

        new Spec { Id = "Back_Adult_Pikeman", Slot = BackstorySlot.Adulthood, Weight = 1f, Group = "mercenary",
                   Title = "창병 대열",
                   Desc  = "줄을 맞춰 서서 버티는 법을 배웠다. 혼자 싸우는 법은 배우지 않았다.",
                   Offsets = new[] { (StatName.Polearm, 12f), (StatName.MeleeDefense, 8f), (StatName.Evasion, -4f) } },

        new Spec { Id = "Back_Adult_Camp",    Slot = BackstorySlot.Adulthood, Weight = 0.9f, Group = "mercenary",
                   Title = "야영 취사병",
                   Desc  = "부대의 배를 채웠다. 싸운 적은 없지만 싸움터에는 늘 있었다.",
                   Offsets = new[] { (StatName.Cooking, 12f), (StatName.Toughness, 5f), (StatName.MeleeAttack, -5f) } },

        // **유년기도 무리가 정한다** — 공용을 없앴으므로(`BackstoryPool`) 무리마다 어릴 적
        // 이야기가 여럿 있어야 한다. 하나뿐이면 잡아 온 산적이 하나같이 `도망친 아이`가 된다.
        new Spec { Id = "Back_Child_Slum",    Slot = BackstorySlot.Childhood, Weight = 1.2f, Group = "bandit",
                   Title = "빈민가의 아이",
                   Desc  = "담벼락 아래에서 컸다. 나눠 주는 사람은 없었고 뺏는 사람만 있었다.",
                   Offsets = new[] { (StatName.Dexterity, 8f), (StatName.Toughness, 5f), (StatName.Social, -4f) } },

        new Spec { Id = "Back_Child_Caravan", Slot = BackstorySlot.Childhood, Weight = 1f, Group = "bandit",
                   Title = "떠돌이 무리에서 자람",
                   Desc  = "한곳에 두 달을 머문 적이 없다. 짐을 싸는 법부터 배웠다.",
                   Offsets = new[] { (StatName.Athletics, 8f), (StatName.Evasion, 5f), (StatName.Growing, -5f) } },

        new Spec { Id = "Back_Child_Drunk",   Slot = BackstorySlot.Childhood, Weight = 0.9f, Group = "bandit",
                   Title = "주정뱅이의 자식",
                   Desc  = "맞으면서 자랐다. 맞는 데는 이골이 났다.",
                   Offsets = new[] { (StatName.Toughness, 10f), (StatName.Social, -6f) } },

        new Spec { Id = "Back_Child_CultTown", Slot = BackstorySlot.Childhood, Weight = 1.1f, Group = "cult",
                   Title = "교단 마을에서 자람",
                   Desc  = "마을 사람 모두가 같은 것을 믿었다. 다른 믿음은 본 적이 없다.",
                   Offsets = new[] { (StatName.DarkMagic, 7f), (StatName.Cognition, 5f), (StatName.Social, -4f) } },

        new Spec { Id = "Back_Child_Taken",   Slot = BackstorySlot.Childhood, Weight = 0.8f, Group = "cult",
                   Title = "밤에 끌려간 아이",
                   Desc  = "누군가 데려갔고 아무도 찾지 않았다. 그 뒤의 일은 말하지 않는다.",
                   Offsets = new[] { (StatName.Toughness, 8f), (StatName.DarkMagic, 6f), (StatName.Social, -8f) } },

        new Spec { Id = "Back_Child_Orphan",  Slot = BackstorySlot.Childhood, Weight = 1f, Group = "mercenary",
                   Title = "고아원 출신",
                   Desc  = "열여섯이 되자 나가라고 했다. 받아 주는 곳은 용병단뿐이었다.",
                   Offsets = new[] { (StatName.MeleeDefense, 7f), (StatName.Toughness, 5f), (StatName.Cognition, -4f) } },
    };

    [MenuItem("DungeonColony/Create Backstories")]
    static void Create()
    {
        Directory.CreateDirectory(Dir);

        int made = 0, kept = 0, fixedGroup = 0;
        foreach (var spec in Table)
        {
            string path = Dir + "/" + spec.Id + ".asset";
            // **이미 있으면 묶음만 맞춘다.** 글과 수치는 저작한 사람 것이지만, *어느 무리의
            // 이력인가*는 이 표가 정하는 **구조**다 — 처음 돌렸을 때는 그 칸이 아예 없었으므로
            // (공용이라는 것이 있던 시절) 여기서 채워 주지 않으면 옛 에셋만 영영 civilian이다.
            var exist = AssetDatabase.LoadAssetAtPath<BackstoryData>(path);
            if (exist != null)
            {
                if (exist.Group != spec.Group)
                {
                    exist.Group = spec.Group;
                    EditorUtility.SetDirty(exist);
                    fixedGroup++;
                }
                kept++;
                continue;
            }

            var asset = ScriptableObject.CreateInstance<BackstoryData>();
            asset.Slot        = spec.Slot;
            asset.Title       = spec.Title;
            asset.Description = spec.Desc;
            asset.Weight      = spec.Weight;
            asset.Group       = spec.Group;

            asset.StatOffsets = new List<TraitData.StatOffset>();
            if (spec.Offsets != null)
                foreach (var o in spec.Offsets)
                    asset.StatOffsets.Add(new TraitData.StatOffset { Stat = o.stat, Delta = o.delta });

            asset.DisabledJobs = new List<JobCategory>();
            if (spec.Bans != null) asset.DisabledJobs.AddRange(spec.Bans);

            AssetDatabase.CreateAsset(asset, path);
            made++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Backstory] " + made + "개 만들고, 있던 " + kept + "개 중 " + fixedGroup +
                  "개의 묶음을 맞췄습니다. 종족 에셋의 UsesBackstory를 켜면 그 종족이 배경을 갖습니다.");
    }
}
