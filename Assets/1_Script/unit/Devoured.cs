using System.Collections.Generic;
using UnityEngine;

// **기둥에 묶여 먹히는 사람**(2026-11-01 — `Abduction` · `CaptivePost`). 켄시처럼 간다(사용자: *"체력이 닳는게 한번에 퍽퍽
// 먹히는게 아니라 적이 먹는 모션중일때 부위의 체력이 계속 줄어드는 … 오른팔이 없어지면 왼팔 그다음 오른다리 왼다리 순서대로
// … 그 부위들이 부셔질때까지"* · *"기절한 상태가 아니면 탈출을 시도해볼 수도 있게"*).
//
// **식사** — 하루 `MealsPerDay`번 끼니가 돌아오면 붙잡은 세력의 먹는 놈 하나가 와서(`DevourNode`) 뜯는 동안(`MealSeconds`)
//   지금 부위의 체력이 초마다 준다(`BodyPartSystem.Gnaw`). 부서지면(−100) 다음 부위: 오른팔 → 왼팔 → 오른다리 → 왼다리 →
//   배(부서지면 죽는다). 부서진 팔다리는 안 낫고 몸에서도 지워진다(`BodyPartSystem` "잃은 팔다리").
// **탈출은 두 걸음**(사용자: *"묶인 상태가 따로 있어서 그걸 풀어내는게 확률인 거고 풀려있으면 탈출한다는 바로 실행되는걸로"*).
//   ① 묶여 있으면 [줄을 푼다](우클릭) — `UntieSeconds` 동안 풀다가 끝에 굴린다(손재주만큼) · 기절하면 멈춘다.
//   ② 풀렸으면 기둥에 선 채 기다린다 — [탈출한다]가 바로 놓아 준다(`Imprisonment.Release` — 우리 폰은 도주 성향을 안 받는다).
//   **들키는 장치는 없다**(사용자: *"근처에 적이 있으면 때릴테니깐 따로 코드를 짤필요는 없어보여 풀린채로 근처에 적이 있어도
//   그냥 지나가면 좋겠어"*) — 묶인 동안은 갇힌 사람이라 표적에서 빠지고, 풀려 나오면 여느 적대처럼 근처의 놈이 덤빈다.
// **끝** — 죽거나 · 갇힘이 풀리거나(업혀 나갔다 — [끌어낸다]) · 기둥에서 멀어지면.
public class Devoured : MonoBehaviour
{
    public const float MealsPerDay  = 4f;
    public const float MealSeconds  = 40f;    // 한 끼에 뜯는 시간(게임 초)
    const float GnawPerSecond = 3f;           // 뜯는 동안 부위 체력이 초마다 주는 양 — 부위 하나(100 → −100)에 끼니 두 번 남짓

    const float UntieSeconds  = 8f;           // 한 번 줄을 푸는 데 드는 시간(게임 초) — 끝에 굴린다
    const float LeftPost      = 6f;           // 기둥에서 이만큼 멀어졌으면 풀려난 것이다(m)

    // 먹히는 순서(사용자) — 다 부서지면 배, 배가 부서지면 죽는다.
    static readonly BodyPartType[] Order =
    {
        BodyPartType.RightArm, BodyPartType.LeftArm, BodyPartType.RightLeg, BodyPartType.LeftLeg, BodyPartType.Abdomen,
    };

    public CaptivePost Post   { get; private set; }
    public FactionData Captor => Post != null ? Post.Captor : null;

    // 지금 끼니를 먹는 놈 · 남은 시간. 비었으면 다음 끼니를 기다린다.
    public UnitBrain Eater    { get; private set; }
    public float     MealLeft { get; private set; }
    float _nextMealAt;

    public bool Loose    { get; private set; }   // 줄이 풀렸다 — [탈출한다]가 바로 된다
    public bool Untying  { get; private set; }   // 지금 줄을 푸는 중
    float _untieLeft;

    UnitBrain      _me;
    BodyPartSystem _body;

    static float MealGap => GameTimeManager.DayDuration / MealsPerDay;

    public void Begin(CaptivePost post)
    {
        Post        = post;
        Eater       = null;
        _nextMealAt = GameTimeManager.Elapsed + MealGap * 0.5f;   // 묶고 나서 조금 뒤 첫 끼
        Loose       = false;
        Untying     = false;
    }

    void Awake()
    {
        TryGetComponent(out _me);
        TryGetComponent(out _body);
    }

    // 지금 묶여 먹히는 사람 — 씬을 훑지 않고 이 목록을 본다(디버그 창).
    static readonly List<Devoured> _all = new();
    public static IReadOnlyList<Devoured> All => _all;
    void OnEnable()  => _all.Add(this);
    void OnDisable() => _all.Remove(this);

