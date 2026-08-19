using System.Collections.Generic;
using UnityEngine;

// **화장터 — 시체를 태워 없앤다.** 되돌릴 수 없다.
//
// **무덤과 나란한 물건이다**(`Grave`). 갈리는 것은 값과 끝맺음이다:
//
//   무덤   — 싸고 즉시. 대신 그 칸을 영영 먹고 **몸이 세상에 남는다**(파낼 수 있다).
//   화장터 — 목재를 먹고 시간이 걸린다. 대신 **끝난다.**
//
// 붙일 예정인 *"교단·망자 습격이 남은 시체를 일으킨다"*가 이 둘의 값을 마저 매긴다 — 그때
// 무덤은 미루는 선택이 되고 화장은 끝내는 선택이 된다. **지금 붙여 두는 이유**는 그 기능이
// 왔을 때 치우는 길이 이미 서 있어야 해서다(치울 길이 없는 채로 시체가 일어나면 그건
// 선택이 아니라 사고다).
//
// **폰이 붙어 있을 필요가 없다.** 시체를 올려놓는 것 자체가 이미 사람이 업고 와서 하는
// 일이고(`ICarrySite`), 불을 지키고 서 있는 것은 아무것도 안 정한다 — 던전 핵이 우리에
// 가둔 하수인을 저 혼자 지배해 가는 것(`Conversion.TickCore`)과 같은 자리다. 그래서 새
// 일감도 새 작업 카테고리도 안 만든다.
public class Pyre : MonoBehaviour, IWorldClickable, ICarrySite
{
    public static readonly List<Pyre> All = new();
    public static event System.Action<Pyre> OnSelected;

    [Tooltip("시체가 놓일 자리. 비우면 화장터 한가운데.")]
    [SerializeField] Transform pyreAnchor;

    [Tooltip("한 번 태우는 데 드는 것. 비우면 공짜다.")]
    [SerializeField] ItemData fuelItem;
    [SerializeField] int      fuelAmount = 5;

    [Tooltip("다 타는 데 걸리는 시간(초). 게임 시간이라 배속을 탄다.")]
    [SerializeField] float burnSeconds = 60f;

    Corpse _burning;
    bool   _paid;        // 이번 몸에 대한 연료를 이미 냈나
    float  _progress;
    float  _fuelCheck;   // 연료가 모자랄 때 다시 물어볼 때까지

    public Corpse Burning    => _burning;
    public bool   IsOccupied => _burning != null;
    public float  Progress   => burnSeconds <= 0f ? 1f : Mathf.Clamp01(_progress / burnSeconds);

    // **왜 안 타는지 화면에 적는다** — 연료가 없으면 아무 일도 안 일어나는데, 그 이유가
    // 어디에도 없으면 고장으로 보인다(`DungeonCore.CoreBlockedReason`과 같은 자리).
    public string BlockedReason { get; private set; }

    public Vector3 PyrePosition => pyreAnchor != null ? pyreAnchor.position : transform.position;

    void OnEnable()  => All.Add(this);
    void OnDisable() => All.Remove(this);

    // **화장터가 철거되면 타던 시체가 도로 나온다.** 안 꺼내면 건물과 함께 조용히 사라진다 —
    // 값을 안 치른 소멸이라 그건 치운 것이 아니라 잃은 것이다.
    //
    // 무덤과 같은 이유로 **`OnDestroy`에 둔다** — 정착지를 옮겨 루트가 꺼질 때 타던 것이
    // 튀어나오면, 돌아왔을 때 화장은 없던 일이 되고 시체만 바닥에 남는다.
    void OnDestroy() => Eject();

    // ── `ICarrySite` ─────────────────────────────────────────────────────────
    public bool Accepts(UnitBrain brain)
        => brain != null && !IsOccupied && PawnCarry.IsCorpse(brain);

    Vector3     ICarrySite.PutPosition => PyrePosition;
    Quaternion? ICarrySite.PutRotation => null;
    bool        ICarrySite.IsFull      => IsOccupied;
    string      ICarrySite.SiteName    => "화장터";

