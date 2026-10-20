using System.Collections.Generic;
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

    // ── 유품 ─────────────────────────────────────────────────────────────────
    //
    // **지녔던 것을 전부 내놓는다 — 배낭 속의 것과 차고 있던 것 둘 다.**
    //
    // **차고 있던 장비는 `Inventory`에 없다.** 격자를 안 먹으므로(시체 창이 장비 열을 따로
    // 그린다) 배낭만 훑으면 **무기와 갑옷이 시체와 함께 조용히 사라진다** — 도축·화장 둘 다
    // 마지막에 몸을 없애기 때문이다. 화장터에는 *"안 뒤지고 올려놓았다고 장비가 사라지면
    // 그건 조용한 손실"*이라고 적혀 있었는데, 정작 코드는 배낭만 비우고 있었다(2026-08-16).
    //
    // **여기 두는 이유**: 부르는 곳이 둘이고(`ButcherJob`·`Pyre`) 앞으로도 늘 텐데, 각자
    // 적으면 하나가 반드시 장비를 빠뜨린다 — 실제로 그렇게 어긋나 있었다. "시체가 지녔던
    // 것"에 답하는 것은 시체다. **무덤은 안 부른다** — 그쪽은 같이 묻고 파낼 수 있어서
    // 잃는 것이 아니다(그것이 무덤과 화장을 가르는 축이다).
    public void SpillBelongings(Vector3 at)
    {
        at += Vector3.up * 0.15f;

        // 차고 있던 것 — 벗겨서 떨군다(`TakeOff*`는 배낭을 안 거친다. 어차피 비울 배낭이라
        // 거쳐 봐야 자리가 없으면 그쪽에서 또 새는 길이 생긴다).
        var eq = GetComponent<Equipment>();
        if (eq != null)
        {
            Drop(eq.TakeOffMainHand(), at);
            Drop(eq.TakeOffOffHand(),  at);
            foreach (ArmorSlot slot in System.Enum.GetValues(typeof(ArmorSlot)))
                Drop(eq.TakeOff(slot), at);
        }

        // 배낭에 든 것.
        if (Inventory == null) return;
        for (int i = Inventory.Items.Count - 1; i >= 0; i--) Drop(Inventory.Items[i], at);
        Inventory.Clear();
    }

    // 등급을 들고 떨군다 — `ItemData`만 넘기면 희귀 철검이 떨어지는 것만으로 일반이 된다.
    static void Drop(ItemInstance inst, Vector3 at)
    {
        if (inst?.Data == null) return;
        DroppedItem.Spawn(inst.Data, Mathf.Max(1, inst.Quantity), at, inst.Quality, inst.Left);   // 반쯤 탄 횃불은 반쯤 탄 채로
    }

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
    // 눈금**이라야 "다음 습격 전에 치워라"가 성립한다.
    //
    // **하루 만에 썩고 사흘이면 뼈다.** 전엔 2일·4일이었는데, 그러면 신선한 몸이 실기 한
    // 시간이라 **싸움이 끝난 뒤 서두를 이유가 없었다** — 도축은 언제 가도 고기가 나왔다.
    // 하루로 좁히면 그날 안에 치우는 것이 일이 되고, 습격 주기(1~3일)와 겹쳐 *다음 습격
    // 전에 뼈가 된다*가 실제로 벌어진다(뼈는 안 삭으므로 그때부터는 쌓이는 쪽이다).
    public const float FreshDays  = 1f;
    public const float RottenDays = 3f;   // 이때부터 뼈

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

    // **나이를 손으로 옮긴다.** `SetBuried`가 `DiedAt`을 밀어 시간을 멈추는 것과 **같은 문**이다
    // — 나이를 바꾸는 길이 둘이면 한쪽이 묻힌 시체를 건드려 땅속에서 삭기 시작한다.
    // 지금 부르는 곳은 시험용 메뉴 하나뿐이다(`DebugNecromancy`): 뼈가 되기까지 게임 내
    // 나흘(1배속 실기 두 시간)이라 *뼈에서 해골이 선다*를 눈으로 볼 방법이 달리 없다.
    public void SetAge(float days)
    {
        days = Mathf.Max(0f, days);

        if (Buried) _buriedAge = days;
        else        DiedAt = GameTimeManager.Elapsed - days * Mathf.Max(1f, GameTimeManager.DayDuration);
    }

    // ── 살아 있는 시체 목록 ──────────────────────────────────────────────────
    //
    // **씬을 훑지 않는다.** 이 저장소가 이미 같은 것을 네 번 두었다(`Storage.All` ·
    // `Bed._all` · `Faction.All` · `WaterSource.All`) — 시체만 빠져 있어서 도축대가
    // **1초마다 `FindObjectsByType<Corpse>`로 씬 전체를 훑었다.**
    //
    // 하필 **치울 시체가 없을 때 가장 자주 돈다**: 일감이 걸려 있으면 `SiteJobSlot`이 아예
    // 안 묻는데(지연 평가), 걸 것이 없으면 매초 전 씬을 훑는다 — 그게 평상시의 대부분이다.
    // 도축대를 여럿 지으면 그만큼 곱해진다(*"규칙이 더 필요하면 도축대를 더 짓는다"*가
    // 설계라 실제로 늘어난다).
    //
    // **이벤트가 아니라 목록이다** — 알림을 돌리는 것이 아니라, 타입이 제 인스턴스를 들고
    // 있을 뿐이다. 그래서 구독을 놓쳐 새는 자리가 없다.
    //
    // `BecomeCorpse`가 런타임에 붙이는 컴포넌트라 `OnEnable`이 그 순간 돈다.
    public static readonly List<Corpse> All = new();

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

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
        CheckVanish();

        var now = Stage;
        if (now == _shown) return;

        _shown = now;
        Tint(now == CorpseStage.Bones ? BonesTint : RottenTint);
    }

    // ── 핵 영역 밖의 시체는 하루면 없어진다 (2026-09-29) ─────────────────────
    //
    // 사용자: *"핵 영역 밖의 시체는 하루만 지나도 사라지는게 맞을 거 같기는 해"*. 저절로 나는 무리(`WildSpawner`)를
    // 굴 여기저기서 잡고 두고 가면 **시체가 판 내내 쌓였다** — 뼈는 안 사라지는 것이 규칙이라서다(위 `Bones`).
    // 그 규칙은 **기지 안의 것**이다(매장·화장이 있어야 할 이유). 밖의 것은 짐승이 뜯어 가고 굴이 삼킨다.
    //
    // 하루면 **신선한 동안**이다 — 그 안에 오면 도축도 털기도 된다. 지녔던 것도 같이 없어진다(하루를 준 것이 그 값이다).
    //
    // **빠지는 것**: 묻힌 시체 · 업혀 가는 시체 · **내 사람**(원정에서 쓰러진 동료의 장비가 하루 만에 사라지면 되찾을 길이 없다).
    public const float VanishOutsideDays = 1f;
    float _vanishCheckAt;

    void CheckVanish()
    {
        if (Time.unscaledTime < _vanishCheckAt) return;
        _vanishCheckAt = Time.unscaledTime + 5f;   // 실시간 — 하루짜리 시계라 촘촘할 필요가 없다

        if (Buried || AgeDays < VanishOutsideDays) return;
        if (Faction != null && Faction.PlayerControlled) return;

        var brain = GetComponent<UnitBrain>();
        if (brain != null && PawnCarry.IsCarried(brain)) return;

        var at = transform.position;
        foreach (var core in DungeonCore.All)
            if (core != null && core.IsWithinAnyRangeWorld(at)) return;

        Destroy(gameObject);
    }

    void Tint(Color c) => Tint(gameObject, c);

    // **몸을 물들이는 일 한 곳.** 죽은 자를 일으키는 쪽(`Necromancy`)도 같은 일을 한다 —
    // 일어난 몸은 모양이 그대로라, 물들이지 않으면 **내 폰과 눈으로 구별되지 않는다.**
    public static void Tint(GameObject body, Color c)
    {
        if (body == null) return;

        var mpb = new MaterialPropertyBlock();
        foreach (var rend in body.GetComponentsInChildren<Renderer>(true))
        {
            rend.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);   // URP
            mpb.SetColor("_Color", c);       // 빌트인 폴백
            rend.SetPropertyBlock(mpb);
        }
    }
}
