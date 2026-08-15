using UnityEngine;

// 시체가 얼마나 삭았나. **단계가 곧 할 수 있는 일**이다(`Butchering`).
public enum CorpseStage
{
    Fresh,    // 신선 — 고기도 가죽도 나온다
    Rotten,   // 썩음 — 고기는 못 쓴다
    Bones,    // 뼈   — 뼈만 남았다. 저절로 사라지지 않는다
}

// 시체. **살아 있을 때의 것 중 남겨야 하는 것만 든다.**
//
// `UnitBrain.BecomeCorpse`가 `Faction` 컴포넌트를 **지운다** — `DetectNode` 같은 자동 탐지가
// `OverlapSphere`로 `Faction`을 훑기 때문에, 컴포넌트가 남아 있으면 시체가 계속 적으로 잡힌다
// (`enabled = false`로는 못 막는다 — `GetComponent`는 꺼진 것도 돌려준다).
//
// 그래서 **소속은 여기서 기억한다.** 지우기만 하면 "누구의 시체인가"가 통째로 사라져,
// 화면이 죽은 자를 소속 없는 무엇으로 취급하게 된다(허기 바가 뜬 것이 그 증상이었다).
// 지우는 것은 자동 판정을 막기 위한 것이지 그 사실을 잊자는 뜻이 아니다.
public class Corpse : MonoBehaviour
{
    public Inventory   Inventory { get; private set; }
    public string      UnitName  { get; private set; }
    public FactionData Faction   { get; private set; }

    public void Init(string unitName, Inventory inventory, FactionData faction)
    {
        UnitName  = unitName;
        Inventory = inventory;
        Faction   = faction;
    }

    // 이 시체가 플레이어 세력이었나. 죽은 폰을 살아 있는 폰과 같은 잣대로 물어야 하는 곳
    // (허기·라벨)이 `Faction.IsPlayer` 대신 쓴다.
    public bool WasPlayer => Faction != null && Faction.PlayerControlled;

    // ── 부패 ──────────────────────────────────────────────────────────────────
    //
    // **시체는 시간이 지나며 세 단계를 밟는다.** 단계마다 할 수 있는 일이 달라서, 그것이
    // "언제까지 치워야 하나"를 만든다 — 신선할 때 도축하면 고기가 나오고, 놓치면 가죽만,
    // 더 놓치면 뼈만 남는다.
    //
    // **뼈는 저절로 안 사라진다.** 그게 매장·화장이 있어야 할 이유이고, 나중에 붙일
    // *"교단이 남은 시체를 일으킨다"*의 재료다(신선하면 좀비, 뼈면 해골).
    //
    // 시각은 `GameTimeManager.Elapsed`로 잰다 — 배속·일시정지가 그대로 반영되고,
    // `Time.time`을 쓰면 멈춰 둔 사이에도 썩는 축이 하나 더 생긴다.
    public float DiedAt { get; private set; }

    // 하루 길이를 기준으로 잡는다(`GameTimeManager.DayDuration`). **습격 주기(1~3일)와 같은
    // 눈금**이라야 "다음 습격 전에 치워라"가 성립한다 — 이틀이면 예고를 보고 움직일 수 있다.
    public const float FreshDays  = 2f;
    public const float RottenDays = 4f;   // 이때부터 뼈

    // 죽은 지 며칠인가. **묻혀 있는 동안은 안 는다**(아래 `SetBuried`).
    public float AgeDays => Buried
        ? _buriedAge
        : (GameTimeManager.Elapsed - DiedAt) / Mathf.Max(1f, GameTimeManager.DayDuration);

    public CorpseStage Stage
    {
        get
        {
            float days = AgeDays;
            return days < FreshDays  ? CorpseStage.Fresh
                 : days < RottenDays ? CorpseStage.Rotten
                 :                     CorpseStage.Bones;
        }
    }

    // ── 매장 ─────────────────────────────────────────────────────────────────
    //
    // **묻히면 시간이 멈춘다.** 땅속에서 계속 삭게 두면 무덤이 "치우는 자리"가 아니라
    // *"보이지 않는 곳에 쌓아 두는 자리"*가 된다 — 파냈을 때 뼈만 나오면 묻어 둔 뜻이 없다.
    //
    // **`DiedAt`을 옮겨 멈춘다.** 파내면 멈춘 나이에서 이어 삭는다 — 되돌리는 길이 있어야
    // 실수로 묻은 것을 무를 수 있고(장비를 안 뒤지고 묻었을 때), 나중에 붙일 *"교단이 무덤을
    // 파낸다"*도 같은 문을 쓴다.
    public bool Buried { get; private set; }
    float _buriedAge;

    public void SetBuried(bool on)
    {
        if (on == Buried) return;

        if (on)
        {
            _buriedAge = AgeDays;
            Buried     = true;
        }
        else
        {
            Buried = false;
            DiedAt = GameTimeManager.Elapsed - _buriedAge * Mathf.Max(1f, GameTimeManager.DayDuration);
        }

        // 땅속에 있는 것은 안 보인다. **몸도 없어야 한다** — 콜라이더를 남기면 흙 위를
        // 클릭했을 때 무덤이 아니라 묻힌 시체가 잡히고, 무엇보다 **`CharacterController`가
        // 보이지 않는 벽으로 남아** 폰이 무덤을 못 지나간다.
        //
        // 그 컨트롤러는 **묻히기 직전에 켜진다** — 업은 것을 내려놓는 길이
        // `PawnPlacement.PlaceAndEnable`이라 늘 켜진 채로 온다. 그래서 "죽을 때 정해진
        // 상태니 안 건드린다"는 말이 여기서는 성립하지 않는다. 나머지 콜라이더와 똑같이 다룬다.
        foreach (var rend in GetComponentsInChildren<Renderer>(true)) rend.enabled = !on;
        foreach (var col in GetComponentsInChildren<Collider>(true))  col.enabled  = !on;
    }

    void Awake() => DiedAt = GameTimeManager.Elapsed;

    // **단계가 바뀌면 색으로 알린다.** 모델을 갈아 끼우려면 에셋이 필요한데, 색만으로도
    // "저건 이제 뼈다"가 읽힌다(우리·죄수 침대를 색으로 가른 것과 같은 자리).
    //
    // `MaterialPropertyBlock`을 쓴다 — `rend.material`로 칠하면 시체마다 머티리얼 인스턴스가
    // 하나씩 생긴다. 철거 예정 표시가 MPB를 쓰지만 시체는 철거 대상이 아니라 겹치지 않는다.
    static readonly Color RottenTint = new(0.45f, 0.42f, 0.30f);
    static readonly Color BonesTint  = new(0.90f, 0.88f, 0.82f);

    CorpseStage _shown = CorpseStage.Fresh;

    void Update()
    {
        var now = Stage;
        if (now == _shown) return;

        _shown = now;
        Tint(now == CorpseStage.Bones ? BonesTint : RottenTint);
    }

    void Tint(Color c)
    {
        var mpb = new MaterialPropertyBlock();
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);   // URP
            mpb.SetColor("_Color", c);       // 빌트인 폴백
            rend.SetPropertyBlock(mpb);
        }
    }
}