    void Update()
    {
        var bb = _me != null ? _me.Blackboard : null;
        if (bb == null || PawnCarry.IsCorpse(_me) || Post == null) { Destroy(this); return; }

        var flat = _me.transform.position - Post.HoldPosition; flat.y = 0f;
        if (!bb.IsPrisoner || bb.CarriedBy != null || flat.sqrMagnitude > LeftPost * LeftPost)
        {
            Debug.Log($"[납치] {_me.UnitName}이(가) 기둥에서 풀려났다");
            Destroy(this);
            return;
        }

        if (Eater != null && !CanEat(Eater)) Eater = null;   // 먹던 놈이 쓰러졌거나 떠났다 — 끼니는 다음 놈이 잇는다
        if (Untying) TickUntie(bb);
    }

    // ── 식사 ────────────────────────────────────────────────────────────────

    public bool MealDue => GameTimeManager.Elapsed >= _nextMealAt;
    public float NextMealIn => Mathf.Max(0f, _nextMealAt - GameTimeManager.Elapsed);
    public void MealNow() => _nextMealAt = GameTimeManager.Elapsed;   // 디버그 창 — 끼니를 당긴다

    // 이 놈이 이 끼니를 먹을 수 있나 — 끼니가 됐고 · 다른 놈이 안 먹고 있다(또는 이 놈이 먹던 중).
    public bool TryClaimMeal(UnitBrain eater)
    {
        if (Eater == eater) return true;
        if (Eater != null || !MealDue || !CanEat(eater) || eater.Data == null) return false;
        if (eater.TryGetComponent(out Faction f) && f.Data != Captor) return false;
        Eater    = eater;
        MealLeft = MealSeconds;
        return true;
    }

    // 뜯는다 — `dt`초만큼. 끼니가 끝나면 다음 끼니를 잡고 false.
    public bool Gnaw(UnitBrain eater, float dt)
    {
        if (Eater != eater || _body == null) return false;

        var part = CurrentPart();
        if (part.HasValue) _body.Gnaw(part.Value, GnawPerSecond * dt);

        MealLeft -= dt;
        if (MealLeft > 0f) return true;

        Eater       = null;
        _nextMealAt = GameTimeManager.Elapsed + MealGap;
        Debug.Log($"[납치] {eater.UnitName}이(가) {_me.UnitName}을(를) 먹고 물러났다 — {PartsLeftText()}");
        return false;
    }

    // 다음에 뜯길 부위와 그 체력(기둥 창).
    public BodyPartType? NextPart => CurrentPart();
    public float PartHP(BodyPartType p) => _body != null ? _body.PartHP(p) : 0f;

    BodyPartType? CurrentPart()
    {
        if (_body == null) return null;
        foreach (var p in Order) if (!_body.IsPartDestroyed(p)) return p;
        return null;
    }

    string PartsLeftText()
    {
        var p = CurrentPart();
        return p.HasValue ? $"다음은 {PawnSheetUI.PartName(p.Value)}({_body.PartHP(p.Value):F0})" : "남은 데가 없다";
    }

    static bool CanEat(UnitBrain b)
    {
        if (b == null || !b.isActiveAndEnabled || PawnCarry.IsCorpse(b) || !Abduction.Devours(b)) return false;
        var bb = b.Blackboard;
        return bb != null && !bb.IsKnockedOut && !bb.IsPrisoner && bb.CarriedBy == null;
    }

    // ── 탈출 ────────────────────────────────────────────────────────────────

    public bool Conscious => _me != null && _me.Blackboard != null && !_me.Blackboard.IsKnockedOut;

    // 줄이 풀릴 확률 — 손재주 0이면 25% · 50이면 50% · 100이면 75%(10~90%로 묶는다).
    public float UntieChance
    {
        get
        {
            float dex = _me != null && _me.Stats != null ? _me.Stats.Dexterity : 50f;
            return Mathf.Clamp(0.25f + dex / 200f, 0.1f, 0.9f);
        }
    }

    public void TryUntie()
    {
        if (!Conscious || Loose || Untying) return;
        Untying    = true;
        _untieLeft = UntieSeconds;
        Debug.Log($"[납치] {_me.UnitName}이(가) 몰래 줄을 풀기 시작한다({UntieChance * 100f:F0}%)");
    }

    // 풀렸으면 바로 나간다.
    public void Escape()
    {
        if (!Loose || !Conscious) return;
        Debug.Log($"[납치] {_me.UnitName}이(가) 기둥에서 빠져나왔다");
        Imprisonment.Release(_me, "기둥에서 줄을 풀고 빠져나왔다");
        Destroy(this);
    }

    void TickUntie(UnitBlackboard bb)
    {
        if (bb.IsKnockedOut) { Untying = false; return; }

        _untieLeft -= Time.deltaTime;
        if (_untieLeft > 0f) return;

        Untying = false;
        Loose   = Random.value < UntieChance;
        Debug.Log(Loose ? $"[납치] {_me.UnitName}의 줄이 풀렸다 — 기둥에 선 채 때를 본다"
                        : $"[납치] {_me.UnitName}이(가) 줄을 못 풀었다");
    }
}
