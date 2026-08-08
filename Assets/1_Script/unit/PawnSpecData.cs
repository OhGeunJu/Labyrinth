using System;
using System.Collections.Generic;
using UnityEngine;

// **저작한 개체 하나** — "이 사람은 이런 사람이다"를 손으로 적어 두는 에셋.
//
// 시드로도 개체를 고정할 수는 있지만, 시드는 **굴림의 결과를 붙들 뿐 무엇이 나올지 고를 수가
// 없다.** "근력 S, 회피 B인 동료"를 만들려면 원하는 조합이 나올 때까지 숫자를 바꿔 가며
// 돌려 봐야 한다 — 그건 저작이 아니라 뽑기다.
//
// 그래서 **적고 싶은 것만 적고 나머지는 굴린다.** 이름 있는 동료라도 51개 스탯을 전부 적을
// 이유는 없다 — 눈에 띄는 몇 개만 못 박고 나머지는 종족 범위에서 나오면 된다.
//
// **SO인 이유**: 같은 인물을 여러 곳이 가리켜야 한다(씬에 세워 두기 · 이벤트 지원군으로 부르기 ·
// 마을 후보로 세우기). GUID로 가리킬 수 있어야 하므로 SO다(`docs/data.md`의 기준).
[CreateAssetMenu(menuName = "DungeonColony/PawnSpecData")]
public class PawnSpecData : ScriptableObject
{
    [Serializable]
    public class GradeFix
    {
        public StatName  Stat;
        public StatGrade Grade = StatGrade.D;
    }

    [Header("사람")]
    [Tooltip("비우면 종족 이름 풀에서 굴린다.")]
    public string PawnName;

    [Tooltip("**적은 스탯만** 그 등급으로 못 박는다. 안 적은 스탯은 종족 범위에서 굴린다.")]
    public List<GradeFix> Grades = new();

    [Tooltip("안 적은 스탯을 굴릴 시드. 0이면 이름에서 뽑는다 — 같은 이름이면 늘 같은 사람이다.")]
    public int Seed;

    [Tooltip("**못 박을 특성만** 적는다. 남는 자리는 굴린 것으로 채운다(등급과 같은 규칙).")]
    public List<TraitData> Traits = new();

    [Header("생김새")]
    // **기본 폰이 못 갖는 모습**을 여기서 준다. 이름 있는 동료가 남들과 똑같이 생기면
    // 이름을 붙인 뜻이 절반 사라진다.
    [Tooltip("0이면 종족 기본(굴린 값). 0.94~1.06이 보통 범위다.")]
    public float BodyScale;

    [Tooltip("폰의 렌더러 재질을 갈아 끼운다. 비우면 프리팹 그대로.")]
    public Material MaterialOverride;

    [Tooltip("전용 모델. 채우면 기본 렌더러를 끄고 이걸 자식으로 붙인다.")]
    public GameObject ModelOverride;

    // 적어 둔 것을 입힌 명세를 만든다. 안 적은 것은 **종족이 스탯별로 저작한 범위**에서 굴린다.
    public PawnSpec Build(UnitData race)
    {
        int seed = Seed != 0 ? Seed : StableSeed(PawnName, name);
        var spec = PawnSpec.Roll(race, seed);

        if (!string.IsNullOrWhiteSpace(PawnName)) spec.Name = PawnName;
        if (BodyScale > 0f)                      spec.BodyScale = BodyScale;

        foreach (var g in Grades)
            if (g != null) spec.Grades[g.Stat] = g.Grade;

        spec.MaterialOverride = MaterialOverride;
        spec.ModelOverride    = ModelOverride;

        ApplyAuthoredTraits(spec);

        // 못 박은 등급이 평균을 바꾼다 — 값(`Price`)과 표시가 그 평균을 쓰므로 다시 센다.
        spec.RecomputeAverage();
        return spec;
    }

    // **적은 특성이 앞자리를 차지하고, 남는 자리는 굴린 것으로 채운다** — 등급과 같은 규칙이다.
    // 세 개를 다 적을 이유는 없다. "겁이 없다"만 정해 두고 나머지 둘은 매번 같되 굴린 것이면
    // 저작한 인물이면서도 굴린 폰과 같은 폭을 갖는다.
    void ApplyAuthoredTraits(PawnSpec spec)
    {
        if (Traits == null || Traits.Count == 0) return;

        var final = new List<TraitData>();
        foreach (var t in Traits)
            if (t != null && !final.Contains(t) && !TraitPool.Conflicts(final, t)) final.Add(t);

        // **적은 것이 굴린 개수를 이긴다.** 굴려서 0개가 나왔어도 저작한 특성은 붙는다 —
        // 안 그러면 "이 동료는 겁이 없다"고 적어 둔 것이 운에 따라 사라진다.
        foreach (var t in spec.Traits)
        {
            if (final.Count >= TraitPool.MaxCount) break;
            if (t == null || final.Contains(t) || TraitPool.Conflicts(final, t)) continue;
            final.Add(t);
        }

        spec.Traits.Clear();
        spec.Traits.AddRange(final);
    }

    // **시드를 안 적어도 같은 사람이 나오게** 이름에서 뽑는다. `string.GetHashCode`는 실행마다
    // 달라질 수 있어(런타임 해시 랜덤화) 쓰면 안 된다 — 직접 센다.
    static int StableSeed(string a, string b)
    {
        string s = !string.IsNullOrWhiteSpace(a) ? a : b;
        if (string.IsNullOrEmpty(s)) return 1;

        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h == 0 ? 1 : h;
        }
    }
}