    void ICarrySite.Put(UnitBrain brain)
    {
        if (!Accepts(brain)) return;

        var corpse = brain.GetComponent<Corpse>();
        if (corpse == null) return;

        // **유품은 안 태운다.** 태우는 것은 몸이지 물건이 아니고, 무엇보다 안 뒤지고
        // 올려놓았다고 장비가 사라지면 그건 조용한 손실이다(`ButcherJob`과 같은 규칙).
        // 발밑에 떨구면 이미 있는 운반 축(H)에 올라탄다.
        //
        // **차고 있던 장비까지 나온다** — `Corpse`가 답한다. 한때 여기서 배낭만 훑어,
        // 바로 위 주석이 막겠다고 적어 둔 그 손실이 실제로 나고 있었다(2026-08-16).
        corpse.SpillBelongings(corpse.transform.position);

        _burning   = corpse;
        _paid      = false;
        _progress  = 0f;
        _fuelCheck = 0f;   // 올려놓은 그 프레임에 바로 불을 붙여 본다

        corpse.transform.SetParent(transform, worldPositionStays: true);
        corpse.transform.position = PyrePosition;
    }

    void Update()
    {
        if (_burning == null) { BlockedReason = null; return; }

        // **남이 가져갔으면 손을 뗀다.** 시체를 업는 길은 여럿이고(우클릭 [업는다], 도축대
        // 일감) 그 어느 것도 화장터에 물어보지 않는다 — 붙들고 있으면 **어디로 실려 갔든
        // 타이머가 끝나는 순간 태워 없앤다.** 자식인지로 묻는 것이 그 단일 판정이다.
        if (_burning.transform.parent != transform)
        {
            _burning      = null;
            _progress     = 0f;
            _paid         = false;
            BlockedReason = null;
            return;
        }

        // **연료는 불을 붙일 때 한 번 낸다.** 매 틱 조금씩 빼면 도중에 떨어졌을 때
        // "반쯤 탄 시체"라는 상태가 생기고, 그걸 표현할 데가 없다.
        //
        // 모자랄 때 **매 프레임 다시 묻지 않는다** — `MaterialPool`은 창고를 전부 훑는다.
        // 자재가 들어오는 것은 초 단위 사건이라 1초에 한 번이면 넉넉하다(청사진이 배낭 변화를
        // 2초 주기로 받는 것과 같은 판단).
        if (!_paid)
        {
            _fuelCheck -= Time.deltaTime;
            if (_fuelCheck > 0f) return;
            _fuelCheck = 1f;

            if (fuelItem != null && fuelAmount > 0)
            {
                if (!MaterialPool.Consume(fuelItem, fuelAmount, null))
                {
                    BlockedReason = $"{fuelItem.ItemName}이(가) 모자란다 " +
                                    $"({MaterialPool.AvailableAnywhere(fuelItem)}/{fuelAmount})";
                    return;
                }
            }
            _paid         = true;
            BlockedReason = null;
        }

        // **`Time.deltaTime`을 그대로 쓴다 — 배수를 또 곱하지 않는다.** 배속은
        // `GameTimeManager`가 `Time.timeScale`로 이미 걸어 두므로(그게 시간의 주인이다),
        // `Speed`를 한 번 더 곱하면 3배속에서 아홉 배로 탄다.
        _progress += Time.deltaTime;
        if (_progress < burnSeconds) return;

        string who = _burning.UnitName;
        Destroy(_burning.gameObject);
        _burning  = null;
        _progress = 0f;
        _paid     = false;

        EventNotificationUI.Notify("화장", $"{who}을(를) 태웠습니다.");
    }

    // 다 타기 전에 무르는 길. 낸 연료는 안 돌려준다 — 불은 이미 붙었다.
    public void Eject()
    {
        var corpse = _burning;
        _burning  = null;
        _progress = 0f;
        _paid     = false;
        if (corpse == null) return;

        corpse.transform.SetParent(null, worldPositionStays: true);
        PawnPlacement.PlaceOnGround(corpse.gameObject, PyrePosition);
    }

    public void OnWorldClick() => OnSelected?.Invoke(this);

    public static Pyre FindNearestFree(Vector3 from)
    {
        Pyre best = null;
        float bestSq = float.MaxValue;
        foreach (var p in All)
        {
            if (p == null || p.IsOccupied) continue;
            float sq = (p.transform.position - from).sqrMagnitude;
            if (sq >= bestSq) continue;
            best   = p;
            bestSq = sq;
        }
        return best;
    }
}
