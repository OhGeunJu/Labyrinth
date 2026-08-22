using UnityEngine;

// **어두우면 덜 보인다 — 그리고 그것이 밤을 위험하게 만든다.**
//
// 조명만 바꾸면 밤은 *보기에만* 밤이다. 폰은 여전히 10m 밖의 적을 정확히 알아채고, 플레이어가
// 잃는 것은 화면이 어둡다는 불편뿐이라 밤을 피할 이유가 안 생긴다. **감지 범위가 줄어야**
// 밤에 적이 코앞까지 오고, 순찰이 뜻을 갖고, 낮에 싸울지 밤을 넘길지가 고르는 일이 된다.
//
// ## 답은 한 벌이어야 한다
//
// "지금 얼마나 보이나"를 묻는 곳이 둘이면(교전 탐지 · 도주 탐지) 반드시 어긋난다 —
// 도망은 밤에도 멀쩡히 치고 싸움만 늦게 붙는 꼴이 된다. 그래서 배율도, 밤눈을 섞는 식도
// 여기 한 벌뿐이다.
//
// ## 시계는 `GameTimeManager`가 든다
//
// 여기는 때(`TimePhase`)만 읽는다. **조명과 같은 축**이라 화면이 저물면 시야도 같이 줄어든다 —
// 어두워 보이는데 잘 보이거나, 밝은데 안 보이면 어느 쪽도 설명할 수 없다.
public static class Vision
{
    // 때마다의 배율. 저녁은 아직 남은 빛이 있고, 새벽이 밤보다 조금 나은 것은 동트기 전
    // 어스름이다. **낮과 아침은 같다** — 아침에 덜 보일 이유가 없다.
    public const float DayFactor     = 1f;
    public const float EveningFactor = 0.7f;
    public const float DawnFactor    = 0.5f;
    public const float NightFactor   = 0.4f;

    // 지금 이 순간의 배율.
    public static float Factor => GameTimeManager.Phase switch
    {
        TimePhase.Evening => EveningFactor,
        TimePhase.Dawn    => DawnFactor,
        TimePhase.Night   => NightFactor,
        _                 => DayFactor,
    };

    // **이 자리에서의 배율 — 불빛이 닿으면 그만큼 걷힌다**(`LightSource`).
    //
    // 배율이 자리마다 다른 것은 밤에 **플레이어가 손댈 데가 있어야** 하기 때문이다. 때만
    // 보면 밤은 견디는 시간이고, 화톳불 하나로 그 둘레가 낮이 되면 **거점을 어떻게 밝힐지가
    // 결정이 된다**(어디에 세울까 · 목재를 얼마나 댈까 · 어느 불을 죽일까).
    //
    // **낮에는 불을 아예 안 훑는다.** 탐지는 폰마다 0.1초에 한 번 도는 자리고 하루의 3분의 2는
    // 밝다 — 이 한 줄이 그 시간 동안의 레지스트리 순회를 통째로 없앤다.
    public static float FactorAt(Vector3 at)
    {
        float f = Factor;
        if (f >= 1f) return f;
        return Mathf.Lerp(f, 1f, LightSource.BrightestAt(at));
    }

    // **밤눈이 그 배율을 되돌린다**(`UnitData.NightVision`, 0~1 — 1이면 밤에도 낮처럼 본다).
    //
    // 밤을 모두에게 똑같이 걸면 그건 세상이 흐려진 것일 뿐이다. 언데드·야수가 밤에도 보면
    // **밤 습격이 실제로 저쪽에 유리한 판**이 되고, 그러면 밤은 "불편한 시간"이 아니라
    // "저들의 시간"이 된다 — 여기서 종족이 뜻을 하나 더 갖는다.
    //
    // **불빛과는 저절로 맞물린다** — 밤눈이 이미 밝게 보는 놈에게 화톳불은 별로 보태 주지
    // 않는다(둘 다 같은 배율을 1로 미는 일이라 겹치는 만큼은 겹치지 않는다).
    //
    // **`at`은 선택 인자가 아니다.** 기본값을 두면 부르는 쪽이 조용히 빠뜨리고, 그러면 그
    // 경로에서만 불빛이 없는 것이 된다 — `Storage.FindStorageWithItem`이 `from`을 안 받아
    // 폰이 맵을 가로지르던 것과 같은 자리다.
    public static float RangeFor(float baseRange, float nightVision, Vector3 at) =>
        baseRange * Mathf.Lerp(FactorAt(at), 1f, Mathf.Clamp01(nightVision));

    public static float RangeFor(UnitBlackboard bb) =>
        bb?.Self == null ? 0f : RangeFor(bb.DetectRange, bb.NightVision, bb.Self.transform.position);
}
