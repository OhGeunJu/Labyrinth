using System;
using System.Collections.Generic;
using UnityEngine;

// **무리의 두목을 세우는 문 — 하나다**(2026-11-02, 사용자: *"떠도는 무리에도 대장이 있어야지"*).
//
// 습격대(`RaidLord`)에서 나왔다 — 두목이 있어야 *무엇을 먼저 쳐야 하나*가 생기고, 부족 대장의 얼굴 문신(`MarkingLook`)도 이력이
// 정한다. 떠도는 무리(`WildSpawner`)도 같은 문을 지난다: 따로 적으면 한쪽만 표식 · 이력 · 문신 중 하나를 빠뜨린다.
public static class BandLeader
{
    public static UnitBrain Promote(IEnumerable<UnitBrain> members, Func<UnitBrain, bool> standing, int salt)
    {
        UnitBrain best = null;
        float bestScore = float.MinValue;

        foreach (var brain in members)
        {
            if (!standing(brain)) continue;

            // 명세가 있으면 그 평균, 없으면 굴려서 태어난 폰이라 실제 스탯이 답이다
            // (`PawnSpec.PriceOf`가 값을 낼 때 쓰는 것과 **같은 갈래**).
            float score = brain.Spec != null ? brain.Spec.GradeAverage
                        : brain.Stats != null ? brain.Stats.Toughness : 0f;
            if (score > bestScore) { bestScore = score; best = brain; }
        }

        if (best == null || best.Data == null) return null;

        // **두목은 뽑는 것이 아니라 세우는 것이다**(2026-09-09, 사용자 결정: *"처음 만들때부터
        // 대장하나에 부하넷을 만들면 되는 거 아니야?"*).
        //
        // 잠깐 *"이끈 이력을 가진 사람을 먼저 고른다"*로 만들었다가 되돌렸다 — 그건 **증상을
        // 쫓는 코드**였다: 이력이 아무에게나 붙는 것은 그대로 두고 뽑는 쪽에서 맞춰 주는
        // 것이라, 그 이력을 읽는 자리가 하나 늘 때마다 같은 맞춤이 또 필요해진다.
        //
        // 이제 **이끄는 이력은 굴림에서 아예 안 나온다**(`BackstoryPool.Fits`). 그래서
        // *"이력은 두목인데 두목이 아니다"*라는 상태가 **만들어질 수가 없고**, 여기서는
        // 예전처럼 가장 센 놈을 고른 뒤 **이력을 주기만** 하면 된다.

        // **화면에서도 알아보게 한다**(2026-09-09). 두목을 세게 만든 이유가 *무엇을 먼저
        // 쳐야 하나*인데, 포위가 붙어 갈래가 셋이 되고도 **어느 갈래에 있는지는 눌러 봐야
        // 알았다** — 그러면 그건 판단이 아니라 뽑기다. 표식의 규칙은 전부 `PawnMark`가
        // 든다(여기서 색이나 높이를 정하면 다음 표식이 그 답을 다시 갖는다).
        PawnMark.Show(best.gameObject, PawnMark.Kind.Leader);

        var storyGroup = best.Blackboard?.Faction?.Data?.BackstoryGroup;

        // **이미 정해진 사람은 다시 안 굴린다.** `Spec`이 있으면 밖에서 주입된 것이다 —
        // 지도에서 걸어온 무리(`SpawnArrivingBand`가 `ApplySpec`으로 그 사람을 그대로 세운다)와
        // 저작한 고정 개체가 그렇다. 여기서 다시 굴리면 **지도에서 두들겨 놓은 무리가 필드에서
        // 딴사람이 되어**, 지도 위에서 싸운 것이 아무 뜻도 못 갖는다.
        //
        // **그래도 두목이긴 하다** — 세우는 것과 세게 만드는 것은 다른 일이고, 이쪽은 이미
        // 그 무리에서 가장 센 사람이다. (굴려서 태어난 폰은 `Spec`이 `null`이라는 것이
        // 이 저장소에 이미 적혀 있다 — `PawnSpec.PriceOf`가 그 위에 서 있다.)
        if (best.Spec == null)
        {
            // **수준을 올려 다시 굴린다.** 스탯만 손으로 밀어 올리면 **특성도 배경도 그대로**라
            // 사용자가 바란 *"출신 배경이나 특성이 더 강한 놈"*이 안 된다 — `PawnSpec.Roll`이
            // 그 셋을 한 번에 낸다(수준을 먼저 뽑고 스탯을 그 언저리에서 굴린다).
            //
            // **시드는 신원에서 낸다** — `Random`으로 뽑으면 같은 판을 다시 봐도 다른 사람이
            // 되고, `GetHashCode`는 실행마다 달라진다(`PawnSpecData`가 먼저 겪은 함정).
            var data = best.Data;
            int seed = Mathf.Abs(StableHash(best.name) ^ salt);

            // **세력의 배경 묶음을 넘긴다**(2026-09-08에 빠뜨린 것을 잡았다). 안 넘기면
            // `civilian` 폴백으로 떨어져 **고블린 두목에게서 사람 이력이 나온다** — 세력마다
            // 이력을 갈라 둔 뜻이 지워지는 자리다.
            best.ApplySpec(PawnSpec.Roll(data, seed,
                                         Bump(data.DefaultMinGrade), Bump(data.DefaultMaxGrade),
                                         null, storyGroup, best.Gender));   // 몸은 이미 섰다 — 그 성별로
        }

        // **이력은 맨 끝에 준다 — 여기가 유일한 문이다.**
        //
        // 굴림에서는 이끄는 이력이 아예 안 나오므로(`BackstoryPool.Fits`) 두목이 그것을
        // 받는 길은 이 한 줄뿐이다. **`ApplySpec` 뒤여야 한다** — 다시 굴리는 일에는 이력
        // 굴림도 딸려 와서, 앞에서 주면 여기서 날아간다(갓 세운 두목이 `짐꾼`을 달고 선다).
        //
        // **명세가 있는 사람에게도 준다.** 안 그러면 지도에서 걸어온 무리는 영영 두목 이력이
        // 없어 **머리 위 표식과 스탯 창이 서로 다른 말을 한다** — *다시 안 굴린다*가 지키려는
        // 것은 **그 사람의 신원**(이름·등급·특성)이지 이력 딱지가 아니다.
        //
        // **묶음에 이끄는 이력이 없으면 굴린 것을 그대로 둔다**(`LeaderOf`가 `null`) —
        // `civilian`으로 안 떨어뜨린다: 그러면 **고블린 두목이 사람 두목 이력**을 단다.
        best.SetAdulthood(BackstoryPool.LeaderOf(storyGroup));
        // 대장의 얼굴 문신(부족마다 — `UnitData.LeaderTattoo`)은 이력이 정한다 — 이력을 받았으니 다시 칠한다.
        if (best.TryGetComponent(out MarkingLook marking)) marking.Apply();
        return best;
    }

    // 세 칸(=15점) 올린다. **한두 칸으로는 화면에서 티가 안 나고**, 그 이상이면 초반 습격에
    // 혼자 못 잡는 놈이 선다.
    static StatGrade Bump(StatGrade g) =>
        (StatGrade)Mathf.Clamp((int)g + 3, 0, (int)StatGrade.SSSPlus);

    // `string.GetHashCode`는 **실행마다 달라진다** — 시드로 쓰면 같은 판이 다시 안 나온다.
    static int StableHash(string s)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return h;
        }
    }

}
