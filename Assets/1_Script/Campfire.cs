using UnityEngine;

// **화톳불 — 저녁에 켜지고 아침에 꺼진다.**
//
// 하는 일은 `LightSource`를 켜고 끄는 것 하나고, 여기가 답하는 것은 **언제 켤까**뿐이다.
//
// ## 켜는 때는 `Vision`이 정한다 — `IsDay`가 아니라
//
// 기준이 `GameTimeManager.IsDay`가 아니라 **`Vision.Factor < 1`**인 것은, 불을 켜는 이유가 곧
// *덜 보이는 것*이기 때문이다. 저녁(0.7)은 `IsDay`가 참이지만 이미 시야가 줄어 있어서, 낮/밤
// 두 칸으로 가르면 **화면이 물들고 폰이 덜 보게 된 뒤에도 불이 안 붙는다.** 눈금이 두 벌이
// 되는 자리이기도 하다 — 시야 배율을 손보면 켜지는 때가 저절로 따라온다.
//
// ## 값은 짓는 데서 한 번만 치른다
//
// 한때 목재를 태웠다(뭉치로 내고 `MaterialPool`이 창고에서 바로 빼는, 화장터와 같은 길).
// **걷어냈다** — 눈금이 문제가 아니라 **그것이 만드는 결정이 없어서**다: 목재는 벌목 몇 번이면
// 남아돌아 "댈까 말까"가 성립하지 않았고, 남는 것은 창고가 빈 드문 날에 불이 조용히 꺼지는
// 것뿐이었다. 게다가 폰이 안 나르므로 **아무도 없는 거점에서 창고가 저 혼자 줄었다.**
//
// 그래서 값은 **어디에 몇 개 세우나**(돌 10짜리 청사진)로만 치른다. 불빛은 반경이 있고 겹쳐도
// 안 밝아지므로, 거점을 밝히는 일은 여전히 배치의 문제로 남는다.
public class Campfire : MonoBehaviour, IWorldClickable
{
    [Tooltip("켜고 끌 불빛. 비우면 자식에서 찾는다.")]
    [SerializeField] LightSource lightSource;

    bool _wanted = true;    // 사람이 켜 두었나(꺼 두면 어두워져도 안 붙는다)

    public bool WantsFire => _wanted;
    public bool IsLit     => lightSource != null && lightSource.Lit;

    void Awake()
    {
        if (lightSource == null) lightSource = GetComponentInChildren<LightSource>(includeInactive: true);
    }

    // **시계에 매달린다 — 매 프레임 묻지 않는다**(`DayNightLighting`과 같은 자리). 답이
    // 바뀌는 것은 하루에 두 번뿐이라, 그것을 `Update`에서 재는 것은 화톳불 수만큼의 헛일이다.
    void OnEnable()
    {
        GameTimeManager.OnTimeChanged += OnTime;
        Apply();
    }

    // 휴면·철거로 이 컴포넌트가 잠들면 불도 꺼진다. `LightSource`가 제 `OnDisable`에서
    // 스스로 끄지만, 그것은 **등이 같이 꺼질 때만** 그렇다 — 여기만 재워지는 길도 있다.
    void OnDisable()
    {
        GameTimeManager.OnTimeChanged -= OnTime;
        lightSource?.SetLit(false);
    }

    void OnTime(float _) => Apply();

    // **끄는 줄을 남겨 둔 이유**: 불빛은 세력을 안 가린다(`LightSource`) — 우리 화톳불 옆에
    // 선 산적도 그만큼 잘 본다. 그래서 "이 자리는 어둡게 둔다"가 값을 갖는다. 태우는 것이
    // 없어졌어도 그 축은 그대로다.
    public void SetWanted(bool on)
    {
        _wanted = on;
        Apply();
    }

    // **지하에서는 시계를 안 본다**(2026-08-29, 기획이 바뀌었다).
    //
    // 지상에서는 *"어두워지면 켠다"*가 맞았다 — 낮에는 불이 필요 없으니까. 지하는 **늘
    // 어둡다.** 시계에 매달아 두면 아침에 저절로 꺼져서, 플레이어가 켜 둔 불이 **영문
    // 모르게 사라진다.**
    //
    // 그래서 지하에서는 **사람이 켜고 끈 그대로**다. 지상 씬에서는 옛 규칙이 그대로 산다 —
    // 축을 지우는 것이 아니라 **지하에서만 시계를 뺀다.**
    void Apply() => lightSource?.SetLit(_wanted && (RockMap.Enabled || Vision.Factor < 1f));

    // **좌클릭에 열 창이 없다**(침대와 같은 자리). 상태는 `InspectPanel`이 적고, 조작은
    // 켜고 끄는 것 하나뿐이라 우클릭 줄로 족하다 — 그것 하나를 위해 창을 지으면 `BedUI`를
    // 없앤 그 자리로 되돌아간다.
    public void OnWorldClick() { }
}
