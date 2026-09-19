using System.Collections.Generic;
using UnityEngine;

// **성별은 몸이 답한다 — 그리고 몸은 태어날 때 한 번 정한다**(2026-09-20, 사용자: *"성별도 만들고 거기에 따라서 맞는
// 체형으로 나오게"* · *"이미 생성된 폰은 바꿀 필요가 없잖아 … 몸도 바꾸나?"*).
//
// 체형마다 프리팹이 따로다(`Chibi_Male` · `Chibi_Female` …) — 어깨 폭과 손 자리가 체형마다 달라 **관절과 아바타가
// 프리팹마다 구워지기** 때문이다. 한 프리팹에 몸 여섯을 넣으면 관절은 한 벌이라 뚱뚱한 몸을 골라도 손이 보통 몸
// 자리에서 흔들린다. 그래서 고르는 것은 **어느 프리팹을 세우나**이고, 그 답은 `PawnSpawn` 하나다.
//
// - **몸이 있으면 몸이 이긴다** — 명세(`PawnSpec.Gender`)는 *아직 몸이 없을 때*(마을 후보처럼 보고 나서 데려오는
//   사람) 몸을 고르는 데 쓰인다. 몸이 선 뒤에 둘이 어긋나면 스폰하는 자리가 `PawnSpawn`을 안 거친 것이다.
// - **몸이 없는 폰(늑대·골렘·블록 인형)은 성별이 없다**(`null`) — 없는 것을 굴려 적지 않는다.
public enum Gender { Male, Female }   // 값은 꼬리에 붙일 것(정수 직렬화)

public static class GenderExt
{
    public static string DisplayName(this Gender g) => g == Gender.Female ? "여성" : "남성";
}

[DisallowMultipleComponent]
public class PawnBody : MonoBehaviour
{
    [Tooltip("이 몸의 성별 — 머리형 여섯도 이 성별의 것만 구워져 있다(`ChibiPawnCreator`).")]
    public Gender Gender;

    [Tooltip("체형 식별자(`Male`·`ThinFemale`…) — 저장·비교용.")]
    public string Shape;

    [Tooltip("화면에 띄울 체형 이름(`마른 여성`…).")]
    public string ShapeLabel;
}

// **폰을 세우는 문 — 하나다**(이벤트 · 세계 콘텐츠 · 무리 실체화 · 모집 · 소환 · 시체 일으키기).
// 하는 일은 *어느 프리팹을 세우나*를 고르는 것 하나뿐이고, 자리 잡기(`PawnPlacement`)는 부르는 쪽이 하던 대로 한다.
public static class PawnSpawn
{
    // 몸을 고를 때 쓸 씨앗. 명세가 있으면 그 씨앗, 없으면 이번에 굴린다(이벤트로 오는 사람).
    public static int SeedOf(PawnSpec spec) => spec != null ? spec.Seed : Random.Range(int.MinValue, int.MaxValue);

    // 이 프리팹 대신 세울 프리팹. 몸 목록이 없으면 그대로다.
    public static GameObject Resolve(GameObject prefab, PawnSpec spec)
    {
        if (prefab == null) return null;
        var variants = prefab.GetComponent<BodyVariants>();
        if (variants == null || variants.Variants.Count == 0) return prefab;

        int    seed   = SeedOf(spec);
        Gender gender = spec != null && spec.Gender.HasValue ? spec.Gender.Value : PawnSpec.GenderFor(seed);
        var    body   = variants.Pick(seed, gender);
        return body != null ? body.gameObject : prefab;
    }

    public static GameObject Instantiate(GameObject prefab, Vector3 pos, Quaternion rot,
                                         Transform parent = null, PawnSpec spec = null)
        => Object.Instantiate(Resolve(prefab, spec), pos, rot, parent);

    public static GameObject Instantiate(GameObject prefab, PawnSpec spec = null)
        => Object.Instantiate(Resolve(prefab, spec));
}
